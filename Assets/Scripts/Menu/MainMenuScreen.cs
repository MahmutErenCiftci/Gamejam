using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Menu
{
    /// <summary>
    /// Ana menü (MainMenu.unity): arka planda oyunun izometrik yatak odası (MenuBedroom prefab'ı, Cenk uyuyor, saat 07:00),
    /// solda logo + slogan + Yeni Oyun / Ayarlar / Çıkış, Cenk'in kafasından yükselen dört rüya bulutu
    /// (Deprem, Sel açık; Orman Yangını, Heyelan "Yakında").
    ///
    /// Girdi (hepsi GameInput; fare EventSystem/InputSystemUIInputModule ile):
    ///   W/S, ↑/↓  sol listede gezin          A/D, ←/→  bulut seç (ilk basışta Sel)
    ///   Enter/Space  seçili bulutu başlat ya da odaktaki butonu kullan     Esc  bulut seçimini bırak
    ///   Fare: buluta gelince seçer (önizleme), tıklayınca başlatır; kilitliye tıklayınca "Yakında".
    ///   Açılış (≤2 sn) herhangi bir tuş/tıkla atlanır; atlayan tuş aynı karede başka bir şey yapmaz.
    /// Başlatma: arayüz 0,4 sn'de söner, (Sel'de kamera Bedroom kadrajına kayar), kısa kararma,
    /// MenuChapterLoader.TryLoad → GameSceneRouter.LoadChapter.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        private enum State { Intro, Buttons, Bubbles, Settings, Loading }

        [Header("Arayüz")]
        [SerializeField] private CanvasGroup uiGroup;
        [SerializeField] private CanvasGroup blackout;
        [SerializeField] private MenuListItem newGameButton;
        [SerializeField] private MenuListItem settingsButton;
        [SerializeField] private MenuListItem exitButton;
        [SerializeField] private MenuChoiceBubble[] bubbles;
        [SerializeField] private Text hint;
        [SerializeField] private CanvasGroup toast;
        [SerializeField] private Text toastText;
        [SerializeField] private MenuSettingsPanel settings;
        [SerializeField] private RectTransform sleepZ;

        [Header("Oda")]
        [SerializeField] private MenuRoomPreview room;
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Bedroom.unity kamerasının konumu: Sel başlarken kamera buraya kayar (aynı kadraj, kesintisiz geçiş).")]
        [SerializeField] private Vector3 gameplayCameraPosition = new Vector3(0.25f, 2.375f, -10f);

        [Header("Süreler")]
        [SerializeField] private float introRoomFade = 0.8f;
        [SerializeField] private float introUiFade = 0.5f;
        [SerializeField] private float startUiFade = 0.4f;
        [SerializeField] private float startBlackFade = 0.3f;

        private const string HintButtons = "W/S: menü   ·   A/D ya da fare: rüya seç   ·   Enter: seç";
        private const string HintBubbles = "A/D: rüya değiştir   ·   Enter ya da tık: başla   ·   Esc: geri";
        private const string HintSettings = "W/S: satır   ·   A/D: ses   ·   Enter: değiştir   ·   Esc: geri";

        private State state = State.Intro;
        private MenuListItem[] buttons;
        private int buttonFocus;
        private int bubbleIndex = -1;
        private float lastMoveY;
        private bool skipInputThisFrame;
        private Coroutine toastRoutine;
        private Coroutine introRoutine;
        private Vector2 zBase;

        private void Awake()
        {
            MenuSettingsPanel.ApplySaved();
            buttons = new[] { newGameButton, settingsButton, exitButton };
            foreach (MenuListItem b in buttons)
            {
                if (b == null) continue;
                b.Hovered += OnButtonHovered;
                b.Clicked += OnButtonClicked;
            }
            foreach (MenuChoiceBubble bubble in bubbles)
            {
                if (bubble == null) continue;
                bubble.Hovered += OnBubbleHovered;
                bubble.Clicked += OnBubbleClicked;
            }
            if (settings != null) settings.Closed += OnSettingsClosed;
            if (toast != null) toast.alpha = 0f;
            if (sleepZ != null) zBase = sleepZ.anchoredPosition;
            SetGroup(uiGroup, 0f);
            SetGroup(blackout, 1f);
            FocusButton(0);
        }

        private void Start() => introRoutine = StartCoroutine(Intro());

        // ---------------------------------------------------------------- akış

        private IEnumerator Intro()
        {
            state = State.Intro;
            if (hint != null) hint.text = HintButtons;
            yield return Fade(blackout, 1f, 0f, introRoomFade);
            yield return Fade(uiGroup, 0f, 1f, introUiFade);
            FinishIntro();
        }

        private void FinishIntro()
        {
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
            SetGroup(blackout, 0f);
            SetGroup(uiGroup, 1f);
            state = State.Buttons;
            SetHint();
        }

        private void Update()
        {
            AnimateSleepZ();
            if (skipInputThisFrame) { skipInputThisFrame = false; return; }

            switch (state)
            {
                case State.Intro:
                    if (AnyInput())
                    {
                        FinishIntro();
                        skipInputThisFrame = true;   // atlayan tuş bir sonraki karede de işlenmesin
                    }
                    break;
                case State.Settings:
                    if (settings != null) settings.Tick();
                    break;
                case State.Buttons:
                case State.Bubbles:
                    HandleMenuKeys();
                    break;
            }
        }

        private static bool AnyInput() =>
            GameInput.ContinuePressed || GameInput.ConfirmPressed || GameInput.BackPressed ||
            GameInput.LeftPressed || GameInput.RightPressed || GameInput.MoveVector.sqrMagnitude > 0.01f ||
            GameInput.ChoiceKeyPressed != 0;

        private void HandleMenuKeys()
        {
            float y = GameInput.MoveVector.y;
            bool up = y > 0.5f && lastMoveY <= 0.5f;
            bool down = y < -0.5f && lastMoveY >= -0.5f;
            lastMoveY = y;

            if (GameInput.LeftPressed) { StepBubble(-1); return; }
            if (GameInput.RightPressed) { StepBubble(1); return; }

            if (up || down)
            {
                if (state == State.Bubbles) EnterButtons();
                else FocusButton((buttonFocus + (up ? buttons.Length - 1 : 1)) % buttons.Length);
                return;
            }

            if (GameInput.BackPressed && state == State.Bubbles) { EnterButtons(); return; }

            if (GameInput.ConfirmPressed)
            {
                if (state == State.Bubbles) StartSelected();
                else Activate(buttons[buttonFocus]);
            }
        }

        // ---------------------------------------------------------------- butonlar

        private void FocusButton(int index)
        {
            buttonFocus = Mathf.Clamp(index, 0, buttons.Length - 1);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) buttons[i].SetFocused(i == buttonFocus && state != State.Bubbles);
        }

        private void EnterButtons()
        {
            state = State.Buttons;
            SelectBubble(-1);
            FocusButton(buttonFocus);
            SetHint();
        }

        private void OnButtonHovered(MenuListItem item)
        {
            if (state != State.Buttons && state != State.Bubbles) return;
            if (state == State.Bubbles) { state = State.Buttons; SelectBubble(-1); SetHint(); }
            FocusButton(System.Array.IndexOf(buttons, item));
        }

        private void OnButtonClicked(MenuListItem item)
        {
            if (state != State.Buttons && state != State.Bubbles) return;
            Activate(item);
        }

        private void Activate(MenuListItem item)
        {
            if (item == newGameButton)
            {
                // Yeni Oyun: seçili rüya yoksa önce Sel seçilir (varsayılan), ikinci Enter başlatır.
                if (bubbleIndex >= 0) StartSelected();
                else { SelectBubble(IndexOf(MenuChapterLoader.Chapter.Flood)); ShowToast("Rüyanı seç, Enter ile başla."); }
            }
            else if (item == settingsButton) OpenSettings();
            else if (item == exitButton) Quit();
        }

        // ---------------------------------------------------------------- bulutlar

        private int IndexOf(MenuChapterLoader.Chapter chapter)
        {
            for (int i = 0; i < bubbles.Length; i++)
                if (bubbles[i] != null && !bubbles[i].Locked && bubbles[i].Chapter == chapter) return i;
            return -1;
        }

        private void StepBubble(int dir)
        {
            if (bubbleIndex < 0) { SelectBubble(IndexOf(MenuChapterLoader.Chapter.Flood)); return; }
            int n = bubbles.Length;
            for (int k = 1; k <= n; k++)
            {
                int i = (bubbleIndex + dir * k + n * 4) % n;
                if (bubbles[i] != null && !bubbles[i].Locked) { SelectBubble(i); return; }
            }
        }

        private void SelectBubble(int index)
        {
            bool changed = index != bubbleIndex;
            bubbleIndex = index;
            for (int i = 0; i < bubbles.Length; i++)
                if (bubbles[i] != null) bubbles[i].SetSelected(i == index);

            if (index >= 0)
            {
                if (state != State.Bubbles) { state = State.Bubbles; FocusButton(buttonFocus); SetHint(); }
                if (changed && room != null)
                    room.SetMode(bubbles[index].Chapter == MenuChapterLoader.Chapter.Flood
                        ? MenuRoomPreview.Mode.Flood : MenuRoomPreview.Mode.Earthquake);
            }
            else if (room != null) room.SetMode(MenuRoomPreview.Mode.None);
        }

        private void OnBubbleHovered(MenuChoiceBubble bubble)
        {
            if ((state != State.Buttons && state != State.Bubbles) || bubble.Locked) return;
            SelectBubble(System.Array.IndexOf(bubbles, bubble));
        }

        private void OnBubbleClicked(MenuChoiceBubble bubble)
        {
            if (state != State.Buttons && state != State.Bubbles) return;
            if (bubble.Locked)
            {
                bubble.Nudge();
                ShowToast("Yakında! Şimdilik Deprem ve Sel oynanabilir.");
                return;
            }
            SelectBubble(System.Array.IndexOf(bubbles, bubble));
            StartSelected();
        }

        private void StartSelected()
        {
            if (bubbleIndex < 0 || state == State.Loading) return;
            StartCoroutine(StartChapter(bubbles[bubbleIndex].Chapter));
        }

        private IEnumerator StartChapter(MenuChapterLoader.Chapter chapter)
        {
            state = State.Loading;
            if (room != null) room.SetMode(chapter == MenuChapterLoader.Chapter.Flood ? MenuRoomPreview.Mode.None : MenuRoomPreview.Mode.Earthquake);
            Debug.Log($"[Menu] {MenuChapterLoader.DisplayName(chapter)} başlıyor.");

            // Arayüz söner; Sel'de kamera Bedroom kadrajına kayar (oda aynı, Cenk aynı yatakta uyanır).
            Vector3 from = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            bool pan = chapter == MenuChapterLoader.Chapter.Flood && cameraTransform != null;
            for (float t = 0f; t < startUiFade; t += Time.unscaledDeltaTime)
            {
                float k = t / startUiFade;
                SetGroup(uiGroup, 1f - k, false);
                if (pan) MoveCamera(Vector3.Lerp(from, gameplayCameraPosition, Smooth(k)));
                yield return null;
            }
            SetGroup(uiGroup, 0f);
            if (pan) MoveCamera(gameplayCameraPosition);
            yield return Fade(blackout, 0f, 1f, startBlackFade);

            if (MenuChapterLoader.TryLoad(chapter)) yield break;

            // Bölüm sahnesi build listesinde yoksa menüye geri dön.
            if (pan) MoveCamera(from);
            yield return Fade(blackout, 1f, 0f, startBlackFade);
            SetGroup(uiGroup, 1f);
            state = State.Bubbles;
            ShowToast($"{MenuChapterLoader.DisplayName(chapter)} bölümü henüz hazır değil.");
        }

        private void MoveCamera(Vector3 p)
        {
            // Piksel ızgarasına oturt (1/64 birim).
            p.x = Mathf.Round(p.x * 64f) / 64f;
            p.y = Mathf.Round(p.y * 64f) / 64f;
            if (room != null) room.SetCameraBase(p);
            else cameraTransform.position = p;
        }

        private static float Smooth(float k) => k * k * (3f - 2f * k);

        // ---------------------------------------------------------------- ayarlar / çıkış

        private void OpenSettings()
        {
            if (settings == null) return;
            SelectBubble(-1);
            state = State.Settings;
            settings.Open();
            SetHint();
        }

        private void OnSettingsClosed()
        {
            state = State.Buttons;
            FocusButton(buttonFocus);
            SetHint();
            skipInputThisFrame = true;
        }

        private void Quit()
        {
            Debug.Log("[Menu] Çıkış.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- yardımcılar

        private void SetHint()
        {
            if (hint == null) return;
            hint.text = state == State.Settings ? HintSettings : state == State.Bubbles ? HintBubbles : HintButtons;
        }

        private void ShowToast(string message)
        {
            if (toast == null) return;
            if (toastText != null) toastText.text = message;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            yield return Fade(toast, toast.alpha, 1f, 0.15f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Fade(toast, 1f, 0f, 0.3f);
            toastRoutine = null;
        }

        // Cenk'in üstünde süzülen küçük "z" (2,4 sn döngü, tam piksel adımlarla).
        private void AnimateSleepZ()
        {
            if (sleepZ == null) return;
            float t = (Time.unscaledTime % 2.4f) / 2.4f;
            sleepZ.anchoredPosition = zBase + new Vector2(Mathf.Round(t * 4f), Mathf.Round(t * 10f));
            var g = sleepZ.GetComponent<Graphic>();
            if (g != null) { Color c = g.color; c.a = Mathf.Sin(t * Mathf.PI) * 0.8f; g.color = c; }
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
        {
            if (group == null) yield break;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetGroup(group, Mathf.Lerp(from, to, t / seconds), false);
                yield return null;
            }
            SetGroup(group, to);
        }

        private static void SetGroup(CanvasGroup group, float alpha, bool updateRaycast = true)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.blocksRaycasts = updateRaycast ? alpha > 0.99f : false;
        }
    }
}
