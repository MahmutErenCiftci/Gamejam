using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// KİTAPLIK SABİTLEME mini oyununun yakın plan arayüzü (kodla kurulan uGUI, 640x360 referans, diğer deprem View'larıyla
    /// aynı EqUi/EarthquakeUISkin düzeni). Akışı BookshelfAnchorMinigame yönetir; bu sınıf yalnızca çizer, canlandırır ve
    /// fare tıklamalarını olay olarak bildirir (klavye kısayollarını kontrolcü GameInput ile okur).
    /// Yerleşim: üstte başlık + adım çipleri, solda sahne (duvar + zemin + 2x kitaplık), sağda adım paneli
    /// (malzeme kartları / bağlantı ilerlemesi / test sonucu), altta tuş ipucu. Sağ üst köşe (sayaç) boş bırakılır;
    /// Canvas sırası sayacın (86) altındadır, sayaç mini oyun açıkken de görünür.
    /// Sprite atanmasa da aynı yerleşimle düz renklerle çalışır.
    /// </summary>
    public class BookshelfAnchorView : MonoBehaviour
    {
        /// <summary>Test sonucu görünümü.</summary>
        public enum ResultKind { Success, Incomplete, WrongMaterial }

        [Header("Görünüm")]
        [Tooltip("Boşsa sahnedeki EarthquakeUISkin (font, panel ve buton sprite'ları)")]
        [SerializeField] private EarthquakeUISkin skin;
        [Tooltip("Canvas sırası. Sayaç (86) bunun ÜSTÜNDE kalmalı ki mini oyun sırasında süre görünsün.")]
        [SerializeField] private int sortingOrder = 84;
        [Tooltip("Karartma yoğunluğu (0..1)")]
        [SerializeField, Range(0f, 1f)] private float dimAmount = 0.78f;

        [Header("Sahne sprite'ları (Assets/Art/UI/Earthquake/Bookshelf)")]
        [SerializeField] private Sprite stage;
        [SerializeField] private Sprite closeup;
        [SerializeField] private Sprite wobble;
        [SerializeField] private Sprite[] fallingBooks = Array.Empty<Sprite>();
        [SerializeField] private Sprite portrait;

        [Header("Malzemeler")]
        [Tooltip("Takılı parça: 0 = duvar ankrajı + kayış, 1 = raf vidası, 2 = dekoratif bant")]
        [SerializeField] private Sprite[] installed = new Sprite[3];
        [Tooltip("Kart ikonları (aynı sıra)")]
        [SerializeField] private Sprite[] icons = new Sprite[3];

        [Header("Alet ve noktalar")]
        [SerializeField] private Sprite driverA;
        [SerializeField] private Sprite driverB;
        [SerializeField] private Sprite pointA;
        [SerializeField] private Sprite pointB;
        [SerializeField] private Sprite ring;
        [SerializeField] private Sprite speck;

        [Header("Rozetler ve kartlar")]
        [SerializeField] private Sprite badgeOk;
        [SerializeField] private Sprite badgeWarn;
        [SerializeField] private Sprite badgeFail;
        [SerializeField] private Sprite cardNormal;
        [SerializeField] private Sprite cardHover;
        [SerializeField] private Sprite cardSelected;
        [SerializeField] private Sprite cardWrong;
        [SerializeField] private Sprite cardRight;

        // ---- Olaylar (fare) ----
        public event Action<int> CardClicked;
        public event Action SelectClicked;
        public event Action TestClicked;
        public event Action ChangeMaterialClicked;
        public event Action ResultClicked;
        public event Action ContinueClicked;

        // ---- Yerleşim sabitleri (referans pikseli, sol-üst orijin) ----
        private const float StageX = 22f, StageY = 40f, StageW = 336f, StageH = 280f;
        private const float PanelX = 372f, PanelY = 40f, PanelW = 246f, PanelH = 284f;
        private const float ArtScale = 2f;
        // Kitaplık yakın plan sprite'ı (72x120) sahnede: sol-üst sahne-sanatı koordinatı (48, 6), taban orta = (84, 126)
        private static readonly Vector2 RigBase = new Vector2(84f * ArtScale, 126f * ArtScale);
        private static readonly Vector2 ShelfArtSize = new Vector2(72f, 120f);
        private static readonly Vector2 ShelfPivotArt = new Vector2(36f, 120f);
        // Bağlantı noktaları (yakın plan sanat koordinatı): üst yüzün arka kenarı, duvarla birleşim
        private static readonly Vector2[] PointArt = { new Vector2(20f, 13f), new Vector2(52f, 13f) };
        private static readonly Vector2[] HardwareArt = { new Vector2(20f, 21f), new Vector2(52f, 21f) };
        // Kitap sıraları (sanat y'si: raf üstü) — düşen kitapların çıkış noktası
        private static readonly float[] BookRowsArt = { 46f, 68f, 90f, 110f };

        private bool built;
        private Font font;
        private Canvas canvas;
        private CanvasGroup group;
        private Image dim;
        private RectTransform header, stageFrame, stageRoot, rig, panel;
        private Image shelfImage;
        private readonly Image[] stepDots = new Image[4];
        private readonly Text[] stepLabels = new Text[4];
        private Text panelTitle;
        private Image wobbleLeft, wobbleRight;

        // Diyalog
        private RectTransform dialogue;
        private Text dialogueText, dialogueHint;

        // Malzeme adımı
        private RectTransform chooseRoot;
        private readonly Image[] cards = new Image[3];
        private readonly EqButton[] cardButtons = new EqButton[3];
        private readonly Text[] cardNames = new Text[3];
        private readonly Text[] cardTags = new Text[3];
        private readonly Image[] cardMarks = new Image[3];
        private readonly int[] cardVerdict = { 0, 0, 0 }; // 0 bilinmiyor, 1 doğru, -1 yanlış
        private int selectedIndex = -1;
        private Image selectButton;
        private EqButton selectEq;

        // Bağlantı adımı
        private RectTransform installRoot;
        private Text progressText, materialText, installHint;
        private Image materialIcon;
        private Image testButton;
        private EqButton testEq;
        private Text testLabel;
        private Image changeButton;
        private EqButton changeEq;
        private readonly Image[] points = new Image[2];
        private readonly EqHoldButton[] pointHolds = new EqHoldButton[2];
        private readonly Text[] pointNumbers = new Text[2];
        private readonly Image[] hardware = new Image[2];
        private Image ringImage, driverImage;
        private int activePoint = -1;

        // Test / sonuç
        private RectTransform testRoot, resultRoot;
        private Text testText;
        private Image resultPill;
        private Text resultLabel, resultEdu, resultButtonLabel;
        private Image resultButton;
        private EqButton resultEq;
        private Image stageBadge;
        private RectTransform stageBand;
        private Text stageBandText;
        private Image stagePill;
        private Text stagePillText;

        private Text footer;
        private readonly List<Image> bookPool = new List<Image>();
        private readonly List<Image> speckPool = new List<Image>();
        private Coroutine wobbleLines;
        private float animTime;

        public bool IsOpen { get; private set; }

        /// <summary>Etkin noktanın üstünde fare basılı mı?</summary>
        public bool PointerHoldingActivePoint => activePoint >= 0 && activePoint < 2 && pointHolds[activePoint] != null && pointHolds[activePoint].Held;

        private void Awake() => EnsureBuilt();

        // =====================================================================
        // Kurulum
        // =====================================================================

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_BookshelfCanvas", sortingOrder, out group);
            dim = EqUi.DimImage(canvas.transform, skin);

            BuildHeader();
            BuildStage();
            BuildPanel();

            footer = EqUi.Txt("Footer", canvas.transform, font, 7, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.Ink);
            EqUi.TL(footer.rectTransform, StageX, 330f, 596f, 16f);
            footer.text = "<color=#" + EqUi.HexOf(EqUi.Gold) + ">[Esc]</color> Çık    ·    <color=#" + EqUi.HexOf(EqUi.Amber) + ">Sayaç durmuyor!</color>";

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        private void BuildHeader()
        {
            var img = EqUi.Img("Header", canvas.transform, EqUi.S(skin, s => s.Button), EqUi.Navy, skin);
            if (img.sprite == null) EqUi.Fallback(img, EqUi.Ink, EqUi.Navy, skin);
            header = img.rectTransform;
            EqUi.TL(header, StageX, 8f, 450f, 26f);
            var title = EqUi.Txt("Title", header, font, 11, EqUi.Gold, TextAnchor.MiddleLeft, EqUi.Ink);
            EqUi.TL(title.rectTransform, 10f, 2f, 160f, 21f);
            title.text = "KİTAPLIĞI SABİTLE";
            string[] names = { "Keşfet", "Malzeme", "Sabitle", "Test" };
            for (int i = 0; i < 4; i++)
            {
                stepDots[i] = EqUi.Img("StepDot" + i, header, EqUi.S(skin, s => s.StepDotOff), EqUi.Muted, skin);
                EqUi.TL(stepDots[i].rectTransform, 172f + i * 68f, 8f, 10f, 10f);
                stepLabels[i] = EqUi.Txt("Step" + i, header, font, 7, EqUi.CreamShade, TextAnchor.MiddleLeft);
                EqUi.TL(stepLabels[i].rectTransform, 185f + i * 68f, 5f, 54f, 16f);
                stepLabels[i].text = (i + 1) + " " + names[i];
            }
        }

        private void BuildStage()
        {
            var frame = EqUi.Img("StageFrame", canvas.transform, null, EqUi.Ink, skin);
            stageFrame = frame.rectTransform;
            EqUi.TL(stageFrame, StageX - 2f, StageY - 2f, StageW + 4f, StageH + 4f);
            var st = EqUi.Img("Stage", stageFrame, stage, EqUi.Hex(0x1F2C52), skin);
            st.preserveAspect = false;
            stageRoot = st.rectTransform;
            EqUi.TL(stageRoot, 2f, 2f, StageW, StageH);
            stageRoot.gameObject.AddComponent<RectMask2D>();
            if (stage == null)
            {
                var floor = EqUi.Img("Floor", stageRoot, null, EqUi.Hex(0x6A472B), skin);
                EqUi.TL(floor.rectTransform, 0f, 248f, StageW, 32f);
            }

            // Kitaplık düzeneği (taban-orta pivot): kitaplık + takılı parçalar birlikte döner/kayar
            rig = EqUi.Rect("ShelfRig", stageRoot);
            rig.anchorMin = rig.anchorMax = new Vector2(0f, 1f);
            rig.pivot = new Vector2(0.5f, 0f);
            rig.sizeDelta = ShelfArtSize * ArtScale;
            rig.anchoredPosition = new Vector2(RigBase.x, -RigBase.y);
            shelfImage = EqUi.Img("Shelf", rig, closeup, EqUi.Hex(0x7A4A2A), skin);
            shelfImage.preserveAspect = false;
            EqUi.Stretch(shelfImage.rectTransform);
            if (closeup == null)
            {
                var inner = EqUi.Img("Inner", shelfImage.transform, null, EqUi.Hex(0x3A2414), skin);
                EqUi.StretchOffsets(inner.rectTransform, 10f, 18f, 18f, 46f);
            }

            for (int i = 0; i < 2; i++)
            {
                hardware[i] = EqUi.Img("Hardware" + i, rig, null, EqUi.Hex(0x9AA3B5), skin);
                hardware[i].gameObject.SetActive(false);
            }

            // Sallanma çizgileri (sahnede, dönmez)
            wobbleLeft = EqUi.Img("WobbleL", stageRoot, wobble, EqUi.White, skin);
            wobbleRight = EqUi.Img("WobbleR", stageRoot, wobble, EqUi.White, skin);
            PlaceArt(wobbleLeft.rectTransform, new Vector2(RigBase.x - 92f, RigBase.y - 150f), new Vector2(7f, 18f) * ArtScale);
            PlaceArt(wobbleRight.rectTransform, new Vector2(RigBase.x + 92f, RigBase.y - 150f), new Vector2(7f, 18f) * ArtScale);
            wobbleRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            wobbleLeft.rectTransform.localScale = new Vector3(1f, 1f, 1f);
            SetWobbleLines(false, EqUi.White);

            // Bağlantı noktaları + alet + halka
            ringImage = EqUi.Img("HoldRing", stageRoot, ring, EqUi.Gold, skin);
            ringImage.type = Image.Type.Filled;
            ringImage.fillMethod = Image.FillMethod.Radial360;
            ringImage.fillOrigin = (int)Image.Origin360.Top;
            ringImage.fillClockwise = true;
            ringImage.fillAmount = 0f;
            ringImage.preserveAspect = false;
            ringImage.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++)
            {
                points[i] = EqUi.Img("Point" + (i + 1), stageRoot, pointA, EqUi.A(EqUi.White, 0.8f), skin, true);
                points[i].preserveAspect = false;
                PlaceArt(points[i].rectTransform, ArtToStage(PointArt[i]), new Vector2(20f, 20f) * ArtScale);
                pointHolds[i] = points[i].gameObject.AddComponent<EqHoldButton>();
                pointNumbers[i] = EqUi.Txt("Number", points[i].transform, font, 9, EqUi.Gold, TextAnchor.MiddleCenter, EqUi.Ink);
                EqUi.TL(pointNumbers[i].rectTransform, 12f, -16f, 16f, 14f);
                pointNumbers[i].text = (i + 1).ToString();
                points[i].gameObject.SetActive(false);
            }
            driverImage = EqUi.Img("Driver", stageRoot, driverA, EqUi.Red, skin);
            driverImage.preserveAspect = false;
            var drt = driverImage.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 1f);
            drt.pivot = new Vector2(2.5f / 26f, 1f - 2.5f / 26f); // uç
            drt.sizeDelta = new Vector2(26f, 26f) * ArtScale;
            driverImage.gameObject.SetActive(false);

            // Sonuç katmanları (sahne üstü)
            stagePill = EqUi.Img("ResultPill", stageRoot, null, EqUi.Green, skin);
            if (stagePill.sprite == null) EqUi.Fallback(stagePill, EqUi.Ink, EqUi.Green, skin);
            EqUi.TL(stagePill.rectTransform, 8f, 8f, 120f, 20f);
            stagePillText = EqUi.Txt("Label", stagePill.transform, font, 9, EqUi.White, TextAnchor.MiddleCenter, EqUi.Ink);
            stagePill.gameObject.SetActive(false);
            stageBadge = EqUi.Img("ResultBadge", stageRoot, badgeOk, EqUi.Green, skin);
            stageBadge.preserveAspect = false;
            PlaceArt(stageBadge.rectTransform, new Vector2(StageW - 34f, 34f), new Vector2(26f, 26f) * ArtScale);
            stageBadge.gameObject.SetActive(false);
            var band = EqUi.Img("ResultBand", stageRoot, null, EqUi.A(EqUi.Ink, 0.86f), skin);
            stageBand = band.rectTransform;
            EqUi.TL(stageBand, 8f, StageH - 46f, StageW - 16f, 38f);
            stageBandText = EqUi.Txt("Text", stageBand, font, 9, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.StretchOffsets(stageBandText.rectTransform, 6f, 2f, 6f, 2f);
            EqUi.BestFit(stageBandText, 7);
            stageBand.gameObject.SetActive(false);

            BuildDialogue();
        }

        private void BuildDialogue()
        {
            dialogue = EqUi.Rect("Dialogue", stageRoot);
            EqUi.TL(dialogue, 8f, StageH - 84f, StageW - 16f, 76f);
            var frame = EqUi.Img("PortraitFrame", dialogue, EqUi.S(skin, s => s.PortraitFrame), EqUi.Navy, skin);
            if (frame.sprite == null) EqUi.Fallback(frame, EqUi.Ink, EqUi.Navy, skin);
            EqUi.TL(frame.rectTransform, 0f, 4f, 68f, 68f);
            var face = EqUi.Img("Portrait", frame.transform, portrait, EqUi.Amber, skin);
            face.preserveAspect = false;
            EqUi.Stretch(face.rectTransform, 2f);
            var bubbleImg = EqUi.Img("Bubble", dialogue, null, EqUi.Cream, skin, true);
            if (bubbleImg.sprite == null) EqUi.Fallback(bubbleImg, EqUi.Ink, EqUi.Cream, skin);
            EqUi.TL(bubbleImg.rectTransform, 74f, 6f, StageW - 16f - 74f, 64f);
            var eq = bubbleImg.gameObject.AddComponent<EqButton>();
            eq.Init();
            eq.Clicked += () => ContinueClicked?.Invoke();
            var who = EqUi.Txt("Name", bubbleImg.transform, font, 7, EqUi.AmberDark, TextAnchor.UpperLeft);
            EqUi.TL(who.rectTransform, 10f, 5f, 120f, 10f);
            who.text = "CENK";
            dialogueText = EqUi.Txt("Line", bubbleImg.transform, font, 10, EqUi.Navy, TextAnchor.UpperLeft);
            EqUi.TL(dialogueText.rectTransform, 10f, 16f, StageW - 16f - 74f - 20f, 36f);
            EqUi.BestFit(dialogueText, 8);
            dialogueHint = EqUi.Txt("Hint", bubbleImg.transform, font, 6, EqUi.Muted, TextAnchor.LowerRight);
            EqUi.TL(dialogueHint.rectTransform, 24f, 50f, StageW - 16f - 74f - 30f, 10f);
            dialogueHint.text = "[E] / tık: devam";
            dialogue.gameObject.SetActive(false);
        }

        private void BuildPanel()
        {
            var img = EqUi.Img("Panel", canvas.transform, EqUi.S(skin, s => s.InfoCard), EqUi.Cream, skin);
            if (img.sprite == null) EqUi.Fallback(img, EqUi.Ink, EqUi.Cream, skin, 1f, 20f, EqUi.Navy);
            panel = img.rectTransform;
            EqUi.TL(panel, PanelX, PanelY, PanelW, PanelH);
            panelTitle = EqUi.Txt("Title", panel, font, 9, EqUi.Cream, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.TopBand(panelTitle.rectTransform, 8f, 8f, 2f, 17f);

            // --- Malzeme seçimi ---
            chooseRoot = EqUi.Rect("Choose", panel);
            EqUi.Stretch(chooseRoot);
            var sub = EqUi.Txt("Sub", chooseRoot, font, 8, EqUi.Navy, TextAnchor.MiddleCenter);
            EqUi.TL(sub.rectTransform, 8f, 26f, PanelW - 16f, 14f);
            sub.text = "Uygun sabitleme yöntemini seç.";
            string[] names = { "Duvar ankrajı +\nsabitleme kayışı", "Yalnızca\nraf vidası", "Dekoratif\nbant" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                cards[i] = EqUi.Img("Card" + i, chooseRoot, cardNormal, EqUi.Navy, skin, true);
                if (cards[i].sprite == null) EqUi.Fallback(cards[i], EqUi.NavyLight, EqUi.Navy, skin);
                EqUi.TL(cards[i].rectTransform, 5f + i * 80f, 44f, 76f, 130f);
                var icon = EqUi.Img("Icon", cards[i].transform, icons != null && icons.Length > i ? icons[i] : null, EqUi.Hex(0x9AA3B5), skin);
                icon.preserveAspect = false;
                EqUi.TL(icon.rectTransform, 2f, 10f, 72f, 60f);
                cardNames[i] = EqUi.Txt("Name", cards[i].transform, font, 7, EqUi.Cream, TextAnchor.UpperCenter, EqUi.Ink);
                EqUi.TL(cardNames[i].rectTransform, 3f, 76f, 70f, 28f);
                cardNames[i].text = names[i];
                EqUi.BestFit(cardNames[i], 6);
                cardTags[i] = EqUi.Txt("Tag", cards[i].transform, font, 7, EqUi.CreamShade, TextAnchor.MiddleCenter);
                EqUi.TL(cardTags[i].rectTransform, 3f, 106f, 70f, 14f);
                var key = EqUi.KeyCap(cards[i].transform, (i + 1).ToString(), font, skin, 7);
                EqUi.TL(key, 5f, 5f, 13f, 12f);
                cardMarks[i] = EqUi.Img("Mark", cards[i].transform, null, EqUi.Green, skin);
                EqUi.TL(cardMarks[i].rectTransform, 58f, 4f, 14f, 14f);
                cardMarks[i].gameObject.SetActive(false);
                cardButtons[i] = cards[i].gameObject.AddComponent<EqButton>();
                cardButtons[i].Init();
                cardButtons[i].Clicked += () => CardClicked?.Invoke(index);
                cardButtons[i].HoverChanged += _ => RefreshCards();
            }
            var adult = EqUi.Txt("Adult", chooseRoot, font, 7, EqUi.Muted, TextAnchor.MiddleCenter);
            EqUi.TL(adult.rectTransform, 8f, 180f, PanelW - 16f, 26f);
            adult.text = "Kitaplık depremden ÖNCE sabitlenir.\nBu işi bir büyüğünle birlikte yap.";
            selectButton = MakeButton(chooseRoot, "Select", "Seç", 11, true, out selectEq, out _);
            EqUi.TL(selectButton.rectTransform, (PanelW - 110f) / 2f, 214f, 110f, 28f);
            selectEq.Clicked += () => SelectClicked?.Invoke();
            var keys = EqUi.Txt("Keys", chooseRoot, font, 6, EqUi.Muted, TextAnchor.MiddleCenter);
            EqUi.TL(keys.rectTransform, 8f, 250f, PanelW - 16f, 12f);
            keys.text = "[1] [2] [3] seç   ·   [Enter] onayla";

            // --- Bağlantılar ---
            installRoot = EqUi.Rect("Install", panel);
            EqUi.Stretch(installRoot);
            var isub = EqUi.Txt("Sub", installRoot, font, 8, EqUi.Navy, TextAnchor.MiddleCenter);
            EqUi.TL(isub.rectTransform, 10f, 26f, PanelW - 20f, 26f);
            isub.text = "Kitaplığın üst kısmındaki iki bağlantıyı sırayla yap.";
            progressText = EqUi.Txt("Progress", installRoot, font, 20, EqUi.Navy, TextAnchor.MiddleCenter);
            EqUi.TL(progressText.rectTransform, 10f, 56f, PanelW - 20f, 30f);
            materialIcon = EqUi.Img("MaterialIcon", installRoot, null, EqUi.Hex(0x9AA3B5), skin);
            materialIcon.preserveAspect = false;
            EqUi.TL(materialIcon.rectTransform, 14f, 94f, 36f, 30f);
            materialText = EqUi.Txt("Material", installRoot, font, 7, EqUi.Navy, TextAnchor.MiddleLeft);
            EqUi.TL(materialText.rectTransform, 56f, 94f, PanelW - 66f, 30f);
            EqUi.BestFit(materialText, 6);
            installHint = EqUi.Txt("Hint", installRoot, font, 7, EqUi.Muted, TextAnchor.MiddleCenter);
            EqUi.TL(installHint.rectTransform, 10f, 132f, PanelW - 20f, 40f);
            installHint.text = "Sıradaki noktanın üstünde fareyi BASILI TUT\nya da [E] tuşunu basılı tut.";
            testButton = MakeButton(installRoot, "Test", "TEST ET", 12, true, out testEq, out testLabel);
            EqUi.TL(testButton.rectTransform, (PanelW - 140f) / 2f, 196f, 140f, 30f);
            testEq.Clicked += () => TestClicked?.Invoke();
            changeButton = MakeButton(installRoot, "Change", "Malzemeyi değiştir", 7, false, out changeEq, out _);
            EqUi.TL(changeButton.rectTransform, (PanelW - 120f) / 2f, 234f, 120f, 18f);
            changeEq.Clicked += () => ChangeMaterialClicked?.Invoke();
            var ikeys = EqUi.Txt("Keys", installRoot, font, 6, EqUi.Muted, TextAnchor.MiddleCenter);
            EqUi.TL(ikeys.rectTransform, 8f, 258f, PanelW - 16f, 12f);
            ikeys.text = "[E] basılı tut: bağla   ·   [Enter] test et";

            // --- Test sürüyor ---
            testRoot = EqUi.Rect("Testing", panel);
            EqUi.Stretch(testRoot);
            testText = EqUi.Txt("Text", testRoot, font, 9, EqUi.Navy, TextAnchor.MiddleCenter);
            EqUi.TL(testText.rectTransform, 10f, 90f, PanelW - 20f, 60f);
            testText.text = "Sağlamlık testi:\nkitaplık yatay yönde sallanıyor...";

            // --- Sonuç ---
            resultRoot = EqUi.Rect("Result", panel);
            EqUi.Stretch(resultRoot);
            resultPill = EqUi.Img("Pill", resultRoot, null, EqUi.Green, skin);
            if (resultPill.sprite == null) EqUi.Fallback(resultPill, EqUi.Ink, EqUi.Green, skin);
            EqUi.TL(resultPill.rectTransform, (PanelW - 150f) / 2f, 32f, 150f, 22f);
            resultLabel = EqUi.Txt("Label", resultPill.transform, font, 10, EqUi.White, TextAnchor.MiddleCenter, EqUi.Ink);
            resultEdu = EqUi.Txt("Edu", resultRoot, font, 8, EqUi.Navy, TextAnchor.UpperCenter);
            EqUi.TL(resultEdu.rectTransform, 12f, 66f, PanelW - 24f, 120f);
            EqUi.BestFit(resultEdu, 6);
            resultButton = MakeButton(resultRoot, "Action", "Tamam", 10, true, out resultEq, out resultButtonLabel);
            EqUi.TL(resultButton.rectTransform, (PanelW - 160f) / 2f, 210f, 160f, 28f);
            resultEq.Clicked += () => ResultClicked?.Invoke();
            var rkeys = EqUi.Txt("Keys", resultRoot, font, 6, EqUi.Muted, TextAnchor.MiddleCenter);
            EqUi.TL(rkeys.rectTransform, 8f, 250f, PanelW - 16f, 12f);
            rkeys.text = "[Enter] / [E] devam";

            ShowPanel(null);
        }

        private Image MakeButton(Transform parent, string name, string label, int size, bool primary, out EqButton eq, out Text text)
        {
            Sprite normal = primary ? EqUi.S(skin, s => s.ButtonStart) : EqUi.S(skin, s => s.Button);
            Sprite hover = primary ? EqUi.S(skin, s => s.ButtonStartHover) : EqUi.S(skin, s => s.ButtonHover);
            var img = EqUi.Img(name, parent, normal, primary ? EqUi.Green : EqUi.Navy, skin, true);
            if (img.sprite == null) EqUi.Fallback(img, EqUi.Ink, primary ? EqUi.Green : EqUi.Navy, skin);
            text = EqUi.Txt("Label", img.transform, font, size, EqUi.White, TextAnchor.MiddleCenter, EqUi.Ink);
            EqUi.StretchOffsets(text.rectTransform, 2f, 2f, 2f, 0f);
            text.text = label;
            eq = img.gameObject.AddComponent<EqButton>();
            eq.Init();
            var captured = img;
            eq.HoverChanged += h =>
            {
                if (normal == null) return;
                captured.sprite = h && hover != null ? hover : normal;
            };
            return img;
        }

        // =====================================================================
        // Açma / kapama
        // =====================================================================

        /// <summary>Arayüzü açar (karartma + panellerin girişi).</summary>
        public IEnumerator Open(float seconds)
        {
            EnsureBuilt();
            IsOpen = true;
            canvas.gameObject.SetActive(true);
            ResetStageFx();
            group.alpha = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!IsOpen) yield break;
                float k = EqUi.OutCubic(t / seconds);
                group.alpha = k;
                EqUi.SetDim(dim, k * dimAmount);
                header.anchoredPosition = EqUi.Snap(new Vector2(StageX, -8f + (1f - k) * 12f));
                panel.anchoredPosition = EqUi.Snap(new Vector2(PanelX + (1f - k) * 24f, -PanelY));
                float s = Mathf.Lerp(0.94f, 1f, EqUi.OutBack(t / seconds));
                stageFrame.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            group.alpha = 1f;
            EqUi.SetDim(dim, dimAmount);
            header.anchoredPosition = new Vector2(StageX, -8f);
            panel.anchoredPosition = new Vector2(PanelX, -PanelY);
            stageFrame.localScale = Vector3.one;
        }

        /// <summary>Yumuşak kapanış.</summary>
        public IEnumerator Close(float seconds)
        {
            if (!IsOpen) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!IsOpen) yield break;
                float k = 1f - EqUi.InCubic(t / seconds);
                group.alpha = k;
                EqUi.SetDim(dim, k * dimAmount);
                yield return null;
            }
            CloseImmediate();
        }

        /// <summary>Hemen kapatır (sayaç bitti / Esc).</summary>
        public void CloseImmediate()
        {
            EnsureBuilt();
            StopAllCoroutines();
            wobbleLines = null;
            IsOpen = false;
            group.alpha = 0f;
            EqUi.SetDim(dim, 0f);
            SetActivePoint(-1, 0);
            canvas.gameObject.SetActive(false);
        }

        /// <summary>Başlıktaki adım çipleri (0 Keşfet, 1 Malzeme, 2 Sabitle, 3 Test).</summary>
        public void SetStep(int step)
        {
            for (int i = 0; i < 4; i++)
            {
                bool done = i < step, on = i == step;
                var sprite = done ? EqUi.S(skin, s => s.StepDotDone) : on ? EqUi.S(skin, s => s.StepDotOn) : EqUi.S(skin, s => s.StepDotOff);
                EqUi.Skin(stepDots[i], sprite, done ? EqUi.Green : on ? EqUi.Amber : EqUi.Muted, skin);
                stepLabels[i].color = on ? EqUi.Gold : done ? EqUi.Hex(0x9FE0B5) : EqUi.CreamShade;
            }
        }

        private void ShowPanel(RectTransform root)
        {
            chooseRoot.gameObject.SetActive(root == chooseRoot);
            installRoot.gameObject.SetActive(root == installRoot);
            testRoot.gameObject.SetActive(root == testRoot);
            resultRoot.gameObject.SetActive(root == resultRoot);
        }

        // =====================================================================
        // 1–2. Keşif
        // =====================================================================

        public void ShowDiscover(string line, string info)
        {
            ShowPanel(testRoot);
            panelTitle.text = "TEHLİKEYİ KEŞFET";
            testText.text = info;
            dialogue.gameObject.SetActive(true);
            dialogueText.text = line;
            StartCoroutine(Pop(dialogue, 0.2f));
        }

        public void HideDiscover() => dialogue.gameObject.SetActive(false);

        /// <summary>Hafif sallanma (keşif): azalan açıyla salınır.</summary>
        public IEnumerator Wobble(float seconds, float degrees)
        {
            SetWobbleLines(true, EqUi.White);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = 1f - t / seconds;
                rig.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f * 3.2f) * degrees * k);
                yield return null;
            }
            rig.localRotation = Quaternion.identity;
            SetWobbleLines(false, EqUi.White);
        }

        // =====================================================================
        // 3. Malzeme
        // =====================================================================

        public void ShowMaterials(int selected)
        {
            dialogue.gameObject.SetActive(false);
            ClearResultOverlay();
            ShowPanel(chooseRoot);
            panelTitle.text = "SABİTLEME MALZEMESİ";
            selectedIndex = selected;
            RefreshCards();
        }

        /// <summary>Kartın sonucunu işaretler (test sonrası açığa çıkar): 1 doğru, -1 yanlış, 0 bilinmiyor.</summary>
        public void SetCardVerdict(int index, int verdict)
        {
            if (index < 0 || index > 2) return;
            cardVerdict[index] = verdict;
            RefreshCards();
        }

        public void SetCardSelected(int index)
        {
            bool changed = index != selectedIndex;
            selectedIndex = index;
            RefreshCards();
            if (changed && index >= 0 && index < 3) StartCoroutine(Pulse(cards[index].rectTransform, 0.16f, 0.08f));
        }

        private void RefreshCards()
        {
            string[] tags = { "(Doğru seçenek)", "(Yeterli değil)", "(Uygun değil)" };
            for (int i = 0; i < 3; i++)
            {
                if (cards[i] == null) continue;
                Sprite s; Color fb;
                if (cardVerdict[i] < 0) { s = cardWrong; fb = EqUi.Red; }
                else if (cardVerdict[i] > 0) { s = cardRight; fb = EqUi.Green; }
                else if (i == selectedIndex) { s = cardSelected; fb = EqUi.Green; }
                else if (cardButtons[i] != null && cardButtons[i].Hovered) { s = cardHover; fb = EqUi.Gold; }
                else { s = cardNormal; fb = EqUi.NavyLight; }
                if (i == selectedIndex && cardVerdict[i] == 0) { s = cardSelected; fb = EqUi.Green; }
                if (s != null) EqUi.Skin(cards[i], s, fb, skin);
                else EqUi.FallbackColors(cards[i], fb, EqUi.Navy);
                cardTags[i].text = cardVerdict[i] != 0 ? tags[i] : (i == selectedIndex ? "Seçildi" : "");
                cardTags[i].color = cardVerdict[i] > 0 ? EqUi.Hex(0x9FE0B5) : cardVerdict[i] < 0 ? EqUi.Hex(0xF5A9A1) : EqUi.Gold;
                cardMarks[i].gameObject.SetActive(cardVerdict[i] != 0);
                if (cardVerdict[i] != 0)
                    EqUi.Skin(cardMarks[i], cardVerdict[i] > 0 ? EqUi.S(skin, k => k.Check) : EqUi.S(skin, k => k.Cross), cardVerdict[i] > 0 ? EqUi.Green : EqUi.Red, skin);
            }
            if (selectButton != null)
            {
                var c = selectButton.color;
                c.a = selectedIndex >= 0 ? 1f : 0.45f;
                selectButton.color = c;
                selectEq.Interactable = selectedIndex >= 0;
            }
        }

        // =====================================================================
        // 4. Bağlantılar
        // =====================================================================

        public void ShowInstall(int material, int done, string materialName)
        {
            dialogue.gameObject.SetActive(false);
            ClearResultOverlay();
            ShowPanel(installRoot);
            panelTitle.text = "SABİTLEME NOKTALARI";
            EqUi.Skin(materialIcon, IconOf(material), EqUi.Hex(0x9AA3B5), skin);
            materialIcon.preserveAspect = false;
            materialText.text = "Seçilen: " + materialName;
            SetInstallProgress(done);
        }

        public void SetInstallProgress(int done)
        {
            progressText.text = done + " / 2";
            bool can = done >= 1;
            var c = testButton.color;
            c.a = can ? 1f : 0.45f;
            testButton.color = c;
            testEq.Interactable = can;
            testLabel.text = "TEST ET";
            installHint.text = done >= 2
                ? "İki bağlantı tamam! Şimdi sağlamlığı test et."
                : "Sıradaki noktanın üstünde fareyi BASILI TUT\nya da [E] tuşunu basılı tut.";
        }

        /// <summary>Etkin noktayı gösterir (index -1: hiçbiri). done: tamamlanan nokta sayısı (onlar gizli).</summary>
        public void SetActivePoint(int index, int done)
        {
            activePoint = index;
            for (int i = 0; i < 2; i++)
            {
                bool show = index >= 0 && i >= done;
                points[i].gameObject.SetActive(show);
                if (show)
                {
                    points[i].color = i == index ? EqUi.White : EqUi.A(EqUi.White, 0.35f);
                    pointNumbers[i].color = i == index ? EqUi.Gold : EqUi.A(EqUi.CreamShade, 0.6f);
                }
            }
            ringImage.gameObject.SetActive(index >= 0);
            driverImage.gameObject.SetActive(index >= 0);
            if (index >= 0)
            {
                PlaceArt(ringImage.rectTransform, ArtToStage(PointArt[index]), new Vector2(24f, 24f) * ArtScale);
                ringImage.fillAmount = 0f;
                PlaceDriver(index, 0f, false);
            }
        }

        /// <summary>Basılı tutma ilerlemesi (0..1) ve aletin titremesi.</summary>
        public void SetHoldProgress(float k, bool holding)
        {
            if (activePoint < 0) return;
            ringImage.fillAmount = Mathf.Clamp01(k);
            ringImage.color = holding ? EqUi.Gold : EqUi.A(EqUi.Gold, 0.6f);
            PlaceDriver(activePoint, k, holding);
        }

        private void PlaceDriver(int index, float k, bool holding)
        {
            Vector2 p = ArtToStage(PointArt[index]);
            // Uç noktaya yaklaşır; basılıyken 2 px titrer ve kare değişir (dönüş hissi)
            Vector2 offset = new Vector2(14f, -14f) * (1f - Mathf.Clamp01(k * 1.5f));
            if (holding) offset += new Vector2(Mathf.Round(Mathf.Sin(Time.time * 60f)) * 2f, 0f);
            driverImage.rectTransform.anchoredPosition = EqUi.Snap(new Vector2(p.x, -p.y) + offset);
            bool frameB = holding && Mathf.Repeat(Time.time, 0.12f) > 0.06f;
            var sprite = frameB ? driverB : driverA;
            if (sprite != null) driverImage.sprite = sprite;
            if (index >= 0) points[index].sprite = Mathf.Repeat(Time.time, 0.5f) > 0.25f && pointB != null ? pointB : (pointA != null ? pointA : points[index].sprite);
        }

        /// <summary>Takılan parçayı gösterir (küçük zıplama + toz).</summary>
        public void PlaceHardware(int index, int material, bool animate)
        {
            if (index < 0 || index > 1) return;
            var img = hardware[index];
            var sprite = InstalledOf(material);
            EqUi.Skin(img, sprite, EqUi.Hex(0x9AA3B5), skin);
            img.preserveAspect = false;
            Vector2 size = sprite != null ? new Vector2(sprite.rect.width, sprite.rect.height) * ArtScale : new Vector2(16f, 24f);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            bool bottomPivot = material != 1;
            rt.pivot = new Vector2(0.5f, bottomPivot ? 0f : 0.5f);
            rt.sizeDelta = size;
            Vector2 art = material == 1 ? new Vector2(HardwareArt[index].x, 18f) : HardwareArt[index];
            rt.anchoredPosition = EqUi.Snap(new Vector2((art.x - ShelfPivotArt.x) * ArtScale, (ShelfPivotArt.y - art.y) * ArtScale));
            rt.localRotation = Quaternion.identity;
            img.color = Color.white;
            img.gameObject.SetActive(true);
            if (animate)
            {
                StartCoroutine(PopScale(rt, 0.2f));
                Vector2 at = ArtToStage(PointArt[index]);
                Specks(at, 5, EqUi.CreamShade);
            }
        }

        public void ClearHardware()
        {
            for (int i = 0; i < 2; i++) hardware[i].gameObject.SetActive(false);
        }

        // =====================================================================
        // 5. Test
        // =====================================================================

        public void ShowTesting()
        {
            ShowPanel(testRoot);
            panelTitle.text = "SAĞLAMLIK TESTİ";
            testText.text = "Sağlamlık testi:\nkitaplık yatay yönde sallanıyor...";
            SetActivePoint(-1, 0);
        }

        /// <summary>Sarsma testi animasyonu (sonuca göre).</summary>
        public IEnumerator PlayTest(ResultKind kind)
        {
            const float duration = 1.6f;
            Color lines = kind == ResultKind.Success ? EqUi.Green : kind == ResultKind.Incomplete ? EqUi.Amber : EqUi.Red;
            SetWobbleLines(true, lines);
            int dropped = 0;
            bool hardwareOff = false;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float wave = Mathf.Sin(t * Mathf.PI * 2f * 4.5f);
                float amp, rot;
                switch (kind)
                {
                    case ResultKind.Success:
                        amp = 4f; rot = wave * 0.6f;
                        break;
                    case ResultKind.Incomplete:
                        amp = 6f; rot = wave * 3.2f + Mathf.Max(0f, wave) * 1.5f;
                        if (dropped < 2 && k > 0.35f + dropped * 0.2f) { DropBook(dropped); dropped++; }
                        break;
                    default:
                        amp = 6f + 4f * k;
                        float lean = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.85f, k)) * -12f;
                        rot = wave * Mathf.Lerp(2f, 5f, k) * (1f - Mathf.InverseLerp(0.6f, 0.95f, k)) + lean;
                        if (!hardwareOff && k > 0.45f) { hardwareOff = true; KnockOffHardware(); }
                        if (dropped < 5 && k > 0.5f + dropped * 0.08f) { DropBook(dropped + 2); dropped++; }
                        break;
                }
                float x = Mathf.Round(wave * amp * (1f - (kind == ResultKind.WrongMaterial ? Mathf.InverseLerp(0.7f, 1f, k) : 0f)) / 2f) * 2f;
                rig.anchoredPosition = new Vector2(RigBase.x + x, -RigBase.y);
                rig.localRotation = Quaternion.Euler(0f, 0f, rot);
                yield return null;
            }
            rig.anchoredPosition = new Vector2(RigBase.x, -RigBase.y);
            if (kind == ResultKind.WrongMaterial) rig.localRotation = Quaternion.Euler(0f, 0f, -12f);
            else
            {
                for (float t = 0f; t < 0.25f; t += Time.deltaTime)
                {
                    rig.localRotation = Quaternion.Slerp(rig.localRotation, Quaternion.identity, t / 0.25f);
                    yield return null;
                }
                rig.localRotation = Quaternion.identity;
            }
            SetWobbleLines(false, lines);
        }

        /// <summary>Sonuç: sahnede rozet + etiket + mesaj şeridi, panelde açıklama + düğme.</summary>
        public void ShowResult(ResultKind kind, string label, string message, string edu, string button)
        {
            ShowPanel(resultRoot);
            panelTitle.text = "TEST SONUCU";
            Color c = kind == ResultKind.Success ? EqUi.Green : kind == ResultKind.Incomplete ? EqUi.Amber : EqUi.Red;
            Color textColor = kind == ResultKind.Incomplete ? EqUi.Ink : EqUi.White;
            EqUi.FallbackColors(resultPill, EqUi.Ink, c);
            resultLabel.text = label;
            resultLabel.color = textColor;
            resultEdu.text = edu;
            resultButtonLabel.text = button;

            stagePill.gameObject.SetActive(true);
            EqUi.FallbackColors(stagePill, EqUi.Ink, c);
            stagePillText.text = label;
            stagePillText.color = textColor;
            stagePill.rectTransform.sizeDelta = new Vector2(Mathf.Max(90f, EqUi.PreferredWidth(stagePillText, label) + 20f), 20f);
            stageBadge.gameObject.SetActive(true);
            EqUi.Skin(stageBadge, kind == ResultKind.Success ? badgeOk : kind == ResultKind.Incomplete ? badgeWarn : badgeFail, c, skin);
            stageBadge.preserveAspect = false;
            StartCoroutine(PopScale(stageBadge.rectTransform, 0.28f));
            stageBand.gameObject.SetActive(true);
            stageBandText.text = message;
            // düşen kitaplar sonuç katmanlarının altında kalsın
            stagePill.transform.SetAsLastSibling();
            stageBadge.transform.SetAsLastSibling();
            stageBand.SetAsLastSibling();
            StartCoroutine(Pop(resultRoot, 0.18f));
        }

        /// <summary>Kitaplığı dik konuma getirir, düşen kitapları/parçaları temizler.</summary>
        public IEnumerator ResetShelf(float seconds)
        {
            ClearResultOverlay();
            Quaternion from = rig.localRotation;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = EqUi.OutBack(t / seconds, 1.6f);
                rig.localRotation = Quaternion.SlerpUnclamped(from, Quaternion.identity, k);
                foreach (var b in bookPool) if (b.gameObject.activeSelf) b.color = EqUi.A(Color.white, 1f - t / seconds);
                yield return null;
            }
            rig.localRotation = Quaternion.identity;
            foreach (var b in bookPool) b.gameObject.SetActive(false);
        }

        private void ClearResultOverlay()
        {
            stagePill.gameObject.SetActive(false);
            stageBadge.gameObject.SetActive(false);
            stageBand.gameObject.SetActive(false);
        }

        private void ResetStageFx()
        {
            ClearResultOverlay();
            rig.localRotation = Quaternion.identity;
            rig.anchoredPosition = new Vector2(RigBase.x, -RigBase.y);
            foreach (var b in bookPool) b.gameObject.SetActive(false);
            foreach (var s in speckPool) s.gameObject.SetActive(false);
            SetWobbleLines(false, EqUi.White);
            dialogue.gameObject.SetActive(false);
        }

        // =====================================================================
        // Küçük efektler
        // =====================================================================

        private void SetWobbleLines(bool on, Color color)
        {
            if (wobbleLines != null) { StopCoroutine(wobbleLines); wobbleLines = null; }
            wobbleLeft.gameObject.SetActive(on);
            wobbleRight.gameObject.SetActive(on);
            if (on && isActiveAndEnabled) wobbleLines = StartCoroutine(WobbleLines(color));
        }

        private IEnumerator WobbleLines(Color color)
        {
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 9f));
                wobbleLeft.color = EqUi.A(color, a);
                wobbleRight.color = EqUi.A(color, a);
                float dx = Mathf.Round(Mathf.Sin(t * 18f)) * 2f;
                wobbleLeft.rectTransform.anchoredPosition = EqUi.Snap(new Vector2(RigBase.x - 92f - dx, -(RigBase.y - 150f)));
                wobbleRight.rectTransform.anchoredPosition = EqUi.Snap(new Vector2(RigBase.x + 92f + dx, -(RigBase.y - 150f)));
                yield return null;
            }
        }

        private void KnockOffHardware()
        {
            for (int i = 0; i < 2; i++)
                if (hardware[i].gameObject.activeSelf) StartCoroutine(FallOff(hardware[i].rectTransform, i == 0 ? -1f : 1f));
        }

        private IEnumerator FallOff(RectTransform rt, float dir)
        {
            Vector2 start = rt.anchoredPosition;
            var img = rt.GetComponent<Image>();
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                rt.anchoredPosition = EqUi.Snap(start + new Vector2(dir * 40f * t, 30f * t - 420f * t * t));
                rt.localRotation = Quaternion.Euler(0f, 0f, dir * -300f * t);
                if (img != null) img.color = EqUi.A(Color.white, 1f - t / 0.6f);
                yield return null;
            }
            rt.gameObject.SetActive(false);
        }

        private void DropBook(int n)
        {
            if (fallingBooks == null || fallingBooks.Length == 0) return;
            Image img = null;
            foreach (var b in bookPool) if (!b.gameObject.activeSelf) { img = b; break; }
            if (img == null)
            {
                img = EqUi.Img("FallingBook", stageRoot, null, EqUi.Red, skin);
                img.preserveAspect = false;
                bookPool.Add(img);
            }
            img.transform.SetAsLastSibling();
            img.sprite = fallingBooks[n % fallingBooks.Length];
            img.color = Color.white;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 1f);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(8f, 6f) * ArtScale;
            img.gameObject.SetActive(true);
            float row = BookRowsArt[n % BookRowsArt.Length];
            float artX = 14f + (n * 17) % 42;
            StartCoroutine(BookFall(img.rectTransform, ArtToStage(new Vector2(artX, row - 6f)), (n % 2 == 0 ? -1f : 1f) * (18f + n * 4f)));
        }

        private IEnumerator BookFall(RectTransform rt, Vector2 start, float vx)
        {
            float floorY = 126f * ArtScale + 6f + (Mathf.Abs(vx) % 3f) * 2f;
            float vy = -60f, y = start.y, x = start.x, rot = 0f;
            while (y < floorY)
            {
                float dt = Time.deltaTime;
                vy += 900f * dt;
                y += vy * dt;
                x += vx * dt;
                rot += vx * 6f * dt;
                rt.anchoredPosition = EqUi.Snap(new Vector2(x, -Mathf.Min(y, floorY)));
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(rot / 15f) * 15f);
                yield return null;
            }
            rt.localRotation = Quaternion.Euler(0f, 0f, vx > 0 ? -90f : 0f);
        }

        private void Specks(Vector2 stagePos, int count, Color color)
        {
            if (speck == null) return;
            for (int i = 0; i < count; i++)
            {
                Image img = null;
                foreach (var s in speckPool) if (!s.gameObject.activeSelf) { img = s; break; }
                if (img == null)
                {
                    img = EqUi.Img("Speck", stageRoot, speck, color, skin);
                    img.preserveAspect = false;
                    speckPool.Add(img);
                }
                img.transform.SetAsLastSibling();
                img.color = color;
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 1f);
                img.rectTransform.sizeDelta = new Vector2(4f, 4f);
                img.gameObject.SetActive(true);
                float ang = (i / (float)count) * Mathf.PI * 2f + 0.4f;
                StartCoroutine(SpeckFly(img, stagePos, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 46f));
            }
        }

        private IEnumerator SpeckFly(Image img, Vector2 start, Vector2 v)
        {
            var rt = img.rectTransform;
            Color c = img.color;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                Vector2 p = start + v * t + new Vector2(0f, 120f * t * t);
                rt.anchoredPosition = EqUi.Snap(new Vector2(p.x, -p.y));
                img.color = EqUi.A(c, 1f - t / 0.35f);
                yield return null;
            }
            img.gameObject.SetActive(false);
        }

        private static IEnumerator PopScale(RectTransform rt, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float s = EqUi.OutBack(t / seconds, 2.2f);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private static IEnumerator Pulse(RectTransform rt, float seconds, float amount)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float s = 1f + amount * Mathf.Sin(t / seconds * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private static IEnumerator Pop(RectTransform rt, float seconds)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                cg.alpha = EqUi.OutCubic(t / seconds);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // =====================================================================
        // Yardımcılar
        // =====================================================================

        private Sprite IconOf(int material) => icons != null && material >= 0 && material < icons.Length ? icons[material] : null;
        private Sprite InstalledOf(int material) => installed != null && material >= 0 && material < installed.Length ? installed[material] : null;

        /// <summary>Yakın plan kitaplık sanat koordinatı (sol-üst orijin) → sahne referans koordinatı (sol-üst, y aşağı).</summary>
        private static Vector2 ArtToStage(Vector2 art) =>
            new Vector2(RigBase.x + (art.x - ShelfPivotArt.x) * ArtScale, RigBase.y - (ShelfPivotArt.y - art.y) * ArtScale);

        /// <summary>Merkezden yerleştirir (sahne koordinatı: sol-üst orijin, y aşağı).</summary>
        private static void PlaceArt(RectTransform rt, Vector2 center, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = EqUi.Snap(new Vector2(center.x, -center.y));
        }

        /// <summary>Etkin noktanın ekran (sahne) konumu — test aracı için.</summary>
        public RectTransform ActivePointRect => activePoint >= 0 ? points[activePoint].rectTransform : null;

        private void Update()
        {
            if (!IsOpen) return;
            animTime += Time.deltaTime;
            // Etkin nokta hafifçe nefes alır
            if (activePoint >= 0 && points[activePoint].gameObject.activeSelf)
            {
                float s = 1f + 0.08f * Mathf.Sin(animTime * 6f);
                points[activePoint].rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
