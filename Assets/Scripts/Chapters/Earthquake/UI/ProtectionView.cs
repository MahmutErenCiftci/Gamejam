using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Sarsıntı sırasında ÇÖK → KAPAN → TUTUN göstergesi (maket: S/eqD_mock_extra.png): adım noktaları
    /// (biten yeşil tik, şimdiki amber + hafif nabız, sıradaki koyu), aralarında çizgi, adım adları, basılı tutma çubuğu
    /// ve ipucu yazısı. Warn() panelin üstünde kısa, hafif sallanan kırmızı uyarı gösterir.
    /// Arayüzü kodla kurar (Canvas sıralaması 89).
    /// </summary>
    public class ProtectionView : ProtectionViewBase
    {
        [SerializeField] private EarthquakeUISkin skin;
        [SerializeField] private int sortingOrder = 89;
        [SerializeField] private float warnSeconds = 1.8f;

        private const int MaxSteps = 5;
        private const float PanelW = 300f, PanelH = 96f, PanelBottom = 6f;
        private const float StepSpacing = 90f;
        private static readonly Color DoneText = EqUi.Hex(0x5FD98E);
        private static readonly Color FutureText = EqUi.Hex(0xA9B0CC);

        private class Step
        {
            public Image dot;
            public Text label;
            public Image line; // bu adımdan öncekine bağlayan çizgi
        }

        private bool built;
        private Font font;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panel;
        private readonly List<Step> steps = new List<Step>();
        private Text promptText;
        private RectTransform barRoot;
        private RectTransform barFillRt;
        private Image barFill;
        private RectTransform warnRoot;
        private CanvasGroup warnGroup;
        private Text warnText;

        private int stepCount;
        private int current = -1;
        private float hold;
        private float shown;
        private bool visible;
        private float clock;
        private float warnT = -1f;
        private Coroutine fadeRoutine;
        private EarthquakeBalanceHUD balanceHUD;

        public override void BeginBalance(EarthquakeBalanceSettings settings, EqBalanceTexts texts)
        {
            Hide();
            if (balanceHUD == null) balanceHUD = gameObject.AddComponent<EarthquakeBalanceHUD>();
            balanceHUD.Begin(settings, texts);
        }

        public override void SetBalance(EarthquakeBalanceModel state)
        {
            if (balanceHUD != null) balanceHUD.Draw(state);
        }

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_ProtectionCanvas", sortingOrder, out group);
            group.blocksRaycasts = false;
            group.interactable = false;

            var pImg = EqUi.Img("ProtectPanel", canvas.transform, EqUi.S(skin, s => s.ProtectPanel), EqUi.Ink, skin);
            panel = pImg.rectTransform;
            EqUi.Place(panel, 0.5f, 0f, new Vector2(0f, PanelBottom), new Vector2(PanelW, PanelH));
            EqUi.Fallback(pImg, EqUi.Ink, EqUi.Navy, skin, 1f, 4f, EqUi.Red);

            for (int i = 0; i < MaxSteps; i++) steps.Add(BuildStep(i));

            // Basılı tutma çubuğu
            var bBack = EqUi.Img("HoldBar_Back", panel, EqUi.S(skin, s => s.HoldBarBack), EqUi.Ink, skin);
            barRoot = bBack.rectTransform;
            EqUi.TL(barRoot, 20f, 52f, 260f, 12f);
            EqUi.Fallback(bBack, EqUi.Ink, EqUi.NavyDark, skin);
            var inner = EqUi.Stretch(EqUi.Rect("Inner", barRoot), 1f);
            barFill = EqUi.Img("HoldBar_Fill", inner, EqUi.S(skin, s => s.HoldBarFill), EqUi.Green, skin);
            barFillRt = barFill.rectTransform;
            barFillRt.anchorMin = Vector2.zero;
            barFillRt.anchorMax = new Vector2(0f, 1f);
            barFillRt.pivot = new Vector2(0f, 0.5f);
            barFillRt.offsetMin = barFillRt.offsetMax = Vector2.zero;
            barRoot.gameObject.SetActive(false);

            promptText = EqUi.Txt("Prompt", panel, font, 8, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.TopBand(promptText.rectTransform, 10f, 10f, 71f, 13f);
            EqUi.BestFit(promptText, 6);

            // Uyarı (panelin üstünde)
            var wImg = EqUi.Img("Warn", canvas.transform, EqUi.S(skin, s => s.Toast), EqUi.Ink, skin);
            warnRoot = wImg.rectTransform;
            EqUi.Place(warnRoot, 0.5f, 0f, new Vector2(0f, PanelBottom + PanelH + 6f), new Vector2(230f, 24f));
            if (wImg.sprite == null)
            {
                EqUi.Fallback(wImg, EqUi.Ink, EqUi.Red, skin);
                var badge = EqUi.Img("Badge", warnRoot, null, EqUi.Cream, skin);
                EqUi.TL(badge.rectTransform, 3f, 3f, 18f, 16f);
            }
            warnGroup = warnRoot.gameObject.AddComponent<CanvasGroup>();
            var wIcon = EqUi.Img("Icon", warnRoot, EqUi.S(skin, s => s.IconHazard), EqUi.Red, skin);
            EqUi.TL(wIcon.rectTransform, 4f, 3f, 16f, 16f);
            if (wIcon.sprite == null) EqUi.TL(wIcon.rectTransform, 7f, 6f, 10f, 10f);
            warnText = EqUi.Txt("Text", warnRoot, font, 8, EqUi.White, TextAnchor.MiddleLeft, EqUi.ShadowRed);
            EqUi.TopBand(warnText.rectTransform, 25f, 6f, 4f, 13f);
            EqUi.BestFit(warnText, 6);
            warnRoot.gameObject.SetActive(false);

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        private Step BuildStep(int i)
        {
            var s = new Step();
            s.line = EqUi.Img("Line" + i, panel, null, EqUi.NavyLight, skin);
            s.dot = EqUi.Img("StepDot" + (i + 1), panel, EqUi.S(skin, k => k.StepDotOff), EqUi.NavyDark, skin);
            s.label = EqUi.Txt("StepLabel" + (i + 1), panel, font, 9, FutureText, TextAnchor.MiddleCenter, EqUi.Ink);
            s.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            s.line.gameObject.SetActive(false);
            s.dot.gameObject.SetActive(false);
            s.label.gameObject.SetActive(false);
            return s;
        }

        public override void Show(string[] stepNames, int currentStep, string prompt)
        {
            EnsureBuilt();
            int n = stepNames == null ? 0 : Mathf.Min(stepNames.Length, MaxSteps);
            bool stepChanged = currentStep != current || n != stepCount;
            stepCount = n;
            current = currentStep;

            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                bool on = i < n;
                s.dot.gameObject.SetActive(on);
                s.label.gameObject.SetActive(on);
                s.line.gameObject.SetActive(on && i > 0);
                if (!on) continue;
                float cx = Mathf.Round(PanelW / 2f + (i - (n - 1) / 2f) * StepSpacing);
                EqUi.TL(s.dot.rectTransform, cx - 8f, 14f, 16f, 16f);
                EqUi.TL(s.label.rectTransform, cx - 45f, 32f, 90f, 12f);
                s.label.text = stepNames[i] ?? "";
                if (i > 0)
                {
                    float prev = Mathf.Round(PanelW / 2f + (i - 1 - (n - 1) / 2f) * StepSpacing);
                    EqUi.TL(s.line.rectTransform, prev + 12f, 21f, cx - prev - 24f, 2f);
                }
                ApplyStep(i);
            }

            promptText.text = prompt ?? "";
            bool holdStep = n > 0 && current == n - 1;
            barRoot.gameObject.SetActive(holdStep || hold > 0f);
            if (stepChanged && !holdStep) SetHoldProgress(0f);
            LayoutPrompt();

            if (!visible) FadeTo(true);
        }

        private void LayoutPrompt()
        {
            // Çubuk yokken ipucu biraz yukarı çıkar (boşluk kalmasın).
            EqUi.TopBand(promptText.rectTransform, 10f, 10f, barRoot.gameObject.activeSelf ? 71f : 56f, 13f);
        }

        private void ApplyStep(int i)
        {
            var s = steps[i];
            bool done = i < current;
            bool active = i == current;
            Sprite sp = done ? EqUi.S(skin, k => k.StepDotDone) : active ? EqUi.S(skin, k => k.StepDotOn) : EqUi.S(skin, k => k.StepDotOff);
            Color fb = done ? EqUi.Green : active ? EqUi.Amber : EqUi.NavyDark;
            EqUi.Skin(s.dot, sp, fb, skin);
            s.dot.rectTransform.localScale = Vector3.one;
            s.label.color = done ? DoneText : active ? EqUi.Amber : FutureText;
            if (i > 0) s.line.color = i <= current ? EqUi.Green : EqUi.NavyLight;
        }

        public override void SetHoldProgress(float t01)
        {
            EnsureBuilt();
            hold = Mathf.Clamp01(t01);
            if (hold > 0f && !barRoot.gameObject.activeSelf)
            {
                barRoot.gameObject.SetActive(true);
                LayoutPrompt();
            }
            if (!isActiveAndEnabled) { shown = hold; DrawBar(); }
        }

        public override void Warn(string text)
        {
            EnsureBuilt();
            warnText.text = text ?? "";
            warnRoot.gameObject.SetActive(true);
            warnGroup.alpha = 1f;
            warnT = 0f;
            if (!visible) FadeTo(true);
        }

        public override void Hide()
        {
            if (balanceHUD != null) balanceHUD.Hide();
            if (!built) return;
            warnT = -1f;
            warnRoot.gameObject.SetActive(false);
            if (visible) FadeTo(false);
        }

        private void FadeTo(bool show)
        {
            visible = show;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = null;
            if (show) canvas.gameObject.SetActive(true);
            if (!isActiveAndEnabled)
            {
                group.alpha = show ? 1f : 0f;
                panel.anchoredPosition = new Vector2(0f, PanelBottom);
                if (!show) ResetState();
                return;
            }
            fadeRoutine = StartCoroutine(Fade(show));
        }

        private IEnumerator Fade(bool show)
        {
            float a0 = group.alpha, a1 = show ? 1f : 0f;
            float y0 = show ? PanelBottom - 30f : panel.anchoredPosition.y, y1 = show ? PanelBottom : PanelBottom - 30f;
            const float dur = 0.2f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = EqUi.OutCubic(t / dur);
                group.alpha = Mathf.Lerp(a0, a1, k);
                panel.anchoredPosition = EqUi.Snap(new Vector2(0f, Mathf.Lerp(y0, y1, k)));
                yield return null;
            }
            group.alpha = a1;
            panel.anchoredPosition = new Vector2(0f, show ? PanelBottom : PanelBottom - 30f);
            if (!show) ResetState();
            fadeRoutine = null;
        }

        private void ResetState()
        {
            canvas.gameObject.SetActive(false);
            hold = shown = 0f;
            current = -1;
            stepCount = 0;
            DrawBar();
            barRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!built || !visible) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;

            // Şimdiki adım: hafif nabız (1.0–1.1)
            if (current >= 0 && current < stepCount)
            {
                float s = 1.05f + 0.05f * Mathf.Sin(clock * Mathf.PI * 2f * 1.2f);
                steps[current].dot.rectTransform.localScale = new Vector3(s, s, 1f);
            }

            // Çubuk yumuşak takip (geri düşüş anlık)
            shown = hold < shown ? hold : Mathf.MoveTowards(shown, hold, dt * 2.5f);
            DrawBar();

            // Uyarı: kısa ±1 px sallanma, sonra solar
            if (warnT >= 0f)
            {
                warnT += dt;
                float dx = warnT < 0.3f ? (((int)(warnT * 40f) % 2 == 0) ? 1f : -1f) : 0f;
                warnRoot.anchoredPosition = new Vector2(dx, PanelBottom + PanelH + 6f);
                float fadeStart = Mathf.Max(0.3f, warnSeconds - 0.4f);
                if (warnT > fadeStart) warnGroup.alpha = 1f - (warnT - fadeStart) / 0.4f;
                if (warnT >= warnSeconds)
                {
                    warnT = -1f;
                    warnRoot.gameObject.SetActive(false);
                }
            }
        }

        private void DrawBar()
        {
            var parent = (RectTransform)barFillRt.parent;
            float w = parent.rect.width > 0f ? parent.rect.width : 258f;
            float px = Mathf.Round(shown * w);
            barFillRt.anchorMax = new Vector2(px / w, 1f);
            barFill.enabled = px >= 1f;
        }
    }
}
