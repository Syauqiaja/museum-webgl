namespace Museum.Lobby
{
    /// <summary>
    /// Failure codes the lobby can raise, and the player-facing text for each.
    /// <see cref="RoomNotFound"/> and <see cref="RoomFull"/> mirror the shared server codes in
    /// <see cref="Museum.Core.ErrorCode"/> and the failure table in Assets/Docs/ui-flow.md; the
    /// other four (<see cref="NameInvalid"/>, <see cref="ConnectionFailed"/>,
    /// <see cref="NotHost"/>, <see cref="NotEnoughPlayers"/>) are client-side and never cross the
    /// wire.
    ///
    /// Codes, not sentences, cross the service boundary — the wording lives here so it is changed
    /// in one place and can be localised later.
    /// </summary>
    public static class LobbyError
    {
        public const string RoomNotFound = "room_not_found";
        public const string RoomFull = "room_full";
        public const string NameInvalid = "name_invalid";
        public const string ConnectionFailed = "connection_failed";
        public const string NotHost = "not_host";
        public const string NotEnoughPlayers = "not_enough_players";
        public const string AlreadyStarted = "already_started";

        public static string MessageFor(string code)
        {
            switch (code)
            {
                case RoomNotFound: return "Room not found";
                case RoomFull: return "Room is full";
                case NameInvalid: return "Enter a name (2–16 characters)";
                case ConnectionFailed: return "Can't reach the server";
                case NotHost: return "Only the host can start";
                case NotEnoughPlayers: return "Need at least 2 players";
                case AlreadyStarted: return "That match already started";
                default: return "Something went wrong";
            }
        }
    }
}
