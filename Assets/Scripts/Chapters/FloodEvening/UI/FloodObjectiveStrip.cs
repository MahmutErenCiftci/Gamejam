using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Görev şeridi (SEL3-30): üst ortada deprem görev banner'ı (amber kenarlı lacivert, UI_EQ_QuestBanner), solda amber
    /// "GÖREV" plakası (UI_EQ_NamePlate), görev metni ("Güvenli güzergâhı takip et.") ve sağda ileri oku. Taşarak iner,
    /// plaka soldan kayar, üstünden bir kez ışık geçer, ok "devam et" der gibi sallanır. Yeni görev başlarken birkaç saniye
    /// görünür (seconds &lt;= 0: Hide'a kadar kalır).
    /// </summary>
    public class FloodObjectiveStrip : FeTimedBanner
    {
        [SerializeField] private string tagLabel = "GÖREV";
        [SerializeField] private string defaultText = "Güvenli güzergâhı takip et.";
        [Tooltip("0 veya altı: Hide çağrılana kadar ekranda kalır.")]
        [SerializeField] private float seconds = 4f;

        [Header("Sprite'lar (Assets/Art/UI/Earthquake)")]
        [SerializeField] private Sprite bannerSprite;
        [SerializeField] private Sprite tagSprite;

        private const float TagW = 46f;

        private Text label;
        private RectTransform tagRect, arrow, shine;

        protected override Vector2 Size => new Vector2(226f, 22f);
        protected override float DefaultSeconds => seconds;

        private void Reset()
        {
            sortingOrder = 61;
            anchor = new Vector2(0.5f, 1f);
            offset = new Vector2(0f, -8f);
            slideFrom = 14f;
        }

        public void Show() => Show(null);

        /// <summary>Görev metnini gösterir (boşsa varsayılan). duration verilmezse Inspector süresi.</summary>
        public void Show(string text, float duration = float.NaN)
        {
            EnsureBuilt();
            label.SetPixelText(string.IsNullOrEmpty(text) ? defaultText : text);
            Open(float.IsNaN(duration) ? DefaultSeconds : duration);
        }

        protected override void Animate(float dt)
        {
            // Plaka 0,1 sn gecikmeyle soldan taşarak gelir.
            float k = FePixel.OutBack((Age - 0.1f) / 0.3f);
            tagRect.anchoredPosition = new Vector2(Mathf.Round(3f - (1f - k) * 10f), 0f);

            // Işık: 0,35–0,85 sn arası soldan sağa bir kez geçer.
            float s = (Age - 0.35f) / 0.5f;
            shine.gameObject.SetActive(s > 0f && s < 1f);
            shine.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(-8f, Root.rect.width, s)), 0f);

            // Ok: 2 px ileri geri.
            arrow.anchoredPosition = new Vector2(-6f + (Mathf.Repeat(Age, 0.6f) < 0.3f ? 0f : 2f), 0f);
        }

        protected override void BuildContent(RectTransform root)
        {
            var frame = bannerSprite != null
                ? FePixel.Sliced("Frame", root, bannerSprite)
                : FeUi.Framed("Frame", root, FePixel.Amber, FePixel.Navy, false, 2f);
            FeUi.Stretch(frame.rectTransform);

            // Işık sütunu çerçevenin içinde kırpılır.
            var clip = FeUi.Rect("ShineClip", root);
            FeUi.Stretch(clip, 2f);
            clip.gameObject.AddComponent<RectMask2D>();
            shine = FeUi.Img("Shine", clip, FeUi.A(Color.white, 0.3f)).rectTransform;
            shine.anchorMin = new Vector2(0f, 0f); shine.anchorMax = new Vector2(0f, 1f); shine.pivot = new Vector2(0f, 0.5f);
            shine.sizeDelta = new Vector2(4f, 0f);

            var tag = tagSprite != null ? FePixel.Sliced("Tag", root, tagSprite) : FeUi.Img("Tag", root, FePixel.Amber);
            tagRect = tag.rectTransform;
            tagRect.anchorMin = new Vector2(0f, 0.5f); tagRect.anchorMax = new Vector2(0f, 0.5f); tagRect.pivot = new Vector2(0f, 0.5f);
            tagRect.sizeDelta = new Vector2(TagW, 16f);
            tagRect.anchoredPosition = new Vector2(3f, 0f);
            var tagText = FePixel.Txt("Label", tag.transform, font, 8, FePixel.Ink, TextAnchor.MiddleCenter, FeUi.A(Color.white, 0.35f));
            tagText.rectTransform.offsetMin = new Vector2(0f, 1f);
            tagText.SetPixelText(tagLabel);

            label = FePixel.Txt("Text", root, font, 10, FePixel.Cream, TextAnchor.MiddleLeft, FePixel.Ink);
            label.rectTransform.offsetMin = new Vector2(TagW + 9f, 1f);
            label.rectTransform.offsetMax = new Vector2(-16f, -1f);
            FeUi.BestFit(label, 8);
            label.SetPixelText(defaultText);

            arrow = FePixel.Icon("Arrow", root, FePixel.Arrow).rectTransform;
            arrow.anchorMin = arrow.anchorMax = new Vector2(1f, 0.5f);
            arrow.pivot = new Vector2(1f, 0.5f);
            arrow.anchoredPosition = new Vector2(-6f, 0f);
        }
    }
}
