using System.Collections.Generic;
using Museum.Core;
using Museum.Net.State;
using TMPro;
using UnityEngine;

namespace Museum.Net
{
    /// <summary>
    /// Keeps the museum's visitors in sight and earshot of each other. Reports where the local
    /// player is a few times a second, draws a <see cref="MuseumVisitorAvatar"/> for everyone else
    /// the `museum` room lists, and carries the shared exhibits (<see cref="MuseumInteractions"/>)
    /// both ways.
    /// </summary>
    /// <remarks>
    /// This is the Museum scene's view of the room; the room itself is held by
    /// <see cref="MuseumPresenceLink"/>, which outlives the scene. Going through a doorway
    /// therefore does not leave: the visitor stops reporting, is marked away with the doorway's
    /// game, and the others keep seeing them standing there. Coming back, the link is still open,
    /// the tag is cleared and reporting resumes from where the player reappears — the same spot
    /// (see <c>FPSController</c>), so nobody sees a jump.
    ///
    /// With no <see cref="ColyseusNetManager"/> in the scene (opened directly in the editor) or
    /// no server reachable, the museum is simply single-player — which is how it always was.
    /// </remarks>
    public sealed class MuseumPresence : MonoBehaviour
    {
        /// <summary>The server-side room name. Contract: server `docs/protocol.md#exhibition-museum-scene`.</summary>
        public const string RoomName = MuseumPresenceLink.RoomName;

        /// <summary>
        /// Position reports per second. Others draw this avatar
        /// <see cref="MuseumVisitorAvatar.InterpolationDelay"/> (two reports) behind, so the two
        /// numbers move together; the room forwards them on a 20 Hz patch.
        /// </summary>
        private const float SendHz = 10f;

        /// <summary>A move smaller than this (metres / degrees) is not worth a message.</summary>
        private const float MinMove = 0.02f;
        private const float MinTurn = 1f;

        [Tooltip("The local player's rig. Its position and yaw are what the others see. Found by the Player tag if empty.")]
        [SerializeField] private Transform player;

        [Tooltip("Font for the names over visitors. Leave empty for the TMP default.")]
        [SerializeField] private TMP_FontAsset nameFont;

        [Tooltip("The bodies other visitors wear — the ASSET_NUSANTARA characters, Assets/Prefabs/Visitors/*.prefab. " +
                 "Each visitor gets one, chosen from their session id so every client agrees. Empty falls back to the player's capsule.")]
        [SerializeField] private GameObject[] visitorCharacters;

        private MuseumPresenceLink _link;
        private readonly Dictionary<string, MuseumVisitorAvatar> _avatars = new Dictionary<string, MuseumVisitorAvatar>();
        private readonly List<string> _gone = new List<string>();

        private Transform _avatarsRoot;
        private Mesh _bodyMesh;
        private Material _bodyMaterial;

        private float _nextSendAt;
        private Vector3 _lastSentPosition;
        private float _lastSentYaw;
        private bool _sentOnce;

        /// <summary>True while the presence room is open.</summary>
        public bool Connected => _link != null && _link.Connected;

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

            // The capsule is only the fallback for a scene whose `visitorCharacters` were lost —
            // the museum has had its inspector values wiped once, and a missing prefab should
            // cost the look, not the presence.
            if (visitorCharacters == null || visitorCharacters.Length == 0)
            {
                Debug.LogWarning("[MuseumPresence] no visitorCharacters — visitors will be drawn as capsules. " +
                                 "Run Museum/Rebuild UI/Wire Scene References.", this);
            }

            var filter = player.GetComponent<MeshFilter>();
            var renderer = player.GetComponent<MeshRenderer>();
            _bodyMesh = filter != null ? filter.sharedMesh : null;
            _bodyMaterial = renderer != null ? renderer.sharedMaterial : null;

            _avatarsRoot = new GameObject("Visitors (Presence)").transform;
        }

        private async void Start()
        {
            if (!enabled) return;

            // FPSController has already taken a pending return in its Awake; one left over (a rig
            // without it) must not keep this visitor marked away for the rest of the visit.
            if (SessionData.Instance != null) SessionData.Instance.ForgetMuseumReturn();

            if (ColyseusNetManager.Instance == null)
            {
                Debug.Log("[MuseumPresence] no ColyseusNetManager — walking the museum alone.", this);
                return;
            }

            _link = MuseumPresenceLink.Ensure();
            _link.StateChanged += Reconcile;
            _link.Interacted += OnRemoteInteracted;
            _link.Lost += ClearAvatars;
            MuseumInteractions.LocalInteracted += OnLocalInteracted;

            // Back from a game: in the hall again, and the first report goes out at once.
            _link.SetActivity(MuseumActivities.InHall);
            _sentOnce = false;
            _nextSendAt = 0f;

            if (_link.Connected && _link.Room.State != null) Reconcile(_link.Room.State);

            await _link.Connect();
        }

        private void Update()
        {
            if (_link == null || player == null) return;

            // Through a doorway: stop reporting and stand here, tagged, until the game is over.
            string away = SessionData.Instance != null ? SessionData.Instance.MuseumReturnActivity : string.Empty;
            if (away.Length > 0)
            {
                _link.SetActivity(away);
                return;
            }

            if (!_link.Connected) return;
            if (Time.unscaledTime < _nextSendAt) return;

            Vector3 position = player.position;
            float yaw = player.eulerAngles.y;

            bool moved = !_sentOnce
                         || (position - _lastSentPosition).sqrMagnitude > MinMove * MinMove
                         || Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw)) > MinTurn;
            if (!moved) return;

            _link.SendMove(position, yaw);

            // A rejoin that happened before the first report could not send the tag's removal.
            if (!_sentOnce) _link.SetActivity(MuseumActivities.InHall);

            _lastSentPosition = position;
            _lastSentYaw = yaw;
            _sentOnce = true;
            _nextSendAt = Time.unscaledTime + 1f / SendHz;
        }

        private void OnLocalInteracted(string station, int index)
        {
            if (_link != null) _link.SendInteract(station, index);
        }

        private void OnRemoteInteracted(string station, int index) => MuseumInteractions.PlayRemote(station, index);

        /// <summary>
        /// Makes the drawn avatars match the room's visitor list: one per session that is not
        /// ours, positioned where the server last heard they were, tagged if they are away.
        /// </summary>
        private void Reconcile(MuseumState state)
        {
            if (state?.visitors == null || _avatarsRoot == null || _link == null) return;

            string me = _link.SessionId;

            _gone.Clear();
            _gone.AddRange(_avatars.Keys);

            state.visitors.ForEach((sessionId, visitor) =>
            {
                if (sessionId == me || visitor == null) return;

                _gone.Remove(sessionId);

                if (!_avatars.TryGetValue(sessionId, out MuseumVisitorAvatar avatar))
                {
                    avatar = MuseumVisitorAvatar.Create(_avatarsRoot, sessionId, CharacterFor(visitor.avatar),
                                                        _bodyMesh, _bodyMaterial, TintFor(sessionId), nameFont);
                    _avatars[sessionId] = avatar;
                }

                avatar.SetName(visitor.displayName);
                avatar.SetActivity(visitor.activity);
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

        private void OnDestroy()
        {
            MuseumInteractions.LocalInteracted -= OnLocalInteracted;

            if (_link != null)
            {
                _link.StateChanged -= Reconcile;
                _link.Interacted -= OnRemoteInteracted;
                _link.Lost -= ClearAvatars;

                // The scene is going. Through a doorway, the room stays open and the visitor is
                // marked away (Update usually did it already); to MainMenu, the link leaves itself.
                string away = SessionData.Instance != null ? SessionData.Instance.MuseumReturnActivity : string.Empty;
                if (away.Length > 0) _link.SetActivity(away);
            }

            ClearAvatars();
            if (_avatarsRoot != null) Destroy(_avatarsRoot.gameObject);
        }

        /// <summary>A stable, saturated colour per session so two visitors standing together can be told apart.</summary>
        private static Color TintFor(string sessionId)
        {
            float hue = (StableHash(sessionId) % 360u) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.9f);
        }

        /// <summary>
        /// The character a visitor chose on the welcome screen, as the room synced it
        /// (<c>MuseumVisitor.avatar</c>, already sanitised server-side). The prefab is matched by
        /// name — <c>"Visitor Jawa L"</c> is <see cref="PlayerAvatars.Jawa"/>. An id with no
        /// prefab falls back to the default character, then to any wired one; null only when
        /// none are wired, and the avatar then falls back to a capsule.
        /// </summary>
        private GameObject CharacterFor(string avatarId)
        {
            if (visitorCharacters == null || visitorCharacters.Length == 0) return null;

            string wanted = PlayerAvatars.Sanitize(avatarId);
            GameObject fallback = null;

            foreach (GameObject character in visitorCharacters)
            {
                if (character == null) continue;

                string id = PlayerAvatars.IdInName(character.name);
                if (id == wanted) return character;
                if (fallback == null || id == PlayerAvatars.Default) fallback = character;
            }

            return fallback;
        }

        /// <summary>FNV-1a over the id: stable across clients and runs, unlike <c>string.GetHashCode</c>.</summary>
        private static uint StableHash(string value)
        {
            uint hash = 2166136261;
            foreach (char c in value ?? string.Empty) hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
}
