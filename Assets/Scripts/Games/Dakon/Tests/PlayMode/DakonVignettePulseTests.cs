using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Museum.Games.Dakon.Tests.PlayMode
{
    /// <summary>
    /// The pulse as it actually runs: a coroutine driving the overlay's alpha. The shape of the
    /// curve is covered in EditMode; what matters here is that a flash reaches the screen, clears
    /// itself, and that a second drop restarts the same pulse rather than stacking a new one.
    /// </summary>
    public class DakonVignettePulseTests
    {
        GameObject _go;
        DakonVignette _vignette;
        Image _image;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("vignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _image = _go.GetComponent<Image>();
            _vignette = _go.AddComponent<DakonVignette>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
        }

        [UnityTest]
        public IEnumerator TheOverlayIsInvisibleUntilSomethingIsDropped()
        {
            yield return null;

            Assert.That(_image.color.a, Is.EqualTo(0f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator AFlashTintsTheScreen()
        {
            _vignette.Flash(correct: true);

            yield return null;
            yield return null;

            Assert.That(_image.color.a, Is.GreaterThan(0f), "the vignette never showed");
            Assert.That(_image.color.g, Is.GreaterThan(_image.color.r), "a correct drop did not read as green");
        }

        [UnityTest]
        public IEnumerator AFlashClearsItself()
        {
            _vignette.Flash(correct: false);

            yield return new WaitForSeconds(_vignette.PulseSeconds + 0.1f);

            Assert.That(_image.color.a, Is.EqualTo(0f).Within(0.001f), "the tint stayed on screen");
        }

        [UnityTest]
        public IEnumerator ASecondDropRestartsTheSamePulse()
        {
            _vignette.Flash(correct: true);
            yield return null;

            _vignette.Flash(correct: false);
            yield return null;
            yield return null;

            Assert.That(_image.color.r, Is.GreaterThan(_image.color.g), "the second drop kept the first drop's colour");
        }

        [UnityTest]
        public IEnumerator FlashingWhileDisabledDoesNotThrow()
        {
            // The view fires this from a drop that can land while the board object is being torn
            // down. There is no coroutine to run then; the flash must simply be a no-op.
            _go.SetActive(false);

            Assert.DoesNotThrow(() => _vignette.Flash(correct: true));

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheOverlayNeverEatsAClick()
        {
            yield return null;

            Assert.That(_image.raycastTarget, Is.False, "the vignette is blocking the cards underneath it");
        }
    }
}
