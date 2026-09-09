using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.Core
{
    /// <summary>
    /// Makes a lesson plaque pageable while the visitor is standing in this trigger: a
    /// keyboard poll gated on the player tag, the same shape as <see cref="SceneTriggerPrompt"/>.
    /// </summary>
    /// <remarks>
    /// This rides on a **doorway's existing trigger** rather than bringing a collider of its
    /// own. Each plaque stands beside the doorway for its game, so that volume already covers
    /// the ground a visitor reads from; a second collider over the same floor would be a
    /// duplicate. It therefore shares a GameObject with a <see cref="SceneTriggerPrompt"/> —
    /// both get the trigger callbacks, and their keys do not overlap (Enter enters, Q/E page).
    ///
    /// Keyboard under the desktop scheme, where FPSController locks the cursor and a uGUI button
    /// on a world-space canvas could never be clicked. Under the touch scheme the cursor is never
    /// locked and the overlay's ‹ › buttons page the plaque instead, routed here by
    /// <see cref="TouchInteractRouter"/>. Both paths land on the same two methods.
    ///
    /// Unlike a doorway, this does not toggle the panel — the plaque is readable from across
    /// the room. It only decides whether the keys are live, and tells the panel so it can say
    /// as much.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public class LessonReader : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";

        [Tooltip("The plaque these keys page. Not a child — it lives on the plaque mesh nearby.")]
        [SerializeField] private LessonPanel panel;

        private bool _playerInside;

        /// <summary>True while the player stands in this plaque's trigger.</summary>
        public bool PlayerInside => _playerInside;

        /// <summary>
        /// Next page. Called by the E key and by the overlay's › button alike. Null-guarded because
        /// the router reaches these from a button, and a plaque with no panel assigned disables
        /// itself in Awake but is still a live reference.
        /// </summary>
        public void Next()
        {
            if (panel != null) panel.Next();
        }

        /// <summary>Previous page. Called by the Q key and by the overlay's ‹ button alike.</summary>
        public void Prev()
        {
            if (panel != null) panel.Prev();
        }

        private void Awake()
        {
            if (panel == null)
            {
                Debug.LogWarning($"[LessonReader] {name}: no LessonPanel assigned — this trigger " +
                                 "does nothing.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = true;
            panel.SetActiveReader(true);
            TouchInteractRouter.Register(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInside = false;
            panel.SetActiveReader(false);
            TouchInteractRouter.Unregister(this);
        }

        private void Update()
        {
            if (!_playerInside) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Q and E only: they sit under the hand that is already on WASD, and they are the
            // keys the Museum's KONTROL panel advertises. Neither clashes with movement, and
            // both stay clear of a doorway's Enter, so overlapping triggers are unambiguous.
            if (keyboard.qKey.wasPressedThisFrame)
            {
                Prev();
            }
            else if (keyboard.eKey.wasPressedThisFrame)
            {
                Next();
            }
        }
    }
}
