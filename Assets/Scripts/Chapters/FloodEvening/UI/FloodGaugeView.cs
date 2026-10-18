using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Sağ kenardaki sel göstergesi (120 sn sel sayacı): el çizimi piksel "taşkın ölçüm direği". Tepede perçinli tabela
    /// plakası (büyük "%NN" + küçük geçen süre), altında 0 / 0,5 / 1,0 / 1,5 m çizgili ölçüm latası (%75–90 sarı, %90–100
    /// kırmızı bölge, ikaz çizgili tepe) ve beton ayak. Su, latanın önünde dipten yükselen yarı saydam piksel sütun olarak
    /// her kare küçük bir dokuya çizilir (açık yüzey çizgisi, hafif dalga, lataya çarpan köpük). Plaka rengi: normal amber,
    /// %75+ turuncu, %90+ turuncu/kırmızı nabız, %100 / kritik kırmızı nabız + sıçrayan damlalar, güvende yeşil. Aşama
    /// değişince plakanın solunda kısa bir kurdele ("Su yükseliyor") birkaç saniye görünür.
    /// Sprite sayfası: Resources/UI/FloodGauge/UI_FE_FloodGauge.png (tools/sel_gauge/gauge.js üretir; sabitler aynı).
    /// Tuval FePixelCanvasScaler ile 640x360 tabanında tam sayı ölçekle büyür (1080p 3x, 768p 2x). Seviyeyi
    /// FloodEveningHud.SetLevel → SetLevel verir.
    /// </summary>
    public class FloodGaugeView : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 61;
        [Tooltip("Göstergenin hedef seviyeye yetişme hızı (birim/sn) — ani kademe olmasın")]
        [SerializeField] private float levelSpeed = 0.12f;
        [Tooltip("Aşama değişince kurdelenin ekranda kaldığı süre (sn)")]
        [SerializeField] private float ribbonSeconds = 3.5f;

        private const string SheetPath = "UI/FloodGauge/UI_FE_FloodGauge";
        // sayfa düzeni (gauge.js ile aynı; y aşağı)
        private const int W = 44, H = 136, PW = 44, PH = 30, RW = 20, RH = 14;
        private const int Zero = 125, Span = 78;           // 0 m çizgisi, 1,5 m'ye kadar piksel
        private const int WaterX0 = 8, WaterX1 = 35, WaterBottom = 129, StaffX0 = 11, StaffX1 = 32;

        private enum Plate { Amber, Orange, Red, RedBright, Green }

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 WaterEdge = new Color32(20, 50, 94, 245);
        private static readonly Color32 WaterDeep = new Color32(27, 76, 143, 235);
        private static readonly Color32 WaterMid = new Color32(46, 120, 199, 215);
        private static readonly Color32 WaterHigh = new Color32(76, 156, 226, 220);
        private static readonly Color32 Crest = new Color32(143, 208, 245, 245);
        private static readonly Color32 Surface = new Color32(214, 242, 255, 255);
        private static readonly Color32 Foam = new Color32(255, 255, 255, 255);
        private static readonly Color32 Bubble = new Color32(190, 232, 252, 220);
        private static readonly Color32 InkC = new Color32(20, 20, 31, 255);

        private struct Bub { public float x, y, v; }
        private readonly List<Bub> bubbles = new List<Bub>();
        private readonly List<Vector4> drops = new List<Vector4>(); // x, y (aşağı), vx, vy
        private readonly List<Sprite> madeSprites = new List<Sprite>();
        private readonly List<PixelDigits> digits = new List<PixelDigits>();
        private Sprite[] plates;

        // built çalışma anı durumu: oyun sırasında betik yeniden derlenirse sıfırlanır, arayüz baştan kurulur (EnsureReady)
        [System.NonSerialized] private bool built;
        private bool visible, safe, critical;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform root, ribbonRect;
        private Image plateImage, ribbonImage;
        private CanvasGroup ribbonGroup;
        private RawImage waterImage;
        private Texture2D tex;
        private Color32[] px;
        private Text ribbonText;
        private PixelDigits timeText, percentText;
        private float shown, target, elapsed, total = 120f, pulse, bubbleAcc, ribbonLeft, ribbonAge;
        private string stageOverride, lastStage;
        // her kare yeni metin üretmemek için: yüzde / saniye değişince bir kez (çöp yok)
        private int lastPct = -1, lastSec = -1;
        private string pctStr = "%0", clockStr = "00:00";
        private Plate lastPlate = (Plate)(-1);

        private void Awake() => EnsureReady();

        /// <summary>Arayüz parçalarından biri yoksa (ilk kurulum ya da oyun içi derleme sonrası) eskisini atıp yeniden kurar.</summary>
        private void EnsureReady()
        {
            if (built && canvas != null && group != null && plateImage != null && waterImage != null && tex != null && px != null
                && plates != null && percentText != null && timeText != null && ribbonImage != null && ribbonText != null && ribbonGroup != null) return;
            if (canvas != null) Destroy(canvas.gameObject);
            else { var old = transform.Find("FE_FloodGaugeCanvas"); if (old != null) Destroy(old.gameObject); }
            built = false;
            digits.Clear(); madeSprites.Clear(); bubbles.Clear(); drops.Clear();
            lastPct = lastSec = -1; lastStage = null; lastPlate = (Plate)(-1);
            Build();
        }

        private void OnDestroy()
        {
            // çalışma anında üretilen doku/sprite'lar sahneyle silinmez: Tekrar Dene'de birikmesin (sayfa dokusu asset'tir, silinmez)
            if (tex != null) Destroy(tex);
            foreach (var s in madeSprites) if (s != null) Destroy(s);
            madeSprites.Clear();
            foreach (var d in digits) d.Release();
        }

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = AfetUyanisi.UI.GameFonts.Objective;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FePixel.CreateCanvas(transform, "FE_FloodGaugeCanvas", sortingOrder, out group);
            group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;

            root = FeUi.Rect("SelDurumu", canvas.transform);
            FeUi.Place(root, 1f, 0.5f, new Vector2(-8f, 6f), new Vector2(W, H));

            var sheet = Resources.Load<Texture2D>(SheetPath);
            if (sheet == null) Debug.LogWarning("[FloodGaugeView] Sprite sayfası yok: Resources/" + SheetPath);

            var post = FeUi.Img("Direk", root, Color.white);
            post.sprite = Cut(sheet, 0, 0, W, H, Vector4.zero);
            FeUi.Stretch(post.rectTransform);
            post.enabled = post.sprite != null;

            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FloodGaugeWater" };
            px = new Color32[W * H];
            waterImage = FeUi.Rect("Su", root).gameObject.AddComponent<RawImage>();
            waterImage.texture = tex; waterImage.raycastTarget = false;
            FeUi.Stretch(waterImage.rectTransform);

            plates = new Sprite[5];
            for (int i = 0; i < 5; i++) plates[i] = Cut(sheet, W + i * PW, 0, PW, PH, Vector4.zero);
            plateImage = FeUi.Img("Plaka", root, Color.white);
            plateImage.sprite = plates[0];
            plateImage.enabled = plates[0] != null;
            FeUi.TL(plateImage.rectTransform, 0f, 0f, PW, PH);

            percentText = new PixelDigits("Yuzde", plateImage.transform, 2); digits.Add(percentText);
            timeText = new PixelDigits("Sure", plateImage.transform, 1); digits.Add(timeText);

            // aşama kurdelesi: plakanın solunda, sağ ucu direğe değer
            ribbonImage = FeUi.Img("Kurdele", root, Color.white);
            ribbonImage.sprite = Cut(sheet, W + 5 * PW, 0, RW, RH, new Vector4(7, 4, 4, 4));
            if (ribbonImage.sprite != null) { ribbonImage.type = Image.Type.Sliced; ribbonImage.pixelsPerUnitMultiplier = 1f; }
            else ribbonImage.enabled = false;
            ribbonImage.transform.SetSiblingIndex(plateImage.transform.GetSiblingIndex()); // plakanın arkasından çıkar
            ribbonRect = ribbonImage.rectTransform;
            ribbonRect.anchorMin = ribbonRect.anchorMax = new Vector2(0f, 1f);
            ribbonRect.pivot = new Vector2(1f, 1f);
            ribbonGroup = ribbonImage.gameObject.AddComponent<CanvasGroup>();
            ribbonGroup.alpha = 0f;
            ribbonText = FePixel.Txt("Yazi", ribbonRect, font, 8, FePixel.Cream, TextAnchor.MiddleCenter, FePixel.Ink);
            ribbonText.horizontalOverflow = HorizontalWrapMode.Overflow;
            ribbonText.rectTransform.offsetMin = new Vector2(6f, 1f);
            ribbonText.rectTransform.offsetMax = new Vector2(-3f, -1f);
            SetTexts();
            Redraw();
        }

        private Sprite Cut(Texture2D sheet, int x, int yDown, int w, int h, Vector4 border)
        {
            if (sheet == null) return null;
            var s = Sprite.Create(sheet, new Rect(x, sheet.height - yDown - h, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = "FloodGauge_" + x;
            madeSprites.Add(s);
            return s;
        }

        public void SetVisible(bool value) { EnsureReady(); visible = value; }
        public void SetLevel(float level01) => target = Mathf.Clamp01(level01);
        public void SetLevelNow(float level01) { EnsureReady(); target = shown = Mathf.Clamp01(level01); Redraw(); }
        public void SetTime(float elapsedSeconds, float totalSeconds) { elapsed = Mathf.Max(0f, elapsedSeconds); total = Mathf.Max(1f, totalSeconds); }
        public void SetSafe(bool value) { EnsureReady(); safe = value; }
        public void SetCritical(bool value) { EnsureReady(); critical = value; }
        /// <summary>Aşama kurdelesinin yerine sabit metin (null = ilerlemeye göre otomatik; boş olmayan metin sürekli görünür).</summary>
        public void SetStageText(string text) => stageOverride = text;

        private bool Full => critical || shown >= 0.999f;

        private void Update()
        {
            EnsureReady();
            if (!built) return;
            float dt = Time.unscaledDeltaTime;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, dt * 3f);
            if (group.alpha <= 0.001f && !visible) return;
            shown = Mathf.MoveTowards(shown, target, levelSpeed * dt * (target - shown > 0.15f ? 4f : 1f));
            pulse += dt;

            // Plaka: %75 turuncu, %90 nabız, %100 / kritik kırmızı nabız, güvende yeşil
            Plate plate;
            if (safe) plate = Plate.Green;
            else if (Full) plate = Mathf.Sin(pulse * 8f) > 0f ? Plate.RedBright : Plate.Red;
            else if (shown >= 0.9f) plate = Mathf.Sin(pulse * 6f) > 0f ? Plate.Red : Plate.Orange;
            else if (shown >= 0.75f) plate = Plate.Orange;
            else plate = Plate.Amber;
            if (plate != lastPlate && plates[(int)plate] != null) { lastPlate = plate; plateImage.sprite = plates[(int)plate]; }

            SetTexts();
            UpdateRibbon(dt);
            Simulate(dt);
            Redraw();
        }

        private void SetTexts()
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(shown * 100f), 0, 100);
            int sec = Mathf.Clamp(Mathf.FloorToInt(elapsed), 0, 5999);
            if (pct != lastPct) { lastPct = pct; pctStr = "%" + pct; }
            if (sec != lastSec) { lastSec = sec; clockStr = (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00"); }
            var ink = (Color)InkC;
            percentText.Set(pctStr, ink);
            percentText.PlaceCentered(PW * 0.5f, 4f);
            timeText.Set(clockStr, ink);
            timeText.PlaceCentered(PW * 0.5f, 19f);
        }

        private void UpdateRibbon(float dt)
        {
            string stage = !string.IsNullOrEmpty(stageOverride) ? stageOverride : safe ? "Güvendesin" : StageLine(shown);
            bool sticky = !string.IsNullOrEmpty(stageOverride) || safe || Full;
            if (stage != lastStage)
            {
                lastStage = stage;
                ribbonText.SetPixelText(stage);
                ribbonText.color = safe ? FePixel.GreenLight : Full ? FeUi.Hex(0xFF8A70) : shown >= 0.75f ? FePixel.Amber : FePixel.Cream;
                float w = Mathf.Ceil(ribbonText.preferredWidth) + 11f;
                ribbonRect.sizeDelta = new Vector2(Mathf.Max(RW, w), RH);
                ribbonLeft = ribbonSeconds; ribbonAge = 0f;
            }
            ribbonLeft -= dt; ribbonAge += dt;
            bool on = sticky || ribbonLeft > 0f;
            ribbonGroup.alpha = Mathf.MoveTowards(ribbonGroup.alpha, on ? 1f : 0f, dt * 5f);
            // sağdan (direğin arkasından) 8 px kayarak çıkar
            float k = FePixel.OutBack(ribbonAge / 0.35f);
            ribbonRect.anchoredPosition = new Vector2(Mathf.Round(1f + (1f - k) * 8f), -8f);
        }

        private static string StageLine(float p)
        {
            if (p >= 0.999f) return "Kritik seviye!";
            if (p >= 0.75f) return "Yollar kapanıyor";
            if (p >= 0.5f) return "Su hızla yükseliyor";
            if (p >= 0.25f) return "Yağmur artıyor";
            if (p >= 0.1f) return "Su birikiyor";
            return "Yağmur başladı";
        }

        // ---------------------------------------------------------------- su çizimi (y aşağı; dokuya ters yazılır)
        private float SurfaceY => Zero - shown * Span;

        private void Simulate(float dt)
        {
            float sy = SurfaceY;
            bool wet = shown > 0.004f;
            float rate = wet && WaterBottom - sy > 6f ? 0.6f + shown * 3f : 0f;
            bubbleAcc += rate * dt;
            while (bubbleAcc >= 1f)
            {
                bubbleAcc -= 1f;
                float bx = Random.value < 0.5f ? Random.Range(WaterX0 + 2f, StaffX0 - 1f) : Random.Range(StaffX1 + 2f, WaterX1 - 1f);
                bubbles.Add(new Bub { x = bx, y = WaterBottom - 1f, v = Random.Range(6f, 11f) });
            }
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                b.y -= b.v * dt;
                if (b.y <= sy + 2f) bubbles.RemoveAt(i); else bubbles[i] = b;
            }
            // Kritik: lataya çarpıp sıçrayan damlalar
            if (Full && !safe && Random.value < dt * 12f)
            {
                bool left = Random.value < 0.5f;
                drops.Add(new Vector4(left ? StaffX0 - 1f : StaffX1 + 1f, sy - 1f, (left ? -1f : 1f) * Random.Range(4f, 12f), -Random.Range(14f, 24f)));
            }
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.x += d.z * dt; d.y += d.w * dt; d.w += 60f * dt;
                if (d.y > sy + 1f || d.x < 0f || d.x >= W) drops.RemoveAt(i); else drops[i] = d;
            }
        }

        private void Redraw()
        {
            if (tex == null || px == null) return;
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            if (shown > 0.004f)
            {
                float t = Time.unscaledTime;
                float sy = SurfaceY;
                float amp = safe ? 0.3f : Mathf.Lerp(0.6f, 1.2f, shown);
                for (int x = WaterX0; x <= WaterX1; x++)
                {
                    float w = Mathf.Sin(t * 2.6f + x * 0.7f) * amp + Mathf.Sin(t * 1.4f - x * 0.35f) * amp * 0.4f;
                    int top = Mathf.Clamp(Mathf.RoundToInt(sy + w), 1, WaterBottom);
                    // yanlar: dalgalı kenar (sabit desen + yavaş kayma), dikdörtgen gibi durmasın
                    for (int y = top; y <= WaterBottom; y++)
                    {
                        int jl = WaterX0 + (((y + Mathf.FloorToInt(t * 3f)) / 3) % 3 == 0 ? 1 : 0);
                        int jr = WaterX1 - (((y + 5 + Mathf.FloorToInt(t * 2.5f)) / 4) % 3 == 0 ? 1 : 0);
                        if (x < jl || x > jr) continue;
                        int d = y - top;
                        Color32 c;
                        if (d == 0) c = Surface;
                        else if (d == 1) c = Crest;
                        else if (x == jl || x == jr || y == WaterBottom) c = WaterEdge;
                        else if (d <= 4) c = WaterHigh;
                        else c = (WaterBottom - y) < (WaterBottom - sy) * 0.4f ? WaterDeep : WaterMid;
                        // derin / orta geçişi: dama
                        if (d > 4 && x != jl && x != jr && Mathf.Abs((WaterBottom - y) - (WaterBottom - sy) * 0.4f) < 1.5f && ((x + y) & 1) == 0) c = WaterDeep;
                        Put(x, y, c);
                    }
                    if (((x + Mathf.FloorToInt(t * 5f)) % 9) == 0) Put(x, top, Foam);
                }
                // lataya çarpan köpük: yüzeyin bir üstünde, lata kenarlarında
                int fy = Mathf.RoundToInt(sy) - 1;
                if (Mathf.Sin(t * 5f) > -0.3f) { Put(StaffX0 - 1, fy, Foam); Put(StaffX1 + 1, fy, Foam); }
                if (Mathf.Sin(t * 5f + 1.7f) > 0.2f) { Put(StaffX0 - 2, fy, Crest); Put(StaffX1 + 2, fy, Crest); }
                foreach (var b in bubbles) Put(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y), Bubble);
                foreach (var d in drops) Put(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.y), Surface);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        private void Put(int x, int yDown, Color32 c)
        {
            if (x < 0 || yDown < 0 || x >= W || yDown >= H) return;
            px[(H - 1 - yDown) * W + x] = c;
        }

        /// <summary>
        /// Plakadaki rakamlar için piksel yazı (3x5 glif, ölçek 1 ya da 2; 1 px kazıma gölgesi): yüzde ve süre. Font küçük
        /// boyda "5"i "S" gibi çizdiği için rakamlar dokuya elle çizilir. Ölçek 2'de "%" kaba büyütülmez, 7x10 elle
        /// çizilmiş ayrı glif kullanılır (iki halka + çapraz çizgi; eski 3x5 glif büyüyünce "Z" / "X" gibi okunuyordu).
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
                [':'] = new[] { "0", "1", "0", "1", "0" },
                ['%'] = new[] { "10001", "00010", "00100", "01000", "10001" },
            };
            /// <summary>Ölçek 2 için elle çizilmiş yüzde (7x10, gerçek piksel).</summary>
            private static readonly string[] PercentBig =
            {
                ".11...1", "1..1..1", "1..1.1.", ".11..1.", "....1..", "...1...", "..1.11.", ".1.1..1", ".1.1..1", "1...11.",
            };
            private static readonly Color32 Engrave = new Color32(255, 255, 255, 70); // plakaya kazınmış gibi: sağ-alt açık
            private readonly RawImage image;
            private readonly int scale;
            private Texture2D tex;
            private Color32[] buf;
            private string current;
            private Color currentColor;

            public PixelDigits(string name, Transform parent, int scale)
            {
                this.scale = Mathf.Clamp(scale, 1, 2);
                image = FeUi.Rect(name, parent).gameObject.AddComponent<RawImage>();
                image.raycastTarget = false;
            }

            private int Advance(char ch)
            {
                if (scale == 2 && ch == '%') return PercentBig[0].Length + scale;
                return ((Glyphs.TryGetValue(ch, out var g) ? g[0].Length : 2) + 1) * scale;
            }

            public void Set(string text, Color color)
            {
                if (text == current && color == currentColor) return;
                current = text; currentColor = color;
                int w = 1 - scale;
                foreach (char ch in text) w += Advance(ch);
                w = Mathf.Max(1, w);
                int h = 5 * scale + 1;
                if (tex == null || tex.width != w || tex.height != h) { if (tex != null) Object.Destroy(tex); tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FloodGaugeDigits" }; }
                if (buf == null || buf.Length != w * h) buf = new Color32[w * h];
                else System.Array.Clear(buf, 0, buf.Length);
                Color32 fg = color;
                int x = 0;
                foreach (char ch in text)
                {
                    if (scale == 2 && ch == '%')
                    {
                        for (int r = 0; r < PercentBig.Length; r++)
                            for (int c = 0; c < PercentBig[r].Length; c++)
                                if (PercentBig[r][c] == '1') Dot(w, h, x + c, r, fg);
                    }
                    else if (Glyphs.TryGetValue(ch, out var g))
                    {
                        for (int r = 0; r < 5; r++)
                            for (int c = 0; c < g[r].Length; c++)
                                if (g[r][c] == '1')
                                    for (int a = 0; a < scale; a++)
                                        for (int b = 0; b < scale; b++) Dot(w, h, x + c * scale + a, r * scale + b, fg);
                    }
                    x += Advance(ch);
                }
                // kazıma gölgesi: dolu pikselin sağ-altı boşsa
                for (int yy = 1; yy < h; yy++)
                    for (int xx = 0; xx < w - 1; xx++)
                    {
                        int i = yy * w + xx, s = (yy - 1) * w + xx + 1;
                        if (buf[i].a == 255 && buf[s].a == 0) buf[s] = Engrave;
                    }
                tex.SetPixels32(buf); tex.Apply(false);
                image.texture = tex;
                image.rectTransform.sizeDelta = new Vector2(w, h);
            }

            /// <summary>Sol üst orijinli (y aşağı) piksel; doku y yukarı.</summary>
            private void Dot(int w, int h, int x, int yDown, Color32 c)
            {
                if (x < 0 || x >= w || yDown < 0 || yDown >= h) return;
                buf[(h - 1 - yDown) * w + x] = c;
            }

            public void Release() { if (tex != null) Object.Destroy(tex); tex = null; }

            /// <summary>Yatay ortalı (y aşağı doğru, ebeveynin sol üstüne göre).</summary>
            public void PlaceCentered(float cx, float y)
            {
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(Mathf.Round(cx - rt.sizeDelta.x * 0.5f), -Mathf.Round(y));
            }
        }
    }
}
