using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Kameraya hafif deprem sarsıntısı verir: birkaç piksel, flaş yok (OYUN_AKISI §7).
    /// Kameranın kendisine eklenir; sarsıntı bitince kamera eski yerine döner.
    /// Kamerayı ChapterCamera o an sürüyorsa (oyuncu takibi) konumu yazmaz, kaymayı ChapterCamera.ShakeOffset'e verir.
    /// Sarsıntı sürerken arka planın kenar vinyeti kırmızımsı olur (RoomBackdrop.SetAlarm).
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

        private bool shaking;
        public bool Shaking
        {
            get => shaking;
            set
            {
                shaking = value;
                RoomBackdrop.SetAlarm(value);
            }
        }

        private Vector3 restPosition;
        private bool wasShaking;
        private bool directWrite; // sarsıntı konumu doğrudan mı yazıyor (ChapterCamera sürmüyorken)
        private ChapterCamera chapterCamera;

        private void Awake()
        {
            chapterCamera = GetComponent<ChapterCamera>();
        }

        private void LateUpdate()
        {
            if (chapterCamera == null) chapterCamera = GetComponent<ChapterCamera>();
            bool viaChapterCamera = chapterCamera != null && chapterCamera.IsDriving;

            if (shaking)
            {
                float t = Time.time * frequency;
                focus = Vector3.Lerp(focus, reducedMotion ? Vector3.zero : FocusOffset, Time.deltaTime * 3f);
                var noise = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f);
                Vector3 offset = focus + noise * (2f * amplitude * Mathf.Clamp01(Intensity)) * (reducedMotion ? 0 : 1);
                offset.x = Mathf.Round(offset.x * 64f) / 64f;
                offset.y = Mathf.Round(offset.y * 64f) / 64f;
                if (viaChapterCamera)
                {
                    if (wasShaking && directWrite) transform.localPosition = restPosition;
                    directWrite = false;
                    chapterCamera.ShakeOffset = offset;
                }
                else
                {
                    if (!wasShaking || !directWrite)
                    {
                        if (chapterCamera != null) chapterCamera.ShakeOffset = Vector2.zero;
                        restPosition = transform.localPosition;
                        directWrite = true;
                    }
                    transform.localPosition = restPosition + offset;
                }
                wasShaking = true;
            }
            else if (wasShaking)
            {
                if (directWrite) transform.localPosition = restPosition;
                if (chapterCamera != null) chapterCamera.ShakeOffset = Vector2.zero;
                wasShaking = false;
                directWrite = false;
                focus = FocusOffset = Vector3.zero;
            }
        }

        private void OnDisable()
        {
            if (directWrite && wasShaking) transform.localPosition = restPosition;
            if (chapterCamera != null) chapterCamera.ShakeOffset = Vector2.zero;
            if (shaking) RoomBackdrop.SetAlarm(false);
            shaking = false;
            wasShaking = false;
            directWrite = false;
            focus = FocusOffset = Vector3.zero;
        }
    }
}
