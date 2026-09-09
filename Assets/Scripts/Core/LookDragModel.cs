using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Turns a stream of drag events into one look delta per frame.
    /// </summary>
    /// <remarks>
    /// The output is in the same units as <c>&lt;Mouse&gt;/delta</c> — pixels since the last frame,
    /// applied without <c>Time.deltaTime</c> — so the touch scheme and the mouse scheme share one
    /// sensitivity and one pitch clamp inside <c>FPSController</c>.
    ///
    /// <see cref="Consume"/> empties the accumulator on purpose. A drag reports nothing on a frame
    /// where the thumb did not move, and a model that kept its last value would spin the camera on
    /// a stationary finger.
    /// </remarks>
    public class LookDragModel
    {
        private Vector2 _pending;

        public LookDragModel(float sensitivity)
        {
            Sensitivity = sensitivity;
        }

        /// <summary>Screen pixels of drag to look-delta units.</summary>
        public float Sensitivity { get; set; }

        /// <summary>Adds one pointer event's drag. Called any number of times per frame.</summary>
        public void AddDrag(Vector2 screenDelta) => _pending += screenDelta * Sensitivity;

        /// <summary>Takes this frame's look delta and clears it.</summary>
        public Vector2 Consume()
        {
            Vector2 look = _pending;
            _pending = Vector2.zero;
            return look;
        }
    }
}
