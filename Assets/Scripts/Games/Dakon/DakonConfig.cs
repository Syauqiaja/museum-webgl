namespace Museum.Games.Dakon
{
    /// <summary>
    /// Tunables for a Dakon game. Defaults are the settled v7 ruleset values. Pool splits
    /// evenly between the two categories; if <see cref="PoolSeeds"/> is odd the extra seed
    /// is Monocot.
    ///
    /// These are only ever *used* offline, by <see cref="LocalDakonSession"/>: online the server
    /// owns the rules and this client renders what it is told. They are still the server's
    /// numbers, though — the server's `src/games/dakon/DakonConfig.ts` holds the same values,
    /// and they must be changed together or a hotseat match stops being the same game as an
    /// online one. Nothing in a scene may override them; that is why the view no longer
    /// serializes pool and grab size.
    /// </summary>
    public sealed class DakonConfig
    {
        /// <summary>Total seeds in the pool. Default 60 (30 Monocot + 30 Dicot) → 6 grabs of 10 = 3 turns/player.</summary>
        public int PoolSeeds = 60;

        /// <summary>
        /// Seeds grabbed per turn (grabs min(GrabSize, poolRemaining)). Equal to
        /// <see cref="HolesPerSide"/> on purpose: a turn is "one seed into each of your own holes",
        /// and a hand larger than the side would have nowhere legal to go.
        /// </summary>
        public int GrabSize = 10;

        /// <summary>Holes per player side. Board ring = HolesPerSide * 2.</summary>
        public int HolesPerSide = 10;

        /// <summary>
        /// Every hole's type, in ring order, and it is not a roll.
        ///
        /// The board's twenty type icons are painted into `Assets/Texture2D/dakon_surface.png` —
        /// a corn kernel here, a taproot there — and a player reads them to decide where a seed
        /// belongs. They were meaningless for as long as the types were shuffled per match:
        /// nothing in either repo could move the paint, so a hole under a taproot was dicot only
        /// by luck. Pinning the ruleset to the picture is the half that can move.
        ///
        /// Ring order follows the anchors, which follow the art. Indices 0–9 are seat 0's row —
        /// the near one on screen, left to right. Indices 10–19 are seat 1's, and that row's `p0`
        /// is the *rightmost* hole, so 10–19 read right to left across the far row. Each entry
        /// below names the icon it was taken from; change one only when the texture changes.
        /// </summary>
        public SeedCategory[] HoleTypes =
        {
            // Seat 0 — near row, screen left to right.
            SeedCategory.Monocot,   //  0  one cotyledon (corn kernel)
            SeedCategory.Dicot,     //  1  pinnate and palmate leaves
            SeedCategory.Dicot,     //  2  two cotyledons
            SeedCategory.Dicot,     //  3  five-petal flower
            SeedCategory.Monocot,   //  4  fibrous roots
            SeedCategory.Dicot,     //  5  taproot
            SeedCategory.Monocot,   //  6  three-part flower
            SeedCategory.Monocot,   //  7  scattered vascular bundles
            SeedCategory.Monocot,   //  8  one cotyledon (corn kernel)
            SeedCategory.Dicot,     //  9  taproot

            // Seat 1 — far row. Its p0 is the rightmost hole, so this runs right to left.
            SeedCategory.Dicot,     // 10  pinnate and palmate leaves
            SeedCategory.Monocot,   // 11  parallel-veined leaf
            SeedCategory.Dicot,     // 12  five-petal flower
            SeedCategory.Monocot,   // 13  fibrous roots
            SeedCategory.Dicot,     // 14  ringed vascular bundles
            SeedCategory.Monocot,   // 15  parallel-veined leaf
            SeedCategory.Monocot,   // 16  three-part flower
            SeedCategory.Monocot,   // 17  scattered vascular bundles
            SeedCategory.Dicot,     // 18  two cotyledons
            SeedCategory.Dicot,     // 19  ringed vascular bundles
        };

        /// <summary>SeedType ids drawn round-robin for Monocot seeds. View maps id -> SeedType asset.</summary>
        public string[] MonocotTypeIds = { "monocot" };

        /// <summary>SeedType ids drawn round-robin for Dicot seeds. View maps id -> SeedType asset.</summary>
        public string[] DicotTypeIds = { "dicot" };

        public int TotalHoles => HolesPerSide * 2;

        public static DakonConfig Default => new DakonConfig();
    }
}
