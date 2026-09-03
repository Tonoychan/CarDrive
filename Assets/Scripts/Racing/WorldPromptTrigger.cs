using UnityEngine;
using MVC.Core;
using Racing.UI;

namespace Racing
{
    /// Shared hover-then-confirm-popup behaviour for world triggers (start a race,
    /// open the garage, open the shop, ...). Handles the hover timer, showing/hiding
    /// RacePromptUI, and the trigger collider plumbing; subclasses just provide the
    /// popup's details text and what happens on Confirm().
    [RequireComponent(typeof(Collider))]
    public abstract class WorldPromptTrigger : MonoBehaviour, IWorldPromptTrigger
    {
        public string promptLabel = "Interact";

        [Tooltip("Popup panel this trigger shows on hover. Each trigger points at its " +
                 "own RacePromptUI instance/panel (Race/Garage/Shop each have their " +
                 "own), so multiple different-looking popups can be on screen at once " +
                 "as long as only one trigger is active at a time.")]
        public RacePromptUI promptUI;

        [Tooltip("How long the player must stay inside the trigger before the start " +
                 "popup appears.")]
        [Min(0f)] public float hoverSecondsToPrompt = 1.5f;

        [Tooltip("Master gate for this trigger -- set false to disable it entirely " +
                 "(e.g. from a progression/unlock system) without touching the " +
                 "GameObject's active state.")]
        public bool isEnabled = true;

        public virtual string PromptLabel => promptLabel;
        public abstract string PromptDetails { get; }
        public virtual string TriggerKind => "TRIGGER";
        public virtual string ActionLabel => "START";
        public virtual string HeaderRight => null;

        Vehicle playerInTrigger;
        float hoverTimer;
        bool promptShown;

        protected virtual void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isEnabled || !CanInteract()) return;

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
                promptUI?.Show(this);
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
                promptUI?.Hide(this);
            }
        }

        public void Confirm()
        {
            OnConfirmed();
            promptShown = false;
            promptUI?.Hide(this);
        }

        public void Cancel()
        {
            promptShown = false;
            promptUI?.Hide(this);
        }

        /// Whether this trigger can currently be interacted with. Defaults to
        /// "no race in progress" -- swapping cars, shopping, or starting another race
        /// mid-race isn't something any of these triggers should allow.
        protected virtual bool CanInteract()
            => RaceManager.Instance == null || RaceManager.Instance.State == RaceState.Idle;

        protected abstract void OnConfirmed();
    }
}
