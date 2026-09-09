using NUnit.Framework;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// Covers the zone table's contract: every position on the track resolves to exactly one
    /// result, shared boundaries go to the band that starts there, both edges are reachable, and
    /// data the author got wrong reads as a miss rather than throwing.
    /// </summary>
    public class SkillCheckZonesTests
    {
        // The shipped default layout: red | yellow | green | yellow | red.
        static SkillCheckZones Default() => new SkillCheckZones(
            new SkillCheckZone(0.00f, 0.30f, EgrangStepResult.Fail),
            new SkillCheckZone(0.30f, 0.42f, EgrangStepResult.Half),
            new SkillCheckZone(0.42f, 0.58f, EgrangStepResult.Full),
            new SkillCheckZone(0.58f, 0.70f, EgrangStepResult.Half),
            new SkillCheckZone(0.70f, 1.00f, EgrangStepResult.Fail));

        [Test]
        public void Evaluate_InsideEachBand_ReturnsThatBandsResult()
        {
            var zones = Default();

            Assert.That(zones.Evaluate(0.15f), Is.EqualTo(EgrangStepResult.Fail));
            Assert.That(zones.Evaluate(0.35f), Is.EqualTo(EgrangStepResult.Half));
            Assert.That(zones.Evaluate(0.50f), Is.EqualTo(EgrangStepResult.Full));
            Assert.That(zones.Evaluate(0.65f), Is.EqualTo(EgrangStepResult.Half));
            Assert.That(zones.Evaluate(0.85f), Is.EqualTo(EgrangStepResult.Fail));
        }

        [Test]
        public void Evaluate_OnSharedBoundary_GoesToTheBandThatStartsThere()
        {
            var zones = Default();

            Assert.That(zones.Evaluate(0.30f), Is.EqualTo(EgrangStepResult.Half), "start of the first yellow");
            Assert.That(zones.Evaluate(0.42f), Is.EqualTo(EgrangStepResult.Full), "start of green");
            Assert.That(zones.Evaluate(0.58f), Is.EqualTo(EgrangStepResult.Half), "end of green");
            Assert.That(zones.Evaluate(0.70f), Is.EqualTo(EgrangStepResult.Fail), "start of the last red");
        }

        [Test]
        public void Evaluate_AtBothTrackEdges_IsCovered()
        {
            var zones = Default();

            Assert.That(zones.Evaluate(0f), Is.EqualTo(EgrangStepResult.Fail));
            Assert.That(zones.Evaluate(1f), Is.EqualTo(EgrangStepResult.Fail));
        }

        [Test]
        public void Evaluate_LastBandEndingAtOne_OwnsTheRightEdge()
        {
            // Nothing starts at 1, so the far edge only has a result because the band that ends
            // there claims it.
            var zones = new SkillCheckZones(new SkillCheckZone(0f, 1f, EgrangStepResult.Full));

            Assert.That(zones.Evaluate(1f), Is.EqualTo(EgrangStepResult.Full));
        }

        [Test]
        public void Evaluate_OutsideZeroToOne_IsClampedToTheEdges()
        {
            var zones = new SkillCheckZones(
                new SkillCheckZone(0.0f, 0.5f, EgrangStepResult.Full),
                new SkillCheckZone(0.5f, 1.0f, EgrangStepResult.Half));

            Assert.That(zones.Evaluate(-3f), Is.EqualTo(EgrangStepResult.Full));
            Assert.That(zones.Evaluate(4f), Is.EqualTo(EgrangStepResult.Half));
        }

        [Test]
        public void Evaluate_InAGapBetweenBands_ReadsAsFail()
        {
            var zones = new SkillCheckZones(
                new SkillCheckZone(0.0f, 0.2f, EgrangStepResult.Full),
                new SkillCheckZone(0.8f, 1.0f, EgrangStepResult.Full));

            Assert.That(zones.Evaluate(0.5f), Is.EqualTo(EgrangStepResult.Fail));
        }

        [Test]
        public void Evaluate_WithNoZones_ReadsAsFail()
        {
            Assert.That(new SkillCheckZones().Evaluate(0.5f), Is.EqualTo(EgrangStepResult.Fail));
        }

        [Test]
        public void Evaluate_NaN_ReadsAsFail()
        {
            Assert.That(Default().Evaluate(float.NaN), Is.EqualTo(EgrangStepResult.Fail));
        }

        [Test]
        public void IndexOf_FindsTheBandCoveringThePosition()
        {
            var zones = Default();

            Assert.That(zones.IndexOf(0.15f), Is.EqualTo(0));
            Assert.That(zones.IndexOf(0.50f), Is.EqualTo(2));
            Assert.That(zones.IndexOf(0.85f), Is.EqualTo(4));
        }

        [Test]
        public void IndexOf_DistinguishesBandsThatShareAResult()
        {
            // Both yellows evaluate the same, so a caller that needs to know which one it landed in
            // has to go through the index.
            var zones = Default();

            Assert.That(zones.IndexOf(0.35f), Is.EqualTo(1));
            Assert.That(zones.IndexOf(0.65f), Is.EqualTo(3));
        }

        [Test]
        public void IndexOf_WhereNoBandCovers_IsNegative()
        {
            var zones = new SkillCheckZones(
                new SkillCheckZone(0.0f, 0.2f, EgrangStepResult.Full),
                new SkillCheckZone(0.8f, 1.0f, EgrangStepResult.Full));

            Assert.That(zones.IndexOf(0.5f), Is.LessThan(0));
            Assert.That(zones.IndexOf(float.NaN), Is.LessThan(0));
        }

        [Test]
        public void Validate_WellFormedTable_Passes()
        {
            Assert.That(Default().Validate(out string error), Is.True, error);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void Validate_EmptyTable_Fails()
        {
            Assert.That(new SkillCheckZones().Validate(out string error), Is.False);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void Validate_InvertedBand_Fails()
        {
            var zones = new SkillCheckZones(new SkillCheckZone(0.6f, 0.3f, EgrangStepResult.Full));

            Assert.That(zones.Validate(out string error), Is.False);
            Assert.That(error, Does.Contain("inverted"));
        }

        [Test]
        public void Validate_OverlappingBands_Fails()
        {
            var zones = new SkillCheckZones(
                new SkillCheckZone(0.0f, 0.6f, EgrangStepResult.Fail),
                new SkillCheckZone(0.4f, 1.0f, EgrangStepResult.Full));

            Assert.That(zones.Validate(out string error), Is.False);
            Assert.That(error, Does.Contain("overlap"));
        }

        [Test]
        public void Validate_BandsOutOfOrder_Fails()
        {
            var zones = new SkillCheckZones(
                new SkillCheckZone(0.6f, 1.0f, EgrangStepResult.Full),
                new SkillCheckZone(0.0f, 0.4f, EgrangStepResult.Fail));

            Assert.That(zones.Validate(out string error), Is.False);
        }

        [Test]
        public void Validate_GapBetweenBands_IsAllowed()
        {
            var zones = new SkillCheckZones(
                new SkillCheckZone(0.0f, 0.2f, EgrangStepResult.Full),
                new SkillCheckZone(0.8f, 1.0f, EgrangStepResult.Full));

            Assert.That(zones.Validate(out string error), Is.True, error);
        }

        [Test]
        public void StepsFor_MapsResultsToDistance()
        {
            Assert.That(EgrangStep.StepsFor(EgrangStepResult.Full), Is.EqualTo(2));
            Assert.That(EgrangStep.StepsFor(EgrangStepResult.Half), Is.EqualTo(1));
            Assert.That(EgrangStep.StepsFor(EgrangStepResult.Fail), Is.EqualTo(0));
        }
    }
}
