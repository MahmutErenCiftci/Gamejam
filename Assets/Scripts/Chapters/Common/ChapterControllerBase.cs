using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AfetUyanisi.Chapters.Objectives;
using AfetUyanisi.Chapters.TempUI;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Sel ve Deprem bölümlerinin ortak akış altyapısı (Mahmut). Akış docs/OYUN_AKISI.md'deki zaman çizelgesini izler.
    /// Ekip parçaları (diyalog, karar, haber, paneller) Inspector'dan bağlanır. Bağlı olmayan parça atlanır ve
    /// Console'a yazılır, böylece bölüm diğer parçalar gelmeden de baştan sona oynatılabilir.
    /// Odalar aynı sahnede farklı dünya konumlarında durur: adım görseli (StepVisual) kökü açar, oyuncuyu koyar,
    /// kamerayı odaya ortalar ya da oyuncuyu takip ettirir (ChapterCamera) ve isterse ortam ışığını değiştirir.
    /// </summary>
    public abstract class ChapterControllerBase : MonoBehaviour
    {
        /// <summary>Bir zaman çizelgesi adımına girilince açılacak görsel kök (ör. F05_STREET → World/Street).</summary>
        [Serializable]
        public class StepVisual
        {
            public string stepId;
            public GameObject root;
            [Tooltip("Adıma girilince oyuncunun konacağı nokta (boşsa oyuncu yerinde kalır).")]
            public Transform playerSpawn;
            [Tooltip("Doğunca oyuncunun bakacağı yön (ekran yönü; (0,-1) = güney/aşağı).")]
            public Vector2 spawnFacing = new Vector2(0f, -1f);
            [Tooltip("Görselde çocuk zaten çizili (hikâye kartı) ise oyuncuyu gizle.")]
            public bool hidePlayer;

            [Tooltip("İsteğe bağlı: kamera bu noktaya ortalanır (sabit oda). Boşsa ve takip alanı da yoksa kamera yerinde kalır.")]
            public Transform cameraAnchor;
            [Tooltip("İsteğe bağlı: kamera oyuncuyu bu alan içinde takip eder (ör. sokak). cameraAnchor'dan önce gelir.")]
            public CameraBounds cameraBounds;

            [Tooltip("İşaretliyse adıma girilince ortam ışığı (Global Light 2D) aşağıdaki renge ve şiddete geçer.")]
            public bool setAmbient;
            public Color ambientColor = Color.white;
            public float ambientIntensity = 1f;
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

        [Header("Oyuncu")]
        [Tooltip("Sahnedeki çocuk (Characters/Boy)")]
        [SerializeField] private GameObject playerObject;

        [Header("Adım görselleri")]
        [Tooltip("Adıma girilince eşleşen kök açılır, diğerleri kapanır. Listede olmayan adımda önceki görsel kalır.")]
        [SerializeField] private StepVisual[] stepVisuals = Array.Empty<StepVisual>();

        [Header("Kamera ve ışık")]
        [Tooltip("Main Camera'daki ChapterCamera; boşsa Main Camera'da aranır. Adım görselindeki kamera ayarı buna uygulanır.")]
        [SerializeField] private ChapterCamera chapterCamera;
        [Tooltip("Sahnenin Global Light 2D'si; adım görselindeki ortam ışığı ayarı buna uygulanır.")]
        [SerializeField] private Light2D globalLight;
        [Tooltip("Aynı oda içinde (kararmadan) ortam ışığı değişirken geçiş süresi (sn)")]
        [SerializeField] private float ambientBlendSeconds = 1f;

        [Header("Hata ayıklama")]
        [Tooltip("İşaretliyse tuş beklemeden akış baştan sona ilerler; adımlar Console'a süreleriyle yazılır.")]
        [SerializeField] private bool debugAutoAdvance;
        [SerializeField] private string currentStep;

        private readonly List<ChoiceData> mistakes = new List<ChoiceData>();
        private StoryData story;
        private IsoPlayerController player;
        private IsoCharacterAnimator playerAnimator;
        private Coroutine ambientBlend;

        private ObjectiveTracker objectives;

        protected abstract ChapterId Chapter { get; }

        /// <summary>
        /// Bölüm görev panelini (ObjectiveTracker + sol üst HUD) kullanıyor mu? Kullanıyorsa adımlar ("step"), etkileşim
        /// noktaları ("point", "F04_MOTHER/Coat"), diyaloglar ("say"), kararlar ("decision", success = ilk denemede doğru)
        /// ve haberler ("news") otomatik bildirilir; diyalog/karar/haber açıkken panel gizlenir.
        /// </summary>
        protected virtual bool UsesObjectives => false;

        /// <summary>Görev takipçisi (UsesObjectives değilse null).</summary>
        protected ObjectiveTracker Objectives => objectives;

        /// <summary>Görev olayı bildirir (ör. NotifyObjective("event", "F05_BOARD")).</summary>
        protected void NotifyObjective(string kind, string key, bool success = true)
        {
            if (objectives != null) objectives.Notify(kind, key, success);
        }

        // Panel bu kapsam boyunca gizli; biten görevler sırada bekler ve sonra oynatılır.
        private IDisposable HideObjectives() => objectives != null ? objectives.DeferFeedback() : null;

        /// <summary>Bölümün baştan sona akışı.</summary>
        protected abstract IEnumerator RunChapter();

        protected bool AutoAdvance => debugAutoAdvance;

        /// <summary>Şu anki zaman çizelgesi adımı (ör. "F05_STREET").</summary>
        protected string CurrentStep => currentStep;

        /// <summary>Oyuncunun hareket bileşeni (yoksa null).</summary>
        protected IsoPlayerController Player => player;

        /// <summary>Bölüm kamerası (yoksa null).</summary>
        protected ChapterCamera ChapterCam => chapterCamera;

        protected virtual void Awake()
        {
            // Sahne menüden değil doğrudan Play ile açıldıysa da "Tekrar Dene" çalışsın.
            if (GameSession.CurrentChapter != Chapter)
                GameSession.StartChapter(Chapter);

            if (storyJson != null)
                story = JsonUtility.FromJson<StoryData>(storyJson.text);

            // Arayüz ekibinin gerçek ipucu/kararma prefabı bağlı değilse geçicileri kullan.
            if (hint == null) hint = new GameObject("TempInteractionHint").AddComponent<TempInteractionHint>();
            if (fader == null) fader = new GameObject("TempScreenFader").AddComponent<TempScreenFader>();

            if (chapterCamera == null && Camera.main != null)
                chapterCamera = Camera.main.GetComponent<ChapterCamera>();

            if (playerObject != null)
            {
                player = playerObject.GetComponent<IsoPlayerController>();
                playerAnimator = playerObject.GetComponent<IsoCharacterAnimator>();
            }
            SetPlayerControl(false);
        }

        protected virtual void Start()
        {
            // Banyodan karartılarak gelindiyse ilk kare de siyah olsun (mutfak EnterRoom ile açılır).
            if (GameSession.MorningDoneInRooms) SetBlack();
            if (UsesObjectives) objectives = ObjectiveTracker.Ensure();
            StartCoroutine(RunChapter());
        }

        /// <summary>
        /// Yeni zaman çizelgesi adımına geçer: görseli değiştirir, Console'a geçen süreyle yazar.
        /// Kamera ve ışık aynı oda içinde yumuşak geçer (oda değişimi için EnterRoom kullan: kararma altında keser).
        /// </summary>
        protected void EnterStep(string stepId) => EnterStep(stepId, false);

        private void EnterStep(string stepId, bool cut)
        {
            currentStep = stepId;
            Debug.Log($"[{Chapter}] {Time.timeSinceLevelLoad:0.0} sn → {stepId}");
            ShowVisualFor(stepId, cut);
            OnStepEntered(stepId);
            NotifyObjective("step", stepId);
        }

        /// <summary>
        /// Adımın görseli açıldıktan hemen sonra çağrılır (EnterRoom'da kararma açılmadan önce): odanın o anki
        /// durumunu kurmak için (ör. şemsiye alındıysa şemsiyelik boş).
        /// </summary>
        protected virtual void OnStepEntered(string stepId) { }

        private void ShowVisualFor(string stepId, bool cut)
        {
            if (stepVisuals == null || !stepVisuals.Any(v => v?.root != null && v.stepId == stepId))
                return;

            // Aynı kök birden fazla adımda kullanılabilir (F05 ve F06 → Street): açık kalacak kök kapatılıp
            // yeniden açılmaz (yoksa içindeki efektler, coroutine'ler ve animasyonlar sıfırlanırdı).
            var matches = stepVisuals.Where(v => v?.root != null && v.stepId == stepId).ToList();
            var keep = new HashSet<GameObject>(matches.Select(v => v.root));
            foreach (var v in stepVisuals)
                if (v?.root != null && !keep.Contains(v.root)) v.root.SetActive(false);
            foreach (var root in keep)
                root.SetActive(true);

            if (playerObject != null)
            {
                playerObject.SetActive(!matches.Any(v => v.hidePlayer));
                var entry = matches.FirstOrDefault(v => v.playerSpawn != null);
                if (entry != null)
                {
                    if (player != null) player.PlaceAt(entry.playerSpawn.position, entry.spawnFacing);
                    else playerObject.transform.position = entry.playerSpawn.position;
                }
            }

            ApplyCamera(matches, cut);
            var ambient = matches.FirstOrDefault(v => v.setAmbient);
            if (ambient != null)
                SetAmbient(ambient.ambientColor, ambient.ambientIntensity, cut ? 0f : ambientBlendSeconds);
        }

        // Takip alanı varsa (ve oyuncu görünüyorsa) oyuncuyu takip et; yoksa sabit noktaya ortala; ikisi de yoksa dokunma.
        private void ApplyCamera(List<StepVisual> matches, bool cut)
        {
            if (chapterCamera == null) return;
            var follow = matches.FirstOrDefault(v => v.cameraBounds != null);
            if (follow != null && playerObject != null && playerObject.activeInHierarchy)
            {
                chapterCamera.Follow(playerObject.transform, follow.cameraBounds, cut);
                return;
            }
            var anchor = matches.FirstOrDefault(v => v.cameraAnchor != null);
            if (anchor != null)
                chapterCamera.ShowPoint(anchor.cameraAnchor.position, cut);
        }

        /// <summary>Ortam ışığını (Global Light 2D) verilen renge/şiddete seconds içinde geçirir (0 = hemen).</summary>
        protected void SetAmbient(Color color, float intensity, float seconds)
        {
            if (globalLight == null) return;
            if (ambientBlend != null) StopCoroutine(ambientBlend);
            ambientBlend = null;
            if (seconds <= 0f || !isActiveAndEnabled)
            {
                globalLight.color = color;
                globalLight.intensity = intensity;
                return;
            }
            ambientBlend = StartCoroutine(BlendAmbient(color, intensity, seconds));
        }

        private IEnumerator BlendAmbient(Color color, float intensity, float seconds)
        {
            Color startColor = globalLight.color;
            float startIntensity = globalLight.intensity;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                globalLight.color = Color.Lerp(startColor, color, k);
                globalLight.intensity = Mathf.Lerp(startIntensity, intensity, k);
                yield return null;
            }
            globalLight.color = color;
            globalLight.intensity = intensity;
            ambientBlend = null;
        }

        protected GameObject PlayerObject => playerObject;

        /// <summary>Oyuncuyu yürütmeden verilen ekran yönüne çevirir ((1,0) = doğu/sağ, (0,1) = kuzey/yukarı).</summary>
        protected void FacePlayer(Vector2 screenDirection)
        {
            if (playerAnimator != null) playerAnimator.Face(screenDirection);
        }

        /// <summary>Oyuncuyu dünyadaki bir noktaya (ör. TV, anne) çevirir.</summary>
        protected void FacePlayerTowards(Vector3 worldPoint)
        {
            if (playerObject != null) FacePlayer(worldPoint - playerObject.transform.position);
        }

        /// <summary>Oyuncuyu gösterir/gizler (ör. uyanıştan önce çocuk yatakta, görünmez).</summary>
        protected void SetPlayerVisible(bool visible)
        {
            if (playerObject != null) playerObject.SetActive(visible);
        }

        /// <summary>Oyuncunun yürüyüp yürüyemeyeceği. Sadece bir etkileşim noktası beklenirken açılır.</summary>
        protected void SetPlayerControl(bool canMove)
        {
            if (player != null) player.CanMove = canMove;
        }

        /// <summary>
        /// Oda değiştirir: ekran kararır, yeni adımın görseli ve oyuncu yerleşir, kamera ve ışık kararma altında
        /// yeni odaya geçer (kesme), ekran açılır.
        /// </summary>
        protected IEnumerator EnterRoom(string stepId, float fadeSeconds = 0.35f)
        {
            SetPlayerControl(false);
            if (objectives != null) yield return objectives.WaitForFeedback(0.45f, 1.2f); // son tik kararmadan görünsün
            yield return FadeOut(fadeSeconds);
            EnterStep(stepId, true);
            yield return FadeIn(fadeSeconds);
        }

        /// <summary>
        /// Açık adımın odasındaki etkileşim noktasını (ör. "Bed", "Door") adıyla bulur; oyuncu oraya yürüyüp E'ye
        /// basana kadar bekler. Bekleme sırasında oyuncu yürüyebilir. Nokta sahnede yoksa her yerden E ile geçilir.
        /// </summary>
        protected IEnumerator UsePoint(string pointName, string label)
        {
            string step = currentStep;
            yield return UsePoint(FindPoint(pointName), label);
            NotifyObjective("point", step + "/" + pointName);
        }

        /// <summary>Verilen etkileşim noktası için UsePoint (ör. otobüsün kapısı). Nokta null ise her yerden E ile geçilir.</summary>
        protected IEnumerator UsePoint(Interactable point, string label)
        {
            if (point == null)
            {
                yield return WaitForInteract(label);
                yield break;
            }

            SetPlayerControl(true);
            if (debugAutoAdvance)
                yield return null;
            else
                yield return point.WaitForUse(player, hint, label);
            SetPlayerControl(false);
        }

        /// <summary>Açık adımın odasındaki etkileşim noktasını adıyla bulur (yoksa null).</summary>
        protected Interactable FindPoint(string pointName)
        {
            foreach (var root in ActiveRoots())
            {
                if (!root.activeInHierarchy) continue;
                var found = root.GetComponentsInChildren<Interactable>(true).FirstOrDefault(i => i.name == pointName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Açık adımın odasında adı verilen objeyi bulur (kapalı olanlar dahil; yoksa null).</summary>
        protected GameObject FindRoomObject(string objectName)
        {
            foreach (var root in ActiveRoots())
            {
                var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(c => c.name == objectName);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        /// <summary>Açık adımın odasında adı verilen objedeki bileşeni bulur (yoksa null).</summary>
        protected T FindRoomComponent<T>(string objectName) where T : Component
        {
            var go = FindRoomObject(objectName);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>Açık odadaki adı verilen objeyi gösterir/gizler (ör. yataktaki uyuyan çocuk "SleepingBoy").</summary>
        protected void SetRoomObjectVisible(string objectName, bool visible)
        {
            foreach (var root in ActiveRoots())
            {
                var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(c => c.name == objectName);
                if (t != null) t.gameObject.SetActive(visible);
            }
        }

        // Şu anki adımın görsel kökleri.
        private IEnumerable<GameObject> ActiveRoots()
        {
            if (stepVisuals == null) yield break;
            foreach (var v in stepVisuals)
                if (v?.root != null && v.stepId == currentStep)
                    yield return v.root;
        }

        /// <summary>Hikâye verisinde (storyJson) bu ID'li diyalog sahnesi var mı?</summary>
        protected bool HasStoryScene(string storySceneId) => story?.FindScene(storySceneId) != null;

        /// <summary>
        /// İki bölümün ortak açılışı (OYUN_AKISI §5.1): yatakta uyanış → odada yürüme → banyo → mutfağa çıkış.
        /// Bitince ekran karanlıktır; bölüm bir sonraki odaya EnterRoom ile geçer.
        /// Açılış oda sahnelerinde (Bedroom → Bathroom) zaten oynandıysa (GameSession.MorningDoneInRooms) atlanır:
        /// ekran siyah, oyuncu görünür bırakılır, bölüm doğrudan mutfaktan (EnterRoom F03/E03 ile açılarak) başlar.
        /// </summary>
        protected IEnumerator PlayMorningIntro(string wakeStepId, string routineStepId)
        {
            if (GameSession.MorningDoneInRooms)
            {
                Debug.Log($"[{Chapter}] Sabah açılışı ({wakeStepId}, {routineStepId}) oda sahnelerinde oynandı, atlandı → mutfak.");
                SetBlack(); // banyo karartarak çıktı; sonraki EnterRoom siyahtan açılsın (pat diye görünmesin)
                SetPlayerVisible(true);
                yield break;
            }

            // 1–3: siyah ekran, oda açılır, çocuk yatakta uyuyor
            EnterStep(wakeStepId);
            SetBlack();
            SetPlayerVisible(false);
            SetRoomObjectVisible("SleepingBoy", true);
            // TODO(Mahmut): 07:00 alarm sesi
            yield return FadeIn(1.5f);
            yield return UsePoint("Bed", "Kalk");

            // 4: kısa kararma, çocuk yatağın yanında belirir
            yield return FadeOut(0.25f);
            SetRoomObjectVisible("SleepingBoy", false);
            SetPlayerVisible(true);
            EnterStep(wakeStepId); // oyuncuyu doğma noktasına koyar
            yield return FadeIn(0.25f);

            // 5–6: iç ses, sonra serbest dolaşma ve kapı
            yield return Say(wakeStepId);
            yield return UsePoint("Door", "Banyoya git");

            // 7–10: banyo, lavabo, mutfağa çıkış
            yield return EnterRoom(routineStepId);
            yield return UsePoint("Sink", "Yüzünü yıka");
            // TODO(Mahmut): 1 sn su efekti (flaş yok)
            yield return Say(routineStepId);
            yield return UsePoint("Door", "Mutfağa git");
            yield return FadeOut(0.35f);
        }

        /// <summary>Story JSON'daki diyalog sahnesini oynatır (Arda'nın DialogueRunner'ı).</summary>
        protected IEnumerator Say(string storySceneId)
        {
            using (HideObjectives())
                yield return SayInner(storySceneId);
            NotifyObjective("say", storySceneId);
        }

        private IEnumerator SayInner(string storySceneId)
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
            result.RequiredCorrect = story?.FindDecision(decisionId)?.GetRequiredCorrect() ?? 1;

            using (HideObjectives())
            {
                if (decisions != null)
                    yield return decisions.Run(decisionId, result, afterEachChoice);
                else
                    yield return AskPlaceholder(decisionId, result, afterEachChoice);
            }

            mistakes.AddRange(result.WrongChoices);
            // Yanlış deneme varsa görev önce uyarı alır ("1 UYARI"), doğru seçimle yine tamamlanır.
            if (result.WrongChoices.Count > 0) NotifyObjective("decision", decisionId, false);
            NotifyObjective("decision", decisionId);
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
            using (HideObjectives())
            {
                if (news != null) yield return news.Play(newsId);
                else Debug.Log($"[{Chapter}] (haber yok) {newsId}");
            }
            NotifyObjective("news", newsId);
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
            // Son görevin tiki ve çubuk dolumu sonuç ekranından önce oynasın; ekran açıkken panel gizli kalır.
            if (objectives != null) yield return objectives.WaitForFeedback(1.4f, 6f);
            using (HideObjectives())
                yield return ShowResultInner();
        }

        private IEnumerator ShowResultInner()
        {
            var data = story?.result;
            string title = mistakes.Count == 0
                ? data?.title_success ?? "Tebrikler! Doğru davrandın."
                : data?.title_with_mistakes ?? "Bölümü tamamladın! Yanlışlarından ders çıkardın.";
            IReadOnlyList<string> learned = data?.learned ?? Array.Empty<string>();
            List<string> wrong = mistakes
                .Select(WrongExampleOf)
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

        // Sonuç ekranındaki "yanlış davranış" maddesi: seçeneğin kendisinde yoksa hikâye verisinden (ID ile) bulunur.
        protected string WrongExampleOf(ChoiceData choice)
        {
            if (!string.IsNullOrEmpty(choice.wrong_example)) return choice.wrong_example;
            var fromStory = story?.decisions?
                .Where(d => d.choices != null)
                .SelectMany(d => d.choices)
                .FirstOrDefault(c => c.id == choice.id);
            return !string.IsNullOrEmpty(fromStory?.wrong_example) ? fromStory.wrong_example : choice.text;
        }
    }
}
