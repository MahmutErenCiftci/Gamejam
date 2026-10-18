using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Apartman kapısının elektronik kilidi + kapı kolu — yakın plan mini oyun (prefab: FE_ApartmentLockUi → "Lock").
    /// Run(data):
    ///  1) KOD: yalnızca fareyle tuşlara tıklanır (üzerine gelince turkuaz çerçeve, tıklayınca tuş 1 px iner + koyulaşır, SFX_Final_Key).
    ///     Ekranda 4 hane görünür. '*' girişi siler, '#' son rakamı siler. 4. rakamda data.pin ile karşılaştırılır:
    ///     yanlış → ekran ~0,6 sn kırmızı yanıp söner (pin_wrong), PinWrong, giriş temizlenir, tekrar denenir (başka ceza yok);
    ///     doğru → LED yeşil, pin_ok, sürgü göstergesi kayar, PinAccepted = true, PinOk. Kapı KENDİLİĞİNDEN açılmaz.
    ///  2) KOL: kol hafifçe yanıp sönen çerçeveyle vurgulanır, handle_hint görünür, imleç kolun üstünde piksel ele döner.
    ///     Kolun ÜSTÜNDE sol tuşa basıp basılı tutarak aşağı sürüklemek gerekir (tek tık asla açmaz):
    ///     ilerleme = aşağı sürükleme / dragPixelsFull (sanal piksel). Kol rozet merkezindeki pivotu etrafında 0°→45° döner
    ///     (önceden çizilmiş 16 kare; yalnızca kol döner), ~%70'te küçük bir tırnak (açı bir an durur, kol titrer), her %15'te tık sesi.
    ///     Basılıyken ≥ successThreshold → SFX_Final_Latch, mandal dili kapıya çekilir, HandleTurned = true, HandleDone (bir kez),
    ///     ~0,4 sn sonra Run döner. Erken bırakma / geri yukarı çekme / imlecin pencereden çıkması → kol ~0,25 sn'de yaylanıp 0'a döner.
    ///  İptal: Esc, sağ tık veya "[Esc] Geri" düğmesi → Run döner, HandleTurned false kalır; PinAccepted korunur (yeniden açılınca
    ///  doğrudan kol aşaması, yeşil LED). Hub: NotifyOpened("lock") / NotifyClosed("lock"). Gerçek zamanlı animasyon, timeScale'e dokunmaz.
    ///  ResetView(): tam sıfırlama (FloodEveningHub.ResetRequested ile de çağrılır).
    /// Ek (sözleşme dışı, isteğe bağlı): ShowResetNotice(data) — kod girilmeden önce kilidin "sıfırlanmış" yakın planı.
    /// </summary>
    public class ApartmentLockView : MonoBehaviour, IFloodEveningResettable
    {
        public const string HubId = "lock";

        [Header("Sprite'lar (Assets/Art/FloodEvening/Final/UI)")]
        [Tooltip("UI_FE_Final_LockBg — kapının yakın planı (272x196)")]
        [SerializeField] private Sprite background;
        [Tooltip("UI_FE_Final_LockBody — kilit gövdesi (90x150)")]
        [SerializeField] private Sprite body;
        [SerializeField] private Sprite rosette, latchBolt, deadbolt, dragArrow, mouseIcon;
        [Tooltip("36 tuş: [durum * 12 + tuş]; durum 0 normal, 1 üzerinde, 2 basılı; tuş sırası 1..9, *, 0, #")]
        [SerializeField] private Sprite[] keySprites = new Sprite[36];
        [Tooltip("LCD: 0..9, Ghost (sönük 8), Dash, Under")]
        [SerializeField] private Sprite[] lcdSprites = new Sprite[13];
        [Tooltip("LED: Off, Red, Green")]
        [SerializeField] private Sprite[] ledSprites = new Sprite[3];
        [Tooltip("Kapı kolu kareleri 0°..45° (3° adım); pivot rozet merkezinde")]
        [SerializeField] private Sprite[] handleFrames = new Sprite[16];
        [SerializeField] private Sprite[] handleGlowFrames = new Sprite[16];
        [Tooltip("Pixelify Sans (boşsa FeUi.DefaultFont)")]
        [SerializeField] private Font font;
        [SerializeField] private Font boldFont;
        [Tooltip("Yalnızca üstte ölçekli prefab tuvali yoksa kurulan kendi tuvalinin sırası")]
        [SerializeField] private int sortingOrder = ApartmentUi.DefaultSortingOrder;

        [Header("Kol")]
        [Tooltip("Kolu tam (45°) çevirmek için gereken aşağı sürükleme (sanal piksel, 480x270 tuvalde)")]
        [SerializeField, Min(10f)] private float dragPixelsFull = 40f;
        [SerializeField, Range(0.5f, 1f)] private float successThreshold = 0.95f;
        [SerializeField, Min(0.05f)] private float springSeconds = 0.25f;
        [SerializeField, Min(0f)] private float successHoldSeconds = 0.4f;

        [Header("Kod")]
        [SerializeField, Min(0.1f)] private float wrongFlashSeconds = 0.6f;
        [SerializeField, Min(0.2f)] private float okSeconds = 0.8f;
        [Tooltip("Dünyayı karartma (proje doğrusal renk uzayında: düşük değerler gözle daha zayıf görünür)")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.72f;

        public bool IsOpen { get; private set; }
        /// <summary>Doğru kod girildi (açılışlar arasında korunur; yalnızca ResetView sıfırlar).</summary>
        public bool PinAccepted { get; private set; }
        /// <summary>Kol eşiğin ötesine çevrildi (kapı açılabilir).</summary>
        public bool HandleTurned { get; private set; }

        public event Action PinOk;
        public event Action PinWrong;
        /// <summary>Kol çevrildi — ResetView'e kadar en fazla bir kez.</summary>
        public event Action HandleDone;

        // QA / hata ayıklama için salt okunur durum
        public string EnteredDigits => entry;
        public float HandleProgress01 => progress;
        public bool Dragging => dragging;
        public string StageName => stage.ToString();
        /// <summary>Özel imleç (el / yumruk) şu an etkin mi — kapanınca false olmalı.</summary>
        public bool CustomCursorActive => cursor != CursorKind.None;

        private enum Stage { Closed, Notice, Pin, PinWrong, PinOk, Handle, Done }
        private enum CursorKind { None, Hand, Grab }

        // yerleşim: çizimle birebir (tools/sel_aksam_final/ui/build_ui_art.js BG / BODY / HANDLE), çerçevenin sol üstünden
        private const float FrameW = 272f, FrameH = 196f, FrameY = 4f;
        private const float BodyX = 20f, BodyY = 22f;
        private const float KeyX0 = 10f, KeyY0 = 47f, KeyStepX = 25f, KeyStepY = 22f, KeyW = 20f, KeyH = 18f;   // gövde içi
        private const float PivotX = 136f, PivotY = 112f, HandlePivotInSprite = 14f, LeverReach = 76f, LeverGrab = 8f;
        private const float LatchClipX = 229f, LatchY = 108f, LatchTravel = 14f;
        private const float BoltTrackX = 12f, BoltTrackY = 32f, BoltLocked = 8f, BoltOpen = -7f;
        private const float OpenSeconds = 0.14f, CloseSeconds = 0.12f, StatusCenterX = 170f;
        private static readonly char[] KeyChars = { '1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#' };

        private ApartmentFinalData data;
        private bool built, running;
        private int session;
        private Stage stage = Stage.Closed;
        private float stageT;
        private string entry = "";
        private bool handleDoneFired;

        // kol
        private bool dragging;
        private float progress, dragStartY, springFrom, springT = 99f, doneT, jiggleT;
        private int lastTick;
        private bool debugHolding, debugCancel;
        private float debugDown;

        // görünüm
        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasGroup group;
        private RectTransform root, frame, handleRt, glowRt, arrowRt, latchRt, boltRt, statusPill, backBtn, hintBar;
        private string shownHint, shownStatus;
        private Image ledImg, handleImg, glowImg, arrowImg, lcdTint, backEdge, mouseImg;
        private readonly Image[] keyImgs = new Image[12];
        private readonly int[] keyShown = new int[12];
        private readonly Image[] lcdGhost = new Image[4], lcdDigit = new Image[4];
        private Text lcdText, titleText, statusText, hintText, backText;
        private int hoverKey = -1, pressedKey = -1;
        private float pressedT, pulseT;
        private CursorKind cursor = CursorKind.None;

        private void Awake() => Build();
        private void OnEnable() => FloodEveningHub.ResetRequested += ResetView;
        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetView;
            session++; running = false; IsOpen = false; dragging = false; debugHolding = false;
            SetCursor(CursorKind.None);
            if (built) HideNow();
        }
        private void OnDestroy() => SetCursor(CursorKind.None);

        public void ResetModule() => ResetView();

        /// <summary>Tam sıfırlama: kod boş, LED kırmızı, kol 0, PinAccepted/HandleTurned false, görünüm gizli, imleç eski haline.</summary>
        public void ResetView()
        {
            session++;
            running = false;
            bool wasOpen = IsOpen;
            IsOpen = false;
            entry = "";
            PinAccepted = false; HandleTurned = false; handleDoneFired = false;
            dragging = false; debugHolding = false; debugCancel = false;
            progress = 0f; springT = 99f; doneT = 0f; jiggleT = 0f; lastTick = 0;
            stage = Stage.Closed; stageT = 0f;
            SetCursor(CursorKind.None);
            if (built) { HideNow(); RenderAll(); }
            if (wasOpen) FloodEveningHub.NotifyClosed(HubId);
        }

        // =====================================================================
        // Ana akış
        // =====================================================================

        /// <summary>
        /// Kilit yakın planı: kol çevrilince (HandleTurned) ya da oyuncu iptal edince döner. Kol zaten çevrildiyse veya
        /// başka bir Run sürerken çağrılırsa hemen döner.
        /// </summary>
        public IEnumerator Run(ApartmentFinalData data)
        {
            if (running) { Debug.LogWarning("[FinalUI] Kilit yakın planı zaten açık.", this); yield break; }
            if (HandleTurned) yield break;
            running = true;
            int my = ++session;
            Prepare(data);
            titleText.text = this.data.pin_title;
            backText.text = this.data.cancel_hint;
            entry = "";
            dragging = false; debugHolding = false; debugCancel = false;
            progress = 0f; springT = 99f; lastTick = 0; jiggleT = 0f;
            Enter(PinAccepted ? Stage.Handle : Stage.Pin);
            if (PinAccepted) stageT = okSeconds;                 // yeniden açılış: sürgü zaten açık
            yield return OpenRoutine(my);
            if (my != session) yield break;

            while (true)
            {
                if (my != session) yield break;
                float dt = ApartmentUi.AnimDelta();
                ApartmentUi.ApplyScale(scaler);
                if (stage == Stage.Done)
                {
                    doneT += dt;
                    if (doneT >= successHoldSeconds) break;
                }
                else if (CancelRequested()) break;
                else Tick(dt);
                Render(dt);
                yield return null;
            }
            yield return CloseRoutine(my);
        }

        /// <summary>
        /// Ek: kod girilmeden kilit kontrol edilince "sıfırlanmış" yakın planı (LCD'de yanıp sönen çizgiler, kırmızı LED,
        /// lock_reset_title / lock_reset_body / lock_hint_mailbox). E / Space / Enter / tık / Esc / sağ tık ile kapanır.
        /// Hub id yine "lock". İsteğe bağlıdır; dünya tarafı bu metinleri kendi diyaloğuyla da gösterebilir.
        /// </summary>
        public IEnumerator ShowResetNotice(ApartmentFinalData data)
        {
            if (running) yield break;
            running = true;
            int my = ++session;
            Prepare(data);
            titleText.text = this.data.lock_reset_title;
            backText.text = this.data.cancel_hint;
            entry = "";
            Enter(Stage.Notice);
            yield return OpenRoutine(my);
            if (my != session) yield break;
            while (true)
            {
                if (my != session) yield break;
                float dt = ApartmentUi.AnimDelta();
                ApartmentUi.ApplyScale(scaler);
                if (CancelRequested() || GameInput.ContinuePressed) break;
                Tick(dt);
                Render(dt);
                yield return null;
            }
            yield return CloseRoutine(my);
        }

        private void Prepare(ApartmentFinalData d)
        {
            data = d ?? new ApartmentFinalData();
            if (!ApartmentFinalData.IsValidPin(data.pin)) data.Validate();
            Build();
        }

        private IEnumerator OpenRoutine(int my)
        {
            root.gameObject.SetActive(true);
            ApartmentUi.SetGroup(group, true, 0f);
            IsOpen = true;
            FloodEveningHub.NotifyOpened(HubId);
            RenderAll();
            float t = 0f;
            while (t < OpenSeconds)
            {
                if (my != session) yield break;
                ApartmentUi.ApplyScale(scaler);
                float e = ApartmentUi.OutCubic(t / OpenSeconds);
                group.alpha = e;
                ApartmentUi.Center(frame, 0f, FrameY - Mathf.Round((1f - e) * 6f), FrameW, FrameH);
                Render(0f);
                yield return null;
                t += ApartmentUi.AnimDelta();
            }
            group.alpha = 1f;
            ApartmentUi.Center(frame, 0f, FrameY, FrameW, FrameH);
            yield return null;                                   // açan tuş/tık burada sayılmasın
        }

        private IEnumerator CloseRoutine(int my)
        {
            dragging = false; debugHolding = false;
            SetCursor(CursorKind.None);
            float t = 0f;
            while (t < CloseSeconds)
            {
                if (my != session) yield break;
                group.alpha = 1f - t / CloseSeconds;
                yield return null;
                t += ApartmentUi.AnimDelta();
            }
            if (my != session) yield break;
            HideNow();
            IsOpen = false;
            if (stage != Stage.Done) { stage = Stage.Closed; progress = 0f; }
            FloodEveningHub.NotifyClosed(HubId);
            yield return null;                                   // kapatan tuş başka bir etkileşimi başlatmasın
            if (my == session) running = false;
        }

        private bool CancelRequested()
        {
            if (debugCancel) { debugCancel = false; return true; }
            if (GameInput.BackPressed || GameInput.RightClickPressed) return true;
            return GameInput.ClickPressed && !dragging && PointerInRect(backBtn);
        }

        private void Enter(Stage s) { stage = s; stageT = 0f; }

        // =====================================================================
        // Kare mantığı
        // =====================================================================

        private void Tick(float dt)
        {
            stageT += dt;
            pressedT -= dt;
            jiggleT = Mathf.Max(0f, jiggleT - dt);
            bool hasPointer = PointerTL(out var p);

            switch (stage)
            {
                case Stage.Pin:
                    hoverKey = hasPointer ? KeyAt(p) : -1;
                    if (GameInput.ClickPressed && hoverKey >= 0) PressKey(hoverKey);
                    else if (GameInput.ClickPressed && hasPointer && OverHandle(p)) LockedJiggle();
                    break;
                case Stage.Notice:
                    hoverKey = -1;
                    break;
                case Stage.PinWrong:
                    hoverKey = -1;
                    if (stageT >= wrongFlashSeconds) { entry = ""; Enter(Stage.Pin); }
                    break;
                case Stage.PinOk:
                    hoverKey = -1;
                    if (stageT >= okSeconds) Enter(Stage.Handle);
                    break;
                case Stage.Handle:
                    hoverKey = -1;
                    TickHandle(dt, hasPointer, p);
                    break;
            }
            if (stage != Stage.Handle) SetCursor(CursorKind.None);
        }

        private void PressKey(int k)
        {
            if (stage != Stage.Pin || k < 0 || k >= KeyChars.Length) return;
            pressedKey = k; pressedT = 0.1f;
            GameAudio.PlaySfx("SFX_Final_Key");
            char c = KeyChars[k];
            if (c == '*') entry = "";
            else if (c == '#') { if (entry.Length > 0) entry = entry.Substring(0, entry.Length - 1); }
            else if (entry.Length < 4) entry += c;
            if (entry.Length < 4) return;

            if (entry == data.pin)
            {
                PinAccepted = true;
                Enter(Stage.PinOk);
                GameAudio.PlaySfx("SFX_Final_PinOk");
                Raise(PinOk, "PinOk");
            }
            else
            {
                Enter(Stage.PinWrong);
                GameAudio.PlaySfx("SFX_Final_PinWrong");
                Raise(PinWrong, "PinWrong");
            }
        }

        /// <summary>Kod girilmeden kola tıklanınca: kol 3° oynar ama dönmez (kilitli olduğu hissedilsin).</summary>
        private void LockedJiggle()
        {
            if (jiggleT > 0f) return;
            jiggleT = 0.18f;
            GameAudio.PlaySfx("SFX_Final_HandleTick", 0.7f);
        }

        private void TickHandle(float dt, bool hasPointer, Vector2 p)
        {
            Vector2 screen = GameInput.PointerPosition;
            bool inside = ApartmentUi.PointerInsideScreen(screen);
            bool over = hasPointer && inside && OverHandle(p);
            float scale = Mathf.Max(1f, canvas != null ? canvas.scaleFactor : 1f);

            if (!dragging)
            {
                if (progress > 0f)
                {
                    springT += dt;
                    progress = springFrom * (1f - ApartmentUi.OutCubic(springT / springSeconds));
                    if (springT >= springSeconds) progress = 0f;
                    lastTick = Mathf.Min(lastTick, Mathf.FloorToInt(progress / 0.15f));
                }
                // sürükleme yalnızca kolun ÜSTÜNDE yeni bir basışla başlar (dışarıda basıp kola gelmek sayılmaz)
                if (GameInput.ClickPressed && over)
                {
                    dragging = true;
                    dragStartY = screen.y + progress * dragPixelsFull * scale;   // yaylanırken tutulursa sıçramasın
                }
            }
            else if (!debugHolding)
            {
                if (!GameInput.ClickHeld || !inside) Release();
                else
                {
                    float down = (dragStartY - screen.y) / scale;
                    SetProgress(Mathf.Clamp01(down / dragPixelsFull));
                }
            }

            SetCursor(dragging ? CursorKind.Grab : over ? CursorKind.Hand : CursorKind.None);
        }

        private void SetProgress(float value)
        {
            progress = value;
            int tick = Mathf.FloorToInt(progress / 0.15f + 0.0001f);
            if (tick > lastTick) { GameAudio.PlaySfx("SFX_Final_HandleTick"); lastTick = tick; }
            else if (tick < lastTick) lastTick = tick;
            if (dragging && progress >= successThreshold) Succeed();
        }

        private void Release()
        {
            dragging = false; debugHolding = false;
            springFrom = progress; springT = 0f;
        }

        private void Succeed()
        {
            if (HandleTurned) return;
            HandleTurned = true;
            dragging = false; debugHolding = false;
            progress = 1f;
            Enter(Stage.Done); doneT = 0f;
            SetCursor(CursorKind.None);
            GameAudio.PlaySfx("SFX_Final_Latch");
            if (!handleDoneFired) { handleDoneFired = true; Raise(HandleDone, "HandleDone"); }
        }

        private void Raise(Action a, string name)
        {
            try { a?.Invoke(); }
            catch (Exception ex) { Debug.LogError("[FinalUI] " + name + " dinleyicisi hata verdi: " + ex.Message, this); Debug.LogException(ex, this); }
        }

        // =====================================================================
        // İmleç / isabet
        // =====================================================================

        /// <summary>İmlecin çerçeve içindeki sol-üst orijinli sanal piksel konumu.</summary>
        private bool PointerTL(out Vector2 tl)
        {
            tl = default;
            if (frame == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, GameInput.PointerPosition, null, out var local)) return false;
            tl = new Vector2(local.x + FrameW * 0.5f, FrameH * 0.5f - local.y);
            return true;
        }

        private bool PointerInRect(RectTransform rt) => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, GameInput.PointerPosition, null);

        private static int KeyAt(Vector2 p)
        {
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 3; c++)
                {
                    float x = BodyX + KeyX0 + c * KeyStepX, y = BodyY + KeyY0 + r * KeyStepY;
                    if (p.x >= x && p.x < x + KeyW && p.y >= y && p.y < y + KeyH) return r * 3 + c;
                }
            return -1;
        }

        /// <summary>İmleç kolun (şu anki açısıyla) üstünde mi: pivot→uç doğru parçasına uzaklık ≤ LeverGrab.</summary>
        private bool OverHandle(Vector2 p)
        {
            float a = VisualAngle(progress) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));          // sol-üst orijin: +y aşağı
            var rel = p - new Vector2(PivotX, PivotY);
            float along = Mathf.Clamp(Vector2.Dot(rel, dir), -6f, LeverReach);
            return (rel - dir * along).sqrMagnitude <= LeverGrab * LeverGrab;
        }

        /// <summary>İlerleme → görünen açı: direnç eğrisi (p^1.2) + ~%70'te tırnak (açı 0,66–0,74 arasında durur, sonra yetişir).</summary>
        private static float VisualAngle(float p)
        {
            float q = p < 0.66f ? p : p < 0.74f ? 0.66f : Mathf.Lerp(0.66f, 1f, (p - 0.74f) / 0.26f);
            return Mathf.Pow(Mathf.Clamp01(q), 1.2f) * 45f;
        }

        private void SetCursor(CursorKind k)
        {
            if (k == cursor) return;
            cursor = k;
            if (k == CursorKind.None) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            else if (k == CursorKind.Hand) Cursor.SetCursor(ApartmentUi.HandCursor, ApartmentUi.HandHotspot, CursorMode.Auto);
            else Cursor.SetCursor(ApartmentUi.GrabCursor, ApartmentUi.GrabHotspot, CursorMode.Auto);
        }

        // =====================================================================
        // Çizim
        // =====================================================================

        private void RenderAll()
        {
            for (int i = 0; i < 12; i++) keyShown[i] = -1;
            Render(0f);
        }

        private void Render(float dt)
        {
            if (!built) return;
            pulseT += dt;

            // tuşlar
            for (int k = 0; k < 12; k++)
            {
                int st = pressedT > 0f && pressedKey == k ? 2 : stage == Stage.Pin && hoverKey == k ? 1 : 0;
                if (keyShown[k] == st) continue;
                keyShown[k] = st;
                int idx = st * 12 + k;
                keyImgs[k].sprite = keySprites != null && idx < keySprites.Length ? keySprites[idx] : null;
            }

            // LCD + LED + durum
            bool ok = stage == Stage.PinOk || stage == Stage.Handle || stage == Stage.Done || (stage == Stage.Closed && PinAccepted);
            string msg = null; Color msgColor = ApartmentUi.LcdMint;
            Color tint = new Color(0f, 0f, 0f, 0f);
            int led = 1;
            if (stage == Stage.PinWrong)
            {
                bool on = Mathf.FloorToInt(stageT * 10f) % 2 == 0;
                msg = data != null ? data.pin_wrong : ""; msgColor = on ? ApartmentUi.LcdRed : new Color(1f, 0.35f, 0.3f, 0.45f);
                tint = new Color(0.9f, 0.1f, 0.08f, on ? 0.45f : 0.2f);
                led = on ? 1 : 0;
            }
            else if (ok)
            {
                led = 2;
                if (stage == Stage.PinOk && stageT < 0.25f) { msg = null; }
                else { msg = data != null ? data.pin_ok : ""; msgColor = ApartmentUi.LcdGreen; }
                tint = new Color(0.2f, 0.9f, 0.4f, 0.16f);
            }
            else if (stage == Stage.Notice)
            {
                bool on = Mathf.FloorToInt(pulseT * 3f) % 2 == 0;
                led = on ? 1 : 0;
            }
            lcdTint.color = tint;
            lcdText.text = msg ?? "";
            lcdText.color = msgColor;
            lcdText.enabled = msg != null;
            for (int i = 0; i < 4; i++)
            {
                lcdGhost[i].enabled = msg == null;
                Sprite sp = null; Color col = ApartmentUi.LcdMint;
                if (msg == null)
                {
                    if (stage == Stage.Notice) { if (Mathf.FloorToInt(pulseT * 3f) % 2 == 0) sp = Lcd(11); }
                    else if (ok) { sp = Lcd(data.pin[i] - '0'); col = ApartmentUi.LcdGreen; }
                    else if (i < entry.Length) sp = Lcd(entry[i] - '0');
                    else if (i == entry.Length && stage == Stage.Pin && Mathf.FloorToInt(pulseT * 2.5f) % 2 == 0) sp = Lcd(12);
                }
                lcdDigit[i].sprite = sp; lcdDigit[i].enabled = sp != null; lcdDigit[i].color = col;
            }
            ledImg.sprite = ledSprites != null && led < ledSprites.Length ? ledSprites[led] : null;

            // sürgü göstergesi: doğru kodla sola kayar
            float boltK = ok ? (stage == Stage.PinOk ? ApartmentUi.OutCubic((stageT - 0.1f) / 0.3f) : 1f) : 0f;
            ApartmentUi.MoveTL(boltRt, Mathf.Lerp(BoltLocked, BoltOpen, boltK), 1f);

            // durum şeridi
            bool showStatus = stage == Stage.PinWrong || stage == Stage.PinOk;
            statusPill.gameObject.SetActive(showStatus);
            if (showStatus)
            {
                statusText.text = stage == Stage.PinWrong ? data.pin_wrong : data.pin_ok;
                statusText.color = stage == Stage.PinWrong ? ApartmentUi.Red : ApartmentUi.Green;
                if (statusText.text != shownStatus)
                {
                    shownStatus = statusText.text;
                    float w = EvenWidth(statusText.preferredWidth + 14f, 60f, 112f);
                    ApartmentUi.ResizeFramed(statusPill, StatusCenterX - w * 0.5f, 30f, w, 20f);
                    ApartmentUi.TL(statusText.rectTransform, 0f, 0f, w, 20f);
                }
            }

            // alt ipucu
            if (stage == Stage.Handle)
            {
                hintText.text = data.handle_hint;
                hintText.color = Color.Lerp(ApartmentUi.Amber, ApartmentUi.Cream, dragging ? 0f : 0.5f + 0.5f * Mathf.Sin(pulseT * 5f));
            }
            else if (stage == Stage.Notice) { hintText.text = data.lock_reset_body + "  " + data.lock_hint_mailbox; hintText.color = ApartmentUi.Cream; }
            else if (stage == Stage.Done) { hintText.text = ""; }
            else if (data != null) { hintText.text = data.pin_hint; hintText.color = jiggleT > 0f ? ApartmentUi.Amber : ApartmentUi.Cream; }
            hintBar.gameObject.SetActive(stage != Stage.Done);
            if (hintText.text != shownHint)
            {
                shownHint = hintText.text;
                float w = EvenWidth(hintText.preferredWidth + 30f, 300f, 470f);
                ApartmentUi.ResizeFramed(hintBar, (FrameW - w) * 0.5f, FrameH + 4f, w, 18f);
                ApartmentUi.TL(hintText.rectTransform, 16f, 0f, w - 18f, 18f);
            }
            mouseImg.enabled = stage == Stage.Pin || stage == Stage.Handle || stage == Stage.PinWrong || stage == Stage.PinOk;

            // kol (önceden çizilmiş kare) + vurgu + titreme
            float angle = VisualAngle(progress);
            if (jiggleT > 0f) angle = jiggleT > 0.09f ? 3f : 0f;
            int frameIdx = Mathf.Clamp(Mathf.RoundToInt(angle / 3f), 0, (handleFrames != null ? handleFrames.Length : 1) - 1);
            if (handleFrames != null && frameIdx < handleFrames.Length) handleImg.sprite = handleFrames[frameIdx];
            float wobble = dragging && progress > 0.66f && progress < 0.74f ? (Mathf.FloorToInt(pulseT * 30f) % 2 == 0 ? 1f : 0f) : 0f;
            ApartmentUi.MoveTL(handleRt, PivotX - HandlePivotInSprite + wobble, PivotY - HandlePivotInSprite);
            bool glow = stage == Stage.Handle;
            glowImg.enabled = glow;
            if (glow)
            {
                if (handleGlowFrames != null && frameIdx < handleGlowFrames.Length) glowImg.sprite = handleGlowFrames[frameIdx];
                ApartmentUi.MoveTL(glowRt, PivotX - HandlePivotInSprite + wobble, PivotY - HandlePivotInSprite);
                float a = dragging ? 0.9f : cursor == CursorKind.Hand ? 1f : 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(pulseT * 5f));
                glowImg.color = new Color(1f, 1f, 1f, a);
            }
            bool arrow = stage == Stage.Handle && !dragging && progress < 0.05f;
            arrowImg.enabled = arrow;
            if (arrow) ApartmentUi.MoveTL(arrowRt, 202f, 122f + Mathf.Floor((0.5f + 0.5f * Mathf.Sin(pulseT * 6f)) * 4f));

            // mandal dili: kol çevrildikçe biraz, başarıda tamamen kapıya çekilir
            float latch = stage == Stage.Done ? Mathf.Lerp(Mathf.Round(0.4f * LatchTravel), LatchTravel, ApartmentUi.OutCubic(doneT / 0.12f)) : Mathf.Round(progress * 0.4f * LatchTravel);
            ApartmentUi.MoveTL(latchRt, -latch, 4f);

            // geri düğmesi: üzerine gelince açık kenar
            bool backHover = IsOpen && PointerInRect(backBtn);
            backEdge.color = backHover ? ApartmentUi.EdgeLight : ApartmentUi.Edge;
            backText.color = backHover ? ApartmentUi.Cream : ApartmentUi.Soft;
        }

        /// <summary>Metne göre kutu genişliği: sınırlar içinde, çift sayı (ortalanınca piksel ızgarasında kalsın).</summary>
        private static float EvenWidth(float w, float min, float max) => Mathf.Round(Mathf.Clamp(Mathf.Ceil(w), min, max) * 0.5f) * 2f;

        private Sprite Lcd(int i) => lcdSprites != null && i >= 0 && i < lcdSprites.Length ? lcdSprites[i] : null;

        private void HideNow()
        {
            ApartmentUi.SetGroup(group, false, 0f);
            root.gameObject.SetActive(false);
        }

        // =====================================================================
        // Kurulum (kodla; sprite'lar prefab'da bağlı)
        // =====================================================================

        private void Build()
        {
            if (built) return;
            built = true;
            data ??= new ApartmentFinalData();
            canvas = ApartmentUi.ResolveCanvas(this, sortingOrder, out scaler);
            Transform host = canvas.transform.IsChildOf(transform) && canvas.transform != transform ? canvas.transform : transform;
            if (host == transform && transform is RectTransform selfRt) ApartmentUi.Stretch(selfRt);
            else if (host == transform) host = canvas.transform;

            root = ApartmentUi.Stretch(ApartmentUi.Rect("LockRoot", host));
            group = root.gameObject.AddComponent<CanvasGroup>();
            var dim = ApartmentUi.Fill("Dim", root, new Color(ApartmentUi.Dim.r, ApartmentUi.Dim.g, ApartmentUi.Dim.b, dimAlpha), true);
            ApartmentUi.Stretch(dim.rectTransform);

            frame = ApartmentUi.Center(ApartmentUi.Rect("Frame", root), 0f, FrameY, FrameW, FrameH);
            ApartmentUi.Sprite("Background", frame, background, 0f, 0f, FrameW, FrameH);

            // mandal dili (kapı kenarının solunda kalan kısmı kırpılır → kapının içine çekilmiş görünür)
            var latchClip = ApartmentUi.TL(ApartmentUi.Rect("LatchClip", frame), LatchClipX, LatchY - 4f, 40f, 17f);
            latchClip.gameObject.AddComponent<RectMask2D>();
            latchRt = ApartmentUi.Sprite("LatchBolt", latchClip, latchBolt, 0f, 4f, 14f, 9f).rectTransform;

            // kilit gövdesi
            var bodyImg = ApartmentUi.Sprite("Body", frame, body, BodyX, BodyY, 90f, 150f);
            var b = bodyImg.rectTransform;
            lcdTint = ApartmentUi.Fill("LcdTint", b, new Color(0f, 0f, 0f, 0f)); ApartmentUi.TL(lcdTint.rectTransform, 11f, 9f, 68f, 18f);
            for (int i = 0; i < 4; i++)
            {
                float x = 21f + i * 13f;
                lcdGhost[i] = ApartmentUi.Sprite("LcdGhost" + i, b, Lcd(10), x, 11f, 8f, 14f); lcdGhost[i].color = ApartmentUi.LcdGhost;
                lcdDigit[i] = ApartmentUi.Sprite("LcdDigit" + i, b, Lcd(8), x, 11f, 8f, 14f);
            }
            lcdText = ApartmentUi.Txt("LcdText", b, font, "", 8, ApartmentUi.LcdMint, TextAnchor.MiddleCenter, 11f, 9f, 68f, 18f);
            ledImg = ApartmentUi.Sprite("Led", b, ledSprites != null && ledSprites.Length > 1 ? ledSprites[1] : null, 70f, 30f, 10f, 10f);
            var boltClip = ApartmentUi.TL(ApartmentUi.Rect("BoltClip", b), BoltTrackX, BoltTrackY, 30f, 7f);
            boltClip.gameObject.AddComponent<RectMask2D>();
            boltRt = ApartmentUi.Sprite("Deadbolt", boltClip, deadbolt, BoltLocked, 1f, 22f, 5f).rectTransform;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 3; c++)
                {
                    int k = r * 3 + c;
                    keyImgs[k] = ApartmentUi.Sprite("Key_" + KeyChars[k], b, keySprites != null && k < keySprites.Length ? keySprites[k] : null,
                        KeyX0 + c * KeyStepX, KeyY0 + r * KeyStepY, KeyW, KeyH);
                }

            // rozet + kol (yalnızca kol döner; vurgu halkası kolun arkasında)
            ApartmentUi.Sprite("Rosette", frame, rosette, PivotX - 13f, PivotY - 13f, 28f, 28f);
            glowImg = ApartmentUi.Sprite("HandleGlow", frame, handleGlowFrames != null && handleGlowFrames.Length > 0 ? handleGlowFrames[0] : null, PivotX - HandlePivotInSprite, PivotY - HandlePivotInSprite, 96f, 80f);
            glowRt = glowImg.rectTransform;
            handleImg = ApartmentUi.Sprite("Handle", frame, handleFrames != null && handleFrames.Length > 0 ? handleFrames[0] : null, PivotX - HandlePivotInSprite, PivotY - HandlePivotInSprite, 96f, 80f);
            handleRt = handleImg.rectTransform;
            arrowImg = ApartmentUi.Sprite("DragArrow", frame, dragArrow, 202f, 122f, 11f, 12f);
            arrowRt = arrowImg.rectTransform;

            // durum şeridi (kapının sağ üstü)
            statusPill = ApartmentUi.Framed("StatusPill", frame, 118f, 30f, 104f, 20f, ApartmentUi.Navy).rectTransform;
            statusText = ApartmentUi.Txt("Text", statusPill, boldFont != null ? boldFont : font, "", 16, ApartmentUi.Green, TextAnchor.MiddleCenter, 0f, 0f, 104f, 20f);

            // başlık + geri düğmesi (çerçevenin üstünde)
            ApartmentUi.Framed("TitleTab", frame, 76f, -20f, 120f, 17f, ApartmentUi.Navy);
            titleText = ApartmentUi.Txt("Title", frame, boldFont != null ? boldFont : font, "", 8, ApartmentUi.Amber, TextAnchor.MiddleCenter, 76f, -20f, 120f, 17f);
            var back = ApartmentUi.Framed("BackButton", frame, FrameW - 70f, -20f, 70f, 17f, ApartmentUi.Navy);
            backBtn = back.rectTransform;
            backEdge = back.transform.Find("Edge").GetComponent<Image>();
            backText = ApartmentUi.Txt("Text", backBtn, font, "", 8, ApartmentUi.Soft, TextAnchor.MiddleCenter, 0f, 0f, 70f, 17f);

            // alt ipucu şeridi
            var hint = hintBar = ApartmentUi.Framed("HintBar", frame, (FrameW - 300f) * 0.5f, FrameH + 4f, 300f, 18f, ApartmentUi.Navy).rectTransform;
            mouseImg = ApartmentUi.Sprite("Mouse", hint, mouseIcon, 5f, 2f, 11f, 15f);
            hintText = ApartmentUi.Txt("Text", hint, font, "", 8, ApartmentUi.Cream, TextAnchor.MiddleCenter, 16f, 0f, 278f, 18f);

            HideNow();
        }

        // =====================================================================
        // QA kancaları (yalnızca editör / geliştirme sürümü)
        // =====================================================================
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA: tuşa fareyle tıklanmış gibi ('0'..'9', '*', '#'). Yalnızca kod aşamasında etkili.</summary>
        public void DebugPress(char key)
        {
            int k = Array.IndexOf(KeyChars, key);
            if (k < 0) { Debug.LogWarning("[FinalUI] DebugPress: bilinmeyen tuş '" + key + "'", this); return; }
            if (stage != Stage.Pin) { Debug.Log("[FinalUI] DebugPress yok sayıldı (aşama " + stage + ")", this); return; }
            PressKey(k);
        }

        /// <summary>
        /// QA: kolu basılı tutup sanal piksel kadar aşağı sürüklemiş gibi. release=false → basılı kalır (sonraki çağrıya kadar);
        /// release=true → aynı karede bırakılır (eşiğe ulaşmadıysa kol yaylanıp döner). Kod kabul edilmeden yok sayılır.
        /// </summary>
        public void DebugDrag(float virtualPixelsDown, bool release)
        {
            if (stage != Stage.Handle || !PinAccepted) { Debug.Log("[FinalUI] DebugDrag yok sayıldı (aşama " + stage + ", kod " + PinAccepted + ")", this); return; }
            dragging = true;
            debugHolding = !release;
            debugDown = virtualPixelsDown;
            SetProgress(Mathf.Clamp01(debugDown / dragPixelsFull));
            if (release && stage == Stage.Handle) Release();
        }

        /// <summary>QA: Esc'e basılmış gibi iptal.</summary>
        public void DebugCancel() { if (IsOpen) debugCancel = true; }
#endif
    }
}
