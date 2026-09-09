using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The joystick's whole behaviour as arithmetic: no scene, no touch, no frame. This is the
    /// reason the touch scheme can be trusted without a phone in hand.
    /// </summary>
    public class VirtualStickModelTests
    {
        private const float Radius = 100f;
        private const float DeadZone = 10f;

        private static VirtualStickModel Stick() => new VirtualStickModel(Radius, DeadZone);

        [Test]
        public void A_drag_inside_the_dead_zone_produces_no_movement()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(5f, 0f));
            Assert.AreEqual(Vector2.zero, move);
        }

        [Test]
        public void A_drag_at_the_radius_produces_full_movement()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(Radius, 0f));
            Assert.AreEqual(1f, move.magnitude, 0.001f);
        }

        [Test]
        public void A_drag_past_the_radius_is_clamped_to_full_movement()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(Radius * 4f, 0f));
            Assert.AreEqual(1f, move.magnitude, 0.001f);
        }

        [Test]
        public void Direction_survives_the_clamp()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(0f, Radius * 3f));
            Assert.AreEqual(0f, move.x, 0.001f);
            Assert.Greater(move.y, 0.9f);
        }

        [Test]
        public void A_diagonal_drag_past_the_radius_stays_on_the_unit_circle()
        {
            Vector2 move = Stick().Evaluate(Vector2.zero, new Vector2(500f, 500f));
            Assert.AreEqual(1f, move.magnitude, 0.001f);
            Assert.AreEqual(move.x, move.y, 0.001f);
        }

        [Test]
        public void Movement_is_measured_from_where_the_finger_landed_not_from_the_screen_origin()
        {
            var origin = new Vector2(640f, 360f);
            Vector2 move = Stick().Evaluate(origin, origin + new Vector2(Radius, 0f));
            Assert.AreEqual(1f, move.magnitude, 0.001f);
        }

        [Test]
        public void The_knob_never_leaves_the_ring()
        {
            Vector2 knob = Stick().KnobOffset(Vector2.zero, new Vector2(900f, 0f));
            Assert.AreEqual(Radius, knob.magnitude, 0.001f);
        }
    }
}
