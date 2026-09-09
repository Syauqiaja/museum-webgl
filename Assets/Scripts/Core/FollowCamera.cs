using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Keeps a camera a fixed distance behind whatever it is following, easing after it rather than
    /// snapping. Put it on the camera.
    ///
    /// The offset is captured from however the camera is already framed in the scene — press
    /// <b>Capture Offset From Scene</b> on the component, or leave <see cref="captureOffsetOnAwake"/>
    /// on and it takes the opening shot as the shot to hold. Framing a camera is art direction, and
    /// hand-typed offsets in an inspector are how a carefully placed shot gets lost.
    ///
    /// Rotation is left alone by default. The camera keeps whatever angle it was authored at: for a
    /// straight course there is nothing to turn towards, and a camera that swings at every stumble is
    /// a camera that makes people ill. Turn on <see cref="lookAtTarget"/> if a later course bends.
    ///
    /// <see cref="frameBehindTarget"/> makes it a third-person camera instead: the offset is held in
    /// the target's own space, so the shot stays behind the back through a turn rather than at a
    /// fixed compass bearing. Neither mode reads input — this camera is never steered by the player.
    /// </summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("What to follow. For Egrang this is the player object EgrangStepMover moves.")]
        [SerializeField] private Transform target;

        [Header("Framing")]
        [Tooltip("World-space offset held from the target. Capture it from the scene rather than typing it.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -5f);
        [Tooltip("Take the offset from how the camera and target are placed right now, on Awake. Off means the authored offset above is used as-is.")]
        [SerializeField] private bool captureOffsetOnAwake = true;

        [Tooltip("Hold the offset in the target's own space — behind its back rather than at a fixed compass point. Third-person framing; leave off for a fixed-angle shot.")]
        [SerializeField] private bool frameBehindTarget;

        [Header("Easing")]
        [Tooltip("Roughly the seconds the camera takes to catch up. Higher is floatier; 0 is a hard lock.")]
        [Min(0f)]
        [SerializeField] private float smoothTime = 0.35f;
        [Tooltip("Cap on how fast the camera may travel, in metres per second. 0 means no cap.")]
        [Min(0f)]
        [SerializeField] private float maxSpeed = 0f;

        [Header("Aim")]
        [Tooltip("Turn the camera towards the target every frame. Off by default — on a straight course it only adds sway.")]
        [SerializeField] private bool lookAtTarget;
        [Tooltip("Height above the target's pivot to aim at, so the camera looks at the torso rather than the feet.")]
        [SerializeField] private float lookAtHeight = 1.2f;

        Vector3 _velocity;

        /// <summary>The object being followed. Assign at runtime when the racer is spawned rather than placed.</summary>
        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                if (target != null && captureOffsetOnAwake) CaptureOffset();
            }
        }

        void Awake()
        {
            if (target == null)
            {
                Debug.LogWarning($"{nameof(FollowCamera)} on '{name}' has no target, so the camera stays put.", this);
                return;
            }

            if (captureOffsetOnAwake) CaptureOffset();
        }

        /// <summary>
        /// Takes the current camera-to-target vector as the offset to hold. On the component's context
        /// menu so a framing set up by eye in the Scene view can be locked in without reading numbers
        /// off the transform.
        /// </summary>
        [ContextMenu("Capture Offset From Scene")]
        public void CaptureOffset()
        {
            if (target == null) return;

            Vector3 worldOffset = transform.position - target.position;

            // Captured in the same space it will be applied in, or the shot moves the moment the
            // target turns.
            offset = frameBehindTarget ? Quaternion.Inverse(target.rotation) * worldOffset : worldOffset;
        }

        /// <summary>Where the camera belongs right now, in world space.</summary>
        Vector3 Destination =>
            target.position + (frameBehindTarget ? target.rotation * offset : offset);

        /// <summary>
        /// Runs in <c>LateUpdate</c> so the target has already moved this frame. Following in
        /// <c>Update</c> would chase last frame's position and show up as a permanent judder.
        /// </summary>
        void LateUpdate()
        {
            if (target == null) return;

            Vector3 destination = Destination;

            transform.position = smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, destination, ref _velocity, smoothTime,
                                     maxSpeed > 0f ? maxSpeed : Mathf.Infinity, Time.deltaTime)
                : destination;

            if (lookAtTarget) transform.LookAt(target.position + Vector3.up * lookAtHeight);
        }

        /// <summary>Drops the camera straight onto its mark, with no ease-in. Call when a run starts, or the first second of the race is the camera catching up.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            transform.position = Destination;
            _velocity = Vector3.zero;
        }
    }
}
