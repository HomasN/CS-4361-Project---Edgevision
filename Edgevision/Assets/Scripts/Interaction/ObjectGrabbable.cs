using UnityEngine;

namespace Edgevision
{
    /// <summary>
    /// Marks an object as grabbable and caches everything the grab system needs.
    ///
    /// The object uses a KINEMATIC rigidbody at all times: while held it is moved
    /// with Rigidbody.MovePosition (which complies with collision, so it slides
    /// along geometry instead of tunnelling), and when released it simply stays
    /// where it is — a stable, solid step the player can stand on.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class ObjectGrabbable : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public Collider MainCollider { get; private set; }
        public Renderer MainRenderer { get; private set; }

        /// <summary>Scale the object had when the scene started (for reference/reset).</summary>
        public Vector3 OriginalScale { get; private set; }

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            MainCollider = GetComponent<Collider>();
            MainRenderer = GetComponentInChildren<Renderer>();
            OriginalScale = transform.localScale;

            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            // Speculative continuous is cheap and reliable for kinematic bodies.
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        // Editor convenience: correct rigidbody setup when the component is added in the Inspector.
        private void Reset()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void OnGrabbed() => IsHeld = true;

        /// <summary>Nothing is reset on release: the final world scale persists.</summary>
        public void OnReleased() => IsHeld = false;
    }
}
