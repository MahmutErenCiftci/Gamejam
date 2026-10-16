using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Kameraya hafif deprem sarsıntısı verir: birkaç piksel, flaş yok (OYUN_AKISI §7).
    /// Kameranın kendisine eklenir; sarsıntı bitince kamera eski yerine döner.
    /// </summary>
    public class EarthquakeCameraShake : MonoBehaviour
    {
        [Tooltip("Dünya birimi cinsinden en fazla kayma (0.05 ≈ birkaç piksel)")]
        [SerializeField] private float amplitude = 0.05f;
        [Tooltip("Saniyedeki titreşim sıklığı")]
        [SerializeField] private float frequency = 25f;

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
                var offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f);
                transform.localPosition = restPosition + offset * (2f * amplitude);
            }
            else if (wasShaking)
            {
                transform.localPosition = restPosition;
                wasShaking = false;
            }
        }
    }
}
