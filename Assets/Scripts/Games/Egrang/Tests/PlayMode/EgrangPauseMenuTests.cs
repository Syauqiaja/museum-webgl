using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// The pause panel's only job beyond showing itself: a press made while it is up must not walk
    /// the racer behind it. The scrim already eats clicks; Space reaches the bar through the Input
    /// System, which is what these pin down (Press() is the one path every input source shares).
    /// </summary>
    public class EgrangPauseMenuTests
    {
        GameObject _bar;
        GameObject _pause;
        int _steps;

        // NUnit reuses one fixture instance for every test, so the count has to start over.
        [SetUp]
        public void SetUp() => _steps = 0;

        [TearDown]
        public void TearDown()
        {
            if (_pause != null) Object.DestroyImmediate(_pause);
            if (_bar != null) Object.DestroyImmediate(_bar);
        }

        SkillCheckBar CreateBar(bool active)
        {
            _bar = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _bar.SetActive(active);
            var bar = _bar.AddComponent<SkillCheckBar>();
            bar.OnStepResult.AddListener(_ => _steps++);
            return bar;
        }

        /// <summary>Authored inactive, as the scene's Pause Panel is.</summary>
        EgrangPauseMenu CreatePause(SkillCheckBar bar)
        {
            _pause = new GameObject("Pause Panel");
            _pause.SetActive(false);
            var menu = _pause.AddComponent<EgrangPauseMenu>();
            menu.Configure(bar);
            return menu;
        }

        [UnityTest]
        public IEnumerator OpenPanel_BlocksPresses_AndClosingReleasesThem()
        {
            SkillCheckBar bar = CreateBar(active: true);
            CreatePause(bar);
            yield return null;

            _pause.SetActive(true);
            bar.Press();

            Assert.That(_steps, Is.EqualTo(0), "a press behind the open pause menu should not step");
            Assert.That(bar.IsLocked, Is.False, "a blocked press should not start the step lockout either");

            _pause.SetActive(false);
            bar.Press();

            Assert.That(_steps, Is.EqualTo(1), "closing the menu should hand input back to the bar");
        }

        [UnityTest]
        public IEnumerator PanelOpenedBeforeTheRunStarts_StillBlocksOnceTheBarWakes()
        {
            // The bar sleeps under the inactive run root while the stilts are picked; a player can
            // open the menu then and still be in it when the countdown releases the run.
            SkillCheckBar bar = CreateBar(active: false);
            CreatePause(bar);
            _pause.SetActive(true);

            _bar.SetActive(true);
            yield return null;

            bar.Press();

            Assert.That(_steps, Is.EqualTo(0));
        }
    }
}
