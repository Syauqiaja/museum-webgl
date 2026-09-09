using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Marks a 3D object as hover/click responsive and owns its material swaps.
    /// Attach to any object with one or more <see cref="Renderer"/>s (on this
    /// object or its children) and a <see cref="Collider"/> so the raycaster can
    /// hit it. Driven by <see cref="HoverClickRaycaster"/> — do not call the
    /// state methods from elsewhere unless you replace the raycaster.
    /// </summary>
    /// <remarks>
    /// Swaps the whole material: on hover/click every affected renderer's
    /// material list is replaced wholesale, and restored to the originals
    /// captured in <see cref="Awake"/>. Assigning <c>Renderer.materials</c>
    /// instantiates per-renderer material copies at runtime (expected — that is
    /// how Unity isolates the swap); the originals stay untouched as shared assets.
    /// </remarks>
    [DisallowMultipleComponent]
    public class Hoverable : MonoBehaviour
    {
        [Tooltip("Material applied while the pointer is over this object.")]
        [SerializeField] private Material hoverMaterial;

        [Tooltip("Material applied while this object is pressed (pointer down).")]
        [SerializeField] private Material clickMaterial;

        [Tooltip("Renderers to affect. Leave empty to auto-collect from this " +
                 "object and its children in Awake.")]
        [SerializeField] private Renderer[] targetRenderers;

        // Original material lists, captured once so any swap is fully reversible.
        private Material[][] _originalMaterials;

        private enum State { Normal, Hover, Click }
        private State _state = State.Normal;

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<Renderer>();

            _originalMaterials = new Material[targetRenderers.Length][];
            for (int i = 0; i < targetRenderers.Length; i++)
                _originalMaterials[i] = targetRenderers[i].sharedMaterials;
        }

        /// <summary>Pointer entered — show the hover material (unless pressed).</summary>
        public void OnHoverEnter()
        {
            if (_state == State.Click) return;
            SetState(State.Hover);
        }

        /// <summary>Pointer left — restore the original materials (unless pressed).</summary>
        public void OnHoverExit()
        {
            if (_state == State.Click) return;
            SetState(State.Normal);
        }

        /// <summary>Pointer pressed on this object — show the click material.</summary>
        public void OnClickDown() => SetState(State.Click);

        /// <summary>
        /// Pointer released. Falls back to hover if still hovered, else normal.
        /// </summary>
        public void OnClickUp(bool stillHovered) =>
            SetState(stillHovered ? State.Hover : State.Normal);

        private void SetState(State next)
        {
            if (_state == next) return;
            _state = next;

            switch (next)
            {
                case State.Hover: Apply(hoverMaterial); break;
                case State.Click: Apply(clickMaterial); break;
                case State.Normal: Restore(); break;
            }
        }

        // Fill each renderer's whole material list with one material. Null material
        // (unassigned in inspector) leaves that renderer's originals in place.
        private void Apply(Material material)
        {
            if (material == null) { Restore(); return; }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                var slots = new Material[_originalMaterials[i].Length];
                for (int s = 0; s < slots.Length; s++) slots[s] = material;
                targetRenderers[i].sharedMaterials = slots;
            }
        }

        private void Restore()
        {
            for (int i = 0; i < targetRenderers.Length; i++)
                targetRenderers[i].sharedMaterials = _originalMaterials[i];
        }
    }
}
