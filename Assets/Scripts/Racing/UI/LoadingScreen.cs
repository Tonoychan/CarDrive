using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Racing.UI
{
    /// Self-bootstrapping, DontDestroyOnLoad loading screen. Garage/Shop are full
    /// scenes (single-mode load, not overlaid on Drive_Scene) so every transition
    /// between Drive/Garage/Shop tears down and reloads a scene -- this covers the
    /// gap. Two Modernist-design looks, picked by destination: "2n" dark rule loader
    /// for Garage/Shop, "2o" red poster loader for Drive (the race).
    ///
    /// Unlike the other screen controllers, this one can't live pre-placed in any
    /// single scene (it has to survive scene transitions) -- so instead of runtime
    /// construction it's a prefab authored once in the Editor (Assets/Resources/UI/
    /// LoadingScreen.prefab, via "Build UI (Editor Only)" below) and instantiated
    /// ONCE per app run. The prefab's own Canvas hierarchy is fully visible/editable
    /// like any other prefab -- open it in Prefab Mode to tweak it.
    public class LoadingScreen : MonoBehaviour
    {
        const string PrefabResourcePath = "UI/LoadingScreen";

        static LoadingScreen instance;
        bool loading;

        [Header("Wired by Build UI (Editor Only) -- do not hand-edit")]
        [SerializeField] Canvas canvas;
        [SerializeField] GameObject ruleLoader, posterLoader;
        [SerializeField] TMP_Text ruleDestText, posterDestText, posterPctText;
        [SerializeField] RectTransform ruleBarFill, posterBarFill;

        static LoadingScreen Instance
        {
            get
            {
                if (instance == null)
                {
                    var prefab = Resources.Load<GameObject>(PrefabResourcePath);
                    if (prefab == null)
                    {
                        Debug.LogError("LoadingScreen: no prefab at Resources/" + PrefabResourcePath +
                            " -- run 'Build UI (Editor Only)' on a LoadingScreen instance and save it as that prefab.");
                        return null;
                    }
                    var go = Instantiate(prefab);
                    go.name = "LoadingScreen";
                    instance = go.GetComponent<LoadingScreen>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        public static void Load(string sceneName)
        {
            var i = Instance;
            if (i != null) i.StartCoroutine(i.LoadRoutine(sceneName));
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            bool isDrive = sceneName == SceneFlow.Drive;
            ruleLoader.SetActive(!isDrive);
            posterLoader.SetActive(isDrive);
            if (!isDrive) ruleDestText.text = sceneName.Replace("_Scene", "").ToUpperInvariant() + "_SCENE";
            else posterDestText.text = "DRIVE\nSCENE";

            SetFill(0f);
            loading = true;
            canvas.gameObject.SetActive(true);
            yield return null; // let the overlay draw at least one frame before the load stalls the main thread

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone)
            {
                SetFill(Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }

            SetFill(1f);
            loading = false;
            canvas.gameObject.SetActive(false);
        }

        void SetFill(float t)
        {
            if (ruleBarFill != null) ruleBarFill.anchorMax = new Vector2(t, 1f);
            if (posterBarFill != null) posterBarFill.anchorMax = new Vector2(t, 1f);
            if (posterPctText != null) posterPctText.text = Mathf.RoundToInt(t * 100f) + "%";
        }

        // ── build (Editor-only -- run once via this context menu, never at runtime) ──

        [ContextMenu("Build UI (Editor Only)")]
        void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // always on top during a scene transition
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildRuleLoader(canvasGo.transform);
            BuildPosterLoader(canvasGo.transform);
            canvasGo.SetActive(false);
        }

        void BuildRuleLoader(Transform parent)
        {
            ruleLoader = new GameObject("RuleLoader", typeof(RectTransform));
            ruleLoader.transform.SetParent(parent, false);
            RacingTheme.Stretch((RectTransform)ruleLoader.transform);
            var bg = RacingTheme.CreateImage("Bg", ruleLoader.transform, RacingTheme.Ink, out _);
            RacingTheme.Stretch(bg);

            var kicker = RacingTheme.CreateLabel("Loading", ruleLoader.transform, "LOADING", 26f, RacingTheme.SemiBold, RacingTheme.OnInkMuted, 4f, TextAlignmentOptions.BottomLeft);
            RacingTheme.PlaceBottomLeft((RectTransform)kicker.transform, 56f, 250f, 600f, 32f);

            ruleDestText = RacingTheme.CreateLabel("Dest", ruleLoader.transform, "GARAGE_SCENE", 96f, RacingTheme.ExtraBold, RacingTheme.Panel, 0f, TextAlignmentOptions.BottomLeft);
            RacingTheme.PlaceBottomLeft((RectTransform)ruleDestText.transform, 56f, 96f, 1400f, 110f);

            var track = RacingTheme.CreateImage("Track", ruleLoader.transform, new Color(0.247f, 0.239f, 0.235f, 1f), out _);
            RacingTheme.PlaceBottomStrip(track, 0f, 20f);
            var fill = RacingTheme.CreateImage("Fill", track, RacingTheme.Accent, out _);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            ruleBarFill = fill;

            for (int i = 0; i < 3; i++)
            {
                var dot = RacingTheme.CreateImage("Dot" + i, ruleLoader.transform, RacingTheme.Accent, out _);
                RacingTheme.PlaceBottomRight(dot, 56f + i * 32f, 96f, 20f, 20f);
            }
        }

        void BuildPosterLoader(Transform parent)
        {
            posterLoader = new GameObject("PosterLoader", typeof(RectTransform));
            posterLoader.transform.SetParent(parent, false);
            RacingTheme.Stretch((RectTransform)posterLoader.transform);
            var bg = RacingTheme.CreateImage("Bg", posterLoader.transform, RacingTheme.Accent, out _);
            RacingTheme.Stretch(bg);

            var kicker = RacingTheme.CreateLabel("Loading", posterLoader.transform, "LOADING", 26f, RacingTheme.SemiBold, RacingTheme.AccentSoft, 4f, TextAlignmentOptions.TopLeft);
            RacingTheme.PlaceTopLeft((RectTransform)kicker.transform, 64f, 64f, 700f, 32f);

            posterDestText = RacingTheme.CreateLabel("Dest", posterLoader.transform, "DRIVE\nSCENE", 150f, RacingTheme.ExtraBold, RacingTheme.Panel, 0f, TextAlignmentOptions.TopLeft);
            posterDestText.enableWordWrapping = true;
            posterDestText.lineSpacing = -20f;
            RacingTheme.PlaceTopLeft((RectTransform)posterDestText.transform, 64f, 220f, 1200f, 420f);

            var track = RacingTheme.CreateImage("Track", posterLoader.transform, new Color(1f, 1f, 1f, 0.35f), out _);
            RacingTheme.PlaceBottomStrip(track, 190f, 2f);
            var fill = RacingTheme.CreateImage("Fill", track, RacingTheme.Panel, out _);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            posterBarFill = fill;

            posterPctText = RacingTheme.CreateLabel("Pct", posterLoader.transform, "0%", 64f, RacingTheme.ExtraBold, RacingTheme.Panel, 0f, TextAlignmentOptions.BottomRight);
            RacingTheme.PlaceBottomRight((RectTransform)posterPctText.transform, 64f, 88f, 260f, 70f);
        }
    }
}
