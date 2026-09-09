using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Museum.Games.Dakon.Tests
{
    public class DakonBoardTests
    {
        static DakonBoard NewGame(int seed, DakonConfig config = null)
        {
            var b = new DakonBoard(config ?? DakonConfig.Default, seed);
            b.StartGame();
            return b;
        }

        // First hand seed whose category matches (wantMatch) or not the given hole type.
        static Seed PickSeed(IReadOnlyList<Seed> hand, SeedCategory holeType, bool wantMatch)
        {
            foreach (var s in hand)
                if ((s.Category == holeType) == wantMatch)
                    return s;
            Assert.Fail($"hand lacks a seed with match={wantMatch} for holeType {holeType}");
            return default;
        }

        // Play the active player's whole current turn by dropping the first hand seed each
        // step. Returns the DropResult of the final drop of the turn.
        static DropResult PlayOneTurn(DakonBoard b)
        {
            DropResult r = default;
            while (true)
            {
                r = b.DropSeed(b.ActivePlayer, b.Hand[0].Id, b.NextHoleIndex);
                Assert.IsTrue(r.Ok, "auto-play drop should be legal");
                if (r.TurnEnded || r.GameOver) return r;
            }
        }

        // ---- StartGame ----

        [Test]
        public void StartGame_sets_initial_state()
        {
            var b = NewGame(1);
            Assert.AreEqual(Phase.InProgress, b.Phase);
            Assert.AreEqual(0, b.ActivePlayer);
            Assert.AreEqual(0, b.NextHoleIndex);
            Assert.AreEqual(15, b.Hand.Count);
            Assert.AreEqual(DakonConfig.Default.PoolSeeds - 15, b.PoolCount);
        }

        // ---- Scoring ----

        [Test]
        public void Match_scores_active_player_in_seed_category()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int next = b.NextHoleIndex;
            var holeType = b.HoleTypeAt(next);
            var seed = PickSeed(b.Hand, holeType, wantMatch: true);
            int before = b.Store(active, holeType);

            var r = b.DropSeed(active, seed.Id, next);

            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.WasMatch);
            Assert.AreEqual(active, r.ScoringPlayer);
            Assert.AreEqual(seed.Category, r.ScoringCategory);
            Assert.AreEqual(holeType, r.ScoringCategory);
            Assert.AreEqual(before + 1, b.Store(active, holeType));
        }

        [Test]
        public void Mismatch_scores_opponent_in_seed_category()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int opp = 1 - active;
            int next = b.NextHoleIndex;
            var holeType = b.HoleTypeAt(next);
            var seed = PickSeed(b.Hand, holeType, wantMatch: false);
            int before = b.Store(opp, seed.Category);

            var r = b.DropSeed(active, seed.Id, next);

            Assert.IsTrue(r.Ok);
            Assert.IsFalse(r.WasMatch);
            Assert.AreEqual(opp, r.ScoringPlayer);
            Assert.AreEqual(seed.Category, r.ScoringCategory);
            Assert.AreEqual(before + 1, b.Store(opp, seed.Category));
        }

        [Test]
        public void Voluntary_mismatch_is_legal_when_a_matching_seed_exists()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int next = b.NextHoleIndex;
            var holeType = b.HoleTypeAt(next);
            // Precondition: a matching seed IS available...
            PickSeed(b.Hand, holeType, wantMatch: true);
            // ...but we deliberately play a non-matching one. Must be allowed (no forced-match).
            var mismatch = PickSeed(b.Hand, holeType, wantMatch: false);

            var r = b.DropSeed(active, mismatch.Id, next);

            Assert.IsTrue(r.Ok);
            Assert.IsFalse(r.WasMatch);
        }

        // ---- Validation ----

        [Test]
        public void Off_turn_drop_is_rejected_with_NotYourTurn()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int pool = b.PoolCount;
            int handCount = b.Hand.Count;

            var r = b.DropSeed(1 - active, b.Hand[0].Id, b.NextHoleIndex);

            Assert.IsFalse(r.Ok);
            Assert.AreEqual(DakonError.NotYourTurn, r.Error);
            Assert.AreEqual(pool, b.PoolCount);
            Assert.AreEqual(handCount, b.Hand.Count);
        }

        [Test]
        public void Wrong_hole_index_is_rejected_with_InvalidHole()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int wrong = (b.NextHoleIndex + 1) % 20;
            int handCount = b.Hand.Count;

            var r = b.DropSeed(active, b.Hand[0].Id, wrong);

            Assert.IsFalse(r.Ok);
            Assert.AreEqual(DakonError.InvalidHole, r.Error);
            Assert.AreEqual(handCount, b.Hand.Count);
        }

        [Test]
        public void Unknown_seed_is_rejected_with_SeedNotInHand()
        {
            var b = NewGame(1);
            var r = b.DropSeed(b.ActivePlayer, "not-a-real-seed", b.NextHoleIndex);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(DakonError.SeedNotInHand, r.Error);
        }

        // ---- Ring / turns ----

        [Test]
        public void Player1_turn_wraps_the_ring_from_index_10()
        {
            var b = NewGame(1);
            PlayOneTurn(b); // finish P0
            Assert.AreEqual(1, b.ActivePlayer);
            Assert.AreEqual(10, b.NextHoleIndex);

            var visited = new List<int>();
            while (true)
            {
                visited.Add(b.NextHoleIndex);
                var r = b.DropSeed(b.ActivePlayer, b.Hand[0].Id, b.NextHoleIndex);
                if (r.TurnEnded || r.GameOver) break;
            }

            var expected = new List<int> { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 0, 1, 2, 3, 4 };
            CollectionAssert.AreEqual(expected, visited);
        }

        [Test]
        public void Turn_end_swaps_active_and_deals_a_new_hand()
        {
            var b = NewGame(1);
            PlayOneTurn(b);
            Assert.AreEqual(1, b.ActivePlayer);
            Assert.AreEqual(15, b.Hand.Count);
            Assert.AreEqual(DakonConfig.Default.PoolSeeds - 30, b.PoolCount);
        }

        // ---- Endgame ----

        [Test]
        public void Pool_drains_to_a_finished_game()
        {
            var b = NewGame(1);
            DropResult last = default;
            while (b.Phase == Phase.InProgress)
                last = PlayOneTurn(b);

            Assert.AreEqual(Phase.Finished, b.Phase);
            Assert.IsTrue(last.GameOver);
            Assert.AreEqual(0, b.PoolCount);
        }

        [Test]
        public void All_seeds_are_scored_and_winner_matches_totals()
        {
            var b = NewGame(1);
            while (b.Phase == Phase.InProgress)
                PlayOneTurn(b);

            int t0 = b.Total(0);
            int t1 = b.Total(1);
            Assert.AreEqual(DakonConfig.Default.PoolSeeds, t0 + t1, "every pool seed must land in some store");

            int? w = b.Winner;
            if (t0 > t1) Assert.AreEqual(0, w);
            else if (t1 > t0) Assert.AreEqual(1, w);
            else Assert.IsNull(w);
        }

        [Test]
        public void Total_equals_sum_of_both_category_stores()
        {
            var b = NewGame(1);
            PlayOneTurn(b);
            int expected = b.Store(0, SeedCategory.Monocot) + b.Store(0, SeedCategory.Dicot);
            Assert.AreEqual(expected, b.Total(0));
        }

        // ---- Hole layout ----

        [Test]
        public void Each_side_has_exactly_five_dicot_and_five_monocot()
        {
            var b = NewGame(7);
            AssertSideSplit(b, 0, 9);
            AssertSideSplit(b, 10, 19);
        }

        static void AssertSideSplit(DakonBoard b, int lo, int hi)
        {
            int dicot = 0, monocot = 0;
            for (int i = lo; i <= hi; i++)
            {
                if (b.HoleTypeAt(i) == SeedCategory.Dicot) dicot++;
                else monocot++;
            }
            Assert.AreEqual(5, dicot, $"holes {lo}..{hi} dicot count");
            Assert.AreEqual(5, monocot, $"holes {lo}..{hi} monocot count");
        }

        /// <summary>
        /// The layout is pinned to the icons painted on the board texture, so it is the same
        /// board every match and on both sides of the wire. The 5/5 count above would pass on a
        /// rolled layout too; this is the assertion that would catch one coming back.
        /// </summary>
        [Test]
        public void The_holes_are_laid_out_as_the_painted_board_whatever_the_seed()
        {
            var pinned = DakonConfig.Default.HoleTypes;

            foreach (int seed in new[] { 1, 7, 42, 9999 })
            {
                var b = NewGame(seed);
                for (int i = 0; i < 20; i++)
                    Assert.AreEqual(pinned[i], b.HoleTypeAt(i), $"seed {seed}, hole {i}");
            }
        }

        [Test]
        public void Same_seed_reproduces_the_same_hole_layout()
        {
            var a = NewGame(42);
            var b = NewGame(42);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(a.HoleTypeAt(i), b.HoleTypeAt(i), $"hole {i}");
        }

        /// <summary>
        /// The seed no longer decides the board, so a board must not be able to write back
        /// through the config it was handed and retype every match after it.
        /// </summary>
        [Test]
        public void A_board_cannot_retype_the_config_it_was_built_from()
        {
            var config = DakonConfig.Default;
            var board = new DakonBoard(config, 1);
            board.StartGame();

            SeedCategory before = config.HoleTypes[0];
            Assert.AreEqual(before, board.HoleTypeAt(0), "the board did not take the pinned type");
            Assert.AreEqual(SeedCategory.Monocot, before, "hole 0 is painted with one cotyledon");
        }
    }
}
