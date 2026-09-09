using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The moving indicator of the skill-check bar, as pure state: a value in 0..1 that sweeps
    /// edge to edge and reverses when it arrives, plus a freeze the bar holds during a step.
    ///
    /// Nothing here knows about transforms or time — the caller supplies delta time — which is what
    /// keeps it unit-testable, the same split <see cref="EgrangStickSolver"/> uses.
    /// </summary>
    public sealed class SkillCheckCursor
    {
        float _sweepSeconds;

        /// <summary>Cursor position along the track. 0 is the far left, 1 the far right.</summary>
        public float Position { get; private set; }

        /// <summary>Travel direction: +1 moving right, -1 moving left.</summary>
        public int Direction { get; private set; } = 1;

        /// <summary>While true, <see cref="Advance"/> does nothing.</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>
        /// Seconds for one edge-to-edge pass. Values at or below zero park the cursor, since a
        /// zero-length sweep has no meaningful speed.
        /// </summary>
        public float SweepSeconds
        {
            get => _sweepSeconds;
            set => _sweepSeconds = value;
        }

        public SkillCheckCursor(float sweepSeconds, float startPosition = 0f, int startDirection = 1)
        {
            _sweepSeconds = sweepSeconds;
            Reset(startPosition, startDirection);
        }

        /// <summary>Puts the cursor at a known position and direction, and unfreezes it.</summary>
        public void Reset(float position, int direction = 1)
        {
            Position = Mathf.Clamp01(position);
            Direction = direction >= 0 ? 1 : -1;
            IsFrozen = false;
        }

        /// <summary>Stops the cursor where it stands. It keeps its position and direction.</summary>
        public void Freeze() => IsFrozen = true;

        /// <summary>Resumes from the frozen position, still travelling the way it was.</summary>
        public void Resume() => IsFrozen = false;

        /// <summary>
        /// Moves the cursor by <paramref name="deltaTime"/> seconds, bouncing off both edges.
        ///
        /// The bounce folds repeatedly rather than clamping once, so a long frame that would carry
        /// the cursor across the track several times still lands inside 0..1 travelling the correct
        /// way — a stall or an editor pause cannot throw the cursor off the bar.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (IsFrozen || _sweepSeconds <= 0f || deltaTime <= 0f || float.IsNaN(deltaTime)) return;

            float position = Position + Direction * (deltaTime / _sweepSeconds);
            int direction = Direction;

            while (position < 0f || position > 1f)
            {
                if (position < 0f)
                {
                    position = -position;
                    direction = 1;
                }
                else
                {
                    position = 2f - position;
                    direction = -1;
                }
            }

            Position = position;
            Direction = direction;
        }
    }
}
