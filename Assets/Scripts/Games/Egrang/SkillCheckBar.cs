using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Museum.Games.Egrang
{
    /// <summary>Raised once per press with the timing outcome.</summary>
    [Serializable]
    public sealed class EgrangStepEvent : UnityEvent<EgrangStepResult> { }

    /// <summary>
    /// The timing bar the player walks with: a cursor sweeps back and forth across a colour-coded
    /// track, and pressing Step scores whatever band it was over — green a full step, yellow a
    /// short one, red a stumble.
    ///
    /// Put this on the bar's root UI object. <see cref="track"/> supplies the width the cursor
    /// travels and <see cref="cursor"/> is the indicator moved along it. The track itself is a
    /// single <c>Image</c> with a plain white sprite: the bar bakes the red/yellow/green bands into
    /// a texture from the zone table and assigns it, so the picture cannot drift out of step with
    /// the numbers the game actually scores against — <see cref="zones"/> is the only source of
    /// truth. Bands meet at hard edges, because a soft edge would show the player a boundary that
    /// is not where the scoring actually changes.
    ///
    /// All the timing lives in <see cref="SkillCheckCursor"/> and <see cref="SkillCheckZones"/>;
    /// this class is the scene-facing shell around them. It only announces the result, so what a
    /// step actually does is up to the listener (<see cref="EgrangStepMover"/> by default).
    /// </summary>
    public sealed class SkillCheckBar : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Rect the cursor travels across. Its width defines the sweep; the colour bands should be children of it.")]
        [SerializeField] private RectTransform track;
        [Tooltip("The moving indicator. Must be a child of the track with a centred anchor and pivot, since it is placed by anchoredPosition about the track's middle.")]
        [SerializeField] private RectTransform cursor;

        [Header("Timing")]
        [Tooltip("Seconds for one edge-to-edge pass. Lower is harder.")]
        [SerializeField] private float sweepSeconds = 1.2f;
        [Tooltip("Seconds the cursor holds still after a press, covering the step animation. Set this at least as long as your longest step clip — it is the only thing stopping a second press from interrupting a step in progress.")]
        [SerializeField] private float lockoutSeconds = 0.6f;

        [Header("Zones")]
        [Tooltip("Colour bands in normalized track space, listed left to right. The zone images are stretched and tinted to match these, so these numbers are the truth and the art follows.")]
        [SerializeField]
        private SkillCheckZones zones = new SkillCheckZones(
            new SkillCheckZone(0.00f, 0.30f, EgrangStepResult.Fail),
            new SkillCheckZone(0.30f, 0.42f, EgrangStepResult.Half),
            new SkillCheckZone(0.42f, 0.58f, EgrangStepResult.Full),
            new SkillCheckZone(0.58f, 0.70f, EgrangStepResult.Half),
            new SkillCheckZone(0.70f, 1.00f, EgrangStepResult.Fail));

        [Header("Track graphic")]
        [Tooltip("The single Image filling the track. Give it a plain white sprite — the bar bakes a gradient from the zones above and assigns it. Leave empty to use an Image on the track object itself.")]
        [SerializeField] private Image trackImage;
        [Tooltip("Band colours. Shared with the stick-selection cards so a card's preview cannot show bands in colours the bar does not use.")]
        [SerializeField] private SkillCheckTrackColors trackColors = SkillCheckTrackColors.Default;
        [Tooltip("Pixel width of the baked track texture. It is stretched across the track, so this is how precisely the band edges land, not size. Higher costs nothing at runtime.")]
        [Range(SkillCheckTrackTexture.MinResolution, SkillCheckTrackTexture.MaxResolution)]
        [SerializeField] private int trackResolution = 512;

        [Header("Input")]
        [Tooltip("Action asset holding the Step action. Leave empty to drive the bar from script via Press().")]
        [SerializeField] private InputActionAsset inputAsset;
        [Tooltip("Action map inside the asset.")]
        [SerializeField] private string actionMapName = "Egrang";
        [Tooltip("Action bound to Space.")]
        [SerializeField] private string stepActionName = "Step";

        [Header("Output")]
        [SerializeField] private EgrangStepEvent onStepResult = new EgrangStepEvent();

        SkillCheckCursor _cursor;
        InputAction _stepAction;
        float _lockoutRemaining;
        Sprite _bakedSprite;
        EgrangStickProfile _pendingProfile;

        /// <summary>Fires once per press with the outcome. Also exposed in the inspector.</summary>
        public EgrangStepEvent OnStepResult => onStepResult;

        /// <summary>Where the cursor currently sits, 0 at the left edge and 1 at the right.</summary>
        public float CursorPosition => _cursor?.Position ?? 0f;

        /// <summary>True while a step is playing out and presses are being ignored.</summary>
        public bool IsLocked => _lockoutRemaining > 0f;

        /// <summary>
        /// While true every press is ignored, whatever it came from — Space, the JALAN button, the
        /// touch tap zone — and the cursor keeps sweeping. Held by <see cref="EgrangPauseMenu"/>:
        /// the race is real-time and the server keeps its clock, so a pause cannot stop the race,
        /// only stop a key typed at the menu from walking the racer behind it.
        /// </summary>
        public bool InputBlocked { get; set; }

        /// <summary>The stick whose difficulty the bar is currently running, or null while on its inspector-authored defaults.</summary>
        public EgrangStickProfile Profile { get; private set; }

        /// <summary>The table presses are scored against — the bar's own copy, whether authored in the inspector or taken from a profile.</summary>
        public SkillCheckZones Zones => zones;

        /// <summary>Seconds for one edge-to-edge cursor pass.</summary>
        public float SweepSeconds => sweepSeconds;

        void Awake()
        {
            _cursor = new SkillCheckCursor(sweepSeconds);

            // A stick chosen before the bar woke up — the selection panel resolves in its own Awake,
            // and enabling this object is usually what brings us here. Adopting it now means callers
            // never have to care which of the two ran first.
            if (_pendingProfile != null)
            {
                Adopt(_pendingProfile);
                _pendingProfile = null;
            }

            if (!zones.Validate(out string error))
            {
                Debug.LogWarning($"{nameof(SkillCheckBar)} on '{name}' has bad zone data: {error}", this);
            }

            if (ResolveTrackImage() == null)
            {
                Debug.LogWarning(
                    $"{nameof(SkillCheckBar)} on '{name}' has no track image, so the gradient goes undrawn. " +
                    "Assign one, or put an Image on the track object.", this);
            }

            BakeTrackGradient();

            if (inputAsset != null)
            {
                InputActionMap map = inputAsset.FindActionMap(actionMapName);
                _stepAction = map?.FindAction(stepActionName);

                if (_stepAction == null)
                {
                    Debug.LogWarning(
                        $"{nameof(SkillCheckBar)} on '{name}' could not find action '{actionMapName}/{stepActionName}'. " +
                        "The bar will sweep but not respond to input.", this);
                }
            }
        }

        void OnEnable()
        {
            if (_stepAction == null) return;

            inputAsset.Enable();
            _stepAction.performed += OnStepPerformed;
        }

        void OnDisable()
        {
            if (_stepAction == null) return;

            _stepAction.performed -= OnStepPerformed;
            inputAsset.Disable();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (_lockoutRemaining > 0f)
            {
                _lockoutRemaining -= dt;
                if (_lockoutRemaining <= 0f)
                {
                    _lockoutRemaining = 0f;
                    _cursor.Resume();
                }
            }

            _cursor.SweepSeconds = sweepSeconds;
            _cursor.Advance(dt);
            PlaceCursor();
        }

        void OnStepPerformed(InputAction.CallbackContext context) => Press();

        /// <summary>
        /// Scores a press at the cursor's current position and starts the lockout. Ignored while
        /// locked or while <see cref="InputBlocked"/>. Public so the bar can be driven from a UI
        /// button or a test without an action asset.
        /// </summary>
        public void Press()
        {
            if (IsLocked || InputBlocked) return;

            _cursor.Freeze();
            EgrangStepResult result = zones.Evaluate(_cursor.Position);
            onStepResult.Invoke(result);

            if (lockoutSeconds > 0f)
            {
                _lockoutRemaining = lockoutSeconds;
            }
            else
            {
                _cursor.Resume();
            }
        }

        /// <summary>
        /// Switches the bar to a stick's difficulty: its zone table and its sweep time. Call this
        /// from the selection panel before the run starts.
        ///
        /// The cursor is sent back to the left edge and unfrozen, so every run begins from the same
        /// place no matter how long the selection screen was open — and the player's first press has
        /// to be aimed rather than handed to them, which starting on green would do. Safe to call
        /// before <c>Awake</c>: the profile is held and applied when the bar wakes.
        /// </summary>
        public void Configure(EgrangStickProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning($"{nameof(SkillCheckBar)} on '{name}' was configured with a null profile; keeping its current zones.", this);
                return;
            }

            if (_cursor == null)
            {
                _pendingProfile = profile;
                return;
            }

            Adopt(profile);
        }

        /// <summary>
        /// Takes a profile's numbers, assuming the cursor already exists. The zone table is copied
        /// rather than referenced: the bar owns its own list, so nothing it does can write back into
        /// a shipped asset — in the editor that edit would persist past play mode.
        /// </summary>
        void Adopt(EgrangStickProfile profile)
        {
            Profile = profile;
            zones = new SkillCheckZones(profile.Zones.Zones);
            sweepSeconds = profile.SweepSeconds;

            _cursor.SweepSeconds = sweepSeconds;
            _cursor.Reset(0f);
            _lockoutRemaining = 0f;

            if (!zones.Validate(out string error))
            {
                Debug.LogWarning($"{nameof(SkillCheckBar)} on '{name}' got bad zone data from profile " +
                                 $"'{profile.name}': {error}", this);
            }

            BakeTrackGradient();
        }

        /// <summary>
        /// Bakes the current zone table and hands the strip to <see cref="trackImage"/>, which
        /// stretches it across the whole track. See <see cref="SkillCheckTrackTexture"/> for why the
        /// bake lives outside this class and why the bands meet at hard edges.
        /// </summary>
        void BakeTrackGradient()
        {
            Image image = ResolveTrackImage();
            if (image == null) return;

            SkillCheckTrackTexture.Release(ref _bakedSprite);
            _bakedSprite = SkillCheckTrackTexture.Bake(zones, trackColors, trackResolution, "SkillCheckBarTrack");

            image.sprite = _bakedSprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }

        Image ResolveTrackImage()
        {
            if (trackImage != null) return trackImage;
            return track != null ? track.GetComponent<Image>() : null;
        }

        void OnDestroy() => SkillCheckTrackTexture.Release(ref _bakedSprite);

#if UNITY_EDITOR
        /// <summary>
        /// Rebakes as the zones and colours are edited, so the Game view shows the bar the player
        /// will actually see. Deferred to the next editor tick because creating assets during
        /// <c>OnValidate</c> itself is not allowed.
        /// </summary>
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                // The object can be gone by the time this runs — an undo, a scene close, a domain
                // reload between the edit and the tick.
                if (this == null || !isActiveAndEnabled) return;
                BakeTrackGradient();
            };
        }
#endif

        void PlaceCursor()
        {
            if (cursor == null || track == null) return;

            float halfWidth = track.rect.width * 0.5f;
            Vector2 position = cursor.anchoredPosition;
            position.x = Mathf.Lerp(-halfWidth, halfWidth, _cursor.Position);
            cursor.anchoredPosition = position;
        }
    }
}
