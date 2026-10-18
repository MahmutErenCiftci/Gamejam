using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// "Yol açıldı." kısa bildirimi (SEL3-30): Cenk'in biraz üstünde deprem görev toast'u (yeşil, UI_EQ_QuestToast) + tik
    /// rozeti (UI_EQ_QuestDot_Done). Yukarıdan sekerek düşer, oturunca etrafına piksel yıldızlar saçılır ve rozet zıplar;
    /// kısa süre sonra solar. Oyunu durdurmaz, tıklama yakalamaz. Show() varsayılan metni, Show(text) verilen metni gösterir.
    /// </summary>
    public class FloodRouteNotice : FeTimedBanner
    {
        [SerializeField] private string defaultText = "Yol açıldı.";
        [SerializeField, Min(0.5f)] private float seconds = 1.8f;

        [Header("Sprite'lar (Assets/Art/UI/Earthquake/QuestHUD)")]
        [SerializeField] private Sprite toastSprite;
        [SerializeField] private Sprite iconSprite;

        private Text label;
        private RectTransform badge;
        private FePixelBurst burst;
        private bool burstDone;

        protected override Vector2 Size => new Vector2(94f, 22f);
        protected override float DefaultSeconds => seconds;

        // Varsayılan yer: ekran ortasının biraz üstü (Cenk'in başının üstü), yukarıdan düşer.
        private void Reset()
        {
            sortingOrder = 63;
            anchor = new Vector2(0.5f, 0.5f);
            offset = new Vector2(0f, 36f);
            slideFrom = 16f;
        }

        public void Show() => Show(null);

        public void Show(string text, float duration = -1f)
        {
            EnsureBuilt();
            label.SetPixelText(string.IsNullOrEmpty(text) ? defaultText : text);
            Open(duration > 0f ? duration : DefaultSeconds);
        }

        protected override float EntryEase(float t) => FePixel.OutBounce(t);

        protected override void OnOpened()
        {
            burstDone = false;
            burst?.Clear();
        }

        protected override void Animate(float dt)
        {
            // İlk yere değişte (≈0,13 sn) yıldızlar iki yana saçılır.
            if (!burstDone && Age > 0.13f && burst != null)
            {
                burstDone = true;
                var colors = new[] { FePixel.Gold, Color.white, FePixel.GreenLight };
                float half = Root.rect.width * 0.5f;
                burst.Emit(new Vector2(-half, 0f), 4, FePixel.Star, colors, Vector2.left, 50f, 50f, 100f, 0.6f);
                burst.Emit(new Vector2(half, 0f), 4, FePixel.Star, colors, Vector2.right, 50f, 50f, 100f, 0.6f);
                burst.Emit(new Vector2(0f, 10f), 4, FePixel.Spark, colors, Vector2.up, 60f, 50f, 90f, 0.5f);
            }
            burst?.Tick(dt);

            // Rozet oturduktan sonra iki kez zıplar.
            float t = Age - 0.15f;
            float hop = t > 0f && t < 0.5f ? Mathf.Abs(Mathf.Sin(t / 0.25f * Mathf.PI)) * 2f * (1f - t / 0.5f) : 0f;
            badge.anchoredPosition = new Vector2(6f, -Mathf.Round(6f - hop));
        }

        protected override void BuildContent(RectTransform root)
        {
            var frame = toastSprite != null
                ? FePixel.Sliced("Frame", root, toastSprite)
                : FeUi.Framed("Frame", root, FePixel.Ink, FePixel.Green);
            FeUi.Stretch(frame.rectTransform);

            var icon = iconSprite != null ? FePixel.Icon("Badge", root, iconSprite) : FePixel.Icon("Badge", root, FePixel.Star, FePixel.Cream);
            badge = icon.rectTransform;
            FeUi.TL(badge, 6f, 6f, badge.sizeDelta.x, badge.sizeDelta.y);

            label = FePixel.Txt("Text", root, font, 10, FePixel.Cream, TextAnchor.MiddleLeft, FePixel.ShadowGreen);
            label.rectTransform.offsetMin = new Vector2(20f, 1f);
            label.rectTransform.offsetMax = new Vector2(-6f, -1f);
            FeUi.BestFit(label, 8);
            label.SetPixelText(defaultText);

            burst = new FePixelBurst(root, "Stars", -120f);
        }
    }
}
