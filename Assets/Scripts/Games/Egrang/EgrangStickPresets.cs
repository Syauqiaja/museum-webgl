namespace Museum.Games.Egrang
{
    /// <summary>
    /// The shipped tuning for one stick, as plain data: the specification shown on its card plus the
    /// two numbers that decide how hard it is to walk on. Zone tables are described by half-widths
    /// about the centre of the track rather than as five explicit bands, so a preset cannot be written
    /// asymmetric by accident — an off-centre green would make the bar unfair in one sweep direction.
    ///
    /// The copy here is specification only — shape, size, formula, bearing area. No preset carries a
    /// difficulty word, because the player is meant to read the numbers and work out for themselves
    /// which pole is the forgiving one, the way they would sizing up real stilts. The card does not
    /// print <see cref="AreaText"/> at all; it is kept here as the answer to the sum the card asks
    /// for, and because the ordering of the three sticks is only legible against it.
    /// </summary>
    public readonly struct EgrangStickPreset
    {
        public readonly EgrangStickShape Shape;
        public readonly string DisplayName;

        /// <summary>Cross-section shape, e.g. "Persegi".</summary>
        public readonly string ShapeText;

        /// <summary>The measurement that defines the cross-section, e.g. "Sisi 8 cm".</summary>
        public readonly string SizeText;

        /// <summary>Area formula for the shape, e.g. "L = s²" — the working, not just the answer.</summary>
        public readonly string FormulaText;

        /// <summary>Bearing area of the cross-section, e.g. "64 cm²". Not shown on the card.</summary>
        public readonly string AreaText;

        /// <summary>Half the width of the green band. The green band spans 0.5 ± this.</summary>
        public readonly float GreenHalfWidth;

        /// <summary>Width of each yellow band, sitting immediately outside green on both sides.</summary>
        public readonly float YellowWidth;

        public readonly float SweepSeconds;

        public EgrangStickPreset(EgrangStickShape shape, string displayName, string shapeText,
                                 string sizeText, string formulaText, string areaText,
                                 float greenHalfWidth, float yellowWidth, float sweepSeconds)
        {
            Shape = shape;
            DisplayName = displayName;
            ShapeText = shapeText;
            SizeText = sizeText;
            FormulaText = formulaText;
            AreaText = areaText;
            GreenHalfWidth = greenHalfWidth;
            YellowWidth = yellowWidth;
            SweepSeconds = sweepSeconds;
        }

        /// <summary>
        /// Expands the preset into the five-band table the bar scores against:
        /// red | yellow | green | yellow | red, centred on the track.
        /// </summary>
        public SkillCheckZones BuildZones()
        {
            float greenStart = 0.5f - GreenHalfWidth;
            float greenEnd = 0.5f + GreenHalfWidth;
            float yellowStart = greenStart - YellowWidth;
            float yellowEnd = greenEnd + YellowWidth;

            return new SkillCheckZones(
                new SkillCheckZone(0f, yellowStart, EgrangStepResult.Fail),
                new SkillCheckZone(yellowStart, greenStart, EgrangStepResult.Half),
                new SkillCheckZone(greenStart, greenEnd, EgrangStepResult.Full),
                new SkillCheckZone(greenEnd, yellowEnd, EgrangStepResult.Half),
                new SkillCheckZone(yellowEnd, 1f, EgrangStepResult.Fail));
        }

        /// <summary>
        /// Milliseconds the cursor spends inside green on one pass — the actual size of the window the
        /// player is aiming at, which neither the band width nor the sweep time tells you on its own.
        /// </summary>
        public float GreenWindowMilliseconds => GreenHalfWidth * 2f * SweepSeconds * 1000f;
    }

    /// <summary>
    /// The three shipped stilts. Kept in code rather than only in assets so the difficulty spread is
    /// unit-tested: a profile asset is filled from here and can then be retuned in the editor.
    ///
    /// Both knobs move together with cross-sectional area — the square pole's 64 cm² gives the widest
    /// green and the slowest sweep, the triangle's 32 cm² the narrowest and the fastest — and they are
    /// spaced roughly a factor of three apart at each step. Anything subtler and the three poles play
    /// the same, which makes the choice decoration; the cards state only the specification, so the
    /// difference has to be legible in the hand rather than in a label.
    ///
    /// Green window per pass: persegi ~720 ms, lingkaran ~225 ms, segitiga ~85 ms.
    /// </summary>
    public static class EgrangStickPresets
    {
        public static readonly EgrangStickPreset Persegi = new EgrangStickPreset(
            EgrangStickShape.Persegi,
            displayName: "Egrang Persegi",
            shapeText: "Persegi",
            sizeText: "Sisi 8 cm",
            formulaText: "L = s²",
            areaText: "64 cm²",
            greenHalfWidth: 0.18f,
            yellowWidth: 0.16f,
            sweepSeconds: 2.0f);

        public static readonly EgrangStickPreset Lingkaran = new EgrangStickPreset(
            EgrangStickShape.Lingkaran,
            displayName: "Egrang Lingkaran",
            shapeText: "Lingkaran",
            sizeText: "Diameter 8 cm",
            formulaText: "L = π × r²",
            areaText: "50,24 cm²",
            greenHalfWidth: 0.09f,
            yellowWidth: 0.10f,
            sweepSeconds: 1.25f);

        public static readonly EgrangStickPreset Segitiga = new EgrangStickPreset(
            EgrangStickShape.Segitiga,
            displayName: "Egrang Segitiga",
            shapeText: "Segitiga sama kaki",
            sizeText: "Alas & tinggi 8 cm",
            formulaText: "L = ½ × a × t",
            areaText: "32 cm²",
            greenHalfWidth: 0.05f,
            yellowWidth: 0.055f,
            sweepSeconds: 0.85f);

        /// <summary>All three, widest cross-section first — the order the selection cards read in.</summary>
        public static readonly EgrangStickPreset[] All = { Persegi, Lingkaran, Segitiga };

        /// <summary>The preset for a shape. Unknown shapes fall back to the widest cross-section, so a new enum member cannot ship an unplayably empty zone table.</summary>
        public static EgrangStickPreset For(EgrangStickShape shape)
        {
            switch (shape)
            {
                case EgrangStickShape.Segitiga: return Segitiga;
                case EgrangStickShape.Lingkaran: return Lingkaran;
                default: return Persegi;
            }
        }
    }
}
