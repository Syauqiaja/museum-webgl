using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using Museum.Core;
using Museum.Net.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Net
{
    /// <summary>
    /// The connection to the server's <c>museum</c> presence room, kept for the whole visit rather
    /// than for one Museum scene. <see cref="MuseumPresence"/> — on the museum's player rig — is
    /// the view that draws from it and reports into it; this is what outlives that scene.
    /// </summary>
    /// <remarks>
    /// It lives through the doorway on purpose. A visitor who goes through to the lobby and into
    /// Dakon or Egrang stays in the room, marked away (<see cref="SetActivity"/>), so the others
    /// keep seeing them standing where they left, tagged "Sedang bermain …". Leaving the room would
    /// make them vanish, which reads as having left the museum.
    ///
    /// It is left on the way to MainMenu (going back there is how a visit ends) and on quit; a
    /// closed tab closes the socket, and the server drops the visitor. Opened straight on the
    /// <see cref="ColyseusNetManager.Client"/>, never through its create/join helpers: those record
    /// the room as the seat this client holds, and the seat is the game room the lobby opens.
    ///
    /// Contract: server <c>docs/protocol.md#exhibition-museum-scene</c>.
    /// </remarks>
    public sealed class MuseumPresenceLink : MonoBehaviour
    {
        /// <summary>The server-side room name.</summary>
        public const string RoomName = "museum";

        /// <summary>Seconds between attempts to get back in after the room went away.</summary>
        private const float RejoinDelay = 5f;
        private const int MaxRejoins = 3;

        public static MuseumPresenceLink Instance { get; private set; }

        private Room<MuseumState> _room;
        private Task _joining;
        private int _rejoins;
        private bool _closing;

        private bool _hasPose;
        private MovePayload _lastPose;
        private string _activity = MuseumActivities.InHall;

        /// <summary>The open room, or null.</summary>
        public Room<MuseumState> Room => _room;

        /// <summary>True while the presence room is open.</summary>
        public bool Connected => _room != null && _room.Connection != null && _room.Connection.IsOpen;

        /// <summary>This client's session id in the room; empty when not in it.</summary>
        public string SessionId => _room != null ? _room.SessionId : string.Empty;

        /// <summary><see cref="MuseumActivities.InHall"/>, or the game this visitor is away playing.</summary>
        public string Activity => _activity;

        /// <summary>The room's state changed (also raised once on joining).</summary>
        public event Action<MuseumState> StateChanged;

        /// <summary>Another visitor set off an exhibit.</summary>
        public event Action<string, int> Interacted;

        /// <summary>The room went away under us; whatever was drawn from it is stale.</summary>
        public event Action Lost;

        /// <summary>The link, made the first time the museum asks for it.</summary>
        public static MuseumPresenceLink Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("Museum Presence (Link)");
            DontDestroyOnLoad(go);
            return go.AddComponent<MuseumPresenceLink>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>Joins the room unless already in it or on the way in. Safe to call every time the museum loads.</summary>
        public Task Connect()
        {
            if (_room != null) return Task.CompletedTask;
            if (_joining != null) return _joining;

            _joining = Join();
            return _joining;
        }

        /// <summary>Where the visitor stands. Remembered, so a rejoin can put them back.</summary>
        public void SendMove(Vector3 position, float yaw)
        {
            _lastPose = new MovePayload { x = position.x, y = position.y, z = position.z, yaw = yaw };
            _hasPose = true;

            if (Connected) _ = _room.Send("move", _lastPose);
        }

        /// <summary>
        /// Marks the visitor away playing <paramref name="game"/>, or back in the hall with
        /// <see cref="MuseumActivities.InHall"/>. Sent only on a change, and only once the room
        /// lists them (after a first move) — a rejoin re-sends it after the pose.
        /// </summary>
        public void SetActivity(string game)
        {
            game = game ?? MuseumActivities.InHall;
            if (game == _activity) return;

            _activity = game;
            if (Connected && _hasPose) _ = _room.Send("activity", new ActivityPayload { game = game });
        }

        /// <summary>An exhibit the local visitor set off, for the others to see and hear.</summary>
        public void SendInteract(string station, int index)
        {
            if (!Connected || _activity != MuseumActivities.InHall) return;
            _ = _room.Send("interact", new InteractPayload { station = station, index = index });
        }

        private async Task Join()
        {
            try
            {
                if (ColyseusNetManager.Instance == null) return;

                var options = new Dictionary<string, object>
                {
                    ["displayName"] = ColyseusNetManager.Instance.PlayerName,
                    ["avatar"] = ColyseusNetManager.Instance.PlayerAvatar,
                };

                Room<MuseumState> room = await ColyseusNetManager.Instance.Client.JoinOrCreate<MuseumState>(RoomName, options);

                // Left for MainMenu, or quit, while the join was in flight.
                if (this == null || _closing)
                {
                    _ = room.Leave(true);
                    return;
                }

                _room = room;
                _rejoins = 0;

                room.OnStateChange += OnStateChange;
                room.OnLeave += OnRoomLeft;
                room.OnError += (code, message) => Debug.LogWarning($"[MuseumPresenceLink] room error {code}: {message}", this);
                room.OnMessage<InteractedPayload>("interacted", OnInteracted);
                room.OnMessage<ErrorPayload>("error", OnServerError);

                // Back after a drop: put the visitor where they were, and away again if they were.
                if (_hasPose)
                {
                    _ = room.Send("move", _lastPose);
                    if (_activity != MuseumActivities.InHall) _ = room.Send("activity", new ActivityPayload { game = _activity });
                }

                if (room.State != null) StateChanged?.Invoke(room.State);
            }
            catch (Exception e)
            {
                // The museum is playable alone; this only loses the company.
                Debug.LogWarning($"[MuseumPresenceLink] could not join the presence room: {e.Message}", this);
            }
            finally
            {
                _joining = null;
            }
        }

        private void OnStateChange(MuseumState state, bool isFirstState) => StateChanged?.Invoke(state);

        private void OnInteracted(InteractedPayload payload)
        {
            if (payload == null || payload.sessionId == SessionId) return;
            Interacted?.Invoke(payload.station, payload.index);
        }

        private void OnServerError(ErrorPayload error)
        {
            // too_fast is a visitor mashing Enter; the press still played locally.
            if (error != null && error.code != "too_fast")
            {
                Debug.LogWarning($"[MuseumPresenceLink] {error.code}: {error.message}", this);
            }
        }

        /// <summary>
        /// The socket closed under us — the server restarted, or the connection dropped. A few
        /// rejoins are tried, wherever the visitor is, before the museum settles for being
        /// single-player.
        /// </summary>
        private async void OnRoomLeft(int code)
        {
            _room = null;
            if (this == null || _closing) return;

            Lost?.Invoke();

            if (_rejoins >= MaxRejoins)
            {
                Debug.LogWarning($"[MuseumPresenceLink] presence room closed ({code}); giving up after {MaxRejoins} rejoins.", this);
                return;
            }

            _rejoins++;
            await Task.Delay(TimeSpan.FromSeconds(RejoinDelay));
            if (this == null || _closing) return;

            await Connect();
        }

        /// <summary>MainMenu is where a visit ends — the others should see this visitor leave.</summary>
        private void OnActiveSceneChanged(Scene from, Scene to)
        {
            if (to.name == SceneReference.MainMenu) Destroy(gameObject);
        }

        private void OnApplicationQuit() => _closing = true;

        private void OnDestroy()
        {
            _closing = true;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (Instance == this) Instance = null;

            Room<MuseumState> room = _room;
            _room = null;

            if (room != null)
            {
                room.OnStateChange -= OnStateChange;
                room.OnLeave -= OnRoomLeft;
                // Straight on the room, not ColyseusNetManager.Leave: that would also clear the
                // seat SessionData holds, and this was never that seat.
                _ = room.Leave(true);
            }
        }

        [Serializable]
        private sealed class MovePayload
        {
            public float x;
            public float y;
            public float z;
            public float yaw;
        }

        [Serializable]
        private sealed class ActivityPayload
        {
            public string game;
        }

        [Serializable]
        private sealed class InteractPayload
        {
            public string station;
            public int index;
        }

        [Serializable]
        public sealed class InteractedPayload
        {
            public string sessionId;
            public string station;
            public int index;
        }
    }
}
