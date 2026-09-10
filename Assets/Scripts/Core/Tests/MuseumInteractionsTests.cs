using System.Collections.Generic;
using NUnit.Framework;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The seam between the museum's shared exhibits and the network: what another visitor set off
    /// reaches the station registered under that id, and nothing else.
    /// </summary>
    public class MuseumInteractionsTests
    {
        private sealed class Recorder : IRemoteInteractable
        {
            public readonly List<int> Played = new List<int>();
            public void PlayRemote(int index) => Played.Add(index);
        }

        private readonly List<(string, int)> _reported = new List<(string, int)>();

        private void Record(string station, int index) => _reported.Add((station, index));

        [SetUp]
        public void SetUp()
        {
            _reported.Clear();
            MuseumInteractions.LocalInteracted += Record;
        }

        [TearDown]
        public void TearDown() => MuseumInteractions.LocalInteracted -= Record;

        [Test]
        public void A_remote_interaction_reaches_the_station_registered_under_its_id()
        {
            var court = new Recorder();
            MuseumInteractions.Register("test-court", court);

            Assert.IsTrue(MuseumInteractions.PlayRemote("test-court", 5));
            CollectionAssert.AreEqual(new[] { 5 }, court.Played);

            MuseumInteractions.Unregister("test-court", court);
        }

        [Test]
        public void An_unknown_station_is_ignored()
        {
            Assert.IsFalse(MuseumInteractions.PlayRemote("test-nothing", 0));
            Assert.IsFalse(MuseumInteractions.PlayRemote(null, 0));
        }

        [Test]
        public void An_unregistered_station_no_longer_plays()
        {
            var gong = new Recorder();
            MuseumInteractions.Register("test-gong", gong);
            MuseumInteractions.Unregister("test-gong", gong);

            Assert.IsFalse(MuseumInteractions.PlayRemote("test-gong", 0));
            Assert.IsEmpty(gong.Played);
        }

        [Test]
        public void An_older_station_unregistering_does_not_unhook_its_replacement()
        {
            var older = new Recorder();
            var newer = new Recorder();
            MuseumInteractions.Register("test-gong", older);
            MuseumInteractions.Register("test-gong", newer);

            MuseumInteractions.Unregister("test-gong", older);

            Assert.IsTrue(MuseumInteractions.PlayRemote("test-gong", 0));
            Assert.AreEqual(1, newer.Played.Count);
            MuseumInteractions.Unregister("test-gong", newer);
        }

        [Test]
        public void A_local_interaction_is_reported_and_a_remote_one_is_not()
        {
            var gasing = new Recorder();
            MuseumInteractions.Register("test-gasing", gasing);

            MuseumInteractions.ReportLocal("test-gasing", 0);
            MuseumInteractions.PlayRemote("test-gasing", 0);

            CollectionAssert.AreEqual(new[] { ("test-gasing", 0) }, _reported, "a remote play must not echo back to the room");
            MuseumInteractions.Unregister("test-gasing", gasing);
        }

        [Test]
        public void The_station_ids_are_the_servers()
        {
            // src/rooms/museumStations.ts — MUSEUM_STATIONS.
            CollectionAssert.AreEqual(new[] { "gong", "gasing", "tembang", "engklek" },
                new[] { MuseumInteractions.Gong, MuseumInteractions.Gasing, MuseumInteractions.Tembang, MuseumInteractions.Engklek });
        }

        [Test]
        public void An_away_visitor_is_tagged_with_the_game_by_name()
        {
            Assert.AreEqual("Sedang bermain Dakon", MuseumActivities.Tag("dakon"));
            Assert.AreEqual("Sedang bermain Egrang", MuseumActivities.Tag("egrang"));
            Assert.AreEqual("Sedang bermain", MuseumActivities.Tag("engklek"), "a game this build has no name for");
            Assert.AreEqual(string.Empty, MuseumActivities.Tag(MuseumActivities.InHall));
            Assert.AreEqual(string.Empty, MuseumActivities.Tag(null));
        }
    }
}
