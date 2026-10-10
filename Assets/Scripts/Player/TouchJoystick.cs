using UnityEngine;
using UnityEngine.EventSystems;
using Museum.Core;

/// <summary>
/// The floating thumb stick: it snaps to wherever the finger lands inside its rect, so the player
/// never has to look down to find it. All the geometry is in <see cref="VirtualStickModel"/>;
/// this class only turns uGUI events into calls on it and moves the knob graphic.
/// </summary>
/// <remarks>
/// It rests visible at its authored position rather than hiding until touched. A stick that only
/// exists once you touch it is a stick a first-time visitor never learns is there — the museum has
/// no attendant to say "drag the left side of the screen", so the control has to say it itself by
/// being on screen. Floating is still the behaviour that matters once a finger lands: the ring
/// jumps to the touch point and returns home on release.
/// </remarks>
[RequireComponent(typeof(RectTransform))]
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("Screen pixels from the touch-down point at which movement is at full speed.")]
    [SerializeField] private float radius = 120f;

    [Tooltip("Screen pixels of slop before any movement is reported.")]
    [SerializeField] private float deadZone = 12f;

    [Tooltip("Optional ring. Rests at its authored position and follows the finger while held.")]
    [SerializeField] private RectTransform ring;

    [Tooltip("Optional knob drawn inside the ring.")]
    [SerializeField] private RectTransform knob;

    [Tooltip("Opacity of the resting ring. It goes fully opaque under a finger. " +
             "Needs a CanvasGroup on the ring; without one the ring simply rests at full opacity.")]
    [Range(0f, 1f)]
    [SerializeField] private float restAlpha = 0.4f;

    private VirtualStickModel _model;
    private CanvasGroup _ringFade;
    private Vector2 _restPosition;
    private Vector2 _origin;
    private bool _held;

    /// <summary>This frame's movement, magnitude 0..1. Zero while nothing is touching it.</summary>
    public Vector2 Value { get; private set; }

    private void Awake()
    {
        _model = new VirtualStickModel(radius, deadZone);

        if (ring == null) return;

        // Captured before anything moves it: the authored position is the home to return to.
        _restPosition = ring.anchoredPosition;
        _ringFade = ring.GetComponent<CanvasGroup>();
        ring.gameObject.SetActive(true);
        Rest();
    }

    private void OnDisable() => Release();

    public void OnPointerDown(PointerEventData eventData)
    {
        _held = true;
        _origin = eventData.position;
        Value = Vector2.zero;

        if (ring != null)
        {
            ring.position = _origin;
            if (_ringFade != null) _ringFade.alpha = 1f;
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
        Rest();
    }

    /// <summary>Puts the ring back where it was authored, dimmed, with the knob centred.</summary>
    private void Rest()
    {
        if (ring == null) return;

        ring.anchoredPosition = _restPosition;
        if (_ringFade != null) _ringFade.alpha = restAlpha;
        if (knob != null) knob.anchoredPosition = Vector2.zero;
    }

    private void MoveKnob(Vector2 current)
    {
        if (knob == null) return;
        knob.position = _origin + _model.KnobOffset(_origin, current);
    }
}
