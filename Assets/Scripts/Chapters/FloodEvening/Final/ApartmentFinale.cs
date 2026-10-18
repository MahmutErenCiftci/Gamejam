using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// APARTMAN FİNALİ (sel sonrası, avlu → apartman girişi). Controller güvenli bölgeye (SafeZone) varılınca Begin()'i bir kez çağırır;
    /// oyuncu serbest yürür ve finalin iki [E] noktasını (IFloodEveningInteraction) sırayla kullanır:
    ///   1) DoorLockInteraction "Kilidi Kontrol Et" → Cenk tuş takımına bakar, kilit kırmızı yanar → "Kilit sıfırlanmış..." → lock_checked
    ///   2) MailboxInteraction "Posta Kutusunu Kontrol Et" → kapak açılır, zarf alınır, mektup yakın planı (ApartmentLetterView) → pin_found
    ///   3) DoorLockInteraction "Kodu Gir" → ApartmentLockView (fareyle PIN; yanlış → kırmızı, doğru → yeşil, kapı AÇILMAZ) → pin_entered;
    ///      ardından kolu fareyle aşağı çekme → handle_turned
    ///   4) Sinematik ("cine_final"): mandal çekilir, kapı gıcırdayıp aralanır, menteşesine doğru açılır, sıcak ışık dışarı taşar,
    ///      Cenk içeri yürür, kapı arkasından yavaşça kapanır → IsComplete + home_entered (kararma ve bitiş ekranı controller'da).
    /// Kodun tek kaynağı ApartmentFinalData.pin (Assets/Data/FloodEvening/apartment_final.json, UI ajanı): mektup ve kilit aynı veriyi alır.
    /// Her adım bir kez bildirilir; kapı kol çekilmeden asla açılmaz. Bölüm sıfırlanınca (Hub.ResetRequested) her şey başa döner.
    /// Bu sınıf durumu ve Cenk yardımcılarını (kısa yürüyüş, duvara dönme, poz) tutar; dünya görselleri ApartmentDoor ve posta kutusundadır.
    /// </summary>
    [DefaultExecutionOrder(1100)] // LateUpdate'te Cenk kopyası, CenkOutfit'in (1000) kıyafet eşlemesinden SONRA kopyalansın
    public class ApartmentFinale : MonoBehaviour, IFloodEveningFinale, IFloodEveningResettable
    {
        public const string CinematicId = "cine_final";
        public const string LockCheckedStep = "lock_checked";
        public const string PinFoundStep = "pin_found";
        public const string PinEnteredStep = "pin_entered";
        public const string HandleTurnedStep = "handle_turned";
        public const string HomeEnteredStep = "home_entered";

        [Header("Veri (UI ajanı: Assets/Data/FloodEvening/apartment_final.json)")]
        [SerializeField] private TextAsset dataJson;

        [Header("Parçalar (FE_ApartmentFinal kurulumunda bağlanır)")]
        [SerializeField] private ApartmentDoor door;
        [SerializeField] private MailboxInteraction mailbox;
        [SerializeField] private DoorLockInteraction doorLock;
        [Tooltip("FE_ApartmentLockUi kökü (UI ajanı): mektup + kilit görünümleri ve aynı apartment_final.json (boşsa sahnede aranır)")]
        [SerializeField] private ApartmentFinalUi ui;
        [Tooltip("FE_ApartmentLockUi içindeki kilit görünümü (boşsa ui'dan / sahneden)")]
        [SerializeField] private ApartmentLockView lockView;
        [Tooltip("FE_ApartmentLockUi içindeki mektup görünümü (boşsa sahnede aranır)")]
        [SerializeField] private ApartmentLetterView letterView;
        [SerializeField] private IsoPlayerController player;

        [Header("Çapalar (dünya)")]
        [Tooltip("Kapının tam önü (eşikten bir adım dışarıda, kapının ortası); Cenk kapıdan uzaksa önce buraya yürür")]
        [SerializeField] private Transform doorFront;
        [Tooltip("Eşik: duvar düzlemindeki kapı ortası (Cenk buradan sonra kasanın arkasında çizilir)")]
        [SerializeField] private Transform threshold;
        [Tooltip("Kapı kanadının düzlemi (eşikten biraz içeride; Cenk buradan sonra kanadın arkasında çizilir)")]
        [SerializeField] private Transform leafPlane;
        [Tooltip("İçeride Cenk'in yürüdüğü son nokta (holün içi)")]
        [SerializeField] private Transform insideSpot;
        [Tooltip("Sinematikte kameranın baktığı nokta (kapı + Cenk)")]
        [SerializeField] private Transform cameraFocus;
        [Tooltip("Duvara bakış yönü (ekran): +gy = sol-yukarı")]
        [SerializeField] private Vector2 wallFacing = new Vector2(-1f, 0.5f);

        [Header("Cenk pozları (Assets/Art/FloodEvening/Final/Cenk, kuzeybatıya bakar)")]
        [SerializeField] private Sprite[] inspectLock = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] reachMailbox = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] takeEnvelope = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] readEnvelope = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] typeKeypad = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] turnHandle = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] reactDoor = Array.Empty<Sprite>();
        [SerializeField, Min(0.04f)] private float frameSeconds = 0.16f;

        [Header("Yürüme")]
        [Tooltip("Etkileşim noktasına kısa adım hızı (dünya birimi / sn)")]
        [SerializeField, Min(0.1f)] private float stepSpeed = 1.1f;
        [Tooltip("Kapıdan içeri yürüme hızı")]
        [SerializeField, Min(0.1f)] private float walkInSpeed = 0.55f;
        [Tooltip("Cenk kasanın içindeyken (cephenin arkası, kanadın önü) sıralama")]
        [SerializeField] private int recessSortingOrder = -51;
        [Tooltip("Cenk kanat düzlemini geçince (kanadın arkası, holün önü) sıralama")]
        [SerializeField] private int insideSortingOrder = -56;
        [Tooltip("Eşikten sonra Cenk'in yerine çizilen kopya. Oyuncunun kendi SpriteRenderer sıralaması hiç değişmez: değişseydi " +
                 "sahnenin duvar kesme sistemi (DynamicWallCutaway) cepheyi 'Cenk'in önünde' sayıp saydamlaştırırdı.")]
        [SerializeField] private SpriteRenderer cenkProxy;
        [Tooltip("Kapı kapanma süresi (sn)")]
        [SerializeField, Min(0.3f)] private float closeSeconds = 1.6f;

        [Header("Yedek metinler (veride yoksa)")]
        [SerializeField] private string lockToastTitle = "KAPI KİLİDİ";
        [SerializeField] private string lockResetFallback = "Kilit sıfırlanmış. Yeni kod gerekli.";
        [SerializeField] private string mailboxHintFallback = "Posta kutusuna bir bak.";
        [SerializeField] private string mailboxPromptFallback = "Posta Kutusunu Kontrol Et";
        [SerializeField] private string lockPromptFallback = "Kilidi Kontrol Et";
        [SerializeField] private string lockPinPromptFallback = "Kodu Gir";
        [Tooltip("Kod kabul edildi, kol henüz çevrilmedi (yakın plan iptal edildiyse)")]
        [SerializeField] private string lockHandlePrompt = "Kapı Kolunu Çevir";
        [SerializeField] private string letterToastTitle = "POSTA KUTUSU";

        [Header("Hata ayıklama")]
        [SerializeField] private bool logSteps = true;

        private ApartmentFinalData data;
        private IsoCharacterAnimator anim;
        private SpriteRenderer playerRenderer;
        private Collider2D[] playerColliders = Array.Empty<Collider2D>();
        private bool[] collidersWereEnabled = Array.Empty<bool>();
        private bool playerModified;
        private int savedSortingOrder;
        private Color savedColor = Color.white;
        private bool cameraMoved;
        private bool proxyActive;
        private bool savedRendererEnabled = true;
        private FloodEveningHud hud;

        // --- durum (testler ve modüller için okunur) ---
        public bool Begun { get; private set; }
        public bool IsComplete { get; private set; }
        public bool LockChecked { get; private set; }
        public bool PinFound { get; private set; }
        public bool PinEntered { get; private set; }
        public bool HandleTurned { get; private set; }
        /// <summary>Sinematik (kapı açılıp Cenk içeri yürürken) sürüyor.</summary>
        public bool Entering { get; private set; }

        public ApartmentDoor Door => door;
        public IsoPlayerController Player => player;
        public ApartmentFinalUi Ui => ui != null ? ui : (ui = FindAnyObjectByType<ApartmentFinalUi>(FindObjectsInactive.Include));
        public ApartmentLockView LockView => lockView != null ? lockView : (lockView = Ui != null && Ui.Lock != null ? Ui.Lock : FindAnyObjectByType<ApartmentLockView>(FindObjectsInactive.Include));
        public ApartmentLetterView LetterView => letterView != null ? letterView : (letterView = Ui != null && Ui.Letter != null ? Ui.Letter : FindAnyObjectByType<ApartmentLetterView>(FindObjectsInactive.Include));

        /// <summary>Tek veri kaynağı (kod, metinler): FE_ApartmentLockUi'nin verisi, yoksa aynı dosya (dataJson). Yüklenemezse varsayılanlar.</summary>
        public ApartmentFinalData Data
        {
            get
            {
                if (data != null) return data;
                try { data = dataJson == null && Ui != null ? Ui.Data : ApartmentFinalData.Load(dataJson); }
                catch (Exception e) { Debug.LogWarning("[APT] apartment_final.json okunamadı: " + e.Message, this); }
                if (data == null) data = new ApartmentFinalData();
                return data;
            }
        }

        public string Pin => Data != null && !string.IsNullOrEmpty(Data.pin) ? Data.pin : "";
        public string MailboxPrompt => Text(Data.mailbox_prompt, mailboxPromptFallback);
        public string LockPrompt => PinEntered ? lockHandlePrompt : PinFound ? Text(Data.lock_prompt_pin, lockPinPromptFallback) : Text(Data.lock_prompt, lockPromptFallback);

        // Modüllerin [E] noktası şu an kullanılabilir mi
        public bool CanUseLock => Begun && !HandleTurned && !Entering && !IsComplete;
        public bool CanUseMailbox => Begun && LockChecked && !Entering && !IsComplete;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (cenkProxy != null) cenkProxy.enabled = false;
            if (player != null)
            {
                anim = player.GetComponent<IsoCharacterAnimator>();
                playerRenderer = player.GetComponent<SpriteRenderer>();
            }
            if (door == null) door = GetComponentInChildren<ApartmentDoor>(true);
            if (mailbox == null) mailbox = GetComponentInChildren<MailboxInteraction>(true);
            if (doorLock == null) doorLock = GetComponentInChildren<DoorLockInteraction>(true);
            RefreshLabels();
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;

        private void LateUpdate()
        {
            if (!proxyActive || cenkProxy == null || playerRenderer == null) return;
            cenkProxy.sprite = playerRenderer.sprite;
            cenkProxy.flipX = playerRenderer.flipX;
            cenkProxy.transform.position = playerRenderer.transform.position;
        }

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            if (player != null) player.ExternalVelocity = Vector2.zero;
        }

        // =====================================================================
        // IFloodEveningFinale
        // =====================================================================

        /// <summary>Güvenli bölgeye varılınca controller bir kez çağırır: [E] noktaları açılır, tuş takımı beklemede yanıp söner.</summary>
        public void Begin()
        {
            if (Begun) return;
            Begun = true;
            RefreshLabels();
            if (door != null) door.SetKeypad(ApartmentDoor.KeypadState.Idle);
            Log("başladı: önce kapı kilidi, sonra posta kutusu, sonra kod + kol");
        }

        // =====================================================================
        // Adımlar (her biri bir kez)
        // =====================================================================

        public void MarkLockChecked()
        {
            if (LockChecked) return;
            LockChecked = true;
            Step(LockCheckedStep);
        }

        public void MarkPinFound()
        {
            if (PinFound) return;
            PinFound = true;
            RefreshLabels();
            Step(PinFoundStep);
        }

        public void MarkPinEntered()
        {
            if (PinEntered) return;
            PinEntered = true;
            RefreshLabels();
            Step(PinEnteredStep);
        }

        public void MarkHandleTurned()
        {
            if (HandleTurned) return;
            HandleTurned = true;
            Step(HandleTurnedStep);
        }

        private void Step(string id)
        {
            Log("adım: " + id);
            FloodEveningHub.Complete(id);
        }

        public void RefreshLabels()
        {
            if (mailbox != null && mailbox.Point != null) mailbox.Point.Setup(MailboxPrompt, "", false, false);
            if (doorLock != null && doorLock.Point != null) doorLock.Point.Setup(LockPrompt, "", false, false);
        }

        // =====================================================================
        // Mesajlar
        // =====================================================================

        /// <summary>"Kilit sıfırlanmış. Yeni kod gerekli." + "Posta kutusuna bir bak." (bilgi şeridi).</summary>
        public void ShowLockResetMessage()
        {
            string body = Text(Data.lock_reset_body, lockResetFallback);
            string hintText = Text(Data.lock_hint_mailbox, mailboxHintFallback);
            Toast(Text(Data.lock_reset_title, lockToastTitle), body + " " + hintText, 4f);
        }

        /// <summary>Mektup yakın planı (UI ajanının ApartmentLetterView'i). Görünüm yoksa kod bilgi şeridinde gösterilir.</summary>
        public IEnumerator ShowLetter()
        {
            var view = LetterView;
            if (view != null)
            {
                yield return view.Show(Data);
                yield return null; // mektubu kapatan tuş başka bir etkileşimi tetiklemesin
                yield break;
            }
            Debug.LogWarning("[APT] ApartmentLetterView yok → kod bilgi şeridinde gösteriliyor", this);
            Toast(letterToastTitle, Data.MailboxEmptyText, 4f);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime) yield return null;
        }

        private void Toast(string title, string body, float seconds)
        {
            if (hud == null) hud = FindAnyObjectByType<FloodEveningHud>(FindObjectsInactive.Include);
            if (hud != null) hud.ShowToast(title, body, FloodEveningHud.ToastKind.Info, seconds);
            Log("mesaj: " + title + " — " + body);
        }

        // =====================================================================
        // Cenk yardımcıları (modüller kullanır)
        // =====================================================================

        /// <summary>Cenk'i verilen noktaya kısa adımlarla yürütür (hareket kilitliyken ExternalVelocity ile). En çok maxSeconds sürer.</summary>
        public IEnumerator StepTo(Transform target, float maxSeconds = 1.2f)
        {
            if (player == null || target == null) yield break;
            ClearPose();
            Vector2 goal = target.position;
            for (float t = 0f; t < maxSeconds; t += Time.deltaTime)
            {
                Vector2 d = goal - (Vector2)player.transform.position;
                float g = IsoLength(d);
                if (g < 0.035f) break;
                player.ExternalVelocity = IsoDir(d) * Mathf.Min(stepSpeed, Mathf.Max(0.25f, g * 5f));
                yield return null;
            }
            player.ExternalVelocity = Vector2.zero;
            for (float t = 0f; t < 0.08f; t += Time.deltaTime) yield return null; // yavaşlayıp dursun
        }

        /// <summary>Cenk duvara (apartman cephesine) döner.</summary>
        public void FaceWall()
        {
            if (anim == null) return;
            anim.SetMotion(Vector2.zero);
            anim.Face(wallFacing);
        }

        public IEnumerator Pose(PoseKind kind, float seconds, bool loop = false) => Pose(Frames(kind), seconds, loop);

        private IEnumerator Pose(Sprite[] frames, float seconds, bool loop)
        {
            if (anim == null || frames == null || frames.Length == 0)
            {
                for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
                yield break;
            }
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                int k = Mathf.FloorToInt(t / frameSeconds);
                k = loop ? k % frames.Length : Mathf.Min(k, frames.Length - 1);
                if (frames[k] != null) anim.ShowPose(frames[k]);
                yield return null;
            }
        }

        /// <summary>Pozun tek bir karesini sabit gösterir (kare yoksa son kare).</summary>
        public void HoldPose(PoseKind kind, int frame)
        {
            var frames = Frames(kind);
            if (anim == null || frames == null || frames.Length == 0) return;
            var s = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
            if (s != null) anim.ShowPose(s);
        }

        public int PoseLength(PoseKind kind) { var f = Frames(kind); return f != null ? f.Length : 0; }

        public void ClearPose() { if (anim != null) anim.ClearPose(); }

        public enum PoseKind { InspectLock, ReachMailbox, TakeEnvelope, ReadEnvelope, TypeKeypad, TurnHandle, ReactDoor }

        private Sprite[] Frames(PoseKind kind)
        {
            switch (kind)
            {
                case PoseKind.InspectLock: return inspectLock;
                case PoseKind.ReachMailbox: return reachMailbox;
                case PoseKind.TakeEnvelope: return takeEnvelope;
                case PoseKind.ReadEnvelope: return readEnvelope;
                case PoseKind.TypeKeypad: return typeKeypad;
                case PoseKind.TurnHandle: return turnHandle;
                default: return reactDoor;
            }
        }

        // =====================================================================
        // Sinematik: kapı açılır, Cenk içeri girer, kapı kapanır
        // =====================================================================

        /// <summary>Kol çekildikten sonra (DoorLockInteraction çağırır; oyuncu hareketi kilitli). Bir kez çalışır.</summary>
        public IEnumerator EnterSequence()
        {
            if (Entering || IsComplete || !HandleTurned) yield break;
            Entering = true;
            FloodEveningHub.NotifyOpened(CinematicId);
            Log("sinematik: mandal → aralık → açılış → içeri");
            try
            {
                var cam = Camera.main != null ? Camera.main.GetComponent<ChapterCamera>() : null;
                if (cam != null && cameraFocus != null) { cam.ShowPoint(cameraFocus.position, false); cameraMoved = true; }

                // 1) kol aşağıda, mandal geri çekilir
                HoldPose(PoseKind.TurnHandle, PoseLength(PoseKind.TurnHandle) - 1);
                if (door != null) yield return door.Unlatch();
                // 2) kapı gıcırdayarak aralanır, ince sıcak ışık çizgisi
                HoldPose(PoseKind.ReactDoor, 0);
                if (door != null) yield return door.Ajar();
                for (float t = 0f; t < 0.35f; t += Time.deltaTime) yield return null;
                // 3) menteşesine doğru açılır, ışık dışarı taşar
                if (door != null) yield return door.SwingOpen();
                yield return Pose(PoseKind.ReactDoor, 0.55f);
                ClearPose();
                // 4) Cenk içeri yürür
                yield return WalkInside();
                // 5) kapı arkasından yavaşça kapanır
                if (door != null) yield return door.Close(closeSeconds);
                for (float t = 0f; t < 0.25f; t += Time.deltaTime) yield return null;

                IsComplete = true;
                Log("Cenk eve girdi → home_entered");
                FloodEveningHub.Complete(HomeEnteredStep);
            }
            finally
            {
                if (player != null) player.ExternalVelocity = Vector2.zero;
                Entering = false;
                FloodEveningHub.NotifyClosed(CinematicId);
            }
        }

        /// <summary>Kapı önü → eşik → kanat düzlemi → hol. Duvar çizgisi yürünebilir alanın sınırı olduğu için
        /// yürüyüş boyunca Cenk'in collider'ı kapalıdır; kasaya girince cephenin arkasında, kanadı geçince kanadın arkasında çizilir.</summary>
        private IEnumerator WalkInside()
        {
            if (player == null) yield break;
            SavePlayer();
            foreach (var c in playerColliders) if (c != null) c.enabled = false;

            Vector2 front = doorFront != null ? (Vector2)doorFront.position : (Vector2)player.transform.position;
            Vector2 sill = threshold != null ? (Vector2)threshold.position : front;
            Vector2 leaf = leafPlane != null ? (Vector2)leafPlane.position : sill;
            Vector2 inside = insideSpot != null ? (Vector2)insideSpot.position : leaf;
            float sillGy = Gy(sill), leafGy = Gy(leaf), insideGy = Gy(inside);

            // kilidin önünden (kapının sağı) doğrudan eşiğe yürür: kameraya dönmesin. Uzaktaysa önce kapının önüne gelir.
            bool farFromDoor = IsoLength(sill - (Vector2)player.transform.position) > 0.7f;
            Vector2[] path = farFromDoor ? new[] { front, sill, leaf, inside } : new[] { sill, leaf, inside };
            foreach (var wp in path)
            {
                for (float t = 0f; t < 3f; t += Time.deltaTime)
                {
                    Vector2 p = player.transform.position;
                    Vector2 d = wp - p;
                    if (IsoLength(d) < 0.04f) break;
                    player.ExternalVelocity = IsoDir(d) * walkInSpeed;
                    ApplyDepth(Gy(p), sillGy, leafGy, insideGy);
                    yield return null;
                }
            }
            player.ExternalVelocity = Vector2.zero;
            ApplyDepth(insideGy + 1f, sillGy, leafGy, insideGy);
            for (float t = 0f; t < 0.15f; t += Time.deltaTime) yield return null;
        }

        /// <summary>Derinliğe göre sıralama ve kaybolma: kasada cephenin arkası, kanattan sonra kanadın arkası, holde yavaşça kaybolur.</summary>
        private void ApplyDepth(float gy, float sillGy, float leafGy, float insideGy)
        {
            if (playerRenderer == null) return;
            bool inside = gy > sillGy + 0.05f;
            float k = insideGy > leafGy ? Mathf.Clamp01((gy - leafGy) / (insideGy - leafGy)) : (gy > leafGy ? 1f : 0f);
            var c = savedColor;
            c.a = savedColor.a * (1f - Mathf.SmoothStep(0f, 1f, k));
            int order = gy > leafGy + 0.03f ? insideSortingOrder : recessSortingOrder;
            if (cenkProxy != null)
            {
                // eşiği geçince kopyaya devret (gerçek renderer gizlenir, sıralaması değişmez)
                SetProxy(inside);
                if (!inside) return;
                cenkProxy.sortingOrder = order;
                cenkProxy.color = c;
                return;
            }
            if (inside) playerRenderer.sortingOrder = order;   // yedek: kopya bağlı değilse doğrudan
            playerRenderer.color = c;
        }

        private void SetProxy(bool on)
        {
            if (cenkProxy == null || playerRenderer == null || proxyActive == on) return;
            proxyActive = on;
            if (on) { LateUpdate(); cenkProxy.sortingLayerID = playerRenderer.sortingLayerID; }
            cenkProxy.enabled = on;
            playerRenderer.enabled = !on && savedRendererEnabled;
        }

        private void SavePlayer()
        {
            if (playerModified || player == null) return;
            playerModified = true;
            playerColliders = player.GetComponents<Collider2D>();
            collidersWereEnabled = new bool[playerColliders.Length];
            for (int i = 0; i < playerColliders.Length; i++) collidersWereEnabled[i] = playerColliders[i] != null && playerColliders[i].enabled;
            if (playerRenderer != null) { savedSortingOrder = playerRenderer.sortingOrder; savedColor = playerRenderer.color; savedRendererEnabled = playerRenderer.enabled; }
        }

        private void RestorePlayer()
        {
            if (!playerModified) return;
            playerModified = false;
            for (int i = 0; i < playerColliders.Length; i++) if (playerColliders[i] != null) playerColliders[i].enabled = collidersWereEnabled[i];
            if (cenkProxy != null) { proxyActive = false; cenkProxy.enabled = false; }
            if (playerRenderer != null) { playerRenderer.sortingOrder = savedSortingOrder; playerRenderer.color = savedColor; playerRenderer.enabled = savedRendererEnabled; }
        }

        // =====================================================================
        // Sıfırlama
        // =====================================================================

        public void ResetModule()
        {
            if (Entering) FloodEveningHub.NotifyClosed(CinematicId);
            Entering = false;
            Begun = IsComplete = LockChecked = PinFound = PinEntered = HandleTurned = false;
            if (player != null) player.ExternalVelocity = Vector2.zero;
            RestorePlayer();
            if (cameraMoved)
            {
                cameraMoved = false;
                var cam = Camera.main != null ? Camera.main.GetComponent<ChapterCamera>() : null;
                var bounds = FindAnyObjectByType<CameraBounds>(FindObjectsInactive.Include);
                if (cam != null && player != null) cam.Follow(player.transform, bounds, true);
            }
            var view = lockView != null ? lockView : FindAnyObjectByType<ApartmentLockView>(FindObjectsInactive.Include);
            if (view != null) view.ResetView();
            RefreshLabels();
        }

        // =====================================================================
        // Yardımcılar
        // =====================================================================

        private static string Text(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

        // 2:1 izometrik: zemin uzunluğu (dikey fark 2 ile), zemin yönü, ızgara gy'si (gy = 2y - x)
        private static float IsoLength(Vector2 d) => Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y);
        private static Vector2 IsoDir(Vector2 d) { float m = IsoLength(d); return m > 0.0001f ? d / m : Vector2.zero; }
        private static float Gy(Vector2 p) => 2f * p.y - p.x;

        private void Log(string message)
        {
            if (logSteps) Debug.Log("[APT] " + message, this);
        }
    }
}
