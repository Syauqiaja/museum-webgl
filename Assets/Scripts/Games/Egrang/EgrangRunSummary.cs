using System.Collections.Generic;

namespace Museum.Games.Egrang
{
    /// <summary>One line in the standings: who raced, where they came, and which lane they ran.</summary>
    public readonly struct EgrangStanding
    {
        /// <summary>Lane number as the player sees it, 1-3. The fallback label, not the label.</summary>
        public readonly int Lane;

        /// <summary>Finishing place, 1-based. 0 for a racer who never crossed the line.</summary>
        public readonly int Place;

        /// <summary>The racer's nickname, or empty when it never arrived.</summary>
        public readonly string Name;

        /// <summary>True for the row belonging to this client, so it can be marked.</summary>
        public readonly bool IsLocal;

        /// <summary>Strides this racer banked, as the server counted them.</summary>
        public readonly int Units;

        /// <summary>Strides the lane is long, so a distance reads as "34/50". 0 when unknown.</summary>
        public readonly int FinishUnits;

        /// <summary>The stilt this racer walked on, e.g. "Egrang Persegi". Empty when unknown.</summary>
        public readonly string StickName;

        /// <summary>That stilt's defining measurement, e.g. "Sisi 8 cm". Empty when unknown.</summary>
        public readonly string StickSize;

        public EgrangStanding(int lane, int place, string name = null, bool isLocal = false,
                              int units = 0, int finishUnits = 0,
                              string stickName = null, string stickSize = null)
        {
            Lane = lane;
            Place = place;
            Name = name ?? string.Empty;
            IsLocal = isLocal;
            Units = units;
            FinishUnits = finishUnits;
            StickName = stickName ?? string.Empty;
            StickSize = stickSize ?? string.Empty;
        }

        /// <summary>True once this row knows how far along the lane the racer got.</summary>
        public bool HasDistance => FinishUnits > 0;

        /// <summary>
        /// What to call this racer on screen: their name, or "Pemain 2" when there isn't one. A
        /// nameless racer is a real case — an offline run, or a lobby entry that never carried a
        /// nickname — and a blank row would read as a rendering fault.
        /// </summary>
        public string Label => string.IsNullOrEmpty(Name) ? $"Pemain {Lane}" : Name;
    }

    /// <summary>
    /// Everything the results panel says about a finished run: where the player came, how long it
    /// took, how the presses were graded, and which stilt was walked on.
    ///
    /// Plain C# so the shape of a result can be built and asserted without a scene — the panel is a
    /// view over this and works nothing out for itself.
    /// </summary>
    public readonly struct EgrangRunSummary
    {
        /// <summary>Shown in place of a time when the racer never finished.</summary>
        public const string NoTime = "--:--.--";

        static readonly IReadOnlyList<EgrangStanding> NoStandings = new EgrangStanding[0];

        /// <summary>Finishing place, 1-based. 0 when this player never crossed the line.</summary>
        public readonly int Place;

        /// <summary>How many racers were in the race, so "Juara 2 dari 3" can be written.</summary>
        public readonly int Racers;

        /// <summary>Seconds from the start of the run to crossing the line. Negative when unfinished.</summary>
        public readonly float Seconds;

        public readonly int FullSteps;
        public readonly int HalfSteps;
        public readonly int FailSteps;

        /// <summary>Stick display name, e.g. "Egrang Persegi".</summary>
        public readonly string StickName;

        /// <summary>Stick defining measurement, e.g. "Sisi 8 cm".</summary>
        public readonly string StickSize;

        /// <summary>Every lane's place, best first. Empty offline, where there is only one racer.</summary>
        public readonly IReadOnlyList<EgrangStanding> Standings;

        public EgrangRunSummary(int place, int racers, float seconds,
                                int fullSteps, int halfSteps, int failSteps,
                                string stickName, string stickSize,
                                IReadOnlyList<EgrangStanding> standings = null)
        {
            Place = place;
            Racers = racers;
            Seconds = seconds;
            FullSteps = fullSteps;
            HalfSteps = halfSteps;
            FailSteps = failSteps;
            StickName = stickName;
            StickSize = stickSize;
            Standings = standings ?? NoStandings;
        }

        /// <summary>Total presses that were graded, however they scored.</summary>
        public int TotalSteps => FullSteps + HalfSteps + FailSteps;

        /// <summary>True once this player has a place — the panel writes "Juara n" only then.</summary>
        public bool HasPlace => Place > 0;

        /// <summary>The run time as <c>mm:ss.ff</c>, or <see cref="NoTime"/> when there is no time to show.</summary>
        public string TimeText => FormatTime(Seconds);

        /// <summary>
        /// Formats a run time as <c>mm:ss.ff</c>. A negative time is a run that never finished and
        /// reads as <see cref="NoTime"/> rather than as a suspiciously fast 00:00.00.
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) return NoTime;

            int hundredths = (int)(seconds * 100f);
            int minutes = hundredths / 6000;
            int wholeSeconds = hundredths / 100 % 60;

            return $"{minutes:00}:{wholeSeconds:00}.{hundredths % 100:00}";
        }

        /// <summary>
        /// Orders lanes for display: finishers by place, then anyone who never finished, by lane.
        /// Sorting here rather than in the panel keeps the one rule the tests care about out of a
        /// MonoBehaviour.
        /// </summary>
        public static IReadOnlyList<EgrangStanding> Sort(IEnumerable<EgrangStanding> standings)
        {
            if (standings == null) return NoStandings;

            var ordered = new List<EgrangStanding>(standings);
            ordered.Sort((a, b) =>
            {
                bool aFinished = a.Place > 0;
                bool bFinished = b.Place > 0;

                if (aFinished != bFinished) return aFinished ? -1 : 1;
                if (aFinished && a.Place != b.Place) return a.Place.CompareTo(b.Place);

                return a.Lane.CompareTo(b.Lane);
            });

            return ordered;
        }
    }
}
