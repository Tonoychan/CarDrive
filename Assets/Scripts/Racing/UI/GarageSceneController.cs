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
    /// Dedicated Garage scene controller (Modernist "centre stage" layout, 2e): top
    /// GARAGE title + SHOP/GARAGE/DRIVE nav, the real 3D car on CarHolder stays visible
    /// through the UI (no flat render placeholder), Left/Right side columns cycle the
    /// roster, bottom bar shows live specs pulled off the displayed vehicle + read-only
    /// upgrade levels + DRIVE. The Canvas content is authored once in the Editor -- via
    /// "Build UI (Editor Only)" below, never at runtime -- and saved into the scene, so
    /// it's visible/editable without Play Mode. This component only wires up the
    /// already-existing elements at runtime.
    public class GarageSceneController : MonoBehaviour
    {
        [System.Serializable]
        public class ReadOnlyCategoryUI
        {
            public string shortName = "ENG";
            public UpgradeCategory category;
        }

        public CarRosterEntry[] roster;
        public Transform carHolder;
        public ReadOnlyCategoryUI[] categories = new ReadOnlyCategoryUI[3];
        public RaceGarageController garage;

        [Header("Wired by Build UI (Editor Only) -- do not hand-edit")]
        [SerializeField] Button leftArrowButton;
        [SerializeField] Button rightArrowButton;
        [SerializeField] Button driveButton;
        [SerializeField] Button shopNavButton;
        [SerializeField] TMP_Text carNameText;
        [SerializeField] TMP_Text[] specValueTexts = new TMP_Text[3];
        [SerializeField] TMP_Text[] upgradeLevelTexts;

        GameObject currentDisplay;
        int index;

        void Start()
        {
            if (leftArrowButton != null) leftArrowButton.onClick.AddListener(() => Cycle(-1));
            if (rightArrowButton != null) rightArrowButton.onClick.AddListener(() => Cycle(1));
            if (driveButton != null) driveButton.onClick.AddListener(OnDrive);
            if (shopNavButton != null) shopNavButton.onClick.AddListener(() => SceneFlow.LoadShop());

            index = Mathf.Clamp(CarSelection.LoadIndex(), 0, roster != null && roster.Length > 0 ? roster.Length - 1 : 0);
            ShowCar(index);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return;
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) Cycle(1);
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame) Cycle(-1);
#endif
        }

        void Cycle(int direction)
        {
            if (roster == null || roster.Length == 0) return;
            index = (index + direction + roster.Length) % roster.Length;
            ShowCar(index);
        }

        void ShowCar(int i)
        {
            if (currentDisplay != null) Destroy(currentDisplay);
            if (roster == null || i < 0 || i >= roster.Length || roster[i].vehiclePrefab == null) return;

            var entry = roster[i];
            // "- Empty" variant (mesh only, no Vehicle/Rigidbody) for the live display --
            // instantiating the full driveable prefab here throws, because Vehicle.Awake()
            // tries to auto-bootstrap an MVC.VehicleManager + camera rig this scene
            // doesn't have. Falls back to the driveable prefab if no preview was set.
            GameObject displayPrefab = entry.previewPrefab != null ? entry.previewPrefab : entry.vehiclePrefab;

            Transform spawn = carHolder != null ? carHolder : transform;
            currentDisplay = Instantiate(displayPrefab, spawn.position, spawn.rotation);

            // In case a non-Empty prefab ever ends up here -- freeze physics/MVC
            // simulation so it just sits on the holder instead of reacting to gravity.
            var liveVehicle = currentDisplay.GetComponent<Vehicle>();
            if (liveVehicle != null) liveVehicle.enabled = false;
            var liveRb = currentDisplay.GetComponent<Rigidbody>();
            if (liveRb != null) liveRb.isKinematic = true;

            if (carNameText != null) carNameText.text = entry.displayName.ToUpperInvariant();
            // Stats read from the driveable prefab ASSET directly (not the instantiated
            // preview) -- reading a component off a prefab asset doesn't run Awake(), so
            // this works whether or not a preview variant is in use.
            RefreshSpecs(entry.vehiclePrefab.GetComponent<Vehicle>(), entry.vehiclePrefab.GetComponent<Rigidbody>());
            RefreshUpgradeLevels();
        }

        void RefreshSpecs(Vehicle vehicle, Rigidbody rb)
        {
            if (specValueTexts == null || specValueTexts.Length < 3 || specValueTexts[0] == null) return;
            float power = vehicle != null && vehicle.Engine != null ? vehicle.Engine.Power : 0f;
            float mass = rb != null ? rb.mass : 0f;
            float topSpeed = vehicle != null ? vehicle.TopSpeed : 0f;
            specValueTexts[0].text = Mathf.RoundToInt(power) + " HP";
            specValueTexts[1].text = Mathf.RoundToInt(mass) + " KG";
            specValueTexts[2].text = Mathf.RoundToInt(topSpeed) + " KM/H";
        }

        void RefreshUpgradeLevels()
        {
            if (upgradeLevelTexts == null) return;
            for (int i = 0; i < categories.Length && i < upgradeLevelTexts.Length; i++)
            {
                var cat = categories[i]?.category;
                if (cat?.tiers == null || upgradeLevelTexts[i] == null) continue;

                int level = 0;
                foreach (var tier in cat.tiers)
                {
                    if (tier != null && garage != null && garage.IsOwned(tier)) level++;
                    else break;
                }
                upgradeLevelTexts[i].text = "LV " + level + "/" + cat.tiers.Length;
            }
        }

        void OnDrive()
        {
            CarSelection.SaveIndex(index);
            SceneFlow.LoadDrive();
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

            var root = new GameObject("GarageUI", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RacingTheme.Stretch((RectTransform)root.transform);

            BuildTopBar(root.transform);
            BuildSideArrows(root.transform);
            BuildBottomBar(root.transform);
        }

        void BuildTopBar(Transform parent)
        {
            var bar = RacingTheme.CreateImage("TopBar", parent, RacingTheme.Panel, out _);
            RacingTheme.PlaceTopStrip(bar, 0f, 104f);
            var rule = RacingTheme.CreateImage("Rule", bar, RacingTheme.Ink, out _);
            RacingTheme.PlaceBottomStrip(rule, 0f, 2f);

            var title = RacingTheme.CreateLabel("Title", bar, "GARAGE", 44f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.MidlineLeft);
            RacingTheme.PlaceTopLeft((RectTransform)title.transform, 48f, 30f, 360f, 50f);

            var nav = new GameObject("Nav", typeof(RectTransform));
            nav.transform.SetParent(bar, false);
            RacingTheme.PlaceTopRight((RectTransform)nav.transform, 48f, 36f, 420f, 40f);
            var navLayout = nav.AddComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 26f;
            navLayout.childAlignment = TextAnchor.MiddleRight;
            navLayout.childForceExpandWidth = false;
            navLayout.childForceExpandHeight = true;

            shopNavButton = CreateNavLink(nav.transform, "SHOP", RacingTheme.Neutral700, interactive: true);
            CreateNavLink(nav.transform, "GARAGE", RacingTheme.Accent, interactive: false);
            CreateNavLink(nav.transform, "DRIVE", RacingTheme.Neutral700, interactive: false); // wired to driveButton separately isn't needed -- bottom bar DRIVE covers it
        }

        Button CreateNavLink(Transform parent, string label, Color color, bool interactive)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = label.Length * 20f + 10f;
            var text = RacingTheme.CreateLabel(label + "_Label", go.transform, label, 24f, RacingTheme.SemiBold, color, 3f, TextAlignmentOptions.MidlineRight);
            RacingTheme.Stretch((RectTransform)text.transform);
            if (!interactive) return null;

            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0f); // invisible hit target
            btn.targetGraphic = img;
            return btn;
        }

        void BuildSideArrows(Transform parent)
        {
            var (leftRt, leftBtn, _) = RacingTheme.CreateButton("LeftArrow", parent, "<", RacingTheme.Panel, RacingTheme.Accent, RacingTheme.Ink, 56f, RacingTheme.ExtraBold);
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0f, 1f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.offsetMin = new Vector2(0f, 202f);
            leftRt.offsetMax = new Vector2(96f, -104f);
            leftArrowButton = leftBtn;

            var (rightRt, rightBtn, _) = RacingTheme.CreateButton("RightArrow", parent, ">", RacingTheme.Panel, RacingTheme.Accent, RacingTheme.Ink, 56f, RacingTheme.ExtraBold);
            rightRt.anchorMin = new Vector2(1f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.offsetMin = new Vector2(-96f, 202f);
            rightRt.offsetMax = new Vector2(0f, -104f);
            rightArrowButton = rightBtn;

            carNameText = RacingTheme.CreateLabel("CarName", parent, "", 64f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.BottomLeft);
            RacingTheme.PlaceBottomLeft((RectTransform)carNameText.transform, 120f, 224f, 1600f, 80f);
        }

        void BuildBottomBar(Transform parent)
        {
            var bar = RacingTheme.CreateImage("BottomBar", parent, RacingTheme.Panel, out _);
            RacingTheme.PlaceBottomStrip(bar, 0f, 200f);
            var rule = RacingTheme.CreateImage("Rule", bar, RacingTheme.Ink, out _);
            RacingTheme.PlaceTopStrip(rule, 0f, 2f);

            string[] specKeys = { "POWER", "MASS", "TOP SPEED" };
            specValueTexts = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var col = new GameObject("Spec" + i, typeof(RectTransform));
                col.transform.SetParent(bar, false);
                RacingTheme.PlaceTopLeft((RectTransform)col.transform, 48f + i * 220f, 26f, 200f, 150f);
                RacingTheme.CreateLabel("Key", col.transform, specKeys[i], 18f, RacingTheme.SemiBold, RacingTheme.Neutral700, 3f, TextAlignmentOptions.TopLeft);
                var val = RacingTheme.CreateLabel("Val", col.transform, "0", 40f, RacingTheme.ExtraBold, RacingTheme.Ink, 0f, TextAlignmentOptions.BottomLeft);
                RacingTheme.PlaceBottomLeft((RectTransform)val.transform, 0f, 0f, 200f, 60f);
                specValueTexts[i] = val;
            }

            upgradeLevelTexts = new TMP_Text[categories.Length];
            for (int i = 0; i < categories.Length; i++)
            {
                var col = new GameObject("Upg" + i, typeof(RectTransform));
                col.transform.SetParent(bar, false);
                RacingTheme.PlaceTopLeft((RectTransform)col.transform, 720f + i * 130f, 26f, 120f, 150f);
                string shortName = categories[i] != null ? categories[i].shortName : "";
                RacingTheme.CreateLabel("Key", col.transform, shortName, 22f, RacingTheme.ExtraBold, RacingTheme.Ink, 1f, TextAlignmentOptions.TopLeft);
                var lv = RacingTheme.CreateLabel("Lv", col.transform, "LV 0/4", 20f, RacingTheme.SemiBold, RacingTheme.Accent, 0f, TextAlignmentOptions.BottomLeft);
                RacingTheme.PlaceBottomLeft((RectTransform)lv.transform, 0f, 0f, 120f, 40f);
                upgradeLevelTexts[i] = lv;
            }

            var (driveRt, driveBtn, _) = RacingTheme.CreateButton("DriveBtn", bar, "DRIVE", RacingTheme.Accent, RacingTheme.AccentHover, RacingTheme.Panel, 44f, RacingTheme.ExtraBold);
            RacingTheme.PlaceBottomRight(driveRt, 0f, 2f, 360f, 198f);
            driveButton = driveBtn;
        }
    }
}
