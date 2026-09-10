using TMPro;
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
    /// It is asked once per page load, not once per visit to this scene. <see cref="SessionData"/>
    /// keeps the answer for the life of the bootstrap object, so a visitor who comes back here
    /// after a game is not asked again what device they are holding, and the name they typed is
    /// waiting in the field. Before 2026-09-10 both were re-asked, which read as "the game logged
    /// me out".
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

        [Tooltip("The nickname field. Pre-filled from SessionData so a returning visitor is not asked twice.")]
        [SerializeField] private TMP_InputField nameInput;

        private void Awake()
        {
            Open();
        }

        /// <summary>Wires the screen from script. The builder writes the same references.</summary>
        public void Configure(GameObject picker, GameObject[] menu, GameObject touchTag, GameObject desktopTag,
                              TMP_InputField nameField = null)
        {
            platformPanel = picker;
            menuObjects = menu ?? new GameObject[0];
            touchHint = touchTag;
            desktopHint = desktopTag;
            nameInput = nameField;
            Open();
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

        /// <summary>
        /// Picker for a fresh page load; straight to the menu when this session already answered.
        /// The scheme is memory-only on SessionData (kiosk rule), so a reload still asks.
        /// </summary>
        private void Open()
        {
            PrefillName();

            SessionData session = SessionData.Instance;
            if (session != null && session.Scheme != ControlScheme.Unknown)
            {
                ShowMenu();
                return;
            }

            ShowPicker();
        }

        private void PrefillName()
        {
            if (nameInput == null || SessionData.Instance == null) return;

            // SetTextWithoutNotify: the field's onValueChanged already writes into
            // SessionData.PlayerName, and echoing the same name back is a wasted PlayerPrefs save.
            nameInput.SetTextWithoutNotify(SessionData.Instance.PlayerName);
        }

        private void ShowPicker()
        {
            if (platformPanel != null) platformPanel.SetActive(true);
            SetMenuVisible(false);

            bool touch = PlatformDetect.LooksLikeTouchDevice();
            if (touchHint != null) touchHint.SetActive(touch);
            if (desktopHint != null) desktopHint.SetActive(!touch);
        }

        private void ShowMenu()
        {
            if (platformPanel != null) platformPanel.SetActive(false);
            SetMenuVisible(true);
        }

        private void Choose(ControlScheme scheme)
        {
            if (SessionData.Instance != null) SessionData.Instance.Scheme = scheme;

            // Fullscreen must be asked for inside a user gesture, and this tap is the first one the
            // page ever gets. Denied or unsupported is fine — PlatformDetect swallows it.
            PlatformDetect.RequestFullscreen();

            ShowMenu();
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
