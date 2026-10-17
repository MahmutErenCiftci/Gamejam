using System.Collections;
using System.Collections.Generic;
using System.Text;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// "AFET FARKINDALIK RAPORU" (maket: S/eqD_mock_report.png): lacivert başlık bandı, rozet + etiket + puan,
    /// ✓/✗ ikonlu 8 istatistik satırı, "Öğrenilecekler" listesi (yanlışlar + ipuçları), [Tekrar Dene] / [Menüye Dön].
    /// Butonlar: [Devam: Sel Bölümü] (birincil; DEPREM → SEL, Sel sahnesi build listesindeyse) [Tekrar Dene] [Menüye Dön].
    /// Klavye: ←/→ (A/D) seçim, Enter/Space/E onay, 1/2/3 = soldan sağa buton; fare ile tıklanır.
    /// Seçim report.choice'a "Next", "Retry" ya da "Menu" olarak yazılır ("Next"te önce ekran kararır);
    /// sahne geçişini sistem yapar (EarthquakeResultManager.ApplyChoice; ekran açık kalır).
    /// Arayüzü kodla kurar (Canvas sıralaması 95).
    /// </summary>
    public class ResultReportView : ResultReportViewBase
    {
        [SerializeField] private EarthquakeUISkin skin;
        [SerializeField] private int sortingOrder = 95;
        [SerializeField] private string defaultTitle = "AFET FARKINDALIK RAPORU";
        [SerializeField] private string notesTitle = "Öğrenilecekler";
        [SerializeField] private string retryLabel = "Tekrar Dene";
        [SerializeField] private string menuLabel = "Menüye Dön";
        [Tooltip("Birincil buton: menüye dönmeden sıradaki bölüme (DEPREM → SEL) geçer. Sonraki bölüm sahnesi yoksa gizlenir.")]
        [SerializeField] private string continueLabel = "Devam: Sel Bölümü";
        [SerializeField] private bool showContinue = true;
        [Tooltip("Devam seçilince ekranın kararma süresi (sn); sonra yeni bölüm kendi giriş kartıyla açılır")]
        [SerializeField] private float continueFadeSeconds = 0.6f;
        [Tooltip("Rozet seçimi için puan eşikleri: USTA, HAZIRLIKLI, GELİŞİYOR, DAHA FAZLA ÖĞREN (veri dosyasındaki etiket eşikleriyle aynı tutun).")]
        [SerializeField] private int[] tierMinScores = { 85, 65, 40, 0 };
        [Tooltip("'Öğrenilecekler' listesinde en fazla kaç madde (tekrarlar ayıklanır)")]
        [SerializeField, Range(1, 8)] private int maxNotes = 5;
        [Tooltip("'Öğrenilecekler' yazı boyutu (640x360 referans pikseli)")]
        [SerializeField] private int notesFontSize = 9;

        public const string ChoiceRetry = "Retry";
        public const string ChoiceMenu = "Menu";
        /// <summary>"Devam: Sel Bölümü" (EarthquakeResultManager.ApplyChoice → GameSceneRouter.ContinueToNextChapter).</summary>
        public const string ChoiceNext = "Next";

        private const float PanelW = 440f, PanelH = 330f;
        private const int RowCount = 9;
        private const float RowTop = 90f, RowStep = 13f;

        private enum Mark { Ok, Bad, Time }

        private class Row
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image icon;
            public Text label;
            public Text value;
        }

        private bool built;
        private Font font;
        private Canvas canvas;
        private CanvasGroup group;
        private Image dim;
        private RectTransform panel;
        private Text titleText;
        private Image badge;
        private Text badgeInitial;
        private Text labelText, scoreText;
        private readonly List<Row> rows = new List<Row>();
        private Text notesText;
        // Buton sırası: 0 = Devam (yalnız sonraki bölüm varsa), 1 = Tekrar Dene, 2 = Menüye Dön.
        private const int BtnNext = 0, BtnRetry = 1, BtnMenu = 2;
        private readonly Image[] buttons = new Image[3];
        private readonly EqButton[] eqButtons = new EqButton[3];
        private readonly List<int> order = new List<int>(); // görünen butonlar, soldan sağa
        private Image blackout;
        private int focus;
        private int clicked = -1;

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_ReportCanvas", sortingOrder, out group);

            dim = EqUi.DimImage(canvas.transform, skin);

            var pImg = EqUi.Img("ReportPanel", canvas.transform, EqUi.S(skin, s => s.ReportPanel), EqUi.Ink, skin);
            panel = pImg.rectTransform;
            EqUi.Place(panel, 0.5f, 0.5f, new Vector2(0f, -1f), new Vector2(PanelW, PanelH));
            if (pImg.sprite == null)
            {
                EqUi.Fallback(pImg, EqUi.Ink, EqUi.Cream, skin, 4f, 24f, EqUi.Navy);
                var line = EqUi.Img("AmberLine", panel, null, EqUi.Amber, skin);
                EqUi.TopBand(line.rectTransform, 1f, 1f, 29f, 2f);
            }

            titleText = EqUi.Txt("Title", panel, font, 13, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.TopBand(titleText.rectTransform, 10f, 10f, 4f, 24f);
            EqUi.BestFit(titleText, 9);

            // Rozet + etiket + puan
            badge = EqUi.Img("Badge", panel, null, EqUi.Amber, skin);
            EqUi.TL(badge.rectTransform, 22f, 40f, 48f, 48f);
            badgeInitial = EqUi.Txt("Initial", badge.transform, font, 20, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            labelText = EqUi.Txt("Label", panel, font, 15, EqUi.GreenDark, TextAnchor.MiddleLeft);
            EqUi.TL(labelText.rectTransform, 78f, 46f, 346f, 22f);
            EqUi.BestFit(labelText, 10);
            scoreText = EqUi.Txt("Score", panel, font, 6, EqUi.Muted, TextAnchor.MiddleLeft);
            EqUi.TL(scoreText.rectTransform, 78f, 69f, 346f, 10f);

            for (int i = 0; i < RowCount; i++) rows.Add(BuildRow(i));

            var sep = EqUi.Img("Separator", panel, null, EqUi.CreamShade, skin);
            EqUi.TL(sep.rectTransform, 16f, RowTop + RowCount * RowStep + 6f, 408f, 1f);

            float notesY = RowTop + RowCount * RowStep + 12f;
            var info = EqUi.Img("NotesIcon", panel, EqUi.S(skin, s => s.IconInfo), EqUi.Blue, skin);
            EqUi.TL(info.rectTransform, 20f, notesY, 16f, 16f);
            if (info.sprite == null) EqUi.TL(info.rectTransform, 23f, notesY + 3f, 10f, 10f);
            var notesHeader = EqUi.Txt("NotesTitle", panel, font, 8, EqUi.Navy, TextAnchor.MiddleLeft);
            EqUi.TL(notesHeader.rectTransform, 40f, notesY, 300f, 16f);
            notesHeader.text = notesTitle;
            notesText = EqUi.Txt("Notes", panel, font, Mathf.Max(7, notesFontSize), EqUi.Navy, TextAnchor.UpperLeft);
            EqUi.TL(notesText.rectTransform, 40f, notesY + 17f, 384f, PanelH - 36f - (notesY + 17f));
            EqUi.BestFit(notesText, 7);

            BuildButton(BtnNext, continueLabel);
            BuildButton(BtnRetry, retryLabel);
            BuildButton(BtnMenu, menuLabel);

            // Devam seçilince kararma (yeni bölüm siyahtan kendi giriş kartıyla açılır)
            blackout = EqUi.Img("Blackout", canvas.transform, null, new Color(0f, 0f, 0f, 0f), skin, false);
            EqUi.Stretch(blackout.rectTransform);
            blackout.gameObject.SetActive(false);

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        private Row BuildRow(int i)
        {
            var r = new Row();
            r.root = EqUi.Rect("Row" + i, panel);
            EqUi.TL(r.root, 16f, RowTop + i * RowStep, 408f, RowStep - 1f);
            r.group = r.root.gameObject.AddComponent<CanvasGroup>();
            if (i % 2 == 0)
            {
                var stripe = EqUi.Img("Stripe", r.root, null, EqUi.A(EqUi.CreamShade, 0.45f), skin);
                EqUi.Stretch(stripe.rectTransform);
            }
            r.icon = EqUi.Img("Icon", r.root, null, EqUi.Green, skin);
            EqUi.TL(r.icon.rectTransform, 6f, 0f, 12f, 12f);
            r.label = EqUi.Txt("Label", r.root, font, 8, EqUi.Ink, TextAnchor.MiddleLeft);
            EqUi.TL(r.label.rectTransform, 24f, 0f, 200f, 13f);
            EqUi.BestFit(r.label, 6);
            r.value = EqUi.Txt("Value", r.root, font, 8, EqUi.Navy, TextAnchor.MiddleRight);
            EqUi.TL(r.value.rectTransform, 200f, 0f, 202f, 13f);
            EqUi.BestFit(r.value, 6);
            return r;
        }

        /// <summary>Birincil (yeşil) buton: Devam varsa o, yoksa Tekrar Dene (eski görünüm).</summary>
        private bool IsPrimary(int i) => order.Count > 0 && order[0] == i;

        private static string ButtonName(int i) => i == BtnNext ? "ContinueButton" : i == BtnRetry ? "RetryButton" : "MenuButton";

        private readonly Text[] buttonLabels = new Text[3];

        private void BuildButton(int i, string label)
        {
            var img = EqUi.Img(ButtonName(i), panel, EqUi.S(skin, s => s.Button), EqUi.Ink, skin, true);
            EqUi.TL(img.rectTransform, 0f, PanelH - 34f, 116f, 18f);
            EqUi.Fallback(img, EqUi.Ink, EqUi.Navy, skin);
            var t = EqUi.Txt("Label", img.transform, font, 9, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.StretchOffsets(t.rectTransform, 2f, 6f, 2f, 1f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = label;
            var b = img.gameObject.AddComponent<EqButton>();
            b.Init();
            int idx = i;
            b.Clicked += () => clicked = idx;
            b.HoverChanged += h => { if (h) { focus = idx; RefreshButtons(); } };
            buttons[i] = img;
            eqButtons[i] = b;
            buttonLabels[i] = t;
        }

        /// <summary>
        /// Görünen butonları dizer: sonraki bölüm (DEPREM → SEL) yüklenebiliyorsa [Devam: Sel Bölümü] [Tekrar Dene] [Menüye Dön],
        /// yoksa eskisi gibi [Tekrar Dene] [Menüye Dön]. Birincil (yeşil) buton en soldaki.
        /// </summary>
        private void LayoutButtons()
        {
            order.Clear();
            bool next = showContinue && GameSceneRouter.NextChapterAfter(AfetUyanisi.Shared.ChapterId.Earthquake).HasValue &&
                        Application.CanStreamedLevelBeLoaded(GameSceneRouter.FirstSceneOf(AfetUyanisi.Shared.ChapterId.Flood));
            if (next) order.Add(BtnNext);
            order.Add(BtnRetry);
            order.Add(BtnMenu);
            buttons[BtnNext].gameObject.SetActive(next);

            float[] widths = next ? new[] { 136f, 116f, 116f } : new[] { 116f, 116f };
            float[] xs = next ? new[] { 26f, 172f, 298f } : new[] { 94f, 230f };
            for (int k = 0; k < order.Count; k++)
            {
                int id = order[k];
                EqUi.TL(buttons[id].rectTransform, xs[k], PanelH - 34f, widths[k], 18f);
                bool primary = k == 0;
                buttonLabels[id].color = primary ? EqUi.White : EqUi.Cream;
                var shadow = buttonLabels[id].GetComponent<Shadow>();
                if (shadow != null) shadow.effectColor = primary ? EqUi.ShadowGreen : EqUi.Ink;
            }
        }

        private void RefreshButtons()
        {
            foreach (int i in order)
            {
                bool f = focus == i;
                bool primary = IsPrimary(i);
                var img = buttons[i];
                if (img.sprite != null && skin != null)
                {
                    Sprite normal = primary ? skin.ButtonStart : skin.Button;
                    Sprite hover = primary ? skin.ButtonStartHover : skin.ButtonHover;
                    EqUi.Skin(img, f && hover != null ? hover : normal, EqUi.Green, skin);
                }
                else EqUi.FallbackColors(img, f ? EqUi.Amber : EqUi.Ink, primary ? EqUi.Green : EqUi.Navy);
            }
        }

        private void MoveFocus(int dir)
        {
            int k = Mathf.Max(0, order.IndexOf(focus));
            focus = order[Mathf.Clamp(k + dir, 0, order.Count - 1)];
            RefreshButtons();
        }

        public override IEnumerator Show(EarthquakeReport report)
        {
            EnsureBuilt();
            if (report == null) report = new EarthquakeReport();
            report.choice = null;

            titleText.text = string.IsNullOrEmpty(report.title) ? defaultTitle : report.title;
            int tier = TierFor(report.score);
            Sprite bs = skin != null ? skin.RankBadge(tier) : null;
            EqUi.Skin(badge, bs, TierColor(tier), skin);
            badgeInitial.text = bs == null && !string.IsNullOrEmpty(report.label) ? report.label.Substring(0, 1) : "";
            labelText.text = report.label ?? "";
            labelText.color = TierColor(tier);
            int target = Mathf.Max(0, report.score);
            SetScore(0, tier);

            var data = BuildRows(report);
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < data.Count;
                rows[i].root.gameObject.SetActive(on);
                if (!on) continue;
                var (label, value, mark, valueColor) = data[i];
                rows[i].label.text = label;
                rows[i].value.text = value;
                rows[i].value.color = valueColor;
                Sprite icon = mark == Mark.Ok ? EqUi.S(skin, s => s.Check) : mark == Mark.Bad ? EqUi.S(skin, s => s.Cross) : EqUi.S(skin, s => s.ClockIcon);
                EqUi.Skin(rows[i].icon, icon, mark == Mark.Ok ? EqUi.Green : mark == Mark.Bad ? EqUi.Red : EqUi.Navy, skin);
                if (icon == null) EqUi.TL(rows[i].icon.rectTransform, 8f, 3f, 8f, 8f);
                else EqUi.TL(rows[i].icon.rectTransform, 6f, 0f, mark == Mark.Time ? 13f : 12f, mark == Mark.Time ? 13f : 12f);
                rows[i].group.alpha = 0f;
            }
            notesText.text = BuildNotes(report, maxNotes);

            LayoutButtons();
            focus = order[0];
            clicked = -1;
            RefreshButtons();
            for (int i = 0; i < 3; i++) eqButtons[i].Interactable = false;

            canvas.gameObject.SetActive(true);
            group.blocksRaycasts = true;

            // Giriş: kararma + panel aşağıdan
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = EqUi.OutCubic(t / 0.3f);
                group.alpha = k;
                EqUi.SetDim(dim, k);
                panel.anchoredPosition = EqUi.Snap(new Vector2(0f, Mathf.Lerp(-21f, -1f, k)));
                yield return null;
            }
            group.alpha = 1f;
            EqUi.SetDim(dim, 1f);
            panel.anchoredPosition = new Vector2(0f, -1f);

            // Satırlar sırayla, puan sayarak artar (Enter/Space/E ile atlanır)
            float reveal = 0.09f * data.Count + 0.25f;
            int startFrame = Time.frameCount;
            for (float t = 0f; t < reveal; t += Time.unscaledDeltaTime)
            {
                if (Time.frameCount > startFrame && t > 0.1f && (GameInput.ConfirmPressed || GameInput.InteractPressed)) break;
                for (int i = 0; i < data.Count; i++) rows[i].group.alpha = Mathf.Clamp01((t - i * 0.09f) / 0.12f);
                SetScore(Mathf.RoundToInt(target * EqUi.OutCubic(t / reveal)), tier);
                yield return null;
            }
            for (int i = 0; i < data.Count; i++) rows[i].group.alpha = 1f;
            SetScore(target, tier);
            yield return Pop(badge.rectTransform);

            // Seçim
            foreach (int i in order) eqButtons[i].Interactable = true;
            int choiceFrame = Time.frameCount;
            int chosen = -1;
            while (chosen < 0)
            {
                if (clicked >= 0 && order.Contains(clicked)) chosen = clicked;
                else if (Time.frameCount > choiceFrame)
                {
                    // Tuşlar soldan sağa: 1 = birincil (Devam ya da Tekrar Dene), 2, 3
                    int k = GameInput.ChoiceKeyPressed;
                    if (k >= 1 && k <= order.Count) chosen = order[k - 1];
                    else if (GameInput.LeftPressed) MoveFocus(-1);
                    else if (GameInput.RightPressed) MoveFocus(1);
                    else if (GameInput.ConfirmPressed || GameInput.InteractPressed) chosen = focus;
                }
                yield return null;
            }
            for (int i = 0; i < 3; i++) eqButtons[i].Interactable = false;
            focus = chosen;
            RefreshButtons();
            // Basılma: 1 px aşağı
            var brt = buttons[chosen].rectTransform;
            Vector2 p = brt.anchoredPosition;
            brt.anchoredPosition = p + new Vector2(0f, -1f);
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime) yield return null;
            brt.anchoredPosition = p;

            if (chosen == BtnNext)
            {
                // Menüye dönmeden kısa kararma; Sel bölümü siyahtan "SEL BÖLÜMÜ" kartıyla açılır.
                group.blocksRaycasts = true;
                blackout.gameObject.SetActive(true);
                blackout.transform.SetAsLastSibling();
                float dur = Mathf.Max(0.05f, continueFadeSeconds);
                for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
                {
                    blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / dur));
                    yield return null;
                }
                blackout.color = Color.black;
                report.choice = ChoiceNext;
            }
            else report.choice = chosen == BtnRetry ? ChoiceRetry : ChoiceMenu;
        }

        /// <summary>Raporu kapatır (sistem sahneyi değiştirmeden devam edecekse).</summary>
        public void Hide()
        {
            if (!built) return;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            canvas.gameObject.SetActive(false);
        }

        private void SetScore(int score, int tier)
        {
            string s = "Puan " + score + " / 100";
            int next = tier > 0 && tierMinScores != null && tier - 1 < tierMinScores.Length ? tierMinScores[tier - 1] : -1;
            if (next > 0) s += "  ·  Bir sonraki seviye: " + next;
            scoreText.text = s;
        }

        private IEnumerator Pop(RectTransform rt)
        {
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float s = 1f + 0.1f * Mathf.Sin(t / 0.25f * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private List<(string, string, Mark, Color)> BuildRows(EarthquakeReport r)
        {
            var list = new List<(string, string, Mark, Color)>();
            Color navy = EqUi.Navy, bad = EqUi.RedDark;
            bool hasDecisions = r.correctDecisions + r.wrongDecisions > 0;
            int good = hasDecisions ? r.correctDecisions : r.correctAnswers;
            int wrong = hasDecisions ? r.wrongDecisions : r.wrongAnswers + r.timeouts;
            int total = good + wrong;
            list.Add(("Doğru kararlar", total > 0 ? good + " / " + total : "-", good > 0 ? Mark.Ok : Mark.Bad, navy));
            list.Add(("Yanlış / süre doldu", wrong.ToString(), wrong == 0 ? Mark.Ok : Mark.Bad, wrong == 0 ? navy : bad));
            list.Add(("Bulunan tehlikeler", r.hazardsTotal > 0 ? r.hazardsFound + " / " + r.hazardsTotal : r.hazardsFound.ToString(),
                r.hazardsFound > 0 ? Mark.Ok : Mark.Bad, r.hazardsFound > 0 ? navy : bad));
            list.Add(("Gereksiz tehlike maruziyeti", r.exposures == 0 ? "Yok" : r.exposures.ToString(),
                r.exposures == 0 ? Mark.Ok : Mark.Bad, r.exposures == 0 ? navy : bad));
            bool protectedOk = r.protection == ProtectionOutcome.TableDropCoverHold || r.protection == ProtectionOutcome.InPlaceDropCover;
            list.Add(("Korunma", string.IsNullOrEmpty(r.protectionText) ? ProtectionText(r.protection) : r.protectionText,
                protectedOk ? Mark.Ok : Mark.Bad, protectedOk ? navy : bad));
            if (r.emergencyBagMinigame)
            {
                bool bagOk = r.emergencyBagPrepared && r.bagTaken;
                string bagText = !r.emergencyBagPrepared ? "Hazırlanmadı" : r.bagTaken ? "Hazırlandı, alındı" : "Hazırlandı, alınmadı";
                list.Add(("Afet çantası", bagText, bagOk ? Mark.Ok : Mark.Bad, bagOk ? navy : bad));
            }
            else list.Add(("Deprem çantası", r.bagTaken ? "Alındı" : "Alınmadı", r.bagTaken ? Mark.Ok : Mark.Bad, r.bagTaken ? navy : bad));
            list.Add(("Asansör", r.elevatorTried ? "Denendi" : "Kullanılmadı", r.elevatorTried ? Mark.Bad : Mark.Ok, r.elevatorTried ? bad : navy));
            if (r.prepTotal > 0)
            {
                bool prepOk = r.prepDone * 2 >= r.prepTotal;
                list.Add(("Hazırlık görevleri", r.prepDone + " / " + r.prepTotal, prepOk ? Mark.Ok : Mark.Bad, prepOk ? navy : bad));
            }
            string remain = !string.IsNullOrEmpty(r.remainingText) ? r.remainingText
                : r.acceleratedReady ? "Hazırlık tamam" : EqUi.Clock(r.remainingAtQuake);
            list.Add(("Depremden önce kalan süre", remain, Mark.Time, navy));
            return list;
        }

        private static string ProtectionText(ProtectionOutcome p)
        {
            switch (p)
            {
                case ProtectionOutcome.TableDropCoverHold: return "Masa altında Çök–Kapan–Tutun";
                case ProtectionOutcome.InPlaceDropCover: return "Yerinde Çök–Kapan";
                case ProtectionOutcome.Exposed: return "Tehlikeye açık yerde";
                default: return "Yapılmadı";
            }
        }

        /// <summary>Yanlışlar (kırmızı madde) + ipuçları (amber madde); tekrarlar ayıklanır, en fazla max madde.</summary>
        private static string BuildNotes(EarthquakeReport r, int max)
        {
            var sb = new StringBuilder();
            string red = EqUi.HexOf(EqUi.RedDark), amber = EqUi.HexOf(EqUi.AmberDark), green = EqUi.HexOf(EqUi.GreenDark);
            var seen = new HashSet<string>();
            int n = 0;
            void Add(string line, string color)
            {
                if (string.IsNullOrEmpty(line) || n >= max) return;
                line = line.Trim();
                if (!seen.Add(line)) return;
                if (n++ > 0) sb.Append('\n');
                sb.Append("<color=#").Append(color).Append(">•</color> ").Append(line);
            }
            if (r.mistakes != null) foreach (var m in r.mistakes) Add(m, red);
            if (r.tips != null) foreach (var tip in r.tips) Add(tip, amber);
            if (n == 0) sb.Append("<color=#").Append(green).Append(">•</color> Harika! Bütün kararların güvenliydi.");
            return sb.ToString();
        }

        private int TierFor(int score)
        {
            if (tierMinScores == null || tierMinScores.Length == 0) return 0;
            for (int i = 0; i < tierMinScores.Length; i++)
                if (score >= tierMinScores[i]) return i;
            return tierMinScores.Length - 1;
        }

        private static Color TierColor(int tier)
        {
            switch (tier)
            {
                case 0: return EqUi.AmberDark;
                case 1: return EqUi.GreenDark;
                case 2: return EqUi.Hex(0x235F9E);
                default: return EqUi.Navy;
            }
        }
    }
}
