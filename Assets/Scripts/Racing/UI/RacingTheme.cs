using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Racing.UI
{
    /// Shared visual language for the "Modernist" racing UI redesign (ink / off-white /
    /// red, Archivo type, sharp 0-radius 2px-bordered panels). Every screen builder --
    /// HUD, Garage, Shop, trigger prompts, loading -- constructs its hierarchy through
    /// these helpers so the look only needs tuning in one place. Coordinates in the
    /// Place* helpers mirror the design doc's CSS (top-left origin, Y grows down) --
    /// they convert to Unity's pivot/anchor convention (Y grows up) internally.
    public static class RacingTheme
    {
        public static readonly Color Ink = new Color32(0x20, 0x1e, 0x1d, 0xff);
        public static readonly Color Panel = new Color32(0xf3, 0xf2, 0xf2, 0xff);
        public static readonly Color PageBg = new Color32(0xe9, 0xe7, 0xe3, 0xff);
        public static readonly Color Accent = new Color32(0xec, 0x30, 0x13, 0xff);
        public static readonly Color AccentHover = new Color32(0xc8, 0x25, 0x0d, 0xff);
        public static readonly Color AccentSoft = new Color32(0xf9, 0xd5, 0xcf, 0xff);
        public static readonly Color Neutral300 = new Color32(0xd6, 0xd3, 0xce, 0xff);
        public static readonly Color Neutral500 = new Color32(0x9b, 0x97, 0x95, 0xff);
        public static readonly Color Neutral600 = new Color32(0x7b, 0x78, 0x76, 0xff);
        public static readonly Color Neutral700 = new Color32(0x5c, 0x59, 0x57, 0xff);
        public static readonly Color OnInkMuted = new Color32(0x8f, 0x8c, 0x89, 0xff);

        static TMP_FontAsset regular, semiBold, extraBold;
        public static TMP_FontAsset Regular => regular != null ? regular : regular = Resources.Load<TMP_FontAsset>("Fonts/Archivo/Archivo-Regular SDF");
        public static TMP_FontAsset SemiBold => semiBold != null ? semiBold : semiBold = Resources.Load<TMP_FontAsset>("Fonts/Archivo/Archivo-SemiBold SDF");
        public static TMP_FontAsset ExtraBold => extraBold != null ? extraBold : extraBold = Resources.Load<TMP_FontAsset>("Fonts/Archivo/Archivo-ExtraBold SDF");

        static Sprite solid;
        public static Sprite Solid => solid != null ? solid : solid = CreateSolidSprite();

        static Sprite circleMask;
        public static Sprite CircleMask => circleMask != null ? circleMask : circleMask = Resources.Load<Sprite>("UI/CircleMask");

        static RenderTexture minimapRT;
        public static RenderTexture MinimapRT => minimapRT != null ? minimapRT : minimapRT = Resources.Load<RenderTexture>("UI/MinimapRT");

        static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "RacingTheme_Solid" };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        // ── construction ──────────────────────────────────────────────────

        public static RectTransform CreateImage(string name, Transform parent, Color color, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            image = go.GetComponent<Image>();
            image.sprite = Solid;
            image.color = color;
            return go.GetComponent<RectTransform>();
        }

        /// A sharp-cornered panel with a solid ink border: a slightly larger ink-colored
        /// backing Image behind an inset content Image. Simplest way to get a crisp
        /// square border in uGUI without 9-slice art. Returns the outer (frame) rect --
        /// its size/position IS the panel's visual bounds; content sits inset by borderPx.
        public static RectTransform CreateFramedPanel(string name, Transform parent, Color fill, out RectTransform content, float borderPx = 2f)
        {
            var frameRt = CreateImage(name, parent, Ink, out _);
            content = CreateImage(name + "_Fill", frameRt, fill, out _);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(borderPx, borderPx);
            content.offsetMax = new Vector2(-borderPx, -borderPx);
            return frameRt;
        }

        /// A circular ink-ringed, masked panel -- backing ink circle + inset content
        /// area clipped round via a UI Mask using the same circle sprite. Returns the
        /// outer (frame) rect; parent children into 'content' to have them clipped.
        public static RectTransform CreateCircleFramedPanel(string name, Transform parent, out RectTransform content, float borderPx = 3f)
        {
            var frameRt = CreateImage(name, parent, Ink, out var frameImg);
            frameImg.sprite = CircleMask;
            frameImg.type = Image.Type.Simple;

            var maskGo = new GameObject(name + "_Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(frameRt, false);
            var maskRt = (RectTransform)maskGo.transform;
            Stretch(maskRt, borderPx, borderPx, borderPx, borderPx);
            var maskImg = maskGo.GetComponent<Image>();
            maskImg.sprite = CircleMask;
            maskImg.type = Image.Type.Simple;
            maskGo.GetComponent<Mask>().showMaskGraphic = false;

            content = maskRt;
            return frameRt;
        }

        public static TMP_Text CreateLabel(string name, Transform parent, string text, float size, TMP_FontAsset font, Color color,
            float spacing = 0f, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.font = font != null ? font : ExtraBold;
            tmp.color = color;
            tmp.characterSpacing = spacing;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        public static (RectTransform rect, Button button, TMP_Text label) CreateButton(
            string name, Transform parent, string text, Color bg, Color hoverBg, Color textColor, float fontSize,
            TMP_FontAsset font = null, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = CreateImage(name, parent, bg, out var image);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = bg;
            colors.highlightedColor = hoverBg;
            colors.pressedColor = hoverBg;
            colors.selectedColor = bg;
            colors.disabledColor = Neutral500;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = CreateLabel(name + "_Label", rt, text, fontSize, font ?? ExtraBold, textColor, 2f, align);
            var labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            return (rt, button, label);
        }

        // ── layout (CSS-style: top-left origin, Y grows down) ───────────────

        public static void PlaceTopLeft(RectTransform rt, float x, float yFromTop, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
            rt.sizeDelta = new Vector2(width, height);
        }

        public static void PlaceBottomLeft(RectTransform rt, float x, float yFromBottom, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, yFromBottom);
            rt.sizeDelta = new Vector2(width, height);
        }

        public static void PlaceTopRight(RectTransform rt, float xFromRight, float yFromTop, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-xFromRight, -yFromTop);
            rt.sizeDelta = new Vector2(width, height);
        }

        public static void PlaceBottomRight(RectTransform rt, float xFromRight, float yFromBottom, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-xFromRight, yFromBottom);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// Anchor-stretched inset from all four edges of the parent (CSS inset:t r b l).
        public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// Full-width strip pinned to the top, CSS `left:0;right:0;top:Ypx;height:Hpx`.
        public static void PlaceTopStrip(RectTransform rt, float yFromTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -yFromTop);
            rt.sizeDelta = new Vector2(0f, height);
        }

        /// Full-width strip pinned to the bottom, CSS `left:0;right:0;bottom:Ypx;height:Hpx`.
        public static void PlaceBottomStrip(RectTransform rt, float yFromBottom, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, yFromBottom);
            rt.sizeDelta = new Vector2(0f, height);
        }
    }
}
