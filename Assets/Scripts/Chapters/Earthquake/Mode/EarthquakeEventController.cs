using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Sarsıntının görsel/işitsel olayı (scriptli, pahalı fizik yok, FLAŞ YOK):
    /// kamera sarsıntısı (mevcut EarthquakeCameraShake; kamerada yoksa eklenir), gürleme ses kancası,
    /// ShakeableProp'lar (titreyen eşyalar, sallanan lamba, düşen kitaplar), toz partikülü ve global Light2D'nin
    /// yavaşça hafif kararması (ani parlama yok). BeginShake/EndShake engellemesiz; Shake(duration) bekleyen sürüm.
    /// ApplyAftermath(): sahnedeki AftermathToggle'ları ve listelenen objeleri uygular (yerde kitaplar, cam kırıkları...).
    /// </summary>
    public class EarthquakeEventController : MonoBehaviour
    {
        /// <summary>Sprite değişimi (sonrası görünüm).</summary>
        [Serializable]
        public class SpriteSwap
        {
            public SpriteRenderer renderer;
            public Sprite afterSprite;
        }

        [Header("Kamera")]
        [Tooltip("Boşsa Main Camera'daki bulunur, yoksa eklenir")]
        [SerializeField] private EarthquakeCameraShake cameraShake;

        [Header("Eşyalar")]
        [Tooltip("Boşsa sahnedeki bütün ShakeableProp'lar toplanır")]
        [SerializeField] private ShakeableProp[] props = Array.Empty<ShakeableProp>();

        [Header("Toz ve ışık")]
        [Tooltip("İsteğe bağlı toz partikülü (sarsıntıda Play, bitince Stop: toz yavaşça çöker)")]
        [SerializeField] private ParticleSystem[] dust = Array.Empty<ParticleSystem>();
        [Tooltip("Kare kare toz bulutları (QuakeDustPuff). Boşsa sahnedeki bütün QuakeDustPuff'lar toplanır.")]
        [SerializeField] private QuakeDustPuff[] dustPuffs = Array.Empty<QuakeDustPuff>();
        [Tooltip("Global Light2D (boşsa sahnedeki Global tipli Light2D bulunur; yoksa ışık değişmez)")]
        [SerializeField] private Light2D globalLight;
        [Tooltip("Sarsıntıda ışık yoğunluğu çarpanı (1 = değişmez). Ani değil, lightFadeSeconds boyunca yumuşak.")]
        [SerializeField, Range(0.3f, 1f)] private float lightDip = 0.75f;
        [SerializeField] private float lightFadeSeconds = 0.6f;

        [Header("Sonrası")]
        [Tooltip("Sonrasında açılacak objeler (AftermathToggle'a ek)")]
        [SerializeField] private GameObject[] showAfter = Array.Empty<GameObject>();
        [Tooltip("Sonrasında kapanacak objeler")]
        [SerializeField] private GameObject[] hideAfter = Array.Empty<GameObject>();
        [SerializeField] private SpriteSwap[] spriteSwaps = Array.Empty<SpriteSwap>();

        [Header("Ses kancaları")]
        [Tooltip("Sarsıntı başında (alçak gürleme)")]
        [SerializeField] private UnityEvent onRumbleStart = new UnityEvent();
        [Tooltip("Sarsıntı bitince")]
        [SerializeField] private UnityEvent onRumbleEnd = new UnityEvent();
        [Tooltip("Sonrası uygulanınca")]
        [SerializeField] private UnityEvent onAftermath = new UnityEvent();
        [Tooltip("İsteğe bağlı gürleme sesi (klibi varsa döngüde çalar)")]
        [SerializeField] private AudioSource rumbleAudio;
        [SerializeField, Range(0f, 1f)] private float generatedRumbleVolume = 0.22f;
        private AudioClip generatedRumble;
        private float rumbleBaseVolume;

        [Header("Performans ölçümü")]
        [Tooltip("Sarsıntı boyunca ortalama kare süresini ölçüp bitince Console'a yazar (kabul testi 19)")]
        [SerializeField] private bool logFrameTime = true;

        private float baseLightIntensity = 1f;
        private Coroutine lightRoutine;
        private bool aftermathApplied;
        private int perfFrames;
        private float perfTime;
        private float perfWorst;

        public bool IsShaking { get; private set; }
        public bool AftermathApplied => aftermathApplied;

        private void Awake()
        {
            if (cameraShake == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cameraShake = cam.GetComponent<EarthquakeCameraShake>();
                    if (cameraShake == null) cameraShake = cam.gameObject.AddComponent<EarthquakeCameraShake>();
                }
            }
            if (props == null || props.Length == 0)
                props = FindObjectsByType<ShakeableProp>(FindObjectsInactive.Include);
            if (dustPuffs == null || dustPuffs.Length == 0)
                dustPuffs = FindObjectsByType<QuakeDustPuff>(FindObjectsInactive.Include);
            if (globalLight == null)
            {
                foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude))
                    if (l.lightType == Light2D.LightType.Global) { globalLight = l; break; }
            }
            if (globalLight != null) baseLightIntensity = globalLight.intensity;
            if (rumbleAudio == null)
            {
                rumbleAudio = gameObject.AddComponent<AudioSource>();
                rumbleAudio.playOnAwake = false;
                rumbleAudio.spatialBlend = 0;
                rumbleAudio.volume = generatedRumbleVolume;
                const int rate = 22050;
                var samples = new float[rate * 2];
                var noise = new System.Random(19);
                float filtered = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2f - 1f, 0.04f);
                    float t = (float)i / rate;
                    samples[i] = (Mathf.Sin(t * 40f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * 63f * Mathf.PI * 2f) + filtered) * 0.25f;
                }
                generatedRumble = AudioClip.Create("EarthquakeRumble_Original", samples.Length, 1, rate, false);
                generatedRumble.SetData(samples, 0);
                rumbleAudio.clip = generatedRumble;
            }
            if (rumbleAudio != null) rumbleBaseVolume = rumbleAudio.volume;
            foreach (var d in dust)
                if (d != null) d.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>Sarsıntıyı başlatır (engellemez).</summary>
        public void BeginShake()
        {
            if (IsShaking) return;
            IsShaking = true;
            Debug.Log("[EQ] SARSINTI başladı (kamera, eşyalar, toz, ışık)", this);
            if (cameraShake != null) cameraShake.Shaking = true;
            foreach (var p in props)
                if (p != null) p.BeginShake();
            foreach (var d in dust)
                if (d != null) d.Play(true);
            foreach (var p in dustPuffs)
                if (p != null) p.BeginShake();
            perfFrames = 0;
            perfTime = 0f;
            perfWorst = 0f;
            FadeLight(baseLightIntensity * lightDip);
            if (rumbleAudio != null && rumbleAudio.clip != null)
            {
                rumbleAudio.loop = true;
                rumbleAudio.Play();
            }
            if (onRumbleStart != null) onRumbleStart.Invoke();
            SetIntensity(0.3f);
        }

        /// <summary>Evre şiddeti değişir; eşya sayaçları ve kitaplık sonuçları yeniden başlatılmaz.</summary>
        public void SetIntensity(float value)
        {
            value = Mathf.Clamp01(value);
            if (cameraShake != null) cameraShake.Intensity = value;
            foreach (var prop in props) if (prop != null) prop.SetIntensity(value);
            if (rumbleAudio != null) rumbleAudio.volume = rumbleBaseVolume * value;
        }

        public void FocusProtection(Vector3 point)
        {
            if (cameraShake == null) return;
            Vector3 offset = point - cameraShake.transform.position;
            offset.z = 0;
            cameraShake.FocusOffset = Vector3.ClampMagnitude(offset * 0.15f, 0.25f);
        }

        /// <summary>Sarsıntıyı bitirir: kamera yerine döner, eşyalar durulur, toz yayılmayı bırakır, ışık geri gelir.</summary>
        public void EndShake()
        {
            if (!IsShaking) return;
            IsShaking = false;
            Debug.Log("[EQ] SARSINTI bitti", this);
            if (cameraShake != null) cameraShake.Shaking = false;
            foreach (var p in props)
                if (p != null) p.EndShake();
            foreach (var d in dust)
                if (d != null) d.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var p in dustPuffs)
                if (p != null) p.EndShake();
            if (logFrameTime && perfFrames > 0)
            {
                float avg = perfTime / perfFrames;
                Debug.Log($"[EQ] PERF sarsıntı: {perfFrames} kare, ort. {avg * 1000f:0.0} ms (~{1f / Mathf.Max(0.0001f, avg):0} FPS), " +
                          $"en kötü {perfWorst * 1000f:0.0} ms", this);
            }
            FadeLight(baseLightIntensity);
            if (rumbleAudio != null) rumbleAudio.Stop();
            if (onRumbleEnd != null) onRumbleEnd.Invoke();
        }

        /// <summary>duration sn sarsar ve bitirir.</summary>
        public IEnumerator Shake(float duration)
        {
            BeginShake();
            yield return new WaitForSeconds(Mathf.Max(0f, duration));
            EndShake();
        }

        /// <summary>Sonrası görünümünü uygular (bir kez).</summary>
        public void ApplyAftermath()
        {
            if (aftermathApplied) return;
            aftermathApplied = true;
            int count = 0;

            foreach (var t in FindObjectsByType<AftermathToggle>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                t.Apply();
                count++;
            }

            // Bileşensiz kısa yol: adı "Aftermath" ile başlayanlar açılır, "BeforeQuake" ile başlayanlar kapanır.
            var named = new List<GameObject>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    named.Add(tr.gameObject);
            foreach (var go in named)
            {
                if (go == null || go.GetComponent<AftermathToggle>() != null) continue;
                if (go.name.StartsWith("Aftermath", StringComparison.Ordinal)) { go.SetActive(true); count++; }
                else if (go.name.StartsWith("BeforeQuake", StringComparison.Ordinal)) { go.SetActive(false); count++; }
            }

            foreach (var go in showAfter)
                if (go != null) { go.SetActive(true); count++; }
            foreach (var go in hideAfter)
                if (go != null) { go.SetActive(false); count++; }
            foreach (var s in spriteSwaps)
                if (s != null && s.renderer != null && s.afterSprite != null) { s.renderer.sprite = s.afterSprite; count++; }

            Debug.Log($"[EQ] sonrası görünümü uygulandı ({count} değişiklik)", this);
            if (onAftermath != null) onAftermath.Invoke();
        }

        private void Update()
        {
            if (!IsShaking || !logFrameTime) return;
            float dt = Time.unscaledDeltaTime;
            perfFrames++;
            perfTime += dt;
            if (dt > perfWorst) perfWorst = dt;
        }

        private void FadeLight(float target)
        {
            if (globalLight == null) return;
            if (lightRoutine != null) StopCoroutine(lightRoutine);
            lightRoutine = StartCoroutine(FadeLightRoutine(target));
        }

        private IEnumerator FadeLightRoutine(float target)
        {
            float start = globalLight.intensity;
            for (float t = 0f; t < lightFadeSeconds; t += Time.deltaTime)
            {
                globalLight.intensity = Mathf.Lerp(start, target, t / Mathf.Max(0.01f, lightFadeSeconds));
                yield return null;
            }
            globalLight.intensity = target;
            lightRoutine = null;
        }

        private void OnDisable()
        {
            IsShaking = false;
            if (globalLight != null) globalLight.intensity = baseLightIntensity;
            if (cameraShake != null) cameraShake.Shaking = false;
            foreach (var prop in props) if (prop != null) prop.EndShake();
            if (rumbleAudio != null) rumbleAudio.Stop();
        }

        private void OnDestroy() { if (generatedRumble != null) Destroy(generatedRumble); }
    }
}
