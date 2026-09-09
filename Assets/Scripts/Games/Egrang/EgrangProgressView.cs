using TMPro;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The course seen from above, flattened to a strip: start on the left, finish on the right, and
    /// a marker sliding between them as the player walks. Put this on the HUD chip that holds the
    /// rail.
    ///
    /// A strip rather than a rendered top-down map because the course is a straight lane — a camera
    /// minimap would spend a render target restating one number the player actually wants, which is
    /// how much further there is to go.
    ///
    /// Pure view: it reads <see cref="EgrangRaceTrack"/> and draws. It never moves the player and
    /// never decides the race is over.
    /// </summary>
    public sealed class EgrangProgressView : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The course being drawn. Leave empty to find the one in the scene.")]
        [SerializeField] private EgrangRaceTrack track;

        [Header("Rail")]
        [Tooltip("Rect the marker travels across. Its width is the whole course.")]
        [SerializeField] private RectTransform rail;
        [Tooltip("The marker moved along the rail. Must be a child of the rail with centred anchor and pivot, since it is placed about the rail's middle.")]
        [SerializeField] private RectTransform marker;
        [Tooltip("Optional. Stretched from the left edge of the rail to the marker, so covered ground reads as filled.")]
        [SerializeField] private RectTransform fill;

        [Header("Readout")]
        [Tooltip("Distance still to run, e.g. \"32 m lagi\". Optional.")]
        [SerializeField] private TMP_Text remainingText;
        [Tooltip("Format for the readout. {0} is metres remaining, {1} is the course length, {2} is percent complete.")]
        [SerializeField] private string remainingFormat = "{0:0} m lagi";
        [Tooltip("Shown once the player crosses the finish line.")]
        [SerializeField] private string finishedText = "FINIS";

        float _railWidth;

        void Awake()
        {
            // No search fallback: three lanes exist, so an unassigned strip has to stay blank rather
            // than quietly show someone else's race. EgrangRace assigns it.
            if (track == null)
            {
                Debug.LogWarning($"{nameof(EgrangProgressView)} on '{name}' has no track yet; " +
                                 "EgrangRace assigns the local lane at runtime.", this);
            }
        }

        void Update()
        {
            if (track == null) return;

            Draw(track.Progress);
        }

        /// <summary>
        /// Points the strip at a lane's track. `EgrangRace` calls this with the local lane — with
        /// three tracks in the scene, finding one by type would pick an arbitrary racer's progress.
        /// </summary>
        public void SetTrack(EgrangRaceTrack value) => track = value;

        /// <summary>
        /// Places the marker and writes the readout. Public so a test — or a replay — can drive the
        /// strip without a live track underneath it.
        /// </summary>
        public void Draw(EgrangTrackProgress progress)
        {
            if (rail != null) _railWidth = rail.rect.width;

            if (marker != null)
            {
                Vector2 position = marker.anchoredPosition;
                position.x = Mathf.Lerp(-_railWidth * 0.5f, _railWidth * 0.5f, progress.Normalized);
                marker.anchoredPosition = position;
            }

            if (fill != null)
            {
                // Anchored to the rail's left edge, so only the width has to move.
                fill.sizeDelta = new Vector2(_railWidth * progress.Normalized, fill.sizeDelta.y);
            }

            if (remainingText != null)
            {
                remainingText.text = progress.HasFinished
                    ? finishedText
                    : string.Format(remainingFormat, progress.RemainingMeters, progress.TotalMeters,
                                    progress.Normalized * 100f);
            }
        }
    }
}
