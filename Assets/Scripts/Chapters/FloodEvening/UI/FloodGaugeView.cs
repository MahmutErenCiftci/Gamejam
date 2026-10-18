using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Sağ kenardaki "SEL DURUMU" göstergesi (120 sn sel sayacı). Lacivert piksel panel: başlık, saat simgesi + geçen süre,
    /// dikey cam tüp (aşağıdan yükselen su, sürekli dalgalanan yüzey, artan kabarcıklar, cam yansıması), temsilî ölçek
    /// (0 m … 1.5 m), yüzde ve kısa aşama yazısı. Çerçeve rengi: normal mavi, %75+ turuncu, %100 kırmızı (nabız + taşma),
    /// güvenli bölgede yeşil. Tüp her kare küçük bir Texture2D'ye çizilir (20x112 px; filtre yok), tuval tam sayı ölçekle
    /// büyür (480x270 sanal piksel), böylece piksel sanatı bozulmaz. Seviyeyi FloodEveningHud.SetLevel → SetLevel verir.
    /// </summary>
    public class FloodGaugeView : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 61;
        [Tooltip("Göstergenin hedef seviyeye yetişme hızı (birim/sn) — ani kademe olmasın")]
        [SerializeField] private float levelSpeed = 0.12f;

        private const int TubeW = 20, TubeH = 112, PanelW = 68, PanelH = 196;
        private static readonly Color NormalBorder = FeUi.Hex(0x3C8FD8);
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 GlassIn = new Color32(11, 22, 38, 235);
        private static readonly Color32 WaterTop = new Color32(86, 190, 255, 255);
        private static readonly Color32 WaterMid = new Color32(38, 132, 230, 255);
        private static readonly Color32 WaterLow = new Color32(24, 78, 160, 255);
        private static readonly Color32 Foam = new Color32(214, 244, 255, 255);
        private static readonly Color32 Crest = new Color32(150, 222, 255, 255);
        private static readonly Color32 Bubble = new Color32(170, 230, 255, 255);
        private static readonly Color32 Shine = new Color32(255, 255, 255, 70);
        private static readonly Color32 TickCol = new Color32(205, 225, 245, 200);

        private struct Bub { public float x, y, v; public bool big; }
        private readonly List<Bub> bubbles = new List<Bub>();
        private readonly List<Vector3> drops = new List<Vector3>(); // taşma damlaları (x, y, vy)

        private bool built, visible, safe, critical;
        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasGroup group;
        private Image frame, frameGlow;
        private RawImage tubeImage;
        private Texture2D tex;
        private Color32[] px;
        private Text titleText, stageText, noteText;
        private PixelDigits timeText, percentText;
        private Image clock;
        private float shown, target, elapsed, total = 120f, pulse, bubbleAcc;
        private string stageOverride;
        // her kare yeni metin üretmemek için: yüzde / saniye değişince bir kez (çöp yok)
        private int lastPct = -1, lastSec = -1;
        private string pctStr = "0%", clockStr = "00:00";
        private Color lastBorder = Color.clear;

        private void Awake() => Build();

        private readonly List<PixelDigits> digits = new List<PixelDigits>();

        private void OnDestroy()
        {
            // çalışma anında üretilen dokular sahneyle silinmez: Tekrar Dene'de birikmesin
            if (tex != null) Destroy(tex);
            if (clock != null && clock.sprite != null) { Destroy(clock.sprite.texture); Destroy(clock.sprite); }
            foreach (var d in digits) d.Release();
        }

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            var go = new GameObject("FE_FloodGaugeCanvas", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;

            var panel = Rect("SelDurumu", go.transform, new Vector2(1f, 0.5f), new Vector2(-6f - PanelW * 0.5f, 10f), new Vector2(PanelW, PanelH));
            frameGlow = Img("Glow", panel, new Color(0.3f, 0.6f, 1f, 0f));
            Stretch(frameGlow.rectTransform, -1f);
            frame = FeUi.Framed("Frame", panel, FeUi.Hex(0x3C8FD8), FeUi.A(FeUi.Panel, 0.94f));
            Stretch(frame.rectTransform, 0f);

            titleText = Txt("Title", panel, "SEL DURUMU", 8, FeUi.Cream, TextAnchor.MiddleCenter);
            TL(titleText.rectTransform, 0f, 4f, PanelW, 10f);

            clock = Img("Clock", panel, Color.white);
            clock.sprite = ClockSprite();
            TL(clock.rectTransform, 12f, 17f, 9f, 9f);
            timeText = new PixelDigits("Time", panel, 2); digits.Add(timeText);
            timeText.Place(24f, 17f);

            tex = new Texture2D(TubeW, TubeH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FloodGaugeTube" };
            px = new Color32[TubeW * TubeH];
            tubeImage = Rect("Tube", panel, new Vector2(0f, 1f), Vector2.zero, new Vector2(TubeW, TubeH)).gameObject.AddComponent<RawImage>();
            tubeImage.texture = tex; tubeImage.raycastTarget = false;
            TL(tubeImage.rectTransform, 10f, 32f, TubeW, TubeH);

            string[] labels = { "1.5 m", "1.0 m", "0.5 m", "0 m" };
            for (int i = 0; i < 4; i++)
            {
                var l = new PixelDigits("Scale" + i, panel, 1); digits.Add(l);
                l.Set(labels[i], FeUi.Muted);
                float y = 32f + 3f + i * ((TubeH - 6f) / 3f) - 3f;
                l.Place(34f, y);
            }
            percentText = new PixelDigits("Percent", panel, 3); digits.Add(percentText);
            percentText.PlaceCentered(PanelW * 0.5f, 32f + TubeH + 3f);
            stageText = Txt("Stage", panel, "", 8, FeUi.Muted, TextAnchor.UpperCenter);
            stageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            TL(stageText.rectTransform, 3f, 32f + TubeH + 21f, PanelW - 6f, 22f);
            noteText = Txt("Note", panel, "ölçek temsilî", 8, FeUi.A(FeUi.Muted, 0.6f), TextAnchor.MiddleCenter);
            noteText.fontSize = 8;
            TL(noteText.rectTransform, 0f, PanelH + 2f, PanelW, 10f);
            ApplyScale();
            Redraw();
        }

        public void SetVisible(bool value) { Build(); visible = value; }
        public void SetLevel(float level01) => target = Mathf.Clamp01(level01);
        public void SetLevelNow(float level01) { Build(); target = shown = Mathf.Clamp01(level01); Redraw(); }
        public void SetTime(float elapsedSeconds, float totalSeconds) { elapsed = Mathf.Max(0f, elapsedSeconds); total = Mathf.Max(1f, totalSeconds); }
        public void SetSafe(bool value) { Build(); safe = value; }
        public void SetCritical(bool value) { Build(); critical = value; }
        /// <summary>Aşama yazısının yerine sabit metin (null = ilerlemeye göre otomatik).</summary>
        public void SetStageText(string text) => stageOverride = text;

        private void Update()
        {
            if (!built) return;
            ApplyScale();
            float dt = Time.unscaledDeltaTime;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, dt * 3f);
            if (group.alpha <= 0.001f && !visible) return;
            shown = Mathf.MoveTowards(shown, target, levelSpeed * dt * (target - shown > 0.15f ? 4f : 1f));
            pulse += dt;

            int pct = Mathf.Clamp(Mathf.RoundToInt(shown * 100f), 0, 100);
            int sec = Mathf.Clamp(Mathf.FloorToInt(elapsed), 0, 5999);
            if (pct != lastPct) { lastPct = pct; pctStr = pct + "%"; }
            if (sec != lastSec) { lastSec = sec; clockStr = (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00"); }

            Color border, txt;
            float glow = 0f;
            if (safe) { border = FeUi.Green; txt = FeUi.Green; }
            else if (critical || shown >= 0.999f) { border = FeUi.Red; txt = FeUi.Red; glow = 0.35f + 0.35f * Mathf.Sin(pulse * 8f); }
            else if (shown >= 0.75f) { border = FeUi.AmberDark; txt = FeUi.AmberDark; glow = shown >= 0.9f ? 0.2f + 0.2f * Mathf.Sin(pulse * 6f) : 0.12f; }
            else { border = NormalBorder; txt = FeUi.Cream; }
            if (border != lastBorder) { lastBorder = border; FeUi.SetFrameColors(frame, border, FeUi.A(FeUi.Panel, 0.94f)); }
            frameGlow.color = FeUi.A(border, glow);
            percentText.Set(pctStr, txt);
            percentText.PlaceCentered(PanelW * 0.5f, 32f + TubeH + 3f);
            timeText.Set(clockStr, safe ? FeUi.Green : shown >= 0.75f ? txt : FeUi.Amber);
            stageText.text = stageOverride ?? (safe ? "Güvendesin. Sel sayacı durdu." : StageLine(shown));
            stageText.color = safe ? FeUi.Green : shown >= 0.75f ? FeUi.A(txt, 0.95f) : FeUi.Muted;

            Simulate(dt);
            Redraw();
        }

        private static string StageLine(float p)
        {
            if (p >= 0.999f) return "Kritik seviye!";
            if (p >= 0.75f) return "Bazı yollar kapanıyor";
            if (p >= 0.5f) return "Şiddetli yağmur, su yükseliyor";
            if (p >= 0.25f) return "Yağmur artıyor, sular birikiyor";
            if (p >= 0.1f) return "Hafif yağmur, birikintiler";
            return "Yağmur başladı";
        }

        private void ApplyScale()
        {
            int s = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.width / 480f, Screen.height / 270f)));
            if (!Mathf.Approximately(scaler.scaleFactor, s)) scaler.scaleFactor = s;
        }

        // ---------------------------------------------------------------- tüp çizimi
        private int FillPx => Mathf.RoundToInt(shown * (TubeH - 6));

        private void Simulate(float dt)
        {
            int fill = FillPx;
            float rate = fill > 2 ? 2f + shown * 14f : 0f;
            bubbleAcc += rate * dt;
            while (bubbleAcc >= 1f)
            {
                bubbleAcc -= 1f;
                bubbles.Add(new Bub { x = Random.Range(4f, TubeW - 5f), y = 3f + Random.Range(0f, Mathf.Max(1f, fill * 0.6f)), v = Random.Range(8f, 18f), big = Random.value < 0.25f });
            }
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                b.y += b.v * dt;
                b.x += Mathf.Sin((b.y + i) * 0.6f) * dt * 3f;
                if (b.y >= 3f + fill - 2f) bubbles.RemoveAt(i); else bubbles[i] = b;
            }
            // Kritik: tepeden taşan damlalar
            if ((critical || shown >= 0.999f) && !safe && Random.value < dt * 14f)
                drops.Add(new Vector3(Random.Range(3f, TubeW - 3f), TubeH - 3f, Random.Range(10f, 22f)));
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.y += d.z * dt; d.z -= 60f * dt;
                if (d.y < TubeH - 12f || d.y >= TubeH) drops.RemoveAt(i); else drops[i] = d;
            }
        }

        private void Redraw()
        {
            if (tex == null) return;
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            Color32 glass = GlassColor();
            // cam gövde (yuvarlatılmış köşe) + iç boşluk
            for (int y = 0; y < TubeH; y++)
                for (int x = 0; x < TubeW; x++)
                {
                    bool corner = (x == 0 || x == TubeW - 1) && (y == 0 || y == TubeH - 1);
                    if (corner) continue;
                    bool edge = x == 0 || x == TubeW - 1 || y == 0 || y == TubeH - 1;
                    px[y * TubeW + x] = edge ? glass : GlassIn;
                }
            // ağzı açık tüp: üst kenar ortada boş, iki yanında çıkıntı
            for (int x = 3; x < TubeW - 3; x++) px[(TubeH - 1) * TubeW + x] = GlassIn;

            int fill = FillPx;
            float t = Time.unscaledTime;
            float amp = 0.6f + shown * 1.4f;
            if (fill > 0)
            {
                for (int x = 2; x < TubeW - 2; x++)
                {
                    float w = Mathf.Sin(t * 3.1f + x * 0.75f) * amp + Mathf.Sin(t * 1.7f - x * 0.33f) * amp * 0.5f;
                    int top = Mathf.Clamp(3 + fill + Mathf.RoundToInt(w), 2, TubeH - 3);
                    for (int y = 2; y <= top; y++)
                    {
                        float k = (float)(y - 2) / Mathf.Max(1, TubeH - 6);
                        Color32 c = k > 0.6f ? WaterTop : k > 0.3f ? WaterMid : WaterLow;
                        if (top - y <= 2 && top - y >= 1) c = Crest;
                        if (y == top) c = ((x + Mathf.FloorToInt(t * 6f)) % 5 == 0) ? Foam : Crest;
                        px[y * TubeW + x] = c;
                    }
                }
                foreach (var b in bubbles)
                {
                    int bx = Mathf.Clamp(Mathf.RoundToInt(b.x), 2, TubeW - 3), by = Mathf.Clamp(Mathf.RoundToInt(b.y), 2, TubeH - 3);
                    px[by * TubeW + bx] = Bubble;
                    if (b.big && bx + 1 < TubeW - 2 && by + 1 < TubeH - 2) { px[(by + 1) * TubeW + bx] = Bubble; px[by * TubeW + bx + 1] = Bubble; }
                }
            }
            // cam yansıması (sol iç kenarda dikey çizgi)
            for (int y = 6; y < TubeH - 8; y++) Blend(3, y, Shine);
            for (int y = 10; y < TubeH - 30; y += 1) if ((y / 7) % 3 != 2) Blend(4, y, Shine);
            // ölçek çizgileri (sağ iç kenar): 0 / 0.5 / 1.0 / 1.5 m büyük, aralar küçük
            for (int i = 0; i <= 12; i++)
            {
                int y = 3 + Mathf.RoundToInt(i * (TubeH - 6) / 12f);
                int len = i % 4 == 0 ? 5 : 2;
                for (int x = TubeW - 2 - len; x < TubeW - 2; x++) Blend(x, Mathf.Min(y, TubeH - 2), TickCol);
            }
            foreach (var d in drops)
            {
                int dx = Mathf.Clamp(Mathf.RoundToInt(d.x), 1, TubeW - 2), dy = Mathf.Clamp(Mathf.RoundToInt(d.y), 1, TubeH - 1);
                px[dy * TubeW + dx] = Foam;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        private Color32 GlassColor()
        {
            if (safe) return new Color32(76, 199, 122, 255);
            if (critical || shown >= 0.999f) return Mathf.Sin(pulse * 8f) > 0f ? new Color32(255, 96, 80, 255) : new Color32(232, 87, 74, 255);
            if (shown >= 0.75f) return new Color32(242, 163, 58, 255);
            return new Color32(150, 205, 245, 255);
        }

        private void Blend(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= TubeW || y >= TubeH) return;
            int i = y * TubeW + x;
            var b = px[i];
            if (b.a == 0) return;
            float a = c.a / 255f;
            px[i] = new Color32((byte)(b.r + (c.r - b.r) * a), (byte)(b.g + (c.g - b.g) * a), (byte)(b.b + (c.b - b.b) * a), b.a);
        }

        private static Sprite ClockSprite()
        {
            string[] rows = {
                "..###..",
                ".#...#.",
                "#..#..#",
                "#..##.#",
                "#.....#",
                ".#...#.",
                "..###..",
            };
            var t = new Texture2D(7, 7, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "FloodGaugeClock" };
            var c = new Color32(246, 198, 125, 255);
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    t.SetPixel(x, 6 - y, rows[y][x] == '#' ? (Color)c : Color.clear);
            t.Apply(false);
            return Sprite.Create(t, new Rect(0, 0, 7, 7), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>
        /// Göstergenin rakamları için küçük piksel yazı (3x5 glif + 1 px gölge): süre, yüzde ve ölçek. Font küçük boyda "5"i
        /// "S" gibi çizdiği için rakamlar dokuya elle çizilir; ölçek (1/2/3) tuvalin tam sayı ölçeğinin üstüne eklenir.
        /// </summary>
        private class PixelDigits
        {
            private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
            {
                ['0'] = new[] { "111", "101", "101", "101", "111" }, ['1'] = new[] { "010", "110", "010", "010", "111" },
                ['2'] = new[] { "111", "001", "111", "100", "111" }, ['3'] = new[] { "111", "001", "111", "001", "111" },
                ['4'] = new[] { "101", "101", "111", "001", "001" }, ['5'] = new[] { "111", "100", "111", "001", "111" },
                ['6'] = new[] { "111", "100", "111", "101", "111" }, ['7'] = new[] { "111", "001", "010", "010", "010" },
                ['8'] = new[] { "111", "101", "111", "101", "111" }, ['9'] = new[] { "111", "101", "111", "001", "111" },
                [':'] = new[] { "0", "1", "0", "1", "0" }, ['.'] = new[] { "0", "0", "0", "0", "1" },
                ['%'] = new[] { "101", "001", "010", "100", "101" }, ['m'] = new[] { "00000", "11110", "10101", "10101", "10101" },
                [' '] = new[] { "0", "0", "0", "0", "0" },
            };
            private readonly RawImage image;
            private readonly int scale;
            private Texture2D tex;
            private Color32[] buf;
            private string current;
            private Color currentColor;

            public PixelDigits(string name, Transform parent, int scale)
            {
                this.scale = Mathf.Max(1, scale);
                image = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
                image.transform.SetParent(parent, false);
                image.raycastTarget = false;
            }

            public void Set(string text, Color color)
            {
                if (text == current && color == currentColor) return;
                current = text; currentColor = color;
                int w = 1;
                foreach (char ch in text) w += (Glyphs.TryGetValue(ch, out var g) ? g[0].Length : 2) + 1;
                int h = 6;
                if (tex == null || tex.width != w) { if (tex != null) Object.Destroy(tex); tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FloodGaugeDigits" }; }
                if (buf == null || buf.Length != w * h) buf = new Color32[w * h];
                else System.Array.Clear(buf, 0, buf.Length);
                var px = buf;
                Color32 fg = color, sh = new Color32(10, 20, 36, 220);
                int x = 0;
                foreach (char ch in text)
                {
                    if (!Glyphs.TryGetValue(ch, out var g)) { x += 3; continue; }
                    for (int r = 0; r < 5; r++)
                        for (int c = 0; c < g[r].Length; c++)
                        {
                            if (g[r][c] != '1') continue;
                            int yy = h - 1 - r;                 // doku y yukarı
                            int si = (yy - 1) * w + x + c + 1;  // gölge: sağ-alt
                            if (yy - 1 >= 0 && px[si].a == 0) px[si] = sh;
                            px[yy * w + x + c] = fg;
                        }
                    x += g[0].Length + 1;
                }
                tex.SetPixels32(px); tex.Apply(false);
                image.texture = tex;
                image.rectTransform.sizeDelta = new Vector2(w * scale, h * scale);
            }

            public void Release() { if (tex != null) Object.Destroy(tex); tex = null; }

            public void Place(float x, float y)
            {
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
            }

            public void PlaceCentered(float cx, float y)
            {
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(Mathf.Round(cx - rt.sizeDelta.x * 0.5f), -Mathf.Round(y));
            }
        }

        // ---------------------------------------------------------------- yardımcılar
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        private static Image Img(string name, Transform parent, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color; img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Panelin sol üst köşesine göre konum (y aşağı doğru), piksel tam sayı.</summary>
        private static void TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y)); rt.sizeDelta = new Vector2(w, h);
        }

        private Text Txt(string name, Transform parent, string text, int size, Color color, TextAnchor align)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.text = text; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
