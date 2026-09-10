using TMPro;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The lobby's tembang stage: Enter plays a slendro phrase in the shape of a dolanan song
    /// and walks its lyric across the stage's label, one line per bar, so the visitor hears
    /// the tune and reads the words together.
    /// </summary>
    public class SongStation : GalleryStation
    {
        [SerializeField] private AudioSource source;

        [Tooltip("Where the lyric lines appear, one at a time, in time with the phrase.")]
        [SerializeField] private TMP_Text lyric;

        [Tooltip("Shown on the lyric label when nothing is playing.")]
        [SerializeField] private string idleText = "Jamuran, ya gégé thok…";

        [Tooltip("Lyric lines, shown in order; each holds for secondsPerLine.")]
        [TextArea(2, 8)]
        [SerializeField] private string[] lines =
        {
            "Jamuran, ya gégé thok",
            "Jamur apa, ya gégé thok",
            "Jamur gajih mbejijih sak ara-ara",
            "Sira mbadhé jamur apa?",
        };

        [SerializeField] private float secondsPerLine = 1.8f;

        private float _started = -100f;
        private int _shown = -1;

        protected override void Awake()
        {
            base.Awake();
            if (source != null) TuneSource(source);
            if (lyric != null) lyric.text = idleText;
        }

        protected override string StationId => MuseumInteractions.Tembang;

        protected override void OnInteract()
        {
            ProceduralAudio.Play(source, ProceduralAudio.DolananPhrase(), 0.9f);
            _started = Time.time;
            _shown = -1;
        }

        /// <summary>
        /// Someone else's song is heard, not read: the lyric banner stays with whoever is reading
        /// it here, so a press across the room does not restart the words under their eyes.
        /// </summary>
        protected override void OnRemoteInteract(int index)
        {
            ProceduralAudio.Play(source, ProceduralAudio.DolananPhrase(), 0.9f);
        }

        protected override void Update()
        {
            base.Update();
            if (lyric == null || lines == null || lines.Length == 0) return;

            float t = Time.time - _started;
            int index = t < 0f ? -1 : Mathf.FloorToInt(t / secondsPerLine);

            if (index >= lines.Length)
            {
                if (_shown != -2)
                {
                    lyric.text = idleText;
                    _shown = -2;
                }

                return;
            }

            if (index != _shown && index >= 0)
            {
                lyric.text = lines[index];
                _shown = index;
            }
        }
    }
}
