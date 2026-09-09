using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1)]
public class FPSInputReader : MonoBehaviour
{
    [SerializeField] private FPSController controller;
    [SerializeField] private InputActionAsset inputAsset;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<FPSController>();
        }

        var map = inputAsset.FindActionMap("Player");
        moveAction = map.FindAction("Move");
        lookAction = map.FindAction("Look");
        sprintAction = map.FindAction("Sprint");
        jumpAction = map.FindAction("Jump");
    }

    private void OnEnable()
    {
        inputAsset.Enable();
        
        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        lookAction.performed += OnLook;
        lookAction.canceled += OnLook;
        sprintAction.performed += OnSprint;
        sprintAction.canceled += OnSprint;
        jumpAction.performed += OnJump;
    }

    private void OnDisable()
    {
        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;
        lookAction.performed -= OnLook;
        lookAction.canceled -= OnLook;
        sprintAction.performed -= OnSprint;
        sprintAction.canceled -= OnSprint;
        jumpAction.performed -= OnJump;
        
        inputAsset.Disable();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        controller.OnMove(context);
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        controller.OnLook(context);
    }

    private void OnSprint(InputAction.CallbackContext context)
    {
        controller.OnSprint(context);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        controller.OnJump(context);
    }
}
