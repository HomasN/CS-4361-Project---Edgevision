using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Edgevision
{
    /// <summary>
    /// Superliminal-style forced-perspective pick up / let go.
    ///
    /// PICK UP: left click while aiming at an ObjectGrabbable. The object's
    /// apparent (on-screen) size at that moment is locked in.
    ///
    /// HOLD: every frame the object is centred on the crosshair and pushed as
    /// FAR AWAY as possible along the view until it touches the scenery behind
    /// it, while its scale is set to S = S0 * D / D0 so it looks exactly the
    /// same size on screen. Aim at a far wall → it becomes huge; aim at the
    /// floor near your feet → it becomes tiny. Its rotation follows the
    /// player's yaw only, so turning keeps the same face toward you, while
    /// looking up / down reveals its bottom / top.
    ///
    /// LET GO: left click again. The object keeps its new size and position
    /// and drops under gravity.
    ///
    /// HOW THE FARTHEST SPOT IS FOUND
    /// Because scale is proportional to distance, the object at distance t is
    /// the object at distance 1 scaled by t about the camera. Every point p on
    /// the object therefore moves along its own ray from the camera:
    ///     p(t) = camera + t * v_p
    /// Raycasting along v_p and dividing the hit distance by |v_p| gives the
    /// largest t that point allows; the minimum over a grid of surface points
    /// is the farthest valid placement. A box overlap check then guards
    /// against thin geometry slipping between the sample rays.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ObjectGrabber : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private ObjectHighlight highlight;
        [SerializeField] private GameUI ui;

        [Header("Targeting")]
        [SerializeField] private float interactionRange = 15f;
        [Tooltip("Layer mask containing grabbable objects (the 'Grabbable' layer).")]
        [SerializeField] private LayerMask grabbableMask;
        [Tooltip("Layers treated as solid scenery (walls, floor, ledge). Other grabbables are added automatically.")]
        [SerializeField] private LayerMask obstructionMask;

        [Header("Placement")]
        [Tooltip("Closest the nearest point of the held object may get to the camera (along view depth).")]
        [SerializeField] private float minViewDepth = 0.3f;
        [Tooltip("How far the object may be pushed when nothing is behind it.")]
        [SerializeField] private float maxPlacementDistance = 150f;
        [Tooltip("Gap kept between the held object and the scenery it rests against.")]
        [SerializeField] private float wallMargin = 0.02f;
        [Tooltip("Sample points per box edge used for the placement raycasts (per face: n x n).")]
        [Range(2, 8)]
        [SerializeField] private int samplesPerEdge = 5;
        [Tooltip("Seconds for the object to slide onto the crosshair after pickup.")]
        [SerializeField] private float centerTime = 0.12f;

        private InputAction attackAction;

        private ObjectGrabbable currentTarget;
        private ObjectGrabbable held;
        private PerspectiveScaler heldScaler;

        private Quaternion yawRelativeRotation;  // object rotation relative to camera yaw
        private Vector3 pickupLocalDirection;    // object direction in camera space at pickup
        private float centerProgress;
        private float currentDistance;
        private readonly List<Vector3> localSamples = new List<Vector3>();
        private Vector3 localBoundsCenter;
        private Vector3 localBoundsSize;
        private bool wasCursorLocked;

        public bool IsHolding => held != null;

        private int PlacementMask => obstructionMask | grabbableMask;

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

        private void OnDisable() => Release();

        // ------------------------------------------------------------------ input

        private void Update()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool justLocked = locked && !wasCursorLocked;
            wasCursorLocked = locked;

            bool canInteract = locked && !justLocked &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());

            if (held == null)
            {
                UpdateTargeting(canInteract);
                if (canInteract && currentTarget != null && GrabPressedThisFrame())
                    Grab(currentTarget);
            }
            else if (!canInteract || GrabPressedThisFrame())
            {
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
                // Hit anything solid so objects behind walls can't be grabbed.
                if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, PlacementMask, QueryTriggerInteraction.Ignore))
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

        // ------------------------------------------------------------ grab/release

        private void Grab(ObjectGrabbable grabbable)
        {
            if (playerCamera == null)
                return;

            Transform cam = playerCamera.transform;
            held = grabbable;
            heldScaler = grabbable.GetComponent<PerspectiveScaler>();
            if (heldScaler == null)
                heldScaler = grabbable.gameObject.AddComponent<PerspectiveScaler>();

            grabbable.OnGrabbed(); // unparents, disables colliders, ignore-raycast layer

            Vector3 toObject = grabbable.transform.position - cam.position;
            float distance = Mathf.Max(0.05f, toObject.magnitude);
            heldScaler.BeginHold(distance); // S0 and D0 captured once
            currentDistance = distance;

            pickupLocalDirection = cam.InverseTransformDirection(toObject / distance);
            centerProgress = 0f;
            yawRelativeRotation = Quaternion.Inverse(YawRotation(cam)) * grabbable.transform.rotation;

            BuildSamples(grabbable);

            currentTarget = null;
            if (highlight != null) highlight.SetTarget(null);
            if (ui != null) ui.SetHolding(true);
        }

        /// <summary>Let go: the object keeps its size and drops.</summary>
        public void Release()
        {
            if (held == null)
                return;

            heldScaler.EndHold();
            held.OnReleased();

            held = null;
            heldScaler = null;
            if (ui != null) ui.SetHolding(false);
        }

        // --------------------------------------------------------------- placement

        // Runs after the player has moved and rotated the camera this frame,
        // so the held object never lags behind the view.
        private void LateUpdate()
        {
            if (held == null || playerCamera == null)
                return;

            Transform cam = playerCamera.transform;
            Vector3 origin = cam.position;
            Vector3 camForward = cam.forward;

            // Slide from the original screen position onto the crosshair.
            centerProgress = centerTime > 0f ? Mathf.Clamp01(centerProgress + Time.deltaTime / centerTime) : 1f;
            float k = Mathf.SmoothStep(0f, 1f, centerProgress);
            Vector3 dir = Vector3.Slerp(cam.TransformDirection(pickupLocalDirection), camForward, k).normalized;

            Quaternion rotation = YawRotation(cam) * yawRelativeRotation;
            Vector3 unitScale = heldScaler.ScaleForDistance(1f); // S0 / D0

            float t = FindFarthestDistance(origin, dir, camForward, rotation, unitScale);
            currentDistance = t;

            held.transform.SetPositionAndRotation(origin + dir * t, rotation);
            heldScaler.ApplyForDistance(t);
        }

        private float FindFarthestDistance(Vector3 origin, Vector3 dir, Vector3 camForward,
                                           Quaternion rotation, Vector3 unitScale)
        {
            int mask = PlacementMask;
            float tMax = Mathf.Min(maxPlacementDistance, heldScaler.MaxDistance);
            float minDepthPerUnit = float.MaxValue;
            float best = tMax;

            for (int i = 0; i < localSamples.Count; i++)
            {
                // Offset of this surface point from the camera when t = 1.
                Vector3 v = dir + rotation * Vector3.Scale(localSamples[i], unitScale);
                float depth = Vector3.Dot(v, camForward);
                if (depth < minDepthPerUnit) minDepthPerUnit = depth;
                if (depth <= 0.0001f)
                    continue; // point behind the camera — handled by tMin

                float len = v.magnitude;
                float maxRay = best * len;
                if (Physics.Raycast(origin, v / len, out RaycastHit hit, maxRay, mask, QueryTriggerInteraction.Ignore))
                {
                    float tPoint = Mathf.Max(0f, hit.distance - wallMargin) / len;
                    if (tPoint < best) best = tPoint;
                }
            }

            // Never let the object swallow the camera or drop below the scale floor.
            float tMin = heldScaler.MinDistance;
            if (minDepthPerUnit > 0.0001f)
                tMin = Mathf.Max(tMin, minViewDepth / minDepthPerUnit);
            else
                tMin = Mathf.Max(tMin, currentDistance);

            float t = Mathf.Max(tMin, best);

            // Safety net: thin geometry can slip between sample rays. Pull the
            // object toward the camera until its box is clear.
            if (t > tMin && Overlaps(origin, dir, rotation, unitScale, t, mask))
            {
                float lo = tMin, hi = t;
                for (int i = 0; i < 10; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    if (Overlaps(origin, dir, rotation, unitScale, mid, mask)) hi = mid;
                    else lo = mid;
                }
                t = lo;
            }
            return t;
        }

        private bool Overlaps(Vector3 origin, Vector3 dir, Quaternion rotation, Vector3 unitScale, float t, int mask)
        {
            Vector3 scale = unitScale * t;
            Vector3 center = origin + dir * t + rotation * Vector3.Scale(localBoundsCenter, scale);
            Vector3 half = Vector3.Scale(localBoundsSize * 0.5f, scale);
            half = new Vector3(Mathf.Max(0.001f, half.x - wallMargin),
                               Mathf.Max(0.001f, half.y - wallMargin),
                               Mathf.Max(0.001f, half.z - wallMargin));
            return Physics.CheckBox(center, half, rotation, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Grid of points on the object's local bounding box surface.</summary>
        private void BuildSamples(ObjectGrabbable grabbable)
        {
            GetLocalBounds(grabbable, out localBoundsCenter, out localBoundsSize);

            localSamples.Clear();
            int n = Mathf.Max(2, samplesPerEdge);
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, w = (axis + 2) % 3;
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            Vector3 p = Vector3.zero;
                            p[axis] = 0.5f * side;
                            p[u] = i / (float)(n - 1) - 0.5f;
                            p[w] = j / (float)(n - 1) - 0.5f;
                            localSamples.Add(localBoundsCenter + Vector3.Scale(p, localBoundsSize));
                        }
            }
            localSamples.Add(localBoundsCenter);
        }

        private static void GetLocalBounds(ObjectGrabbable grabbable, out Vector3 center, out Vector3 size)
        {
            if (grabbable.MainCollider is BoxCollider box)
            {
                center = box.center;
                size = box.size;
                return;
            }
            if (grabbable.MainCollider is MeshCollider mc && mc.sharedMesh != null)
            {
                center = mc.sharedMesh.bounds.center;
                size = mc.sharedMesh.bounds.size;
                return;
            }
            MeshFilter mf = grabbable.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                center = mf.sharedMesh.bounds.center;
                size = mf.sharedMesh.bounds.size;
                return;
            }
            center = Vector3.zero;
            size = Vector3.one;
        }

        private static Quaternion YawRotation(Transform cam) => Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
    }
}
