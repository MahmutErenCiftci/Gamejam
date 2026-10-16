using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.News
{
    // Tek bir söylenti/duyuru kartının görünümü. Mantık RumorCards'ta.
    public class RumorCardView : MonoBehaviour
    {
        public enum State { Normal, Rejected, Accepted }

        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] Text keyText;
        [SerializeField] Text sourceText;
        [SerializeField] Text bodyText;
        [SerializeField] GameObject stamp;
        [SerializeField] Text stampText;

        [SerializeField] Color normalColor = new Color(0.97f, 0.95f, 0.88f, 1f);
        [SerializeField] Color rejectedColor = new Color(0.75f, 0.72f, 0.68f, 1f);
        [SerializeField] Color acceptedColor = new Color(0.82f, 0.95f, 0.84f, 1f);

        public Button Button => button;

        public void Bind(RumorCardData card, int number)
        {
            keyText.text = number.ToString();
            sourceText.text = card.sourceLabel;
            bodyText.text = card.text;
            SetState(State.Normal);
        }

        public void SetState(State state)
        {
            background.color = state == State.Rejected ? rejectedColor
                             : state == State.Accepted ? acceptedColor
                             : normalColor;
            stamp.SetActive(state != State.Normal);
            if (state == State.Rejected)
            {
                stampText.text = "DOĞRULANMAMIŞ";
                stampText.color = new Color(0.75f, 0.1f, 0.1f, 1f);
            }
            else if (state == State.Accepted)
            {
                stampText.text = "RESMÎ KAYNAK";
                stampText.color = new Color(0.1f, 0.5f, 0.2f, 1f);
            }
        }
    }
}
