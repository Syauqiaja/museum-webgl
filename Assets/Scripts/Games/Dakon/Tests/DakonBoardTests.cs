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

        // First hole on the active player's side that has not taken a seed this turn.
        static int FreeHole(DakonBoard b)
        {
            int lo = b.ActivePlayer * b.HolesPerSide;
            for (int i = lo; i < lo + b.HolesPerSide; i++)
                if (!b.IsSown(i)) return i;
            Assert.Fail("no free hole on the active side");
            return -1;
        }

        // Play the active player's whole current turn by dropping the first hand seed into
        // the first free hole each step. Returns the DropResult of the final drop of the turn.
        static DropResult PlayOneTurn(DakonBoard b)
        {
            DropResult r = default;
            while (true)
            {
                r = b.DropSeed(b.ActivePlayer, b.Hand[0].Id, FreeHole(b));
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
            Assert.AreEqual(0u, b.SownMask);
            Assert.AreEqual(DakonConfig.Default.GrabSize, b.Hand.Count);
            Assert.AreEqual(DakonConfig.Default.PoolSeeds - DakonConfig.Default.GrabSize, b.PoolCount);
        }

        /// <summary>
        /// A hand is exactly one side's worth of holes: that is what turns "place ten seeds" into
        /// "fill your ten holes", and the only reason the one-seed-per-hole rule can never leave
        /// a player holding seeds with nowhere to put them.
        /// </summary>
        [Test]
        public void A_hand_is_one_side_of_holes()
        {
            Assert.AreEqual(DakonConfig.Default.HolesPerSide, DakonConfig.Default.GrabSize);
        }

        // ---- Scoring ----

        [Test]
        public void Match_scores_active_player_in_seed_category()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int next = FreeHole(b);
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
            int next = FreeHole(b);
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
            int next = FreeHole(b);
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

            var r = b.DropSeed(1 - active, b.Hand[0].Id, FreeHole(b));

            Assert.IsFalse(r.Ok);
            Assert.AreEqual(DakonError.NotYourTurn, r.Error);
            Assert.AreEqual(pool, b.PoolCount);
            Assert.AreEqual(handCount, b.Hand.Count);
        }

        [Test]
        public void Opponents_hole_is_rejected_with_InvalidHole()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int theirs = (1 - active) * b.HolesPerSide;
            int handCount = b.Hand.Count;

            var r = b.DropSeed(active, b.Hand[0].Id, theirs);

            Assert.IsFalse(r.Ok);
            Assert.AreEqual(DakonError.InvalidHole, r.Error);
            Assert.AreEqual(handCount, b.Hand.Count);
        }

        [Test]
        public void Out_of_range_hole_is_rejected_with_InvalidHole()
        {
            var b = NewGame(1);
            Assert.AreEqual(DakonError.InvalidHole, b.DropSeed(b.ActivePlayer, b.Hand[0].Id, -1).Error);
            Assert.AreEqual(DakonError.InvalidHole, b.DropSeed(b.ActivePlayer, b.Hand[0].Id, b.HoleCount).Error);
        }

        [Test]
        public void Unknown_seed_is_rejected_with_SeedNotInHand()
        {
            var b = NewGame(1);
            var r = b.DropSeed(b.ActivePlayer, "not-a-real-seed", FreeHole(b));
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(DakonError.SeedNotInHand, r.Error);
        }

        // ---- One seed per hole ----

        [Test]
        public void Any_own_hole_may_be_chosen_in_any_order()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int handCount = b.Hand.Count;

            // Last hole on the side first, then the first: the order used to be forced.
            int last = active * b.HolesPerSide + b.HolesPerSide - 1;
            int first = active * b.HolesPerSide;

            Assert.IsTrue(b.DropSeed(active, b.Hand[0].Id, last).Ok);
            Assert.IsTrue(b.DropSeed(active, b.Hand[0].Id, first).Ok);
            Assert.AreEqual(handCount - 2, b.Hand.Count);
            Assert.IsTrue(b.IsSown(last));
            Assert.IsTrue(b.IsSown(first));
        }

        [Test]
        public void A_second_seed_into_the_same_hole_is_rejected_with_HoleAlreadySown()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int hole = FreeHole(b);

            Assert.IsTrue(b.DropSeed(active, b.Hand[0].Id, hole).Ok);
            int handCount = b.Hand.Count;

            var r = b.DropSeed(active, b.Hand[0].Id, hole);

            Assert.IsFalse(r.Ok);
            Assert.AreEqual(DakonError.HoleAlreadySown, r.Error);
            Assert.AreEqual(handCount, b.Hand.Count);
        }

        [Test]
        public void Sown_mask_tracks_the_holes_filled_this_turn()
        {
            var b = NewGame(1);
            int active = b.ActivePlayer;
            int a = active * b.HolesPerSide + 2;
            int c = active * b.HolesPerSide + 7;

            b.DropSeed(active, b.Hand[0].Id, a);
            b.DropSeed(active, b.Hand[0].Id, c);

            Assert.AreEqual((1u << a) | (1u << c), b.SownMask);
        }

        /// <summary>
        /// A rejected drop must not mark the hole: otherwise a mistyped seed id would quietly
        /// eat a hole and leave the player one short of finishing the turn.
        /// </summary>
        [Test]
        public void A_refused_drop_does_not_sow_the_hole()
        {
            var b = NewGame(1);
            int hole = FreeHole(b);

            b.DropSeed(b.ActivePlayer, "not-a-real-seed", hole);

            Assert.IsFalse(b.IsSown(hole));
        }

        // ---- Turns ----

        [Test]
        public void A_turn_ends_when_every_own_hole_has_a_seed()
        {
            var b = NewGame(1);
            int side = b.ActivePlayer;

            var r = PlayOneTurn(b);

            Assert.IsTrue(r.TurnEnded);
            Assert.AreEqual(1 - side, b.ActivePlayer);
            for (int i = 0; i < b.HoleCount; i++)
                Assert.IsFalse(b.IsSown(i), $"hole {i} still sown after the turn ended");
            Assert.AreEqual(0u, b.SownMask);
        }

        [Test]
        public void Player1_sows_only_the_second_side()
        {
            var b = NewGame(1);
            PlayOneTurn(b); // finish P0
            Assert.AreEqual(1, b.ActivePlayer);

            Assert.AreEqual(DakonError.InvalidHole, b.DropSeed(1, b.Hand[0].Id, 0).Error);
            Assert.IsTrue(b.DropSeed(1, b.Hand[0].Id, 10).Ok);
            Assert.IsTrue(b.DropSeed(1, b.Hand[0].Id, 19).Ok);
        }

        [Test]
        public void Turn_end_swaps_active_and_deals_a_new_hand()
        {
            var b = NewGame(1);
            PlayOneTurn(b);
            Assert.AreEqual(1, b.ActivePlayer);
            Assert.AreEqual(DakonConfig.Default.GrabSize, b.Hand.Count);
            Assert.AreEqual(DakonConfig.Default.PoolSeeds - 2 * DakonConfig.Default.GrabSize, b.PoolCount);
        }

        [Test]
        public void Each_player_gets_three_turns()
        {
            var b = NewGame(1);
            int turns = 0;
            while (b.Phase == Phase.InProgress)
            {
                PlayOneTurn(b);
                turns++;
            }
            Assert.AreEqual(6, turns);
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
