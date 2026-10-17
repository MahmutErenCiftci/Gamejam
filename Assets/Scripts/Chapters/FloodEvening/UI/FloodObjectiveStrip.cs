using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Görev şeridi (SEL3-30): üst ortada ince koyu şerit, solda amber "GÖREV" etiketi, yanında görev metni
    /// ("Güvenli güzergâhı takip et."). Yeni görev başlarken birkaç saniye görünür (seconds &lt;= 0: Hide'a kadar kalır).
    /// Sol üstteki görev paneliyle (ObjectiveHudView) aynı palet: lacivert zemin, amber vurgu, krem yazı.
    /// </summary>
    public class FloodObjectiveStrip : FeTimedBanner
    {
        [SerializeField] private string tagLabel = "GÖREV";
        [SerializeField] private string defaultText = "Güvenli güzergâhı takip et.";
        [Tooltip("0 veya altı: Hide çağrılana kadar ekranda kalır.")]
        [SerializeField] private float seconds = 4f;

        private Text label;
        private RectTransform tagRect;
        private float t;

        protected override Vector2 Size => new Vector2(252f, 24f);
        protected override float DefaultSeconds => seconds;

        private void Reset()
        {
            sortingOrder = 61;
            anchor = new Vector2(0.5f, 1f);
            offset = new Vector2(0f, -8f);
            slideFrom = 10f;
        }

        public void Show() => Show(null);

        /// <summary>Görev metnini gösterir (boşsa varsayılan). duration verilmezse Inspector süresi.</summary>
        public void Show(string text, float duration = float.NaN)
        {
            EnsureBuilt();
            label.text = string.IsNullOrEmpty(text) ? defaultText : text;
            Open(float.IsNaN(duration) ? DefaultSeconds : duration);
        }

        protected override void OnOpened() => t = 0f;

        // "GÖREV" etiketi girişte soldan kayar.
        protected override void Animate(float dt)
        {
            t += dt;
            float k = FeUi.OutCubic(t / 0.3f);
            tagRect.anchoredPosition = new Vector2(Mathf.Round(3f - (1f - k) * 6f), 0f);
        }

        protected override void BuildContent(RectTransform root)
        {
            var frame = FeUi.Framed("Frame", root, FeUi.Hex(0x2F6A8C), FeUi.A(FeUi.Ink, 0.92f), false, 2f);
            FeUi.Stretch(frame.rectTransform);

            var tag = FeUi.Img("Tag", root, FeUi.Amber);
            tagRect = tag.rectTransform;
            tagRect.anchorMin = new Vector2(0f, 0f); tagRect.anchorMax = new Vector2(0f, 1f); tagRect.pivot = new Vector2(0f, 0.5f);
            tagRect.sizeDelta = new Vector2(54f, -6f);
            tagRect.anchoredPosition = new Vector2(3f, 0f);
            var tagText = FeUi.Txt("Label", tag.transform, font, 11, FeUi.Ink, TextAnchor.MiddleCenter, false);
            FeUi.BestFit(tagText, 8);
            tagText.text = tagLabel;

            label = FeUi.Txt("Text", root, font, 12, FeUi.Cream, TextAnchor.MiddleLeft);
            label.rectTransform.offsetMin = new Vector2(63f, 2f);
            label.rectTransform.offsetMax = new Vector2(-6f, -2f);
            FeUi.BestFit(label, 9);
            label.text = defaultText;
        }
    }
}
