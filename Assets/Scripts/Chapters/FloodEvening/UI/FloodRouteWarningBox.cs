using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Kısa yol uyarı kutusu (SEL3-30): üst ortada koyu panel, üstte kırmızı-krem bariyer şeridi, zıplayan "!" rozeti,
    /// kırmızı başlık ve Cenk'in iç sesi ("Yol kapanmış. Kavşağa dönüp diğer yolu kullanmalıyım."). Renk tek başına bilgi
    /// taşımaz: ikon + başlık + metin birlikte. Oyunu durdurmaz, tıklama yakalamaz.
    /// Show(title, body, seconds) imzası DangerToast ile aynıdır; bariyer bileşeni hangisini kullanacağını seçebilir.
    /// </summary>
    public class FloodRouteWarningBox : FeTimedBanner
    {
        [SerializeField] private string defaultTitle = "YOL KAPALI";
        [SerializeField, TextArea] private string defaultBody = "Yol kapanmış. Kavşağa dönüp diğer yolu kullanmalıyım.";
        [SerializeField, Min(0.5f)] private float seconds = 3.5f;

        private Text titleText, bodyText;
        private RectTransform badge;
        private float t;

        protected override Vector2 Size => new Vector2(276f, 64f);
        protected override float DefaultSeconds => seconds;

        // Görev şeridinin (üst orta, 22 px) altında kalır.
        private void Reset()
        {
            sortingOrder = 64;
            anchor = new Vector2(0.5f, 1f);
            offset = new Vector2(0f, -36f);
            slideFrom = 10f;
        }

        public void Show() => Show(null, null, -1f);

        /// <summary>Boş başlık/metin varsayılanı kullanır; seconds &lt;= 0 ise Inspector süresi.</summary>
        public void Show(string title, string body, float duration)
        {
            EnsureBuilt();
            titleText.text = string.IsNullOrEmpty(title) ? defaultTitle : title;
            bodyText.text = string.IsNullOrEmpty(body) ? defaultBody : body;
            Open(duration > 0f ? duration : DefaultSeconds);
        }

        protected override void OnOpened() => t = 0f;

        // "!" rozeti ilk saniyede üç kez zıplar, sonra durur (göz yormasın).
        protected override void Animate(float dt)
        {
            t += dt;
            float hop = t < 1.05f ? Mathf.Abs(Mathf.Sin(t / 0.35f * Mathf.PI)) * 3f : 0f;
            badge.anchoredPosition = new Vector2(8f, -Mathf.Round(14f - hop));
        }

        protected override void BuildContent(RectTransform root)
        {
            var frame = FeUi.Framed("Frame", root, FeUi.Red, FeUi.A(FeUi.Panel, 0.95f), false, 2f);
            FeUi.Stretch(frame.rectTransform);

            // Bariyer şeridi: 4 px yüksek, 8 px'lik kırmızı/krem bloklar (kısa yoldaki bariyerle aynı dil).
            var stripe = FeUi.Rect("Stripe", root);
            stripe.anchorMin = new Vector2(0f, 1f); stripe.anchorMax = new Vector2(1f, 1f); stripe.pivot = new Vector2(0.5f, 1f);
            stripe.offsetMin = new Vector2(2f, -6f); stripe.offsetMax = new Vector2(-2f, -2f);
            stripe.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < 40; i++)
            {
                var b = FeUi.Img("S" + i, stripe, i % 2 == 0 ? FeUi.Red : FeUi.Cream).rectTransform;
                b.anchorMin = b.anchorMax = new Vector2(0f, 0.5f);
                b.pivot = new Vector2(0f, 0.5f);
                b.sizeDelta = new Vector2(8f, 4f);
                b.anchoredPosition = new Vector2(i * 8f, 0f);
            }

            badge = FeUi.Badge("Icon", root, font, FeUi.Red).rectTransform;
            FeUi.TL(badge, 8f, 14f, 20f, 20f);
            var glyph = badge.Find("Glyph").GetComponent<Text>();
            glyph.fontSize = 14;
            glyph.color = FeUi.Cream;

            titleText = FeUi.Txt("Title", root, font, 12, FeUi.Red, TextAnchor.MiddleLeft);
            FeUi.TL(titleText.rectTransform, 36f, 9f, Size.x - 44f, 15f);
            Grow(titleText.rectTransform);
            FeUi.BestFit(titleText, 9);
            titleText.text = defaultTitle;

            bodyText = FeUi.Txt("Body", root, font, 12, FeUi.Cream, TextAnchor.UpperLeft);
            FeUi.TL(bodyText.rectTransform, 36f, 24f, Size.x - 44f, 37f);
            Grow(bodyText.rectTransform);
            FeUi.BestFit(bodyText, 9);
            bodyText.text = defaultBody;
        }

        // Kutu dar ekranda kısılınca metin alanı da kısılsın (sağ kenara 8 px pay).
        private static void Grow(RectTransform rt)
        {
            float x = rt.anchoredPosition.x, y = rt.anchoredPosition.y, h = rt.sizeDelta.y;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(x, y - h);
            rt.offsetMax = new Vector2(-8f, y);
        }
    }
}
