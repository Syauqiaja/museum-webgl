namespace Museum.Core
{
    /// <summary>
    /// What the launcher hands to the Lobby scene: which room to open and under
    /// what name. Loading a scene can't take arguments, so the request is parked
    /// in <see cref="Pending"/> and picked up by the lobby on Awake.
    /// </summary>
    public sealed class LobbyRequest
    {
        private static LobbyRequest _pending;

        /// <summary>Set by the launcher just before loading the Lobby scene.</summary>
        public static LobbyRequest Pending
        {
            get => _pending;
            set
            {
                _pending = value;
                if (value != null) LastGameScene = value.GameScene;
            }
        }

        /// <summary>
        /// The game scene the most recent request led to. Outlives <see cref="Pending"/>, which the
        /// lobby clears the moment it reads it — <see cref="GameAudio"/> hears about the lobby
        /// loading after that, and plays this game's music there.
        /// </summary>
        public static string LastGameScene { get; private set; } = string.Empty;

        public string RoomName { get; }
        public int MaxPlayers { get; }
        public string GameScene { get; }
        public string DisplayName { get; }

        public LobbyRequest(string roomName, int maxPlayers, string gameScene, string displayName)
        {
            RoomName = roomName;
            MaxPlayers = maxPlayers;
            GameScene = gameScene;
            DisplayName = displayName;
        }
    }
}
