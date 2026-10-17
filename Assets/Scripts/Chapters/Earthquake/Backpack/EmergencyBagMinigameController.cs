using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Earthquake.Mode;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// AFET ÇANTASI mini oyunu (deprem modu, yatak odası). Yakın plan arayüzünü Awake'te değil ilk açılışta kodla kurar
    /// (640x360 referans, Canvas sırası 84: 120 sn sayacı (86) her zaman üstte görünür).
    /// Durumlar: Idle → CameraZoom → BackpackOpening → Packing ⇄ ReadyToClose → ZipperClosing → Success → CameraReturn → Idle.
    /// Esc: güvenle çıkar (içerik korunur, görev tamamlanmaz). Sayaç biterse (EarthquakeMinigameHub.InterruptRequested ya da
    /// sayaç HasExpired) her şey anında kesilir: sürükleme/fermuar iptal, arayüz kapanır, kamera eski hâline döner.
    /// Mini oyun sayacı DURDURMAZ. Başarıda EarthquakeMinigameHub.CompleteTask("emergency_bag").
    /// Kamera: ChapterCamera ve PixelPerfectCamera geçici kapatılır, 0.5 sn yumuşak yakınlaşma (ortho boyutu / zoomFactor),
    /// çıkışta konum + boyut birebir geri yazılır ve bileşenler yeniden açılır.
    /// Eşyalar EmergencyItemData varlıklarıdır (Inspector'daki items listesi); arayüz sprite'ları Assets/Art/UI/Earthquake/Backpack.
    /// </summary>
    public class EmergencyBagMinigameController : MonoBehaviour
    {
        public enum BagState { Idle, CameraZoom, BackpackOpening, Packing, ReadyToClose, ZipperClosing, Success, CameraReturn }

        public const string MinigameId = "emergency_bag";
        public const string TaskId = "emergency_bag";

        private enum Tone { Info, Good, Warn, Comfort }

        [Header("Eşyalar")]
        [Tooltip("Masadaki eşyalar (gerekli gruplar + rahatlatıcı + gereksiz). Sıra = masadaki sıra (4 sütun).")]
        [SerializeField] private EmergencyItemData[] items = Array.Empty<EmergencyItemData>();
        [Tooltip("Çantanın ana yuva sayısı (3 sütun ızgara)")]
        [SerializeField, Range(3, 12)] private int slotCount = 9;
        [Tooltip("Ön cep: rahatlatıcı eşya (oyuncak ayı) ana yuvaları doldurmadan buraya girer")]
        [SerializeField] private bool frontPocket = true;

        [Header("Arayüz")]
        [SerializeField] private EarthquakeUISkin skin;
        [Tooltip("Sayaç Canvas'ı 86: mini oyun onun altında kalır, süre hep görünür")]
        [SerializeField] private int sortingOrder = 84;
        [Tooltip("Yakın plan sanatında 1 sprite pikseli = kaç referans pikseli")]
        [SerializeField, Range(1, 3)] private int artScale = 2;
        [SerializeField] private Sprite bagBody;
        [SerializeField] private Sprite bagLid;
        [SerializeField] private Sprite bagGlow;
        [SerializeField] private Sprite zipHandle;
        [SerializeField] private Sprite slotSprite;
        [SerializeField] private Sprite slotFrame;
        [SerializeField] private Sprite tableSprite;
        [SerializeField] private Sprite cardFrame;
        [SerializeField] private Sprite itemShadow;
        [SerializeField] private Sprite sparkle;
        [SerializeField] private Sprite arrow;
        [SerializeField] private Sprite cenkPortrait;

        [Header("Kamera")]
        [SerializeField, Range(1f, 3f)] private float zoomFactor = 2f;
        [SerializeField] private float zoomSeconds = 0.5f;
        [SerializeField] private float returnSeconds = 0.45f;
        [Tooltip("Yakınlaşmada odanın kararma miktarı")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.62f;
        [Tooltip("Kameranın çantaya göre hedef kayması (dünya birimi)")]
        [SerializeField] private Vector2 focusOffset = new Vector2(-0.6f, 0.2f);

        [Header("Ses (boş bırakılabilir)")]
        [SerializeField] private AudioClip interactClip;
        [SerializeField] private AudioClip zipOpenClip;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField] private AudioClip dropClip;
        [SerializeField] private AudioClip correctClip;
        [SerializeField] private AudioClip warningClip;
        [SerializeField] private AudioClip removeClip;
        [SerializeField] private AudioClip zipTickClip;
        [SerializeField] private AudioClip zipCloseClip;
        [SerializeField] private AudioClip successClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        [Header("Metinler")]
        [SerializeField] private string title = "AFET ÇANTASI";
        [SerializeField] private string instruction = "Gerekli eşyaları çantaya sürükle.";
        [SerializeField] private string speakerName = "CENK";
        [SerializeField] private string introLine = "Afet çantasında neler olmalı? Gerekli eşyaları seçmeliyim.";
        [SerializeField] private string idleHint = "Eşyayı fareyle tut, çantanın içine bırak.";
        [SerializeField] private string fullWarning = "Çanta doldu! Öncelikli ihtiyaçlarını kontrol et.";
        [SerializeField] private string unnecessaryDefault = "Bu eşya gerekli malzemeler için yer bırakmayabilir.";
        [SerializeField] private string comfortDefault = "Moral verir ama önceliği düşük. Önce temel ihtiyaçlar!";
        [SerializeField] private string closePrompt = "Çantayı kapat.";
        [SerializeField] private string zipHint = "Fermuarı tut ve sonuna kadar sağa sürükle.";
        [SerializeField] private string successTitle = "Afet çantası hazır!";
        [SerializeField] private string successBody = "Temel ihtiyaçlarını hazırladın.";
        [SerializeField] private string successBadge = "HAZIRLIK TAMAMLANDI";
        [SerializeField] private string removeLabel = "Çıkar";
        [SerializeField] private string escLabel = "Çık";
        [SerializeField] private string packedLabel = "ÇANTADAKİLER";
        [SerializeField] private string zipBlockedText = "Önce fermuarı biraz aç.";

        [Header("Test")]
        [Tooltip("Adımları Console'a yazar ([BAG] ...)")]
        [SerializeField] private bool logSteps = true;

        // ---------------- Yerleşim (referans pikseli, sol-üst orijin) ----------------
        private const float IntroSeconds = 2.2f;
        private static readonly Rect TableRect = new Rect(12f, 46f, 312f, 232f);
        private static readonly Rect FeedbackRect = new Rect(12f, 284f, 312f, 64f);
        private static readonly Rect BagRect = new Rect(368f, 36f, 224f, 256f);
        private static readonly Rect CollectRect = new Rect(360f, 298f, 240f, 50f);
        private const float CellW = 72f, CellH = 61f, CellX0 = 12f, CellY0 = 12f;
        // Çanta sanatı (sprite pikseli, 112x128): iç bölme, yuvalar, cep, fermuar yolu
        private const int ArtCavityX = 18, ArtCavityY = 28, ArtSlotW = 24, ArtSlotH = 22, ArtSlotGap = 2;
        private const int ArtPocketX = 44, ArtPocketY = 100;
        private const int ArtZipY = 26, ArtZipX0 = 18, ArtZipX1 = 94, ArtLidX0 = 16, ArtLidX1 = 96, ArtBagW = 112;

        // ---------------- Durum ----------------
        private BagState state = BagState.Idle;
        private EmergencyBagInventory inventory;
        private readonly List<EmergencyItemData> required = new List<EmergencyItemData>();
        private Action<bool> onClosed;
        private bool completed;
        private bool introShown;
        private bool hubOpen;
        private int selectedSlot = EmergencyBagInventory.NoSlot;
        private float lastTickProgress;
        private float introUntil = -1f;
        private Coroutine flowRoutine;
        private Coroutine missingRoutine;
        private EmergencyItemDragHandler draggingCard;
        private RectTransform dragGhost;
        private bool dropHandled;
        private EmergencyCountdownManager countdown;
        private IsoPlayerController player;
        private bool playerCouldMove;

        // Kamera
        private Camera cam;
        private Behaviour pixelPerfect;
        private Behaviour chapterCamera;
        private bool pixelPerfectWasOn, chapterCameraWasOn;
        private Vector3 savedCamPos;
        private float savedCamSize;
        private bool cameraSaved;
        private Transform focus;

        // Arayüz
        private bool built;
        private Font font;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Image dim;
        private CanvasGroup rootGroup;
        private RectTransform root;
        private Text instructionText;
        private RectTransform table;
        private readonly List<Card> cards = new List<Card>();
        private Image feedbackPanel;
        private RectTransform portraitRoot;
        private Image portraitImage;
        private Image feedbackIcon;
        private Text feedbackName;
        private Text feedbackBody;
        private bool feedbackOnLight;
        private EqButton removeButton;
        private RectTransform bagRoot;
        private RectTransform bagScale;
        private Image bagBodyImage;
        private Image bagGlowImage;
        private Image lidImage;
        private readonly List<Slot> slots = new List<Slot>();
        private Slot pocketSlot;
        private RectTransform zipTrack;
        private RectTransform zipHandleRect;
        private Image zipHandleImage;
        private EmergencyBagZipperController zipper;
        private RectTransform zipArrow;
        private Vector2 zipArrowBase;
        private RectTransform fxLayer;
        private Image checkImage;
        private readonly List<Image> collectBoxes = new List<Image>();
        private Image collectPocketBox;
        private Text collectCount;
        private RectTransform successBanner;
        private CanvasGroup successGroup;
        private RectTransform dragLayer;
        private AudioSource audioSource;

        private sealed class Card
        {
            public EmergencyItemData item;
            public RectTransform rt;
            public EmergencyItemDragHandler handler;
            public Image frame;
            public Image icon;
            public Image check;
            public Text label;
            public bool packed;
        }

        private sealed class Slot
        {
            public int index;
            public RectTransform rt;
            public Image back;
            public Image icon;
            public Image frame;
        }

        // =====================================================================
        // Dış API
        // =====================================================================

        public BagState State => state;
        public bool IsOpen => state != BagState.Idle;
        public bool IsCompleted => completed;
        public EmergencyBagInventory Inventory => inventory;
        public IReadOnlyList<EmergencyItemData> Items => items;
        public int RequiredCount => required.Count;
        public int RequiredPacked => inventory != null ? inventory.CountPacked(required) : 0;
        public float ZipProgress => zipper != null ? zipper.Progress : 0f;
        public string FeedbackText => feedbackBody != null ? feedbackBody.text : "";

        /// <summary>Başarıyla kapatıldığında (fermuar sonuna kadar) bir kez tetiklenir.</summary>
        public event Action Completed;
        /// <summary>Mini oyun kapandığında (tamamlandı mı).</summary>
        public event Action<bool> Closed;

        private void Awake()
        {
            EnsureModel();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        private void EnsureModel()
        {
            if (inventory != null) return;
            inventory = new EmergencyBagInventory(slotCount, frontPocket);
            required.Clear();
            foreach (var it in items)
                if (it != null && it.IsRequired && !required.Contains(it)) required.Add(it);
            if (required.Count > slotCount)
                Debug.LogWarning($"[BAG] gerekli eşya sayısı ({required.Count}) yuva sayısından ({slotCount}) fazla — çanta hiç tamamlanamaz!", this);
        }

        /// <summary>
        /// Mini oyunu açar (çanta objesi = kamera odağı). Açıksa, tamamlandıysa ya da eşya yoksa false.
        /// closed(tamamlandı mı) mini oyun kapanınca (Esc, başarı ya da sayaç kesmesi) bir kez çağrılır.
        /// </summary>
        public bool Open(Transform focusTarget, Action<bool> closed)
        {
            EnsureModel();
            if (IsOpen || completed) return false;
            if (items == null || items.Length == 0)
            {
                Debug.LogWarning("[BAG] eşya listesi boş, mini oyun açılmadı", this);
                return false;
            }
            countdown = FindAnyObjectByType<EmergencyCountdownManager>();
            if (countdown != null && countdown.HasExpired) return false;

            EnsureBuilt();
            focus = focusTarget;
            onClosed = closed;
            hubOpen = true;
            EarthquakeMinigameHub.InterruptRequested += Interrupt;
            EarthquakeMinigameHub.NotifyOpened(MinigameId);

            player = FindAnyObjectByType<IsoPlayerController>();
            if (player != null)
            {
                playerCouldMove = player.CanMove;
                player.CanMove = false;
            }
            Play(interactClip);
            Log($"açıldı (gerekli {RequiredPacked}/{required.Count}, dolu {inventory.FilledCount}/{inventory.Capacity})");
            flowRoutine = StartCoroutine(OpenFlow());
            return true;
        }

        /// <summary>Esc ile aynı: güvenle çıkar (içerik korunur, görev tamamlanmaz).</summary>
        public void Exit()
        {
            if (!IsCancellable()) return;
            Log("Esc → çıkılıyor (içerik korunuyor)");
            CancelInteractions();
            StopFlow();
            flowRoutine = StartCoroutine(CloseFlow(false));
        }

        /// <summary>Sayaç bitti ya da hub kesmesi: her şey ANINDA kapanır, kamera eski hâline döner.</summary>
        public void Interrupt()
        {
            if (!IsOpen) return;
            Log($"KESİLDİ ({state}) → anında kapanıyor");
            StopFlow();
            StopAllCoroutines();
            CancelInteractions();
            RestoreCamera();
            HideUiImmediate();
            // Oyuncunun hareketini mod kontrolcüsü yönetir (sarsıntı başlıyor): burada açılmaz.
            Finish(restorePlayer: false);
        }

        // =====================================================================
        // Akış
        // =====================================================================

        private IEnumerator OpenFlow()
        {
            state = BagState.CameraZoom;
            canvas.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            successGroup.alpha = 0f;
            successBanner.gameObject.SetActive(false);
            checkImage.gameObject.SetActive(false);
            SetGlow(0f, EqUi.Gold);
            RefreshAll();
            SetZipperVisible(false);
            zipper.SetProgress(0f, false);
            SetLid(1f); // kapalı çanta ile başlar
            bagScale.localScale = new Vector3(0.9f, 0.9f, 1f);
            bagScale.anchoredPosition = new Vector2(0f, -10f);
            ShowFeedback(Tone.Info, "", introShown ? idleHint : "", null, false);
            SetInstruction(instruction);

            SaveCamera();
            Vector3 fromPos = savedCamPos;
            float fromSize = savedCamSize;
            Vector3 target = focus != null ? focus.position + (Vector3)focusOffset : fromPos;
            Vector3 toPos = SnapCam(new Vector3(target.x, target.y, fromPos.z));
            float toSize = fromSize / Mathf.Max(1f, zoomFactor);
            Log("kamera yakınlaşıyor");
            for (float t = 0f; t < zoomSeconds; t += EmergencyBagFx.Dt)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / zoomSeconds);
                SetCamera(Vector3.Lerp(fromPos, toPos, k), Mathf.Lerp(fromSize, toSize, k));
                EqUi.SetDim(dim, k * dimAlpha / EqUi.DimAlpha);
                if (t > zoomSeconds * 0.45f) rootGroup.alpha = EqUi.OutCubic((t - zoomSeconds * 0.45f) / (zoomSeconds * 0.55f));
                yield return null;
            }
            SetCamera(toPos, toSize);
            EqUi.SetDim(dim, dimAlpha / EqUi.DimAlpha);
            rootGroup.alpha = 1f;

            // Çanta öne gelir, fermuar açılır
            state = BagState.BackpackOpening;
            for (float t = 0f; t < 0.22f; t += EmergencyBagFx.Dt)
            {
                float k = EqUi.OutBack(t / 0.22f, 1.6f);
                float s = Mathf.LerpUnclamped(0.9f, 1f, k);
                bagScale.localScale = new Vector3(s, s, 1f);
                bagScale.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-10f, 0f, k));
                yield return null;
            }
            bagScale.localScale = Vector3.one;
            bagScale.anchoredPosition = Vector2.zero;
            Play(zipOpenClip);
            SetZipperVisible(true);
            zipper.Interactable = false;
            for (float t = 0f; t < 0.32f; t += EmergencyBagFx.Dt)
            {
                float p = 1f - EqUi.OutCubic(t / 0.32f);
                zipper.SetProgress(p, false);
                SetLid(p);
                yield return null;
            }
            zipper.SetProgress(0f, false);
            SetLid(0f);
            SetZipperVisible(false);
            StartCoroutine(EmergencyBagFx.Bounce(bagScale, 1.04f, 0.18f));
            rootGroup.blocksRaycasts = true;

            state = BagState.Packing;
            Log("paketleme başladı");
            if (!introShown)
            {
                introShown = true;
                ShowFeedback(Tone.Info, speakerName, introLine, null, false, portrait: true);
                introUntil = Time.unscaledTime + IntroSeconds;
            }
            else ShowFeedback(Tone.Info, "", idleHint, null, false);
            UpdatePackingState();
            flowRoutine = null;
        }

        private IEnumerator CloseFlow(bool success)
        {
            state = BagState.CameraReturn;
            rootGroup.blocksRaycasts = false;
            canvasGroup.blocksRaycasts = false;
            yield return EmergencyBagFx.FadeGroup(rootGroup, rootGroup.alpha, 0f, 0.2f);

            Vector3 fromPos = cam != null ? cam.transform.position : savedCamPos;
            float fromSize = cam != null ? cam.orthographicSize : savedCamSize;
            float seconds = success ? returnSeconds : Mathf.Min(returnSeconds, 0.35f);
            float dim0 = dimAlpha / EqUi.DimAlpha;
            for (float t = 0f; t < seconds; t += EmergencyBagFx.Dt)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                if (cameraSaved) SetCamera(Vector3.Lerp(fromPos, savedCamPos, k), Mathf.Lerp(fromSize, savedCamSize, k));
                EqUi.SetDim(dim, dim0 * (1f - k));
                yield return null;
            }
            RestoreCamera();
            HideUiImmediate();
            flowRoutine = null;
            Finish(restorePlayer: true);
        }

        private void Finish(bool restorePlayer)
        {
            state = BagState.Idle;
            if (restorePlayer && player != null) player.CanMove = playerCouldMove;
            if (hubOpen)
            {
                hubOpen = false;
                EarthquakeMinigameHub.InterruptRequested -= Interrupt;
                EarthquakeMinigameHub.NotifyClosed(MinigameId);
            }
            Log($"kapandı (tamamlandı: {completed})");
            var cb = onClosed;
            onClosed = null;
            cb?.Invoke(completed);
            Closed?.Invoke(completed);
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Sayaç bittiyse (hub kesmesi gelmese bile) mini oyun depremi asla bekletmez.
            if (countdown != null && countdown.HasExpired)
            {
                Interrupt();
                return;
            }

            if (IsCancellable() && GameInput.BackPressed)
            {
                Exit();
                return;
            }

            if (introUntil > 0f && (Time.unscaledTime >= introUntil || GameInput.ContinuePressed))
            {
                introUntil = -1f;
                if (selectedSlot == EmergencyBagInventory.NoSlot && (state == BagState.Packing || state == BagState.ReadyToClose))
                    ShowFeedback(Tone.Info, "", state == BagState.ReadyToClose ? zipHint : idleHint, null, false);
            }

            if (zipArrow != null && zipArrow.gameObject.activeSelf)
            {
                float k = Mathf.Repeat(Time.unscaledTime * 1.6f, 1f);
                zipArrow.anchoredPosition = zipArrowBase + new Vector2(Mathf.Round(k * 3f) * 4f, 0f);
            }

            if (dragGhost != null && draggingCard != null && !draggingCard.Dragging && !dropHandled)
                ReturnGhost(draggingCard); // sürükleme olayı kaybolduysa (odak vb.)
        }

        private bool IsCancellable() =>
            state == BagState.CameraZoom || state == BagState.BackpackOpening || state == BagState.Packing
            || state == BagState.ReadyToClose || state == BagState.ZipperClosing;

        private void StopFlow()
        {
            if (flowRoutine != null) StopCoroutine(flowRoutine);
            flowRoutine = null;
        }

        private void CancelInteractions()
        {
            introUntil = -1f;
            if (draggingCard != null) draggingCard.CancelDrag();
            draggingCard = null;
            if (dragGhost != null) Destroy(dragGhost.gameObject);
            dragGhost = null;
            if (zipper != null)
            {
                zipper.Cancel();
                zipper.Interactable = false;
            }
            if (missingRoutine != null) StopCoroutine(missingRoutine);
            missingRoutine = null;
            foreach (var c in cards)
            {
                c.rt.localScale = Vector3.one;
                SetCardVisual(c);
            }
            if (state == BagState.ZipperClosing) state = BagState.ReadyToClose;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnDisable()
        {
            if (IsOpen) Interrupt();
        }

        // =====================================================================
        // Sürükle-bırak (EmergencyItemDragHandler / EmergencyBagDropZone çağırır)
        // =====================================================================

        internal void OnCardHover(EmergencyItemDragHandler h, bool on)
        {
            var c = Find(h);
            if (c == null || c.packed || draggingCard != null) return;
            c.rt.localScale = on && CanDrag() ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
            c.frame.color = on && CanDrag() ? EqUi.A(EqUi.Gold, 0.9f) : EqUi.A(EqUi.Gold, 0f);
        }

        internal void OnCardPress(EmergencyItemDragHandler h, bool down)
        {
            var c = Find(h);
            if (c == null || c.packed || !CanDrag()) return;
            c.rt.localScale = down ? new Vector3(1.12f, 1.12f, 1f) : (h.Hovered ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one);
        }

        internal bool BeginCardDrag(EmergencyItemDragHandler h, PointerEventData e)
        {
            var c = Find(h);
            if (c == null || c.packed || !CanDrag() || draggingCard != null) return false;
            if (state == BagState.ReadyToClose && zipper.Progress > 0.02f)
            {
                ShowFeedback(Tone.Warn, "", zipBlockedText, null, false);
                return false;
            }
            introUntil = -1f;
            draggingCard = h;
            dropHandled = false;
            c.rt.localScale = Vector3.one;
            c.frame.color = EqUi.A(EqUi.Gold, 0f);
            c.icon.color = EqUi.A(Color.white, 0.3f);

            var img = EqUi.Img("DragGhost", dragLayer, c.item.Icon, EqUi.Cream, null);
            img.raycastTarget = false;
            img.color = EqUi.A(Color.white, 0.82f);
            dragGhost = img.rectTransform;
            dragGhost.anchorMin = dragGhost.anchorMax = new Vector2(0.5f, 0.5f);
            dragGhost.pivot = new Vector2(0.5f, 0.5f);
            dragGhost.sizeDelta = IconSize;
            dragGhost.localScale = new Vector3(1.15f, 1.15f, 1f);
            dragGhost.anchoredPosition = ScreenToLayer(e.position, e.pressEventCamera);
            Play(pickupClip);
            Log($"sürükleniyor: {c.item.Id}");
            return true;
        }

        internal void DragCard(EmergencyItemDragHandler h, PointerEventData e)
        {
            if (h != draggingCard || dragGhost == null) return;
            dragGhost.anchoredPosition = EqUi.Snap(ScreenToLayer(e.position, e.pressEventCamera));
        }

        internal void EndCardDrag(EmergencyItemDragHandler h, PointerEventData e)
        {
            if (h != draggingCard) return;
            SetGlow(0f, EqUi.Gold);
            if (!dropHandled) ReturnGhost(h);
            draggingCard = null;
        }

        internal void OnBagHover(bool on)
        {
            if (draggingCard == null) return;
            SetGlow(on ? 0.85f : 0f, EqUi.Gold);
        }

        internal void DropOnBag(PointerEventData e)
        {
            if (draggingCard == null || dragGhost == null || !CanDrag()) return;
            var c = Find(draggingCard);
            if (c == null) return;
            var result = inventory.TryAdd(c.item, out int index);
            switch (result)
            {
                case EmergencyBagInventory.AddResult.Duplicate:
                    ShowFeedback(Tone.Info, c.item.FullName, "Bu eşya zaten çantada.", c.item.Icon, false);
                    return; // EndDrag geri gönderir

                case EmergencyBagInventory.AddResult.Full:
                    Log($"çanta dolu, {c.item.Id} sığmadı");
                    Play(warningClip);
                    ShowFullWarning();
                    return;
            }

            dropHandled = true;
            c.packed = true;
            SetCardVisual(c);
            var slot = SlotOf(index);
            var ghost = dragGhost;
            dragGhost = null;
            Play(dropClip);
            Log($"çantaya kondu: {c.item.Id} → {(index == EmergencyBagInventory.PocketIndex ? "ön cep" : "yuva " + index)} " +
                $"(gerekli {RequiredPacked}/{required.Count}, dolu {inventory.FilledCount}/{inventory.Capacity})");

            Vector2 to = ScreenToLayer(RectTransformUtility.WorldToScreenPoint(null, slot.icon.rectTransform.TransformPoint(slot.icon.rectTransform.rect.center)), null);
            StartCoroutine(EmergencyBagFx.Fly(ghost, to, 0.14f, () =>
            {
                if (ghost != null) Destroy(ghost.gameObject);
                PlaceInSlot(slot, c.item);
            }, 1f));
            SelectSlot(EmergencyBagInventory.NoSlot, false);
            switch (c.item.Category)
            {
                case EmergencyItemData.ItemCategory.Required:
                    Play(correctClip);
                    ShowFeedback(Tone.Good, "✓ " + c.item.FullName, Or(c.item.Feedback, "Gerekli bir eşya."), c.item.Icon, false);
                    break;
                case EmergencyItemData.ItemCategory.Comfort:
                    ShowFeedback(Tone.Comfort, c.item.FullName, Or(c.item.Feedback, comfortDefault), c.item.Icon,
                        index != EmergencyBagInventory.PocketIndex);
                    if (index != EmergencyBagInventory.PocketIndex) selectedSlot = index;
                    break;
                default:
                    Play(warningClip);
                    selectedSlot = index;
                    ShowFeedback(Tone.Warn, c.item.FullName, Or(c.item.Feedback, unnecessaryDefault), c.item.Icon, true);
                    break;
            }
            RefreshCollection();
            UpdatePackingState();
        }

        private void PlaceInSlot(Slot slot, EmergencyItemData item)
        {
            if (slot == null) return;
            if (inventory.Get(slot.index) != item) return; // bu arada çıkarıldıysa
            slot.icon.sprite = item.Icon;
            slot.icon.enabled = true;
            slot.icon.color = Color.white;
            StartCoroutine(EmergencyBagFx.Pop(slot.icon.rectTransform, 1.35f, 0.22f));
            Vector2 c = SlotCenterInFx(slot);
            if (item.IsRequired)
            {
                StartCoroutine(EmergencyBagFx.Sparkles(fxLayer, c, sparkle, EqUi.Gold, 6, 70f, 0.4f));
                StartCoroutine(EmergencyBagFx.Pulse(slot.frame, EqUi.A(EqUi.Green, 0.95f), EqUi.A(EqUi.Green, 0f), 1, 0.35f));
            }
            else if (item.Category == EmergencyItemData.ItemCategory.Unnecessary)
                StartCoroutine(EmergencyBagFx.Pulse(slot.frame, EqUi.A(EqUi.Amber, 1f), selectedSlot == slot.index ? EqUi.A(EqUi.Gold, 1f) : EqUi.A(EqUi.Amber, 0f), 2, 0.5f));
            else
                StartCoroutine(EmergencyBagFx.Sparkles(fxLayer, c, sparkle, EqUi.Blue, 4, 50f, 0.35f));
            StartCoroutine(EmergencyBagFx.Bounce(bagScale, 1.025f, 0.16f));
            RefreshSlotFrames();
        }

        private void ReturnGhost(EmergencyItemDragHandler h)
        {
            var c = Find(h);
            var ghost = dragGhost;
            dragGhost = null;
            if (draggingCard == h) draggingCard = null;
            if (ghost == null || c == null)
            {
                if (ghost != null) Destroy(ghost.gameObject);
                SetCardVisual(c);
                return;
            }
            Vector2 to = ScreenToLayer(RectTransformUtility.WorldToScreenPoint(null, c.icon.rectTransform.TransformPoint(c.icon.rectTransform.rect.center)), null);
            StartCoroutine(EmergencyBagFx.Fly(ghost, to, 0.18f, () =>
            {
                if (ghost != null) Destroy(ghost.gameObject);
                SetCardVisual(c);
            }, 1f));
        }

        private bool CanDrag() => state == BagState.Packing || state == BagState.ReadyToClose;

        // =====================================================================
        // Yuva seçimi + Çıkar
        // =====================================================================

        internal void OnSlotClicked(int index)
        {
            if (!CanDrag()) return;
            var item = inventory.Get(index);
            if (item == null) return;
            if (zipper.Progress > 0.02f)
            {
                ShowFeedback(Tone.Warn, "", zipBlockedText, null, false);
                return;
            }
            introUntil = -1f;
            SelectSlot(selectedSlot == index ? EmergencyBagInventory.NoSlot : index, true);
        }

        private void SelectSlot(int index, bool showFeedback)
        {
            selectedSlot = index;
            RefreshSlotFrames();
            if (!showFeedback) return;
            var item = inventory.Get(index);
            if (item == null)
            {
                ShowFeedback(Tone.Info, "", state == BagState.ReadyToClose ? zipHint : idleHint, null, false);
                return;
            }
            Tone tone = item.IsRequired ? Tone.Good : item.Category == EmergencyItemData.ItemCategory.Comfort ? Tone.Comfort : Tone.Warn;
            string text = item.IsRequired ? Or(item.Feedback, "Gerekli bir eşya.")
                : item.Category == EmergencyItemData.ItemCategory.Comfort ? Or(item.Feedback, comfortDefault)
                : Or(item.Feedback, unnecessaryDefault);
            ShowFeedback(tone, item.FullName, text, item.Icon, true);
        }

        private void RemoveSelected()
        {
            if (!CanDrag() || selectedSlot == EmergencyBagInventory.NoSlot) return;
            if (zipper.Progress > 0.02f) return;
            int index = selectedSlot;
            var item = inventory.RemoveAt(index);
            if (item == null) return;
            var slot = SlotOf(index);
            var card = cards.Find(x => x.item == item);
            Play(removeClip);
            Log($"çıkarıldı: {item.Id} (gerekli {RequiredPacked}/{required.Count}, dolu {inventory.FilledCount}/{inventory.Capacity})");

            // Kısa çıkarma animasyonu: yuvadaki ikonun kopyası masadaki yerine uçar.
            if (slot != null && card != null)
            {
                var img = EqUi.Img("RemoveGhost", dragLayer, item.Icon, EqUi.Cream, null);
                img.raycastTarget = false;
                var g = img.rectTransform;
                g.anchorMin = g.anchorMax = new Vector2(0.5f, 0.5f);
                g.pivot = new Vector2(0.5f, 0.5f);
                g.sizeDelta = IconSize;
                g.anchoredPosition = ScreenToLayer(RectTransformUtility.WorldToScreenPoint(null, slot.icon.rectTransform.TransformPoint(slot.icon.rectTransform.rect.center)), null);
                Vector2 to = ScreenToLayer(RectTransformUtility.WorldToScreenPoint(null, card.icon.rectTransform.TransformPoint(card.icon.rectTransform.rect.center)), null);
                card.packed = false;
                card.icon.color = EqUi.A(Color.white, 0f);
                card.check.gameObject.SetActive(false);
                StartCoroutine(EmergencyBagFx.Fly(g, to, 0.2f, () =>
                {
                    if (g != null) Destroy(g.gameObject);
                    SetCardVisual(card);
                    StartCoroutine(EmergencyBagFx.Pop(card.rt, 1.15f, 0.18f));
                }, 1f));
            }
            else if (card != null)
            {
                card.packed = false;
                SetCardVisual(card);
            }
            if (slot != null)
            {
                slot.icon.enabled = false;
                slot.icon.sprite = null;
            }
            selectedSlot = EmergencyBagInventory.NoSlot;
            RefreshSlotFrames();
            RefreshCollection();
            ShowFeedback(Tone.Info, "", Or(null, idleHint), null, false);
            UpdatePackingState();
        }

        private void ShowFullWarning()
        {
            ShowFeedback(Tone.Warn, "", fullWarning, null, false);
            int extra = inventory.FirstNonRequiredSlot();
            if (extra != EmergencyBagInventory.NoSlot)
            {
                selectedSlot = extra;
                RefreshSlotFrames();
                var item = inventory.Get(extra);
                ShowFeedback(Tone.Warn, fullWarning, item != null ? $"{item.FullName} çıkarılabilir: {Or(item.Feedback, unnecessaryDefault)}" : "", item != null ? item.Icon : null, true);
            }
            if (missingRoutine != null) StopCoroutine(missingRoutine);
            missingRoutine = StartCoroutine(HighlightMissing());
            StartCoroutine(EmergencyBagFx.Pulse(bagGlowImage, EqUi.A(EqUi.Amber, 0.9f), EqUi.A(EqUi.Amber, 0f), 2, 0.6f));
        }

        private IEnumerator HighlightMissing()
        {
            var missing = cards.FindAll(c => c.item.IsRequired && !c.packed);
            for (float t = 0f; t < 2.6f; t += EmergencyBagFx.Dt)
            {
                float a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 6f));
                foreach (var c in missing) c.frame.color = EqUi.A(EqUi.Gold, a);
                yield return null;
            }
            foreach (var c in missing) c.frame.color = EqUi.A(EqUi.Gold, 0f);
            missingRoutine = null;
        }

        // =====================================================================
        // Paketleme durumu + fermuar
        // =====================================================================

        private void UpdatePackingState()
        {
            bool all = inventory.HasAll(required);
            if (all && state == BagState.Packing)
            {
                state = BagState.ReadyToClose;
                Log("bütün gerekli eşyalar çantada → fermuarı kapat");
                SetInstruction(closePrompt);
                SetZipperVisible(true);
                zipper.Interactable = true;
                zipper.SetProgress(0f, false);
                lastTickProgress = 0f;
                SetLid(0f);
                StartCoroutine(EmergencyBagFx.Pop(zipHandleRect, 1.4f, 0.25f));
                if (selectedSlot == EmergencyBagInventory.NoSlot && introUntil < 0f)
                    StartCoroutine(ShowZipHintLater());
            }
            else if (!all && (state == BagState.ReadyToClose || state == BagState.ZipperClosing))
            {
                state = BagState.Packing;
                Log("gerekli bir eşya çıkarıldı → paketlemeye dönüldü");
                SetInstruction(instruction);
                zipper.Cancel();
                zipper.Interactable = false;
                zipper.SetProgress(0f, false);
                SetLid(0f);
                SetZipperVisible(false);
            }
        }

        private IEnumerator ShowZipHintLater()
        {
            yield return new WaitForSecondsRealtime(0.9f);
            if (state == BagState.ReadyToClose && selectedSlot == EmergencyBagInventory.NoSlot && introUntil < 0f)
                ShowFeedback(Tone.Good, closePrompt, zipHint, null, false);
        }

        private void OnZipGrabbed()
        {
            if (state != BagState.ReadyToClose) return;
            state = BagState.ZipperClosing;
            introUntil = -1f;
            SelectSlot(EmergencyBagInventory.NoSlot, false);
            zipArrow.gameObject.SetActive(false);
        }

        private void OnZipProgress(float p)
        {
            if (state != BagState.ZipperClosing && state != BagState.ReadyToClose) return;
            SetLid(p);
            if (Mathf.Abs(p - lastTickProgress) >= 0.07f)
            {
                lastTickProgress = p;
                Play(zipTickClip, 0.6f);
            }
            float squash = 1f - 0.03f * p;
            bagScale.localScale = new Vector3(1f, squash, 1f);
        }

        private void OnZipReleased(float p)
        {
            if (state != BagState.ZipperClosing) return;
            if (p >= 0.95f && inventory.HasAll(required))
            {
                StopFlow();
                flowRoutine = StartCoroutine(SuccessFlow());
                return;
            }
            state = BagState.ReadyToClose;
            bagScale.localScale = Vector3.one;
            zipArrow.gameObject.SetActive(p < 0.05f);
            Log($"fermuar bırakıldı (%{p * 100f:0}) — ilerleme korunuyor");
        }

        private IEnumerator SuccessFlow()
        {
            state = BagState.Success;
            zipper.Interactable = false;
            zipper.SetProgress(1f, false);
            SetLid(1f);
            completed = true;
            Log("FERMUAR KAPANDI → afet çantası hazır (görev: emergency_bag)");
            EarthquakeMinigameHub.CompleteTask(TaskId);
            Completed?.Invoke();
            SetInstruction("");
            Play(zipCloseClip);

            yield return EmergencyBagFx.Bounce(bagScale, 1.1f, 0.3f);
            Play(successClip);
            SetGlow(1f, EqUi.Green);
            StartCoroutine(EmergencyBagFx.Pulse(bagGlowImage, EqUi.A(EqUi.Green, 1f), EqUi.A(EqUi.Green, 0.55f), 2, 0.8f));
            Vector2 center = new Vector2(BagRect.width * 0.5f, -BagRect.height * 0.42f);
            StartCoroutine(EmergencyBagFx.Sparkles(fxLayer, center, sparkle, EqUi.Gold, 10, 120f, 0.7f));
            StartCoroutine(EmergencyBagFx.Sparkles(fxLayer, center, sparkle, EqUi.Green, 8, 90f, 0.6f));
            checkImage.gameObject.SetActive(true);
            StartCoroutine(EmergencyBagFx.Pop(checkImage.rectTransform, 0.2f, 0.3f));

            successBanner.gameObject.SetActive(true);
            successBanner.localScale = new Vector3(0.85f, 0.85f, 1f);
            StartCoroutine(EmergencyBagFx.Pop(successBanner, 0.85f, 0.25f));
            ShowFeedback(Tone.Good, successTitle, inventory.Pocket != null ? successBody + " Oyuncak ayın da ön cepte." : successBody, null, false);
            yield return EmergencyBagFx.FadeGroup(successGroup, 0f, 1f, 0.2f);

            // ~1 sn başarı geri bildirimi (tıklayınca erken geçer)
            float shown = 0f;
            while (shown < 1.25f)
            {
                shown += EmergencyBagFx.Dt;
                if (shown > 0.45f && GameInput.ContinuePressed) break;
                yield return null;
            }
            yield return CloseFlow(true);
        }

        // =====================================================================
        // Kamera
        // =====================================================================

        private void SaveCamera()
        {
            cam = Camera.main;
            cameraSaved = cam != null;
            if (!cameraSaved) return;
            savedCamPos = cam.transform.position;
            savedCamSize = cam.orthographicSize;
            pixelPerfect = cam.GetComponent("PixelPerfectCamera") as Behaviour;
            chapterCamera = cam.GetComponent<ChapterCamera>();
            pixelPerfectWasOn = pixelPerfect != null && pixelPerfect.enabled;
            chapterCameraWasOn = chapterCamera != null && chapterCamera.enabled;
            if (chapterCamera != null) chapterCamera.enabled = false;
            if (pixelPerfect != null) pixelPerfect.enabled = false;
            cam.orthographicSize = savedCamSize;
        }

        private void SetCamera(Vector3 pos, float size)
        {
            if (cam == null) return;
            cam.transform.position = SnapCam(pos);
            cam.orthographicSize = size;
        }

        private void RestoreCamera()
        {
            if (!cameraSaved) return;
            cameraSaved = false;
            if (cam != null)
            {
                cam.transform.position = savedCamPos;
                cam.orthographicSize = savedCamSize;
            }
            if (pixelPerfect != null && pixelPerfectWasOn) pixelPerfect.enabled = true;
            if (chapterCamera != null && chapterCameraWasOn) chapterCamera.enabled = true;
            Log("kamera eski hâline döndü");
        }

        private static Vector3 SnapCam(Vector3 p) => new Vector3(Mathf.Round(p.x * 128f) / 128f, Mathf.Round(p.y * 128f) / 128f, p.z);

        // =====================================================================
        // Görünüm güncelleme
        // =====================================================================

        private Vector2 IconSize => new Vector2(20f * artScale, 20f * artScale);

        private void RefreshAll()
        {
            foreach (var c in cards)
            {
                c.packed = inventory.Contains(c.item);
                c.rt.localScale = Vector3.one;
                SetCardVisual(c);
            }
            foreach (var s in slots) SetSlotIcon(s);
            if (pocketSlot != null) SetSlotIcon(pocketSlot);
            selectedSlot = EmergencyBagInventory.NoSlot;
            RefreshSlotFrames();
            RefreshCollection();
        }

        private void SetSlotIcon(Slot s)
        {
            var item = inventory.Get(s.index);
            s.icon.enabled = item != null;
            s.icon.sprite = item != null ? item.Icon : null;
            s.icon.color = Color.white;
            s.icon.rectTransform.localScale = Vector3.one;
        }

        private void SetCardVisual(Card c)
        {
            if (c == null) return;
            bool dragging = draggingCard != null && draggingCard == c.handler;
            c.icon.color = EqUi.A(Color.white, c.packed ? 0.28f : dragging ? 0.3f : 1f);
            c.check.gameObject.SetActive(c.packed);
            c.label.color = c.packed ? EqUi.A(EqUi.Cream, 0.5f) : EqUi.Cream;
            c.frame.color = EqUi.A(EqUi.Gold, 0f);
        }

        private void RefreshSlotFrames()
        {
            foreach (var s in slots) s.frame.color = s.index == selectedSlot ? EqUi.A(EqUi.Gold, 1f) : EqUi.A(EqUi.Gold, 0f);
            if (pocketSlot != null) pocketSlot.frame.color = selectedSlot == EmergencyBagInventory.PocketIndex ? EqUi.A(EqUi.Gold, 1f) : EqUi.A(EqUi.Gold, 0f);
        }

        private void RefreshCollection()
        {
            int n = 0;
            for (int i = 0; i < collectBoxes.Count; i++)
            {
                var item = inventory.Get(i);
                var icon = collectBoxes[i].transform.GetChild(collectBoxes[i].transform.childCount - 1).GetComponent<Image>();
                icon.enabled = item != null;
                icon.sprite = item != null ? item.Icon : null;
                EqUi.FallbackColors(collectBoxes[i], EqUi.Ink, item == null ? EqUi.NavyDark : item.IsRequired ? EqUi.GreenDark : EqUi.AmberDark);
                if (item != null) n++;
            }
            if (collectPocketBox != null)
            {
                var icon = collectPocketBox.transform.GetChild(collectPocketBox.transform.childCount - 1).GetComponent<Image>();
                icon.enabled = inventory.Pocket != null;
                icon.sprite = inventory.Pocket != null ? inventory.Pocket.Icon : null;
            }
            collectCount.text = $"{n}/{inventory.Capacity}";
            collectCount.color = n >= inventory.Capacity ? EqUi.Amber : EqUi.Cream;
        }

        private void SetLid(float p)
        {
            p = Mathf.Clamp01(p);
            float x = Mathf.Lerp(ArtLidX0, ArtLidX1, p);
            lidImage.fillAmount = p <= 0.001f ? 0f : x / ArtBagW;
        }

        private void SetZipperVisible(bool on)
        {
            zipHandleRect.gameObject.SetActive(on);
            zipArrow.gameObject.SetActive(on && state == BagState.ReadyToClose && zipper.Progress < 0.05f);
        }

        private void SetGlow(float alpha, Color color)
        {
            if (bagGlowImage == null) return;
            bagGlowImage.color = EqUi.A(color, alpha);
        }

        private void SetInstruction(string text)
        {
            if (instructionText != null) instructionText.text = text ?? "";
        }

        private void ShowFeedback(Tone tone, string header, string body, Sprite icon, bool showRemove, bool portrait = false)
        {
            if (feedbackBody == null) return;
            bool hasHeader = !string.IsNullOrEmpty(header);
            portraitRoot.gameObject.SetActive(portrait);
            feedbackIcon.gameObject.SetActive(!portrait && icon != null);
            if (!portrait && icon != null) feedbackIcon.sprite = icon;
            float left = portrait || icon != null ? 62f : 10f;
            feedbackName.rectTransform.offsetMin = new Vector2(left, feedbackName.rectTransform.offsetMin.y);
            feedbackBody.rectTransform.offsetMin = new Vector2(left, feedbackBody.rectTransform.offsetMin.y);
            feedbackBody.rectTransform.offsetMax = new Vector2(showRemove ? -78f : -10f, hasHeader ? -20f : -8f);
            feedbackName.gameObject.SetActive(hasHeader);
            feedbackName.text = header ?? "";
            switch (tone)
            {
                case Tone.Good: feedbackName.color = EqUi.Green; break;
                case Tone.Warn: feedbackName.color = EqUi.Amber; break;
                case Tone.Comfort: feedbackName.color = EqUi.Gold; break;
                default: feedbackName.color = EqUi.Gold; break;
            }
            feedbackBody.text = body ?? "";
            feedbackBody.color = feedbackOnLight
                ? (tone == Tone.Warn && !hasHeader ? EqUi.AmberDark : EqUi.Ink)
                : (tone == Tone.Warn && !hasHeader ? EqUi.Amber : EqUi.Cream);
            removeButton.gameObject.SetActive(showRemove);
            removeButton.Interactable = showRemove;
            if (showRemove) StartCoroutine(EmergencyBagFx.Pop(removeButton.transform, 1.25f, 0.2f));
            if (tone == Tone.Warn && feedbackPanel != null)
                StartCoroutine(EmergencyBagFx.Pop(feedbackPanel.rectTransform, 1.03f, 0.18f));
        }

        // =====================================================================
        // Yardımcılar
        // =====================================================================

        private Card Find(EmergencyItemDragHandler h) => h == null ? null : cards.Find(c => c.handler == h);

        private Slot SlotOf(int index) => index == EmergencyBagInventory.PocketIndex ? pocketSlot : slots.Find(s => s.index == index);

        private Vector2 SlotCenterInFx(Slot s)
        {
            Vector3 world = s.rt.TransformPoint(s.rt.rect.center);
            Vector2 local = fxLayer.InverseTransformPoint(world);
            // fxLayer pivotu sol-üst; Sparkles sol-üst çapalı parçalar kullanır
            return local;
        }

        private Vector2 ScreenToLayer(Vector2 screen, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, screen, null, out var local);
            return local;
        }

        private static string Or(string a, string b) => string.IsNullOrEmpty(a) ? b : a;

        private void Play(AudioClip clip, float scale = 1f)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, volume * scale);
        }

        private void Log(string message)
        {
            if (!logSteps) return;
            float remaining = countdown != null ? countdown.GetRemainingTime() : -1f;
            Debug.Log($"[BAG] t={Time.timeSinceLevelLoad:0.0} sayaç={remaining:0.0} {state} → {message}", this);
        }

        private void HideUiImmediate()
        {
            if (!built) return;
            foreach (Transform child in dragLayer) Destroy(child.gameObject);
            foreach (Transform child in fxLayer) if (child.name == "Spark") Destroy(child.gameObject);
            if (checkImage != null) checkImage.gameObject.SetActive(false);
            EqUi.SetDim(dim, 0f);
            rootGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvas.gameObject.SetActive(false);
        }

        // ---------------- Test yardımcıları (ekran noktaları) ----------------

        public Vector2 ScreenPointOfCard(string itemId)
        {
            var c = cards.Find(x => x.item != null && x.item.Id == itemId);
            return c != null ? ScreenCenter(c.icon.rectTransform) : Vector2.zero;
        }

        public Vector2 ScreenPointOfSlot(int index)
        {
            var s = SlotOf(index);
            return s != null ? ScreenCenter(s.rt) : Vector2.zero;
        }

        public Vector2 ScreenPointOfBag() => bagBodyImage != null ? ScreenCenter(slots.Count > 4 ? slots[4].rt : bagBodyImage.rectTransform) : Vector2.zero;
        public Vector2 ScreenPointOfRemove() => removeButton != null ? ScreenCenter((RectTransform)removeButton.transform) : Vector2.zero;
        public Vector2 ScreenPointOfZipHandle() => zipHandleImage != null ? ScreenCenter(zipHandleImage.rectTransform) : Vector2.zero;
        public Vector2 ScreenPointOfZipEnd() =>
            zipTrack != null ? RectTransformUtility.WorldToScreenPoint(null, zipTrack.TransformPoint(zipper.PointAt(1f) + new Vector2(6f, -6f))) : Vector2.zero;
        public Vector2 ScreenPointOfTableEmpty() => table != null ? RectTransformUtility.WorldToScreenPoint(null, table.TransformPoint(new Vector3(TableRect.width * 0.5f, -TableRect.height + 8f, 0f))) : Vector2.zero;
        public bool RemoveVisible => removeButton != null && removeButton.gameObject.activeInHierarchy;
        public int SelectedSlot => selectedSlot;

        private static Vector2 ScreenCenter(RectTransform rt) => RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));

        // =====================================================================
        // Arayüz kurulumu (ilk açılışta bir kez)
        // =====================================================================

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_BagCanvas", sortingOrder, out canvasGroup);
            int s = artScale;

            dim = EqUi.DimImage(canvas.transform, skin);
            root = EqUi.Rect("Root", canvas.transform);
            EqUi.Stretch(root);
            rootGroup = root.gameObject.AddComponent<CanvasGroup>();

            // --- Başlık ---
            var titleText = EqUi.Txt("Title", root, font, 12, EqUi.Gold, TextAnchor.UpperLeft, EqUi.Ink);
            EqUi.TL(titleText.rectTransform, 16f, 10f, 300f, 16f);
            titleText.text = title;
            instructionText = EqUi.Txt("Instruction", root, font, 9, EqUi.Cream, TextAnchor.UpperLeft, EqUi.Ink);
            EqUi.TL(instructionText.rectTransform, 16f, 27f, 340f, 14f);
            var esc = EqUi.Rect("EscHint", root);
            EqUi.TL(esc, 440f, 12f, 70f, 14f);
            var key = EqUi.KeyCap(esc, "Esc", font, skin, 7);
            EqUi.TL(key, 0f, 1f, 24f, 12f);
            var escText = EqUi.Txt("Label", esc, font, 9, EqUi.Cream, TextAnchor.UpperLeft, EqUi.Ink);
            EqUi.TL(escText.rectTransform, 28f, 0f, 40f, 14f);
            escText.text = escLabel;

            // --- Masa + eşya kartları ---
            var tableImg = EqUi.Img("Table", root, tableSprite, EqUi.Hex(0x8C5A35), skin);
            tableImg.preserveAspect = false;
            table = tableImg.rectTransform;
            EqUi.TL(table, TableRect.x, TableRect.y, TableRect.width, TableRect.height);
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null) continue;
                int col = cards.Count % 4, row = cards.Count / 4;
                cards.Add(BuildCard(item, cards.Count, CellX0 + col * CellW, CellY0 + row * CellH));
            }

            // --- Geri bildirim paneli ---
            feedbackPanel = EqUi.Img("Feedback", root, EqUi.S(skin, k => k.InfoCard), EqUi.Navy, skin);
            EqUi.TL(feedbackPanel.rectTransform, FeedbackRect.x, FeedbackRect.y, FeedbackRect.width, FeedbackRect.height);
            if (feedbackPanel.sprite == null) EqUi.Fallback(feedbackPanel, EqUi.Ink, EqUi.Navy, skin);
            feedbackPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            feedbackPanel.rectTransform.anchoredPosition += new Vector2(FeedbackRect.width * 0.5f, -FeedbackRect.height * 0.5f);
            var fp = feedbackPanel.rectTransform;
            var frameImg = EqUi.Img("PortraitFrame", fp, EqUi.S(skin, k => k.PortraitFrame), EqUi.Gold, skin);
            portraitRoot = frameImg.rectTransform;
            EqUi.TL(portraitRoot, 7f, 7f, 50f, 50f);
            if (frameImg.sprite == null) EqUi.Fallback(frameImg, EqUi.Gold, EqUi.NavyLight, skin);
            portraitImage = EqUi.Img("Portrait", portraitRoot, cenkPortrait, EqUi.NavyLight, skin);
            EqUi.Stretch(portraitImage.rectTransform, 3f);
            portraitImage.preserveAspect = true;
            feedbackIcon = EqUi.Img("ItemIcon", fp, null, EqUi.Cream, skin);
            EqUi.TL(feedbackIcon.rectTransform, 12f, 12f, IconSize.x, IconSize.y);
            feedbackName = EqUi.Txt("Name", fp, font, 9, EqUi.Gold, TextAnchor.UpperLeft, EqUi.Ink);
            EqUi.StretchOffsets(feedbackName.rectTransform, 62f, FeedbackRect.height - 20f, 10f, 7f);
            feedbackName.horizontalOverflow = HorizontalWrapMode.Overflow;
            feedbackOnLight = feedbackPanel.sprite != null; // UI_EQ_InfoCard: lacivert bant + krem gövde
            feedbackBody = feedbackOnLight
                ? EqUi.Txt("Body", fp, font, 9, EqUi.Ink, TextAnchor.UpperLeft)
                : EqUi.Txt("Body", fp, font, 9, EqUi.Cream, TextAnchor.UpperLeft, EqUi.Ink);
            EqUi.StretchOffsets(feedbackBody.rectTransform, 62f, 6f, 10f, 20f);
            EqUi.BestFit(feedbackBody, 7);

            var rb = EqUi.Img("RemoveButton", fp, EqUi.S(skin, k => k.Button), EqUi.RedDark, skin, true);
            if (rb.sprite == null) EqUi.Fallback(rb, EqUi.Ink, EqUi.RedDark, skin);
            else rb.color = EqUi.Hex(0xF2C6A0);
            var rbt = rb.rectTransform;
            EqUi.Place(rbt, 1f, 0f, new Vector2(-8f, 8f), new Vector2(62f, 20f));
            rbt.pivot = new Vector2(0.5f, 0.5f);
            rbt.anchoredPosition = new Vector2(-8f - 31f, 8f + 10f);
            var rbText = EqUi.Txt("Label", rbt, font, 9, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.StretchOffsets(rbText.rectTransform, 0f, 2f, 0f, 0f);
            rbText.text = removeLabel;
            if (rb.sprite != null) rbText.color = EqUi.Cream;
            removeButton = rb.gameObject.AddComponent<EqButton>();
            removeButton.Init();
            removeButton.Clicked += RemoveSelected;
            removeButton.HoverChanged += on => rbt.localScale = on ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
            removeButton.gameObject.SetActive(false);

            // --- Çanta ---
            bagRoot = EqUi.Rect("Bag", root);
            EqUi.TL(bagRoot, BagRect.x, BagRect.y, BagRect.width, BagRect.height);
            bagScale = EqUi.Rect("BagScale", bagRoot);
            EqUi.Stretch(bagScale);
            bagScale.pivot = new Vector2(0.5f, 0f);
            bagScale.anchoredPosition = Vector2.zero;

            bagGlowImage = EqUi.Img("Glow", bagScale, bagGlow, EqUi.A(EqUi.Gold, 0f), skin);
            EqUi.Stretch(bagGlowImage.rectTransform);
            bagGlowImage.preserveAspect = false;
            bagBodyImage = EqUi.Img("Body", bagScale, bagBody, EqUi.Navy, skin, true);
            EqUi.Stretch(bagBodyImage.rectTransform);
            bagBodyImage.preserveAspect = false;
            bagBodyImage.gameObject.AddComponent<EmergencyBagDropZone>().Bind(this);

            var slotsRoot = bagBodyImage.rectTransform; // yuvalar gövdenin çocuğu: Drop olayı gövdeye çıkar
            for (int i = 0; i < slotCount; i++)
            {
                int col = i % 3, row = i / 3;
                float x = (ArtCavityX + col * (ArtSlotW + ArtSlotGap)) * s;
                float y = (ArtCavityY + row * (ArtSlotH + ArtSlotGap)) * s;
                slots.Add(BuildSlot(slotsRoot, i, x, y));
            }
            if (frontPocket) pocketSlot = BuildSlot(slotsRoot, EmergencyBagInventory.PocketIndex, ArtPocketX * s, ArtPocketY * s);

            lidImage = EqUi.Img("Lid", bagScale, bagLid, EqUi.Navy, skin);
            EqUi.Stretch(lidImage.rectTransform);
            lidImage.preserveAspect = false;
            lidImage.type = Image.Type.Filled;
            lidImage.fillMethod = Image.FillMethod.Horizontal;
            lidImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            lidImage.fillAmount = 0f;

            // Fermuar
            zipTrack = EqUi.Rect("ZipTrack", bagScale);
            EqUi.Stretch(zipTrack);
            zipTrack.pivot = new Vector2(0f, 1f);
            zipHandleImage = EqUi.Img("ZipHandle", zipTrack, zipHandle, EqUi.Amber, skin, true);
            zipHandleRect = zipHandleImage.rectTransform;
            zipHandleRect.anchorMin = zipHandleRect.anchorMax = new Vector2(0f, 1f);
            Vector2 handleSize = zipHandle != null ? zipHandle.rect.size * s : new Vector2(10f * s, 16f * s);
            zipHandleRect.sizeDelta = handleSize;
            zipHandleRect.pivot = new Vector2(0.5f, 1f - 2.5f / (handleSize.y / s));
            var hit = EqUi.Img("Hit", zipHandleRect, null, EqUi.A(EqUi.Ink, 0f), skin, true);
            EqUi.Place(hit.rectTransform, 0.5f, 0.5f, Vector2.zero, handleSize + new Vector2(18f, 14f));
            zipper = zipHandleRect.gameObject.AddComponent<EmergencyBagZipperController>();
            zipper.Configure(zipTrack, zipHandleRect, new Vector2(ArtZipX0 * s, -ArtZipY * s), new Vector2(ArtZipX1 * s, -ArtZipY * s), 0f);
            zipper.Grabbed += OnZipGrabbed;
            zipper.ProgressChanged += OnZipProgress;
            zipper.Released += OnZipReleased;
            var arrowImg = EqUi.Img("ZipArrow", zipTrack, arrow, EqUi.Gold, skin);
            zipArrow = arrowImg.rectTransform;
            zipArrow.anchorMin = zipArrow.anchorMax = new Vector2(0f, 1f);
            zipArrow.pivot = new Vector2(0f, 0.5f);
            zipArrow.sizeDelta = arrow != null ? arrow.rect.size * s : new Vector2(18f, 14f);
            zipArrow.anchoredPosition = new Vector2((ArtZipX0 + 10) * s, -(ArtZipY - 9) * s);
            zipArrowBase = zipArrow.anchoredPosition;
            zipArrow.gameObject.SetActive(false);

            fxLayer = EqUi.Rect("Fx", bagScale);
            EqUi.Stretch(fxLayer);
            fxLayer.pivot = new Vector2(0f, 1f);
            checkImage = EqUi.Img("Check", fxLayer, EqUi.S(skin, k => k.Check), EqUi.Green, skin);
            var cr = checkImage.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(36f, 36f);
            cr.anchoredPosition = new Vector2(BagRect.width - 26f, -20f);
            checkImage.gameObject.SetActive(false);

            // --- Çantadakiler paneli (doluluk göstergesi) ---
            var cp = EqUi.Img("Collection", root, EqUi.S(skin, k => k.InfoCard), EqUi.Navy, skin);
            if (cp.sprite == null) EqUi.Fallback(cp, EqUi.Ink, EqUi.Navy, skin);
            EqUi.TL(cp.rectTransform, CollectRect.x, CollectRect.y, CollectRect.width, CollectRect.height);
            var cpl = EqUi.Txt("Label", cp.rectTransform, font, 8, EqUi.Gold, TextAnchor.UpperLeft, EqUi.Ink);
            EqUi.TL(cpl.rectTransform, 9f, 5f, 150f, 12f);
            cpl.text = packedLabel;
            collectCount = EqUi.Txt("Count", cp.rectTransform, font, 9, EqUi.Cream, TextAnchor.UpperRight, EqUi.Ink);
            EqUi.TL(collectCount.rectTransform, CollectRect.width - 70f, 4f, 61f, 12f);
            for (int i = 0; i < slotCount; i++)
                collectBoxes.Add(BuildMiniBox(cp.rectTransform, 9f + i * 22f, 21f));
            if (frontPocket)
            {
                collectPocketBox = BuildMiniBox(cp.rectTransform, 9f + slotCount * 22f + 6f, 21f);
                EqUi.FallbackColors(collectPocketBox, EqUi.Ink, EqUi.NavyLight);
            }

            // --- Başarı bandı ---
            var sb = EqUi.Img("Success", root, EqUi.S(skin, k => k.InfoCard), EqUi.Navy, skin);
            if (sb.sprite == null) EqUi.Fallback(sb, EqUi.Green, EqUi.Navy, skin);
            successBanner = sb.rectTransform;
            EqUi.Place(successBanner, 0.5f, 0.5f, new Vector2(0f, 10f), new Vector2(270f, 90f));
            successBanner.pivot = new Vector2(0.5f, 0.5f);
            successGroup = sb.gameObject.AddComponent<CanvasGroup>();
            successGroup.blocksRaycasts = false;
            var sCheck = EqUi.Img("Check", successBanner, EqUi.S(skin, k => k.Check), EqUi.Green, skin);
            EqUi.TL(sCheck.rectTransform, 14f, 34f, 36f, 36f);
            var st = EqUi.Txt("Title", successBanner, font, 14, EqUi.Green, TextAnchor.UpperLeft, EqUi.Ink);
            EqUi.TL(st.rectTransform, 60f, 26f, 200f, 20f);
            st.text = successTitle;
            var sbody = sb.sprite != null ? EqUi.Txt("Body", successBanner, font, 9, EqUi.Ink, TextAnchor.UpperLeft)
                : EqUi.Txt("Body", successBanner, font, 9, EqUi.Cream, TextAnchor.UpperLeft, EqUi.Ink);
            EqUi.TL(sbody.rectTransform, 60f, 47f, 200f, 14f);
            sbody.text = successBody;
            var badge = EqUi.Img("Badge", successBanner, null, EqUi.Green, skin);
            EqUi.TL(badge.rectTransform, 60f, 66f, 126f, 15f);
            EqUi.Fallback(badge, EqUi.GreenDark, EqUi.Green, skin);
            var bt = EqUi.Txt("Label", badge.rectTransform, font, 8, EqUi.White, TextAnchor.MiddleCenter, EqUi.ShadowGreen);
            bt.text = successBadge;
            successBanner.gameObject.SetActive(false);

            // --- Sürükleme katmanı (en üstte) ---
            dragLayer = EqUi.Rect("DragLayer", root);
            EqUi.Stretch(dragLayer);

            canvas.gameObject.SetActive(false);
            Log($"arayüz kuruldu ({cards.Count} eşya, {slotCount} yuva{(frontPocket ? " + ön cep" : "")})");
        }

        private Card BuildCard(EmergencyItemData item, int index, float x, float y)
        {
            var img = EqUi.Img("Card_" + item.Id, table, null, EqUi.A(EqUi.Ink, 0f), skin, true);
            var rt = img.rectTransform;
            EqUi.TL(rt, x, y, CellW, CellH);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + CellW * 0.5f, -y - CellH * 0.5f);

            var frame = EqUi.Img("Frame", rt, cardFrame, EqUi.Gold, skin);
            EqUi.StretchOffsets(frame.rectTransform, 3f, 1f, 3f, 1f);
            if (frame.sprite == null) frame.color = EqUi.A(EqUi.Gold, 0f);
            frame.color = EqUi.A(EqUi.Gold, 0f);

            var shadow = EqUi.Img("Shadow", rt, itemShadow, EqUi.A(EqUi.Ink, 0.35f), skin);
            EqUi.TL(shadow.rectTransform, (CellW - 36f) * 0.5f, 37f, 36f, 10f);
            var icon = EqUi.Img("Icon", rt, item.Icon, EqUi.Cream, skin);
            EqUi.TL(icon.rectTransform, (CellW - IconSize.x) * 0.5f, 2f, IconSize.x, IconSize.y);
            var label = EqUi.Txt("Name", rt, font, 8, EqUi.Cream, TextAnchor.UpperCenter, EqUi.Ink);
            EqUi.TL(label.rectTransform, 0f, 45f, CellW, 14f);
            label.text = item.DisplayName;
            var check = EqUi.Img("Check", rt, EqUi.S(skin, k => k.Check), EqUi.Green, skin);
            EqUi.TL(check.rectTransform, CellW - 22f, 2f, 14f, 14f);
            check.gameObject.SetActive(false);

            var handler = img.gameObject.AddComponent<EmergencyItemDragHandler>();
            handler.Bind(this, item, index);
            return new Card { item = item, rt = rt, handler = handler, frame = frame, icon = icon, check = check, label = label };
        }

        private Slot BuildSlot(RectTransform parent, int index, float x, float y)
        {
            int s = artScale;
            var back = EqUi.Img(index == EmergencyBagInventory.PocketIndex ? "PocketSlot" : "Slot" + index, parent, slotSprite, EqUi.Hex(0x0E1426), skin, true);
            var rt = back.rectTransform;
            EqUi.TL(rt, x, y, ArtSlotW * s, ArtSlotH * s);
            back.preserveAspect = false;
            var icon = EqUi.Img("Item", rt, null, EqUi.Cream, skin);
            var ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = IconSize;
            ir.anchoredPosition = new Vector2(0f, s);
            icon.enabled = false;
            var frame = EqUi.Img("Frame", rt, slotFrame, EqUi.Gold, skin);
            EqUi.Stretch(frame.rectTransform);
            frame.preserveAspect = false;
            frame.color = EqUi.A(EqUi.Gold, 0f);
            var btn = back.gameObject.AddComponent<EmergencyBagSlotButton>();
            btn.Bind(this, index);
            return new Slot { index = index, rt = rt, back = back, icon = icon, frame = frame };
        }

        private Image BuildMiniBox(RectTransform parent, float x, float y)
        {
            var box = EqUi.Img("Box", parent, null, EqUi.NavyDark, skin);
            EqUi.TL(box.rectTransform, x, y, 20f, 20f);
            EqUi.Fallback(box, EqUi.Ink, EqUi.NavyDark, skin);
            var icon = EqUi.Img("Icon", box.rectTransform, null, EqUi.Cream, skin);
            EqUi.Stretch(icon.rectTransform);
            icon.enabled = false;
            return box;
        }
    }
}
