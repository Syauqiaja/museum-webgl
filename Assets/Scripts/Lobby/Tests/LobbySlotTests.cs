using NUnit.Framework;

namespace Museum.Lobby.Tests
{
    /// <summary>
    /// A slot is either a seated player or a visible "Waiting…" placeholder. The room always
    /// shows all four, so emptiness has to be a first-class state rather than a null.
    /// </summary>
    public class LobbySlotTests
    {
        [Test]
        public void Empty_HasNoSessionIdAndIsEmpty()
        {
            LobbySlot slot = LobbySlot.Empty;

            Assert.That(slot.IsEmpty, Is.True);
            Assert.That(slot.SessionId, Is.Empty);
            Assert.That(slot.IsHost, Is.False);
        }

        [Test]
        public void Occupied_CarriesNameAndHostFlag()
        {
            LobbySlot slot = LobbySlot.Occupied("s1", "Budi", isHost: true);

            Assert.That(slot.IsEmpty, Is.False);
            Assert.That(slot.SessionId, Is.EqualTo("s1"));
            Assert.That(slot.DisplayName, Is.EqualTo("Budi"));
            Assert.That(slot.IsHost, Is.True);
        }
    }
}
