namespace Museum.Core
{
    /// <summary>
    /// Scene names as constants, so the loader calls and the Build Settings list
    /// can't drift apart from a typo in a string literal.
    /// </summary>
    public static class SceneReference
    {
        public const string MainMenu = "MainMenu";
        public const string Museum = "Museum";
        public const string Lobby = "Lobby";
        public const string Dakon = "Dakon";
        public const string Egrang = "Egrang";
    }
}
