using System.Collections.Generic;
using NUnit.Framework;

namespace Museum.Games.Egrang.Tests
{
    public class EgrangRunSummaryTests
    {
        [Test]
        public void ASubMinuteTimeReadsAsMinutesSecondsAndHundredths()
        {
            Assert.That(EgrangRunSummary.FormatTime(23.456f), Is.EqualTo("00:23.45"));
        }

        [Test]
        public void AnOverMinuteTimeCarriesIntoTheMinutesField()
        {
            Assert.That(EgrangRunSummary.FormatTime(83.456f), Is.EqualTo("01:23.45"));
        }

        [Test]
        public void ARunThatNeverFinishedHasNoTimeRatherThanZero()
        {
            // 00:00.00 would read as an impossibly fast run rather than as an unfinished one.
            Assert.That(EgrangRunSummary.FormatTime(-1f), Is.EqualTo(EgrangRunSummary.NoTime));
        }

        [Test]
        public void StandingsAreOrderedByPlace()
        {
            IReadOnlyList<EgrangStanding> sorted = EgrangRunSummary.Sort(new[]
            {
                new EgrangStanding(3, 2),
                new EgrangStanding(1, 3),
                new EgrangStanding(2, 1),
            });

            Assert.That(sorted[0].Lane, Is.EqualTo(2));
            Assert.That(sorted[1].Lane, Is.EqualTo(3));
            Assert.That(sorted[2].Lane, Is.EqualTo(1));
        }

        [Test]
        public void RacersWhoNeverFinishedSortAfterEveryFinisherByLane()
        {
            IReadOnlyList<EgrangStanding> sorted = EgrangRunSummary.Sort(new[]
            {
                new EgrangStanding(3, 0),
                new EgrangStanding(1, 0),
                new EgrangStanding(2, 1),
            });

            Assert.That(sorted[0].Lane, Is.EqualTo(2));
            Assert.That(sorted[1].Lane, Is.EqualTo(1));
            Assert.That(sorted[2].Lane, Is.EqualTo(3));
        }

        [Test]
        public void AStandingIsLabelledByNameAndFallsBackToItsLane()
        {
            Assert.That(new EgrangStanding(2, 1, "Budi").Label, Is.EqualTo("Budi"));
            Assert.That(new EgrangStanding(2, 1).Label, Is.EqualTo("Pemain 2"));
            Assert.That(new EgrangStanding(3, 0, "").Label, Is.EqualTo("Pemain 3"));
        }

        [Test]
        public void SortingKeepsEachRacerWithTheirOwnName()
        {
            IReadOnlyList<EgrangStanding> sorted = EgrangRunSummary.Sort(new[]
            {
                new EgrangStanding(1, 2, "Budi", true),
                new EgrangStanding(2, 1, "Sari"),
            });

            Assert.That(sorted[0].Name, Is.EqualTo("Sari"));
            Assert.That(sorted[0].IsLocal, Is.False);
            Assert.That(sorted[1].Name, Is.EqualTo("Budi"));
            Assert.That(sorted[1].IsLocal, Is.True);
        }

        [Test]
        public void ASummaryWithNoStandingsIsEmptyRatherThanNull()
        {
            var summary = new EgrangRunSummary(1, 1, 12.5f, 20, 3, 2, "Egrang Persegi", "Sisi 8 cm");

            Assert.That(summary.Standings, Is.Empty);
            Assert.That(summary.TotalSteps, Is.EqualTo(25));
            Assert.That(summary.HasPlace, Is.True);
            Assert.That(summary.TimeText, Is.EqualTo("00:12.50"));
        }

        [Test]
        public void APlaceOfZeroIsNotAPlace()
        {
            var summary = new EgrangRunSummary(0, 3, -1f, 4, 0, 9, "Egrang Segitiga", "Sisi 8 cm");

            Assert.That(summary.HasPlace, Is.False);
            Assert.That(summary.TimeText, Is.EqualTo(EgrangRunSummary.NoTime));
        }
    }
}
