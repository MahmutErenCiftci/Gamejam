using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Earthquake;
using AfetUyanisi.Chapters.Earthquake.Mode;
using AfetUyanisi.Chapters.FloodEvening;
using AfetUyanisi.Core;
using AfetUyanisi.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AfetUyanisi.UI
{
    /// <summary>
    /// Oyun içi duraklatma menüsü. İlk sahneden önce Resources/AfetUyanisiUI/PauseMenu prefab'ından kurulur
    /// (DontDestroyOnLoad), bu yüzden bölüm sahnelerine eklenmesi gerekmez. Ana menüde çalışmaz.
    ///
    /// Esc → "DURAKLATILDI": Devam Et / Ana Menüye Dön. Ana Menüye Dön → onay: "Ana menüye dönmek istiyor musun?"
    /// Evet → GameSceneRouter.ReturnToMenu (sonuç ekranındaki "Menüye Dön" ile aynı yol), Vazgeç → duraklatma menüsü.
    /// Esc: onay penceresinde vazgeçer, duraklatma menüsünde oyuna döner.
    ///
    /// Duraklatınca: Time.timeScale 0 (fizik/oyuncu, animasyon, sayaç, bölüm akışı durur), sesler duraklar
    /// (AudioListener.pause), GameInput.GameplayLocked açılır (menüde basılan tuşlar oyuna gitmez), EventSystem
    /// seçimi bırakılır. Devam edince hepsi eski haline döner; kilit bir kare sonra kalkar (Devam'ı seçen Enter
    /// arkadaki diyaloğu ilerletmesin).
    ///
    /// Esc'i kendisi kullanan açık bir mini oyun/panel varsa (çanta, kitaplık, dal, şifre, kapı kolu, tabela; deprem
    /// mini oyunları) Esc ona kalır, menü açılmaz. Sahne yüklendikten sonraki 0,5 sn'de ve başka bir şey zamanı
    /// durdurmuşken de açılmaz.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("Görünüm (ana menüyle aynı)")]
        [SerializeField] private Font font;
        [Tooltip("Assets/Art/Menu/UI/Panel")]
        [SerializeField] private Sprite panelSprite;
        [Tooltip("Assets/Art/Menu/UI/PanelLit")]
        [SerializeField] private Sprite panelLitSprite;
        [SerializeField] private Color textColor = new Color(0.95f, 0.92f, 0.85f);
        [SerializeField] private Color titleColor = new Color(1f, 0.74f, 0.4f);
        [SerializeField] private Color focusColor = new Color(1f, 0.82f, 0.45f);
        [SerializeField] private Color mutedColor = new Color(0.7f, 0.7f, 0.86f);
        [SerializeField] private int sortingOrder = 32000;

        private const string ResourcePath = "AfetUyanisiUI/PauseMenu";
        private const float FadeSeconds = 0.12f, LeaveFadeSeconds = 0.25f, SceneGraceSeconds = 0.5f, OwnerScanSeconds = 1f;
        private static readonly Color ShadowColor = new Color(0.02f, 0.02f, 0.06f, 0.9f);
        private static readonly Color PanelFill = new Color32(0x1A, 0x1C, 0x36, 0xFF);

        private enum View { Hidden, Pause, Confirm, Leaving }

        private static PauseMenu instance;

        /// <summary>Oyun şu an duraklatma menüsüyle durdurulmuş mu.</summary>
        public static bool IsPaused => instance != null && instance.view != View.Hidden;

        private View view = View.Hidden;
        private bool built;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform pausePanel, confirmPanel;
        private Image blackout;
        private ButtonVisual[] pauseButtons, confirmButtons;
        private int pauseFocus, confirmFocus = 1;
        private float fade, lastMoveY, lastMoveX, savedTimeScale = 1f, sceneLoadedAt;
        private int openedFrame = -1, unlockAfterFrame = -1;
        private GameObject previousSelection;
        private EventSystem ownEventSystem;

        // Esc'i kendisi kullanan oyun parçaları (sahne yüklenince ve saniyede bir yeniden bulunur).
        private readonly List<EmergencyBagMinigameController> bags = new List<EmergencyBagMinigameController>();
        private readonly List<BookshelfAnchorMinigame> shelves = new List<BookshelfAnchorMinigame>();
        private readonly List<BranchObstacle> branches = new List<BranchObstacle>();
        private readonly List<FloodPuzzleInteractionBase> puzzles = new List<FloodPuzzleInteractionBase>();
        private float nextOwnerScanAt;
        private bool ownerActiveLastFrame;

        private sealed class ButtonVisual
        {
            public Image Frame;
            public Text Label, Marker;
            // Renkli düğme (onay penceresi): düz piksel gövde, üstte açık, altta koyu şerit.
            public Accent Accent;
            public Image Fill, Light, Dark;
        }

        /// <summary>Renkli düğme tonları: gövde, odakta gövde, üst parlaklık, alt gölge.</summary>
        private sealed class Accent
        {
            public Color Base, Focus, Light, Dark;
        }

        // Evet yeşil, Vazgeç kırmızı (piksel tonlar; yazı krem, koyu gölgeli).
        private static readonly Accent GreenAccent = new Accent
        {
            Base = new Color32(0x1F, 0x8F, 0x47, 0xFF), Focus = new Color32(0x2B, 0xB3, 0x5A, 0xFF),
            Light = new Color32(0x5C, 0xD6, 0x7E, 0xFF), Dark = new Color32(0x12, 0x5A, 0x2C, 0xFF),
        };
        private static readonly Accent RedAccent = new Accent
        {
            Base = new Color32(0xB0, 0x2E, 0x26, 0xFF), Focus = new Color32(0xD9, 0x3B, 0x2E, 0xFF),
            Light = new Color32(0xF0, 0x6E, 0x5C, 0xFF), Dark = new Color32(0x6E, 0x1A, 0x16, 0xFF),
        };
        private static readonly Color Outline = new Color32(0x0A, 0x0A, 0x16, 0xFF);
        private static readonly Color FocusRing = new Color32(0xFF, 0xD2, 0x7E, 0xFF);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var prefab = Resources.Load<PauseMenu>(ResourcePath);
            var menu = prefab != null ? Instantiate(prefab) : new GameObject().AddComponent<PauseMenu>();
            if (prefab == null) Debug.LogWarning("[Duraklatma] Resources/" + ResourcePath + " yok; varsayılan font ve düz paneller kullanılıyor.");
            menu.name = "PauseMenu";
            DontDestroyOnLoad(menu.gameObject);
            instance = menu;
        }

        private void Awake()
        {
            EnsureBuilt();
            SetVisible(0f);
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnDestroy()
        {
            if (view != View.Hidden) RestoreGame(1f);
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------ akış

        private void Update()
        {
            // Devam Et'i seçen tuş bu karede oyuna gitmesin: kilit bir sonraki karede kalkar.
            if (unlockAfterFrame >= 0 && Time.frameCount > unlockAfterFrame)
            {
                unlockAfterFrame = -1;
                if (view == View.Hidden) GameInput.GameplayLocked = false;
            }

            float target = view == View.Pause || view == View.Confirm ? 1f : 0f;
            if (view != View.Leaving && !Mathf.Approximately(fade, target))
                SetVisible(Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / FadeSeconds));

            if (view == View.Hidden)
            {
                bool ownerNow = EscOwnerActive();
                if (GameInput.BackPressed && !ownerNow && !ownerActiveLastFrame && CanPause()) Pause();
                ownerActiveLastFrame = ownerNow;
                return;
            }
            if (view == View.Leaving) return;

            // Bir bölüm kodu zamanı geri açarsa yeniden durdur.
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            if (Time.frameCount == openedFrame) return;
            HandleKeys();
        }

        private bool CanPause()
        {
            if (SceneManager.GetActiveScene().name == GameSceneRouter.MainMenuScene) return false;
            if (Time.unscaledTime - sceneLoadedAt < SceneGraceSeconds) return false;
            // Başka bir sistem zamanı zaten durdurmuşsa (ör. editör testleri) karışma.
            return Time.timeScale > 0f;
        }

        public void Pause()
        {
            if (view != View.Hidden) return;
            EnsureBuilt();
            view = View.Pause;
            openedFrame = Time.frameCount;
            unlockAfterFrame = -1;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            GameInput.GameplayLocked = true;

            // Bölüm arayüzünde seçili bir buton varsa Enter ona gitmesin; fare için EventSystem şart.
            var es = EventSystem.current;
            previousSelection = es != null ? es.currentSelectedGameObject : null;
            if (es != null) es.SetSelectedGameObject(null);
            else CreateEventSystem();

            lastMoveY = ReadMoveY();
            lastMoveX = ReadMoveX();
            pauseFocus = 0;
            ShowPanel(View.Pause);
            Debug.Log("[Duraklatma] Oyun duraklatıldı.");
        }

        public void Resume()
        {
            if (view != View.Pause && view != View.Confirm) return;
            view = View.Hidden;
            RestoreGame(savedTimeScale);
            unlockAfterFrame = Time.frameCount;   // GameplayLocked bir kare daha açık kalır
            GameInput.GameplayLocked = true;
            var es = EventSystem.current;
            if (es != null && previousSelection != null && previousSelection.activeInHierarchy) es.SetSelectedGameObject(previousSelection);
            previousSelection = null;
            Debug.Log("[Duraklatma] Oyuna devam.");
        }

        private void RestoreGame(float timeScale)
        {
            Time.timeScale = timeScale;
            AudioListener.pause = false;
            GameInput.GameplayLocked = false;
            if (ownEventSystem != null) { Destroy(ownEventSystem.gameObject); ownEventSystem = null; }
        }

        private void OpenConfirm()
        {
            view = View.Confirm;
            confirmFocus = 1;   // güvenli seçenek: Vazgeç
            ShowPanel(View.Confirm);
        }

        private void CancelConfirm()
        {
            view = View.Pause;
            pauseFocus = 1;
            ShowPanel(View.Pause);
        }

        private IEnumerator ReturnToMenu()
        {
            view = View.Leaving;
            group.blocksRaycasts = true;
            blackout.gameObject.SetActive(true);
            for (float t = 0f; t < LeaveFadeSeconds; t += Time.unscaledDeltaTime)
            {
                blackout.color = new Color(0f, 0f, 0f, t / LeaveFadeSeconds);
                yield return null;
            }
            blackout.color = Color.black;
            // Menü normal zamanla açılmalı; kararma ana menünün kendi açılışı (siyahtan) ile birleşir.
            RestoreGame(1f);
            previousSelection = null;
            Debug.Log("[Duraklatma] Ana menüye dönülüyor.");
            GameSceneRouter.ReturnToMenu();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            sceneLoadedAt = Time.unscaledTime;
            nextOwnerScanAt = 0f;
            ownerActiveLastFrame = false;
            if (view != View.Hidden)
            {
                // Duraklatılmışken sahne değiştiyse (ör. Evet) her şeyi normale al ve kapan.
                RestoreGame(view == View.Leaving ? 1f : savedTimeScale);
                view = View.Hidden;
                unlockAfterFrame = -1;
                previousSelection = null;
            }
            if (blackout != null) blackout.gameObject.SetActive(false);
            SetVisible(0f);
        }

        // ------------------------------------------------------------------ girdi

        private static float ReadMoveY() => GameInput.ReadUnlocked(() => GameInput.MoveVector.y);
        private static float ReadMoveX() => GameInput.ReadUnlocked(() => GameInput.MoveVector.x);

        private void HandleKeys()
        {
            bool back = GameInput.ReadUnlocked(() => GameInput.BackPressed);
            bool confirm = GameInput.ReadUnlocked(() => GameInput.ConfirmPressed);
            float y = ReadMoveY(), x = ReadMoveX();
            bool prev = (y > 0.5f && lastMoveY <= 0.5f) || (x < -0.5f && lastMoveX >= -0.5f);
            bool next = (y < -0.5f && lastMoveY >= -0.5f) || (x > 0.5f && lastMoveX <= 0.5f);
            lastMoveY = y;
            lastMoveX = x;

            if (view == View.Pause)
            {
                if (back) { Resume(); return; }
                if (prev || next) FocusPause(1 - pauseFocus);
                if (confirm) ActivatePause(pauseFocus);
            }
            else if (view == View.Confirm)
            {
                if (back) { CancelConfirm(); return; }
                if (prev || next) FocusConfirm(1 - confirmFocus);
                if (confirm) ActivateConfirm(confirmFocus);
            }
        }

        private void ActivatePause(int index)
        {
            if (view != View.Pause) return;
            if (index == 0) Resume();
            else OpenConfirm();
        }

        private void ActivateConfirm(int index)
        {
            if (view != View.Confirm) return;
            if (index == 0) StartCoroutine(ReturnToMenu());
            else CancelConfirm();
        }

        /// <summary>Esc'i kendisi kullanan bir mini oyun/panel açık mı (açıksa Esc ona kalır).</summary>
        private bool EscOwnerActive()
        {
            if (EarthquakeMinigameHub.AnyOpen) return true;
            if (Time.unscaledTime >= nextOwnerScanAt)
            {
                nextOwnerScanAt = Time.unscaledTime + OwnerScanSeconds;
                Refill(bags);
                Refill(shelves);
                Refill(branches);
                Refill(puzzles);
            }
            foreach (var b in bags) if (b != null && b.IsOpen) return true;
            foreach (var s in shelves) if (s != null && s.IsOpen) return true;
            foreach (var b in branches) if (b != null && b.IsOpen) return true;
            foreach (var p in puzzles) if (p != null && p.IsOpen) return true;
            return false;
        }

        private static void Refill<T>(List<T> list) where T : Object
        {
            list.Clear();
            list.AddRange(FindObjectsByType<T>(FindObjectsInactive.Exclude));
        }

        private void CreateEventSystem()
        {
            var go = new GameObject("PauseEventSystem");
            go.transform.SetParent(transform, false);
            ownEventSystem = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ------------------------------------------------------------------ görünüm

        private void ShowPanel(View which)
        {
            pausePanel.gameObject.SetActive(which == View.Pause);
            confirmPanel.gameObject.SetActive(which == View.Confirm);
            FocusPause(pauseFocus);
            FocusConfirm(confirmFocus);
        }

        private void FocusPause(int index)
        {
            pauseFocus = Mathf.Clamp(index, 0, 1);
            for (int i = 0; i < pauseButtons.Length; i++) Apply(pauseButtons[i], i == pauseFocus);
        }

        private void FocusConfirm(int index)
        {
            confirmFocus = Mathf.Clamp(index, 0, 1);
            for (int i = 0; i < confirmButtons.Length; i++) Apply(confirmButtons[i], i == confirmFocus);
        }

        private void Apply(ButtonVisual b, bool focused)
        {
            if (b.Accent != null)
            {
                // Odakta: altın çerçeve, daha parlak gövde ve ▶; yazı hep krem (renkli zeminde okunur).
                b.Frame.color = focused ? FocusRing : Outline;
                b.Fill.color = focused ? b.Accent.Focus : b.Accent.Base;
                b.Light.color = b.Accent.Light;
                b.Dark.color = b.Accent.Dark;
                b.Label.color = focused ? Color.white : textColor;
                b.Marker.color = Color.white;
                b.Marker.enabled = focused;
                return;
            }
            b.Frame.sprite = focused && panelLitSprite != null ? panelLitSprite : panelSprite;
            if (panelSprite == null) b.Frame.color = focused ? titleColor : mutedColor;
            b.Label.color = focused ? focusColor : textColor;
            b.Marker.enabled = focused;
        }

        private void SetVisible(float k)
        {
            fade = k;
            if (group == null) return;
            group.alpha = k;
            group.blocksRaycasts = view == View.Pause || view == View.Confirm || view == View.Leaving;
            float e = 1f - (1f - k) * (1f - k);
            float dy = Mathf.Round((1f - e) * -6f);
            pausePanel.anchoredPosition = new Vector2(0f, dy);
            confirmPanel.anchoredPosition = new Vector2(0f, dy);
            canvas.gameObject.SetActive(k > 0f || view == View.Leaving);
        }

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            var canvasGo = new GameObject("PauseCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;
            // Ana menüyle aynı ölçek: 640x360 referans.
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            group = canvasGo.AddComponent<CanvasGroup>();
            var root = (RectTransform)canvasGo.transform;

            var backdrop = Img("Backdrop", root, null, new Color(0.01f, 0.01f, 0.04f, 0.6f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            // Duraklatma: başlık, iki buton, ipucu.
            pausePanel = Window("PausePanel", root, new Vector2(240f, 120f));
            var title = Txt("Title", pausePanel, "DURAKLATILDI", 16, titleColor, TextAnchor.MiddleCenter);
            TL(title.rectTransform, 0f, 8f, 240f, 20f);
            pauseButtons = new[]
            {
                MakeButton(pausePanel, "ResumeButton", "Devam Et", 28f, 36f, 184f, () => FocusPause(0), () => ActivatePause(0)),
                MakeButton(pausePanel, "MenuButton", "Ana Menüye Dön", 28f, 64f, 184f, () => FocusPause(1), () => ActivatePause(1)),
            };
            var hint = Txt("Hint", pausePanel, "W/S: seç   ·   Enter: onayla   ·   Esc: devam et", 10, mutedColor, TextAnchor.MiddleCenter);
            TL(hint.rectTransform, 0f, 96f, 240f, 14f);

            // Onay: soru, açıklama, Evet / Vazgeç (varsayılan Vazgeç).
            confirmPanel = Window("ConfirmPanel", root, new Vector2(272f, 116f));
            var question = Txt("Question", confirmPanel, "Ana menüye dönmek istiyor musun?", 12, textColor, TextAnchor.MiddleCenter);
            TL(question.rectTransform, 0f, 12f, 272f, 18f);
            var note = Txt("Note", confirmPanel, "Bu bölümdeki ilerlemen kaybolur.", 10, mutedColor, TextAnchor.MiddleCenter);
            TL(note.rectTransform, 0f, 31f, 272f, 14f);
            confirmButtons = new[]
            {
                MakeButton(confirmPanel, "YesButton", "Evet", 26f, 56f, 104f, () => FocusConfirm(0), () => ActivateConfirm(0), GreenAccent),
                MakeButton(confirmPanel, "CancelButton", "Vazgeç", 142f, 56f, 104f, () => FocusConfirm(1), () => ActivateConfirm(1), RedAccent),
            };
            var confirmHint = Txt("Hint", confirmPanel, "A/D: seç   ·   Enter: onayla   ·   Esc: vazgeç", 10, mutedColor, TextAnchor.MiddleCenter);
            TL(confirmHint.rectTransform, 0f, 90f, 272f, 14f);

            blackout = Img("Blackout", root, null, new Color(0f, 0f, 0f, 0f));
            Stretch(blackout.rectTransform);
            blackout.raycastTarget = true;
            blackout.gameObject.SetActive(false);

            pausePanel.gameObject.SetActive(false);
            confirmPanel.gameObject.SetActive(false);
        }

        private RectTransform Window(string name, RectTransform root, Vector2 size)
        {
            var frame = Img(name, root, null, Color.clear);
            frame.raycastTarget = true;
            var rt = frame.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            // Panel'in içi hafif saydam; altına opak zemin (Ayarlar ekranıyla aynı).
            var fill = Img("Fill", rt, null, PanelFill);
            Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            var border = Img("Frame", rt, panelSprite, panelSprite != null ? Color.white : mutedColor);
            Stretch(border.rectTransform);
            if (panelSprite == null) border.enabled = false;
            return rt;
        }

        private ButtonVisual MakeButton(RectTransform parent, string name, string label, float x, float y, float w,
            System.Action hover, System.Action click, Accent accent = null)
        {
            const float h = 22f;
            var frame = Img(name, parent, accent != null ? null : panelSprite, accent != null ? Outline : Color.white);
            frame.raycastTarget = true;
            TL(frame.rectTransform, x, y, w, h);
            Image fill = null, light = null, dark = null;
            if (accent != null)
            {
                // 1 px çerçeve (odakta altın), içte gövde; üstte 1 px açık, altta 2 px koyu şerit: basılabilir görünüm.
                fill = Img("Fill", frame.rectTransform, null, accent.Base);
                Stretch(fill.rectTransform);
                fill.rectTransform.offsetMin = new Vector2(1f, 1f);
                fill.rectTransform.offsetMax = new Vector2(-1f, -1f);
                light = Img("Light", fill.rectTransform, null, accent.Light);
                light.rectTransform.anchorMin = new Vector2(0f, 1f);
                light.rectTransform.anchorMax = Vector2.one;
                light.rectTransform.pivot = new Vector2(0.5f, 1f);
                light.rectTransform.sizeDelta = new Vector2(0f, 1f);
                dark = Img("Dark", fill.rectTransform, null, accent.Dark);
                dark.rectTransform.anchorMin = Vector2.zero;
                dark.rectTransform.anchorMax = new Vector2(1f, 0f);
                dark.rectTransform.pivot = new Vector2(0.5f, 0f);
                dark.rectTransform.sizeDelta = new Vector2(0f, 2f);
            }
            var text = Txt("Text", frame.rectTransform, label, 14, textColor, TextAnchor.MiddleCenter);
            TL(text.rectTransform, 0f, 1f, w, h);
            var marker = Txt("Arrow", frame.rectTransform, "▶", 11, titleColor, TextAnchor.MiddleCenter);
            TL(marker.rectTransform, 4f, 1f, 12f, h);
            var pointer = frame.gameObject.AddComponent<SettingsPointer>();
            pointer.Entered = e => { if (view == View.Pause || view == View.Confirm) hover(); };
            pointer.Clicked = e => { if (Time.frameCount != openedFrame) { hover(); click(); } };
            return new ButtonVisual { Frame = frame, Label = text, Marker = marker, Accent = accent, Fill = fill, Light = light, Dark = dark };
        }

        private Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text Txt(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = new Vector2(1f, -1f);
            return t;
        }

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
    }
}
