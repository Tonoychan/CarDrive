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

        Vehicle playerInTrigger;

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
            RacePromptUI.Instance?.Show(this);
        }

        void OnTriggerExit(Collider other)
        {
            var vehicle = other.GetComponentInParent<Vehicle>();
            if (vehicle == null || vehicle != playerInTrigger) return;

            playerInTrigger = null;
            RacePromptUI.Instance?.Hide(this);
        }

        public void Confirm()
        {
            if (course == null || RaceManager.Instance == null) return;
            RaceManager.Instance.BeginRace(course);
            RacePromptUI.Instance?.Hide(this);
        }
    }
}
