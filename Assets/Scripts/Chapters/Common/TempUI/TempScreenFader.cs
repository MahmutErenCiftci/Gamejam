using System.Collections;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.TempUI
{
    /// <summary>
    /// GEÇİCİ ekran kararması: tam ekran siyah perde. Arayüz ekibinin gerçek FadePanel'i gelene kadar
    /// bölüm akışı fader bağlı değilse bunu otomatik oluşturur.
    /// </summary>
    public class TempScreenFader : ScreenFaderBase
    {
        private CanvasGroup group;

        private void Awake()
        {
            var canvas = new GameObject("TempFaderCanvas", typeof(Canvas), typeof(CanvasGroup)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            group = canvas.GetComponent<CanvasGroup>();

            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)image.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = Color.black;

            Apply(0f);
        }

        public override IEnumerator FadeOut(float seconds) => FadeTo(1f, seconds);

        public override IEnumerator FadeIn(float seconds) => FadeTo(0f, seconds);

        public override void SetOpaque(bool black) => Apply(black ? 1f : 0f);

        private IEnumerator FadeTo(float target, float seconds)
        {
            float start = group.alpha;
            if (seconds <= 0f || Mathf.Approximately(start, target))
            {
                Apply(target);
                yield break;
            }
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(start, target, t / seconds));
                yield return null;
            }
            Apply(target);
        }

        private void Apply(float alpha)
        {
            group.alpha = alpha;
            group.blocksRaycasts = alpha > 0.01f;
        }
    }
}
