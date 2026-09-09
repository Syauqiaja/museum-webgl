using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// Drag arrives as any number of pointer events per frame and must leave as exactly one
    /// look delta per frame, then be gone. Holding a stale value would spin the camera forever.
    /// </summary>
    public class LookDragModelTests
    {
        [Test]
        public void Drag_is_scaled_by_sensitivity()
        {
            var model = new LookDragModel(0.5f);
            model.AddDrag(new Vector2(10f, 4f));

            Vector2 look = model.Consume();
            Assert.AreEqual(5f, look.x, 0.001f);
            Assert.AreEqual(2f, look.y, 0.001f);
        }

        [Test]
        public void Several_drags_in_one_frame_sum()
        {
            var model = new LookDragModel(1f);
            model.AddDrag(new Vector2(3f, 0f));
            model.AddDrag(new Vector2(4f, 1f));

            Vector2 look = model.Consume();
            Assert.AreEqual(7f, look.x, 0.001f);
            Assert.AreEqual(1f, look.y, 0.001f);
        }

        [Test]
        public void A_frame_with_no_drag_produces_zero_not_the_last_value()
        {
            var model = new LookDragModel(1f);
            model.AddDrag(new Vector2(20f, 20f));
            model.Consume();

            Assert.AreEqual(Vector2.zero, model.Consume());
        }

        [Test]
        public void Sensitivity_can_be_changed_between_frames()
        {
            var model = new LookDragModel(1f) { Sensitivity = 2f };
            model.AddDrag(new Vector2(3f, 0f));

            Assert.AreEqual(6f, model.Consume().x, 0.001f);
        }
    }
}
