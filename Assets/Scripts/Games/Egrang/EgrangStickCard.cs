using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// One stilt on the selection screen: its name and the specification of its cross-section. Put
    /// this on the card root, next to a <c>Button</c>.
    ///
    /// The card states measurements and nothing else — no difficulty word, no stability meter, no
    /// preview of the timing bar. Which pole is the forgiving one is for the player to work out from
    /// the numbers, the same judgement they would make picking up real stilts, and a label claiming
    /// "Mudah" is also one more thing that can drift out of step with the zone table as it is retuned.
    ///
    /// The bearing area is deliberately <b>not</b> shown, though the profile still carries it: the
    /// size and the formula are on the card, and working the area out from them is the thing this
    /// minigame is teaching. Printing the answer next to the question skips the lesson.
    ///
    /// Pure view: it holds no game state and decides nothing. <see cref="EgrangStickSelector"/> owns
    /// the choice.
    /// </summary>
    public sealed class EgrangStickCard : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("Button that picks this stick. Leave empty to look for one on this object.")]
        [SerializeField] private Button button;
        [Tooltip("Shown only while this card is the current pick — a frame, a glow, a tick. Optional.")]
        [SerializeField] private GameObject selectedIndicator;

        [Header("Weight")]
        [Tooltip("Solid plate behind the card's text. Optional; without one the card relies on its frame sprite, which the 3D scene reads straight through.")]
        [SerializeField] private Image backdrop;
        [Tooltip("Backdrop colour while this card is not the pick.")]
        [SerializeField] private Color idleTint = new Color(0.078f, 0.063f, 0.047f, 0.72f);
        [Tooltip("Backdrop colour while this card is the pick — heavier, so the chosen stilt reads at a glance.")]
        [SerializeField] private Color selectedTint = new Color(0.129f, 0.102f, 0.071f, 1f);

        [Header("Specification")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("Cross-section shape, e.g. \"Persegi\".")]
        [SerializeField] private TMP_Text shapeText;
        [Tooltip("Defining measurement, e.g. \"Sisi 8 cm\".")]
        [SerializeField] private TMP_Text sizeText;
        [Tooltip("Area formula, e.g. \"L = s²\" — the player works the area out from this and the size.")]
        [SerializeField] private TMP_Text formulaText;
        [Tooltip("Worked-out area, e.g. \"64 cm²\" — the answer the formula and size lead to. Optional.")]
        [SerializeField] private TMP_Text areaText;

        Action<EgrangStickProfile> _onSelect;

        /// <summary>The stick this card is showing, or null before <see cref="Bind"/>.</summary>
        public EgrangStickProfile Profile { get; private set; }

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>
        /// Fills the card in from a profile and says who to tell when it is clicked. Called by the
        /// selector at startup; a null profile hides the card, which is how a scene laid out for
        /// three sticks survives being given two.
        /// </summary>
        public void Bind(EgrangStickProfile profile, Action<EgrangStickProfile> onSelect)
        {
            Profile = profile;
            _onSelect = onSelect;

            if (profile == null)
            {
                gameObject.SetActive(false);
                return;
            }

            Set(nameText, profile.DisplayName);
            Set(shapeText, profile.ShapeText);
            Set(sizeText, profile.SizeText);
            Set(formulaText, profile.FormulaText);
            Set(areaText, profile.AreaText);

            SetSelected(false);
        }

        /// <summary>
        /// Shows or hides the "this is the current pick" state: the gold frame, and the weight of
        /// the plate behind the text. The unpicked cards stay legible rather than dimming away —
        /// they are the comparison the player is being asked to make.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (selectedIndicator != null) selectedIndicator.SetActive(selected);
            if (backdrop != null) backdrop.color = selected ? selectedTint : idleTint;
        }

        void HandleClick() => _onSelect?.Invoke(Profile);

        static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
