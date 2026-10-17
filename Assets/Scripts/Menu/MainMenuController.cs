using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AfetUyanisi.Menu
{
    // Ana menü akışı:
    //   Ana ekran (logo + Yeni Oyun / Ayarlar / Çıkış, bulut yok)
    //   → Yeni Oyun: düşünce baloncukları ve iki rüya bulutu (DEPREM, SEL) belirir
    //   → bulut seçilir (fare veya A/D, oklar); odada önizleme oynar
    //   → Bölümü Başlat veya Enter: kararma ve bölüm yükleme
    //   Esc: afet seçiminden ana ekrana döner (intro tekrar oynamaz).
    // Odadaki önizlemeleri MenuEffectsController, SelectionChanged/SelectionCleared olaylarıyla yönetir.
    public class MainMenuController : MonoBehaviour
    {
        enum State { Main, Selecting, Loading }

        [Header("Ana ekran")]
        [SerializeField] SpriteFader title;
        [SerializeField] CanvasGroup mainButtons;
        [SerializeField] Button newGameButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        [Header("Afet seçimi")]
        [SerializeField] DisasterBubble[] bubbles;
        [SerializeField] ThoughtTrail thoughtTrail;
        [SerializeField] CanvasGroup selectionPanel;
        [SerializeField] Button startChapterButton;
        [SerializeField] Button backButton;
        [SerializeField] float bubbleStagger = 0.25f;
        [SerializeField] float panelFadeDuration = 0.3f;

        [Header("Ayarlar")]
        [SerializeField] GameObject settingsPanel;
        [SerializeField] Button settingsBackButton;

        [Header("Bildirim")]
        [SerializeField] GameObject tooltip;
        [SerializeField] Text tooltipText;
        [SerializeField] float tooltipDuration = 2f;

        [Header("Geçiş")]
        [SerializeField] CanvasGroup fadePanel;
        [SerializeField] float fadeDuration = 0.5f;

        public event Action<MenuChapterLoader.Chapter> SelectionChanged;
        public event Action SelectionCleared;

        State state = State.Main;
        int selectedIndex = -1;
        Coroutine tooltipRoutine;
        Coroutine panelRoutine;

        // Intro (MENU-04) oynarken menü girdisi kapalı tutulur.
        public bool InputEnabled { get; set; } = true;

        bool SettingsOpen => settingsPanel != null && settingsPanel.activeSelf;

        void Awake()
        {
            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i].Clicked += OnBubbleClicked;

            newGameButton.onClick.AddListener(EnterSelection);
            settingsButton.onClick.AddListener(OpenSettings);
            exitButton.onClick.AddListener(ExitGame);
            startChapterButton.onClick.AddListener(StartSelectedChapter);
            backButton.onClick.AddListener(ExitSelection);
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(CloseSettings);

            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (tooltip != null) tooltip.SetActive(false);
            SetGroup(selectionPanel, 0f);
            if (fadePanel != null)
            {
                fadePanel.alpha = 0f;
                fadePanel.blocksRaycasts = false;
            }
            UpdateStartButton();
        }

        void Update()
        {
            if (!InputEnabled || state == State.Loading)
                return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (SettingsOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) CloseSettings();
                return;
            }

            if (state == State.Main)
            {
                // Yukarı/aşağı ve Enter, EventSystem'in buton gezinmesiyle çalışır; burada sadece odak kaybını toparla.
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null &&
                    (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                     keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame))
                    FocusMainButtons();
                return;
            }

            // Afet seçimi
            if (keyboard.escapeKey.wasPressedThisFrame)
                ExitSelection();
            else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                MoveSelection(-1);
            else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                MoveSelection(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                StartSelectedChapter();
        }

        // Intro bitince çağrılır.
        public void OnIntroFinished() => FocusMainButtons();

        void FocusMainButtons()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
        }

        public void EnterSelection()
        {
            if (!InputEnabled || state != State.Main)
                return;
            state = State.Selecting;
            HideTooltip();
            // A/D'nin seçimdeki butonlar arasında gezinmemesi için UI odağını bırak.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (title != null) StartCoroutine(FadeSprites(title, title.Alpha, 0f));
            if (thoughtTrail != null) thoughtTrail.Show();
            float delay = thoughtTrail != null ? thoughtTrail.TotalDuration : 0f;
            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i].Show(delay + i * bubbleStagger);

            CrossFadePanels(mainButtons, selectionPanel);
            selectedIndex = -1;
            UpdateStartButton();
        }

        public void ExitSelection()
        {
            if (state != State.Selecting)
                return;
            state = State.Main;
            ClearSelection();
            foreach (DisasterBubble bubble in bubbles) bubble.Hide();
            if (thoughtTrail != null) thoughtTrail.Hide();
            if (title != null) StartCoroutine(FadeSprites(title, title.Alpha, 1f));
            CrossFadePanels(selectionPanel, mainButtons);
            FocusMainButtons();
        }

        void MoveSelection(int direction)
        {
            int count = bubbles.Length;
            if (selectedIndex < 0)
                Select(direction < 0 ? 0 : count - 1);
            else
                Select((selectedIndex + direction + count) % count);
        }

        void OnBubbleClicked(DisasterBubble bubble)
        {
            if (!InputEnabled || state != State.Selecting)
                return;
            Select(Array.IndexOf(bubbles, bubble));
        }

        // Aynı bulut tekrar seçilse de bildirilir; deprem önizlemesi böylece yeniden oynatılabilir.
        void Select(int index)
        {
            if (index < 0 || index >= bubbles.Length)
                return;
            selectedIndex = index;
            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i].SetSelected(i == index);
            HideTooltip();
            UpdateStartButton();
            SelectionChanged?.Invoke(bubbles[index].Chapter);
        }

        void ClearSelection()
        {
            selectedIndex = -1;
            foreach (DisasterBubble bubble in bubbles) bubble.SetSelected(false);
            UpdateStartButton();
            SelectionCleared?.Invoke();
        }

        void UpdateStartButton()
        {
            if (startChapterButton != null)
                startChapterButton.interactable = selectedIndex >= 0;
        }

        public void StartSelectedChapter()
        {
            if (state != State.Selecting)
                return;
            if (selectedIndex < 0)
            {
                ShowTooltip("Önce bir rüya seç: DEPREM ya da SEL.");
                return;
            }
            StartCoroutine(LoadChapterRoutine(bubbles[selectedIndex].Chapter));
        }

        IEnumerator LoadChapterRoutine(MenuChapterLoader.Chapter chapter)
        {
            state = State.Loading;
            yield return Fade(0f, 1f);

            if (MenuChapterLoader.TryLoad(chapter))
                yield break;

            // Bölüm sahnesi henüz hazır değilse seçime geri dön.
            yield return Fade(1f, 0f);
            state = State.Selecting;
            ShowTooltip($"{MenuChapterLoader.DisplayName(chapter)} bölümü henüz hazır değil.");
        }

        IEnumerator Fade(float from, float to)
        {
            if (fadePanel == null)
                yield break;
            fadePanel.blocksRaycasts = true;
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                fadePanel.alpha = Mathf.Lerp(from, to, t / fadeDuration);
                yield return null;
            }
            fadePanel.alpha = to;
            fadePanel.blocksRaycasts = to > 0f;
        }

        void CrossFadePanels(CanvasGroup from, CanvasGroup to)
        {
            if (panelRoutine != null) StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(CrossFade(from, to));
        }

        IEnumerator CrossFade(CanvasGroup from, CanvasGroup to)
        {
            SetGroup(from, from != null ? from.alpha : 0f, false);
            float fromStart = from != null ? from.alpha : 0f;
            float toStart = to != null ? to.alpha : 0f;
            for (float t = 0f; t < panelFadeDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / panelFadeDuration;
                SetGroup(from, Mathf.Lerp(fromStart, 0f, k), false);
                SetGroup(to, Mathf.Lerp(toStart, 1f, k), false);
                yield return null;
            }
            SetGroup(from, 0f);
            SetGroup(to, 1f);
            panelRoutine = null;
        }

        static void SetGroup(CanvasGroup group, float alpha, bool updateRaycast = true)
        {
            if (group == null) return;
            group.alpha = alpha;
            // Görünmeyen paneldeki butonlar tıklanmasın; interactable'a dokunulmaz (gri tona geçmesin).
            group.blocksRaycasts = updateRaycast ? alpha > 0.99f : false;
        }

        static IEnumerator FadeSprites(SpriteFader fader, float from, float to)
        {
            const float duration = 0.3f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                fader.Alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            fader.Alpha = to;
        }

        void OpenSettings()
        {
            if (!InputEnabled || state != State.Main || settingsPanel == null)
                return;
            HideTooltip();
            settingsPanel.SetActive(true);
            if (EventSystem.current != null && settingsBackButton != null)
                EventSystem.current.SetSelectedGameObject(settingsBackButton.gameObject);
        }

        void CloseSettings()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            FocusMainButtons();
        }

        void ExitGame()
        {
            if (!InputEnabled || state != State.Main)
                return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowTooltip(string message)
        {
            if (tooltip == null)
                return;
            if (tooltipText != null)
                tooltipText.text = message;
            tooltip.SetActive(true);
            if (tooltipRoutine != null)
                StopCoroutine(tooltipRoutine);
            tooltipRoutine = StartCoroutine(HideTooltipAfterDelay());
        }

        IEnumerator HideTooltipAfterDelay()
        {
            yield return new WaitForSecondsRealtime(tooltipDuration);
            HideTooltip();
        }

        void HideTooltip()
        {
            if (tooltipRoutine != null)
            {
                StopCoroutine(tooltipRoutine);
                tooltipRoutine = null;
            }
            if (tooltip != null)
                tooltip.SetActive(false);
        }
    }
}
