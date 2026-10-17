using UnityEngine;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// Resources/Audio için 2D ses API'si. Sahne/prefab gerektirmez: ilk sahne yüklenmeden önce tek bir
    /// DontDestroyOnLoad "GameAudio" nesnesi kurulur (GameAudioRuntime + AudioAutoWiring).
    /// Kategori seviyeleri: müzik 0.5, ortam 0.4, efekt 0.8; çağrılardaki ses değeri bu seviyeye göre görelidir (0–1).
    /// Ana ses yalnızca AudioListener.volume'dadır (menü ayarı, PlayerPrefs "AfetUyanisi.Volume").
    /// </summary>
    public static class GameAudio
    {
        public const string MusicChannel = "music";
        public const string AmbienceChannel = "ambience";
        /// <summary>MenuSettingsPanel.VolumeKey ile aynı anahtar.</summary>
        public const string VolumePreference = "AfetUyanisi.Volume";

        public const float MusicLevel = 0.5f;
        public const float AmbienceLevel = 0.4f;
        public const float SfxLevel = 0.8f;

        private static GameAudioRuntime runtime;
        private static bool quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { runtime = null; quitting = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            AudioListener.volume = Clamp(PlayerPrefs.GetFloat(VolumePreference, 1f));
            Ensure();
        }

        private static GameAudioRuntime Ensure()
        {
            if (quitting || !Application.isPlaying) return null;
            if (runtime != null) return runtime;
            runtime = Object.FindAnyObjectByType<GameAudioRuntime>();
            if (runtime != null) return runtime;
            var root = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(root);
            runtime = root.AddComponent<GameAudioRuntime>();
            root.AddComponent<AudioAutoWiring>();
            return runtime;
        }

        /// <summary>Kısa efekt. id: "SFX_UI_Click" ya da "SFX/SFX_UI_Click" (uzantısız). vol: efekt seviyesine göre (0–1).</summary>
        public static void PlaySfx(string id, float vol = 1f) => Ensure()?.PlaySfx(id, Clamp(vol) * SfxLevel);

        public static void PlayMusic(string id, float vol = 1f, float fadeSec = 0.8f) =>
            Ensure()?.PlayLoop(id, MusicChannel, Clamp(vol) * MusicLevel, Duration(fadeSec));

        public static void PlayAmbience(string id, float vol = 1f, float fadeSec = 0.8f) =>
            Ensure()?.PlayLoop(id, AmbienceChannel, Clamp(vol) * AmbienceLevel, Duration(fadeSec));

        /// <summary>Efekt döngüsü (alarm, telefon, gürleme) kendi kanalında; aynı klip baştan başlamaz.</summary>
        public static void PlayLoop(string id, string channel, float vol = 1f, float fadeSec = 0.1f) =>
            Ensure()?.PlayLoop(id, channel, Clamp(vol) * LevelOf(channel), Duration(fadeSec));

        public static void StopLoop(string channel, float fadeSec = 0.4f)
        {
            if (runtime != null) runtime.StopLoop(channel, Duration(fadeSec));
        }

        public static void StopMusic(float fadeSec = 0.8f) => StopLoop(MusicChannel, fadeSec);
        public static void StopAmbience(float fadeSec = 0.8f) => StopLoop(AmbienceChannel, fadeSec);

        /// <summary>Ana ses (0–1): AudioListener.volume + PlayerPrefs; menü ayar paneliyle aynı anahtar.</summary>
        public static float MasterVolume => AudioListener.volume;
        public static void SetMasterVolume(float value)
        {
            float v = Clamp(value);
            PlayerPrefs.SetFloat(VolumePreference, v);
            AudioListener.volume = v;
        }

        /// <summary>Resources'tan klip (Overrides öncelikli); yoksa null.</summary>
        public static AudioClip LoadClip(string id) => Ensure()?.Load(id);

        internal static void StopTransientAudio()
        {
            if (runtime != null) runtime.StopTransientAudio();
        }

        internal static void Shutdown() { quitting = true; }

        private static float LevelOf(string channel) =>
            channel == MusicChannel ? MusicLevel : channel == AmbienceChannel ? AmbienceLevel : SfxLevel;

        internal static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        private static float Duration(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
