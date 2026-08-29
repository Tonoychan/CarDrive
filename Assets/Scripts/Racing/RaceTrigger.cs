using UnityEngine;
using MVC.Core;
using Racing.UI;

namespace Racing
{
    [RequireComponent(typeof(Collider))]
    public class RaceTrigger : MonoBehaviour
    {
        public RaceCourse course;
        public string promptLabel = "Start Race";

        [Tooltip("How long the player must stay inside the trigger before the start " +
                 "popup appears.")]
        [Min(0f)] public float hoverSecondsToPrompt = 1.5f;

        Vehicle playerInTrigger;
        float hoverTimer;
        bool promptShown;

        void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (RaceManager.Instance != null && RaceManager.Instance.State != RaceState.Idle) return;

            var vehicle = other.GetComponentInParent<Vehicle>();
            if (vehicle == null || vehicle.HasAI) return;

            playerInTrigger = vehicle;
            hoverTimer = 0f;
            promptShown = false;
        }

        void Update()
        {
            if (playerInTrigger == null || promptShown) return;

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
            if (course == null || RaceManager.Instance == null) return;
            RaceManager.Instance.BeginRace(course);
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
