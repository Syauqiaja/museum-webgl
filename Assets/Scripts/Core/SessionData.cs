using Colyseus;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Persistent per-visit identity: the nickname typed on MainMenu, the session id the
    /// server gave this client's seat, and the reconnection token that seat is reclaimed with.
    /// Lives on the same DontDestroyOnLoad bootstrap object as <see cref="SceneLoader"/> and
    /// <see cref="ColyseusNetManager"/>, and follows the same singleton pattern.
    ///
    /// Every multiplayer minigame reads its seat from here rather than from the room object:
    /// the lobby's room does not survive the scene load into the game (see
    /// <c>ColyseusLobbyService</c>), so the identity has to outlive it.
    ///
    /// What survives what:
    /// <list type="bullet">
    /// <item><see cref="PlayerName"/> — written to PlayerPrefs, so it survives a tab refresh
    /// and the visitor is not asked twice.</item>
    /// <item><see cref="SessionId"/> / <see cref="ReconnectionToken"/> — memory only. Both are
    /// issued per connection: a stored copy would name a seat that no longer exists, and the
    /// client would try to reclaim it instead of joining cleanly.</item>
    /// <item><see cref="Scheme"/> — memory only. This build runs as a museum kiosk; the next
    /// visitor may not be on the device the last one was.</item>
    /// </list>
    ///
    /// This is deliberately dumb storage — no networking, no validation policy of its own
    /// beyond running names through <see cref="PlayerNameRules"/> so a bad value can never be
    /// stored in the first place.
    /// </summary>
    public class SessionData : MonoBehaviour
    {
        /// <summary>PlayerPrefs key for the nickname. Namespaced so it cannot collide with a game's own key.</summary>
        private const string PlayerNameKey = "museum.session.playerName";

        /// <summary>PlayerPrefs key for the stable profile id.</summary>
        private const string PlayerIdKey = "museum.session.playerId";

        public static SessionData Instance { get; private set; }

        private string _playerName = string.Empty;

        /// <summary>
        /// Nickname, always stored sanitized and mirrored into PlayerPrefs. Empty until the
        /// player enters one.
        /// </summary>
        public string PlayerName
        {
            get => _playerName;
            set
            {
                string sanitized = PlayerNameRules.Sanitize(value);

                if (sanitized == _playerName)
                {
                    return;
                }

                _playerName = sanitized;
                PlayerPrefs.SetString(PlayerNameKey, _playerName);

                // Saved eagerly: WebGL flushes PlayerPrefs to IndexedDB asynchronously and a
                // museum visitor may close the tab seconds after typing their name.
                PlayerPrefs.Save();
            }
        }

        /// <summary>True once a usable nickname has been entered; drives the lobby's fallback prompt.</summary>
        public bool HasPlayerName => _playerName.Length >= PlayerNameRules.MinLength;

        /// <summary>
        /// Stable profile id for this browser/kiosk, minted once and kept in PlayerPrefs. Sent
        /// as the <c>playerId</c> join option so the server can keep lifetime stats (games
        /// played, wins) across visits — see the server's docs/database.md.
        ///
        /// It is not a credential and the server treats it as one: it decides which stats row
        /// is updated, never what this client is allowed to do in a room. Clearing site data
        /// mints a new one, which loses the history and nothing else.
        /// </summary>
        public string PlayerId { get; private set; } = string.Empty;

        /// <summary>
        /// The control scheme the visitor picked on MainMenu. Memory only, deliberately: this
        /// build runs as a museum kiosk, so the next visitor may not be on the device the last
        /// one was, and a remembered answer would be worse than asking.
        /// </summary>
        public ControlScheme Scheme { get; set; } = ControlScheme.Unknown;

        /// <summary>True only once the visitor has explicitly chosen touch.</summary>
        public bool IsTouch => Scheme == ControlScheme.Sentuh;

        /// <summary>Last room code the player created or typed, so a failed join keeps it on screen.</summary>
        public string LastRoomCode { get; set; } = string.Empty;

        /// <summary>
        /// This client's seat in the current room, as the server names it (Colyseus
        /// <c>Room.SessionId</c>; the fake lobby mints the same shape). Empty when not in a room.
        /// A game scene compares it against the ids in its state to know which side it plays.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>True while this client holds a seat.</summary>
        public bool HasSession => !string.IsNullOrEmpty(SessionId);

        /// <summary>
        /// Server room name of the current room ("dakon", "egrang") — the wire identifier, not
        /// the shareable code. Needed to re-open the right room type on a reconnect.
        /// </summary>
        public string RoomName { get; private set; } = string.Empty;

        /// <summary>
        /// Token that reclaims <see cref="SessionId"/> after a dropped socket or a scene load,
        /// within the server's <c>allowReconnection</c> window (networking.md §7). Null when
        /// there is nothing to reclaim.
        /// </summary>
        public ReconnectionToken ReconnectionToken { get; private set; }

        /// <summary>True when a reconnect can be attempted — a room was opened and not left.</summary>
        public bool CanReconnect => ReconnectionToken != null;

        /// <summary>
        /// Record the seat a create/join just produced. Called by <see cref="ColyseusNetManager"/>
        /// for real rooms; the lobby sets <see cref="SessionId"/> and <see cref="LastRoomCode"/>
        /// directly while it is still running on the fake service (no token to record).
        /// </summary>
        public void SetRoomSession(string roomName, string roomCode, string sessionId, ReconnectionToken token)
        {
            RoomName = roomName ?? string.Empty;
            LastRoomCode = roomCode ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            ReconnectionToken = token;
        }

        /// <summary>
        /// Forget the seat on an explicit leave. <see cref="LastRoomCode"/> is deliberately kept:
        /// it is the code sitting in the join field, not part of the seat.
        /// </summary>
        public void ClearRoomSession()
        {
            RoomName = string.Empty;
            SessionId = string.Empty;
            ReconnectionToken = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Only this component goes, not the GameObject: the bootstrap object carries
                // SceneLoader and ColyseusNetManager too, and destroying it would take the
                // scene's whole bootstrap — including singletons that had not run Awake yet.
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _playerName = PlayerNameRules.Sanitize(PlayerPrefs.GetString(PlayerNameKey, string.Empty));
            PlayerId = LoadOrMintPlayerId();
        }

        /// <summary>
        /// Read the stored profile id, or mint one on first run. Written in the server's
        /// expected form (lowercase 36-char GUID); anything else it receives is ignored.
        /// </summary>
        private static string LoadOrMintPlayerId()
        {
            string stored = PlayerPrefs.GetString(PlayerIdKey, string.Empty);

            if (stored.Length == 36)
            {
                return stored;
            }

            string minted = System.Guid.NewGuid().ToString("D").ToLowerInvariant();
            PlayerPrefs.SetString(PlayerIdKey, minted);
            PlayerPrefs.Save();
            return minted;
        }
    }
}
