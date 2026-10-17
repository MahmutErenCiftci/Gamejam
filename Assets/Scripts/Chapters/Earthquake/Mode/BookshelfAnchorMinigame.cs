using System.Collections;
using AfetUyanisi.Chapters.Earthquake.Mode;
using AfetUyanisi.Chapters.Rooms;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// KİTAPLIĞI SABİTLE mini oyunu (deprem ÖNCESİ hazırlık; görev id "secure_bookshelf"). Yatak odasındaki kitaplığın
    /// InspectableObject'i ile aynı objeye eklenir; E ile açılır (tehlike "bulundu" sayılır), akış:
    ///  1 Etkileşim → 2 Keşif (kitaplık hafifçe sallanır, Cenk: "Bu kitaplık duvara sabitlenmemiş!...")
    ///  → 3 Malzeme seçimi (duvar ankrajı + kayış / yalnızca raf vidası / dekoratif bant; yanlış seçim de denenebilir)
    ///  → 4 İki sabitleme noktası sırayla (fare ya da E basılı tutulur) → 5 TEST ET (yatay sarsma):
    ///  Başarılı (görev tamam, bookshelfSecured) / Eksik sabitleme (tamamla) / Hatalı malzeme (yeniden seç).
    /// Sayaç HİÇ durmaz (EarthquakeMinigameHub.AnyOpen iken mod kontrolcüsü duraklatmayı kaldırır); sayaç biter ya da
    /// InterruptRequested gelirse mini oyun anında kapanır, kamera ve girdi geri verilir. Esc ilerlemeyi koruyarak çıkar.
    /// Deprem sırasında: sabitlendiyse kitaplık yerinde kalır (yalnızca titrer); sabitlenmediyse devrilir (devrik sprite,
    /// sandalye devrilir, yer kaplanır) ve rapor bunu "Öğrenilecekler"e yazar (EarthquakeStats.bookshelfSecured).
    /// Kamera: PixelPerfectCamera varsa referans çözünürlüğü tam sayı oranla küçültülür (piksel-güvenli 2x yakınlaşma),
    /// kapanınca konum, refResolution ve orthographicSize birebir geri yüklenir.
    /// </summary>
    [RequireComponent(typeof(InspectableObject))]
    public class BookshelfAnchorMinigame : MonoBehaviour
    {
        public const string MinigameId = "secure_bookshelf";

        /// <summary>Sabitleme malzemesi (kart sırası).</summary>
        public enum AnchorMaterial { None = -1, AnchorStrap = 0, ShelfScrew = 1, Tape = 2 }

        /// <summary>Mini oyun durumu.</summary>
        public enum State { Idle, Opening, Discover, Choose, Install, Testing, Result, Closing }

        [Header("Bağlantılar (boşsa sahnede bulunur)")]
        [SerializeField] private EarthquakeModeController mode;
        [SerializeField] private BookshelfAnchorView view;
        [SerializeField] private EmergencyCountdownManager countdown;
        [SerializeField] private EarthquakeEventController quakeEvents;
        [SerializeField] private InfoCardViewBase cardView;
        [SerializeField] private RoomInteractor interactor;
        [SerializeField] private IsoPlayerController player;

        [Header("Dünyadaki kitaplık (boşsa odanın Furniture altında adla bulunur)")]
        [Tooltip("Furniture/Bookshelf (ayakta kitaplık)")]
        [SerializeField] private SpriteRenderer shelfRenderer;
        [Tooltip("Furniture/Bookshelf/Bookshelf_Anchors — sabitlenince açılan köşebent/kayış bindirmesi")]
        [SerializeField] private GameObject anchorsVisual;
        [Tooltip("Furniture/Bookshelf_Fallen — sabitlenmediyse sarsıntıda açılan devrik kitaplık")]
        [SerializeField] private GameObject fallenVisual;
        [Tooltip("Furniture/Chair — kitaplık devrilince devrik sandalye sprite'ına geçer")]
        [SerializeField] private SpriteRenderer chairRenderer;
        [SerializeField] private Sprite chairFallenSprite;

        [Header("Kamera")]
        [Tooltip("Kitaplık konumuna eklenen odak kayması (dünya birimi)")]
        [SerializeField] private Vector2 focusOffset = new Vector2(0f, 0.5f);
        [Tooltip("Tam sayı yakınlaşma (PixelPerfectCamera refResolution / bu değer)")]
        [SerializeField, Range(1, 3)] private int zoomFactor = 2;
        [SerializeField] private float cameraSeconds = 0.35f;

        [Header("Akış")]
        [Tooltip("Bir noktayı bağlamak için basılı tutma süresi (sn)")]
        [SerializeField] private float holdSeconds = 0.8f;
        [Tooltip("Bırakınca ilerlemenin sıfıra inme süresi (sn)")]
        [SerializeField] private float holdDecaySeconds = 1.2f;
        [Tooltip("Keşif balonu kendiliğinden geçme süresi (sn)")]
        [SerializeField] private float discoverSeconds = 4f;
        [Tooltip("Başarı ekranının kendiliğinden kapanma süresi (sn)")]
        [SerializeField] private float successHoldSeconds = 2.2f;

        [Header("Deprem")]
        [Tooltip("Sarsıntı başladıktan kaç sn sonra sabitlenmemiş kitaplık devrilir")]
        [SerializeField] private float fallDelay = 1.3f;
        [SerializeField] private float tipSeconds = 0.35f;

        [Header("Ses kancaları (isteğe bağlı; klip yoksa sessiz)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip wobbleClip;
        [SerializeField] private AudioClip selectClip;
        [SerializeField] private AudioClip drillClip;
        [SerializeField] private AudioClip fastenClip;
        [SerializeField] private AudioClip shakeClip;
        [SerializeField] private AudioClip successClip;
        [SerializeField] private AudioClip warnClip;
        [SerializeField] private AudioClip failClip;
        [SerializeField] private AudioClip crashClip;

        [Header("Metinler")]
        [SerializeField] private string actionLabel = "Kitaplığı sabitle";
        [SerializeField] private string securedLabel = "İncele";
        [SerializeField, TextArea] private string discoverLine = "Bu kitaplık duvara sabitlenmemiş! Depremde devrilebilir.";

        private static readonly string[] MaterialNames = { "Duvar ankrajı + sabitleme kayışı", "Yalnızca raf vidası", "Dekoratif bant" };

        private InspectableObject inspectable;
        private EarthquakeRoom room;
        private Collider2D shelfCollider;
        private PolygonCollider2D fallenCollider;
        private State state = State.Idle;
        private AnchorMaterial material = AnchorMaterial.None;
        private int selectedCard = -1;
        private int installed;
        private float holdProgress;
        private bool discovered;
        private bool secured;
        private bool fell;
        private bool open;
        private bool interrupted;
        private bool exitRequested;
        private bool eNeedsRelease;
        private int inputGuardFrame;
        private bool wasShaking;
        private bool quakeHandled;
        private Coroutine cameraRoutine;
        private bool cameraBusy;
        private readonly int[] verdicts = { 0, 0, 0 };

        // Fare olayları (görünümden)
        private int clickedCard = -1;
        private bool clickedSelect, clickedTest, clickedChange, clickedResult, clickedContinue;

        // Kamera durumu
        private Camera cam;
        private PixelPerfectCamera ppc;
        private ChapterCamera chapterCam;
        private bool cameraSaved;
        private Vector3 savedCamPos;
        private float savedOrtho;
        private int savedRefX, savedRefY;
        private Transform savedFollow;

        // ---- Test / diğer sistemler için okunur durum ----
        public State CurrentState => state;
        public AnchorMaterial ChosenMaterial => material;
        public int Installed => installed;
        public bool IsOpen => open;
        public bool Secured => secured;
        public bool Fell => fell;
        public bool Discovered => discovered;

        // =====================================================================
        // Kurulum
        // =====================================================================

        private void Awake()
        {
            inspectable = GetComponent<InspectableObject>();
            room = GetComponentInParent<EarthquakeRoom>();
            if (mode == null) mode = FindAnyObjectByType<EarthquakeModeController>(FindObjectsInactive.Include);
            if (view == null) view = FindAnyObjectByType<BookshelfAnchorView>(FindObjectsInactive.Include);
            if (countdown == null) countdown = FindAnyObjectByType<EmergencyCountdownManager>(FindObjectsInactive.Include);
            if (quakeEvents == null) quakeEvents = FindAnyObjectByType<EarthquakeEventController>(FindObjectsInactive.Include);
            if (cardView == null) cardView = FindAnyObjectByType<InfoCardViewBase>(FindObjectsInactive.Include);
            if (interactor == null) interactor = FindAnyObjectByType<RoomInteractor>(FindObjectsInactive.Include);
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);

            var furniture = room != null ? room.transform.Find("Furniture") : null;
            if (shelfRenderer == null && furniture != null)
            {
                var t = furniture.Find("Bookshelf");
                if (t != null) shelfRenderer = t.GetComponent<SpriteRenderer>();
            }
            if (shelfRenderer != null)
            {
                shelfCollider = shelfRenderer.GetComponent<Collider2D>();
                if (anchorsVisual == null)
                {
                    var a = shelfRenderer.transform.Find("Bookshelf_Anchors");
                    if (a != null) anchorsVisual = a.gameObject;
                }
            }
            if (fallenVisual == null && furniture != null)
            {
                var f = furniture.Find("Bookshelf_Fallen");
                if (f != null) fallenVisual = f.gameObject;
            }
            if (chairRenderer == null && furniture != null)
            {
                var c = furniture.Find("Chair");
                if (c != null) chairRenderer = c.GetComponent<SpriteRenderer>();
            }
            if (fallenVisual != null)
            {
                fallenCollider = fallenVisual.GetComponent<PolygonCollider2D>();
                fallenVisual.SetActive(false);
            }
            if (anchorsVisual != null) anchorsVisual.SetActive(false);
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        private void OnEnable()
        {
            EarthquakeMinigameHub.InterruptRequested += OnInterruptRequested;
            if (view != null)
            {
                view.CardClicked += OnCardClicked;
                view.SelectClicked += OnSelectClicked;
                view.TestClicked += OnTestClicked;
                view.ChangeMaterialClicked += OnChangeClicked;
                view.ResultClicked += OnResultClicked;
                view.ContinueClicked += OnContinueClicked;
            }
        }

        private void OnDisable()
        {
            EarthquakeMinigameHub.InterruptRequested -= OnInterruptRequested;
            if (view != null)
            {
                view.CardClicked -= OnCardClicked;
                view.SelectClicked -= OnSelectClicked;
                view.TestClicked -= OnTestClicked;
                view.ChangeMaterialClicked -= OnChangeClicked;
                view.ResultClicked -= OnResultClicked;
                view.ContinueClicked -= OnContinueClicked;
            }
            if (open) Interrupt("devre dışı");
        }

        private void Start()
        {
            // Mod kontrolcüsü Awake'te kitaplığı "İncele" kartına bağladı; aynı noktayı mini oyuna yeniden bağla.
            if (interactor != null && inspectable != null && inspectable.Point != null)
            {
                interactor.Register(inspectable.Point, before: OnUse);
                inspectable.Point.Setup(actionLabel, "", false, false);
                inspectable.Point.SetAvailable(true);
            }
            else Debug.LogWarning("[EQ-SHELF] RoomInteractor ya da Interactable yok; mini oyun bağlanamadı.", this);
            if (view == null) Debug.LogWarning("[EQ-SHELF] BookshelfAnchorView yok; mini oyun yalnızca Console'a yazar.", this);
        }

        // =====================================================================
        // Etkileşim
        // =====================================================================

        private EarthquakePhase Phase => mode != null ? mode.Phase : EarthquakePhase.Prep;

        private IEnumerator OnUse()
        {
            bool prep = Phase == EarthquakePhase.Prep && (countdown == null || !countdown.HasExpired);
            if (mode != null && mode.AutoPlay)
            {
                // Otomatik test turu: mini oyunu açmadan yalnızca tehlikeyi "bulundu" say.
                MarkFound();
                Log("otomatik oyun: mini oyun atlandı, tehlike bulundu sayıldı");
                yield break;
            }
            if (!prep || secured || view == null)
            {
                yield return ShowInfoCard();
                yield break;
            }
            yield return Run();
        }

        private void MarkFound()
        {
            if (inspectable == null) return;
            inspectable.MarkInspected();
            bool preQuake = Phase == EarthquakePhase.Intro || Phase == EarthquakePhase.Prep;
            if (mode != null && mode.Stats != null) mode.Stats.AddInspected(inspectable.Id, inspectable.IsHazard && preQuake);
        }

        private IEnumerator ShowInfoCard()
        {
            MarkFound();
            string title = inspectable != null ? inspectable.Title : "Kitaplık";
            string body; HazardClass kind;
            if (Phase == EarthquakePhase.After || Phase == EarthquakePhase.Report)
            {
                if (secured) { body = "Sabitlediğin kitaplık yerinde kaldı. Birkaç kitap düştü, dikkatli geç."; kind = HazardClass.Protective; }
                else if (fell) { body = "Kitaplık devrildi! Sabitlenmemiş uzun mobilyalar depremde devrilir. Yanından uzak dur, bir büyüğüne haber ver."; kind = HazardClass.Hazard; }
                else { body = inspectable != null ? inspectable.AfterText : ""; kind = HazardClass.Hazard; }
            }
            else if (secured) { body = "Kitaplık duvara ankraj ve kayışla sabitlendi. Depremde devrilmeyecek."; kind = HazardClass.Protective; }
            else { body = inspectable != null ? inspectable.Text : ""; kind = HazardClass.Hazard; }
            Log($"kart: {title} — {body}");
            if (cardView != null) yield return cardView.ShowCard(title, body, kind);
            else yield return new WaitForSeconds(1.5f);
        }

        // =====================================================================
        // Mini oyun
        // =====================================================================

        private IEnumerator Run()
        {
            open = true;
            interrupted = false;
            exitRequested = false;
            eNeedsRelease = true;
            ClearClicks();
            state = State.Opening;
            EarthquakeMinigameHub.NotifyOpened(MinigameId);
            MarkFound();
            Log($"açıldı (malzeme {material}, bağlantı {installed}/2)");
            Play(openClip);

            SaveCamera();
            cameraRoutine = StartCoroutine(CameraIn());
            yield return view.Open(0.3f);
            if (interrupted) yield break;
            inputGuardFrame = Time.frameCount + 1;

            if (!discovered)
            {
                state = State.Discover;
                view.SetStep(0);
                view.ShowDiscover(discoverLine, inspectable != null ? inspectable.Text : "");
                view.StartCoroutine(view.Wobble(0.9f, 1.8f));
                Play(wobbleClip);
                float t = 0f;
                while (!interrupted && !exitRequested && t < discoverSeconds)
                {
                    if (CheckExit()) break;
                    if (Time.frameCount > inputGuardFrame && (clickedContinue || (!eNeedsRelease && GameInput.ContinuePressed) || GameInput.ConfirmPressed)) break;
                    if (!GameInput.InteractHeld) eNeedsRelease = false;
                    t += Time.deltaTime;
                    yield return null;
                }
                clickedContinue = false;
                if (interrupted) yield break;
                discovered = true;
                view.HideDiscover();
            }

            if (!exitRequested) EnterChooseOrInstall();

            while (!interrupted && !exitRequested)
            {
                if (CheckExit()) break;
                if (!GameInput.InteractHeld) eNeedsRelease = false;
                switch (state)
                {
                    case State.Choose: TickChoose(); break;
                    case State.Install: TickInstall(); break;
                    case State.Testing: break;
                    case State.Result: yield return TickResult(); break;
                }
                ClearClicks();
                if (state == State.Idle) break; // başarıyla kapandı
                yield return null;
            }

            if (interrupted) yield break;
            if (open) yield return CloseSmooth(exitRequested ? "Esc ile çıkıldı (ilerleme korunur)" : "kapandı");
        }

        private bool CheckExit()
        {
            if (!GameInput.BackPressed || Time.frameCount <= inputGuardFrame) return false;
            exitRequested = true;
            return true;
        }

        private void EnterChooseOrInstall()
        {
            if (material == AnchorMaterial.None)
            {
                state = State.Choose;
                view.SetStep(1);
                view.ShowMaterials(selectedCard);
                for (int i = 0; i < 3; i++) view.SetCardVerdict(i, verdicts[i]);
            }
            else
            {
                state = State.Install;
                view.SetStep(2);
                view.ShowInstall((int)material, installed, MaterialNames[(int)material]);
                view.ClearHardware();
                for (int i = 0; i < installed; i++) view.PlaceHardware(i, (int)material, false);
                view.SetActivePoint(installed < 2 ? installed : -1, installed);
                holdProgress = 0f;
            }
        }

        private void TickChoose()
        {
            int key = GameInput.ChoiceKeyPressed;
            int pick = clickedCard >= 0 ? clickedCard : (key >= 1 && key <= 3 ? key - 1 : -1);
            if (pick >= 0 && pick != selectedCard)
            {
                selectedCard = pick;
                view.SetCardSelected(pick);
                Play(selectClip);
                Log($"kart seçildi: {MaterialNames[pick]}");
            }
            bool confirm = clickedSelect || GameInput.ConfirmPressed || (!eNeedsRelease && GameInput.InteractPressed);
            if (confirm && selectedCard >= 0 && Time.frameCount > inputGuardFrame)
            {
                material = (AnchorMaterial)selectedCard;
                installed = 0;
                Log($"malzeme onaylandı: {MaterialNames[selectedCard]}");
                eNeedsRelease = GameInput.InteractHeld;
                EnterChooseOrInstall();
            }
        }

        private void TickInstall()
        {
            if (clickedChange)
            {
                Log("malzeme değiştiriliyor");
                material = AnchorMaterial.None;
                installed = 0;
                view.ClearHardware();
                view.SetActivePoint(-1, 0);
                EnterChooseOrInstall();
                return;
            }
            if (installed < 2)
            {
                bool holding = (!eNeedsRelease && GameInput.InteractHeld) || view.PointerHoldingActivePoint;
                float before = holdProgress;
                holdProgress = Mathf.Clamp01(holdProgress + (holding ? Time.deltaTime / holdSeconds : -Time.deltaTime / holdDecaySeconds));
                if (holding && before <= 0f) Play(drillClip);
                view.SetHoldProgress(holdProgress, holding);
                if (holdProgress >= 1f)
                {
                    installed++;
                    holdProgress = 0f;
                    view.PlaceHardware(installed - 1, (int)material, true);
                    view.SetInstallProgress(installed);
                    view.SetActivePoint(installed < 2 ? installed : -1, installed);
                    Play(fastenClip);
                    Log($"bağlantı {installed}/2 tamam ({material})");
                }
            }
            bool test = clickedTest || GameInput.ConfirmPressed;
            if (test && installed >= 1) StartCoroutine(RunTest());
        }

        private IEnumerator RunTest()
        {
            state = State.Testing;
            view.SetStep(3);
            view.ShowTesting();
            var kind = material != AnchorMaterial.AnchorStrap ? BookshelfAnchorView.ResultKind.WrongMaterial
                : installed >= 2 ? BookshelfAnchorView.ResultKind.Success : BookshelfAnchorView.ResultKind.Incomplete;
            Log($"TEST ET: malzeme {material}, bağlantı {installed}/2 → {kind}");
            Play(shakeClip);
            var routine = view.StartCoroutine(view.PlayTest(kind));
            float wait = 1.6f + 0.3f;
            for (float t = 0f; t < wait; t += Time.deltaTime)
            {
                if (interrupted || exitRequested || !open)
                {
                    if (routine != null && view != null) view.StopCoroutine(routine);
                    yield break;
                }
                yield return null;
            }

            switch (kind)
            {
                case BookshelfAnchorView.ResultKind.Success:
                    verdicts[0] = 1;
                    Secure();
                    Play(successClip);
                    view.ShowResult(kind, "Başarılı", "Kitaplık sağlam şekilde sabitlendi!\nDepremde yerinde kalacak.",
                        "Duvar ankrajı (dübel + vida) kitaplığı duvara bağlar, kayış devrilmesini önler. Ağır kitapları alt raflara koymak da ek güvenlik sağlar.",
                        "Tamam");
                    break;
                case BookshelfAnchorView.ResultKind.Incomplete:
                    Play(warnClip);
                    view.ShowResult(kind, "Eksik sabitleme", "Bağlantılar tam değil.\nŞiddetli sarsıntıda devrilebilir.",
                        "Tek bağlantı kitaplığın bir yanını boşta bırakır. İkinci noktayı da sabitle, sonra yeniden test et.",
                        "Bağlantıyı tamamla");
                    break;
                default:
                    verdicts[(int)material] = -1;
                    Play(failClip);
                    string edu = material == AnchorMaterial.Tape
                        ? "Dekoratif bant ağırlık taşımaz, sarsıntıda hemen kopar. Duvar ankrajı ve sabitleme kayışı kullanmalısın."
                        : "Raf vidası yalnızca rafları birbirine tutar, kitaplığı duvara bağlamaz. Duvar ankrajı ve sabitleme kayışı kullanmalısın.";
                    view.ShowResult(kind, "Hatalı malzeme", "Seçilen malzeme uygun değil.\nKitaplık sabitlenemedi.", edu, "Malzemeyi değiştir");
                    break;
            }
            lastResult = kind;
            resultShownAt = Time.time;
            inputGuardFrame = Time.frameCount + 1;
            state = State.Result;
        }

        private BookshelfAnchorView.ResultKind lastResult;
        private float resultShownAt;

        private IEnumerator TickResult()
        {
            bool go = clickedResult || ((GameInput.ConfirmPressed || (!eNeedsRelease && GameInput.InteractPressed)) && Time.frameCount > inputGuardFrame);
            if (lastResult == BookshelfAnchorView.ResultKind.Success && Time.time - resultShownAt > successHoldSeconds) go = true;
            if (!go) yield break;
            ClearClicks();
            eNeedsRelease = GameInput.InteractHeld;
            switch (lastResult)
            {
                case BookshelfAnchorView.ResultKind.Success:
                    state = State.Idle;
                    yield return CloseSmooth("başarıyla tamamlandı");
                    break;
                case BookshelfAnchorView.ResultKind.Incomplete:
                    yield return view.ResetShelf(0.3f);
                    EnterChooseOrInstall();
                    break;
                default:
                    yield return view.ResetShelf(0.35f);
                    material = AnchorMaterial.None;
                    selectedCard = -1;
                    installed = 0;
                    view.ClearHardware();
                    EnterChooseOrInstall();
                    break;
            }
        }

        private void Secure()
        {
            if (secured) return;
            secured = true;
            if (mode != null && mode.Stats != null) mode.Stats.bookshelfSecured = true;
            if (anchorsVisual != null) anchorsVisual.SetActive(true);
            if (inspectable != null && inspectable.Point != null) inspectable.Point.Setup(securedLabel, "", false, false);
            // Sabitlenen kitaplık artık devrilmez: "kitaplıktan uzak dur" uyarısı/maruziyeti kapanır.
            foreach (var zone in FindObjectsByType<AfetUyanisi.Chapters.Earthquake.HazardZone>(FindObjectsInactive.Include))
                if (zone.WarningId == "bookshelf" || zone.ExposureKind == "bookshelf") zone.Suppressed = true;
            EarthquakeMinigameHub.CompleteTask(MinigameId);
            Log("GÖREV TAMAM: kitaplık sabitlendi (bookshelfSecured = true)");
        }

        private IEnumerator CloseSmooth(string why)
        {
            state = State.Closing;
            if (cameraRoutine != null) StopCoroutine(cameraRoutine);
            cameraRoutine = StartCoroutine(CameraOut(cameraSeconds));
            yield return view.Close(0.22f);
            float t = 0f;
            while (cameraBusy && t < cameraSeconds + 0.2f && !interrupted) { t += Time.deltaTime; yield return null; }
            FinishClose(why);
        }

        private void FinishClose(string why)
        {
            if (!open) return;
            open = false;
            state = State.Idle;
            if (cameraRoutine != null) { StopCoroutine(cameraRoutine); cameraRoutine = null; }
            cameraBusy = false;
            RestoreCameraInstant();
            EarthquakeMinigameHub.NotifyClosed(MinigameId);
            Log($"kapandı: {why} (malzeme {material}, bağlantı {installed}/2, sabit: {secured})");
        }

        private void OnInterruptRequested() => Interrupt("InterruptRequested");

        /// <summary>Anında güvenli kapanış (sayaç bitti): animasyon yok, kamera ve girdi hemen geri.</summary>
        public void Interrupt(string why)
        {
            if (!open) return;
            interrupted = true;
            StopAllCoroutines();
            cameraRoutine = null;
            cameraBusy = false;
            if (view != null) view.CloseImmediate();
            if (audioSource != null) audioSource.Stop();
            FinishClose("KESİLDİ (" + why + ")");
        }

        // =====================================================================
        // Deprem sonucu
        // =====================================================================

        private void Update()
        {
            if (open && countdown != null && countdown.HasExpired) Interrupt("sayaç bitti");

            if (quakeEvents != null)
            {
                bool shaking = quakeEvents.IsShaking;
                if (shaking && !wasShaking && !quakeHandled)
                {
                    quakeHandled = true;
                    if (open) Interrupt("sarsıntı başladı");
                    if (!secured) StartCoroutine(TipOver());
                    else Log("C: kitaplık sabit — yerinde kalıyor");
                }
                wasShaking = shaking;
            }

            // Devrik kitaplık oyuncuyu içine hapsetmesin: oyuncu izden çıkınca çarpıştırıcı açılır.
            if (fell && fallenCollider != null && !fallenCollider.enabled && player != null && !InsideFallen(player.transform.position))
                fallenCollider.enabled = true;
        }

        private IEnumerator TipOver()
        {
            yield return new WaitForSeconds(fallDelay);
            if (shelfRenderer == null || fallenVisual == null)
            {
                fell = true;
                Log("C: kitaplık devrildi (görsel yok)");
                yield break;
            }
            Log("C: SABİTLENMEMİŞ kitaplık devriliyor!");
            var t = shelfRenderer.transform;
            Quaternion rest = t.localRotation;
            for (float k = 0f; k < tipSeconds; k += Time.deltaTime)
            {
                float e = k / tipSeconds;
                float angle = Mathf.Round(e * e * 34f / 4f) * 4f; // 4°'lik adımlar (piksel dostu)
                t.localRotation = rest * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            t.localRotation = rest;
            shelfRenderer.enabled = false;
            if (shelfCollider != null) shelfCollider.enabled = false;
            foreach (var r in shelfRenderer.GetComponentsInChildren<SpriteRenderer>(true))
                if (r != shelfRenderer) r.enabled = false;
            fallenVisual.SetActive(true);
            if (fallenCollider != null) fallenCollider.enabled = player == null || !InsideFallen(player.transform.position);
            if (chairRenderer != null && chairFallenSprite != null)
            {
                chairRenderer.sprite = chairFallenSprite;
                var shake = chairRenderer.GetComponent<ShakeableProp>();
                if (shake != null) shake.enabled = false;
            }
            fell = true;
            // Devrik kitaplık artık sabitlenemez: ipucu "Kitaplığı sabitle" kalmasın.
            if (inspectable != null && inspectable.Point != null) inspectable.Point.Setup(securedLabel, "", false, false);
            Play(crashClip);
            bool hit = PlayerInFallPath();
            if (mode != null && mode.Stats != null)
            {
                mode.Stats.bookshelfFell = true;
                if (hit) mode.Stats.hitByBookshelf = true;
            }
            if (hit)
            {
                Log("C: kitaplık OYUNCUNUN ÜSTÜNE devrildi → yaralandı");
                StartCoroutine(ScreenFlash(new Color(0.85f, 0.26f, 0.23f, 0.55f), 0.7f));
                StartCoroutine(BlinkPlayer());
            }
            else StartCoroutine(ScreenFlash(new Color(0.96f, 0.92f, 0.85f, 0.3f), 0.3f));
            // küçük "güm": 2 px yukarıdan oturur
            var ft = fallenVisual.transform;
            Vector3 p = ft.localPosition;
            ft.localPosition = p + new Vector3(0f, 2f / 64f, 0f);
            yield return new WaitForSeconds(0.06f);
            ft.localPosition = p;
        }

        /// <summary>
        /// Oyuncu devrilme alanında mı: devrik kitaplığın izi ya da "kitaplıktan uzak dur" tehlike bölgesi.
        /// Sağlam bir masanın altındaysa korunmuş sayılır.
        /// </summary>
        private bool PlayerInFallPath()
        {
            if (player == null) return false;
            Vector3 pos = player.transform.position;
            var protection = FindAnyObjectByType<ProtectionMinigameController>(FindObjectsInactive.Exclude);
            // Yalnızca gerçekten masanın altına geçtiyse korunur. (Masanın 1.2'lik yaklaşma yarıçapı kitaplığın önünü
            // kapladığı için eskiden kitaplığın dibinde duran oyuncu hiç yaralanmıyordu.)
            if (protection != null && protection.IsUnderTable) return false;
            if (InsideFallen(pos)) return true;
            foreach (var zone in FindObjectsByType<HazardZone>(FindObjectsInactive.Exclude))
                if ((zone.WarningId == "bookshelf" || zone.ExposureKind == "bookshelf") && zone.Contains(pos)) return true;
            return false;
        }

        /// <summary>Kısa tam ekran renk parlaması (çarpma/devrilme vurgusu); kendi tuvalini kurar ve sonunda yok eder.</summary>
        private static IEnumerator ScreenFlash(Color color, float seconds)
        {
            var go = new GameObject("EQ_ScreenFlash", typeof(Canvas), typeof(UnityEngine.UI.Image));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            for (float k = 0f; k < seconds; k += Time.unscaledDeltaTime)
            {
                float a = color.a * (1f - k / seconds);
                img.color = new Color(color.r, color.g, color.b, Mathf.Round(a * 8f) / 8f); // kademeli (piksel dostu) sönüm
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>Oyuncu sprite'ı birkaç kez kırmızı yanıp söner (yaralanma geri bildirimi).</summary>
        private IEnumerator BlinkPlayer()
        {
            if (player == null) yield break;
            var renderers = player.GetComponentsInChildren<SpriteRenderer>();
            var original = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) original[i] = renderers[i].color;
            var hurt = new Color(1f, 0.45f, 0.42f, 1f);
            for (int n = 0; n < 4; n++)
            {
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].color = original[i] * hurt;
                yield return new WaitForSeconds(0.12f);
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].color = original[i];
                yield return new WaitForSeconds(0.12f);
            }
        }

        private bool InsideFallen(Vector3 world)
        {
            if (fallenCollider == null) return false;
            var pts = fallenCollider.points;
            Vector2 p = fallenCollider.transform.InverseTransformPoint(world);
            p -= fallenCollider.offset;
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                if (((pts[i].y > p.y) != (pts[j].y > p.y)) && (p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x))
                    inside = !inside;
            return inside;
        }

        // =====================================================================
        // Kamera (piksel-güvenli yakınlaşma)
        // =====================================================================

        private void SaveCamera()
        {
            cam = Camera.main;
            cameraSaved = cam != null;
            if (!cameraSaved) return;
            ppc = cam.GetComponent<PixelPerfectCamera>();
            chapterCam = cam.GetComponent<ChapterCamera>();
            savedCamPos = cam.transform.position;
            savedOrtho = cam.orthographicSize;
            if (ppc != null) { savedRefX = ppc.refResolutionX; savedRefY = ppc.refResolutionY; }
            savedFollow = chapterCam != null ? chapterCam.Target : null;
        }

        private Vector3 Focus()
        {
            Vector3 basePos = shelfRenderer != null ? shelfRenderer.transform.position : transform.position;
            return basePos + (Vector3)focusOffset;
        }

        private IEnumerator CameraIn()
        {
            cameraBusy = true;
            if (!cameraSaved) { cameraBusy = false; yield break; }
            Vector3 focus = Focus();
            Vector3 end = Snap(new Vector3(focus.x, focus.y, savedCamPos.z));
            if (chapterCam != null) chapterCam.ShowPoint(end, false);
            for (float t = 0f; t < cameraSeconds; t += Time.deltaTime)
            {
                if (chapterCam == null) cam.transform.position = Snap(Vector3.Lerp(savedCamPos, end, Mathf.SmoothStep(0f, 1f, t / cameraSeconds)));
                yield return null;
            }
            if (chapterCam == null) cam.transform.position = end;
            ApplyZoom(true);
            cameraBusy = false;
        }

        private IEnumerator CameraOut(float seconds)
        {
            cameraBusy = true;
            if (!cameraSaved) { cameraBusy = false; yield break; }
            ApplyZoom(false);
            Vector3 start = cam.transform.position;
            if (chapterCam != null)
            {
                if (savedFollow != null) chapterCam.Follow(savedFollow, room != null ? room.Bounds : null, false);
                else chapterCam.ShowPoint(savedCamPos, false);
            }
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (chapterCam == null) cam.transform.position = Snap(Vector3.Lerp(start, savedCamPos, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            if (chapterCam == null) cam.transform.position = savedCamPos;
            cameraBusy = false;
        }

        private void ApplyZoom(bool on)
        {
            if (!cameraSaved || cam == null) return;
            int f = Mathf.Max(1, zoomFactor);
            if (ppc != null && ppc.enabled)
            {
                ppc.refResolutionX = on ? Mathf.Max(2, savedRefX / f) : savedRefX;
                ppc.refResolutionY = on ? Mathf.Max(2, savedRefY / f) : savedRefY;
            }
            else cam.orthographicSize = on ? savedOrtho / f : savedOrtho;
        }

        private void RestoreCameraInstant()
        {
            if (!cameraSaved || cam == null) return;
            if (ppc != null) { ppc.refResolutionX = savedRefX; ppc.refResolutionY = savedRefY; }
            cam.orthographicSize = savedOrtho;
            if (chapterCam != null)
            {
                if (savedFollow != null) chapterCam.Follow(savedFollow, room != null ? room.Bounds : null, true);
                else chapterCam.ShowPoint(savedCamPos, true);
            }
            cam.transform.position = savedCamPos;
            cameraSaved = false;
        }

        private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 64f) / 64f, Mathf.Round(p.y * 64f) / 64f, p.z);

        // =====================================================================
        // Yardımcılar
        // =====================================================================

        private void OnCardClicked(int i) { if (state == State.Choose) clickedCard = i; }
        private void OnSelectClicked() { if (state == State.Choose) clickedSelect = true; }
        private void OnTestClicked() { if (state == State.Install) clickedTest = true; }
        private void OnChangeClicked() { if (state == State.Install) clickedChange = true; }
        private void OnResultClicked() { if (state == State.Result) clickedResult = true; }
        private void OnContinueClicked() { if (state == State.Discover) clickedContinue = true; }

        private void ClearClicks()
        {
            clickedCard = -1;
            clickedSelect = clickedTest = clickedChange = clickedResult = false;
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }

        private void Log(string message)
        {
            float remaining = countdown != null ? countdown.GetRemainingTime() : 0f;
            Debug.Log($"[EQ-SHELF] sayaç={remaining:0.0} {state} → {message}", this);
        }
    }
}
