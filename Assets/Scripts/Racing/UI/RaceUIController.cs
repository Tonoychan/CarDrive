using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MVC;
using MVC.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Racing.UI
{
    /// HUD/countdown/results (Modernist design system, corner-anchored HUD layout
    /// "2a"). The Canvas hierarchy is authored once in the Editor -- via the "Build
    /// UI (Editor Only)" context menu below, never at runtime -- and saved into the
    /// scene like any hand-built UI, so it's visible/editable without Play Mode.
    /// This component only finds/wires the already-existing elements at runtime.
    public class RaceUIController : MonoBehaviour
    {
        [SerializeField] GameObject countdownPanel, hudPanel, resultsPanel;

        // Countdown
        [SerializeField] TMP_Text countdownStripText, countdownNumberText, countdownGoText;
        [SerializeField] RectTransform countdownRule;

        // HUD
        [SerializeField] TMP_Text speedText, gearText, positionText, positionOfText, courseTitleText, remainText;
        [SerializeField] RectTransform progressFill;

        // Results
        [SerializeField] TMP_Text resultsHeaderText, resultsTimeText;
        [SerializeField] Transform resultsRowsParent;
        [SerializeField] Button raceAgainButton;

        string courseName = "RACE";
        int opponentCount;
        bool resultsShowing;

        Vehicle playerVehicle;
        Rigidbody playerRb;
        [SerializeField] GameObject speedMeter;

        void Awake()
        {
            if (raceAgainButton != null) raceAgainButton.onClick.AddListener(() => RaceManager.Instance?.RaceAgain());
        }

        void OnEnable()
        {
            RaceEvents.CountdownStarted += OnCountdownStarted;
            RaceEvents.CountdownTick += OnCountdownTick;
            RaceEvents.RaceStarted += OnRaceStarted;
            RaceEvents.PositionChanged += OnPositionChanged;
            RaceEvents.LapChanged += OnLapChanged;
            RaceEvents.ProgressChanged += OnProgressChanged;
            RaceEvents.RaceFinished += OnRaceFinished;
            RaceEvents.RaceInfo += OnRaceInfo;

            SetActive(countdownPanel, false);
            SetActive(hudPanel, false);
            SetActive(resultsPanel, false);
        }

        void OnDisable()
        {
            RaceEvents.CountdownStarted -= OnCountdownStarted;
            RaceEvents.CountdownTick -= OnCountdownTick;
            RaceEvents.RaceStarted -= OnRaceStarted;
            RaceEvents.PositionChanged -= OnPositionChanged;
            RaceEvents.LapChanged -= OnLapChanged;
            RaceEvents.ProgressChanged -= OnProgressChanged;
            RaceEvents.RaceFinished -= OnRaceFinished;
            RaceEvents.RaceInfo -= OnRaceInfo;
        }

        void LateUpdate()
        {
            // MVC's own VehicleUIController re-enables its SpeedMeter gauge in its own
            // Update(), undoing a one-time Awake() disable -- LateUpdate runs after
            // every Update this frame, so this wins the race instead of flickering.
            if (speedMeter != null && speedMeter.activeSelf) speedMeter.SetActive(false);
        }

        void Update()
        {
            if (hudPanel.activeSelf) UpdateLiveReadouts();

            if (!resultsShowing) return;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseResults();
#endif
        }

        void UpdateLiveReadouts()
        {
            if (playerVehicle == null)
            {
                var vm = FindFirstObjectByType<VehicleManager>();
                playerVehicle = vm != null ? vm.PlayerVehicle : null;
                playerRb = playerVehicle != null ? playerVehicle.GetComponent<Rigidbody>() : null;
            }
            if (playerVehicle == null) return;

            int speed = playerRb != null ? Mathf.RoundToInt(playerRb.linearVelocity.magnitude * 3.6f) : 0;
            speedText.text = speed.ToString();
            gearText.text = playerVehicle.CurrentGearToString;
        }

        void CloseResults()
        {
            resultsShowing = false;
            SetActive(resultsPanel, false);
            RaceManager.Instance?.ResetToIdle();
        }

        void OnRaceInfo(string name, int opponents)
        {
            courseName = name.ToUpperInvariant();
            opponentCount = opponents;
        }

        void OnCountdownStarted(float seconds)
        {
            resultsShowing = false;
            SetActive(resultsPanel, false);
            SetActive(countdownPanel, true);
            playerVehicle = null;

            countdownStripText.text = courseName + "   ·   " + opponentCount + " RACER" + (opponentCount == 1 ? "" : "S");
            countdownGoText.gameObject.SetActive(false);
            countdownRule.gameObject.SetActive(false);
            countdownNumberText.gameObject.SetActive(true);
        }

        void OnCountdownTick(float remaining)
        {
            bool go = remaining <= 0.05f;
            countdownNumberText.gameObject.SetActive(!go);
            countdownGoText.gameObject.SetActive(go);
            countdownRule.gameObject.SetActive(go);
            if (!go) countdownNumberText.text = Mathf.CeilToInt(remaining).ToString();
        }

        void OnRaceStarted()
        {
            SetActive(countdownPanel, false);
            SetActive(hudPanel, true);
            courseTitleText.text = courseName;
        }

        void OnPositionChanged(int position, int totalRacers)
        {
            positionText.text = Ordinal(position);
            positionOfText.text = "/ " + totalRacers;
        }

        void OnLapChanged(int currentLap, int totalLaps)
        {
            // Corner-anchored HUD (2a) has no dedicated lap readout -- lap count only
            // matters for multi-lap circuits, which this project doesn't currently race.
        }

        void OnProgressChanged(float remainingMeters, float fraction)
        {
            remainText.text = Mathf.Max(0, Mathf.RoundToInt(remainingMeters)) + " M";
            var anchorMax = progressFill.anchorMax;
            anchorMax.x = Mathf.Clamp01(fraction);
            progressFill.anchorMax = anchorMax;
        }

        void OnRaceFinished(RaceResult[] results)
        {
            SetActive(hudPanel, false);
            SetActive(resultsPanel, true);
            resultsShowing = true;

            float leaderTime = results.Length > 0 ? results[0].elapsedSeconds : 0f;
            resultsHeaderText.text = courseName;
            resultsTimeText.text = FormatTime(leaderTime);

            foreach (Transform child in resultsRowsParent) Destroy(child.gameObject);
            for (int i = 0; i < results.Length; i++)
                BuildResultRow(results[i], leaderTime, i);
        }

        void BuildResultRow(RaceResult r, float leaderTime, int index)
        {
            var row = new GameObject("Row" + index, typeof(RectTransform));
            row.transform.SetParent(resultsRowsParent, false);
            var rowRt = (RectTransform)row.transform;
            RacingTheme.PlaceTopStrip(rowRt, index * 70f, 66f);

            if (index > 0)
                RacingTheme.CreateImage("Divider", rowRt, RacingTheme.Neutral300, out _);
            if (index > 0)
                RacingTheme.PlaceTopStrip((RectTransform)rowRt.GetChild(rowRt.childCount - 1), 0f, 1f);

            var pos = RacingTheme.CreateLabel("Pos", rowRt, r.position.ToString(), 30f, RacingTheme.ExtraBold,
                r.isPlayer ? RacingTheme.Accent : RacingTheme.Ink, 0f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceTopLeft((RectTransform)pos.transform, 0f, 8f, 70f, 50f);

            var name = RacingTheme.CreateLabel("Name", rowRt, r.participantName.ToUpperInvariant(), 24f, RacingTheme.SemiBold,
                r.isPlayer ? RacingTheme.Accent : RacingTheme.Ink, 1f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceTopLeft((RectTransform)name.transform, 90f, 8f, 600f, 50f);

            string timeStr = index == 0 ? FormatTime(r.elapsedSeconds) : "+" + (r.elapsedSeconds - leaderTime).ToString("0.00");
            var time = RacingTheme.CreateLabel("Time", rowRt, timeStr, 26f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.MidlineRight);
            var timeRt = (RectTransform)time.transform;
            RacingTheme.Stretch(timeRt, 0, 8, 0, 8);
        }

        static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rem = seconds - minutes * 60f;
            return $"{minutes}:{rem:00.00}";
        }

        static void SetActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        static string Ordinal(int n)
        {
            int rem100 = n % 100;
            if (rem100 == 11 || rem100 == 12 || rem100 == 13) return n + "<size=45%>TH</size>";
            switch (n % 10)
            {
                case 1: return n + "<size=45%>ST</size>";
                case 2: return n + "<size=45%>ND</size>";
                case 3: return n + "<size=45%>RD</size>";
                default: return n + "<size=45%>TH</size>";
            }
        }

        // ── build (Editor-only -- run once via this context menu, never at runtime) ──

        [ContextMenu("Build UI (Editor Only)")]
        void Build()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            Transform canvasT = canvas != null ? canvas.transform : transform;

            // The MVC needle/gear gauge this HUD's digital readouts replace.
            var speedMeterT = canvasT.Find("SpeedMeter");
            speedMeter = speedMeterT != null ? speedMeterT.gameObject : null;
            if (speedMeter != null) speedMeter.SetActive(false);

            var root = canvasT.Find("RaceHUD");
            GameObject rootGo;
            if (root != null)
            {
                rootGo = root.gameObject;
                for (int i = rootGo.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(rootGo.transform.GetChild(i).gameObject);
            }
            else
            {
                rootGo = new GameObject("RaceHUD", typeof(RectTransform));
                rootGo.transform.SetParent(canvasT, false);
            }
            var rootRt = (RectTransform)rootGo.transform;
            RacingTheme.Stretch(rootRt);

            BuildCountdown(rootRt);
            BuildHud(rootRt);
            BuildResults(rootRt);
        }

        void BuildCountdown(Transform parent)
        {
            countdownPanel = new GameObject("Countdown", typeof(RectTransform));
            countdownPanel.transform.SetParent(parent, false);
            RacingTheme.Stretch((RectTransform)countdownPanel.transform);

            var strip = RacingTheme.CreateImage("Strip", countdownPanel.transform, RacingTheme.Panel, out _);
            RacingTheme.PlaceTopStrip(strip, 0f, 64f);
            countdownStripText = RacingTheme.CreateLabel("StripText", strip, "", 22f, RacingTheme.SemiBold, RacingTheme.Ink, 3f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.Stretch((RectTransform)countdownStripText.transform, 32, 0, 32, 0);

            countdownNumberText = RacingTheme.CreateLabel("Number", countdownPanel.transform, "3", 320f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.Center);
            RacingTheme.Stretch((RectTransform)countdownNumberText.transform, 0, 64, 0, 0);

            countdownRule = RacingTheme.CreateImage("GoRule", countdownPanel.transform, RacingTheme.Accent, out _);
            RacingTheme.PlaceTopStrip(countdownRule, 460f, 160f);

            countdownGoText = RacingTheme.CreateLabel("Go", countdownPanel.transform, "GO", 180f, RacingTheme.ExtraBold, RacingTheme.Panel, 4f, TextAlignmentOptions.Center);
            RacingTheme.Stretch((RectTransform)countdownGoText.transform, 0, 64, 0, 0);
        }

        void BuildHud(Transform parent)
        {
            hudPanel = new GameObject("Hud", typeof(RectTransform));
            hudPanel.transform.SetParent(parent, false);
            RacingTheme.Stretch((RectTransform)hudPanel.transform);

            // Bottom-left: KM/H + gear.
            var speedFrame = RacingTheme.CreateFramedPanel("SpeedCard", hudPanel.transform, RacingTheme.Panel, out var speedContent);
            RacingTheme.PlaceBottomLeft(speedFrame, 40f, 40f, 460f, 210f);
            var speedLabel = RacingTheme.CreateLabel("Label", speedContent, "KM/H", 20f, RacingTheme.SemiBold, RacingTheme.Neutral700, 3f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)speedLabel.transform, 24f, 16f, 200f, 26f);
            speedText = RacingTheme.CreateLabel("Speed", speedContent, "0", 130f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.BottomLeft);
            RacingTheme.PlaceBottomLeft((RectTransform)speedText.transform, 24f, 20f, 280f, 140f);
            var gearFrame = RacingTheme.CreateImage("GearBadge", speedContent, RacingTheme.Ink, out _);
            RacingTheme.PlaceBottomRight(gearFrame, 24f, 24f, 100f, 90f);
            var gearFill = RacingTheme.CreateImage("Fill", gearFrame, RacingTheme.Accent, out _);
            RacingTheme.Stretch(gearFill, 2, 2, 2, 2);
            gearText = RacingTheme.CreateLabel("Gear", gearFill, "1", 52f, RacingTheme.ExtraBold, RacingTheme.Panel, 0f, TextAlignmentOptions.Center);
            RacingTheme.Stretch((RectTransform)gearText.transform);

            // Top-left: position.
            var posFrame = RacingTheme.CreateImage("PositionCard", hudPanel.transform, RacingTheme.Ink, out _);
            RacingTheme.PlaceTopLeft(posFrame, 64f, 64f, 300f, 130f);
            positionText = RacingTheme.CreateLabel("Position", posFrame, "1<size=45%>ST</size>", 90f, RacingTheme.ExtraBold, RacingTheme.Panel, 0f, TextAlignmentOptions.BottomLeft);
            RacingTheme.PlaceBottomLeft((RectTransform)positionText.transform, 26f, 18f, 180f, 100f);
            positionOfText = RacingTheme.CreateLabel("Of", posFrame, "/ 1", 26f, RacingTheme.SemiBold, RacingTheme.OnInkMuted, 0f, TextAlignmentOptions.BottomLeft);
            RacingTheme.PlaceBottomLeft((RectTransform)positionOfText.transform, 190f, 26f, 100f, 40f);

            // Bottom-right: course + remaining distance + progress bar.
            var progFrame = RacingTheme.CreateFramedPanel("ProgressCard", hudPanel.transform, RacingTheme.Panel, out var progContent);
            RacingTheme.PlaceBottomRight(progFrame, 64f, 64f, 420f, 150f);
            courseTitleText = RacingTheme.CreateLabel("Course", progContent, "", 20f, RacingTheme.SemiBold, RacingTheme.Neutral700, 3f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)courseTitleText.transform, 22f, 18f, 220f, 26f);
            remainText = RacingTheme.CreateLabel("Remain", progContent, "0 M", 22f, RacingTheme.ExtraBold, RacingTheme.Accent, 0f, TextAlignmentOptions.TopRight);
            RacingTheme.PlaceTopRight((RectTransform)remainText.transform, 22f, 16f, 160f, 28f);
            var track = RacingTheme.CreateImage("Track", progContent, RacingTheme.Neutral300, out _);
            RacingTheme.PlaceBottomLeft(track, 22f, 24f, 376f, 16f);
            var fill = RacingTheme.CreateImage("Fill", track, RacingTheme.Accent, out _);
            progressFill = fill;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        void BuildResults(Transform parent)
        {
            resultsPanel = new GameObject("Results", typeof(RectTransform));
            resultsPanel.transform.SetParent(parent, false);
            RacingTheme.Stretch((RectTransform)resultsPanel.transform);
            var bg = RacingTheme.CreateImage("Bg", resultsPanel.transform, RacingTheme.Panel, out _);
            RacingTheme.Stretch(bg);

            var header = RacingTheme.CreateImage("Header", resultsPanel.transform, RacingTheme.Panel, out _);
            RacingTheme.PlaceTopStrip(header, 0f, 170f);
            var headerRule = RacingTheme.CreateImage("Rule", header, RacingTheme.Ink, out _);
            RacingTheme.PlaceBottomStrip(headerRule, 0f, 2f);
            var finished = RacingTheme.CreateLabel("Finished", header, "FINISHED", 56f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceTopLeft((RectTransform)finished.transform, 56f, 55f, 360f, 60f);
            resultsHeaderText = RacingTheme.CreateLabel("Course", header, "", 24f, RacingTheme.SemiBold, RacingTheme.Neutral700, 3f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceTopLeft((RectTransform)resultsHeaderText.transform, 380f, 60f, 400f, 50f);
            resultsTimeText = RacingTheme.CreateLabel("Time", header, "0:00.00", 56f, RacingTheme.ExtraBold, RacingTheme.Accent, 0f, TextAlignmentOptions.MidlineRight);
            RacingTheme.PlaceTopRight((RectTransform)resultsTimeText.transform, 56f, 55f, 300f, 60f);

            var rowsGo = new GameObject("Rows", typeof(RectTransform));
            rowsGo.transform.SetParent(resultsPanel.transform, false);
            var rowsRt = (RectTransform)rowsGo.transform;
            RacingTheme.PlaceTopStrip(rowsRt, 172f, 460f);
            // Left/right padding to match the header's 56px inset.
            rowsRt.offsetMin = new Vector2(56f, rowsRt.offsetMin.y);
            rowsRt.offsetMax = new Vector2(-56f, rowsRt.offsetMax.y);
            resultsRowsParent = rowsRt;

            var footer = RacingTheme.CreateImage("Footer", resultsPanel.transform, RacingTheme.Panel, out _);
            RacingTheme.PlaceBottomStrip(footer, 0f, 130f);
            var footerRule = RacingTheme.CreateImage("Rule", footer, RacingTheme.Ink, out _);
            RacingTheme.PlaceTopStrip(footerRule, 0f, 2f);
            var hint = RacingTheme.CreateLabel("Hint", footer, "ESC — CLOSE", 22f, RacingTheme.SemiBold, RacingTheme.Neutral700, 2f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceTopLeft((RectTransform)hint.transform, 56f, 0f, 500f, 130f);

            var (rt, btn, _) = RacingTheme.CreateButton("RaceAgain", footer, "RACE AGAIN", RacingTheme.Accent, RacingTheme.AccentHover, RacingTheme.Panel, 32f, RacingTheme.ExtraBold);
            RacingTheme.PlaceBottomRight(rt, 0f, 2f, 340f, 128f);
            raceAgainButton = btn;
        }
    }
}
