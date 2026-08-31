using UnityEngine;
using MVC.Core;
using Racing.UI;

namespace Racing
{
    /// Lives on its own GameObject (typically a child of the RaceCourse, e.g.
    /// "TriggerArea") and auto-wires to the nearest RaceCourse up the hierarchy via
    /// GetComponentInParent -- no manual dragging. This keeps the trigger volume free
    /// to be placed/sized independently of the course's root object while the whole
    /// race (course data, path, opponent presets, trigger) still ships as one
    /// self-contained prefab.
    [RequireComponent(typeof(Collider))]
    public class RaceTrigger : MonoBehaviour
    {
        public string promptLabel = "Start Race";

        [Tooltip("How long the player must stay inside the trigger before the start " +
                 "popup appears.")]
        [Min(0f)] public float hoverSecondsToPrompt = 1.5f;

        [Tooltip("Master gate for this trigger -- set false to disable it entirely " +
                 "(e.g. from a progression/unlock system) without touching the " +
                 "GameObject's active state.")]
        public bool isEnabled = true;

        public RaceCourse Course { get; private set; }

        Vehicle playerInTrigger;
        float hoverTimer;
        bool promptShown;

        void Awake()
        {
            Course = GetComponentInParent<RaceCourse>();
        }

        void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isEnabled) return;
            if (RaceManager.Instance != null && RaceManager.Instance.State != RaceState.Idle) return;

            var vehicle = other.GetComponentInParent<Vehicle>();
            if (vehicle == null || vehicle.HasAI) return;

            playerInTrigger = vehicle;
            hoverTimer = 0f;
            promptShown = false;
        }

        void Update()
        {
            if (!isEnabled || playerInTrigger == null || promptShown) return;

            hoverTimer += Time.deltaTime;
            if (hoverTimer >= hoverSecondsToPrompt)
            {
                promptShown = true;
                RacePromptUI.Instance?.Show(this);
            }
        }

        void OnTriggerExit(Collider other)
        {
            var vehicle = other.GetComponentInParent<Vehicle>();
            if (vehicle == null || vehicle != playerInTrigger) return;

            playerInTrigger = null;
            hoverTimer = 0f;
            if (promptShown)
            {
                promptShown = false;
                RacePromptUI.Instance?.Hide(this);
            }
        }

        public void Confirm()
        {
            if (Course == null || RaceManager.Instance == null) return;
            RaceManager.Instance.BeginRace(Course);
            promptShown = false;
            RacePromptUI.Instance?.Hide(this);
        }

        public void Cancel()
        {
            promptShown = false;
            RacePromptUI.Instance?.Hide(this);
        }
    }
}
