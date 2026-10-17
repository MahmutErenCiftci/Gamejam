using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AfetUyanisi.News
{
    // Sel F05 söylenti/haber kartları (NEWS-03).
    // Oyuncu 1..N tuşu ya da tıklama ile güvendiği kartı seçer. Söylenti seçilirse kart
    // "DOĞRULANMAMIŞ" olur ve neden güvenilmediği anlatılır; ceza yok, tekrar seçer.
    // Resmî duyuru seçilince geri bildirim + devam -> onFinished(yanlış deneme sayısı).
    // Not: Tıklama için sahnede EventSystem gerekir (bölüm sahnesinde olmalı); tuşlar onsuz da çalışır.
    public class RumorCards : MonoBehaviour
    {
        [Serializable] public class FinishedEvent : UnityEvent<int> { }

        [Header("Veri")]
        [SerializeField] TextAsset rumorsJson;

        [Header("UI")]
        [SerializeField] GameObject root;
        [SerializeField] Text promptText;
        [SerializeField] Text feedbackText;
        [SerializeField] Image feedbackFrame;
        [SerializeField] Text feedbackIcon;
        [SerializeField] Color correctColor = new Color(0.09f, 0.66f, 0.34f, 1f);
        [SerializeField] Color wrongColor = new Color(0.91f, 0.22f, 0.17f, 1f);
        [SerializeField] Text fictionNoticeText;
        [SerializeField] RumorCardView[] cardViews;
        [SerializeField] GameObject continueHint;

        [SerializeField] bool playOnStart;

        public FinishedEvent onFinished;
        public event Action<int> Finished;

        public bool IsPlaying { get; private set; }

        RumorCardsData data;
        bool choosing;
        int wrongAttempts;

        void Awake()
        {
            if (root != null) root.SetActive(false);
            for (int i = 0; i < cardViews.Length; i++)
            {
                int index = i;
                cardViews[i].Button.onClick.AddListener(() => Choose(index));
            }
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            if (IsPlaying) return;
            data = RumorCardsData.FromJson(rumorsJson);
            if (data == null) return;
            if (data.cards.Length > cardViews.Length)
                Debug.LogWarning($"[News] {data.cards.Length} kart var ama prefabta {cardViews.Length} yuva var; fazlası gösterilmez.");

            promptText.text = data.prompt;
            fictionNoticeText.text = data.fictionNotice;
            feedbackText.text = "";
            if (feedbackFrame != null) feedbackFrame.gameObject.SetActive(false);
            continueHint.SetActive(false);
            for (int i = 0; i < cardViews.Length; i++)
            {
                bool used = i < data.cards.Length;
                cardViews[i].gameObject.SetActive(used);
                if (used) cardViews[i].Bind(data.cards[i], i + 1);
            }

            wrongAttempts = 0;
            choosing = true;
            IsPlaying = true;
            root.SetActive(true);
        }

        void Update()
        {
            if (!choosing) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            for (int i = 0; i < Mathf.Min(data.cards.Length, cardViews.Length, 9); i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Choose(i);
                    return;
                }
            }
        }

        void Choose(int index)
        {
            if (!choosing || index >= data.cards.Length) return;
            var card = data.cards[index];
            feedbackText.text = card.feedback;
            if (feedbackFrame != null)
            {
                feedbackFrame.gameObject.SetActive(true);
                feedbackFrame.color = card.official ? correctColor : wrongColor;
                feedbackIcon.text = card.official ? "✓" : "!";
                feedbackIcon.color = feedbackFrame.color;
            }

            if (!card.official)
            {
                wrongAttempts++;
                cardViews[index].SetState(RumorCardView.State.Rejected);
                return;
            }

            choosing = false;
            cardViews[index].SetState(RumorCardView.State.Accepted);
            StartCoroutine(WaitAndFinish());
        }

        IEnumerator WaitAndFinish()
        {
            // Seçimi yapan tık/tuş aynı karede "devam" sayılmasın.
            yield return null;
            continueHint.SetActive(true);
            while (!NewsInput.ContinuePressed()) yield return null;

            root.SetActive(false);
            IsPlaying = false;
            onFinished?.Invoke(wrongAttempts);
            Finished?.Invoke(wrongAttempts);
        }
    }
}
