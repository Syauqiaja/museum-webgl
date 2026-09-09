using NUnit.Framework;
using UnityEngine;

namespace Museum.Core.Tests
{
    /// <summary>
    /// What every multiplayer minigame relies on: the nickname outlives the tab, and the seat
    /// (session id + reconnection token) does not outlive the room it belongs to.
    ///
    /// PlayMode rather than EditMode because <see cref="SessionData"/> is a
    /// <c>DontDestroyOnLoad</c> singleton, and that call throws outside play mode.
    /// </summary>
    public class SessionDataTests
    {
        private const string PlayerNameKey = "museum.session.playerName";

        private GameObject _go;
        private string _savedPrefValue;
        private bool _hadPrefValue;

        [SetUp]
        public void SetUp()
        {
            _hadPrefValue = PlayerPrefs.HasKey(PlayerNameKey);
            _savedPrefValue = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
            PlayerPrefs.DeleteKey(PlayerNameKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }

            if (_hadPrefValue)
            {
                PlayerPrefs.SetString(PlayerNameKey, _savedPrefValue);
            }
            else
            {
                PlayerPrefs.DeleteKey(PlayerNameKey);
            }
        }

        [Test]
        public void PlayerName_IsSanitizedOnStore()
        {
            SessionData session = NewSession();

            session.PlayerName = "   Budi   Santoso   ";

            Assert.AreEqual("Budi Santoso", session.PlayerName);
            Assert.IsTrue(session.HasPlayerName);
        }

        [Test]
        public void PlayerName_TooShortAfterTrim_IsNotAName()
        {
            SessionData session = NewSession();

            session.PlayerName = " a ";

            Assert.IsFalse(session.HasPlayerName);
        }

        [Test]
        public void PlayerName_SurvivesAFreshSession()
        {
            NewSession().PlayerName = "Siti";
            Object.DestroyImmediate(_go);

            SessionData revisit = NewSession();

            Assert.AreEqual("Siti", revisit.PlayerName);
            Assert.IsTrue(revisit.HasPlayerName);
        }

        [Test]
        public void Seat_IsEmptyUntilARoomIsJoined()
        {
            SessionData session = NewSession();

            Assert.IsFalse(session.HasSession);
            Assert.IsFalse(session.CanReconnect);
            Assert.AreEqual(string.Empty, session.SessionId);
            Assert.AreEqual(string.Empty, session.RoomName);
        }

        [Test]
        public void SetRoomSession_RecordsTheSeat()
        {
            SessionData session = NewSession();

            session.SetRoomSession("dakon", "XBM7A", "abc123", null);

            Assert.AreEqual("dakon", session.RoomName);
            Assert.AreEqual("XBM7A", session.LastRoomCode);
            Assert.AreEqual("abc123", session.SessionId);
            Assert.IsTrue(session.HasSession);
        }

        [Test]
        public void ClearRoomSession_DropsTheSeatButKeepsTheTypedCode()
        {
            SessionData session = NewSession();
            session.SetRoomSession("dakon", "XBM7A", "abc123", null);

            session.ClearRoomSession();

            Assert.IsFalse(session.HasSession);
            Assert.IsFalse(session.CanReconnect);
            Assert.AreEqual(string.Empty, session.RoomName);

            // Still on screen in the join field — the code is not part of the seat.
            Assert.AreEqual("XBM7A", session.LastRoomCode);
        }

        [Test]
        public void Seat_IsNotRestoredOnAFreshSession()
        {
            NewSession().SetRoomSession("dakon", "XBM7A", "abc123", null);
            Object.DestroyImmediate(_go);

            SessionData revisit = NewSession();

            // A session id issued to a socket that no longer exists must not come back:
            // the client would claim a seat nobody is holding.
            Assert.IsFalse(revisit.HasSession);
            Assert.IsFalse(revisit.CanReconnect);
        }

        /// <summary>A bootstrap object as a scene creates it — Awake runs on AddComponent.</summary>
        private SessionData NewSession()
        {
            _go = new GameObject("Bootstrap (test)");
            return _go.AddComponent<SessionData>();
        }
    }
}
