using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Another visitor walking the museum: one of the ASSET_NUSANTARA characters, with a name
    /// floating over it. Pure view — it is told where its visitor is and follows; it never
    /// decides anything.
    /// </summary>
    /// <remarks>
    /// The body is one of <c>Assets/Prefabs/Visitors/*.prefab</c> — variants of the four
    /// <c>ASSET_NUSANTARA/1_Karakter</c> models (Jawa, Bali, Bugis, Minang), each a Humanoid
    /// avatar of its own, scaled from 1.2 m to <see cref="CharacterHeight"/>. All four run
    /// <c>Anim_MuseumVisitor</c>: one 1D blend tree on <c>Speed</c> — <c>Visitor Idle Stand</c>
    /// at 0, <c>Visitor Walk</c> at <see cref="WalkClipSpeed"/>, <c>Visitor Run</c> at
    /// <see cref="RunClipSpeed"/> — retargeted per body by the Humanoid rig. All three are derived
    /// from the <c>2_Animasi</c> clips: the source idle stands in the egrang pose, and the source
    /// walk/run hold the arms out, which tore the A-posed Minang mesh (see networking.md). The
    /// clips are in-place (root motion baked into the pose); movement stays code-driven.
    ///
    /// The prefab is handed in by whoever spawns the avatar. With none (a scene whose reference
    /// was lost), the avatar falls back to the local player's capsule, tinted per visitor — the
    /// presence keeps working, it only looks plainer.
    ///
    /// <b>Smoothing is snapshot interpolation.</b> Positions arrive in bursts — ten reports a
    /// second from the visitor, forwarded on the room's 20 Hz patch — and chasing the newest one
    /// makes an avatar lurch and stop between packets. Instead every position is stamped with
    /// its arrival time and the avatar is drawn <see cref="InterpolationDelay"/> in the past,
    /// between the two positions either side of that moment. That delay is the price: others are
    /// seen where they were a fifth of a second ago, which nobody walking a museum can notice. A
    /// position further than <see cref="SnapDistance"/> from the last is a floor change or a
    /// first sighting and is jumped to.
    ///
    /// <b>The body faces its direction of travel</b>, not the yaw the visitor sends. That yaw is
    /// their first-person camera, and a body that followed it would spin whenever they looked
    /// around. It only orients a first sighting; from then on the avatar turns toward where it
    /// is moving and keeps that heading when it stops.
    /// </remarks>
    public sealed class MuseumVisitorAvatar : MonoBehaviour
    {
        /// <summary>A jump longer than this is not a stride — do not slide across the building for it.</summary>
        public const float SnapDistance = 6f;

        /// <summary>Crown height of every visitor, set by the prefabs' scale (1.417 × the models' 1.2 m).</summary>
        public const float CharacterHeight = 1.7f;

        /// <summary>
        /// Ground speed (m/s) the <c>Walk</c> and <c>Run</c> clips imply at visitor scale, measured
        /// from foot travel. They are the blend tree's thresholds, so up to a run the feet plant.
        /// </summary>
        public const float WalkClipSpeed = 0.80f;
        public const float RunClipSpeed = 1.50f;

        /// <summary>
        /// How far in the past avatars are drawn. Two send intervals (the visitor reports every
        /// 0.1 s), so one late or lost packet still leaves a position to move toward.
        /// </summary>
        public const float InterpolationDelay = 0.2f;

        /// <summary>The visitor's report interval — used to ease into motion after standing still.</summary>
        private const float ExpectedInterval = 0.1f;

        /// <summary>Positions older than this behind the render time are dropped.</summary>
        private const float BufferSeconds = 1f;

        /// <summary>
        /// Positions are the local player's capsule centre, and the capsule is 2 m tall, so the
        /// floor is 1 m below. Every locomotion clip keeps the soles on the model's origin —
        /// <c>Visitor Idle Stand</c> by its root height, <c>Visitor Walk</c>/<c>Run</c> by
        /// "Based Upon: Feet" — so the model's origin goes on the floor.
        /// </summary>
        private const float CharacterDrop = 1f;

        /// <summary>The nameplate sits this far above the avatar's origin (the capsule's centre).</summary>
        private const float CapsuleNameHeight = 1.45f;
        private const float CharacterNameHeight = CharacterHeight - CharacterDrop + 0.1f;

        /// <summary>
        /// Past <see cref="RunClipSpeed"/> the blend has nothing faster, so <c>Run</c> is played
        /// faster instead, in proportion. Players walk at 5 m/s and sprint at 8 — far past a
        /// chibi's natural stride — so the rate is capped where the legs still read as running
        /// and the rest is accepted as foot slide.
        /// </summary>
        private const float MaxPlaybackRate = 3.5f;

        /// <summary>How quickly the measured speed follows the real one — steadies the idle/walk switch.</summary>
        private const float SpeedSmoothing = 8f;

        /// <summary>
        /// Below this speed (m/s) the avatar keeps the facing it has: a visitor coming to a stop
        /// drifts a few millimetres a frame, and turning to face each drift reads as a twitch.
        /// </summary>
        private const float FacingMinSpeed = 0.3f;

        /// <summary>Seconds the body takes to swing round to a new heading — quick, but never a snap.</summary>
        private const float FacingSmoothTime = 0.12f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int WalkRateParam = Animator.StringToHash("WalkRate");

        private struct Snapshot
        {
            public float Time;
            public Vector3 Position;
            public float Yaw;
        }

        /// <summary>Received positions, oldest first.</summary>
        private readonly List<Snapshot> _snapshots = new List<Snapshot>();

        private Animator _animator;
        private float _speed;

        private float _facingYaw;
        private float _facingTarget;
        private float _facingVelocity;

        private TMP_Text _label;
        private Transform _plate;
        private Transform _camera;

        /// <summary>Session id of the visitor this avatar stands for.</summary>
        public string SessionId { get; private set; } = string.Empty;

        /// <summary>The name over the avatar's head.</summary>
        public string DisplayName => _label != null ? _label.text : string.Empty;

        /// <summary>The newest position received for this visitor.</summary>
        public Vector3 TargetPosition => _snapshots.Count > 0 ? _snapshots[_snapshots.Count - 1].Position : transform.position;

        /// <summary>
        /// Spawns an avatar wearing <paramref name="character"/>. When that is null it wears
        /// <paramref name="fallbackBody"/> instead — the local player's capsule — tinted with
        /// <paramref name="tint"/> so capsules can be told apart.
        /// </summary>
        public static MuseumVisitorAvatar Create(Transform parent, string sessionId, GameObject character,
                                                 Mesh fallbackBody, Material fallbackMaterial, Color tint,
                                                 TMP_FontAsset nameFont)
        {
            var go = new GameObject($"Visitor {sessionId}");
            go.transform.SetParent(parent, false);

            var avatar = go.AddComponent<MuseumVisitorAvatar>();
            avatar.SessionId = sessionId ?? string.Empty;

            if (character != null)
            {
                avatar.BuildCharacter(character);
                avatar.BuildNameplate(nameFont, CharacterNameHeight);
            }
            else
            {
                avatar.BuildCapsule(fallbackBody, fallbackMaterial, tint);
                avatar.BuildNameplate(nameFont, CapsuleNameHeight);
            }

            return avatar;
        }

        /// <summary>Names the visitor. An empty name hides the plate rather than showing a blank card.</summary>
        public void SetName(string value)
        {
            if (_label == null) return;

            _label.text = value ?? string.Empty;
            _plate.gameObject.SetActive(_label.text.Length > 0);
        }

        /// <summary>
        /// Where the visitor is now, in world units, as just received. It is not shown yet: it
        /// joins the buffer the avatar is drawn from, <see cref="InterpolationDelay"/> behind.
        /// <paramref name="yaw"/> — the visitor's camera heading — only orients a first sighting
        /// or a teleport; after that the body faces where it walks.
        /// </summary>
        public void SetTarget(Vector3 position, float yaw)
        {
            float now = Time.unscaledTime;
            var snapshot = new Snapshot { Time = now, Position = position, Yaw = yaw };

            if (_snapshots.Count == 0)
            {
                Teleport(snapshot);
                return;
            }

            Snapshot last = _snapshots[_snapshots.Count - 1];

            if ((position - last.Position).sqrMagnitude > SnapDistance * SnapDistance)
            {
                Teleport(snapshot);
                return;
            }

            // The room re-sends every visitor whenever any one of them changes, and a visitor
            // turning their camera in place re-sends too, so an unchanged position is common. It
            // carries no motion; keeping the older stamp is what lets the next real move ease in
            // from here rather than from the latest re-send.
            if (position == last.Position) return;

            // Standing still sends nothing, so the first move after a pause would otherwise
            // interpolate across the whole silence — i.e. all but jump. Restamp the resting pose
            // one interval ago so the first stride takes as long as every other.
            if (now - last.Time > ExpectedInterval * 1.5f)
            {
                last.Time = now - ExpectedInterval;
                _snapshots.Add(last);
            }

            // Two arrivals in the same frame: the newer wins.
            if (Mathf.Approximately(_snapshots[_snapshots.Count - 1].Time, now))
            {
                _snapshots[_snapshots.Count - 1] = snapshot;
            }
            else
            {
                _snapshots.Add(snapshot);
            }
        }

        private void Teleport(Snapshot snapshot)
        {
            _snapshots.Clear();
            _snapshots.Add(snapshot);
            transform.SetPositionAndRotation(snapshot.Position, Quaternion.Euler(0f, snapshot.Yaw, 0f));
            _speed = 0f;
            _facingYaw = _facingTarget = snapshot.Yaw;
            _facingVelocity = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float moved = 0f;

            if (_snapshots.Count > 0)
            {
                Vector3 before = transform.position;

                Sample(Time.unscaledTime - InterpolationDelay, out Vector3 position, out _);

                // Horizontal only: climbing the stairs is still walking, and a snap (handled in
                // SetTarget) never reaches this line.
                Vector3 step = position - before;
                step.y = 0f;
                moved = step.magnitude;

                // Face the way the body is going — strafing left turns it left, backing up turns
                // it round — never the way the visitor's camera points. Standing still keeps the
                // last heading, so looking around in place does not spin the body for others.
                if (dt > 0f && moved / dt > FacingMinSpeed)
                {
                    _facingTarget = Quaternion.LookRotation(step).eulerAngles.y;
                }

                _facingYaw = Mathf.SmoothDampAngle(_facingYaw, _facingTarget, ref _facingVelocity, FacingSmoothTime);
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _facingYaw, 0f));
            }

            Animate(moved, dt);
            FacePlateToCamera();
        }

        /// <summary>
        /// The pose at <paramref name="renderTime"/>: interpolated between the two snapshots
        /// either side of it, or held at the newest when the buffer has run dry (the visitor
        /// stopped, or a packet is late) — holding is never wrong for long, extrapolating is.
        /// </summary>
        private void Sample(float renderTime, out Vector3 position, out float yaw)
        {
            // Drop what the render time has passed, keeping one snapshot behind it to lerp from.
            while (_snapshots.Count > 2 && _snapshots[1].Time <= renderTime) _snapshots.RemoveAt(0);
            while (_snapshots.Count > 1 && _snapshots[0].Time < renderTime - BufferSeconds) _snapshots.RemoveAt(0);

            Snapshot from = _snapshots[0];

            if (_snapshots.Count == 1 || renderTime <= from.Time)
            {
                position = from.Position;
                yaw = from.Yaw;
                return;
            }

            Snapshot to = _snapshots[1];

            if (renderTime >= to.Time)
            {
                position = to.Position;
                yaw = to.Yaw;
                return;
            }

            float t = Mathf.InverseLerp(from.Time, to.Time, renderTime);
            position = Vector3.Lerp(from.Position, to.Position, t);
            yaw = Mathf.LerpAngle(from.Yaw, to.Yaw, t);
        }

        private void Animate(float moved, float dt)
        {
            if (_animator == null || dt <= 0f) return;

            _speed = Mathf.Lerp(_speed, moved / dt, 1f - Mathf.Exp(-SpeedSmoothing * dt));

            _animator.SetFloat(SpeedParam, _speed);
            _animator.SetFloat(WalkRateParam, Mathf.Clamp(_speed / RunClipSpeed, 1f, MaxPlaybackRate));
        }

        private void BuildCharacter(GameObject character)
        {
            // No collider on the prefab: the visitor's own client already keeps them out of
            // walls, and a collider here would let one avatar shove another's rigidbody around.
            GameObject body = Instantiate(character, transform, false);
            body.name = "Body";
            body.transform.localPosition = new Vector3(0f, -CharacterDrop, 0f);
            body.transform.localRotation = Quaternion.identity;

            _animator = body.GetComponent<Animator>();
            if (_animator != null) _animator.applyRootMotion = false;
        }

        private void BuildCapsule(Mesh body, Material material, Color tint)
        {
            var capsule = new GameObject("Body");
            capsule.transform.SetParent(transform, false);

            var filter = capsule.AddComponent<MeshFilter>();
            filter.sharedMesh = body;

            var renderer = capsule.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // A property block rather than `.material`, so thirty visitors share one material
            // instead of cloning it thirty times. URP Lit reads `_BaseColor`; the second name is
            // for a material that happens to be on the older shader.
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(block);
        }

        private void BuildNameplate(TMP_FontAsset font, float height)
        {
            var go = new GameObject("Name", typeof(RectTransform));
            _plate = go.transform;
            _plate.SetParent(transform, false);
            _plate.localPosition = new Vector3(0f, height, 0f);
            // The same 0.1 scale every world-space label in the museum uses (lobby hints, the
            // coming-soon notice), so the font sizes read in the same units.
            _plate.localScale = Vector3.one * 0.1f;

            var rect = (RectTransform)_plate;
            rect.sizeDelta = new Vector2(24f, 4f);

            var text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.fontSize = 8f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.957f, 0.918f, 0.835f, 1f);
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            _label = text;

            SetName(string.Empty);
        }

        private void FacePlateToCamera()
        {
            if (_plate == null) return;

            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
            if (_camera == null) return;

            // Facing away from the camera, because 3D text draws on its local +Z: LookAt(camera)
            // would show the visitor's name mirrored.
            Vector3 away = _plate.position - _camera.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) _plate.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
