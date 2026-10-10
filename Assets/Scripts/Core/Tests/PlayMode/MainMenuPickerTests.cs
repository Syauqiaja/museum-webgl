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

        // SessionData writes the name and avatar straight to PlayerPrefs, so a test that sets one
        // would hand it to every later test's fresh session — and to the Editor's own. Stashed and
        // cleared per test, then put back, as SessionDataTests does.
        private static readonly string[] PrefKeys = { "museum.session.playerName", "museum.session.playerAvatar" };
        private readonly string[] _savedPrefs = new string[PrefKeys.Length];
        private readonly bool[] _hadPrefs = new bool[PrefKeys.Length];

        [SetUp]
        public void StashPrefs()
        {
            for (int i = 0; i < PrefKeys.Length; i++)
            {
                _hadPrefs[i] = PlayerPrefs.HasKey(PrefKeys[i]);
                _savedPrefs[i] = PlayerPrefs.GetString(PrefKeys[i], string.Empty);
                PlayerPrefs.DeleteKey(PrefKeys[i]);
            }
        }

        [TearDown]
        public void RestorePrefs()
        {
            for (int i = 0; i < PrefKeys.Length; i++)
            {
                if (_hadPrefs[i]) PlayerPrefs.SetString(PrefKeys[i], _savedPrefs[i]);
                else PlayerPrefs.DeleteKey(PrefKeys[i]);
            }
        }

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

        private GameObject[] _frames = new GameObject[0];

        private MainMenu GivenMenuWithAvatars()
        {
            _menuObject = new GameObject("MainMenu");
            _picker = new GameObject("Platform Panel");
            _title = new GameObject("Title");
            _frames = new GameObject[PlayerAvatars.Ids.Count];
            for (int i = 0; i < _frames.Length; i++) _frames[i] = new GameObject("Selected Frame " + i);

            var menu = _menuObject.AddComponent<MainMenu>();
            menu.Configure(_picker, new[] { _title }, null, null, null, _frames);
            return menu;
        }

        [TearDown]
        public void TearDownFrames()
        {
            foreach (GameObject frame in _frames)
            {
                if (frame != null) Object.DestroyImmediate(frame);
            }
            _frames = new GameObject[0];
        }

        [Test]
        public void Jawa_is_selected_by_default()
        {
            GivenSession();
            GivenMenuWithAvatars();

            Assert.IsTrue(_frames[0].activeSelf, "Jawa's frame");
            for (int i = 1; i < _frames.Length; i++) Assert.IsFalse(_frames[i].activeSelf, $"frame {i}");
        }

        [Test]
        public void Selecting_an_avatar_stores_it_and_moves_the_frame()
        {
            SessionData session = GivenSession();
            MainMenu menu = GivenMenuWithAvatars();

            menu.SelectAvatar(2);

            Assert.AreEqual(PlayerAvatars.Bugis, session.PlayerAvatar);
            for (int i = 0; i < _frames.Length; i++) Assert.AreEqual(i == 2, _frames[i].activeSelf, $"frame {i}");
        }

        [Test]
        public void An_out_of_range_avatar_index_changes_nothing()
        {
            SessionData session = GivenSession();
            MainMenu menu = GivenMenuWithAvatars();

            menu.SelectAvatar(9);

            Assert.AreEqual(PlayerAvatars.Jawa, session.PlayerAvatar);
            Assert.IsTrue(_frames[0].activeSelf);
        }

        [Test]
        public void A_returning_visitor_sees_their_avatar_still_selected()
        {
            SessionData session = GivenSession();
            session.PlayerAvatar = PlayerAvatars.Bali;

            GivenMenuWithAvatars();

            Assert.IsTrue(_frames[1].activeSelf, "Bali's frame");
            Assert.IsFalse(_frames[0].activeSelf);
        }
    }
}
