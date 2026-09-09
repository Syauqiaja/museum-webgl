using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.Core
{
    /// <summary>
    /// What the browser says about the device. Advisory only: the single caller is MainMenu's
    /// platform picker, and all it does with the answer is mark one button "Disarankan". The
    /// visitor's tap is what sets <see cref="SessionData.Scheme"/>.
    /// </summary>
    /// <remarks>
    /// Outside a WebGL player — the Editor, tests — the jslib is not linked, so the check falls
    /// back to Unity's own signals. That fallback is what the EditMode tests exercise.
    /// </remarks>
    public static class PlatformDetect
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int MuseumIsTouchDevice();
        [DllImport("__Internal")] private static extern void MuseumRequestFullscreen();
#endif

        /// <summary>True when the browser reports a touch device. Never authoritative.</summary>
        public static bool LooksLikeTouchDevice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return MuseumIsTouchDevice() == 1;
#else
            return Application.isMobilePlatform || Touchscreen.current != null;
#endif
        }

        /// <summary>
        /// Asks the browser for fullscreen. Only meaningful inside a user gesture, which is why
        /// the picker's button tap is the one place that calls it. A no-op everywhere else.
        /// </summary>
        public static void RequestFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MuseumRequestFullscreen();
#endif
        }
    }
}
