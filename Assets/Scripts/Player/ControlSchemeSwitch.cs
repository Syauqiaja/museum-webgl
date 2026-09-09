using UnityEngine;
using Museum.Core;

/// <summary>
/// Enables the input source that matches the visitor's chosen scheme and disables the other, so
/// only one thing is writing to <see cref="FPSController"/> at a time.
/// </summary>
/// <remarks>
/// An unmade choice reads as desktop, which keeps Museum.unity playable when it is opened
/// straight from the Editor without going through MainMenu.
/// </remarks>
[DefaultExecutionOrder(-2)]
public class ControlSchemeSwitch : MonoBehaviour
{
    [Tooltip("Behaviours live only under the keyboard-and-mouse scheme, e.g. FPSInputReader.")]
    [SerializeField] private Behaviour[] desktopBehaviours = new Behaviour[0];

    [Tooltip("Behaviours live only under the touch scheme, e.g. TouchInputSource.")]
    [SerializeField] private Behaviour[] touchBehaviours = new Behaviour[0];

    /// <summary>Wires the switch from script. The scene wires the same two arrays in the inspector.</summary>
    public void Configure(Behaviour[] desktop, Behaviour[] touch)
    {
        desktopBehaviours = desktop;
        touchBehaviours = touch;
        Apply();
    }

    private void Awake() => Apply();

    private void Apply()
    {
        bool touch = SessionData.Instance != null && SessionData.Instance.IsTouch;

        foreach (Behaviour behaviour in desktopBehaviours)
        {
            if (behaviour != null) behaviour.enabled = !touch;
        }

        foreach (Behaviour behaviour in touchBehaviours)
        {
            if (behaviour != null) behaviour.enabled = touch;
        }
    }
}
