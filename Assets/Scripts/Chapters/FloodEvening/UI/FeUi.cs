using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel bölümünün kodla kurulan arayüz parçaları için küçük yardımcı (LEAD): koyu paneller, turkuaz vurgu,
    /// okunur Türkçe metin (Nunito, Resources/Objectives/Nunito-Black). Referans 640x360, match 0.5 (deprem arayüzüyle aynı).
    /// </summary>
    public static class FeUi
    {
        public static readonly Color Ink = Hex(0x0A1424);
        public static readonly Color Panel = Hex(0x0F1E33);
        public static readonly Color PanelLight = Hex(0x17304D);
        public static readonly Color Teal = Hex(0x35C6C0);
        public static readonly Color TealDark = Hex(0x1E7F86);
        public static readonly Color Cream = Hex(0xF3ECDD);
        public static readonly Color Muted = Hex(0x9FB3C8);
        public static readonly Color Amber = Hex(0xF6C67D);
        public static readonly Color AmberDark = Hex(0xF2A33A);
        public static readonly Color Red = Hex(0xE8574A);
        public static readonly Color Green = Hex(0x4CC77A);
        public static readonly Color Water = Hex(0x2E78C7);
        public static readonly Color WaterDeep = Hex(0x1B4C8F);

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        public static Color A(Color c, float a) { c.a = a; return c; }

        public static Font DefaultFont()
        {
            var f = Resources.Load<Font>("Objectives/Nunito-Black");
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Canvas CreateCanvas(Transform owner, string name, int order, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.layer = 5;
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            group = go.GetComponent<CanvasGroup>();
            EnsureEventSystem();
            return canvas;
        }

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
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Img(string name, Transform parent, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Kenarlıklı panel: dış = kenar rengi, içte 1 px boşlukla dolgu ("Fill").</summary>
        public static Image Framed(string name, Transform parent, Color border, Color fill, bool raycast = false, float inset = 1f)
        {
            var outer = Img(name, parent, border, raycast);
            var inner = Img("Fill", outer.transform, fill);
            Stretch(inner.rectTransform, inset);
            return outer;
        }

        public static void SetFrameColors(Image framed, Color border, Color fill)
        {
            framed.color = border;
            var f = framed.transform.Find("Fill");
            if (f != null) f.GetComponent<Image>().color = fill;
        }

        public static Text Txt(string name, Transform parent, Font font, int size, Color color, TextAnchor align, bool shadow = true)
        {
            var rt = Rect(name, parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.lineSpacing = 1.05f;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = A(Ink, 0.9f);
                sh.effectDistance = new Vector2(1f, -1f);
            }
            return t;
        }

        /// <summary>Uzun metin taşmasın: kutuya sığmazsa yazı min boyuta kadar küçülür.</summary>
        public static void BestFit(Text t, int min)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = t.fontSize;
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

        /// <summary>Sol-üst orijinli yerleşim (ebeveynin sol üstünden x sağa, y aşağı).</summary>
        public static RectTransform TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>anchor = pivot = (ax, ay).</summary>
        public static RectTransform Place(RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>
        /// Tehlike/uyarı ikonu: renkli kare rozet + içinde "!" (yalnız renge güvenmemek için ikon + metin birlikte kullanılır).
        /// </summary>
        public static Image Badge(string name, Transform parent, Font font, Color color, string glyph = "!")
        {
            var b = Framed(name, parent, Ink, color);
            var t = Txt("Glyph", b.transform, font, 11, Ink, TextAnchor.MiddleCenter, false);
            t.text = glyph;
            return b;
        }

        public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    }
}
