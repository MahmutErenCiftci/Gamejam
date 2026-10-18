using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Devrilen ağacı yoldan açma mini oyununun ritim göstergesi (P-FLOOD 18 Eki; BranchObstacle Rhythm kipi kullanır).
    /// Berk'in FePixel arayüz diliyle (deprem görev paneli çerçevesi, tutma çubuğu sprite'ları, piksel [E] tuş kapağı, Pixel Operator):
    ///   üst satır  [E] + büyük komut "İT!" / "ÇEK!" (her iyi vuruşta sırayla değişir) + sağda kısa ipucu
    ///   orta satır çubuk: yeşil vuruş penceresi (her basıştan sonra rastgele yeni yere geçer, iyi vuruşlarla biraz daralır),
    ///              üstünde ileri-geri gezen işaretçi
    ///   alt satır  vuruş kutucukları (dolan yeşil; yarım ceza = amber) + "[Esc] Bırak"
    /// Geri bildirim: iyi vuruşta panel zıplar, pencere parlar, kıvılcım; ritim dışı basışta kısa sarsıntı ve amber "!" ("Ritmi bekle").
    /// Görünüm tuş OKUMAZ; durum BranchObstacle'dan gelir. Arayüz kodla kurulur (Canvas 66).
    /// </summary>
    public class FloodRhythmView : MonoBehaviour
    {
        [Header("Yazı")]
        [Tooltip("Assets/Fonts/PixelOperator/PixelOperatorTR-Bold.ttf (boşsa oyunun hikâye fontu)")]
        [SerializeField] private Font font;
        [SerializeField] private string pushLabel = "İT!";
        [SerializeField] private string pullLabel = "ÇEK!";
        [SerializeField] private string tipLabel = "Yeşilde bas";
        [SerializeField] private string missLabel = "Ritmi bekle";
        [SerializeField] private string cancelLabel = "[Esc] Bırak";

        [Header("Sprite'lar (Assets/Art/UI/Earthquake)")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite barBackSprite;

        [Header("Yerleşim (640x360 referans pikseli)")]
        [SerializeField] private int sortingOrder = 66;
        [SerializeField] private float bottomMargin = 34f;
        [SerializeField] private float width = 224f;

        private const float PanelH = 64f, Side = 10f, BarY = 26f, BarH = 12f;

        private bool built, visible;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panel, barRect, windowRect, markerRect, pipsRect;
        private Image keycap, windowImg, windowGlow, markerImg, missBadge;
        private Text cmdText, tipText, cancelText, missText;
        private Image[] pips = new Image[0];
        private FePixelBurst burst;
        private float fadeK, sinceGood = 99f, sinceMiss = 99f, doneT = -1f, marker, windowHalf = 0.3f, windowCenter, sinceMove = 99f;
        private static Sprite plateSprite;
        private bool push = true;

        public bool IsVisible => visible;

        private void Awake() => Build();

        private void OnDisable()
        {
            if (!built || burst == null) return;
            visible = false; fadeK = 0f; burst.Clear();
            canvas.gameObject.SetActive(false);
        }

        /// <summary>Paneli açar: required = gereken iyi vuruş, windowHalf = pencerenin yarı genişliği (çubuğun yarısına oranla).</summary>
        public void Show(int required, float windowHalf01)
        {
            Build();
            windowHalf = Mathf.Clamp(windowHalf01, 0.05f, 0.9f);
            BuildPips(Mathf.Max(1, required));
            visible = true;
            doneT = -1f;
            sinceGood = sinceMiss = 99f;
            canvas.gameObject.SetActive(true);
            Layout();
        }

        public void Hide() { Build(); visible = false; }

        /// <summary>İşaretçi konumu -1 (sol) .. 1 (sağ); 0 = pencerenin ortası.</summary>
        public void SetMarker(float m) => marker = Mathf.Clamp(m, -1f, 1f);

        /// <summary>Sıradaki komut: true = İT, false = ÇEK.</summary>
        public void SetPrompt(bool isPush)
        {
            push = isPush;
            if (cmdText != null) cmdText.SetPixelText(push ? pushLabel : pullLabel);
        }

        /// <summary>İlerleme (iyi vuruş sayısı; ceza yarım vuruş düşürebilir).</summary>
        public void SetBeats(float beats)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                float f = beats - i;
                FeUi.SetFrameColors(pips[i], FePixel.Ink, f >= 1f ? FePixel.GreenLight : f >= 0.5f ? FePixel.Amber : FeUi.Hex(0x1C2742));
            }
        }

        public void Good()
        {
            sinceGood = 0f;
            if (burst == null) return;
            var at = new Vector2(0f, PanelH * 0.5f - BarY - BarH * 0.5f); // pencerenin ortası (panel merkezine göre)
            burst.Emit(at, 6, FePixel.Spark, new[] { FePixel.GreenLight, FePixel.Gold, Color.white }, Vector2.up, 80f, 70f, 140f, 0.45f);
        }

        public void Miss() => sinceMiss = 0f;

        /// <summary>Yeşil pencerenin yeri (-1..1, çubuğun ortasına göre) ve yarı genişliği (aynı birim). Her basıştan sonra yer değiştirir.</summary>
        public void SetWindow(float center, float half)
        {
            Build();
            windowCenter = Mathf.Clamp(center, -1f, 1f);
            windowHalf = Mathf.Clamp(half, 0.05f, 0.9f);
            sinceMove = 0f;
            Layout();
        }

        public void Done()
        {
            doneT = 0f;
            if (burst != null) burst.Emit(Vector2.zero, 12, FePixel.Star, new[] { FePixel.Gold, FePixel.GreenLight, Color.white }, Vector2.up, 120f, 80f, 160f, 0.6f);
        }

        private void Update()
        {
            if (!built || burst == null) return;
            float dt = Time.unscaledDeltaTime;
            fadeK = Mathf.MoveTowards(fadeK, visible ? 1f : 0f, dt / (visible ? 0.2f : 0.15f));
            if (!visible && fadeK <= 0f) { burst.Clear(); canvas.gameObject.SetActive(false); return; }
            sinceGood += dt; sinceMiss += dt;
            if (doneT >= 0f) doneT += dt;
            burst.Tick(dt);

            group.alpha = Mathf.Clamp01(fadeK * 1.5f);
            float rise = (1f - FePixel.OutBack(fadeK)) * 12f;
            float hop = sinceGood < 0.16f ? Mathf.Sin(sinceGood / 0.16f * Mathf.PI) * 3f : 0f;
            float shake = FePixel.Shake(sinceMiss, 0.22f, 2f);
            panel.anchoredPosition = new Vector2(shake, Mathf.Round(bottomMargin - rise + hop));

            // işaretçi (tam piksel), pencere: içindeyken parlak yeşil, iyi vuruşta beyaz parlama
            float barW = barRect.rect.width - 2f;
            bool inside = Mathf.Abs(marker - windowCenter) <= windowHalf;
            sinceMove += dt;
            // yeni yerinde kısa "belirme": ilk 0,12 sn 2 px büyük
            float pop = sinceMove < 0.12f ? 2f : 0f;
            windowRect.anchoredPosition = new Vector2(Mathf.Round(windowCenter * (barW + 2f) * 0.5f), 0f);
            windowRect.sizeDelta = new Vector2(Mathf.Max(6f, Mathf.Round((barW + 2f) * windowHalf)) + pop * 2f, BarH - 2f + pop);
            markerRect.anchoredPosition = new Vector2(Mathf.Round(1f + (marker * 0.5f + 0.5f) * (barW - 3f)), 0f);
            markerImg.color = inside ? Color.white : FePixel.Gold;
            windowImg.color = inside ? FePixel.GreenLight : FePixel.Green;
            float glow = sinceGood < 0.2f ? 1f - sinceGood / 0.2f : 0f;
            if (doneT >= 0f) glow = Mathf.Repeat(doneT, 0.3f) < 0.15f ? 0.6f : 0.2f;
            windowGlow.color = FeUi.A(Color.white, 0.75f * glow);

            keycap.sprite = sinceGood < 0.1f || sinceMiss < 0.1f ? FePixel.KeyDown : FePixel.KeyUp;
            cmdText.color = doneT >= 0f ? Color.white : sinceGood < 0.15f ? Color.white : push ? FePixel.Gold : FePixel.Amber;
            bool missing = sinceMiss < 0.6f && doneT < 0f;
            if (missBadge.gameObject.activeSelf != missing) missBadge.gameObject.SetActive(missing);
            missText.enabled = missing;
            tipText.enabled = !missing;
        }

        private void Layout()
        {
            float w = width;
            var cr = (RectTransform)canvas.transform;
            if (cr.rect.width > 0f) w = Mathf.Round(Mathf.Min(width, cr.rect.width - 32f));
            panel.sizeDelta = new Vector2(w, PanelH);
            float inner = w - Side * 2f;
            float win = Mathf.Max(6f, Mathf.Round(inner * windowHalf));
            windowRect.sizeDelta = new Vector2(win, BarH - 2f);
            windowRect.anchoredPosition = new Vector2(Mathf.Round(windowCenter * inner * 0.5f), 0f);
            LayoutPips(w);
        }

        private void BuildPips(int n)
        {
            if (pips.Length == n) return;
            foreach (var p in pips) if (p != null) Destroy(p.gameObject);
            pips = new Image[n];
            for (int i = 0; i < n; i++) pips[i] = FeUi.Framed("Pip" + i, pipsRect, FePixel.Ink, FeUi.Hex(0x1C2742));
        }

        private void LayoutPips(float w)
        {
            const float size = 7f, gap = 3f;
            float total = pips.Length * size + (pips.Length - 1) * gap;
            for (int i = 0; i < pips.Length; i++)
                FeUi.TL(pips[i].rectTransform, Side + i * (size + gap), 0f, size, size);
            pipsRect.sizeDelta = new Vector2(total, size);
        }

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FePixel.CreateCanvas(transform, "FE_RhythmCanvas", sortingOrder, out group);
            group.interactable = false;
            group.blocksRaycasts = false;

            panel = FeUi.Rect("RhythmPanel", canvas.transform);
            FeUi.Place(panel, 0.5f, 0f, new Vector2(0f, bottomMargin), new Vector2(width, PanelH));
            // el yapımı piksel levha: mürekkep kontur, amber metal pervaz (eğimli: üst/sol açık, alt/sağ koyu), köşelerde perçin,
            // içi koyu ahşap tahtalar (yatay derz çizgileri + birkaç budak) — göstergenin turuncu levhasıyla aynı dil
            var frame = FePixel.Sliced("Frame", panel, PlateSprite());
            FeUi.Stretch(frame.rectTransform);
            for (int i = 0; i < 3; i++)
            {
                var seam = FeUi.Img("Seam" + i, panel, FeUi.Hex(0x1A120C));
                var srt = seam.rectTransform;
                srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
                float y = 6f + (i + 1) * (PanelH - 12f) / 4f;
                srt.offsetMin = new Vector2(6f, -y - 1f); srt.offsetMax = new Vector2(-6f, -y);
                var hi = FeUi.Img("SeamHi" + i, panel, FeUi.Hex(0x4A3424));
                var hrt = hi.rectTransform;
                hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
                hrt.offsetMin = new Vector2(6f, -y - 2f); hrt.offsetMax = new Vector2(-6f, -y - 1f);
            }

            keycap = FePixel.Icon("Keycap", panel, FePixel.KeyUp);
            FeUi.TL(keycap.rectTransform, Side, 6f, 15f, 16f);
            cmdText = FePixel.Txt("Command", panel, font, 16, FePixel.Gold, TextAnchor.MiddleLeft, FePixel.Ink);
            FeUi.TL(cmdText.rectTransform, Side + 20f, 4f, 90f, 20f);
            tipText = FePixel.Txt("Tip", panel, font, 8, FePixel.Cream, TextAnchor.MiddleRight, FePixel.Ink);
            RightRow(tipText.rectTransform, 8f, 12f, 110f);
            tipText.SetPixelText(tipLabel);
            missText = FePixel.Txt("Miss", panel, font, 8, FePixel.Amber, TextAnchor.MiddleRight, FePixel.Ink);
            RightRow(missText.rectTransform, 8f, 12f, 96f);
            missText.SetPixelText(missLabel);
            missBadge = FeUi.Badge("MissBadge", panel, font, FePixel.Amber);
            missBadge.rectTransform.anchorMin = missBadge.rectTransform.anchorMax = new Vector2(1f, 1f);
            missBadge.rectTransform.pivot = new Vector2(1f, 1f);
            missBadge.rectTransform.anchoredPosition = new Vector2(-Side - 98f, -7f);
            missBadge.rectTransform.sizeDelta = new Vector2(11f, 11f);

            // çubuk: arka, ortada yeşil pencere (+ parlama), orta çizgi, gezen işaretçi
            barRect = FeUi.Rect("Bar", panel);
            barRect.anchorMin = new Vector2(0f, 1f); barRect.anchorMax = new Vector2(1f, 1f); barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(Side, -(BarY + BarH)); barRect.offsetMax = new Vector2(-Side, -BarY);
            var back = barBackSprite != null ? FePixel.Sliced("Back", barRect, barBackSprite) : FeUi.Framed("Back", barRect, FePixel.Ink, FeUi.Hex(0x1C2742));
            FeUi.Stretch(back.rectTransform);
            windowImg = FeUi.Img("Window", barRect, FePixel.Green);
            windowRect = windowImg.rectTransform;
            windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowGlow = FeUi.Img("Glow", windowRect, FeUi.A(Color.white, 0f));
            FeUi.Stretch(windowGlow.rectTransform);
            var center = FeUi.Img("Center", barRect, FeUi.A(FePixel.Ink, 0.6f));
            var crt = center.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1f, BarH - 4f);
            var markerFrame = FeUi.Img("MarkerOutline", barRect, FePixel.Ink);
            markerRect = markerFrame.rectTransform;
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0f, 0.5f);
            markerRect.pivot = new Vector2(0f, 0.5f);
            markerRect.sizeDelta = new Vector2(5f, BarH + 6f);
            markerImg = FeUi.Img("Marker", markerRect, FePixel.Gold);
            FeUi.Stretch(markerImg.rectTransform, 1f);

            pipsRect = FeUi.Rect("Pips", panel);
            FeUi.TL(pipsRect, 0f, BarY + BarH + 8f, 100f, 7f);
            cancelText = FePixel.Txt("Cancel", panel, font, 8, FeUi.Muted, TextAnchor.MiddleRight, FePixel.Ink);
            RightRow(cancelText.rectTransform, BarY + BarH + 5f, 12f, 90f);
            cancelText.SetPixelText(cancelLabel);

            burst = new FePixelBurst(panel, "Sparks");
            SetPrompt(true);
            visible = false;
            fadeK = 0f;
            canvas.gameObject.SetActive(false);
        }

        // 24x24 levha (9 dilim, kenar 7 px): 1 px mürekkep, 2 px amber pervaz + 1 px koyu iç kenar, köşe perçinleri, ahşap iç
        private static Sprite PlateSprite()
        {
            if (plateSprite != null) return plateSprite;
            const int S = 24, B = 7;
            Color ink = FePixel.Ink, amber = FePixel.Amber, light = FePixel.Gold, dark = FeUi.Hex(0x9A5A1A), inner = FeUi.Hex(0x5A3414);
            Color wood = FeUi.Hex(0x2E2018), woodHi = FeUi.Hex(0x3A2A1E), rivet = FeUi.Hex(0x6B4420), rivetHi = FeUi.Hex(0xFFE8B0);
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FE_RhythmPlate" };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(S - 1 - x, S - 1 - y)); // kenara uzaklık
                    bool corner = (x == 0 || x == S - 1) && (y == 0 || y == S - 1);
                    Color c;
                    if (corner) c = Color.clear;
                    else if (d == 0) c = ink;
                    else if (d <= 2) c = (x <= 2 || y >= S - 3) && !(x >= S - 3 || y <= 2) ? light : (x >= S - 3 || y <= 2) && !(x <= 2 || y >= S - 3) ? dark : amber;
                    else if (d == 3) c = inner;
                    else c = ((x + y * 3) % 11 == 0) ? woodHi : wood;
                    tex.SetPixel(x, y, c);
                }
            // perçinler (köşelerde, pervazın üstünde): 2x2, sol üst parlak
            foreach (var p in new[] { new Vector2Int(1, 1), new Vector2Int(S - 3, 1), new Vector2Int(1, S - 3), new Vector2Int(S - 3, S - 3) })
            {
                tex.SetPixel(p.x, p.y, rivet); tex.SetPixel(p.x + 1, p.y, rivet); tex.SetPixel(p.x, p.y + 1, rivetHi); tex.SetPixel(p.x + 1, p.y + 1, rivet);
            }
            tex.Apply(false, true);
            return plateSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(B, B, B, B));
        }

        // sağ kenara yaslı satır (üstten y, yükseklik h, genişlik w)
        private static void RightRow(RectTransform rt, float y, float h, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-Side, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
