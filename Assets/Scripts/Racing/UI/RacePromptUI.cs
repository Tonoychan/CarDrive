using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Racing.UI
{
    /// Generic hover-popup panel: shows whatever IWorldPromptTrigger points at it.
    /// Not a singleton -- each WorldPromptTrigger is wired (in the Inspector) to the
    /// RacePromptUI instance it should show, so Race/Garage/Shop triggers can each
    /// drive their own instance. The "corner card" (Modernist design system) is
    /// authored once in the Editor -- via "Build UI (Editor Only)" below, never at
    /// runtime -- and saved into the scene, so it's visible/editable without Play
    /// Mode. This component only wires up the already-existing elements at runtime.
    public class RacePromptUI : MonoBehaviour
    {
        const float CardWidth = 840f;
        const float HeaderHeight = 74f;
        const float BodyHeightNoKicker = 214f;
        const float BodyHeightWithKicker = 250f;
        const float FooterHeight = 96f;

        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text headerKindText, headerRightText, kickerText, titleText, detailsText;
        [SerializeField] RectTransform headerRow, bodyRow;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startButtonLabel;
        [SerializeField] Button cancelButton;

        IWorldPromptTrigger active;

        void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartPressed);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelPressed);
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (active == null) return;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                OnCancelPressed();
#endif
        }

        public void Show(IWorldPromptTrigger trigger)
        {
            active = trigger;
            panel.SetActive(true);

            bool hasHeader = !string.IsNullOrEmpty(trigger.HeaderRight);
            headerRow.gameObject.SetActive(hasHeader);
            if (hasHeader)
            {
                headerKindText.text = trigger.TriggerKind.ToUpperInvariant();
                headerRightText.text = trigger.HeaderRight;
            }
            kickerText.gameObject.SetActive(!hasHeader);
            if (!hasHeader) kickerText.text = trigger.TriggerKind.ToUpperInvariant();

            titleText.text = trigger.PromptLabel.ToUpperInvariant();
            detailsText.text = trigger.PromptDetails;

            float bodyTop = hasHeader ? HeaderHeight : 0f;
            float bodyHeight = hasHeader ? BodyHeightNoKicker : BodyHeightWithKicker;
            RacingTheme.PlaceTopStrip(bodyRow, bodyTop, bodyHeight);
            ((RectTransform)titleText.transform).anchoredPosition = new Vector2(40f, -(hasHeader ? 32f : 56f));

            var frame = (RectTransform)panel.transform;
            frame.sizeDelta = new Vector2(CardWidth, bodyTop + bodyHeight + FooterHeight);

            if (startButtonLabel != null) startButtonLabel.text = trigger.ActionLabel.ToUpperInvariant();
        }

        public void Hide(IWorldPromptTrigger trigger)
        {
            if (active != trigger) return;
            active = null;
            panel.SetActive(false);
        }

        void OnStartPressed()
        {
            active?.Confirm();
        }

        void OnCancelPressed()
        {
            active?.Cancel();
        }

        // ── build (Editor-only -- run once via this context menu, never at runtime) ──

        [ContextMenu("Build UI (Editor Only)")]
        void Build()
        {
            // This GameObject is a plain (non-RectTransform) transform alongside
            // RaceManager/VehicleManager etc -- the card has to be built under the
            // scene's Canvas, not as a child of this transform.
            var canvas = GetComponentInChildren<Canvas>(true) ?? FindFirstObjectByType<Canvas>();
            Transform canvasT = canvas != null ? canvas.transform : transform;

            var frame = RacingTheme.CreateFramedPanel("PromptCard", canvasT, RacingTheme.Panel, out var content);
            panel = frame.gameObject;
            RacingTheme.PlaceBottomLeft(frame, 40f, 40f, CardWidth, HeaderHeight + BodyHeightWithKicker + FooterHeight);

            // Header bar (ink) -- shown only when the trigger provides HeaderRight.
            var headerFrame = RacingTheme.CreateImage("Header", content, RacingTheme.Ink, out _);
            headerRow = headerFrame;
            RacingTheme.PlaceTopStrip(headerFrame, 0f, HeaderHeight);
            headerKindText = RacingTheme.CreateLabel("Kind", headerFrame, "", 20f, RacingTheme.SemiBold, RacingTheme.OnInkMuted, 3f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.Stretch((RectTransform)headerKindText.transform, 32, 0, 0, 0);
            headerRightText = RacingTheme.CreateLabel("Right", headerFrame, "", 20f, RacingTheme.SemiBold, RacingTheme.Accent, 3f, TextAlignmentOptions.MidlineRight);
            RacingTheme.Stretch((RectTransform)headerRightText.transform, 0, 0, 32, 0);

            // Body: optional kicker, title, red rule, details.
            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(content, false);
            bodyRow = bodyGo.GetComponent<RectTransform>();

            kickerText = RacingTheme.CreateLabel("Kicker", bodyRow, "", 18f, RacingTheme.SemiBold, RacingTheme.Neutral700, 3f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)kickerText.transform, 40f, 26f, CardWidth - 80f, 24f);

            titleText = RacingTheme.CreateLabel("Title", bodyRow, "", 52f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)titleText.transform, 40f, 56f, CardWidth - 80f, 66f);

            var rule = RacingTheme.CreateImage("Rule", bodyRow, RacingTheme.Accent, out _);
            RacingTheme.PlaceTopLeft(rule, 40f, 132f, 140f, 5f);

            detailsText = RacingTheme.CreateLabel("Details", bodyRow, "", 19f, RacingTheme.Regular, RacingTheme.Neutral700, 0f, TextAlignmentOptions.TopLeft);
            var detailsRt = (RectTransform)detailsText.transform;
            RacingTheme.PlaceTopLeft(detailsRt, 40f, 152f, CardWidth - 80f, 90f);
            detailsText.enableWordWrapping = true;

            // Footer: primary action (fills) + cancel (fixed width), split by a divider.
            var footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(content, false);
            var footerRt = footer.GetComponent<RectTransform>();
            RacingTheme.PlaceBottomStrip(footerRt, 0f, FooterHeight);

            var footerRule = RacingTheme.CreateImage("FooterRule", footerRt, RacingTheme.Ink, out _);
            RacingTheme.PlaceTopStrip(footerRule, 0f, 2f);

            const float cancelWidth = 260f;
            var (startRt, startBtn, startLabel) = RacingTheme.CreateButton("StartBtn", footerRt, "START",
                RacingTheme.Accent, RacingTheme.AccentHover, RacingTheme.Panel, 30f, RacingTheme.ExtraBold, TextAlignmentOptions.MidlineLeft);
            startRt.anchorMin = new Vector2(0f, 0f);
            startRt.anchorMax = new Vector2(1f, 1f);
            startRt.offsetMin = new Vector2(0f, 2f);
            startRt.offsetMax = new Vector2(-cancelWidth - 2f, 0f);
            ((RectTransform)startLabel.transform).offsetMin = new Vector2(40f, 0f);
            startButton = startBtn;
            startButtonLabel = startLabel;
            var startHint = RacingTheme.CreateLabel("Hint", startRt, "E", 18f, RacingTheme.SemiBold, RacingTheme.AccentSoft, 2f, TextAlignmentOptions.Center);
            RacingTheme.PlaceTopRight((RectTransform)startHint.transform, 40f, 26f, 40f, 32f);

            var (cancelRt, cancelBtn, cancelLabel) = RacingTheme.CreateButton("CancelBtn", footerRt, "CANCEL",
                RacingTheme.Panel, RacingTheme.Ink, RacingTheme.Ink, 26f, RacingTheme.ExtraBold, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceBottomRight(cancelRt, 0f, 2f, cancelWidth, FooterHeight - 2f);
            ((RectTransform)cancelLabel.transform).offsetMin = new Vector2(32f, 0f);
            cancelButton = cancelBtn;
            var cancelHint = RacingTheme.CreateLabel("Hint", cancelRt, "ESC", 16f, RacingTheme.SemiBold, RacingTheme.Neutral600, 2f, TextAlignmentOptions.MidlineRight);
            RacingTheme.Stretch((RectTransform)cancelHint.transform, 0, 0, 32, 0);
            // Cancel's label text color doesn't tint with the button hover state (only
            // the Image does) -- acceptable, the background swap alone reads as hover.
        }
    }
}
