using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Dakon.Tests
{
    public class DakonPileTests
    {
        [Test]
        public void First_seed_sits_at_center()
        {
            Assert.AreEqual(Vector3.zero, DakonPile.OffsetFor(0, 1f));
        }

        [Test]
        public void Same_index_and_radius_is_deterministic()
        {
            Assert.AreEqual(DakonPile.OffsetFor(7, 2f), DakonPile.OffsetFor(7, 2f));
        }

        [Test]
        public void Horizontal_offset_stays_within_radius()
        {
            const float radius = 1.5f;
            for (int n = 0; n < 60; n++)
            {
                var o = DakonPile.OffsetFor(n, radius);
                float horiz = Mathf.Sqrt(o.x * o.x + o.z * o.z);
                Assert.LessOrEqual(horiz, radius + 1e-4f, $"seed {n} horiz {horiz} exceeds radius");
            }
        }

        [Test]
        public void Height_is_non_decreasing_in_n()
        {
            const float radius = 1f;
            float prev = DakonPile.OffsetFor(0, radius).y;
            for (int n = 1; n < 60; n++)
            {
                float y = DakonPile.OffsetFor(n, radius).y;
                Assert.GreaterOrEqual(y, prev - 1e-4f, $"seed {n} y {y} dropped below {prev}");
                prev = y;
            }
        }
    }
}
