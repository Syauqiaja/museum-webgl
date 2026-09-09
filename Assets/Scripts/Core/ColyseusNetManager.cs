using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Persistent (DontDestroyOnLoad) wrapper around the Colyseus <see cref="Client"/>.
    /// Owns the connection, the player's chosen nickname, and the reconnection token so
    /// they survive scene loads (MainMenu → minigame). Thin by design: it opens rooms and
    /// hands the typed <see cref="Room{T}"/> back to the caller (the per-game controller),
    /// which wires OnStateChange / OnMessage / OnError itself.
    /// See Assets/Docs/networking.md and Assets/Docs/architecture.md.
    /// </summary>
    public class ColyseusNetManager : MonoBehaviour
    {
        public static ColyseusNetManager Instance { get; private set; }

        [SerializeField] private ServerConfig config;

        /// <summary>
        /// Player nickname, sent as `displayName` on every create/join. Stored on
        /// <see cref="SessionData"/> — a second copy here would be a second thing to keep in sync.
        /// Falls back to "Player" only if no name was ever entered.
        /// </summary>
        public string PlayerName
        {
            get
            {
                if (SessionData.Instance != null && SessionData.Instance.HasPlayerName)
                {
                    return SessionData.Instance.PlayerName;
                }

                return "Player";
            }
            set
            {
                if (SessionData.Instance != null)
                {
                    SessionData.Instance.PlayerName = value;
                }
            }
        }

        private Client _client;

        /// <summary>
        /// The room the lobby opened, kept alive across the scene load that follows.
        ///
        /// Stored as <see cref="object"/> because <see cref="Room{T}"/> has no non-generic base
        /// worth naming here; <see cref="TakeLiveRoom{T}"/> does the one cast, and the room name
        /// beside it is what makes that cast safe.
        /// </summary>
        private object _liveRoom;
        private string _liveRoomName;

        /// <summary>True once at least one room has been opened and not explicitly left.</summary>
        /// <remarks>
        /// The seat itself (session id, reconnection token, room name) lives on
        /// <see cref="SessionData"/>, not here: the lobby's room object dies with the lobby
        /// scene, so the identity has to sit on the bootstrap singleton that outlives it.
        /// </remarks>
        public bool CanReconnect => SessionData.Instance != null && SessionData.Instance.CanReconnect;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Component only — see SessionData.Awake for why the bootstrap GameObject stays.
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Lazily builds the Colyseus client against the configured endpoint.</summary>
        public Client Client
        {
            get
            {
                if (_client == null)
                {
                    // Generated scenes (the lobby builder) add this component in code and cannot
                    // pick an asset reference, so fall back to the one in Resources rather than
                    // making every generator remember to wire it.
                    if (config == null)
                    {
                        config = Resources.Load<ServerConfig>("ServerConfig");
                    }

                    if (config == null)
                    {
                        throw new InvalidOperationException(
                            "ColyseusNetManager has no ServerConfig assigned, and none was found " +
                            "at Resources/ServerConfig.");
                    }

                    _client = new Client(config.Endpoint);
                }

                return _client;
            }
        }

        /// <summary>
        /// Host flow — create a new room; the server generates the shareable code
        /// (<see cref="Room{T}.RoomId"/>). Caller keeps the returned typed room.
        /// </summary>
        public async Task<Room<T>> CreateRoom<T>(string roomName, Dictionary<string, object> options = null)
            where T : Colyseus.Schema.Schema
        {
            Room<T> room = await Client.Create<T>(roomName, BuildOptions(options));
            Cache(room);
            return room;
        }

        /// <summary>Join flow — join an existing room by its shareable code.</summary>
        /// <remarks>
        /// Throws on room-not-found/expired; a full room surfaces as a connection error
        /// (inspect the exception, don't assume "not found"). See networking.md §3.
        /// </remarks>
        public async Task<Room<T>> JoinRoomById<T>(string roomId, Dictionary<string, object> options = null)
            where T : Colyseus.Schema.Schema
        {
            Room<T> room = await Client.JoinById<T>(roomId, BuildOptions(options));
            Cache(room);
            return room;
        }

        /// <summary>
        /// Takes the still-open room the lobby left behind, or null if there is none of the
        /// requested type.
        ///
        /// **Why this exists at all.** A Colyseus room is a socket, and a socket does not care
        /// about scenes: when the lobby scene unloads, its <see cref="Room{T}"/> object is
        /// dropped but the connection underneath is still open and the server still counts that
        /// session as present. Reconnecting by token on top of that is not a reconnect — the
        /// server has no <c>allowReconnection</c> pending for a session that never left, so it
        /// closes the *new* connection (code 4002) while the SDK still hands back a Room object.
        /// The result is a room that is bound, silent and permanently empty: no state, no
        /// patches, no error. That is what "the board has no cards" looked like.
        ///
        /// So the same-process handoff is this: the lobby's room is kept here and picked up by
        /// the game scene. The token path below stays for the case it was written for — the
        /// socket really is gone (a WebGL page reload), and the server really is holding the
        /// seat open.
        /// </summary>
        public Room<T> TakeLiveRoom<T>(string roomName)
            where T : Colyseus.Schema.Schema
        {
            if (_liveRoom == null || _liveRoomName != roomName)
            {
                return null;
            }

            Room<T> room = _liveRoom as Room<T>;

            // A name match with a type mismatch means the caller and the lobby disagree about
            // what "dakon" is, which is a bug worth seeing rather than silently reconnecting past.
            if (room == null)
            {
                Debug.LogWarning(
                    $"A live '{roomName}' room is held, but not as {typeof(T).Name} — ignoring it.", this);
                return null;
            }

            _liveRoom = null;
            _liveRoomName = null;

            // Held rooms are only worth anything while their socket is open. A closed one is the
            // case the reconnection token exists for, so report nothing here and let the caller
            // fall through to it rather than binding a room that can never receive a patch.
            if (room.Connection == null || !room.Connection.IsOpen)
            {
                Debug.LogWarning(
                    $"The held '{roomName}' room's connection is closed — reconnecting instead.", this);
                return null;
            }

            return room;
        }

        /// <summary>
        /// Hands this manager the room to carry into the next scene. Called by whoever opened it
        /// (the lobby) at the moment the scene it lives in is about to go away.
        /// </summary>
        public void KeepRoomAlive<T>(Room<T> room)
            where T : Colyseus.Schema.Schema
        {
            if (room == null) return;

            _liveRoom = room;
            _liveRoomName = room.Name;
        }

        /// <summary>Forgets the carried room without closing it — used when the seat is released.</summary>
        public void ForgetLiveRoom()
        {
            _liveRoom = null;
            _liveRoomName = null;
        }

        /// <summary>
        /// Reconnect into the seat cached from the last create/join, within the server's
        /// allowReconnection window. Returns null if there is nothing to reconnect to.
        ///
        /// Only correct once the previous socket is closed — see <see cref="TakeLiveRoom{T}"/>.
        /// </summary>
        public async Task<Room<T>> Reconnect<T>()
            where T : Colyseus.Schema.Schema
        {
            if (!CanReconnect)
            {
                return null;
            }

            Room<T> room = await Client.Reconnect<T>(SessionData.Instance.ReconnectionToken);
            Cache(room);
            return room;
        }

        /// <summary>
        /// Explicitly leave. Always call this on quit / return-to-lobby rather than just
        /// destroying the scene — it lets the server run onLeave / idle cleanup promptly.
        /// </summary>
        public async Task Leave<T>(Room<T> room, bool consented = true)
            where T : Colyseus.Schema.Schema
        {
            if (room != null)
            {
                try
                {
                    await room.Leave(consented);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Room.Leave threw (already closed?): {e.Message}", this);
                }
            }

            if (SessionData.Instance != null)
            {
                SessionData.Instance.ClearRoomSession();
            }

            ForgetLiveRoom();
        }

        private Dictionary<string, object> BuildOptions(Dictionary<string, object> options)
        {
            options ??= new Dictionary<string, object>();
            options["displayName"] = PlayerName;
            return options;
        }

        /// <summary>
        /// Record the seat this create/join/reconnect produced, so a later scene (or a dropped
        /// socket) can find it. Warns rather than throws when the bootstrap singleton is
        /// missing: the room is already open and playable, only reconnect is lost.
        ///
        /// Reads the room type from <see cref="Room{T}.Name"/> rather than trusting a
        /// caller-supplied string — the server sets it identically on create and join, so this
        /// is right for every caller. Passing it in used to also work for CreateRoom (its
        /// argument is the type by construction) but broke JoinRoomById, whose corresponding
        /// argument is the shareable code, not the type.
        /// </summary>
        private void Cache<T>(Room<T> room)
            where T : Colyseus.Schema.Schema
        {
            // Opening a room makes any room still held from an earlier scene stale: the seat this
            // client is playing is the new one. Left in place, that old room would be handed to
            // the next game scene instead of this one.
            if (!ReferenceEquals(_liveRoom, room))
            {
                ForgetLiveRoom();
            }

            if (SessionData.Instance == null)
            {
                Debug.LogWarning("No SessionData in the scene — the room's session id and " +
                                 "reconnection token have nowhere to live, so a reconnect after " +
                                 "a scene load will not be possible.", this);
                return;
            }

            SessionData.Instance.SetRoomSession(room.Name, room.RoomId, room.SessionId, room.ReconnectionToken);
        }
    }
}
