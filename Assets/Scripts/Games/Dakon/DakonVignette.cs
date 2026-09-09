using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// The board's answer to a drop: a soft screen-edge glow, green when the seed matched the hole
    /// it went into and red when it did not, so the museum's players read right-or-wrong without
    /// reading anything. Presentational only — the score it echoes was settled by
    /// <see cref="DakonBoard"/> (offline) or the server (online) before this ever runs.
    ///
    /// The pulse is a plain coroutine rather than a tween: LeanTween's updater is stranded from the
    /// second Play onward under disabled domain reload (see <c>Assets/Docs/ui-style.md</c> §8), and
    /// feedback that silently stops firing is worse than no feedback at all.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class DakonVignette : MonoBehaviour
    {
        [Tooltip("Overlay the tint is drawn on. Resolved from this object if unassigned.")]
        [SerializeField] private Image image;

        [Tooltip("Tint of a drop that matched its hole. Shares Egrang's 'full' green so both games read as one palette.")]
        [SerializeField] private Color correctColor = new Color(0.25f, 0.80f, 0.35f);

        [Tooltip("Tint of a drop that missed — the point went to the opponent.")]
        [SerializeField] private Color wrongColor = new Color(0.85f, 0.20f, 0.20f);

        [Tooltip("Strongest the tint gets at the screen edge. A vignette, not a flashbang — this is a hint in the corner of the eye, and it competes with a board the player is trying to watch.")]
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.16f;

        [Tooltip("Seconds to reach full tint. Short: the answer should feel simultaneous with the click.")]
        [SerializeField] private float riseSeconds = 0.08f;

        [Tooltip("Seconds to fade back out.")]
        [SerializeField] private float fallSeconds = 0.35f;

        [Tooltip("Resolution of the baked vignette sprite. Only used when no sprite is authored on the Image.")]
        [SerializeField] private int bakeResolution = 256;

        Sprite _baked;
        Coroutine _pulse;

        /// <summary>How long one pulse lasts, start to clear.</summary>
        public float PulseSeconds => Mathf.Max(0f, riseSeconds) + Mathf.Max(0f, fallSeconds);

        void Awake()
        {
            if (image == null) image = GetComponent<Image>();

            if (image != null)
            {
                if (image.sprite == null)
                {
                    _baked = VignetteTexture.Bake(bakeResolution);
                    image.sprite = _baked;
                }

                // It covers the whole screen, including the cards. Anything but a pass-through here
                // would eat every click in the game.
                image.raycastTarget = false;
                image.type = Image.Type.Simple;
                SetAlpha(0f);
            }

            if (transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        void OnDestroy() => VignetteTexture.Release(ref _baked);

        /// <summary>The tint a drop of this outcome is shown in.</summary>
        public Color ColorFor(bool correct) => correct ? correctColor : wrongColor;

        /// <summary>
        /// Pulse once. A second drop landing mid-pulse restarts this same pulse rather than
        /// stacking another — a player emptying a hand fast gets one answer per drop, not a
        /// brightening pile of them.
        /// </summary>
        public void Flash(bool correct)
        {
            if (image == null) image = GetComponent<Image>();
            if (image == null) return;

            Color tint = ColorFor(correct);
            image.color = new Color(tint.r, tint.g, tint.b, 0f);

            // Disabled or mid-teardown there is no coroutine to run. The drop still happened and the
            // board still scored it; only the flourish is skipped.
            if (!isActiveAndEnabled) return;

            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(Pulse());
        }

        IEnumerator Pulse()
        {
            float rise = Mathf.Max(0f, riseSeconds);
            float fall = Mathf.Max(0f, fallSeconds);
            float total = rise + fall;

            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                SetAlpha(AlphaAt(t, rise, fall, peakAlpha));
                yield return null;
            }

            SetAlpha(0f);
            _pulse = null;
        }

        /// <summary>
        /// Alpha of the pulse <paramref name="elapsed"/> seconds in: a linear rise to
        /// <paramref name="peak"/>, then a smoothed fall back to nothing. Static and pure so the
        /// shape can be tested without a play mode.
        /// </summary>
        public static float AlphaAt(float elapsed, float rise, float fall, float peak)
        {
            if (elapsed <= 0f) return rise <= 0f ? peak : 0f;
            if (elapsed < rise) return peak * (elapsed / rise);

            float t = elapsed - rise;
            if (fall <= 0f || t >= fall) return 0f;

            return peak * (1f - Mathf.SmoothStep(0f, 1f, t / fall));
        }

        void SetAlpha(float a)
        {
            Color c = image.color;
            image.color = new Color(c.r, c.g, c.b, a);
        }
    }
}
