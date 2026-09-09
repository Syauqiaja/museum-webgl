using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Museum.Lobby
{
    /// <summary>
    /// An in-memory room. It exists because the Node server has no `egrang` room yet
    /// (Assets/Docs/games/egrang.md is a stub) and the lobby screen should not wait on it: with
    /// this, every slot, host migration, the full-room rejection and every error toast are
    /// playable and unit-testable with nothing running.
    ///
    /// It is also the contract. Whatever the server room ends up doing, it has to agree with
    /// FakeLobbyServiceTests — that is the point of writing the rules down here rather than
    /// inside a MonoBehaviour.
    ///
    /// Not thread-safe and not meant to be: everything happens on Unity's main thread. The
    /// methods are async only to match <see cref="ILobbyService"/>, whose real implementation
    /// awaits the network.
    /// </summary>
    public sealed class FakeLobbyService : ILobbyService
    {
        // The one room this fake knows about. A single fake client can only be in one room, and
        // simulated peers exist only inside it.
        private List<LobbySlot> _slots;
        private string _code = string.Empty;
        private string _mySessionId = string.Empty;
        private string _hostSessionId = string.Empty;
        private LobbyPhase _phase = LobbyPhase.Waiting;
        private bool _inRoom;

        private readonly Random _random;
        private int _nextSessionNumber;
        private string _forcedFailure;

        public FakeLobbyService(int seed = 0)
        {
            _random = seed == 0 ? new Random() : new Random(seed);
        }

        public LobbyRoomSnapshot Current { get; private set; }

        public event Action<LobbyRoomSnapshot> RoomUpdated;
        public event Action<string, string> Failed;

        public Task CreateRoom(string roomName, string displayName, int maxPlayers)
        {
            if (ConsumeForcedFailure() || !SeatIsNamed(displayName))
            {
                return Task.CompletedTask;
            }

            _slots = new List<LobbySlot>(maxPlayers);
            for (int i = 0; i < maxPlayers; i++)
            {
                _slots.Add(LobbySlot.Empty);
            }

            _code = RoomCode.Generate(_random);
            _phase = LobbyPhase.Waiting;
            _inRoom = true;

            _mySessionId = NextSessionId();
            _hostSessionId = _mySessionId;
            _slots[0] = LobbySlot.Occupied(_mySessionId, Museum.Core.PlayerNameRules.Sanitize(displayName), true);

            Publish();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Joining a code the fake did not mint cannot work — there is no directory of rooms to
        /// look it up in. Only the code from a room this instance created is accepted, which is
        /// enough to walk the join screen; two real clients need the server.
        /// </summary>
        public Task JoinRoom(string code, string displayName)
        {
            if (ConsumeForcedFailure() || !SeatIsNamed(displayName))
            {
                return Task.CompletedTask;
            }

            string sanitized = RoomCode.Sanitize(code);
            if (_slots == null || sanitized.Length == 0 || sanitized != _code)
            {
                Fail(LobbyError.RoomNotFound);
                return Task.CompletedTask;
            }

            int seat = FirstEmptySeat();
            if (seat < 0)
            {
                Fail(LobbyError.RoomFull);
                return Task.CompletedTask;
            }

            _inRoom = true;
            _mySessionId = NextSessionId();
            _slots[seat] = LobbySlot.Occupied(_mySessionId, Museum.Core.PlayerNameRules.Sanitize(displayName), false);

            Publish();
            return Task.CompletedTask;
        }

        public Task StartGame()
        {
            if (ConsumeForcedFailure() || Current == null)
            {
                return Task.CompletedTask;
            }

            if (!Current.AmHost)
            {
                Fail(LobbyError.NotHost);
                return Task.CompletedTask;
            }

            if (Current.OccupiedCount < LobbyRoomSnapshot.MinPlayersToStart)
            {
                Fail(LobbyError.NotEnoughPlayers);
                return Task.CompletedTask;
            }

            _phase = LobbyPhase.InProgress;
            Publish();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Vacates this client's seat but keeps the room and its simulated peers alive, so the
        /// create-then-join walkthrough works: leave the room you made, join it back by code.
        /// </summary>
        /// <summary>
        /// Nothing to hand over: this lobby has no socket, and the fake room can simply stay in
        /// memory for as long as this service does.
        /// </summary>
        public void HandOffToGameScene()
        {
        }

        public Task Leave()
        {
            if (_inRoom && _slots != null)
            {
                Vacate(_mySessionId);
            }

            _inRoom = false;
            _mySessionId = string.Empty;
            Current = null;
            return Task.CompletedTask;
        }

        // ---- dev / test helpers: stand in for other people joining ----

        /// <summary>Seat a simulated peer. Returns their session id, or empty if the room was full.</summary>
        public string AddSimulatedPlayer(string displayName)
        {
            if (_slots == null)
            {
                Fail(LobbyError.RoomNotFound);
                return string.Empty;
            }

            int seat = FirstEmptySeat();
            if (seat < 0)
            {
                Fail(LobbyError.RoomFull);
                return string.Empty;
            }

            string sessionId = NextSessionId();
            _slots[seat] = LobbySlot.Occupied(sessionId, Museum.Core.PlayerNameRules.Sanitize(displayName),
                isHost: _hostSessionId.Length == 0);

            if (_hostSessionId.Length == 0)
            {
                _hostSessionId = sessionId;
            }

            Publish();
            return sessionId;
        }

        /// <summary>Remove a seated player by session id, migrating the host if they were it.</summary>
        public void RemoveSimulatedPlayer(string sessionId)
        {
            if (_slots == null)
            {
                return;
            }

            Vacate(sessionId);
            Publish();
        }

        /// <summary>Make the next Create/Join/Start fail with this code, to exercise the error toasts.</summary>
        public void ForceNextFailure(string errorCode)
        {
            _forcedFailure = errorCode;
        }

        // ---- internals ----

        private bool SeatIsNamed(string displayName)
        {
            if (Museum.Core.PlayerNameRules.IsValid(displayName))
            {
                return true;
            }

            Fail(LobbyError.NameInvalid);
            return false;
        }

        private bool ConsumeForcedFailure()
        {
            if (_forcedFailure == null)
            {
                return false;
            }

            string code = _forcedFailure;
            _forcedFailure = null;
            Fail(code);
            return true;
        }

        private int FirstEmptySeat()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Vacate(string sessionId)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].SessionId == sessionId)
                {
                    _slots[i] = LobbySlot.Empty;
                    break;
                }
            }

            if (_hostSessionId != sessionId)
            {
                return;
            }

            // Host left: the lowest remaining seat inherits it, so the room never becomes
            // unstartable just because the creator closed their tab.
            _hostSessionId = string.Empty;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    _hostSessionId = _slots[i].SessionId;
                    _slots[i] = LobbySlot.Occupied(_slots[i].SessionId, _slots[i].DisplayName, true);
                    break;
                }
            }
        }

        private string NextSessionId() => $"sim{++_nextSessionNumber}";

        private void Publish()
        {
            Current = new LobbyRoomSnapshot(_code, new List<LobbySlot>(_slots), _phase, _mySessionId,
                _hostSessionId);
            RoomUpdated?.Invoke(Current);
        }

        private void Fail(string code)
        {
            Failed?.Invoke(code, LobbyError.MessageFor(code));
        }
    }
}
