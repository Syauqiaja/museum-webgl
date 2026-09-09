using System.Collections.Generic;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Which lane each player races in. Lanes are the server's seat numbers, identically on every
    /// screen: the racer in lane 2 is the same person for all three players, which is what lets one
    /// client talk about another's position at all — and what a spectator view would need.
    ///
    /// Plain C# rather than a MonoBehaviour so the mapping can be tested without a scene.
    /// </summary>
    public sealed class EgrangSeating
    {
        readonly Dictionary<string, int> _laneBySession = new Dictionary<string, int>();
        readonly Dictionary<int, string> _sessionByLane = new Dictionary<int, string>();
        readonly Dictionary<string, string> _nameBySession = new Dictionary<string, string>();
        string _local = string.Empty;

        /// <summary>How many seats are filled.</summary>
        public int Count => _laneBySession.Count;

        /// <summary>The local player's lane, or -1 before the local session is known or seated.</summary>
        public int LocalLane => LaneOf(_local);

        /// <summary>Seats a session. Re-seating an existing session moves it rather than duplicating it.</summary>
        public void Assign(string sessionId, int seat)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            if (_laneBySession.TryGetValue(sessionId, out int previous)) _sessionByLane.Remove(previous);

            if (_sessionByLane.TryGetValue(seat, out string priorOccupant) && priorOccupant != sessionId)
                _laneBySession.Remove(priorOccupant);

            _laneBySession[sessionId] = seat;
            _sessionByLane[seat] = sessionId;
        }

        /// <summary>Marks which session is this client. Safe to call before the seat is known.</summary>
        public void SetLocal(string sessionId) => _local = sessionId ?? string.Empty;

        /// <summary>The lane a session races in, or -1 if it is not seated.</summary>
        public int LaneOf(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return -1;
            return _laneBySession.TryGetValue(sessionId, out int lane) ? lane : -1;
        }

        /// <summary>Who races in a lane, or null if it is empty.</summary>
        public string SessionAt(int lane) => _sessionByLane.TryGetValue(lane, out string sessionId) ? sessionId : null;

        /// <summary>True when this session is the local player. False before the local id is set.</summary>
        public bool IsLocal(string sessionId) =>
            !string.IsNullOrEmpty(sessionId) && sessionId == _local;

        /// <summary>
        /// Remembers what a player is called. Kept here beside the seating rather than in a second
        /// map elsewhere: everything on screen that names a racer starts from a lane, and this is
        /// the class that turns a lane into a person.
        /// </summary>
        public void SetName(string sessionId, string displayName)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            _nameBySession[sessionId] = displayName ?? string.Empty;
        }

        /// <summary>The name of the player in a lane, or empty when the lane is free or unnamed.</summary>
        public string NameOf(int lane)
        {
            string sessionId = SessionAt(lane);
            if (string.IsNullOrEmpty(sessionId)) return string.Empty;

            return _nameBySession.TryGetValue(sessionId, out string name) ? name ?? string.Empty : string.Empty;
        }

        /// <summary>The name behind a session id, or empty when it is unknown.</summary>
        public string NameOfSession(string sessionId) =>
            !string.IsNullOrEmpty(sessionId) && _nameBySession.TryGetValue(sessionId, out string name)
                ? name ?? string.Empty
                : string.Empty;
    }
}
