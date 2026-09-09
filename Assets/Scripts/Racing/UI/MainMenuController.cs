using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Racing.UI
{
    /// Boot screen: title + Career / Quick Race / Multiplayer / Options / Exit. Canvas
    /// hierarchy authored once via "Build UI (Editor Only)", same pattern as every
    /// other screen in this project -- this component only wires the already-existing
    /// buttons at runtime. Career and Quick Race/Multiplayer both load Drive_Scene --
    /// GameMode records which, so Drive_Scene's RaceTrigger/RaceSelectUI know whether
    /// to respect PlayerProgress.Level (Career) or open every race regardless
    /// (Quick Race/Multiplayer, sharing the exact same map and RaceDefinitions).
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] Button careerButton, quickRaceButton, multiplayerButton, optionsButton, exitButton;

        void Start()
        {
            if (careerButton != null) careerButton.onClick.AddListener(() => { GameMode.SetCareer(); SceneFlow.LoadDrive(); });
            if (quickRaceButton != null) quickRaceButton.onClick.AddListener(() => { GameMode.SetQuickRace(); SceneFlow.LoadDrive(); });
            if (multiplayerButton != null) multiplayerButton.onClick.AddListener(() => { GameMode.SetMultiplayer(); SceneFlow.LoadDrive(); });

            // Options/Exit intentionally left unwired -- no Options screen exists yet,
            // and Application.Quit() is a no-op in WebGL builds (the shipping target)
            // anyway, so leaving Exit inert matches its actual real-world behavior
            // rather than pretending it does something in the browser.
        }

        // ── build (Editor-only -- run once via this context menu, never at runtime) ──

        [ContextMenu("Build UI (Editor Only)")]
        void Build()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            Transform canvasT = canvas.transform;

            var existing = canvasT.Find("MainMenu");
            if (existing != null) DestroyImmediate(existing.gameObject);

            var root = new GameObject("MainMenu", typeof(RectTransform));
            root.transform.SetParent(canvasT, false);
            RacingTheme.Stretch((RectTransform)root.transform);

            var bg = RacingTheme.CreateImage("Bg", root.transform, RacingTheme.Ink, out _);
            RacingTheme.Stretch(bg);

            var kicker = RacingTheme.CreateLabel("Kicker", root.transform, "ARCADE STREET RACING", 24f, RacingTheme.SemiBold, RacingTheme.Accent, 6f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)kicker.transform, 64f, 64f, 900f, 34f);

            var title = RacingTheme.CreateLabel("Title", root.transform, "T'S MOST\nWANTED", 140f, RacingTheme.ExtraBold, RacingTheme.Panel, 0f, TextAlignmentOptions.TopLeft);
            title.enableWordWrapping = true;
            title.lineSpacing = -20f;
            RacingTheme.PlaceTopLeft((RectTransform)title.transform, 60f, 110f, 1300f, 380f);

            var rule = RacingTheme.CreateImage("Rule", root.transform, RacingTheme.Accent, out _);
            RacingTheme.PlaceBottomStrip(rule, 0f, 6f);

            BuildMenu(root.transform);
        }

        void BuildMenu(Transform parent)
        {
            var menu = new GameObject("Menu", typeof(RectTransform));
            menu.transform.SetParent(parent, false);
            RacingTheme.PlaceBottomLeft((RectTransform)menu.transform, 64f, 96f, 520f, 400f);

            string[] labels = { "CAREER", "QUICK RACE", "MULTIPLAYER", "OPTIONS", "EXIT" };
            const float btnHeight = 64f, gap = 14f;
            for (int i = 0; i < labels.Length; i++)
            {
                float yFromBottom = (labels.Length - 1 - i) * (btnHeight + gap);
                // First three read as active/primary (red); Options/Exit read as
                // secondary/inactive since neither does anything yet.
                bool primary = i < 3;
                var bg = primary ? RacingTheme.Accent : RacingTheme.Panel;
                var hover = primary ? RacingTheme.AccentHover : RacingTheme.Neutral300;
                var fg = primary ? RacingTheme.Panel : RacingTheme.Neutral700;

                var (rt, btn, label) = RacingTheme.CreateButton(labels[i] + "Btn", menu.transform, labels[i],
                    bg, hover, fg, 26f, RacingTheme.ExtraBold, TextAlignmentOptions.MidlineLeft);
                RacingTheme.PlaceBottomLeft(rt, 0f, yFromBottom, 520f, btnHeight);
                if (label != null) label.margin = new Vector4(32f, 0f, 32f, 0f);

                switch (i)
                {
                    case 0: careerButton = btn; break;
                    case 1: quickRaceButton = btn; break;
                    case 2: multiplayerButton = btn; break;
                    case 3: optionsButton = btn; break;
                    case 4: exitButton = btn; break;
                }
            }
        }
    }
}
