using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// PlayMode cover for the one thing the selection flow depends on: the bar actually scores
    /// against the chosen stick's table. Runs in PlayMode because the bar needs its <c>Awake</c> and
    /// a real <c>Image</c> to bake into.
    ///
    /// The bar's own object is created inactive in the awkward case, mirroring the scene layout —
    /// the bar lives under an inactive run root, so it is configured before it has ever woken up.
    /// </summary>
    public class SkillCheckBarConfigureTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        SkillCheckBar CreateBar(bool active)
        {
            _root = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _root.SetActive(active);
            return _root.AddComponent<SkillCheckBar>();
        }

        static EgrangStickProfile Profile(EgrangStickShape shape)
        {
            var profile = ScriptableObject.CreateInstance<EgrangStickProfile>();
            profile.name = shape.ToString();
            profile.ApplyPreset(shape);
            return profile;
        }

        [UnityTest]
        public IEnumerator Configure_MakesTheBarScoreAgainstTheChosenSticksZones()
        {
            SkillCheckBar bar = CreateBar(active: true);
            yield return null;

            EgrangStickProfile segitiga = Profile(EgrangStickShape.Segitiga);
            bar.Configure(segitiga);

            // Derived from the preset rather than written out, so retuning the sticks cannot leave this
            // test asserting a band layout that no longer ships.
            EgrangStickPreset preset = EgrangStickPresets.Segitiga;
            float outsideTheTriangle = 0.5f - preset.GreenHalfWidth - preset.YellowWidth - 0.01f;

            Assert.That(bar.Zones.Evaluate(0.5f), Is.EqualTo(EgrangStepResult.Full));
            Assert.That(bar.Zones.Evaluate(outsideTheTriangle), Is.EqualTo(EgrangStepResult.Fail));
            Assert.That(bar.Profile, Is.SameAs(segitiga));

            // The same press on the square pole is a clean step — which is the whole point of
            // configuring the bar per stick, and what makes the choice worth making.
            Assert.That(EgrangStickPresets.Persegi.BuildZones().Evaluate(outsideTheTriangle),
                        Is.EqualTo(EgrangStepResult.Full),
                        "the sticks should differ enough that a triangle miss is a square hit");

            Object.DestroyImmediate(segitiga);
        }

        [UnityTest]
        public IEnumerator Configure_BeforeAwake_IsAppliedWhenTheBarIsEnabled()
        {
            SkillCheckBar bar = CreateBar(active: false);
            EgrangStickProfile segitiga = Profile(EgrangStickShape.Segitiga);

            bar.Configure(segitiga);
            _root.SetActive(true);
            yield return null;

            Assert.That(bar.Profile, Is.SameAs(segitiga), "the pending profile should be adopted in Awake");
            Assert.That(bar.SweepSeconds, Is.EqualTo(segitiga.SweepSeconds).Within(0.0001f));

            Object.DestroyImmediate(segitiga);
        }

        [UnityTest]
        public IEnumerator Configure_SendsTheCursorBackToTheEdgeSoEveryRunStartsTheSame()
        {
            SkillCheckBar bar = CreateBar(active: true);
            yield return null;
            yield return null; // let the cursor travel away from the edge

            EgrangStickProfile persegi = Profile(EgrangStickShape.Persegi);
            bar.Configure(persegi);

            Assert.That(bar.CursorPosition, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(bar.IsLocked, Is.False);

            Object.DestroyImmediate(persegi);
        }

        [UnityTest]
        public IEnumerator Configure_WithNull_KeepsTheCurrentZones()
        {
            SkillCheckBar bar = CreateBar(active: true);
            yield return null;

            EgrangStickProfile persegi = Profile(EgrangStickShape.Persegi);
            bar.Configure(persegi);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("null profile"));
            bar.Configure(null);

            Assert.That(bar.Profile, Is.SameAs(persegi));

            Object.DestroyImmediate(persegi);
        }
    }
}
