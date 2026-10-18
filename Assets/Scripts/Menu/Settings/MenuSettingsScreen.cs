using System;
using System.Collections.Generic;
using AfetUyanisi.Core;
using AfetUyanisi.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Menu
{
    /// <summary>
    /// Ana menünün Ayarlar ekranı (MainMenu.unity, menü Canvas'ının çocuğu; arayüzünü Awake'te kendisi kurar).
    /// Menünün piksel dili: lacivert Panel, seçilince turuncu çerçeveli PanelLit, krem yazı, odakta sıcak sarı yazı + ▶.
    /// Dört bölüm (sekme): Ses (müzik / efekt kaydırıcısı), Ekran (tam ekran, çözünürlük, Uygula), Kontroller (oyunun
    /// gerçek tuşları, GameInput ile aynı; tuş atama sistemi olmadığı için yalnızca liste), Parlaklık (kaydırıcı + gri
    /// ayar şeridi). Altta Varsayılanlara Dön ve Geri. Değerler GameSettings'e (PlayerPrefs) yazılır, GameSettingsRuntime
    /// sese ve görüntüye anında uygular.
    ///
    /// Girdi (GameInput): W/S ↑/↓ satır · A/D ←/→ sekme, değer, düğme (kaydırıcıda basılı tutunca tekrar eder) ·
    /// Enter/Space seç · Esc geri. Fare: üstüne gelinen satır odaklanır; sekme/düğme/ok tıklanır, kaydırıcı sürüklenir.
    /// </summary>
    public class MenuSettingsScreen : MonoBehaviour
    {
        [Header("Görünüm (menü butonlarıyla aynı)")]
        [SerializeField] private Font font;
        [Tooltip("Assets/Art/Menu/UI/Panel")]
        [SerializeField] private Sprite panelSprite;
        [Tooltip("Assets/Art/Menu/UI/PanelLit")]
        [SerializeField] private Sprite panelLitSprite;
        [SerializeField] private Color textColor = new Color(0.95f, 0.92f, 0.85f);
        [SerializeField] private Color titleColor = new Color(1f, 0.74f, 0.4f);
        [SerializeField] private Color focusColor = new Color(1f, 0.82f, 0.45f);
        [SerializeField] private Color mutedColor = new Color(0.7f, 0.7f, 0.86f);
        [SerializeField, Range(0f, 1f)] private float backdropAlpha = 0.6f;

        public event Action Closed;
        public bool IsOpen { get; private set; }

        private const float WindowW = 432f, WindowH = 330f;
        private const float ContentX = 14f, ContentY = 56f, ContentW = 404f, ContentH = 216f;
        private const float ButtonsY = 280f, HintY = 308f;
        private const float KeySize = 19f, KeyGap = 2f, ArrowSize = 7f;
        private const float RowX = 8f, RowW = 388f, RowH = 24f;
        private const float ControlX = 168f, ControlW = 212f;
        private const float FadeSeconds = 0.14f, MessageSeconds = 2.2f;
        private const float RepeatDelay = 0.35f, RepeatRate = 0.06f;
        // Press Start 2P 8 px ızgara (8 = 640x360'ta tam piksel); Pixel Operator 12 ile büyük harf boyu aynı (7 px).
        private const int BodySize = 12;

        private static readonly string[] TabNames = { "Ses", "Ekran", "Kontroller", "Parlaklık" };
        private static readonly Color ShadowColor = new Color(0.02f, 0.02f, 0.06f, 0.9f);
        // Panel.png'nin renkleri: dış çizgi 0A0A16, iç zemin 1A1C36.
        private static readonly Color TrackBorder = new Color32(0x0A, 0x0A, 0x16, 0xFF);
        private static readonly Color PanelFill = new Color32(0x1A, 0x1C, 0x36, 0xFF);
        // Tuş başlığı: yan/gölge, yüz, üst parlaklık (Panel'in lacivert tonları).
        private static readonly Color KeySide = new Color32(0x23, 0x26, 0x4A, 0xFF);
        private static readonly Color KeyFace = new Color32(0x3A, 0x3F, 0x6E, 0xFF);
        private static readonly Color KeyShine = new Color32(0x62, 0x68, 0x9E, 0xFF);
        private static readonly Color TrackBack = new Color(0.13f, 0.14f, 0.27f);

        // Kontroller: GameInput'taki gerçek atamalar (docs/OYUN_AKISI.md §3). Boş eylem = başlık satırı.
        private static readonly (string keys, string action)[] ControlLines =
        {
            ("GENEL", null),
            ("E", "Etkileşim: kalk, eşya al, incele"),
            ("E basılı tut", "Dalı it, rafı sabitle"),
            ("E / Boşluk / Enter / Sol tık", "Konuşmayı ilerlet"),
            ("1 – 5  /  Fare", "Seçim yap"),
            ("Esc", "Geri, paneli kapat"),
            ("DEPREM", null),
            ("S  →  A  →  D basılı", "Çök, Kapan, Tutun"),
            ("Boşluk basılı + A / D", "Korunurken dengede kal"),
            ("Boşluk", "Hazırlık bitince zamanı hızlandır"),
            ("SEL", null),
            ("0 – 9  /  Backspace", "Kapı şifresini gir, sil"),
            ("Sol tık + daire  /  D", "Bahçe kapısının kolunu çevir"),
        };

        private bool built;
        private CanvasGroup group;
        private RectTransform window;
        private Text hint;
        private RectTransform[] pages;
        private readonly List<Row>[] pageRows = { new List<Row>(), new List<Row>(), new List<Row>(), new List<Row>() };
        private TabBar tabBar;
        private ButtonBar buttonBar;
        private readonly List<Row> focusables = new List<Row>();
        private int tab, focus;
        private float fade, lastMoveY, repeatAt, messageUntil, lastTickAt;
        private int repeatDir, openedFrame = -1;
        private string message;

        // Ekran sekmesi: seçili (bekleyen) ve uygulanmış değerler.
        private List<Vector2Int> resolutions = new List<Vector2Int>();
        private int resIndex;
        private bool pendingFullscreen, appliedFullscreen;
        private Vector2Int appliedResolution;
        private Text displayStatus;

        private void Awake()
        {
            EnsureBuilt();
            SetVisible(0f);
        }

        // ------------------------------------------------------------------ aç / kapat

        public void Open()
        {
            if (IsOpen) return;
            EnsureBuilt();
            IsOpen = true;
            openedFrame = Time.frameCount;
            lastMoveY = GameInput.MoveVector.y;
            repeatDir = 0;
            message = null;
            // Menü butonu seçili kalırsa Enter alttaki butona da gider.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            LoadDisplayState();
            SelectTab(0, false);
            Focus(0, false);
            RefreshAll();
            transform.SetAsLastSibling();
            FixFadeOrder();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GameSettings.Save();
            Sfx("SFX_UI_Back");
            Closed?.Invoke();
        }

        // Menünün kararma paneli (FadePanel) her zaman en üstte kalsın.
        private void FixFadeOrder()
        {
            var parent = transform.parent;
            if (parent == null) return;
            var fadePanel = parent.Find("FadePanel");
            if (fadePanel != null) fadePanel.SetAsLastSibling();
        }

        private void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(fade, target))
                SetVisible(Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / FadeSeconds));
            if (!IsOpen) return;

            UpdateHint();
            if (Time.frameCount == openedFrame) return;   // açan Enter/tık bu karede ekranda bir şey seçmesin
            HandleKeys();
        }

        private void HandleKeys()
        {
            if (GameInput.BackPressed) { Close(); return; }

            float y = GameInput.MoveVector.y;
            bool up = y > 0.5f && lastMoveY <= 0.5f, down = y < -0.5f && lastMoveY >= -0.5f;
            lastMoveY = y;
            if (up) { Focus((focus + focusables.Count - 1) % focusables.Count, true); return; }
            if (down) { Focus((focus + 1) % focusables.Count, true); return; }

            Row row = focusables[focus];
            int dir = GameInput.LeftPressed ? -1 : GameInput.RightPressed ? 1 : 0;
            int held = Mathf.RoundToInt(GameInput.MoveX);
            if (dir != 0)
            {
                row.Step(dir);
                repeatDir = dir;
                repeatAt = Time.unscaledTime + RepeatDelay;
            }
            else if (repeatDir != 0 && held == repeatDir)
            {
                if (row.Repeats && Time.unscaledTime >= repeatAt)
                {
                    row.Step(repeatDir);
                    repeatAt = Time.unscaledTime + RepeatRate;
                }
            }
            else repeatDir = 0;

            if (GameInput.ConfirmPressed) row.Confirm();
        }

        // ------------------------------------------------------------------ odak / sekme

        private void Focus(int index, bool sound)
        {
            if (focusables.Count == 0) return;
            index = Mathf.Clamp(index, 0, focusables.Count - 1);
            if (sound && index != focus) Sfx("SFX_UI_Hover", 0.35f);
            focus = index;
            for (int i = 0; i < focusables.Count; i++) focusables[i].SetFocused(i == focus);
            repeatDir = 0;
        }

        private void FocusRow(Row row)
        {
            int i = focusables.IndexOf(row);
            if (i >= 0) Focus(i, true);
        }

        private void SelectTab(int index, bool sound)
        {
            tab = (index + TabNames.Length) % TabNames.Length;
            for (int i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(i == tab);
            Row focused = focus >= 0 && focus < focusables.Count ? focusables[focus] : tabBar;
            focusables.Clear();
            focusables.Add(tabBar);
            focusables.AddRange(pageRows[tab]);
            focusables.Add(buttonBar);
            int keep = focusables.IndexOf(focused);
            Focus(keep >= 0 ? keep : 0, false);
            tabBar.Refresh();
            if (sound) Sfx("SFX_UI_Click");
        }

        private void UpdateHint()
        {
            bool showMessage = message != null && Time.unscaledTime < messageUntil;
            hint.color = showMessage ? titleColor : mutedColor;
            hint.text = showMessage ? message : focusables.Count > 0 ? focusables[focus].Hint : "";
        }

        private void ShowMessage(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + MessageSeconds;
        }

        private void RefreshAll()
        {
            foreach (var rows in pageRows)
                foreach (var row in rows) row.Refresh();
            tabBar.Refresh();
            buttonBar.Refresh();
            RefreshDisplayStatus();
        }

        // ------------------------------------------------------------------ eylemler

        private void ResetDefaults()
        {
            GameSettings.ResetAudioAndBrightness();
            pendingFullscreen = GameSettings.DefaultFullscreen;
            resIndex = IndexOfResolution(GameSettings.NativeResolution);
            ApplyDisplay(false);
            RefreshAll();
            Sfx("SFX_UI_Click");
            ShowMessage("Varsayılan ayarlar yüklendi.");
        }

        private void LoadDisplayState()
        {
            resolutions = GameSettings.AvailableResolutions();
            appliedResolution = new Vector2Int(Screen.width, Screen.height);
            appliedFullscreen = GameSettings.CurrentFullscreen;
#if UNITY_EDITOR
            // Editörde Game penceresi Screen.SetResolution ile değişmez; son uygulanan (kayıtlı) seçimi göster.
            if (GameSettings.HasSavedDisplay)
            {
                appliedFullscreen = GameSettings.SavedFullscreen;
                if (GameSettings.TryGetSavedResolution(out int w, out int h)) appliedResolution = new Vector2Int(w, h);
            }
#endif
            resIndex = IndexOfResolution(appliedResolution);
            pendingFullscreen = appliedFullscreen;
        }

        private int IndexOfResolution(Vector2Int size)
        {
            int i = resolutions.IndexOf(size);
            if (i >= 0) return i;
            resolutions.Add(size);
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return resolutions.IndexOf(size);
        }

        private Vector2Int PendingResolution =>
            resolutions.Count > 0 ? resolutions[Mathf.Clamp(resIndex, 0, resolutions.Count - 1)] : appliedResolution;

        private bool DisplayPending => PendingResolution != appliedResolution || pendingFullscreen != appliedFullscreen;

        private void ApplyDisplay(bool announce)
        {
            GameSettings.ApplyDisplay(PendingResolution, pendingFullscreen);
            appliedResolution = PendingResolution;
            appliedFullscreen = pendingFullscreen;
            RefreshDisplayStatus();
            if (!announce) return;
            Sfx("SFX_UI_Click");
            ShowMessage(Application.isEditor ? "Kaydedildi. Editörde pencere değişmez, oyunda uygulanır." : "Ekran ayarı uygulandı.");
        }

        private void RefreshDisplayStatus()
        {
            if (displayStatus == null) return;
            string now = $"Şu an: {SizeText(appliedResolution)}  ·  {(appliedFullscreen ? "Tam ekran" : "Pencere")}";
            displayStatus.text = DisplayPending ? now + "\n<color=#" + ColorUtility.ToHtmlStringRGB(titleColor) + ">Değişiklik bekliyor: Uygula'ya bas.</color>" : now;
        }

        private static string SizeText(Vector2Int size) => $"{size.x} × {size.y}";

        private static string Percent(float value) => "%" + Mathf.RoundToInt(value * 100f);

        private void Sfx(string id, float volume = 0.5f) => GameAudio.PlaySfx(id, volume);

        // Kaydırıcı sürüklenirken tık sesi seyrek çalar (efekt kaydırıcısında yeni düzeyi duyurur).
        private void Tick()
        {
            if (Time.unscaledTime - lastTickAt < 0.08f) return;
            lastTickAt = Time.unscaledTime;
            Sfx("SFX_UI_Click");
        }

        private void SetVisible(float k)
        {
            fade = k;
            if (group == null) return;
            group.alpha = k;
            group.blocksRaycasts = IsOpen;
            group.interactable = IsOpen;
            if (window != null)
            {
                float e = 1f - (1f - k) * (1f - k);
                window.anchoredPosition = new Vector2(0f, Mathf.Round((1f - e) * -8f));
            }
            bool show = k > 0f;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).gameObject.activeSelf != show) transform.GetChild(i).gameObject.SetActive(show);
        }

        // ------------------------------------------------------------------ kurulum

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            var root = (RectTransform)transform;
            Stretch(root);
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            // Arkadaki menüyü karartır ve tıklamaları keser.
            var backdrop = Img("Backdrop", root, null, new Color(0.01f, 0.01f, 0.04f, backdropAlpha));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            // Panel'in içi hafif saydam; geniş pencerede arkadaki şehir yazıları bastırmasın diye altına opak zemin.
            var frame = Img("Window", root, null, Color.clear);
            window = frame.rectTransform;
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(WindowW, WindowH);
            frame.raycastTarget = true;
            var fill = Img("Fill", window, null, PanelFill);
            Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            var border = Img("Frame", window, panelSprite, Color.white);
            Stretch(border.rectTransform);

            var title = Txt("Title", window, "AYARLAR", 16, titleColor, TextAnchor.MiddleCenter);
            TL(title.rectTransform, 0f, 5f, WindowW, 22f);

            tabBar = new TabBar(this, window);

            var content = Img("Content", window, null, new Color(0f, 0f, 0.02f, 0.28f));
            TL(content.rectTransform, ContentX, ContentY, ContentW, ContentH);
            pages = new RectTransform[TabNames.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i] = NewRect("Page_" + TabNames[i], content.rectTransform);
                Stretch(pages[i]);
            }
            BuildAudioPage(pages[0], pageRows[0]);
            BuildDisplayPage(pages[1], pageRows[1]);
            BuildControlsPage(pages[2]);
            BuildBrightnessPage(pages[3], pageRows[3]);

            buttonBar = new ButtonBar(this, window);

            hint = Txt("Hint", window, "", 8, mutedColor, TextAnchor.MiddleCenter);
            TL(hint.rectTransform, 8f, HintY - 3f, WindowW - 16f, 20f);
            hint.supportRichText = true;
            hint.horizontalOverflow = HorizontalWrapMode.Wrap; // 54 harften uzun ipucu iki satıra iner
            hint.lineSpacing = 1.25f;

            focusables.Add(tabBar);
            focusables.Add(buttonBar);
        }

        private void BuildAudioPage(RectTransform page, List<Row> rows)
        {
            rows.Add(new SliderRow(this, page, "Müzik", 10f, 0f, 1f, 0.05f,
                () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v));
            rows.Add(new SliderRow(this, page, "Ses Efektleri", 40f, 0f, 1f, 0.05f,
                () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v));
            var note = Txt("Note", page, "Müzik: menü ve bölüm müzikleri.\nSes efektleri: ortam, arayüz ve olay sesleri.", 8, mutedColor, TextAnchor.UpperLeft);
            TL(note.rectTransform, RowX + 4f, 76f, RowW - 8f, 30f);
        }

        private void BuildDisplayPage(RectTransform page, List<Row> rows)
        {
            rows.Add(new ChoiceRow(this, page, "Tam Ekran", 10f,
                () => pendingFullscreen ? "Açık" : "Kapalı",
                d => { pendingFullscreen = !pendingFullscreen; RefreshDisplayStatus(); }));
            rows.Add(new ChoiceRow(this, page, "Çözünürlük", 40f,
                () => resolutions.Count > 0 ? SizeText(PendingResolution) : "-",
                d =>
                {
                    if (resolutions.Count == 0) return;
                    resIndex = (resIndex + d + resolutions.Count) % resolutions.Count;
                    RefreshDisplayStatus();
                }));
            rows.Add(new ActionRow(this, page, "Uygula", 70f, () => DisplayPending, () => ApplyDisplay(true)));
            displayStatus = Txt("Status", page, "", 8, mutedColor, TextAnchor.UpperCenter);
            TL(displayStatus.rectTransform, RowX, 104f, RowW, 28f);
            displayStatus.supportRichText = true;
            displayStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void BuildControlsPage(RectTransform page)
        {
            const float lineH = 11f, top = 70f;
            BuildMovementKeys(page, 6f);
            var line = Img("Divider", page, null, new Color(1f, 1f, 1f, 0.08f));
            TL(line.rectTransform, RowX, top - 4f, RowW, 1f);

            float y = top;
            bool stripe = false;
            foreach (var (keys, action) in ControlLines)
            {
                if (action == null)
                {
                    if (y > top) y += 1f;
                    var head = Txt("Header", page, keys, 8, titleColor, TextAnchor.MiddleLeft);
                    TL(head.rectTransform, RowX + 4f, y, 200f, lineH);
                    y += lineH;
                    stripe = false;
                    continue;
                }
                if (stripe)
                {
                    var band = Img("Stripe", page, null, new Color(1f, 1f, 1f, 0.035f));
                    TL(band.rectTransform, RowX, y, RowW, lineH);
                }
                stripe = !stripe;
                var k = BodyTxt("Keys", page, keys, focusColor, TextAnchor.MiddleLeft);
                TL(k.rectTransform, RowX + 10f, y, 152f, lineH);
                var a = BodyTxt("Action", page, action, textColor, TextAnchor.MiddleLeft);
                TL(a.rectTransform, RowX + 168f, y, RowW - 172f, lineH);
                y += lineH;
            }
        }

        // Hareket: W A S D tuş kümesi, her tuşun yönünü gösteren oklar (W ↑, A ←, S ↓, D →), "ya da" ve ok tuşları
        // kümesi; sağda kısa açıklama. GameInput.MoveVector: ekran yönünde 8 yön, iki tuş birlikte çapraz.
        private void BuildMovementKeys(RectTransform page, float y0)
        {
            const float step = KeySize + KeyGap;
            float rowTop = y0 + ArrowSize + KeyGap, rowBottom = rowTop + step;
            // Oklar tam piksele oturur: yatayda tuşun ortası, dikeyde tuş yüzünün (alt kenar gölgesi hariç) ortası.
            float arrowInset = (KeySize - ArrowSize) / 2f;
            float arrowSide = Mathf.Floor(2f + (KeySize - 5f) / 2f - ArrowSize / 2f);

            float x0 = RowX + 4f + ArrowSize + KeyGap;
            Keycap(page, x0 + step, rowTop, "W", -1f);
            Keycap(page, x0, rowBottom, "A", -1f);
            Keycap(page, x0 + step, rowBottom, "S", -1f);
            Keycap(page, x0 + 2f * step, rowBottom, "D", -1f);
            Arrow(page, x0 + step + arrowInset, y0, 0f, titleColor);                             // W üstünde ↑
            Arrow(page, x0 - ArrowSize - KeyGap, rowBottom + arrowSide, 90f, titleColor);        // A solunda ←
            Arrow(page, x0 + step + arrowInset, rowBottom + KeySize + KeyGap, 180f, titleColor); // S altında ↓
            Arrow(page, x0 + 3f * step, rowBottom + arrowSide, -90f, titleColor);                // D sağında →

            float orX = x0 + 3f * step + ArrowSize + 6f;
            var or = Txt("Or", page, "ya da", 8, mutedColor, TextAnchor.MiddleCenter);
            TL(or.rectTransform, orX, rowTop, 30f, step + KeySize);

            float x1 = orX + 36f;
            Keycap(page, x1 + step, rowTop, null, 0f);
            Keycap(page, x1, rowBottom, null, 90f);
            Keycap(page, x1 + step, rowBottom, null, 180f);
            Keycap(page, x1 + 2f * step, rowBottom, null, -90f);

            float tx = x1 + 3f * step + 10f, tw = RowX + RowW - tx;
            var head = Txt("Header", page, "HAREKET", 8, titleColor, TextAnchor.MiddleLeft);
            TL(head.rectTransform, tx, y0 + 1f, tw, 11f);
            var walk = BodyTxt("Walk", page, "W yukarı, A sola, S aşağı, D sağa.", textColor, TextAnchor.MiddleLeft);
            TL(walk.rectTransform, tx, y0 + 13f, tw, 11f);
            var diag = BodyTxt("Diagonal", page, "İki tuş birlikte: çapraz yürü.", textColor, TextAnchor.MiddleLeft);
            TL(diag.rectTransform, tx, y0 + 25f, tw, 11f);
            var note = BodyTxt("Note", page, "Tuşlar sabittir, değiştirilemez.", mutedColor, TextAnchor.MiddleLeft);
            TL(note.rectTransform, tx, y0 + 39f, tw, 11f);
        }

        /// <summary>
        /// Piksel tuş başlığı: koyu dış çizgi, alt kenarı kalın gölgeli yüz (basılabilir görünüm), üstte ince parlaklık.
        /// letter boşsa yüzde arrowAngle yönünde ok çizilir (0 yukarı, 90 sol, 180 aşağı, -90 sağ).
        /// </summary>
        private void Keycap(RectTransform page, float x, float y, string letter, float arrowAngle)
        {
            var outer = Img("Key", page, null, TrackBorder);
            TL(outer.rectTransform, x, y, KeySize, KeySize);
            var side = Img("Side", outer.rectTransform, null, KeySide);
            Stretch(side.rectTransform);
            side.rectTransform.offsetMin = new Vector2(1f, 1f);
            side.rectTransform.offsetMax = new Vector2(-1f, -1f);
            var face = Img("Face", outer.rectTransform, null, KeyFace);
            Stretch(face.rectTransform);
            face.rectTransform.offsetMin = new Vector2(2f, 3f);
            face.rectTransform.offsetMax = new Vector2(-2f, -2f);
            var shine = Img("Shine", face.rectTransform, null, KeyShine);
            shine.rectTransform.anchorMin = new Vector2(0f, 1f);
            shine.rectTransform.anchorMax = Vector2.one;
            shine.rectTransform.pivot = new Vector2(0.5f, 1f);
            shine.rectTransform.sizeDelta = new Vector2(0f, 1f);

            if (!string.IsNullOrEmpty(letter))
            {
                var t = Txt("Letter", face.rectTransform, letter, 8, textColor, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(-2f, 0f);
                t.rectTransform.offsetMax = new Vector2(2f, 1f);
            }
            else
            {
                var a = Img("Arrow", face.rectTransform, ArrowSprite, textColor);
                var rt = a.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(ArrowSize, ArrowSize);
                rt.anchoredPosition = new Vector2(0f, 0.5f);
                rt.localEulerAngles = new Vector3(0f, 0f, arrowAngle);
            }
        }

        private void Arrow(RectTransform page, float x, float y, float angle, Color color)
        {
            var a = Img("Arrow", page, ArrowSprite, color);
            TL(a.rectTransform, x, y, ArrowSize, ArrowSize);
            // Sol üst köşeye göre yerleştir, kendi ortası çevresinde döndür.
            a.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            a.rectTransform.anchoredPosition = new Vector2(x + ArrowSize / 2f, -(y + ArrowSize / 2f));
            a.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        private static Sprite arrowSprite;

        /// <summary>7x7 piksel yukarı ok (beyaz; Image rengiyle boyanır, döndürülerek diğer yönler).</summary>
        private static Sprite ArrowSprite
        {
            get
            {
                if (arrowSprite != null) return arrowSprite;
                string[] rows =
                {
                    "...#...",
                    "..###..",
                    ".#####.",
                    "#######",
                    "..###..",
                    "..###..",
                    "..###..",
                };
                var tex = new Texture2D(7, 7, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SettingsArrow" };
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 7; x++)
                        tex.SetPixel(x, 6 - y, rows[y][x] == '#' ? Color.white : Color.clear);
                tex.Apply();
                arrowSprite = Sprite.Create(tex, new Rect(0, 0, 7, 7), new Vector2(0.5f, 0.5f), 100f);
                arrowSprite.name = "SettingsArrow";
                return arrowSprite;
            }
        }

        private void BuildBrightnessPage(RectTransform page, List<Row> rows)
        {
            rows.Add(new SliderRow(this, page, "Parlaklık", 10f, GameSettings.MinBrightness, GameSettings.MaxBrightness, 0.05f,
                () => GameSettings.Brightness, v => GameSettings.Brightness = v));

            // Ayar şeridi: koyudan açığa sekiz kare; soldaki zar zor seçilmeli, sağdaki beyaza kaçmamalı.
            float[] steps = { 0.03f, 0.07f, 0.12f, 0.2f, 0.32f, 0.48f, 0.68f, 0.9f };
            const float size = 20f, gap = 4f;
            float x = (ContentW - (steps.Length * size + (steps.Length - 1) * gap)) / 2f;
            var strip = Img("Strip", page, null, TrackBorder);
            TL(strip.rectTransform, x - 3f, 49f, steps.Length * size + (steps.Length - 1) * gap + 6f, size + 6f);
            for (int i = 0; i < steps.Length; i++)
            {
                var sq = Img("Step" + i, page, null, new Color(steps[i], steps[i], steps[i]));
                TL(sq.rectTransform, x + i * (size + gap), 52f, size, size);
            }
            var note = Txt("Note", page, "Soldaki kare zar zor seçilene kadar ayarla.\nSağdaki kare beyaza kaçmamalı.", 8, mutedColor, TextAnchor.UpperCenter);
            TL(note.rectTransform, RowX, 84f, RowW, 30f);
        }

        // ------------------------------------------------------------------ yardımcılar

        private RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text Txt(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<Text>();
            t.font = font != null ? font : GameFonts.Menu;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.15f;
            t.supportRichText = false;
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        // Kontroller sayfasındaki tuş/açıklama listesi okuma metnidir: Press Start 2P her harfe 8 px verdiği için
        // "tuş + açıklama" satırları 388 px'e sığmaz; bu satırlar oyunun okuma yazı tipiyle (Pixel Operator) yazılır.
        private Text BodyTxt(string name, Transform parent, string text, Color color, TextAnchor anchor)
        {
            var t = Txt(name, parent, text, BodySize, color, anchor);
            t.font = GameFonts.Story;
            t.lineSpacing = 1f;
            return t;
        }

        /// <summary>Görünmez tıklama alanı + olay aktarıcı.</summary>
        private SettingsPointer HitArea(string name, Transform parent, float x, float y, float w, float h)
        {
            var img = Img(name, parent, null, new Color(0f, 0f, 0f, 0f));
            img.raycastTarget = true;
            TL(img.rectTransform, x, y, w, h);
            return img.gameObject.AddComponent<SettingsPointer>();
        }

        // Sol üst köşeye göre (y aşağı doğru) piksel yerleşim.
        private static void TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ================================================================== satırlar

        /// <summary>Odaklanabilir satır: klavyede W/S ile gezilir, A/D ve Enter'a kendi tepkisini verir.</summary>
        private abstract class Row
        {
            protected readonly MenuSettingsScreen S;
            protected Image Frame;
            protected Text Label, Marker;

            protected Row(MenuSettingsScreen screen) { S = screen; }

            public bool Focused { get; private set; }
            public abstract string Hint { get; }
            public virtual bool Repeats => false;
            public virtual void Step(int dir) { }
            public virtual void Confirm() { }
            public virtual void Refresh() { }

            public void SetFocused(bool focused)
            {
                Focused = focused;
                ApplyFocus();
            }

            protected virtual void ApplyFocus()
            {
                if (Frame != null)
                {
                    Frame.sprite = Focused ? S.panelLitSprite : S.panelSprite;
                    Frame.color = Focused ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                }
                if (Label != null) Label.color = Focused ? S.focusColor : S.textColor;
                if (Marker != null) Marker.enabled = Focused;
            }

            /// <summary>Standart satır: çerçeve, ▶ işareti, sol etiket; üstüne gelince odaklanır.</summary>
            protected RectTransform BuildRow(RectTransform page, string label, float y, TextAnchor labelAnchor = TextAnchor.MiddleLeft)
            {
                Frame = S.Img("Row_" + label, page, S.panelSprite, Color.white);
                Frame.raycastTarget = true;
                var rt = Frame.rectTransform;
                TL(rt, RowX, y, RowW, RowH);
                Marker = S.Txt("Marker", rt, "▶", 8, S.titleColor, TextAnchor.MiddleCenter);
                TL(Marker.rectTransform, 4f, 1f, 12f, RowH);
                Label = S.Txt("Label", rt, label, 8, S.textColor, labelAnchor);
                if (labelAnchor == TextAnchor.MiddleCenter) TL(Label.rectTransform, 0f, 1f, RowW, RowH);
                else TL(Label.rectTransform, 18f, 1f, ControlX - 22f, RowH);
                var pointer = rt.gameObject.AddComponent<SettingsPointer>();
                pointer.Entered = e => S.FocusRow(this);
                pointer.Clicked = e => { S.FocusRow(this); OnRowClicked(); };
                ApplyFocus();
                return rt;
            }

            protected virtual void OnRowClicked() { }
        }

        private sealed class SliderRow : Row
        {
            private readonly float min, max, step;
            private readonly Func<float> get;
            private readonly Action<float> set;
            private readonly RectTransform track, fill, knob;
            private readonly Image knobImage;
            private readonly Text value;
            private const float TrackW = 160f, TrackH = 8f, KnobW = 6f, KnobH = 14f;

            public SliderRow(MenuSettingsScreen s, RectTransform page, string label, float y, float min, float max, float step,
                Func<float> get, Action<float> set) : base(s)
            {
                this.min = min; this.max = max; this.step = step; this.get = get; this.set = set;
                var row = BuildRow(page, label, y);

                var border = S.Img("Track", row, null, TrackBorder);
                track = border.rectTransform;
                TL(track, ControlX, (RowH - TrackH) / 2f, TrackW, TrackH);
                var back = S.Img("Back", track, null, TrackBack);
                Stretch(back.rectTransform);
                back.rectTransform.offsetMin = new Vector2(1f, 1f);
                back.rectTransform.offsetMax = new Vector2(-1f, -1f);
                var f = S.Img("Fill", track, null, S.titleColor);
                fill = f.rectTransform;
                TL(fill, 1f, 1f, 0f, TrackH - 2f);
                var shine = S.Img("Shine", fill, null, S.focusColor);
                shine.rectTransform.anchorMin = new Vector2(0f, 1f);
                shine.rectTransform.anchorMax = new Vector2(1f, 1f);
                shine.rectTransform.pivot = new Vector2(0.5f, 1f);
                shine.rectTransform.sizeDelta = new Vector2(0f, 2f);

                var k = S.Img("Knob", row, null, TrackBorder);
                knob = k.rectTransform;
                TL(knob, ControlX, (RowH - KnobH) / 2f, KnobW, KnobH);
                knobImage = S.Img("Face", knob, null, S.textColor);
                Stretch(knobImage.rectTransform);
                knobImage.rectTransform.offsetMin = new Vector2(1f, 1f);
                knobImage.rectTransform.offsetMax = new Vector2(-1f, -1f);

                value = S.Txt("Value", row, "", 8, S.textColor, TextAnchor.MiddleRight);
                TL(value.rectTransform, ControlX + TrackW + 4f, 1f, RowW - ControlX - TrackW - 12f, RowH);

                // Sürükleme alanı izden biraz geniş: küçük ize tutturmak kolay olsun.
                var hit = S.HitArea("TrackHit", row, ControlX - 6f, 0f, TrackW + 12f, RowH);
                hit.Entered = e => S.FocusRow(this);
                hit.Pressed = e => { S.FocusRow(this); SetFromPointer(e); };
                hit.Dragged = SetFromPointer;
                hit.Released = e => GameSettings.Save();
                Refresh();
            }

            public override string Hint => "A/D: azalt / artır   ·   Fare: sürükle   ·   Esc: geri";
            public override bool Repeats => true;

            public override void Step(int dir)
            {
                float v = Mathf.Clamp(Mathf.Round((get() + dir * step) / step) * step, min, max);
                if (Mathf.Approximately(v, get())) return;
                set(v);
                S.Tick();
                Refresh();
            }

            private void SetFromPointer(PointerEventData e)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out Vector2 local)) return;
                Rect r = track.rect;
                float t = Mathf.Clamp01((local.x - r.xMin - 1f) / (r.width - 2f));
                float v = Mathf.Round((min + t * (max - min)) * 100f) / 100f;
                if (Mathf.RoundToInt(v * 100f) == Mathf.RoundToInt(get() * 100f)) return;
                set(v);
                S.Tick();
                Refresh();
            }

            public override void Refresh()
            {
                float t = Mathf.InverseLerp(min, max, get());
                float inner = TrackW - 2f;
                float w = Mathf.Round(inner * t);
                fill.sizeDelta = new Vector2(w, TrackH - 2f);
                knob.anchoredPosition = new Vector2(ControlX + Mathf.Clamp(Mathf.Round(1f + w - KnobW / 2f), 0f, TrackW - KnobW), -(RowH - KnobH) / 2f);
                value.text = Percent(get());
            }

            protected override void ApplyFocus()
            {
                base.ApplyFocus();
                if (knobImage != null) knobImage.color = Focused ? S.focusColor : S.textColor;
                if (value != null) value.color = Focused ? S.focusColor : S.textColor;
            }
        }

        /// <summary>"◀ değer ▶" satırı: A/D ya da Enter değiştirir; fareyle oklara (sol/sağ yarı) tıklanır.</summary>
        private sealed class ChoiceRow : Row
        {
            private readonly Func<string> text;
            private readonly Action<int> change;
            private readonly Text value, left, right;

            public ChoiceRow(MenuSettingsScreen s, RectTransform page, string label, float y, Func<string> text, Action<int> change) : base(s)
            {
                this.text = text;
                this.change = change;
                var row = BuildRow(page, label, y);
                left = S.Txt("Left", row, "◀", 8, S.mutedColor, TextAnchor.MiddleCenter);
                TL(left.rectTransform, ControlX, 1f, 14f, RowH);
                right = S.Txt("Right", row, "▶", 8, S.mutedColor, TextAnchor.MiddleCenter);
                TL(right.rectTransform, ControlX + ControlW - 14f, 1f, 14f, RowH);
                value = S.Txt("Value", row, "", 8, S.textColor, TextAnchor.MiddleCenter);
                TL(value.rectTransform, ControlX + 14f, 1f, ControlW - 28f, RowH);

                var hitLeft = S.HitArea("HitLeft", row, ControlX - 4f, 0f, ControlW / 2f + 4f, RowH);
                hitLeft.Entered = e => S.FocusRow(this);
                hitLeft.Clicked = e => { S.FocusRow(this); Step(-1); };
                var hitRight = S.HitArea("HitRight", row, ControlX + ControlW / 2f, 0f, ControlW / 2f + 4f, RowH);
                hitRight.Entered = e => S.FocusRow(this);
                hitRight.Clicked = e => { S.FocusRow(this); Step(1); };
                Refresh();
            }

            public override string Hint => "A/D ya da Enter: değiştir   ·   Esc: geri";

            public override void Step(int dir)
            {
                change(dir);
                S.Sfx("SFX_UI_Click");
                Refresh();
                S.RefreshAll();
            }

            public override void Confirm() => Step(1);
            protected override void OnRowClicked() => Step(1);

            public override void Refresh()
            {
                if (value != null) value.text = text();
            }

            protected override void ApplyFocus()
            {
                base.ApplyFocus();
                Color c = Focused ? S.titleColor : S.mutedColor;
                if (left != null) left.color = c;
                if (right != null) right.color = c;
                if (value != null) value.color = Focused ? S.focusColor : S.textColor;
            }
        }

        /// <summary>Ortalanmış eylem satırı (Uygula); koşul sağlanmıyorsa soluk görünür, Enter bir şey yapmaz.</summary>
        private sealed class ActionRow : Row
        {
            private readonly Func<bool> enabled;
            private readonly Action action;
            private readonly string label;

            public ActionRow(MenuSettingsScreen s, RectTransform page, string label, float y, Func<bool> enabled, Action action) : base(s)
            {
                this.label = label;
                this.enabled = enabled;
                this.action = action;
                BuildRow(page, label, y, TextAnchor.MiddleCenter);
            }

            public override string Hint => enabled() ? "Enter: ekran ayarını uygula ve kaydet   ·   Esc: geri" : "Uygulanacak değişiklik yok   ·   Esc: geri";

            public override void Confirm()
            {
                if (!enabled()) return;
                action();
                S.RefreshAll();
            }

            protected override void OnRowClicked() => Confirm();

            public override void Refresh() => ApplyFocus();

            protected override void ApplyFocus()
            {
                base.ApplyFocus();
                if (Label == null) return;
                bool on = enabled();
                Label.text = on ? label : label + " (değişiklik yok)";
                if (!on) Label.color = S.mutedColor;
            }
        }

        /// <summary>Sekmeler: A/D bölüm değiştirir; fareyle sekmeye tıklanır.</summary>
        private sealed class TabBar : Row
        {
            private const float TabW = 98f, TabH = 20f, Gap = 4f, Y = 30f;
            private readonly Image[] tabs = new Image[TabNames.Length];
            private readonly Text[] labels = new Text[TabNames.Length];
            private readonly Text arrowLeft, arrowRight;

            public TabBar(MenuSettingsScreen s, RectTransform window) : base(s)
            {
                float x0 = (WindowW - (TabNames.Length * TabW + (TabNames.Length - 1) * Gap)) / 2f;
                for (int i = 0; i < TabNames.Length; i++)
                {
                    int index = i;
                    tabs[i] = S.Img("Tab_" + TabNames[i], window, S.panelSprite, Color.white);
                    tabs[i].raycastTarget = true;
                    TL(tabs[i].rectTransform, x0 + i * (TabW + Gap), Y, TabW, TabH);
                    labels[i] = S.Txt("Label", tabs[i].rectTransform, TabNames[i], 8, S.textColor, TextAnchor.MiddleCenter);
                    TL(labels[i].rectTransform, 0f, 1f, TabW, TabH);
                    var pointer = tabs[i].gameObject.AddComponent<SettingsPointer>();
                    pointer.Entered = e => S.FocusRow(this);
                    pointer.Clicked = e =>
                    {
                        S.FocusRow(this);
                        if (S.tab != index) S.SelectTab(index, true);
                    };
                }
                arrowLeft = S.Txt("ArrowLeft", window, "◀", 8, S.titleColor, TextAnchor.MiddleCenter);
                TL(arrowLeft.rectTransform, x0 - 13f, Y + 1f, 12f, TabH);
                arrowRight = S.Txt("ArrowRight", window, "▶", 8, S.titleColor, TextAnchor.MiddleCenter);
                TL(arrowRight.rectTransform, WindowW - x0 + 1f, Y + 1f, 12f, TabH);
                ApplyFocus();
            }

            public override string Hint => "A/D: bölüm değiştir   ·   W/S: satır   ·   Esc: geri";

            public override void Step(int dir) => S.SelectTab(S.tab + dir, true);

            public override void Refresh() => ApplyFocus();

            protected override void ApplyFocus()
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    bool active = i == S.tab;
                    tabs[i].sprite = active ? S.panelLitSprite : S.panelSprite;
                    tabs[i].color = active ? Color.white : new Color(1f, 1f, 1f, 0.7f);
                    labels[i].color = active ? (Focused ? S.focusColor : S.titleColor) : S.mutedColor;
                }
                if (arrowLeft != null) arrowLeft.enabled = Focused;
                if (arrowRight != null) arrowRight.enabled = Focused;
            }
        }

        /// <summary>Alt düğmeler: Varsayılanlara Dön ve Geri. A/D aralarında gezer.</summary>
        private sealed class ButtonBar : Row
        {
            private const float H = 22f, Y = ButtonsY;
            private readonly Image[] buttons = new Image[2];
            private readonly Text[] labels = new Text[2];
            private readonly Text[] markers = new Text[2];
            private int index = 1;

            public ButtonBar(MenuSettingsScreen s, RectTransform window) : base(s)
            {
                string[] names = { "Varsayılanlara Dön", "Geri" };
                float[] widths = { 176f, 104f };
                float x = (WindowW - (widths[0] + widths[1] + 10f)) / 2f;
                for (int i = 0; i < 2; i++)
                {
                    int sub = i;
                    // Ad "Back" içermesin: ses kancası (AudioUiSoundHooks) Button'lara bağlanır, bunlar Button değil.
                    buttons[i] = S.Img(i == 0 ? "DefaultsButton" : "CloseButton", window, S.panelSprite, Color.white);
                    buttons[i].raycastTarget = true;
                    TL(buttons[i].rectTransform, x, Y, widths[i], H);
                    labels[i] = S.Txt("Text", buttons[i].rectTransform, names[i], 8, S.textColor, TextAnchor.MiddleCenter);
                    TL(labels[i].rectTransform, 0f, 1f, widths[i], H);
                    markers[i] = S.Txt("Arrow", buttons[i].rectTransform, "▶", 8, S.titleColor, TextAnchor.MiddleCenter);
                    TL(markers[i].rectTransform, 4f, 1f, 12f, H);
                    var pointer = buttons[i].gameObject.AddComponent<SettingsPointer>();
                    pointer.Entered = e => Hover(sub);
                    pointer.Clicked = e => { Hover(sub); Confirm(); };
                    x += widths[i] + 10f;
                }
                ApplyFocus();
            }

            public override string Hint => index == 0
                ? "Enter: bütün ayarları başlangıç değerine al   ·   Esc: geri"
                : "A/D: düğme   ·   Enter: menüye dön   ·   Esc: geri";

            private void Hover(int sub)
            {
                bool changed = sub != index;
                index = sub;
                if (Focused && changed) S.Sfx("SFX_UI_Hover", 0.35f);
                S.FocusRow(this);
                ApplyFocus();
            }

            public override void Step(int dir)
            {
                index = (index + dir + 2) % 2;
                S.Sfx("SFX_UI_Hover", 0.35f);
                ApplyFocus();
            }

            public override void Confirm()
            {
                if (index == 0) S.ResetDefaults();
                else S.Close();
            }

            public override void Refresh() => ApplyFocus();

            protected override void ApplyFocus()
            {
                for (int i = 0; i < 2; i++)
                {
                    bool on = Focused && i == index;
                    buttons[i].sprite = on ? S.panelLitSprite : S.panelSprite;
                    labels[i].color = on ? S.focusColor : S.textColor;
                    markers[i].enabled = on;
                }
            }
        }
    }
}
