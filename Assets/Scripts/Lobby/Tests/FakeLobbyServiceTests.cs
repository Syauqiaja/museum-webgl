using System.Collections.Generic;
using NUnit.Framework;

namespace Museum.Lobby.Tests
{
    /// <summary>
    /// The fake is not a throwaway mock — it is the behaviour the Node room has to match, written
    /// down and executable. These tests are therefore the lobby contract: seat assignment, the
    /// full-room rejection, host migration when the host walks away.
    /// </summary>
    public class FakeLobbyServiceTests
    {
        FakeLobbyService _service;
        List<LobbyRoomSnapshot> _snapshots;
        List<string> _errors;

        [SetUp]
        public void SetUp()
        {
            _service = new FakeLobbyService(seed: 1234);
            _snapshots = new List<LobbyRoomSnapshot>();
            _errors = new List<string>();
            _service.RoomUpdated += s => _snapshots.Add(s);
            _service.Failed += (code, _) => _errors.Add(code);
        }

        void CreateRoom() => _service.CreateRoom("egrang", "Budi", 4).GetAwaiter().GetResult();

        [Test]
        public void CreateRoom_SeatsTheCreatorAsHostInTheFirstSlot()
        {
            CreateRoom();

            LobbyRoomSnapshot room = _service.Current;
            Assert.That(room.Code, Is.Not.Empty);
            Assert.That(room.Slots.Count, Is.EqualTo(4));
            Assert.That(room.Slots[0].DisplayName, Is.EqualTo("Budi"));
            Assert.That(room.Slots[0].IsHost, Is.True);
            Assert.That(room.Slots[1].IsEmpty, Is.True);
            Assert.That(room.AmHost, Is.True);
            Assert.That(room.Phase, Is.EqualTo(LobbyPhase.Waiting));
            Assert.That(_snapshots, Has.Count.EqualTo(1), "creating raises exactly one update");
        }

        [Test]
        public void CreateRoom_WithAnUnusableName_FailsAndCreatesNothing()
        {
            _service.CreateRoom("egrang", " ", 4).GetAwaiter().GetResult();

            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.NameInvalid }));
            Assert.That(_service.Current, Is.Null);
            Assert.That(_snapshots, Is.Empty);
        }

        [Test]
        public void AddSimulatedPlayer_FillsTheLowestEmptySlotAndRaisesAnUpdate()
        {
            CreateRoom();

            _service.AddSimulatedPlayer("Sari");

            Assert.That(_service.Current.Slots[1].DisplayName, Is.EqualTo("Sari"));
            Assert.That(_service.Current.Slots[1].IsHost, Is.False);
            Assert.That(_service.Current.OccupiedCount, Is.EqualTo(2));
            Assert.That(_snapshots, Has.Count.EqualTo(2));
        }

        [Test]
        public void AddSimulatedPlayer_BeyondCapacity_FailsWithRoomFull()
        {
            CreateRoom();
            _service.AddSimulatedPlayer("Sari");
            _service.AddSimulatedPlayer("Andi");
            _service.AddSimulatedPlayer("Rina");

            _service.AddSimulatedPlayer("Fifth");

            Assert.That(_service.Current.IsFull, Is.True);
            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.RoomFull }));
        }

        [Test]
        public void RemoveSimulatedPlayer_FreesTheSlot()
        {
            CreateRoom();
            string sessionId = _service.AddSimulatedPlayer("Sari");

            _service.RemoveSimulatedPlayer(sessionId);

            Assert.That(_service.Current.Slots[1].IsEmpty, Is.True);
            Assert.That(_service.Current.OccupiedCount, Is.EqualTo(1));
        }

        [Test]
        public void RemovingTheHost_MigratesHostToTheLowestRemainingSeat()
        {
            CreateRoom();
            _service.AddSimulatedPlayer("Sari");
            string hostSessionId = _service.Current.HostSessionId;

            _service.RemoveSimulatedPlayer(hostSessionId);

            Assert.That(_service.Current.Slots[0].IsEmpty, Is.True);
            Assert.That(_service.Current.Slots[1].IsHost, Is.True);
            Assert.That(_service.Current.HostSessionId, Is.EqualTo(_service.Current.Slots[1].SessionId));
        }

        [Test]
        public void JoinRoom_WithAnUnknownCode_FailsWithRoomNotFound()
        {
            _service.JoinRoom("ZZZZZ", "Budi").GetAwaiter().GetResult();

            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.RoomNotFound }));
            Assert.That(_service.Current, Is.Null);
        }

        [Test]
        public void JoinRoom_WithTheCodeOfTheRoomJustCreated_SeatsTheJoinerAsNonHost()
        {
            // Create the room, leave a peer behind in it, then walk back in by code — the
            // create-then-join path a single developer uses to exercise the join screen.
            CreateRoom();
            string code = _service.Current.Code;
            _service.AddSimulatedPlayer("Sari");
            _service.Leave().GetAwaiter().GetResult();

            _service.JoinRoom(code, "Andi").GetAwaiter().GetResult();

            Assert.That(_errors, Is.Empty);
            Assert.That(_service.Current.Code, Is.EqualTo(code));
            Assert.That(_service.Current.OccupiedCount, Is.EqualTo(2), "Sari is still seated");
            Assert.That(_service.Current.AmHost, Is.False, "the host migrated to Sari when the creator left");
        }

        [Test]
        public void StartGame_BelowTheMinimum_FailsAndLeavesThePhaseAlone()
        {
            CreateRoom();

            _service.StartGame().GetAwaiter().GetResult();

            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.NotEnoughPlayers }));
            Assert.That(_service.Current.Phase, Is.EqualTo(LobbyPhase.Waiting));
        }

        [Test]
        public void StartGame_AsHostWithTwoPlayers_MovesThePhaseToInProgress()
        {
            CreateRoom();
            _service.AddSimulatedPlayer("Sari");

            _service.StartGame().GetAwaiter().GetResult();

            Assert.That(_errors, Is.Empty);
            Assert.That(_service.Current.Phase, Is.EqualTo(LobbyPhase.InProgress));
        }

        [Test]
        public void Leave_ClearsTheCurrentRoom()
        {
            CreateRoom();

            _service.Leave().GetAwaiter().GetResult();

            Assert.That(_service.Current, Is.Null);
        }

        [Test]
        public void ForceNextFailure_MakesTheNextCallFailWithThatCode()
        {
            _service.ForceNextFailure(LobbyError.ConnectionFailed);

            CreateRoom();

            Assert.That(_errors, Is.EqualTo(new[] { LobbyError.ConnectionFailed }));
            Assert.That(_service.Current, Is.Null, "a forced failure creates nothing");
        }
    }
}
