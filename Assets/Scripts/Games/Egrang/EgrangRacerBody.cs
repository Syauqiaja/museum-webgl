using Museum.Core;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Dresses a lane's walker in the character its player chose on the welcome screen. Sits on
    /// <c>Egrang Player</c>, beside the Animator <see cref="EgrangStepMover"/> triggers.
    /// </summary>
    /// <remarks>
    /// The authored walker is the Jawa kid — <c>Armature</c> + <c>char1</c> on a Generic rig,
    /// played by <c>Anim_Egrang</c>'s own egrang clips. <b>That rig keeps playing whatever the
    /// walker wears</b>: nothing here touches the Animator, its controller or its clips, so every
    /// lane runs exactly the egrang step, half step and fall it always did.
    ///
    /// Jawa is the authored body, so wearing Jawa changes nothing. Any other character is placed
    /// beside the authored skeleton as its own un-animated body, the authored mesh is hidden, and
    /// every frame the authored skeleton's pose is copied onto it through two
    /// <see cref="HumanPoseHandler"/>s (Jawa's avatar → the chosen model's). Humanoid muscle space
    /// carries the pose across different proportions — the Minang character's shorter legs bend
    /// the same way Jawa's do — and the body is then lifted or lowered so its soles land where
    /// Jawa's are, which is where the stilts' footplates are. Both stilts are re-pointed at the
    /// new body's hands and feet (<see cref="EgrangStick.Rebind"/>).
    ///
    /// Why not retarget the clips instead: the egrang clips are Generic and key every bone's
    /// position and scale, so played on another body they stretch it to Jawa's proportions; and a
    /// Humanoid bake of them, played on a Humanoid Animator, loses the height that puts the walker
    /// up on the stilts (Unity takes a generated clip's own body curve as its "original" root).
    /// Copying the pose leaves the authored animation as the single source of motion.
    ///
    /// Runs before the stilts (<see cref="EgrangStick"/> is order 100) so they read this frame's pose.
    /// </remarks>
    [DefaultExecutionOrder(50)]
    public sealed class EgrangRacerBody : MonoBehaviour
    {
        private const string ArmatureName = "Armature";
        private const string MeshName = "char1";

        [Tooltip("This walker's two stilts. Found among this object's children if empty.")]
        [SerializeField] private EgrangStick[] sticks = new EgrangStick[0];

        [Tooltip("The ASSET_NUSANTARA character models (Char_*.fbx, Humanoid), matched to an avatar id by " +
                 "name. Jawa's is required: its avatar is how the authored Jawa skeleton's pose is read.")]
        [SerializeField] private GameObject[] characters = new GameObject[0];

        private bool _initialised;
        private string _wearing = PlayerAvatars.Default;

        private Renderer _authoredMesh;
        private Transform[] _authoredHands = new Transform[0];
        private Transform[] _authoredFeet = new Transform[0];
        private Transform _authoredLeftFoot;
        private Transform _authoredRightFoot;

        private GameObject _body;
        private Transform _bodyLeftFoot;
        private Transform _bodyRightFoot;
        private HumanPoseHandler _source;
        private HumanPoseHandler _target;
        private HumanPose _pose;

        /// <summary>The avatar id the walker currently wears.</summary>
        public string Wearing => _wearing;

        /// <summary>The worn body's root, or null while wearing the authored Jawa.</summary>
        public Transform WornBody => _body != null ? _body.transform : null;

        /// <summary>Wires the component from script, as the wiring repair does in the scene.</summary>
        public void Configure(EgrangStick[] walkerSticks, GameObject[] characterModels)
        {
            sticks = walkerSticks ?? new EgrangStick[0];
            characters = characterModels ?? new GameObject[0];
            _initialised = false;
        }

        private void Awake() => Initialise();

        /// <summary>
        /// Puts the walker in <paramref name="avatarId"/>'s body. Unknown ids wear Jawa; a character
        /// with no model wired (or no Jawa model to read the pose through) is refused with a warning
        /// and the walker keeps what it has. Wearing what is already worn does nothing.
        /// </summary>
        public void Wear(string avatarId)
        {
            Initialise();

            string id = PlayerAvatars.Sanitize(avatarId);
            if (id == _wearing) return;

            GameObject model = null;
            Avatar sourceAvatar = null;
            if (id != PlayerAvatars.Default)
            {
                model = ModelFor(id);
                sourceAvatar = AvatarOf(ModelFor(PlayerAvatars.Default));
                if (model == null || AvatarOf(model) == null || sourceAvatar == null)
                {
                    Debug.LogWarning($"[EgrangRacerBody] '{name}' cannot wear '{id}': " +
                                     (model == null ? "no model for it is wired." : "a Humanoid avatar is missing."), this);
                    return;
                }
            }

            TakeOff();
            if (model != null) PutOn(model, sourceAvatar);
            _wearing = id;
        }

        private void Initialise()
        {
            if (_initialised) return;
            _initialised = true;

            if (sticks == null || sticks.Length == 0) sticks = GetComponentsInChildren<EgrangStick>(true);

            Transform mesh = transform.Find(MeshName);
            _authoredMesh = mesh != null ? mesh.GetComponent<Renderer>() : null;

            Transform armature = transform.Find(ArmatureName);
            _authoredLeftFoot = FindDeep(armature, "LeftFoot");
            _authoredRightFoot = FindDeep(armature, "RightFoot");

            _authoredHands = new Transform[sticks.Length];
            _authoredFeet = new Transform[sticks.Length];
            for (int i = 0; i < sticks.Length; i++)
            {
                if (sticks[i] == null) continue;
                _authoredHands[i] = sticks[i].HandBone;
                _authoredFeet[i] = sticks[i].FootBone;
            }
        }

        private void PutOn(GameObject model, Avatar sourceAvatar)
        {
            // Its own child, not merged into this object: the walker's Animator binds "Armature/…"
            // by path from here, and must keep finding only the authored skeleton.
            _body = Instantiate(model, transform, false);
            _body.name = "Body (" + model.name + ")";
            Avatar targetAvatar = AvatarOf(model);

            // The model ships an Animator of its own; this body is posed by hand, not animated.
            Animator own = _body.GetComponent<Animator>();
            if (own != null) Destroy(own);

            _source = new HumanPoseHandler(sourceAvatar, transform);
            _target = new HumanPoseHandler(targetAvatar, _body.transform);

            Transform armature = _body.transform.Find(ArmatureName);
            _bodyLeftFoot = FindDeep(armature, "LeftFoot");
            _bodyRightFoot = FindDeep(armature, "RightFoot");

            if (_authoredMesh != null) _authoredMesh.enabled = false;

            for (int i = 0; i < sticks.Length; i++)
            {
                if (sticks[i] == null || _authoredHands[i] == null || _authoredFeet[i] == null) continue;

                Transform hand = FindDeep(armature, _authoredHands[i].name);
                Transform foot = FindDeep(armature, _authoredFeet[i].name);
                if (hand == null || foot == null)
                {
                    Debug.LogWarning($"[EgrangRacerBody] '{model.name}' has no '{_authoredHands[i].name}'/'{_authoredFeet[i].name}' — stilt {i} stays on the authored skeleton.", this);
                    continue;
                }

                sticks[i].Rebind(hand, foot);
            }

            CopyPose();
        }

        private void TakeOff()
        {
            _source?.Dispose();
            _target?.Dispose();
            _source = null;
            _target = null;

            if (_body != null) Destroy(_body);
            _body = null;

            if (_authoredMesh != null) _authoredMesh.enabled = true;

            for (int i = 0; i < sticks.Length; i++)
            {
                if (sticks[i] != null) sticks[i].Rebind(_authoredHands[i], _authoredFeet[i]);
            }
        }

        private void LateUpdate() => CopyPose();

        /// <summary>
        /// The authored skeleton's pose onto the worn body, then the body moved so its lower sole
        /// sits at the authored one's height — the footplates are welded there.
        /// </summary>
        private void CopyPose()
        {
            if (_source == null || _target == null || _body == null) return;

            _body.transform.localPosition = Vector3.zero;
            _source.GetHumanPose(ref _pose);
            _target.SetHumanPose(ref _pose);

            if (_authoredLeftFoot == null || _authoredRightFoot == null || _bodyLeftFoot == null || _bodyRightFoot == null) return;

            float authored = Mathf.Min(_authoredLeftFoot.position.y, _authoredRightFoot.position.y);
            float worn = Mathf.Min(_bodyLeftFoot.position.y, _bodyRightFoot.position.y);
            _body.transform.position += Vector3.up * (authored - worn);
        }

        private void OnDestroy()
        {
            _source?.Dispose();
            _target?.Dispose();
        }

        private GameObject ModelFor(string id)
        {
            foreach (GameObject character in characters)
            {
                if (character != null && PlayerAvatars.IdInName(character.name) == id) return character;
            }

            return null;
        }

        private static Avatar AvatarOf(GameObject model)
        {
            Animator animator = model != null ? model.GetComponent<Animator>() : null;
            return animator != null && animator.avatar != null && animator.avatar.isHuman ? animator.avatar : null;
        }

        private static Transform FindDeep(Transform root, string boneName)
        {
            if (root == null) return null;
            if (root.name == boneName) return root;

            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, boneName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
