using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Bölüm giriş kartı (LEAD): siyah ekranda "SEL BÖLÜMÜ" + "Başka bir gün — şiddetli yağışlı bir akşam".
    /// Depremden sonra da menüden de aynı kartla açılır; karakterin hasarlı evine döndüğü ya da depremin seli
    /// tetiklediği izlenimini vermez. En az minSeconds sonra E/Space/Enter/tık ile geçilebilir, holdSeconds sonra kendiliğinden geçer.
    /// Canvas 120 (ekran perdesinin üstünde).
    /// </summary>
    public class FloodEveningTitleCard : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 120;
        [SerializeField] private float holdSeconds = 3.2f;
        [SerializeField] private float minSeconds = 0.9f;

        private bool built;
        private CanvasGroup group, textGroup;
        private RectTransform line;
        private Canvas canvas;
        private Text titleText, subtitleText, footText;

        private void Awake() => Build();

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FeUi.CreateCanvas(transform, "FE_TitleCardCanvas", sortingOrder, out group);
            var black = FeUi.Img("Black", canvas.transform, Color.black, true);
            FeUi.Stretch(black.rectTransform);
            var textRoot = FeUi.Rect("Text", canvas.transform);
            FeUi.Stretch(textRoot);
            textGroup = textRoot.gameObject.AddComponent<CanvasGroup>();

            var t = FeUi.Txt("Title", textRoot, font, 30, FeUi.Cream, TextAnchor.MiddleCenter);
            FeUi.Place(t.rectTransform, 0.5f, 0.5f, new Vector2(0f, 22f), new Vector2(560f, 44f));
            titleText = t;
            line = FeUi.Img("Line", textRoot, FeUi.Teal).rectTransform;
            FeUi.Place(line, 0.5f, 0.5f, new Vector2(0f, 0f), new Vector2(180f, 2f));
            var s = FeUi.Txt("Subtitle", textRoot, font, 12, FeUi.Teal, TextAnchor.MiddleCenter);
            FeUi.Place(s.rectTransform, 0.5f, 0.5f, new Vector2(0f, -20f), new Vector2(560f, 22f));
            FeUi.BestFit(s, 9);
            subtitleText = s;
            var f = FeUi.Txt("Footnote", textRoot, font, 7, FeUi.Muted, TextAnchor.MiddleCenter);
            FeUi.Place(f.rectTransform, 0.5f, 0f, new Vector2(0f, 14f), new Vector2(560f, 12f));
            footText = f;

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        /// <summary>Kartı siyah üstünde oynatır; döndüğünde kart kaybolmuştur ve arkadaki ekran perdesi (fader) siyahtır.</summary>
        public IEnumerator Play(string title, string subtitle, string footnote, bool skipInput = true)
        {
            Build();
            titleText.text = title ?? "";
            subtitleText.text = subtitle ?? "";
            footText.text = footnote ?? "";
            // Sahne yüklenirken ilk karelerin uzun süresi kartı atlatmasın: birkaç sabit kare bekle, kare süresini sınırla.
            for (int i = 0; i < 3; i++) yield return null;
            canvas.gameObject.SetActive(true);
            group.alpha = 1f;
            textGroup.alpha = 0f;
            for (float k = 0f; k < 0.6f; k += Dt)
            {
                textGroup.alpha = FeUi.OutCubic(k / 0.6f);
                line.sizeDelta = new Vector2(Mathf.Lerp(20f, 180f, FeUi.OutCubic(k / 0.6f)), 2f);
                yield return null;
            }
            textGroup.alpha = 1f;
            int startFrame = Time.frameCount;
            for (float k = 0.6f; k < holdSeconds; k += Dt)
            {
                if (skipInput && k >= minSeconds && Time.frameCount > startFrame && GameInput.ContinuePressed) break;
                yield return null;
            }
            for (float k = 0f; k < 0.5f; k += Dt)
            {
                textGroup.alpha = 1f - k / 0.5f;
                yield return null;
            }
            yield return null; // kartı geçen tuş bir sonraki adımı tetiklemesin
        }

        private static float Dt => Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        /// <summary>Kartın siyah zeminini de kaldırır (arkada fader siyah kalmalı).</summary>
        public void Hide()
        {
            if (!built) return;
            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }
    }
}
