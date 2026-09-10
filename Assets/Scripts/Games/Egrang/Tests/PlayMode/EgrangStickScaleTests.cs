using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// The stilt on a scaled root — the shipped FBX is imported at ×151. The solver's own tests are
    /// all unit scale, which is how a binding measured in raw local units went unnoticed: the tip
    /// was welded to the foot instead of the footplate, and the grip never reached the hand.
    /// </summary>
    public class EgrangStickScaleTests
    {
        const float Scale = 100f;
        const float Tol = 1e-3f;

        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// A stick modelled along +Y in raw units: tip 0.002 below the footplate, grip 0.006 above
        /// it — 0.2 m and 0.6 m once the ×100 root scale is applied.
        /// </summary>
        EgrangStick Build(Vector3 foot, Vector3 hand, out Transform footplate, out Transform grip, out Transform tip)
        {
            _root = new GameObject("Rig");

            var footBone = new GameObject("Foot").transform;
            footBone.SetParent(_root.transform, false);
            footBone.position = foot;

            var handBone = new GameObject("Hand").transform;
            handBone.SetParent(_root.transform, false);
            handBone.position = hand;

            var stickObject = new GameObject("Stick_L");
            stickObject.SetActive(false);
            stickObject.transform.SetParent(_root.transform, false);
            stickObject.transform.localScale = Vector3.one * Scale;

            footplate = Marker(stickObject.transform, "Stick_L_Foot", Vector3.zero);
            grip = Marker(stickObject.transform, "Stick_L_Grip", new Vector3(0f, 0.006f, 0f));
            tip = Marker(stickObject.transform, "Stick_L_Tip", new Vector3(0f, -0.002f, 0f));

            var stick = stickObject.AddComponent<EgrangStick>();
            stick.Rebind(handBone, footBone);   // before Awake, which refuses a stick with no bones
            stickObject.SetActive(true);
            return stick;
        }

        static Transform Marker(Transform parent, string name, Vector3 local)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.localPosition = local;
            return marker;
        }

        static void AssertVector(Vector3 expected, Vector3 actual, string what)
        {
            Assert.That((expected - actual).magnitude, Is.LessThan(Tol), $"{what}: expected {expected}, got {actual}");
        }

        [UnityTest]
        public IEnumerator ScaledStick_WeldsTheFootplateNotTheTipToTheFoot()
        {
            Vector3 foot = new Vector3(1f, 0.5f, 0f);
            EgrangStick stick = Build(foot, foot + new Vector3(0.1f, 0.7f, 0f), out Transform footplate, out _, out Transform tip);
            yield return null;   // one LateUpdate

            AssertVector(foot, footplate.position, "footplate");
            Assert.That(footplate.position.y - tip.position.y, Is.GreaterThan(0.19f),
                        "the tip should hang ~0.2 m below the footplate, not sit on the foot");
            Assert.That(stick.TipPosition.y, Is.LessThan(foot.y - 0.19f));
        }

        [UnityTest]
        public IEnumerator ScaledStick_GripSlidesToTheHandWithinItsTravel()
        {
            Vector3 foot = new Vector3(1f, 0.5f, 0f);
            Vector3 hand = foot + new Vector3(0.1f, 0.7f, 0.05f);   // 0.71 m: inside 0.6 ± 0.25
            EgrangStick stick = Build(foot, hand, out _, out Transform grip, out _);
            yield return null;

            Assert.IsFalse(stick.IsClamped, "a hand 0.71 m up a 0.6 ± 0.25 m travel should be reachable");
            Assert.That(stick.Slack, Is.EqualTo(0f).Within(Tol));
            AssertVector(hand, grip.position, "grip");
        }
    }
}
