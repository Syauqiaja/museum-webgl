using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Core
{
    /// <summary>
    /// The world-space plaque that shows one game's lesson text, a page at a time. Content
    /// comes from a <see cref="GameLessonData"/> asset; paging is driven by
    /// <see cref="LessonReader"/>, which owns the keyboard and the proximity trigger.
    /// </summary>
    /// <remarks>
    /// The long sections are paginated by TMP itself (<c>TextOverflowModes.Page</c>) at a
    /// fixed font size. Auto-shrinking a 1300-character section would make it unreadable from
    /// standing distance, and scrolling is not available: the museum runs in FPS mode with the
    /// cursor locked, so there is no pointer to scroll with.
    /// </remarks>
    public class LessonPanel : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("The lesson to show. Leave empty to resolve lessonKey from Resources/lessons instead.")]
        [SerializeField] private GameLessonData lesson;

        [Tooltip("Fallback key, resolved as Resources/lessons/<key>. Survives a lost inspector reference.")]
        [SerializeField] private string lessonKey;

        [Header("Labels")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private TMP_Text hintLabel;

        [Header("Page indicator")]
        [Tooltip("Parent of the page dots. Its children are created at runtime, one per page.")]
        [SerializeField] private RectTransform dotsRoot;

        [Tooltip("Dot template, a child of dotsRoot. Cloned per page and left disabled itself.")]
        [SerializeField] private Image dotTemplate;

        [Header("Keycaps")]
        [Tooltip("Backgrounds of the two key hints. Tinted, not clicked — the museum has no cursor.")]
        [SerializeField] private Image prevKeycap;
        [SerializeField] private Image nextKeycap;

        [Header("Tabs")]
        [Tooltip("Tab backgrounds, one per lesson section and in the same order.")]
        [SerializeField] private Image[] tabFrames;

        [Tooltip("Tab labels, parallel to tabFrames.")]
        [SerializeField] private TMP_Text[] tabLabels;

        [Tooltip("Gold rule along the top of a tab. Shown on the active one only.")]
        [SerializeField] private Image[] tabAccents;

        [Tooltip("Each tab's height, which the strip lays out. Raising one is what marks it active.")]
        [SerializeField] private LayoutElement[] tabHeights;

        [Header("Copy")]
        [SerializeField] private string inRangeHint = "Q E  ganti halaman";
        [SerializeField] private string outOfRangeHint = "Dekati untuk membaca";

        // ui-style.md §5. Duplicated as literals because MuseumUIStyle is editor-only.
        private static readonly Color Gold = new Color(0.757f, 0.624f, 0.380f, 1f);
        private static readonly Color Tan = new Color(0.847f, 0.753f, 0.631f, 1f);
        private static readonly Color DotIdle = new Color(0.847f, 0.753f, 0.631f, 0.35f);
        private static readonly Color TabIdleFrame = new Color(1f, 1f, 1f, 0.263f);
        private static readonly Color TabIdleLabel = new Color(0.847f, 0.753f, 0.631f, 0.6f);

        // The raised active tab is what makes the strip read as tabs rather than as chips, so
        // the two heights have to match the ones the builder lays the strip out with.
        private const float TabActiveHeight = 40f;
        private const float TabIdleHeight = 33f;

        private LessonPage[] _pages = new LessonPage[0];
        private int[] _pageCounts = new int[0];
        private Image[] _dots = new Image[0];
        private int _current;

        /// <summary>True while a visitor is close enough for the keys to do anything.</summary>
        public bool ReaderInRange { get; private set; }

        private void Start()
        {
            if (lesson == null && !string.IsNullOrEmpty(lessonKey))
            {
                lesson = Resources.Load<GameLessonData>($"lessons/{lessonKey}");
            }

            if (lesson == null)
            {
                // Same call as StreamedVideoScreen makes for a missing catalog: log and stay
                // walkable rather than throw. A blank plaque is better than a broken museum.
                Debug.LogError($"[LessonPanel] {name}: no lesson assigned and none at " +
                               $"Resources/lessons/{lessonKey} — this plaque has nothing to show.", this);
                gameObject.SetActive(false);
                return;
            }

            if (titleLabel != null) titleLabel.text = lesson.displayName;

            BuildPages();
            BuildDots();
            HideEmptyTabs();
            SetActiveReader(false);
            Show(0);
        }

        /// <summary>Steps to the next page, wrapping past the last one back to the first.</summary>
        public void Next() => Show(LessonPaging.Wrap(_current + 1, _pages.Length));

        /// <summary>Steps back a page, wrapping past the first one to the last.</summary>
        public void Prev() => Show(LessonPaging.Wrap(_current - 1, _pages.Length));

        /// <summary>
        /// Called by <see cref="LessonReader"/> as the visitor enters or leaves the reading
        /// volume. The panel keeps rendering either way — this only changes the affordance,
        /// so a visitor can tell at a glance whether the keys are live.
        /// </summary>
        public void SetActiveReader(bool inRange)
        {
            ReaderInRange = inRange;

            Color keycapTint = inRange ? Gold : Tan;
            if (prevKeycap != null) prevKeycap.color = keycapTint;
            if (nextKeycap != null) nextKeycap.color = keycapTint;

            if (hintLabel != null)
            {
                hintLabel.text = inRange ? inRangeHint : outOfRangeHint;
                hintLabel.color = inRange ? Gold : Tan;
            }
        }

        /// <summary>Shows one page of the flattened page list. Out-of-range indices are ignored.</summary>
        public void Show(int page)
        {
            if (_pages.Length == 0 || bodyLabel == null) return;
            if (page < 0 || page >= _pages.Length) return;

            _current = page;
            LessonPage target = _pages[page];
            LessonSection section = lesson.sections[target.Section];

            if (headingLabel != null) headingLabel.text = section.heading;

            ShowTab(target.Section);

            bodyLabel.text = section.body;
            bodyLabel.ForceMeshUpdate();
            bodyLabel.pageToDisplay = target.PageInSection;

            if (pageLabel != null) pageLabel.text = $"{page + 1}/{_pages.Length}";

            for (int i = 0; i < _dots.Length; i++)
            {
                if (_dots[i] != null) _dots[i].color = i == page ? Gold : DotIdle;
            }
        }

        // Ask TMP how many pages each section needs at the panel's fixed size, then flatten
        // those counts into the order the visitor steps through.
        private void BuildPages()
        {
            if (bodyLabel == null || lesson.sections == null)
            {
                _pages = new LessonPage[0];
                _pageCounts = new int[0];
                return;
            }

            bodyLabel.overflowMode = TextOverflowModes.Page;
            bodyLabel.enableAutoSizing = false;

            var counts = new int[lesson.sections.Length];

            for (int i = 0; i < lesson.sections.Length; i++)
            {
                LessonSection section = lesson.sections[i];

                if (section == null || string.IsNullOrWhiteSpace(section.body))
                {
                    counts[i] = 0;
                    continue;
                }

                bodyLabel.text = section.body;
                // pageCount is only valid after the layout runs, and Start is too early for
                // TMP's own update loop to have done it.
                bodyLabel.ForceMeshUpdate();
                counts[i] = Mathf.Max(1, bodyLabel.textInfo.pageCount);
            }

            _pageCounts = counts;
            _pages = LessonPaging.Flatten(counts);
        }

        /// <summary>
        /// Marks one tab as the section being read and dims the rest. The tabs are an indicator,
        /// not a control: the museum locks the cursor, so nothing here is ever clicked — the
        /// active tab simply follows whichever page Q/E landed on.
        /// </summary>
        private void ShowTab(int section)
        {
            if (tabFrames == null) return;

            for (int i = 0; i < tabFrames.Length; i++)
            {
                bool active = i == section;

                if (tabFrames[i] != null)
                {
                    // White is the frame sprite's own opaque fill; the idle tint is ui-style.md's
                    // inner-frame wash, so an inactive tab sinks into the panel behind it.
                    tabFrames[i].color = active ? Color.white : TabIdleFrame;
                }

                // Through the LayoutElement, never the rect: a tab's width is driven by the
                // strip's layout group, and writing its sizeDelta collapses the whole strip.
                if (tabHeights != null && i < tabHeights.Length && tabHeights[i] != null)
                {
                    float height = active ? TabActiveHeight : TabIdleHeight;
                    tabHeights[i].preferredHeight = height;
                    tabHeights[i].minHeight = height;
                }

                if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                {
                    tabLabels[i].color = active ? Gold : TabIdleLabel;
                }

                if (tabAccents != null && i < tabAccents.Length && tabAccents[i] != null)
                {
                    tabAccents[i].enabled = active;
                }
            }
        }

        /// <summary>
        /// Drops the tabs of sections that produced no pages. The curriculum matrix has gaps, and
        /// a tab a visitor can never reach is worse than no tab at all.
        /// </summary>
        private void HideEmptyTabs()
        {
            if (tabFrames == null) return;

            for (int i = 0; i < tabFrames.Length; i++)
            {
                if (tabFrames[i] == null) continue;

                bool hasPages = i < _pageCounts.Length && _pageCounts[i] > 0;
                tabFrames[i].gameObject.SetActive(hasPages);
            }
        }

        // One dot per page, cloned from the template so the styling stays in the scene.
        private void BuildDots()
        {
            if (dotsRoot == null || dotTemplate == null)
            {
                _dots = new Image[0];
                return;
            }

            dotTemplate.gameObject.SetActive(false);
            _dots = new Image[_pages.Length];

            for (int i = 0; i < _pages.Length; i++)
            {
                Image dot = Instantiate(dotTemplate, dotsRoot);
                dot.name = $"Dot {i + 1}";
                dot.gameObject.SetActive(true);
                _dots[i] = dot;
            }
        }
    }
}
