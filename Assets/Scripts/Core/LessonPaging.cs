using System.Collections.Generic;

namespace Museum.Core
{
    /// <summary>
    /// One page of a lesson: which section it belongs to, and which TMP page within that
    /// section's body text it shows.
    /// </summary>
    /// <remarks>
    /// <see cref="PageInSection"/> is one-based because that is what
    /// <c>TMP_Text.pageToDisplay</c> expects.
    /// </remarks>
    public readonly struct LessonPage
    {
        public readonly int Section;
        public readonly int PageInSection;

        public LessonPage(int section, int pageInSection)
        {
            Section = section;
            PageInSection = pageInSection;
        }
    }

    /// <summary>
    /// Turns per-section page counts into the flat page order the reader steps through, and
    /// owns the wrap-around index arithmetic.
    /// </summary>
    /// <remarks>
    /// Pure C# with no UnityEngine dependency so it is testable in EditMode without a scene:
    /// the page counts themselves come from TMP at runtime, but the ordering is not TMP's
    /// business.
    /// </remarks>
    public static class LessonPaging
    {
        /// <summary>
        /// Expands [2, 1, 3] into six pages: section 0 pages 1-2, section 1 page 1,
        /// section 2 pages 1-3. Sections that produced no pages (empty body text) are
        /// skipped rather than contributing a blank page.
        /// </summary>
        public static LessonPage[] Flatten(int[] pageCountPerSection)
        {
            if (pageCountPerSection == null) return new LessonPage[0];

            var pages = new List<LessonPage>();

            for (int section = 0; section < pageCountPerSection.Length; section++)
            {
                int count = pageCountPerSection[section];

                for (int page = 1; page <= count; page++)
                {
                    pages.Add(new LessonPage(section, page));
                }
            }

            return pages.ToArray();
        }

        /// <summary>
        /// Wraps an index into [0, count). Stepping back from the first page lands on the
        /// last one — an unattended exhibit should never present a dead control.
        /// </summary>
        public static int Wrap(int index, int count)
        {
            if (count <= 0) return 0;

            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
