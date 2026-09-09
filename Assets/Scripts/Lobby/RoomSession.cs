using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using Museum.Core;
using Museum.Net.State;

namespace Museum.Lobby
{
    /// <summary>
    /// A live Colyseus room, minus the generic parameter.
    ///
    /// <c>Room&lt;T&gt;</c> is generic over the state schema and the decoder addresses fields by
    /// index, so a Dakon room genuinely has to be opened as <c>Room&lt;DakonState&gt;</c> — reading
    /// it as the base class would leave the decoder unable to place the game's own fields. The
    /// lobby, though, only ever reads what every game inherits (phase, host, players). This class
    /// is that seam: one non-generic surface, one generic subclass per state type, and a factory
    /// that maps a room name to the right one.
    ///
    /// Adding a game means adding a line to <see cref="Factories"/> and nothing else here.
    /// </summary>
    internal abstract class RoomSession
    {
        /// <summary>Room name → how to open that room with its own generated schema.</summary>
        private static readonly Dictionary<string, IRoomFactory> Factories =
            new Dictionary<string, IRoomFactory>(StringComparer.OrdinalIgnoreCase)
            {
                { "dakon", new RoomFactory<DakonState>() },
                { "egrang", new RoomFactory<EgrangState>() },
            };

        /// <summary>Raised on every state patch — the lobby republishes a whole snapshot.</summary>
        public event Action Changed;

        /// <summary>An `error` message from the server: (code, message).</summary>
        public event Action<string, string> ServerError;

        /// <summary>The socket closed, for any reason including the server going away.</summary>
        public event Action Closed;

        public abstract string RoomId { get; }
        public abstract string SessionId { get; }
        public abstract BaseGameState State { get; }
        public abstract Task Leave(bool consented);
        public abstract void Send(string type);

        /// <summary>
        /// Hands the underlying room to <see cref="ColyseusNetManager"/> to carry into the game
        /// scene, and stops forwarding its events to this (about to be destroyed) lobby.
        ///
        /// The alternative — letting the lobby's room object fall away and reconnecting by token
        /// in the game scene — does not work: the socket stays open, so the server has no
        /// reconnection pending and closes the second connection while the SDK still returns a
        /// Room. See <see cref="ColyseusNetManager.TakeLiveRoom{T}"/>.
        /// </summary>
        public abstract void HandOffToGameScene();

        protected void RaiseChanged() => Changed?.Invoke();
        protected void RaiseServerError(string code, string message) => ServerError?.Invoke(code, message);
        protected void RaiseClosed() => Closed?.Invoke();

        /// <summary>True when this room name has a generated schema on the client.</summary>
        public static bool Supports(string roomName) =>
            !string.IsNullOrEmpty(roomName) && Factories.ContainsKey(roomName);

        public static Task<RoomSession> Create(string roomName, Dictionary<string, object> options) =>
            FactoryFor(roomName).Create(roomName, options);

        public static Task<RoomSession> JoinById(string roomName, string code, Dictionary<string, object> options) =>
            FactoryFor(roomName).JoinById(code, options);

        private static IRoomFactory FactoryFor(string roomName)
        {
            if (Factories.TryGetValue(roomName ?? string.Empty, out IRoomFactory factory))
            {
                return factory;
            }

            throw new NotSupportedException(
                $"No generated schema for room '{roomName}'. Regenerate it — see Assets/Scripts/Net/README.md.");
        }

        private interface IRoomFactory
        {
            Task<RoomSession> Create(string roomName, Dictionary<string, object> options);
            Task<RoomSession> JoinById(string code, Dictionary<string, object> options);
        }

        private sealed class RoomFactory<T> : IRoomFactory where T : BaseGameState, new()
        {
            public async Task<RoomSession> Create(string roomName, Dictionary<string, object> options) =>
                new TypedSession<T>(await ColyseusNetManager.Instance.CreateRoom<T>(roomName, options));

            public async Task<RoomSession> JoinById(string code, Dictionary<string, object> options) =>
                new TypedSession<T>(await ColyseusNetManager.Instance.JoinRoomById<T>(code, options));
        }

        /// <summary>The generic half: holds the typed room and forwards its events upward.</summary>
        private sealed class TypedSession<T> : RoomSession where T : BaseGameState, new()
        {
            private readonly Room<T> _room;

            // Kept as fields rather than inline lambdas so the handoff can unsubscribe them: the
            // room outlives this lobby now, and a patch delivered to a destroyed lobby is a
            // MissingReferenceException per patch for the rest of the match.
            private readonly Room<T>.StateChangeEventHandler _onStateChange;
            private readonly CloseWithCodeEventHandler _onLeave;
            private readonly ErrorEventHandler _onError;

            public TypedSession(Room<T> room)
            {
                _room = room;

                _onStateChange = (_, __) => RaiseChanged();
                _onLeave = _ => RaiseClosed();

                // Transport-level failure (the server refusing something after the join
                // succeeded). Reported as a connection problem: it is not one of the lobby's
                // own rejections, which arrive as `error` messages below.
                _onError = (_, message) => RaiseServerError(LobbyError.ConnectionFailed, message);

                _room.OnStateChange += _onStateChange;
                _room.OnLeave += _onLeave;
                _room.OnError += _onError;

                _room.OnMessage<ErrorPayload>(MessageType.Error,
                    payload => RaiseServerError(payload.code, payload.message));
            }

            public override void HandOffToGameScene()
            {
                _room.OnStateChange -= _onStateChange;
                _room.OnLeave -= _onLeave;
                _room.OnError -= _onError;

                DropMessageHandler(MessageType.Error);

                if (ColyseusNetManager.Instance != null)
                {
                    ColyseusNetManager.Instance.KeepRoomAlive(_room);
                }
            }

            /// <summary>
            /// Un-registers one of this lobby's <c>OnMessage</c> handlers, by reflection because
            /// the SDK has no way to do it in public API.
            ///
            /// It is not optional. <c>Room.OnMessage</c> is a bare <c>Dictionary.Add</c> on a
            /// protected dictionary, so the game scene registering the same message type on the
            /// room it inherits throws "An item with the same key has already been added" — and
            /// the lobby's handler would otherwise keep firing into a destroyed scene anyway.
            /// Only the types this class added are removed, so the SDK's own internal handlers
            /// survive.
            /// </summary>
            private void DropMessageHandler(string type)
            {
                System.Reflection.FieldInfo field = typeof(Room<T>).GetField(
                    "OnMessageHandlers",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field?.GetValue(_room) is System.Collections.IDictionary handlers)
                {
                    handlers.Remove(type);
                    return;
                }

                // A future SDK could rename the field. Say so loudly: the symptom otherwise is an
                // exception thrown deep inside the next scene's session constructor.
                UnityEngine.Debug.LogWarning(
                    "Could not reach the Colyseus room's message handlers to un-register " +
                    $"'{type}'. The game scene may throw when it registers its own.");
            }

            public override string RoomId => _room.RoomId;
            public override string SessionId => _room.SessionId;
            public override BaseGameState State => _room.State;

            public override Task Leave(bool consented) => _room.Leave(consented);

            public override void Send(string type) => _room.Send(type, new { });
        }
    }
}
