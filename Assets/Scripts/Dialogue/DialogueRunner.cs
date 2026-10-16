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
        [SerializeField] Text promptText;
        [SerializeField] Button[] choiceButtons;
        [SerializeField] Text[] choiceTexts;
        [SerializeField] Text timerText;
        [SerializeField] Text feedbackText;
        [SerializeField] GameObject decisionContinueHint;
        [SerializeField] Color correctColor = new Color(0.55f, 0.95f, 0.6f, 1f);
        [SerializeField] Color wrongColor = new Color(1f, 0.7f, 0.45f, 1f);

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
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int index = i;
                choiceButtons[i].onClick.AddListener(() => pickedIndex = index);
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
            float shown = 0f;
            while (shown < text.Length)
            {
                // Yazarken basılırsa satırı tamamla.
                if (DialogueInput.ContinuePressed()) break;
                shown += charsPerSecond * Time.deltaTime;
                target.text = text.Substring(0, Mathf.Min(text.Length, (int)shown));
                yield return null;
            }
            target.text = text;
            yield return null;
        }

        IEnumerator RunDecision(StoryDecision decision, Action<StoryChoice> onDone)
        {
            IsBusy = true;
            int count = Mathf.Min(decision.choices.Length, choiceButtons.Length);
            if (decision.choices.Length > choiceButtons.Length)
                Debug.LogWarning($"[Dialogue] '{decision.id}' {decision.choices.Length} seçenekli, prefabta {choiceButtons.Length} yuva var.");

            promptText.text = decision.prompt;
            feedbackText.text = "";
            decisionContinueHint.SetActive(false);
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool used = i < count;
                choiceButtons[i].gameObject.SetActive(used);
                choiceButtons[i].interactable = true;
                if (used) choiceTexts[i].text = $"{i + 1}. {decision.choices[i].text}";
            }
            bool timed = decision.time_limit > 0f;
            timerText.gameObject.SetActive(timed);
            decisionPanel.SetActive(true);

            // Seçenekler sayaç başlamadan önce de görünür; sayaç ilk karede başlar.
            pickedIndex = -1;
            float remaining = decision.time_limit;
            yield return null;
            while (pickedIndex < 0)
            {
                int key = DialogueInput.ChoicePressed(count);
                if (key >= 0) pickedIndex = key;
                if (timed)
                {
                    remaining -= Time.deltaTime;
                    timerText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
                    if (remaining <= 0f) break;
                }
                yield return null;
            }

            foreach (var b in choiceButtons) b.interactable = false;
            var choice = pickedIndex >= 0 ? decision.choices[pickedIndex] : null;
            if (choice != null)
            {
                feedbackText.text = choice.feedback;
                feedbackText.color = choice.correct ? correctColor : wrongColor;
            }
            else
            {
                feedbackText.text = decision.timeout_feedback;
                feedbackText.color = wrongColor;
            }

            yield return null;
            decisionContinueHint.SetActive(true);
            while (!DialogueInput.ContinuePressed()) yield return null;

            decisionPanel.SetActive(false);
            IsBusy = false;
            onDone?.Invoke(choice);
            onDecisionMade?.Invoke(decision.id, choice?.id ?? "", choice != null && choice.correct);
            DecisionMade?.Invoke(decision.id, choice);
        }
    }
}
