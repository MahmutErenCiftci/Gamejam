using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Apartman finali yakın planlarının (mektup, kilit) ortak yardımcısı: piksel stil palet, 480x270 sanal tuval ve
    /// TAM SAYI ölçek (WindHud ile aynı: floor(min(W/480, H/270)), ConstantPixelSize), sol-üst orijinli yerleşim,
    /// metin ve fare imleci (kodla üretilen piksel el / yumruk).
    /// </summary>
    public static class ApartmentUi
    {
        public const float VirtualWidth = 480f, VirtualHeight = 270f;
        public const int DefaultSortingOrder = 70;   // FloodEveningHud 60, WindHud 66'nın üstünde

        public static readonly Color Out = new Color32(0x04, 0x10, 0x2A, 255);
        public static readonly Color Navy = new Color32(0x12, 0x20, 0x40, 245);
        public static readonly Color NavyLight = new Color32(0x1A, 0x2C, 0x55, 255);
        public static readonly Color Edge = new Color32(0x27, 0x45, 0x84, 255);
        public static readonly Color EdgeLight = new Color32(0x3A, 0x62, 0xB0, 255);
        public static readonly Color Dim = new Color32(0x0A, 0x14, 0x24, 255);
        public static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 255);
        public static readonly Color Amber = new Color32(0xF5, 0xB7, 0x31, 255);
        public static readonly Color Green = new Color32(0x7F, 0xE0, 0x9A, 255);
        public static readonly Color Red = new Color32(0xFF, 0x6B, 0x5A, 255);
        public static readonly Color Soft = new Color32(0xA9, 0xC4, 0xF0, 255);
        public static readonly Color Ink = new Color32(0x1F, 0x22, 0x36, 255);
        public static readonly Color InkBlue = new Color32(0x2E, 0x4C, 0x8E, 255);
        public static readonly Color LcdMint = new Color32(0x8F, 0xF0, 0xD0, 255);
        public static readonly Color LcdRed = new Color32(0xFF, 0x5A, 0x4A, 255);
        public static readonly Color LcdGreen = new Color32(0x6C, 0xF2, 0x8A, 255);
        public static readonly Color LcdGhost = new Color32(0x8F, 0xF0, 0xD0, 9);   // doğrusal renk uzayında düşük alfa bile parlak görünür

        /// <summary>Ekran pikseli / sanal piksel (en az 1, tam sayı).</summary>
        public static int IntegerScale() => Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.width / VirtualWidth, Screen.height / VirtualHeight)));

        /// <summary>
        /// Görünümün çizileceği tuval: üstte ConstantPixelSize ölçekli bir Canvas varsa (prefab kökü) onu kullanır,
        /// yoksa owner altında kendi Screen Space Overlay tuvalini kurar.
        /// </summary>
        public static Canvas ResolveCanvas(Component owner, int sortingOrder, out CanvasScaler scaler)
        {
            var c = owner.GetComponentInParent<Canvas>(true);
            scaler = c != null ? c.GetComponent<CanvasScaler>() : null;
            if (c != null && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                return c;
            var go = new GameObject("FE_FinalCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            go.transform.SetParent(owner.transform, false);
            c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = sortingOrder; c.pixelPerfect = true;
            scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyScale(scaler);
            return c;
        }

        public static void ApplyScale(CanvasScaler scaler)
        {
            if (scaler == null) return;
            float s = IntegerScale();
            if (!Mathf.Approximately(scaler.scaleFactor, s)) scaler.scaleFactor = s;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Ebeveynin ortasına göre (x sağa, y yukarı), boyut w x h.</summary>
        public static RectTransform Center(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y)); rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Ebeveynin sol üstünden (x sağa, y AŞAĞI) — çizimdeki piksel koordinatlarıyla birebir.</summary>
        public static RectTransform TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y)); rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static void MoveTL(RectTransform rt, float x, float y) => rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));

        /// <summary>Sprite'lı resim, sol üstten (x, y) ve sprite'ın yerel piksel boyutunda.</summary>
        public static Image Sprite(string name, Transform parent, Sprite sp, float x, float y, float fallbackW = 8f, float fallbackH = 8f)
        {
            var rt = TL(Rect(name, parent), x, y, sp != null ? sp.rect.width : fallbackW, sp != null ? sp.rect.height : fallbackH);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp; img.raycastTarget = false;
            if (sp == null) img.color = new Color(1f, 0f, 1f, 0.5f);   // eksik sprite: göze batsın
            return img;
        }

        public static Image Fill(string name, Transform parent, Color c, bool raycast = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Kenarlıklı kutu (WindHud dili): dış koyu çizgi + lacivert kenar + dolgu.</summary>
        public static Image Framed(string name, Transform parent, float x, float y, float w, float h, Color fill)
        {
            var outer = Fill(name, parent, Out); TL(outer.rectTransform, x, y, w, h);
            var edge = Fill("Edge", outer.transform, Edge); TL(edge.rectTransform, 1f, 1f, w - 2f, h - 2f);
            var inner = Fill("Fill", edge.transform, fill); TL(inner.rectTransform, 1f, 1f, w - 4f, h - 4f);
            return outer;
        }

        /// <summary>Framed kutunun boyutunu değiştirir (sol üst köşe sabit; kenar ve dolgu birlikte).</summary>
        public static void ResizeFramed(RectTransform outer, float x, float y, float w, float h)
        {
            TL(outer, x, y, w, h);
            var edge = outer.Find("Edge") as RectTransform; if (edge == null) return;
            TL(edge, 1f, 1f, w - 2f, h - 2f);
            var fill = edge.Find("Fill") as RectTransform; if (fill != null) TL(fill, 1f, 1f, w - 4f, h - 4f);
        }

        public static Text Txt(string name, Transform parent, Font font, string text, int size, Color color, TextAnchor align, float x, float y, float w, float h, bool wrap = false)
        {
            var rt = TL(Rect(name, parent), x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font != null ? font : FeUi.DefaultFont();
            t.fontSize = size; t.color = color; t.alignment = align; t.text = text ?? ""; t.raycastTarget = false; t.supportRichText = false;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.0f;
            return t;
        }

        public static void SetGroup(CanvasGroup g, bool visible, float alpha)
        {
            if (g == null) return;
            g.alpha = alpha; g.blocksRaycasts = visible; g.interactable = visible;
        }

        /// <summary>Animasyon adımı: gerçek zaman, tek karede en fazla 1/30 sn (takılmadan sonra animasyon atlanmasın).</summary>
        public static float AnimDelta() => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }

        /// <summary>İmleç ekran içinde mi (oyun penceresinin dışına çıkınca sürükleme bırakılır).</summary>
        public static bool PointerInsideScreen(Vector2 p) => p.x >= 0f && p.y >= 0f && p.x < Screen.width && p.y < Screen.height;

        // ------------------------------------------------------------------ fare imleci
        // # = dış çizgi, w = ten/krem, s = gölge. 16x16, 2x büyütülür (32x32 donanım imleci).
        private static readonly string[] HandRows =
        {
            ".....##.........", "....#ww#........", "....#ww#........", "....#ww#........", "....#ww###......", "....#ww#ww##....",
            ".##.#ww#ww#w##..", "#ww##ww#ww#ww#..", "#www#wwwwwwws#..", ".#wwwwwwwwwws#..", "..#wwwwwwwwws#..", "..#wwwwwwwwws#..",
            "...#wwwwwwws#...", "....#wwwwwws#...", "....#wwwwwws#...", "....#########...",
        };
        private static readonly string[] GrabRows =
        {
            "................", "................", "................", "................", "....##.##.##....", "...#ww#ww#ww##..",
            "...#ww#ww#ww#w#.", ".###wwwwwwwwws#.", "#ww#wwwwwwwwws#.", "#wwwwwwwwwwwws#.", ".#wwwwwwwwwwws#.", "..#wwwwwwwwws#..",
            "...#wwwwwwws#...", "....#wwwwwws#...", "....#wwwwwws#...", "....#########...",
        };
        private static Texture2D handTex, grabTex;
        public const int CursorScale = 2;
        public static Vector2 HandHotspot => new Vector2(6f, 1f) * CursorScale;
        public static Vector2 GrabHotspot => new Vector2(8f, 8f) * CursorScale;

        public static Texture2D HandCursor => handTex != null ? handTex : (handTex = BuildCursor(HandRows, "FE_Final_CursorHand"));
        public static Texture2D GrabCursor => grabTex != null ? grabTex : (grabTex = BuildCursor(GrabRows, "FE_Final_CursorGrab"));

        private static Texture2D BuildCursor(string[] rows, string name)
        {
            int n = rows.Length, s = CursorScale, size = n * s;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color32[size * size];
            Color32 ol = Out, w = Cream, sh = new Color32(0xC9, 0xBF, 0xA6, 255), clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    char ch = rows[y][x];
                    var c = ch == '#' ? ol : ch == 'w' ? w : ch == 's' ? sh : clear;
                    for (int dy = 0; dy < s; dy++)
                        for (int dx = 0; dx < s; dx++)
                            px[(size - 1 - (y * s + dy)) * size + x * s + dx] = c;   // doku satırları alttan başlar
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { handTex = null; grabTex = null; }
    }
}
