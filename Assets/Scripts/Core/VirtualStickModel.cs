using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The geometry of an on-screen thumb stick, with no MonoBehaviour and no scene: given where
    /// a finger landed and where it is now, what movement does that mean, and where does the knob
    /// get drawn.
    /// </summary>
    /// <remarks>
    /// The origin is passed in rather than fixed because the stick floats — it appears wherever
    /// the thumb lands in its half of the screen, which is what stops a player having to look
    /// down to find it.
    ///
    /// This is input shaping, not a game rule: it decides how a gesture reads, never how far a
    /// player travels. Vector2 is UnityEngine's, which is fine here for the same reason.
    /// </remarks>
    public readonly struct VirtualStickModel
    {
        /// <summary>Screen pixels from the origin at which movement reaches full speed.</summary>
        public readonly float Radius;

        /// <summary>Screen pixels of slop before any movement is reported at all.</summary>
        public readonly float DeadZone;

        public VirtualStickModel(float radius, float deadZone)
        {
            Radius = Mathf.Max(1f, radius);
            DeadZone = Mathf.Clamp(deadZone, 0f, Radius - 1f);
        }

        /// <summary>
        /// Movement for this frame: direction from the origin, magnitude ramping 0 to 1 between
        /// the dead zone and the radius. Matches the range the WASD 2DVector composite produces,
        /// so <c>FPSController</c> needs no second speed scale.
        /// </summary>
        public Vector2 Evaluate(Vector2 origin, Vector2 current)
        {
            Vector2 offset = current - origin;
            float distance = offset.magnitude;
            if (distance <= DeadZone) return Vector2.zero;

            Vector2 direction = offset / distance;
            float travel = Mathf.InverseLerp(DeadZone, Radius, distance);
            return direction * travel;
        }

        /// <summary>Where to draw the knob relative to the ring's centre.</summary>
        public Vector2 KnobOffset(Vector2 origin, Vector2 current)
            => Vector2.ClampMagnitude(current - origin, Radius);
    }
}
