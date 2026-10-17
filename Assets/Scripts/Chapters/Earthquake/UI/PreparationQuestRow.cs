using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Hazırlık HUD'unun tek görev satırı (bütün görevler için ortak bileşen). Üç durum:
    /// Incomplete = gri boş halka + krem yazı; Active = amber gösterge + hafif nabız + parlak yazı;
    /// Completed = yeşil tik (pop) + yeşil yazı + kısa yeşil parlama + ince üstü çizili.
    /// Arayüzünü kodla kurar (Create); animasyonlarını PreparationQuestHUD her karede Tick ile ilerletir (ölçeksiz zaman).
    /// </summary>
    public class PreparationQuestRow : MonoBehaviour
    {
        public enum State { Incomplete, Active, Completed }

        private static readonly Color TextIdle = EqUi.A(EqUi.Cream, 0.86f);
        private static readonly Color TextActive = Color.white;
        private static readonly Color TextDone = EqUi.Hex(0x5BD58A);
        private static readonly Color SuffixColor = EqUi.Hex(0x9AA0BC);

        /// <summary>Tamamlanan görevin üstünü ince çizgiyle çiz (piksel yazıda okunurluğu düşürdüğü için kapalı).</summary>
        public static bool UseStrike = false;

        private RectTransform rt;
        private CanvasGroup group;
        private Image glow;
        private Image dot;
        private Text label;
        private Image strike;
        private Sprite dotEmpty, dotActive, dotDone;
        private string baseLabel = "";
        private string suffix = "";
        private float popSeconds = 0.15f, highlightSeconds = 0.3f;
        private float popT = -1f, flashT = -1f;

        public string TaskId { get; private set; }
        public State Current { get; private set; } = State.Incomplete;
        public RectTransform Rect => rt;
        /// <summary>Etiketin referans pikseli cinsinden tercih edilen genişliği (sığma kontrolü için).</summary>
        public float LabelPreferredWidth => label != null ? EqUi.PreferredWidth(label, label.text) : 0f;
        public float LabelWidth => label != null ? label.rectTransform.rect.width : 0f;

        /// <summary>Satırı parent altında kurar. Yükseklik/konum çağıran tarafından verilir (TopBand).</summary>
        public static PreparationQuestRow Create(Transform parent, string taskId, Font font, int fontSize,
            Sprite empty, Sprite active, Sprite doneSprite, Sprite glowSprite, EarthquakeUISkin skin)
        {
            var root = EqUi.Rect("Row_" + taskId, parent);
            var row = root.gameObject.AddComponent<PreparationQuestRow>();
            row.Build(taskId, font, fontSize, empty, active, doneSprite, glowSprite, skin);
            return row;
        }

        private void Build(string taskId, Font font, int fontSize, Sprite empty, Sprite active, Sprite doneSprite, Sprite glowSprite, EarthquakeUISkin skin)
        {
            TaskId = taskId;
            rt = (RectTransform)transform;
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            dotEmpty = empty;
            dotActive = active;
            dotDone = doneSprite;

            glow = EqUi.Img("Glow", rt, glowSprite, Color.white, skin);
            EqUi.Stretch(glow.rectTransform);
            if (glowSprite != null && glowSprite.border.sqrMagnitude > 0f)
            {
                glow.type = Image.Type.Sliced;
                glow.pixelsPerUnitMultiplier = EqUi.RefPixelsPerUnit / glowSprite.pixelsPerUnit;
            }
            glow.color = new Color(1f, 1f, 1f, 0f);

            dot = EqUi.Img("Dot", rt, empty, EqUi.Muted, skin);
            dot.type = Image.Type.Simple;
            dot.preserveAspect = false;
            var drt = dot.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(9f, 9f);
            drt.anchoredPosition = new Vector2(7.5f, 0f);

            label = EqUi.Txt("Label", rt, font, fontSize, TextIdle, TextAnchor.MiddleLeft, EqUi.Ink);
            EqUi.StretchOffsets(label.rectTransform, 15f, 0f, 2f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;

            strike = EqUi.Img("Strike", rt, null, EqUi.A(TextDone, 0.55f), skin);
            var srt = strike.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = new Vector2(15f, 0f);
            srt.sizeDelta = new Vector2(0f, 1f);
            strike.enabled = false;
        }

        public void SetTimings(float pop, float highlight)
        {
            popSeconds = Mathf.Max(0.05f, pop);
            highlightSeconds = Mathf.Max(0.05f, highlight);
        }

        /// <summary>Metin + isteğe bağlı ara ilerleme ("1/2").</summary>
        public void SetLabel(string text, string progressSuffix)
        {
            baseLabel = text ?? "";
            suffix = progressSuffix ?? "";
            RefreshText();
        }

        /// <summary>Etiket sığmazsa yazıyı küçültür (en az minSize).</summary>
        public void FitLabel(int minSize)
        {
            if (label == null || LabelWidth <= 1f) return;
            int size = label.fontSize;
            while (size > minSize && LabelPreferredWidth > LabelWidth)
            {
                size--;
                label.fontSize = size;
            }
        }

        public void SetAlpha(float a)
        {
            if (group != null) group.alpha = a;
        }

        /// <summary>Durumu değiştirir. animate: tamamlanınca tik pop + yeşil parlama.</summary>
        public void SetState(State s, bool animate)
        {
            if (Current == State.Completed && s != State.Completed) return; // tamamlanan görev geri açılmaz
            bool becameDone = s == State.Completed && Current != State.Completed;
            Current = s;
            switch (s)
            {
                case State.Completed: dot.sprite = dotDone; dot.color = dotDone != null ? Color.white : EqUi.Green; break;
                case State.Active: dot.sprite = dotActive; dot.color = dotActive != null ? Color.white : EqUi.Amber; break;
                default: dot.sprite = dotEmpty; dot.color = dotEmpty != null ? Color.white : EqUi.Muted; break;
            }
            if (becameDone)
            {
                suffix = "";
                if (animate)
                {
                    popT = 0f;
                    flashT = 0f;
                }
            }
            RefreshText();
            if (!animate || !becameDone) dot.rectTransform.localScale = Vector3.one;
        }

        private void RefreshText()
        {
            if (label == null) return;
            string t = baseLabel;
            if (!string.IsNullOrEmpty(suffix) && Current != State.Completed)
                t += " <color=#" + EqUi.HexOf(SuffixColor) + ">" + suffix + "</color>";
            label.text = t;
            label.color = Current == State.Completed ? TextDone : Current == State.Active ? TextActive : TextIdle;
            if (strike != null)
            {
                strike.enabled = UseStrike && Current == State.Completed;
                if (strike.enabled)
                {
                    float w = Mathf.Min(EqUi.PreferredWidth(label, baseLabel), Mathf.Max(0f, LabelWidth));
                    strike.rectTransform.sizeDelta = new Vector2(Mathf.Round(w), 1f);
                }
            }
        }

        /// <summary>HUD her karede çağırır (ölçeksiz zaman). clock: nabız için ortak saat.</summary>
        public void Tick(float dt, float clock)
        {
            if (dot == null) return;
            float scale = 1f;
            if (popT >= 0f)
            {
                popT += dt / popSeconds;
                // 0 → 1.35 → 1 (pop)
                scale = popT < 0.6f ? Mathf.Lerp(0.2f, 1.35f, EqUi.OutCubic(popT / 0.6f)) : Mathf.Lerp(1.35f, 1f, (popT - 0.6f) / 0.4f);
                if (popT >= 1f) { popT = -1f; scale = 1f; }
            }
            else if (Current == State.Active)
            {
                scale = 1f + 0.12f * (0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 2f * 0.9f));
            }
            dot.rectTransform.localScale = new Vector3(scale, scale, 1f);

            float glowA = 0f;
            Color glowC = EqUi.Green;
            if (flashT >= 0f)
            {
                flashT += dt / highlightSeconds;
                glowA = 0.75f * (1f - Mathf.Clamp01(flashT));
                if (flashT >= 1f) flashT = -1f;
            }
            else if (Current == State.Active)
            {
                glowC = EqUi.Amber;
                glowA = 0.10f + 0.06f * Mathf.Sin(clock * Mathf.PI * 2f * 0.9f);
            }
            glowC.a = glowA;
            glow.color = glowC;
        }
    }
}
