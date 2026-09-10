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
    [RequireComponent(typeof(Collider))]
    public abstract class GalleryStation : MonoBehaviour, IInteractable
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
            OnInteract();
        }

        /// <summary>The station's own effect. Runs only while the player is inside.</summary>
        protected abstract void OnInteract();

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

            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) OnInteract();
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

        /// <summary>A 3D source tuned for a room this size: full spatial blend, audible across the atrium, not the building.</summary>
        public static void TuneSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2.5f;
            source.maxDistance = 30f;
            source.dopplerLevel = 0f;
        }
    }
}
