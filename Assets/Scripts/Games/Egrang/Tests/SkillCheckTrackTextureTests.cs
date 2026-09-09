using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// The bake's one job: every pixel carries the colour of the result a press at that position
    /// would score. If a pixel and <see cref="SkillCheckZones.Evaluate"/> disagree, the bar is
    /// showing the player a boundary that is not where the scoring changes.
    /// </summary>
    public class SkillCheckTrackTextureTests
    {
        static readonly SkillCheckTrackColors Colors = new SkillCheckTrackColors(Color.red, Color.yellow, Color.green);

        static SkillCheckZones Zones() => EgrangStickPresets.Lingkaran.BuildZones();

        /// <summary>
        /// Compares the way Unity does. The strip is an RGBA32 texture, so a channel comes back
        /// quantised to eight bits — <c>Color.yellow</c>'s 0.92156860 reads back as 0.92156870 — and
        /// NUnit's default equality is exact. Colour's own <c>==</c> carries the tolerance that makes
        /// "is this pixel yellow" the question actually being asked.
        /// </summary>
        static void AssertColor(Color actual, Color expected, string message)
        {
            Assert.That(actual == expected, Is.True, $"{message}: expected {expected}, was {actual}");
        }

        [Test]
        public void Bake_ColorsEveryPixelWithTheResultThatPositionScores()
        {
            SkillCheckZones zones = Zones();
            Sprite sprite = SkillCheckTrackTexture.Bake(zones, Colors, 512);

            try
            {
                Color[] pixels = sprite.texture.GetPixels();
                Assert.That(pixels.Length, Is.EqualTo(512));

                for (int x = 0; x < pixels.Length; x++)
                {
                    float t = (x + 0.5f) / pixels.Length;
                    AssertColor(pixels[x], Colors.For(zones.Evaluate(t)), $"pixel {x} (t = {t:0.0000})");
                }
            }
            finally
            {
                SkillCheckTrackTexture.Release(ref sprite);
            }
        }

        [Test]
        public void Bake_PutsTheSeamsWhereTheScoringChanges()
        {
            SkillCheckZones zones = Zones();
            Sprite sprite = SkillCheckTrackTexture.Bake(zones, Colors, 1024);

            try
            {
                Color[] pixels = sprite.texture.GetPixels();

                // Straddle green's edges: the pixel just inside must be green and the one just
                // outside must not be, within the one-pixel precision the strip has.
                foreach (SkillCheckZone zone in zones.Zones)
                {
                    if (zone.Result != EgrangStepResult.Full) continue;

                    int inside = Mathf.Clamp(Mathf.FloorToInt(zone.Start * pixels.Length) + 1, 0, pixels.Length - 1);
                    int outside = Mathf.Clamp(Mathf.FloorToInt(zone.Start * pixels.Length) - 1, 0, pixels.Length - 1);

                    AssertColor(pixels[inside], Colors.Full, "just inside green");
                    Assert.That(pixels[outside] == Colors.Full, Is.False, "just outside green");
                }
            }
            finally
            {
                SkillCheckTrackTexture.Release(ref sprite);
            }
        }

        [Test]
        public void Bake_UsesPointFilteringSoStretchingKeepsTheEdgesHard()
        {
            Sprite sprite = SkillCheckTrackTexture.Bake(Zones(), Colors, 128);

            try
            {
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(sprite.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            }
            finally
            {
                SkillCheckTrackTexture.Release(ref sprite);
            }
        }

        [Test]
        public void Bake_ClampsTheResolutionRatherThanFailing()
        {
            Sprite tiny = SkillCheckTrackTexture.Bake(Zones(), Colors, 1);
            Sprite huge = SkillCheckTrackTexture.Bake(Zones(), Colors, 100000);

            try
            {
                Assert.That(tiny.texture.width, Is.EqualTo(SkillCheckTrackTexture.MinResolution));
                Assert.That(huge.texture.width, Is.EqualTo(SkillCheckTrackTexture.MaxResolution));
            }
            finally
            {
                SkillCheckTrackTexture.Release(ref tiny);
                SkillCheckTrackTexture.Release(ref huge);
            }
        }

        [Test]
        public void Release_ClearsTheReferenceAndIsSafeTwice()
        {
            Sprite sprite = SkillCheckTrackTexture.Bake(Zones(), Colors, 64);

            SkillCheckTrackTexture.Release(ref sprite);
            Assert.That(sprite, Is.Null);

            Assert.DoesNotThrow(() => SkillCheckTrackTexture.Release(ref sprite));
        }
    }
}
