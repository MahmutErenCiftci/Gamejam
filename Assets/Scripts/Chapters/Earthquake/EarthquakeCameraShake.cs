using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Kameraya hafif deprem sarsıntısı verir: birkaç piksel, flaş yok (OYUN_AKISI §7).
    /// Kameranın kendisine eklenir; sarsıntı bitince kamera eski yerine döner.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class EarthquakeCameraShake : MonoBehaviour
    {
        [Tooltip("Dünya birimi cinsinden en fazla kayma (0.05 ≈ birkaç piksel)")]
        [SerializeField] private float amplitude = 0.05f;
        [Tooltip("Saniyedeki titreşim sıklığı")]
        [SerializeField] private float frequency = 25f;
        [Tooltip("Açıkken kamera sarsılmaz; HUD ve denge alıştırması aynen çalışır.")]
        [SerializeField] private bool reducedMotion;
        public bool ReducedMotion { get => reducedMotion; set => reducedMotion = value; }
        public float Intensity { get; set; } = 1f;
        public Vector3 FocusOffset { get; set; }
        private Vector3 focus;

        public bool Shaking { get; set; }

        private Vector3 restPosition;
        private bool wasShaking;

        private void LateUpdate()
        {
            if (Shaking)
            {
                if (!wasShaking)
                {
                    restPosition = transform.localPosition;
                    wasShaking = true;
                }

                float t = Time.time * frequency;
                focus = Vector3.Lerp(focus, reducedMotion ? Vector3.zero : FocusOffset, Time.deltaTime * 3f);
                var offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f);
                Vector3 shift = focus + offset * (2f * amplitude * Mathf.Clamp01(Intensity)) * (reducedMotion ? 0 : 1);
                shift.x = Mathf.Round(shift.x * 64f) / 64f;
                shift.y = Mathf.Round(shift.y * 64f) / 64f;
                transform.localPosition = restPosition + shift;
            }
            else if (wasShaking)
            {
                transform.localPosition = restPosition;
                wasShaking = false;
                focus = FocusOffset = Vector3.zero;
            }
        }

        private void OnDisable()
        {
            if (wasShaking) transform.localPosition = restPosition;
            wasShaking = false;
            Shaking = false;
            focus = FocusOffset = Vector3.zero;
        }
    }
}
