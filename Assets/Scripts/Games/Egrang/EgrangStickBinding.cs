using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Bind-pose description of one stilt, captured once from the authored prefab and then held
    /// constant. It answers two questions the solver needs every frame: where the footplate sits
    /// relative to the stick root, and which local direction the shaft runs along.
    ///
    /// All offsets are in the stick root's local space, so the struct survives the root being
    /// moved/rotated arbitrarily. See <see cref="EgrangStickSolver.Solve"/>.
    /// </summary>
    public readonly struct EgrangStickBinding
    {
        /// <summary>Footplate (<c>_Foot</c> marker) position in stick-root local space.</summary>
        public readonly Vector3 FootstepLocalOffset;

        /// <summary>Unit vector from footplate towards grip, in stick-root local space.</summary>
        public readonly Vector3 ShaftAxisLocal;

        /// <summary>Footplate-to-grip distance in the authored pose. Used as the resting handle height.</summary>
        public readonly float BindHandleDistance;

        /// <summary>
        /// Distance from the ground tip (<c>_Tip</c> marker) up to the grip in the authored pose.
        /// Marker order down the pole is grip, footplate, tip, so this spans the whole visible
        /// shaft. Used only as a conservative sanity cap on grip travel — the pole may well extend
        /// above the grip, and nothing marks its top, so this is a heuristic bound rather than an
        /// exact one. Zero when no tip marker was supplied.
        /// </summary>
        public readonly float ShaftLength;

        EgrangStickBinding(Vector3 footstepLocalOffset, Vector3 shaftAxisLocal,
                           float bindHandleDistance, float shaftLength)
        {
            FootstepLocalOffset = footstepLocalOffset;
            ShaftAxisLocal = shaftAxisLocal;
            BindHandleDistance = bindHandleDistance;
            ShaftLength = shaftLength;
        }

        /// <summary>
        /// Builds a binding from the marker positions in stick-root local space.
        /// <paramref name="tipLocal"/> is optional; pass the footplate position to skip it.
        /// Degenerate input (grip on top of footplate) falls back to a +Y shaft rather than NaN.
        /// </summary>
        public static EgrangStickBinding Create(Vector3 footstepLocal, Vector3 gripLocal, Vector3 tipLocal)
        {
            Vector3 shaft = gripLocal - footstepLocal;
            float bindDistance = shaft.magnitude;
            Vector3 axis = bindDistance > EgrangStickSolver.Epsilon
                ? shaft / bindDistance
                : Vector3.up;

            float shaftLength = (gripLocal - tipLocal).magnitude;

            return new EgrangStickBinding(footstepLocal, axis, bindDistance, shaftLength);
        }
    }
}
