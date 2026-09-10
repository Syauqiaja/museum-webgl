using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using Museum.Core;
using Museum.Net.State;
using TMPro;
using UnityEngine;

namespace Museum.Net
{
    /// <summary>
    /// Keeps the museum's visitors in sight of each other. Joins the server's `museum` presence
    /// room, reports where the local player is a few times a second, and draws a
    /// <see cref="MuseumVisitorAvatar"/> for everyone else the room lists.
    /// </summary>
    /// <remarks>
    /// This is the one place the Museum scene touches the network, and it is deliberately not a
    /// match: there is no seat, no host, no reconnection token. The room is opened straight on
    /// the <see cref="ColyseusNetManager.Client"/> rather than through its create/join helpers,
    /// because those record the room as "the seat this client holds" on <see cref="SessionData"/>
    /// — and the seat that matters is the Dakon or Egrang one the lobby is about to open.
    ///
    /// The room is left when this component is destroyed, which is what happens when a doorway
    /// loads the lobby. A player who comes back to the museum joins afresh.
    ///
    /// With no <see cref="ColyseusNetManager"/> in the scene (opened directly in the editor) or
    /// no server reachable, the museum is simply single-player — which is how it always was.
    /// </remarks>
    public sealed class MuseumPresence : MonoBehaviour
    {
        /// <summary>The server-side room name. Contract: server `docs/protocol.md#exhibition-museum-scene`.</summary>
        public const string RoomName = "museum";

        /// <summary>Position reports per second. Five is enough for a walking pace at the room's 10 Hz patch rate.</summary>
        private const float SendHz = 5f;

        /// <summary>A move smaller than this (metres / degrees) is not worth a message.</summary>
        private const float MinMove = 0.02f;
        private const float MinTurn = 1f;

        /// <summary>Seconds between attempts to get back in after the room went away.</summary>
        private const float RejoinDelay = 5f;
        private const int MaxRejoins = 3;

        [Tooltip("The local player's rig. Its position and yaw are what the others see. Found by the Player tag if empty.")]
        [SerializeField] private Transform player;

        [Tooltip("Font for the names over visitors. Leave empty for the TMP default.")]
        [SerializeField] private TMP_FontAsset nameFont;

        private Room<MuseumState> _room;
        private readonly Dictionary<string, MuseumVisitorAvatar> _avatars = new Dictionary<string, MuseumVisitorAvatar>();
        private readonly List<string> _gone = new List<string>();

        private Transform _avatarsRoot;
        private Mesh _bodyMesh;
        private Material _bodyMaterial;

        private float _nextSendAt;
        private Vector3 _lastSentPosition;
        private float _lastSentYaw;
        private bool _sentOnce;

        private int _rejoins;
        private bool _quitting;

        /// <summary>True while the presence room is open.</summary>
        public bool Connected => _room != null && _room.Connection != null && _room.Connection.IsOpen;

        /// <summary>How many other visitors are currently drawn.</summary>
        public int VisibleVisitors => _avatars.Count;

        private void Awake()
        {
            if (player == null)
            {
                GameObject tagged = GameObject.FindWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            if (player == null)
            {
                Debug.LogWarning("[MuseumPresence] no Player in the scene — nothing to report.", this);
                enabled = false;
                return;
            }

            // The avatars wear the local player's own body, so a visitor is recognisably "another
            // one of me" — and there is no prefab to lose.
            var filter = player.GetComponent<MeshFilter>();
            var renderer = player.GetComponent<MeshRenderer>();
            _bodyMesh = filter != null ? filter.sharedMesh : null;
            _bodyMaterial = renderer != null ? renderer.sharedMaterial : null;

            _avatarsRoot = new GameObject("Visitors (Presence)").transform;
        }

        private async void Start()
        {
            if (ColyseusNetManager.Instance == null)
            {
                Debug.Log("[MuseumPresence] no ColyseusNetManager — walking the museum alone.", this);
                return;
            }

            await Join();
        }

        private async Task Join()
        {
            try
            {
                var options = new Dictionary<string, object>
                {
                    ["displayName"] = ColyseusNetManager.Instance.PlayerName,
                };

                Room<MuseumState> room = await ColyseusNetManager.Instance.Client.JoinOrCreate<MuseumState>(RoomName, options);

                // The scene may have gone while the join was in flight.
                if (this == null || _quitting)
                {
                    _ = room.Leave(true);
                    return;
                }

                _room = room;
                _rejoins = 0;
                _sentOnce = false;
                _nextSendAt = 0f;

                room.OnStateChange += OnStateChange;
                room.OnLeave += OnRoomLeft;
                room.OnError += (code, message) =>
                    Debug.LogWarning($"[MuseumPresence] room error {code}: {message}", this);

                if (room.State != null) Reconcile(room.State);
            }
            catch (Exception e)
            {
                // The museum is playable alone; this only loses the company.
                Debug.LogWarning($"[MuseumPresence] could not join the presence room: {e.Message}", this);
            }
        }

        private void Update()
        {
            if (!Connected || player == null) return;
            if (Time.unscaledTime < _nextSendAt) return;

            Vector3 position = player.position;
            float yaw = player.eulerAngles.y;

            bool moved = !_sentOnce
                         || (position - _lastSentPosition).sqrMagnitude > MinMove * MinMove
                         || Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw)) > MinTurn;
            if (!moved) return;

            _room.Send("move", new MovePayload { x = position.x, y = position.y, z = position.z, yaw = yaw });

            _lastSentPosition = position;
            _lastSentYaw = yaw;
            _sentOnce = true;
            _nextSendAt = Time.unscaledTime + 1f / SendHz;
        }

        private void OnStateChange(MuseumState state, bool isFirstState) => Reconcile(state);

        /// <summary>
        /// Makes the drawn avatars match the room's visitor list: one per session that is not
        /// ours, positioned where the server last heard they were.
        /// </summary>
        private void Reconcile(MuseumState state)
        {
            if (state?.visitors == null || _avatarsRoot == null) return;

            string me = _room != null ? _room.SessionId : string.Empty;

            _gone.Clear();
            _gone.AddRange(_avatars.Keys);

            state.visitors.ForEach((sessionId, visitor) =>
            {
                if (sessionId == me || visitor == null) return;

                _gone.Remove(sessionId);

                if (!_avatars.TryGetValue(sessionId, out MuseumVisitorAvatar avatar))
                {
                    avatar = MuseumVisitorAvatar.Create(_avatarsRoot, sessionId, _bodyMesh, _bodyMaterial,
                                                        TintFor(sessionId), nameFont);
                    _avatars[sessionId] = avatar;
                }

                avatar.SetName(visitor.displayName);
                avatar.SetTarget(new Vector3(visitor.x, visitor.y, visitor.z), visitor.yaw);
            });

            foreach (string sessionId in _gone) RemoveAvatar(sessionId);
        }

        private void RemoveAvatar(string sessionId)
        {
            if (_avatars.TryGetValue(sessionId, out MuseumVisitorAvatar avatar) && avatar != null)
            {
                Destroy(avatar.gameObject);
            }

            _avatars.Remove(sessionId);
        }

        private void ClearAvatars()
        {
            foreach (MuseumVisitorAvatar avatar in _avatars.Values)
            {
                if (avatar != null) Destroy(avatar.gameObject);
            }

            _avatars.Clear();
        }

        /// <summary>
        /// The socket closed under us — the server restarted, or the connection dropped. The
        /// avatars are stale the moment that happens, so they go, and a few rejoins are tried
        /// before the museum settles for being single-player.
        /// </summary>
        private async void OnRoomLeft(int code)
        {
            _room = null;
            if (this == null || _quitting) return;

            ClearAvatars();

            if (_rejoins >= MaxRejoins)
            {
                Debug.LogWarning($"[MuseumPresence] presence room closed ({code}); giving up after {MaxRejoins} rejoins.", this);
                return;
            }

            _rejoins++;
            await Task.Delay(TimeSpan.FromSeconds(RejoinDelay));
            if (this == null || _quitting || ColyseusNetManager.Instance == null) return;

            await Join();
        }

        private void OnApplicationQuit() => _quitting = true;

        private void OnDestroy()
        {
            _quitting = true;

            Room<MuseumState> room = _room;
            _room = null;

            if (room != null)
            {
                room.OnStateChange -= OnStateChange;
                room.OnLeave -= OnRoomLeft;
                // Straight on the room, not ColyseusNetManager.Leave: that would also clear the
                // seat SessionData holds, and this was never that seat.
                _ = room.Leave(true);
            }

            ClearAvatars();
            if (_avatarsRoot != null) Destroy(_avatarsRoot.gameObject);
        }

        /// <summary>A stable, saturated colour per session so two visitors standing together can be told apart.</summary>
        private static Color TintFor(string sessionId)
        {
            uint hash = 2166136261;
            foreach (char c in sessionId ?? string.Empty) hash = (hash ^ c) * 16777619;

            float hue = (hash % 360u) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.9f);
        }

        [Serializable]
        private sealed class MovePayload
        {
            public float x;
            public float y;
            public float z;
            public float yaw;
        }
    }
}
