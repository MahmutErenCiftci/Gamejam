using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel olay modüllerinin (rüzgâr göstergesi, dal/geçit paneli, bariyer uyarısı) kodla kurulan uGUI parçaları:
    /// 640x360 referanslı Screen Space Overlay canvas, koyu panel + turkuaz vurgu paleti, metin ve yerleşim yardımcıları.
    /// </summary>
    internal static class EventsUi
    {
        public static readonly Color Ink = Hex(0x0A1220);
        public static readonly Color PanelFill = Hex(0x0E1A2B, 0.94f);
        public static readonly Color PanelLight = Hex(0x18293F);
        public static readonly Color Teal = Hex(0x3CCFC4);
        public static readonly Color TealDark = Hex(0x1E7F86);
        public static readonly Color Cream = Hex(0xF3ECDD);
        public static readonly Color Muted = Hex(0xA9B6CC);
        public static readonly Color Danger = Hex(0xE8392B);
        public static readonly Color DangerDark = Hex(0x5A0F0A);
        public static readonly Color Amber = Hex(0xF2A33A);
        public static readonly Color Safe = Hex(0x18A957);

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static Font ResolveFont(Font assigned) =>
            assigned != null ? assigned : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>owner'ın altında (owner bir Canvas altındaysa sahne kökünde) 640x360 referanslı overlay canvas kurar.</summary>
        public static Canvas CreateCanvas(Component owner, string name, int order, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.layer = 5;
            if (owner.GetComponentInParent<Canvas>(true) == null) go.transform.SetParent(owner.transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = false;
            return canvas;
        }

        /// <summary>Fare tıklamaları için sahnede EventSystem yoksa InputSystemUIInputModule ile kurar.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Img(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null) img.preserveAspect = true;
            return img;
        }

        /// <summary>1 px kenarlıklı koyu panel; üstte 2 px vurgu şeridi. Dönen Image kenarlıktır, içerik "Fill"in üstüne eklenir.</summary>
        public static Image Panel(string name, Transform parent, Color edge, Color accent, bool raycast = false)
        {
            var border = Img(name, parent, edge, null, raycast);
            var fill = Img("Fill", border.transform, PanelFill);
            Stretch(fill.rectTransform, 1f);
            var band = Img("Accent", border.transform, accent);
            var b = band.rectTransform;
            b.anchorMin = new Vector2(0f, 1f);
            b.anchorMax = new Vector2(1f, 1f);
            b.pivot = new Vector2(0.5f, 1f);
            b.offsetMin = new Vector2(1f, -3f);
            b.offsetMax = new Vector2(-1f, -1f);
            return border;
        }

        public static Text Txt(string name, Transform parent, Font font, int size, Color color, TextAnchor align)
        {
            var t = Rect(name, parent).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(6, size - 3);
            t.resizeTextMaxSize = size;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Ebeveyn içinde sol-üst köşe orijinli yerleşim (referans pikseli).</summary>
        public static RectTransform TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Çapa (ax, ay) noktasına göre yerleşim; pivot çapayla aynı.</summary>
        public static RectTransform Place(RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
