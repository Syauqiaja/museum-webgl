using System;
using System.Collections.Generic;
using Museum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Lobby
{
    /// <summary>
    /// Drives the lobby screen: which panel is showing, and turning service events into rendered
    /// slots. It never touches Colyseus and never mutates a slot — every refresh re-renders a
    /// whole <see cref="LobbyRoomSnapshot"/> handed to it by <see cref="ILobbyService"/>, which
    /// is what lets the fake service be swapped for the real one without editing this file.
    ///
    /// The name panel is a fallback, not the main way in: the nickname is normally typed on
    /// MainMenu. It exists because a museum launch point can drop a visitor straight here with
    /// no name set, and a create/join screen with an empty name is a dead end.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [Header("Service")]
        [Tooltip("Use the in-memory fake instead of the server. Editor convenience only — two browsers can never meet on it.")]
        [SerializeField] private bool useFakeService = false;

        [Header("Fallback request")]
        [Tooltip("Used when the scene is opened directly in the Editor with no LobbyRequest set.")]
        [SerializeField] private string fallbackRoomName = "egrang";
        [SerializeField] private int fallbackMaxPlayers = 3;
        [SerializeField] private string fallbackGameScene = SceneReference.Egrang;
        [SerializeField] private string fallbackDisplayName = "Egrang";

        [Header("Panels")]
        [SerializeField] private GameObject namePanel;
        [SerializeField] private GameObject entryPanel;
        [SerializeField] private GameObject roomPanel;

        [Header("Name panel")]
        [SerializeField] private TMP_InputField nameInput;

        [Header("Entry panel")]
        [Tooltip("Dialog title: \"<game> — <n> Pemain\". Filled from the LobbyRequest, so the one " +
                 "generic scene can front any game.")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_InputField codeInput;

        [Header("Room panel")]
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private LobbySlotView slotPrefab;
        [SerializeField] private Button startButton;
        [Tooltip("Why Mulai is greyed out: \"Minimal 2 pemain untuk mulai\" until enough players are seated.")]
        [SerializeField] private TMP_Text startHintText;

        [Header("Toast")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private float toastSeconds = 2.5f;

        private ILobbyService _service;
        private LobbyRequest _request;
        private readonly List<LobbySlotView> _slotViews = new List<LobbySlotView>();
        private float _toastHideAt;
        private bool _loadingGame;

        /// <summary>One call is in flight; the create/join/start buttons stay off until it returns.</summary>
        private bool _busy;

        /// <summary>
        /// This client wants/holds a seat. Set when a create or join is issued, cleared when that
        /// attempt fails and when the player leaves. A flag rather than inspecting the snapshot
        /// ("is MySessionId in room.Slots") because the snapshot cannot tell a live room from a
        /// stale update that arrives after Leave() — and that late update is exactly the one that
        /// must not drag the player back into a room they walked out of. Set before the call
        /// rather than after it because a service is free to raise RoomUpdated synchronously from
        /// inside CreateRoom/JoinRoom, which the fake does.
        /// </summary>
        private bool _seated;

        private void Start()
        {
            _request = LobbyRequest.Pending ??
                new LobbyRequest(fallbackRoomName, fallbackMaxPlayers, fallbackGameScene, fallbackDisplayName);

            // Consumed — clear it so a future entry point that forgets to set one lands on the
            // fallback (visibly wrong) instead of silently inheriting the previous game's request.
            LobbyRequest.Pending = null;

            if (useFakeService && !Application.isEditor)
            {
                Debug.LogWarning("LobbyController is running the in-memory FakeLobbyService in a player " +
                                 "build — nobody can actually play together. Turn off Use Fake Service " +
                                 "on the Lobby scene's Canvas before shipping.", this);
            }

            _service = useFakeService ? (ILobbyService)new FakeLobbyService() : new ColyseusLobbyService(_request.RoomName);
            _service.RoomUpdated += OnRoomUpdated;
            _service.Failed += OnFailed;

            BuildSlotViews(_request.MaxPlayers);

            if (titleText != null)
            {
                titleText.text = $"{_request.DisplayName} — {_request.MaxPlayers} Pemain";
            }

            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }

            if (codeInput != null)
            {
                codeInput.text = SessionData.Instance != null ? SessionData.Instance.LastRoomCode : string.Empty;
            }

            bool named = SessionData.Instance != null && SessionData.Instance.HasPlayerName;
            ShowPanel(named ? entryPanel : namePanel);
        }

        private void OnDestroy()
        {
            if (_service == null)
            {
                return;
            }

            _service.RoomUpdated -= OnRoomUpdated;
            _service.Failed -= OnFailed;
        }

        private void Update()
        {
            if (toastRoot != null && toastRoot.activeSelf && Time.unscaledTime >= _toastHideAt)
            {
                toastRoot.SetActive(false);
            }
        }

        // ---- button handlers ----

        /// <summary>Name panel → Entry panel. Wired to the confirm button.</summary>
        public void ConfirmName()
        {
            string typed = nameInput != null ? nameInput.text : string.Empty;

            if (!PlayerNameRules.IsValid(typed))
            {
                ShowToast(LobbyError.MessageFor(LobbyError.NameInvalid));
                return;
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.PlayerName = typed;
            }

            ShowPanel(entryPanel);
        }

        // The three calls below are async void on purpose: they are UnityEvent handlers, so there is
        // no caller to await them and no one to observe a returned Task's exception. Each therefore
        // catches everything itself and routes it down the same path a Failed event takes — a real
        // service can throw before it ever gets to raise Failed (DNS failure, refused socket, a
        // cancelled task), and a swallowed exception would leave the screen frozen with no toast.
        // Their names and signatures are load-bearing: the generated scene wires them by name.

        public async void CreateRoom()
        {
            if (_busy)
            {
                return; // a second click while the first create is still in flight would make two rooms
            }

            _busy = true;
            _seated = true;
            RefreshBusyState();

            try
            {
                await _service.CreateRoom(_request.RoomName, PlayerName(), _request.MaxPlayers);
            }
            catch (Exception e)
            {
                ReportUnexpected(e);
            }
            finally
            {
                _busy = false;
                RefreshBusyState();
            }
        }

        public async void JoinRoom()
        {
            if (_busy)
            {
                return;
            }

            string code = RoomCode.Sanitize(codeInput != null ? codeInput.text : string.Empty);

            if (!RoomCode.IsPlausible(code))
            {
                ShowToast(LobbyError.MessageFor(LobbyError.RoomNotFound));
                return;
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.LastRoomCode = code;
            }

            _busy = true;
            _seated = true;
            RefreshBusyState();

            try
            {
                await _service.JoinRoom(code, PlayerName());
            }
            catch (Exception e)
            {
                ReportUnexpected(e);
            }
            finally
            {
                _busy = false;
                RefreshBusyState();
            }
        }

        public async void StartGame()
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            RefreshBusyState();

            try
            {
                await _service.StartGame();
            }
            catch (Exception e)
            {
                ReportUnexpected(e);
            }
            finally
            {
                _busy = false;
                RefreshBusyState();
            }
        }

        public void LeaveRoom()
        {
            _seated = false;
            _ = _service.Leave();
            ShowPanel(entryPanel);
        }

        /// <summary>Copy the room code so it can be pasted into a chat message.</summary>
        public void CopyCode()
        {
            if (_service.Current == null)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = _service.Current.Code;
            ShowToast("Kode disalin");
        }

        /// <summary>
        /// Entry panel "Kembali". The visitor came from a museum doorway, so back is the museum —
        /// not MainMenu, which would put the platform picker and name field in front of them again
        /// and read as "I got logged out". Kept as a separate method: <see cref="BackToMainMenu"/>
        /// is API by name (CLAUDE.md) and old scenes may still point at it.
        /// </summary>
        public void BackToMuseum()
        {
            _seated = false;
            _ = _service.Leave();
            SceneLoader.Instance.LoadScene(SceneReference.Museum);
        }

        public void BackToMainMenu()
        {
            _seated = false;
            _ = _service.Leave();
            SceneLoader.Instance.LoadScene(SceneReference.MainMenu);
        }

        // ---- service events ----

        private void OnRoomUpdated(LobbyRoomSnapshot room)
        {
            if (!_seated)
            {
                // An update for a room this client is not in — a real service can push one after a
                // leave. Rendering it would yank the player back into a room they walked out of.
                return;
            }

            ShowPanel(roomPanel);

            if (codeText != null)
            {
                codeText.text = room.Code;
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.LastRoomCode = room.Code;
            }

            for (int i = 0; i < _slotViews.Count; i++)
            {
                LobbySlot slot = i < room.Slots.Count ? room.Slots[i] : LobbySlot.Empty;
                _slotViews[i].Render(slot, i + 1, !slot.IsEmpty && slot.SessionId == room.MySessionId);
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(room.AmHost);
                startButton.interactable = room.CanStart && !_busy;
            }

            if (startHintText != null)
            {
                startHintText.text = StartHintFor(room);
            }

            if (room.Phase == LobbyPhase.InProgress && !_loadingGame)
            {
                _loadingGame = true;

                // The room goes with us. Reconnecting to it from the game scene instead would
                // hit a server that never saw us leave — see ILobbyService.HandOffToGameScene.
                _service.HandOffToGameScene();

                SceneLoader.Instance.LoadScene(_request.GameScene);
            }
        }

        private void OnFailed(string code, string message)
        {
            ShowToast(message);

            // OnFailed handles every Failed event, not just create/join. A failure while seated
            // in a room (e.g. a rejected StartGame) leaves _service.Current non-null, so the
            // player stays put and the toast above is the only feedback. Only a failed create or
            // join leaves no room to stay in — that's the case that falls back to a panel, with
            // the code the player typed still in the field.
            if (_service.Current != null)
            {
                return;
            }

            _seated = false;

            // A rejected name is the one failure the entry panel cannot recover from: it has no
            // name field, so the player would toast forever with nowhere to type. Route it to the
            // name panel, which is what ui-flow.md's failure table already promises.
            ShowPanel(code == LobbyError.NameInvalid ? namePanel : entryPanel);
        }

        // ---- helpers ----

        /// <summary>
        /// Turns an exception out of a service call into the same feedback a Failed event gives.
        /// It is logged as well as toasted: a thrown exception is a bug or an environment problem,
        /// not one of the outcomes LobbyError enumerates.
        /// </summary>
        private void ReportUnexpected(Exception e)
        {
            Debug.LogException(e, this);
            OnFailed(LobbyError.ConnectionFailed, LobbyError.MessageFor(LobbyError.ConnectionFailed));
        }

        /// <summary>Keeps the buttons that issue a service call off while one is in flight.</summary>
        private void RefreshBusyState()
        {
            if (startButton != null)
            {
                startButton.interactable = !_busy && _service.Current != null && _service.Current.CanStart;
            }
        }

        private string PlayerName() =>
            SessionData.Instance != null ? SessionData.Instance.PlayerName : string.Empty;

        /// <summary>
        /// The line under the seats. It says why Mulai is grey rather than leaving the host to
        /// guess, and tells a guest what they are waiting for. The number is the rule's, not copy.
        /// </summary>
        public static string StartHintFor(LobbyRoomSnapshot room)
        {
            int minimum = LobbyRoomSnapshot.MinPlayersToStart;

            if (room.OccupiedCount < minimum)
            {
                int missing = minimum - room.OccupiedCount;
                return $"Minimal {minimum} pemain untuk mulai — tunggu {missing} pemain lagi";
            }

            return room.AmHost
                ? "Semua siap? Tekan Mulai."
                : "Menunggu pembuat ruangan menekan Mulai";
        }

        private void BuildSlotViews(int maxPlayers)
        {
            if (slotContainer == null || slotPrefab == null)
            {
                Debug.LogError("LobbyController: slotContainer or slotPrefab is not assigned.", this);
                return;
            }

            for (int i = 0; i < maxPlayers; i++)
            {
                LobbySlotView view = Instantiate(slotPrefab, slotContainer);
                view.name = $"Slot {i + 1}";
                view.Render(LobbySlot.Empty, i + 1, false);
                _slotViews.Add(view);
            }
        }

        private void ShowPanel(GameObject panel)
        {
            if (namePanel != null) namePanel.SetActive(panel == namePanel);
            if (entryPanel != null) entryPanel.SetActive(panel == entryPanel);
            if (roomPanel != null) roomPanel.SetActive(panel == roomPanel);
        }

        private void ShowToast(string message)
        {
            if (toastRoot == null || toastText == null)
            {
                Debug.LogWarning($"Lobby toast (no UI assigned): {message}", this);
                return;
            }

            toastText.text = message;
            toastRoot.SetActive(false);
            toastRoot.SetActive(true); // re-enable so the tween replays from the start
            _toastHideAt = Time.unscaledTime + toastSeconds;
        }
    }
}
