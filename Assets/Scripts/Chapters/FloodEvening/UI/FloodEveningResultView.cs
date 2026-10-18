using System.Collections;
using System.Collections.Generic;
using System.Text;
using AfetUyanisi.Chapters.Earthquake;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>Akşam sel bölümünün sonuç verisi (FloodEveningController doldurur; puan ve satırları rapor ekranı ekler).</summary>
    public class FloodEveningResult
    {
        public bool success;
        public string title;
        public string message;
        public readonly List<string> correct = new List<string>();
        public readonly List<string> corrected = new List<string>();
        public readonly List<string> lessons = new List<string>();
        public string note;
        /// <summary>Doğru karar yoksa gösterilecek satır.</summary>
        public string emptyCorrect;
        /// <summary>Oyuncunun seçimi: "Next", "Retry" ya da "Menu".</summary>
        public string choice;
        /// <summary>Rapordaki puan (0–100). Ekran gösterilince FloodEveningResultView hesaplar; önce -1.</summary>
        public int score = -1;
        /// <summary>Rapor satırları "etiket: değer" (kayıt ve test için; ekran doldurur).</summary>
        public readonly List<string> rows = new List<string>();
        /// <summary>"Öğrenilecekler" maddeleri (ekran doldurur).</summary>
        public readonly List<string> notes = new List<string>();
    }

    /// <summary>
    /// "SEL FARKINDALIK RAPORU" — deprem raporuyla (ResultReportView) aynı görünüm: aynı 9-slice panel/buton sprite'ları
    /// (Assets/Art/UI/Earthquake), Berk'in yazı tipi (GameFonts.Story), lacivert başlık bandı, su dalgalı madalya
    /// (UI_FE_Medal_Water), büyük sonuç başlığı + "Puan X / 100", ✓ / ⚠ / ✗ ikonlu satırlar, "Öğrenilecekler" (3 madde:
    /// oyuncunun gerçekten yaptığı hatalardan; hata yoksa genel doğru bilgiler) ve üç buton:
    /// [Tekrar Dene] [Menüye Dön] [Deprem Bölümüne Geç / Oyunu Bitir]. Devam butonu başarısız ekranda da vardır
    /// (lider kararı 18 Eki): GameFlow.ContinueAfter(Flood) bölümü tamamlanmış saymadan sıradaki afete geçer.
    /// Yeşil (birincil) buton: başarıda Devam, başarısızlıkta Tekrar Dene; odak onunla başlar.
    /// Satır verisi sahnedeki FloodEveningController'ın okunur durumundan (adımlar, yanlışlar, kısa yol, süre) ve
    /// çevre tehlikelerinin sayaçlarından (FloodElectricHazards.ShockCount, FloodDebrisField.BumpCount) gelir; verisi
    /// olmayan satır gösterilmez. Klavye: ←/→ (A/D) seçim, Enter/Space/E onay, 1/2/3 soldan sağa, R Tekrar Dene;
    /// fare ile tıklanır. Klavye gezintisi de hover/tık sesi çalar (fare sesleri AudioAutoWiring'den, EqButton).
    /// Seçim result.choice'a yazılır ("Next"te önce ekran kararır); sahne geçişini controller yapar. Canvas 110.
    /// </summary>
    public class FloodEveningResultView : MonoBehaviour
    {
        [Tooltip("Boşsa oyun içi yazı tipi (GameFonts.Story), deprem raporuyla aynı")]
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 110;
        [Tooltip("Ekran açıldıktan sonra tuşların yok sayıldığı süre (son diyaloğu kapatan tuş butona basmasın)")]
        [SerializeField] private float inputDelay = 0.6f;
        [Tooltip("'Devam' seçilince ekranın kararma süresi (sn)")]
        [SerializeField] private float continueFadeSeconds = 0.6f;

        [Header("Metinler")]
        [SerializeField] private string reportTitle = "SEL FARKINDALIK RAPORU";
        [SerializeField] private string notesTitle = "Öğrenilecekler";
        [SerializeField] private string retryLabel = "Tekrar Dene";
        [SerializeField] private string menuLabel = "Menüye Dön";
        [SerializeField] private string toEarthquakeLabel = "Deprem Bölümüne Geç";
        [SerializeField] private string finishLabel = "Oyunu Bitir";
        [Tooltip("'Öğrenilecekler' madde sayısı")]
        [SerializeField, Range(1, 5)] private int maxNotes = 3;

        [Header("Görseller (deprem raporuyla aynı sprite'lar; boşsa düz renk)")]
        [SerializeField] private Sprite reportPanel;
        [SerializeField] private Sprite dimSprite;
        [SerializeField] private Sprite button;
        [SerializeField] private Sprite buttonHover;
        [SerializeField] private Sprite buttonStart;
        [SerializeField] private Sprite buttonStartHover;
        [SerializeField] private Sprite checkIcon;
        [SerializeField] private Sprite crossIcon;
        [SerializeField] private Sprite warningIcon;
        [SerializeField] private Sprite clockIcon;
        [SerializeField] private Sprite infoIcon;
        [Tooltip("UI_FE_Medal_Water (48x48, tools/ui_report/medal_water.js)")]
        [SerializeField] private Sprite medal;

        public const string ChoiceRetry = "Retry";
        public const string ChoiceMenu = "Menu";
        /// <summary>Devam butonu: sıradaki afet ya da oyun sonu (GameFlow.ContinueAfter).</summary>
        public const string ChoiceNext = "Next";

        private const float PanelW = 440f, PanelH = 330f;
        private const int RowCount = 9;
        private const float RowTop = 90f, RowStep = 13f;
        // Buton sırası (soldan sağa, lider maketi): 0 = Tekrar Dene, 1 = Menüye Dön, 2 = Devam.
        private const int BtnRetry = 0, BtnMenu = 1, BtnNext = 2;

        private enum Mark { Ok, Warn, Bad, Time }

        private class Row
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image icon;
            public Text label;
            public Text value;
        }

        private bool built;
        private Canvas canvas;
        private CanvasGroup group;
        private Image dim, blackout, medalImg;
        private RectTransform panel;
        private Text titleText, labelText, scoreText, notesText;
        private readonly List<Row> rows = new List<Row>();
        private readonly Image[] buttons = new Image[3];
        private readonly Text[] buttonLabels = new Text[3];
        private readonly EqButton[] eqButtons = new EqButton[3];
        private readonly List<int> order = new List<int>();
        private string primaryChoice = ChoiceRetry;
        private int focus;
        private int clicked = -1;

        public bool IsShown { get; private set; }

        /// <summary>
        /// Eski API (flood_evening.json "ui" etiketleri). Rapor başlığı ve bölüm başlıkları artık bu ekranın kendi metinleridir;
        /// retry/menu boş değilse buton metni olur ("[R] " öneki atılır, R tuşu yine çalışır). Birincil buton Tekrar Dene'ye döner.
        /// </summary>
        public void SetLabels(string header, string correct, string corrected, string lessons, string retry, string menu)
        {
            Build();
            string r = (retry ?? "").Trim();
            if (r.StartsWith("[R]")) r = r.Substring(3).Trim();
            buttonLabels[BtnRetry].text = string.IsNullOrEmpty(r) ? retryLabel : r;
            buttonLabels[BtnMenu].text = string.IsNullOrEmpty(menu) ? menuLabel : menu;
            primaryChoice = ChoiceRetry;
        }

        /// <summary>Yeşil (birincil, ilk odaklı) butonu seçer: ChoiceNext (başarı) ya da ChoiceRetry. Devam butonunun metni
        /// GameFlow'dan gelir ("Deprem Bölümüne Geç" / "Oyunu Bitir"); label yalnız Tekrar Dene için kullanılır.</summary>
        public void SetPrimaryAction(string choice, string label)
        {
            Build();
            primaryChoice = string.IsNullOrEmpty(choice) ? ChoiceRetry : choice;
            if (primaryChoice == ChoiceRetry && !string.IsNullOrEmpty(label)) buttonLabels[BtnRetry].text = label;
        }

        private void Awake() => Build();

        // =====================================================================
        // Kurulum (deprem raporuyla aynı ölçüler, 640x360 referans)
        // =====================================================================

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = EqUi.ResolveFont(null);
            canvas = EqUi.CreateCanvas(this, "FE_ResultCanvas", sortingOrder, out group);

            dim = EqUi.Img("Dim", canvas.transform, dimSprite, EqUi.A(EqUi.Ink, 0f), null, true);
            dim.preserveAspect = false;
            dim.type = Image.Type.Simple;
            EqUi.Stretch(dim.rectTransform);
            EqUi.SetDim(dim, 0f);

            var pImg = EqUi.Img("ReportPanel", canvas.transform, reportPanel, EqUi.Ink, null);
            panel = pImg.rectTransform;
            EqUi.Place(panel, 0.5f, 0.5f, new Vector2(0f, -1f), new Vector2(PanelW, PanelH));
            if (pImg.sprite == null)
            {
                EqUi.Fallback(pImg, EqUi.Ink, EqUi.Cream, null, 4f, 24f, EqUi.Navy);
                var line = EqUi.Img("AmberLine", panel, null, EqUi.Amber, null);
                EqUi.TopBand(line.rectTransform, 1f, 1f, 29f, 2f);
            }

            titleText = EqUi.Txt("Title", panel, font, 13, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.TopBand(titleText.rectTransform, 10f, 10f, 4f, 24f);
            EqUi.BestFit(titleText, 9);
            titleText.text = reportTitle;

            // Madalya + sonuç başlığı + puan
            medalImg = EqUi.Img("Medal", panel, medal, EqUi.Blue, null);
            EqUi.TL(medalImg.rectTransform, 22f, 38f, 48f, 48f);
            labelText = EqUi.Txt("Label", panel, font, 15, EqUi.GreenDark, TextAnchor.MiddleLeft);
            EqUi.TL(labelText.rectTransform, 78f, 44f, 346f, 22f);
            EqUi.BestFit(labelText, 10);
            scoreText = EqUi.Txt("Score", panel, font, 8, EqUi.Muted, TextAnchor.MiddleLeft);
            EqUi.TL(scoreText.rectTransform, 78f, 67f, 346f, 12f);

            for (int i = 0; i < RowCount; i++) rows.Add(BuildRow(i));

            float sepY = RowTop + (RowCount - 1) * RowStep + 6f;
            var sep = EqUi.Img("Separator", panel, null, EqUi.CreamShade, null);
            EqUi.TL(sep.rectTransform, 16f, sepY, 408f, 1f);

            float notesY = sepY + 6f;
            var info = EqUi.Img("NotesIcon", panel, infoIcon, EqUi.Blue, null);
            EqUi.TL(info.rectTransform, 20f, notesY, 16f, 16f);
            if (info.sprite == null) EqUi.TL(info.rectTransform, 23f, notesY + 3f, 10f, 10f);
            var notesHeader = EqUi.Txt("NotesTitle", panel, font, 9, EqUi.Navy, TextAnchor.MiddleLeft);
            EqUi.TL(notesHeader.rectTransform, 40f, notesY, 300f, 16f);
            notesHeader.text = notesTitle;
            notesText = EqUi.Txt("Notes", panel, font, 9, EqUi.Navy, TextAnchor.UpperLeft);
            EqUi.TL(notesText.rectTransform, 44f, notesY + 18f, 380f, PanelH - 40f - (notesY + 18f));
            notesText.lineSpacing = 1.15f;
            EqUi.BestFit(notesText, 7);

            BuildButton(BtnRetry, retryLabel);
            BuildButton(BtnMenu, menuLabel);
            BuildButton(BtnNext, toEarthquakeLabel);

            blackout = EqUi.Img("Blackout", canvas.transform, null, new Color(0f, 0f, 0f, 0f), null, false);
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
                var stripe = EqUi.Img("Stripe", r.root, null, EqUi.A(EqUi.CreamShade, 0.45f), null);
                EqUi.Stretch(stripe.rectTransform);
            }
            r.icon = EqUi.Img("Icon", r.root, null, EqUi.Green, null);
            EqUi.TL(r.icon.rectTransform, 6f, 0f, 12f, 12f);
            r.label = EqUi.Txt("Label", r.root, font, 8, EqUi.Ink, TextAnchor.MiddleLeft);
            EqUi.TL(r.label.rectTransform, 24f, 0f, 200f, 13f);
            EqUi.BestFit(r.label, 6);
            r.value = EqUi.Txt("Value", r.root, font, 8, EqUi.Navy, TextAnchor.MiddleRight);
            EqUi.TL(r.value.rectTransform, 200f, 0f, 202f, 13f);
            EqUi.BestFit(r.value, 6);
            return r;
        }

        private static string ButtonName(int i) => i == BtnNext ? "ContinueButton" : i == BtnRetry ? "RetryButton" : "MenuButton";

        private void BuildButton(int i, string label)
        {
            var img = EqUi.Img(ButtonName(i), panel, button, EqUi.Ink, null, true);
            EqUi.TL(img.rectTransform, 0f, PanelH - 34f, 128f, 18f);
            EqUi.Fallback(img, EqUi.Ink, EqUi.Navy, null);
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

        private int PrimaryButton => primaryChoice == ChoiceNext && order.Contains(BtnNext) ? BtnNext
            : primaryChoice == ChoiceMenu ? BtnMenu : BtnRetry;

        /// <summary>[Tekrar Dene] [Menüye Dön] [Devam]; Devam yalnız sıradaki afetin sahnesi (ya da oyun sonu) varsa.</summary>
        private void LayoutButtons()
        {
            const ChapterId flood = ChapterId.Flood;
            bool next = GameFlow.CanContinueAfter(flood, true);
            if (next)
            {
                var n = GameFlow.NextChapter(flood);
                buttonLabels[BtnNext].text = !n.HasValue ? finishLabel
                    : n.Value == ChapterId.Earthquake ? toEarthquakeLabel : GameFlow.ContinueLabel(flood);
            }
            order.Clear();
            order.Add(BtnRetry);
            order.Add(BtnMenu);
            if (next) order.Add(BtnNext);
            buttons[BtnNext].gameObject.SetActive(next);

            float[] widths = next ? new[] { 128f, 128f, 128f } : new[] { 128f, 128f };
            float[] xs = next ? new[] { 20f, 156f, 292f } : new[] { 84f, 228f };
            int primary = PrimaryButton;
            for (int k = 0; k < order.Count; k++)
            {
                int id = order[k];
                EqUi.TL(buttons[id].rectTransform, xs[k], PanelH - 34f, widths[k], 18f);
                bool p = id == primary;
                buttonLabels[id].color = p ? EqUi.White : EqUi.Cream;
                var shadow = buttonLabels[id].GetComponent<Shadow>();
                if (shadow != null) shadow.effectColor = p ? EqUi.ShadowGreen : EqUi.Ink;
            }
        }

        private void RefreshButtons()
        {
            int primary = PrimaryButton;
            foreach (int i in order)
            {
                bool f = focus == i;
                bool p = i == primary;
                var img = buttons[i];
                Sprite normal = p ? buttonStart : button;
                Sprite hover = p ? buttonStartHover : buttonHover;
                if (normal != null)
                {
                    EqUi.Skin(img, f && hover != null ? hover : normal, EqUi.Green, null);
                    var fill = img.transform.Find("Fill");
                    if (fill != null) fill.gameObject.SetActive(false);
                }
                else EqUi.FallbackColors(img, f ? EqUi.Amber : EqUi.Ink, p ? EqUi.Green : EqUi.Navy);
            }
        }

        private void MoveFocus(int dir)
        {
            int k = Mathf.Max(0, order.IndexOf(focus));
            int nf = order[Mathf.Clamp(k + dir, 0, order.Count - 1)];
            if (nf == focus) return;
            focus = nf;
            RefreshButtons();
            GameAudio.PlaySfx("SFX_UI_Hover", 0.35f);
        }

        // =====================================================================
        // Gösterim
        // =====================================================================

        /// <summary>Ekranı gösterir ve oyuncu seçene kadar bekler (result.choice dolar).</summary>
        public IEnumerator Show(FloodEveningResult result)
        {
            Build();
            if (result == null) result = new FloodEveningResult();
            result.choice = null;
            blackout.gameObject.SetActive(false);

            var data = Collect(result, out int score, out List<(string text, bool mistake)> notes);
            result.score = score;
            result.rows.Clear();
            foreach (var d in data) result.rows.Add(d.label + ": " + d.value);
            result.notes.Clear();
            foreach (var n in notes) result.notes.Add(n.text);
            Debug.Log("[FE] Sel raporu: " + (result.success ? "başarılı" : "başarısız") + ", puan " + score + " | " +
                string.Join(" | ", result.rows) + " | öğrenilecekler: " + string.Join(" / ", result.notes));

            titleText.text = reportTitle;
            labelText.text = string.IsNullOrEmpty(result.title) ? (result.success ? "EVE ULAŞTIN" : "SEL KRİTİK SEVİYEYE ULAŞTI") : result.title;
            labelText.color = result.success ? EqUi.GreenDark : EqUi.AmberDark;
            EqUi.Skin(medalImg, medal, result.success ? EqUi.Blue : EqUi.NavyLight, null);
            SetScore(0);

            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < data.Count;
                rows[i].root.gameObject.SetActive(on);
                if (!on) continue;
                var d = data[i];
                rows[i].label.text = d.label;
                rows[i].value.text = d.value;
                rows[i].value.color = d.mark == Mark.Bad ? EqUi.RedDark : d.mark == Mark.Warn ? EqUi.AmberDark : EqUi.Navy;
                Sprite icon = d.mark == Mark.Ok ? checkIcon : d.mark == Mark.Bad ? crossIcon : d.mark == Mark.Warn ? warningIcon : clockIcon;
                Color fallback = d.mark == Mark.Ok ? EqUi.Green : d.mark == Mark.Bad ? EqUi.Red : d.mark == Mark.Warn ? EqUi.Amber : EqUi.Navy;
                EqUi.Skin(rows[i].icon, icon, fallback, null);
                if (icon == null) EqUi.TL(rows[i].icon.rectTransform, 8f, 3f, 8f, 8f);
                else if (d.mark == Mark.Warn) EqUi.TL(rows[i].icon.rectTransform, 6f, 0f, 12f, 12f);
                else EqUi.TL(rows[i].icon.rectTransform, 6f, 0f, d.mark == Mark.Time ? 13f : 12f, d.mark == Mark.Time ? 13f : 12f);
                rows[i].group.alpha = 0f;
            }
            notesText.text = NotesText(notes);

            LayoutButtons();
            focus = PrimaryButton;
            clicked = -1;
            RefreshButtons();
            for (int i = 0; i < 3; i++) eqButtons[i].Interactable = false;

            canvas.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            IsShown = true;

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
            float shownAt = Time.unscaledTime;
            float reveal = 0.09f * data.Count + 0.25f;
            int startFrame = Time.frameCount;
            for (float t = 0f; t < reveal; t += Time.unscaledDeltaTime)
            {
                if (Time.frameCount > startFrame && Time.unscaledTime - shownAt > inputDelay * 0.5f &&
                    (GameInput.ConfirmPressed || GameInput.InteractPressed)) break;
                for (int i = 0; i < data.Count; i++) rows[i].group.alpha = Mathf.Clamp01((t - i * 0.09f) / 0.12f);
                SetScore(Mathf.RoundToInt(score * EqUi.OutCubic(t / reveal)));
                yield return null;
            }
            for (int i = 0; i < data.Count; i++) rows[i].group.alpha = 1f;
            SetScore(score);
            yield return Pop(medalImg.rectTransform);
            while (Time.unscaledTime - shownAt < inputDelay) yield return null;

            // Seçim
            foreach (int i in order) eqButtons[i].Interactable = true;
            int choiceFrame = Time.frameCount;
            int chosen = -1;
            bool byKey = false;
            while (chosen < 0)
            {
                if (clicked >= 0 && order.Contains(clicked)) chosen = clicked;
                else if (Time.frameCount > choiceFrame)
                {
                    int k = GameInput.ChoiceKeyPressed;
                    if (k >= 1 && k <= order.Count) { chosen = order[k - 1]; byKey = true; }
                    else if (GameInput.RetryPressed) { chosen = BtnRetry; byKey = true; }
                    else if (GameInput.LeftPressed) MoveFocus(-1);
                    else if (GameInput.RightPressed) MoveFocus(1);
                    else if (GameInput.ConfirmPressed || GameInput.InteractPressed) { chosen = focus; byKey = true; }
                }
                yield return null;
            }
            // Fare tıkının sesi AudioAutoWiring'den (EqButton) gelir; klavye onayı burada duyulur.
            if (byKey) GameAudio.PlaySfx("SFX_UI_Click", 0.5f);
            for (int i = 0; i < 3; i++) eqButtons[i].Interactable = false;
            focus = chosen;
            RefreshButtons();
            var brt = buttons[chosen].rectTransform;
            Vector2 p0 = brt.anchoredPosition;
            brt.anchoredPosition = p0 + new Vector2(0f, -1f);
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime) yield return null;
            brt.anchoredPosition = p0;

            result.choice = chosen == BtnNext ? ChoiceNext : chosen == BtnRetry ? ChoiceRetry : ChoiceMenu;
            if (result.choice == ChoiceNext)
            {
                // Menüye dönmeden kısa kararma; sıradaki afet kendi giriş kartıyla açılır.
                blackout.gameObject.SetActive(true);
                blackout.transform.SetAsLastSibling();
                float dur = Mathf.Max(0.05f, continueFadeSeconds);
                for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
                {
                    blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / dur));
                    yield return null;
                }
                blackout.color = Color.black;
            }
        }

        public void Hide()
        {
            if (!built) return;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            canvas.gameObject.SetActive(false);
            IsShown = false;
        }

        private void SetScore(int score) => scoreText.text = "Puan " + score + " / 100";

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

        // =====================================================================
        // Veri: satırlar, puan, öğrenilecekler (yalnız gerçekten ölçülen şeyler)
        // =====================================================================

        private struct RowData
        {
            public string label, value;
            public Mark mark;
            public RowData(string l, string v, Mark m) { label = l; value = v; mark = m; }
        }

        private static bool Has<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include) != null;

        private List<RowData> Collect(FloodEveningResult r, out int score, out List<(string text, bool mistake)> notes)
        {
            var list = new List<RowData>();
            notes = new List<(string, bool)>();
            var ctl = Object.FindAnyObjectByType<FloodEveningController>(FindObjectsInactive.Include);
            bool success = r.success;
            if (ctl == null)
            {
                // Test sahnesi: controller yoksa yalnız sonuç listeleri
                int c = r.correct.Count, w = r.corrected.Count;
                list.Add(new RowData("Doğru kararlar", c.ToString(), c > 0 ? Mark.Ok : Mark.Bad));
                list.Add(new RowData("Düzeltilen yanlışlar", w.ToString(), w == 0 ? Mark.Ok : Mark.Warn));
                score = success ? 70 : 30;
                AddTips(notes, r);
                return list;
            }

            var electric = Object.FindAnyObjectByType<FloodElectricHazards>(FindObjectsInactive.Include);
            var debris = Object.FindAnyObjectByType<FloodDebrisField>(FindObjectsInactive.Include);
            bool phonePresent = Has<FloodWarningPhone>();
            bool rumorPresent = Has<RumorCheckPoint>();
            bool branchPresent = Has<BranchObstacle>();
            bool finalePresent = Has<ApartmentFinale>();

            int shocks = electric != null ? electric.ShockCount : 0;
            int bumps = debris != null ? debris.BumpCount : 0;
            bool warningRead = ctl.HasStep("warning_read");
            bool highRoad = ctl.HasStep("high_road");
            bool shortTried = ctl.ShortRoadTried;
            bool branch = ctl.HasStep("branch_cleared");
            int rumorTrusted = ctl.MistakeCount("rumor_trusted");
            int wrongCode = ctl.MistakeCount("wrong_code");
            bool reached = success && ctl.HasStep("safe_zone");
            float remaining = success ? Mathf.Max(0f, ctl.FloodSeconds - ctl.Elapsed) : 0f;

            // Doğru kararlar: bölümde gerçekten var olan karar noktaları
            int good = 0, total = 0;
            void Decision(bool ok) { total++; if (ok) good++; }
            if (phonePresent) Decision(warningRead);
            Decision(highRoad && !shortTried);
            if (rumorPresent) Decision(ctl.HasStep("rumor_checked") && rumorTrusted == 0);
            if (branchPresent) Decision(branch);
            if (electric != null) Decision(shocks == 0);
            Decision(reached);
            list.Add(new RowData("Doğru kararlar", good + " / " + total,
                good == total ? Mark.Ok : good * 2 >= total ? Mark.Warn : Mark.Bad));

            int wrong = rumorTrusted + wrongCode;
            string wrongText = success ? wrong.ToString() : wrong > 0 ? wrong + " + süre doldu" : "Süre doldu";
            list.Add(new RowData("Yanlış / süre doldu", wrongText, wrong == 0 && success ? Mark.Ok : Mark.Bad));

            if (electric != null || debris != null)
            {
                int n = shocks + bumps;
                list.Add(new RowData("Gereksiz tehlike maruziyeti", n == 0 ? "Yok" : n + " kez", n == 0 ? Mark.Ok : Mark.Bad));
            }

            string route = highRoad ? (shortTried ? "Kısmen takip edildi" : "Takip edildi") : "Takip edilmedi";
            list.Add(new RowData("Güvenli rota", route, highRoad ? (shortTried ? Mark.Warn : Mark.Ok) : Mark.Bad));

            if (phonePresent)
                list.Add(new RowData("Sel uyarısı", warningRead ? "Okundu" : "Okunmadı", warningRead ? Mark.Ok : Mark.Bad));

            if (electric != null)
                list.Add(new RowData("Elektrik tehlikesi", shocks == 0 ? "Uzak duruldu" : shocks + " kez temas", shocks == 0 ? Mark.Ok : Mark.Bad));

            if (finalePresent)
            {
                string[] homeSteps = { ApartmentFinale.LockCheckedStep, ApartmentFinale.PinFoundStep, ApartmentFinale.PinEnteredStep,
                    ApartmentFinale.HandleTurnedStep, ApartmentFinale.HomeEnteredStep };
                int done = 0;
                foreach (var s in homeSteps) if (ctl.HasStep(s)) done++;
                list.Add(new RowData("Eve giriş görevleri", done + " / " + homeSteps.Length,
                    done == homeSteps.Length ? Mark.Ok : done > 0 ? Mark.Warn : Mark.Bad));
            }

            list.Add(new RowData("Kalan süre", EqUi.Clock(remaining), Mark.Time));

            // Puan (100): varış 30 + eve giriş 10 + uyarı 10 + rota 15 + dal 10 + elektrik 10 + enkaz 5 + kalan süre 10, yanlış başına -5
            int sc = 0;
            if (reached) sc += 30;
            if (finalePresent ? ctl.HasStep(ApartmentFinale.HomeEnteredStep) && success : reached) sc += 10;
            if (!phonePresent || warningRead) sc += 10;
            if (highRoad) sc += shortTried ? 10 : 15;
            if (branchPresent ? branch : reached) sc += 10;
            sc += shocks == 0 ? 10 : shocks == 1 ? 5 : 0;
            sc += bumps == 0 ? 5 : bumps <= 2 ? 2 : 0;
            if (success) sc += Mathf.RoundToInt(10f * Mathf.Clamp01(remaining / Mathf.Max(1f, ctl.FloodSeconds)));
            sc -= 5 * wrong;
            score = Mathf.Clamp(sc, 0, 100);

            // Öğrenilecekler: önce oyuncunun gerçek hataları, sonra genel doğru bilgiler
            if (!success) notes.Add(("Sel uyarısı gelince oyalanma; güvenli rotadan hemen ilerle.", true));
            if (shocks > 0) notes.Add(("Sarkan kabloya ve yakınındaki suya yaklaşma.", true));
            if (shortTried || !highRoad) notes.Add(("Kapatılmış ya da suyla kaplı yola girme.", true));
            if (phonePresent && !warningRead) notes.Add(("Resmî sel uyarısını hemen oku ve uygula.", true));
            if (rumorTrusted > 0) notes.Add(("Söylentiye değil, resmî duyurulara güven.", true));
            if (bumps > 0) notes.Add(("Akan sudaki yüzen eşyalardan uzak dur.", true));
            if (notes.Count > maxNotes) notes.RemoveRange(maxNotes, notes.Count - maxNotes);
            AddTips(notes, r);
            return list;
        }

        // Aynı konuyu iki kez söylemesin diye konu anahtarları (küçük harf).
        private static readonly string[] Topics = { "kablo", "kapat", "suyla kaplı", "söylenti", "uyarı", "yüzen", "yönlendirme" };

        private void AddTips(List<(string text, bool mistake)> notes, FloodEveningResult r)
        {
            var used = new HashSet<string>();
            foreach (var n in notes) foreach (var t in Topics) if (n.text.ToLowerInvariant().Contains(t)) used.Add(t);
            var tips = new List<string>(r.lessons);
            tips.Add("Suyla kaplı yola girme; akan su seni düşürebilir.");
            tips.Add("Sarkan kabloya ve yakınındaki suya yaklaşma.");
            tips.Add("Yetkililerin yönlendirmelerini izle.");
            foreach (var tip in tips)
            {
                if (notes.Count >= maxNotes) break;
                if (string.IsNullOrEmpty(tip)) continue;
                string low = tip.ToLowerInvariant();
                bool dup = false;
                foreach (var t in Topics) if (low.Contains(t) && used.Contains(t)) { dup = true; break; }
                if (dup) continue;
                foreach (var t in Topics) if (low.Contains(t)) used.Add(t);
                notes.Add((tip.Trim(), false));
            }
        }

        private static string NotesText(List<(string text, bool mistake)> notes)
        {
            var sb = new StringBuilder();
            string red = EqUi.HexOf(EqUi.RedDark), amber = EqUi.HexOf(EqUi.AmberDark);
            for (int i = 0; i < notes.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append("<color=#").Append(notes[i].mistake ? red : amber).Append(">•</color> ").Append(notes[i].text);
            }
            return sb.ToString();
        }
    }
}
