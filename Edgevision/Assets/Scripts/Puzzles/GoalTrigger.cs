using UnityEngine;
using UnityEngine.Events;

namespace Edgevision
{
    /// <summary>
    /// Trigger zone marking the exit of the test room. Fires once when the
    /// player enters: shows the completion message and invokes onGoalReached
    /// for future extensibility (scene loads, sounds, etc.).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GoalTrigger : MonoBehaviour
    {
        [SerializeField] private GameUI ui;
        [SerializeField] private UnityEvent onGoalReached;

        private bool triggered;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning("[GoalTrigger] Collider should be a trigger.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered)
                return;
            // Accept the "Player" tag or any object with a PlayerController in its hierarchy.
            if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerController>() == null)
                return;

            triggered = true;
            if (ui != null)
                ui.ShowCompletion();
            onGoalReached?.Invoke();
            Debug.Log("[GoalTrigger] Player reached the exit — room complete!");
        }
    }
}
