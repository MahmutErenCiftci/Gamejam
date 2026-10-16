using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AfetUyanisi.Dialogue
{
    // Diyalog ve karar oynatıcı. Satırın konuşanı sahnede bir DialogueSpeaker ise satır onun
    // başının üstünde balonda, değilse (anlatıcı, sonuç metni) alttaki kutuda gösterilir.
    // Bölüm sahnesi:
    //   runner.PlayScene("F03_MOTHER", () => ...);            // satır satır diyalog
    //   runner.ShowDecision("F_D1", choice => ...);           // seçenek + geri bildirim
    // choice == null ise süre doldu (time_limit > 0 olan kararlarda).
    // Satır: E / Space / Enter / sol tık ile ilerler, yazarken basılırsa satır tamamlanır.
    // Seçenek: 1..N tuşları ya da tıklama (tıklama için sahnede EventSystem gerekir).
    public class DialogueRunner : MonoBehaviour
    {
        [Serializable] public class SceneEvent : UnityEvent<string> { }
        [Serializable] public class DecisionEvent : UnityEvent<string, string, bool> { }

        [Header("Veri")]
        [SerializeField] TextAsset storyJson;

        [Header("Konuşma balonu")]
        [SerializeField] SpeechBubble bubble;

        [Header("Diyalog kutusu (konuşanı sahnede olmayan satırlar)")]
        [SerializeField] GameObject dialoguePanel;
        [SerializeField] GameObject speakerPlate;
        [SerializeField] Text speakerText;
        [SerializeField] Text lineText;
        [SerializeField] GameObject continueHint;

        [Header("Karar paneli")]
        [SerializeField] GameObject decisionPanel;
        [SerializeField] CanvasGroup decisionGroup;
        [SerializeField] RectTransform decisionBox;
        [SerializeField] Text promptText;
        [SerializeField] DecisionChoiceView[] choices;
        [SerializeField] GameObject timerBadge;
        [SerializeField] Image timerFill;
        [SerializeField] Text timerText;
        [SerializeField] GameObject feedbackPanel;
        [SerializeField] Image feedbackAccent;
        [SerializeField] Text feedbackIcon;
        [SerializeField] Text feedbackText;
        [SerializeField] GameObject decisionContinueHint;
        [SerializeField] Color correctColor = new Color(0.09f, 0.66f, 0.34f, 1f);
        [SerializeField] Color wrongColor = new Color(0.91f, 0.22f, 0.17f, 1f);
        [SerializeField] Color timerStartColor = new Color(0.95f, 0.77f, 0.2f, 1f);
        [SerializeField] float openDuration = 0.2f;

        [Header("Zamanlama")]
        [SerializeField] float charsPerSecond = 45f;

        [Header("Test")]
        [SerializeField] string playSceneOnStart;

        // Sahne id'si
        public SceneEvent onSceneFinished;
        // Karar id'si, seçenek id'si (süre dolduysa boş), doğru mu
        public DecisionEvent onDecisionMade;
        public event Action<string> SceneFinished;
        public event Action<string, StoryChoice> DecisionMade;

        public bool IsBusy { get; private set; }
        public StoryData Data => data ??= StoryData.FromJson(storyJson);

        StoryData data;
        int pickedIndex = -1;

        void Awake()
        {
            dialoguePanel.SetActive(false);
            decisionPanel.SetActive(false);
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i].Button.onClick.AddListener(() => pickedIndex = index);
            }
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(playSceneOnStart)) PlayScene(playSceneOnStart);
        }

        public void PlayScene(string sceneId, Action onDone = null)
        {
            if (IsBusy) { Debug.LogWarning($"[Dialogue] Meşgulken '{sceneId}' istendi, yok sayıldı."); return; }
            var scene = Data?.FindScene(sceneId);
            if (scene == null) return;
            StartCoroutine(RunScene(scene, onDone));
        }

        public void ShowDecision(string decisionId, Action<StoryChoice> onDone = null)
        {
            if (IsBusy) { Debug.LogWarning($"[Dialogue] Meşgulken '{decisionId}' istendi, yok sayıldı."); return; }
            var decision = Data?.FindDecision(decisionId);
            if (decision == null) return;
            StartCoroutine(RunDecision(decision, onDone));
        }

        IEnumerator RunScene(StoryScene scene, Action onDone)
        {
            IsBusy = true;
            yield return null;

            foreach (var line in scene.lines)
            {
                Text target;
                GameObject hint;
                var speaker = bubble != null ? DialogueSpeaker.Find(line.speaker) : null;
                if (speaker != null)
                {
                    dialoguePanel.SetActive(false);
                    bubble.Show(speaker, line.text);
                    target = bubble.Text;
                    hint = bubble.ContinueHint;
                }
                else
                {
                    if (bubble != null) bubble.Hide();
                    dialoguePanel.SetActive(true);
                    bool hasSpeaker = !string.IsNullOrEmpty(line.speaker);
                    speakerPlate.SetActive(hasSpeaker);
                    speakerText.text = line.speaker;
                    target = lineText;
                    hint = continueHint;
                }

                yield return TypeLine(target, hint, line.text);
                hint.SetActive(true);
                while (!DialogueInput.ContinuePressed()) yield return null;
                hint.SetActive(false);
                yield return null;
            }

            if (bubble != null) bubble.Hide();
            dialoguePanel.SetActive(false);
            IsBusy = false;
            onDone?.Invoke();
            onSceneFinished?.Invoke(scene.id);
            SceneFinished?.Invoke(scene.id);
        }

        IEnumerator TypeLine(Text target, GameObject hint, string text)
        {
            hint.SetActive(false);
            target.text = "";
            int length = RichTextTyper.VisibleLength(text);
            float shown = 0f;
            while (shown < length)
            {
                // Yazarken basılırsa satırı tamamla.
                if (DialogueInput.ContinuePressed()) break;
                shown += charsPerSecond * Time.deltaTime;
                target.text = RichTextTyper.Substring(text, (int)shown);
                yield return null;
            }
            target.text = text;
            yield return null;
        }

        IEnumerator RunDecision(StoryDecision decision, Action<StoryChoice> onDone)
        {
            IsBusy = true;
            int count = Mathf.Min(decision.choices.Length, choices.Length);
            if (decision.choices.Length > choices.Length)
                Debug.LogWarning($"[Dialogue] '{decision.id}' {decision.choices.Length} seçenekli, prefabta {choices.Length} yuva var.");

            promptText.text = decision.prompt;
            feedbackPanel.SetActive(false);
            decisionContinueHint.SetActive(false);
            for (int i = 0; i < choices.Length; i++)
            {
                bool used = i < count;
                choices[i].gameObject.SetActive(used);
                choices[i].Button.interactable = true;
                if (used) choices[i].Bind(i + 1, decision.choices[i].text);
            }
            bool timed = decision.time_limit > 0f;
            timerBadge.SetActive(timed);
            UpdateTimer(decision.time_limit, decision.time_limit);
            decisionPanel.SetActive(true);
            yield return Open();

            // Seçenekler sayaç başlamadan önce de görünür; sayaç açılış bitince başlar.
            pickedIndex = -1;
            float remaining = decision.time_limit;
            while (pickedIndex < 0)
            {
                int key = DialogueInput.ChoicePressed(count);
                if (key >= 0) pickedIndex = key;
                if (timed)
                {
                    remaining -= Time.deltaTime;
                    UpdateTimer(remaining, decision.time_limit);
                    if (remaining <= 0f) break;
                }
                yield return null;
            }

            // Sonuç: seçilen kart ✓/✗, doğru seçenek her durumda yeşil, diğerleri soluk.
            var choice = pickedIndex >= 0 ? decision.choices[pickedIndex] : null;
            for (int i = 0; i < count; i++)
            {
                choices[i].Button.interactable = false;
                var c = decision.choices[i];
                choices[i].SetState(c.correct ? DecisionChoiceView.State.Correct
                                  : i == pickedIndex ? DecisionChoiceView.State.Wrong
                                  : DecisionChoiceView.State.Dimmed);
            }

            bool good = choice != null && choice.correct;
            feedbackText.text = choice != null ? choice.feedback : decision.timeout_feedback;
            feedbackAccent.color = good ? correctColor : wrongColor;
            feedbackIcon.text = good ? "✓" : "!";
            feedbackIcon.color = good ? correctColor : wrongColor;
            feedbackPanel.SetActive(true);

            yield return null;
            decisionContinueHint.SetActive(true);
            while (!DialogueInput.ContinuePressed()) yield return null;

            decisionPanel.SetActive(false);
            IsBusy = false;
            onDone?.Invoke(choice);
            onDecisionMade?.Invoke(decision.id, choice?.id ?? "", choice != null && choice.correct);
            DecisionMade?.Invoke(decision.id, choice);
        }

        IEnumerator Open()
        {
            for (float t = 0f; t < openDuration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / openDuration);
                decisionGroup.alpha = k;
                decisionBox.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
                yield return null;
            }
            decisionGroup.alpha = 1f;
            decisionBox.localScale = Vector3.one;
        }

        void UpdateTimer(float remaining, float limit)
        {
            if (limit <= 0f) return;
            float k = Mathf.Clamp01(remaining / limit);
            timerFill.fillAmount = k;
            timerFill.color = Color.Lerp(wrongColor, timerStartColor, k);
            timerText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
        }
    }
}
