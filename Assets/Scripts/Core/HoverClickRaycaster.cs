using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.Core
{
    /// <summary>
    /// Central pointer raycaster: one per scene. Each frame it casts from the
    /// pointer through <see cref="raycastCamera"/> and drives the material state
    /// of whichever <see cref="Hoverable"/> is under the cursor. No per-object
    /// Update loops — every hoverable object stays passive until this hits it.
    /// </summary>
    /// <remarks>
    /// Uses the new Input System (<see cref="Pointer.current"/>), so it works for
    /// mouse and touch, including WebGL builds where raw pointer polling is fine.
    /// Objects need a <see cref="Collider"/> to be hit. Restrict what is testable
    /// with <see cref="hittableLayers"/> to keep the raycast cheap.
    /// </remarks>
    public class HoverClickRaycaster : MonoBehaviour
    {
        [Tooltip("Camera used to build the pointer ray. Defaults to Camera.main.")]
        [SerializeField] private Camera raycastCamera;

        [Tooltip("Only colliders on these layer`s are considered hoverable.")]
        [SerializeField] private LayerMask hittableLayers = ~0;

        [Tooltip("Max ray distance in world units.")]
        [SerializeField] private float maxDistance = 100f;

        // The hoverable currently under the pointer (may be null).
        private Hoverable _current;
        // The hoverable that received the last OnClickDown, if the button is held.
        private Hoverable _pressed;

        private void Awake()
        {
            if (raycastCamera == null) raycastCamera = Camera.main;
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || raycastCamera == null) return;

            Hoverable hit = Raycast(pointer.position.ReadValue());
            UpdateHover(hit);
            UpdatePress(pointer, hit);
        }

        private Hoverable Raycast(Vector2 screenPos)
        {
            Ray ray = raycastCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit info, maxDistance, hittableLayers))
                return info.collider.GetComponentInParent<Hoverable>();
            return null;
        }

        // Fire enter/exit as the object under the cursor changes.
        private void UpdateHover(Hoverable hit)
        {
            if (hit == _current) return;

            if (_current != null) _current.OnHoverExit();
            if (hit != null) hit.OnHoverEnter();
            _current = hit;
        }

        // Track press/release on the primary pointer button.
        private void UpdatePress(Pointer pointer, Hoverable hit)
        {
            var press = pointer.press;

            if (press.wasPressedThisFrame && hit != null)
            {
                _pressed = hit;
                _pressed.OnClickDown();
            }
            else if (press.wasReleasedThisFrame && _pressed != null)
            {
                bool stillHovered = _pressed == hit;
                _pressed.OnClickUp(stillHovered);
                _pressed = null;
            }
        }
    }
}
