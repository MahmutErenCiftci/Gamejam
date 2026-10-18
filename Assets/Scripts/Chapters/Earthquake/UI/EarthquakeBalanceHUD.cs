using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// TUTUN adımındaki denge göstergesi (640x360 referans, kamera sarsıntısından bağımsız). Girdi ya da oyun durumu
    /// yönetmez; ProtectionView her karede <see cref="Draw"/> ile modeli verir. Üç hâl modelden okunur:
    ///  • Talimat (model başlamadı): "İşareti YEŞİLDE tut: A sola, D sağa" + alt şeritte "SPACE'e bas ve basılı tut".
    ///  • Canlı: [A] ◄ kırmızı | amber | YEŞİL | amber | kırmızı ► [D] çubuğu ve işaret; işaret yeşilden çıkınca
    ///    basılması gereken tuş parlar ve durum satırı "A ile ortaya dön" / kırmızıda "Dengeni koru!" der; kalan süre
    ///    sayaç + azalan çubukla görünür; alt şerit SPACE basılı mı (tutunuyor mu) gösterir.
    ///  • Sonuç (model bitti): "Sarsıntı bitti — iyi tutundun!" ya da "Biraz zorlandın…".
    /// Üst panel en fazla ~86, alt şerit ~32 referans pikseli kaplar; ortadaki Cenk ve masa açık kalır.
    /// </summary>
    public class EarthquakeBalanceHUD : MonoBehaviour
    {
        private const float PanelW = 360f, PanelH = 84f, PanelTop = 6f;
        private const float BarX = 48f, BarY = 30f, BarW = 264f, BarH = 16f;
        private const float PillW = 300f, PillH = 22f, PillBottom = 8f;

        // Görev paleti (Navy #263559/#1C2742, Ink #14141F, Cream #F4EBD8, Gold #FFD27E, Amber #F2A33A, Green #18A957, Red #D8433B)
        private static readonly Color Ink = EqUi.Hex(0x14141F);
        private static readonly Color Navy = EqUi.Hex(0x263559);
        private static readonly Color NavyDark = EqUi.Hex(0x1C2742);
        private static readonly Color Cream = EqUi.Hex(0xF4EBD8);
        private static readonly Color Gold = EqUi.Hex(0xFFD27E);
        private static readonly Color Amber = EqUi.Hex(0xF2A33A);
        private static readonly Color Green = EqUi.Hex(0x18A957);
        private static readonly Color Red = EqUi.Hex(0xD8433B);
        private static readonly Color GreenText = EqUi.Hex(0x5FD98E);
        private static readonly Color RedText = EqUi.Hex(0xFF7A6E);
        private static readonly Color Muted = EqUi.Hex(0x5A6080);

        private EarthquakeUISkin skin;
        private Font font;
        private Canvas canvas;
        private CanvasGroup group;
        private EqBalanceTexts texts;
        private EarthquakeBalanceSettings settings;

        private RectTransform panel, bar, marker, timeFill, pill;
        private RectTransform amberZone, greenZone, leftKey, rightKey;
        private Text title, subtitle, timeText, status, pillText;
        private Image leftGlow, rightGlow, pillImage;
        private Image[] leftArrow, rightArrow;
        private float shownSince;
        private int lastStatus = -99, lastPill = -99, lastSeconds = -1, lastStage = -1;

        /// <summary>QA/test: durum satırındaki metin.</summary>
        public string StatusText => status != null ? status.text : "";
        /// <summary>QA/test: alt şerit (SPACE) metni.</summary>
        public string HoldText => pillText != null ? pillText.text : "";
        public bool IsShown => canvas != null && canvas.gameObject.activeSelf;

        public void Begin(EarthquakeBalanceSettings configuration, EqBalanceTexts content) => Begin(configuration, content, null);

        public void Begin(EarthquakeBalanceSettings configuration, EqBalanceTexts content, EarthquakeUISkin uiSkin)
        {
            settings = configuration ?? new EarthquakeBalanceSettings();
            texts = content ?? new EqBalanceTexts();
            if (uiSkin != null) skin = uiSkin;
            if (canvas == null) Build();
            canvas.gameObject.SetActive(true);
            group.alpha = 1f;
            shownSince = Time.unscaledTime;
            lastStatus = lastPill = -99;
            lastSeconds = lastStage = -1;
            LayoutZones();
            title.text = texts.title;
            subtitle.text = texts.subtitle;
        }

        private void Build()
        {
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_BalanceCanvas", 91, out group);
            group.blocksRaycasts = false;
            group.interactable = false;

            // --- Üst panel ---
            var pImg = EqUi.Img("BalancePanel", canvas.transform, EqUi.S(skin, s => s.ProtectPanel), Ink, skin);
            panel = pImg.rectTransform;
            EqUi.Place(panel, 0.5f, 1f, new Vector2(0f, -PanelTop), new Vector2(PanelW, PanelH));
            EqUi.Fallback(pImg, Ink, Navy, skin, 1f, 4f, Red);

            title = EqUi.Txt("Title", panel, font, 12, Gold, TextAnchor.MiddleLeft, Ink);
            EqUi.TL(title.rectTransform, 14f, 6f, 70f, 16f);
            subtitle = EqUi.Txt("Subtitle", panel, font, 9, Cream, TextAnchor.MiddleLeft, Ink);
            EqUi.TL(subtitle.rectTransform, 74f, 7f, 214f, 14f);
            EqUi.BestFit(subtitle, 8);
            timeText = EqUi.Txt("TimeLeft", panel, font, 11, Cream, TextAnchor.MiddleRight, Ink);
            EqUi.TL(timeText.rectTransform, 290f, 6f, 56f, 16f);

            // Tuş rozetleri + oklar (A sola, D sağa)
            leftGlow = Glow("A_Glow", 14f);
            leftKey = EqUi.KeyCap(panel, "A", font, skin, 10);
            EqUi.TL(leftKey, 18f, BarY - 1f, 20f, 18f);
            leftArrow = Arrow("A_Arrow", 9f, BarY + 8f, -1);
            rightGlow = Glow("D_Glow", PanelW - 14f - 28f);
            rightKey = EqUi.KeyCap(panel, "D", font, skin, 10);
            EqUi.TL(rightKey, PanelW - 18f - 20f, BarY - 1f, 20f, 18f);
            rightArrow = Arrow("D_Arrow", PanelW - 9f, BarY + 8f, 1);

            // Denge çubuğu: kırmızı (tehlike) | amber | yeşil (güvenli) | amber | kırmızı
            var back = EqUi.Img("BalanceBar", panel, null, Ink, skin);
            bar = back.rectTransform;
            EqUi.TL(bar, BarX, BarY, BarW, BarH);
            var inner = EqUi.Stretch(EqUi.Rect("Zones", bar), 1f);
            var red = EqUi.Img("Danger", inner, null, Red, skin);
            EqUi.Stretch(red.rectTransform);
            amberZone = EqUi.Img("Careful", inner, null, Amber, skin).rectTransform;
            greenZone = EqUi.Img("Safe", inner, null, Green, skin).rectTransform;
            DangerMark(inner, 0f);
            DangerMark(inner, 1f);
            var center = EqUi.Img("Center", inner, null, EqUi.A(Cream, 0.55f), skin);
            EqUi.Place(center.rectTransform, 0.5f, 0.5f, Vector2.zero, new Vector2(1f, BarH - 2f));

            // İşaret (Cenk'in dengesi): mürekkep çerçeveli krem çizgi + üstte aşağı bakan üçgen
            marker = EqUi.Rect("Indicator", bar);
            marker.anchorMin = marker.anchorMax = new Vector2(0f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(6f, BarH + 4f);
            var mOutline = EqUi.Img("Outline", marker, null, Ink, skin);
            EqUi.Stretch(mOutline.rectTransform);
            var mFill = EqUi.Img("Fill", marker, null, Cream, skin);
            EqUi.Stretch(mFill.rectTransform, 1f);
            for (int i = 0; i < 4; i++)
            {
                float w = 9f - i * 2f;
                var row = EqUi.Img("Tip" + i, marker, null, i == 0 ? Ink : Cream, skin);
                EqUi.Place(row.rectTransform, 0.5f, 1f, new Vector2(0f, 4f - i), new Vector2(w, 1f));
            }

            // Durum satırı
            status = EqUi.Txt("Status", panel, font, 10, Gold, TextAnchor.MiddleCenter, Ink);
            EqUi.TL(status.rectTransform, 12f, 51f, PanelW - 24f, 15f);
            EqUi.BestFit(status, 8);

            // Kalan süre çubuğu (azalır)
            var tBack = EqUi.Img("TimeBar", panel, null, Ink, skin);
            EqUi.TL(tBack.rectTransform, 14f, 71f, PanelW - 28f, 6f);
            var tInner = EqUi.Stretch(EqUi.Rect("Inner", tBack.rectTransform), 1f);
            var tBg = EqUi.Img("Back", tInner, null, NavyDark, skin);
            EqUi.Stretch(tBg.rectTransform);
            var tFill = EqUi.Img("Fill", tInner, null, Gold, skin);
            timeFill = tFill.rectTransform;
            timeFill.anchorMin = Vector2.zero;
            timeFill.anchorMax = Vector2.one;
            timeFill.pivot = new Vector2(0f, 0.5f);
            timeFill.offsetMin = timeFill.offsetMax = Vector2.zero;

            // --- Alt şerit: SPACE (tutun) ---
            pillImage = EqUi.Img("HoldPill", canvas.transform, null, Ink, skin);
            pill = pillImage.rectTransform;
            EqUi.Place(pill, 0.5f, 0f, new Vector2(0f, PillBottom), new Vector2(PillW, PillH));
            EqUi.Fallback(pillImage, Ink, NavyDark, skin);
            var space = EqUi.KeyCap(pill, "SPACE", font, skin, 9);
            EqUi.TL(space, 4f, 3f, 48f, 16f);
            pillText = EqUi.Txt("Text", pill, font, 9, Cream, TextAnchor.MiddleLeft, Ink);
            EqUi.TopBand(pillText.rectTransform, 58f, 6f, 3f, 16f);
            EqUi.BestFit(pillText, 8);

            canvas.gameObject.SetActive(false);
        }

        private Image Glow(string name, float x)
        {
            var g = EqUi.Img(name, panel, null, EqUi.A(Gold, 0f), skin);
            EqUi.TL(g.rectTransform, x, BarY - 4f, 28f, 24f);
            return g;
        }

        /// <summary>Piksel ok: 4 sütun (1,3,5,7 px), uç tipX'te. dir -1 = sola, +1 = sağa bakar.</summary>
        private Image[] Arrow(string name, float tipX, float midY, int dir)
        {
            var cols = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var c = EqUi.Img(name + i, panel, null, Muted, skin);
                float h = 1f + i * 2f;
                float x = dir < 0 ? tipX + i : tipX - i - 1f;
                EqUi.TL(c.rectTransform, x, midY - h / 2f, 1f, h);
                cols[i] = c;
            }
            return cols;
        }

        /// <summary>Kırmızı bölgede "!" işareti (renk körlüğüne karşı renkten bağımsız ipucu).</summary>
        private void DangerMark(RectTransform zones, float side)
        {
            var t = EqUi.Txt("Danger!" + side, zones, font, 10, Cream, TextAnchor.MiddleCenter, Ink);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(side, 0.5f);
            t.rectTransform.pivot = new Vector2(side, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(side < 0.5f ? 4f : -4f, 0f);
            t.rectTransform.sizeDelta = new Vector2(10f, BarH);
            t.text = "!";
        }

        private void LayoutZones()
        {
            float inner = BarW - 2f;
            amberZone.anchorMin = amberZone.anchorMax = amberZone.pivot = new Vector2(0.5f, 0.5f);
            amberZone.anchoredPosition = Vector2.zero;
            amberZone.sizeDelta = new Vector2(Mathf.Round(inner * settings.redThreshold), BarH - 2f);
            greenZone.anchorMin = greenZone.anchorMax = greenZone.pivot = new Vector2(0.5f, 0.5f);
            greenZone.anchoredPosition = Vector2.zero;
            greenZone.sizeDelta = new Vector2(Mathf.Round(inner * settings.greenZone), BarH - 2f);
        }

        public void Draw(EarthquakeBalanceModel state)
        {
            if (canvas == null || state == null) return;
            float now = Time.unscaledTime;
            float blink = 0.5f + 0.5f * Mathf.Sin((now - shownSince) * Mathf.PI * 2f * 1.6f);
            bool intro = !state.Started && !state.Finished;
            bool done = state.Finished;
            int zone = state.Zone;
            int need = zone == 0 ? 0 : (state.Position > 0f ? -1 : 1); // -1: A basılmalı, +1: D basılmalı
            bool released = !intro && !done && !state.Holding && state.ReleasedSeconds > settings.releaseGrace;

            // İşaret
            float half = BarW / 2f - 3f;
            marker.anchoredPosition = new Vector2(Mathf.Round(BarW / 2f + Mathf.Clamp(state.Position, -1f, 1f) * half), 0f);

            // Tuş vurgusu: talimatta ikisi de nabız atar, canlıda yalnızca gereken tuş.
            bool hiA = intro || (!done && need < 0);
            bool hiD = intro || (!done && need > 0);
            Key(leftKey, leftGlow, leftArrow, hiA, blink, zone == 2);
            Key(rightKey, rightGlow, rightArrow, hiD, blink, zone == 2);

            // Başlık satırı
            int stage = intro ? -1 : done ? 3 : state.Stage;
            if (stage != lastStage)
            {
                lastStage = stage;
                subtitle.text = stage == 0 ? texts.initial : stage == 1 ? texts.strong : stage == 2 ? texts.irregular : stage == 3 ? "" : texts.subtitle;
            }
            int seconds = done ? 0 : Mathf.CeilToInt(state.RemainingSeconds - 0.001f);
            if (seconds != lastSeconds)
            {
                lastSeconds = seconds;
                timeText.text = Format(texts.timeLeft, seconds);
            }
            float remaining = done ? 0f : 1f - state.Progress;
            timeFill.anchorMax = new Vector2(Mathf.Round(remaining * (PanelW - 30f)) / (PanelW - 30f), 1f);

            // Durum satırı
            int code = intro ? 10 : done ? (state.WentWell ? 20 : 21) : zone * 3 + (need < 0 ? 0 : need > 0 ? 1 : 2);
            if (code != lastStatus)
            {
                lastStatus = code;
                string side = need < 0 ? texts.driftLeft : texts.driftRight;
                status.text = intro ? texts.controls
                    : done ? (state.WentWell ? texts.resultGood : texts.resultHard)
                    : zone == 2 ? texts.unstable + "  " + side
                    : zone == 1 ? side
                    : texts.stable;
                status.color = intro ? Gold : done ? (state.WentWell ? GreenText : Amber) : zone == 2 ? RedText : zone == 1 ? Amber : GreenText;
                status.fontSize = status.resizeTextMaxSize = AfetUyanisi.UI.GameFonts.SizeFor(status.font, done || zone == 2 ? 11 : 10);
            }
            float statusPulse = !done && zone == 2 ? 1f + 0.06f * blink : 1f;
            status.rectTransform.localScale = new Vector3(statusPulse, statusPulse, 1f);

            // Alt şerit: SPACE
            int pillState = done ? 3 : intro ? 0 : released ? 2 : 1;
            pill.gameObject.SetActive(!done);
            if (pillState != lastPill)
            {
                lastPill = pillState;
                pillText.text = pillState == 0 ? texts.startHint : pillState == 2 ? texts.release : texts.note;
                pillText.color = pillState == 2 ? RedText : Cream;
            }
            if (!done)
            {
                Color border = pillState == 1 ? Green : pillState == 2 ? Color.Lerp(RedText, Red, blink) : Color.Lerp(Amber, Gold, blink);
                Color fill = pillState == 2 ? Color.Lerp(NavyDark, EqUi.Hex(0x4A1F28), blink) : NavyDark;
                EqUi.FallbackColors(pillImage, border, fill);
                float s = pillState == 1 ? 1f : 1f + 0.04f * blink;
                pill.localScale = new Vector3(s, s, 1f);
            }
        }

        private static void Key(RectTransform key, Image glow, Image[] arrow, bool highlight, float blink, bool urgent)
        {
            float k = highlight ? blink : 0f;
            glow.color = EqUi.A(urgent ? EqUi.Hex(0xFF7A6E) : Gold, highlight ? 0.25f + 0.45f * k : 0f);
            float s = highlight ? 1f + 0.12f * k : 1f;
            key.localScale = new Vector3(s, s, 1f);
            var c = highlight ? Color.Lerp(Amber, Gold, k) : Muted;
            foreach (var a in arrow) a.color = c;
        }

        private static string Format(string pattern, int value)
        {
            if (string.IsNullOrEmpty(pattern)) return value.ToString();
            return pattern.Replace("{0}", value.ToString());
        }

        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }
        private void OnDisable() => Hide();
    }
}
