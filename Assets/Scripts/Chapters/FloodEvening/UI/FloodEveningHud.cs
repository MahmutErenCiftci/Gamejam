using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel bölümünün HUD'u (LEAD): sağ kenarda dikey "SU SEVİYESİ" göstergesi (çevresel gösterge; sayısal süre YOK)
    /// ve üst ortada ikon + başlık + açıklamalı kısa uyarı şeritleri (aşamalı uyarılar, bariyer mesajı yedeği).
    /// Gösterge yalnız ortak ilerlemeyi (FloodEveningHub.Progress01) gösterir; altında "Suya hiç girme" notu durur,
    /// böylece hiçbir seviyenin suya girmek için güvenli olduğu ima edilmez. Arayüz kodla kurulur (Canvas 60).
    /// </summary>
    public class FloodEveningHud : MonoBehaviour
    {
        public enum ToastKind { Info, Warning, Danger }

        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 60;
        [Tooltip("Göstergenin ilerlemeye yetişme hızı (birim/sn)")]
        [SerializeField] private float levelSpeed = 0.35f;

        private class Toast { public string title, body; public ToastKind kind; public float seconds; }

        private bool built;
        private Canvas canvas;
        private CanvasGroup gaugeGroup;
        private RectTransform gaugeRoot, fill, surface;
        private Text stageText, gaugeTitleText, gaugeNoteText;
        private RectTransform toastRoot;
        private CanvasGroup toastGroup;
        private Image toastFrame, toastBadge;
        private Text toastTitle, toastBody, toastGlyph;
        private readonly Queue<Toast> toasts = new Queue<Toast>();
        private Coroutine toastRoutine;
        private float shownLevel, targetLevel;
        private bool gaugeVisible;
        private const float BarH = 120f, BarW = 18f;

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

            // --- Su seviyesi göstergesi (sağ kenar, dikey) ---
            gaugeRoot = FeUi.Rect("WaterGauge", canvas.transform);
            FeUi.Place(gaugeRoot, 1f, 0.5f, new Vector2(-10f, 6f), new Vector2(64f, 196f));
            gaugeGroup = gaugeRoot.gameObject.AddComponent<CanvasGroup>();
            var back = FeUi.Framed("Panel", gaugeRoot, FeUi.TealDark, FeUi.A(FeUi.Panel, 0.92f));
            FeUi.Stretch(back.rectTransform);
            var title = FeUi.Txt("Title", gaugeRoot, font, 8, FeUi.Teal, TextAnchor.MiddleCenter);
            FeUi.TL(title.rectTransform, 2f, 4f, 60f, 12f);
            FeUi.BestFit(title, 6);
            gaugeTitleText = title;

            var barFrame = FeUi.Framed("Bar", gaugeRoot, FeUi.Ink, FeUi.Hex(0x0B1626));
            FeUi.TL(barFrame.rectTransform, 32f - BarW * 0.5f - 1f, 19f, BarW + 2f, BarH + 2f);
            fill = FeUi.Img("Fill", barFrame.transform, FeUi.Water).rectTransform;
            fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(1f, 0f); fill.pivot = new Vector2(0.5f, 0f);
            fill.offsetMin = new Vector2(1f, 1f); fill.offsetMax = new Vector2(-1f, 1f);
            surface = FeUi.Img("Surface", fill, FeUi.Hex(0x8FD3FF)).rectTransform;
            surface.anchorMin = new Vector2(0f, 1f); surface.anchorMax = new Vector2(1f, 1f); surface.pivot = new Vector2(0.5f, 1f);
            surface.offsetMin = new Vector2(0f, -2f); surface.offsetMax = Vector2.zero;
            // Ölçek çizgileri (rakam yok: süre gösterilmez)
            for (int i = 1; i < 4; i++)
            {
                var tick = FeUi.Img("Tick" + i, barFrame.transform, FeUi.A(FeUi.Cream, 0.35f));
                var rt = tick.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, i / 4f); rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(6f, 1f); rt.anchoredPosition = new Vector2(-1f, 0f);
            }

            stageText = FeUi.Txt("Stage", gaugeRoot, font, 7, FeUi.Cream, TextAnchor.MiddleCenter);
            FeUi.TL(stageText.rectTransform, 2f, 19f + BarH + 4f, 60f, 11f);
            FeUi.BestFit(stageText, 6);

            var noteBadge = FeUi.Badge("NoteIcon", gaugeRoot, font, FeUi.Amber);
            FeUi.TL(noteBadge.rectTransform, 5f, 19f + BarH + 18f, 12f, 12f);
            var note = FeUi.Txt("Note", gaugeRoot, font, 7, FeUi.Amber, TextAnchor.UpperLeft);
            FeUi.TL(note.rectTransform, 19f, 19f + BarH + 16f, 44f, 26f);
            FeUi.BestFit(note, 6);
            gaugeNoteText = note;

            // --- Uyarı şeridi (üst orta) ---
            toastRoot = FeUi.Rect("Toast", canvas.transform);
            FeUi.Place(toastRoot, 0.5f, 1f, new Vector2(0f, -8f), new Vector2(300f, 44f));
            toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
            toastFrame = FeUi.Framed("Frame", toastRoot, FeUi.Teal, FeUi.A(FeUi.Panel, 0.95f));
            FeUi.Stretch(toastFrame.rectTransform);
            toastBadge = FeUi.Badge("Icon", toastRoot, font, FeUi.Amber);
            FeUi.TL(toastBadge.rectTransform, 8f, 13f, 18f, 18f);
            toastGlyph = toastBadge.transform.Find("Glyph").GetComponent<Text>();
            toastGlyph.fontSize = 13;
            toastTitle = FeUi.Txt("Title", toastRoot, font, 9, FeUi.Amber, TextAnchor.UpperLeft);
            FeUi.TL(toastTitle.rectTransform, 32f, 5f, 262f, 12f);
            FeUi.BestFit(toastTitle, 7);
            toastBody = FeUi.Txt("Body", toastRoot, font, 8, FeUi.Cream, TextAnchor.UpperLeft);
            FeUi.TL(toastBody.rectTransform, 32f, 18f, 262f, 24f);
            FeUi.BestFit(toastBody, 6);
            toastGroup.alpha = 0f;

            gaugeGroup.alpha = 0f;
            SetLevelNow(0f);
        }

        /// <summary>Gösterge başlığı ve altındaki not (metinler flood_evening.json "ui": gauge_title, gauge_note).</summary>
        public void SetGaugeTexts(string title, string note)
        {
            Build();
            gaugeTitleText.text = title ?? "";
            gaugeNoteText.text = note ?? "";
        }

        /// <summary>Su seviyesi göstergesini açar/kapatır (uyarı okunup süre başlayınca açılır).</summary>
        public void SetGaugeVisible(bool visible)
        {
            Build();
            gaugeVisible = visible;
        }

        /// <summary>Göstergenin hedef seviyesi (0..1, ortak ilerleme). Yumuşakça yetişir.</summary>
        public void SetLevel(float level01) => targetLevel = Mathf.Clamp01(level01);

        public void SetLevelNow(float level01)
        {
            targetLevel = shownLevel = Mathf.Clamp01(level01);
            ApplyLevel();
        }

        /// <summary>Göstergenin altındaki kısa aşama yazısı ("Yağmur artıyor" vb.).</summary>
        public void SetStage(string text, Color color)
        {
            Build();
            stageText.text = text ?? "";
            stageText.color = color;
        }

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
            gaugeVisible = false;
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
                for (float k = 0f; k < 0.2f; k += Time.unscaledDeltaTime) { toastGroup.alpha = k / 0.2f; yield return null; }
                toastGroup.alpha = 1f;
                for (float k = 0f; k < t.seconds; k += Time.unscaledDeltaTime) yield return null;
                for (float k = 0f; k < 0.25f; k += Time.unscaledDeltaTime) { toastGroup.alpha = 1f - k / 0.25f; yield return null; }
                toastGroup.alpha = 0f;
            }
            toastRoutine = null;
        }

        private void Update()
        {
            if (!built) return;
            float a = gaugeGroup.alpha;
            float wanted = gaugeVisible ? 1f : 0f;
            if (!Mathf.Approximately(a, wanted)) gaugeGroup.alpha = Mathf.MoveTowards(a, wanted, Time.unscaledDeltaTime * 3f);
            if (!Mathf.Approximately(shownLevel, targetLevel))
            {
                shownLevel = Mathf.MoveTowards(shownLevel, targetLevel, levelSpeed * Time.unscaledDeltaTime);
                ApplyLevel();
            }
            // Yüzeyde hafif dalga (piksel kayması yok, sadece parlaklık)
            if (surface != null)
            {
                var img = surface.GetComponent<Image>();
                img.color = Color.Lerp(FeUi.Hex(0x8FD3FF), FeUi.Hex(0x5FB2EE), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
            }
        }

        private void ApplyLevel()
        {
            if (fill == null) return;
            float h = Mathf.Round(Mathf.Lerp(4f, BarH, shownLevel));
            fill.offsetMax = new Vector2(-1f, 1f + h);
            fill.GetComponent<Image>().color = Color.Lerp(FeUi.Water, FeUi.WaterDeep, shownLevel);
        }
    }
}
