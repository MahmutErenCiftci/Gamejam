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
        FinalApproach,  // "Yol açıldı." → son sokak (J) → apartmanın ön bahçesi (avlu)
        Finale,         // avluya (SafeZone) varıldı: apartman finali (posta kutusu, PIN, kapı kolu); sel YAVAŞLAYARAK sürer, avlu da dolar
        Success,        // Cenk eve girdi (final yoksa: avluya varıldı) → bitiş ekranı
        Failed          // sel sayacı (85 sn) doldu, eve girilemedi → başarısız ekranı
    }

    /// <summary>
    /// AKŞAM SEL BÖLÜMÜ — FloodEvening sahnesinin tek akış/sonuç sorumlusu (LEAD).
    /// Intro → Warning → RouteChoice → (kısa yol: köşeden sonra kapalı, bir kez "Yol kapanmış..." repliği, oyuncu YÜRÜYEREK döner;
    /// hata sayılmaz, raporda nötr bilgi) → LongRoad → Obstacle (EVENTS'in ağaç düşüşü + dal itme) → FinalApproach → Success.
    /// Durum geri gitmez; modüllerin Hub adımlarından ve oyuncunun konumundan (HighRoad, BarrierZone, SafeZone) türetilir.
    /// SEL SAYACI (lider kararı 18 Eki; 85 sn, P-FLOOD): telefon uyarısı kapandığı an TEK bir sayaç başlar (StartFloodSequence; tekrar
    /// çağrılsa da ikinci sayaç olmaz). FloodEveningHub.Progress01 = geçen / floodSeconds bütün sistemlerin tek kaynağıdır: sağdaki gösterge,
    /// yağmur, haritadaki su (FloodWaterField), yol kapanmaları, sesler. Süre yalnız oynanışta akar: diyalog, okuma panelleri ve
    /// zorunlu sinematiklerde (ağaç düşüşü) durur; mini oyunlarda (dal itme) ve çevre tehlikelerinde (yüzen enkaz, elektrik çarpması tepkisi) akar; Time.timeScale = 0 (pause) durdurur.
    /// Avluya (SafeZone) varınca sayaç DURMAZ (lider kararı 18 Eki öğlen): apartman finali (IFloodEveningFinale) oynanırken sel
    /// finaleRate hızıyla (ör. 0,15x) sürer, avlu da su alır; sayaç ancak Cenk eve girince (home_entered) durur → bitiş ekranı.
    /// Süre dolarsa (eve girilmediyse; finalde de) Failed: kritik uyarı + başarısız ekranı; Tekrar Dene sahneyi baştan kurar.
    /// Rotadan çıkan modüller (şifre, bahçe kapısı, ipuçları, söylenti) sahnede yoksa akış sorunsuz çalışır;
    /// sahneye konursa IFloodEveningInteraction olarak yine kaydedilir.
    /// </summary>
    public class FloodEveningController : MonoBehaviour
    {
        [Header("Sel sayacı (tek zaman kaynağı)")]
        [Tooltip("Uyarıdan sonra selin kritik seviyeye (%100) varma süresi (oynanışta geçen sn)")]
        [SerializeField] private float floodSeconds = 85f;
        [Tooltip("Apartman finalinde (avludan eve girene kadar) sel sayacının hız çarpanı: sel sürer ama adil kalır")]
        [SerializeField, Range(0.05f, 1f)] private float finaleRate = 0.15f;
        [Tooltip("Süre dolunca bölüm başarısız olur (kapalıysa su %100'de kalır, oyun sürer)")]
        [SerializeField] private bool failOnTimeout = true;
        [Tooltip("Sağ kenardaki SEL DURUMU göstergesi")]
        [SerializeField] private bool showFloodGauge = true;
        [Tooltip("Sayacı durduran zorunlu sinematikler — Hub.NotifyOpened id öneki (ağaç düşüşü vb.)")]
        [SerializeField] private string[] cinematicPanels = { "treefall", "cine" };

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
        [SerializeField] private Vector2 rainCurveRange = new Vector2(0.22f, 1f);
        [Tooltip("Yağmur artış eğrisi üssü (>1: başta hafif, sonda güçlü)")]
        [SerializeField] private float rainExponent = 1.35f;

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
        private float elapsed;       // sel sayacı: oynanışta geçen süre (tek zaman kaynağı)
        private float playSeconds;   // uyarıdan sonra geçen toplam süre (bilgi)
        private bool running;
        private bool floodStarted;   // sayaç bir kez başlar
        private bool floodStopped;   // güvenli bölge ya da süre doldu
        private float debugTimeScale = 1f;
        private IFloodEveningFinale finale;
        private bool finished;
        private bool arrivedFromSchool; // FLOOD-START: okul çıkışından gelindi (kart yok, Cenk yürüyerek girer)
        private float schoolRain = -1f;
        private bool freeRoam;
        private bool saying;
        private bool lastControl;
        private bool onHighRoad;
        private bool forkSaid;
        private bool shortRoadTried;
        private float lastRainSet = -1f;
        private bool treeFocus;
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
        public float FloodSeconds => floodSeconds;
        public float FinaleRate => finaleRate;
        /// <summary>Avluya varıldığı andaki sayaç (sn; varılmadıysa -1). Test ölçümü için.</summary>
        public float YardElapsed { get; private set; } = -1f;
        public float Progress01 => Mathf.Clamp01(elapsed / Mathf.Max(1f, floodSeconds));
        public bool FloodStarted => floodStarted;
        public bool FloodStopped => floodStopped;
        public bool IsFinished => finished;
        public bool IsPaused => ComputePaused();
        public bool ShortRoadTried => shortRoadTried;
        public FloodEveningResult Result => result;
        public bool HasStep(string id) => steps.Contains(id);
        public int MistakeCount(string id) => mistakes.TryGetValue(id, out int n) ? n : 0;

        /// <summary>Yalnız test: sel sayacını ayarlar (Editor / Development build).</summary>
        public void DebugSetElapsed(float seconds)
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            elapsed = Mathf.Clamp(seconds, 0f, floodSeconds);
        }

        /// <summary>Yalnız test: sel ilerlemesini 0..1 ayarlar (%0, %25, %50, %75, %100).</summary>
        public void DebugSetProgress(float progress01) => DebugSetElapsed(Mathf.Clamp01(progress01) * floodSeconds);

        /// <summary>Yalnız test: sayacın akış hızı (1 = normal, 4 = dört kat).</summary>
        public void DebugSetTimeScale(float scale)
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            debugTimeScale = Mathf.Clamp(scale, 0f, 20f);
        }

        public float DebugTimeScale => debugTimeScale;

        // =====================================================================
        // Kurulum
        // =====================================================================

        private void Awake()
        {
            // Bölüm her açılışta temiz başlar: bayraklar (backpack_taken, raincoat_taken, quake_bag_taken, görevler) silinir.
            // Lider kararı: Cenk deprem modundaki görünümüyle (çantasız yelekli; yağmurluk/çanta yok) oynar.
            // FLOOD-START: okul çıkışından (FloodSchool) mı gelindi? Bayraklar silinmeden önce okunur (bkz. ArriveFromSchool).
            arrivedFromSchool = FloodSchoolHandoff.ConsumeArrival(out schoolRain);
            GameSession.StartChapter(ChapterId.Flood);
            CenkOutfit.RefreshAll();

            Time.timeScale = 1f;
            FloodEveningHub.Progress01 = 0f;
            FloodEveningHub.Paused = true;
            FloodEveningHub.FreeRoam = false;
            FloodEveningHub.FloodStarted = false;
            FloodEveningHub.FloodStopped = false;
            FloodEveningHub.FloodSeconds = floodSeconds;
            ObjectiveHudView.ProgressWord = "İLERLEME"; // P-FLOOD: görev panelinde "HAZIRLIK" yerine (deprem değil)
            FloodEveningHub.OutdoorAudio01 = 1f; // önceki oyunda eve girerken 0'a inmişti (Tekrar Dene / menüden yeniden giriş)

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
            FloodEveningHub.OutdoorAudio01 = 1f;
            FloodEveningHub.CinematicActive = false;
            ObjectiveHudView.ProgressWord = "HAZIRLIK";
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
                if (mb is IFloodEveningFinale f && finale == null) { finale = f; Log("apartman finali: " + mb.GetType().Name); }
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
            if (rain != null) rain.SetIntensityNow(rainCurveRange.x * 0.6f);
            ObjEvent("FE_START");
            if (arrivedFromSchool)
                yield return ArriveFromSchool(); // FLOOD-START: kart zaten okulda gösterildi
            else
            {
                texts.FirstLine("FE_TITLE", out string cardTitle, out string cardSubtitle);
                yield return titleCard.Play(cardTitle, cardSubtitle, T(FloodEveningTexts.Keys.CardNote));
                titleCard.Hide(); // arkada perde siyah

                if (chapterCamera != null && introView != null) chapterCamera.ShowPoint(introView.position, true);
                else FollowPlayer(true);
                yield return fader.FadeIn(1.0f);
                if (rain != null) rain.SetIntensity(rainCurveRange.x, 2.5f);
                if (introView != null && chapterCamera != null)
                {
                    yield return Wait(1.2f);
                    FollowPlayer(false); // kısa kurgu geçişi: mahalleden Cenk'e kayar
                    yield return Wait(1.0f);
                }
            }
            yield return Say(introLine, null);

            // ---------------- Warning (bir kez) ----------------
            SetState(FloodEveningState.Warning);
            GameAudio.PlaySfx("SFX_Phone_Notify", 0.8f);
            GameAudio.PlaySfx("SFX_UI_Phone_Vibrate", 0.7f); // P-FLOOD: bildirimle birlikte titreşim
            if (warning != null) yield return warning.ShowWarning();
            else yield return FallbackWarning();
            yield return null; // uyarıyı kapatan girdi bir sonraki etkileşimi tetiklemesin
            MarkStep("warning_read");
            StartFloodSequence(); // bildirim bitti → tek sayaç; ardından gelen replik sırasında süre durur
            yield return Say(afterWarningLine, null);

            // ---------------- Serbest yürüyüş ----------------
            SetState(FloodEveningState.RouteChoice);
            ObjEvent("FE_ROUTE");
            SetFreeRoam(true);
            Log("serbest yürüyüş başladı (sel sayacı " + floodSeconds.ToString("0") + " sn)");

            while (!finished) yield return null;
        }

        /// <summary>
        /// OnFloodAlertCompleted → bütün sel sistemlerini başlatan TEK olay. Tekrar çağrılırsa hiçbir şey yapmaz (ikinci sayaç olmaz).
        /// Gösterge, yağmur, su ve sesler FloodEveningHub.Progress01'i okur; burada yalnız sayaç ve gösterge açılır.
        /// </summary>
        public void StartFloodSequence()
        {
            if (floodStarted || finished) return;
            floodStarted = true;
            elapsed = 0f;
            playSeconds = 0f;
            running = true;
            FloodEveningHub.FloodStarted = true;
            FloodEveningHub.Progress01 = 0f;
            hud.SetGaugeVisible(showFloodGauge);
            hud.SetFloodTime(0f, floodSeconds);
            Log("SEL SAYACI BAŞLADI");
        }

        // ---------------- FLOOD-START: okul çıkışından geliş ----------------

        /// <summary>
        /// Okul çıkışı + köprü (FloodSchool) bitti: "SEL BÖLÜMÜ" kartı orada gösterildi, burada tekrar edilmez.
        /// Cenk okuldan çıktığı yönde (Spawn yönü, sağ yukarı) yürüyerek başlangıç noktasına gelir, kararma bu sırada açılır;
        /// yağmur okuldaki şiddetle sürer, sonra sel eğrisinin başlangıcına iner. Telefon uyarısı ve 120 sn sel sayacı
        /// bundan sonra (Warning) her zamanki gibi başlar: okul bölümünde süre yoktur.
        /// </summary>
        private IEnumerator ArriveFromSchool()
        {
            Log("okul çıkışından gelindi: başlık kartı atlandı, Cenk sokağa yürüyerek giriyor");
            float startRain = schoolRain >= 0f ? schoolRain : rainCurveRange.x;
            if (rain != null) rain.SetIntensityNow(startRain);
            Vector3 to = player != null ? player.transform.position : Vector3.zero;
            Vector2 dir = spawnFacing.sqrMagnitude > 0.0001f ? spawnFacing.normalized : new Vector2(0.894f, 0.447f);
            if (player != null) player.PlaceAt(to - (Vector3)(dir * 0.7f), spawnFacing);
            FollowPlayer(true);
            var fade = StartCoroutine(fader.FadeIn(0.9f));
            if (player != null)
            {
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    Vector2 left = (Vector2)(to - player.transform.position);
                    if (left.sqrMagnitude < 0.0025f) break;
                    player.ExternalVelocity = left.normalized * 1.5f;
                    yield return null;
                }
                player.ExternalVelocity = Vector2.zero;
                player.PlaceAt(to, spawnFacing);
            }
            yield return fade;
            if (rain != null) rain.SetIntensity(rainCurveRange.x, 2.5f);
            yield return Wait(0.3f);
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

            DebugKeys();
            playSeconds += Time.deltaTime;
            bool paused = ComputePaused();
            FloodEveningHub.Paused = paused;
            // Finalde (avlu → ev) sel yavaşlar ama durmaz (finaleRate).
            float rate = state == FloodEveningState.Finale ? finaleRate : 1f;
            if (!paused && !floodStopped) elapsed = Mathf.Min(floodSeconds, elapsed + Time.deltaTime * debugTimeScale * rate);
            float p = Progress01;
            FloodEveningHub.Progress01 = p;
            hud.SetLevel(p);
            hud.SetFloodTime(elapsed, floodSeconds);
            UpdateWeather(p);
            UpdateStageWarnings(p);

            if (state == FloodEveningState.Finale && ((finale != null && finale.IsComplete) || steps.Contains("home_entered"))) { Finish(); return; }
            // süre doluşu yalnız oynanışta sonuçlanır: diyalog / okuma paneli / sinematik açıkken (ör. F9 ile) panel kapanınca.
            // Apartman finalinde de geçerli (çok yavaş oynanırsa su avluyu basar).
            if (!paused && !floodStopped && failOnTimeout && elapsed >= floodSeconds) { Fail(); return; }
            if (state == FloodEveningState.Finale) return;

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

            if (state == FloodEveningState.FinalApproach && (InsideAny(safeZoneAreas) || steps.Contains("safe_zone"))) ReachSafeZone();
        }

        /// <summary>Sayaç durur mu: diyalog, okuma paneli, zorunlu sinematik; mini oyun ve çevre tehlikesi tepkileri süreyi DURDURMAZ.</summary>
        private bool ComputePaused()
        {
            if (!running || finished || saying || !freeRoam) return true;
            // modülün kendi oynattığı diyalog da (ör. dal itilince "Geçit açıldı...") süreyi durdurur
            var adapter = dialogue as DialogueRunnerAdapter;
            if (adapter != null && adapter.Runner != null && adapter.Runner.IsBusy) return true;
            foreach (var id in openPanels)
            {
                foreach (var prefix in readingPanels)
                    if (!string.IsNullOrEmpty(prefix) && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
                foreach (var prefix in cinematicPanels)
                    if (!string.IsNullOrEmpty(prefix) && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Geliştirici kısayolları (yalnız Editor / Development build): F5..F9 = %0/25/50/75/100, F10 = sayaç x4 aç/kapa.</summary>
        private void DebugKeys()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (GameInput.DevKeyPressed(UnityEngine.InputSystem.Key.F5)) DebugSetProgress(0f);
            else if (GameInput.DevKeyPressed(UnityEngine.InputSystem.Key.F6)) DebugSetProgress(0.25f);
            else if (GameInput.DevKeyPressed(UnityEngine.InputSystem.Key.F7)) DebugSetProgress(0.5f);
            else if (GameInput.DevKeyPressed(UnityEngine.InputSystem.Key.F8)) DebugSetProgress(0.75f);
            else if (GameInput.DevKeyPressed(UnityEngine.InputSystem.Key.F9)) DebugSetProgress(1f);
            else if (GameInput.DevKeyPressed(UnityEngine.InputSystem.Key.F10)) DebugSetTimeScale(debugTimeScale > 1f ? 1f : 4f);
        }

        /// <summary>Hareket ve [E] ipucu: yalnız serbest dolaşmada, diyalog/panel/olay kapalıyken.</summary>
        private void UpdateControl()
        {
            bool control = freeRoam && !finished && !saying && openPanels.Count == 0 && !interactor.IsBusy;
            bool cine = false;
            foreach (var id in openPanels) foreach (var prefix in cinematicPanels) if (!string.IsNullOrEmpty(prefix) && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) cine = true;
            FloodEveningHub.CinematicActive = cine;
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
            float target = Mathf.Lerp(rainCurveRange.x, rainCurveRange.y, Mathf.Pow(Mathf.Clamp01(p), Mathf.Max(0.1f, rainExponent)));
            if (Mathf.Abs(target - lastRainSet) < 0.02f) return;
            lastRainSet = target;
            rain.SetIntensity(target, 1.5f);
        }

        // =====================================================================
        // Sonuç
        // =====================================================================

        /// <summary>
        /// Avluya (SafeZone) varıldı. Final varsa başlar ve sel YAVAŞLAYARAK sürer (finaleRate; avlu da su alır, sayaç eve girince durur);
        /// final yoksa sayaç durur ve bitiş.
        /// </summary>
        private void ReachSafeZone()
        {
            if (floodStopped || finished || state == FloodEveningState.Finale) return;
            MarkStep("safe_zone");
            YardElapsed = elapsed;
            GameAudio.PlaySfx("SFX_FE_SafeZone", 0.75f);
            Log($"AVLU: apartmana varıldı (sayaç {elapsed:0.0}/{floodSeconds:0} sn = %{Progress01 * 100f:0}, oynanış {playSeconds:0.0} sn, kısa yol {(shortRoadTried ? "denendi" : "denenmedi")})");
            if (finale == null) { floodStopped = true; FloodEveningHub.FloodStopped = true; Finish(); return; }
            SetState(FloodEveningState.Finale);
            Log($"final: sel x{finaleRate:0.00} hızla sürüyor");
            hud.ShowToast(UiOr(KeyToastHomeTitle, "APARTMANINA ULAŞTIN"), UiOr(KeyToastFinaleBody, "Su avluya da geliyor. Hemen kapıyı aç, eve gir!"),
                FloodEveningHud.ToastKind.Warning, 3f);
            finale.Begin();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            running = false;
            FloodEveningHub.Paused = true;
            FloodEveningHub.FreeRoam = false;
            bool viaFinale = state == FloodEveningState.Finale;
            if (!floodStopped) { floodStopped = true; FloodEveningHub.FloodStopped = true; MarkStep("safe_zone"); }
            hud.SetFloodSafe(true); // eve girildi: sayaç durdu, gösterge yeşil
            SetState(FloodEveningState.Success);
            Log($"BİTTİ: {(viaFinale ? "Cenk eve girdi" : "güvenli bölgeye varıldı")} (sayaç {elapsed:0.0} sn = %{Progress01 * 100f:0}, avlu %{(YardElapsed >= 0f ? YardElapsed / floodSeconds * 100f : -1f):0}, oynanış {playSeconds:0.0} sn)");
            StartCoroutine(EndSequence(viaFinale));
        }

        /// <summary>Sel sayacı doldu: kritik uyarı → başarısız ekranı (Tekrar Dene sahneyi baştan kurar; bütün sel durumu sıfırlanır).</summary>
        private void Fail()
        {
            if (finished) return;
            finished = true;
            running = false;
            floodStopped = true;
            FloodEveningHub.FloodStopped = true;
            FloodEveningHub.Progress01 = 1f;
            FloodEveningHub.Paused = true;
            FloodEveningHub.FreeRoam = false;
            SetState(FloodEveningState.Failed);
            Log($"BAŞARISIZ: sel kritik seviyede (oynanış {playSeconds:0.0} sn, durum {state})");
            StartCoroutine(FailSequence());
        }

        private IEnumerator FailSequence()
        {
            SetFreeRoam(false);
            interactor.InteractionEnabled = false;
            if (player != null) { player.CanMove = false; player.SpeedMultiplier = 1f; player.ExternalVelocity = Vector2.zero; }
            sayQueue.Clear();
            FloodEveningHub.RequestReset(); // açık mini oyun panelleri kapanır, çevre tehlikeleri durur
            // [E] etkileşimi sürüyorsa (ör. dal basılı tutuluyor) durdur: süre dolduktan sonra tamamlanıp
            // sonuç ekranının üstünde diyalog açmasın / doğru karar sayılmasın
            interactor.StopAllCoroutines();
            hud.SetLevelNow(1f);
            hud.SetFloodCritical(true);
            GameAudio.PlaySfx("SFX_Flood_Critical", 0.9f);
            if (chapterCamera != null) StartCoroutine(Shake(0.6f, 0.05f));
            hud.ShowToast(UiOr(KeyFailTitle, "SEL KRİTİK SEVİYEYE ULAŞTI"), UiOr(KeyFailBody, "Güvenli bölgeye zamanında ulaşamadın."),
                FloodEveningHud.ToastKind.Danger, 3f);
            yield return Wait(2.6f);
            hud.HideAll();

            var r = new FloodEveningResult { success = false };
            r.title = UiOr(KeyFailTitle, "SEL KRİTİK SEVİYEYE ULAŞTI");
            r.message = UiOr(KeyFailBody, "Güvenli bölgeye zamanında ulaşamadın.");
            r.emptyCorrect = T(FloodEveningTexts.Keys.ResultNoCorrect);
            CollectSteps(r);
            r.lessons.AddRange(texts.Learned);
            r.note = texts.Note;
            result = r;
            resultView.SetLabels(T(FloodEveningTexts.Keys.ResultHeader), T(FloodEveningTexts.Keys.ResultCorrectHeader),
                T(FloodEveningTexts.Keys.ResultCorrectedHeader), T(FloodEveningTexts.Keys.ResultLessonsHeader),
                "[R] " + UiOr(KeyFailRetry, "Tekrar Dene"), T(FloodEveningTexts.Keys.ResultMenu));
            yield return resultView.Show(result);
            Log("seçim: " + result.choice);
            Time.timeScale = 1f;
            FloodEveningHub.Paused = false;
            if (result.choice == FloodEveningResultView.ChoiceRetry)
            {
                GameSession.StartChapter(ChapterId.Flood);
                // FLOOD-START: okuldan gelindiyse mahalle baştan (okul/köprü tekrar oynanmaz), değilse bölüm baştan.
                if (!FloodSchoolHandoff.RetryInStreet()) GameSceneRouter.RestartCurrentChapter();
            }
            // P-UI: başarısız raporda da "Deprem Bölümüne Geç / Oyunu Bitir" var; sel tamamlanmış sayılmaz (GameFlow "geçildi").
            else if (result.choice == FloodEveningResultView.ChoiceNext) GameFlow.ContinueAfter(ChapterId.Flood);
            else GameSceneRouter.ReturnToMenu();
        }

        private IEnumerator FadeOutdoorAudio(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                FloodEveningHub.OutdoorAudio01 = 1f - t / seconds;
                yield return null;
            }
            FloodEveningHub.OutdoorAudio01 = 0f;
        }

        /// <summary>Aşama uyarıları (ilerleme eşikleri): üst şerit + %75 ve %90'da kısa uyarı sesi. Her eşik bir kez.</summary>
        private void UpdateStageWarnings(float p)
        {
            if (floodStopped || finished) return;
            while (stageShown < StageAt.Length && p >= StageAt[stageShown])
            {
                int i = stageShown++;
                if (p - StageAt[i] > 0.08f) continue; // test kısayoluyla atlanan eşikler sessiz geçer
                switch (i)
                {
                    case 0: if (state == FloodEveningState.Finale) break; hud.ShowToast(T(FloodEveningTexts.Keys.Toast2Title), T(FloodEveningTexts.Keys.Toast2Body), FloodEveningHud.ToastKind.Info, 3f); break;
                    case 1: if (state == FloodEveningState.Finale) break; hud.ShowToast(T(FloodEveningTexts.Keys.Toast3Title), T(FloodEveningTexts.Keys.Toast3Body), FloodEveningHud.ToastKind.Warning, 3.5f); break;
                    case 2:
                        GameAudio.PlaySfx("SFX_Flood_Warn", 0.8f);
                        if (state == FloodEveningState.Finale) break; // finalde yalnız ses (finale özel şerit %90'da)
                        hud.ShowToast(UiOr(KeyCriticalTitle, "KRİTİK TEHLİKE"), UiOr(KeyCriticalBody, "Bazı yollar suyla kapanıyor. Hemen güvenli bölgeye ilerle!"),
                            FloodEveningHud.ToastKind.Danger, 3.5f);
                        break;
                    case 3:
                        GameAudio.PlaySfx("SFX_Flood_Warn", 0.9f);
                        if (state == FloodEveningState.Finale)
                            hud.ShowToast(UiOr(KeyHurryTitle, "SU HIZLA YÜKSELİYOR"), UiOr(KeyHurryFinaleBody, "Su avluyu basıyor. Kapıyı aç, hemen içeri gir!"),
                                FloodEveningHud.ToastKind.Danger, 3f);
                        else
                            hud.ShowToast(UiOr(KeyHurryTitle, "SU HIZLA YÜKSELİYOR"), UiOr(KeyHurryBody, "Az zaman kaldı. Apartmanının avlusuna ulaş!"),
                                FloodEveningHud.ToastKind.Danger, 3f);
                        break;
                }
            }
        }

        private static readonly float[] StageAt = { 0.25f, 0.5f, 0.75f, 0.9f };
        private int stageShown;

        private IEnumerator Shake(float seconds, float amount)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = amount * (1f - t / seconds);
                chapterCamera.ShakeOffset = new Vector2(UnityEngine.Random.Range(-k, k), UnityEngine.Random.Range(-k, k) * 0.5f);
                yield return null;
            }
            chapterCamera.ShakeOffset = Vector2.zero;
        }

        private IEnumerator EndSequence(bool viaFinale)
        {
            // FLOW: Sel tamamlandı (Cenk eve girdi / güvenli bölge). Sonuç ekranının birincil butonu sıradaki afete
            // (deprem henüz oynanmadıysa "Devam: Deprem Bölümü") ya da oyun sonuna ("Oyunu Bitir") gider (GameFlow).
            GameFlow.CompleteChapter(ChapterId.Flood);
            SetFreeRoam(false);
            interactor.InteractionEnabled = false;
            if (player != null) { player.CanMove = false; player.SpeedMultiplier = 1f; }
            sayQueue.Clear();
            while (saying) yield return null;
            if (viaFinale)
            {
                // Cenk içeride: yağmur sesi ve görüntü yavaşça kararır, ardından bitiş ekranı.
                StartCoroutine(FadeOutdoorAudio(1.8f));
                yield return fader.FadeOut(1.6f);
                hud.HideAll();
            }
            else
            {
                yield return Wait(0.4f);
                hud.ShowToast(T(FloodEveningTexts.Keys.ToastSafeTitle), T(FloodEveningTexts.Keys.ToastSafeBody), FloodEveningHud.ToastKind.Info, 3f);
                yield return Say(safeLine, null);
                yield return Wait(0.8f);
                hud.HideAll();
            }

            result = BuildResult();
            if (viaFinale)
            {
                result.title = UiOr(KeyEndTitle, "EVE ULAŞTIN!");
                result.message = UiOr(KeyEndSubtitle, "EŞİK · SEL BÖLÜMÜ TAMAMLANDI");
            }
            Log("sonuç: " + result.title + " · doğru " + result.correct.Count + ", bilgi " + result.corrected.Count);
            resultView.SetPrimaryAction(FloodEveningResultView.ChoiceNext, GameFlow.ContinueLabel(ChapterId.Flood));
            yield return resultView.Show(result);
            Log("seçim: " + result.choice);
            Time.timeScale = 1f;
            FloodEveningHub.Paused = false;
            // Devam → GameFlow, Tekrar Dene → mahalle baştan (tamamlanma bilgisi kalır), Menü → oyun sıfırlanır.
            if (result.choice == FloodEveningResultView.ChoiceNext) GameFlow.ContinueAfter(ChapterId.Flood);
            else if (result.choice == FloodEveningResultView.ChoiceRetry)
            {
                // P-UI: rapor başarıda da Tekrar Dene sunar (deprem raporu gibi); tamamlanma bilgisi değişmez.
                GameSession.StartChapter(ChapterId.Flood);
                if (!FloodSchoolHandoff.RetryInStreet()) GameSceneRouter.RestartCurrentChapter();
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
            CollectSteps(r);

            r.lessons.AddRange(texts.Learned);
            r.note = texts.Note;
            return r;
        }

        /// <summary>Doğru kararlar + nötr bilgiler (kısa yol) + düzeltilen yanlışlar (başarı ve başarısızlık ekranı ortak).</summary>
        private void CollectSteps(FloodEveningResult r)
        {
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
        }

        // Sel sayacı / final metinleri: flood_evening.json "ui" içinde varsa oradan, yoksa buradaki Türkçe varsayılan.
        private const string KeyFailTitle = "flood_fail_title", KeyFailBody = "flood_fail_body", KeyFailRetry = "flood_fail_retry";
        private const string KeyToastHomeTitle = "toast_home_title", KeyToastHomeBody = "toast_home_body";
        private const string KeyEndTitle = "end_title", KeyEndSubtitle = "end_subtitle";
        private const string KeyCriticalTitle = "toast_critical_title", KeyCriticalBody = "toast_critical_body";
        private const string KeyHurryTitle = "toast_hurry_title", KeyHurryBody = "toast_hurry_body";
        private const string KeyToastFinaleBody = "toast_home_flood_body", KeyHurryFinaleBody = "toast_hurry_finale_body";

        private string UiOr(string key, string fallback) => texts.HasUi(key) ? texts.Ui(key) : fallback;

        // =====================================================================
        // Hub olayları
        // =====================================================================

        private void OnPanelOpened(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            openPanels.Add(id);
            Log($"panel açıldı: {id}");
            // P-FLOOD: ağaç düşüşünde kamera yumuşakça Cenk ile ağacın arasına kayar (ikisi de kadrajda), bitince Cenk'e döner
            if (id.StartsWith("treefall", StringComparison.OrdinalIgnoreCase) && chapterCamera != null && player != null)
            {
                var tree = FindAnyObjectByType<FloodTreeFallEvent>();
                if (tree != null) { treeFocus = true; chapterCamera.ShowPoint(Vector3.Lerp(player.transform.position, tree.FocusPoint + new Vector3(0f, 0.45f, 0f), 0.75f), false); }
            }
        }

        private void OnPanelClosed(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            openPanels.Remove(id);
            Log($"panel kapandı: {id}");
            if (treeFocus && id.StartsWith("treefall", StringComparison.OrdinalIgnoreCase)) { treeFocus = false; FollowPlayer(false); }
        }

        private void OnStep(string id)
        {
            if (state == FloodEveningState.Failed) return; // süre dolduktan sonra tamamlanan adım sonuçta "doğru karar" sayılmaz
            MarkStep(id);
        }

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
