using UnityEngine;
using UnityEngine.UI;

namespace Edgevision
{
    /// <summary>
    /// HUD for the prototype: a center crosshair that tints when a grabbable
    /// object is targeted, a context-sensitive prompt line, and a completion
    /// overlay shown by GoalTrigger.
    /// Uses legacy uGUI Text so no extra packages or font assets are required.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Graphic crosshair;
        [SerializeField] private Text promptText;
        [SerializeField] private GameObject completionRoot;

        [Header("Crosshair")]
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField] private Color targetColor = new Color(1f, 0.85f, 0.3f, 1f);

        [Header("Prompts")]
        [TextArea]
        [SerializeField] private string explorePrompt = "WASD move · Space jump\nAim at a cube and LMB to grab";
        [TextArea]
        [SerializeField] private string targetPrompt = "LMB: grab";
        [TextArea]
        [SerializeField] private string holdingPrompt = "Scroll: push/pull (resizes) · LMB: release";

        private bool targeting;
        private bool holding;

        private void Awake()
        {
            if (completionRoot != null)
                completionRoot.SetActive(false);
            Refresh();
        }

        public void SetTargeting(bool value)
        {
            targeting = value;
            Refresh();
        }

        public void SetHolding(bool value)
        {
            holding = value;
            if (value) targeting = false;
            Refresh();
        }

        public void ShowCompletion()
        {
            if (completionRoot != null)
                completionRoot.SetActive(true);
            if (promptText != null)
                promptText.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            if (crosshair != null)
                crosshair.color = targeting && !holding ? targetColor : idleColor;
            if (promptText != null)
                promptText.text = holding ? holdingPrompt : (targeting ? targetPrompt : explorePrompt);
        }
    }
}
