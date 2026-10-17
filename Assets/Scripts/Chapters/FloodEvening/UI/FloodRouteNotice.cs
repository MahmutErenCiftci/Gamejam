using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// "Yol açıldı." kısa bildirimi (SEL3-30): Cenk'in biraz üstünde küçük koyu kutu, yeşil kenar + piksel tik rozeti,
    /// kısa süre görünüp kaybolur; oyunu durdurmaz, tıklama yakalamaz. Show() varsayılan metni, Show(text) verilen metni
    /// (ör. flood_evening.json) gösterir.
    /// </summary>
    public class FloodRouteNotice : FeTimedBanner
    {
        [SerializeField] private string defaultText = "Yol açıldı.";
        [SerializeField, Min(0.5f)] private float seconds = 1.8f;

        private Text label;
        private RectTransform badge;
        private float t;

        protected override Vector2 Size => new Vector2(132f, 26f);
        protected override float DefaultSeconds => seconds;

        // Varsayılan yer: ekran ortasının biraz üstü (Cenk'in başının üstü), yukarıdan iner.
        private void Reset()
        {
            sortingOrder = 63;
            anchor = new Vector2(0.5f, 0.5f);
            offset = new Vector2(0f, 52f);
            slideFrom = 6f;
        }

        public void Show() => Show(null);

        public void Show(string text, float duration = -1f)
        {
            EnsureBuilt();
            label.text = string.IsNullOrEmpty(text) ? defaultText : text;
            Open(duration > 0f ? duration : DefaultSeconds);
        }

        protected override void OnOpened() => t = 0f;

        // Rozet girişte bir kez küçük zıplar.
        protected override void Animate(float dt)
        {
            t += dt;
            float hop = t < 0.3f ? Mathf.Sin(t / 0.3f * Mathf.PI) * 3f : 0f;
            badge.anchoredPosition = new Vector2(6f, -Mathf.Round(5f - hop));
        }

        protected override void BuildContent(RectTransform root)
        {
            var frame = FeUi.Framed("Frame", root, FeUi.Green, FeUi.A(FeUi.Panel, 0.95f), false, 2f);
            FeUi.Stretch(frame.rectTransform);

            badge = FeUi.Framed("Badge", root, FeUi.Ink, FeUi.Green).rectTransform;
            FeUi.TL(badge, 6f, 5f, 16f, 16f);
            // Piksel tik (fontta ✓ olmayabilir): 2 px karelerden.
            int[,] px = { { 2, 7 }, { 4, 9 }, { 6, 7 }, { 8, 5 }, { 10, 3 } };
            for (int i = 0; i < px.GetLength(0); i++)
            {
                var p = FeUi.Img("Tick" + i, badge, FeUi.Ink).rectTransform;
                FeUi.TL(p, px[i, 0] + 1f, px[i, 1] - 1f, 2f, 2f);
            }

            label = FeUi.Txt("Text", root, font, 12, FeUi.Cream, TextAnchor.MiddleLeft);
            label.rectTransform.offsetMin = new Vector2(28f, 2f);
            label.rectTransform.offsetMax = new Vector2(-6f, -2f);
            FeUi.BestFit(label, 9);
            label.text = defaultText;
        }
    }
}
