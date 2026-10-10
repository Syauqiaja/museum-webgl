using NUnit.Framework;

namespace Museum.Core.Tests
{
    /// <summary>Which track each scene plays, and how the Lobby knows which game it leads to.</summary>
    public class SceneMusicTests
    {
        private LobbyRequest _savedPending;

        [SetUp]
        public void SetUp() => _savedPending = LobbyRequest.Pending;

        [TearDown]
        public void TearDown() => LobbyRequest.Pending = _savedPending;

        [Test]
        public void The_menu_and_the_museum_share_the_museum_track()
        {
            Assert.AreEqual(MusicTrack.Museum, SceneMusic.For(SceneReference.MainMenu, ""));
            Assert.AreEqual(MusicTrack.Museum, SceneMusic.For(SceneReference.Museum, ""));
        }

        [Test]
        public void Each_game_plays_its_own_track()
        {
            Assert.AreEqual(MusicTrack.Dakon, SceneMusic.For(SceneReference.Dakon, ""));
            Assert.AreEqual(MusicTrack.Egrang, SceneMusic.For(SceneReference.Egrang, ""));
        }

        [Test]
        public void The_lobby_plays_the_track_of_the_game_it_leads_to()
        {
            Assert.AreEqual(MusicTrack.Dakon, SceneMusic.For(SceneReference.Lobby, SceneReference.Dakon));
            Assert.AreEqual(MusicTrack.Egrang, SceneMusic.For(SceneReference.Lobby, SceneReference.Egrang));
            Assert.AreEqual(MusicTrack.Museum, SceneMusic.For(SceneReference.Lobby, ""), "a lobby opened directly");
        }

        [Test]
        public void A_scene_the_game_does_not_know_plays_nothing()
        {
            Assert.AreEqual(MusicTrack.None, SceneMusic.For("InitTestScene", ""));
        }

        [Test]
        public void The_lobbys_game_outlives_the_request_the_lobby_clears()
        {
            LobbyRequest.Pending = new LobbyRequest("dakon", 2, SceneReference.Dakon, "Dakon");
            LobbyRequest.Pending = null;   // as LobbyController does on Awake

            Assert.AreEqual(SceneReference.Dakon, LobbyRequest.LastGameScene);
        }
    }
}
