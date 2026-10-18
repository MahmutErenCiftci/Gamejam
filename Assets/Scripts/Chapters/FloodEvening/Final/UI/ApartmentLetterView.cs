using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Apartman yönetiminin mektubu — yakın plan (prefab: Assets/Prefabs/FloodEvening/FE_ApartmentLockUi.prefab → "Letter").
    /// Show(data): zarf açılır (~0,3 sn: kapak kalkar, kâğıt zarftan çıkar), eski kâğıt üzerinde başlık, iki satır metin ve
    /// data.pin'in 4 büyük rakamı görünür; dünya hafifçe kararır. E / Space / Enter / sol tık (ayrıca Esc / sağ tık) ile kapanır.
    /// Açan tuş sayılmaz (bir kare beklenir, tuşa yeniden basılması gerekir). Kapanınca bir kare daha bekleyip döner.
    /// Hub: açılışta NotifyOpened("read_letter"), kapanışta NotifyClosed("read_letter"). Süre gerçek zamanla işler, timeScale'e dokunmaz.
    /// Arayüz kodla kurulur (WindHud gibi); sprite'lar ve font prefab kurucusunda bağlanır. Gizliyken tıklamayı engellemez.
    /// </summary>
    public class ApartmentLetterView : MonoBehaviour, IFloodEveningResettable
    {
        public const string HubId = "read_letter";

        [Header("Sprite'lar (Assets/Art/FloodEvening/Final/UI)")]
        [SerializeField] private Sprite paper;
        [SerializeField] private Sprite envelopeBack, envelopeFront, flapClosed, flapOpen;
        [Tooltip("Büyük rakamlar 0..9 (UI_FE_Final_LetterDigit_0..9)")]
        [SerializeField] private Sprite[] digits = new Sprite[10];
        [Tooltip("Pixelify Sans (boşsa FeUi.DefaultFont)")]
        [SerializeField] private Font font;
        [SerializeField] private Font boldFont;
        [Tooltip("Yalnızca üstte ölçekli prefab tuvali yoksa kurulan kendi tuvalinin sırası")]
        [SerializeField] private int sortingOrder = ApartmentUi.DefaultSortingOrder;

        [Header("Ayarlar")]
        [Tooltip("Dünyayı karartma (proje doğrusal renk uzayında: düşük değerler gözle daha zayıf görünür)")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.55f;
        [SerializeField, Min(0.1f)] private float openSeconds = 0.32f;
        [SerializeField, Min(0.05f)] private float closeSeconds = 0.15f;

        public bool IsOpen { get; private set; }

        // yerleşim (sanal piksel, ekran ortasına göre; çizim: tools/sel_aksam_final/ui/build_ui_art.js LETTER / ENVL)
        private const float PaperW = 180f, PaperH = 204f, PaperY = 8f;
        private const float EnvW = 196f, EnvH = 112f, EnvPocket = 60f, FlapH = 60f, EnvY = -66f;
        private const float ClipOpenBottom = -1000f;
        private const float DigitW = 16f, DigitH = 24f, DigitX0 = 46f, DigitStep = 26f, DigitY = 79f;

        private bool built, running, closeRequested;
        private int session;
        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform root, clip, paperRt, envBackRt, envFrontRt, pill;
        private CanvasGroup group, envBackGroup, envFrontGroup, pillGroup;
        private Image flapClosedImg, flapOpenImg;
        private readonly Image[] digitImgs = new Image[4];
        private Text headerText, body1Text, body2Text, closeText;

        private void Awake() => Build();
        private void OnEnable() => FloodEveningHub.ResetRequested += ResetView;
        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetView;
            session++; running = false; IsOpen = false;
            if (built) HideNow();
        }

        public void ResetModule() => ResetView();

        /// <summary>Tam sıfırlama: görünüm gizli, açık oturum varsa kapanır (Hub'a kapandı bildirilir).</summary>
        public void ResetView()
        {
            session++;
            running = false; closeRequested = false;
            bool wasOpen = IsOpen;
            IsOpen = false;
            if (built) HideNow();
            if (wasOpen) FloodEveningHub.NotifyClosed(HubId);
        }

        /// <summary>Mektubu gösterir; oyuncu kapatana kadar sürer. Aynı anda ikinci çağrı hemen döner.</summary>
        public IEnumerator Show(ApartmentFinalData data)
        {
            if (running) yield break;
            running = true; closeRequested = false;
            int my = ++session;
            data ??= new ApartmentFinalData();
            if (!ApartmentFinalData.IsValidPin(data.pin)) data.Validate();
            Build();
            headerText.text = data.letter_header;
            body1Text.text = data.letter_body1;
            body2Text.text = data.letter_body2;
            closeText.text = data.letter_close;
            var pin = data.PinDigits;
            for (int i = 0; i < 4; i++) { digitImgs[i].sprite = digits != null && pin[i] < digits.Length ? digits[pin[i]] : null; digitImgs[i].enabled = digitImgs[i].sprite != null; }

            root.gameObject.SetActive(true);
            ApartmentUi.SetGroup(group, true, 0f);
            IsOpen = true;
            FloodEveningHub.NotifyOpened(HubId);
            GameAudio.PlaySfx("SFX_Final_Paper");

            float t = 0f;
            while (t < openSeconds)
            {
                if (my != session) yield break;
                ApartmentUi.ApplyScale(scaler);
                ApplyOpen(t);
                yield return null;
                t += ApartmentUi.AnimDelta();
            }
            ApplyOpen(openSeconds + 1f);
            yield return null;                                   // açan tuş bu kapanışı tetiklemesin

            float pulse = 0f;
            while (true)
            {
                if (my != session) yield break;
                ApartmentUi.ApplyScale(scaler);
                pulse += Time.unscaledDeltaTime;
                pillGroup.alpha = Mathf.Min(1f, pillGroup.alpha + Time.unscaledDeltaTime / 0.2f);
                closeText.color = Color.Lerp(ApartmentUi.Amber, ApartmentUi.Cream, 0.5f + 0.5f * Mathf.Sin(pulse * 5f));
                if (closeRequested || GameInput.ContinuePressed || GameInput.BackPressed || GameInput.RightClickPressed) break;
                yield return null;
            }

            GameAudio.PlaySfx("SFX_Final_Paper", 0.6f);
            t = 0f;
            while (t < closeSeconds)
            {
                if (my != session) yield break;
                float k = t / closeSeconds;
                group.alpha = 1f - k;
                PlacePaper(PaperY - 16f * ApartmentUi.InCubic(k), ClipOpenBottom);
                yield return null;
                t += ApartmentUi.AnimDelta();
            }
            if (my != session) yield break;
            HideNow();
            IsOpen = false;
            FloodEveningHub.NotifyClosed(HubId);
            yield return null;                                   // kapatan tuş başka bir etkileşimi başlatmasın
            if (my == session) running = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA: mektubu tuşa basılmış gibi kapatır (yalnızca editör / geliştirme sürümü).</summary>
        public void DebugClose() { if (IsOpen) closeRequested = true; }
#endif

        // ------------------------------------------------------------------ animasyon
        /// <summary>Açılış: 0–0,08 zarf belirir, 0,10 kapak açılır, 0,12–0,30 kâğıt yükselir, 0,18'den sonra zarf aşağı çekilip kaybolur.</summary>
        private void ApplyOpen(float t)
        {
            float appear = Mathf.Clamp01(t / 0.08f);
            float away = Mathf.Clamp01((t - 0.18f) / Mathf.Max(0.05f, openSeconds - 0.18f));
            group.alpha = appear;
            float envY = Mathf.Lerp(EnvY - 18f, EnvY, ApartmentUi.OutCubic(appear)) - 150f * ApartmentUi.InCubic(away);
            ApartmentUi.Center(envBackRt, 0f, envY, EnvW, EnvH);
            ApartmentUi.Center(envFrontRt, 0f, envY, EnvW, EnvH);
            envBackGroup.alpha = envFrontGroup.alpha = 1f - away;
            bool open = t >= 0.10f;
            flapClosedImg.enabled = !open; flapOpenImg.enabled = open;

            // kâğıt: zarfın içinden (üst kenarı cebin çizgisinin altında) son konumuna
            float pocket = Mathf.Round(envY + EnvH * 0.5f - EnvPocket);
            float startY = EnvY + EnvH * 0.5f - EnvPocket - PaperH * 0.5f - 2f;
            float rise = ApartmentUi.OutCubic(Mathf.Clamp01((t - 0.12f) / 0.18f));
            float paperY = Mathf.Lerp(startY, PaperY, rise);
            PlacePaper(paperY, t >= openSeconds ? ClipOpenBottom : pocket);
            if (t < openSeconds) pillGroup.alpha = 0f;
        }

        /// <summary>Kâğıdı ekran ortasına göre paperY'ye koyar; kırpma kutusunun alt kenarı clipBottom (kâğıt kutunun çocuğu).</summary>
        private void PlacePaper(float paperY, float clipBottom)
        {
            const float clipTop = 600f;
            float center = Mathf.Round((clipBottom + clipTop) * 0.5f);
            ApartmentUi.Center(clip, 0f, center, 2000f, clipTop - clipBottom);
            ApartmentUi.Center(paperRt, 0f, paperY - center, PaperW, PaperH);
        }

        private void HideNow()
        {
            ApartmentUi.SetGroup(group, false, 0f);
            pillGroup.alpha = 0f;
            root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ kurulum
        private void Build()
        {
            if (built) return;
            built = true;
            canvas = ApartmentUi.ResolveCanvas(this, sortingOrder, out scaler);
            Transform host = canvas.transform.IsChildOf(transform) && canvas.transform != transform ? canvas.transform : transform;
            if (host == transform && transform is RectTransform selfRt) ApartmentUi.Stretch(selfRt);
            else if (host == transform) host = canvas.transform;

            root = ApartmentUi.Stretch(ApartmentUi.Rect("LetterRoot", host));
            group = root.gameObject.AddComponent<CanvasGroup>();
            var dim = ApartmentUi.Fill("Dim", root, new Color(ApartmentUi.Dim.r, ApartmentUi.Dim.g, ApartmentUi.Dim.b, dimAlpha), true);
            ApartmentUi.Stretch(dim.rectTransform);

            // zarfın arka yüzü + açık kapak (kâğıdın arkasında)
            envBackRt = ApartmentUi.Center(ApartmentUi.Rect("EnvelopeBack", root), 0f, EnvY, EnvW, EnvH);
            envBackGroup = envBackRt.gameObject.AddComponent<CanvasGroup>();
            ApartmentUi.Sprite("Back", envBackRt, envelopeBack, 0f, 0f, EnvW, EnvH);
            flapOpenImg = ApartmentUi.Sprite("FlapOpen", envBackRt, flapOpen, 0f, -FlapH, EnvW, FlapH);

            // kâğıt (zarftan çıkarken cep çizgisinin altı kırpılır)
            clip = ApartmentUi.Rect("Clip", root);
            clip.gameObject.AddComponent<RectMask2D>();
            paperRt = ApartmentUi.Center(ApartmentUi.Rect("Paper", clip), 0f, 0f, PaperW, PaperH);
            var pImg = paperRt.gameObject.AddComponent<Image>(); pImg.sprite = paper; pImg.raycastTarget = false;
            if (paper == null) pImg.color = ApartmentUi.Cream;
            var bold = boldFont != null ? boldFont : font;
            headerText = ApartmentUi.Txt("Header", paperRt, bold, "", 8, ApartmentUi.InkBlue, TextAnchor.MiddleLeft, 30f, 8f, 140f, 16f);
            body1Text = ApartmentUi.Txt("Body1", paperRt, font, "", 8, ApartmentUi.Ink, TextAnchor.MiddleCenter, 12f, 34f, 156f, 32f, true);
            for (int i = 0; i < 4; i++)
                digitImgs[i] = ApartmentUi.Sprite("Digit" + i, paperRt, digits != null && digits.Length > 0 ? digits[0] : null, DigitX0 + i * DigitStep, DigitY, DigitW, DigitH);
            body2Text = ApartmentUi.Txt("Body2", paperRt, font, "", 8, ApartmentUi.Ink, TextAnchor.MiddleCenter, 12f, 114f, 156f, 24f, true);

            // zarfın ön cebi + kapalı kapak (kâğıdın önünde)
            envFrontRt = ApartmentUi.Center(ApartmentUi.Rect("EnvelopeFront", root), 0f, EnvY, EnvW, EnvH);
            envFrontGroup = envFrontRt.gameObject.AddComponent<CanvasGroup>();
            ApartmentUi.Sprite("Front", envFrontRt, envelopeFront, 0f, 0f, EnvW, EnvH);
            flapClosedImg = ApartmentUi.Sprite("FlapClosed", envFrontRt, flapClosed, 0f, 0f, EnvW, FlapH);

            // "[E] Kapat" — kâğıdın altında lacivert şerit
            pill = ApartmentUi.Center(ApartmentUi.Rect("ClosePill", root), 0f, PaperY - PaperH * 0.5f - 13f, 96f, 18f);
            pillGroup = pill.gameObject.AddComponent<CanvasGroup>();
            ApartmentUi.Framed("Frame", pill, 0f, 0f, 96f, 18f, ApartmentUi.Navy);
            closeText = ApartmentUi.Txt("Text", pill, font, "", 8, ApartmentUi.Amber, TextAnchor.MiddleCenter, 0f, 0f, 96f, 18f);

            HideNow();
        }
    }
}
