using TMPro;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The HUD's list of who is racing and how far along they are: one row per lane, named, with a
    /// fill that tracks that lane's progress. Put this on the HUD strip that holds the rows.
    ///
    /// The big progress rail beside it (<see cref="EgrangProgressView"/>) draws the local lane only
    /// — it answers "how much further do I have to go". This answers the other question, "am I
    /// winning", which needs everyone on screen at once and so is a list rather than a rail.
    ///
    /// Pure view: <see cref="EgrangRace"/> names the lanes once and pushes progress each frame.
    /// </summary>
    public sealed class EgrangRosterView : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Row
        {
            [Tooltip("Racer's name, e.g. \"Budi (Kamu)\".")]
            public TMP_Text nameText;
            [Tooltip("Optional. Stretched from 0 to its full width as that lane advances.")]
            public RectTransform fill;
            [Tooltip("Optional root, hidden when the lane is empty.")]
            public GameObject root;
        }

        [Tooltip("Rows in lane order: lane 1 first.")]
        [SerializeField] private Row[] rows = new Row[0];
        [Tooltip("Appended to this client's own row.")]
        [SerializeField] private string localSuffix = " (Kamu)";

        readonly float[] _fillWidths = new float[8];

        /// <summary>Names a lane's row. An empty name hides the row — that lane has nobody in it.</summary>
        public void SetName(int lane, string displayName, bool isLocal)
        {
            Row row = RowAt(lane);
            if (row == null) return;

            bool named = !string.IsNullOrEmpty(displayName);

            if (row.root != null) row.root.SetActive(named);
            if (row.nameText != null)
            {
                row.nameText.text = named ? displayName + (isLocal ? localSuffix : string.Empty) : string.Empty;
            }
        }

        /// <summary>Draws how far along a lane is, 0 at the start line and 1 at the finish.</summary>
        public void SetProgress(int lane, float normalized)
        {
            Row row = RowAt(lane);
            if (row?.fill == null) return;

            // Full width comes from the rail behind the fill, not from the fill itself: the fill
            // ships at zero width — a lane that has not moved must not read as a finished one — and
            // its own width is what gets overwritten below.
            if (lane < _fillWidths.Length && _fillWidths[lane] <= 0f)
            {
                var rail = row.fill.parent as RectTransform;
                _fillWidths[lane] = rail != null ? rail.rect.width : row.fill.rect.width;
            }

            float full = lane < _fillWidths.Length ? _fillWidths[lane] : row.fill.rect.width;
            row.fill.sizeDelta = new Vector2(full * Mathf.Clamp01(normalized), row.fill.sizeDelta.y);
        }

        Row RowAt(int lane) => rows != null && lane >= 0 && lane < rows.Length ? rows[lane] : null;
    }
}
