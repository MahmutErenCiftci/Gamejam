using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Şifre paneli arayüzü (prefab: Assets/Prefabs/FloodEvening/UI/FE_CodePanelView.prefab). Mantık CodePanelMinigame'dedir;
    /// bu sınıf çizer, düğme tıklamalarını bildirir ve panelin bütün metinlerini (Inspector alanları) tutar.
    /// Rakam kutuları prefab'da 4 adettir (Slot1..Slot4); kod uzunluğu farklıysa fazla kutular gizlenir.
    /// </summary>
    public class CodePanelView : FloodPuzzleViewBase
    {
        public enum SlotState { Normal, Wrong, Success }
        public enum Tone { Info, Ok, Warn, Error, Success }

        [Header("Parçalar")]
        [SerializeField] private RectTransform display;
        [SerializeField] private Image[] slotBoxes = new Image[0];
        [SerializeField] private Text[] slotTexts = new Text[0];
        [SerializeField] private Text[] slotIndexTexts = new Text[0];
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text notesTitleText;
        [SerializeField] private Text notesBodyText;
        [SerializeField] private Text notesHelpText;
        [SerializeField] private Text keyboardHintText;
        [Tooltip("0..9 rakam düğmeleri (indeks = rakam).")]
        [SerializeField] private Button[] digitButtons = new Button[10];
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        [Header("Metinler — panel")]
        [SerializeField] private string title = "ŞİFRE PANELİ";
        [SerializeField] private string closeLabel = "Kapat (Esc)";
        [SerializeField] private string deleteLabel = "Sil";
        [SerializeField] private string confirmLabel = "Onayla";
        [SerializeField] private string emptySlot = "_";
        [Tooltip("{0} = hane numarası")]
        [SerializeField] private string slotIndexFormat = "{0}. hane";
        [SerializeField] private string keyboardHint = "Klavye: 0–9 rakam  ·  Backspace: Sil  ·  Enter: Onayla  ·  Esc: Kapat";
        [SerializeField] private string notesTitle = "OKUDUĞUN İPUÇLARI";
        [SerializeField, TextArea] private string notesEmpty = "Henüz ipucu okumadın.\nLevhalar bu bahçede, panelin yakınında.";
        [Tooltip("{0} = hane, {1} = rakam")]
        [SerializeField] private string noteLineFormat = "{0}. hane: <color=#F6C67D>{1}</color>";
        [Tooltip("{0} = okunan, {1} = toplam levha")]
        [SerializeField] private string notesCountFormat = "Okunan levha: {0}/{1}";
        [SerializeField, TextArea] private string notesHelp = "Levhayı yeniden okumak için paneli kapat, levhaya git: [E].";

        [Header("Metinler — mesajlar")]
        [SerializeField] private string msgStart = "İpucu levhalarındaki rakamları hane sırasıyla gir.";
        [Tooltip("{0} = girilen, {1} = toplam hane")]
        [SerializeField] private string msgProgress = "{0}/{1} hane girildi.";
        [SerializeField] private string msgFull = "Kod tamam: Onayla (Enter).";
        [Tooltip("{0} = toplam hane")]
        [SerializeField] private string msgOverflow = "{0} hane doldu: Onayla ya da Sil.";
        [Tooltip("{0} = toplam hane, {1} = girilen")]
        [SerializeField] private string msgTooShort = "Kod {0} haneli. Şu an {1} hane var.";
        [Tooltip("{0} = denenen kod, {1} = ipucu cümlesi")]
        [SerializeField] private string msgWrong = "Kod yanlış ({0}). {1}";
        [SerializeField] private string msgWrongUnread = "Okumadığın levha var; bu bahçede ara.";
        [SerializeField] private string msgWrongOrder = "Hane sırasını kontrol et: 1. hane en solda.";
        [SerializeField] private string msgAfterWrong = "Yeni kodu gir: önce bir rakama bas ya da Sil.";
        [SerializeField] private string msgSuccess = "Doğru kod! Giriş kapısı açılıyor...";

        public event Action<int> DigitClicked;
        public event Action DeleteClicked;
        public event Action ConfirmClicked;
        public event Action CloseClicked;

        public string MsgStart => msgStart;
        public string MsgFull => msgFull;
        public string MsgAfterWrong => msgAfterWrong;
        public string MsgSuccess => msgSuccess;
        public string Progress(int n, int len) => string.Format(msgProgress, n, len);
        public string Overflow(int len) => string.Format(msgOverflow, len);
        public string TooShort(int len, int n) => string.Format(msgTooShort, len, n);
        public string Wrong(string tried, bool allRead) => string.Format(msgWrong, tried, allRead ? msgWrongOrder : msgWrongUnread);

        private float shake;

        public override void Show()
        {
            base.Show();
            shake = 0f;
            SetText(titleText, title);
            SetText(keyboardHintText, keyboardHint);
            SetText(notesTitleText, notesTitle);
            SetText(notesHelpText, notesHelp);
            if (closeButton != null) SetText(closeButton.GetComponentInChildren<Text>(true), closeLabel);
            if (deleteButton != null) SetText(deleteButton.GetComponentInChildren<Text>(true), deleteLabel);
            if (confirmButton != null) SetText(confirmButton.GetComponentInChildren<Text>(true), confirmLabel);
            for (int i = 0; i < slotIndexTexts.Length; i++) SetText(slotIndexTexts[i], string.Format(slotIndexFormat, i + 1));
        }

        public void Shake(float seconds) => shake = Mathf.Max(shake, seconds);

        public void TickShake(float unscaledDt)
        {
            if (display == null) return;
            if (shake > 0f)
            {
                shake = Mathf.Max(0f, shake - unscaledDt);
                display.anchoredPosition = new Vector2(Mathf.Sin(shake * 70f) * 4f * Mathf.Min(1f, shake / 0.35f), display.anchoredPosition.y);
            }
            else if (display.anchoredPosition.x != 0f) display.anchoredPosition = new Vector2(0f, display.anchoredPosition.y);
        }

        public void RenderEntry(string entry, int length, SlotState state)
        {
            for (int i = 0; i < slotBoxes.Length; i++)
            {
                bool used = i < length;
                if (slotBoxes[i] != null && slotBoxes[i].gameObject.activeSelf != used) slotBoxes[i].gameObject.SetActive(used);
                if (i < slotIndexTexts.Length && slotIndexTexts[i] != null && slotIndexTexts[i].gameObject.activeSelf != used) slotIndexTexts[i].gameObject.SetActive(used);
                if (!used) continue;
                bool filled = i < entry.Length;
                if (i < slotTexts.Length && slotTexts[i] != null)
                {
                    slotTexts[i].text = filled ? entry[i].ToString() : emptySlot;
                    slotTexts[i].color = filled ? FloodPuzzleUi.Cream : FloodPuzzleUi.Muted;
                }
                Color border = state == SlotState.Success ? FloodPuzzleUi.Green
                    : state == SlotState.Wrong ? FloodPuzzleUi.Red
                    : i == entry.Length ? FloodPuzzleUi.Turquoise : FloodPuzzleUi.TurquoiseDark;
                if (slotBoxes[i] != null) FloodPuzzleUi.SetBoxColors(slotBoxes[i], border, FloodPuzzleUi.Ink);
            }
        }

        public void SetMessage(string text, Tone tone)
        {
            if (messageText == null) return;
            messageText.text = text ?? "";
            messageText.color = tone == Tone.Success ? FloodPuzzleUi.Green
                : tone == Tone.Error ? FloodPuzzleUi.Red
                : tone == Tone.Warn ? FloodPuzzleUi.Amber
                : tone == Tone.Ok ? FloodPuzzleUi.Turquoise
                : FloodPuzzleUi.Muted;
        }

        /// <summary>Okunan ipuçları (hane → rakam) ve okunan/toplam levha sayısı.</summary>
        public void SetNotes(SortedDictionary<int, char> known, int read, int total)
        {
            if (notesBodyText == null) return;
            var sb = new StringBuilder();
            if (known == null || known.Count == 0) sb.Append("<color=#9FB3C8>").Append(notesEmpty).Append("</color>");
            else foreach (var kv in known) sb.Append(string.Format(noteLineFormat, kv.Key, kv.Value)).Append('\n');
            if (total > 0) sb.Append("\n<color=#9FB3C8>").Append(string.Format(notesCountFormat, read, total)).Append("</color>");
            notesBodyText.text = sb.ToString().TrimEnd('\n');
        }

        protected override void WireButtons()
        {
            for (int d = 0; d < digitButtons.Length && d < 10; d++)
            {
                int digit = d;
                Wire(digitButtons[d], () => DigitClicked?.Invoke(digit));
            }
            Wire(deleteButton, () => DeleteClicked?.Invoke());
            Wire(confirmButton, () => ConfirmClicked?.Invoke());
            Wire(closeButton, () => CloseClicked?.Invoke());
        }

        protected override void Build(Font f, RectTransform root)
        {
            var dim = FloodPuzzleUi.Img("Dim", root, FloodPuzzleUi.Hex(0x0B1220, 0.62f), null, true);
            FloodPuzzleUi.Stretch(dim.rectTransform);

            panel = FloodPuzzleUi.Box("Panel", root, FloodPuzzleUi.Turquoise, FloodPuzzleUi.Panel, 0f, 0f, 384f, 256f, true).rectTransform;
            FloodPuzzleUi.At(FloodPuzzleUi.Img("TitleBand", panel, FloodPuzzleUi.PanelLight).rectTransform, 0f, 114f, 382f, 26f);
            titleText = FloodPuzzleUi.TxtAt("Title", panel, f, 13, FloodPuzzleUi.Turquoise, TextAnchor.MiddleLeft, -60f, 114f, 240f, 20f);
            closeButton = FloodPuzzleUi.Btn("CloseButton", panel, f, closeLabel, 9, 140f, 114f, 84f, 18f,
                FloodPuzzleUi.TurquoiseDark, FloodPuzzleUi.Ink, FloodPuzzleUi.Cream);

            const float lx = -78f; // sol sütun merkezi
            display = FloodPuzzleUi.Rect("Display", panel);
            FloodPuzzleUi.At(display, 0f, 0f, 384f, 256f);
            const int n = 4;
            slotBoxes = new Image[n];
            slotTexts = new Text[n];
            slotIndexTexts = new Text[n];
            float step = 38f, x0 = lx - step * (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                slotBoxes[i] = FloodPuzzleUi.Box("Slot" + (i + 1), display, FloodPuzzleUi.TurquoiseDark, FloodPuzzleUi.Ink, x0 + i * step, 74f, 32f, 36f);
                slotTexts[i] = FloodPuzzleUi.Txt("Digit", slotBoxes[i].transform, f, 22, FloodPuzzleUi.Cream, TextAnchor.MiddleCenter);
                slotIndexTexts[i] = FloodPuzzleUi.TxtAt("Index" + (i + 1), panel, f, 8, FloodPuzzleUi.Muted, TextAnchor.MiddleCenter, x0 + i * step, 50f, 32f, 10f);
            }

            messageText = FloodPuzzleUi.TxtAt("Message", panel, f, 9, FloodPuzzleUi.Muted, TextAnchor.MiddleCenter, lx, 30f, 200f, 26f);

            string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", deleteLabel, "0", confirmLabel };
            digitButtons = new Button[10];
            for (int i = 0; i < labels.Length; i++)
            {
                int col = i % 3, row = i / 3;
                float bx = lx + (col - 1) * 50f, by = 2f - row * 28f;
                bool confirm = i == 11, del = i == 9;
                Color border = confirm ? FloodPuzzleUi.Turquoise : FloodPuzzleUi.TurquoiseDark;
                Color fill = confirm ? FloodPuzzleUi.TurquoiseDark : FloodPuzzleUi.PanelLight;
                string goName = confirm ? "Key_Confirm" : del ? "Key_Delete" : "Key_" + labels[i];
                var b = FloodPuzzleUi.Btn(goName, panel, f, labels[i], confirm || del ? 10 : 14, bx, by, 46f, 24f, border, fill, FloodPuzzleUi.Cream);
                if (confirm) confirmButton = b;
                else if (del) deleteButton = b;
                else digitButtons[labels[i][0] - '0'] = b;
            }

            var notesBox = FloodPuzzleUi.Box("Notes", panel, FloodPuzzleUi.TurquoiseDark, FloodPuzzleUi.Ink, 104f, -2f, 156f, 176f);
            notesTitleText = FloodPuzzleUi.TxtAt("NotesTitle", notesBox.transform, f, 9, FloodPuzzleUi.Turquoise, TextAnchor.MiddleCenter, 0f, 76f, 148f, 14f);
            notesBodyText = FloodPuzzleUi.TxtAt("NotesBody", notesBox.transform, f, 10, FloodPuzzleUi.Cream, TextAnchor.UpperCenter, 0f, 6f, 144f, 118f);
            notesHelpText = FloodPuzzleUi.TxtAt("NotesHelp", notesBox.transform, f, 8, FloodPuzzleUi.Muted, TextAnchor.LowerCenter, 0f, -70f, 146f, 30f);
            keyboardHintText = FloodPuzzleUi.TxtAt("KeyboardHint", panel, f, 8, FloodPuzzleUi.Muted, TextAnchor.MiddleCenter, 0f, -116f, 370f, 12f);
        }
    }
}
