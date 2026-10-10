using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Museum.Core
{
    /// <summary>
    /// A button's or toggle's click and hover sounds, through <see cref="GameAudio"/>. Added at
    /// runtime to every one <see cref="GameAudio"/> finds — never placed in a scene.
    /// </summary>
    /// <remarks>
    /// The click rides the control's own event (<c>onClick</c>, or a toggle turning on), so it
    /// sounds for a tap, a click and a keyboard submit alike, and never for a disabled button.
    /// The listener is added in code and is not serialised, so the scenes' by-name wiring
    /// (CLAUDE.md: handler names are API) is untouched. Hover is desktop-only: a touch "enters"
    /// the button in the same instant it taps it, and two sounds per tap is one too many.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UiSelectableSound : MonoBehaviour, IPointerEnterHandler
    {
        private Selectable _selectable;
        private Button _button;
        private Toggle _toggle;
        private UnityAction _onClick;
        private UnityAction<bool> _onToggle;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _button = _selectable as Button;
            _toggle = _selectable as Toggle;

            _onClick = () => { if (!Silent) GameAudio.PlayClick(); };
            _onToggle = on => { if (on && !Silent) GameAudio.PlayClick(); };

            if (_button != null) _button.onClick.AddListener(_onClick);
            if (_toggle != null) _toggle.onValueChanged.AddListener(_onToggle);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(_onClick);
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(_onToggle);
        }

        /// <summary>
        /// Checked when the sound would play, not once: whoever marks a button
        /// <see cref="SilentButton"/> may do it after this component was added.
        /// </summary>
        private bool Silent => TryGetComponent(out SilentButton _);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable == null || !_selectable.IsInteractable() || Silent) return;
            if (SessionData.Instance != null && SessionData.Instance.IsTouch) return;

            GameAudio.PlayHover();
        }
    }
}
