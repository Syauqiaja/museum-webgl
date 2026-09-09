using UnityEngine;

/// <summary>
/// Pushes the touch overlay's widgets into <see cref="FPSController"/>'s script input surface —
/// the touch-scheme counterpart of <see cref="FPSInputReader"/>. Exactly one of the two is
/// enabled, by <see cref="ControlSchemeSwitch"/>.
/// </summary>
[DefaultExecutionOrder(-1)]
public class TouchInputSource : MonoBehaviour
{
    [SerializeField] private FPSController controller;
    [SerializeField] private TouchJoystick joystick;
    [SerializeField] private TouchLookArea lookArea;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<FPSController>();
    }

    /// <summary>Wires the source from script. The scene wires the same three through the inspector.</summary>
    public void Configure(FPSController target, TouchJoystick stick, TouchLookArea look)
    {
        controller = target;
        joystick = stick;
        lookArea = look;
    }

    private void Update()
    {
        if (controller == null) return;

        if (joystick != null) controller.SetMoveInput(joystick.Value);
        if (lookArea != null) controller.AddLookDelta(lookArea.ConsumeLook());
    }

    /// <summary>UnityEvent target for the Lompat button.</summary>
    public void Jump()
    {
        if (controller != null) controller.PressJump();
    }
}
