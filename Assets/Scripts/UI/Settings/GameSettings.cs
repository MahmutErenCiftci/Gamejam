using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.UI
{
    /// <summary>
    /// Oyuncu tercihleri (Ayarlar ekranı): müzik ve efekt ses düzeyi, parlaklık, tam ekran ve çözünürlük.
    /// Değerler PlayerPrefs'te ("AfetUyanisi.Settings.*") saklanır, oyun açılınca GameSettingsRuntime uygular.
    /// Ses ve parlaklık değişince anında uygulanır (Changed olayı); ekran ayarı ApplyDisplay ile uygulanıp kaydedilir.
    /// Ana ses (AudioListener.volume, GameAudio'nun "AfetUyanisi.Volume" anahtarı) burada değişmez.
    /// </summary>
    public static class GameSettings
    {
        public const float DefaultMusic = 1f;
        public const float DefaultSfx = 1f;
        public const float DefaultBrightness = 1f;
        public const float MinBrightness = 0.5f;
        public const float MaxBrightness = 1.5f;
        public const bool DefaultFullscreen = true;
        /// <summary>Listede gösterilen en küçük çözünürlük (arayüz 640x360 referansla kurulu).</summary>
        public const int MinWidth = 640, MinHeight = 360;

        private const string Prefix = "AfetUyanisi.Settings.";
        private const string MusicKey = Prefix + "MusicVolume";
        private const string SfxKey = Prefix + "SfxVolume";
        private const string BrightnessKey = Prefix + "Brightness";
        private const string FullscreenKey = Prefix + "Fullscreen";
        private const string WidthKey = Prefix + "ResolutionWidth";
        private const string HeightKey = Prefix + "ResolutionHeight";

        private static bool loaded;
        private static float music, sfx, brightness;

        /// <summary>Müzik, efekt ya da parlaklık değişti (GameSettingsRuntime dinler).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        /// <summary>Müzik ses düzeyi (0–1).</summary>
        public static float MusicVolume
        {
            get { Load(); return music; }
            set => SetValue(ref music, Mathf.Clamp01(value), MusicKey);
        }

        /// <summary>Ses efektleri düzeyi (0–1): müzik dışındaki bütün sesler (ortam, arayüz, olay sesleri).</summary>
        public static float SfxVolume
        {
            get { Load(); return sfx; }
            set => SetValue(ref sfx, Mathf.Clamp01(value), SfxKey);
        }

        /// <summary>Parlaklık çarpanı (0,5–1,5; 1 = değişiklik yok).</summary>
        public static float Brightness
        {
            get { Load(); return brightness; }
            set => SetValue(ref brightness, Mathf.Clamp(value, MinBrightness, MaxBrightness), BrightnessKey);
        }

        /// <summary>Kayıtlı tam ekran tercihi (kayıt yoksa varsayılan).</summary>
        public static bool SavedFullscreen => PlayerPrefs.GetInt(FullscreenKey, DefaultFullscreen ? 1 : 0) == 1;

        /// <summary>Kayıtlı çözünürlük; kayıt yoksa false.</summary>
        public static bool TryGetSavedResolution(out int width, out int height)
        {
            width = PlayerPrefs.GetInt(WidthKey, 0);
            height = PlayerPrefs.GetInt(HeightKey, 0);
            return width >= MinWidth && height >= MinHeight;
        }

        public static bool HasSavedDisplay => PlayerPrefs.HasKey(FullscreenKey) || PlayerPrefs.HasKey(WidthKey);

        /// <summary>Şu anki pencere/ekran kipi tam ekran mı.</summary>
        public static bool CurrentFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

        /// <summary>Monitörün kendi çözünürlüğü (varsayılan çözünürlük).</summary>
        public static Vector2Int NativeResolution
        {
            get
            {
                int w = Display.main != null ? Display.main.systemWidth : Screen.currentResolution.width;
                int h = Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height;
                return new Vector2Int(Mathf.Max(w, MinWidth), Mathf.Max(h, MinHeight));
            }
        }

        /// <summary>
        /// Seçilebilir çözünürlükler: monitörün desteklediği kipler (yenileme hızı ayrımı olmadan), en az 640x360,
        /// küçükten büyüğe. Şu anki boyut ve monitörün kendi çözünürlüğü listede yoksa eklenir.
        /// </summary>
        public static List<Vector2Int> AvailableResolutions()
        {
            var list = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)
                Add(list, new Vector2Int(r.width, r.height));
            Add(list, NativeResolution);
            Add(list, new Vector2Int(Screen.width, Screen.height));
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        private static void Add(List<Vector2Int> list, Vector2Int size)
        {
            if (size.x >= MinWidth && size.y >= MinHeight && !list.Contains(size)) list.Add(size);
        }

        /// <summary>Ekran kipini ve çözünürlüğü uygular, kaydeder. Editörde Game penceresi değişmez (Unity kısıtı).</summary>
        public static void ApplyDisplay(Vector2Int size, bool fullscreen)
        {
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(WidthKey, size.x);
            PlayerPrefs.SetInt(HeightKey, size.y);
            PlayerPrefs.Save();
            Screen.SetResolution(size.x, size.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            Debug.Log($"[Ayarlar] Ekran: {size.x}x{size.y}, {(fullscreen ? "tam ekran" : "pencere")}.");
        }

        /// <summary>Müzik, efekt ve parlaklığı başlangıç değerlerine alır (ekranı ayrıca ApplyDisplay uygular).</summary>
        public static void ResetAudioAndBrightness()
        {
            Load();
            music = DefaultMusic;
            sfx = DefaultSfx;
            brightness = DefaultBrightness;
            PlayerPrefs.SetFloat(MusicKey, music);
            PlayerPrefs.SetFloat(SfxKey, sfx);
            PlayerPrefs.SetFloat(BrightnessKey, brightness);
            Changed?.Invoke();
        }

        /// <summary>Bekleyen değerleri diske yazar (ekran kapanırken, sürükleme bitince).</summary>
        public static void Save() => PlayerPrefs.Save();

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, DefaultMusic));
            sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultSfx));
            brightness = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness), MinBrightness, MaxBrightness);
        }

        private static void SetValue(ref float field, float value, string key)
        {
            Load();
            if (Mathf.Approximately(field, value)) return;
            field = value;
            PlayerPrefs.SetFloat(key, value);
            Changed?.Invoke();
        }
    }
}
