using NUnit.Framework;

namespace Museum.Games.Egrang.Tests
{
    public class EgrangSeatingTests
    {
        [Test]
        public void LaneFollowsTheServerSeat_NotJoinOrder()
        {
            var seating = new EgrangSeating();

            seating.Assign("c", 2);
            seating.Assign("a", 0);
            seating.Assign("b", 1);

            Assert.That(seating.LaneOf("a"), Is.EqualTo(0));
            Assert.That(seating.LaneOf("b"), Is.EqualTo(1));
            Assert.That(seating.LaneOf("c"), Is.EqualTo(2));
            Assert.That(seating.SessionAt(2), Is.EqualTo("c"));
            Assert.That(seating.Count, Is.EqualTo(3));
        }

        [Test]
        public void ANameFollowsItsPlayerToWhicheverLaneTheySit()
        {
            var seating = new EgrangSeating();
            seating.Assign("a", 0);
            seating.SetName("a", "Budi");

            Assert.That(seating.NameOf(0), Is.EqualTo("Budi"));

            // Re-seating moves the person, and their name goes with them.
            seating.Assign("a", 2);

            Assert.That(seating.NameOf(2), Is.EqualTo("Budi"));
            Assert.That(seating.NameOf(0), Is.Empty);
        }

        [Test]
        public void AnUnknownOrUnnamedSessionHasNoName()
        {
            var seating = new EgrangSeating();
            seating.Assign("a", 0);

            Assert.That(seating.NameOf(0), Is.Empty);
            Assert.That(seating.NameOf(1), Is.Empty);
            Assert.That(seating.NameOfSession("nobody"), Is.Empty);
        }

        [Test]
        public void OnlyTheLocalSessionReadsAsLocal()
        {
            var seating = new EgrangSeating();
            seating.Assign("me", 1);
            seating.Assign("them", 0);
            seating.SetLocal("me");

            Assert.That(seating.IsLocal("me"), Is.True);
            Assert.That(seating.IsLocal("them"), Is.False);
            Assert.That(seating.IsLocal(null), Is.False);
        }

        [Test]
        public void LocalLaneIsWhicheverSeatTheLocalSessionGot()
        {
            var seating = new EgrangSeating();
            seating.Assign("a", 0);
            seating.Assign("me", 2);

            seating.SetLocal("me");

            Assert.That(seating.LocalLane, Is.EqualTo(2));
        }

        [Test]
        public void UnknownSessionsAndEmptyLanesReadAsAbsent()
        {
            var seating = new EgrangSeating();
            seating.Assign("a", 0);

            Assert.That(seating.LaneOf("nobody"), Is.EqualTo(-1));
            Assert.That(seating.SessionAt(1), Is.Null);
            Assert.That(seating.LocalLane, Is.EqualTo(-1));
        }

        [Test]
        public void ReassigningASessionMovesItRatherThanDuplicating()
        {
            var seating = new EgrangSeating();
            seating.Assign("a", 0);

            seating.Assign("a", 1);

            Assert.That(seating.LaneOf("a"), Is.EqualTo(1));
            Assert.That(seating.SessionAt(0), Is.Null);
            Assert.That(seating.Count, Is.EqualTo(1));
        }

        [Test]
        public void SeatingOntoAnOccupiedLaneEvictsTheOccupant()
        {
            var seating = new EgrangSeating();
            seating.Assign("alice", 0);
            seating.Assign("bob", 1);

            seating.Assign("alice", 1);

            Assert.That(seating.LaneOf("bob"), Is.EqualTo(-1));
            Assert.That(seating.LaneOf("alice"), Is.EqualTo(1));
            Assert.That(seating.SessionAt(1), Is.EqualTo("alice"));
            Assert.That(seating.SessionAt(0), Is.Null);
            Assert.That(seating.Count, Is.EqualTo(1));
        }
    }
}
