using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// MainMenu's two screens: the platform picker the visitor sees first, and the name-and-play
    /// menu behind it.
    /// </summary>
    /// <remarks>
    /// The picker exists because no browser probe is trustworthy enough to hand someone controls
    /// they cannot use. <see cref="PlatformDetect"/> only decides which of the two buttons wears
    /// the "Disarankan" tag; the tap decides the rest.
    ///
    /// The menu is hidden by a list of objects rather than by one parent panel because MainMenu's
    /// hierarchy survived the 2026-08-19 asset loss intact and MainMenuUIBuilder adopts it rather
    /// than replacing it (ui-style.md §9). Re-parenting those objects would undo that.
    ///
    /// <c>ChooseTouch</c>, <c>ChooseDesktop</c> and <c>GoToMuseum</c> are wired into the scene by
    /// name and are API (CLAUDE.md). Do not rename them.
    /// </remarks>
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("The full-screen 'Kamu main pakai apa?' panel. Shown first, dismissed on a choice.")]
        [SerializeField] private GameObject platformPanel;

        [Tooltip("Everything behind the picker — Title, Name Input, Play Button. Hidden until a choice is made.")]
        [SerializeField] private GameObject[] menuObjects = new GameObject[0];

        [Tooltip("The 'Disarankan' tag on the touch button. Shown only when the browser says touch.")]
        [SerializeField] private GameObject touchHint;

        [Tooltip("The 'Disarankan' tag on the desktop button. Shown only when the browser says desktop.")]
        [SerializeField] private GameObject desktopHint;

        private void Awake()
        {
            ShowPicker();
        }

        /// <summary>Wires the screen from script. The builder writes the same four references.</summary>
        public void Configure(GameObject picker, GameObject[] menu, GameObject touchTag, GameObject desktopTag)
        {
            platformPanel = picker;
            menuObjects = menu ?? new GameObject[0];
            touchHint = touchTag;
            desktopHint = desktopTag;
            ShowPicker();
        }

        /// <summary>UnityEvent target — API, wired by name. Do not rename.</summary>
        public void ChooseTouch() => Choose(ControlScheme.Sentuh);

        /// <summary>UnityEvent target — API, wired by name. Do not rename.</summary>
        public void ChooseDesktop() => Choose(ControlScheme.Desktop);

        /// <summary>UnityEvent target — API, wired by name. Do not rename.</summary>
        public void GoToMuseum()
        {
            SceneLoader.Instance.LoadScene(SceneReference.Museum);
        }

        private void ShowPicker()
        {
            if (platformPanel != null) platformPanel.SetActive(true);
            SetMenuVisible(false);

            bool touch = PlatformDetect.LooksLikeTouchDevice();
            if (touchHint != null) touchHint.SetActive(touch);
            if (desktopHint != null) desktopHint.SetActive(!touch);
        }

        private void Choose(ControlScheme scheme)
        {
            if (SessionData.Instance != null) SessionData.Instance.Scheme = scheme;

            // Fullscreen must be asked for inside a user gesture, and this tap is the first one the
            // page ever gets. Denied or unsupported is fine — PlatformDetect swallows it.
            PlatformDetect.RequestFullscreen();

            if (platformPanel != null) platformPanel.SetActive(false);
            SetMenuVisible(true);
        }

        private void SetMenuVisible(bool visible)
        {
            foreach (GameObject go in menuObjects)
            {
                if (go != null) go.SetActive(visible);
            }
        }
    }
}
