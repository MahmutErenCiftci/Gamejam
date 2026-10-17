using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    // SEL seçilince arka plandaki şehri su basar. Yağmur hafif başlar; su önce çukurlarda birikir, sonra yerden yavaş
    // yavaş yükselir (sokaklar, arabalar, ağaç dipleri, dükkânlar su altında kalır), en sonunda yüzey dalgalanır.
    // Yağmurun şiddeti su seviyesine bağlıdır: su yükseldikçe damlalar çoğalır, hızlanır, rüzgârla daha çok eğilir,
    // yağmur sesi yükselir. Seçim değişince su hızla çekilir, yağmur eski haline döner.
    public class CityFlood : MonoBehaviour
    {
        [SerializeField] SpriteRenderer water;
        [Tooltip("Yükselen su seviyeleri, alçaktan yükseğe.")]
        [SerializeField] Sprite[] rising;
        [Tooltip("Her seviyenin zeminden yüksekliği (piksel); yüzen arabalar bunu izler.")]
        [SerializeField] float[] levels;
        [Tooltip("En yüksek seviyede sırayla oynayan dalga kareleri.")]
        [SerializeField] Sprite[] waves;
        [SerializeField] float waveLevel = 20f;

        [Header("Yağmur (şiddet su seviyesiyle artar)")]
        [Tooltip("Bütün ekrana yağan fırtına yağmuru.")]
        [SerializeField] ParticleSystem rain;
        [Tooltip("Odanın penceresindeki yağmur.")]
        [SerializeField] ParticleSystem windowRain;
        [SerializeField] AudioSource rainAudio;
        [Tooltip("Su yokken → en yüksek seviyede: saniyedeki damla.")]
        [SerializeField] Vector2 rainRate = new Vector2(14f, 420f);
        [SerializeField] Vector2 windowRainRate = new Vector2(8f, 90f);
        [Tooltip("Düşüş hızı (birim/sn).")]
        [SerializeField] Vector2 fallSpeed = new Vector2(2.6f, 5.2f);
        [Tooltip("Rüzgârla yana kayma (birim/sn, negatif sola).")]
        [SerializeField] Vector2 windSpeed = new Vector2(-0.3f, -1.6f);
        [SerializeField] Vector2 rainVolume = new Vector2(0.1f, 0.55f);

        [Header("Zamanlama (sn)")]
        [Tooltip("Su görünmeden önce yalnızca hafif yağmurun yağdığı süre.")]
        [SerializeField] float rainOnlySeconds = 1.6f;
        [SerializeField] float riseSeconds = 8f;
        [SerializeField] float drainSeconds = 0.6f;
        [SerializeField] float waveInterval = 0.32f;

        Coroutine routine;
        int frame = -1;
        bool captured;
        float baseRate, baseWindowRate, baseX, baseY;

        // Şu anki su seviyesi (piksel, 0 = su yok).
        public float Level { get; private set; }

        void Awake() => Clear();

        public void Play()
        {
            if (routine != null) StopCoroutine(routine);
            Capture();
            routine = StartCoroutine(Rise());
        }

        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = frame >= 0 && isActiveAndEnabled ? StartCoroutine(Drain()) : null;
            if (routine == null) Clear();
            RestoreRain();
        }

        void OnDisable() => Clear();

        void Clear()
        {
            frame = -1;
            Level = 0f;
            if (water != null)
            {
                water.sprite = null;
                water.enabled = false;
            }
        }

        void Show(int i)
        {
            frame = i;
            Level = i < levels.Length ? levels[i] : 0f;
            water.enabled = true;
            water.sprite = rising[i];
        }

        // Sahnedeki ilk yağmur ayarları (fırtına kalkınca geri dönülür).
        void Capture()
        {
            if (captured) return;
            captured = true;
            if (rain != null)
            {
                baseRate = rain.emission.rateOverTime.constant;
                var v = rain.velocityOverLifetime;
                baseX = v.x.constant;
                baseY = v.y.constant;
            }
            if (windowRain != null) baseWindowRate = windowRain.emission.rateOverTime.constant;
        }

        void RestoreRain()
        {
            if (!captured) return;
            if (rain != null)
            {
                var e = rain.emission;
                e.rateOverTime = baseRate;
                var v = rain.velocityOverLifetime;
                v.x = baseX;
                v.y = baseY;
            }
            if (windowRain != null)
            {
                var e = windowRain.emission;
                e.rateOverTime = baseWindowRate;
            }
        }

        // k: 0 (su yok) → 1 (en yüksek seviye). Basamaklı değil, su seviyesini yumuşakça izler.
        void SetRain(float k, bool audio)
        {
            if (rain != null)
            {
                var e = rain.emission;
                e.rateOverTime = Mathf.Lerp(rainRate.x, rainRate.y, k * k);
                var v = rain.velocityOverLifetime;
                v.x = Mathf.Lerp(windSpeed.x, windSpeed.y, k);
                v.y = -Mathf.Lerp(fallSpeed.x, fallSpeed.y, k);
            }
            if (windowRain != null)
            {
                var e = windowRain.emission;
                e.rateOverTime = Mathf.Lerp(windowRainRate.x, windowRainRate.y, k);
            }
            // Pencerenin fırtına geçişi (ilk ~1,5 sn) sesi kendisi açar; sonra şiddete göre bu ayarlar.
            if (audio && rainAudio != null) rainAudio.volume = Mathf.Lerp(rainVolume.x, rainVolume.y, k);
        }

        IEnumerator Rise()
        {
            // Yağmur sıfırdan, hafif başlasın (önceden ısıtılmış sağanak bir anda görünmesin).
            if (rain != null && frame < 0) rain.Clear(true);
            int startFrame = Mathf.Max(0, frame);
            float total = rainOnlySeconds + riseSeconds;
            float maxLevel = waveLevel;
            for (float t = frame >= 0 ? rainOnlySeconds : 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float rise = Mathf.Clamp01((t - rainOnlySeconds) / riseSeconds);
                if (t >= rainOnlySeconds)
                {
                    int target = Mathf.Min(rising.Length - 1, (int)(rise * rising.Length));
                    if (target >= startFrame && target != frame) Show(target);
                }
                // Şiddet: su yokken hafif; su yükseldikçe (ara seviyeler de yumuşakça) artar.
                float smoothLevel = t < rainOnlySeconds ? 0f : Mathf.Lerp(levels.Length > 0 ? levels[0] : 0f, maxLevel, rise);
                SetRain(Mathf.Clamp01(smoothLevel / maxLevel), t >= rainOnlySeconds);
                yield return null;
            }
            Show(rising.Length - 1);
            Level = waveLevel;
            SetRain(1f, true);
            for (int f = 0; ; f = (f + 1) % waves.Length)
            {
                water.sprite = waves[f];
                yield return new WaitForSecondsRealtime(waveInterval);
            }
        }

        IEnumerator Drain()
        {
            float step = drainSeconds / Mathf.Max(1, rising.Length);
            for (int i = Mathf.Min(frame, rising.Length - 1); i >= 0; i--)
            {
                Show(i);
                yield return new WaitForSecondsRealtime(step);
            }
            Clear();
            routine = null;
        }
    }
}
