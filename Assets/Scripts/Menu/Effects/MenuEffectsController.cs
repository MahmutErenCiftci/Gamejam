using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Afet seçimine göre oda ve şehir önizlemesini yönetir. Aynı anda tek önizleme: SEL → pencerede fırtına,
    // şehri su basar (sarsıntı durur, yıkılan binalar yerine döner); DEPREM → fırtına ve su çekilir, oda sarsılır,
    // şehirde binalar yıkılır; seçim yok → gün doğumu. Aynı bulut tekrar seçilirse sarsıntı baştan oynar.
    public class MenuEffectsController : MonoBehaviour
    {
        [SerializeField] MainMenuController menu;
        [SerializeField] DynamicWindowController window;
        [SerializeField] EarthquakePreview earthquake;
        [Tooltip("Arka plandaki şehirde binaların yıkılması (DEPREM).")]
        [SerializeField] CityQuake cityQuake;
        [Tooltip("Arka plandaki şehri su basması (SEL).")]
        [SerializeField] CityFlood cityFlood;
        [Tooltip("Sokaktaki insanlar ve arabalar: depremde panik, selde yüzen arabalar.")]
        [SerializeField] StreetLife streetLife;

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
                if (cityQuake != null) cityQuake.Restore();
                if (window != null) window.SetWeather(DynamicWindowController.Weather.Storm);
                if (cityFlood != null) cityFlood.Play();
                if (streetLife != null) streetLife.SetMode(StreetLife.Mode.Flood);
            }
            else
            {
                if (window != null) window.SetWeather(DynamicWindowController.Weather.Clear);
                if (cityFlood != null) cityFlood.Stop();
                if (earthquake != null) earthquake.Play();
                if (cityQuake != null) cityQuake.Play();
                if (streetLife != null) streetLife.SetMode(StreetLife.Mode.Panic);
            }
        }

        void OnSelectionCleared()
        {
            if (earthquake != null) earthquake.StopAndReset();
            if (cityQuake != null) cityQuake.Restore();
            if (cityFlood != null) cityFlood.Stop();
            if (window != null) window.SetWeather(DynamicWindowController.Weather.Clear);
            if (streetLife != null) streetLife.SetMode(StreetLife.Mode.Normal);
        }
    }
}
