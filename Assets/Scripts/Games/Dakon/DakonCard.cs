using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// One card in the active player's hand (card-game style). Purely presentational: it
    /// carries the <see cref="Seed.Id"/> it stands for and reports a click back to
    /// <see cref="DakonView"/>. Card state is always derived from board.Hand — never a
    /// separate source of truth.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DakonCard : MonoBehaviour
    {
        [Tooltip("Optional label showing the seed's name / category.")]
        [SerializeField] private TextMeshProUGUI label;

        [Tooltip("Optional image showing the seed type's card sprite.")]
        [SerializeField] private Image artwork;

        [Tooltip("Button that fires the play. If null, an auto-added Button is used.")]
        [SerializeField] private Button button;

        [Tooltip("Dimmed while the card cannot be played. Added at runtime if unassigned.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("Alpha of a card that cannot be played right now.")]
        [SerializeField] private float dimmedAlpha = 0.45f;

        public string SeedId { get; private set; }

        Action<string> _onChosen;

        public void Init(Seed seed, SeedType type, Action<string> onChosen)
        {
            SeedId = seed.Id;
            _onChosen = onChosen;

            // Resolved here rather than required in the inspector, so the authored card prefab
            // does not have to be re-saved for the dimming to work.
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (label != null)
                label.text = type != null ? type.DisplayName : seed.Category.ToString();

            if (artwork != null)
            {
                artwork.sprite = type != null ? type.CardSprite : null;
                artwork.enabled = artwork.sprite != null;
            }

            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onChosen?.Invoke(SeedId));
            }
        }

        /// <summary>
        /// Locks the card and dims it. The dim matters most for a card that has just been played
        /// online: it has left the hand as far as the player is concerned, but the seed is still
        /// on its way to the server, so it cannot be destroyed yet. Greying it is what makes a
        /// fast click look like it landed.
        /// </summary>
        public void SetInteractable(bool value)
        {
            if (button != null) button.interactable = value;
            if (canvasGroup != null) canvasGroup.alpha = value ? 1f : dimmedAlpha;
        }
    }
}
