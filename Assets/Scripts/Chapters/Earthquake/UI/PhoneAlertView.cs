using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Telefon bildirimi (maket: S/eqD_mock_phone.png): arka plan kararırken telefon alttan hafif sekerek kayar ve
    /// ±1 px titrer; kilit ekranında büyük saat + tarih, kırmızı başlıklı bildirim kartı ve yeşil [BAŞLA] düğmesi.
    /// E / Enter / Space / fare ile kapanır. Arayüzü kodla kurar (Canvas sıralaması 92).
    /// Okunabilirlik (640x360 referansı): gövde 8 px, not 7 px, en küçük yazı 7 px; BAŞLA düğmesinde [E] tuş rozeti,
    /// kartın altında diğer tuşlar yazar.
    /// </summary>
    public class PhoneAlertView : PhoneAlertViewBase
    {
        [SerializeField] private EarthquakeUISkin skin;
        [SerializeField] private int sortingOrder = 92;
        [SerializeField] private string clockText = "07:00";
        [Tooltip("Boşsa bugünün tarihi Türkçe yazılır (ör. \"Cumartesi, 17 Ekim\").")]
        [SerializeField] private string dateText = "";
        [Tooltip("Kartın altındaki ikinci ipucu (asıl tuş [E] düğmenin üstünde rozet olarak durur).")]
        [SerializeField] private string startHint = "Enter, Space ya da tıklama da olur";
        [SerializeField] private float slideSeconds = 0.55f;
        [SerializeField] private float vibrateSeconds = 0.9f;

        private static readonly Vector2 PhoneSize = new Vector2(168f, 288f);
        private const float RestY = 36f;
        private const float HiddenY = -300f;
        private const float ButtonY = 91f;
        private static readonly Color DateColor = EqUi.Hex(0xA9B0CC);

        private bool built;
        private Font font;
        private Canvas canvas;
        private CanvasGroup group;
        private Image dim;
        private RectTransform phone;
        private Text appText, titleText, bodyText, noteText, buttonText, statusClock, bigClock, dateLabel, hintText;
        private RectTransform buttonKey;
        private Image buttonImage;
        private EqButton button;
        private bool clicked;
        private bool hovered;

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_PhoneCanvas", sortingOrder, out group);

            dim = EqUi.DimImage(canvas.transform, skin);

            // Telefon gövdesi (Simple, 168x288)
            var body = EqUi.Img("Phone", canvas.transform, EqUi.S(skin, s => s.PhoneFrame), EqUi.Ink, skin);
            body.preserveAspect = false;
            phone = body.rectTransform;
            EqUi.Place(phone, 0.5f, 0f, new Vector2(0f, HiddenY), PhoneSize);
            if (body.sprite == null)
            {
                body.color = EqUi.Hex(0x4A4E6A);
                var inner = EqUi.Img("Body", phone, null, EqUi.Hex(0x1B1C2A), skin);
                EqUi.Stretch(inner.rectTransform, 2f);
                var scr = EqUi.Img("Screen", phone, null, EqUi.Navy, skin);
                EqUi.TL(scr.rectTransform, 8f, 10f, 152f, 268f);
                var notch = EqUi.Img("Notch", phone, null, EqUi.Hex(0x1B1C2A), skin);
                EqUi.TL(notch.rectTransform, 61f, 10f, 46f, 8f);
                var home = EqUi.Img("Home", phone, null, EqUi.Hex(0x8A8FA8), skin);
                EqUi.TL(home.rectTransform, 65f, 270f, 38f, 2f);
                var bat = EqUi.Img("Battery", phone, null, EqUi.Green, skin);
                EqUi.TL(bat.rectTransform, 141f, 13f, 10f, 5f);
            }

            statusClock = EqUi.Txt("StatusClock", phone, font, 7, EqUi.Cream, TextAnchor.MiddleLeft);
            EqUi.TL(statusClock.rectTransform, 14f, 10f, 40f, 9f);

            bigClock = EqUi.Txt("BigClock", phone, font, 28, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.A(EqUi.Ink, 0.6f));
            EqUi.TopBand(bigClock.rectTransform, 8f, 8f, 34f, 32f);
            bigClock.horizontalOverflow = HorizontalWrapMode.Overflow;
            dateLabel = EqUi.Txt("Date", phone, font, 8, DateColor, TextAnchor.MiddleCenter);
            EqUi.TopBand(dateLabel.rectTransform, 8f, 8f, 66f, 12f);

            // Bildirim kartı (144x118, 9-slice: kırmızı başlık y1..12, krem gövde)
            //  başlık (kimden) → büyük başlık (ne) → kısa mesaj → ayraç → eğitim notu → [E] BAŞLA
            var card = EqUi.Img("NotifCard", phone, EqUi.S(skin, s => s.PhoneNotifCard), EqUi.Ink, skin);
            var cardRt = EqUi.TL(card.rectTransform, 12f, 86f, 144f, 118f);
            if (card.sprite == null)
            {
                EqUi.Fallback(card, EqUi.Ink, EqUi.Cream, skin, 1f, 12f, EqUi.Red);
                var tri = EqUi.Img("Warn", cardRt, null, EqUi.Amber, skin);
                EqUi.TL(tri.rectTransform, 5f, 3f, 8f, 8f);
            }

            appText = EqUi.Txt("App", cardRt, font, 7, EqUi.White, TextAnchor.MiddleLeft, EqUi.ShadowRed);
            EqUi.TopBand(appText.rectTransform, 17f, 4f, 1f, 11f);
            EqUi.BestFit(appText, 6);

            titleText = EqUi.Txt("Title", cardRt, font, 9, EqUi.Ink, TextAnchor.MiddleLeft);
            EqUi.TopBand(titleText.rectTransform, 6f, 6f, 15f, 13f);
            EqUi.BestFit(titleText, 8);

            bodyText = EqUi.Txt("Body", cardRt, font, 8, EqUi.Navy, TextAnchor.UpperLeft);
            EqUi.TopBand(bodyText.rectTransform, 6f, 6f, 29f, 24f);
            EqUi.BestFit(bodyText, 7);

            var sep = EqUi.Img("Separator", cardRt, null, EqUi.CreamShade, skin);
            EqUi.TopBand(sep.rectTransform, 6f, 6f, 55f, 1f);

            noteText = EqUi.Txt("Note", cardRt, font, 7, EqUi.Muted, TextAnchor.UpperLeft);
            EqUi.TopBand(noteText.rectTransform, 6f, 6f, 58f, 28f);
            EqUi.BestFit(noteText, 6);

            // [E] BAŞLA (yeşil, 84x18, kart içinde ortalı; solda tuş rozeti)
            buttonImage = EqUi.Img("StartButton", cardRt, EqUi.S(skin, s => s.ButtonStart), EqUi.Ink, skin, true);
            EqUi.Place(buttonImage.rectTransform, 0.5f, 1f, new Vector2(0f, -ButtonY), new Vector2(84f, 18f));
            EqUi.Fallback(buttonImage, EqUi.Ink, EqUi.Green, skin);
            buttonKey = EqUi.KeyCap(buttonImage.transform, "E", font, skin, 9);
            EqUi.Place(buttonKey, 0f, 0.5f, new Vector2(4f, 1f), new Vector2(15f, 14f));
            buttonText = EqUi.Txt("Label", buttonImage.transform, font, 9, EqUi.White, TextAnchor.MiddleCenter, EqUi.ShadowGreen);
            EqUi.StretchOffsets(buttonText.rectTransform, 18f, 6f, 2f, 1f);
            buttonText.horizontalOverflow = HorizontalWrapMode.Overflow;

            button = buttonImage.gameObject.AddComponent<EqButton>();
            button.Init();
            button.Clicked += () => clicked = true;
            button.HoverChanged += h => { hovered = h; RefreshButton(false); };

            hintText = EqUi.Txt("Hint", phone, font, 7, DateColor, TextAnchor.MiddleCenter);
            EqUi.TopBand(hintText.rectTransform, 8f, 8f, 210f, 11f);

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        private void RefreshButton(bool pressed)
        {
            var rt = buttonImage.rectTransform;
            if (buttonImage.sprite != null && skin != null)
            {
                Sprite s = (hovered || pressed) && skin.ButtonStartHover != null ? skin.ButtonStartHover : skin.ButtonStart;
                EqUi.Skin(buttonImage, s, EqUi.Green, skin);
            }
            else EqUi.FallbackColors(buttonImage, hovered ? EqUi.Cream : EqUi.Ink, hovered ? EqUi.Hex(0x22C46A) : EqUi.Green);
            // Basınca 1 px aşağı (dudak hissi), ölçek yok → pikseller bozulmaz
            rt.anchoredPosition = new Vector2(0f, pressed ? -ButtonY - 1f : -ButtonY);
        }

        public override IEnumerator Show(string app, string title, string body, string note, string buttonLabel)
        {
            EnsureBuilt();
            appText.text = app ?? "";
            titleText.text = title ?? "";
            bodyText.text = body ?? "";
            noteText.text = note ?? "";
            buttonText.text = string.IsNullOrEmpty(buttonLabel) ? "BAŞLA" : buttonLabel;
            statusClock.text = clockText;
            bigClock.text = clockText;
            dateLabel.text = string.IsNullOrEmpty(dateText) ? TurkishDate(DateTime.Now) : dateText;
            hintText.text = startHint;
            clicked = false;
            hovered = false;
            button.Interactable = false;
            RefreshButton(false);

            canvas.gameObject.SetActive(true);
            group.alpha = 1f;
            group.blocksRaycasts = true;

            // Kararma + alttan kayma (hafif sekme) + titreşim (±1 px)
            float total = Mathf.Max(slideSeconds, vibrateSeconds);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                EqUi.SetDim(dim, EqUi.OutCubic(t / 0.45f));
                float y = Mathf.LerpUnclamped(HiddenY, RestY, EqUi.OutBack(t / slideSeconds, 0.9f));
                float vib = t < vibrateSeconds ? Vibrate(t) : 0f;
                phone.anchoredPosition = EqUi.Snap(new Vector2(vib, y));
                yield return null;
            }
            EqUi.SetDim(dim, 1f);
            phone.anchoredPosition = new Vector2(0f, RestY);

            // Kısa ikinci titreşim (bildirim "bzzt")
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                phone.anchoredPosition = new Vector2(Vibrate(t), RestY);
                yield return null;
            }
            phone.anchoredPosition = new Vector2(0f, RestY);

            button.Interactable = true;
            int openFrame = Time.frameCount;
            float wait = 0f;
            while (true)
            {
                wait += Time.unscaledDeltaTime;
                if (clicked) break;
                if (Time.frameCount > openFrame && wait > 0.15f && (GameInput.ContinuePressed || GameInput.ConfirmPressed)) break;
                yield return null;
            }
            button.Interactable = false;

            RefreshButton(true);
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime) yield return null;
            hovered = false;
            RefreshButton(false);

            // Aşağı kayarak çık, kararma açılır
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.35f;
                phone.anchoredPosition = EqUi.Snap(new Vector2(0f, Mathf.LerpUnclamped(RestY, HiddenY, EqUi.InBack(k, 0.8f))));
                EqUi.SetDim(dim, 1f - k);
                yield return null;
            }
            group.alpha = 0f;
            group.blocksRaycasts = false;
            canvas.gameObject.SetActive(false);
        }

        // ±1 px, ~25 Hz (piksel hareketi, yanıp sönme değil)
        private static float Vibrate(float t) => ((int)(t * 50f) % 2 == 0) ? 1f : -1f;

        private static readonly string[] Days = { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
        private static readonly string[] Months = { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };

        private static string TurkishDate(DateTime d) => Days[(int)d.DayOfWeek] + ", " + d.Day + " " + Months[d.Month - 1];
    }
}
