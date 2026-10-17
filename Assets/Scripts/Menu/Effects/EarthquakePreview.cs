using System;
using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    // DEPREM seçilince kısa sarsıntı önizlemesi: kamera sarsılır, eşyalar tepki verir (sarkıt lamba ve
    // asılı bitki sallanır, raf/kitap/poster titrer), çocuk irkilir, alçak gürleme çalar. ~1,3 sn sonra
    // her şey eski konumuna döner. Fizik yok; sadece transform ve basit eğriler. Flaş yok.
    public class EarthquakePreview : MonoBehaviour
    {
        public enum Reaction { Jitter, Sway, Tilt }

        [Serializable]
        public class Prop
        {
            public Transform target;
            public Reaction reaction = Reaction.Jitter;
            [Tooltip("Jitter: piksel, Sway/Tilt: derece.")]
            public float strength = 1f;
        }

        [SerializeField] Transform cameraTransform;
        [Tooltip("Piksel cinsinden kamera sarsıntı genliği.")]
        [SerializeField] float cameraPixels = 3f;
        [SerializeField] Prop[] props;
        [SerializeField] SleepingBoy boy;
        [SerializeField] float duration = 1.3f;
        [SerializeField] float frequency = 16f;
        [Tooltip("Sarkıt lamba gibi Sway objelerinin salınım sıklığı (Hz).")]
        [SerializeField] float swayFrequency = 1.6f;

        [Header("Ses (clip boşsa kodla üretilir)")]
        [SerializeField] AudioSource rumbleSource;
        [SerializeField] AudioClip rumbleClip;
        [SerializeField] float rumbleVolume = 0.6f;

        const float PixelsPerUnit = 64f;
        Vector3 cameraBase;
        Vector3[] basePositions;
        Quaternion[] baseRotations;
        Coroutine routine;

        public bool IsPlaying => routine != null;

        void Awake()
        {
            if (cameraTransform != null) cameraBase = cameraTransform.localPosition;
            basePositions = new Vector3[props.Length];
            baseRotations = new Quaternion[props.Length];
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].target == null) continue;
                basePositions[i] = props[i].target.localPosition;
                baseRotations[i] = props[i].target.localRotation;
            }
        }

        public void Play()
        {
            StopAndReset();
            if (boy != null) boy.PlayStartled();
            if (rumbleSource != null)
                rumbleSource.PlayOneShot(rumbleClip != null ? rumbleClip : MenuAudioSynth.Rumble, rumbleVolume);
            routine = StartCoroutine(Shake());
        }

        public void StopAndReset()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (rumbleSource != null) rumbleSource.Stop();
            Restore();
        }

        void OnDisable() => StopAndReset();

        IEnumerator Shake()
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                // Hızlı başlar, yumuşakça yatışır.
                float envelope = Mathf.Clamp01(k * 8f) * (1f - k) * (1f - k);
                float n = t * frequency;

                if (cameraTransform != null)
                {
                    var offset = new Vector3(Noise(n, 0.1f), Noise(0.7f, n), 0f) * (cameraPixels * envelope / PixelsPerUnit);
                    cameraTransform.localPosition = cameraBase + offset;
                }

                for (int i = 0; i < props.Length; i++)
                {
                    Prop prop = props[i];
                    if (prop.target == null) continue;
                    switch (prop.reaction)
                    {
                        case Reaction.Jitter:
                            var jitter = new Vector3(Noise(n + i * 3.1f, 2.2f), Noise(4.4f, n + i), 0f) * (prop.strength * envelope);
                            // Pikselde tut: yarım piksel kaymalar bulanık görünür.
                            jitter.x = Mathf.Round(jitter.x) / PixelsPerUnit;
                            jitter.y = Mathf.Round(jitter.y) / PixelsPerUnit;
                            prop.target.localPosition = basePositions[i] + jitter;
                            break;
                        case Reaction.Sway:
                            // Sarkıt: sarsıntı bitse de bir süre sönümlü salınır.
                            float swing = Mathf.Sin(t * swayFrequency * Mathf.PI * 2f) * prop.strength * (1f - k);
                            prop.target.localRotation = baseRotations[i] * Quaternion.Euler(0f, 0f, swing);
                            break;
                        case Reaction.Tilt:
                            prop.target.localRotation = baseRotations[i] * Quaternion.Euler(0f, 0f, Noise(n + i, 7.7f) * prop.strength * envelope);
                            break;
                    }
                }
                yield return null;
            }
            Restore();
            routine = null;
        }

        static float Noise(float x, float y) => Mathf.PerlinNoise(x, y) * 2f - 1f;

        // Sahne kapanırken hedefler bu objeden önce yok edilmiş olabilir.
        void Restore()
        {
            if (basePositions == null) return;
            if (cameraTransform != null) cameraTransform.localPosition = cameraBase;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].target == null) continue;
                props[i].target.localPosition = basePositions[i];
                props[i].target.localRotation = baseRotations[i];
            }
        }
    }
}
