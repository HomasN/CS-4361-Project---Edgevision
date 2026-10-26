using UnityEngine;
using UnityEngine.InputSystem;

namespace Edgevision
{
    /// <summary>
    /// CharacterController-based first-person controller.
    /// WASD movement, mouse-look with pitch clamping, Spacebar jump, gravity,
    /// Escape releases the cursor, left-click re-locks it.
    ///
    /// Input comes from the project InputActionAsset ("Player" map with
    /// Move / Look / Jump actions). Runs early in script order so cursor
    /// state is settled before interaction scripts read it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-100)]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Input actions asset containing a 'Player' map (Move/Look/Jump).")]
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("Camera used for view pitching. Defaults to Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [Tooltip("Apex height of a jump in meters. 1.5 lets the player mount steps up to ~1.3m.")]
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -20f;

        [Header("Look")]
        [Tooltip("Degrees of rotation per unit of mouse delta.")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float pitchLimit = 85f;

        private CharacterController controller;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;

        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            if (cameraTransform == null)
                Debug.LogError("[PlayerController] No camera assigned and no Camera.main found.", this);

            if (inputActions == null)
            {
                Debug.LogError("[PlayerController] Input actions asset not assigned. " +
                               "Run Edgevision > Build Prototype Room or assign InputSystem_Actions.", this);
                enabled = false;
                return;
            }

            moveAction = inputActions.FindAction("Player/Move");
            lookAction = inputActions.FindAction("Player/Look");
            jumpAction = inputActions.FindAction("Player/Jump");
            if (moveAction == null || lookAction == null || jumpAction == null)
                Debug.LogError("[PlayerController] Could not find 'Player/Move', 'Player/Look' or 'Player/Jump' " +
                               "in the input actions asset.", this);
        }

        private void OnEnable()
        {
            if (inputActions != null)
            {
                InputActionMap map = inputActions.FindActionMap("Player");
                if (map != null) map.Enable();
            }
            SetCursorLocked(true);
        }

        private void OnDisable() => SetCursorLocked(false);

        private void Update()
        {
            HandleCursor();
            HandleLook();
            HandleMove();
        }

        private void HandleCursor()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked &&
                     Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked || lookAction == null || cameraTransform == null)
                return;

            Vector2 delta = lookAction.ReadValue<Vector2>();

            // Yaw on the player root, pitch (clamped) on the camera.
            transform.Rotate(Vector3.up, delta.x * mouseSensitivity);
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -pitchLimit, pitchLimit);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMove()
        {
            Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 planar = transform.right * input.x + transform.forward * input.y;
            if (planar.sqrMagnitude > 1f)
                planar.Normalize();

            if (controller.isGrounded)
            {
                verticalVelocity = -2f; // small downward bias keeps the capsule grounded
                if (jumpAction != null && jumpAction.WasPressedThisFrame() &&
                    Cursor.lockState == CursorLockMode.Locked)
                {
                    // v = sqrt(2 * h * g): launch speed needed to reach jumpHeight.
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = planar * walkSpeed + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
