using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The course: two markers in the scene and the racer running between them. Put this on an empty
    /// object with the start and finish as children, so moving the course means dragging two
    /// handles rather than editing numbers.
    ///
    /// Read <see cref="Progress"/> for the current standing. The arithmetic itself lives in
    /// <see cref="EgrangTrackProgress"/>, outside <c>MonoBehaviour</c>, because the authority server
    /// will need the same reading to rank players and the two must not drift apart.
    ///
    /// Nothing here ends the race. Crossing the line raises <see cref="Finished"/> once and that is
    /// all — the timer, the placement and the results screen are not built yet, and a track that
    /// silently froze the player would be a worse guess than leaving it to the listener.
    /// </summary>
    public sealed class EgrangRaceTrack : MonoBehaviour
    {
        [Header("Course")]
        [Tooltip("Start line. Leave empty to use this object's own position.")]
        [SerializeField] private Transform start;
        [Tooltip("Finish line. Drag it to where the course actually ends.")]
        [SerializeField] private Transform finish;

        [Header("Racer")]
        [Tooltip("The player object whose progress is measured — the one EgrangStepMover moves.")]
        [SerializeField] private Transform racer;

        bool _announcedFinish;

        /// <summary>Raised the first time the racer reaches the finish line. The hook for the race timer and, later, the results screen.</summary>
        public event System.Action Finished;

        /// <summary>Where the start line sits, falling back to this object so a half-wired track still measures something.</summary>
        public Vector3 StartPosition => start != null ? start.position : transform.position;

        /// <summary>Where the finish line sits.</summary>
        public Vector3 FinishPosition => finish != null ? finish.position : StartPosition;

        /// <summary>Length of the course in metres, measured on the ground plane.</summary>
        public float LengthMeters => Progress.TotalMeters;

        /// <summary>The racer's current standing. Safe to read every frame; it is four vector operations.</summary>
        public EgrangTrackProgress Progress =>
            racer != null
                ? EgrangTrackProgress.Evaluate(StartPosition, FinishPosition, racer.position)
                : new EgrangTrackProgress(0f, 0f, 0f, Vector3.Distance(StartPosition, FinishPosition));

        void Awake()
        {
            if (finish == null)
            {
                Debug.LogWarning($"{nameof(EgrangRaceTrack)} on '{name}' has no finish line, so the course " +
                                 "has zero length and progress reads as complete.", this);
            }

            if (racer == null)
            {
                Debug.LogWarning($"{nameof(EgrangRaceTrack)} on '{name}' has no racer assigned, so progress " +
                                 "stays at the start line.", this);
            }
        }

        void Update()
        {
            if (_announcedFinish || racer == null) return;

            if (Progress.HasFinished)
            {
                _announcedFinish = true;
                Finished?.Invoke();
            }
        }

        /// <summary>Re-arms the finish event. Call when a fresh run starts, or the second race never announces its finish.</summary>
        public void ResetRun() => _announcedFinish = false;

#if UNITY_EDITOR
        /// <summary>
        /// Draws the course in the Scene view. The line is the thing being measured, and without it
        /// the two markers are a pair of empties nobody can see the relationship between.
        /// </summary>
        void OnDrawGizmos()
        {
            Vector3 a = StartPosition;
            Vector3 b = FinishPosition;

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(a, 0.4f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(b, 0.4f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(a, b);

            UnityEditor.Handles.Label((a + b) * 0.5f, $"{Vector3.Distance(a, b):0.#} m");
        }
#endif
    }
}
