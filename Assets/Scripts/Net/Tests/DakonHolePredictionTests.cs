using NUnit.Framework;

namespace Museum.Net.Tests
{
    /// <summary>
    /// The pipelining rules, and the mobile-only race that made every drop illegal: an accepted
    /// drop is announced by `drop_applied` immediately, while the state patch carrying the new
    /// `nextHoleIndex` follows on the room's patch interval. Anchoring on the synced index inside
    /// that window aims at the hole that was already filled.
    /// </summary>
    public class DakonHolePredictionTests
    {
        const int Holes = 20;

        [Test]
        public void First_drop_of_a_turn_uses_the_synced_index()
        {
            var prediction = new DakonHolePrediction();

            Assert.AreEqual(10, prediction.Aim("s1", serverNextHole: 10, holeCount: Holes));
        }

        [Test]
        public void A_burst_counts_forward_from_the_last_aim()
        {
            var prediction = new DakonHolePrediction();

            Assert.AreEqual(0, prediction.Aim("s1", 0, Holes));
            Assert.AreEqual(1, prediction.Aim("s2", 0, Holes));
            Assert.AreEqual(2, prediction.Aim("s3", 0, Holes));
        }

        [Test]
        public void An_accepted_drop_anchors_the_next_aim_before_the_patch_arrives()
        {
            var prediction = new DakonHolePrediction();

            prediction.Aim("s1", serverNextHole: 4, holeCount: Holes);
            prediction.Applied("s1", holeIndex: 4, turnEnded: false);

            // The patch has not landed, so the synced index is still the stale 4. Aiming there
            // again is the bug this class exists for.
            Assert.AreEqual(5, prediction.Aim("s2", serverNextHole: 4, holeCount: Holes));
        }

        [Test]
        public void The_opponents_accepted_drop_anchors_us_too()
        {
            var prediction = new DakonHolePrediction();

            prediction.Applied("opponent-seed", holeIndex: 12, turnEnded: false);

            Assert.AreEqual(13, prediction.Aim("s1", serverNextHole: 12, holeCount: Holes));
        }

        [Test]
        public void The_aim_wraps_around_the_ring()
        {
            var prediction = new DakonHolePrediction();

            prediction.Applied("s1", holeIndex: Holes - 1, turnEnded: false);

            Assert.AreEqual(0, prediction.Aim("s2", serverNextHole: Holes - 1, holeCount: Holes));
        }

        [Test]
        public void A_turn_ending_drop_anchors_nothing()
        {
            var prediction = new DakonHolePrediction();

            prediction.Aim("s1", 6, Holes);
            prediction.Applied("s1", holeIndex: 6, turnEnded: true);

            // The server restarted the ring at the other seat's first hole; only the patch knows
            // where that is.
            Assert.AreEqual(0, prediction.Aim("s2", serverNextHole: 0, holeCount: Holes));
        }

        [Test]
        public void A_refusal_drops_every_guess_queued_behind_it()
        {
            var prediction = new DakonHolePrediction();

            prediction.Aim("s1", 3, Holes);
            prediction.Aim("s2", 3, Holes);
            Assert.AreEqual(2, prediction.InFlightCount);

            prediction.Reset();

            Assert.AreEqual(0, prediction.InFlightCount);
            Assert.AreEqual(3, prediction.Aim("s3", serverNextHole: 3, holeCount: Holes));
        }

        [Test]
        public void Only_the_matching_seed_leaves_the_queue()
        {
            var prediction = new DakonHolePrediction();

            prediction.Aim("s1", 0, Holes);
            prediction.Aim("s2", 0, Holes);

            prediction.Applied("s1", 0, turnEnded: false);
            Assert.AreEqual(1, prediction.InFlightCount);

            // s2 is still outstanding, so the burst keeps counting from its own last aim (1)
            // rather than restarting from what the server has confirmed (0).
            Assert.AreEqual(2, prediction.Aim("s3", serverNextHole: 0, holeCount: Holes));
        }

        [Test]
        public void An_empty_board_aims_at_zero_instead_of_dividing_by_it()
        {
            var prediction = new DakonHolePrediction();

            prediction.Applied("s1", holeIndex: 0, turnEnded: false);

            Assert.AreEqual(0, prediction.Aim("s2", serverNextHole: 0, holeCount: 0));
        }
    }
}
