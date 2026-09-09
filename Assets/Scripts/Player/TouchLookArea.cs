using UnityEngine;
using UnityEngine.EventSystems;
using Museum.Core;

/// <summary>
/// Drag anywhere in this rect to look around. Sits over the right side of the screen, behind
/// nothing: it is a full rect with a transparent graphic so uGUI will route drags to it.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TouchLookArea : MonoBehaviour, IDragHandler
{
    [Tooltip("Screen pixels of drag to look-delta units. Compared against FPSController's own mouse sensitivity.")]
    [SerializeField] private float sensitivity = 2.7f;

    private LookDragModel _model;

    private void Awake() => _model = new LookDragModel(sensitivity);

    public void OnDrag(PointerEventData eventData)
    {
        _model ??= new LookDragModel(sensitivity);
        _model.AddDrag(eventData.delta);
    }

    /// <summary>This frame's look delta, cleared as it is read.</summary>
    public Vector2 ConsumeLook()
    {
        _model ??= new LookDragModel(sensitivity);
        return _model.Consume();
    }
}
