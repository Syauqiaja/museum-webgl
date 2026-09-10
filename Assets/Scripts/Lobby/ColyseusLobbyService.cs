using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Museum.Core;
using Museum.Net.State;

namespace Museum.Lobby
{
    /// <summary>
    /// The real transport: a Colyseus room behind <see cref="ILobbyService"/>.
    ///
    /// It reads only the base lobby fields every game's state inherits (phase, host, players), so
    /// one implementation fronts every game; <see cref="RoomSession"/> handles the part that does
    /// need the concrete schema. Nothing above this line changes — LobbyController still renders
    /// whole snapshots and never mutates one.
    ///
    /// **How the room reaches the game scene.** This service owns a live room that dies with the
    /// lobby scene, and Unity's scene API has no channel for handing a live object across a load.
    /// The handoff is therefore a *reconnect*, not a handover: <see cref="ColyseusNetManager"/>
    /// caches the room's ReconnectionToken into <see cref="SessionData"/> as soon as the room
    /// opens, and the game scene reconnects with it — landing back in the same seat instead of
    /// joining a second room. Which is why <see cref="Leave"/> runs only when the player actually
    /// walks out, never on the way into the game.
    /// </summary>
    public sealed class ColyseusLobbyService : ILobbyService
    {
        /// <summary>Server room name ("dakon", "egrang") — fixed for the lobby's lifetime.</summary>
        private readonly string _roomName;

        private RoomSession _session;

        /// <summary>Seats the doorway asked for, so empty slots render before anyone joins.</summary>
        private int _maxPlayers = 2;

        public ColyseusLobbyService(string roomName)
        {
            _roomName = roomName;
        }

        public LobbyRoomSnapshot Current { get; private set; }

        public event Action<LobbyRoomSnapshot> RoomUpdated;
        public event Action<string, string> Failed;

        public Task CreateRoom(string roomName, string displayName, int maxPlayers)
        {
            _maxPlayers = Math.Max(1, maxPlayers);

            // Private: a created room is reachable by its code only, never handed to the next
            // stranger who presses Join — the server excludes private rooms from public
            // matchmaking (server docs/room-system.md).
            return Open(displayName, options => RoomSession.Create(roomName, options));
        }

        public Task JoinRoom(string code, string displayName)
        {
            string sanitized = RoomCode.Sanitize(code);
            return Open(displayName, options => RoomSession.JoinById(_roomName, sanitized, options));
        }

        /// <summary>
        /// Host-only, and the server says so: it validates the request and answers a rejection
        /// with an `error` message, which surfaces through <see cref="Failed"/> like any other.
        /// </summary>
        public Task StartGame()
        {
            _session?.Send("start_game");
            return Task.CompletedTask;
        }

        /// <summary>
        /// The match is starting and this scene is about to unload: give the room to the
        /// bootstrap singleton so the game scene can keep playing on the very same socket.
        ///
        /// Not a <see cref="Leave"/> — the seat is not being given up — and not a no-op either.
        /// Dropping the room here and reconnecting by token in the game scene is what left both
        /// players staring at an empty board: the socket is still open, so the server has no
        /// reconnection pending, refuses the second connection (4002), and the game scene binds
        /// a room that never receives a single patch.
        /// </summary>
        public void HandOffToGameScene()
        {
            RoomSession session = _session;

            // Same order as Leave: cleared first, so the close this lobby will never hear about
            // cannot be reported as a lost connection.
            _session = null;
            Current = null;

            session?.HandOffToGameScene();
        }

        public async Task Leave()
        {
            RoomSession session = _session;

            // Cleared first so the resulting close is not reported as a lost connection.
            _session = null;
            Current = null;

            if (session == null)
            {
                return;
            }

            try
            {
                // Consented: frees the seat now rather than holding it for the reconnect window,
                // which is what "I walked out" should mean to the other player.
                await session.Leave(true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Leaving the lobby room threw (already closed?): {e.Message}");
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.ClearRoomSession();
            }
        }

        // --- opening a room ------------------------------------------------

        /// <summary>
        /// Create or join, then wire the room up. Both paths share everything after the call
        /// itself, failure mapping included — a player gets "room not found" for the same reason
        /// whichever button they pressed.
        /// </summary>
        private async Task Open(string displayName, Func<Dictionary<string, object>, Task<RoomSession>> open)
        {
            if (ColyseusNetManager.Instance == null)
            {
                Fail(LobbyError.ConnectionFailed, "No ColyseusNetManager in the scene.");
                return;
            }

            if (!RoomSession.Supports(_roomName))
            {
                Fail(LobbyError.ConnectionFailed, $"This build has no schema for the '{_roomName}' room.");
                return;
            }

            var options = new Dictionary<string, object>
            {
                { "displayName", displayName },
                { "private", true },
                // Cosmetic; the server sanitises it and the Egrang lanes render it.
                { "avatar", SessionData.Instance != null ? SessionData.Instance.PlayerAvatar : PlayerAvatars.Default },
            };

            if (SessionData.Instance != null && SessionData.Instance.PlayerId.Length > 0)
            {
                // Stats key only — the server never treats it as authorisation.
                options["playerId"] = SessionData.Instance.PlayerId;
            }

            try
            {
                _session = await open(options);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Lobby room open failed: {e}");
                Fail(MapOpenFailure(e), null);
                return;
            }

            _session.Changed += Publish;
            _session.ServerError += OnServerError;
            _session.Closed += OnClosed;

            Publish();
        }

        // --- events --------------------------------------------------------

        /// <summary>Turn current room state into a whole snapshot, per the ILobbyService contract.</summary>
        private void Publish()
        {
            RoomSession session = _session;
            if (session?.State == null)
            {
                return;
            }

            Current = Snapshot(session);
            RoomUpdated?.Invoke(Current);
        }

        private LobbyRoomSnapshot Snapshot(RoomSession session)
        {
            BaseGameState state = session.State;

            var bySeat = new Dictionary<int, BasePlayer>();
            int highestSeat = -1;

            // `players` stays null until the decoder places the first map patch. Open() publishes
            // once before that patch arrives, so an empty board here is normal, not an error.
            state.players?.ForEach((sessionId, player) =>
            {
                bySeat[player.seat] = player;
                highestSeat = Math.Max(highestSeat, player.seat);
            });

            // Seat index, not map order: the server assigns a stable seat and every client must
            // render the same player in the same slot.
            int seats = Math.Max(_maxPlayers, highestSeat + 1);
            var slots = new List<LobbySlot>(seats);

            for (int i = 0; i < seats; i++)
            {
                slots.Add(bySeat.TryGetValue(i, out BasePlayer player)
                    ? LobbySlot.Occupied(player.sessionId, player.displayName,
                        player.sessionId == state.hostSessionId)
                    : LobbySlot.Empty);
            }

            return new LobbyRoomSnapshot(
                session.RoomId,
                slots,
                PhaseFrom(state.phase),
                session.SessionId,
                state.hostSessionId);
        }

        /// <summary>
        /// "finished" maps to InProgress rather than a phase of its own: to the lobby both mean
        /// "not a room you can still be waiting in", and the controller's only reaction to
        /// InProgress — load the game scene — is the right one for a match that ended while the
        /// player was still looking at the room panel.
        /// </summary>
        private static LobbyPhase PhaseFrom(string phase)
        {
            switch (phase)
            {
                case "in_progress":
                case "finished":
                    return LobbyPhase.InProgress;
                default:
                    return LobbyPhase.Waiting;
            }
        }

        /// <summary>An `error` message from the server — a rejected start_game, mostly.</summary>
        private void OnServerError(string code, string message)
        {
            Failed?.Invoke(code, string.IsNullOrEmpty(message) ? LobbyError.MessageFor(code) : message);
        }

        /// <summary>
        /// The socket closed while we were seated. Only reported when the player did not ask for
        /// it: <see cref="Leave"/> clears the session first, so a deliberate exit is silent.
        /// </summary>
        private void OnClosed()
        {
            if (_session == null)
            {
                return;
            }

            _session = null;
            Current = null;
            Fail(LobbyError.ConnectionFailed, null);
        }

        private void Fail(string code, string message)
        {
            Failed?.Invoke(code, message ?? LobbyError.MessageFor(code));
        }

        /// <summary>
        /// Colyseus reports a refused create/join as an exception whose text carries the reason,
        /// so the mapping is by inspection. Anything unrecognised is reported as a connection
        /// problem rather than guessed at — "room not found" for a server that is simply down
        /// would send the player back to retype a code that was already correct.
        /// </summary>
        private static string MapOpenFailure(Exception e)
        {
            string text = (e.Message ?? string.Empty).ToLowerInvariant();

            if (text.Contains("already_started"))
            {
                return LobbyError.AlreadyStarted;
            }

            if (text.Contains("not found") || text.Contains("expired") || text.Contains("invalid room"))
            {
                return LobbyError.RoomNotFound;
            }

            if (text.Contains("locked") || text.Contains("full"))
            {
                return LobbyError.RoomFull;
            }

            return LobbyError.ConnectionFailed;
        }
    }
}
