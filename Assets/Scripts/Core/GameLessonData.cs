using System;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>One headed block of exhibit copy — a single column of the curriculum matrix.</summary>
    [Serializable]
    public class LessonSection
    {
        [Tooltip("Shown above the body, in caps. e.g. \"ASAL-USUL\".")]
        public string heading;

        [Tooltip("Indonesian prose. Paginated by TMP at runtime, so length is not capped here.")]
        [TextArea(4, 20)]
        public string body;
    }

    /// <summary>
    /// The educational content for one traditional game, as shown on its museum plaque.
    /// Authored from the curriculum spreadsheet — see Assets/Docs/lessons.md for the
    /// pipeline and the provenance of the text.
    /// </summary>
    /// <remarks>
    /// Sections are a list rather than fixed fields: the source matrix has gaps, and a game
    /// with three sections must not render a blank fourth page.
    /// </remarks>
    [CreateAssetMenu(menuName = "Museum/Game Lesson", fileName = "Lesson_Game")]
    public class GameLessonData : ScriptableObject
    {
        [Tooltip("Lower-case identifier, e.g. \"dakon\". Doubles as the Resources file name.")]
        public string gameKey;

        [Tooltip("Shown as the panel's title. e.g. \"Dakon\".")]
        public string displayName;

        public LessonSection[] sections;
    }
}
