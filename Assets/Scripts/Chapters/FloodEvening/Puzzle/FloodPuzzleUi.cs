using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel bulmacalarının (bahçe kapısı, şifre paneli, ipucu levhası) kodla kurulan uGUI parçaları.
    /// 640x360 referans Canvas (Screen Space Overlay), koyu panel + turkuaz vurgu, Türkçe karakterli oyun içi font (GameFonts.Story).
    /// Koordinatlar: ebeveynin ortasına göre referans pikseli (y yukarı).
    /// </summary>
    internal static class FloodPuzzleUi
    {
        public const int SortingOrder = 85;

        public static readonly Color Ink = Hex(0x0B1220);
        public static readonly Color Panel = Hex(0x112035);
        public static readonly Color PanelLight = Hex(0x1B3150);
        public static readonly Color Turquoise = Hex(0x2EC4B6);
        public static readonly Color TurquoiseDark = Hex(0x1A7F78);
        public static readonly Color Cream = Hex(0xF3ECDD);
        public static readonly Color Muted = Hex(0x9FB3C8);
        public static readonly Color Amber = Hex(0xF6C67D);
        public static readonly Color Red = Hex(0xE8574B);
        public static readonly Color Green = Hex(0x3DD68C);

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static Font ResolveFont(Font assigned)
        {
            return assigned != null ? assigned : AfetUyanisi.UI.GameFonts.Story;
        }

        /// <summary>Verilen objeyi overlay Canvas yapar (640x360 referans, eksik bileşenleri ekler).</summary>
        public static Canvas SetupCanvas(GameObject go, out CanvasGroup group)
        {
            go.layer = 5;
            var canvas = Get<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = Get<CanvasScaler>(go);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Get<GraphicRaycaster>(go);
            group = Get<CanvasGroup>(go);
            return canvas;
        }

        private static T Get<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// <summary>Fare tıklamaları için EventSystem yoksa InputSystemUIInputModule ile kurar.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        /// <summary>Tıklanan düğme seçili kalırsa Enter/Space onu da "submit" eder; tek girdi tek işlem için seçimi boşalt.</summary>
        public static void ClearSelection()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
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

        /// <summary>Ebeveyn ortasına göre konum (referans pikseli), boyut.</summary>
        public static RectTransform At(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static Image Img(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null) img.preserveAspect = true;
            return img;
        }

        /// <summary>1 piksel kenarlı kutu: dış = kenar rengi, içte "Fill".</summary>
        public static Image Box(string name, Transform parent, Color border, Color fill, float x, float y, float w, float h, bool raycast = false)
        {
            var outer = Img(name, parent, border, null, raycast);
            At(outer.rectTransform, x, y, w, h);
            var inner = Img("Fill", outer.transform, fill);
            Stretch(inner.rectTransform, 1f);
            return outer;
        }

        public static void SetBoxColors(Image box, Color border, Color fill)
        {
            box.color = border;
            var f = box.transform.Find("Fill");
            if (f != null) f.GetComponent<Image>().color = fill;
        }

        public static Text Txt(string name, Transform parent, Font font, int size, Color color, TextAnchor align)
        {
            var rt = Rect(name, parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = AfetUyanisi.UI.GameFonts.SizeFor(font, size);
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text TxtAt(string name, Transform parent, Font font, int size, Color color, TextAnchor align,
            float x, float y, float w, float h)
        {
            var t = Txt(name, parent, font, size, color, align);
            At(t.rectTransform, x, y, w, h);
            return t;
        }

        /// <summary>uGUI Button (renk geçişli) + ortalı "Label" yazısı. Gezinme kapalı. Dinleyiciyi görünüm çalışma anında bağlar (prefab'a kaydolmaz).</summary>
        public static Button Btn(string name, Transform parent, Font font, string label, int size,
            float x, float y, float w, float h, Color border, Color fill, Color textColor)
        {
            var box = Box(name, parent, border, fill, x, y, w, h, true);
            var btn = box.gameObject.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = box.transform.Find("Fill").GetComponent<Image>();
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1.6f;
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            var t = Txt("Label", box.transform, font, size, textColor, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, 2f);
            t.text = label;
            return btn;
        }

        public static float SmoothStep01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
