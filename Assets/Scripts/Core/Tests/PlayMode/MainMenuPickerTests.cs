using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The first thing a visitor touches. What matters is that their tap — not the browser's
    /// guess — is what ends up on SessionData, and that the menu behind it appears afterwards.
    /// </summary>
    public class MainMenuPickerTests
    {
        private GameObject _bootstrap;
        private GameObject _menuObject;
        private GameObject _picker;
        private GameObject _title;

        private SessionData GivenSession()
        {
            _bootstrap = new GameObject("Bootstrap");
            return _bootstrap.AddComponent<SessionData>();
        }

        private MainMenu GivenMenu()
        {
            _menuObject = new GameObject("MainMenu");
            _picker = new GameObject("Platform Panel");
            _title = new GameObject("Title");

            var menu = _menuObject.AddComponent<MainMenu>();
            menu.Configure(_picker, new[] { _title }, null, null);
            return menu;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { _menuObject, _picker, _title, _bootstrap })
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void The_picker_is_shown_and_the_menu_hidden_until_a_choice_is_made()
        {
            GivenSession();
            GivenMenu();

            Assert.IsTrue(_picker.activeSelf);
            Assert.IsFalse(_title.activeSelf);
        }

        [Test]
        public void Choosing_touch_writes_the_touch_scheme()
        {
            SessionData session = GivenSession();
            MainMenu menu = GivenMenu();

            menu.ChooseTouch();

            Assert.AreEqual(ControlScheme.Sentuh, session.Scheme);
        }

        [Test]
        public void Choosing_desktop_writes_the_desktop_scheme()
        {
            SessionData session = GivenSession();
            MainMenu menu = GivenMenu();

            menu.ChooseDesktop();

            Assert.AreEqual(ControlScheme.Desktop, session.Scheme);
        }

        [Test]
        public void A_choice_reveals_the_menu_and_dismisses_the_picker()
        {
            GivenSession();
            MainMenu menu = GivenMenu();

            menu.ChooseDesktop();

            Assert.IsFalse(_picker.activeSelf);
            Assert.IsTrue(_title.activeSelf);
        }

        [Test]
        public void Choosing_without_a_SessionData_does_not_throw()
        {
            // MainMenu.unity opened straight from the Editor has no bootstrap object.
            MainMenu menu = GivenMenu();
            Assert.DoesNotThrow(menu.ChooseTouch);
        }

        [Test]
        public void A_session_that_already_chose_skips_the_picker()
        {
            // Back from a game: the bootstrap object still holds the answer, so asking again
            // reads as "the game logged me out".
            SessionData session = GivenSession();
            session.Scheme = ControlScheme.Desktop;

            GivenMenu();

            Assert.IsFalse(_picker.activeSelf);
            Assert.IsTrue(_title.activeSelf);
        }

        [Test]
        public void The_name_field_is_prefilled_from_the_session()
        {
            SessionData session = GivenSession();
            session.PlayerName = "Sari";

            _menuObject = new GameObject("MainMenu");
            _picker = new GameObject("Platform Panel");
            _title = new GameObject("Title");
            var field = _title.AddComponent<TMPro.TMP_InputField>();

            var menu = _menuObject.AddComponent<MainMenu>();
            menu.Configure(_picker, new[] { _title }, null, null, field);

            Assert.AreEqual("Sari", field.text);
        }
    }
}
