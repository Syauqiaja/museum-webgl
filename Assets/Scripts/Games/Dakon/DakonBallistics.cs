using System;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// The launch velocity that carries a seed from where it is to where it belongs, under
    /// gravity alone.
    ///
    /// Solving for a velocity and then letting physics run is what separates a thrown seed from
    /// an interpolated one: after the launch nothing steers it, so it bounces off the rim, rolls
    /// down the bowl and settles against the seeds already there. A lerp cannot do any of that,
    /// because a lerp already knows where it is going to stop.
    ///
    /// Split out here, free of UnityEngine, because it is arithmetic and arithmetic is worth
    /// testing without a scene. The axes are passed in flat rather than as vectors for the same
    /// reason — the caller does the projecting.
    /// </summary>
    public static class DakonBallistics
    {
        /// <summary>Shortest flight we will ever ask for; below this the solve gets explosive.</summary>
        public const float MinFlightSeconds = 0.05f;

        /// <summary>
        /// Velocity that puts a body at (<paramref name="toHorizontal"/>, <paramref name="toVertical"/>)
        /// exactly <paramref name="flightSeconds"/> after it leaves
        /// (<paramref name="fromHorizontal"/>, <paramref name="fromVertical"/>).
        ///
        /// Straight from the equations of motion: horizontal is unaccelerated, so it is distance
        /// over time; vertical has to add back the drop gravity will have applied by then.
        /// </summary>
        /// <param name="gravity">Signed — negative points down.</param>
        public static void Solve(float fromHorizontal, float fromVertical,
                                 float toHorizontal, float toVertical,
                                 float gravity, float flightSeconds,
                                 out float horizontalVelocity, out float verticalVelocity)
        {
            // A caller asking for an instant arrival is asking for infinite speed. Clamping beats
            // returning one: an infinity here would leave the seed somewhere off the table.
            float t = Math.Max(MinFlightSeconds, flightSeconds);

            horizontalVelocity = (toHorizontal - fromHorizontal) / t;
            verticalVelocity = (toVertical - fromVertical - 0.5f * gravity * t * t) / t;
        }

        /// <summary>
        /// How high a throw of this length should arc.
        ///
        /// The apex has to scale with the distance or it is wrong at both ends: a fixed arc tall
        /// enough to drop into a hole across the board sends a seed being nudged five centimetres
        /// half a metre into the air, which reads as a hiccup rather than a throw. Proportional
        /// keeps every throw the same shape — a short one is a small version of a long one.
        /// </summary>
        public static float ApexFor(float distance, float perDistance, float minApex, float maxApex)
        {
            float apex = Math.Abs(distance) * perDistance;

            if (apex < minApex) apex = minApex;
            if (apex > maxApex) apex = maxApex;

            return apex;
        }

        /// <summary>
        /// Velocity for a throw that peaks <paramref name="apex"/> above whichever end is higher,
        /// then falls onto the target.
        ///
        /// This is the one to aim a seed with. Choosing a flight *time* instead lets the arc go
        /// as flat as the distance demands, and a seed arriving almost horizontally skips off the
        /// far rim of a six-centimetre cup instead of dropping into it. Fixing the apex fixes the
        /// descent angle, so a throw from across the board falls in as steeply as one from beside
        /// it — only slower.
        /// </summary>
        /// <param name="gravity">Signed — negative points down.</param>
        public static void SolveByApex(float fromVertical, float toVertical, float horizontalDistance,
                                       float apex, float gravity,
                                       out float horizontalVelocity, out float verticalVelocity,
                                       out float flightSeconds)
        {
            float g = Math.Abs(gravity);
            if (g < 1e-5f) g = 9.81f;

            // The peak has to clear both ends, or "rising to the apex" is a fall.
            float peak = Math.Max(fromVertical, toVertical) + Math.Max(0.001f, apex);

            verticalVelocity = (float)Math.Sqrt(2f * g * (peak - fromVertical));

            float rise = verticalVelocity / g;
            float fall = (float)Math.Sqrt(2f * (peak - toVertical) / g);

            flightSeconds = Math.Max(MinFlightSeconds, rise + fall);
            horizontalVelocity = horizontalDistance / flightSeconds;
        }

        /// <summary>
        /// When a body launched from <paramref name="fromVertical"/> with
        /// <paramref name="verticalVelocity"/> will next be at <paramref name="toVertical"/> on
        /// the way *down*, or -1 if gravity never takes it that low.
        ///
        /// This is the clock the throw is steered against. A seed only has to be over the right
        /// spot at one instant — the moment it drops past the bowl's mouth — so the correction is
        /// spread across however long is left until then rather than applied as a lurch.
        /// </summary>
        /// <param name="gravity">Signed — negative points down.</param>
        public static float SecondsToFallTo(float fromVertical, float verticalVelocity,
                                            float toVertical, float gravity)
        {
            float a = 0.5f * gravity;
            float b = verticalVelocity;
            float c = fromVertical - toVertical;

            if (Math.Abs(a) < 1e-6f) return b < -1e-6f ? -c / b : -1f;

            float disc = b * b - 4f * a * c;
            if (disc < 0f) return -1f;

            float root = (float)Math.Sqrt(disc);
            float t1 = (-b + root) / (2f * a);
            float t2 = (-b - root) / (2f * a);

            // The later of the two crossings is the descending one; a body still on its way up
            // passes the height twice and only the second pass is an arrival.
            float t = Math.Max(t1, t2);
            return t > 0f ? t : -1f;
        }

        /// <summary>
        /// One frame of steering on one horizontal axis: nudge <paramref name="currentVelocity"/>
        /// toward whatever would close <paramref name="offset"/> in <paramref name="secondsLeft"/>.
        ///
        /// A launch is solved exactly, so with nothing in the way this returns what it was given.
        /// It earns its place after a graze — the rim, the seed already in the bowl — which is
        /// what used to send a seed wide and cost it a second throw. Correcting continuously
        /// spends that error over the rest of the arc instead of over a whole extra hop.
        ///
        /// Two limits keep it from reading as a homing missile: the blend is exponential, so the
        /// correction is strongest while there is still flight left to hide it in, and
        /// <paramref name="maxCorrectionPerSecond"/> caps how hard it may ever pull. Whatever
        /// those two leave behind is small enough for the bowl itself to catch.
        /// </summary>
        public static float Steer(float offset, float secondsLeft, float currentVelocity,
                                  float responsiveness, float maxCorrectionPerSecond,
                                  float deltaSeconds)
        {
            if (secondsLeft <= MinFlightSeconds || deltaSeconds <= 0f) return currentVelocity;

            float needed = offset / secondsLeft;
            float blend = 1f - (float)Math.Exp(-responsiveness * deltaSeconds);
            float delta = (needed - currentVelocity) * blend;

            float cap = maxCorrectionPerSecond * deltaSeconds;
            if (delta > cap) delta = cap;
            if (delta < -cap) delta = -cap;

            return currentVelocity + delta;
        }

        /// <summary>
        /// The wall the modelled bowl does not have. Given how far a seed is from the bowl's axis
        /// and how fast it is moving away from it, returns the distance and speed it is allowed.
        ///
        /// A six-centimetre cup with a thin shell lets a seed that arrives with any pace left
        /// clip the lip and leave, and a seed that leaves is a seed that has to be thrown again.
        /// Reflecting it inward at <paramref name="bounce"/> instead keeps the rattle — the part
        /// that reads as a real seed finding its place — and drops the escape, which never read
        /// as anything.
        /// </summary>
        /// <returns>True if either value was changed.</returns>
        public static bool Contain(float distance, float outwardSpeed, float bowlRadius,
                                   float bounce,
                                   out float containedDistance, out float containedOutwardSpeed)
        {
            containedDistance = distance;
            containedOutwardSpeed = outwardSpeed;

            if (distance <= bowlRadius && outwardSpeed <= 0f) return false;

            if (distance > bowlRadius) containedDistance = bowlRadius;
            if (outwardSpeed > 0f && distance >= bowlRadius) containedOutwardSpeed = -outwardSpeed * bounce;

            return containedDistance != distance || containedOutwardSpeed != outwardSpeed;
        }
    }
}
