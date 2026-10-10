using UnityEngine;

namespace Museum.Core
{
    /// <summary>What has to be in reach before a control is worth putting on screen.</summary>
    public enum InteractionCue
    {
        /// <summary>Something the Interaksi button can act on — a doorway or a gallery station.</summary>
        Doorway = 0,

        /// <summary>A lesson plaque the player is standing at — the ‹ › page buttons.</summary>
        LessonPaging = 1,
    }

    /// <summary>
    /// Shows its control only while <see cref="TouchInteractRouter"/> has something for it to act
    /// on. An Interaksi button with no doorway under it, or page arrows with no plaque in front of
    /// them, are buttons that do nothing when tapped — worse than absent, because a visitor who
    /// taps one and gets no response has been told the control is broken.
    /// </summary>
    /// <remarks>
    /// Hides through a <see cref="CanvasGroup"/> rather than <c>SetActive(false)</c>, and the
    /// difference is load-bearing: a deactivated GameObject stops receiving <c>Update</c>, so it
    /// could never notice the doorway it is waiting for and would never come back. The object
    /// stays active and goes transparent, non-interactive and raycast-transparent instead — which
    /// also means it stops eating taps meant for the look area behind it.
    ///
    /// Polls rather than subscribing because the router's registrations are plain static setters
    /// called from trigger callbacks; an event would be the better shape if a third caller ever
    /// appears, but two call sites do not pay for one.
    /// </remarks>
    [RequireComponent(typeof(CanvasGroup))]
    public class ShownWhenAvailable : MonoBehaviour
    {
        [Tooltip("Which of the router's two registrations this control needs.")]
        [SerializeField] private InteractionCue cue;

        private CanvasGroup _group;
        private bool _shown = true;

        /// <summary>True when the router currently holds what this control acts on.</summary>
        public bool Available => cue == InteractionCue.Doorway
            ? TouchInteractRouter.CurrentPrompt != null || TouchInteractRouter.CurrentInteractable != null
            : TouchInteractRouter.CurrentReader != null;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();

            // Force the first write: a CanvasGroup left at alpha 0 in the scene has to be corrected
            // on the frame the control does become available, not assumed to already agree.
            _shown = !Available;
            Apply(Available);
        }

        private void Update() => Apply(Available);

        private void Apply(bool show)
        {
            if (_shown == show) return;

            _shown = show;
            _group.alpha = show ? 1f : 0f;
            _group.interactable = show;
            _group.blocksRaycasts = show;
        }
    }
}
