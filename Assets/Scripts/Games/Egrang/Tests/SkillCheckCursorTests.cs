using NUnit.Framework;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// Covers the oscillator's contract: it stays inside 0..1 whatever delta time it is handed,
    /// reverses at both edges, and a freeze holds the cursor exactly where the player caught it.
    /// </summary>
    public class SkillCheckCursorTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void Advance_MovesRightAtOneTrackPerSweep()
        {
            var cursor = new SkillCheckCursor(sweepSeconds: 2f);

            cursor.Advance(0.5f);

            Assert.That(cursor.Position, Is.EqualTo(0.25f).Within(Tol));
            Assert.That(cursor.Direction, Is.EqualTo(1));
        }

        [Test]
        public void Advance_AtTheRightEdge_ReflectsAndReverses()
        {
            var cursor = new SkillCheckCursor(sweepSeconds: 1f, startPosition: 0.8f);

            cursor.Advance(0.4f); // would reach 1.2

            Assert.That(cursor.Position, Is.EqualTo(0.8f).Within(Tol));
            Assert.That(cursor.Direction, Is.EqualTo(-1));
        }

        [Test]
        public void Advance_AtTheLeftEdge_ReflectsAndReverses()
        {
            var cursor = new SkillCheckCursor(sweepSeconds: 1f, startPosition: 0.2f, startDirection: -1);

            cursor.Advance(0.4f); // would reach -0.2

            Assert.That(cursor.Position, Is.EqualTo(0.2f).Within(Tol));
            Assert.That(cursor.Direction, Is.EqualTo(1));
        }

        [Test]
        public void Advance_LandingExactlyOnAnEdge_StaysThereWithoutFlipping()
        {
            var cursor = new SkillCheckCursor(sweepSeconds: 1f, startPosition: 0.5f);

            cursor.Advance(0.5f);

            Assert.That(cursor.Position, Is.EqualTo(1f).Within(Tol));
            Assert.That(cursor.Direction, Is.EqualTo(1), "the reversal belongs to the frame that overshoots");
        }

        [Test]
        public void Advance_WithAFrameLongerThanSeveralSweeps_StaysOnTheTrack()
        {
            // A stall or an editor pause must not throw the cursor off the bar.
            var cursor = new SkillCheckCursor(sweepSeconds: 0.1f);

            cursor.Advance(5f); // fifty sweeps in one frame

            Assert.That(cursor.Position, Is.InRange(0f, 1f));
        }

        [Test]
        public void Advance_OverManySweeps_FoldsToTheSamePlaceAsStepping()
        {
            var jumped = new SkillCheckCursor(sweepSeconds: 1f, startPosition: 0.3f);
            var stepped = new SkillCheckCursor(sweepSeconds: 1f, startPosition: 0.3f);

            jumped.Advance(3.4f);
            for (int i = 0; i < 34; i++) stepped.Advance(0.1f);

            Assert.That(jumped.Position, Is.EqualTo(stepped.Position).Within(1e-3f));
            Assert.That(jumped.Direction, Is.EqualTo(stepped.Direction));
        }

        [Test]
        public void Freeze_HoldsThePositionThePlayerCaught()
        {
            var cursor = new SkillCheckCursor(sweepSeconds: 1f);
            cursor.Advance(0.4f);
            float caught = cursor.Position;

            cursor.Freeze();
            cursor.Advance(10f);

            Assert.That(cursor.IsFrozen, Is.True);
            Assert.That(cursor.Position, Is.EqualTo(caught).Within(Tol));
        }

        [Test]
        public void Resume_ContinuesFromTheFrozenPositionAndDirection()
        {
            var cursor = new SkillCheckCursor(sweepSeconds: 1f, startPosition: 0.6f, startDirection: -1);
            cursor.Freeze();
            cursor.Advance(5f);
            cursor.Resume();

            cursor.Advance(0.1f);

            Assert.That(cursor.Position, Is.EqualTo(0.5f).Within(Tol));
            Assert.That(cursor.Direction, Is.EqualTo(-1));
        }

        [Test]
        public void Advance_WithNonPositiveSweep_ParksTheCursor()
        {
            var cursor = new SkillCheckCursor(sweepSeconds: 0f, startPosition: 0.4f);

            cursor.Advance(1f);

            Assert.That(cursor.Position, Is.EqualTo(0.4f).Within(Tol));
        }

        [Test]
        public void Reset_PlacesTheCursorAndClearsTheFreeze()
        {
            var cursor = new SkillCheckCursor(sweepSeconds: 1f);
            cursor.Freeze();

            cursor.Reset(0.75f, -1);

            Assert.That(cursor.Position, Is.EqualTo(0.75f).Within(Tol));
            Assert.That(cursor.Direction, Is.EqualTo(-1));
            Assert.That(cursor.IsFrozen, Is.False);
        }
    }
}
