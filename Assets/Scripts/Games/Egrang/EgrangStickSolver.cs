using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Two-pivot stilt constraint. The hand and foot are authored animation; the stick is a pure
    /// follower derived from them, so this is the only place the geometry lives.
    ///
    /// The footplate is the hard pivot — it stays welded to the foot bone no matter what. The
    /// handle is the soft pivot: the shaft aims at the hand and the grip slides up or down the
    /// shaft to meet it. Only when the foot-to-hand distance leaves the travel range does the grip
    /// clamp, and the overshoot is reported as <see cref="EgrangStickPose.Slack"/> instead of
    /// being silently absorbed.
    ///
    /// Twist about the shaft is locked: the pole is round-ish in profile and a spinning stick reads
    /// as a bug, so the roll reference is a fixed world axis. Only the aim direction is animated.
    ///
    /// Pure/static — <c>UnityEngine</c> is used only for <see cref="Vector3"/>/<see cref="Quaternion"/>,
    /// mirroring <c>DakonPile</c>, so it is unit-testable and portable off the main thread.
    /// </summary>
    public static class EgrangStickSolver
    {
        /// <summary>Below this length a direction is treated as degenerate and a fallback is used.</summary>
        public const float Epsilon = 1e-5f;

        /// <summary>Squared <see cref="Epsilon"/>, for comparisons against <c>sqrMagnitude</c>.</summary>
        public const float EpsilonSqr = Epsilon * Epsilon;

        /// <summary>
        /// Solves the stick pose for one frame. Never returns NaN: a hand sitting exactly on the
        /// footplate falls back to the foot's own up axis, and a shaft parallel to the roll
        /// reference falls back to a second reference direction.
        /// </summary>
        public static EgrangStickPose Solve(in EgrangStickBinding binding, in EgrangStickSolveInput input)
        {
            // ---- aim: footplate -> hand ----
            Vector3 aim = input.HandWorldPosition - input.FootWorldPosition;
            float reach = aim.magnitude;
            Vector3 up = reach > Epsilon
                ? aim / reach
                : input.FootWorldRotation * Vector3.up;

            // ---- orientation: minimal arc from the modelled shaft axis onto the aim ----
            // Shortest-arc, so at the bind pose the rotation is identity and the model keeps the
            // orientation it was authored with. Building a LookRotation here instead would pick an
            // arbitrary twist reference and can land the stick 180 degrees out.
            Quaternion rotation = Quaternion.FromToRotation(binding.ShaftAxisLocal, up);
            if (Mathf.Abs(input.RollOffsetDegrees) > Epsilon)
                rotation = Quaternion.AngleAxis(input.RollOffsetDegrees, up) * rotation;

            // ---- footplate is the pivot: back the root out from the foot bone ----
            Vector3 position = input.FootWorldPosition - rotation * binding.FootstepLocalOffset;

            // ---- handle slides along the shaft to wherever the hand is ----
            float min = Mathf.Max(0f, input.MinHandleDistance);
            float max = Mathf.Max(min, input.MaxHandleDistance);
            float handleDistance = Mathf.Clamp(reach, min, max);
            float slack = Mathf.Max(0f, reach - max);
            bool isClamped = reach > max || reach < min;

            return new EgrangStickPose(position, rotation, handleDistance, slack, isClamped);
        }
    }
}
