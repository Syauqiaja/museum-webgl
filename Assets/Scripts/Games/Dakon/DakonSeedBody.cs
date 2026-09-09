using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// One seed's physical life: thrown, left alone while it falls and rattles into its bowl,
    /// then pinned once it stops. Added at runtime to a spawned seed — the eight species prefabs
    /// stay pure art, so nobody has to remember to keep colliders in step across all of them.
    ///
    /// The seed is thrown, never steered. That is the whole point: a lerp already knows where it
    /// will stop, so it cannot bounce off a rim or come to rest leaning on the seed that landed
    /// before it. Once the throw leaves, only gravity and the board decide.
    ///
    /// Every seed still has to end up in the hole it scored in, because the pile is the visible
    /// record of the match — so one throw has to be enough. It is made enough while the seed is
    /// still moving, not afterwards: the arc is steered by fractions of its own speed toward the
    /// point where it will cross the bowl's mouth (<see cref="Steer"/>), and inside the bowl an
    /// invisible wall reflects what the thin modelled shell would have let escape
    /// (<see cref="Contain"/>). Both are corrections a viewer cannot pick out of a tumbling
    /// seed, which is the whole reason they replaced picking the seed up and throwing it a
    /// second time.
    ///
    /// The placement in <see cref="Settle"/> is what is left of that older repair, kept as a
    /// backstop the pile cannot do without. It is not expected to run.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DakonSeedBody : MonoBehaviour
    {
        /// <summary>Where this seed is headed and how wide the bowl is once it gets there.</summary>
        private Transform _target;
        private float _bowlRadius;
        private float _bowlDepth;
        private int _pileSlot;
        private float _pileRadius;
        private DakonSeedTuning _tuning;

        /// <summary>The exact point this throw was solved for, kept so the flight can be steered
        /// back onto it after a graze. Recomputing it per frame would move the goalposts.</summary>
        private Vector3 _aim;

        /// <summary>False until the throw leaves; nothing is steered or contained before that.</summary>
        private bool _inFlight;

        private Rigidbody _body;
        private Collider _collider;
        private Coroutine _watch;

        /// <summary>
        /// Every seed currently in the air. Seeds in flight are made to pass through each other,
        /// and solid again the moment they land.
        ///
        /// Two seeds thrown on the same frame start within a centimetre of each other, because
        /// they are launched from adjacent cards in the same hand. Left solid, they spawn
        /// overlapping and the depenetration solver flings both across the table — which reads as
        /// the throw being random when it is nothing of the kind. They still land on the seeds
        /// already resting in the bowl, which is the collision that carries the pile.
        /// </summary>
        private static readonly List<DakonSeedBody> InFlight = new List<DakonSeedBody>();

        /// <summary>True once the seed has stopped and been pinned where it lies.</summary>
        public bool IsSettled { get; private set; }

        /// <summary>True if this seed had to be put back into its bowl by hand — the backstop
        /// fired, and the throw did not do its job.</summary>
        public bool WasCorrected { get; private set; }

        /// <summary>
        /// How far from its bowl's centre the seed actually came to rest, before any correction.
        /// Kept because it is the only way to tell a throw that is landing honestly from one that
        /// only looks right because it was nudged afterwards.
        /// </summary>
        public float LandedOffset { get; private set; }

        /// <summary>True if the seed was pinned by the timeout rather than by going still.</summary>
        public bool TimedOut { get; private set; }

        /// <summary>
        /// Give the seed a body and a shape. The collider is sized from the art's own bounds, so
        /// a long flat seed keeps a long flat footprint and stacks the way it looks like it should.
        /// </summary>
        public static DakonSeedBody Attach(GameObject seed, DakonSeedTuning tuning)
        {
            var body = seed.GetComponent<Rigidbody>();
            if (body == null) body = seed.AddComponent<Rigidbody>();

            body.mass = tuning.Mass;
            body.linearDamping = tuning.Drag;
            body.angularDamping = tuning.AngularDrag;

            // A seed is roughly a centimetre against a board wall a millimetre thick. Discrete
            // collision checks the gap between frames and finds nothing there, so the seed drops
            // straight through the table.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (seed.GetComponentInChildren<Collider>() == null) AddColliderFromArt(seed, tuning);

            var seedBody = seed.GetComponent<DakonSeedBody>();
            if (seedBody == null) seedBody = seed.AddComponent<DakonSeedBody>();
            seedBody._body = body;
            seedBody._tuning = tuning;
            seedBody._collider = seed.GetComponentInChildren<Collider>();
            return seedBody;
        }

        /// <summary>
        /// A box the size of the rendered seed, added to the mesh's own object rather than the
        /// seed root. These seeds are flat, and their art sits under a tilted child transform: a
        /// box built in the root's space would be the AABB of a tilted lentil — near cubic, and a
        /// cube stacks nothing like a seed does. On the mesh's own object the box is the mesh's
        /// own bounds, so it stays as flat as the thing it is standing in for.
        ///
        /// A convex mesh per species would roll more truthfully still, but that is eight extra
        /// cooks for a difference nobody can see at a centimetre across.
        /// </summary>
        private static void AddColliderFromArt(GameObject seed, DakonSeedTuning tuning)
        {
            var filter = seed.GetComponentInChildren<MeshFilter>();
            GameObject host = filter != null ? filter.gameObject : seed;

            var box = host.AddComponent<BoxCollider>();

            if (filter != null && filter.sharedMesh != null)
            {
                Bounds bounds = filter.sharedMesh.bounds;
                box.center = bounds.center;
                box.size = bounds.size;
            }

            if (tuning.Material != null) box.material = tuning.Material;
        }

        /// <summary>
        /// Throw the seed at <paramref name="target"/>, aiming a little short of the rim so a
        /// clean shot lands in the bowl rather than on its lip, and let go.
        /// </summary>
        public void ThrowTo(Transform target, float bowlRadius, float bowlDepth, int pileSlot, float pileRadius)
        {
            LiftClearOfCurrentBowl();

            _target = target;
            _bowlRadius = bowlRadius;
            _bowlDepth = bowlDepth;
            _pileSlot = pileSlot;
            _pileRadius = pileRadius;
            IsSettled = false;

            Vector3 from = transform.position;
            Vector3 to = AimPoint();
            _aim = to;

            Vector3 flat = to - from;
            flat.y = 0f;
            float distance = flat.magnitude;

            float apex = DakonBallistics.ApexFor(distance, _tuning.ApexPerDistance,
                                                _tuning.MinApexHeight, _tuning.MaxApexHeight);

            float horizontal, vertical, flight;
            DakonBallistics.SolveByApex(from.y, to.y, distance, apex, Physics.gravity.y,
                                        out horizontal, out vertical, out flight);

            Vector3 direction = distance > 1e-5f ? flat / distance : Vector3.zero;
            Vector3 velocity = direction * horizontal + Vector3.up * vertical;

            _body.isKinematic = false;
            _body.WakeUp();
            _body.linearVelocity = velocity;

            // Seeds are not thrown by a machine. A little tumble also stops a column of identical
            // shapes landing in the identical pose and reading as one object.
            _body.angularVelocity = Random.insideUnitSphere * _tuning.SpinSpeed;

            _inFlight = true;
            EnterFlight();

            if (_watch != null) StopCoroutine(_watch);
            _watch = StartCoroutine(WatchUntilSettled(flight));
        }

        /// <summary>
        /// Raise a seed that is already lying in a bowl until it clears the rim.
        ///
        /// Only the game-over sweep hits this, and without it the sweep does not work at all: a
        /// settled seed rests at the bottom of a six-centimetre cup, so a throw aimed across the
        /// board drives it straight into the near wall and it never leaves. A real player lifts
        /// the seeds out before moving them, and so does this.
        /// </summary>
        private void LiftClearOfCurrentBowl()
        {
            if (_target == null) return;

            float rim = _target.position.y + _tuning.SweepLift;
            if (transform.position.y >= rim) return;

            transform.position = new Vector3(transform.position.x, rim, transform.position.z);
        }

        /// <summary>Go transparent to the other seeds in the air, and join them.</summary>
        private void EnterFlight()
        {
            if (_collider == null || InFlight.Contains(this)) return;

            foreach (DakonSeedBody other in InFlight)
                if (other != null && other._collider != null)
                    Physics.IgnoreCollision(_collider, other._collider, true);

            InFlight.Add(this);
        }

        /// <summary>Solid again — including against whatever is still on its way down.</summary>
        private void LeaveFlight()
        {
            if (!InFlight.Remove(this) || _collider == null) return;

            foreach (DakonSeedBody other in InFlight)
                if (other != null && other._collider != null)
                    Physics.IgnoreCollision(_collider, other._collider, false);
        }

        /// <summary>A seed cleared off the board mid-flight must not be left in the list.</summary>
        private void OnDestroy()
        {
            InFlight.Remove(this);
        }

        /// <summary>
        /// The two corrections that make one throw enough, applied in the same step the solver
        /// runs in so they read as part of the flight rather than as something done to it.
        ///
        /// Which one applies is a question of where the seed is. Above the bowl's mouth it is
        /// still being thrown, so it is steered; at or below the mouth, and close enough that the
        /// bowl is plainly where it is going, it is being caught, so it is contained. A seed that
        /// is merely low — one leaving a card held below board level, say — is neither.
        /// </summary>
        private void FixedUpdate()
        {
            if (!_inFlight || IsSettled || _target == null || _body == null || _body.isKinematic) return;

            Vector3 position = transform.position;
            float mouth = _target.position.y + _tuning.CaptureHeight;

            Vector3 fromAxis = position - _target.position;
            fromAxis.y = 0f;

            if (position.y > mouth) Steer(position, mouth);
            else if (fromAxis.magnitude <= _bowlRadius * _tuning.CaptureMargin) Contain(position, fromAxis);
        }

        /// <summary>
        /// Bend the arc so that when the seed falls past the bowl's mouth it is over the point it
        /// was aimed at.
        ///
        /// The remaining flight is what the correction is divided by, so an early graze is
        /// answered gently over half a second and a late one more firmly — and either way by a
        /// velocity change small enough to sit inside the tumble. An untouched throw was solved
        /// to arrive there already, so this hands back the velocity it was given.
        /// </summary>
        private void Steer(Vector3 position, float mouth)
        {
            Vector3 velocity = _body.linearVelocity;

            float left = DakonBallistics.SecondsToFallTo(position.y, velocity.y, mouth, Physics.gravity.y);
            if (left <= 0f) return;

            velocity.x = DakonBallistics.Steer(_aim.x - position.x, left, velocity.x,
                                               _tuning.SteerResponse, _tuning.MaxSteerPerSecond,
                                               Time.fixedDeltaTime);
            velocity.z = DakonBallistics.Steer(_aim.z - position.z, left, velocity.z,
                                               _tuning.SteerResponse, _tuning.MaxSteerPerSecond,
                                               Time.fixedDeltaTime);

            _body.linearVelocity = velocity;
        }

        /// <summary>
        /// Keep a seed that is already in the bowl in the bowl, by giving the cup the side wall
        /// its thin modelled shell does not reliably provide.
        ///
        /// Only the outward part of the motion is touched: the seed still falls, still tumbles,
        /// still lands on whatever is under it. What it cannot do any more is carry a bounce out
        /// over the lip — which is the miss the second throw existed to repair.
        /// </summary>
        private void Contain(Vector3 position, Vector3 fromAxis)
        {
            float distance = fromAxis.magnitude;
            Vector3 outward = distance > 1e-5f ? fromAxis / distance : Vector3.zero;

            Vector3 velocity = _body.linearVelocity;
            float outwardSpeed = Vector3.Dot(velocity, outward);

            float containedDistance, containedSpeed;
            if (!DakonBallistics.Contain(distance, outwardSpeed, _bowlRadius, _tuning.CaptureBounce,
                                         out containedDistance, out containedSpeed))
                return;

            _body.linearVelocity = velocity + outward * (containedSpeed - outwardSpeed);

            if (containedDistance < distance)
                _body.position = new Vector3(_target.position.x + outward.x * containedDistance,
                                             position.y,
                                             _target.position.z + outward.z * containedDistance);
        }

        /// <summary>
        /// The point actually aimed at: down inside the bowl, scattered within a fraction of its
        /// radius.
        ///
        /// The depth is the part that matters. A hole anchor sits at the board's top surface, and
        /// a shot aimed there arrives at rim height still carrying all its horizontal speed — so
        /// it skips off the lip and finishes on the board. Aiming at the floor turns the last part
        /// of the flight into a descent into the bowl, and the walls catch what is left.
        /// </summary>
        private Vector3 AimPoint()
        {
            // Each seed is aimed at the slot it would be given in the pile, not at a random spot.
            // The slots are spread on a golden-angle spiral, so consecutive seeds into the same
            // hole aim at visibly different places — which is what stops the third seed landing
            // squarely on the second and pinging off it. The jitter on top keeps the throw from
            // looking like it is hitting marks.
            Vector3 slot = DakonPile.OffsetFor(_pileSlot, _bowlRadius * _tuning.SlotSpread);
            Vector2 jitter = Random.insideUnitCircle * (_bowlRadius * _tuning.AimScatter);

            return _target.position
                   + new Vector3(slot.x + jitter.x, -_bowlDepth * _tuning.AimDepth, slot.z + jitter.y);
        }

        /// <summary>
        /// Wait out the flight, then wait for the seed to stop moving. The timeout is the seed
        /// that never quite sleeps — balanced on two others, or rolling in a slow circle — and it
        /// matters more than it sounds: without it the pile would never be pinned and the sweep
        /// would have nothing settled to sweep.
        /// </summary>
        private IEnumerator WatchUntilSettled(float flight)
        {
            yield return new WaitForSeconds(flight * 0.9f);

            float waited = 0f;
            float still = 0f;

            while (waited < _tuning.SettleTimeoutSeconds)
            {
                float step = Time.deltaTime;
                waited += step;

                bool moving = _body.linearVelocity.sqrMagnitude > _tuning.StillSpeed * _tuning.StillSpeed;
                still = moving ? 0f : still + step;

                if (_body.IsSleeping() || still >= _tuning.StillSeconds) break;

                yield return null;
            }

            TimedOut = waited >= _tuning.SettleTimeoutSeconds;
            Settle();
        }

        /// <summary>
        /// Stop simulating this seed and, if it finished outside its bowl, put it back.
        ///
        /// Pinning matters as much as the correction. Sixty live bodies resting on each other for
        /// the rest of a match is both a running cost and a slow drift: contacts jitter, a pile
        /// creeps, and seeds end up in a neighbouring hole long after anyone threw them.
        /// </summary>
        public void Settle()
        {
            if (_watch != null) { StopCoroutine(_watch); _watch = null; }

            _inFlight = false;
            LeaveFlight();

            if (_target == null) { IsSettled = true; return; }

            Vector3 offset = transform.position - _target.position;
            offset.y = 0f;
            LandedOffset = offset.magnitude;

            if (offset.magnitude > _bowlRadius)
            {
                // The backstop. Steering and containment are meant to make this unreachable, and
                // a seed that gets here has done something neither covers — come to rest on the
                // board, most likely, having never reached its bowl at all. It is placed rather
                // than thrown again: a second hop is the thing this replaced, and one frame of
                // teleport is less of a lie than a seed sitting outside the hole it scored in.
                WasCorrected = true;

                // Down inside the bowl, not on its rim. The pile offset is measured from the
                // anchor, and the anchor is at board level — dropping a corrected seed there and
                // then freezing it would leave it hovering over the hole it is supposed to be in.
                transform.position = _target.position
                                     + Vector3.down * _bowlDepth
                                     + DakonPile.OffsetFor(_pileSlot, _pileRadius);
            }

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;

            // Pinned, not merely asleep. Left dynamic, a seed resting on the board's thin
            // non-convex shell creeps into it and drops through the table — measured at roughly
            // half of them over a match. Kinematic also stops a settled pile drifting between
            // holes long after anyone threw into it.
            _body.isKinematic = true;

            // Deliberately not parented to the anchor it landed in: the hole anchors are scaled
            // to 0.05, and hanging a rigidbody off a scaled transform makes its collider and its
            // art disagree. The board does not move, so a settled seed has nothing to follow.
            IsSettled = true;
        }
    }

    /// <summary>
    /// The dials for how a thrown seed behaves. One object so the view can hold a single
    /// serialized block and hand the same settings to every seed it spawns.
    /// </summary>
    [System.Serializable]
    public sealed class DakonSeedTuning
    {
        [Tooltip("Seeds are light; heavier ones punch through a pile instead of resting on it.")]
        public float Mass = 0.02f;

        public float Drag = 0.05f;

        [Tooltip("High enough that a seed stops spinning once it is down, not while it is in the air.")]
        public float AngularDrag = 2f;

        [Tooltip("Physics material for the seeds. Low bounce, real friction; leave empty for Unity's default.")]
        public PhysicsMaterial Material;

        [Tooltip("Random tumble given to a thrown seed, in radians per second.")]
        public float SpinSpeed = 6f;

        [Tooltip("How far above its hole's rim a settled seed is lifted before the game-over sweep throws it. Below the rim it just drives into the side of the hole.")]
        public float SweepLift = 0.03f;

        [Tooltip("Random jitter on top of the aimed slot, as a fraction of the bowl radius.")]
        [Range(0f, 1f)] public float AimScatter = 0.15f;

        [Tooltip("How much of the bowl the pile slots spread across. Wider means consecutive seeds land further apart, but too wide aims at the rim.")]
        [Range(0f, 1f)] public float SlotSpread = 0.5f;

        [Tooltip("How far down the bowl a throw aims, as a fraction of its depth. 0 aims at the rim and seeds skip off the lip; 1 aims at the floor.")]
        [Range(0f, 1f)] public float AimDepth = 0.85f;

        [Tooltip("How high a throw arcs, as a fraction of how far it travels. This sets how steeply a seed drops into the bowl — too low and it skims the far rim instead of falling in.")]
        public float ApexPerDistance = 0.4f;

        [Tooltip("Floor on the arc. Keeps a seed being nudged a few centimetres from lobbing like a throw across the board.")]
        public float MinApexHeight = 0.03f;

        [Tooltip("Ceiling on the arc, for the longest throw on the board.")]
        public float MaxApexHeight = 0.6f;

        [Tooltip("Below this speed a seed counts as stopped.")]
        public float StillSpeed = 0.02f;

        [Tooltip("How long it must stay that slow before it is pinned.")]
        public float StillSeconds = 0.25f;

        [Tooltip("Pinned regardless after this long — the seed that balances and never sleeps.")]
        public float SettleTimeoutSeconds = 4f;

        [Tooltip("How hard the arc is steered back onto its aim point after a graze. Higher converges sooner; too high and the seed visibly changes its mind.")]
        public float SteerResponse = 5f;

        [Tooltip("Ceiling on the steering, in metres per second of velocity change per second. This is what keeps a correction inside the tumble instead of on top of it.")]
        public float MaxSteerPerSecond = 0.6f;

        [Tooltip("How far above the hole's rim the bowl starts catching the seed instead of steering it.")]
        public float CaptureHeight = 0.015f;

        [Tooltip("How far outside the bowl radius a low seed still counts as arriving, as a multiple of that radius. Keeps a seed that merely passes below rim height on its way elsewhere from being caught.")]
        public float CaptureMargin = 1.5f;

        [Tooltip("How much of its outward speed a seed keeps when the bowl wall turns it back. 0 kills the rattle; 1 makes the cup a drum.")]
        [Range(0f, 1f)] public float CaptureBounce = 0.35f;
    }
}
