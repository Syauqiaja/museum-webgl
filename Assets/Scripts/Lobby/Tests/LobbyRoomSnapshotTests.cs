using System.Collections.Generic;
using NUnit.Framework;

namespace Museum.Lobby.Tests
{
    /// <summary>
    /// CanStart is the whole start rule in one expression, so the button is never more than a
    /// view of it. The server re-checks the same thing — this is UX, not authority.
    /// </summary>
    public class LobbyRoomSnapshotTests
    {
        static LobbyRoomSnapshot Room(int occupied, LobbyPhase phase = LobbyPhase.Waiting,
            string mySessionId = "s0")
        {
            var slots = new List<LobbySlot>();
            for (int i = 0; i < 4; i++)
            {
                slots.Add(i < occupied
                    ? LobbySlot.Occupied($"s{i}", $"P{i}", isHost: i == 0)
                    : LobbySlot.Empty);
            }

            return new LobbyRoomSnapshot("ABCDE", slots, phase, mySessionId, "s0");
        }

        [Test]
        public void OccupiedCount_CountsOnlySeatedPlayers()
        {
            Assert.That(Room(2).OccupiedCount, Is.EqualTo(2));
            Assert.That(Room(4).OccupiedCount, Is.EqualTo(4));
        }

        [Test]
        public void IsFull_OnlyWhenEverySlotIsTaken()
        {
            Assert.That(Room(3).IsFull, Is.False);
            Assert.That(Room(4).IsFull, Is.True);
        }

        [Test]
        public void CanStart_IsFalseForTheHostAlone()
        {
            Assert.That(Room(1).CanStart, Is.False);
        }

        [Test]
        public void CanStart_IsTrueForTheHostAtTwoPlayers()
        {
            Assert.That(Room(2).CanStart, Is.True);
        }

        [Test]
        public void CanStart_IsFalseForANonHostEvenWithEnoughPlayers()
        {
            Assert.That(Room(3, mySessionId: "s1").CanStart, Is.False);
        }

        [Test]
        public void CanStart_IsFalseOncePhaseHasLeftWaiting()
        {
            Assert.That(Room(4, LobbyPhase.Starting).CanStart, Is.False);
            Assert.That(Room(4, LobbyPhase.InProgress).CanStart, Is.False);
        }

        [Test]
        public void AmHost_ComparesMySessionIdWithTheHostSeat()
        {
            Assert.That(Room(2).AmHost, Is.True);
            Assert.That(Room(2, mySessionId: "s1").AmHost, Is.False);
        }
    }
}
