namespace Museum.Lobby
{
    /// <summary>
    /// One seat in a lobby room: who is sitting in it, or nobody.
    ///
    /// A seat is a value, not a live object — the UI re-renders from whole snapshots rather than
    /// mutating slots in place, so an empty seat is a real <see cref="LobbySlot"/> with no
    /// session rather than a null the callers have to guard.
    /// </summary>
    public readonly struct LobbySlot
    {
        /// <summary>Server session holding the seat, or empty when nobody is.</summary>
        public readonly string SessionId;

        /// <summary>Name to show against the seat. Empty for an empty seat.</summary>
        public readonly string DisplayName;

        /// <summary>Whether this seat's player is the one who may start the match.</summary>
        public readonly bool IsHost;

        private LobbySlot(string sessionId, string displayName, bool isHost)
        {
            SessionId = sessionId;
            DisplayName = displayName;
            IsHost = isHost;
        }

        /// <summary>An unoccupied seat.</summary>
        public static LobbySlot Empty => new LobbySlot(string.Empty, string.Empty, false);

        /// <summary>A seat held by a player.</summary>
        public static LobbySlot Occupied(string sessionId, string displayName, bool isHost) =>
            new LobbySlot(sessionId ?? string.Empty, displayName ?? string.Empty, isHost);

        public bool IsEmpty => string.IsNullOrEmpty(SessionId);
    }
}
