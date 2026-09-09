using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// One lane's racer: the pairing of a mover, the track it runs on, and the seat that owns it.
    /// Put this on the lane root (`Player N Point`).
    ///
    /// It counts strides as well as animating them, because the server counts strides too — holding
    /// the same number locally is what makes a server echo either confirm the local prediction or
    /// correct it, without having to measure the transform back into units.
    /// </summary>
    public sealed class EgrangRacer : MonoBehaviour
    {
        [Tooltip("Seat this lane belongs to: 0 for Player 1 Point, 1 for Player 2, 2 for Player 3.")]
        [SerializeField] private int lane;
        [Tooltip("This lane's track. Leave empty to take one from this object or its children.")]
        [SerializeField] private EgrangRaceTrack track;
        [Tooltip("The racer that walks this lane. Leave empty to find one in the children.")]
        [SerializeField] private EgrangStepMover mover;
        [Tooltip("Lane start. Leave empty to use the track's own start marker.")]
        [SerializeField] private Transform start;
        [Tooltip("Lane finish. Leave empty to use the track's own finish marker.")]
        [SerializeField] private Transform finish;

        int _units;

        /// <summary>Seat that races here.</summary>
        public int Lane => lane;

        /// <summary>This lane's track, for the HUD to read.</summary>
        public EgrangRaceTrack Track => track;

        /// <summary>
        /// The transform that actually travels down the lane — the mover's, not this one's.
        ///
        /// This component sits on the lane root (`Player N Point`), which is a fixed mark on the
        /// ground: it is where the lane *is*, not where the racer has got to. Anything that
        /// follows a racer (the camera, most obviously) has to hold this instead, or it holds a
        /// point that never moves and reads as a camera that has stopped working.
        ///
        /// Falls back to this object's own transform so a lane with no mover still answers with
        /// something rather than null.
        /// </summary>
        public Transform Body => mover != null ? mover.transform : transform;

        /// <summary>Strides banked locally. Compared against the server's count on every echo.</summary>
        public int BankedUnits => _units;

        /// <summary>How far along the lane this racer is.</summary>
        public EgrangTrackProgress Progress => track != null
            ? track.Progress
            : new EgrangTrackProgress(0f, 0f, 0f, 0f);

        /// <summary>
        /// True once this lane's mover has reached (or passed) its own finish marker. Measured
        /// directly from this racer's own start/finish/mover — the same geometry <see
        /// cref="SnapToUnits"/> clamps against — rather than through <see cref="Track"/>, whose
        /// separate racer wiring is a scene concern this doesn't want to depend on.
        /// </summary>
        public bool HasFinishedLane => mover != null &&
            EgrangTrackProgress.Evaluate(StartPoint(), FinishPoint(), mover.transform.position).HasFinished;

        void Awake()
        {
            if (track == null) track = GetComponentInChildren<EgrangRaceTrack>();
            if (mover == null) mover = GetComponentInChildren<EgrangStepMover>();

            if (mover == null)
            {
                Debug.LogWarning($"{nameof(EgrangRacer)} on '{name}' has no mover, so this lane cannot race.", this);
            }
        }

        /// <summary>Wiring from code — used by the tests and by any runtime lane building.</summary>
        public void Bind(int lane, EgrangRaceTrack track, EgrangStepMover mover, Transform start, Transform finish)
        {
            this.lane = lane;
            this.track = track;
            this.mover = mover;
            this.start = start;
            this.finish = finish;
        }

        /// <summary>Plays one step and banks it. The same call serves the local press and a remote echo.</summary>
        public void ApplyStep(EgrangStepResult result)
        {
            _units += EgrangStep.StepsFor(result);

            if (mover != null) mover.OnStepResult(result);
        }

        /// <summary>
        /// Places the racer at a stride count without animating: reconnect, and repair after the
        /// server disagreed with a local prediction.
        /// </summary>
        public void SnapToUnits(int units)
        {
            if (mover == null) return;

            Vector3 from = StartPoint();
            Vector3 to = FinishPoint();
            float laneLength = Vector3.Distance(from, to);
            float metres = Mathf.Clamp(units * mover.StepLength, 0f, laneLength);

            // Bank the count the clamped position actually represents, not the raw request: a
            // stray or out-of-range echo must not leave BankedUnits describing ground the racer
            // was never placed on. Degenerate lanes/step lengths bank 0 rather than divide by zero.
            _units = laneLength <= 0f || mover.StepLength <= 0f
                ? 0
                : Mathf.RoundToInt(metres / mover.StepLength);
            mover.SnapTo(laneLength <= 0f ? from : from + (to - from).normalized * metres);
        }

        Vector3 StartPoint() => start != null ? start.position
            : track != null ? track.StartPosition
            : transform.position;

        Vector3 FinishPoint() => finish != null ? finish.position
            : track != null ? track.FinishPosition
            : transform.position;
    }
}
