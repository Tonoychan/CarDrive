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
        public TMP_Text label;
        public Button confirmButton;

        RaceTrigger active;

        void Awake()
        {
            Instance = this;
            if (panel != null) panel.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmPressed);
        }

        void Update()
        {
            if (active == null) return;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                OnConfirmPressed();
#endif
        }

        public void Show(RaceTrigger trigger)
        {
            active = trigger;
            if (panel != null) panel.SetActive(true);
            if (label != null) label.text = $"Press E or tap to start: {trigger.promptLabel}";
        }

        public void Hide(RaceTrigger trigger)
        {
            if (active != trigger) return;
            active = null;
            if (panel != null) panel.SetActive(false);
        }

        void OnConfirmPressed()
        {
            active?.Confirm();
        }
    }
}
