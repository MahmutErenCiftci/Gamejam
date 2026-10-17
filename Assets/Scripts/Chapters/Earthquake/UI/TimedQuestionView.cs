using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Annenin süreli sorusu (maket: S/eqD_mock_question.png): panelin üstüne taşan portre çerçevesi + "ANNE" plakası,
    /// soru, yeşil→amber→kırmızı boşalan süre çubuğu, 1–4 tuş rozetli şık satırları (fareyle üzerine gelme/tıklama).
    /// Geri bildirim: seçilen şık yeşil ✓ / kırmızı ✗ olur, altında "DOĞRU!" / "YANLIŞ" / "SÜRE DOLDU!" + ceza + açıklama;
    /// E/Space/Enter/tık ya da birkaç saniye sonra biter. TryGetChoice 0 tabanlı şık indeksi verir.
    /// Arayüzü kodla kurar (Canvas sıralaması 90).
    /// </summary>
    public class TimedQuestionView : TimedQuestionViewBase
    {
        [SerializeField] private EarthquakeUISkin skin;
        [SerializeField] private int sortingOrder = 90;
        [SerializeField, Range(0f, 1f)] private float dimAmount = 0.6f;
        [Tooltip("Geri bildirim en az bu kadar ekranda kalır, sonra kendiliğinden geçer (E/Space ile daha erken geçilir).")]
        [SerializeField] private float feedbackAutoSeconds = 2.5f;
        [Tooltip("Uzun açıklamalar için karakter başına ek okuma süresi.")]
        [SerializeField] private float feedbackSecondsPerChar = 0.02f;
        [SerializeField] private float feedbackMaxSeconds = 5f;
        [SerializeField] private string correctHeader = "DOĞRU!";
        [SerializeField] private string wrongHeader = "YANLIŞ";
        [SerializeField] private string timeoutHeader = "SÜRE DOLDU!";
        [SerializeField] private string continueHint = "[E] Devam";

        private const int MaxOptions = 5;
        private const float PanelW = 560f, PanelBottom = 12f;
        private const float OptTop = 48f, OptStep = 26f, OptH = 24f, OptX = 12f, OptW = 536f;

        private class Option
        {
            public RectTransform root;
            public Image frame;
            public Image badge;      // yalnız yedek görünümde
            public Text badgeText;
            public Text label;
            public Image mark;
            public EqButton button;
            public CanvasGroup group;
            public bool hovered;
        }

        private enum OptState { Normal, Selected, Correct, Wrong }

        private bool built;
        private Font font;
        private Canvas canvas;
        private CanvasGroup group;
        private Image dim;
        private RectTransform panel;
        private Image portrait;
        private Text portraitInitial;
        private Image nameplate;
        private Text nameText;
        private Text questionText;
        private RectTransform barRoot;
        private RectTransform barFillRt;
        private Image barFill;
        private readonly List<Option> options = new List<Option>();
        private Image feedbackBox;
        private CanvasGroup feedbackGroup;
        private Image feedbackIcon;
        private Text feedbackHeader, feedbackPenalty, feedbackBody, feedbackHint;

        private int optionCount;
        private float limit = 5f;
        private float timeLeft = 5f;
        private bool open;
        private bool locked;
        private int pending = -1;
        private int selected = -1;
        private Coroutine closeRoutine;
        private Coroutine openRoutine;
        private float clock;

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_QuestionCanvas", sortingOrder, out group);

            dim = EqUi.DimImage(canvas.transform, skin);

            var pImg = EqUi.Img("QuestionPanel", canvas.transform, EqUi.S(skin, s => s.QuestionPanel), EqUi.Ink, skin);
            panel = pImg.rectTransform;
            EqUi.Place(panel, 0.5f, 0f, new Vector2(0f, PanelBottom), new Vector2(PanelW, PanelHeight(4)));
            EqUi.Fallback(pImg, EqUi.Ink, EqUi.Navy, skin);

            // Portre (panelin üstünden taşar) + isim plakası
            var frame = EqUi.Img("PortraitFrame", panel, EqUi.S(skin, s => s.PortraitFrame), EqUi.Amber, skin);
            EqUi.TL(frame.rectTransform, 12f, -34f, 72f, 72f);
            EqUi.Fallback(frame, EqUi.Amber, EqUi.NavyLight, skin);
            portrait = EqUi.Img("Portrait", frame.transform, null, Color.white, skin);
            EqUi.TL(portrait.rectTransform, 4f, 4f, 64f, 64f);
            portrait.preserveAspect = true;
            portraitInitial = EqUi.Txt("Initial", frame.transform, font, 28, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);

            nameplate = EqUi.Img("NamePlate", panel, EqUi.S(skin, s => s.Nameplate), EqUi.Amber, skin);
            EqUi.TL(nameplate.rectTransform, 90f, -8f, 46f, 15f);
            EqUi.Fallback(nameplate, EqUi.Ink, EqUi.Amber, skin);
            nameText = EqUi.Txt("Name", nameplate.transform, font, 7, EqUi.Ink, TextAnchor.MiddleCenter);
            EqUi.StretchOffsets(nameText.rectTransform, 2f, 2f, 2f, 1f);
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Soru
            questionText = EqUi.Txt("Question", panel, font, 11, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.Ink);
            EqUi.TL(questionText.rectTransform, 92f, 11f, 456f, 18f);
            EqUi.BestFit(questionText, 7);

            // Süre çubuğu (beyaz dolgu renklendirilir)
            var bBack = EqUi.Img("TimeBar_Back", panel, EqUi.S(skin, s => s.TimeBarBack), EqUi.NavyDark, skin);
            barRoot = bBack.rectTransform;
            EqUi.TL(barRoot, 92f, 32f, 456f, 8f);
            EqUi.Fallback(bBack, EqUi.Ink, EqUi.NavyDark, skin);
            var inner = EqUi.Stretch(EqUi.Rect("Inner", barRoot), 1f);
            barFill = EqUi.Img("TimeBar_Fill", inner, EqUi.S(skin, s => s.TimeBarFill), EqUi.Green, skin);
            barFillRt = barFill.rectTransform;
            barFillRt.anchorMin = Vector2.zero;
            barFillRt.anchorMax = Vector2.one;
            barFillRt.pivot = new Vector2(0f, 0.5f);
            barFillRt.offsetMin = barFillRt.offsetMax = Vector2.zero;

            for (int i = 0; i < MaxOptions; i++) options.Add(BuildOption(i));
            BuildFeedback();

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        private static float PanelHeight(int n) => OptTop + Mathf.Max(1, n) * OptStep + 12f;

        private Option BuildOption(int i)
        {
            var o = new Option();
            o.frame = EqUi.Img("Option" + (i + 1), panel, EqUi.S(skin, s => s.Option), EqUi.Ink, skin, true);
            o.root = o.frame.rectTransform;
            EqUi.TL(o.root, OptX, OptTop + i * OptStep, OptW, OptH);
            if (o.frame.sprite == null)
            {
                EqUi.Fallback(o.frame, EqUi.Ink, EqUi.Cream, skin);
                o.badge = EqUi.Img("Badge", o.root, null, EqUi.Navy, skin);
                EqUi.TL(o.badge.rectTransform, 3f, 3f, 15f, 16f);
            }
            o.group = o.root.gameObject.AddComponent<CanvasGroup>();

            o.badgeText = EqUi.Txt("Key", o.root, font, 9, EqUi.Cream, TextAnchor.MiddleCenter);
            EqUi.TL(o.badgeText.rectTransform, 3f, 3f, 15f, 16f);
            o.badgeText.text = (i + 1).ToString();

            o.label = EqUi.Txt("Text", o.root, font, 9, EqUi.Ink, TextAnchor.MiddleLeft);
            EqUi.TopBand(o.label.rectTransform, 23f, 24f, 3f, 16f);
            EqUi.BestFit(o.label, 6);

            o.mark = EqUi.Img("Mark", o.root, null, Color.white, skin);
            o.mark.rectTransform.anchorMin = o.mark.rectTransform.anchorMax = new Vector2(1f, 1f);
            o.mark.rectTransform.pivot = new Vector2(1f, 1f);
            o.mark.rectTransform.anchoredPosition = new Vector2(-8f, -5f);
            o.mark.rectTransform.sizeDelta = new Vector2(12f, 12f);
            o.mark.gameObject.SetActive(false);

            o.button = o.root.gameObject.AddComponent<EqButton>();
            o.button.Init();
            int idx = i;
            o.button.Clicked += () => Choose(idx);
            o.button.HoverChanged += h => { o.hovered = h; if (!locked && open) ApplyState(idx, OptState.Normal); };
            o.root.gameObject.SetActive(false);
            return o;
        }

        private void BuildFeedback()
        {
            feedbackBox = EqUi.Img("Feedback", panel, EqUi.S(skin, s => s.InfoCard), EqUi.Ink, skin);
            EqUi.Fallback(feedbackBox, EqUi.Ink, EqUi.Cream, skin, 1f, 17f, EqUi.NavyDark);
            feedbackGroup = feedbackBox.gameObject.AddComponent<CanvasGroup>();
            feedbackGroup.blocksRaycasts = false;
            var rt = feedbackBox.rectTransform;

            feedbackIcon = EqUi.Img("Icon", rt, null, EqUi.Green, skin);
            EqUi.TL(feedbackIcon.rectTransform, 6f, 3f, 12f, 12f);

            feedbackHeader = EqUi.Txt("Header", rt, font, 9, EqUi.Green, TextAnchor.MiddleLeft, EqUi.Ink);
            EqUi.TopBand(feedbackHeader.rectTransform, 24f, 120f, 2f, 15f);
            feedbackHeader.horizontalOverflow = HorizontalWrapMode.Overflow;

            feedbackPenalty = EqUi.Txt("Penalty", rt, font, 9, EqUi.Hex(0xFF7A62), TextAnchor.MiddleRight, EqUi.Ink);
            EqUi.TopBand(feedbackPenalty.rectTransform, 200f, 8f, 2f, 15f);

            feedbackBody = EqUi.Txt("Body", rt, font, 8, EqUi.Ink, TextAnchor.UpperLeft);
            EqUi.StretchOffsets(feedbackBody.rectTransform, 8f, 13f, 8f, 22f);
            EqUi.BestFit(feedbackBody, 6);

            feedbackHint = EqUi.Txt("Hint", rt, font, 6, EqUi.Muted, TextAnchor.LowerRight);
            EqUi.StretchOffsets(feedbackHint.rectTransform, 8f, 4f, 8f, 4f);
            feedbackHint.text = continueHint;

            feedbackBox.gameObject.SetActive(false);
        }

        public override void Open(string speaker, Sprite portraitSprite, string question, string[] opts, float timeLimit)
        {
            EnsureBuilt();
            if (closeRoutine != null) { StopCoroutine(closeRoutine); closeRoutine = null; }

            nameText.text = string.IsNullOrEmpty(speaker) ? "ANNE" : speaker;
            float plateW = Mathf.Max(46f, Mathf.Ceil(EqUi.PreferredWidth(nameText, nameText.text)) + 14f);
            EqUi.TL(nameplate.rectTransform, 90f, -8f, plateW, 15f);
            Sprite p = portraitSprite != null ? portraitSprite : (skin != null ? skin.DefaultPortrait : null);
            portrait.sprite = p;
            portrait.enabled = p != null;
            portraitInitial.text = p == null ? nameText.text.Substring(0, 1) : "";
            questionText.text = question ?? "";

            optionCount = opts == null ? 0 : Mathf.Min(opts.Length, MaxOptions);
            panel.sizeDelta = new Vector2(PanelW, PanelHeight(optionCount));
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                bool on = i < optionCount;
                o.root.gameObject.SetActive(on);
                if (!on) continue;
                EqUi.TL(o.root, OptX, OptTop + i * OptStep, OptW, OptH);
                o.label.text = opts[i] ?? "";
                o.hovered = false;
                o.group.alpha = 1f;
                o.button.Interactable = true;
                ApplyState(i, OptState.Normal);
            }

            limit = Mathf.Max(0.1f, timeLimit);
            timeLeft = limit;
            pending = -1;
            selected = -1;
            locked = false;
            open = true;
            feedbackBox.gameObject.SetActive(false);
            questionText.gameObject.SetActive(true);
            barRoot.gameObject.SetActive(true);
            barRoot.localScale = Vector3.one;
            UpdateBar();

            canvas.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            if (isActiveAndEnabled)
            {
                if (openRoutine != null) StopCoroutine(openRoutine);
                openRoutine = StartCoroutine(OpenAnim());
            }
            else
            {
                group.alpha = 1f;
                EqUi.SetDim(dim, dimAmount);
                panel.anchoredPosition = new Vector2(0f, PanelBottom);
            }
        }

        private IEnumerator OpenAnim()
        {
            const float dur = 0.22f;
            float a0 = group.alpha;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = EqUi.OutCubic(t / dur);
                group.alpha = Mathf.Lerp(a0, 1f, k);
                EqUi.SetDim(dim, dimAmount * k);
                panel.anchoredPosition = EqUi.Snap(new Vector2(0f, Mathf.Lerp(PanelBottom - 40f, PanelBottom, k)));
                yield return null;
            }
            group.alpha = 1f;
            EqUi.SetDim(dim, dimAmount);
            panel.anchoredPosition = new Vector2(0f, PanelBottom);
            openRoutine = null;
        }

        public override void SetTimeLeft(float seconds)
        {
            EnsureBuilt();
            timeLeft = Mathf.Clamp(seconds, 0f, limit);
            UpdateBar();
        }

        private void UpdateBar()
        {
            float k = limit > 0f ? Mathf.Clamp01(timeLeft / limit) : 0f;
            var parent = (RectTransform)barFillRt.parent;
            float w = parent.rect.width > 0f ? parent.rect.width : 454f;
            float px = Mathf.Round(k * w);
            barFillRt.anchorMax = new Vector2(px / w, 1f);
            barFill.enabled = px >= 1f;
            barFill.color = EqUi.BarColor(k);
        }

        public override bool TryGetChoice(out int index)
        {
            index = pending;
            if (pending < 0) return false;
            pending = -1;
            return true;
        }

        private void Choose(int idx)
        {
            if (!open || locked || idx < 0 || idx >= optionCount) return;
            locked = true;
            selected = idx;
            pending = idx;
            for (int i = 0; i < optionCount; i++)
            {
                options[i].button.Interactable = false;
                if (i == idx) ApplyState(i, OptState.Selected);
                else options[i].group.alpha = 0.55f;
            }
        }

        private void Update()
        {
            if (!built || !open) return;
            clock += Time.unscaledDeltaTime;
            if (!locked)
            {
                int key = GameInput.ChoiceKeyPressed;
                if (key >= 1 && key <= optionCount) Choose(key - 1);
            }
            // Son 1.5 sn: çubuk yumuşakça nabız atar (yanıp sönme yok)
            float s = 1f;
            if (!locked && timeLeft <= 1.5f && timeLeft > 0f) s = 1f + 0.25f * Mathf.Abs(Mathf.Sin(clock * Mathf.PI * 2f));
            barRoot.localScale = new Vector3(1f, s, 1f);
        }

        private void ApplyState(int i, OptState st)
        {
            var o = options[i];
            bool hover = st == OptState.Normal && o.hovered;
            Sprite sp = null;
            Color border = EqUi.Ink, fill = EqUi.Cream, badge = EqUi.Navy, badgeText = EqUi.Cream;
            Sprite mark = null;
            switch (st)
            {
                case OptState.Normal:
                    sp = hover ? EqUi.S(skin, s => s.OptionHover) : EqUi.S(skin, s => s.Option);
                    if (hover) { border = EqUi.Amber; fill = EqUi.Hex(0xFFF6E2); badge = EqUi.Amber; badgeText = EqUi.Ink; }
                    break;
                case OptState.Selected:
                    sp = EqUi.S(skin, s => s.OptionHover);
                    border = EqUi.Amber; fill = EqUi.Hex(0xFFF6E2); badge = EqUi.Amber; badgeText = EqUi.Ink;
                    break;
                case OptState.Correct:
                    sp = EqUi.S(skin, s => s.OptionCorrect);
                    border = EqUi.Green; fill = EqUi.Hex(0xD3F1DC); badge = EqUi.Green; badgeText = EqUi.White;
                    mark = EqUi.S(skin, s => s.Check);
                    break;
                case OptState.Wrong:
                    sp = EqUi.S(skin, s => s.OptionWrong);
                    border = EqUi.Red; fill = EqUi.Hex(0xF9D3CD); badge = EqUi.Red; badgeText = EqUi.White;
                    mark = EqUi.S(skin, s => s.Cross);
                    break;
            }
            if (sp == null) sp = EqUi.S(skin, s => s.Option);

            if (sp != null) EqUi.Skin(o.frame, sp, fill, skin);
            else
            {
                EqUi.FallbackColors(o.frame, border, fill);
                if (o.badge != null) o.badge.color = badge;
            }
            o.badgeText.color = badgeText;

            bool showMark = st == OptState.Correct || st == OptState.Wrong;
            o.mark.gameObject.SetActive(showMark);
            if (showMark)
            {
                EqUi.Skin(o.mark, mark, st == OptState.Correct ? EqUi.Green : EqUi.Red, skin);
                o.mark.preserveAspect = true;
            }
            o.group.alpha = 1f;
        }

        public override IEnumerator ShowFeedback(bool correct, bool timedOut, string text, int penaltySeconds)
        {
            EnsureBuilt();
            locked = true;
            for (int i = 0; i < optionCount; i++) options[i].button.Interactable = false;
            bool hasPick = !timedOut && selected >= 0 && selected < optionCount;

            // 1) Şık durumu: seçilen yeşil ✓ / kırmızı ✗, diğerleri soluk
            if (hasPick)
            {
                for (int i = 0; i < optionCount; i++)
                {
                    if (i == selected) ApplyState(i, correct ? OptState.Correct : OptState.Wrong);
                    else options[i].group.alpha = 0.45f;
                }
            }
            else
            {
                for (int i = 0; i < optionCount; i++) options[i].group.alpha = 0.45f;
                timeLeft = 0f;
                UpdateBar();
            }
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime) yield return null;

            // 2) Seçilen şık en üste kayar, altında açıklama kutusu açılır
            float boxTop = OptTop;
            if (hasPick)
            {
                var srt = options[selected].root;
                float fromY = OptTop + selected * OptStep;
                for (int i = 0; i < optionCount; i++)
                    if (i != selected) options[i].root.gameObject.SetActive(false);
                for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
                {
                    float k = EqUi.OutCubic(t / 0.18f);
                    EqUi.TL(srt, OptX, Mathf.Round(Mathf.Lerp(fromY, OptTop, k)), OptW, OptH);
                    yield return null;
                }
                EqUi.TL(srt, OptX, OptTop, OptW, OptH);
                boxTop = OptTop + OptStep;
            }
            else
            {
                for (int i = 0; i < optionCount; i++) options[i].root.gameObject.SetActive(false);
            }

            float boxBottom = OptTop + Mathf.Max(optionCount, 4) * OptStep - 2f;
            panel.sizeDelta = new Vector2(PanelW, PanelHeight(Mathf.Max(optionCount, 4)));
            EqUi.TL(feedbackBox.rectTransform, OptX, boxTop, OptW, boxBottom - boxTop);

            Color c = timedOut ? EqUi.Amber : correct ? EqUi.Hex(0x5FD98E) : EqUi.Hex(0xFF7A62);
            feedbackHeader.text = timedOut ? timeoutHeader : correct ? correctHeader : wrongHeader;
            feedbackHeader.color = c;
            Sprite icon = timedOut ? EqUi.S(skin, s => s.ClockIcon) : correct ? EqUi.S(skin, s => s.Check) : EqUi.S(skin, s => s.Cross);
            EqUi.Skin(feedbackIcon, icon, c, skin);
            feedbackPenalty.text = penaltySeconds > 0 ? "-" + penaltySeconds + " SANİYE" : "";
            feedbackBody.text = text ?? "";
            feedbackBox.gameObject.SetActive(true);

            var hdr = feedbackHeader.rectTransform;
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.2f;
                feedbackGroup.alpha = EqUi.OutCubic(k);
                float s = Mathf.Lerp(1.25f, 1f, EqUi.OutBack(k, 1f));
                hdr.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            feedbackGroup.alpha = 1f;
            hdr.localScale = Vector3.one;

            float auto = feedbackAutoSeconds + feedbackSecondsPerChar * (text != null ? text.Length : 0);
            auto = Mathf.Max(feedbackAutoSeconds, Mathf.Min(feedbackMaxSeconds, auto));
            int startFrame = Time.frameCount;
            for (float t = 0f; t < auto; t += Time.unscaledDeltaTime)
            {
                if (Time.frameCount > startFrame && t > 0.35f && (GameInput.ContinuePressed || GameInput.ConfirmPressed)) break;
                yield return null;
            }
        }

        public override void Close()
        {
            if (!built) return;
            open = false;
            locked = true;
            pending = -1;
            if (openRoutine != null) { StopCoroutine(openRoutine); openRoutine = null; }
            if (isActiveAndEnabled && canvas.gameObject.activeSelf)
            {
                if (closeRoutine != null) StopCoroutine(closeRoutine);
                closeRoutine = StartCoroutine(CloseAnim());
            }
            else Hide();
        }

        private IEnumerator CloseAnim()
        {
            float a0 = group.alpha;
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.15f;
                group.alpha = Mathf.Lerp(a0, 0f, k);
                yield return null;
            }
            Hide();
            closeRoutine = null;
        }

        private void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            feedbackBox.gameObject.SetActive(false);
            canvas.gameObject.SetActive(false);
        }
    }
}
