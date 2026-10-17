using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Afet seçimine göre oda önizlemesini yönetir. Aynı anda tek önizleme: SEL → pencerede fırtına
    // (sarsıntı durur, her şey yerine döner); DEPREM → fırtına kalkar, sarsıntı oynar; seçim yok →
    // gün doğumu. Aynı bulut tekrar seçilirse sarsıntı baştan oynar.
    public class MenuEffectsController : MonoBehaviour
    {
        [SerializeField] MainMenuController menu;
        [SerializeField] DynamicWindowController window;
        [SerializeField] EarthquakePreview earthquake;

        // Kodla üretilen sesler ilk kullanımda ~50-90 ms sürüyor (ilk şimşekte takılma). Sahne açılırken,
        // intro karanlığının arkasında bir kez üretilir.
        void Awake()
        {
            _ = MenuAudioSynth.Rain;
            _ = MenuAudioSynth.Thunder;
            _ = MenuAudioSynth.Rumble;
        }

        void OnEnable()
        {
            if (menu == null) return;
            menu.SelectionChanged += OnSelectionChanged;
            menu.SelectionCleared += OnSelectionCleared;
        }

        void OnDisable()
        {
            if (menu == null) return;
            menu.SelectionChanged -= OnSelectionChanged;
            menu.SelectionCleared -= OnSelectionCleared;
        }

        void OnSelectionChanged(MenuChapterLoader.Chapter chapter)
        {
            if (chapter == MenuChapterLoader.Chapter.Flood)
            {
                if (earthquake != null) earthquake.StopAndReset();
                if (window != null) window.SetWeather(DynamicWindowController.Weather.Storm);
            }
            else
            {
                if (window != null) window.SetWeather(DynamicWindowController.Weather.Clear);
                if (earthquake != null) earthquake.Play();
            }
        }

        void OnSelectionCleared()
        {
            if (earthquake != null) earthquake.StopAndReset();
            if (window != null) window.SetWeather(DynamicWindowController.Weather.Clear);
        }
    }
}
