namespace Museum.Core
{
    /// <summary>The scene tracks in <c>Assets/Sounds</c>.</summary>
    public enum MusicTrack
    {
        None,
        Museum,
        Dakon,
        Egrang,
    }

    /// <summary>
    /// Which track a scene plays. Pure, so the choice is testable without audio.
    /// </summary>
    /// <remarks>
    /// MainMenu shares the museum's track — there is no menu track, and the welcome screen is the
    /// museum's front door. The Lobby plays the music of the game its doorway leads to, so the
    /// track that starts while players gather carries on unbroken into the match; a lobby opened
    /// any other way plays the museum's. A scene not listed (a test scene) plays nothing.
    /// </remarks>
    public static class SceneMusic
    {
        public static MusicTrack For(string sceneName, string lobbyGameScene)
        {
            switch (sceneName)
            {
                case SceneReference.MainMenu:
                case SceneReference.Museum:
                    return MusicTrack.Museum;
                case SceneReference.Dakon:
                    return MusicTrack.Dakon;
                case SceneReference.Egrang:
                    return MusicTrack.Egrang;
                case SceneReference.Lobby:
                    if (lobbyGameScene == SceneReference.Dakon) return MusicTrack.Dakon;
                    if (lobbyGameScene == SceneReference.Egrang) return MusicTrack.Egrang;
                    return MusicTrack.Museum;
                default:
                    return MusicTrack.None;
            }
        }
    }
}
