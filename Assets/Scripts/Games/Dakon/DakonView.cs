using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Museum.Core;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// Renders a Dakon game and feeds it player input. It never owns the rules: it reads an
    /// <see cref="IDakonSession"/> and animates what that session reports. Which session it gets
    /// decides the mode — a local <see cref="LocalDakonSession"/> for hotseat, or the networked
    /// one bound by the Dakon scene's net bootstrap when the player arrived from the lobby, where
    /// the server is authoritative and a drop is a request rather than a fact.
    ///
    /// Online, input is only live on this client's own turn; in hotseat both sides share the
    /// screen and whoever's turn it is plays.
    ///
    /// Interaction is single-action: the target hole is always the forced
    /// <see cref="DakonBoard.NextHoleIndex"/>, so the player only picks a card. The next hole
    /// stays highlighted as the destination indicator, not a second required click.
    /// </summary>
    public sealed class DakonView : MonoBehaviour
    {
        // Pool size and grab size are deliberately not editable here. They are rules, and the
        // rules belong to the server (src/games/dakon/DakonConfig.ts) — a scene that could set
        // its own would make a hotseat game a different length from an online one, which is how
        // this scene ended up dealing 60 seeds against a server dealing 120. DakonConfig's
        // defaults mirror the server's; change them there, in step with it, or not at all.
        [Header("Config")]
        [SerializeField] private int rngSeed = 0;
        [SerializeField] private bool randomizeSeedOnStart = true;

        [Tooltip("Authored seed species. Their categories/ids drive the pool; sprites/names render on cards. If empty, the model's string defaults are used.")]
        [SerializeField] private SeedType[] seedTypes;

        [Header("Board scene refs")]
        [Tooltip("20 hole anchors, ring order 0..19 (0..9 = P0 side, 10..19 = P1 side).")]
        [SerializeField] private Transform[] holeAnchors = new Transform[20];
        [Tooltip("Optional per-hole labels (len 20) showing each hole's type after StartGame.")]
        [SerializeField] private Text[] holeLabels = new Text[20];
        [Tooltip("Marker object moved onto the current nextHoleIndex anchor. Optional.")]
        [SerializeField] private Transform highlightMarker;

        [Header("Hand (card layout)")]
        [SerializeField] private RectTransform handContainer;
        [SerializeField] private DakonCard cardPrefab;

        [Tooltip("Hidden while the opponent plays. Leave empty to hide the hand container itself; point it at the whole hand frame when the cards sit inside a scroll view, so the empty frame goes too.")]
        [SerializeField] private GameObject handRoot;

        [Tooltip("Shown in place of the hand while the opponent is playing. Must sit outside the hidden hand root, or it goes dark with it.")]
        [SerializeField] private GameObject waitingForTurnPanel;

        [Tooltip("Text of the waiting panel. Optional; left alone if unassigned.")]
        [SerializeField] private TextMeshProUGUI waitingForTurnLabel;

        [Tooltip("Wording shown while it is the opponent's turn. {0} is replaced by their name.")]
        [SerializeField] private string waitingForTurnText = "Mohon tunggu giliran {0}";

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI turnLabel;
        [SerializeField] private TextMeshProUGUI poolLabel;
        [SerializeField] private TextMeshProUGUI scoreP0Label;
        [SerializeField] private TextMeshProUGUI scoreP1Label;
        [SerializeField] private TextMeshProUGUI toastLabel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI gameOverLabel;

        [Tooltip("The toast frame (background + label), hidden until there is something to say. Leave empty to show/hide the label's own object.")]
        [SerializeField] private GameObject toastRoot;

        [Tooltip("How long a toast stays on screen before it clears itself.")]
        [SerializeField] private float toastSeconds = 2f;

        [Header("Feedback")]
        [Tooltip("Screen-edge glow pulsed green on a matching drop and red on a mismatch. Optional; found among this object's children when unassigned.")]
        [SerializeField] private DakonVignette vignette;

        [Header("Exit")]
        [Tooltip("Leaves for the museum from the results panel. Optional.")]
        [SerializeField] private Button gameOverExitButton;

        [Tooltip("Scene both this and the pause panel's exit load.")]
        [SerializeField] private string exitScene = SceneReference.Museum;

        [Header("3D seeds")]
        [Tooltip("Camera used to project a played card's screen position into world space (the 3D seed's launch point). Null = Camera.main.")]
        [SerializeField] private Camera boardCamera;
        [Tooltip("Storehouse bins, index = player*2 + category (0 P1-Monocot, 1 P1-Dicot, 2 P2-Monocot, 3 P2-Dicot). Seeds fly here at game over.")]
        [SerializeField] private Transform[] storeAnchors = new Transform[4];

        [Tooltip("Fallback scatter radius, used only to place a seed that settled outside its bowl. Must be smaller than the bowl or the fix puts it back out again.")]
        [SerializeField] private float pileRadius = 0.04f;

        [Tooltip("Inner radius of a playing hole, measured off the board mesh. A seed resting further out than this is treated as a stray.")]
        [SerializeField] private float holeBowlRadius = 0.06f;

        [Tooltip("How far a playing hole's floor sits below its anchor. Throws aim down into the bowl by this much; aiming at the anchor itself lands seeds on the rim.")]
        [SerializeField] private float holeBowlDepth = 0.058f;

        [Tooltip("Inner radius of a storehouse bin. The end bowls are wider than the playing holes.")]
        [SerializeField] private float storeBowlRadius = 0.09f;

        [Tooltip("How far a storehouse bin's floor sits below its anchor.")]
        [SerializeField] private float storeBowlDepth = 0.082f;

        [Header("Seed physics")]
        [Tooltip("How a thrown seed behaves once it leaves the card.")]
        [SerializeField] private DakonSeedTuning seedTuning = new DakonSeedTuning();

        [Tooltip("Gap between one seed leaving for its bin and the next, at game over. Zero launches all sixty as a single block.")]
        [SerializeField] private float sweepStaggerSeconds = 0.02f;

        [Tooltip("How far above the card's projected point a seed is spawned. The projection lands on the board's own surface, and a seed born inside the board is shoved out of it before it can be thrown.")]
        [SerializeField] private float launchClearance = 0.05f;

        [Tooltip("Shortest gap between two seeds being thrown. Drops that land in the same frame queue up instead of spawning on top of each other.")]
        [SerializeField] private float throwSpacingSeconds = 0.07f;

        IDakonSession _session;

        /// <summary>Set when something else (the net bootstrap) supplied the session.</summary>
        bool _bound;

        /// <summary>Set once Start has run, so a late reconnect failure knows who owns the fallback.</summary>
        bool _started;
        readonly List<DakonCard> _cards = new List<DakonCard>();
        readonly Dictionary<string, SeedType> _typeMap = new Dictionary<string, SeedType>();

        /// <summary>
        /// Seeds we have asked to drop and not yet seen applied. There is deliberately no single
        /// "busy" flag: a drop locks its own card and nothing else, so the player can play a whole
        /// hand as fast as they can click while the seeds are still arcing into their holes.
        /// </summary>
        readonly HashSet<string> _pending = new HashSet<string>();

        struct SpawnedSeed
        {
            public Transform Obj;
            public DakonSeedBody Body;
            public int ScoringPlayer;
            public SeedCategory Category;
        }
        readonly List<SpawnedSeed> _seeds = new List<SpawnedSeed>();
        int[] _holePile;
        readonly int[] _storePile = new int[4];

        /// <summary>Container the thrown seeds live under, made on first use.</summary>
        Transform _seedRoot;

        /// <summary>Throws waiting their turn, and the coroutine working through them.</summary>
        readonly Queue<PendingThrow> _throwQueue = new Queue<PendingThrow>();
        Coroutine _thrower;

        struct PendingThrow
        {
            public GameObject Prefab;
            public Vector3 Start;
            public int Hole;
            public int ScoringPlayer;
            public SeedCategory Category;
        }

        /// <summary>The one running toast countdown, so a second message replaces the first.</summary>
        Coroutine _toastTimer;

        void Awake()
        {
            // A runtime listener, not a persistent one: the results panel's exit is reached
            // through a serialized reference, so it adds nothing to the by-name UnityEvent
            // surface the generated scenes wire against.
            if (gameOverExitButton != null) gameOverExitButton.onClick.AddListener(BackToMainMenu);

            // Found rather than required: the overlay is one object on this scene's Canvas, and a
            // scene whose inspector links did not survive (see Assets/Docs/scene-setup.md) would
            // otherwise lose the feedback silently rather than noisily.
            if (vignette == null) vignette = GetComponentInChildren<DakonVignette>(includeInactive: true);

            // Down before the first frame, whatever the scene was saved with: the toast only
            // ever answers a refused drop, and there cannot be one yet.
            HideToast();
        }

        void Start()
        {
            _started = true;

            // A networked session binds in its own Awake, before this runs. Nothing bound by now
            // means nobody is driving this scene from a room, so it is a local hotseat game.
            if (!_bound)
            {
                NewGame();
            }
        }

        /// <summary>
        /// Hand this view the session to render. Called by the net bootstrap before Start; the
        /// view then never creates a local board, so the two modes cannot both be running.
        /// </summary>
        public void Bind(IDakonSession session)
        {
            if (session == null) return;

            _bound = true;
            Attach(session);
        }

        /// <summary>
        /// Claims this view for a network session that is still connecting. The net bootstrap
        /// reconnects in an <c>async</c> Awake, so its <c>await</c> yields and <see cref="Start"/>
        /// runs first — without this the view starts a hotseat game, deals a full local hand, and
        /// then has it swapped for the server's board a moment later. The player sees fifteen
        /// cards appear and vanish, and every card they clicked in that window went to a session
        /// that no longer exists.
        ///
        /// Pair every call with <see cref="ReleaseNetworkReservation"/> on the failure path, or a
        /// failed reconnect leaves the scene with no game at all.
        /// </summary>
        public void ReserveForNetwork()
        {
            _bound = true;
        }

        /// <summary>
        /// Gives up a reservation whose session never arrived, and starts the local game
        /// <see cref="Start"/> would have started. Does nothing once a session has bound.
        /// </summary>
        public void ReleaseNetworkReservation()
        {
            if (_session != null) return;

            _bound = false;

            // A reconnect can fail before Start has even run. Leaving it to Start keeps the
            // local game to exactly one, instead of one here and a second one a frame later.
            if (_started) NewGame();
        }

        /// <summary>
        /// Leaves the match for <see cref="exitScene"/> — the museum the player walked in from,
        /// which is what both this scene's exits are labelled.
        ///
        /// The name is frozen: the pause panel's button stores it as a persistent listener in
        /// Dakon.unity, and CLAUDE.md lists it among the handler names a generated scene wires
        /// by string. Renaming it here breaks that button silently. (It loaded MainMenu once;
        /// that, not the name, was the bug.)
        ///
        /// No consented room leave: the session keeps its room private and no game scene in
        /// this project leaves one. After game_over the match row is already closed, and
        /// mid-match a dropped socket is the forfeit the server already models. Clearing the
        /// room session is the part that matters here — a stale reconnection token would send
        /// the next visit to this scene reconnecting into a room that no longer exists.
        /// </summary>
        public void BackToMainMenu()
        {
            if (string.IsNullOrEmpty(exitScene)) return;

            if (SessionData.Instance != null) SessionData.Instance.ClearRoomSession();

            // The loader rides in on the bootstrap object the lobby carries between scenes.
            // Playing Dakon on its own has no loader, and "the exit button does nothing" is a
            // worse answer than a cut to the museum.
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(exitScene);
            else SceneManager.LoadScene(exitScene);
        }

        public void NewGame()
        {
            if (randomizeSeedOnStart) rngSeed = Random.Range(int.MinValue, int.MaxValue);

            Attach(new LocalDakonSession(BuildConfig(), rngSeed));
        }

        /// <summary>Subscribe to a session and draw its opening position.</summary>
        void Attach(IDakonSession session)
        {
            Detach();

            _session = session;
            _session.DropApplied += OnDropApplied;
            _session.DropRejected += OnDropRejected;
            _session.HandChanged += OnHandChanged;
            _session.GameOver += OnSessionGameOver;
            _session.StateChanged += OnStateChanged;
            _session.BoardReady += OnBoardReady;

            // A fresh session is a fresh board: whatever the last one left us mid-animation, no
            // input is outstanding on this one.
            _pending.Clear();

            // The config's TypeId -> SeedType map is what resolves sprites and 3D prefabs, and a
            // networked session never calls BuildConfig — so build it here too.
            BuildConfig();

            ClearSeeds();

            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            if (_toastTimer != null) { StopCoroutine(_toastTimer); _toastTimer = null; }
            HideToast();

            RenderHoles();
            DealHand();
            RefreshHud();
            MoveHighlight();
        }

        void Detach()
        {
            if (_session == null) return;

            _session.DropApplied -= OnDropApplied;
            _session.DropRejected -= OnDropRejected;
            _session.HandChanged -= OnHandChanged;
            _session.GameOver -= OnSessionGameOver;
            _session.StateChanged -= OnStateChanged;
            _session.BoardReady -= OnBoardReady;
            _session = null;
        }

        void OnDestroy()
        {
            if (gameOverExitButton != null) gameOverExitButton.onClick.RemoveListener(BackToMainMenu);
            Detach();
        }

        // Build the pure model config from the authored SeedType catalog. Also (re)builds the
        // TypeId -> SeedType map the view uses to render cards. Falls back to the model's
        // string defaults when no SeedTypes are assigned.
        DakonConfig BuildConfig()
        {
            _typeMap.Clear();
            var config = new DakonConfig();

            if (seedTypes == null || seedTypes.Length == 0) return config;

            var monocot = new List<string>();
            var dicot = new List<string>();
            foreach (var type in seedTypes)
            {
                if (type == null) continue;
                _typeMap[type.TypeId] = type;
                (type.Category == SeedCategory.Monocot ? monocot : dicot).Add(type.TypeId);
            }

            if (monocot.Count > 0) config.MonocotTypeIds = monocot.ToArray();
            if (dicot.Count > 0) config.DicotTypeIds = dicot.ToArray();
            return config;
        }

        SeedType ResolveType(string typeId)
        {
            return typeId != null && _typeMap.TryGetValue(typeId, out var type) ? type : null;
        }

        // ---- rendering ----

        void RenderHoles()
        {
            if (holeLabels == null) return;
            for (int i = 0; i < holeLabels.Length && i < _session.HoleCount; i++)
                if (holeLabels[i] != null)
                    holeLabels[i].text = _session.HoleTypeAt(i).ToString();
        }

        void DealHand()
        {
            foreach (var card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();

            if (handContainer == null || cardPrefab == null)
            {
                Debug.LogWarning($"[DAKON-DIAG] DealHand aborted: handContainer={(handContainer == null ? "null" : "ok")} cardPrefab={(cardPrefab == null ? "null" : "ok")}");
                return;
            }

            foreach (var seed in _session.Hand)
            {
                var card = Instantiate(cardPrefab, handContainer);
                card.Init(seed, ResolveType(seed.TypeId), OnCardChosen);
                _cards.Add(card);
            }

            // TEMP DIAG — remove once the empty-hand bug is closed.
            Debug.Log($"[DAKON-DIAG] DealHand session={_session.GetType().Name} phase={_session.Phase} " +
                      $"hand={_session.Hand.Count} cards={_cards.Count} myTurn={_session.IsMyTurn} " +
                      $"seat={_session.MySeat} active={_session.ActivePlayer} holes={_session.HoleCount}");

            RefreshHandDisplay();
        }

        /// <summary>
        /// Show the hand only to the player whose turn it is; everyone else gets the waiting
        /// line instead. Online the hand is synced publicly and is always the *active* player's,
        /// so leaving it on screen would show the waiting player someone else's cards and invite
        /// them to click one. Hiding it is also the honest picture: they have no hand right now.
        ///
        /// Hotseat is unaffected — whoever is to move is the person sitting at the screen, so
        /// <see cref="IDakonSession.IsMyTurn"/> is true for the whole match and the panel never
        /// appears.
        /// </summary>
        void RefreshHandDisplay()
        {
            bool playing = _session != null && _session.Phase == Phase.InProgress;
            bool mine = playing && _session.IsMyTurn;

            // A card already sent stays locked; every other card in the hand stays live. Nothing
            // waits on an animation.
            SetCardsInteractable(mine);

            // Only a live match has a turn to wait for. Once it is over the results panel owns
            // the screen, and a "wait your turn" line under it would be nonsense.
            bool waiting = playing && !mine;

            // TEMP DIAG — remove once the empty-hand bug is closed.
            Debug.Log($"[DAKON-DIAG] RefreshHandDisplay playing={playing} mine={mine} waiting={waiting} cards={_cards.Count}");

            GameObject hand = handRoot != null ? handRoot
                : handContainer != null ? handContainer.gameObject
                : null;

            if (hand != null) hand.SetActive(!waiting);
            if (waitingForTurnPanel != null) waitingForTurnPanel.SetActive(waiting);
            if (waitingForTurnLabel != null) waitingForTurnLabel.text = WaitingForTurnText();
        }

        /// <summary>
        /// The waiting line with the opponent's name in it. A scene authored before the wording
        /// took a {0} still holds the old literal in its serialized override, and that override
        /// wins — so a format string without a placeholder is used as-is rather than treated as
        /// an error.
        /// </summary>
        string WaitingForTurnText()
        {
            if (string.IsNullOrEmpty(waitingForTurnText)) return string.Empty;
            if (!waitingForTurnText.Contains("{0}")) return waitingForTurnText;

            return string.Format(waitingForTurnText, _session.DisplayNameOf(_session.ActivePlayer));
        }

        void RefreshHud()
        {
            if (turnLabel != null) turnLabel.text = TurnText();
            if (poolLabel != null) poolLabel.text = $"Pool: {_session.PoolCount}";
            if (scoreP0Label != null) scoreP0Label.text = $"{_session.DisplayNameOf(0)}: {_session.Total(0)}";
            if (scoreP1Label != null) scoreP1Label.text = $"{_session.DisplayNameOf(1)}: {_session.Total(1)}";
        }

        /// <summary>
        /// "Giliran kamu" reads better than the player's own name when the player *is* that seat —
        /// and online they always are one specific seat. Everyone else is named, so a room of two
        /// strangers can tell who they are waiting for. Hotseat has no registered players, so the
        /// session hands back seat labels there and both sides read the same way.
        /// </summary>
        string TurnText()
        {
            if (_session.Phase != Phase.InProgress)
            {
                return string.Empty;
            }

            bool ownsOneSeat = _session.MySeat == _session.ActivePlayer || !_session.IsMyTurn;
            bool online = _bound;

            if (online && ownsOneSeat && _session.IsMyTurn)
            {
                return "Giliran kamu";
            }

            return $"Giliran {_session.DisplayNameOf(_session.ActivePlayer)}";
        }

        void MoveHighlight()
        {
            if (highlightMarker == null) return;
            int i = _session.NextHoleIndex;
            if (holeAnchors != null && i >= 0 && i < holeAnchors.Length && holeAnchors[i] != null)
            {
                highlightMarker.position = holeAnchors[i].position;
                highlightMarker.gameObject.SetActive(_session.Phase == Phase.InProgress);
            }
        }

        // ---- input ----

        void OnCardChosen(string seedId)
        {
            // Only this card is off limits, and only because it is already sent. Clicking the
            // next one does not wait on it.
            if (_pending.Contains(seedId) || _session.Phase != Phase.InProgress) return;

            if (!_session.IsMyTurn)
            {
                ShowToast(DakonErrorText.NotYourTurn);
                return;
            }

            // Nothing is animated here: the drop is a request. Locally the session answers before
            // this returns; online it answers when the server says so. Either way the picture
            // changes in OnDropApplied and nowhere else, so the two modes cannot diverge.
            _pending.Add(seedId);

            var chosen = _cards.Find(c => c != null && c.SeedId == seedId);
            if (chosen != null) chosen.SetInteractable(false);

            _session.RequestDrop(seedId);
        }

        void OnDropApplied(DakonDrop drop)
        {
            _pending.Remove(drop.SeedId);

            // Ahead of the animation, not after it. The seed takes a full second to reach its
            // hole and several can be in the air at once; a score that only caught up when the
            // last arc landed would read as a bug.
            RefreshHud();
            MoveHighlight();

            ResolveDrop(drop);
        }

        void OnDropRejected(DakonError error)
        {
            ShowToast(DakonErrorText.MessageFor(error));

            // A refusal invalidates anything queued behind it, so the whole hand is rebuilt from
            // the session — server truth online, the board itself locally — rather than trying to
            // work out which of our outstanding drops survived.
            _pending.Clear();
            DealHand();
        }

        /// <summary>
        /// The board's holes now exist. Online this is the real start of the game — the session
        /// was bound while the player was still leaving the lobby, so everything sized by
        /// <see cref="IDakonSession.HoleCount"/> (the per-hole seed piles) was sized from an empty
        /// board and has to be built again here. Locally it never fires; Attach already did this.
        /// </summary>
        void OnBoardReady()
        {
            _pending.Clear();
            ClearSeeds();
            RenderHoles();
            DealHand();
            RefreshHud();
            MoveHighlight();
        }

        /// <summary>A fresh draw — for us or for the opponent, whose hand we also render.</summary>
        void OnHandChanged()
        {
            // A fresh draw: whatever was outstanding belonged to the hand that just ended.
            _pending.Clear();

            DealHand();
            RefreshHud();
            MoveHighlight();
        }

        void OnStateChanged()
        {
            RefreshHud();
            MoveHighlight();

            // The turn can pass on a patch that deals no new hand — an opponent's drop, or a
            // forfeit ending the match — and that is exactly when the cards must lock.
            RefreshHandDisplay();
        }

        void OnSessionGameOver()
        {
            // Results first, sweep second. The scores are the point of the screen, and gating
            // them behind an animation means any hiccup in the sweep leaves the players staring
            // at a finished board with no idea who won.
            ShowGameOver();

            if (isActiveAndEnabled)
            {
                StartCoroutine(RouteSeedsToStores());
            }
        }

        /// <summary>
        /// Resolve one applied drop: the card leaves the hand and a 3D seed is thrown at its hole.
        /// Purely decorative — the HUD and the input are already up to date by the time this runs,
        /// and the seed flies on its own afterwards, so a fast player simply has several in the
        /// air at once. They do not interfere: each owns its own card, its own seed object and its
        /// own pile slot.
        /// </summary>
        void ResolveDrop(DakonDrop drop)
        {
            FlashOutcome(drop);

            var card = _cards.Find(c => c != null && c.SeedId == drop.SeedId);
            var type = ResolveType(drop.TypeId);
            bool holeValid = holeAnchors != null && drop.HoleIndex >= 0
                             && drop.HoleIndex < holeAnchors.Length && holeAnchors[drop.HoleIndex] != null;

            if (type != null && type.SeedPrefab != null && holeValid)
            {
                // Lifted clear of the board. CardTopWorld projects the card to the hole's depth,
                // which puts it within a millimetre of the board's top face — so the seed would
                // spawn overlapping the table and be flung sideways by depenetration long before
                // gravity got a say. That looked exactly like the arbitrary scatter this replaces.
                Vector3 start = CardTopWorld(card, drop.HoleIndex) + Vector3.up * launchClearance;
                if (card != null) { _cards.Remove(card); Destroy(card.gameObject); }
                SpawnSeedToHole(type.SeedPrefab, start, drop.HoleIndex, drop.ScoringPlayer, drop.Category);
            }
            else if (card != null)
            {
                _cards.Remove(card);
                Destroy(card.gameObject);
            }

            // The hand refill and the game-over sweep are the session's to announce (HandChanged /
            // GameOver), because online they are the server's decisions and arrive separately.
        }

        /// <summary>
        /// Right or wrong, as a colour. A seed that landed in a hole of its own kind scored for
        /// the player who dropped it; anything else scored for the opponent. That is read back off
        /// the board's hole types rather than carried in the drop, because the hole types are
        /// fixed for the match and the server sends no such field — inventing one would put the
        /// two repos' protocols out of step.
        ///
        /// Fired with the HUD, not when the seed lands: the answer has to feel simultaneous with
        /// the click, and several seeds can be in the air at once.
        /// </summary>
        void FlashOutcome(DakonDrop drop)
        {
            if (vignette == null || _session == null) return;
            if (drop.HoleIndex < 0 || drop.HoleIndex >= _session.HoleCount) return;

            vignette.Flash(_session.HoleTypeAt(drop.HoleIndex) == drop.Category);
        }

        Vector3 CardTopWorld(DakonCard card, int hole)
        {
            var cam = boardCamera != null ? boardCamera : Camera.main;
            if (card == null || cam == null)
                return holeAnchors[hole].position;

            RectTransform rt = card.transform as RectTransform;
            if (rt == null)
                return holeAnchors[hole].position;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;

            float depth = cam.WorldToScreenPoint(holeAnchors[hole].position).z;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, topCenter);
            return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        }

        /// <summary>
        /// Queue a seed to be thrown. Nothing here waits for it to land: once thrown, the seed
        /// owns its flight, which is what lets a fast player have half a hand in the air at once
        /// without any of them queueing behind the others.
        ///
        /// The queue exists for the other extreme — several drops resolving on the *same* frame,
        /// which happens when a player empties a hand in one burst and the server answers all of
        /// them at once. Spawned together they appear a centimetre apart and spend their first
        /// contact shoving each other across the board. A few tens of milliseconds between throws
        /// is invisible and removes the problem.
        /// </summary>
        void SpawnSeedToHole(GameObject prefab, Vector3 start, int hole, int scoringPlayer, SeedCategory category)
        {
            _throwQueue.Enqueue(new PendingThrow
            {
                Prefab = prefab,
                Start = start,
                Hole = hole,
                ScoringPlayer = scoringPlayer,
                Category = category,
            });

            if (_thrower == null && isActiveAndEnabled) _thrower = StartCoroutine(ThrowQueued());
        }

        IEnumerator ThrowQueued()
        {
            var gap = throwSpacingSeconds > 0f ? new WaitForSeconds(throwSpacingSeconds) : null;

            while (_throwQueue.Count > 0)
            {
                Throw(_throwQueue.Dequeue());
                if (gap != null) yield return gap;
            }

            _thrower = null;
        }

        void Throw(PendingThrow pending)
        {
            if (pending.Prefab == null) return;
            if (holeAnchors == null || pending.Hole < 0 || pending.Hole >= holeAnchors.Length) return;

            Transform anchor = holeAnchors[pending.Hole];
            if (anchor == null) return;

            var obj = Instantiate(pending.Prefab, pending.Start, Random.rotation, SeedRoot());
            var body = DakonSeedBody.Attach(obj, seedTuning);
            body.ThrowTo(anchor, holeBowlRadius, holeBowlDepth, NextPileSlot(pending.Hole), pileRadius);

            _seeds.Add(new SpawnedSeed
            {
                Obj = obj.transform,
                Body = body,
                ScoringPlayer = pending.ScoringPlayer,
                Category = pending.Category,
            });
        }

        /// <summary>
        /// A plain container for thrown seeds. They are not parented to the hole they land in —
        /// those anchors are scaled to 0.05, and a rigidbody under a scaled parent renders at a
        /// different size than it collides at.
        /// </summary>
        Transform SeedRoot()
        {
            if (_seedRoot == null)
            {
                _seedRoot = new GameObject("Dakon Seeds").transform;
                _seedRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            return _seedRoot;
        }

        /// <summary>
        /// Game-over sweep: every piled seed is thrown to its scoring player's category bin and
        /// settles there, the same way it settled in its hole. A mismatched seed scored to the
        /// opponent, so it routes by the seed's own recorded ScoringPlayer, not by who dropped it.
        ///
        /// Staggered rather than simultaneous. Sixty seeds leaving on the same frame is a block
        /// moving, not a board being emptied — and it lands sixty bodies in two bowls at once,
        /// which is also the worst case for the solver.
        /// </summary>
        IEnumerator RouteSeedsToStores()
        {
            // The last drops of the match may still be queued or mid-flight. Sweeping now would
            // leave those seeds behind on the board, still in a hole, contradicting the totals the
            // results panel is already showing.
            while (_throwQueue.Count > 0 || _thrower != null) yield return null;

            float grace = 0f;
            while (grace < seedTuning.SettleTimeoutSeconds && !AllSeedsSettled())
            {
                grace += Time.deltaTime;
                yield return null;
            }

            var wait = sweepStaggerSeconds > 0f ? new WaitForSeconds(sweepStaggerSeconds) : null;

            foreach (var s in _seeds)
            {
                if (s.Obj == null || s.Body == null) continue;

                int idx = s.ScoringPlayer * 2 + (int)s.Category;
                if (storeAnchors == null || idx < 0 || idx >= storeAnchors.Length || storeAnchors[idx] == null)
                    continue;   // unwired bin: leave the seed in its hole

                s.Body.ThrowTo(storeAnchors[idx], storeBowlRadius, storeBowlDepth, _storePile[idx]++, pileRadius);

                if (wait != null) yield return wait;
            }
        }

        bool AllSeedsSettled()
        {
            foreach (var s in _seeds)
                if (s.Body != null && !s.Body.IsSettled) return false;

            return true;
        }

        /// <summary>
        /// How many seeds are already piled in this hole, counting this one. Grows the array to
        /// fit rather than trusting it to be the right length: an out-of-range here would throw
        /// inside the drop coroutine, which would leave the input locked and the board dead.
        /// </summary>
        int NextPileSlot(int hole)
        {
            if (_holePile == null || _holePile.Length <= hole)
            {
                System.Array.Resize(ref _holePile, hole + 1);
            }

            return _holePile[hole]++;
        }

        void ClearSeeds()
        {
            // Anything still waiting to be thrown belongs to the board being cleared away.
            _throwQueue.Clear();
            if (_thrower != null) { StopCoroutine(_thrower); _thrower = null; }

            foreach (var s in _seeds)
                if (s.Obj != null) Destroy(s.Obj.gameObject);
            _seeds.Clear();

            int holes = _session.HoleCount;
            if (_holePile == null || _holePile.Length != holes) _holePile = new int[holes];
            else System.Array.Clear(_holePile, 0, _holePile.Length);
            System.Array.Clear(_storePile, 0, _storePile.Length);
        }

        /// <summary>
        /// Enable the hand, minus whatever is already on its way to the server. Per-card rather
        /// than wholesale, so a state patch arriving mid-burst cannot hand a played card back to
        /// the player and let them play it twice.
        /// </summary>
        void SetCardsInteractable(bool value)
        {
            foreach (var card in _cards)
                if (card != null) card.SetInteractable(value && !_pending.Contains(card.SeedId));
        }

        /// <summary>
        /// Puts a line up and takes it down again. A toast that never cleared would leave the
        /// reason for a refusal sitting over a board that has moved on several drops since, so
        /// each message restarts the same timer rather than stacking one coroutine per refusal.
        /// </summary>
        void ShowToast(string message)
        {
            if (toastLabel == null) return;

            toastLabel.text = message;

            // The frame carries a background, so an empty string is not enough to make it go
            // away — an idle board would keep an empty bar floating over it. Visibility is the
            // toast's own state, and only a refusal turns it on.
            GameObject frame = ToastFrame;
            if (frame != null) frame.SetActive(true);

            if (_toastTimer != null) StopCoroutine(_toastTimer);

            // Disabled or mid-teardown there is no coroutine to run: the text is set, which is
            // all a toast promises, and the next Attach clears it.
            _toastTimer = isActiveAndEnabled && toastSeconds > 0f
                ? StartCoroutine(ClearToastAfter(toastSeconds))
                : null;
        }

        IEnumerator ClearToastAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            HideToast();
            _toastTimer = null;
        }

        /// <summary>The object switched on and off with the toast: its frame, or the label itself.</summary>
        GameObject ToastFrame =>
            toastRoot != null ? toastRoot
            : toastLabel != null ? toastLabel.gameObject
            : null;

        void HideToast()
        {
            if (toastLabel != null) toastLabel.text = string.Empty;

            GameObject frame = ToastFrame;
            if (frame != null) frame.SetActive(false);
        }

        void ShowGameOver()
        {
            if (highlightMarker != null) highlightMarker.gameObject.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (gameOverLabel == null) return;

            int? w = _session.Winner;
            string outcome = w.HasValue ? $"{_session.DisplayNameOf(w.Value)} menang!" : "Seri!";

            // The jingle is for a win on this device: online, only the winner's; offline both
            // players share the screen (MySeat there is just whose turn it is), so any win.
            if (w.HasValue && (w.Value == _session.MySeat || _session is LocalDakonSession))
            {
                Museum.Core.GameAudio.PlayWin();
            }

            gameOverLabel.text = $"{outcome}\n{_session.DisplayNameOf(0)} {_session.Total(0)}  –  " +
                                 $"{_session.DisplayNameOf(1)} {_session.Total(1)}";
        }
    }
}
