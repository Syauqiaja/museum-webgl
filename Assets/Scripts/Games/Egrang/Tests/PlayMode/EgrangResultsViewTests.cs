using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests.PlayMode
{
    /// <summary>
    /// The panel as the scene actually holds it: authored **inactive**, so `Awake` has never run
    /// by the time the race ends and calls <see cref="EgrangResultsView.Show"/>.
    ///
    /// A view built in code is active from the moment `AddComponent` returns, which is why the
    /// race tests never caught this: `Awake` had already run there. In the scene, `SetActive(true)`
    /// inside `Show` is what finally runs `Awake` — and an `Awake` that hides the panel
    /// unconditionally closes the results in the same frame the race opened them.
    /// </summary>
    public class EgrangResultsViewTests
    {
        GameObject _panel;

        [SetUp]
        public void SetUp()
        {
            _panel = new GameObject("results");
            _panel.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_panel != null) Object.DestroyImmediate(_panel);
        }

        static EgrangRunSummary AnyRun() =>
            new EgrangRunSummary(1, 3, 12.5f, 20, 4, 2, "Egrang Persegi", "Sisi 8 cm");

        [Test]
        public void ShowingAPanelThatStartsInactiveLeavesItOnScreen()
        {
            var view = _panel.AddComponent<EgrangResultsView>();

            view.Show(AnyRun());

            Assert.That(_panel.activeSelf, Is.True, "the results panel closed itself as it opened");
            Assert.That(view.IsShowing, Is.True);
        }

        [Test]
        public void APanelThatStartsInactiveStaysDownUntilItIsShown()
        {
            _panel.AddComponent<EgrangResultsView>();

            // Awake runs here, on the first activation that is not a Show.
            _panel.SetActive(true);

            Assert.That(_panel.activeSelf, Is.False, "an unshown results panel must not cover the run");
        }

        [Test]
        public void HidingTakesTheShownPanelBackDown()
        {
            var view = _panel.AddComponent<EgrangResultsView>();
            view.Show(AnyRun());

            view.Hide();

            Assert.That(_panel.activeSelf, Is.False);
            Assert.That(view.IsShowing, Is.False);
        }
    }
}
