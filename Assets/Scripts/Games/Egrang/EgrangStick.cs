using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Drives one stilt from the character's animated hand and foot. Put it on the stick root;
    /// the rig needs two instances, one per side.
    ///
    /// The animation is authored on the character, never on the stick — this component only reads
    /// the two bones and writes the stick transform, so it must run after the <c>Animator</c>
    /// (hence <c>LateUpdate</c> plus a late execution order). All geometry lives in
    /// <see cref="EgrangStickSolver"/>; this class is the scene-facing shell around it.
    ///
    /// The footplate is welded to the foot bone and the grip slides along the shaft to reach the
    /// hand. When the hand outruns the shaft, <see cref="Slack"/> goes positive instead of the
    /// stick stretching.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class EgrangStick : MonoBehaviour
    {
        [Header("Character bones (animated)")]
        [Tooltip("Hand bone the grip follows. The grip slides along the shaft to stay with it.")]
        [SerializeField] private Transform handBone;
        [Tooltip("Foot bone the footplate is welded to. Also supplies the stick's roll about its shaft.")]
        [SerializeField] private Transform footBone;

        [Header("Bone offsets")]
        [Tooltip("Offset from the foot bone pivot to the actual contact point, in foot-bone local space. The ankle pivot is not the sole, so this is usually a small downward/forward nudge.")]
        [SerializeField] private Vector3 footOffset = Vector3.zero;
        [Tooltip("Offset from the hand bone pivot to the palm's grip point, in hand-bone local space. The wrist pivot is not where the pole sits inside the fist.")]
        [SerializeField] private Vector3 handOffset = Vector3.zero;

        [Header("Stick markers")]
        [Tooltip("Marker names must contain this to be auto-found. The shipped FBXs hold BOTH stilts, named \"..._L_Foot\" and \"..._R_Foot\", so set this to \"_L_\" or \"_R_\" — otherwise both components bind the same side.")]
        [SerializeField] private string markerFilter = "_L_";
        [Tooltip("Footplate pivot, welded to the foot bone. Leave empty to auto-find a descendant whose name ends in \"_Foot\".")]
        [SerializeField] private Transform footstep;
        [Tooltip("Hand grip that slides along the shaft. Leave empty to auto-find a descendant ending in \"_Grip\".")]
        [SerializeField] private Transform handle;
        [Tooltip("Ground end of the pole. Never constrained; only caps how far the grip may slide. Leave empty to auto-find \"_Tip\".")]
        [SerializeField] private Transform tip;

        [Header("Handle travel")]
        [Tooltip("How far the grip may slide either side of its authored height, in metres. Used when the explicit range below is left at zero.")]
        [SerializeField] private float handleTravel = 0.25f;
        [Tooltip("Explicit lower bound of grip distance from the footplate. 0 = derive from handleTravel.")]
        [SerializeField] private float minHandleDistance = 0f;
        [Tooltip("Explicit upper bound of grip distance from the footplate. 0 = derive from handleTravel.")]
        [SerializeField] private float maxHandleDistance = 0f;

        [Header("Debug")]
        [Tooltip("Draw the solved shaft and the hand/foot targets in the Scene view.")]
        [SerializeField] private bool drawGizmos = true;

        EgrangStickBinding _binding;
        float _minDistance;
        float _maxDistance;
        bool _bound;

        /// <summary>Metres the hand is currently overreaching past the top of the grip's travel. 0 while reachable.</summary>
        public float Slack { get; private set; }

        /// <summary>True while the grip is pinned at either end of its travel instead of tracking the hand.</summary>
        public bool IsClamped { get; private set; }

        /// <summary>Current grip distance from the footplate along the shaft.</summary>
        public float HandleDistance { get; private set; }

        /// <summary>The hand bone the grip follows.</summary>
        public Transform HandBone => handBone;

        /// <summary>The foot bone the footplate is welded to.</summary>
        public Transform FootBone => footBone;

        /// <summary>
        /// Points the stilt at another body's hand and foot — <see cref="EgrangRacerBody"/> swaps the
        /// walker's skeleton when a player chose a different character. The marker layout is the
        /// stick's own, so the binding does not change; only the bones it reads do.
        /// </summary>
        public void Rebind(Transform hand, Transform foot)
        {
            if (hand == null || foot == null) return;

            handBone = hand;
            footBone = foot;
        }

        /// <summary>World contact point the footplate is welded to: the foot bone plus <c>footOffset</c>.</summary>
        public Vector3 FootTarget => footBone.TransformPoint(footOffset);

        /// <summary>World grip point the handle chases: the hand bone plus <c>handOffset</c>.</summary>
        public Vector3 HandTarget => handBone.TransformPoint(handOffset);

        /// <summary>
        /// Foot bone rotation. Twist about the shaft is locked, so this is used only as the
        /// fallback aim axis when the hand collapses onto the footplate.
        /// </summary>
        public Quaternion FootTargetRotation => footBone.rotation;

        /// <summary>
        /// World position of the pole's ground end (<c>_Tip</c>). The solver never constrains it —
        /// it simply falls out of the welded footplate and the foot-to-hand aim — so gameplay can
        /// read it to decide whether the stilt is planted. Falls back to the footplate when the
        /// model has no tip marker.
        /// </summary>
        public Vector3 TipPosition => tip != null ? tip.position : footstep.position;

        // ---- setup ----

        void Awake()
        {
            if (footstep == null) footstep = FindMarker("_Foot");
            if (handle == null) handle = FindMarker("_Grip");
            if (tip == null) tip = FindMarker("_Tip");

            if (footstep == null || handle == null)
            {
                Debug.LogError($"{nameof(EgrangStick)} on '{name}' needs a footstep (_Foot) and a handle (_Grip) marker.", this);
                enabled = false;
                return;
            }

            if (handBone == null || footBone == null)
            {
                Debug.LogError($"{nameof(EgrangStick)} on '{name}' needs both handBone and footBone assigned.", this);
                enabled = false;
                return;
            }

            CaptureBinding();
        }

        /// <summary>
        /// Reads the authored marker layout and resolves the grip's travel range. Call again if the
        /// stick model is swapped at runtime.
        ///
        /// Every offset is captured in <b>metres along the root's local axes</b> — the local marker
        /// positions times the root's scale. The solver backs the root out from the foot and slides
        /// the grip in world metres, and the shipped stilt FBX is imported at ×151: measured in raw
        /// local units the footplate sat 0.0025 "metres" above the tip, so the tip was welded to the
        /// foot, the footplate floated 0.33 m up the shin, and the grip was pinned at the top of a
        /// travel range 150× too short for the hand to ever reach (2026-09-11). Unit-scale sticks,
        /// which is all the solver's tests use, never showed it.
        /// </summary>
        public void CaptureBinding()
        {
            Vector3 scale = transform.lossyScale;
            Vector3 footstepLocal = Vector3.Scale(transform.InverseTransformPoint(footstep.position), scale);
            Vector3 gripLocal = Vector3.Scale(transform.InverseTransformPoint(handle.position), scale);
            Vector3 tipLocal = tip != null ? Vector3.Scale(transform.InverseTransformPoint(tip.position), scale) : footstepLocal;

            _binding = EgrangStickBinding.Create(footstepLocal, gripLocal, tipLocal);

            bool explicitRange = maxHandleDistance > 0f;
            _minDistance = explicitRange
                ? Mathf.Max(0f, minHandleDistance)
                : Mathf.Max(0f, _binding.BindHandleDistance - handleTravel);
            _maxDistance = explicitRange
                ? Mathf.Max(_minDistance, maxHandleDistance)
                : _binding.BindHandleDistance + handleTravel;

            // The grip can never slide past the top of the pole.
            if (_binding.ShaftLength > 0f)
                _maxDistance = Mathf.Min(_maxDistance, _binding.ShaftLength);
            _maxDistance = Mathf.Max(_minDistance, _maxDistance);

            HandleDistance = _binding.BindHandleDistance;
            _bound = true;
        }

        /// <summary>
        /// Finds the marker ending in <paramref name="suffix"/> and matching <c>markerFilter</c>.
        /// The filter matters: the shipped stilt FBXs contain the left and right pole in one
        /// hierarchy, so an unfiltered search silently binds both components to the same side.
        /// Ambiguous matches are an error rather than a coin flip.
        /// </summary>
        Transform FindMarker(string suffix)
        {
            Transform found = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                if (!t.name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(markerFilter) &&
                    t.name.IndexOf(markerFilter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (found != null)
                {
                    Debug.LogError($"{nameof(EgrangStick)} on '{name}': '{suffix}' is ambiguous — both " +
                                   $"'{found.name}' and '{t.name}' match markerFilter '{markerFilter}'. " +
                                   "Narrow the filter or assign the markers by hand.", this);
                    return null;
                }
                found = t;
            }
            return found;
        }

        // ---- per-frame solve ----

        void LateUpdate()
        {
            // A body swap destroys the skeleton the bones belonged to for a frame before Rebind lands.
            if (!_bound || handBone == null || footBone == null) return;

            var input = new EgrangStickSolveInput(
                FootTarget, FootTargetRotation, HandTarget,
                _minDistance, _maxDistance);

            EgrangStickPose pose = EgrangStickSolver.Solve(_binding, input);

            transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            // Root moved first, so this lands the grip on the shaft at the solved height regardless
            // of how deep in the hierarchy the marker sits. Rotation only, never TransformPoint: the
            // binding is already in metres, and TransformPoint would apply the root's scale twice.
            handle.position = pose.Position + pose.Rotation *
                (_binding.FootstepLocalOffset + _binding.ShaftAxisLocal * pose.HandleDistance);

            HandleDistance = pose.HandleDistance;
            Slack = pose.Slack;
            IsClamped = pose.IsClamped;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || footBone == null || handBone == null) return;

            Vector3 footTarget = FootTarget;
            Vector3 handTarget = HandTarget;

            // Bone pivot -> offset contact point, so the offsets can be dialled in visually.
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(footBone.position, footTarget);
            Gizmos.DrawLine(handBone.position, handTarget);

            Gizmos.color = IsClamped ? Color.red : Color.green;
            Gizmos.DrawLine(footTarget, handTarget);
            Gizmos.DrawWireSphere(footTarget, 0.03f);
            Gizmos.DrawWireSphere(handTarget, 0.03f);
        }
    }
}
