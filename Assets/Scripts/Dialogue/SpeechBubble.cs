using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Dialogue
{
    // Konuşan karakterin başının üstünde duran balon (konsept: PREVIEW_Mother_Concept / RumorTalk).
    // Boyutu satırın tamamına göre baştan ayarlanır, daktilo yazarken balon büyümez.
    // Ekran dışına taşmaz; kuyruk her zaman konuşana doğru bakar.
    public class SpeechBubble : MonoBehaviour
    {
        [SerializeField] RectTransform bubble;
        [SerializeField] RectTransform tail;
        [SerializeField] Text text;
        [SerializeField] GameObject continueHint;
        [SerializeField] float maxWidth = 560f;
        [SerializeField] float minWidth = 160f;
        [SerializeField] Vector2 padding = new Vector2(28f, 22f);
        [SerializeField] float screenMargin = 16f;
        [SerializeField] float tailInset = 30f;

        public Text Text => text;
        public GameObject ContinueHint => continueHint;

        RectTransform canvasRect;
        DialogueSpeaker target;

        // Prefabta kapalı durur; ilk Show'da açılır.
        public void Show(DialogueSpeaker speaker, string fullLine)
        {
            target = speaker;
            gameObject.SetActive(true);
            if (canvasRect == null) canvasRect = (RectTransform)GetComponentInParent<Canvas>().transform;
            Resize(fullLine);
            text.text = "";
            continueHint.SetActive(false);
            Follow();
        }

        public void Hide()
        {
            target = null;
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

        void LateUpdate()
        {
            if (target != null) Follow();
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
