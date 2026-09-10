using TMPro;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// A walk-on engklek court in the lobby: numbered petak that light and chime as the visitor
    /// steps on them in order, and a fanfare on reaching the gunung at the top. Stepping out of
    /// order does nothing — the museum is not a place to be told off — the court simply waits
    /// for the right petak. Tiles are <see cref="HopscotchTile"/> triggers under this object.
    /// </summary>
    /// <remarks>
    /// Progress is counted in rows, not tiles, because a real court has side-by-side petak
    /// (4–5, 7–8) landed on together; a visitor walking up the middle enters both at once, in
    /// whichever order the physics engine reports them.
    ///
    /// Other visitors see and hear each accepted step — the petak lights, the chime or fanfare
    /// plays — through <see cref="MuseumInteractions"/>, but the progress is each visitor's own:
    /// two people hopping at once must not advance or restart each other's run.
    /// </remarks>
    public class HopscotchCourse : MonoBehaviour, IRemoteInteractable
    {
        [Tooltip("Every petak of the court, bottom to top.")]
        [SerializeField] private HopscotchTile[] tiles;

        [Tooltip("Row of each tile, parallel to tiles: 0 for petak 1, rising toward the gunung.")]
        [SerializeField] private int[] rows;

        [SerializeField] private AudioSource source;

        [Tooltip("Progress readout beside the court; blank to skip.")]
        [SerializeField] private TMP_Text status;

        [SerializeField] private string idleText = "Injak petak 1 sampai gunung, berurutan";
        [SerializeField] private string doneText = "Sampai gunung! Mulai lagi dari petak 1";

        [Tooltip("Seconds a lit petak stays lit after being stepped on.")]
        [SerializeField] private float glow = 6f;

        private int _row = -1;
        private int _lastRow;

        private void Awake()
        {
            if (source != null) GalleryStation.TuneSource(source);
            if (status != null) status.text = idleText;

            _lastRow = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null) tiles[i].Bind(this, i);
                if (i < rows.Length) _lastRow = Mathf.Max(_lastRow, rows[i]);
            }
        }

        private void OnEnable() => MuseumInteractions.Register(MuseumInteractions.Engklek, this);

        private void OnDisable() => MuseumInteractions.Unregister(MuseumInteractions.Engklek, this);

        private int RowOf(int index) => index < rows.Length ? rows[index] : index;

        /// <summary>Called by a tile the player has just stepped onto.</summary>
        internal void Stepped(int index)
        {
            int row = RowOf(index);

            if (row == 0) _row = 0;                // the first petak always (re)starts the course
            else if (row == _row + 1) _row = row;  // the next row: advance
            else if (row != _row) return;          // out of order: wait

            tiles[index].Light(glow);
            MuseumInteractions.ReportLocal(MuseumInteractions.Engklek, index);

            if (_row >= _lastRow)
            {
                ProceduralAudio.Play(source, ProceduralAudio.Fanfare(), 0.9f);
                if (status != null) status.text = doneText;
                _row = -1;
                return;
            }

            ProceduralAudio.Play(source, ProceduralAudio.Chime(row), 0.8f);
            if (status != null) status.text = $"Petak {index + 1} — lanjut ke baris berikutnya";
        }

        /// <summary>
        /// Another visitor's accepted step: the same light and sound, reaching the gunung gets the
        /// fanfare — and nothing else. This visitor's own progress and status line are untouched.
        /// </summary>
        public void PlayRemote(int index)
        {
            if (tiles == null || index < 0 || index >= tiles.Length || tiles[index] == null) return;

            int row = RowOf(index);
            tiles[index].Light(glow);

            if (row >= _lastRow) ProceduralAudio.Play(source, ProceduralAudio.Fanfare(), 0.9f);
            else ProceduralAudio.Play(source, ProceduralAudio.Chime(row), 0.8f);
        }
    }
}
