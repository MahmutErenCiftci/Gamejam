using System.Collections;
using System.Collections.Generic;
using System.Text;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>Akşam sel bölümünün sonuç verisi (FloodEveningController doldurur).</summary>
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
    }

    /// <summary>
    /// Sonuç ve öğrenme ekranı (LEAD): başlık (Eve güvenle ulaştın / süre doldu), açıklama, "Doğru kararların",
    /// "Düzeltilen yanlışlar", "Unutma" (öğrenilenler) ve iki buton: birincil + [Menüye Dön].
    /// Birincil buton başarısız ekranda [R] Tekrar Dene ("Retry"), başarı ekranında SetPrimaryAction ile
    /// "Devam: Deprem Bölümü" / "Oyunu Bitir" ("Next"; seçilince ekran kararır) olur (GameFlow).
    /// Klavye: ←/→ (A/D) seçim, Enter/Space/E onay, 1/2 kısayol (R yalnız Tekrar Dene'de); fare ile tıklanır. Canvas 110.
    /// Seçim result.choice'a yazılır; sahne geçişini controller yapar.
    /// </summary>
    public class FloodEveningResultView : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 110;
        [Tooltip("Ekran açıldıktan sonra tuşların yok sayıldığı süre (son diyaloğu kapatan tuş butona basmasın)")]
        [SerializeField] private float inputDelay = 0.6f;

        public const string ChoiceRetry = "Retry";
        public const string ChoiceMenu = "Menu";
        /// <summary>Başarı ekranının birincil butonu: sıradaki afet ya da oyun sonu (GameFlow.ContinueAfter).</summary>
        public const string ChoiceNext = "Next";
        [Tooltip("'Devam' seçilince ekranın kararma süresi (sn)")]
        [SerializeField] private float continueFadeSeconds = 0.6f;
        private string primaryChoice = ChoiceRetry;
        private Image blackout;

        private const float PanelW = 560f, PanelH = 324f;

        private bool built;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panel;
        private Image headerBand, titleBadge;
        private Text titleText, messageText, correctText, correctedText, lessonsText, noteText, badgeGlyph;
        private Text correctHeader, correctedHeader, lessonsHeader, headerText;
        private readonly Text[] buttonTexts = new Text[2];
        private readonly Image[] buttons = new Image[2];
        private readonly FeButton[] feButtons = new FeButton[2];
        private int focus;
        private int clicked = -1;

        public bool IsShown { get; private set; }

        /// <summary>Etiketler (flood_evening.json "ui": result_header, result_*_header, result_retry, result_menu).</summary>
        public void SetLabels(string header, string correct, string corrected, string lessons, string retry, string menu)
        {
            Build();
            headerText.text = header ?? "";
            correctHeader.text = correct ?? "";
            correctedHeader.text = corrected ?? "";
            lessonsHeader.text = lessons ?? "";
            buttonTexts[0].text = retry ?? "";
            buttonTexts[1].text = menu ?? "";
            primaryChoice = ChoiceRetry;
        }

        /// <summary>Birincil (sol) butonun seçimini ve metnini değiştirir (ör. ChoiceNext + "Devam: Deprem Bölümü").
        /// SetLabels birincil butonu yeniden Tekrar Dene yapar.</summary>
        public void SetPrimaryAction(string choice, string label)
        {
            Build();
            primaryChoice = string.IsNullOrEmpty(choice) ? ChoiceRetry : choice;
            if (label != null) buttonTexts[0].text = label;
        }

        private void Awake() => Build();

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FeUi.CreateCanvas(transform, "FE_ResultCanvas", sortingOrder, out group);
            var dim = FeUi.Img("Dim", canvas.transform, FeUi.A(FeUi.Ink, 0.78f), true);
            FeUi.Stretch(dim.rectTransform);

            var p = FeUi.Framed("Panel", canvas.transform, FeUi.Teal, FeUi.Panel);
            panel = p.rectTransform;
            FeUi.Place(panel, 0.5f, 0.5f, Vector2.zero, new Vector2(PanelW, PanelH));
            headerBand = FeUi.Img("Header", panel, FeUi.TealDark);
            FeUi.TL(headerBand.rectTransform, 1f, 1f, PanelW - 2f, 20f);
            var h = FeUi.Txt("HeaderText", headerBand.transform, font, 9, FeUi.Cream, TextAnchor.MiddleCenter);
            headerText = h;

            titleBadge = FeUi.Badge("TitleIcon", panel, font, FeUi.Green, "!");
            FeUi.TL(titleBadge.rectTransform, 18f, 30f, 22f, 22f);
            badgeGlyph = titleBadge.transform.Find("Glyph").GetComponent<Text>();
            badgeGlyph.fontSize = 14;
            titleText = FeUi.Txt("Title", panel, font, 15, FeUi.Green, TextAnchor.MiddleLeft);
            FeUi.TL(titleText.rectTransform, 48f, 28f, PanelW - 64f, 26f);
            FeUi.BestFit(titleText, 10);
            messageText = FeUi.Txt("Message", panel, font, 9, FeUi.Cream, TextAnchor.UpperLeft);
            FeUi.TL(messageText.rectTransform, 18f, 58f, PanelW - 36f, 26f);
            FeUi.BestFit(messageText, 7);

            var sep = FeUi.Img("Sep", panel, FeUi.A(FeUi.Teal, 0.4f));
            FeUi.TL(sep.rectTransform, 16f, 88f, PanelW - 32f, 1f);

            // Sol sütun: doğru kararlar + düzeltilen yanlışlar
            correctHeader = FeUi.Txt("CorrectHeader", panel, font, 9, FeUi.Green, TextAnchor.MiddleLeft);
            FeUi.TL(correctHeader.rectTransform, 18f, 94f, 262f, 13f);
            correctText = FeUi.Txt("Correct", panel, font, 8, FeUi.Cream, TextAnchor.UpperLeft);
            FeUi.TL(correctText.rectTransform, 22f, 109f, 258f, 82f);
            FeUi.BestFit(correctText, 6);
            correctedHeader = FeUi.Txt("CorrectedHeader", panel, font, 9, FeUi.Amber, TextAnchor.MiddleLeft);
            FeUi.TL(correctedHeader.rectTransform, 18f, 194f, 262f, 13f);
            correctedText = FeUi.Txt("Corrected", panel, font, 8, FeUi.Cream, TextAnchor.UpperLeft);
            FeUi.TL(correctedText.rectTransform, 22f, 209f, 258f, 56f);
            FeUi.BestFit(correctedText, 6);

            // Sağ sütun: öğrenilenler
            var lessonsBox = FeUi.Framed("LessonsBox", panel, FeUi.A(FeUi.Teal, 0.55f), FeUi.PanelLight);
            FeUi.TL(lessonsBox.rectTransform, 292f, 94f, 252f, 172f);
            var lh = FeUi.Txt("LessonsHeader", lessonsBox.transform, font, 9, FeUi.Teal, TextAnchor.MiddleLeft);
            FeUi.TL(lh.rectTransform, 8f, 4f, 236f, 13f);
            lessonsHeader = lh;
            lessonsText = FeUi.Txt("Lessons", lessonsBox.transform, font, 8, FeUi.Cream, TextAnchor.UpperLeft);
            FeUi.TL(lessonsText.rectTransform, 8f, 20f, 236f, 148f);
            FeUi.BestFit(lessonsText, 6);

            noteText = FeUi.Txt("Note", panel, font, 7, FeUi.Muted, TextAnchor.MiddleCenter);
            FeUi.TL(noteText.rectTransform, 16f, 270f, PanelW - 32f, 12f);
            FeUi.BestFit(noteText, 6);

            BuildButton(0, 120f, 176f);
            BuildButton(1, 312f, 128f);

            blackout = FeUi.Img("Blackout", canvas.transform, new Color(0f, 0f, 0f, 0f), true);
            FeUi.Stretch(blackout.rectTransform);
            blackout.gameObject.SetActive(false);

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        private void BuildButton(int i, float x, float w)
        {
            var img = FeUi.Framed(i == 0 ? "RetryButton" : "MenuButton", panel, FeUi.Ink, i == 0 ? FeUi.TealDark : FeUi.PanelLight, true);
            FeUi.TL(img.rectTransform, x, PanelH - 34f, w, 22f);
            var t = FeUi.Txt("Label", img.transform, font, 9, FeUi.Cream, TextAnchor.MiddleCenter);
            FeUi.Stretch(t.rectTransform, 2f);
            FeUi.BestFit(t, 7);
            buttonTexts[i] = t;
            var b = img.gameObject.AddComponent<FeButton>();
            b.Init();
            int idx = i;
            b.Clicked += () => clicked = idx;
            b.HoverChanged += hov => { if (hov) { focus = idx; Refresh(); } };
            buttons[i] = img;
            feButtons[i] = b;
        }

        private void Refresh()
        {
            for (int i = 0; i < 2; i++)
                FeUi.SetFrameColors(buttons[i], focus == i ? FeUi.Amber : FeUi.Ink, i == 0 ? FeUi.TealDark : FeUi.PanelLight);
        }

        /// <summary>Ekranı gösterir ve oyuncu seçene kadar bekler (result.choice dolar).</summary>
        public IEnumerator Show(FloodEveningResult result)
        {
            Build();
            if (result == null) result = new FloodEveningResult();
            result.choice = null;
            blackout.gameObject.SetActive(false);
            Color accent = result.success ? FeUi.Green : FeUi.Amber;
            titleText.text = result.title ?? "";
            titleText.color = accent;
            FeUi.SetFrameColors(titleBadge, FeUi.Ink, accent);
            badgeGlyph.text = result.success ? "+" : "!";
            messageText.text = result.message ?? "";
            correctText.text = Bullets(result.correct, "#4CC77A", result.emptyCorrect ?? "");
            bool anyCorrected = result.corrected.Count > 0;
            correctedHeader.gameObject.SetActive(anyCorrected);
            correctedText.gameObject.SetActive(anyCorrected);
            correctedText.text = Bullets(result.corrected, "#F6C67D", "");
            lessonsText.text = Bullets(result.lessons, "#35C6C0", "");
            noteText.text = result.note ?? "";

            focus = 0;
            clicked = -1;
            Refresh();
            for (int i = 0; i < 2; i++) feButtons[i].Interactable = false;
            canvas.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            IsShown = true;
            for (float k = 0f; k < 0.3f; k += Time.unscaledDeltaTime)
            {
                float e = FeUi.OutCubic(k / 0.3f);
                group.alpha = e;
                panel.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Lerp(-18f, 0f, e)));
                yield return null;
            }
            group.alpha = 1f;
            panel.anchoredPosition = Vector2.zero;
            float shownAt = Time.unscaledTime;
            while (Time.unscaledTime - shownAt < inputDelay) yield return null;

            for (int i = 0; i < 2; i++) feButtons[i].Interactable = true;
            int frame = Time.frameCount;
            int chosen = -1;
            while (chosen < 0)
            {
                if (clicked >= 0) chosen = clicked;
                else if (Time.frameCount > frame)
                {
                    int key = GameInput.ChoiceKeyPressed;
                    if (key == 1 || key == 2) chosen = key - 1;
                    else if (GameInput.RetryPressed && primaryChoice == ChoiceRetry) chosen = 0;
                    else if (GameInput.LeftPressed) { focus = 0; Refresh(); }
                    else if (GameInput.RightPressed) { focus = 1; Refresh(); }
                    else if (GameInput.ConfirmPressed || GameInput.InteractPressed) chosen = focus;
                }
                yield return null;
            }
            for (int i = 0; i < 2; i++) feButtons[i].Interactable = false;
            focus = chosen;
            Refresh();
            var rt = buttons[chosen].rectTransform;
            Vector2 p0 = rt.anchoredPosition;
            rt.anchoredPosition = p0 + new Vector2(0f, -1f);
            for (float k = 0f; k < 0.12f; k += Time.unscaledDeltaTime) yield return null;
            rt.anchoredPosition = p0;
            result.choice = chosen == 0 ? primaryChoice : ChoiceMenu;
            if (result.choice == ChoiceNext)
            {
                // Menüye dönmeden kısa kararma; sıradaki afet kendi giriş kartıyla açılır.
                blackout.gameObject.SetActive(true);
                blackout.transform.SetAsLastSibling();
                float dur = Mathf.Max(0.05f, continueFadeSeconds);
                for (float k = 0f; k < dur; k += Time.unscaledDeltaTime)
                {
                    blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(k / dur));
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

        private static string Bullets(List<string> items, string hex, string empty)
        {
            if (items == null || items.Count == 0) return empty;
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append("<color=").Append(hex).Append(">•</color> ").Append(items[i]);
            }
            return sb.ToString();
        }
    }
}
