using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Per-frame sample fed to <see cref="EgrangStickSolver.Solve"/>: the animated hand and foot
    /// bones plus the handle's allowed travel range. Nothing here reads the scene, which is what
    /// keeps the solver unit-testable.
    /// </summary>
    public readonly struct EgrangStickSolveInput
    {
        /// <summary>World position the footplate must be welded to (the animated foot bone).</summary>
        public readonly Vector3 FootWorldPosition;

        /// <summary>World rotation of the foot bone. Supplies the stick's roll about its own shaft.</summary>
        public readonly Quaternion FootWorldRotation;

        /// <summary>World position of the animated hand bone. Defines the aim direction and handle height.</summary>
        public readonly Vector3 HandWorldPosition;

        /// <summary>Lowest the handle may slide down the shaft, measured from the footplate.</summary>
        public readonly float MinHandleDistance;

        /// <summary>Highest the handle may slide up the shaft, measured from the footplate.</summary>
        public readonly float MaxHandleDistance;

        /// <summary>
        /// Constant twist about the shaft, in degrees. Roll is otherwise locked; this exists only
        /// to correct a model whose authored facing is off (180 for a back-to-front import).
        /// </summary>
        public readonly float RollOffsetDegrees;

        public EgrangStickSolveInput(Vector3 footWorldPosition, Quaternion footWorldRotation,
                                     Vector3 handWorldPosition,
                                     float minHandleDistance, float maxHandleDistance,
                                     float rollOffsetDegrees = 0f)
        {
            RollOffsetDegrees = rollOffsetDegrees;
            FootWorldPosition = footWorldPosition;
            FootWorldRotation = footWorldRotation;
            HandWorldPosition = handWorldPosition;
            MinHandleDistance = minHandleDistance;
            MaxHandleDistance = maxHandleDistance;
        }
    }
}
