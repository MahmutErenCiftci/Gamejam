using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.News
{
    // Tek bir söylenti/duyuru kartının görünümü. Mantık RumorCards'ta.
    // Fareyle üstüne gelince büyür; reddedilince sallanır, kabul edilince zıplar.
    public class RumorCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum State { Normal, Rejected, Accepted }

        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] Text keyText;
        [SerializeField] Image sourcePill;
        [SerializeField] Text sourceText;
        [SerializeField] Text bodyText;
        [SerializeField] GameObject stamp;
        [SerializeField] Image stampFrame;
        [SerializeField] Text stampText;

        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color rejectedColor = new Color(0.82f, 0.8f, 0.78f, 1f);
        [SerializeField] Color acceptedColor = new Color(0.8f, 0.95f, 0.85f, 1f);
        [SerializeField] Color rumorPill = new Color(0.45f, 0.45f, 0.52f, 1f);
        [SerializeField] Color officialPill = new Color(0.09f, 0.55f, 0.85f, 1f);
        [SerializeField] Color wrongColor = new Color(0.91f, 0.22f, 0.17f, 1f);
        [SerializeField] Color correctColor = new Color(0.09f, 0.66f, 0.34f, 1f);
        [SerializeField] float hoverScale = 1.05f;

        public Button Button => button;

        float targetScale = 1f;
        float wiggleTime = -1f;
        float bounceTime = -1f;

        public void Bind(RumorCardData card, int number)
        {
            keyText.text = number.ToString();
            sourceText.text = card.sourceLabel;
            sourcePill.color = card.official ? officialPill : rumorPill;
            bodyText.text = card.text;
            SetState(State.Normal);
        }

        public void SetState(State state)
        {
            background.color = state == State.Rejected ? rejectedColor
                             : state == State.Accepted ? acceptedColor
                             : normalColor;
            stamp.SetActive(state != State.Normal);
            transform.localRotation = Quaternion.identity;
            targetScale = 1f;

            if (state == State.Rejected)
            {
                stampText.text = "DOĞRULANMAMIŞ";
                stampFrame.color = wrongColor;
                wiggleTime = 0f;
            }
            else if (state == State.Accepted)
            {
                stampText.text = "RESMÎ KAYNAK";
                stampFrame.color = correctColor;
                bounceTime = 0f;
            }
            else
            {
                wiggleTime = bounceTime = -1f;
            }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (button.interactable && !stamp.activeSelf) targetScale = hoverScale;
        }

        public void OnPointerExit(PointerEventData e) => targetScale = 1f;

        void Update()
        {
            float s = transform.localScale.x;
            if (bounceTime >= 0f)
            {
                bounceTime += Time.deltaTime;
                float k = bounceTime / 0.35f;
                s = 1f + Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.08f;
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
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 6f) * 3f * (1f - k));
                if (k >= 1f) { wiggleTime = -1f; transform.localRotation = Quaternion.identity; }
            }
        }
    }
}
