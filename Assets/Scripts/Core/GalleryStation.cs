using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.Core
{
    /// <summary>
    /// A lobby exhibit the visitor can walk up to and set off: the same trigger-and-Enter shape
    /// as a doorway (<see cref="SceneTriggerPrompt"/>), but the key does something in the
    /// room instead of leaving it. Subclasses say what; this handles the standing-in-it part,
    /// the floating "Tekan Enter" hint, and the touch overlay's Interaksi button via
    /// <see cref="TouchInteractRouter"/>.
    /// </summary>
    /// <remarks>
    /// Shares its trigger GameObject with a <see cref="LessonReader"/>: a station has a plaque,
    /// and the floor a visitor reads it from is the floor they interact from. Keys do not
    /// overlap (Enter acts, Q/E page).
    /// </remarks>
    /// <remarks>
    /// What one visitor sets off, the others see and hear: a local press is reported through
    /// <see cref="MuseumInteractions"/> and plays on everyone else's copy as
    /// <see cref="OnRemoteInteract"/> — by default the same effect, wherever they stand. The 3D
    /// source's rolloff is what makes it "if close enough".
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public abstract class GalleryStation : MonoBehaviour, IInteractable, IRemoteInteractable
    {
        [SerializeField] private string playerTag = "Player";

        [Tooltip("World-space label shown while the visitor stands in the trigger. Turned to face the camera.")]
        [SerializeField] private TMP_Text hint;

        [Tooltip("What the hint says under the desktop scheme; the touch scheme substitutes 'Ketuk Interaksi'.")]
        [SerializeField] private string hintText = "Tekan Enter";

        private bool _playerInside;
        private Transform _camera;

        /// <summary>True while the player stands in this station's trigger.</summary>
        public bool PlayerInside => _playerInside;

        /// <summary>Called by Enter, and by the overlay's Interaksi button through the router.</summary>
        public void Interact()
        {
            if (!_playerInside) return;
            InteractLocally();
        }

        /// <summary>Another visitor set this station off; plays it here without reporting it back.</summary>
        public void PlayRemote(int index) => OnRemoteInteract(index);

        /// <summary>This station's id in the museum room — one of <see cref="MuseumInteractions"/>' constants.</summary>
        protected abstract string StationId { get; }

        /// <summary>The station's own effect, set off by the local visitor standing in it.</summary>
        protected abstract void OnInteract();

        /// <summary>What another visitor setting it off looks and sounds like here. The same effect unless overridden.</summary>
        protected virtual void OnRemoteInteract(int index) => OnInteract();

        private void InteractLocally()
        {
            OnInteract();
            MuseumInteractions.ReportLocal(StationId, 0);
        }

        protected virtual void OnEnable() => MuseumInteractions.Register(StationId, this);

        protected virtual void OnDisable() => MuseumInteractions.Unregister(StationId, this);

        protected virtual void Awake()
        {
            if (hint != null)
            {
                bool touch = SessionData.Instance != null && SessionData.Instance.IsTouch;
                hint.text = touch ? "Ketuk Interaksi" : hintText;
                hint.gameObject.SetActive(false);
            }
        }

        protected virtual void Update()
        {
            if (!_playerInside) return;

            if (hint != null)
            {
                if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
                if (_camera != null)
                {
                    Vector3 away = hint.transform.position - _camera.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.001f) hint.transform.rotation = Quaternion.LookRotation(away);
                }
            }

            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) InteractLocally();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = true;
            if (hint != null) hint.gameObject.SetActive(true);
            TouchInteractRouter.Register(this);
            OnPlayerEnter();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = false;
            if (hint != null) hint.gameObject.SetActive(false);
            TouchInteractRouter.Unregister(this);
            OnPlayerExit();
        }

        protected virtual void OnPlayerEnter() { }

        protected virtual void OnPlayerExit() { }

        /// <summary>Beyond this many metres a station is silent.</summary>
        public const float AudibleDistance = 25f;

        /// <summary>
        /// A 3D source tuned for a room this size: full spatial blend, audible across the atrium,
        /// not the building. Linear, because a logarithmic rolloff never reaches zero — it only
        /// stops falling at its max distance — and with visitors setting stations off for each
        /// other, a gong struck in the lobby would reach everyone on every floor.
        /// </summary>
        public static void TuneSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2.5f;
            source.maxDistance = AudibleDistance;
            source.dopplerLevel = 0f;
        }
    }
}
