#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>JALAN — a button wired to the bar's Press — makes no UI click; other buttons still do.</summary>
    public class SkillCheckBarSilenceTests
    {
        private GameObject _bar;
        private GameObject _step;
        private GameObject _other;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { _bar, _step, _other })
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void A_button_that_presses_the_bar_is_marked_silent_and_others_are_not()
        {
            _bar = new GameObject("Bar");
            _bar.SetActive(false);
            var bar = _bar.AddComponent<SkillCheckBar>();

            _step = new GameObject("Step Button", typeof(RectTransform), typeof(Image), typeof(Button));
            UnityEventTools.AddVoidPersistentListener(_step.GetComponent<Button>().onClick, bar.Press);
            _other = new GameObject("Pause Button", typeof(RectTransform), typeof(Image), typeof(Button));

            _bar.SetActive(true);   // Awake, as enabling the run root does

            Assert.IsNotNull(_step.GetComponent<Museum.Core.SilentButton>(), "JALAN");
            Assert.IsNull(_other.GetComponent<Museum.Core.SilentButton>(), "any other button");
        }
    }
}
#endif
