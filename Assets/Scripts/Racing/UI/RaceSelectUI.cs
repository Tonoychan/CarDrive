using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Racing.UI
{
    /// Quick Race / Multiplayer's race picker: lists every RaceCourse currently placed
    /// in Drive_Scene (the same career map/courses RaceTrigger uses) and lets the
    /// player jump straight into any of them, ignoring PlayerProgress.Level entirely --
    /// see GameMode.BypassesLevelGate. Hidden entirely in Career mode; shown
    /// automatically on load otherwise, and reappears whenever RaceManager returns to
    /// Idle (RaceEvents.RaceReset) so picking another race after one finishes doesn't
    /// require going back through the main menu. Canvas hierarchy authored once via
    /// "Build UI (Editor Only)", same pattern as every other screen in this project --
    /// this component only wires the already-existing elements at runtime.
    public class RaceSelectUI : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Transform rowsParent;
        [SerializeField] Button backButton;

        void OnEnable()
        {
            RaceEvents.RaceReset += OnRaceReset;
        }

        void OnDisable()
        {
            RaceEvents.RaceReset -= OnRaceReset;
        }

        void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(() => SceneFlow.LoadMainMenu());

            bool show = GameMode.BypassesLevelGate;
            if (show) RefreshList();
            SetVisible(show);
        }

        void OnRaceReset()
        {
            if (!GameMode.BypassesLevelGate) return;
            RefreshList();
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        void RefreshList()
        {
            if (rowsParent == null) return;
            foreach (Transform child in rowsParent) Destroy(child.gameObject);

            var courses = FindObjectsByType<RaceCourse>(FindObjectsSortMode.None)
                .Where(c => c.definition != null)
                .OrderBy(c => c.definition.requiredLevel)
                .ToList();

            for (int i = 0; i < courses.Count; i++)
                BuildRow(courses[i], i);
        }

        void BuildRow(RaceCourse course, int index)
        {
            var def = course.definition;
            var row = new GameObject("Row" + index, typeof(RectTransform));
            row.transform.SetParent(rowsParent, false);
            var rowRt = (RectTransform)row.transform;
            RacingTheme.PlaceTopStrip(rowRt, index * 96f, 90f);

            if (index > 0)
            {
                var div = RacingTheme.CreateImage("Divider", rowRt, RacingTheme.Neutral300, out _);
                RacingTheme.PlaceTopStrip(div, 0f, 1f);
            }

            var name = RacingTheme.CreateLabel("Name", rowRt, def.raceName.ToUpperInvariant(), 32f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)name.transform, 0f, 14f, 620f, 40f);

            int opponents = def.opponentCount;
            var meta = RacingTheme.CreateLabel("Meta", rowRt,
                opponents + " OPPONENT" + (opponents == 1 ? "" : "S") + "   ·   LV " + def.requiredLevel,
                20f, RacingTheme.SemiBold, RacingTheme.Neutral700, 1f, TextAlignmentOptions.BottomLeft);
            RacingTheme.PlaceBottomLeft((RectTransform)meta.transform, 0f, 12f, 620f, 26f);

            var (btnRt, btn, _) = RacingTheme.CreateButton("GoBtn", rowRt, "RACE",
                RacingTheme.Accent, RacingTheme.AccentHover, RacingTheme.Panel, 24f, RacingTheme.ExtraBold);
            RacingTheme.PlaceBottomRight(btnRt, 0f, 16f, 180f, 58f);
            btn.onClick.AddListener(() => StartRace(course));
        }

        void StartRace(RaceCourse course)
        {
            SetVisible(false);
            RaceManager.Instance?.BeginRace(course);
        }

        // ── build (Editor-only -- run once via this context menu, never at runtime) ──

        [ContextMenu("Build UI (Editor Only)")]
        void Build()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            Transform canvasT = canvas.transform;

            var existing = canvasT.Find("RaceSelect");
            if (existing != null) DestroyImmediate(existing.gameObject);

            panel = new GameObject("RaceSelect", typeof(RectTransform));
            panel.transform.SetParent(canvasT, false);
            RacingTheme.Stretch((RectTransform)panel.transform);

            var dim = RacingTheme.CreateImage("Dim", panel.transform, new Color(0.126f, 0.118f, 0.114f, 0.92f), out _);
            RacingTheme.Stretch(dim);

            var card = RacingTheme.CreateFramedPanel("Card", panel.transform, RacingTheme.Panel, out var content, 3f);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(840f, 680f);
            card.anchoredPosition = Vector2.zero;

            var header = RacingTheme.CreateLabel("Title", content, "SELECT RACE", 44f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)header.transform, 48f, 40f, 500f, 56f);
            var rule = RacingTheme.CreateImage("Rule", content, RacingTheme.Ink, out _);
            RacingTheme.PlaceTopStrip(rule, 110f, 2f);

            var rowsGo = new GameObject("Rows", typeof(RectTransform));
            rowsGo.transform.SetParent(content, false);
            var rowsRt = (RectTransform)rowsGo.transform;
            RacingTheme.Stretch(rowsRt, 48f, 130f, 48f, 100f);
            rowsParent = rowsRt;

            var (backRt, backBtn, _) = RacingTheme.CreateButton("Back", content, "BACK TO MENU",
                RacingTheme.Panel, RacingTheme.Neutral300, RacingTheme.Ink, 20f, RacingTheme.ExtraBold);
            RacingTheme.PlaceBottomLeft(backRt, 48f, 24f, 260f, 52f);
            backButton = backBtn;

            SetVisible(false);
        }
    }
}
