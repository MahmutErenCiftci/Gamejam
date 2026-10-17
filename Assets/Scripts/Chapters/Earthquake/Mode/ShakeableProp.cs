using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Sarsıntıda eşya tepkisi (scriptli, fizik yok). EarthquakeEventController BeginShake/EndShake çağırır.
    ///  • Jitter: eşya birkaç piksel titrer (1/64 ızgaraya yuvarlanır), sarsıntı bitince yerine döner.
    ///  • Swing: sarkıt lamba gibi Z ekseninde sallanır, bitince yavaşça durulur.
    ///  • Fall: (düşen kitap/saksı) fallDelay sn sonra fallOffset kadar aşağı/yana düşer ve orada kalır (yerine dönmez).
    /// Görsel bir çocuk objedeyse "target" ona verilir (pivot/collider sabit kalsın).
    /// </summary>
    public class ShakeableProp : MonoBehaviour
    {
        /// <summary>Tepki türü.</summary>
        public enum Mode { Jitter, Swing, Fall }

        [SerializeField] private Mode mode = Mode.Jitter;
        [Tooltip("Hareket ettirilecek obje (boşsa bu obje)")]
        [SerializeField] private Transform target;
        [Tooltip("Jitter: en fazla kayma (dünya birimi; 2/64 ≈ 2 px)")]
        [SerializeField] private float jitterAmplitude = 2f / 64f;
        [Tooltip("Jitter: saniyede kaç kez yeni konum")]
        [SerializeField] private float jitterRate = 18f;
        [Tooltip("Swing: en fazla açı (derece)")]
        [SerializeField] private float swingAngle = 10f;
        [Tooltip("Swing: salınım sıklığı (Hz)")]
        [SerializeField] private float swingFrequency = 1.4f;
        [Tooltip("Swing: sarsıntı bitince durulma süresi (sn)")]
        [SerializeField] private float swingSettle = 1.5f;
        [Tooltip("Fall: sarsıntı başladıktan kaç sn sonra düşer")]
        [SerializeField] private float fallDelay = 1.5f;
        [Tooltip("Fall: düşüş süresi (sn)")]
        [SerializeField] private float fallDuration = 0.35f;
        [Tooltip("Fall: düşme hedefi (yerel kayma, dünya birimi)")]
        [SerializeField] private Vector3 fallOffset = new Vector3(0.2f, -0.4f, 0f);
        [Tooltip("Fall: düştükten sonra dönme açısı (derece)")]
        [SerializeField] private float fallRotation = 80f;
        [Tooltip("Fall: düşünce bu objeyi aç (ör. yerdeki kitap sprite'ı); boşsa target hareket eder")]
        [SerializeField] private GameObject fallenVersion;

        private const float Ppu = 64f;
        private Vector3 restLocalPos;
        private Quaternion restLocalRot;
        private bool shaking;
        private float shakeTime;
        private float jitterTimer;
        private float settleTimer;
        private float intensity = 1f;
        private bool fallen;
        private bool hasRest;

        public bool IsShaking => shaking;
        private Transform T => target != null ? target : transform;

        private void Awake()
        {
            CaptureRest();
            if (fallenVersion != null && mode == Mode.Fall) fallenVersion.SetActive(false);
        }

        private void CaptureRest()
        {
            restLocalPos = T.localPosition;
            restLocalRot = T.localRotation;
            hasRest = true;
        }

        /// <summary>Sarsıntı başlar (intensity 0..1 genlik çarpanı).</summary>
        public void BeginShake(float shakeIntensity = 1f)
        {
            if (!hasRest) CaptureRest();
            if (!shaking && !fallen) CaptureRest();
            intensity = Mathf.Clamp01(shakeIntensity);
            shaking = true;
            shakeTime = 0f;
            jitterTimer = 0f;
        }

        /// <summary>Sarsıntı biter: titreyenler yerine döner, lamba durulur, düşen düştüğü yerde kalır.</summary>
        public void EndShake()
        {
            if (!shaking) return;
            shaking = false;
            settleTimer = swingSettle;
            if (mode == Mode.Jitter) T.localPosition = restLocalPos;
            if (mode == Mode.Fall && !fallen) Fall(true);
        }

        private void Update()
        {
            if (shaking) shakeTime += Time.deltaTime;

            switch (mode)
            {
                case Mode.Jitter:
                    if (!shaking) return;
                    jitterTimer -= Time.deltaTime;
                    if (jitterTimer <= 0f)
                    {
                        jitterTimer = 1f / Mathf.Max(1f, jitterRate);
                        float a = jitterAmplitude * intensity;
                        var offset = new Vector3(Random.Range(-a, a), Random.Range(-a, a) * 0.5f, 0f);
                        T.localPosition = restLocalPos + Snap(offset);
                    }
                    break;

                case Mode.Swing:
                    float amp;
                    if (shaking) amp = swingAngle * intensity;
                    else if (settleTimer > 0f)
                    {
                        settleTimer -= Time.deltaTime;
                        amp = swingAngle * intensity * Mathf.Clamp01(settleTimer / Mathf.Max(0.01f, swingSettle));
                    }
                    else
                    {
                        if (T.localRotation != restLocalRot) T.localRotation = restLocalRot;
                        return;
                    }
                    float angle = Mathf.Sin(Time.time * swingFrequency * Mathf.PI * 2f) * amp;
                    T.localRotation = restLocalRot * Quaternion.Euler(0f, 0f, angle);
                    break;

                case Mode.Fall:
                    if (shaking && !fallen && shakeTime >= fallDelay) Fall(false);
                    break;
            }
        }

        private void Fall(bool instant)
        {
            fallen = true;
            if (fallenVersion != null)
            {
                fallenVersion.SetActive(true);
                T.gameObject.SetActive(false);
                return;
            }
            if (instant || !isActiveAndEnabled)
            {
                T.localPosition = restLocalPos + fallOffset;
                T.localRotation = restLocalRot * Quaternion.Euler(0f, 0f, fallRotation);
                return;
            }
            StartCoroutine(FallRoutine());
        }

        private System.Collections.IEnumerator FallRoutine()
        {
            for (float t = 0f; t < fallDuration; t += Time.deltaTime)
            {
                float k = t / Mathf.Max(0.01f, fallDuration);
                float eased = k * k; // yerçekimi hissi
                T.localPosition = restLocalPos + Snap(fallOffset * eased);
                T.localRotation = restLocalRot * Quaternion.Euler(0f, 0f, fallRotation * eased);
                yield return null;
            }
            T.localPosition = restLocalPos + fallOffset;
            T.localRotation = restLocalRot * Quaternion.Euler(0f, 0f, fallRotation);
        }

        private static Vector3 Snap(Vector3 v) =>
            new Vector3(Mathf.Round(v.x * Ppu) / Ppu, Mathf.Round(v.y * Ppu) / Ppu, v.z);
    }
}
