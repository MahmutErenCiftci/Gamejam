using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel bölümünün HUD'u (LEAD): sağ kenarda "SEL DURUMU" göstergesi (FloodGaugeView: 120 sn sel sayacı, süre + yüzde)
    /// ve üst ortada ikon + başlık + açıklamalı kısa uyarı şeritleri (aşamalı uyarılar, kritik uyarı, bariyer mesajı yedeği).
    /// Gösterge ortak ilerlemeyi (FloodEveningHub.Progress01) gösterir. Arayüz kodla kurulur (şeritler Canvas 60, gösterge 61).
    /// </summary>
    public class FloodEveningHud : MonoBehaviour
    {
        public enum ToastKind { Info, Warning, Danger }

        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 60;
        [Tooltip("SEL DURUMU göstergesi (boşsa bu objeye eklenir)")]
        [SerializeField] private FloodGaugeView gauge;

        private class Toast { public string title, body; public ToastKind kind; public float seconds; }

        private bool built;
        private Canvas canvas;
        private RectTransform toastRoot;
        private CanvasGroup toastGroup;
        private Image toastFrame, toastBadge;
        private Text toastTitle, toastBody, toastGlyph;
        private readonly Queue<Toast> toasts = new Queue<Toast>();
        private Coroutine toastRoutine;
        private Vector2 toastAnchor = new Vector2(-1f, -1f), toastPos;
        // Şerit 280 birim: sol üstteki görev paneliyle (640x360 tabanında x 8..176) çakışmasın diye gerekirse sağa kayar
        private static readonly Vector2 ToastSize = new Vector2(280f, 44f);
        private const float ToastMinLeft = 184f;

        /// <summary>Şu an bir uyarı şeridi ekranda ya da sırada mı.</summary>
        public bool IsToastShowing => toastRoutine != null;

        private void Awake() => Build();

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FeUi.CreateCanvas(transform, "FE_HudCanvas", sortingOrder, out var group);
            group.blocksRaycasts = false;
            group.interactable = false;

            if (gauge == null) gauge = GetComponent<FloodGaugeView>();
            if (gauge == null) gauge = gameObject.AddComponent<FloodGaugeView>();

            // --- Uyarı şeridi (üst orta) ---
            toastRoot = FeUi.Rect("Toast", canvas.transform);
            FeUi.Place(toastRoot, 0.5f, 1f, new Vector2(0f, -8f), ToastSize);
            toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
            toastFrame = FeUi.Framed("Frame", toastRoot, FeUi.Teal, FeUi.A(FeUi.Panel, 0.95f));
            FeUi.Stretch(toastFrame.rectTransform);
            toastBadge = FeUi.Badge("Icon", toastRoot, font, FeUi.Amber);
            FeUi.TL(toastBadge.rectTransform, 8f, 13f, 18f, 18f);
            toastGlyph = toastBadge.transform.Find("Glyph").GetComponent<Text>();
            toastGlyph.fontSize = 13;
            toastTitle = FeUi.Txt("Title", toastRoot, font, 9, FeUi.Amber, TextAnchor.UpperLeft);
            FeUi.TL(toastTitle.rectTransform, 32f, 5f, ToastSize.x - 38f, 12f);
            FeUi.BestFit(toastTitle, 7);
            toastBody = FeUi.Txt("Body", toastRoot, font, 8, FeUi.Cream, TextAnchor.UpperLeft);
            FeUi.TL(toastBody.rectTransform, 32f, 18f, ToastSize.x - 38f, 24f);
            FeUi.BestFit(toastBody, 6);
            toastGroup.alpha = 0f;
        }

        /// <summary>Eski gösterge metinleri (SEL DURUMU göstergesi kendi başlığını kullanır; uyumluluk için durur).</summary>
        public void SetGaugeTexts(string title, string note) => Build();

        /// <summary>SEL DURUMU göstergesini açar/kapatır (uyarı okunup sayaç başlayınca açılır).</summary>
        public void SetGaugeVisible(bool visible) { Build(); gauge.SetVisible(visible); }

        /// <summary>Göstergenin hedef seviyesi (0..1, ortak ilerleme). Yumuşakça yetişir.</summary>
        public void SetLevel(float level01) { Build(); gauge.SetLevel(level01); }

        public void SetLevelNow(float level01) { Build(); gauge.SetLevelNow(level01); }

        /// <summary>Geçen süre / toplam (göstergede mm:ss).</summary>
        public void SetFloodTime(float elapsed, float total) { Build(); gauge.SetTime(elapsed, total); }

        /// <summary>Güvenli bölgeye varıldı: gösterge yeşil, sayaç durdu yazısı.</summary>
        public void SetFloodSafe(bool safe) { Build(); gauge.SetSafe(safe); }

        /// <summary>%100: kırmızı nabız + taşma.</summary>
        public void SetFloodCritical(bool critical) { Build(); gauge.SetCritical(critical); }

        /// <summary>Göstergenin altındaki kısa aşama yazısı (null = otomatik).</summary>
        public void SetStage(string text, Color color) { Build(); gauge.SetStageText(text); }

        /// <summary>İkon + başlık + açıklama şeridi (sıraya girer, süre dolunca kaybolur).</summary>
        public void ShowToast(string title, string body, ToastKind kind, float seconds = 3.5f)
        {
            Build();
            toasts.Enqueue(new Toast { title = title, body = body, kind = kind, seconds = Mathf.Max(1f, seconds) });
            if (toastRoutine == null) toastRoutine = StartCoroutine(PlayToasts());
        }

        /// <summary>Bütün HUD'u gizler (sonuç ekranı).</summary>
        public void HideAll()
        {
            Build();
            gauge.SetVisible(false);
            toasts.Clear();
            if (toastRoutine != null) { StopCoroutine(toastRoutine); toastRoutine = null; }
            toastGroup.alpha = 0f;
        }

        private IEnumerator PlayToasts()
        {
            while (toasts.Count > 0)
            {
                var t = toasts.Dequeue();
                Color c = t.kind == ToastKind.Danger ? FeUi.Red : t.kind == ToastKind.Warning ? FeUi.Amber : FeUi.Teal;
                FeUi.SetFrameColors(toastFrame, c, FeUi.A(FeUi.Panel, 0.95f));
                FeUi.SetFrameColors(toastBadge, FeUi.Ink, c);
                toastGlyph.text = t.kind == ToastKind.Info ? "i" : "!";
                toastTitle.color = c;
                toastTitle.text = t.title ?? "";
                toastBody.text = t.body ?? "";
                PlaceToast();
                for (float k = 0f; k < 0.2f; k += Time.unscaledDeltaTime) { PlaceToast(); toastGroup.alpha = k / 0.2f; yield return null; }
                toastGroup.alpha = 1f;
                for (float k = 0f; k < t.seconds; k += Time.unscaledDeltaTime) { PlaceToast(); yield return null; }
                for (float k = 0f; k < 0.25f; k += Time.unscaledDeltaTime) { PlaceToast(); toastGroup.alpha = 1f - k / 0.25f; yield return null; }
                toastGroup.alpha = 0f;
            }
            toastRoutine = null;
        }

        /// <summary>Şerit üst ortada (görev panelinin sağında).</summary>
        private void PlaceToast()
        {
            Vector2 anchor = new Vector2(0.5f, 1f), pos = new Vector2(0f, -8f);
            // görev paneli sol üstte → şeridin sol kenarı panelin sağında kalsın (en-boy oranı değişse de)
            float w = ((RectTransform)canvas.transform).rect.width;
            pos.x = Mathf.Max(0f, Mathf.Ceil(ToastMinLeft - (w * 0.5f - ToastSize.x * 0.5f)));
            if (anchor == toastAnchor && pos == toastPos) return;
            toastAnchor = anchor; toastPos = pos;
            FeUi.Place(toastRoot, anchor.x, anchor.y, pos, ToastSize);
        }

    }
}
