using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// Covers the two-pivot contract: the footplate never leaves the foot bone, the shaft always
    /// aims at the hand, the grip slides instead of the stick stretching, and degenerate input
    /// produces a finite pose rather than NaN.
    /// </summary>
    public class EgrangStickSolverTests
    {
        const float Tol = 1e-4f;

        // A stick modelled along +Y: tip 0.2 below the footplate, grip 1.0 above it.
        static EgrangStickBinding Binding(Vector3 footstepLocal = default)
        {
            return EgrangStickBinding.Create(
                footstepLocal,
                footstepLocal + Vector3.up * 1.0f,
                footstepLocal + Vector3.down * 0.2f);
        }

        static EgrangStickSolveInput Input(Vector3 foot, Vector3 hand, Quaternion footRot,
                                           float min = 0.75f, float max = 1.25f)
            => new EgrangStickSolveInput(foot, footRot, hand, min, max);

        static void AssertVector(Vector3 expected, Vector3 actual, string what)
        {
            Assert.That((expected - actual).magnitude, Is.LessThan(Tol),
                $"{what}: expected {expected}, got {actual}");
        }

        [Test]
        public void RestPose_HandAtBindHeight_IsIdentityAndSlackFree()
        {
            var binding = Binding();
            Vector3 foot = new Vector3(3f, 0f, -2f);
            var pose = EgrangStickSolver.Solve(binding, Input(foot, foot + Vector3.up, Quaternion.identity));

            AssertVector(foot, pose.Position, "root position");
            AssertVector(Vector3.up, pose.Rotation * binding.ShaftAxisLocal, "shaft direction");
            Assert.That(pose.HandleDistance, Is.EqualTo(binding.BindHandleDistance).Within(Tol));
            Assert.That(pose.Slack, Is.EqualTo(0f).Within(Tol));
            Assert.IsFalse(pose.IsClamped);
        }

        // Regression: a LookRotation-based roll reference built in local space does not correspond
        // to one built in world space, and lands the stick 180 degrees out.
        [Test]
        public void BindPose_LeavesModelOrientationUntouched()
        {
            var binding = Binding();
            Vector3 foot = new Vector3(1f, 2f, 3f);
            var pose = EgrangStickSolver.Solve(binding, Input(foot, foot + Vector3.up, Quaternion.identity));

            Assert.That(Quaternion.Angle(Quaternion.identity, pose.Rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void RollOffset_TwistsAboutTheShaftOnly()
        {
            var binding = Binding();
            Vector3 foot = Vector3.zero;
            Vector3 hand = Vector3.up;
            var input = new EgrangStickSolveInput(foot, Quaternion.identity, hand, 0.75f, 1.25f, 180f);

            var pose = EgrangStickSolver.Solve(binding, input);

            AssertVector(Vector3.up, pose.Rotation * binding.ShaftAxisLocal, "shaft direction");
            AssertVector(Vector3.left, pose.Rotation * Vector3.right, "flipped facing");
        }

        [Test]
        public void HandBeyondMax_ClampsHandleAndReportsSlack()
        {
            var binding = Binding();
            Vector3 foot = Vector3.zero;
            float reach = 1.6f;
            var pose = EgrangStickSolver.Solve(binding, Input(foot, Vector3.up * reach, Quaternion.identity));

            Assert.IsTrue(pose.IsClamped);
            Assert.That(pose.HandleDistance, Is.EqualTo(1.25f).Within(Tol));
            Assert.That(pose.Slack, Is.EqualTo(reach - 1.25f).Within(Tol));
        }

        [Test]
        public void HandBelowMin_ClampsHandleButReportsNoSlack()
        {
            var binding = Binding();
            var pose = EgrangStickSolver.Solve(binding, Input(Vector3.zero, Vector3.up * 0.4f, Quaternion.identity));

            Assert.IsTrue(pose.IsClamped);
            Assert.That(pose.HandleDistance, Is.EqualTo(0.75f).Within(Tol));
            Assert.That(pose.Slack, Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void LateralHand_AimsShaftAtHand()
        {
            var binding = Binding();
            Vector3 foot = new Vector3(1f, 0.5f, 0f);
            Vector3 hand = foot + new Vector3(0.3f, 0.9f, -0.2f);
            var pose = EgrangStickSolver.Solve(binding, Input(foot, hand, Quaternion.identity));

            AssertVector((hand - foot).normalized, pose.Rotation * binding.ShaftAxisLocal, "shaft direction");
        }

        [Test]
        public void FootRoll_DoesNotTwistStick_RollIsLocked()
        {
            var binding = Binding();
            Vector3 foot = Vector3.zero;
            Vector3 hand = Vector3.up;

            var straight = EgrangStickSolver.Solve(binding, Input(foot, hand, Quaternion.identity));
            var rolled = EgrangStickSolver.Solve(binding, Input(foot, hand, Quaternion.Euler(0f, 40f, 0f)));

            AssertVector(Vector3.up, rolled.Rotation * binding.ShaftAxisLocal, "shaft direction");
            AssertVector(straight.Rotation * Vector3.right, rolled.Rotation * Vector3.right,
                "stick must not twist when only the foot rolls");
        }

        [Test]
        public void AimIsTheOnlyThingThatRotatesTheStick()
        {
            var binding = Binding();
            Vector3 foot = Vector3.zero;
            Vector3 hand = foot + new Vector3(0.2f, 1f, 0.1f);

            var a = EgrangStickSolver.Solve(binding, Input(foot, hand, Quaternion.identity));
            var b = EgrangStickSolver.Solve(binding, Input(foot, hand, Quaternion.Euler(20f, -70f, 35f)));

            Assert.That(Quaternion.Angle(a.Rotation, b.Rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void HandOnFootstep_FallsBackToFootUpAxis_NoNaN()
        {
            var binding = Binding();
            Quaternion footRot = Quaternion.Euler(15f, 30f, 0f);
            var pose = EgrangStickSolver.Solve(binding, Input(Vector3.zero, Vector3.zero, footRot));

            AssertVector(footRot * Vector3.up, pose.Rotation * binding.ShaftAxisLocal, "fallback shaft direction");
            Assert.IsFalse(float.IsNaN(pose.Position.x + pose.Position.y + pose.Position.z));
            Assert.IsFalse(float.IsNaN(pose.Rotation.x + pose.Rotation.y + pose.Rotation.z + pose.Rotation.w));
        }

        [Test]
        public void OffsetFootplate_StillLandsOnFootBone()
        {
            Vector3 footstepLocal = new Vector3(0.05f, 0.4f, -0.02f);
            var binding = Binding(footstepLocal);
            Vector3 foot = new Vector3(-2f, 1.1f, 4f);
            Vector3 hand = foot + new Vector3(0.1f, 1.05f, 0.25f);

            var pose = EgrangStickSolver.Solve(binding, Input(foot, hand, Quaternion.Euler(0f, 25f, 5f)));

            Vector3 solvedFootplate = pose.Position + pose.Rotation * binding.FootstepLocalOffset;
            AssertVector(foot, solvedFootplate, "footplate world position");
        }
    }
}
