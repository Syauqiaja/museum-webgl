using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// What the museum's HUD says and shows once the visitor has picked a scheme. Both behaviours
    /// here failed silently before 2026-09-10: the KONTROL card advertised WASD to a phone, and the
    /// doorway prompt told a touch visitor to press a key, because <c>promptLabel</c> was never
    /// wired and the runtime text therefore never ran.
    /// </summary>
    public class SchemeHudTests
    {
        private GameObject _bootstrap;
        private GameObject _root;
        private GameObject _doorway;

        private void GivenScheme(ControlScheme scheme)
        {
            _bootstrap = new GameObject("Bootstrap");
            _bootstrap.AddComponent<SessionData>().Scheme = scheme;
        }

        private void GivenNoChoiceWasMade() => _bootstrap = new GameObject("Bootstrap");

        [TearDown]
        public void TearDown()
        {
            if (_doorway != null)
            {
                // Router registrations are statics that outlive a test; leaving one set would
                // hand the next test a doorway it never created.
                TouchInteractRouter.Unregister(_doorway.GetComponent<SceneTriggerPrompt>());
                Object.DestroyImmediate(_doorway);
            }

            if (_root != null) Object.DestroyImmediate(_root);
            if (_bootstrap != null) Object.DestroyImmediate(_bootstrap);
        }

        // --- the KONTROL card ---------------------------------------------------------

        [Test]
        public void DesktopOnly_hides_itself_under_the_touch_scheme()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Tutorial");
            _root.AddComponent<DesktopOnly>();

            Assert.IsFalse(_root.activeSelf);
        }

        [Test]
        public void DesktopOnly_stays_visible_under_the_desktop_scheme()
        {
            GivenScheme(ControlScheme.Desktop);
            _root = new GameObject("Tutorial");
            _root.AddComponent<DesktopOnly>();

            Assert.IsTrue(_root.activeSelf);
        }

        [Test]
        public void An_unmade_choice_keeps_the_desktop_HUD()
        {
            // Unknown reads as desktop everywhere in this client, so a scene opened straight from
            // the Editor still shows the legend rather than hiding it.
            GivenNoChoiceWasMade();
            _root = new GameObject("Tutorial");
            _root.AddComponent<DesktopOnly>();

            Assert.IsTrue(_root.activeSelf);
        }

        // --- the doorway prompt -------------------------------------------------------

        /// <summary>
        /// Builds a doorway the way the scene does. The object is held inactive while the label is
        /// wired, because <c>AddComponent</c> would otherwise run <c>Awake</c> against a null one —
        /// which is exactly the state the four real doorways shipped in.
        /// </summary>
        private TMP_Text GivenADoorwayPrompt()
        {
            _root = new GameObject("Doorway");
            _root.SetActive(false);

            SceneTriggerPrompt prompt = _root.AddComponent<SceneTriggerPrompt>();

            var capObject = new GameObject("Text (TMP)", typeof(RectTransform));
            capObject.transform.SetParent(_root.transform);
            TMP_Text cap = capObject.AddComponent<TextMeshProUGUI>();

            typeof(SceneTriggerPrompt)
                .GetField("promptLabel", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(prompt, cap);

            _root.SetActive(true);
            return cap;
        }

        [Test]
        public void The_prompt_names_the_Enter_key_on_desktop()
        {
            GivenScheme(ControlScheme.Desktop);
            Assert.AreEqual("ENTER", GivenADoorwayPrompt().text);
        }

        [Test]
        public void The_prompt_names_the_Interaksi_button_on_touch()
        {
            // There is no Enter key on a phone, and no keyboard to press it with. The overlay's
            // Interaksi button is what the visitor can actually reach.
            GivenScheme(ControlScheme.Sentuh);
            Assert.AreEqual("INTERAKSI", GivenADoorwayPrompt().text);
        }

        [Test]
        public void The_prompt_names_the_Enter_key_when_no_scheme_was_chosen()
        {
            GivenNoChoiceWasMade();
            Assert.AreEqual("ENTER", GivenADoorwayPrompt().text);
        }

        // --- controls that only appear when they do something -------------------------

        private CanvasGroup GivenAGatedButton(InteractionCue cue)
        {
            _root = new GameObject("Button", typeof(RectTransform), typeof(CanvasGroup));
            _root.SetActive(false);

            ShownWhenAvailable gate = _root.AddComponent<ShownWhenAvailable>();
            typeof(ShownWhenAvailable)
                .GetField("cue", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(gate, cue);

            _root.SetActive(true);
            return _root.GetComponent<CanvasGroup>();
        }

        [Test]
        public void The_Interaksi_button_is_hidden_with_no_doorway_in_reach()
        {
            GivenScheme(ControlScheme.Sentuh);
            CanvasGroup group = GivenAGatedButton(InteractionCue.Doorway);

            Assert.AreEqual(0f, group.alpha);
            Assert.IsFalse(group.interactable);

            // Raycast-transparent too, so it does not eat taps meant for the look area behind it.
            Assert.IsFalse(group.blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator The_Interaksi_button_appears_once_a_doorway_registers()
        {
            GivenScheme(ControlScheme.Sentuh);
            CanvasGroup group = GivenAGatedButton(InteractionCue.Doorway);

            _doorway = new GameObject("Doorway");
            TouchInteractRouter.Register(_doorway.AddComponent<SceneTriggerPrompt>());

            yield return null;   // one frame, so the gate's own Update runs

            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(group.interactable);
            Assert.IsTrue(group.blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator The_Interaksi_button_goes_away_again_when_the_doorway_unregisters()
        {
            GivenScheme(ControlScheme.Sentuh);
            CanvasGroup group = GivenAGatedButton(InteractionCue.Doorway);

            _doorway = new GameObject("Doorway");
            var prompt = _doorway.AddComponent<SceneTriggerPrompt>();
            TouchInteractRouter.Register(prompt);
            yield return null;

            TouchInteractRouter.Unregister(prompt);
            yield return null;

            // The gate has to survive the round trip: hiding via SetActive would have stopped
            // Update at the first step and stranded the button on screen forever.
            Assert.AreEqual(0f, group.alpha);
        }

        [UnityTest]
        public IEnumerator The_Interaksi_button_appears_at_a_gallery_station()
        {
            // Interact() acts on a station as well as a doorway, so a gate that only watched
            // doorways hid the one button that would have rung the gong.
            GivenScheme(ControlScheme.Sentuh);
            CanvasGroup group = GivenAGatedButton(InteractionCue.Doorway);

            var station = new StationStandIn();
            TouchInteractRouter.Register(station);
            try
            {
                yield return null;

                Assert.AreEqual(1f, group.alpha);
                Assert.IsTrue(group.blocksRaycasts);
            }
            finally
            {
                TouchInteractRouter.Unregister(station);
            }
        }

        private sealed class StationStandIn : IInteractable
        {
            public void Interact() { }
        }

        [UnityTest]
        public IEnumerator The_page_arrows_ignore_a_doorway()
        {
            // A doorway is not a plaque: standing in one must not light up the page arrows.
            GivenScheme(ControlScheme.Sentuh);
            CanvasGroup group = GivenAGatedButton(InteractionCue.LessonPaging);

            _doorway = new GameObject("Doorway");
            TouchInteractRouter.Register(_doorway.AddComponent<SceneTriggerPrompt>());

            yield return null;

            Assert.AreEqual(0f, group.alpha);
        }
    }
}
