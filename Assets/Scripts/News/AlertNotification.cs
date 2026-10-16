using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AfetUyanisi.News
{
    // Deprem EQ01→EQ02 kurgu telefon bildirimi (NEWS-02).
    // Akış: telefon alttan kayarak gelir -> titrer (onShown: ses burada çalınabilir) ->
    // süre dolar ya da tuşa basılır -> (hideOnFinish ise) geri kayar -> onFinished.
    // Bildirim kurgudur; uyarının belirli bir süre önce geldiği iddia edilmez.
    public class AlertNotification : MonoBehaviour
    {
        [Header("Veri")]
        [SerializeField] TextAsset alertJson;

        [Header("UI")]
        [SerializeField] GameObject root;
        [SerializeField] RectTransform phone;
        [SerializeField] Text appNameText;
        [SerializeField] Text timeText;
        [SerializeField] Text titleText;
        [SerializeField] Text bodyText;
        [SerializeField] Text fictionNoticeText;

        [Header("Zamanlama")]
        [SerializeField] float slideDuration = 0.35f;
        [SerializeField] float shakeDuration = 0.6f;
        [SerializeField] float shakeAmplitude = 12f;
        [SerializeField] float holdDuration = 3f;
        [SerializeField] bool hideOnFinish = true;
        [SerializeField] bool playOnStart;

        public UnityEvent onShown;
        public UnityEvent onFinished;
        public event Action Shown;
        public event Action Finished;

        public bool IsPlaying { get; private set; }

        Vector2 shownPos;

        void Awake()
        {
            if (phone != null) shownPos = phone.anchoredPosition;
            if (root != null) root.SetActive(false);
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            if (IsPlaying) return;
            var data = AlertNotificationData.FromJson(alertJson);
            if (data == null) return;

            appNameText.text = data.appName;
            timeText.text = data.time;
            titleText.text = data.title;
            bodyText.text = data.body;
            fictionNoticeText.text = data.fictionNotice;
            StartCoroutine(Run());
        }

        // Bildirim ekranda bırakıldıysa (hideOnFinish = false) bölüm sahnesi kapatır.
        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            root.SetActive(true);

            var hiddenPos = shownPos - new Vector2(0f, phone.rect.height + shownPos.y + 50f);
            yield return Slide(hiddenPos, shownPos);

            onShown?.Invoke();
            Shown?.Invoke();

            for (float t = 0f; t < shakeDuration; t += Time.deltaTime)
            {
                float fade = 1f - t / shakeDuration;
                phone.anchoredPosition = shownPos + new Vector2(Mathf.Sin(t * 60f) * shakeAmplitude * fade, 0f);
                yield return null;
            }
            phone.anchoredPosition = shownPos;

            for (float t = 0f; t < holdDuration; t += Time.deltaTime)
            {
                if (NewsInput.ContinuePressed()) break;
                yield return null;
            }
            yield return null;

            if (hideOnFinish)
            {
                yield return Slide(shownPos, hiddenPos);
                root.SetActive(false);
            }

            IsPlaying = false;
            onFinished?.Invoke();
            Finished?.Invoke();
        }

        IEnumerator Slide(Vector2 from, Vector2 to)
        {
            for (float t = 0f; t < slideDuration; t += Time.deltaTime)
            {
                phone.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / slideDuration));
                yield return null;
            }
            phone.anchoredPosition = to;
        }
    }
}
