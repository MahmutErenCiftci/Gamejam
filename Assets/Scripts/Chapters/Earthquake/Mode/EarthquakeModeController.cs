using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Rooms;
using AfetUyanisi.Chapters.TempUI;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// DEPREM MODU — 120 sn farkındalık simülasyonu, EarthquakeHome sahnesinin baştan sona akışı (EQ_SPEC A–E):
    ///  A. Siyahtan açılış, Cenk uyuyor → telefon titrer/çalar → kısa kararma, Cenk yatağın yanında uyanır → kamera telefona
    ///     kayar → telefon bildirimi [BAŞLA] → 120 sn sayaç.
    ///  B. Serbest keşif: E ile inceleme (kart açıkken sayaç durur), tehlike bölgeleri (ayna: uyarı + bir kerelik ceza),
    ///     annenin süreli soruları (Q1 anneye ilk E'de ya da sayaç 95'te, Q2 65'te, Q3 35'te; kart/geçiş açıksa hemen sonra),
    ///     hepsi soruldu + en az 4 inceleme → "[Space] Simülasyonu hızlandır" (kalan süre 5 sn).
    ///  C. Sayaç 0 → SARSINTI: kamera/eşya/toz/ışık, Çök–Kapan–Tutun (masaya yakınsa masa altında, değilse yerinde),
    ///     tehlike bölgesindeyse maruziyet.
    ///  D. Sonrası: ortam değişir, anne "İyi misin?" → Q4, cam kırıkları uyarısı (ceza yok, kayıt), çanta (şimdi alınır),
    ///     daire kapısı → koridor (asansör = yanlış karar kaydı, merdiven) → toplanma alanı.
    ///  E. AFET FARKINDALIK RAPORU → Tekrar Dene / Menüye Dön.
    /// Oda geçişleri: kapı Interactable'ı (EarthquakeDoor) → sayaç durur → kararma → spawn + kamera noktası → açılma.
    /// Görünümler yalnızca *ViewBase sözleşmeleriyle kullanılır; bağlı değilse Console'a yazılır ve akış kendiliğinden devam eder.
    /// debugAutoPlay: bütün akışı seçilen senaryoyla (debugScenario) kendisi oynar; her adım Console'a "[EQ] t=.." diye yazılır.
    /// </summary>
    public class EarthquakeModeController : MonoBehaviour
    {
        /// <summary>Otomatik test senaryoları.</summary>
        public enum DebugScenario { AllCorrect, AllWrong, Timeouts, MirrorPenalty, NoProtection }

        [Header("Veri")]
        [Tooltip("Assets/Data/Earthquake/earthquake_mode.json (boşsa gömülü varsayılan metinler)")]
        [SerializeField] private TextAsset modeJson;
        [Tooltip("DialogueRunnerAdapter (DialogueRunner storyJson = Assets/Data/Story/earthquake_home.json)")]
        [SerializeField] private DialogueRunnerBase dialogue;
        [Tooltip("İsteğe bağlı: diyalog oynatıcısı yokken balon satırları buradan Console'a yazılır (earthquake_home.json)")]
        [SerializeField] private TextAsset storyJson;

        [Header("Oyuncu")]
        [Tooltip("Cenk (IsoPlayerController + IsoCharacterAnimator). Boşsa sahnede bulunur.")]
        [SerializeField] private IsoPlayerController player;
        [Tooltip("İrkilme pozu (uyanınca ve sarsıntı başında, isteğe bağlı)")]
        [SerializeField] private Sprite startledPose;
        [SerializeField] private float startledSeconds = 0.5f;

        [Header("Yatak ve uyanış")]
        [Tooltip("Yatağın SpriteRenderer'ı (uyuyan Cenk'li ↔ boş)")]
        [SerializeField] private SpriteRenderer bedRenderer;
        [SerializeField] private Sprite bedSleeping;
        [SerializeField] private Sprite bedEmpty;
        [Tooltip("İsteğe bağlı: yatakta uyuyan Cenk ayrı bir obje ise (uyanınca gizlenir)")]
        [SerializeField] private GameObject sleepingKid;
        [Tooltip("Uyanınca Cenk'in belireceği nokta (boşsa yatak odasının 'WakeSpawn' çocuğu, o da yoksa spawn)")]
        [SerializeField] private Transform wakeSpawn;
        [SerializeField] private Vector2 wakeFacing = new Vector2(0f, -1f);

        [Header("Odalar")]
        [Tooltip("Boşsa sahnedeki bütün EarthquakeRoom'lar")]
        [SerializeField] private EarthquakeRoom[] rooms = Array.Empty<EarthquakeRoom>();

        [Header("Sistem bileşenleri (boşsa sahnede aranır, yoksa bu objeye eklenir)")]
        [SerializeField] private EmergencyCountdownManager countdown;
        [SerializeField] private TimePenaltySystem penalties;
        [SerializeField] private TimedDecisionController decisions;
        [SerializeField] private PhoneAlertController phone;
        [SerializeField] private EarthquakeEventController quakeEvents;
        [SerializeField] private ProtectionMinigameController protection;
        [SerializeField] private EarthquakeResultManager results;

        [Header("Görünümler (UI agent'ı; boşsa sahnede aranır, yoksa Console)")]
        [SerializeField] private CountdownViewBase countdownView;
        [SerializeField] private PhoneAlertViewBase phoneView;
        [SerializeField] private InfoCardViewBase cardView;
        [SerializeField] private TimedQuestionViewBase questionView;
        [SerializeField] private ProtectionViewBase protectionView;
        [SerializeField] private ResultReportViewBase reportView;

        [Header("Ekip parçaları")]
        [Tooltip("Boşsa TempInteractionHint oluşturulur")]
        [SerializeField] private InteractionHintBase hint;
        [Tooltip("Boşsa TempScreenFader oluşturulur")]
        [SerializeField] private ScreenFaderBase fader;
        [Tooltip("Boşsa bu objeye eklenir")]
        [SerializeField] private RoomInteractor interactor;

        [Header("Özel noktalar (boşsa adla bulunur: Mother, QuakeBag, Elevator, Phone)")]
        [SerializeField] private Interactable mother;
        [Tooltip("Toplanma alanındaki anne (boşsa salondaki anne, toplanma alanının 'MotherSpot' noktasına taşınır)")]
        [SerializeField] private GameObject assemblyMother;
        [SerializeField] private InspectableObject quakeBag;
        [Tooltip("Çanta alınınca gizlenecek görsel (boşsa çantanın SpriteRenderer'ları)")]
        [SerializeField] private GameObject quakeBagVisual;
        [SerializeField] private Interactable elevator;
        [SerializeField] private Transform phoneProp;

        [Header("Akış ayarları")]
        [SerializeField] private float fadeInSeconds = 1.5f;
        [Tooltip("Sakin oda süresi (telefon çalmadan önce)")]
        [SerializeField] private float calmSeconds = 1f;
        [Tooltip("Uyanmadan önce telefonun çalma süresi")]
        [SerializeField] private float ringSeconds = 2f;
        [SerializeField] private float wakeFadeSeconds = 0.25f;
        [SerializeField] private float transitionFadeSeconds = 0.3f;
        [Tooltip("Q1: sayaç bu değere inince (anneye daha önce gidilmediyse) anne seslenir")]
        [SerializeField] private float q1At = 95f;
        [SerializeField] private float q2At = 65f;
        [SerializeField] private float q3At = 35f;
        [Tooltip("İki soru arasında oyuncunun serbestçe dolaştığı en az süre (sayacın gerçekten aktığı sn). " +
                 "Ceza sayacı bir sonraki sorunun eşiğinin altına atlatsa bile sorular art arda açılmaz; " +
                 "sayaç bu arada biterse bekleyen hazırlık sorusu düşer (ceza ve puan kaybı yok).")]
        [SerializeField] private float minQuestionGap = 8f;
        [Tooltip("Hazırlık tamam ipucu için gereken en az inceleme")]
        [SerializeField] private int minInspectedForReady = 4;
        [Tooltip("Hızlandırınca kalan süre (sn)")]
        [SerializeField] private float accelerateTo = 5f;
        [Tooltip("Hazırlık tamam ipucunun tekrar gösterilme aralığı (sn)")]
        [SerializeField] private float readyHintRepeat = 15f;
        [Tooltip("Sarsıntının en kısa süresi (sn)")]
        [SerializeField] private float minShakeSeconds = 4f;
        [Tooltip("Sarsıntı bitince tozun çökme süresi (sn)")]
        [SerializeField] private float settleSeconds = 1.2f;
        [Tooltip("Görünüm yokken kartın ekranda kalma süresi (sn)")]
        [SerializeField] private float fallbackCardSeconds = 2f;
        [Tooltip("Toast süresi (sn)")]
        [SerializeField] private float toastSeconds = 3.5f;

        [Header("Balon replikleri (earthquake_home.json)")]
        [SerializeField] private string wakeLine = "EQH_WAKE";
        [SerializeField] private string phoneAfterLine = "EQH_PHONE_AFTER";
        [SerializeField] private string motherCallLine = "EQH_MOTHER_CALL";
        [SerializeField] private string motherGreetLine = "EQH_MOTHER_GREET";
        [SerializeField] private string readyLine = "EQH_READY";
        [SerializeField] private string quakeLine = "EQH_QUAKE";
        [SerializeField] private string afterMotherLine = "EQH_AFTER_MOTHER";
        [SerializeField] private string bagLine = "EQH_BAG";
        [SerializeField] private string exitLine = "EQH_EXIT";
        [SerializeField] private string assemblyLine = "EQH_ASSEMBLY";

        [Header("Hata ayıklama / otomatik test")]
        [Tooltip("İşaretliyse bütün akışı kendisi oynar (tuş gerekmez), adımlar Console'a yazılır")]
        [SerializeField] private bool debugAutoPlay;
        [SerializeField] private DebugScenario debugScenario = DebugScenario.AllCorrect;
        [Tooltip("Otomatik oyunda engelleyen görünümler (kart, telefon, rapor, diyalog) de açılsın (girdiyi test aracı verir)")]
        [SerializeField] private bool autoPlayUseViews;
        [Tooltip("Otomatik oyunda Time.timeScale (ör. 3 = üç kat hızlı)")]
        [SerializeField, Range(0.5f, 8f)] private float debugTimeScale = 1f;
        [Tooltip("Otomatik oyun bitince rapordaki seçimi uygulasın mı (Tekrar Dene sahneyi yeniden yükler)")]
        [SerializeField] private bool autoNavigateAtEnd;

        // --- Çalışma durumu ---
        private EarthquakeModeData data;
        private readonly EarthquakeStats stats = new EarthquakeStats();
        private EarthquakePhase phase = EarthquakePhase.Intro;
        private EarthquakeRoom currentRoom;
        private readonly List<InspectableObject> inspectables = new List<InspectableObject>();
        private readonly List<EarthquakeDoor> doors = new List<EarthquakeDoor>();
        private readonly List<HazardZone> zones = new List<HazardZone>();
        private readonly Dictionary<Interactable, Func<IEnumerator>> fallbackDoors = new Dictionary<Interactable, Func<IEnumerator>>();
        private readonly HashSet<string> askedQuestions = new HashSet<string>();
        private readonly HashSet<Interactable> specialPoints = new HashSet<Interactable>();
        private readonly object interactorToken = new object();
        private readonly object eventToken = new object();
        private readonly object transitionToken = new object();
        private bool interactorPaused;
        private bool freeRoam;
        private bool transitioning;
        private bool readyShown;
        private float lastReadyToast = -999f;
        private bool bagReminderShown;
        private bool reachedAssembly;
        private Interactable apartmentDoorPoint;
        private EarthquakeDoor apartmentDoor;
        private float previousTimeScale = 1f;
        private bool timeScaleChanged;
        private float freeSinceQuestion = 999f;
        private string pendingQuestionLogged;
        private EmergencyBagInteractable emergencyBag;

        // --- Testler için okunur durum ---
        public EarthquakePhase Phase => phase;
        public EarthquakeStats Stats => stats;
        public EarthquakeReport LastReport { get; private set; }
        public bool IsFinished { get; private set; }
        public EarthquakeModeData Data => data;
        public bool AutoPlay => debugAutoPlay;
        public DebugScenario Scenario => debugScenario;

        private bool Bypass => debugAutoPlay && !autoPlayUseViews;

        // =====================================================================
        // Kurulum
        // =====================================================================

        private void Awake()
        {
            if (!GameSession.CurrentChapter.HasValue || GameSession.CurrentChapter.Value != ChapterId.Earthquake)
                GameSession.StartChapter(ChapterId.Earthquake);

            data = EarthquakeModeData.Load(modeJson);

            if (player == null) player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (rooms == null || rooms.Length == 0)
                rooms = FindObjectsByType<EarthquakeRoom>(FindObjectsInactive.Include);

            ResolveComponents();
            ResolveViews();

            if (hint == null) hint = new GameObject("TempInteractionHint").AddComponent<TempInteractionHint>();
            if (fader == null) fader = new GameObject("TempScreenFader").AddComponent<TempScreenFader>();
            if (interactor == null) interactor = gameObject.AddComponent<RoomInteractor>();
            interactor.Configure(player, dialogue, hint, storyJson);
            interactor.AutoAdvance = Bypass;
            interactor.InteractionEnabled = false;
            interactor.Used += OnPointUsed;
            if (player != null) player.CanMove = false;

            ResolveSpecialPoints();
            SetupPoints();
            SetupZones();

            if (protection != null)
            {
                protection.Player = player;
                protection.Texts = data.protection;
            }
            if (phone != null)
            {
                if (phone.PhoneProp == null && phoneProp != null) phone.PhoneProp = phoneProp;
                phone.Hint = hint;
            }
            ApplyAutoPlaySettings();
        }

        private void Start()
        {
            Log($"deprem modu açıldı (veri {data.version}, {inspectables.Count} inceleme, {stats.hazardsTotal} tehlike, " +
                $"{doors.Count} kapı, {zones.Count} bölge, {rooms.Length} oda, otomatik: {(debugAutoPlay ? debugScenario.ToString() : "kapalı")})");
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (interactor != null) interactor.Used -= OnPointUsed;
            if (timeScaleChanged) Time.timeScale = previousTimeScale;
        }

        private void ResolveComponents()
        {
            countdown = Resolve(countdown);
            penalties = Resolve(penalties);
            decisions = Resolve(decisions);
            phone = Resolve(phone);
            quakeEvents = Resolve(quakeEvents);
            protection = Resolve(protection);
            results = Resolve(results);

            penalties.Countdown = penalties.Countdown != null ? penalties.Countdown : countdown;
            if (decisions.Countdown == null) decisions.Countdown = countdown;
            if (decisions.Penalties == null) decisions.Penalties = penalties;
        }

        private T Resolve<T>(T current) where T : Component
        {
            if (current != null) return current;
            var found = FindAnyObjectByType<T>(FindObjectsInactive.Include);
            return found != null ? found : gameObject.AddComponent<T>();
        }

        private void ResolveViews()
        {
            if (countdownView == null) countdownView = FindAnyObjectByType<CountdownViewBase>(FindObjectsInactive.Include);
            if (phoneView == null) phoneView = FindAnyObjectByType<PhoneAlertViewBase>(FindObjectsInactive.Include);
            if (cardView == null) cardView = FindAnyObjectByType<InfoCardViewBase>(FindObjectsInactive.Include);
            if (questionView == null) questionView = FindAnyObjectByType<TimedQuestionViewBase>(FindObjectsInactive.Include);
            if (protectionView == null) protectionView = FindAnyObjectByType<ProtectionViewBase>(FindObjectsInactive.Include);
            if (reportView == null) reportView = FindAnyObjectByType<ResultReportViewBase>(FindObjectsInactive.Include);

            if (countdown.View == null) countdown.View = countdownView;
            if (decisions.View == null) decisions.View = questionView;
            if (phone.View == null) phone.View = phoneView;
            if (protection.View == null) protection.View = protectionView;
            if (results.View == null) results.View = reportView;

            Log($"görünümler: sayaç {Has(countdown.View)}, telefon {Has(phone.View)}, kart {Has(cardView)}, soru {Has(decisions.View)}, " +
                $"korunma {Has(protection.View)}, rapor {Has(results.View)}");
        }

        private static string Has(UnityEngine.Object o) => o != null ? "var" : "YOK(Console)";

        private void ResolveSpecialPoints()
        {
            if (mother == null) mother = FindNamed<Interactable>("Mother");
            if (quakeBag == null) quakeBag = FindNamed<InspectableObject>("QuakeBag");
            if (elevator == null) elevator = FindNamed<Interactable>("Elevator");
            if (phoneProp == null)
            {
                var p = FindNamed<Transform>("Phone");
                if (p != null) phoneProp = p;
            }
            if (wakeSpawn == null)
            {
                var bed = RoomOf(EarthquakeRoom.RoomId.Bedroom);
                if (bed != null) wakeSpawn = bed.FindChild("WakeSpawn");
            }
        }

        /// <summary>Sahnede (kapalılar dahil) adı tam eşleşen ilk bileşen.</summary>
        private T FindNamed<T>(string objectName) where T : Component
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<T>(true))
                    if (c.gameObject.name == objectName) return c;
            return null;
        }

        private void SetupPoints()
        {
            var registered = new HashSet<Interactable>();

            // Kapılar
            doors.AddRange(FindObjectsByType<EarthquakeDoor>(FindObjectsInactive.Include));
            foreach (var door in doors)
            {
                var d = door;
                if (d.Point == null) continue;
                d.Point.Setup(d.Kind == EarthquakeDoor.DoorKind.ApartmentExit ? d.LockedLabel : d.Label, "", false, false);
                interactor.Register(d.Point, before: () => UseDoor(d));
                registered.Add(d.Point);
                if (d.Kind == EarthquakeDoor.DoorKind.ApartmentExit) { apartmentDoor = d; apartmentDoorPoint = d.Point; }
            }
            // Bileşensiz kapılar için ad yedeği
            RegisterFallbackDoor("BedroomDoor", EarthquakeDoor.DoorKind.Room, EarthquakeRoom.RoomId.Living, "Salona geç", registered);
            RegisterFallbackDoor("LivingDoor", EarthquakeDoor.DoorKind.Room, EarthquakeRoom.RoomId.Bedroom, "Odana dön", registered);
            RegisterFallbackDoor("ApartmentDoor", EarthquakeDoor.DoorKind.ApartmentExit, EarthquakeRoom.RoomId.Stairwell, "Kapıya bak", registered);
            RegisterFallbackDoor("Stairs", EarthquakeDoor.DoorKind.StairsDown, EarthquakeRoom.RoomId.Assembly, "Merdivenden in", registered);

            // Anne
            if (mother != null && !registered.Contains(mother))
            {
                mother.Setup("Anneyle konuş", "", false, false);
                interactor.Register(mother, before: TalkToMother);
                registered.Add(mother);
            }
            // Asansör
            if (elevator != null && !registered.Contains(elevator))
            {
                elevator.Setup("Asansöre bin", "", false, false);
                interactor.Register(elevator, before: UseElevator);
                registered.Add(elevator);
            }

            // Kapı/anne/asansör noktaları otomatik turda "inceleme" sayılmasın
            foreach (var p in registered) specialPoints.Add(p);

            // İncelenebilirler (çanta dahil; kapı/anne/asansör olanlar yalnızca veri alır)
            foreach (var insp in FindObjectsByType<InspectableObject>(FindObjectsInactive.Include))
            {
                insp.ApplyData(data.FindInspectable(insp.Id));
                if (insp.Point == null) continue;
                inspectables.Add(insp);
                if (registered.Contains(insp.Point)) continue;
                var i = insp;
                if (i == quakeBag)
                {
                    i.Point.Setup("Deprem çantasına bak", "", false, false);
                    interactor.Register(i.Point, before: UseBag);
                }
                else
                {
                    i.Point.Setup(i.ActionLabel, "", false, false);
                    interactor.Register(i.Point, before: () => Inspect(i));
                }
                registered.Add(i.Point);
                if (!i.HasData) Log($"uyarı: '{i.Id}' için JSON inspectables kaydı yok, yedek metin kullanılıyor");
            }
            // Afet çantası (mini oyun): hazırlıkta sayaç DURMADAN mini oyun açılır; sarsıntıdan sonra çanta alınır.
            foreach (var bag in FindObjectsByType<EmergencyBagInteractable>(FindObjectsInactive.Include))
            {
                if (bag.Point == null || registered.Contains(bag.Point)) continue;
                var b = bag;
                if (emergencyBag == null) emergencyBag = b;
                b.ApplyLabel(EarthquakePhase.Prep);
                interactor.Register(b.Point, before: () => UseEmergencyBag(b));
                registered.Add(b.Point);
                specialPoints.Add(b.Point);
                stats.emergencyBagMinigame = true;
            }
            stats.hazardsTotal = inspectables.FindAll(x => x.IsHazard).Count;
            stats.questionsTotal = data.questions != null ? data.questions.Length : 4;

            // Bağlanmamış (diyalogsuz) noktalar ölü ipucu göstermesin
            foreach (var p in FindObjectsByType<Interactable>(FindObjectsInactive.Include))
            {
                if (registered.Contains(p) || !string.IsNullOrEmpty(p.DialogueId)) continue;
                p.SetAvailable(false);
                Log($"'{p.name}' noktası deprem modunda kullanılmıyor (kapatıldı)");
            }
        }

        private void RegisterFallbackDoor(string objectName, EarthquakeDoor.DoorKind kind, EarthquakeRoom.RoomId target, string label,
            HashSet<Interactable> registered)
        {
            var point = FindNamed<Interactable>(objectName);
            if (point == null || registered.Contains(point)) return;
            var room = RoomOf(target);
            if (room == null) return;
            point.Setup(label, "", false, false);
            Func<IEnumerator> use = () => UseDoorKind(kind, room, room.DefaultSpawn, room.DefaultFacing, point);
            fallbackDoors[point] = use;
            interactor.Register(point, before: use);
            registered.Add(point);
            if (kind == EarthquakeDoor.DoorKind.ApartmentExit) apartmentDoorPoint = point;
            Log($"kapı yedeği: '{objectName}' → {target} (EarthquakeDoor bileşeni yok)");
        }

        private void SetupZones()
        {
            zones.AddRange(FindObjectsByType<HazardZone>(FindObjectsInactive.Include));
            foreach (var z in zones)
            {
                z.Bind(player, penalties, countdown, cardView, () => phase, IsBlocked, data.FindWarning(z.WarningId));
                z.Penalized += OnZonePenalized;
                z.Entered += OnZoneEntered;
            }
        }

        private void ApplyAutoPlaySettings()
        {
            decisions.BypassBlockingViews = Bypass;
            phone.BypassBlockingViews = Bypass;
            results.BypassBlockingViews = Bypass;
            if (!debugAutoPlay) return;

            decisions.AutoChooser = q =>
            {
                switch (debugScenario)
                {
                    case DebugScenario.AllWrong: return q.FirstWrongIndex;
                    case DebugScenario.Timeouts: return -1;
                    default: return q.CorrectIndex;
                }
            };
            decisions.AutoAnswerDelay = 1f;
            switch (debugScenario)
            {
                case DebugScenario.AllWrong: protection.Auto = ProtectionMinigameController.AutoInput.RespondWithRelease; break;
                case DebugScenario.NoProtection: protection.Auto = ProtectionMinigameController.AutoInput.NoInput; break;
                default: protection.Auto = ProtectionMinigameController.AutoInput.Respond; break;
            }
            if (!Mathf.Approximately(debugTimeScale, 1f))
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = debugTimeScale;
                timeScaleChanged = true;
            }
        }

        // =====================================================================
        // Akış
        // =====================================================================

        private IEnumerator Run()
        {
            yield return IntroPhase();
            yield return PrepPhase();
            yield return QuakePhase();
            // Kitaplık üstüne devrildiyse sarsıntı sonrası adımlar atlanır: bölüm "YARALANDIN" raporuyla biter.
            if (stats.hitByBookshelf)
            {
                countdown.SetStatusText(null);
                countdown.SetVisible(false);
            }
            else yield return AfterPhase();
            yield return ReportPhase();
        }

        // ---------------- A. Uyanış + telefon ----------------
        private IEnumerator IntroPhase()
        {
            phase = EarthquakePhase.Intro;
            Log("A: siyah ekran, Cenk uyuyor (07:00)");
            fader.SetOpaque(true);
            SetFreeRoam(false);
            SetPlayerVisible(false);
            SetBedSleeping(true);
            countdown.SetVisible(false);
            var bedroom = RoomOf(EarthquakeRoom.RoomId.Bedroom) ?? (rooms.Length > 0 ? rooms[0] : null);
            EnterRoom(bedroom);

            yield return WaitForSteadyFrames();
            yield return fader.FadeIn(fadeInSeconds);
            yield return new WaitForSeconds(calmSeconds);

            Log("A: telefon çalıyor");
            phone.StartRinging();
            yield return new WaitForSeconds(ringSeconds);

            Log("A: kısa kararma → Cenk uyanır");
            yield return fader.FadeOut(wakeFadeSeconds);
            SetBedSleeping(false);
            Transform spawn = wakeSpawn != null ? wakeSpawn : (bedroom != null ? bedroom.DefaultSpawn : null);
            PlacePlayer(spawn, wakeFacing);
            yield return fader.FadeIn(wakeFadeSeconds);
            yield return Startled();
            yield return Say(wakeLine);

            Log("A: kamera telefona kayar, bildirim");
            yield return phone.FocusCamera(bedroom != null ? bedroom.CameraAnchor.position : Vector3.zero);
            yield return phone.ShowAlert(data.phone);
            phone.StopRinging();
            if (bedroom != null)
            {
                yield return EarthquakeCameraUtil.MoveTo(bedroom.CameraAnchor.position, false, 0.4f);
                // Yakın kamera (GameCameraSettings): serbest dolaşımda oyuncuyu oda içinde yumuşak takip et.
                if (bedroom.Bounds == null && player != null && player.gameObject.activeInHierarchy)
                    EarthquakeCameraUtil.FollowRoom(player.transform, bedroom.transform, bedroom.CameraAnchor.position, false);
            }
            yield return Say(phoneAfterLine);
        }

        // ---------------- B. 120 sn hazırlık ----------------
        private IEnumerator PrepPhase()
        {
            phase = EarthquakePhase.Prep;
            countdown.StartTimer(data.countdownSeconds);
            Log($"B: {data.countdownSeconds:0} sn sayaç başladı, serbest keşif");
            SetFreeRoam(true);
            if (debugAutoPlay) StartCoroutine(AutoPilotPrep());

            while (!countdown.HasExpired)
            {
                if (CanStartEvent())
                {
                    string due = NextDueQuestion();
                    if (due != null)
                    {
                        yield return ScheduledQuestion(due);
                        continue;
                    }
                    if (readyShown && !stats.acceleratedReady && GameInput.AcceleratePressed) Accelerate();
                    if (IsReady())
                    {
                        if (!readyShown)
                        {
                            yield return ShowReady();
                            continue;
                        }
                        RepeatReadyToast();
                    }
                }
                yield return null;
            }

            // Sarsıntı: açık mini oyunlar (afet çantası...) anında kesilir; açık bir etkileşim varsa bitmesini bekle.
            Mode.EarthquakeMinigameHub.RequestInterruptAll();
            SetFreeRoam(false);
            DropUnaskedQuestions();
            while (interactor.IsBusy || decisions.IsAsking || transitioning) yield return null;
            for (float t = 0f; Mode.EarthquakeMinigameHub.AnyOpen && t < 2f; t += Time.unscaledDeltaTime) yield return null;
        }

        private string NextDueQuestion()
        {
            float r = countdown.GetRemainingTime();
            string due = null;
            if (!askedQuestions.Contains("Q1") && r <= q1At) due = "Q1";
            else if (askedQuestions.Contains("Q1") && !askedQuestions.Contains("Q2") && r <= q2At) due = "Q2";
            else if (askedQuestions.Contains("Q2") && !askedQuestions.Contains("Q3") && r <= q3At) due = "Q3";
            if (due == null) return null;
            // Art arda soru yok: önceki sorudan sonra oyuncu en az minQuestionGap sn serbest dolaşmış olmalı
            // (ör. Q2'deki -30 ceza sayacı 65'ten 35'e atlatınca Q3 hemen açılmaz).
            if (freeSinceQuestion < minQuestionGap)
            {
                if (pendingQuestionLogged != due)
                {
                    pendingQuestionLogged = due;
                    Log($"B: {due} sırada — önceki sorudan sonra {minQuestionGap:0} sn serbest dolaşma bekleniyor");
                }
                return null;
            }
            return due;
        }

        /// <summary>Sayaç bittiğinde hâlâ sorulmamış hazırlık soruları düşer (ceza yok, puanlamada sayılmaz).</summary>
        private void DropUnaskedQuestions()
        {
            foreach (var id in new[] { "Q1", "Q2", "Q3" })
            {
                if (askedQuestions.Contains(id) || data.FindQuestion(id) == null) continue;
                askedQuestions.Add(id);
                stats.droppedQuestions.Add(id);
                Log($"B: sayaç bitti, {id} sorulamadı → düşürüldü (ceza yok)");
            }
        }

        private IEnumerator ScheduledQuestion(string id)
        {
            SetFreeRoam(false);
            countdown.PushPause(eventToken);
            if (id == "Q1")
            {
                Log("B: sayaç 95 → anne seslenir (Q1)");
                yield return Say(motherCallLine);
            }
            FacePlayerToMother();
            yield return AskQuestion(id);
            countdown.PopPause(eventToken);
            if (phase == EarthquakePhase.Prep && !countdown.HasExpired) SetFreeRoam(true);
        }

        /// <summary>Q1–Q3 soruldu, en az minInspectedForReady inceleme yapıldı ve henüz hızlandırılmadı.</summary>
        private bool IsReady() =>
            askedQuestions.Contains("Q1") && askedQuestions.Contains("Q2") && askedQuestions.Contains("Q3")
            && stats.InspectedCount >= minInspectedForReady && !stats.acceleratedReady
            && countdown.GetRemainingTime() > accelerateTo + 0.5f; // son saniyelerde hızlandırma ipucu anlamsız

        private IEnumerator ShowReady()
        {
            readyShown = true;
            Log($"B: hazırlık tamam ({stats.InspectedCount} inceleme) → [Space] hızlandır ipucu");
            SetFreeRoam(false);
            countdown.PushPause(eventToken);
            yield return Say(readyLine);
            countdown.PopPause(eventToken);
            RepeatReadyToast();
            if (!countdown.HasExpired) SetFreeRoam(true);
        }

        private void RepeatReadyToast()
        {
            if (Time.time - lastReadyToast < readyHintRepeat) return;
            lastReadyToast = Time.time;
            Toast("HAZIRLIK TAMAM", data.after.readyHint, Mathf.Max(toastSeconds, 5f));
        }

        private void Accelerate()
        {
            stats.remainingAtAccelerate = countdown.GetRemainingTime();
            if (countdown.Accelerate(accelerateTo))
            {
                stats.acceleratedReady = true;
                Log($"B: simülasyon hızlandırıldı ({stats.remainingAtAccelerate:0.0} sn kalmıştı)");
            }
        }

        // ---------------- C. Sarsıntı + korunma ----------------
        private IEnumerator QuakePhase()
        {
            phase = EarthquakePhase.Quake;
            stats.remainingAtQuake = countdown.GetRemainingTime();
            countdown.SetStatusText("SARSINTI!");
            Log("C: SARSINTI başlıyor");

            Vector3 pos = player != null ? player.transform.position : Vector3.zero;
            var exposedZones = zones.FindAll(z => z.IsActiveIn(EarthquakePhase.Quake) && z.Contains(pos));
            foreach (var z in exposedZones) Log($"C: oyuncu tehlike bölgesinde: {z.Id} ({z.ExposureKind})");

            quakeEvents.BeginShake();
            float shakeStart = Time.time;
            yield return Startled();
            // Balon engellemesin ve tuş istemesin: kendi kendine kapanır, E yalnızca korunmaya gider.
            if (HasStory(quakeLine)) StartCoroutine(SayTimed(quakeLine, 1.8f));

            ProtectionMinigameController.Result result = null;
            bool protectionDone = false;
            var protectionRoutine = StartCoroutine(protection.Run(pos, r => { result = r; protectionDone = true; }));
            // Sabitlenmemiş kitaplık oyuncunun üstüne devrilirse korunma anında kesilir.
            while (!protectionDone && !stats.hitByBookshelf) yield return null;
            if (stats.hitByBookshelf)
            {
                if (!protectionDone && protectionRoutine != null) StopCoroutine(protectionRoutine);
                if (protection.View != null) protection.View.Hide();
                yield return InjuredByBookshelf(shakeStart);
                yield break;
            }
            while (Time.time - shakeStart < minShakeSeconds && !stats.hitByBookshelf) yield return null;
            if (stats.hitByBookshelf) { yield return InjuredByBookshelf(shakeStart); yield break; }
            quakeEvents.EndShake();
            countdown.SetStatusText("SARSINTI BİTTİ");
            yield return new WaitForSeconds(settleSeconds);

            var outcome = result != null ? result.outcome : ProtectionOutcome.None;
            stats.protectionUnderTable = result != null && result.table != null && outcome == ProtectionOutcome.TableDropCoverHold;
            stats.protectionHoldReleased = result != null && result.releasedDuringHold;
            stats.protectionIncomplete = result != null && outcome != ProtectionOutcome.None && !result.holdCompleted;

            var feedback = new List<string>();
            switch (outcome)
            {
                case ProtectionOutcome.TableDropCoverHold: feedback.Add(data.protection.tableSuccess); break;
                case ProtectionOutcome.InPlaceDropCover: feedback.Add(data.protection.inPlaceSuccess); break;
                default: feedback.Add(data.protection.none); break;
            }
            if (stats.protectionIncomplete) feedback.Add(data.protection.releaseWarning);
            foreach (var z in exposedZones)
            {
                string text = data.protection.ExposedText(z.ExposureKind);
                if (stats.AddExposure("quake:" + EarthquakeModeData.NormalizeId(z.ExposureKind), text) && !string.IsNullOrEmpty(text))
                    feedback.Add(text);
            }
            if (stats.bookshelfFell)
                feedback.Add("Sabitlenmemiş kitaplık devrildi! Yanında olsaydın üstüne düşebilirdi.");
            if (outcome == ProtectionOutcome.InPlaceDropCover && exposedZones.Count > 0) outcome = ProtectionOutcome.Exposed;
            stats.protection = outcome;
            Log($"C: korunma = {outcome} ({EarthquakeResultManager.ProtectionText(outcome)}), maruziyet {exposedZones.Count}");

            quakeEvents.ApplyAftermath();
            protection.RestoreAfter();
            bool good = outcome == ProtectionOutcome.TableDropCoverHold || outcome == ProtectionOutcome.InPlaceDropCover;
            yield return ShowCard("SARSINTI BİTTİ", string.Join("\n", feedback), good ? HazardClass.Protective : HazardClass.Hazard);
        }

        /// <summary>
        /// Kitaplık oyuncunun üstüne devrildi: sarsıntı kısa sürede biter, korunma "yapılmadı" sayılır ve
        /// "YARALANDIN" kartı gösterilir (çocuk dostu: kan/ağır yaralanma yok, annesi yanına gelir).
        /// </summary>
        private IEnumerator InjuredByBookshelf(float shakeStart)
        {
            SetFreeRoam(false);
            Log("C: kitaplık oyuncunun üstüne devrildi → YARALANDIN (bölüm başarısız)");
            float until = Mathf.Min(minShakeSeconds, (Time.time - shakeStart) + 1.2f);
            while (Time.time - shakeStart < until) yield return null;
            quakeEvents.EndShake();
            countdown.SetStatusText("SARSINTI BİTTİ");
            yield return new WaitForSeconds(settleSeconds);

            stats.protection = ProtectionOutcome.None;
            stats.AddExposure("quake:bookshelf", "Devrilen kitaplığın yanındaydın.");
            quakeEvents.ApplyAftermath();
            protection.RestoreAfter();
            yield return ShowCard("KİTAPLIK ÜSTÜNE DEVRİLDİ!",
                "Sabitlenmemiş kitaplık sarsıntıda devrildi ve üstüne düştü. Yaralandın, annen hemen yanına koştu.\n" +
                "Depremden ÖNCE uzun mobilyaları duvara sabitle; sarsıntıda onlardan uzak dur, sağlam masanın altına gir.",
                HazardClass.Hazard);
        }

        // ---------------- D. Sarsıntı sonrası ----------------
        private IEnumerator AfterPhase()
        {
            phase = EarthquakePhase.After;
            countdown.SetStatusText(null);
            countdown.SetVisible(false);
            Log("D: sarsıntı sonrası — anne: İyi misin?");

            FacePlayerToMother();
            yield return Say(afterMotherLine);
            yield return AskQuestion("Q4");
            Toast("ÖNCE TEHLİKELERİ KONTROL ET", data.after.checkHazards, toastSeconds + 1f);

            if (quakeBag != null && quakeBag.Point != null) quakeBag.Point.Setup("Deprem çantasını al", "", false, false);
            if (emergencyBag != null) emergencyBag.ApplyLabel(EarthquakePhase.After);
            if (apartmentDoorPoint != null)
            {
                string exitLabel = apartmentDoor != null ? apartmentDoor.Label : null;
                if (string.IsNullOrEmpty(exitLabel) || exitLabel == "Geç") exitLabel = "Dışarı çık";
                apartmentDoorPoint.Setup(exitLabel, "", false, false);
            }

            SetFreeRoam(true);
            if (debugAutoPlay) StartCoroutine(AutoPilotAfter());

            while (!reachedAssembly) yield return null;
            while (interactor.IsBusy || transitioning) yield return null;

            SetFreeRoam(false);
            Log("D: toplanma alanı");
            Toast("TOPLANMA ALANI", data.after.assembly, toastSeconds + 1f);
            yield return Say(assemblyLine);
        }

        // ---------------- E. Rapor ----------------
        private IEnumerator ReportPhase()
        {
            phase = EarthquakePhase.Report;
            stats.penaltySeconds = penalties.TotalSeconds();
            if (!stats.bagTaken)
                stats.AddMistake(emergencyBag != null && !stats.emergencyBagPrepared ? "Afet çantasını depremden önce hazırlamadın." : "Deprem çantasını almadın.");
            var report = results.Build(stats, data);
            LastReport = report;
            Log($"E: rapor (puan {report.score}, {report.label})");
            yield return results.Show(report, debugAutoPlay);
            IsFinished = true;
            Log($"E: bitti, seçim: {(string.IsNullOrEmpty(report.choice) ? "(yok)" : report.choice)}");

            if (debugAutoPlay && !autoNavigateAtEnd) yield break;
            EarthquakeResultManager.ApplyChoice(report.choice);
        }

        // =====================================================================
        // Nokta kullanımları
        // =====================================================================

        private IEnumerator Inspect(InspectableObject insp)
        {
            bool first = insp.MarkInspected();
            // "Bulunan tehlikeler" yalnızca sarsıntıdan ÖNCE fark edilen tehlikeleri sayar.
            bool preQuake = phase == EarthquakePhase.Intro || phase == EarthquakePhase.Prep;
            stats.AddInspected(insp.Id, insp.IsHazard && preQuake);
            Log($"inceleme: {insp.Id} ({insp.Kind}){(first ? "" : " tekrar")} — toplam {stats.InspectedCount}, tehlike {stats.HazardsFound}/{stats.hazardsTotal}");
            yield return ShowCard(insp.Title, insp.TextFor(phase), insp.Kind);
        }

        private IEnumerator TalkToMother()
        {
            FacePlayerToMother();
            if (phase == EarthquakePhase.Prep && !askedQuestions.Contains("Q1"))
            {
                Log("B: anneye ilk kez gidildi → Q1");
                yield return Say(motherGreetLine);
                yield return AskQuestion("Q1");
                yield break;
            }
            if (phase == EarthquakePhase.After)
            {
                if (!askedQuestions.Contains("Q4")) { yield return AskQuestion("Q4"); yield break; }
                yield return ShowCard("ANNE", data.after.checkHazards, HazardClass.Safety);
                yield break;
            }
            var info = data.FindInspectable("mother");
            stats.AddInspected("mother", false);
            yield return ShowCard(info != null ? info.title : "Anne", info != null ? info.text : "Deprem planınızı birlikte konuşun.", HazardClass.Info);
        }

        private IEnumerator AskQuestion(string id)
        {
            if (askedQuestions.Contains(id)) yield break;
            askedQuestions.Add(id);
            var q = data.FindQuestion(id);
            if (q == null)
            {
                Log($"soru {id} veride yok, atlandı");
                yield break;
            }
            TimedDecisionController.Result result = null;
            yield return decisions.Ask(q, r => result = r);
            freeSinceQuestion = 0f;
            pendingQuestionLogged = null;
            if (result == null) yield break;
            int ci = q.CorrectIndex;
            stats.answers.Add(new EarthquakeStats.AnswerRecord
            {
                questionId = q.id,
                question = q.question,
                chosenIndex = result.chosenIndex,
                correct = result.correct,
                timedOut = result.timedOut,
                correctText = ci >= 0 ? q.options[ci].text : "",
                explanation = ci >= 0 ? q.options[ci].explanation : result.feedback
            });
        }

        private IEnumerator UseBag()
        {
            if (phase != EarthquakePhase.After)
            {
                yield return Inspect(quakeBag);
                yield break;
            }
            if (stats.bagTaken) yield break;
            stats.bagTaken = true;
            stats.AddInspected(quakeBag.Id, quakeBag.IsHazard);
            Log("D: deprem çantası alındı");
            quakeBag.Point.SetAvailable(false);
            HideBagVisual();
            yield return Say(bagLine);
            Toast("DEPREM ÇANTASI", data.after.bagTaken, toastSeconds);
        }

        private void HideBagVisual()
        {
            if (quakeBagVisual != null)
            {
                quakeBagVisual.SetActive(false);
                return;
            }
            foreach (var r in quakeBag.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
        }

        /// <summary>
        /// Afet çantası (EmergencyBagInteractable). Hazırlıkta mini oyunu açar ve hemen döner: sayaç DURMAZ, serbest dolaşma
        /// mini oyun kapanana kadar kapalıdır (sorular/olaylar araya girmez). Sarsıntıdan sonra: hazırsa çanta alınır
        /// (bagTaken), değilse "çanta boş" ders kartı. Sarsıntı sırasında kullanılamaz (öncelik Çök–Kapan–Tutun).
        /// </summary>
        private IEnumerator UseEmergencyBag(EmergencyBagInteractable bag)
        {
            if (phase == EarthquakePhase.Prep && !countdown.HasExpired && !bag.IsPrepared && bag.Minigame != null)
            {
                SetFreeRoam(false);
                bool opened = bag.OpenMinigame(completedBag =>
                {
                    if (completedBag)
                    {
                        stats.emergencyBagPrepared = true;
                        stats.AddInspected("emergency_bag", false);
                        Log("B: afet çantası hazırlandı (mini oyun tamamlandı)");
                    }
                    bag.ApplyLabel(phase);
                    if (phase == EarthquakePhase.Prep && !countdown.HasExpired) SetFreeRoam(true);
                });
                Log(opened ? "B: afet çantası mini oyunu açıldı (sayaç akmaya devam ediyor)" : "B: afet çantası mini oyunu açılamadı");
                if (!opened && phase == EarthquakePhase.Prep && !countdown.HasExpired) SetFreeRoam(true);
                yield break;
            }
            if (phase == EarthquakePhase.After)
            {
                if (bag.IsPrepared)
                {
                    if (stats.bagTaken) yield break;
                    stats.bagTaken = true;
                    stats.AddInspected("emergency_bag", false);
                    Log("D: hazırlanan afet çantası alındı");
                    bag.MarkTaken();
                    yield return Say(bagLine);
                    Toast("AFET ÇANTASI", bag.TakenToast, toastSeconds);
                    yield break;
                }
                bag.MarkEmptyChecked();
                Log("D: afet çantası hazırlanmamıştı → ders kartı");
                yield return ShowCard("AFET ÇANTASI HAZIR DEĞİL", bag.EmptyAfterText, HazardClass.Hazard);
                yield break;
            }
            yield return ShowCard(bag.Title, bag.IsPrepared ? bag.ReadyText : "Afet çantası boş. Sarsıntıdan önce hazırlamalısın.", HazardClass.Safety);
        }

        private IEnumerator UseElevator()
        {
            if (phase != EarthquakePhase.After)
            {
                var info = data.FindInspectable("elevator");
                yield return ShowCard(info != null ? info.title : "Asansör", info != null ? info.text : data.after.elevator, HazardClass.Exit);
                yield break;
            }
            if (!stats.elevatorTried)
            {
                stats.elevatorTried = true;
                stats.AddMistake(data.after.elevator);
                Log("D: asansör denendi → yanlış karar kaydı (ilerletmez)");
            }
            yield return ShowCard("ASANSÖR KULLANILMAZ!", data.after.elevator, HazardClass.Hazard);
        }

        private IEnumerator UseDoor(EarthquakeDoor door) =>
            UseDoorKind(door.Kind, door.TargetRoom, door.TargetSpawn, door.ArriveFacing, door.Point);

        private IEnumerator UseDoorKind(EarthquakeDoor.DoorKind kind, EarthquakeRoom room, Transform spawn, Vector2 facing, Interactable point)
        {
            switch (kind)
            {
                case EarthquakeDoor.DoorKind.ApartmentExit:
                    if (phase != EarthquakePhase.After)
                    {
                        var info = data.FindInspectable("apartment_door");
                        stats.AddInspected("apartment_door", false);
                        Log("B: daire kapısı kilitli — sarsıntıda dışarı koşulmaz");
                        yield return ShowCard(info != null ? info.title : "Daire Kapısı", data.after.doorLockedBefore, HazardClass.Exit);
                        yield break;
                    }
                    if (!stats.bagTaken && !bagReminderShown)
                    {
                        bagReminderShown = true;
                        Log("D: çanta alınmadan çıkış denendi → hatırlatma");
                        yield return ShowCard(emergencyBag != null ? "AFET ÇANTASI" : "DEPREM ÇANTASI",
                            emergencyBag != null ? emergencyBag.ExitReminder : data.after.bagReminder, HazardClass.Safety);
                        yield break;
                    }
                    Log("D: daire kapısı → koridor");
                    yield return Transition(room, spawn, facing);
                    if (mother != null && assemblyMother != null) mother.gameObject.SetActive(false);
                    break;

                case EarthquakeDoor.DoorKind.StairsDown:
                    if (phase != EarthquakePhase.After)
                    {
                        var info = data.FindInspectable("stairs");
                        yield return ShowCard(info != null ? info.title : "Merdiven", info != null ? info.text : data.after.stairs, HazardClass.Safety);
                        yield break;
                    }
                    stats.usedStairs = true;
                    stats.AddInspected("stairs", false);
                    Log("D: merdivenden iniliyor → toplanma alanı");
                    Toast("MERDİVEN", data.after.stairs, toastSeconds);
                    yield return Say(exitLine);
                    yield return Transition(room, spawn, facing, PrepareAssembly);
                    reachedAssembly = true;
                    break;

                default:
                    Log($"kapı → {(room != null ? room.Id.ToString() : "?")}");
                    yield return Transition(room, spawn, facing);
                    break;
            }
        }

        private void PrepareAssembly()
        {
            if (assemblyMother != null)
            {
                assemblyMother.SetActive(true);
                return;
            }
            var assembly = RoomOf(EarthquakeRoom.RoomId.Assembly);
            var spot = assembly != null ? assembly.FindChild("MotherSpot") : null;
            if (mother != null && spot != null)
            {
                mother.gameObject.SetActive(true);
                mother.transform.position = spot.position;
                mother.SetAvailable(false);
            }
        }

        // =====================================================================
        // Oda geçişi, kamera, oyuncu
        // =====================================================================

        private IEnumerator Transition(EarthquakeRoom room, Transform spawn, Vector2 facing, Action beforeFadeIn = null)
        {
            if (room == null)
            {
                Log("geçiş: hedef oda yok, atlandı");
                yield break;
            }
            transitioning = true;
            countdown.PushPause(transitionToken);
            yield return fader.FadeOut(transitionFadeSeconds);
            PlacePlayer(spawn != null ? spawn : room.DefaultSpawn, facing.sqrMagnitude > 0.0001f ? facing : room.DefaultFacing);
            EnterRoom(room);
            beforeFadeIn?.Invoke();
            yield return null;
            yield return fader.FadeIn(transitionFadeSeconds);
            countdown.PopPause(transitionToken);
            transitioning = false;
            Log($"oda: {room.Id}");
        }

        private void EnterRoom(EarthquakeRoom room)
        {
            if (room == null) return;
            currentRoom = room;
            if (room.Bounds != null && player != null) EarthquakeCameraUtil.Follow(player.transform, room.Bounds);
            else if (player != null && player.gameObject.activeInHierarchy)
                EarthquakeCameraUtil.FollowRoom(player.transform, room.transform, room.CameraAnchor.position, true);
            else EarthquakeCameraUtil.CutTo(room.CameraAnchor.position);
        }

        private EarthquakeRoom RoomOf(EarthquakeRoom.RoomId id)
        {
            if (rooms == null) return null;
            foreach (var r in rooms)
                if (r != null && r.Id == id) return r;
            return null;
        }

        private void PlacePlayer(Transform spawn, Vector2 facing)
        {
            if (player == null) return;
            player.gameObject.SetActive(true);
            player.PlaceAt(spawn != null ? spawn.position : player.transform.position, facing);
        }

        private void SetPlayerVisible(bool visible)
        {
            if (player != null) player.gameObject.SetActive(visible);
        }

        private void SetFreeRoam(bool on)
        {
            freeRoam = on;
            interactor.InteractionEnabled = on;
            if (player != null) player.CanMove = on;
        }

        private void SetBedSleeping(bool sleeping)
        {
            if (bedRenderer != null)
            {
                var sprite = sleeping ? bedSleeping : bedEmpty;
                if (sprite != null) bedRenderer.sprite = sprite;
            }
            if (sleepingKid != null) sleepingKid.SetActive(sleeping);
        }

        private IEnumerator Startled()
        {
            if (startledPose == null || player == null) yield break;
            var anim = player.GetComponent<IsoCharacterAnimator>();
            if (anim == null) yield break;
            anim.ShowPose(startledPose);
            yield return new WaitForSeconds(startledSeconds);
            anim.ClearPose();
        }

        private void FacePlayerToMother()
        {
            if (player == null || mother == null || !mother.gameObject.activeInHierarchy) return;
            Vector2 toMother = mother.transform.position - player.transform.position;
            if (toMother.sqrMagnitude > 25f) return; // başka odadaysa dönme
            var anim = player.GetComponent<IsoCharacterAnimator>();
            if (anim != null) anim.Face(toMother);
            var motherAnim = mother.GetComponent<IsoCharacterAnimator>();
            if (motherAnim != null) motherAnim.Face(-toMother);
        }

        private static IEnumerator WaitForSteadyFrames()
        {
            int steady = 0;
            for (int i = 0; i < 30 && steady < 2; i++)
            {
                yield return null;
                steady = Time.unscaledDeltaTime < 0.1f ? steady + 1 : 0;
            }
        }

        // =====================================================================
        // Olaylar, yardımcılar
        // =====================================================================

        private void OnPointUsed(Interactable point)
        {
            if (interactorPaused) return;
            countdown.PushPause(interactorToken);
            interactorPaused = true;
        }

        private void Update()
        {
            // Mini oyunlar (kitaplık, afet çantası) sayacı DURDURMAZ: açık bir mini oyun varsa etkileşim duraklatması kalkar.
            if (interactorPaused && (!interactor.IsBusy || Mode.EarthquakeMinigameHub.AnyOpen))
            {
                countdown.PopPause(interactorToken);
                interactorPaused = false;
            }
            // Soru arası serbest dolaşma süresi: yalnızca sayaç gerçekten akarken (kart/soru/geçiş/balon sırasında değil).
            if (phase == EarthquakePhase.Prep && freeRoam && countdown.IsRunning && !countdown.IsPaused)
                freeSinceQuestion += Time.deltaTime;
        }

        private void OnZonePenalized(HazardZone zone)
        {
            if (!stats.mirrorPenaltyZones.Contains(zone.Id)) stats.mirrorPenaltyZones.Add(zone.Id);
            stats.AddMistake($"{zone.WarningTitle} {zone.WarningText}");
            Log($"B: tehlike bölgesi cezası ({zone.Id})");
        }

        private void OnZoneEntered(HazardZone zone, EarthquakePhase zonePhase)
        {
            if (zonePhase != EarthquakePhase.After) return;
            string text = !string.IsNullOrEmpty(zone.WarningText) ? zone.WarningText : data.after.glassWarning;
            if (stats.AddExposure("after:" + zone.Id, text)) Log($"D: cam kırıkları üzerinden geçildi ({zone.Id}) — uyarı, ceza yok, kayıt");
        }

        private bool IsBlocked() => !freeRoam || transitioning || interactor.IsBusy || decisions.IsAsking;

        private bool CanStartEvent() => freeRoam && !transitioning && !interactor.IsBusy && !decisions.IsAsking;

        private IEnumerator ShowCard(string title, string body, HazardClass kind)
        {
            Log($"KART [{kind}] {title}: {body}");
            if (cardView != null && !Bypass)
            {
                yield return cardView.ShowCard(title, body, kind);
                yield break;
            }
            float wait = Bypass ? 0.4f : fallbackCardSeconds;
            yield return null;
            for (float t = 0f; t < wait; t += Time.deltaTime)
            {
                if (!Bypass && t > 0.2f && GameInput.ContinuePressed) break;
                yield return null;
            }
        }

        private void Toast(string title, string body, float seconds)
        {
            if (string.IsNullOrEmpty(body)) return;
            Log($"TOAST {title}: {body}");
            if (cardView != null) cardView.ShowToast(title, body, seconds);
        }

        private IEnumerator Say(string id)
        {
            if (string.IsNullOrEmpty(id)) yield break;
            if (!HasStory(id))
            {
                Log($"(balon yok) {id}");
                yield break;
            }
            Log($"balon {id}");
            yield return interactor.Say(id);
        }

        /// <summary>
        /// Balonu girdi beklemeden gösterir ve süre dolunca kapatır (sarsıntı anı: E'yi diyalog değil korunma almalı).
        /// DialogueRunner meşgulse ya da otomatik oyunda normal Say'e düşer.
        /// </summary>
        private IEnumerator SayTimed(string id, float seconds)
        {
            var adapter = dialogue as DialogueRunnerAdapter;
            var runner = adapter != null ? adapter.Runner : null;
            var scene = runner != null && runner.Data != null ? runner.Data.FindScene(id) : null;
            var bubble = runner != null ? runner.GetComponentInChildren<AfetUyanisi.Dialogue.SpeechBubble>(true) : null;
            if (Bypass || scene == null || scene.lines == null || bubble == null || runner.IsBusy)
            {
                yield return Say(id);
                yield break;
            }
            Log($"balon {id} (kendiliğinden kapanır)");
            foreach (var line in scene.lines)
            {
                var speaker = AfetUyanisi.Dialogue.DialogueSpeaker.Find(line.speaker);
                if (speaker == null) continue;
                bubble.Show(speaker, line.text);
                bubble.Text.text = line.text;
                yield return new WaitForSeconds(seconds);
                if (runner.IsBusy) yield break; // araya gerçek bir diyalog girdiyse balon artık onun
                bubble.Hide();
            }
        }

        private bool HasStory(string id)
        {
            var adapter = dialogue as DialogueRunnerAdapter;
            if (adapter != null && adapter.Runner != null)
            {
                var d = adapter.Runner.Data;
                return d != null && d.scenes != null && Array.Exists(d.scenes, s => s.id == id);
            }
            return dialogue != null || storyJson != null;
        }

        private void Log(string message)
        {
            float remaining = countdown != null ? countdown.GetRemainingTime() : 0f;
            Debug.Log($"[EQ] t={Time.timeSinceLevelLoad:0.0} sayaç={remaining:0.0} {phase} → {message}", this);
        }

        // =====================================================================
        // Otomatik oyun (debugAutoPlay)
        // =====================================================================

        private IEnumerator AutoPilotPrep()
        {
            Log($"OTO: hazırlık senaryosu {debugScenario}");
            var bedroom = RoomOf(EarthquakeRoom.RoomId.Bedroom);
            var living = RoomOf(EarthquakeRoom.RoomId.Living);
            bool thorough = debugScenario == DebugScenario.AllCorrect || debugScenario == DebugScenario.MirrorPenalty;

            // 1) Yatak odasındakileri incele
            int limit = thorough ? 99 : 2;
            foreach (var insp in InspectablesIn(bedroom))
            {
                if (limit-- <= 0) break;
                yield return AutoUse(insp.Point);
            }

            // Timeouts: annenin 95'te seslenmesini (zamanlı Q1) yatak odasında bekle
            if (debugScenario == DebugScenario.Timeouts)
                while (!askedQuestions.Contains("Q1") && !countdown.HasExpired) yield return null;

            // 2) Salona geç
            if (currentRoom != living) yield return AutoGoTo(EarthquakeRoom.RoomId.Living);

            // 3) Ayna cezası testi
            if (debugScenario == DebugScenario.MirrorPenalty || debugScenario == DebugScenario.AllWrong)
                yield return AutoMirrorDwell();

            // 4) Anne (Q1)
            if (!askedQuestions.Contains("Q1") && mother != null) yield return AutoUse(mother);

            // 5) Salondakileri incele
            limit = thorough ? 99 : 3;
            foreach (var insp in InspectablesIn(living))
            {
                if (insp == quakeBag && !thorough) continue;
                if (limit-- <= 0) break;
                yield return AutoUse(insp.Point);
            }
            if (apartmentDoorPoint != null && thorough) yield return AutoUse(apartmentDoorPoint);
            if (emergencyBag != null && thorough && !emergencyBag.IsPrepared)
            {
                Log("OTO: afet çantası hazır sayıldı (mini oyun otomatik testte oynanmaz)");
                emergencyBag.MarkPrepared();
                stats.emergencyBagPrepared = true;
                emergencyBag.ApplyLabel(phase);
            }

            // 6) Sarsıntıyı bekleyeceği yer
            yield return WaitIdle();
            AutoPositionForQuake();

            // 7) Q2/Q3 sayaçla gelir; hazırlık tamamsa hızlandır
            while (!countdown.HasExpired)
            {
                if (readyShown && !stats.acceleratedReady && CanStartEvent()
                    && (debugScenario == DebugScenario.AllCorrect || debugScenario == DebugScenario.MirrorPenalty))
                {
                    yield return new WaitForSeconds(0.5f);
                    if (CanStartEvent()) { Log("OTO: [Space] hızlandır"); Accelerate(); }
                }
                yield return null;
            }
        }

        private IEnumerator AutoPilotAfter()
        {
            Log($"OTO: sonrası senaryosu {debugScenario}");
            yield return WaitIdle();
            if (currentRoom == null || currentRoom.Id != EarthquakeRoom.RoomId.Living)
                yield return AutoGoTo(EarthquakeRoom.RoomId.Living);

            if (debugScenario == DebugScenario.AllWrong)
            {
                var glass = zones.Find(z => z.IsActiveIn(EarthquakePhase.After));
                if (glass != null)
                {
                    Log($"OTO: cam kırıklarına basılıyor ({glass.Id})");
                    player.PlaceAt(glass.InnerPoint(), new Vector2(0f, -1f));
                    yield return new WaitForSeconds(1f);
                    if (LivingRoom != null) player.PlaceAt(LivingRoom.DefaultSpawn.position, new Vector2(0f, -1f));
                    yield return new WaitForSeconds(0.3f);
                }
            }
            else if (emergencyBag != null) yield return AutoUse(emergencyBag.Point);
            else if (quakeBag != null) yield return AutoUse(quakeBag.Point);

            // Daire kapısı (AllWrong: çantasız ilk deneme hatırlatma verir, ikinci denemede çıkar)
            for (int i = 0; i < 3 && currentRoom != null && currentRoom.Id == EarthquakeRoom.RoomId.Living; i++)
                if (apartmentDoorPoint != null) yield return AutoUse(apartmentDoorPoint);

            if (debugScenario == DebugScenario.AllWrong && elevator != null) yield return AutoUse(elevator);

            var stairs = doors.Find(d => d.Kind == EarthquakeDoor.DoorKind.StairsDown);
            Interactable stairsPoint = stairs != null ? stairs.Point : null;
            if (stairsPoint == null)
                foreach (var kv in fallbackDoors)
                    if (kv.Key.name == "Stairs") stairsPoint = kv.Key;
            if (stairsPoint != null) yield return AutoUse(stairsPoint);
            else
            {
                Log("OTO: merdiven bulunamadı → toplanma alanına doğrudan geçiliyor");
                stats.usedStairs = true;
                var assembly = RoomOf(EarthquakeRoom.RoomId.Assembly);
                yield return Transition(assembly, assembly != null ? assembly.DefaultSpawn : null, Vector2.zero, PrepareAssembly);
                reachedAssembly = true;
            }
        }

        private EarthquakeRoom LivingRoom => RoomOf(EarthquakeRoom.RoomId.Living);

        private List<InspectableObject> InspectablesIn(EarthquakeRoom room)
        {
            if (room == null) return new List<InspectableObject>();
            return inspectables.FindAll(i => room.Owns(i) && i.gameObject.activeInHierarchy && i.Point != null && i.Point.Available
                                             && !specialPoints.Contains(i.Point));
        }

        private IEnumerator WaitIdle()
        {
            while (!CanStartEvent()) yield return null;
        }

        private IEnumerator AutoUse(Interactable point)
        {
            if (point == null) yield break;
            yield return WaitIdle();
            if (phase == EarthquakePhase.Prep && countdown.HasExpired) yield break;
            Log($"OTO: [E] {point.name} ({point.ActionLabel})");
            yield return interactor.Use(point);
            yield return null;
        }

        private IEnumerator AutoGoTo(EarthquakeRoom.RoomId target)
        {
            var door = doors.Find(d => d.Kind == EarthquakeDoor.DoorKind.Room && d.TargetRoom != null && d.TargetRoom.Id == target
                                       && currentRoom != null && currentRoom.Owns(d));
            Interactable point = door != null ? door.Point : null;
            if (point == null)
            {
                string fallbackName = target == EarthquakeRoom.RoomId.Living ? "BedroomDoor" : "LivingDoor";
                foreach (var kv in fallbackDoors)
                    if (kv.Key.name == fallbackName) point = kv.Key;
            }
            if (point != null)
            {
                yield return AutoUse(point);
                yield break;
            }
            Log($"OTO: {target} kapısı bulunamadı → doğrudan geçiş");
            var room = RoomOf(target);
            yield return Transition(room, room != null ? room.DefaultSpawn : null, Vector2.zero);
        }

        private IEnumerator AutoMirrorDwell()
        {
            var mirror = zones.Find(z => EarthquakeModeData.SameId(z.WarningId, "mirror") && z.IsActiveIn(EarthquakePhase.Prep));
            if (mirror == null)
            {
                Log("OTO: ayna bölgesi yok, ayna testi atlandı");
                yield break;
            }
            Vector3 away = LivingRoom != null ? LivingRoom.DefaultSpawn.position : player.transform.position;
            int before = penalties.CountOf(TimePenaltySystem.PenaltyType.MirrorExposure);
            for (int visit = 1; visit <= 2; visit++)
            {
                yield return WaitIdle();
                Log($"OTO: ayna bölgesine giriliyor ({visit}. kez), 4 sn bekleniyor");
                player.PlaceAt(mirror.InnerPoint(), new Vector2(-1f, 1f));
                for (float t = 0f; t < 4f; t += Time.deltaTime) yield return null;
                player.PlaceAt(away, new Vector2(0f, -1f));
                yield return new WaitForSeconds(0.6f);
                int count = penalties.CountOf(TimePenaltySystem.PenaltyType.MirrorExposure) - before;
                Log($"OTO: ayna cezası sayısı = {count} (beklenen 1)");
            }
        }

        private void AutoPositionForQuake()
        {
            if (player == null) return;
            switch (debugScenario)
            {
                case DebugScenario.AllWrong:
                {
                    var z = zones.Find(x => x.IsActiveIn(EarthquakePhase.Quake) && currentRoom != null && currentRoom.Owns(x)
                                            && !EarthquakeModeData.SameId(x.WarningId, "mirror"))
                            ?? zones.Find(x => x.IsActiveIn(EarthquakePhase.Quake));
                    if (z != null)
                    {
                        Log($"OTO: sarsıntıyı tehlike bölgesinde bekliyor ({z.Id})");
                        player.PlaceAt(z.InnerPoint(), new Vector2(0f, -1f));
                    }
                    break;
                }
                case DebugScenario.Timeouts:
                {
                    // Masadan uzak, bölge dışında bir yer: oda spawn'ı (uygun değilse biraz kaydırılır).
                    Vector3 p = currentRoom != null ? currentRoom.DefaultSpawn.position : player.transform.position;
                    for (int i = 0; i < 8 && (protection.FindTable(p) != null || zones.Exists(z => z.IsActiveIn(EarthquakePhase.Quake) && z.Contains(p))); i++)
                        p += new Vector3(0.5f, -0.25f, 0f);
                    Log("OTO: sarsıntıyı masadan uzakta bekliyor (yerinde Ç-K)");
                    player.PlaceAt(p, new Vector2(0f, -1f));
                    break;
                }
                default:
                {
                    SturdyTable table = null;
                    foreach (var t in protection.Tables)
                        if (t != null && currentRoom != null && currentRoom.Owns(t)) { table = t; break; }
                    if (table == null && protection.Tables.Length > 0) table = protection.Tables[0];
                    if (table != null)
                    {
                        Log($"OTO: sarsıntıyı sağlam masanın yanında bekliyor ({table.name})");
                        player.PlaceAt(table.ApproachPosition, new Vector2(0f, 1f));
                    }
                    break;
                }
            }
        }
    }
}
