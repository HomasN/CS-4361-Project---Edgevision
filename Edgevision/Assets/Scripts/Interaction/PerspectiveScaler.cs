using UnityEngine;

namespace Edgevision
{
    /// <summary>
    /// Forced-perspective scaling — the core Edgevision mechanic.
    ///
    /// THE MATH
    /// --------
    /// For a perspective camera, the projected on-screen size of an object is
    /// approximately proportional to its world size divided by its distance:
    ///
    ///     screenSize ∝ worldSize / distance
    ///
    /// To keep the apparent size constant while the hold distance changes, the
    /// physical scale must therefore satisfy S1 / D1 = S2 / D2, which gives:
    ///
    ///     S2 = S1 * (D2 / D1)
    ///
    /// S1 (<see cref="ReferenceScale"/>) and D1 (<see cref="ReferenceDistance"/>)
    /// are captured ONCE at pickup. Every frame the new scale is recomputed from
    /// that fixed reference pair — NEVER by multiplying the current scale — so
    /// floating-point error cannot accumulate into runaway growth/shrink
    /// (no exponential drift), and repeated pickup/release cycles stay stable.
    ///
    /// This demonstrates the graphics concept that changing an object's physical
    /// size together with its distance can produce an identical projected size.
    /// (Approximation: works best for moderate FOV and distances measured
    /// consistently from the camera position, which is what we do.)
    ///
    /// COLLIDERS
    /// ---------
    /// Uniform transform scaling scales the attached BoxCollider with it, so the
    /// collider always matches the visual size. The companion ObjectGrabber
    /// overlap-checks before applying growth so the object can't pop into walls.
    /// </summary>
    public class PerspectiveScaler : MonoBehaviour
    {
        [Header("Scale limits (multipliers of the pickup scale)")]
        [Min(0.01f)]
        [SerializeField] private float minScaleFactor = 0.2f;
        [Min(0.01f)]
        [SerializeField] private float maxScaleFactor = 5f;

        [Header("Safety")]
        [Tooltip("Absolute floor for any world scale axis; prevents degenerate/zero scale.")]
        [SerializeField] private float minAbsoluteAxisScale = 0.05f;

        /// <summary>World scale captured at pickup. Constant while held.</summary>
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

        /// <summary>
        /// Returns the scale factor for a camera distance: factor = D2 / D1,
        /// clamped to [minScaleFactor, maxScaleFactor] and to the absolute
        /// per-axis safety floor.
        /// </summary>
        public float ComputeScaleFactor(float currentDistance)
        {
            float factor = Mathf.Max(0.01f, currentDistance) / ReferenceDistance;

            // Never let any axis drop below the absolute safety floor.
            float smallestRefAxis = Mathf.Min(ReferenceScale.x, Mathf.Min(ReferenceScale.y, ReferenceScale.z));
            float absoluteFloor = smallestRefAxis > 0f
                ? minAbsoluteAxisScale / smallestRefAxis
                : minScaleFactor;

            return Mathf.Clamp(factor, Mathf.Max(minScaleFactor, absoluteFloor), maxScaleFactor);
        }

        /// <summary>
        /// Applies ReferenceScale * factor to the transform. Guards against
        /// zero/negative scale so the object can never become degenerate.
        /// </summary>
        public void ApplyScaleFactor(float factor)
        {
            Vector3 newScale = ReferenceScale * factor;
            if (newScale.x <= 0f || newScale.y <= 0f || newScale.z <= 0f)
            {
                Debug.LogWarning($"[PerspectiveScaler] Refused invalid scale {newScale} on '{name}'.", this);
                return;
            }
            transform.localScale = newScale;
        }

        /// <summary>
        /// Release: the final world scale is simply left on the transform —
        /// nothing is reset, so the resized object persists and stays interactable.
        /// </summary>
        public void EndHold() => IsHeld = false;
    }
}
