using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Games.Dakon.Tests
{
    /// <summary>
    /// The pieces of the drop-feedback vignette that need no play mode: the baked sprite and the
    /// pulse's shape. The coroutine that drives the pulse is covered in the PlayMode suite.
    /// </summary>
    public class VignetteTextureTests
    {
        [Test]
        public void BakedSpriteIsClearAtTheCentre()
        {
            Sprite sprite = VignetteTexture.Bake(64);

            Color centre = sprite.texture.GetPixel(32, 32);

            Assert.That(centre.a, Is.EqualTo(0f).Within(0.01f), "the middle of the screen is tinted");
            VignetteTexture.Release(ref sprite);
        }

        [Test]
        public void BakedSpriteIsOpaqueAtTheEdge()
        {
            Sprite sprite = VignetteTexture.Bake(64);

            Color edge = sprite.texture.GetPixel(63, 32);

            Assert.That(edge.a, Is.EqualTo(1f).Within(0.01f), "the border never reaches full tint");
            VignetteTexture.Release(ref sprite);
        }

        [Test]
        public void BakedSpriteIsWhiteSoTheImageColourTintsIt()
        {
            Sprite sprite = VignetteTexture.Bake(64);

            Color edge = sprite.texture.GetPixel(63, 32);

            Assert.That(edge.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(edge.g, Is.EqualTo(1f).Within(0.01f));
            Assert.That(edge.b, Is.EqualTo(1f).Within(0.01f));
            VignetteTexture.Release(ref sprite);
        }

        /// <summary>
        /// The band belongs to the border. Half way out from the centre is where the board and
        /// the hand are, and tinting there is what made the old vignette read as a full-screen
        /// flash rather than a hint.
        /// </summary>
        [Test]
        public void BakedSpriteIsStillClearHalfWayToTheEdge()
        {
            Sprite sprite = VignetteTexture.Bake(64);

            Color middle = sprite.texture.GetPixel(48, 32);

            Assert.That(middle.a, Is.EqualTo(0f).Within(0.01f), "the board is under the tint");
            VignetteTexture.Release(ref sprite);
        }

        [Test]
        public void ReleaseClearsTheCallersReference()
        {
            Sprite sprite = VignetteTexture.Bake(64);

            VignetteTexture.Release(ref sprite);

            Assert.That(sprite, Is.Null);
        }
    }

    public class DakonVignetteAlphaTests
    {
        const float Rise = 0.1f;
        const float Fall = 0.4f;
        const float Peak = 0.35f;

        [Test]
        public void ThePulseStartsInvisible()
        {
            Assert.That(DakonVignette.AlphaAt(0f, Rise, Fall, Peak), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ThePulseIsFullyTintedWhenTheRiseEnds()
        {
            Assert.That(DakonVignette.AlphaAt(Rise, Rise, Fall, Peak), Is.EqualTo(Peak).Within(0.001f));
        }

        [Test]
        public void ThePulseIsGoneWhenTheFallEnds()
        {
            Assert.That(DakonVignette.AlphaAt(Rise + Fall, Rise, Fall, Peak), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ThePulseStaysGoneAfterwards()
        {
            Assert.That(DakonVignette.AlphaAt(Rise + Fall + 5f, Rise, Fall, Peak), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void AnInstantRiseIsAlreadyAtItsPeak()
        {
            // A zero rise is a legal authoring choice — it must read as "on", not divide by zero.
            Assert.That(DakonVignette.AlphaAt(0f, 0f, Fall, Peak), Is.EqualTo(Peak).Within(0.001f));
        }
    }

    public class DakonVignetteColourTests
    {
        GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("vignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void ACorrectDropTintsGreen()
        {
            var vignette = _go.AddComponent<DakonVignette>();

            Color c = vignette.ColorFor(correct: true);

            Assert.That(c.g, Is.GreaterThan(c.r), "the 'correct' tint is not green");
        }

        [Test]
        public void AWrongDropTintsRed()
        {
            var vignette = _go.AddComponent<DakonVignette>();

            Color c = vignette.ColorFor(correct: false);

            Assert.That(c.r, Is.GreaterThan(c.g), "the 'wrong' tint is not red");
        }
    }
}
