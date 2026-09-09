using System;
using System.Threading.Tasks;

namespace Museum.Lobby
{
    /// <summary>
    /// Everything the lobby screen can ask of a room, with no Colyseus type in sight. Two
    /// implementations: <see cref="FakeLobbyService"/> (in memory, playable and testable before
    /// the server has an `egrang` room) and ColyseusLobbyService (later, once the server's
    /// protocol.md defines the lobby state).
    ///
    /// Contract:
    /// - Every successful call raises <see cref="RoomUpdated"/> with a whole new snapshot.
    ///   Callers render from that snapshot and never mutate one.
    /// - Every failure raises <see cref="Failed"/> with a <see cref="LobbyError"/> code and does
    ///   NOT raise <see cref="RoomUpdated"/>. Failures are reported through the event rather than
    ///   thrown, so the UI has one path for "the server said no" whether it came from a local
    ///   guard or the wire.
    /// </summary>
    public interface ILobbyService
    {
        /// <summary>Latest snapshot, or null before a room is created or joined.</summary>
        LobbyRoomSnapshot Current { get; }

        event Action<LobbyRoomSnapshot> RoomUpdated;

        /// <summary>(code, message) — code is a <see cref="LobbyError"/> constant.</summary>
        event Action<string, string> Failed;

        /// <summary>Host flow. The room code is generated on the far side and arrives in the snapshot.</summary>
        Task CreateRoom(string roomName, string displayName, int maxPlayers);

        /// <summary>Join an existing room by its shareable code.</summary>
        Task JoinRoom(string code, string displayName);

        /// <summary>
        /// Host-only. Moves the room to <see cref="LobbyPhase.InProgress"/>.
        ///
        /// Calling it while not in a room is currently a silent no-op — unpinned rather than
        /// specified. A server implementation may instead raise <see cref="Failed"/>; nothing
        /// depends on either choice yet, and the UI never offers the button outside a room.
        /// </summary>
        Task StartGame();

        /// <summary>Explicit leave — never just drop the connection (Assets/Docs/networking.md §8).</summary>
        Task Leave();

        /// <summary>
        /// The match has started and the lobby scene is about to unload: release the room to
        /// whatever outlives the scene, without giving up the seat.
        ///
        /// An offline implementation has nothing to hand over and does nothing. For the Colyseus
        /// one this is not optional — the connection has to survive the scene load, because a
        /// room whose socket is still open cannot be re-entered by reconnection token.
        /// </summary>
        void HandOffToGameScene();
    }
}
