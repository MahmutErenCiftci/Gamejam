using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Dialogue
{
    // Karar panelindeki tek seçenek kartı: tuş rozeti + metin + sonuç işareti (✓ / ✗).
    // Fareyle üstüne gelince büyür; yanlışsa sallanır, doğruysa zıplar.
    public class DecisionChoiceView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum State { Normal, Correct, Wrong, Dimmed }

        [SerializeField] Button button;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image background;
        [SerializeField] Image badge;
        [SerializeField] Text numberText;
        [SerializeField] Text labelText;
        [SerializeField] Text markText;

        [SerializeField] Color normalBg = Color.white;
        [SerializeField] Color normalBadge = new Color(0.16f, 0.2f, 0.32f, 1f);
        [SerializeField] Color correctColor = new Color(0.09f, 0.66f, 0.34f, 1f);
        [SerializeField] Color wrongColor = new Color(0.91f, 0.22f, 0.17f, 1f);
        [SerializeField] float hoverScale = 1.06f;

        public Button Button => button;

        float targetScale = 1f;
        float wiggleTime = -1f;
        float bounceTime = -1f;

        public void Bind(int number, string text)
        {
            numberText.text = number.ToString();
            labelText.text = text;
            SetState(State.Normal);
        }

        public void SetState(State state)
        {
            group.alpha = state == State.Dimmed ? 0.55f : 1f;
            markText.gameObject.SetActive(state == State.Correct || state == State.Wrong);
            targetScale = 1f;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;

            switch (state)
            {
                case State.Correct:
                    background.color = Color.Lerp(normalBg, correctColor, 0.3f);
                    badge.color = correctColor;
                    markText.text = "✓";
                    markText.color = correctColor;
                    bounceTime = 0f;
                    break;
                case State.Wrong:
                    background.color = Color.Lerp(normalBg, wrongColor, 0.3f);
                    badge.color = wrongColor;
                    markText.text = "✗";
                    markText.color = wrongColor;
                    wiggleTime = 0f;
                    break;
                default:
                    background.color = normalBg;
                    badge.color = normalBadge;
                    wiggleTime = bounceTime = -1f;
                    break;
            }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (button.interactable) targetScale = hoverScale;
        }

        public void OnPointerExit(PointerEventData e) => targetScale = 1f;

        void Update()
        {
            float s = transform.localScale.x;
            if (bounceTime >= 0f)
            {
                bounceTime += Time.deltaTime;
                float k = bounceTime / 0.35f;
                s = 1f + Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.1f;
                if (k >= 1f) bounceTime = -1f;
            }
            else
            {
                s = Mathf.Lerp(s, targetScale, 1f - Mathf.Exp(-18f * Time.deltaTime));
            }
            transform.localScale = new Vector3(s, s, 1f);

            if (wiggleTime >= 0f)
            {
                wiggleTime += Time.deltaTime;
                float k = wiggleTime / 0.4f;
                float angle = Mathf.Sin(k * Mathf.PI * 6f) * 4f * (1f - k);
                transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                if (k >= 1f) { wiggleTime = -1f; transform.localRotation = Quaternion.identity; }
            }
        }
    }
}
