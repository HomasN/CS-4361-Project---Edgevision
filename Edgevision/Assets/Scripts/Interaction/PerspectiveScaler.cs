using UnityEngine;

namespace Edgevision
{
    /// <summary>
    /// Forced-perspective scaling — the core Edgevision mechanic.
    ///
    /// THE MATH
    /// --------
    /// For a perspective camera, the projected on-screen size of an object is
    /// proportional to its world size divided by its distance from the camera:
    ///
    ///     screenSize ∝ worldSize / distance
    ///
    /// To keep the apparent size constant while the distance changes, the
    /// physical scale must satisfy S1 / D1 = S2 / D2, which gives:
    ///
    ///     S2 = S1 * (D2 / D1)
    ///
    /// S1 (<see cref="ReferenceScale"/>) and D1 (<see cref="ReferenceDistance"/>)
    /// are captured ONCE at pickup. Every frame the scale is recomputed from that
    /// fixed pair — never by multiplying the current scale — so floating-point
    /// error cannot accumulate into runaway growth or shrink.
    ///
    /// Because scale grows linearly with distance, the held object at distance
    /// D is an exact copy of the object at distance 1 scaled about the camera
    /// position by a factor D. Every point of the object therefore slides along
    /// its own ray from the camera, which is what lets ObjectGrabber find the
    /// farthest valid placement with plain raycasts (Superliminal-style).
    /// </summary>
    public class PerspectiveScaler : MonoBehaviour
    {
        [Header("Absolute scale limits (world scale, per axis)")]
        [Tooltip("No axis may shrink below this world scale.")]
        [Min(0.001f)]
        [SerializeField] private float minAbsoluteAxisScale = 0.05f;
        [Tooltip("No axis may grow beyond this world scale.")]
        [Min(0.01f)]
        [SerializeField] private float maxAbsoluteAxisScale = 40f;

        /// <summary>Scale captured at pickup. Constant while held.</summary>
        public Vector3 ReferenceScale { get; private set; }

        /// <summary>Camera distance captured at pickup. Constant while held.</summary>
        public float ReferenceDistance { get; private set; }

        public bool IsHeld { get; private set; }

        /// <summary>Capture the fixed reference pair at the moment of pickup.</summary>
        public void BeginHold(float distanceToCamera)
        {
            ReferenceScale = transform.localScale;
            ReferenceDistance = Mathf.Max(0.01f, distanceToCamera);
            IsHeld = true;
        }

        /// <summary>S2 = S1 * (D2 / D1).</summary>
        public Vector3 ScaleForDistance(float distance)
        {
            return ReferenceScale * (Mathf.Max(0.0001f, distance) / ReferenceDistance);
        }

        /// <summary>Smallest distance at which no axis drops below the minimum scale.</summary>
        public float MinDistance
        {
            get
            {
                float smallest = Mathf.Min(ReferenceScale.x, Mathf.Min(ReferenceScale.y, ReferenceScale.z));
                return smallest > 0f ? ReferenceDistance * (minAbsoluteAxisScale / smallest) : 0f;
            }
        }

        /// <summary>Largest distance at which no axis exceeds the maximum scale.</summary>
        public float MaxDistance
        {
            get
            {
                float largest = Mathf.Max(ReferenceScale.x, Mathf.Max(ReferenceScale.y, ReferenceScale.z));
                return largest > 0f ? ReferenceDistance * (maxAbsoluteAxisScale / largest) : float.MaxValue;
            }
        }

        /// <summary>Apply the scale for a distance, refusing degenerate values.</summary>
        public void ApplyForDistance(float distance)
        {
            Vector3 newScale = ScaleForDistance(distance);
            if (newScale.x <= 0f || newScale.y <= 0f || newScale.z <= 0f ||
                float.IsNaN(newScale.x) || float.IsNaN(newScale.y) || float.IsNaN(newScale.z))
            {
                Debug.LogWarning($"[PerspectiveScaler] Refused invalid scale {newScale} on '{name}'.", this);
                return;
            }
            transform.localScale = newScale;
        }

        /// <summary>Release: the final scale is left on the transform and persists.</summary>
        public void EndHold() => IsHeld = false;
    }
}
