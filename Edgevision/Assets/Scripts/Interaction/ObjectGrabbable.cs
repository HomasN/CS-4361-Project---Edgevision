using UnityEngine;

namespace Edgevision
{
    /// <summary>
    /// Marks an object as grabbable and manages its physics state.
    ///
    /// While held (Superliminal-style) the object is a pure "ghost": kinematic,
    /// colliders disabled and moved to the Ignore Raycast layer, so it never
    /// pushes the player and never blocks the placement raycasts that
    /// ObjectGrabber fires through it. On release it becomes solid again and
    /// (by default) a normal dynamic rigidbody that falls under gravity, keeping
    /// whatever size it was given.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class ObjectGrabbable : MonoBehaviour
    {
        private const int IgnoreRaycastLayer = 2;

        [Tooltip("If true the object falls with gravity when released (like Superliminal). " +
                 "If false it freezes in place where it was let go.")]
        [SerializeField] private bool dropWithGravity = true;
        [Tooltip("Scale rigidbody mass with volume so big objects feel heavy.")]
        [SerializeField] private bool scaleMassWithVolume = true;

        public Rigidbody Body { get; private set; }
        public Collider MainCollider { get; private set; }
        public Renderer MainRenderer { get; private set; }

        /// <summary>Scale the object had when the scene started.</summary>
        public Vector3 OriginalScale { get; private set; }

        public bool IsHeld { get; private set; }

        private Collider[] colliders;
        private bool[] colliderWasEnabled;
        private Transform[] layerTransforms;
        private int[] originalLayers;
        private float baseMass;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            MainCollider = GetComponent<Collider>();
            MainRenderer = GetComponentInChildren<Renderer>();
            OriginalScale = transform.lossyScale;
            baseMass = Body.mass;

            Body.isKinematic = !dropWithGravity;
            Body.useGravity = true;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void Reset()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public void OnGrabbed()
        {
            IsHeld = true;

            // World-space scale must equal localScale for the scaling math.
            if (transform.parent != null)
                transform.SetParent(null, true);

            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None; // transform is driven directly

            colliders = GetComponentsInChildren<Collider>();
            colliderWasEnabled = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderWasEnabled[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }

            layerTransforms = GetComponentsInChildren<Transform>(true);
            originalLayers = new int[layerTransforms.Length];
            for (int i = 0; i < layerTransforms.Length; i++)
            {
                originalLayers[i] = layerTransforms[i].gameObject.layer;
                layerTransforms[i].gameObject.layer = IgnoreRaycastLayer;
            }
        }

        /// <summary>Make the object solid again. Its final scale persists.</summary>
        public void OnReleased()
        {
            IsHeld = false;

            if (layerTransforms != null)
                for (int i = 0; i < layerTransforms.Length; i++)
                    if (layerTransforms[i] != null)
                        layerTransforms[i].gameObject.layer = originalLayers[i];

            if (colliders != null)
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null)
                        colliders[i].enabled = colliderWasEnabled[i];

            Physics.SyncTransforms();

            if (scaleMassWithVolume)
            {
                Vector3 s = transform.lossyScale;
                float volumeRatio = Mathf.Abs(s.x * s.y * s.z) /
                                    Mathf.Max(1e-6f, Mathf.Abs(OriginalScale.x * OriginalScale.y * OriginalScale.z));
                Body.mass = Mathf.Clamp(baseMass * volumeRatio, 0.05f, 10000f);
            }

            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.isKinematic = !dropWithGravity;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
                Body.WakeUp();
            }
        }
    }
}
