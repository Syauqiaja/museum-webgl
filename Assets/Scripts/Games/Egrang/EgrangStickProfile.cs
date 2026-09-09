using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// One selectable stilt: the specification shown on its selection card and the two numbers that
    /// make it easy or hard — its zone table and its sweep time.
    ///
    /// Difficulty comes from the pole's cross-section. At the same 8 cm maximum width the square pole
    /// bears on 64 cm², the round one on 50,24 cm² and the triangular one on 32 cm², and more bearing
    /// surface means a steadier stilt. In this game that reads as a wider green band (more of the
    /// sweep scores a clean step) and a slower sweep (more time to aim at it).
    ///
    /// The card copy is deliberately specification only — no "Mudah", no stability meter, and not
    /// even the area itself: the card gives the size and the formula, and the player does the sum.
    /// The difficulty lives in how the bar behaves rather than in a word on a card that could drift
    /// away from the numbers. <see cref="AreaText"/> is kept as the answer, unshown.
    ///
    /// One asset per shape, under <c>Assets/Data/Egrang/</c>. A fresh asset fills itself in from
    /// <see cref="EgrangStickPresets"/>, so the shipped tuning lives in code where the tests can
    /// check it, and the assets stay editable for retuning without a recompile.
    /// </summary>
    [CreateAssetMenu(menuName = "Museum/Egrang/Stick Profile", fileName = "EgrangStick")]
    public sealed class EgrangStickProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Cross-section this profile describes. Drives the preset applied by Reset and the Apply Shape Preset menu item.")]
        [SerializeField] private EgrangStickShape shape = EgrangStickShape.Persegi;

        [Header("Card specification")]
        [Tooltip("Name on the selection card, e.g. \"Egrang Persegi\".")]
        [SerializeField] private string displayName = "Egrang Persegi";
        [Tooltip("Cross-section shape, e.g. \"Persegi\".")]
        [SerializeField] private string shapeText = "Persegi";
        [Tooltip("The measurement that defines the cross-section, e.g. \"Sisi 8 cm\".")]
        [SerializeField] private string sizeText = "Sisi 8 cm";
        [Tooltip("Area formula for the shape, e.g. \"L = s²\".")]
        [SerializeField] private string formulaText = "L = s²";
        [Tooltip("Bearing area, e.g. \"64 cm²\". Not shown on the card — the player works it out from the size and the formula — and never scored against; the zone table below is what the game scores.")]
        [SerializeField] private string areaText = "64 cm²";

        [Header("Difficulty")]
        [Tooltip("Zone table the bar scores against while this stick is in use. Wider green = easier. These numbers are the truth; the bar's colours are baked from them.")]
        [SerializeField] private SkillCheckZones zones = new SkillCheckZones();
        [Tooltip("Seconds for one edge-to-edge cursor pass. Lower is harder.")]
        [Min(0.05f)]
        [SerializeField] private float sweepSeconds = 2f;

        public EgrangStickShape Shape => shape;
        public string DisplayName => displayName;
        public string ShapeText => shapeText;
        public string SizeText => sizeText;
        public string FormulaText => formulaText;
        public string AreaText => areaText;

        /// <summary>The zone table to score against. Callers that keep it should copy it — see <see cref="SkillCheckBar.Configure"/>.</summary>
        public SkillCheckZones Zones => zones;

        public float SweepSeconds => sweepSeconds;

        /// <summary>Fills a new asset in from its shape's preset, so a freshly created profile is already playable.</summary>
        void Reset() => ApplyPreset();

        /// <summary>Switches the asset to another shape and takes that shape's preset wholesale.</summary>
        public void ApplyPreset(EgrangStickShape newShape)
        {
            shape = newShape;
            ApplyPreset();
        }

        /// <summary>
        /// Overwrites every field from <see cref="EgrangStickPresets"/> for the current
        /// <see cref="shape"/>. Exposed on the asset's context menu because changing the shape of an
        /// existing asset otherwise leaves the old shape's specification and numbers behind, which is
        /// the one way this data can quietly end up lying to the player.
        /// </summary>
        [ContextMenu("Apply Shape Preset")]
        public void ApplyPreset()
        {
            EgrangStickPreset preset = EgrangStickPresets.For(shape);

            displayName = preset.DisplayName;
            shapeText = preset.ShapeText;
            sizeText = preset.SizeText;
            formulaText = preset.FormulaText;
            areaText = preset.AreaText;
            zones = preset.BuildZones();
            sweepSeconds = preset.SweepSeconds;
        }

#if UNITY_EDITOR
        /// <summary>Warns on a zone table the author can fix, at author time rather than mid-game.</summary>
        void OnValidate()
        {
            if (zones != null && zones.Zones.Count > 0 && !zones.Validate(out string error))
            {
                Debug.LogWarning($"{nameof(EgrangStickProfile)} '{name}' has bad zone data: {error}", this);
            }
        }
#endif
    }
}
