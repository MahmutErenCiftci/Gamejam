using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Sağ üst 120 sn sayacı (UI_EQ_TimerPanel + saat ikonu). Renk: 120–61 krem, 60–31 amber, 30–11 kırmızı,
    /// ≤10 kırmızı + hafif nabız (yanıp sönme yok). Ceza: "-10 SANİYE" yukarı süzülür, sayı yeni değere kayarak iner,
    /// panel kısa ve hafif (±2 px) sarsılır. Arayüzü Awake'te kodla kurar (Canvas sıralaması varsayılan 86).
    /// </summary>
    public class CountdownView : CountdownViewBase
    {
        [SerializeField] private EarthquakeUISkin skin;
        [SerializeField] private int sortingOrder = 86;
        [Tooltip("Sayı yeni değere bu sürede iner (ceza ya da hızlandırma).")]
        [SerializeField] private float dropTweenSeconds = 0.5f;
        [SerializeField] private float penaltyFloatSeconds = 1.4f;
        [Tooltip("Son 10 sn'de her tam saniyede bir çağrılır (tik sesi). Sistem de tik kancası veriyorsa yalnızca birini bağlayın.")]
        [SerializeField] private UnityEvent onTick = new UnityEvent();

        private const float PanelW = 64f, PanelWStatus = 88f, PanelH = 22f;
        private static readonly Vector2 PanelPos = new Vector2(-8f, -8f);

        private bool built;
        private Font font;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panel;
        private RectTransform timeRoot;
        private Text timeText;
        private Text statusText;

        private float target = 120f;
        private float shown = 120f;
        private float tweenFrom;
        private float tweenT = -1f;
        private int dropFrame = -10;
        private float dropFromValue;
        private float shakeT = -1f;
        private float flashT = -1f;
        private float visibleTarget;
        private float visibleAlpha;
        private string status;
        private int lastTickSecond = -1;
        private float clock;
        private int floatCount;

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_CountdownCanvas", sortingOrder, out group);
            group.blocksRaycasts = false;
            group.interactable = false;

            var img = EqUi.Img("TimerPanel", canvas.transform, EqUi.S(skin, s => s.TimerPanel), EqUi.Navy, skin);
            panel = img.rectTransform;
            EqUi.Place(panel, 1f, 1f, PanelPos, new Vector2(PanelW, PanelH));
            if (img.sprite == null)
            {
                EqUi.Fallback(img, EqUi.Ink, EqUi.Navy, skin);
                var well = EqUi.Img("Well", panel, null, EqUi.NavyDark, skin);
                EqUi.TL(well.rectTransform, 3f, 3f, 16f, 14f);
            }

            var icon = EqUi.Img("Clock", panel, EqUi.S(skin, s => s.ClockIcon), EqUi.Cream, skin);
            EqUi.TL(icon.rectTransform, 4f, 3f, 13f, 13f);
            if (icon.sprite == null) EqUi.TL(icon.rectTransform, 7f, 6f, 8f, 8f);

            timeRoot = EqUi.Rect("TimeRoot", panel);
            EqUi.StretchOffsets(timeRoot, 22f, 6f, 4f, 2f);
            timeText = EqUi.Txt("Time", timeRoot, font, 12, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            timeText.text = EqUi.Clock(target);

            statusText = EqUi.Txt("Status", panel, font, 10, EqUi.Red, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.StretchOffsets(statusText.rectTransform, 22f, 6f, 4f, 2f);
            statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            statusText.gameObject.SetActive(false);

            visibleAlpha = visibleTarget = 0f;
            group.alpha = 0f;
        }

        public override void SetVisible(bool v)
        {
            EnsureBuilt();
            visibleTarget = v ? 1f : 0f;
            if (!isActiveAndEnabled)
            {
                visibleAlpha = visibleTarget;
                group.alpha = visibleAlpha;
            }
        }

        public override void SetTime(float secondsRemaining)
        {
            EnsureBuilt();
            float v = Mathf.Max(0f, secondsRemaining);
            if (v < target - 1.5f)
            {
                // Ani düşüş (ceza / hızlandırma): sayı kayarak insin.
                dropFrame = Time.frameCount;
                dropFromValue = shown;
                tweenFrom = shown;
                tweenT = 0f;
            }
            else if (v > target + 1.5f)
            {
                tweenT = -1f;
                shown = v;
            }
            target = v;
            if (!isActiveAndEnabled)
            {
                shown = v;
                Refresh(0f);
            }
        }

        public override void ShowPenalty(int seconds, string reason)
        {
            EnsureBuilt();
            tweenFrom = Time.frameCount - dropFrame <= 2 ? dropFromValue : shown;
            tweenT = 0f;
            shakeT = 0f;
            flashT = 0f;
            SpawnFloat(seconds, reason);
        }

        public override void SetStatusText(string text)
        {
            EnsureBuilt();
            status = string.IsNullOrEmpty(text) ? null : text;
            statusText.text = status ?? "";
            statusText.gameObject.SetActive(status != null);
            timeRoot.gameObject.SetActive(status == null);
            // Uzun durum yazısı ("SARSINTI BİTTİ") panele sığsın: genişlik metne göre büyür.
            float statusW = status != null ? Mathf.Max(PanelWStatus, Mathf.Ceil(statusText.preferredWidth) + 30f) : PanelW;
            panel.sizeDelta = new Vector2(statusW, PanelH);
        }

        private void Update()
        {
            if (!built) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;

            if (!Mathf.Approximately(visibleAlpha, visibleTarget))
            {
                visibleAlpha = Mathf.MoveTowards(visibleAlpha, visibleTarget, dt / 0.25f);
                group.alpha = visibleAlpha;
            }

            if (tweenT >= 0f)
            {
                tweenT += dt / Mathf.Max(0.05f, dropTweenSeconds);
                shown = Mathf.Lerp(tweenFrom, target, EqUi.OutCubic(tweenT));
                if (tweenT >= 1f) { tweenT = -1f; shown = target; }
            }
            else shown = target;

            // Sarsılma: ±2 px, sönümlü, piksele oturtulmuş
            Vector2 offset = Vector2.zero;
            if (shakeT >= 0f)
            {
                shakeT += dt;
                float k = 1f - shakeT / 0.35f;
                if (k <= 0f) shakeT = -1f;
                else offset = new Vector2(Mathf.Round(Mathf.Sin(shakeT * 70f) * 2f * k), 0f);
            }
            panel.anchoredPosition = PanelPos + offset;

            Refresh(dt);
            TickCheck();
        }

        private void Refresh(float dt)
        {
            timeText.text = EqUi.Clock(shown);
            Color c = ColorFor(shown);
            if (flashT >= 0f)
            {
                flashT += dt;
                float k = 1f - flashT / 0.6f;
                if (k <= 0f) flashT = -1f;
                else c = Color.Lerp(c, EqUi.Red, k);
            }
            timeText.color = c;

            float scale = 1f;
            if (status == null && shown <= 10.001f && shown > 0f)
            {
                // Hafif nabız: sayı değişince büyür, yavaşça geri döner.
                float frac = shown - Mathf.Floor(shown);
                float beat = Mathf.Clamp01((frac - 0.7f) / 0.3f);
                scale = 1f + 0.1f * Mathf.Sin(beat * Mathf.PI);
            }
            else if (status != null)
            {
                scale = 1f + 0.05f * Mathf.Sin(clock * Mathf.PI * 2f * 0.8f);
            }
            timeRoot.localScale = new Vector3(scale, scale, 1f);
            statusText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void TickCheck()
        {
            if (status != null || visibleTarget < 0.5f) return;
            int s = Mathf.CeilToInt(target - 0.0001f);
            if (s > 10 || s <= 0) { lastTickSecond = s; return; }
            if (lastTickSecond > s) onTick.Invoke();
            lastTickSecond = s;
        }

        private static Color ColorFor(float s)
        {
            if (s > 60f) return EqUi.Cream;
            if (s > 30f) return EqUi.Amber;
            return EqUi.Red;
        }

        private void SpawnFloat(int seconds, string reason)
        {
            var root = EqUi.Rect("Penalty", canvas.transform);
            // Üst üste gelen cezalar biraz aşağıdan başlasın.
            float y = -34f - (floatCount % 2) * 6f;
            floatCount++;
            EqUi.Place(root, 1f, 1f, new Vector2(-10f, y), new Vector2(150f, 26f));
            var big = EqUi.Txt("Amount", root, font, 9, EqUi.Red, TextAnchor.UpperRight, EqUi.Ink);
            big.text = "-" + Mathf.Abs(seconds) + " SANİYE";
            if (!string.IsNullOrEmpty(reason))
            {
                var small = EqUi.Txt("Reason", root, font, 6, EqUi.Cream, TextAnchor.UpperRight, EqUi.Ink);
                EqUi.StretchOffsets(small.rectTransform, 0f, 0f, 0f, 12f);
                small.text = reason;
            }
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            if (isActiveAndEnabled) StartCoroutine(Float(root, cg));
            else Destroy(root.gameObject, penaltyFloatSeconds);
        }

        private IEnumerator Float(RectTransform rt, CanvasGroup cg)
        {
            Vector2 start = rt.anchoredPosition;
            float dur = Mathf.Max(0.3f, penaltyFloatSeconds);
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                rt.anchoredPosition = EqUi.Snap(start + new Vector2(0f, 10f * EqUi.OutCubic(k)));
                float s = k < 0.12f ? Mathf.Lerp(0.85f, 1f, EqUi.OutBack(k / 0.12f)) : 1f;
                rt.localScale = new Vector3(s, s, 1f);
                cg.alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                yield return null;
            }
            Destroy(rt.gameObject);
        }
    }
}
