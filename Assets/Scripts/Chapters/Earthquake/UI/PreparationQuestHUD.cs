using System.Collections.Generic;
using AfetUyanisi.Chapters.Earthquake.Mode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// DEPREM HAZIRLIK HUD'u (sağ üst). Eski CountdownView'ın yerini alır: EmergencyCountdownManager'ın TEK görünümüdür
    /// (CountdownViewBase), yani süre, ceza (-10/-30 uçan yazı + sarsılma + kırmızı parlama), amber/kırmızı uyarı ve
    /// son 10 sn tik kancası buradadır; kendi sayacı yoktur.
    /// Akış: sayaç başlayınca (telefon bildirimi BAŞLA) → A) kısa "DEPREM HAZIRLIK SİMÜLASYONU" uyarısı + alarm sesi
    /// → B) panel sağdan kayarak gelir, satırlar sırayla belirir → C) oyun boyunca görünür.
    /// Liste KISALIR: tamamlanan görev kısa tik + üstü çizilme animasyonundan (removeHoldSeconds ≈ 0,4 sn) sonra söner ve
    /// satırı kapanır; alttaki satırlar piksel adımlarıyla yukarı kayar, panel aynı karede kısalır (üst üste binme yok).
    /// Hepsi bitince yalnızca son satır ("Tüm hazırlıklar tamam!") kalır; son görevin satırı kapanırken o açılır (yükseklik sabit).
    /// Görev durumu PreparationQuestManager'dadır (HUD yalnızca gösterir). Mini oyun açıkken (EarthquakeMinigameHub.Opened)
    /// kompakt moda (süre + sayaç + küçük çubuk) geçer, kapanınca geri açılır. Süre 0'a inince / sarsıntı durum yazısı
    /// gelince görev paneli bir kez kapanır, yerine küçük durum etiketi ("SARSINTI!") gösterilir.
    /// Arayüzü kodla kurar (diğer deprem görünümleri gibi); Canvas: Screen Space Overlay, 640x360, sıra 86.
    /// </summary>
    public class PreparationQuestHUD : CountdownViewBase
    {
        private enum Mode { Hidden, Alarm, Reveal, Shown, Closing }

        [Header("Bağlantılar")]
        [SerializeField] private EarthquakeUISkin skin;
        [Tooltip("Görev durumu. Boşsa sahnede bulunur; yoksa HUD yalnızca süreyi gösterir.")]
        [SerializeField] private PreparationQuestManager quests;
        [SerializeField] private int sortingOrder = 86;

        [Header("Piksel yazı tipi (Türkçe: ç ğ ı İ ö ş ü)")]
        [Tooltip("Assets/Art/UI/Fonts/PixelifySans/PixelifySans-Regular.ttf (OFL). Boşsa skin yazı tipi.")]
        [SerializeField] private Font pixelFont;
        [Tooltip("Başlık ve süre için ayrı kesim (boşsa pixelFont). Pixelify Sans Bold'un üst üste binen konturları Unity'de delik bırakıyor; Regular kullanın.")]
        [SerializeField] private Font pixelFontBold;
        [SerializeField] private int titleSize = 10;
        [SerializeField] private int taskSize = 8;
        [SerializeField] private int timerSize = 12;
        [SerializeField] private int smallSize = 8;

        [Header("Sprite'lar (Assets/Art/UI/Earthquake/QuestHUD)")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite innerSprite;
        [SerializeField] private Sprite rowGlowSprite;
        [SerializeField] private Sprite dotEmpty;
        [SerializeField] private Sprite dotActive;
        [SerializeField] private Sprite dotDone;
        [SerializeField] private Sprite alarmIcon;
        [SerializeField] private Sprite bannerSprite;
        [SerializeField] private Sprite toastSprite;
        [SerializeField] private Sprite barBack;
        [SerializeField] private Sprite barFill;

        [Header("Yerleşim (referans pikseli, 640x360)")]
        [SerializeField] private Vector2 margin = new Vector2(8f, 8f);
        [SerializeField] private float panelWidth = 180f;
        [SerializeField] private float compactWidth = 98f;
        [SerializeField] private float rowHeight = 13f;

        [Header("Metinler")]
        [SerializeField] private string title = "DEPREM HAZIRLIĞI";
        [SerializeField] private string alarmText = "HAZIRLIK BAŞLADI!";
        [SerializeField] private string completeText = "GÖREV TAMAMLANDI!";
        [SerializeField] private string allDoneText = "HAZIRLIK TAMAM!";
        [Tooltip("Bütün görevler bitip listeden çıkınca kalan tek satır")]
        [SerializeField] private string finalLineText = "Tüm hazırlıklar tamam!";
        [SerializeField] private string penaltyFormat = "-{0} SANİYE";

        [Header("Zamanlama (sn, ölçeksiz)")]
        [SerializeField] private float alarmSeconds = 0.5f;
        [SerializeField] private float revealSeconds = 0.35f;
        [SerializeField] private float rowStagger = 0.05f;
        [SerializeField] private float checkPopSeconds = 0.15f;
        [SerializeField] private float rowHighlightSeconds = 0.3f;
        [SerializeField] private float barFillSeconds = 0.35f;
        [SerializeField] private float toastSeconds = 0.7f;
        [SerializeField] private float compactSeconds = 0.25f;
        [SerializeField] private float hideSeconds = 0.3f;
        [SerializeField] private float completionGap = 0.3f;
        [SerializeField] private float dropTweenSeconds = 0.5f;
        [SerializeField] private float penaltyFloatSeconds = 1.4f;
        [Tooltip("Tamamlanan satır: tik + üstü çizilme süresi, ardından sönme ve satırın kapanması (liste kısalır)")]
        [SerializeField] private float removeHoldSeconds = 0.4f;
        [SerializeField] private float removeFadeSeconds = 0.15f;
        [SerializeField] private float removeCollapseSeconds = 0.25f;

        [Header("Süre uyarıları")]
        [SerializeField] private float amberAt = 30f;
        [SerializeField] private float redAt = 10f;

        [Header("Ses (klip yoksa kısa sentez bip)")]
        [SerializeField] private AudioClip alarmClip;
        [SerializeField] private AudioClip completeClip;
        [SerializeField] private AudioClip warningClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField] private bool synthFallback = true;

        [Header("Kancalar")]
        [Tooltip("Son redAt saniyede her tam saniyede (tik sesi). Sistem de tik kancası veriyorsa yalnızca birini bağlayın.")]
        [SerializeField] private UnityEvent onTick = new UnityEvent();
        [SerializeField] private UnityEvent onAlarm = new UnityEvent();
        [SerializeField] private UnityEvent onTaskCompleted = new UnityEvent();

        private const float HeaderH = 33f, ListTop = 35f, ListPad = 5f, CompactH = 34f, NowRowH = 14f;

        // "ŞİMDİ: …" satırı (EarthquakeGuidance.SetNowText): panelde başlığın altında; panel kapalıyken (sarsıntı sonrası) sağ üstte küçük etiket.
        private RectTransform nowRow;
        private CanvasGroup nowRowGroup;
        private Text nowRowText;
        private RectTransform nowPill;
        private CanvasGroup nowPillGroup;
        private Text nowPillText;
        private string nowValue;
        private bool nowStandalone;
        private float nowH, nowPillAlpha;

        private bool built;
        private Font font, fontBold;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panel, panelRoot;
        private CanvasGroup panelGroup;
        private Image alarmImg;
        private Text titleText, timeText, counterText;
        private RectTransform timeRoot, barRoot, barFillRt, listRoot;
        private CanvasGroup titleGroup, listGroup;
        private Image barFillImg;
        private RectTransform banner;
        private CanvasGroup bannerGroup;
        private Image bannerIcon;
        private RectTransform toast;
        private CanvasGroup toastGroup;
        private Text toastTitle, toastBody;
        private RectTransform statusRoot;
        private CanvasGroup statusGroup;
        private Text statusText;
        private AudioSource audioSource;

        private readonly List<PreparationQuestRow> rows = new List<PreparationQuestRow>();
        // Tamamlandı ama çıkış animasyonu liste görünür olunca (kompakt mod / bilgi kartı bitince) başlayacak satırlar.
        private readonly List<PreparationQuestRow> pendingRemoval = new List<PreparationQuestRow>();
        private PreparationQuestRow finalRow;
        private float listWeight;
        private readonly Queue<string> pendingFx = new Queue<string>();
        private readonly HashSet<string> animated = new HashSet<string>();
        private readonly HashSet<string> openMinigames = new HashSet<string>();
        private bool questsBound;

        private Mode mode = Mode.Hidden;
        private float modeT;
        private bool everShown;
        private bool prepOver;
        private string status;
        private float statusAlpha, statusTarget;

        private float target = 60f, shown = 60f, tweenFrom, tweenT = -1f;
        private int dropFrame = -10;
        private float dropFromValue;
        private float shakeT = -1f, flashT = -1f;
        private int lastTickSecond = -1;
        private bool warnedRed;
        private float clock;
        private int floatCount;

        private float compactK, compactTarget;
        private float barShown, barTarget, barFrom, barT = -1f;
        private float fxCooldown;
        private float toastT = -1f;
        private float panelH = 120f;

        /// <summary>Test için okunur durum.</summary>
        public bool IsPanelVisible => mode == Mode.Alarm || mode == Mode.Reveal || mode == Mode.Shown;
        public bool IsCompact => compactTarget > 0.5f;
        public bool PreparationOver => prepOver;
        public int RowCount => rows.Count;
        /// <summary>Listede hâlâ görünen (çıkmamış) görev satırı sayısı.</summary>
        public int VisibleRowCount => rows.FindAll(r => !r.Removed).Count;
        /// <summary>Son satır ("Tüm hazırlıklar tamam!") görünüyor mu.</summary>
        public bool FinalLineShown => finalRow != null && finalRow.Weight >= 0.999f;
        /// <summary>Panelin tam (kompakt olmayan) yüksekliği, referans pikseli.</summary>
        public float PanelHeight => panelH;
        public PreparationQuestManager Quests => quests;

        private void Awake() => EnsureBuilt();

        private void OnEnable()
        {
            EarthquakeMinigameHub.Opened += OnMinigameOpened;
            EarthquakeMinigameHub.Closed += OnMinigameClosed;
            EarthquakeMinigameHub.InterruptRequested += OnInterrupt;
            openMinigames.Clear();
            compactTarget = 0f;
            BindQuests();
        }

        private void OnDisable()
        {
            EarthquakeMinigameHub.Opened -= OnMinigameOpened;
            EarthquakeMinigameHub.Closed -= OnMinigameClosed;
            EarthquakeMinigameHub.InterruptRequested -= OnInterrupt;
            UnbindQuests();
        }

        private void Start() => BindQuests();

        // =====================================================================
        // Kurulum
        // =====================================================================

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = pixelFont != null ? pixelFont : EqUi.ResolveFont(skin);
            fontBold = pixelFontBold != null ? pixelFontBold : font;
            canvas = EqUi.CreateCanvas(this, "EQ_QuestHudCanvas", sortingOrder, out group);
            group.blocksRaycasts = false;
            group.interactable = false;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (synthFallback)
            {
                if (alarmClip == null) alarmClip = Synth("hud_alarm", new[] { 880f, 660f, 880f, 660f }, 0.11f, 0.5f);
                if (completeClip == null) completeClip = Synth("hud_complete", new[] { 784f, 1046f, 1318f }, 0.06f, 0.45f);
                if (warningClip == null) warningClip = Synth("hud_warning", new[] { 1046f, 0f, 1046f }, 0.07f, 0.4f);
            }

            BuildPanel();
            BuildBanner();
            BuildToast();
            BuildStatus();
            BuildNowPill();

            group.alpha = 1f;
            SetPanelVisual(0f, 0f);
            bannerGroup.alpha = 0f;
            toastGroup.alpha = 0f;
            statusGroup.alpha = 0f;
            panelRoot.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            toast.gameObject.SetActive(false);
            statusRoot.gameObject.SetActive(false);
        }

        private Image Sliced(string name, Transform parent, Sprite sprite, Color fallback)
        {
            var img = EqUi.Img(name, parent, sprite, fallback, skin);
            return img;
        }

        private void BuildPanel()
        {
            // panelRoot: sağ üst köşeye bağlı; kayma/sarsılma bunun konumuyla yapılır.
            panelRoot = EqUi.Rect("PreparationHUD", canvas.transform);
            EqUi.Place(panelRoot, 1f, 1f, new Vector2(-margin.x, -margin.y), new Vector2(panelWidth, 120f));
            panelGroup = panelRoot.gameObject.AddComponent<CanvasGroup>();
            panelGroup.blocksRaycasts = false;
            panelRoot.gameObject.AddComponent<RectMask2D>();
            panel = panelRoot;

            var bg = Sliced("Panel", panel, panelSprite, EqUi.NavyDark);
            EqUi.Stretch(bg.rectTransform);
            if (bg.sprite == null) EqUi.Fallback(bg, EqUi.Ink, EqUi.NavyDark, skin);

            // Header (açık mavi iç panel): ikon, başlık, süre, ilerleme
            var header = Sliced("Header", panel, innerSprite, EqUi.Navy);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.offsetMin = new Vector2(3f, -3f - (HeaderH - 5f));
            header.rectTransform.offsetMax = new Vector2(-3f, -3f);

            alarmImg = EqUi.Img("AlarmIcon", panel, alarmIcon, EqUi.Amber, skin);
            alarmImg.type = Image.Type.Simple;
            EqUi.TL(alarmImg.rectTransform, 7f, 6f, 11f, 11f);

            var titleRt = EqUi.Rect("Title", panel);
            EqUi.TL(titleRt, 22f, 4f, 110f, 15f);
            titleGroup = titleRt.gameObject.AddComponent<CanvasGroup>();
            titleText = EqUi.Txt("Text", titleRt, fontBold, titleSize, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.Ink);
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.text = title;

            // Süre (sağa bağlı: kompakt modda yerinde kalır)
            timeRoot = EqUi.Rect("Timer", panel);
            EqUi.Place(timeRoot, 1f, 1f, new Vector2(-6f, -4f), new Vector2(50f, 15f));
            var clockImg = EqUi.Img("Clock", timeRoot, EqUi.S(skin, s => s.ClockIcon), EqUi.Cream, skin);
            clockImg.type = Image.Type.Simple;
            EqUi.TL(clockImg.rectTransform, 0f, 1f, 13f, 13f);
            timeText = EqUi.Txt("Time", timeRoot, fontBold, timerSize, EqUi.Cream, TextAnchor.MiddleRight, EqUi.Ink);
            EqUi.StretchOffsets(timeText.rectTransform, 14f, 0f, 0f, 0f);
            timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            timeText.text = EqUi.Clock(target);

            // İlerleme çubuğu (yatayda esner) + sayaç
            barRoot = EqUi.Rect("ProgressBar", panel);
            barRoot.anchorMin = new Vector2(0f, 1f);
            barRoot.anchorMax = new Vector2(1f, 1f);
            barRoot.pivot = new Vector2(0f, 1f);
            barRoot.offsetMin = new Vector2(7f, -21f - 7f);
            barRoot.offsetMax = new Vector2(-33f, -21f);
            var back = Sliced("Back", barRoot, barBack, EqUi.Ink);
            EqUi.Stretch(back.rectTransform);
            if (back.sprite == null) EqUi.Fallback(back, EqUi.Ink, EqUi.Hex(0x161F36), skin);
            barFillImg = Sliced("Fill", barRoot, barFill, Color.white);
            barFillRt = barFillImg.rectTransform;
            barFillRt.anchorMin = new Vector2(0f, 0f);
            barFillRt.anchorMax = new Vector2(0f, 1f);
            barFillRt.pivot = new Vector2(0f, 0.5f);
            barFillRt.offsetMin = new Vector2(1f, 2f);
            barFillRt.offsetMax = new Vector2(1f, -2f);
            barFillImg.color = EqUi.Green;

            counterText = EqUi.Txt("Counter", panel, font, smallSize, EqUi.Cream, TextAnchor.MiddleRight, EqUi.Ink);
            EqUi.Place(counterText.rectTransform, 1f, 1f, new Vector2(-6f, -18f), new Vector2(26f, 12f));
            counterText.horizontalOverflow = HorizontalWrapMode.Overflow;
            counterText.text = "0 / 0";

            listRoot = EqUi.Rect("TaskList", panel);
            listRoot.anchorMin = new Vector2(0f, 1f);
            listRoot.anchorMax = new Vector2(1f, 1f);
            listRoot.pivot = new Vector2(0.5f, 1f);
            listRoot.offsetMin = new Vector2(4f, -ListTop - 7f * rowHeight);
            listRoot.offsetMax = new Vector2(-4f, -ListTop);
            listGroup = listRoot.gameObject.AddComponent<CanvasGroup>();

            // ŞİMDİ satırı: amber zemin + sol çizgi, tek satır (görev listesinin üstünde)
            nowRow = EqUi.Rect("NowRow", panel);
            EqUi.TopBand(nowRow, 4f, 4f, ListTop, NowRowH - 1f);
            nowRowGroup = nowRow.gameObject.AddComponent<CanvasGroup>();
            nowRowGroup.blocksRaycasts = false;
            var nowBg = EqUi.Img("Bg", nowRow, null, EqUi.A(EqUi.Amber, 0.22f), skin);
            EqUi.Stretch(nowBg.rectTransform);
            var nowBar = EqUi.Img("Bar", nowRow, null, EqUi.Amber, skin);
            nowBar.rectTransform.anchorMin = new Vector2(0f, 0f);
            nowBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            nowBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            nowBar.rectTransform.offsetMin = Vector2.zero;
            nowBar.rectTransform.offsetMax = new Vector2(2f, 0f);
            nowRowText = EqUi.Txt("Text", nowRow, font, taskSize, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.Ink);
            EqUi.StretchOffsets(nowRowText.rectTransform, 6f, 0f, 2f, 0f);
            EqUi.BestFit(nowRowText, Mathf.Max(6, taskSize - 2));
            nowRow.gameObject.SetActive(false);
        }

        /// <summary>
        /// Yönlendirme satırı "ŞİMDİ: …" (EarthquakeGuidance). text boşsa satır kalkar. standalone: görev paneli kapalıyken
        /// (sarsıntı sonrası) sağ üstte küçük etiket olarak gösterilsin mi.
        /// </summary>
        public void SetNowText(string text, bool standalone)
        {
            EnsureBuilt();
            nowStandalone = standalone;
            string value = string.IsNullOrEmpty(text) ? null : text;
            if (value == nowValue) return;
            nowValue = value;
            string rich = value == null ? "" : "<color=#" + EqUi.HexOf(EqUi.Gold) + ">ŞİMDİ:</color> " + value;
            nowRowText.text = rich;
            nowPillText.text = rich;
            if (value != null)
            {
                float w = Mathf.Ceil(EqUi.PreferredWidth(nowPillText, rich)) + 14f;
                nowPill.sizeDelta = new Vector2(Mathf.Clamp(w, 60f, 260f), 18f);
            }
            nowH = value != null ? NowRowH : 0f;
            nowRow.gameObject.SetActive(value != null);
            ApplyListLayout();
            RefreshRows();
        }

        /// <summary>Görev panelinin (ya da ŞİMDİ etiketinin) ekran dikdörtgeni (piksel); görünmüyorsa false. Yönlendirme oku bunun altında kalır.</summary>
        public bool TryGetPanelScreenRect(out Rect rect)
        {
            rect = default;
            RectTransform rt = IsPanelVisible && panelGroup != null && panelGroup.alpha > 0.05f ? panelRoot
                : nowPill != null && nowPillAlpha > 0.05f ? nowPill : null;
            if (rt == null) return false;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            rect = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            return true;
        }

        /// <summary>
        /// Görev listesi ŞİMDİ satırının altından başlar; panel yüksekliği görünen satırların toplam payına (listWeight) göre,
        /// tam piksele yuvarlanır (kapanan satırın altındakiler ile panelin alt kenarı aynı adımla kayar).
        /// </summary>
        private void ApplyListLayout()
        {
            if (listRoot == null) return;
            float top = ListTop + nowH;
            listRoot.offsetMin = new Vector2(4f, -top - (rows.Count + 1) * rowHeight);
            listRoot.offsetMax = new Vector2(-4f, -top);
            panelH = top + Mathf.Round(listWeight * rowHeight) + ListPad;
        }

        /// <summary>
        /// Satırları üst üste dizer: her satırın üstü = önceki satırların Weight toplamı × rowHeight (yuvarlanmış). Son satır
        /// (hepsi tamam) yalnızca bütün görevlerin tamamlanma animasyonu oynayınca, kalan satır payını tamamlayacak kadar açılır.
        /// </summary>
        private void LayoutRows()
        {
            if (rows.Count == 0 && finalRow == null) return;
            float acc = 0f;
            foreach (var r in rows)
            {
                if (!r.Removed) PlaceRow(r, acc);
                acc += r.Weight;
            }
            if (finalRow != null)
            {
                bool all = quests != null && quests.TotalCount > 0 && AnimatedDone() >= quests.TotalCount;
                float w = all ? Mathf.Clamp01(1f - acc) : 0f;
                finalRow.SetPresence(w, Mathf.Clamp01((w - 0.5f) / 0.5f));
                if (w > 0f) PlaceRow(finalRow, acc);
                acc += finalRow.Weight;
            }
            if (!Mathf.Approximately(acc, listWeight))
            {
                listWeight = acc;
                ApplyListLayout();
            }
        }

        private void PlaceRow(PreparationQuestRow r, float slot)
        {
            float y = Mathf.Round(slot * rowHeight);
            if (Mathf.Approximately(r.LayoutY, y)) return;
            r.LayoutY = y;
            EqUi.TopBand(r.Rect, 0f, 0f, y, rowHeight - 1f);
        }

        /// <summary>Bekleyen çıkışlar yalnızca liste tam görünürken başlar (kompakt modda / kart açıkken oyuncu animasyonu görmez).</summary>
        private void UpdatePendingRemoval()
        {
            if (pendingRemoval.Count == 0) return;
            if (mode != Mode.Shown || compactTarget > 0.5f || compactK > 0.001f || InfoCardOpen()) return;
            foreach (var r in pendingRemoval) r.BeginRemove(true);
            pendingRemoval.Clear();
        }

        private void BuildNowPill()
        {
            nowPill = EqUi.Rect("NowPill", canvas.transform);
            EqUi.Place(nowPill, 1f, 1f, new Vector2(-margin.x, -margin.y), new Vector2(120f, 18f));
            nowPillGroup = nowPill.gameObject.AddComponent<CanvasGroup>();
            nowPillGroup.blocksRaycasts = false;
            var bg = EqUi.Img("Bg", nowPill, EqUi.S(skin, s => s.TimerPanel), EqUi.Navy, skin);
            EqUi.Stretch(bg.rectTransform);
            if (bg.sprite == null) EqUi.Fallback(bg, EqUi.Ink, EqUi.Navy, skin);
            nowPillText = EqUi.Txt("Text", nowPill, font, taskSize, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.StretchOffsets(nowPillText.rectTransform, 6f, 1f, 6f, 0f);
            nowPillText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nowPillGroup.alpha = 0f;
            nowPill.gameObject.SetActive(false);
        }

        private void UpdateNowPill(float dt)
        {
            if (nowRowGroup != null) nowRowGroup.alpha = listGroup != null ? listGroup.alpha : 1f;
            float want = nowStandalone && nowValue != null && mode == Mode.Hidden && statusTarget <= 0f ? 1f : 0f;
            if (Mathf.Approximately(nowPillAlpha, want)) return;
            nowPillAlpha = Mathf.MoveTowards(nowPillAlpha, want, dt / 0.2f);
            nowPillGroup.alpha = nowPillAlpha;
            nowPill.gameObject.SetActive(nowPillAlpha > 0f);
        }

        private void BuildBanner()
        {
            banner = EqUi.Rect("AlarmBanner", canvas.transform);
            EqUi.Place(banner, 1f, 1f, new Vector2(-margin.x, -margin.y), new Vector2(panelWidth, 22f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            var bg = Sliced("Bg", banner, bannerSprite, EqUi.Amber);
            EqUi.Stretch(bg.rectTransform);
            if (bg.sprite == null) EqUi.Fallback(bg, EqUi.Amber, EqUi.NavyDark, skin);
            bannerIcon = EqUi.Img("Icon", banner, alarmIcon, EqUi.Amber, skin);
            bannerIcon.type = Image.Type.Simple;
            EqUi.TL(bannerIcon.rectTransform, 6f, 5f, 11f, 11f);
            var t = EqUi.Txt("Text", banner, fontBold, smallSize, EqUi.Gold, TextAnchor.MiddleLeft, EqUi.Ink);
            EqUi.StretchOffsets(t.rectTransform, 21f, 1f, 4f, 0f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = alarmText;
        }

        private void BuildToast()
        {
            toast = EqUi.Rect("CompletionToast", canvas.transform);
            EqUi.Place(toast, 1f, 1f, new Vector2(-margin.x, -margin.y - 120f), new Vector2(panelWidth, 26f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            var bg = Sliced("Bg", toast, toastSprite, EqUi.Green);
            EqUi.Stretch(bg.rectTransform);
            if (bg.sprite == null) EqUi.Fallback(bg, EqUi.Ink, EqUi.Green, skin);
            var check = EqUi.Img("Check", toast, dotDone, EqUi.Cream, skin);
            check.type = Image.Type.Simple;
            EqUi.TL(check.rectTransform, 6f, 8f, 9f, 9f);
            toastTitle = EqUi.Txt("Title", toast, fontBold, smallSize, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.ShadowGreen);
            EqUi.TL(toastTitle.rectTransform, 19f, 2f, panelWidth - 24f, 11f);
            toastTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            toastBody = EqUi.Txt("Body", toast, font, smallSize, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.ShadowGreen);
            EqUi.TL(toastBody.rectTransform, 19f, 13f, panelWidth - 24f, 11f);
            toastBody.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void BuildStatus()
        {
            statusRoot = EqUi.Rect("StatusPill", canvas.transform);
            EqUi.Place(statusRoot, 1f, 1f, new Vector2(-margin.x, -margin.y), new Vector2(92f, 22f));
            statusGroup = statusRoot.gameObject.AddComponent<CanvasGroup>();
            statusGroup.blocksRaycasts = false;
            var bg = EqUi.Img("Bg", statusRoot, EqUi.S(skin, s => s.TimerPanel), EqUi.Navy, skin);
            EqUi.Stretch(bg.rectTransform);
            if (bg.sprite == null) EqUi.Fallback(bg, EqUi.Ink, EqUi.Navy, skin);
            var icon = EqUi.Img("Icon", statusRoot, alarmIcon, EqUi.Red, skin);
            icon.type = Image.Type.Simple;
            EqUi.TL(icon.rectTransform, 5f, 5f, 11f, 11f);
            statusText = EqUi.Txt("Text", statusRoot, fontBold, titleSize, EqUi.Red, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.StretchOffsets(statusText.rectTransform, 22f, 3f, 4f, 1f);
            statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // =====================================================================
        // Görev verisi
        // =====================================================================

        private void BindQuests()
        {
            if (questsBound) return;
            if (quests == null) quests = FindAnyObjectByType<PreparationQuestManager>(FindObjectsInactive.Include);
            if (quests == null) return;
            quests.TaskCompleted += OnQuestCompleted;
            quests.Changed += OnQuestsChanged;
            quests.Locked += OnQuestsLocked;
            questsBound = true;
            if (built) OnQuestsChanged();
        }

        private void UnbindQuests()
        {
            if (!questsBound || quests == null) return;
            quests.TaskCompleted -= OnQuestCompleted;
            quests.Changed -= OnQuestsChanged;
            quests.Locked -= OnQuestsLocked;
            questsBound = false;
        }

        /// <summary>Satırları görev tanımlarından kurar (bir kez; eksik tanım/yönetici varsa boş liste).</summary>
        private void EnsureRows()
        {
            if (rows.Count > 0 || quests == null) return;
            var defs = quests.Tasks;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                var row = PreparationQuestRow.Create(listRoot, d.id, font, taskSize, dotEmpty, dotActive, dotDone, rowGlowSprite, skin);
                row.SetTimings(checkPopSeconds, rowHighlightSeconds);
                row.SetRemoveTimings(removeHoldSeconds, removeFadeSeconds, removeCollapseSeconds);
                EqUi.TopBand(row.Rect, 0f, 0f, i * rowHeight, rowHeight - 1f);
                row.SetLabel(d.label, null);
                rows.Add(row);
                // Daha önce tamamlanmış görev listeye hiç girmez (animasyonsuz çıkar).
                if (quests.IsDone(d.id))
                {
                    row.SetState(PreparationQuestRow.State.Completed, false);
                    row.BeginRemove(false);
                    animated.Add(d.id);
                }
            }
            if (defs.Count > 0)
            {
                finalRow = PreparationQuestRow.Create(listRoot, "all_done", font, taskSize, dotEmpty, dotActive, dotDone, rowGlowSprite, skin);
                EqUi.TopBand(finalRow.Rect, 0f, 0f, 0f, rowHeight - 1f);
                finalRow.SetLabel(finalLineText, null);
                finalRow.SetState(PreparationQuestRow.State.Completed, false);
                finalRow.SetPresence(0f, 0f);
            }
            // Piksel bölmeler: her görev için bir hücre
            for (int i = 1; i < defs.Count; i++)
            {
                var div = EqUi.Img("Divider" + i, barRoot, null, EqUi.A(EqUi.Ink, 0.85f), skin);
                var drt = div.rectTransform;
                drt.anchorMin = new Vector2((float)i / defs.Count, 0f);
                drt.anchorMax = new Vector2((float)i / defs.Count, 1f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.offsetMin = new Vector2(-0.5f, 2f);
                drt.offsetMax = new Vector2(0.5f, -2f);
            }
            listWeight = -1f;
            LayoutRows();
            Canvas.ForceUpdateCanvases();
            var fit = new List<PreparationQuestRow>(rows);
            if (finalRow != null) fit.Add(finalRow);
            foreach (var r in fit)
            {
                r.FitLabel(Mathf.Max(6, taskSize - 2));
                if (r.LabelPreferredWidth > r.LabelWidth + 0.5f)
                    Debug.LogWarning($"[EQ-HUD] '{r.TaskId}' satırı panele sığmıyor ({r.LabelPreferredWidth:0} > {r.LabelWidth:0} px)", this);
            }
            RefreshRows();
            barShown = barTarget = Fraction();
            ApplyBar();
        }

        /// <summary>Çubuk oranı: animasyonu oynamış tamamlanmalar (aynı anda gelen iki görev çubuğu iki adımda doldurur).</summary>
        private float Fraction() => quests != null && quests.TotalCount > 0 ? (float)AnimatedDone() / quests.TotalCount : 0f;

        private void OnQuestsChanged()
        {
            if (!built) return;
            EnsureRows();
            RefreshRows();
        }

        private void OnQuestCompleted(string id)
        {
            if (animated.Contains(id)) return;
            if (mode == Mode.Hidden || prepOver)
            {
                // Görünmüyorken tamamlanan görev: animasyonsuz uygula.
                animated.Add(id);
                var r = rows.Find(x => x.TaskId == id);
                if (r != null)
                {
                    r.SetState(PreparationQuestRow.State.Completed, false);
                    r.BeginRemove(false);
                    pendingRemoval.Remove(r);
                }
                barShown = barTarget = Fraction();
                ApplyBar();
                return;
            }
            if (!pendingFx.Contains(id)) pendingFx.Enqueue(id);
        }

        private void OnQuestsLocked()
        {
            if (!prepOver && everShown) BeginClose();
        }

        /// <summary>Satır durumları (tamamlanma animasyonu bekleyenler hariç) + ara ilerleme + sayaç.</summary>
        private void RefreshRows()
        {
            if (quests == null) return;
            string active = quests.ActiveTaskId;
            // Kuyruktaki animasyonlar oynayana kadar "aktif" bir önceki eksik görevde kalabilir; bu sorun değil.
            foreach (var r in rows)
            {
                bool done = quests.IsDone(r.TaskId) && animated.Contains(r.TaskId);
                if (done) { r.SetState(PreparationQuestRow.State.Completed, false); continue; }
                var def = quests.Find(r.TaskId);
                string suffix = null;
                if (def != null && quests.TryGetProgress(r.TaskId, out int cur, out int max) && max > 1)
                    suffix = cur + "/" + max;
                r.SetLabel(def != null ? def.label : r.TaskId, suffix);
                bool isActive = r.TaskId == active && compactTarget < 0.5f;
                r.SetState(isActive ? PreparationQuestRow.State.Active : PreparationQuestRow.State.Incomplete, false);
            }
            counterText.text = AnimatedDone() + " / " + quests.TotalCount;
        }

        private int AnimatedDone()
        {
            int n = 0;
            foreach (var id in animated) if (quests != null && quests.IsDone(id)) n++;
            return n;
        }

        // =====================================================================
        // CountdownViewBase
        // =====================================================================

        public override void SetVisible(bool v)
        {
            EnsureBuilt();
            if (v)
            {
                if (prepOver || mode != Mode.Hidden) return; // alarm iki kez gelirse tekrar oynamaz
                BindQuests();
                EnsureRows();
                everShown = true;
                StartAlarm();
            }
            else
            {
                // Tamamen gizle (sarsıntı sonrası / açılış)
                if (mode != Mode.Hidden) BeginClose();
                statusTarget = 0f;
                if (!isActiveAndEnabled) ForceHidden();
            }
        }

        public override void SetTime(float secondsRemaining)
        {
            EnsureBuilt();
            float v = Mathf.Max(0f, secondsRemaining);
            if (v < target - 1.5f)
            {
                dropFrame = Time.frameCount;
                dropFromValue = shown;
                tweenFrom = shown;
                tweenT = 0f;
            }
            else if (v > target + 1.5f)
            {
                tweenT = -1f;
                shown = v;
                warnedRed = false;
            }
            target = v;
            if (!isActiveAndEnabled)
            {
                shown = v;
                RefreshTimer(0f);
            }
            if (v <= 0f && everShown && !prepOver) BeginClose();
        }

        public override void ShowPenalty(int seconds, string reason)
        {
            EnsureBuilt();
            tweenFrom = Time.frameCount - dropFrame <= 2 ? dropFromValue : shown;
            tweenT = 0f;
            shakeT = 0f;
            flashT = 0f;
            SpawnFloat(seconds, reason);
        }

        public override void SetStatusText(string text)
        {
            EnsureBuilt();
            status = string.IsNullOrEmpty(text) ? null : text;
            if (status != null)
            {
                if (!prepOver && everShown) BeginClose();
                statusText.text = status;
                float w = Mathf.Max(92f, Mathf.Ceil(EqUi.PreferredWidth(statusText, status)) + 32f);
                statusRoot.sizeDelta = new Vector2(w, 22f);
                statusRoot.gameObject.SetActive(true);
                statusTarget = 1f;
            }
            else statusTarget = 0f;
        }

        // =====================================================================
        // Akış
        // =====================================================================

        private void StartAlarm()
        {
            mode = Mode.Alarm;
            modeT = 0f;
            banner.gameObject.SetActive(true);
            bannerGroup.alpha = 0f;
            PlayClip(alarmClip);
            onAlarm.Invoke();
            Debug.Log("[EQ-HUD] alarm → hazırlık HUD'u açılıyor", this);
        }

        private void StartReveal()
        {
            mode = Mode.Reveal;
            modeT = 0f;
            panelRoot.gameObject.SetActive(true);
            foreach (var r in rows) r.SetAlpha(0f);
            if (finalRow != null) finalRow.SetAlpha(0f);
        }

        /// <summary>Görev paneli bir kez kapanır (süre 0, sarsıntı ya da gizle çağrısı).</summary>
        private void BeginClose()
        {
            prepOver = true;
            pendingFx.Clear();
            if (mode == Mode.Hidden || mode == Mode.Closing) return;
            mode = Mode.Closing;
            modeT = 0f;
            Debug.Log("[EQ-HUD] hazırlık bitti → görev paneli kapanıyor", this);
        }

        private void ForceHidden()
        {
            mode = Mode.Hidden;
            panelRoot.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            toast.gameObject.SetActive(false);
            statusAlpha = statusTarget = 0f;
            statusGroup.alpha = 0f;
            statusRoot.gameObject.SetActive(false);
        }

        private void OnMinigameOpened(string id)
        {
            if (!string.IsNullOrEmpty(id)) openMinigames.Add(id); else openMinigames.Add("?");
            compactTarget = 1f;
            RefreshRows();
        }

        private void OnMinigameClosed(string id)
        {
            if (!string.IsNullOrEmpty(id)) openMinigames.Remove(id); else openMinigames.Remove("?");
            compactTarget = openMinigames.Count > 0 ? 1f : 0f;
            RefreshRows();
        }

        private void OnInterrupt()
        {
            openMinigames.Clear();
            compactTarget = 0f;
        }

        // =====================================================================
        // Kare güncellemesi
        // =====================================================================

        private void Update()
        {
            if (!built) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;
            modeT += dt;

            // Satırlar önce: panel yüksekliği (LayoutRows → panelH) aynı karede SetPanelVisual'a girer, satır ile alt kenar birlikte kayar.
            UpdatePendingRemoval();
            foreach (var r in rows) r.Tick(dt, clock);
            if (finalRow != null) finalRow.Tick(dt, clock);
            LayoutRows();
            UpdateMode(dt);
            UpdateCompact(dt);
            UpdateTimer(dt);
            UpdateBar(dt);
            UpdateFx(dt);
            UpdateStatus(dt);
            UpdateNowPill(dt);
        }

        private void UpdateMode(float dt)
        {
            switch (mode)
            {
                case Mode.Alarm:
                {
                    float a = Mathf.Max(0.05f, alarmSeconds);
                    float k = modeT / a;
                    float inK = EqUi.OutCubic(Mathf.Clamp01(modeT / 0.12f));
                    bannerGroup.alpha = inK;
                    banner.anchoredPosition = EqUi.Snap(new Vector2(-margin.x + 24f * (1f - inK), -margin.y));
                    float s = 1f + 0.25f * Mathf.Abs(Mathf.Sin(modeT * Mathf.PI * 4f));
                    bannerIcon.rectTransform.localScale = new Vector3(s, s, 1f);
                    if (k >= 1f) StartReveal();
                    break;
                }
                case Mode.Reveal:
                {
                    float r = Mathf.Max(0.05f, revealSeconds);
                    float k = EqUi.OutCubic(modeT / r);
                    // banner yukarı kayıp söner, panel sağdan gelir
                    bannerGroup.alpha = 1f - Mathf.Clamp01(modeT / (r * 0.7f));
                    if (bannerGroup.alpha <= 0f) banner.gameObject.SetActive(false);
                    SetPanelVisual(k, 1f - k);
                    // başlık ve çubuk, ardından satırlar sırayla
                    titleGroup.alpha = Mathf.Clamp01((modeT - r * 0.2f) / (r * 0.5f)) * (1f - compactK);
                    float rowsStart = r * 0.6f;
                    int vi = 0; // kademe yalnızca listede görünen satırlar arasında
                    foreach (var row in rows)
                    {
                        if (row.Removed) continue;
                        row.SetAlpha(Mathf.Clamp01((modeT - rowsStart - vi * rowStagger) / 0.12f));
                        vi++;
                    }
                    if (finalRow != null) finalRow.SetAlpha(Mathf.Clamp01((modeT - rowsStart - vi * rowStagger) / 0.12f));
                    if (modeT >= rowsStart + rows.Count * rowStagger + 0.12f && modeT >= r)
                    {
                        mode = Mode.Shown;
                        foreach (var row in rows) row.SetAlpha(1f);
                        if (finalRow != null) finalRow.SetAlpha(1f);
                        SetPanelVisual(1f, 0f);
                    }
                    break;
                }
                case Mode.Shown:
                    SetPanelVisual(1f, 0f);
                    titleGroup.alpha = 1f - compactK;
                    break;
                case Mode.Closing:
                {
                    float h = Mathf.Max(0.05f, hideSeconds);
                    float k = EqUi.InCubic(modeT / h);
                    SetPanelVisual(1f - k, k);
                    bannerGroup.alpha = Mathf.Min(bannerGroup.alpha, 1f - k);
                    if (modeT >= h)
                    {
                        mode = Mode.Hidden;
                        panelRoot.gameObject.SetActive(false);
                        banner.gameObject.SetActive(false);
                        toast.gameObject.SetActive(false);
                        toastT = -1f;
                    }
                    break;
                }
            }
            float ap = mode == Mode.Alarm ? 1f + 0.2f * Mathf.Abs(Mathf.Sin(modeT * Mathf.PI * 4f)) : 1f;
            alarmImg.rectTransform.localScale = new Vector3(ap, ap, 1f);
        }

        /// <summary>alpha + sağdan kayma (slide = 0..1, 1 = tamamen dışarıda), sarsılma ve yükseklik.</summary>
        private void SetPanelVisual(float alpha, float slide)
        {
            if (panelGroup == null) return;
            panelGroup.alpha = alpha;
            Vector2 offset = new Vector2((panelWidth + margin.x + 4f) * slide, 0f);
            if (shakeT >= 0f)
            {
                float k = 1f - shakeT / 0.35f;
                if (k > 0f) offset.x += Mathf.Round(Mathf.Sin(shakeT * 70f) * 2f * k);
            }
            panelRoot.anchoredPosition = EqUi.Snap(new Vector2(-margin.x, -margin.y) + offset);
            float e = EqUi.OutCubic(compactK);
            float w = Mathf.Round(Mathf.Lerp(panelWidth, compactWidth, e));
            float h = Mathf.Round(Mathf.Lerp(panelH, CompactH, e));
            panelRoot.sizeDelta = new Vector2(w, h);
        }

        private void UpdateCompact(float dt)
        {
            if (!Mathf.Approximately(compactK, compactTarget))
            {
                compactK = Mathf.MoveTowards(compactK, compactTarget, dt / Mathf.Max(0.05f, compactSeconds));
                listGroup.alpha = 1f - Mathf.Clamp01(compactK * 2f);
                if (mode == Mode.Shown) titleGroup.alpha = 1f - compactK;
            }
        }

        private void UpdateTimer(float dt)
        {
            if (tweenT >= 0f)
            {
                tweenT += dt / Mathf.Max(0.05f, dropTweenSeconds);
                shown = Mathf.Lerp(tweenFrom, target, EqUi.OutCubic(tweenT));
                if (tweenT >= 1f) { tweenT = -1f; shown = target; }
            }
            else shown = target;
            if (shakeT >= 0f)
            {
                shakeT += dt;
                if (shakeT > 0.35f) shakeT = -1f;
            }
            RefreshTimer(dt);
            TickCheck();
        }

        private void RefreshTimer(float dt)
        {
            timeText.text = EqUi.Clock(shown);
            Color c = shown > amberAt ? EqUi.Cream : shown > redAt ? EqUi.Amber : EqUi.Red;
            if (flashT >= 0f)
            {
                flashT += dt;
                float k = 1f - flashT / 0.6f;
                if (k <= 0f) flashT = -1f;
                else c = Color.Lerp(c, EqUi.Red, k);
            }
            timeText.color = c;

            float scale = 1f;
            if (shown > 0f && shown <= redAt + 0.001f)
            {
                float frac = shown - Mathf.Floor(shown);
                float beat = Mathf.Clamp01((frac - 0.7f) / 0.3f);
                scale = 1f + 0.12f * Mathf.Sin(beat * Mathf.PI);
            }
            else if (shown > 0f && shown <= amberAt + 0.001f)
            {
                scale = 1f + 0.04f * (0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 2f));
            }
            timeRoot.localScale = new Vector3(scale, scale, 1f);
        }

        private void TickCheck()
        {
            if (!IsPanelVisible || prepOver) return;
            int s = Mathf.CeilToInt(target - 0.0001f);
            if (s > redAt || s <= 0) { lastTickSecond = s; return; }
            if (!warnedRed)
            {
                warnedRed = true;
                PlayClip(warningClip);
            }
            if (lastTickSecond > s) onTick.Invoke();
            lastTickSecond = s;
        }

        private void UpdateBar(float dt)
        {
            if (barT >= 0f)
            {
                barT += dt / Mathf.Max(0.05f, barFillSeconds);
                barShown = Mathf.Lerp(barFrom, barTarget, EqUi.OutCubic(barT));
                if (barT >= 1f) { barT = -1f; barShown = barTarget; }
                ApplyBar();
            }
        }

        private void ApplyBar()
        {
            if (barRoot == null) return;
            float inner = Mathf.Max(0f, barRoot.rect.width - 2f);
            float w = Mathf.Round(inner * Mathf.Clamp01(barShown));
            barFillRt.offsetMax = new Vector2(1f + w, -2f);
            barFillImg.enabled = w >= 1f;
        }

        private void LateUpdate()
        {
            // Çubuk genişliği kompakt geçişte değişir: dolguyu her karede yeniden oranla.
            if (built && (compactK > 0f && compactK < 1f)) ApplyBar();
        }

        private InfoCardView infoCards;

        // İnceleme kartı açıkken "GÖREV TAMAMLANDI!" toast'ı kartın üstüne çıkmasın: kart kapanınca oynar.
        private bool InfoCardOpen()
        {
            if (infoCards == null) infoCards = FindAnyObjectByType<InfoCardView>(FindObjectsInactive.Include);
            return infoCards != null && infoCards.IsCardOpen;
        }

        private void UpdateFx(float dt)
        {
            if (fxCooldown > 0f) fxCooldown -= dt;
            if (pendingFx.Count > 0 && fxCooldown <= 0f && (mode == Mode.Shown || mode == Mode.Reveal) && !InfoCardOpen())
            {
                string id = pendingFx.Dequeue();
                PlayCompletion(id);
                fxCooldown = completionGap;
            }

            if (toastT >= 0f)
            {
                toastT += dt;
                float inS = 0.12f, outS = 0.2f, hold = Mathf.Max(0.1f, toastSeconds);
                float a = toastT < inS ? toastT / inS : toastT < inS + hold ? 1f : 1f - (toastT - inS - hold) / outS;
                toastGroup.alpha = Mathf.Clamp01(a);
                float y = -margin.y - (Mathf.Round(panelRoot.sizeDelta.y) + 3f);
                float pop = toastT < inS ? Mathf.Lerp(0.85f, 1f, EqUi.OutBack(toastT / inS)) : 1f;
                toast.anchoredPosition = EqUi.Snap(new Vector2(-margin.x, y + (toastT < inS ? 4f * (1f - toastT / inS) : 0f)));
                toast.localScale = new Vector3(pop, pop, 1f);
                if (a <= 0f && toastT > inS)
                {
                    toastT = -1f;
                    toast.gameObject.SetActive(false);
                }
            }
        }

        private void PlayCompletion(string id)
        {
            if (!animated.Add(id)) return;
            var row = rows.Find(r => r.TaskId == id);
            if (row != null)
            {
                row.SetState(PreparationQuestRow.State.Completed, true);
                if (!pendingRemoval.Contains(row)) pendingRemoval.Add(row); // tik + çizgi + kapanma, liste görünür olunca
            }
            barFrom = barShown;
            barTarget = Fraction();
            barT = 0f;
            RefreshRows();
            PlayClip(completeClip);
            onTaskCompleted.Invoke();

            bool all = quests != null && quests.DoneCount >= quests.TotalCount;
            toastTitle.text = all ? allDoneText : completeText;
            var def = quests != null ? quests.Find(id) : null;
            toastBody.text = def != null ? def.label : id;
            toast.gameObject.SetActive(true);
            toastT = 0f;
        }

        private void UpdateStatus(float dt)
        {
            if (!Mathf.Approximately(statusAlpha, statusTarget))
            {
                statusAlpha = Mathf.MoveTowards(statusAlpha, statusTarget, dt / 0.2f);
                statusGroup.alpha = statusAlpha;
                if (statusAlpha <= 0f) statusRoot.gameObject.SetActive(false);
            }
            if (status != null)
            {
                float s = 1f + 0.05f * Mathf.Sin(clock * Mathf.PI * 2f * 0.8f);
                statusText.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        // =====================================================================
        // Ceza uçan yazısı, ses
        // =====================================================================

        private void SpawnFloat(int seconds, string reason)
        {
            var root = EqUi.Rect("Penalty", canvas.transform);
            float panelW = IsPanelVisible ? Mathf.Round(panelRoot.sizeDelta.x) : 92f;
            float y = -margin.y - 2f - (floatCount % 2) * 7f;
            floatCount++;
            EqUi.Place(root, 1f, 1f, new Vector2(-margin.x - panelW - 4f, y), new Vector2(150f, 26f));
            var big = EqUi.Txt("Amount", root, fontBold, smallSize + 1, EqUi.Red, TextAnchor.UpperRight, EqUi.Ink);
            big.text = string.Format(penaltyFormat, Mathf.Abs(seconds));
            if (!string.IsNullOrEmpty(reason))
            {
                var small = EqUi.Txt("Reason", root, font, Mathf.Max(6, smallSize - 1), EqUi.Cream, TextAnchor.UpperRight, EqUi.Ink);
                EqUi.StretchOffsets(small.rectTransform, 0f, 0f, 0f, 12f);
                small.text = reason;
            }
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            if (isActiveAndEnabled) StartCoroutine(Float(root, cg));
            else Destroy(root.gameObject, penaltyFloatSeconds);
        }

        private System.Collections.IEnumerator Float(RectTransform rt, CanvasGroup cg)
        {
            Vector2 start = rt.anchoredPosition;
            float dur = Mathf.Max(0.3f, penaltyFloatSeconds);
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                rt.anchoredPosition = EqUi.Snap(start + new Vector2(0f, 10f * EqUi.OutCubic(k)));
                float s = k < 0.12f ? Mathf.Lerp(0.85f, 1f, EqUi.OutBack(k / 0.12f)) : 1f;
                rt.localScale = new Vector3(s, s, 1f);
                cg.alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || audioSource == null || volume <= 0f) return;
            audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>Kısa 8-bit bip dizisi (ses dosyası yokken yedek). freqs: her nota (0 = sessizlik).</summary>
        private static AudioClip Synth(string name, float[] freqs, float noteSeconds, float gain)
        {
            const int rate = 22050;
            int per = Mathf.Max(1, Mathf.RoundToInt(noteSeconds * rate));
            var data = new float[per * freqs.Length];
            for (int n = 0; n < freqs.Length; n++)
            {
                float f = freqs[n];
                if (f <= 0f) continue;
                for (int i = 0; i < per; i++)
                {
                    float t = (float)i / rate;
                    float env = Mathf.Min(1f, i / (rate * 0.004f)) * Mathf.Clamp01(1f - (float)i / per) ;
                    float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
                    float sn = Mathf.Sin(2f * Mathf.PI * f * t);
                    data[n * per + i] = gain * env * (0.35f * sq + 0.65f * sn);
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
