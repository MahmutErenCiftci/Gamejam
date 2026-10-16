using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AfetUyanisi.News
{
    // Sel F03 TV haber sekansı (NEWS-01).
    // Akış: uyarı ekranı -> haber satırları (daktilo) + kayan bant -> bitince OnFinished.
    // Devam: E / Space / Enter / sol tık. Bölüm sahnesi Play() çağırır ve Finished'i dinler.
    public class NewsBroadcast : MonoBehaviour
    {
        [Header("Veri")]
        [SerializeField] TextAsset newsJson;

        [Header("Ekranlar")]
        [SerializeField] GameObject root;
        [SerializeField] GameObject warningScreen;
        [SerializeField] GameObject newsScreen;

        [Header("Uyarı ekranı")]
        [SerializeField] Text warningTitleText;
        [SerializeField] Text warningLevelText;

        [Header("Haber ekranı")]
        [SerializeField] Text channelText;
        [SerializeField] Text headlineText;
        [SerializeField] Text bodyText;
        [SerializeField] Text fictionNoticeText;
        [SerializeField] Text tickerText;
        [SerializeField] GameObject continueHint;

        [Header("Zamanlama")]
        [SerializeField] float warningDuration = 2.5f;
        [SerializeField] float charsPerSecond = 40f;
        [SerializeField] float tickerSpeed = 120f;
        [SerializeField] bool playOnStart;

        public UnityEvent onFinished;
        public event Action Finished;

        public bool IsPlaying { get; private set; }

        NewsBroadcastData data;
        RectTransform tickerRect;
        float tickerStartX;

        void Awake()
        {
            if (tickerText != null) tickerRect = tickerText.rectTransform;
            if (root != null) root.SetActive(false);
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            if (IsPlaying) return;
            data = NewsBroadcastData.FromJson(newsJson);
            if (data == null) return;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            root.SetActive(true);

            warningTitleText.text = data.warningTitle;
            warningLevelText.text = data.warningLevel;
            warningScreen.SetActive(true);
            newsScreen.SetActive(false);

            for (float t = 0f; t < warningDuration; t += Time.deltaTime)
            {
                if (PressedThisFrame()) break;
                yield return null;
            }
            yield return null;

            warningScreen.SetActive(false);
            newsScreen.SetActive(true);
            channelText.text = data.channelName;
            headlineText.text = data.headline;
            fictionNoticeText.text = data.fictionNotice;
            tickerText.text = data.ticker;
            ResetTicker();

            foreach (var line in data.lines)
            {
                yield return TypeLine(line);
                continueHint.SetActive(true);
                while (!PressedThisFrame()) yield return null;
                continueHint.SetActive(false);
                yield return null;
            }

            root.SetActive(false);
            IsPlaying = false;
            onFinished?.Invoke();
            Finished?.Invoke();
        }

        IEnumerator TypeLine(string line)
        {
            continueHint.SetActive(false);
            bodyText.text = "";
            float shown = 0f;
            while (shown < line.Length)
            {
                // Yazarken basılırsa satırı tamamla.
                if (PressedThisFrame()) break;
                shown += charsPerSecond * Time.deltaTime;
                bodyText.text = line.Substring(0, Mathf.Min(line.Length, (int)shown));
                yield return null;
            }
            bodyText.text = line;
            yield return null;
        }

        void Update()
        {
            if (!IsPlaying || tickerRect == null || !newsScreen.activeSelf) return;

            var pos = tickerRect.anchoredPosition;
            pos.x -= tickerSpeed * Time.deltaTime;
            if (pos.x < -tickerText.preferredWidth) pos.x = tickerStartX;
            tickerRect.anchoredPosition = pos;
        }

        void ResetTicker()
        {
            if (tickerRect == null) return;
            // Bant sağ kenardan girer; Canvas boyutu ancak ekran açıkken kesinleşir.
            Canvas.ForceUpdateCanvases();
            tickerStartX = ((RectTransform)tickerRect.parent).rect.width;
            tickerRect.anchoredPosition = new Vector2(tickerStartX, tickerRect.anchoredPosition.y);
        }

        static bool PressedThisFrame()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
    }
}
