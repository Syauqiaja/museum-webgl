using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests.PlayMode
{
    public class EgrangStepMoverSnapTests
    {
        [Test]
        public void SnapTo_PlacesTheMoverExactly()
        {
            var go = new GameObject("mover");
            var mover = go.AddComponent<EgrangStepMover>();

            mover.SnapTo(new Vector3(1f, 2f, 3f));

            Assert.That(go.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void StepLength_IsTheAuthoredStride()
        {
            var go = new GameObject("mover");
            var mover = go.AddComponent<EgrangStepMover>();

            Assert.That(mover.StepLength, Is.EqualTo(0.5f).Within(0.0001f));
            Object.DestroyImmediate(go);
        }
    }
}
