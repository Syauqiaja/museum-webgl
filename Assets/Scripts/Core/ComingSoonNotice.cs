using TMPro;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// An exhibit whose game is not built yet. The same trigger shape as a doorway
    /// (<see cref="SceneTriggerPrompt"/>), but there is nothing to press: it only says so while
    /// the visitor stands there, and it registers with nothing — the Interaksi button and the
    /// Enter key both stay dead here on purpose.
    /// </summary>
    /// <remarks>
    /// Rides on the exhibit's existing video trigger volume, like a doorway does, so the same
    /// walk-in starts the footage, opens the lesson and shows the notice. A disabled doorway was
    /// the previous answer; it was honest but mute, and a visitor who has just left the Egrang
    /// bay expects a prompt of some kind.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public class ComingSoonNotice : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";

        [Tooltip("World-space label shown while the visitor stands in the trigger. Turned to face the camera.")]
        [SerializeField] private TMP_Text notice;

        [Tooltip("What the label says. No key is named: there is nothing to press.")]
        [SerializeField] private string text = "Segera hadir";

        private bool _playerInside;
        private Transform _camera;

        /// <summary>True while the player stands in this exhibit's trigger.</summary>
        public bool PlayerInside => _playerInside;

        private void Awake()
        {
            if (notice == null)
            {
                Debug.LogWarning($"[ComingSoonNotice] {name}: no label assigned — this trigger " +
                                 "says nothing.", this);
                enabled = false;
                return;
            }

            notice.text = text;
            notice.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_playerInside || notice == null) return;

            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
            if (_camera == null) return;

            Vector3 away = notice.transform.position - _camera.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) notice.transform.rotation = Quaternion.LookRotation(away);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = true;
            if (notice != null) notice.gameObject.SetActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = false;
            if (notice != null) notice.gameObject.SetActive(false);
        }
    }
}
