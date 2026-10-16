using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Sel ve Deprem bölümlerinin ortak akış altyapısı (Mahmut). Akış docs/OYUN_AKISI.md'deki zaman çizelgesini izler.
    /// Ekip parçaları (diyalog, karar, haber, paneller) Inspector'dan bağlanır. Bağlı olmayan parça atlanır ve
    /// Console'a yazılır, böylece bölüm diğer parçalar gelmeden de baştan sona oynatılabilir.
    /// </summary>
    public abstract class ChapterControllerBase : MonoBehaviour
    {
        /// <summary>Bir zaman çizelgesi adımına girilince açılacak görsel kök (ör. F05_STREET → World/Street).</summary>
        [Serializable]
        public class StepVisual
        {
            public string stepId;
            public GameObject root;
        }

        [Header("Veri")]
        [Tooltip("Assets/Data/Story/<bölüm>.json — sonuç ekranı ve yer tutucu akış bunu okur")]
        [SerializeField] private TextAsset storyJson;

        [Header("Ekip parçaları")]
        [SerializeField] private DialogueRunnerBase dialogue;   // Arda
        [SerializeField] private DecisionRunnerBase decisions;  // Arda
        [SerializeField] private NewsPlayerBase news;           // Eren
        [SerializeField] private InteractionHintBase hint;      // Berk
        [SerializeField] private ScreenFaderBase fader;         // Berk
        [SerializeField] private ResultViewBase resultView;     // Berk

        [Header("Adım görselleri")]
        [Tooltip("Adıma girilince eşleşen kök açılır, diğerleri kapanır. Listede olmayan adımda önceki görsel kalır.")]
        [SerializeField] private StepVisual[] stepVisuals = Array.Empty<StepVisual>();

        [Header("Hata ayıklama")]
        [Tooltip("İşaretliyse tuş beklemeden akış baştan sona ilerler; adımlar Console'a süreleriyle yazılır.")]
        [SerializeField] private bool debugAutoAdvance;
        [SerializeField] private string currentStep;

        private readonly List<ChoiceData> mistakes = new List<ChoiceData>();
        private StoryData story;

        protected abstract ChapterId Chapter { get; }

        /// <summary>Bölümün baştan sona akışı.</summary>
        protected abstract IEnumerator RunChapter();

        protected bool AutoAdvance => debugAutoAdvance;

        protected virtual void Awake()
        {
            // Sahne menüden değil doğrudan Play ile açıldıysa da "Tekrar Dene" çalışsın.
            if (GameSession.CurrentChapter != Chapter)
                GameSession.StartChapter(Chapter);

            if (storyJson != null)
                story = JsonUtility.FromJson<StoryData>(storyJson.text);
        }

        protected virtual void Start()
        {
            StartCoroutine(RunChapter());
        }

        /// <summary>Yeni zaman çizelgesi adımına geçer: görseli değiştirir, Console'a geçen süreyle yazar.</summary>
        protected void EnterStep(string stepId)
        {
            currentStep = stepId;
            Debug.Log($"[{Chapter}] {Time.timeSinceLevelLoad:0.0} sn → {stepId}");
            ShowVisualFor(stepId);
        }

        private void ShowVisualFor(string stepId)
        {
            if (stepVisuals == null || !stepVisuals.Any(v => v?.root != null && v.stepId == stepId))
                return;

            // Önce kapat, sonra aç: aynı kök birden fazla adımda kullanılabilir (F05 ve F06 → Street).
            foreach (var v in stepVisuals)
                if (v?.root != null && v.stepId != stepId) v.root.SetActive(false);
            foreach (var v in stepVisuals)
                if (v?.root != null && v.stepId == stepId) v.root.SetActive(true);
        }

        /// <summary>İki bölümün ortak açılışı: Uyanış + Sabah Rutini (OYUN_AKISI §5).</summary>
        protected IEnumerator PlayMorningIntro(string wakeStepId, string routineStepId)
        {
            EnterStep(wakeStepId);
            SetBlack();
            // TODO(Mahmut): 07:00 alarm sesi, çocuğun gözünü açması
            yield return FadeIn(1.5f);
            yield return WaitForInteract("Kalk");
            yield return Say(wakeStepId);
            // TODO(Mahmut): kapıya yürüme (GameInput.MoveX); şimdilik etkileşimle geçiliyor
            yield return WaitForInteract("Banyoya git");

            EnterStep(routineStepId);
            yield return WaitForInteract("Yüzünü yıka");
            // TODO(Mahmut): 1 sn su efekti
            yield return Say(routineStepId);
        }

        /// <summary>Story JSON'daki diyalog sahnesini oynatır (Arda'nın DialogueRunner'ı).</summary>
        protected IEnumerator Say(string storySceneId)
        {
            if (dialogue != null)
            {
                yield return dialogue.Play(storySceneId);
                yield break;
            }

            var scene = story?.FindScene(storySceneId);
            if (scene?.lines == null)
            {
                Debug.Log($"[{Chapter}] (diyalog yok) {storySceneId}");
                yield break;
            }

            foreach (var line in scene.lines)
                Debug.Log($"[{Chapter}]   {line.speaker}: {line.text}");
            yield return null;
        }

        /// <summary>Kararı sorar (Arda'nın DecisionRunner'ı); yanlış seçimler sonuç ekranı için toplanır.</summary>
        protected IEnumerator Ask(string decisionId, DecisionResult result = null,
            Func<ChoiceData, IEnumerator> afterEachChoice = null)
        {
            result ??= new DecisionResult();
            result.DecisionId = decisionId;

            if (decisions != null)
                yield return decisions.Run(decisionId, result, afterEachChoice);
            else
                yield return AskPlaceholder(decisionId, result, afterEachChoice);

            mistakes.AddRange(result.WrongChoices);
        }

        // DecisionRunner bağlı değilken: soruyu Console'a yazar, doğru seçenek(ler)i kendisi seçer.
        private IEnumerator AskPlaceholder(string decisionId, DecisionResult result,
            Func<ChoiceData, IEnumerator> afterEachChoice)
        {
            var decision = story?.FindDecision(decisionId);
            if (decision?.choices == null)
            {
                Debug.Log($"[{Chapter}] (karar yok) {decisionId}");
                yield break;
            }

            Debug.Log($"[{Chapter}]   KARAR {decisionId}: {decision.prompt}");
            foreach (var choice in decision.choices.Where(c => c.correct).Take(decision.GetRequiredCorrect()))
            {
                Debug.Log($"[{Chapter}]   (yer tutucu seçti) {choice.id} \"{choice.text}\" → {choice.feedback}");
                result.CorrectChoices.Add(choice);
                result.FinalChoice = choice;
                if (afterEachChoice != null)
                    yield return afterEachChoice(choice);
            }
        }

        /// <summary>Haberi / kartı gösterir (Eren'in haber oynatıcısı).</summary>
        protected IEnumerator ShowNews(string newsId)
        {
            if (news != null)
            {
                yield return news.Play(newsId);
                yield break;
            }

            Debug.Log($"[{Chapter}] (haber yok) {newsId}");
        }

        /// <summary>"[E] {label}" ipucu gösterir ve oyuncu E'ye basana kadar bekler.</summary>
        protected IEnumerator WaitForInteract(string label) =>
            WaitForKey("E", label, () => GameInput.InteractPressed);

        protected IEnumerator WaitForKey(string keyLabel, string actionLabel, Func<bool> pressed)
        {
            ShowHint(keyLabel, actionLabel);
            yield return null; // önceki adımda basılan tuş bu adımı da geçirmesin
            while (!debugAutoAdvance && !pressed())
                yield return null;
            HideHint();
        }

        protected void ShowHint(string keyLabel, string actionLabel)
        {
            if (hint != null) hint.Show(keyLabel, actionLabel);
            else Debug.Log($"[{Chapter}]   [{keyLabel}] {actionLabel}");
        }

        protected void HideHint()
        {
            if (hint != null) hint.Hide();
        }

        protected void SetBlack()
        {
            if (fader != null) fader.SetOpaque(true);
        }

        protected IEnumerator FadeIn(float seconds)
        {
            if (fader != null) yield return fader.FadeIn(seconds);
        }

        protected IEnumerator FadeOut(float seconds)
        {
            if (fader != null) yield return fader.FadeOut(seconds);
        }

        /// <summary>Sonuç ekranı (OYUN_AKISI §8): öğrenilenler + bu oynanışta yapılan yanlışlar, sonra tekrar/menü.</summary>
        protected IEnumerator ShowResult()
        {
            var data = story?.result;
            string title = mistakes.Count == 0
                ? data?.title_success ?? "Tebrikler! Doğru davrandın."
                : data?.title_with_mistakes ?? "Bölümü tamamladın! Yanlışlarından ders çıkardın.";
            IReadOnlyList<string> learned = data?.learned ?? Array.Empty<string>();
            List<string> wrong = mistakes
                .Select(m => string.IsNullOrEmpty(m.wrong_example) ? m.text : m.wrong_example)
                .Distinct()
                .ToList();
            string note = data?.note ?? "";

            if (resultView == null)
            {
                Debug.Log($"[{Chapter}] SONUÇ: {title}\n  Öğrenilenler: {string.Join(" · ", learned)}\n" +
                          $"  Yanlışlar: {(wrong.Count == 0 ? "yok" : string.Join(" · ", wrong))}");
                if (debugAutoAdvance) yield break;
                Debug.Log($"[{Chapter}] Menüye dönmek için Space / Enter / E.");
                yield return null;
                while (!GameInput.ContinuePressed) yield return null;
                GameSceneRouter.ReturnToMenu();
                yield break;
            }

            bool? retry = null;
            Action onRetry = () => retry = true;
            Action onMenu = () => retry = false;
            resultView.RetryClicked += onRetry;
            resultView.MenuClicked += onMenu;
            resultView.Show(title, learned, wrong, note);

            while (retry == null) yield return null;

            resultView.RetryClicked -= onRetry;
            resultView.MenuClicked -= onMenu;
            if (retry.Value) GameSceneRouter.RestartCurrentChapter();
            else GameSceneRouter.ReturnToMenu();
        }
    }
}
