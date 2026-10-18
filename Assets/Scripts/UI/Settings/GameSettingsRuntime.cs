using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AfetUyanisi.UI
{
    /// <summary>
    /// Ayarların oyunun her sahnesinde geçerli olması için ilk sahneden önce kurulan tek DontDestroyOnLoad nesnesi:
    ///  • Ses: Resources/AfetUyanisiSettings/SettingsMixer'ın Music ve SFX gruplarının düzeyi (MusicVolume / SfxVolume).
    ///    Çıkışı atanmamış her AudioSource bir gruba yönlendirilir: GameAudio'nun müzik kanalı (Loop_music, MUS_* klipleri)
    ///    Music'e, geri kalan her şey (ortam, efekt, arayüz, sahnelerdeki kaynaklar) SFX'e. Ses kodunun kendisi değişmez.
    ///  • Parlaklık: en üstte (sıralama 32760) tam ekran bir katman, oyunun görüntüsünü çarpar (BrightnessOverlay shader'ı).
    ///  • Ekran: kayıtlı çözünürlük ve tam ekran tercihi açılışta bir kez uygulanır (yalnızca oyun build'inde).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSettingsRuntime : MonoBehaviour
    {
        private const string ResourceFolder = "AfetUyanisiSettings/";
        private const string MusicParam = "MusicVolume", SfxParam = "SfxVolume";
        private const float ScanSeconds = 0.5f;

        private static GameSettingsRuntime instance;

        private AudioMixer mixer;
        private AudioMixerGroup musicGroup, sfxGroup;
        private GameAudioRuntime gameAudio;
        private readonly List<AudioSource> gameAudioSources = new List<AudioSource>();
        private float nextScanAt;
        private Image overlay;
        private Material overlayMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("GameSettings");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameSettingsRuntime>();
#if !UNITY_EDITOR
            if (GameSettings.HasSavedDisplay)
            {
                Vector2Int size;
                if (GameSettings.TryGetSavedResolution(out int w, out int h)) size = new Vector2Int(w, h);
                else size = new Vector2Int(Screen.width, Screen.height);
                Screen.SetResolution(size.x, size.y, GameSettings.SavedFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
#endif
        }

        private void Awake()
        {
            mixer = Resources.Load<AudioMixer>(ResourceFolder + "SettingsMixer");
            if (mixer != null)
            {
                musicGroup = Group("Music");
                sfxGroup = Group("SFX");
            }
            else Debug.LogWarning("[Ayarlar] SettingsMixer bulunamadı; müzik/efekt düzeyi uygulanamaz.", this);
            BuildOverlay();
        }

        private AudioMixerGroup Group(string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            return groups.Length > 0 ? groups[0] : null;
        }

        private void OnEnable()
        {
            GameSettings.Changed += Apply;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            GameSettings.Changed -= Apply;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // AudioMixer.SetFloat Awake'te bazen başlangıç anlık görüntüsüne yenik düşer; Start'ta uygula.
        private void Start()
        {
            Apply();
            RouteAll();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RouteAll();

        private void LateUpdate()
        {
            // GameAudio kaynaklarını çalmaya başladıkları karede yönlendir (ilk efekt tam seste kaçmasın).
            if (gameAudio == null) gameAudio = FindAnyObjectByType<GameAudioRuntime>();
            if (gameAudio != null)
            {
                gameAudio.GetComponentsInChildren(true, gameAudioSources);
                foreach (var source in gameAudioSources) Route(source);
            }
            if (Time.unscaledTime >= nextScanAt) RouteAll();
        }

        private void OnApplicationQuit() => GameSettings.Save();

        // ------------------------------------------------------------------ ses

        private void RouteAll()
        {
            nextScanAt = Time.unscaledTime + ScanSeconds;
            if (mixer == null) return;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                Route(source);
        }

        private void Route(AudioSource source)
        {
            if (source == null || source.outputAudioMixerGroup != null || musicGroup == null || sfxGroup == null) return;
            source.outputAudioMixerGroup = IsMusic(source) ? musicGroup : sfxGroup;
        }

        private static bool IsMusic(AudioSource source)
        {
            if (source.gameObject.name == "Loop_" + GameAudio.MusicChannel) return true;
            return source.clip != null && source.clip.name.StartsWith("MUS_");
        }

        private void Apply()
        {
            if (mixer != null)
            {
                mixer.SetFloat(MusicParam, ToDecibel(GameSettings.MusicVolume));
                mixer.SetFloat(SfxParam, ToDecibel(GameSettings.SfxVolume));
            }
            ApplyBrightness(GameSettings.Brightness);
        }

        /// <summary>0–1 doğrusal düzeyi desibele çevirir (0 → -80 dB, sessiz).</summary>
        private static float ToDecibel(float linear) => linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;

        // ------------------------------------------------------------------ parlaklık

        private void BuildOverlay()
        {
            var shader = Resources.Load<Shader>(ResourceFolder + "BrightnessOverlay");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[Ayarlar] BrightnessOverlay shader'ı yok; parlaklık uygulanamaz.", this);
                return;
            }
            overlayMaterial = new Material(shader) { name = "BrightnessOverlay (runtime)" };

            var canvasGo = new GameObject("BrightnessCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;

            var imageGo = new GameObject("Brightness", typeof(RectTransform));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)imageGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            overlay = imageGo.AddComponent<Image>();
            overlay.material = overlayMaterial;
            overlay.raycastTarget = false;
            ApplyBrightness(GameSettings.Brightness);
        }

        private void ApplyBrightness(float brightness)
        {
            if (overlay == null) return;
            bool neutral = Mathf.Abs(brightness - 1f) < 0.005f;
            overlay.gameObject.SetActive(!neutral);
            if (neutral) return;

            // Doğrusal renk uzayında köşe rengi gama → doğrusal çevrilir; çarpım böylece gama uzayında "×parlaklık" olur.
            if (brightness < 1f)
            {
                overlayMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor);
                overlayMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                overlay.color = new Color(brightness, brightness, brightness, 1f);
            }
            else
            {
                // Sonuç = alttaki × (1 + c): c, doğrusal uzayda istenen çarpanın 1 fazlası (en çok 1, yani ×2).
                float add = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? Mathf.Min(1f, Mathf.Pow(brightness, 2.2f) - 1f)
                    : brightness - 1f;
                float c = QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.LinearToGammaSpace(add) : add;
                overlayMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor);
                overlayMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                overlay.color = new Color(c, c, c, 1f);
            }
            overlay.SetMaterialDirty();
        }

        private void OnDestroy()
        {
            if (overlayMaterial != null) Destroy(overlayMaterial);
            if (instance == this) instance = null;
        }
    }
}
