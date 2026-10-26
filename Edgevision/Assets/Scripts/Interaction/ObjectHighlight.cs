using UnityEngine;

namespace Edgevision
{
    /// <summary>
    /// Tints the currently targeted grabbable object using a MaterialPropertyBlock,
    /// so no material instances are created at runtime and the tint restores
    /// cleanly when the target changes. ObjectGrabber drives SetTarget().
    /// </summary>
    public class ObjectHighlight : MonoBehaviour
    {
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField, Range(0f, 1f)] private float blend = 0.55f;

        // URP Lit base color property.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer current;
        private MaterialPropertyBlock mpb;

        private void Awake() => mpb = new MaterialPropertyBlock();

        /// <summary>Set the renderer to highlight, or null to clear.</summary>
        public void SetTarget(Renderer renderer)
        {
            if (renderer == current)
                return;

            ClearCurrent();
            current = renderer;

            if (current != null)
            {
                Color baseColor = Color.white;
                if (current.sharedMaterial != null && current.sharedMaterial.HasProperty(BaseColorId))
                    baseColor = current.sharedMaterial.GetColor(BaseColorId);

                mpb.SetColor(BaseColorId, Color.Lerp(baseColor, highlightColor, blend));
                current.SetPropertyBlock(mpb);
            }
        }

        private void ClearCurrent()
        {
            if (current == null)
                return;
            mpb.Clear(); // clearing the block restores the material's own color
            current.SetPropertyBlock(mpb);
            current = null;
        }

        private void OnDisable() => ClearCurrent();
    }
}
