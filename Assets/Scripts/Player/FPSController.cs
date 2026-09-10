using UnityEngine;
using UnityEngine.InputSystem;
using Museum.Core;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class FPSController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float minYRotation = -90f;
    [SerializeField] private float maxYRotation = 90f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isSprintPressed;
    private bool isJumpPressed;

    private float xRotation = 0f;
    private int groundContactCount;
    private bool isGrounded => groundContactCount > 0;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        ReturnToWhereTheVisitorLeft();

        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
            }
        }

        if (cameraTransform != null && !cameraTransform.IsChildOf(transform))
        {
            Debug.LogWarning(
                $"[{nameof(FPSController)}] {cameraTransform.name} is not a child of {transform.name}. " +
                "Yaw (body rotation) will not visually affect the camera. Parent the camera under the player.",
                this);
        }

        if (UsesCursorLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// Back from a game: stand where the doorway was walked through, facing the way the visitor
    /// faced, instead of at the rig's authored spot by the entrance. Runs in Awake, before any
    /// Start, so the museum presence's first report is already this spot and nobody sees a jump.
    /// </summary>
    private void ReturnToWhereTheVisitorLeft()
    {
        if (SessionData.Instance == null || !SessionData.Instance.TryTakeMuseumReturn(out Vector3 position, out float yaw)) return;

        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        transform.SetPositionAndRotation(position, facing);
        rb.position = position;
        rb.rotation = facing;
        rb.linearVelocity = Vector3.zero;
    }

    /// <summary>
    /// Pointer lock is a desktop-only mechanism: mobile browsers have no Pointer Lock API, and
    /// locking there would leave the player with no look control at all. An unmade choice — the
    /// museum scene opened directly in the Editor — reads as desktop, which is how it has always
    /// behaved.
    /// </summary>
    public bool UsesCursorLock =>
        SessionData.Instance == null || SessionData.Instance.Scheme != ControlScheme.Sentuh;

    private void Update()
    {
        HandleCursorLock();
        HandleMouseLook();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleJump();
    }

    private void HandleCursorLock()
    {
        if (!UsesCursorLock) return;

        // WebGL/browsers drop pointer lock on focus loss and require a fresh user
        // gesture (click) to re-acquire it — otherwise look input silently stops.
        if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsGroundLayer(collision.gameObject.layer))
        {
            groundContactCount++;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (IsGroundLayer(collision.gameObject.layer))
        {
            groundContactCount = Mathf.Max(0, groundContactCount - 1);
        }
    }

    private bool IsGroundLayer(int layer)
    {
        return (groundMask.value & (1 << layer)) != 0;
    }

    private void HandleMovement()
    {
        float currentSpeed = isSprintPressed ? sprintSpeed : moveSpeed;

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 horizontalVelocity = currentSpeed * moveInput.magnitude * move.normalized;

        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
    }

    private void HandleJump()
    {
        if (isJumpPressed && isGrounded)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(velocity.x, jumpForce, velocity.z);
        }

        isJumpPressed = false;
    }

    private void HandleMouseLook()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minYRotation, maxYRotation);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseX);

        // Consumed. The mouse re-sends a delta every frame it moves and a zero when it stops, so
        // clearing here changes nothing for it; touch adds deltas and depends on it.
        lookInput = Vector2.zero;
    }

    public void OnMove(InputAction.CallbackContext context) => SetMoveInput(context.ReadValue<Vector2>());

    public void OnLook(InputAction.CallbackContext context) => AddLookDelta(context.ReadValue<Vector2>());

    public void OnSprint(InputAction.CallbackContext context) => SetSprint(context.ReadValueAsButton());

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed) PressJump();
    }

    /// <summary>Movement for this frame, in the same range the WASD composite produces.</summary>
    public void SetMoveInput(Vector2 move) => moveInput = move;

    /// <summary>
    /// Adds look delta in <c>&lt;Mouse&gt;/delta</c> units. Additive rather than assigned so several
    /// touch events in one frame sum instead of overwriting each other.
    /// </summary>
    public void AddLookDelta(Vector2 delta) => lookInput += delta;

    public void SetSprint(bool held) => isSprintPressed = held;

    public void PressJump() => isJumpPressed = true;
}
