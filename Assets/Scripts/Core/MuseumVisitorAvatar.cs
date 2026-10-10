using TMPro;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Another visitor walking the museum: a capsule in the same shape as the local player's rig,
    /// with a name floating over it. Pure view — it is told where its visitor is and glides
    /// there; it never decides anything.
    /// </summary>
    /// <remarks>
    /// Built in code rather than authored as a prefab, for the same reason
    /// <see cref="MinimapOnlyObject"/> builds its marker: this project's inspector values have
    /// been lost once already, and a view with nothing to wire cannot be unwired. The capsule is
    /// the local player's own mesh and material, handed in by whoever spawns it, so a visitor
    /// looks like "you" seen from outside rather than like a second kind of thing.
    ///
    /// Positions arrive ten times a second (the museum room's patch rate), so the avatar moves
    /// toward its last-known target over the interval rather than jumping there; a target
    /// further than <see cref="SnapDistance"/> away is a floor change or a first sighting and is
    /// snapped to.
    /// </remarks>
    public sealed class MuseumVisitorAvatar : MonoBehaviour
    {
        /// <summary>A jump longer than this is not a stride — do not slide across the building for it.</summary>
        public const float SnapDistance = 6f;

        /// <summary>Metres per second the avatar may glide; a little over the player's sprint speed so it never falls behind.</summary>
        private const float GlideSpeed = 9f;

        /// <summary>Degrees per second the body may turn.</summary>
        private const float TurnSpeed = 540f;

        /// <summary>The nameplate sits this far above the capsule's centre (the capsule is 2 m tall).</summary>
        private const float NameHeight = 1.45f;

        private Vector3 _targetPosition;
        private float _targetYaw;
        private bool _hasTarget;

        private TMP_Text _label;
        private Transform _plate;
        private Transform _camera;

        /// <summary>Session id of the visitor this avatar stands for.</summary>
        public string SessionId { get; private set; } = string.Empty;

        /// <summary>The name over the avatar's head.</summary>
        public string DisplayName => _label != null ? _label.text : string.Empty;

        /// <summary>Where the avatar was last told its visitor is.</summary>
        public Vector3 TargetPosition => _targetPosition;

        /// <summary>
        /// Spawns an avatar. <paramref name="body"/> is copied from the local player's renderer
        /// so the two match; <paramref name="tint"/> distinguishes visitors from each other.
        /// </summary>
        public static MuseumVisitorAvatar Create(Transform parent, string sessionId, Mesh body, Material material,
                                                 Color tint, TMP_FontAsset nameFont)
        {
            var go = new GameObject($"Visitor {sessionId}");
            go.transform.SetParent(parent, false);

            var avatar = go.AddComponent<MuseumVisitorAvatar>();
            avatar.SessionId = sessionId ?? string.Empty;
            avatar.BuildBody(body, material, tint);
            avatar.BuildNameplate(nameFont);
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
        /// Where the visitor is now, in world units, and which way their body faces. The first
        /// target is taken as-is; later ones are glided to unless they are too far to be a step.
        /// </summary>
        public void SetTarget(Vector3 position, float yaw)
        {
            bool snap = !_hasTarget || (position - transform.position).sqrMagnitude > SnapDistance * SnapDistance;

            _targetPosition = position;
            _targetYaw = yaw;
            _hasTarget = true;

            if (snap)
            {
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            }
        }

        private void LateUpdate()
        {
            if (_hasTarget)
            {
                float dt = Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, GlideSpeed * dt);

                float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, _targetYaw, TurnSpeed * dt);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            FacePlateToCamera();
        }

        private void BuildBody(Mesh body, Material material, Color tint)
        {
            var capsule = new GameObject("Body");
            capsule.transform.SetParent(transform, false);

            // No collider: the visitor's own client already keeps them out of walls, and a
            // collider here would let one avatar shove another's rigidbody around.
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

        private void BuildNameplate(TMP_FontAsset font)
        {
            var go = new GameObject("Name", typeof(RectTransform));
            _plate = go.transform;
            _plate.SetParent(transform, false);
            _plate.localPosition = new Vector3(0f, NameHeight, 0f);
            // The same 0.1 scale every world-space label in the museum uses (lobby hints, the
            // coming-soon notice), so the font sizes read in the same units.
            _plate.localScale = Vector3.one * 0.1f;

            var rect = (RectTransform)_plate;
            rect.sizeDelta = new Vector2(24f, 4f);

            var text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.fontSize = 6f;
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
