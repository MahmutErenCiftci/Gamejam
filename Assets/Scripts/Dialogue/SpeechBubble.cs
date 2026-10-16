using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Dialogue
{
    // Konuşan karakterin başının üstünde duran balon (konsept: PREVIEW_Mother_Concept / RumorTalk).
    // Boyutu satırın tamamına göre baştan ayarlanır, daktilo yazarken balon büyümez.
    // Ekran dışına taşmaz; kuyruk her zaman konuşana doğru bakar. Açılırken hafifçe "pop" yapar.
    public class SpeechBubble : MonoBehaviour
    {
        [SerializeField] RectTransform bubble;
        [SerializeField] RectTransform tail;
        [SerializeField] Text text;
        [SerializeField] RectTransform continueHint;
        [SerializeField] float maxWidth = 560f;
        [SerializeField] float minWidth = 160f;
        [SerializeField] Vector2 padding = new Vector2(30f, 24f);
        [SerializeField] float screenMargin = 16f;
        [SerializeField] float tailInset = 34f;
        [SerializeField] float popDuration = 0.18f;
        [SerializeField] float hintBob = 4f;

        public Text Text => text;
        public GameObject ContinueHint => continueHint.gameObject;

        RectTransform canvasRect;
        DialogueSpeaker target;
        DialogueSpeaker lastTarget;
        Vector2 hintBase;
        bool hintBaseSet;

        // Prefabta kapalı durur; ilk Show'da açılır.
        public void Show(DialogueSpeaker speaker, string fullLine)
        {
            bool newSpeaker = speaker != lastTarget || !gameObject.activeSelf;
            target = lastTarget = speaker;
            gameObject.SetActive(true);
            if (canvasRect == null) canvasRect = (RectTransform)GetComponentInParent<Canvas>().transform;
            if (!hintBaseSet) { hintBase = continueHint.anchoredPosition; hintBaseSet = true; }

            Resize(fullLine);
            text.text = "";
            continueHint.gameObject.SetActive(false);
            Follow();

            // Aynı kişi konuşmaya devam ediyorsa balon zıplamasın.
            StopAllCoroutines();
            if (newSpeaker) StartCoroutine(Pop());
            else bubble.localScale = Vector3.one;
        }

        public void Hide()
        {
            target = lastTarget = null;
            gameObject.SetActive(false);
        }

        void Resize(string fullLine)
        {
            var gen = text.cachedTextGeneratorForLayout;
            float unwrapped = gen.GetPreferredWidth(fullLine, text.GetGenerationSettings(Vector2.zero)) / text.pixelsPerUnit;
            float inner = Mathf.Clamp(unwrapped + 2f, minWidth - padding.x * 2f, maxWidth - padding.x * 2f);
            float height = gen.GetPreferredHeight(fullLine, text.GetGenerationSettings(new Vector2(inner, 0f))) / text.pixelsPerUnit;
            bubble.sizeDelta = new Vector2(inner + padding.x * 2f, height + padding.y * 2f);
        }

        IEnumerator Pop()
        {
            for (float t = 0f; t < popDuration; t += Time.deltaTime)
            {
                bubble.localScale = Vector3.one * EaseOutBack(t / popDuration, 0.7f);
                yield return null;
            }
            bubble.localScale = Vector3.one;
        }

        static float EaseOutBack(float x, float from)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
            return Mathf.LerpUnclamped(from, 1f, e);
        }

        void LateUpdate()
        {
            if (target != null) Follow();
            if (continueHint.gameObject.activeSelf)
                continueHint.anchoredPosition = hintBase + new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.time * 5f)) * hintBob);
        }

        void Follow()
        {
            var cam = Camera.main;
            if (cam == null || target == null) return;

            Vector2 screen = cam.WorldToScreenPoint(target.HeadPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var head);

            // Balon: pivot alt-orta; kafanın üstüne kuyruk yüksekliği kadar yukarı.
            var size = bubble.sizeDelta;
            var half = canvasRect.rect.size * 0.5f;
            float x = Mathf.Clamp(head.x, -half.x + size.x * 0.5f + screenMargin, half.x - size.x * 0.5f - screenMargin);
            float y = Mathf.Min(head.y + tail.sizeDelta.y, half.y - size.y - screenMargin);
            bubble.anchoredPosition = new Vector2(x, y);

            // Kuyruk konuşanın üstünde kalır ama balonun köşelerinden taşmaz.
            float tailX = Mathf.Clamp(head.x - x, -size.x * 0.5f + tailInset, size.x * 0.5f - tailInset);
            tail.anchoredPosition = new Vector2(tailX, tail.anchoredPosition.y);
        }
    }
}
