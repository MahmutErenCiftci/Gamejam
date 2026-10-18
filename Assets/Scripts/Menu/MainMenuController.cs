using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [Tooltip("Seçim sırasında odayı karartan katman.")]
        [SerializeField] SpriteFader selectionDim;
        [Tooltip("Seçili rüyanın adı ve kısa açıklaması.")]
        [SerializeField] Text selectionInfo;
        [SerializeField] string idleInfo = "Bir bulut seç, sonra Bölümü Başlat'a bas.";
        [Tooltip("Bulutlarla aynı sırada, her rüyanın kısa açıklaması.")]
        [SerializeField] string[] bubbleInfo;
        [SerializeField] Color infoTitleColor = new Color(1f, 0.71f, 0.35f);
        [Tooltip("Seçimde durdurulur; z harfleri düşünce yolunun üstüne binmesin.")]
        [SerializeField] SleepZzz sleepZzz;

        [Header("Ayarlar")]
        [SerializeField] MenuSettingsScreen settingsScreen;

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

        bool SettingsOpen => settingsScreen != null && settingsScreen.IsOpen;

        void Awake()
        {
            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i].Clicked += OnBubbleClicked;

            newGameButton.onClick.AddListener(EnterSelection);
            settingsButton.onClick.AddListener(OpenSettings);
            exitButton.onClick.AddListener(ExitGame);
            startChapterButton.onClick.AddListener(StartSelectedChapter);
            backButton.onClick.AddListener(ExitSelection);
            if (settingsScreen != null)
                settingsScreen.Closed += OnSettingsClosed;

            if (tooltip != null) tooltip.SetActive(false);
            SetGroup(selectionPanel, 0f);
            if (fadePanel != null)
            {
                fadePanel.alpha = 0f;
                fadePanel.blocksRaycasts = false;
            }
            UpdateStartButton();
            UpdateInfo();
        }

        void Update()
        {
            if (!InputEnabled || state == State.Loading)
                return;

            // Ayarlar ekranı kendi girdisini okur (Esc dahil).
            if (SettingsOpen)
                return;

            if (state == State.Main)
            {
                // Yukarı/aşağı ve Enter, EventSystem'in buton gezinmesiyle çalışır; burada sadece odak kaybını toparla.
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null &&
                    Mathf.Abs(GameInput.MoveVector.y) > 0f)
                    FocusMainButtons();
                return;
            }

            // Afet seçimi
            if (GameInput.BackPressed)
                ExitSelection();
            else if (GameInput.LeftPressed)
                MoveSelection(-1);
            else if (GameInput.RightPressed)
                MoveSelection(1);
            else if (GameInput.ConfirmPressed)
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
            if (selectionDim != null) StartCoroutine(FadeSprites(selectionDim, selectionDim.Alpha, 1f));
            if (sleepZzz != null) sleepZzz.enabled = false;
            if (thoughtTrail != null) thoughtTrail.Show();
            float delay = thoughtTrail != null ? thoughtTrail.TotalDuration : 0f;
            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i].Show(delay + i * bubbleStagger);

            CrossFadePanels(mainButtons, selectionPanel);
            selectedIndex = -1;
            UpdateStartButton();
            UpdateInfo();
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
            if (selectionDim != null) StartCoroutine(FadeSprites(selectionDim, selectionDim.Alpha, 0f));
            if (sleepZzz != null) sleepZzz.enabled = true;
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
            UpdateInfo();
            SelectionChanged?.Invoke(bubbles[index].Chapter);
        }

        void ClearSelection()
        {
            selectedIndex = -1;
            foreach (DisasterBubble bubble in bubbles) bubble.SetSelected(false);
            UpdateStartButton();
            UpdateInfo();
            SelectionCleared?.Invoke();
        }

        void UpdateInfo()
        {
            if (selectionInfo == null)
                return;
            if (selectedIndex < 0)
            {
                selectionInfo.text = idleInfo;
                return;
            }
            string name = MenuChapterLoader.DisplayName(bubbles[selectedIndex].Chapter).ToUpperInvariant();
            string info = bubbleInfo != null && selectedIndex < bubbleInfo.Length ? bubbleInfo[selectedIndex] : "";
            selectionInfo.text = $"<color=#{ColorUtility.ToHtmlStringRGB(infoTitleColor)}>{name}</color>   {info}";
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
            GameAudio.PlaySfx("SFX_UI_Confirm", 0.8f); // bölüm başlatma onayı (P-UI)
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
            if (!InputEnabled || state != State.Main || settingsScreen == null || SettingsOpen)
                return;
            HideTooltip();
            settingsScreen.Open();
        }

        // Odak bir kare sonra Ayarlar butonuna döner: ekranı kapatan Enter aynı karede butona "submit" olarak gidip
        // ekranı yeniden açmasın.
        void OnSettingsClosed() => StartCoroutine(SelectNextFrame(settingsButton));

        static IEnumerator SelectNextFrame(Selectable target)
        {
            yield return null;
            if (EventSystem.current != null && target != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
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
