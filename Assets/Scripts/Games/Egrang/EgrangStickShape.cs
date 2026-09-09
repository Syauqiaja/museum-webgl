namespace Museum.Games.Egrang
{
    /// <summary>
    /// Cross-section of a stilt pole, which is how the game names its three difficulties.
    /// Cross-sectional area is the stability parameter: at the same 8 cm maximum width, a square
    /// pole is the widest bearing surface (64 cm²), a round one is smaller (50,24 cm²), and an
    /// isosceles triangle is the smallest (32 cm²). More area means a steadier stilt, so the order
    /// of these members is also the order of difficulty.
    /// </summary>
    public enum EgrangStickShape
    {
        /// <summary>Square section, 64 cm². Widest, steadiest, easiest — the beginner's pick.</summary>
        Persegi = 0,

        /// <summary>Round section, 50,24 cm². Middling stability, tilts evenly in every direction.</summary>
        Lingkaran = 1,

        /// <summary>Isosceles triangular section, 32 cm². Smallest bearing surface, hardest to hold.</summary>
        Segitiga = 2,
    }
}
