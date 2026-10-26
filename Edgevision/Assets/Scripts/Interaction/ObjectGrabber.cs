using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Edgevision
{
    /// <summary>
    /// Handles object interaction: raycasts from the center of the camera,
    /// picks up / releases ObjectGrabbable objects with left click, and adjusts
    /// the hold distance with the mouse scroll wheel.
    ///
    /// While an object is held, its PerspectiveScaler keeps the apparent size
    /// constant (S2 = S1 * D2 / D1) and two safety checks keep it out of
    /// geometry: a sphere cast clamps the distance along the view direction,
    /// and an overlap box prevents growing sideways into walls.
    /// </summary>
    public class ObjectGrabber : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private ObjectHighlight highlight;
        [SerializeField] private GameUI ui;

        [Header("Targeting")]
        [SerializeField] private float interactionRange = 6f;
        [Tooltip("Layer mask containing grabbable objects (the 'Grabbable' layer).")]
        [SerializeField] private LayerMask grabbableMask;
        [Tooltip("Layers treated as solid obstructions (walls, floor, ledge).")]
        [SerializeField] private LayerMask obstructionMask;

        [Header("Holding")]
        [SerializeField] private float minHoldDistance = 1f;
        [SerializeField] private float maxHoldDistance = 8f;
        [Tooltip("Hold-distance change per scroll-wheel notch.")]
        [SerializeField] private float scrollSpeed = 1.5f;
        [Tooltip("Margin kept between the held object and obstructions.")]
        [SerializeField] private float wallMargin = 0.05f;

        private InputAction attackAction;

        private ObjectGrabbable currentTarget;
        private ObjectGrabbable held;
        private PerspectiveScaler heldScaler;
        private float holdDistance;
        private float lastSafeDistance;
        private bool wasCursorLocked;

        public bool IsHolding => held != null;

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main;
            if (playerCamera == null)
                Debug.LogError("[ObjectGrabber] No camera assigned or found.", this);

            if (inputActions == null)
            {
                Debug.LogError("[ObjectGrabber] Input actions asset not assigned. " +
                               "Run Edgevision > Build Prototype Room or assign InputSystem_Actions.", this);
                enabled = false;
                return;
            }

            attackAction = inputActions.FindAction("Player/Attack");
            if (attackAction == null)
                Debug.LogError("[ObjectGrabber] 'Player/Attack' action not found in the input actions asset.", this);
        }

        private void OnEnable()
        {
            if (inputActions != null)
            {
                InputActionMap map = inputActions.FindActionMap("Player");
                if (map != null) map.Enable();
            }
        }

        private void Update()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool justLocked = locked && !wasCursorLocked;
            wasCursorLocked = locked;

            // Interaction is blocked while the cursor is free, on the frame it
            // re-locks (so the locking click doesn't grab), and over UI elements.
            bool canInteract = locked && !justLocked &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());

            if (held == null)
            {
                UpdateTargeting(canInteract);
                if (canInteract && currentTarget != null && GrabPressedThisFrame())
                    Grab(currentTarget);
            }
            else
            {
                UpdateHoldDistance();
                // Releasing also happens if the cursor unlocks (e.g. Escape).
                if (!canInteract || GrabPressedThisFrame())
                    Release();
            }
        }

        private bool GrabPressedThisFrame()
        {
            if (attackAction != null)
                return attackAction.WasPressedThisFrame();
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private void UpdateTargeting(bool canInteract)
        {
            ObjectGrabbable newTarget = null;

            if (canInteract && playerCamera != null)
            {
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, grabbableMask, QueryTriggerInteraction.Ignore))
                    newTarget = hit.collider.GetComponentInParent<ObjectGrabbable>();
            }

            if (newTarget == currentTarget)
                return;

            currentTarget = newTarget;
            if (highlight != null)
                highlight.SetTarget(currentTarget != null ? currentTarget.MainRenderer : null);
            if (ui != null)
                ui.SetTargeting(currentTarget != null);
        }

        private void Grab(ObjectGrabbable grabbable)
        {
            if (playerCamera == null)
                return;

            held = grabbable;
            heldScaler = grabbable.GetComponent<PerspectiveScaler>();

            float distance = Vector3.Distance(playerCamera.transform.position, grabbable.transform.position);
            holdDistance = Mathf.Clamp(distance, minHoldDistance, maxHoldDistance);
            lastSafeDistance = holdDistance;

            grabbable.OnGrabbed();
            if (heldScaler != null)
                heldScaler.BeginHold(holdDistance); // reference scale + distance captured here
            else
                Debug.LogWarning($"[ObjectGrabber] '{grabbable.name}' has no PerspectiveScaler; it will not resize.", grabbable);

            currentTarget = null;
            if (highlight != null) highlight.SetTarget(null);
            if (ui != null) ui.SetHolding(true);
        }

        /// <summary>Release the held object. Its final world scale persists.</summary>
        public void Release()
        {
            if (held == null)
                return;

            if (heldScaler != null)
                heldScaler.EndHold(); // final scale stays on the transform
            held.OnReleased();

            held = null;
            heldScaler = null;
            if (ui != null) ui.SetHolding(false);
        }

        private void UpdateHoldDistance()
        {
            if (Mouse.current == null)
                return;
            float notches = Mouse.current.scroll.ReadValue().y / 120f; // 120 = one wheel notch
            if (!Mathf.Approximately(notches, 0f))
                holdDistance = Mathf.Clamp(holdDistance + notches * scrollSpeed, minHoldDistance, maxHoldDistance);
        }

        private void FixedUpdate()
        {
            if (held == null || playerCamera == null)
                return;

            Transform cam = playerCamera.transform;
            Vector3 origin = cam.position;
            Vector3 forward = cam.forward;

            // 1) Scale factor for the desired distance (the S2 = S1 * D2 / D1
            //    computation itself lives in PerspectiveScaler).
            float factor = heldScaler != null ? heldScaler.ComputeScaleFactor(holdDistance) : 1f;
            Vector3 halfExtents = ComputeHalfExtents(factor);

            // Keep the camera outside the cube: floor the distance by its scaled size.
            float distanceFloor = Mathf.Max(minHoldDistance, halfExtents.magnitude * 0.5f);
            if (holdDistance < distanceFloor)
            {
                holdDistance = distanceFloor;
                factor = heldScaler != null ? heldScaler.ComputeScaleFactor(holdDistance) : factor;
                halfExtents = ComputeHalfExtents(factor);
            }

            // 2) Clamp the distance against geometry along the view direction
            //    (sphere radius = smallest scaled half-extent, conservative).
            float radius = Mathf.Max(0.05f, Mathf.Min(halfExtents.x, Mathf.Min(halfExtents.y, halfExtents.z)) - wallMargin);
            float allowedDistance = holdDistance;
            if (Physics.SphereCast(origin, radius, forward, out RaycastHit hit, holdDistance, obstructionMask, QueryTriggerInteraction.Ignore))
                allowedDistance = Mathf.Max(distanceFloor, hit.distance - wallMargin);

            // 3) Overlap check at the target spot: never grow into a wall.
            //    If it would overlap, stay at the last known-safe distance.
            float allowedFactor = heldScaler != null ? heldScaler.ComputeScaleFactor(allowedDistance) : factor;
            Vector3 targetPosition = origin + forward * allowedDistance;
            if (WouldOverlap(targetPosition, ComputeHalfExtents(allowedFactor)))
            {
                allowedDistance = lastSafeDistance;
                allowedFactor = heldScaler != null ? heldScaler.ComputeScaleFactor(allowedDistance) : 1f;
                targetPosition = origin + forward * allowedDistance;
            }
            else
            {
                lastSafeDistance = allowedDistance;
            }

            // 4) Apply scale (transform scaling scales the BoxCollider too), then move.
            if (heldScaler != null)
                heldScaler.ApplyScaleFactor(allowedFactor);

            // Kinematic MovePosition complies with collision: the cube slides along
            // geometry instead of tunnelling, and remains a solid body the player
            // can stand on after release.
            held.Body.MovePosition(targetPosition);
        }

        private Vector3 ComputeHalfExtents(float factor)
        {
            if (held != null && heldScaler != null && held.MainCollider is BoxCollider box)
                return Vector3.Scale(box.size * 0.5f, heldScaler.ReferenceScale) * factor;
            return Vector3.one * 0.25f * Mathf.Max(0.01f, factor);
        }

        private bool WouldOverlap(Vector3 position, Vector3 halfExtents)
        {
            Collider[] overlaps = Physics.OverlapBox(position, halfExtents, Quaternion.identity, obstructionMask, QueryTriggerInteraction.Ignore);
            foreach (Collider c in overlaps)
            {
                if (held != null && c.transform.root == held.transform.root)
                    continue; // never count our own collider
                return true;
            }
            return false;
        }
    }
}
