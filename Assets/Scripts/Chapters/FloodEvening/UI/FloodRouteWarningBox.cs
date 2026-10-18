using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Kısa yol uyarı kutusu (SEL3-30): deprem telefon uyarı kartı (UI_EQ_PhoneNotifCard: kırmızı başlık bandı + tehlike
    /// ikonu, krem gövde). Başlıkta "YOL KAPALI", gövdede Cenk'in iç sesi ("Yol kapanmış. Kavşağa dönüp diğer yolu
    /// kullanmalıyım."). Girişte taşarak iner ve "dur!" der gibi yatay sarsılır, başlık bandı üç kez parlar. Renk tek başına
    /// bilgi taşımaz: ikon + başlık + metin birlikte. Oyunu durdurmaz, tıklama yakalamaz.
    /// Show(title, body, seconds) imzası DangerToast ile aynıdır; bariyer bileşeni hangisini kullanacağını seçebilir.
    /// </summary>
    public class FloodRouteWarningBox : FeTimedBanner
    {
        [SerializeField] private string defaultTitle = "YOL KAPALI";
        [SerializeField, TextArea] private string defaultBody = "Yol kapanmış. Kavşağa dönüp diğer yolu kullanmalıyım.";
        [SerializeField, Min(0.5f)] private float seconds = 3.5f;

        [Header("Sprite (Assets/Art/UI/Earthquake/UI_EQ_PhoneNotifCard)")]
        [SerializeField] private Sprite cardSprite;

        private const float HeaderH = 15f;

        private Text titleText, bodyText;
        private Image headerFlash;

        protected override Vector2 Size => new Vector2(220f, 50f);
        protected override float DefaultSeconds => seconds;

        // Görev şeridinin (üst orta, 22 px) altında kalır.
        private void Reset()
        {
            sortingOrder = 64;
            anchor = new Vector2(0.5f, 1f);
            offset = new Vector2(0f, -34f);
            slideFrom = 12f;
        }

        public void Show() => Show(null, null, -1f);

        /// <summary>Boş başlık/metin varsayılanı kullanır; seconds &lt;= 0 ise Inspector süresi.</summary>
        public void Show(string title, string body, float duration)
        {
            EnsureBuilt();
            titleText.SetPixelText(string.IsNullOrEmpty(title) ? defaultTitle : title);
            bodyText.SetPixelText(string.IsNullOrEmpty(body) ? defaultBody : body);
            Open(duration > 0f ? duration : DefaultSeconds);
        }

        protected override void Animate(float dt)
        {
            // Oturduktan sonra 0,35 sn yatay sarsıntı (±3 px).
            Jolt = new Vector2(FePixel.Shake(Age - 0.3f < 0f ? 99f : Age - 0.3f, 0.35f, 3f, 45f), 0f);

            // Başlık bandı ilk 0,9 sn'de üç kez parlar, başlık yazısı krem ↔ altın.
            bool on = Age < 0.9f && Mathf.Repeat(Age, 0.3f) < 0.15f;
            headerFlash.color = FeUi.A(Color.white, on ? 0.28f : 0f);
            titleText.color = on ? FePixel.Gold : FePixel.Cream;
        }

        protected override void BuildContent(RectTransform root)
        {
            var card = cardSprite != null
                ? FePixel.Sliced("Card", root, cardSprite)
                : FeUi.Framed("Card", root, FePixel.Ink, FePixel.Cream);
            FeUi.Stretch(card.rectTransform);

            // Başlık bandının üstünde parlama katmanı (kartın iç kenarlarına kadar).
            headerFlash = FeUi.Img("HeaderFlash", root, FeUi.A(Color.white, 0f));
            var hf = headerFlash.rectTransform;
            hf.anchorMin = new Vector2(0f, 1f); hf.anchorMax = new Vector2(1f, 1f); hf.pivot = new Vector2(0.5f, 1f);
            hf.offsetMin = new Vector2(2f, -(HeaderH - 1f)); hf.offsetMax = new Vector2(-2f, -2f);

            titleText = FePixel.Txt("Title", root, font, 10, FePixel.Cream, TextAnchor.MiddleLeft, FePixel.ShadowRed);
            var trt = titleText.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0f, 1f);
            trt.offsetMin = new Vector2(19f, -HeaderH); trt.offsetMax = new Vector2(-6f, -2f);
            FeUi.BestFit(titleText, 8);
            titleText.SetPixelText(defaultTitle);

            bodyText = FePixel.Txt("Body", root, font, 10, FePixel.Ink, TextAnchor.MiddleLeft, FeUi.A(Color.white, 0.5f));
            var brt = bodyText.rectTransform;
            brt.offsetMin = new Vector2(7f, 6f); brt.offsetMax = new Vector2(-7f, -(HeaderH + 2f));
            FeUi.BestFit(bodyText, 8);
            bodyText.SetPixelText(defaultBody);
        }
    }
}
