namespace Museum.Core
{
    /// <summary>
    /// How the visitor is playing. Chosen by the visitor on MainMenu's first screen, never by
    /// the build: a browser probe can only recommend (see <see cref="PlatformDetect"/>), because
    /// a wrong guess would hand someone controls they cannot use.
    /// </summary>
    public enum ControlScheme
    {
        /// <summary>Nobody has chosen yet. Everything that reads this treats it as Desktop.</summary>
        Unknown = 0,

        /// <summary>Phone or tablet: on-screen joystick, drag to look, tap buttons.</summary>
        Sentuh = 1,

        /// <summary>Keyboard and mouse, with pointer lock.</summary>
        Desktop = 2,
    }
}
