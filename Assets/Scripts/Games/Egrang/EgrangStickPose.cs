using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Result of one <see cref="EgrangStickSolver.Solve"/> call: where to put the stick root this
    /// frame, how far up the shaft the handle slid, and whether the hand outran the shaft.
    /// Shaped field-for-field like a future wire message so a P3 authoritative server and the
    /// client agree on the same numbers.
    /// </summary>
    public readonly struct EgrangStickPose
    {
        /// <summary>World position for the stick root, placed so the footplate lands on the foot bone.</summary>
        public readonly Vector3 Position;

        /// <summary>World rotation for the stick root: shaft aimed at the hand, roll taken from the foot.</summary>
        public readonly Quaternion Rotation;

        /// <summary>Handle distance from the footplate along the shaft, already clamped to the travel range.</summary>
        public readonly float HandleDistance;

        /// <summary>
        /// How far the hand overshot the top of the travel range, in metres. Zero while the hand is
        /// reachable (including when it is clamped at the bottom). A sustained positive value means
        /// the grip has visually detached — gameplay can treat it as a stumble.
        /// </summary>
        public readonly float Slack;

        /// <summary>True when the raw foot-to-hand distance fell outside the travel range at either end.</summary>
        public readonly bool IsClamped;

        public EgrangStickPose(Vector3 position, Quaternion rotation, float handleDistance,
                               float slack, bool isClamped)
        {
            Position = position;
            Rotation = rotation;
            HandleDistance = handleDistance;
            Slack = slack;
            IsClamped = isClamped;
        }
    }
}
