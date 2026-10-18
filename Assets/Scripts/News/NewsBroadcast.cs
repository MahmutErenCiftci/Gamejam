using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AfetUyanisi.News
{
    // Sel F03 TV haber sekansı (NEWS-01), görsel: visuals/previews/PREVIEW_News_Concept.png.
    // TV'de spiker + yağış haritası + uyarı ölçeği + SON DAKİKA manşeti; spikerin sözleri
    // altyazıda daktiloyla yazılır, yazarken ağzı oynar. Bitince onFinished / Finished.
    // Devam: E / Space / Enter / sol tık. Bölüm sahnesi Play() çağırır.
    public class NewsBroadcast : MonoBehaviour
    {
        [Header("Veri")]
        [SerializeField] TextAsset newsJson;

        [Header("UI")]
        [SerializeField] GameObject root;
        [SerializeField] Text channelText;
        [SerializeField] Text programText;
        [SerializeField] Text fictionNoticeText;
        [SerializeField] Text breakingText;
        [SerializeField] Text headlineText;
        [SerializeField] Text subheadlineText;
        [SerializeField] Text speakerText;
        [SerializeField] Text subtitleText;
        [SerializeField] GameObject continueHint;

        [Header("Uyarı ölçeği")]
        [SerializeField] Text legendTitleText;
        [SerializeField] Text legendNoteText;
        [SerializeField] Image[] legendSwatches;
        [SerializeField] Text[] legendLabels;

        [Header("Spiker")]
        [SerializeField] Image anchorImage;
        [SerializeField] Sprite anchorIdle;
        [SerializeField] Sprite anchorTalk;
        [SerializeField] float mouthInterval = 0.12f;

        [Header("Zamanlama")]
        [SerializeField] float charsPerSecond = 40f;
        [SerializeField] bool playOnStart;

        public UnityEvent onFinished;
        public event Action Finished;

        public bool IsPlaying { get; private set; }

        NewsBroadcastData data;

        void Awake()
        {
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

            channelText.text = data.channelName;
            programText.text = data.programName;
            fictionNoticeText.text = data.fictionNotice;
            breakingText.text = data.breakingLabel;
            headlineText.text = data.headline;
            subheadlineText.text = data.subheadline;
            speakerText.text = data.speakerName;
            subtitleText.text = "";
            continueHint.SetActive(false);
            FillLegend();

            StartCoroutine(Run());
        }

        void FillLegend()
        {
            legendTitleText.text = data.legendTitle;
            legendNoteText.text = data.legendNote;
            int count = data.legend == null ? 0 : data.legend.Length;
            for (int i = 0; i < legendSwatches.Length; i++)
            {
                bool used = i < count;
                legendSwatches[i].transform.parent.gameObject.SetActive(used);
                if (!used) continue;
                legendLabels[i].text = data.legend[i].label;
                if (ColorUtility.TryParseHtmlString(data.legend[i].color, out var c))
                    legendSwatches[i].color = c;
            }
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            root.SetActive(true);
            yield return null;

            foreach (var line in data.lines)
            {
                yield return TypeLine(line);
                continueHint.SetActive(true);
                while (!NewsInput.ContinuePressed()) yield return null;
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
            subtitleText.text = "";
            float shown = 0f, mouthTimer = 0f;
            bool mouthOpen = false;
            while (shown < line.Length)
            {
                // Yazarken basılırsa satırı tamamla.
                if (NewsInput.ContinuePressed()) break;
                shown += charsPerSecond * Time.deltaTime;
                subtitleText.text = line.Substring(0, Mathf.Min(line.Length, (int)shown));

                mouthTimer += Time.deltaTime;
                if (mouthTimer >= mouthInterval)
                {
                    mouthTimer = 0f;
                    mouthOpen = !mouthOpen;
                    SetMouth(mouthOpen);
                }
                yield return null;
            }
            subtitleText.text = line;
            SetMouth(false);
            yield return null;
        }

        void SetMouth(bool open)
        {
            if (anchorImage == null) return;
            var sprite = open ? anchorTalk : anchorIdle;
            if (sprite != null) anchorImage.sprite = sprite;
        }
    }
}
