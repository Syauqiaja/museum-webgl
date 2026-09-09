using TMPro;
using UnityEngine;

namespace Museum.Lobby
{
    /// <summary>
    /// One seat in the room. Purely presentational: it renders the slot it is handed and owns
    /// nothing (Assets/Docs/ui-style.md §9) — same shape as EgrangStickCard.
    ///
    /// An empty seat is drawn, not hidden. Visible empty chairs tell a player how many more people
    /// the game is waiting for; collapsing the list would not.
    /// </summary>
    public sealed class LobbySlotView : MonoBehaviour
    {
        [Header("Text")]
        [Tooltip("Player's nickname, or the waiting placeholder when the seat is empty.")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("Seat number, e.g. \"Pemain 2\".")]
        [SerializeField] private TMP_Text seatText;

        [Header("Badges")]
        [Tooltip("Shown on the host's seat only. Optional.")]
        [SerializeField] private GameObject hostBadge;
        [Tooltip("Shown on this client's own seat only. Optional.")]
        [SerializeField] private GameObject youBadge;

        [Header("Copy")]
        [SerializeField] private string emptyLabel = "Menunggu…";

        public void Render(LobbySlot slot, int seatNumber, bool isMe)
        {
            if (seatText != null)
            {
                seatText.text = $"Pemain {seatNumber}";
            }

            if (nameText != null)
            {
                nameText.text = slot.IsEmpty ? emptyLabel : slot.DisplayName;
            }

            if (hostBadge != null)
            {
                hostBadge.SetActive(!slot.IsEmpty && slot.IsHost);
            }

            if (youBadge != null)
            {
                youBadge.SetActive(!slot.IsEmpty && isMe);
            }
        }
    }
}
