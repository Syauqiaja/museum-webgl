using UnityEngine;
using UnityEngine.EventSystems;
using Museum.Core;

/// <summary>
/// The floating thumb stick: it appears wherever the finger lands inside its rect, so the player
/// never has to look down to find it. All the geometry is in <see cref="VirtualStickModel"/>;
/// this class only turns uGUI events into calls on it and moves the knob graphic.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("Screen pixels from the touch-down point at which movement is at full speed.")]
    [SerializeField] private float radius = 120f;

    [Tooltip("Screen pixels of slop before any movement is reported.")]
    [SerializeField] private float deadZone = 12f;

    [Tooltip("Optional ring that moves to the touch-down point. Hidden until touched.")]
    [SerializeField] private RectTransform ring;

    [Tooltip("Optional knob drawn inside the ring.")]
    [SerializeField] private RectTransform knob;

    private VirtualStickModel _model;
    private Vector2 _origin;
    private bool _held;

    /// <summary>This frame's movement, magnitude 0..1. Zero while nothing is touching it.</summary>
    public Vector2 Value { get; private set; }

    private void Awake() => _model = new VirtualStickModel(radius, deadZone);

    private void OnDisable() => Release();

    public void OnPointerDown(PointerEventData eventData)
    {
        _held = true;
        _origin = eventData.position;
        Value = Vector2.zero;

        if (ring != null)
        {
            ring.gameObject.SetActive(true);
            ring.position = _origin;
        }
        MoveKnob(_origin);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_held) return;
        Value = _model.Evaluate(_origin, eventData.position);
        MoveKnob(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    private void Release()
    {
        _held = false;
        Value = Vector2.zero;
        if (ring != null) ring.gameObject.SetActive(false);
    }

    private void MoveKnob(Vector2 current)
    {
        if (knob == null) return;
        knob.position = _origin + _model.KnobOffset(_origin, current);
    }
}
