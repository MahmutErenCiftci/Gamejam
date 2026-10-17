using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AfetUyanisi.Menu
{
    /// <summary>
    /// Menüdeki izometrik yatak odasının önizleme efektleri (MenuBedroom prefab'ı üzerinde).
    ///   Yok   → gün doğumu (Bedroom.unity ile aynı ışık).
    ///   Sel   → pencere fırtınaya döner, camda yağmur, oda biraz kararır, ara sıra uzak şimşek; hafif yağmur sesi.
    ///   Deprem→ kısa, sönümlü, EN FAZLA 1 piksellik kamera sarsıntısı + tavandan birkaç toz zerresi + alçak gürleme.
    /// Bütün kaymalar 1/64 birim (1 oyun pikseli) ızgarasında; eşyalar tek tek sallanmaz.
    /// </summary>
    public class MenuRoomPreview : MonoBehaviour
    {
        public enum Mode { None, Flood, Earthquake }

        [Header("Kamera")]
        [SerializeField] private Transform cameraTransform;

        [Header("Işık")]
        [SerializeField] private Light2D globalLight;
        [SerializeField] private Light2D[] windowLights;
        [SerializeField] private Color stormGlobalColor = new Color(0.62f, 0.70f, 0.90f);
        [SerializeField] private float stormGlobalIntensity = 0.5f;
        [SerializeField] private Color stormWindowColor = new Color(0.55f, 0.65f, 0.90f);
        [SerializeField] private float stormWindowFactor = 0.45f;

        [Header("Pencere")]
        [SerializeField] private SpriteRenderer windowStorm;
        [SerializeField] private SpriteRenderer windowRain;
        [SerializeField] private Sprite[] rainFrames;
        [SerializeField] private SpriteRenderer windowFlash;
        [SerializeField] private float rainFps = 10f;
        [SerializeField] private float blendSeconds = 0.6f;
        [SerializeField] private Vector2 lightningInterval = new Vector2(4f, 7f);

        [Header("Deprem")]
        [Tooltip("Sarsıntı süresi (sn). Genlik en fazla 1 oyun pikseli.")]
        [SerializeField] private float shakeSeconds = 0.7f;
        [SerializeField] private int shakePixels = 2;
        [SerializeField] private SpriteRenderer[] dust;
        [Tooltip("Toz: x = sol sınır, width = genişlik; y/height = duvar üst kenarının ne kadar altından başlar (birim).")]
        [SerializeField] private Rect dustArea = new Rect(-2.6f, 0.1f, 5.4f, 0.5f);
        [Tooltip("Arka köşenin tepesi (dünya). Duvar üst kenarları buradan 1:2 eğimle iner.")]
        [SerializeField] private Vector2 ceilingPeak = new Vector2(0.44f, 4.72f);
        [SerializeField] private float dustFall = 1.1f;

        [Header("Ses (boşsa MenuAudioSynth üretir)")]
        [SerializeField] private AudioSource rainSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private float rainVolume = 0.25f;
        [SerializeField] private float rumbleVolume = 0.35f;
        [SerializeField] private float thunderVolume = 0.2f;

        private const float PixelsPerUnit = 64f;

        private Mode mode;
        private float storm;            // 0 gün doğumu, 1 fırtına
        private Color baseGlobalColor;
        private float baseGlobalIntensity;
        private Color[] baseWindowColors;
        private float[] baseWindowIntensity;
        private Vector3 cameraBase;
        private float shakeStart = -10f;
        private float nextLightning;
        private float flash;
        private float[] dustStart;
        private Vector3[] dustFrom;

        public Mode Current => mode;

        private void Awake()
        {
            if (cameraTransform != null) cameraBase = cameraTransform.position;
            if (globalLight != null) { baseGlobalColor = globalLight.color; baseGlobalIntensity = globalLight.intensity; }
            int n = windowLights != null ? windowLights.Length : 0;
            baseWindowColors = new Color[n];
            baseWindowIntensity = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (windowLights[i] == null) continue;
                baseWindowColors[i] = windowLights[i].color;
                baseWindowIntensity[i] = windowLights[i].intensity;
            }
            int d = dust != null ? dust.Length : 0;
            dustStart = new float[d];
            dustFrom = new Vector3[d];
            for (int i = 0; i < d; i++) { dustStart[i] = -10f; if (dust[i] != null) dust[i].enabled = false; }
            ApplyStorm();
            SetAlpha(windowFlash, 0f);
        }

        /// <summary>Kamera bölüm geçişinde kaydırılacaksa sarsıntının dönüş noktası da güncellenir.</summary>
        public void SetCameraBase(Vector3 position)
        {
            cameraBase = position;
            if (cameraTransform != null) cameraTransform.position = position;
        }

        public void SetMode(Mode next)
        {
            if (next == Mode.Earthquake) PlayQuake();
            if (next == mode) return;
            mode = next;

            if (mode == Mode.Flood)
            {
                nextLightning = Time.unscaledTime + 1.2f;
                if (rainSource != null)
                {
                    if (rainSource.clip == null) rainSource.clip = MenuAudioSynth.Rain;
                    rainSource.loop = true;
                    rainSource.volume = 0f;
                    if (!rainSource.isPlaying) rainSource.Play();
                }
            }
        }

        private void PlayQuake()
        {
            shakeStart = Time.unscaledTime;
            if (sfxSource != null) sfxSource.PlayOneShot(MenuAudioSynth.Rumble, rumbleVolume);
            if (dust == null) return;
            for (int i = 0; i < dust.Length; i++)
            {
                if (dust[i] == null) continue;
                // Zerreler sırayla, biraz gecikmeyle düşer; konumlar piksel ızgarasında.
                dustStart[i] = Time.unscaledTime + 0.05f * i + Random.Range(0f, 0.1f);
                // Duvarların üst kenarının hemen altından (oda köşesi ceilingPeak, kenar eğimi 1:2) düşer.
                float x = dustArea.x + Random.value * dustArea.width;
                float edge = ceilingPeak.y - Mathf.Abs(x - ceilingPeak.x) * 0.5f;
                dustFrom[i] = Snap(new Vector3(x, edge - dustArea.y - Random.value * dustArea.height, 0f));
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float target = mode == Mode.Flood ? 1f : 0f;
            storm = Mathf.MoveTowards(storm, target, dt / Mathf.Max(0.01f, blendSeconds));

            // Şimşek: sadece pencerede kısa, yumuşak bir parlama (göz yormaz).
            if (mode == Mode.Flood && storm > 0.9f && Time.unscaledTime >= nextLightning)
            {
                flash = 1f;
                nextLightning = Time.unscaledTime + Random.Range(lightningInterval.x, lightningInterval.y);
                if (sfxSource != null) sfxSource.PlayOneShot(MenuAudioSynth.Thunder, thunderVolume);
            }
            flash = Mathf.MoveTowards(flash, 0f, dt / 0.35f);
            ApplyStorm();

            if (rainSource != null && rainSource.isPlaying)
            {
                rainSource.volume = rainVolume * storm;
                if (storm <= 0f && mode != Mode.Flood) rainSource.Stop();
            }

            UpdateShake();
            UpdateDust();
        }

        private void ApplyStorm()
        {
            SetAlpha(windowStorm, storm);
            if (windowRain != null)
            {
                SetAlpha(windowRain, storm * 0.9f);
                if (rainFrames != null && rainFrames.Length > 0 && storm > 0f)
                    windowRain.sprite = rainFrames[(int)(Time.unscaledTime * rainFps) % rainFrames.Length];
            }
            SetAlpha(windowFlash, flash * 0.8f * storm);

            if (globalLight != null)
            {
                globalLight.color = Color.Lerp(baseGlobalColor, stormGlobalColor, storm);
                globalLight.intensity = Mathf.Lerp(baseGlobalIntensity, stormGlobalIntensity, storm) + flash * 0.12f * storm;
            }
            for (int i = 0; i < baseWindowColors.Length; i++)
            {
                if (windowLights[i] == null) continue;
                windowLights[i].color = Color.Lerp(baseWindowColors[i], stormWindowColor, storm);
                windowLights[i].intensity = baseWindowIntensity[i] * Mathf.Lerp(1f, stormWindowFactor, storm) + flash * 0.4f * storm;
            }
        }

        private void UpdateShake()
        {
            if (cameraTransform == null) return;
            float t = Time.unscaledTime - shakeStart;
            if (t < 0f || t > shakeSeconds)
            {
                cameraTransform.position = cameraBase;
                return;
            }
            // Sönümlü, tam piksel adımlı sarsıntı (~16 Hz): ilk %30'da en fazla shakePixels, sonra 1 piksel,
            // son %25'te durur. Bütün oda birlikte kayar; yatak ya da eşyalar ayrıca sallanmaz.
            float k = t / shakeSeconds;
            int amp = k < 0.3f ? shakePixels : k < 0.75f ? 1 : 0;
            int step = (int)(t * 16f);
            int[] pattern = { 1, -1, 0, -1, 1, 0, 1, -1 };
            int dx = pattern[step % pattern.Length] * amp;
            int dy = (step % 3 == 1 && k < 0.5f) ? -1 : 0;
            cameraTransform.position = cameraBase + new Vector3(dx, dy, 0f) / PixelsPerUnit;
        }

        private void UpdateDust()
        {
            if (dust == null) return;
            for (int i = 0; i < dust.Length; i++)
            {
                SpriteRenderer d = dust[i];
                if (d == null) continue;
                float t = Time.unscaledTime - dustStart[i];
                float life = 1.4f;
                if (t < 0f || t > life) { d.enabled = false; continue; }
                d.enabled = true;
                float fall = dustFall * (t / life) * (0.6f + 0.4f * t / life);
                float sway = Mathf.Sin(t * 5f + i) * 1.5f / PixelsPerUnit;
                d.transform.position = Snap(dustFrom[i] + new Vector3(sway, -fall, 0f));
                SetAlpha(d, Mathf.Clamp01(Mathf.Min(t / 0.15f, (life - t) / 0.4f)) * 0.9f);
            }
        }

        private static Vector3 Snap(Vector3 p) =>
            new Vector3(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit, p.z);

        private static void SetAlpha(SpriteRenderer r, float a)
        {
            if (r == null) return;
            Color c = r.color;
            c.a = a;
            r.color = c;
            r.enabled = a > 0.001f;
        }
    }
}
