using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Flood;
using AfetUyanisi.Chapters.Objectives;
using AfetUyanisi.Chapters.Rooms;
using AfetUyanisi.Chapters.TempUI;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>Akşam sel bölümünün durumları (zikzak rota, lider kararı 17 Eki 23:46).</summary>
    public enum FloodEveningState
    {
        Intro,          // "SEL BÖLÜMÜ" kartı + kısa açılış
        Warning,        // telefon uyarısı (bir kez)
        RouteChoice,    // dönüşlü sokaklar → tek kavşak (KISA YOL / UZUN YOL)
        LongRoad,       // uzun yolda (HighRoad'a girildi)
        Obstacle,       // ağaç düştü; kırık dal E'ye art arda basarak itilecek
        FinalApproach,  // "Yol açıldı." → bir köşe daha → devam noktası
        Success         // devam noktasına (SafeZone) varıldı → sonuç ekranı
    }

    /// <summary>
    /// AKŞAM SEL BÖLÜMÜ — FloodEvening sahnesinin tek akış/sonuç sorumlusu (LEAD).
    /// Intro → Warning → RouteChoice → (kısa yol: köşeden sonra kapalı, bir kez "Yol kapanmış..." repliği, oyuncu YÜRÜYEREK döner;
    /// hata sayılmaz, raporda nötr bilgi) → LongRoad → Obstacle (EVENTS'in ağaç düşüşü + dal itme) → FinalApproach → Success.
    /// Durum geri gitmez; modüllerin Hub adımlarından ve oyuncunun konumundan (HighRoad, BarrierZone, SafeZone) türetilir.
    /// SÜRE BASKISI YOK: geri sayım, süre dolması ve başarısızlık yok; zaman kaybı yalnızca gerçek yürüme süresidir.
    /// Ortam ilerlemesi (FloodEveningHub.Progress01): yağmur ve çevresel su oynanış süresiyle yavaşça artar (yalnız görsel).
    /// Rotadan çıkan modüller (şifre, bahçe kapısı, ipuçları, söylenti, rüzgâr) sahnede yoksa akış sorunsuz çalışır;
    /// sahneye konursa IFloodEveningInteraction olarak yine kaydedilir.
    /// </summary>
    public class FloodEveningController : MonoBehaviour
    {
        [Header("Ortam ilerlemesi (yalnız görsel; süre baskısı değil)")]
        [Tooltip("Yağmur ve çevresel suyun en yüksek seviyeye varma süresi (serbest dolaşmada geçen sn)")]
        [SerializeField] private float ambientSeconds = 150f;
        [Tooltip("Sağdaki su seviyesi göstergesi (lider kararı: süre baskısı yaratmasın diye kapalı)")]
        [SerializeField] private bool showWaterGauge;

        [Header("Oyuncu ve dünya (boşsa adla bulunur)")]
        [SerializeField] private IsoPlayerController player;
        [Tooltip("Başlangıç noktası (boşsa 'Spawn')")]
        [SerializeField] private Transform spawn;
        [SerializeField] private Vector2 spawnFacing = new Vector2(1f, 0.5f);
        [Tooltip("Açılışta kameranın kısa süre gösterdiği nokta (boşsa 'IntroView'; yoksa doğrudan Cenk)")]
        [SerializeField] private Transform introView;
        [Tooltip("Uzun yol alanı (boşsa 'HighRoad' altındaki trigger'lar; o da yoksa HighRoad noktasına yakınlık)")]
        [SerializeField] private Collider2D[] highRoadAreas = Array.Empty<Collider2D>();
        [SerializeField] private float highRoadRadius = 1.4f;
        [Tooltip("Kısa yolun kapalı noktası (boşsa 'BarrierZone' trigger'ı): ilk girişte bir kez replik + nötr kayıt")]
        [SerializeField] private Collider2D[] shortRoadClosedAreas = Array.Empty<Collider2D>();
        [Tooltip("Devam noktası / güvenli bölge (boşsa 'SafeZone' altındaki trigger'lar)")]
        [SerializeField] private Collider2D[] safeZoneAreas = Array.Empty<Collider2D>();
        [Tooltip("Kamera takip alanı (boşsa 'CameraBounds')")]
        [SerializeField] private CameraBounds cameraBounds;
        [SerializeField] private RainController rain;
        [Tooltip("Yağmur yoğunluğu: ilerleme 0'da / 1'de")]
        [SerializeField] private Vector2 rainRange = new Vector2(0.35f, 0.85f);

        [Header("Ekip parçaları (boşsa sahnede aranır / geçici oluşturulur)")]
        [SerializeField] private DialogueRunnerBase dialogue;
        [Tooltip("Assets/Data/Story/flood_evening.json — diyaloglar VE bütün ekran metinleri (FloodEveningTexts)")]
        [SerializeField] private TextAsset storyJson;
        [SerializeField] private InteractionHintBase hint;
        [SerializeField] private ScreenFaderBase fader;
        [SerializeField] private RoomInteractor interactor;
        [SerializeField] private FloodEveningHud hud;
        [SerializeField] private FloodEveningTitleCard titleCard;
        [SerializeField] private FloodEveningResultView resultView;

        [Header("Replikler (flood_evening.json sahne ID'leri)")]
        [SerializeField] private string introLine = "FE_INTRO";
        [SerializeField] private string afterWarningLine = "FE_AFTER_WARNING";
        [Tooltip("Kavşağa (\"Fork\") ilk yaklaşınca bir kez")]
        [SerializeField] private string forkLine = "FE_FORK";
        [SerializeField] private float forkRadius = 1.6f;
        [Tooltip("Kısa yol kapalı noktasına ilk girişte bir kez")]
        [SerializeField] private string shortClosedLine = "FE_SHORT_CLOSED";
        [SerializeField] private string finalLine = "FE_FINAL";
        [SerializeField] private string safeLine = "FE_SAFE";

        [Header("Ortamı durduran (okuma) paneller — Hub.NotifyOpened id öneki")]
        [SerializeField] private string[] readingPanels = { "rumor", "clue", "sign", "info", "read", "warning" };

        [Header("Hata ayıklama")]
        [SerializeField] private bool logSteps = true;

        // --- durum ---
        private FloodEveningState state = FloodEveningState.Intro;
        private float elapsed;       // serbest dolaşmada geçen süre (ortam ilerlemesi)
        private float playSeconds;   // uyarıdan sonra geçen toplam oynanış süresi (bilgi)
        private bool running;
        private bool finished;
        private bool freeRoam;
        private bool saying;
        private bool lastControl;
        private bool onHighRoad;
        private bool forkSaid;
        private bool shortRoadTried;
        private float lastRainSet = -1f;
        private ChapterCamera chapterCamera;
        private ObjectiveTracker objectives;
        private Transform forkPoint;
        private Transform highRoadPoint;
        private readonly HashSet<string> openPanels = new HashSet<string>();
        private readonly HashSet<string> steps = new HashSet<string>();
        private readonly Dictionary<string, int> mistakes = new Dictionary<string, int>();
        private readonly Queue<KeyValuePair<string, string>> sayQueue = new Queue<KeyValuePair<string, string>>();
        private readonly List<IFloodEveningInteraction> modules = new List<IFloodEveningInteraction>();
        private IFloodEveningWarning warning;
        private Coroutine sayRoutine;
        private FloodEveningResult result;
        private FloodEveningTexts texts;
        private string T(string key) => texts.Ui(key);

        // --- testler için okunur durum ---
        public FloodEveningState State => state;
        public float Elapsed => elapsed;
        public float PlaySeconds => playSeconds;
        public float Progress01 => Mathf.Clamp01(elapsed / Mathf.Max(1f, ambientSeconds));
        public bool IsFinished => finished;
        public bool IsPaused => ComputePaused();
        public bool ShortRoadTried => shortRoadTried;
        public FloodEveningResult Result => result;
        public bool HasStep(string id) => steps.Contains(id);
        public int MistakeCount(string id) => mistakes.TryGetValue(id, out int n) ? n : 0;

        /// <summary>Yalnız test: ortam ilerlemesini ayarlar (Editor / Development build).</summary>
        public void DebugSetElapsed(float seconds)
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            elapsed = Mathf.Clamp(seconds, 0f, ambientSeconds);
        }

        // =====================================================================
        // Kurulum
        // =====================================================================

        private void Awake()
        {
            // Bölüm her açılışta temiz başlar: bayraklar (backpack_taken, raincoat_taken, quake_bag_taken, görevler) silinir.
            // Lider kararı: Cenk deprem modundaki görünümüyle (çantasız yelekli; yağmurluk/çanta yok) oynar.
            GameSession.StartChapter(ChapterId.Flood);
            CenkOutfit.RefreshAll();

            Time.timeScale = 1f;
            FloodEveningHub.Progress01 = 0f;
            FloodEveningHub.Paused = true;
            FloodEveningHub.FreeRoam = false;

            if (player == null) player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (spawn == null) spawn = FindNamed<Transform>("Spawn");
            if (introView == null) introView = FindNamed<Transform>("IntroView");
            forkPoint = FindNamed<Transform>("Fork");
            highRoadPoint = FindNamed<Transform>("HighRoad");
            if (highRoadAreas == null || highRoadAreas.Length == 0) highRoadAreas = CollidersUnder("HighRoad");
            if (shortRoadClosedAreas == null || shortRoadClosedAreas.Length == 0) shortRoadClosedAreas = CollidersUnder("BarrierZone");
            if (safeZoneAreas == null || safeZoneAreas.Length == 0) safeZoneAreas = CollidersUnder("SafeZone");
            if (cameraBounds == null) cameraBounds = FindNamed<CameraBounds>("CameraBounds");
            if (cameraBounds == null) cameraBounds = FindAnyObjectByType<CameraBounds>(FindObjectsInactive.Include);
            if (rain == null) rain = FindAnyObjectByType<RainController>(FindObjectsInactive.Include);
            if (dialogue == null) dialogue = FindAnyObjectByType<DialogueRunnerBase>(FindObjectsInactive.Include);
            if (hint == null) hint = FindAnyObjectByType<InteractionHintBase>(FindObjectsInactive.Include);
            if (hint == null) hint = new GameObject("TempInteractionHint").AddComponent<TempInteractionHint>();
            if (fader == null) fader = FindAnyObjectByType<ScreenFaderBase>(FindObjectsInactive.Include);
            if (fader == null) fader = new GameObject("TempScreenFader").AddComponent<TempScreenFader>();
            hud = Resolve(hud);
            titleCard = Resolve(titleCard);
            resultView = Resolve(resultView);
            interactor = Resolve(interactor);

            texts = FloodEveningTexts.Load(storyJson);
            hud.SetGaugeTexts(T(FloodEveningTexts.Keys.GaugeTitle), T(FloodEveningTexts.Keys.GaugeNote));
            resultView.SetLabels(T(FloodEveningTexts.Keys.ResultHeader), T(FloodEveningTexts.Keys.ResultCorrectHeader),
                T(FloodEveningTexts.Keys.ResultCorrectedHeader), T(FloodEveningTexts.Keys.ResultLessonsHeader),
                T(FloodEveningTexts.Keys.ResultRetry), T(FloodEveningTexts.Keys.ResultMenu));
            interactor.Configure(player, dialogue, hint, storyJson);
            interactor.InteractionEnabled = false;
            if (player != null) { player.CanMove = false; player.SpeedMultiplier = 1f; }

            FloodEveningHub.MinigameOpened += OnPanelOpened;
            FloodEveningHub.MinigameClosed += OnPanelClosed;
            FloodEveningHub.StepCompleted += OnStep;
            FloodEveningHub.Mistake += OnMistake;
            FloodEveningHub.SayRequest += OnSayRequest;

            RegisterModules();
        }

        private void Start()
        {
            objectives = ObjectiveTracker.Ensure();
            if (objectives != null) objectives.AddVisibilityBlocker(ObjectivesBlocked);
            FloodEveningHub.RequestReset();
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            FloodEveningHub.MinigameOpened -= OnPanelOpened;
            FloodEveningHub.MinigameClosed -= OnPanelClosed;
            FloodEveningHub.StepCompleted -= OnStep;
            FloodEveningHub.Mistake -= OnMistake;
            FloodEveningHub.SayRequest -= OnSayRequest;
            if (objectives != null) objectives.RemoveVisibilityBlocker(ObjectivesBlocked);
            if (player != null) player.SpeedMultiplier = 1f;
            FloodEveningHub.FreeRoam = false;
            FloodEveningHub.Paused = false;
            FloodEveningHub.Progress01 = 0f;
            Time.timeScale = 1f;
        }

        private bool ObjectivesBlocked() => finished || !freeRoam || saying || openPanels.Count > 0;

        private T Resolve<T>(T current) where T : Component
        {
            if (current != null) return current;
            var found = FindAnyObjectByType<T>(FindObjectsInactive.Include);
            return found != null ? found : gameObject.AddComponent<T>();
        }

        private void RegisterModules()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb is IFloodEveningWarning w && warning == null) warning = w;
                if (mb is IFloodEveningInteraction m && m.Point != null && !modules.Contains(m))
                {
                    modules.Add(m);
                    var module = m;
                    interactor.Register(m.Point, before: () => RunModule(module));
                    Log($"modül: {mb.GetType().Name} → [{m.Point.KeyLabel}] {m.Point.ActionLabel} ({m.Point.name})");
                }
            }
            if (warning == null) Log("uyarı modülü (IFloodEveningWarning) yok → yedek uyarı şeridi kullanılacak");
        }

        private IEnumerator RunModule(IFloodEveningInteraction m)
        {
            if (m == null || !m.CanUse || finished) yield break;
            yield return m.Play();
            yield return null; // paneli kapatan tuş yeni bir kullanımı başlatmasın
        }

        // =====================================================================
        // Akış
        // =====================================================================

        private IEnumerator Run()
        {
            // ---------------- Intro ----------------
            SetState(FloodEveningState.Intro);
            fader.SetOpaque(true);
            PlacePlayer();
            chapterCamera = Camera.main != null ? Camera.main.GetComponent<ChapterCamera>() : null;
            if (rain != null) rain.SetIntensityNow(rainRange.x * 0.6f);
            ObjEvent("FE_START");
            texts.FirstLine("FE_TITLE", out string cardTitle, out string cardSubtitle);
            yield return titleCard.Play(cardTitle, cardSubtitle, T(FloodEveningTexts.Keys.CardNote));
            titleCard.Hide(); // arkada perde siyah

            if (chapterCamera != null && introView != null) chapterCamera.ShowPoint(introView.position, true);
            else FollowPlayer(true);
            yield return fader.FadeIn(1.0f);
            if (rain != null) rain.SetIntensity(rainRange.x, 2.5f);
            if (introView != null && chapterCamera != null)
            {
                yield return Wait(1.2f);
                FollowPlayer(false); // kısa kurgu geçişi: mahalleden Cenk'e kayar
                yield return Wait(1.0f);
            }
            yield return Say(introLine, null);

            // ---------------- Warning (bir kez) ----------------
            SetState(FloodEveningState.Warning);
            GameAudio.PlaySfx("SFX_Phone_Notify", 0.8f);
            if (warning != null) yield return warning.ShowWarning();
            else yield return FallbackWarning();
            yield return null; // uyarıyı kapatan girdi bir sonraki etkileşimi tetiklemesin
            MarkStep("warning_read");
            yield return Say(afterWarningLine, null);

            // ---------------- Serbest yürüyüş ----------------
            SetState(FloodEveningState.RouteChoice);
            ObjEvent("FE_ROUTE");
            hud.SetGaugeVisible(showWaterGauge);
            elapsed = 0f;
            playSeconds = 0f;
            running = true;
            SetFreeRoam(true);
            Log("serbest yürüyüş başladı (süre baskısı yok)");

            while (!finished) yield return null;
        }

        private IEnumerator FallbackWarning()
        {
            Log("yedek uyarı şeridi (FloodWarningPhone bağlı değil)");
            hud.ShowToast(T(FloodEveningTexts.Keys.WarningFallbackTitle), T(FloodEveningTexts.Keys.WarningFallbackBody),
                FloodEveningHud.ToastKind.Warning, 6f);
            yield return null;
            float t = 0f;
            while (t < 1f || !GameInput.ContinuePressed) { t += Time.unscaledDeltaTime; if (t > 8f) break; yield return null; }
        }

        private void Update()
        {
            if (finished) return;
            UpdateControl();
            if (!running) return;

            playSeconds += Time.deltaTime;
            bool paused = ComputePaused();
            FloodEveningHub.Paused = paused;
            if (!paused) elapsed += Time.deltaTime;
            float p = Progress01;
            FloodEveningHub.Progress01 = p;
            hud.SetLevel(p);
            UpdateWeather(p);

            if (player != null)
            {
                if (!forkSaid && forkPoint != null && player.GroundDistanceTo(forkPoint.position) <= forkRadius)
                {
                    forkSaid = true;
                    QueueLine(forkLine, null);
                }
                if (!shortRoadTried && InsideAny(shortRoadClosedAreas))
                {
                    // Kısa yol kapalı: hata değil, nötr bilgi. Replik bir kez; geri dönüş oyuncunun yürüyüşüyle.
                    shortRoadTried = true;
                    Log("kısa yol denendi (kapalı) — nötr kayıt");
                    ObjEvent("short_road_tried");
                    QueueLine(shortClosedLine, null);
                }
                onHighRoad |= InsideAny(highRoadAreas);
                if (!onHighRoad && (highRoadAreas == null || highRoadAreas.Length == 0) && highRoadPoint != null)
                    onHighRoad = player.GroundDistanceTo(highRoadPoint.position) <= highRoadRadius;
            }
            UpdateDerivedState();

            if (state == FloodEveningState.FinalApproach && (InsideAny(safeZoneAreas) || steps.Contains("safe_zone"))) Finish();
        }

        private bool ComputePaused()
        {
            if (!running || finished || saying || !freeRoam) return true;
            foreach (var id in openPanels)
                foreach (var prefix in readingPanels)
                    if (!string.IsNullOrEmpty(prefix) && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Hareket ve [E] ipucu: yalnız serbest dolaşmada, diyalog/panel/olay kapalıyken.</summary>
        private void UpdateControl()
        {
            bool control = freeRoam && !finished && !saying && openPanels.Count == 0 && !interactor.IsBusy;
            FloodEveningHub.FreeRoam = control;
            interactor.InteractionEnabled = freeRoam && !finished && !saying;
            if (control != lastControl && player != null)
            {
                player.CanMove = control;
                if (!control) player.SpeedMultiplier = 1f;
            }
            lastControl = control;
            if (objectives != null) objectives.SetDialogueOpen(saying);

            foreach (var m in modules)
                if (m != null && m.Point != null) m.Point.SetAvailable(m.CanUse && !finished);

            if (sayRoutine == null && sayQueue.Count > 0 && freeRoam && openPanels.Count == 0 && !interactor.IsBusy && !finished)
                sayRoutine = StartCoroutine(PlayQueuedLines());
        }

        /// <summary>Durum adımlardan türetilir: ağaç düştü → Obstacle, dal itildi → FinalApproach; uzun yola girildi → LongRoad.</summary>
        private void UpdateDerivedState()
        {
            FloodEveningState derived = FloodEveningState.RouteChoice;
            if (steps.Contains("branch_cleared")) derived = FloodEveningState.FinalApproach;
            else if (steps.Contains("tree_fell")) derived = FloodEveningState.Obstacle;
            else if (onHighRoad || steps.Contains("high_road")) derived = FloodEveningState.LongRoad;
            if (derived <= state) return;

            SetState(derived);
            switch (derived)
            {
                case FloodEveningState.LongRoad:
                    MarkStep("high_road");
                    break;
                case FloodEveningState.Obstacle:
                    MarkStep("high_road");
                    ObjEvent("FE_OBSTACLE");
                    break;
                case FloodEveningState.FinalApproach:
                    MarkStep("high_road");
                    ObjEvent("FE_FINAL");
                    QueueLine(finalLine, null);
                    break;
            }
        }

        private void UpdateWeather(float p)
        {
            if (rain == null) return;
            float target = Mathf.Lerp(rainRange.x, rainRange.y, p);
            if (Mathf.Abs(target - lastRainSet) < 0.02f) return;
            lastRainSet = target;
            rain.SetIntensity(target, 1.5f);
        }

        // =====================================================================
        // Sonuç
        // =====================================================================

        private void Finish()
        {
            if (finished) return;
            finished = true;
            running = false;
            FloodEveningHub.Paused = true;
            FloodEveningHub.FreeRoam = false;
            SetState(FloodEveningState.Success);
            MarkStep("safe_zone");
            Log($"DEVAM NOKTASI: güvenli bölgeye varıldı (oynanış {playSeconds:0.0} sn, kısa yol {(shortRoadTried ? "denendi" : "denenmedi")})");
            StartCoroutine(EndSequence());
        }

        private IEnumerator EndSequence()
        {
            SetFreeRoam(false);
            interactor.InteractionEnabled = false;
            if (player != null) { player.CanMove = false; player.SpeedMultiplier = 1f; }
            sayQueue.Clear();
            while (saying) yield return null;
            yield return Wait(0.4f);
            hud.ShowToast(T(FloodEveningTexts.Keys.ToastSafeTitle), T(FloodEveningTexts.Keys.ToastSafeBody), FloodEveningHud.ToastKind.Info, 3f);
            yield return Say(safeLine, null);
            yield return Wait(0.8f);
            hud.HideAll();

            result = BuildResult();
            Log("sonuç: " + result.title + " · doğru " + result.correct.Count + ", bilgi " + result.corrected.Count);
            yield return resultView.Show(result);
            Log("seçim: " + result.choice);
            Time.timeScale = 1f;
            FloodEveningHub.Paused = false;
            if (result.choice == FloodEveningResultView.ChoiceRetry)
            {
                // Sel tekrarı depremi başlatmaz: şu anki bölüm Sel → FloodEvening baştan.
                GameSession.StartChapter(ChapterId.Flood);
                GameSceneRouter.RestartCurrentChapter();
            }
            else GameSceneRouter.ReturnToMenu();
        }

        /// <summary>
        /// Sonuç verisi — metinlerin hepsi flood_evening.json'dan: başlık (result.title_success), doğru kararlar
        /// (FE_RESULT_STEPS, speaker = adım id'si), nötr bilgiler / düzeltilen yanlışlar (FE_RESULT_MISTAKES, speaker = id;
        /// kısa yol: "short_road_tried"), öğrenilenler (result.learned) ve not.
        /// </summary>
        private FloodEveningResult BuildResult()
        {
            var r = new FloodEveningResult { success = true };
            r.title = texts.ResultTitle(true, mistakes.Count > 0);
            r.message = T(FloodEveningTexts.Keys.ResultSuccessMessage);
            r.emptyCorrect = T(FloodEveningTexts.Keys.ResultNoCorrect);

            string[] stepOrder = { "warning_read", "high_road", "rumor_checked", "branch_cleared", "gate_opened", "code_opened", "safe_zone" };
            foreach (var id in stepOrder)
            {
                if (!steps.Contains(id)) continue;
                if (id == "rumor_checked" && MistakeCount("rumor_trusted") > 0) continue;
                string line = texts.SceneLine("FE_RESULT_STEPS", id);
                if (!string.IsNullOrEmpty(line)) r.correct.Add(line);
            }

            if (shortRoadTried)
            {
                string info = texts.SceneLine("FE_RESULT_MISTAKES", "short_road_tried");
                if (!string.IsNullOrEmpty(info)) r.corrected.Add(info);
            }
            foreach (var kv in mistakes)
            {
                string line = texts.SceneLine("FE_RESULT_MISTAKES", kv.Key);
                if (!string.IsNullOrEmpty(line)) r.corrected.Add(kv.Value > 1 ? line + " (x" + kv.Value + ")" : line);
            }

            r.lessons.AddRange(texts.Learned);
            r.note = texts.Note;
            return r;
        }

        // =====================================================================
        // Hub olayları
        // =====================================================================

        private void OnPanelOpened(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            openPanels.Add(id);
            Log($"panel açıldı: {id}");
        }

        private void OnPanelClosed(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            openPanels.Remove(id);
            Log($"panel kapandı: {id}");
        }

        private void OnStep(string id) => MarkStep(id);

        private void MarkStep(string id)
        {
            if (string.IsNullOrEmpty(id) || !steps.Add(id)) return;
            Log("adım: " + id);
            ObjEvent(id);
        }

        private void OnMistake(string id)
        {
            if (string.IsNullOrEmpty(id) || finished) return;
            // Zikzak rota: kısa yolu denemek hata sayılmaz (BarrierStop eski sürümü "low_road" bildirirse nötr bilgiye çevrilir).
            if (id == "low_road")
            {
                if (!shortRoadTried) { shortRoadTried = true; ObjEvent("short_road_tried"); }
                return;
            }
            mistakes[id] = MistakeCount(id) + 1;
            Log($"yanlış: {id} (x{mistakes[id]})");
            if (objectives != null) objectives.Notify("event", id, false);
        }

        private void OnSayRequest(string lineId, string fallback)
        {
            if (finished) return;
            QueueLine(lineId, fallback);
        }

        private void QueueLine(string lineId, string fallback)
        {
            if (string.IsNullOrEmpty(lineId) && string.IsNullOrEmpty(fallback)) return;
            sayQueue.Enqueue(new KeyValuePair<string, string>(lineId, fallback));
        }

        private IEnumerator PlayQueuedLines()
        {
            while (sayQueue.Count > 0 && !finished)
            {
                var item = sayQueue.Dequeue();
                yield return Say(item.Key, item.Value);
            }
            sayRoutine = null;
        }

        // =====================================================================
        // Yardımcılar
        // =====================================================================

        /// <summary>Diyalog satırı (hareket kilitli). Veride yoksa yedek metin uyarı şeridinde gösterilir.</summary>
        private IEnumerator Say(string lineId, string fallback)
        {
            bool has = texts.HasScene(lineId) && HasStoryScene(lineId);
            if (!has && string.IsNullOrEmpty(fallback))
            {
                if (!string.IsNullOrEmpty(lineId)) Log($"(diyalog yok) {lineId}");
                yield break;
            }
            saying = true;
            UpdateControl();
            try
            {
                if (has) yield return interactor.Say(lineId);
                else
                {
                    hud.ShowToast(T(FloodEveningTexts.Keys.ToastSayTitle), fallback, FloodEveningHud.ToastKind.Danger, 3.5f);
                    yield return Wait(1.2f);
                }
            }
            finally
            {
                saying = false;
            }
            yield return null; // diyaloğu kapatan tuş başka bir şeyi tetiklemesin
        }

        private bool HasStoryScene(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var adapter = dialogue as DialogueRunnerAdapter;
            var data = adapter != null && adapter.Runner != null ? adapter.Runner.Data : null;
            return data != null && data.scenes != null && Array.Exists(data.scenes, s => s.id == id);
        }

        private void SetFreeRoam(bool value)
        {
            freeRoam = value;
            UpdateControl();
        }

        private void SetState(FloodEveningState next)
        {
            if (state == next && next != FloodEveningState.Intro) return;
            state = next;
            Log($"durum → {next}");
        }

        private void PlacePlayer()
        {
            if (player == null) return;
            if (spawn != null) player.PlaceAt(spawn.position, spawnFacing);
            FollowPlayer(true);
        }

        private void FollowPlayer(bool cut)
        {
            if (chapterCamera == null && Camera.main != null) chapterCamera = Camera.main.GetComponent<ChapterCamera>();
            if (chapterCamera == null || player == null) return;
            if (cameraBounds != null) chapterCamera.Follow(player.transform, cameraBounds, cut);
        }

        private bool InsideAny(Collider2D[] areas)
        {
            if (player == null || areas == null) return false;
            Vector2 feet = player.transform.position;
            foreach (var c in areas)
                if (c != null && c.isActiveAndEnabled && c.OverlapPoint(feet)) return true;
            return false;
        }

        private Collider2D[] CollidersUnder(string objectName)
        {
            var t = FindNamed<Transform>(objectName);
            if (t == null) return Array.Empty<Collider2D>();
            var list = new List<Collider2D>();
            foreach (var c in t.GetComponentsInChildren<Collider2D>(true))
                if (c.isTrigger) list.Add(c);
            return list.ToArray();
        }

        private T FindNamed<T>(string objectName) where T : Component
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<T>(true))
                    if (c.gameObject.name == objectName && !IsUnused(c.transform)) return c;
            return null;
        }

        /// <summary>ENV v3: rotadan çıkan parçalar "Unused" altında durur; çapalar oradan alınmaz.</summary>
        private static bool IsUnused(Transform t)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == "Unused") return true;
            return false;
        }

        private void ObjEvent(string key)
        {
            if (objectives != null) objectives.Notify("event", key);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        private void Log(string message)
        {
            if (logSteps) Debug.Log($"[FE] t={playSeconds:0.0} {message}", this);
        }
    }
}
