using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MVC.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Racing.UI
{
    /// Dedicated Shop scene controller (Modernist "rail + preview" layout, 2i): the
    /// real 3D car on CarHolder fills the left two-thirds (specs printed under it),
    /// a right-hand rail lists Engine/Brakes/Handling with a pip ladder and one
    /// Upgrade button per category (always buys the next un-owned tier). The Canvas
    /// content is authored once in the Editor -- via "Build UI (Editor Only)" below,
    /// never at runtime -- and saved into the scene, so it's visible/editable without
    /// Play Mode. This component only wires up the already-existing elements at runtime.
    public class ShopSceneController : MonoBehaviour
    {
        [System.Serializable]
        public class CategoryUI
        {
            public UpgradeCategory category;
        }

        [System.Serializable]
        public class PipRow
        {
            public Image[] pips;
        }

        public CategoryUI[] categories = new CategoryUI[3];
        public RaceGarageController garage;

        [Header("Car preview (display only, no cycling here)")]
        public CarRosterEntry[] roster;
        public Transform carHolder;

        [Header("Wired by Build UI (Editor Only) -- do not hand-edit")]
        [SerializeField] TMP_Text carNameText;
        [SerializeField] TMP_Text[] specValueTexts = new TMP_Text[3];
        [SerializeField] TMP_Text balanceText;
        [SerializeField] TMP_Text[] rowLevelTexts;
        [SerializeField] TMP_Text[] rowNextTexts;
        [SerializeField] Button[] rowUpgradeButtons;
        [SerializeField] PipRow[] rowPips;
        [SerializeField] Button exitButton;

        GameObject carDisplay;

        void Start()
        {
            if (exitButton != null) exitButton.onClick.AddListener(() => SceneFlow.LoadDrive());
            for (int i = 0; i < rowUpgradeButtons.Length; i++)
            {
                if (rowUpgradeButtons[i] == null) continue;
                var captured = i;
                rowUpgradeButtons[i].onClick.AddListener(() => TryUpgrade(captured));
            }

            ShowSelectedCar();
            RefreshAll();
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SceneFlow.LoadDrive();
#endif
        }

        void OnEnable()
        {
            if (PlayerCurrency.Instance != null) PlayerCurrency.Instance.BalanceChanged += OnBalanceChanged;
        }

        void OnDisable()
        {
            if (PlayerCurrency.Instance != null) PlayerCurrency.Instance.BalanceChanged -= OnBalanceChanged;
        }

        void OnBalanceChanged(int balance) => RefreshAll();

        void ShowSelectedCar()
        {
            if (roster == null || roster.Length == 0) return;
            int index = Mathf.Clamp(CarSelection.LoadIndex(), 0, roster.Length - 1);
            var entry = roster[index];
            if (carNameText != null) carNameText.text = entry.displayName.ToUpperInvariant();

            if (carHolder == null || entry.vehiclePrefab == null) return;
            if (carDisplay != null) Destroy(carDisplay);
            // "- Empty" variant for display -- the full driveable prefab's Vehicle.Awake()
            // throws here trying to auto-bootstrap a VehicleManager this scene lacks.
            GameObject displayPrefab = entry.previewPrefab != null ? entry.previewPrefab : entry.vehiclePrefab;
            carDisplay = Instantiate(displayPrefab, carHolder.position, carHolder.rotation);

            var liveVehicle = carDisplay.GetComponent<Vehicle>();
            if (liveVehicle != null) liveVehicle.enabled = false;
            var liveRb = carDisplay.GetComponent<Rigidbody>();
            if (liveRb != null) liveRb.isKinematic = true;

            if (specValueTexts != null && specValueTexts.Length >= 3 && specValueTexts[0] != null)
            {
                // Stats read from the driveable prefab ASSET directly, not the
                // instantiated preview -- reading a prefab asset's component doesn't
                // run Awake(), so this works regardless of which variant is displayed.
                var vehicle = entry.vehiclePrefab.GetComponent<Vehicle>();
                var rb = entry.vehiclePrefab.GetComponent<Rigidbody>();
                float power = vehicle != null && vehicle.Engine != null ? vehicle.Engine.Power : 0f;
                float mass = rb != null ? rb.mass : 0f;
                float topSpeed = vehicle != null ? vehicle.TopSpeed : 0f;
                specValueTexts[0].text = Mathf.RoundToInt(power) + " HP";
                specValueTexts[1].text = Mathf.RoundToInt(mass) + " KG";
                specValueTexts[2].text = Mathf.RoundToInt(topSpeed) + " KM/H";
            }
        }

        void TryUpgrade(int index)
        {
            var cat = categories[index]?.category;
            if (cat?.tiers == null) return;
            int level = OwnedLevel(cat);
            if (level >= cat.tiers.Length) return;

            var part = cat.tiers[level];
            if (part == null || garage == null) return;

            garage.Purchase(part);
            RefreshAll();
        }

        int OwnedLevel(UpgradeCategory cat)
        {
            if (cat?.tiers == null) return 0;
            int level = 0;
            foreach (var tier in cat.tiers)
            {
                if (tier != null && garage != null && garage.IsOwned(tier)) level++;
                else break;
            }
            return level;
        }

        void RefreshAll()
        {
            if (balanceText != null)
                balanceText.text = (PlayerCurrency.Instance != null ? PlayerCurrency.Instance.Balance : 0).ToString("N0");

            for (int i = 0; i < categories.Length; i++)
                RefreshRow(i);
        }

        void RefreshRow(int i)
        {
            var cat = categories[i]?.category;
            if (cat?.tiers == null) return;

            int level = OwnedLevel(cat);
            int maxLevel = cat.tiers.Length;
            bool maxed = level >= maxLevel;
            var next = maxed ? null : cat.tiers[level];

            if (rowLevelTexts[i] != null) rowLevelTexts[i].text = "LV " + level + "/" + maxLevel;
            if (rowNextTexts[i] != null) rowNextTexts[i].text = maxed ? "MAXED OUT" : next != null ? next.cost + " COINS" : "-";

            var pips = rowPips[i]?.pips;
            if (pips != null)
                for (int p = 0; p < pips.Length; p++)
                    pips[p].color = p < level ? RacingTheme.Accent : RacingTheme.Neutral300;

            if (rowUpgradeButtons[i] != null)
            {
                bool canBuy = !maxed && next != null &&
                              (PlayerCurrency.Instance == null || PlayerCurrency.Instance.CanAfford(next.cost));
                rowUpgradeButtons[i].interactable = canBuy;
                var label = rowUpgradeButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = maxed ? "MAXED" : "UPGRADE";
            }
        }

        // ── build (Editor-only -- run once via this context menu, never at runtime) ──

        [ContextMenu("Build UI (Editor Only)")]
        void Build()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            Transform parent = canvas.transform;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name != "EventSystem") DestroyImmediate(child.gameObject);
            }

            var root = new GameObject("ShopUI", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RacingTheme.Stretch((RectTransform)root.transform);

            const float railWidth = 660f;

            BuildLeftPreview(root.transform, railWidth);
            BuildRightRail(root.transform, railWidth);
        }

        void BuildLeftPreview(Transform parent, float railWidth)
        {
            var left = new GameObject("Preview", typeof(RectTransform));
            left.transform.SetParent(parent, false);
            RacingTheme.Stretch((RectTransform)left.transform, 0, 0, railWidth, 0);

            carNameText = RacingTheme.CreateLabel("CarName", left.transform, "", 56f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)carNameText.transform, 56f, 40f, 900f, 70f);

            string[] specKeys = { "POWER", "MASS", "TOP SPEED" };
            specValueTexts = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var col = new GameObject("Spec" + i, typeof(RectTransform));
                col.transform.SetParent(left.transform, false);
                RacingTheme.PlaceBottomLeft((RectTransform)col.transform, 56f + i * 240f, 48f, 220f, 100f);
                RacingTheme.CreateLabel("Key", col.transform, specKeys[i], 18f, RacingTheme.SemiBold, RacingTheme.Neutral700, 3f, TextAlignmentOptions.TopLeft);
                var val = RacingTheme.CreateLabel("Val", col.transform, "0", 36f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.BottomLeft);
                RacingTheme.PlaceBottomLeft((RectTransform)val.transform, 0f, 0f, 220f, 44f);
                specValueTexts[i] = val;
            }
        }

        void BuildRightRail(Transform parent, float railWidth)
        {
            var rail = RacingTheme.CreateImage("Rail", parent, RacingTheme.Panel, out _);
            rail.anchorMin = new Vector2(1f, 0f);
            rail.anchorMax = new Vector2(1f, 1f);
            rail.pivot = new Vector2(1f, 0.5f);
            rail.offsetMin = new Vector2(-railWidth, 0f);
            rail.offsetMax = Vector2.zero;
            var edgeRule = RacingTheme.CreateImage("EdgeRule", rail, RacingTheme.Ink, out _);
            edgeRule.anchorMin = new Vector2(0f, 0f);
            edgeRule.anchorMax = new Vector2(0f, 1f);
            edgeRule.pivot = new Vector2(0f, 0.5f);
            edgeRule.offsetMin = Vector2.zero;
            edgeRule.sizeDelta = new Vector2(2f, 0f);

            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(rail, false);
            RacingTheme.PlaceTopStrip((RectTransform)header.transform, 0f, 100f);
            var headerRule = RacingTheme.CreateImage("Rule", header.transform, RacingTheme.Ink, out _);
            RacingTheme.PlaceBottomStrip(headerRule, 0f, 2f);
            var shopTitle = RacingTheme.CreateLabel("Title", header.transform, "SHOP", 40f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceTopLeft((RectTransform)shopTitle.transform, 44f, 30f, 200f, 44f);
            balanceText = RacingTheme.CreateLabel("Balance", header.transform, "0", 36f, RacingTheme.ExtraBold, RacingTheme.Accent, 0f, TextAlignmentOptions.MidlineRight);
            RacingTheme.PlaceTopRight((RectTransform)balanceText.transform, 44f, 30f, 260f, 44f);

            int n = categories.Length;
            rowLevelTexts = new TMP_Text[n];
            rowNextTexts = new TMP_Text[n];
            rowUpgradeButtons = new Button[n];
            rowPips = new PipRow[n];

            float rowHeight = 220f;
            float footerHeight = 130f;
            for (int i = 0; i < n; i++)
                BuildCategoryRow(rail, i, 100f + i * rowHeight, rowHeight);

            BuildFooter(rail, footerHeight);
        }

        void BuildCategoryRow(Transform parent, int index, float yFromTop, float rowHeight)
        {
            var row = new GameObject("Row" + index, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RacingTheme.PlaceTopStrip((RectTransform)row.transform, yFromTop, rowHeight);
            if (index > 0)
            {
                var div = RacingTheme.CreateImage("Divider", row.transform, RacingTheme.Neutral300, out _);
                RacingTheme.PlaceTopStrip(div, 0f, 1f);
            }

            string name = categories[index]?.category != null ? categories[index].category.displayName.ToUpperInvariant() : "";
            var nameLabel = RacingTheme.CreateLabel("Name", row.transform, name, 34f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft(nameLabel.rectTransform, 44f, 20f, 500f, 44f);

            var lv = RacingTheme.CreateLabel("Level", row.transform, "LV 0/4", 22f, RacingTheme.SemiBold, RacingTheme.Accent, 1f, TextAlignmentOptions.TopRight);
            RacingTheme.PlaceTopRight((RectTransform)lv.transform, 44f, 26f, 160f, 34f);
            rowLevelTexts[index] = lv;

            int pipCount = categories[index]?.category?.tiers?.Length ?? 4;
            var pips = new Image[pipCount];
            float pipSize = 18f, pipGap = 10f;
            for (int p = 0; p < pipCount; p++)
            {
                var pip = RacingTheme.CreateImage("Pip" + p, row.transform, RacingTheme.Neutral300, out var pipImg);
                RacingTheme.PlaceTopLeft(pip, 44f + p * (pipSize + pipGap), 80f, pipSize, pipSize);
                pips[p] = pipImg;
            }
            rowPips[index] = new PipRow { pips = pips };

            var next = RacingTheme.CreateLabel("Next", row.transform, "", 20f, RacingTheme.SemiBold, RacingTheme.Neutral700, 1f, TextAlignmentOptions.BottomLeft);
            RacingTheme.PlaceBottomLeft((RectTransform)next.transform, 44f, 24f, 300f, 30f);
            rowNextTexts[index] = next;

            var (btnRt, btn, _) = RacingTheme.CreateButton("UpgradeBtn", row.transform, "UPGRADE",
                RacingTheme.Accent, RacingTheme.AccentHover, RacingTheme.Panel, 20f, RacingTheme.ExtraBold);
            RacingTheme.PlaceBottomRight(btnRt, 44f, 20f, 190f, 46f);
            rowUpgradeButtons[index] = btn;
        }

        void BuildFooter(Transform parent, float footerHeight)
        {
            var footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(parent, false);
            RacingTheme.PlaceBottomStrip((RectTransform)footer.transform, 0f, footerHeight);
            var rule = RacingTheme.CreateImage("Rule", footer.transform, RacingTheme.Ink, out _);
            RacingTheme.PlaceTopStrip(rule, 0f, 2f);

            var hint = RacingTheme.CreateLabel("Hint", footer.transform, "ESC — LEAVE SHOP", 20f, RacingTheme.SemiBold, RacingTheme.Neutral700, 1f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceTopLeft((RectTransform)hint.transform, 44f, 0f, 380f, footerHeight);

            var (exitRt, exitBtn, _) = RacingTheme.CreateButton("ExitBtn", footer.transform, "EXIT",
                RacingTheme.Panel, RacingTheme.Ink, RacingTheme.Ink, 22f, RacingTheme.ExtraBold);
            RacingTheme.PlaceTopRight(exitRt, 44f, 0f, 140f, footerHeight);
            exitButton = exitBtn;
        }
    }
}
