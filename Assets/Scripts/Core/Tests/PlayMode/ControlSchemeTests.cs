using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The scheme is per-visit memory on the bootstrap object. These tests build the object the
    /// way a scene does — AddComponent runs Awake — the same shape SessionDataTests uses.
    /// </summary>
    public class ControlSchemeTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("Bootstrap");

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private SessionData Bootstrap() => _go.AddComponent<SessionData>();

        [Test]
        public void Scheme_starts_unknown()
        {
            SessionData session = Bootstrap();
            Assert.AreEqual(ControlScheme.Unknown, session.Scheme);
        }

        [Test]
        public void IsTouch_is_false_until_the_visitor_picks_touch()
        {
            SessionData session = Bootstrap();
            Assert.IsFalse(session.IsTouch);

            session.Scheme = ControlScheme.Desktop;
            Assert.IsFalse(session.IsTouch);

            session.Scheme = ControlScheme.Sentuh;
            Assert.IsTrue(session.IsTouch);
        }

        [Test]
        public void Scheme_is_not_written_to_PlayerPrefs()
        {
            SessionData session = Bootstrap();
            session.Scheme = ControlScheme.Sentuh;

            // A kiosk serves a new visitor each time; a remembered scheme would be wrong for them.
            Assert.IsFalse(PlayerPrefs.HasKey("museum.session.controlScheme"));
        }
    }
}
