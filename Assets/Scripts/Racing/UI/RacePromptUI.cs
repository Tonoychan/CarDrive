using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Racing.UI
{
    public class RacePromptUI : MonoBehaviour
    {
        public static RacePromptUI Instance { get; private set; }

        public GameObject panel;
        public TMP_Text titleText;
        public TMP_Text detailsText;
        public Button startButton;
        public Button cancelButton;

        RaceTrigger active;

        void Awake()
        {
            Instance = this;
            if (panel != null) panel.SetActive(false);
            if (startButton != null) startButton.onClick.AddListener(OnStartPressed);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelPressed);
        }

        void Update()
        {
            if (active == null) return;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                OnCancelPressed();
#endif
        }

        public void Show(RaceTrigger trigger)
        {
            active = trigger;
            if (panel != null) panel.SetActive(true);

            var def = trigger.Course != null ? trigger.Course.definition : null;
            if (titleText != null)
                titleText.text = def != null && !string.IsNullOrEmpty(def.raceName) ? def.raceName : trigger.promptLabel;
            if (detailsText != null)
            {
                int opponents = def != null ? def.opponentCount : 0;
                float countdown = def != null ? def.countdownSeconds : 3f;
                string desc = def != null && !string.IsNullOrEmpty(def.description) ? def.description + "\n\n" : "";
                detailsText.text = $"{desc}{opponents} opponent{(opponents == 1 ? "" : "s")} - {countdown:0}s countdown";
            }
        }

        public void Hide(RaceTrigger trigger)
        {
            if (active != trigger) return;
            active = null;
            if (panel != null) panel.SetActive(false);
        }

        void OnStartPressed()
        {
            active?.Confirm();
        }

        void OnCancelPressed()
        {
            active?.Cancel();
        }
    }
}
