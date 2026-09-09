using NUnit.Framework;

namespace Museum.Games.Dakon.Tests
{
    public class DakonBallisticsTests
    {
        const float G = -9.81f;
        const float Tolerance = 1e-4f;

        /// <summary>Where the solved shot actually is after <paramref name="time"/> seconds.</summary>
        static void Integrate(float fromX, float fromY, float vx, float vy, float time,
                              out float x, out float y)
        {
            x = fromX + vx * time;
            y = fromY + vy * time + 0.5f * G * time * time;
        }

        [Test]
        public void Shot_arrives_at_the_target_after_the_flight_time()
        {
            float vx, vy;
            DakonBallistics.Solve(0f, 0f, 3f, 1.5f, G, 0.8f, out vx, out vy);

            float x, y;
            Integrate(0f, 0f, vx, vy, 0.8f, out x, out y);

            Assert.AreEqual(3f, x, Tolerance, "horizontal");
            Assert.AreEqual(1.5f, y, Tolerance, "vertical");
        }

        [Test]
        public void A_drop_straight_down_needs_no_horizontal_speed()
        {
            float vx, vy;
            DakonBallistics.Solve(2f, 5f, 2f, 5f - 1f, G, 0.5f, out vx, out vy);

            Assert.AreEqual(0f, vx, Tolerance, "nothing to cover horizontally");

            float x, y;
            Integrate(2f, 5f, vx, vy, 0.5f, out x, out y);
            Assert.AreEqual(4f, y, Tolerance);
        }

        /// <summary>
        /// Asking to fall further than gravity manages on its own is the only case that really
        /// starts downward — a gentle drop has to be tossed *up* first, or it arrives early.
        /// Free fall covers 1.23m in 0.5s, so 1m in 0.5s is a lob, and 2m is a throw down.
        /// </summary>
        [Test]
        public void Falling_faster_than_gravity_alone_starts_downward()
        {
            float lobX, lobY, throwX, throwY;
            DakonBallistics.Solve(0f, 5f, 0f, 4f, G, 0.5f, out lobX, out lobY);
            DakonBallistics.Solve(0f, 5f, 0f, 3f, G, 0.5f, out throwX, out throwY);

            Assert.Greater(lobY, 0f, "a 1m drop in 0.5s is slower than free fall");
            Assert.Less(throwY, 0f, "a 2m drop in 0.5s is faster than free fall");
        }

        [Test]
        public void A_target_above_the_start_is_thrown_upward()
        {
            float vx, vy;
            DakonBallistics.Solve(0f, 0f, 1f, 2f, G, 0.6f, out vx, out vy);

            Assert.Greater(vy, 0f);

            float x, y;
            Integrate(0f, 0f, vx, vy, 0.6f, out x, out y);
            Assert.AreEqual(2f, y, Tolerance);
        }

        [Test]
        public void A_longer_flight_arrives_just_the_same_but_slower_horizontally()
        {
            float fastX, fastY, slowX, slowY;
            DakonBallistics.Solve(0f, 0f, 4f, 0f, G, 0.4f, out fastX, out fastY);
            DakonBallistics.Solve(0f, 0f, 4f, 0f, G, 1.2f, out slowX, out slowY);

            Assert.Less(slowX, fastX, "more time to cover the same ground means less speed");

            float x, y;
            Integrate(0f, 0f, slowX, slowY, 1.2f, out x, out y);
            Assert.AreEqual(4f, x, Tolerance);
            Assert.AreEqual(0f, y, Tolerance);
        }

        /// <summary>
        /// A zero or negative flight time is a division by zero dressed up as a parameter. The
        /// solver clamps rather than returning an infinity that would fling a seed off the board.
        /// </summary>
        [Test]
        public void A_non_positive_flight_time_is_clamped_rather_than_dividing_by_zero()
        {
            float vx, vy;
            DakonBallistics.Solve(0f, 0f, 1f, 0f, G, 0f, out vx, out vy);

            Assert.IsFalse(float.IsInfinity(vx) || float.IsNaN(vx), "vx was " + vx);
            Assert.IsFalse(float.IsInfinity(vy) || float.IsNaN(vy), "vy was " + vy);
        }

        // ---- apex-aimed throws ----

        [Test]
        public void An_apex_throw_lands_on_its_target()
        {
            float vx, vy, flight;
            DakonBallistics.SolveByApex(1f, 0.5f, 0.6f, 0.25f, G, out vx, out vy, out flight);

            float x, y;
            Integrate(0f, 1f, vx, vy, flight, out x, out y);

            Assert.AreEqual(0.6f, x, 1e-3f, "horizontal");
            Assert.AreEqual(0.5f, y, 1e-3f, "vertical");
        }

        [Test]
        public void An_apex_throw_rises_the_requested_height_above_the_higher_end()
        {
            float vx, vy, flight;
            DakonBallistics.SolveByApex(1f, 0.5f, 0.6f, 0.25f, G, out vx, out vy, out flight);

            // Peak is where vertical speed runs out.
            float timeToPeak = vy / 9.81f;
            float x, peakY;
            Integrate(0f, 1f, vx, vy, timeToPeak, out x, out peakY);

            Assert.AreEqual(1f + 0.25f, peakY, 1e-3f);
        }

        /// <summary>
        /// The whole reason this solve exists: a throw from the far side of the board has to come
        /// down as steeply as one from beside the hole, or it skims the rim instead of dropping in.
        /// </summary>
        [Test]
        public void Distance_does_not_flatten_the_descent()
        {
            float nearX, nearY, nearT, farX, farY, farT;
            DakonBallistics.SolveByApex(1f, 0.5f, 0.2f, 0.25f, G, out nearX, out nearY, out nearT);
            DakonBallistics.SolveByApex(1f, 0.5f, 2.0f, 0.25f, G, out farX, out farY, out farT);

            Assert.AreEqual(nearT, farT, 1e-4f, "same apex, same time in the air");
            Assert.AreEqual(nearY, farY, 1e-4f, "same apex, same vertical launch");
            Assert.Greater(farX, nearX, "distance is paid for horizontally, not by flattening");
        }

        /// <summary>
        /// The nudge case: a seed being thrown a few centimetres back into its hole must not lob
        /// as high as one crossing the whole board.
        /// </summary>
        [Test]
        public void A_short_throw_arcs_lower_than_a_long_one()
        {
            float nudge = DakonBallistics.ApexFor(0.05f, 0.4f, 0.03f, 0.6f);
            float acrossTheBoard = DakonBallistics.ApexFor(1.5f, 0.4f, 0.03f, 0.6f);

            Assert.Less(nudge, acrossTheBoard);
            Assert.AreEqual(0.03f, nudge, Tolerance, "a tiny throw takes the floor, not the ceiling");
            Assert.AreEqual(0.6f, acrossTheBoard, Tolerance, "a long one is capped");
        }

        [Test]
        public void Apex_is_proportional_between_its_bounds()
        {
            Assert.AreEqual(0.4f, DakonBallistics.ApexFor(1f, 0.4f, 0.03f, 0.6f), Tolerance);
            Assert.AreEqual(0.2f, DakonBallistics.ApexFor(0.5f, 0.4f, 0.03f, 0.6f), Tolerance);
        }

        [Test]
        public void A_flat_apex_request_is_clamped_rather_than_stalling()
        {
            float vx, vy, flight;
            DakonBallistics.SolveByApex(0.5f, 0.5f, 1f, 0f, G, out vx, out vy, out flight);

            Assert.Greater(flight, 0f);
            Assert.IsFalse(float.IsNaN(vx) || float.IsInfinity(vx), "vx was " + vx);
            Assert.IsFalse(float.IsNaN(vy) || float.IsInfinity(vy), "vy was " + vy);
        }

        // --- Steering and containment: what makes one throw enough -----------------------

        [Test]
        public void The_descending_crossing_is_the_one_reported()
        {
            // Thrown up at 3 m/s from 0; it passes 0.2m on the way up and again on the way down.
            float t = DakonBallistics.SecondsToFallTo(0f, 3f, 0.2f, G);

            float x, y;
            Integrate(0f, 0f, 0f, 3f, t, out x, out y);

            Assert.AreEqual(0.2f, y, Tolerance, "it is at the height asked for");
            Assert.Greater(t, 3f / -G, "and past the apex, not before it");
        }

        [Test]
        public void A_height_gravity_never_reaches_reports_no_crossing()
        {
            // Dropped from 0.2m; it is never coming back up to 0.5m.
            Assert.AreEqual(-1f, DakonBallistics.SecondsToFallTo(0.2f, 0f, 0.5f, G), Tolerance);
        }

        /// <summary>
        /// The case the steering exists for and the case it must stay out of, together: a throw
        /// that was never disturbed is handed back untouched, and one that was is bent toward
        /// closing the gap.
        /// </summary>
        [Test]
        public void Steering_corrects_an_offset_and_leaves_an_on_target_throw_alone()
        {
            const float onTarget = 1.2f;
            float undisturbed = DakonBallistics.Steer(onTarget * 0.4f, 0.4f, onTarget, 5f, 0.6f, 0.02f);
            Assert.AreEqual(onTarget, undisturbed, Tolerance, "nothing to fix, nothing changed");

            float corrected = DakonBallistics.Steer(0.6f, 0.4f, onTarget, 5f, 0.6f, 0.02f);
            Assert.Greater(corrected, onTarget, "0.6m in 0.4s needs more than 1.2 m/s");
        }

        [Test]
        public void Steering_never_pulls_harder_than_its_cap()
        {
            // A metre off with a twentieth of a second left is an impossible ask; the answer has
            // to be a nudge rather than the 20 m/s the arithmetic alone would call for.
            float v = DakonBallistics.Steer(1f, 0.2f, 0f, 5f, 0.6f, 0.02f);

            Assert.LessOrEqual(v, 0.6f * 0.02f + Tolerance, "capped at the per-second rate");
            Assert.Greater(v, 0f, "but still in the right direction");
        }

        [Test]
        public void Steering_stops_once_there_is_no_flight_left_to_use()
        {
            float v = DakonBallistics.Steer(0.5f, 0.001f, 1f, 5f, 0.6f, 0.02f);

            Assert.AreEqual(1f, v, Tolerance, "no lurch in the last millisecond");
        }

        [Test]
        public void A_seed_inside_the_bowl_is_left_to_rattle()
        {
            float distance, speed;
            bool changed = DakonBallistics.Contain(0.02f, 0.3f, 0.06f, 0.35f, out distance, out speed);

            Assert.IsFalse(changed);
            Assert.AreEqual(0.02f, distance, Tolerance);
            Assert.AreEqual(0.3f, speed, Tolerance, "moving outward but nowhere near the wall");
        }

        [Test]
        public void A_seed_leaving_the_bowl_is_turned_back_and_slowed()
        {
            float distance, speed;
            bool changed = DakonBallistics.Contain(0.07f, 0.4f, 0.06f, 0.35f, out distance, out speed);

            Assert.IsTrue(changed);
            Assert.AreEqual(0.06f, distance, Tolerance, "put back on the wall, not at the centre");
            Assert.AreEqual(-0.14f, speed, Tolerance, "inward, at 35% of what it arrived with");
        }

        [Test]
        public void A_seed_at_the_wall_moving_inward_is_not_pushed_again()
        {
            float distance, speed;
            DakonBallistics.Contain(0.06f, -0.2f, 0.06f, 0.35f, out distance, out speed);

            Assert.AreEqual(-0.2f, speed, Tolerance, "already going where it should");
        }

    }
}
