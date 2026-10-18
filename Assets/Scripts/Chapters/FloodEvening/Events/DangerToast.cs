using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Engellemeyen tehlike uyarısı: üst ortada koyu panel, kırmızı kenar + şerit, tehlike ikonu, başlık ve kısa açıklama
    /// (renk tek başına bilgi taşımaz: ikon + yazı da var). Arayüzü kodla kurar (Canvas sıralaması 45).
    /// </summary>
    public class DangerToast : MonoBehaviour
    {
        [SerializeField] private Font font;
        [Tooltip("Tehlike ikonu (Assets/Art/UI/Earthquake/UI_EQ_Icon_Hazard.png). Boşsa '!' rozeti çizilir.")]
        [SerializeField] private Sprite icon;
        [SerializeField] private int sortingOrder = 45;
        [SerializeField] private float topMargin = 30f;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panel;
        private Text titleText, bodyText;
        private Coroutine routine;

        public bool IsVisible => group != null && group.alpha > 0.01f;

        /// <summary>Uyarıyı seconds saniye gösterir (gerçek zaman). Üst üste çağrılırsa süre yenilenir.</summary>
        public void Show(string title, string body, float seconds)
        {
            EnsureBuilt();
            titleText.text = title ?? "";
            bodyText.text = body ?? "";
            if (routine != null) StopCoroutine(routine);
            if (!isActiveAndEnabled) return;
            routine = StartCoroutine(Run(Mathf.Max(0.5f, seconds)));
        }

        public void Hide()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (group != null) group.alpha = 0f;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void OnDisable() => Hide();

        private IEnumerator Run(float seconds)
        {
            canvas.gameObject.SetActive(true);
            for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                float k = EventsUi.Smooth01(t / 0.18f);
                group.alpha = k;
                panel.anchoredPosition = new Vector2(0f, -topMargin + Mathf.Round((1f - k) * 10f));
                yield return null;
            }
            group.alpha = 1f;
            panel.anchoredPosition = new Vector2(0f, -topMargin);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / 0.25f;
                yield return null;
            }
            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
            routine = null;
        }

        private void EnsureBuilt()
        {
            if (canvas != null) return;
            var f = EventsUi.ResolveFont(font);
            canvas = EventsUi.CreateCanvas(this, "FE_DangerToastCanvas", sortingOrder, out group);

            var p = EventsUi.Panel("DangerToast", canvas.transform, EventsUi.Danger, EventsUi.Danger);
            panel = EventsUi.Place(p.rectTransform, 0.5f, 1f, new Vector2(0f, -topMargin), new Vector2(250f, 42f));

            var badge = EventsUi.Img("IconBack", panel, EventsUi.DangerDark);
            EventsUi.TL(badge.rectTransform, 6f, 8f, 28f, 28f);
            if (icon != null)
            {
                var ic = EventsUi.Img("Icon", badge.transform, Color.white, icon);
                EventsUi.Stretch(ic.rectTransform, 4f);
            }
            else
            {
                var ex = EventsUi.Txt("Icon", badge.transform, f, 18, EventsUi.Danger, TextAnchor.MiddleCenter);
                EventsUi.Stretch(ex.rectTransform);
                ex.text = "!";
            }

            titleText = EventsUi.Txt("Title", panel, f, 10, EventsUi.Danger, TextAnchor.MiddleLeft);
            EventsUi.TL(titleText.rectTransform, 40f, 6f, 204f, 13f);
            bodyText = EventsUi.Txt("Body", panel, f, 8, EventsUi.Cream, TextAnchor.UpperLeft);
            EventsUi.TL(bodyText.rectTransform, 40f, 20f, 204f, 20f);

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }
    }
}
