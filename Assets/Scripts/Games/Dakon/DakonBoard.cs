using System;
using System.Collections.Generic;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// Pure-C# Dakon model (v7 ruleset). Sole owner of all game state and rules; contains no
    /// UnityEngine dependency so it can be unit-tested without a scene. Deterministic given a
    /// fixed rng seed. See Assets/Docs/games/dakon.md.
    ///
    /// v7 replaced the forced sowing order: the player chooses which held seed goes into which
    /// of their own holes, one seed per hole per turn. A hand is the size of a side, so a turn
    /// is "fill your ten holes" and the decision is which unavoidable mismatches to take.
    /// </summary>
    public sealed class DakonBoard
    {
        readonly DakonConfig _config;
        readonly Random _rng;

        SeedCategory[] _holes;                 // hole type per ring index, fixed for the game
        bool[] _sown;                          // hole received a seed this turn
        readonly List<Seed> _pool = new List<Seed>();
        readonly List<Seed> _hand = new List<Seed>();
        readonly List<Seed>[,] _store;         // [player, category] -> scored seeds

        int _activePlayer;
        Phase _phase = Phase.Waiting;
        int _monocotTypeCursor;
        int _dicotTypeCursor;

        public DakonBoard(DakonConfig config, int rngSeed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _rng = new Random(rngSeed);
            _store = new List<Seed>[2, 2];
            for (int p = 0; p < 2; p++)
                for (int c = 0; c < 2; c++)
                    _store[p, c] = new List<Seed>();
        }

        // ---- setup ----

        public void StartGame()
        {
            _holes = HoleLayout();
            _sown = new bool[_holes.Length];
            FillPool();

            _activePlayer = 0;
            _phase = Phase.InProgress;
            GrabHand();
        }

        /// <summary>
        /// The hole types, copied from <see cref="DakonConfig.HoleTypes"/> rather than rolled.
        ///
        /// They used to be shuffled per side each match, which quietly made the twenty type icons
        /// painted on the board decorative: a player reading a taproot over a hole was right half
        /// the time. The picture cannot move, so the ruleset does — see the array's own comment
        /// for how ring order maps onto the two printed rows.
        ///
        /// Copied, not aliased, because the config is shared and a board must not be able to
        /// write through it into the next game.
        /// </summary>
        SeedCategory[] HoleLayout()
        {
            var layout = new SeedCategory[_config.TotalHoles];
            var pinned = _config.HoleTypes;

            for (int i = 0; i < layout.Length; i++)
                layout[i] = i < pinned.Length ? pinned[i] : SeedCategory.Monocot;

            return layout;
        }

        void FillPool()
        {
            _pool.Clear();
            int total = _config.PoolSeeds;
            int monocot = (total + 1) / 2; // extra seed is Monocot if odd
            for (int i = 0; i < total; i++)
            {
                var category = i < monocot ? SeedCategory.Monocot : SeedCategory.Dicot;
                _pool.Add(new Seed($"s{i}", category, NextTypeId(category)));
            }
        }

        string NextTypeId(SeedCategory category)
        {
            if (category == SeedCategory.Monocot)
            {
                var set = _config.MonocotTypeIds;
                return set[_monocotTypeCursor++ % set.Length];
            }
            else
            {
                var set = _config.DicotTypeIds;
                return set[_dicotTypeCursor++ % set.Length];
            }
        }

        // Grab min(GrabSize, poolRemaining) random seeds from the pool into the hand.
        void GrabHand()
        {
            _hand.Clear();
            int take = Math.Min(_config.GrabSize, _pool.Count);
            for (int i = 0; i < take; i++)
            {
                int idx = _rng.Next(_pool.Count);
                _hand.Add(_pool[idx]);
                _pool.RemoveAt(idx);
            }
        }

        /// <summary>
        /// Which seat a ring index belongs to: 0 for the first <paramref name="holesPerSide"/>
        /// holes, 1 for the rest. Static because the view needs the same answer for a hole it
        /// is about to raycast, before any session has said anything.
        /// </summary>
        public static int SideOf(int holeIndex, int holesPerSide) => holeIndex < holesPerSide ? 0 : 1;

        // ---- play ----

        /// <summary>
        /// Drop one held seed into one of the active player's own holes. Every rejection is a
        /// rule, not an exception; the caller turns the error into wording.
        /// </summary>
        public DropResult DropSeed(int player, string seedId, int holeIndex)
        {
            if (_phase != Phase.InProgress) return DropResult.Fail(DakonError.InvalidHole);
            if (player != _activePlayer) return DropResult.Fail(DakonError.NotYourTurn);
            if (holeIndex < 0 || holeIndex >= _holes.Length) return DropResult.Fail(DakonError.InvalidHole);
            if (SideOf(holeIndex, _config.HolesPerSide) != _activePlayer) return DropResult.Fail(DakonError.InvalidHole);
            if (_sown[holeIndex]) return DropResult.Fail(DakonError.HoleAlreadySown);

            int handIdx = _hand.FindIndex(s => s.Id == seedId);
            if (handIdx < 0) return DropResult.Fail(DakonError.SeedNotInHand);

            Seed seed = _hand[handIdx];
            bool wasMatch = seed.Category == _holes[holeIndex];
            int scoringPlayer = wasMatch ? _activePlayer : 1 - _activePlayer;

            _store[scoringPlayer, (int)seed.Category].Add(seed);
            _hand.RemoveAt(handIdx);
            _sown[holeIndex] = true;

            var result = new DropResult
            {
                Ok = true,
                ScoringPlayer = scoringPlayer,
                ScoringCategory = seed.Category,
                WasMatch = wasMatch,
            };

            if (_hand.Count == 0)
            {
                result.TurnEnded = true;
                Array.Clear(_sown, 0, _sown.Length);

                if (_pool.Count == 0)
                {
                    _phase = Phase.Finished;
                    result.GameOver = true;
                }
                else
                {
                    _activePlayer = 1 - _activePlayer;
                    GrabHand();
                }
            }

            return result;
        }

        // ---- read-only accessors ----

        public int ActivePlayer => _activePlayer;
        public IReadOnlyList<Seed> Hand => _hand;
        public int HoleCount => _holes.Length;
        public int HolesPerSide => _config.HolesPerSide;
        public SeedCategory HoleTypeAt(int index) => _holes[index];

        /// <summary>True once a seed has landed in this hole during the current turn.</summary>
        public bool IsSown(int index) => _sown != null && index >= 0 && index < _sown.Length && _sown[index];

        /// <summary>
        /// The sown holes as a bitmask, bit i = hole i — the shape the server syncs it in, so the
        /// two sessions read the same thing. Twenty holes fit comfortably in 32 bits.
        /// </summary>
        public uint SownMask
        {
            get
            {
                uint mask = 0;
                if (_sown == null) return mask;
                for (int i = 0; i < _sown.Length && i < 32; i++)
                    if (_sown[i]) mask |= 1u << i;
                return mask;
            }
        }

        public int Store(int player, SeedCategory category) => _store[player, (int)category].Count;
        public IReadOnlyList<Seed> StoreSeeds(int player, SeedCategory category) => _store[player, (int)category];
        public int Total(int player) => Store(player, SeedCategory.Monocot) + Store(player, SeedCategory.Dicot);
        public int PoolCount => _pool.Count;
        public Phase Phase => _phase;

        /// <summary>Only meaningful when Phase == Finished. 0/1 = winner, null = tie.</summary>
        public int? Winner
        {
            get
            {
                if (_phase != Phase.Finished) return null;
                int t0 = Total(0), t1 = Total(1);
                if (t0 > t1) return 0;
                if (t1 > t0) return 1;
                return null;
            }
        }
    }
}
