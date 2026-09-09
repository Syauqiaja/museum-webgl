using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// The course reading has to survive the two things the player actually does: wander sideways
    /// across the lane, and stand somewhere outside the start–finish span. Both would otherwise show
    /// up as progress the racer has not made.
    /// </summary>
    public class EgrangTrackProgressTests
    {
        static readonly Vector3 Start = new Vector3(101f, 35f, 113f);
        static readonly Vector3 Finish = new Vector3(101f, 35f, 163f);

        static EgrangTrackProgress At(Vector3 position) => EgrangTrackProgress.Evaluate(Start, Finish, position);

        [Test]
        public void AtTheStartLine_IsZero()
        {
            EgrangTrackProgress progress = At(Start);

            Assert.That(progress.Normalized, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(progress.RemainingMeters, Is.EqualTo(50f).Within(0.001f));
            Assert.That(progress.TotalMeters, Is.EqualTo(50f).Within(0.001f));
            Assert.That(progress.HasFinished, Is.False);
        }

        [Test]
        public void AtTheFinishLine_IsComplete()
        {
            EgrangTrackProgress progress = At(Finish);

            Assert.That(progress.Normalized, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(progress.RemainingMeters, Is.EqualTo(0f).Within(0.001f));
            Assert.That(progress.HasFinished, Is.True);
        }

        [Test]
        public void Halfway_IsHalf()
        {
            EgrangTrackProgress progress = At(new Vector3(101f, 35f, 138f));

            Assert.That(progress.Normalized, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(progress.TravelledMeters, Is.EqualTo(25f).Within(0.001f));
            Assert.That(progress.RemainingMeters, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void DriftingSideways_DoesNotCountAsProgress()
        {
            // The player wobbles across the lane on every step. Only movement along the course counts.
            EgrangTrackProgress straight = At(new Vector3(101f, 35f, 138f));
            EgrangTrackProgress drifted = At(new Vector3(107f, 35f, 138f));

            Assert.That(drifted.Normalized, Is.EqualTo(straight.Normalized).Within(0.0001f));
        }

        [Test]
        public void ClimbingDoesNotCountAsProgress()
        {
            // Height is off the ground plane: a rise in the terrain is not distance down the course.
            EgrangTrackProgress raised = At(new Vector3(101f, 44f, 138f));

            Assert.That(raised.Normalized, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void BehindTheStart_ClampsToZero()
        {
            EgrangTrackProgress progress = At(new Vector3(101f, 35f, 100f));

            Assert.That(progress.Normalized, Is.EqualTo(0f));
            Assert.That(progress.TravelledMeters, Is.EqualTo(0f));
            Assert.That(progress.RemainingMeters, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void PastTheFinish_ClampsToOne()
        {
            EgrangTrackProgress progress = At(new Vector3(101f, 35f, 200f));

            Assert.That(progress.Normalized, Is.EqualTo(1f));
            Assert.That(progress.RemainingMeters, Is.EqualTo(0f));
            Assert.That(progress.HasFinished, Is.True);
        }

        [Test]
        public void RemainingNeverGoesNegative()
        {
            for (float z = 90f; z < 210f; z += 3f)
            {
                Assert.That(At(new Vector3(101f, 35f, z)).RemainingMeters, Is.GreaterThanOrEqualTo(0f));
            }
        }

        [Test]
        public void ACourseWithNoLength_ReadsAsFinishedRatherThanNaN()
        {
            // A half-wired track — finish marker left on top of the start — must not divide by zero
            // and paint a NaN into the HUD.
            EgrangTrackProgress progress = EgrangTrackProgress.Evaluate(Start, Start, Start + Vector3.forward * 5f);

            Assert.That(float.IsNaN(progress.Normalized), Is.False);
            Assert.That(progress.Normalized, Is.EqualTo(1f));
            Assert.That(progress.TotalMeters, Is.EqualTo(0f));
        }

        [Test]
        public void WorksOnACourseThatIsNotAxisAligned()
        {
            Vector3 start = new Vector3(10f, 0f, 10f);
            Vector3 finish = new Vector3(40f, 0f, 50f); // 3-4-5: 50 m long.

            EgrangTrackProgress progress = EgrangTrackProgress.Evaluate(start, finish, new Vector3(25f, 0f, 30f));

            Assert.That(progress.TotalMeters, Is.EqualTo(50f).Within(0.001f));
            Assert.That(progress.Normalized, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
