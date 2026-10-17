using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Pencerenin katmanlı hava durumu. Açık: gün doğumu, şehir silueti, güneş. Fırtına: koyu gökyüzü,
    // bulutlar, yağmur parçacıkları, camda damlalar, ara sıra şimşek ve odaya mavi-beyaz ışık.
    // Dış katmanlar SpriteMask ile cam alanında kırpılır. Geçiş ~1,5 sn; açık havaya dönünce fırtına
    // objeleri tamamen kapatılır (görünmeyen efekt çalışmaz). Şimşek tek ve yumuşaktır, art arda flaş yok.
    public class DynamicWindowController : MonoBehaviour
    {
        public enum Weather { Clear, Storm }

        [Header("Katmanlar")]
        [SerializeField] SpriteRenderer[] clearLayers;
        [SerializeField] SpriteRenderer[] stormLayers;
        [Tooltip("Fırtına sırasında açık olan kök (yağmur, damlalar, şimşek, bulutlar).")]
        [SerializeField] GameObject stormRoot;
        [SerializeField] ParticleSystem rain;
        [SerializeField] SpriteRenderer droplets;
        [SerializeField] Transform[] driftingClouds;
        [SerializeField] float cloudDriftPixelsPerSecond = 3f;

        [Header("Şimşek")]
        [SerializeField] SpriteRenderer lightningBolt;
        [SerializeField] SpriteRenderer windowFlash;
        [SerializeField] float windowFlashPeak = 0.7f;
        [Tooltip("Odaya düşen ışık. Düşük tutulmalı; yüksek olunca oda gri pusa girer.")]
        [SerializeField] SpriteRenderer roomFlash;
        [SerializeField] float roomFlashPeak = 0.08f;
        [SerializeField] Vector2 firstLightningDelay = new Vector2(1.2f, 2.2f);
        [SerializeField] Vector2 lightningInterval = new Vector2(5f, 9f);

        [Header("Oda ışığı")]
        [Tooltip("Tüm odanın üstünde ince bir renk katmanı: açıkta sıcak turuncu, fırtınada soğuk lacivert.")]
        [SerializeField] SpriteRenderer roomTint;
        [SerializeField] Color clearTint = new Color(1f, 0.6f, 0.25f, 0.07f);
        [SerializeField] Color stormTint = new Color(0.08f, 0.12f, 0.3f, 0.3f);

        [Header("Ses (clip boşsa kodla üretilir)")]
        [SerializeField] AudioSource rainAudio;
        [SerializeField] AudioSource thunderAudio;
        [SerializeField] AudioClip rainClip;
        [SerializeField] AudioClip thunderClip;
        [SerializeField] float rainVolume = 0.3f;
        [SerializeField] float thunderVolume = 0.55f;

        [SerializeField] float transitionDuration = 1.5f;

        const float PixelsPerUnit = 16f;
        public Weather Current { get; private set; } = Weather.Clear;
        float storm;               // 0 açık, 1 fırtına
        Coroutine transition;
        Coroutine lightning;
        Vector3[] cloudStart;

        void Awake()
        {
            cloudStart = new Vector3[driftingClouds.Length];
            for (int i = 0; i < driftingClouds.Length; i++)
                cloudStart[i] = driftingClouds[i].localPosition;
            ApplyBlend(0f);
            SetFlash(0f);
            if (stormRoot != null) stormRoot.SetActive(false);
        }

        public void SetWeather(Weather weather)
        {
            if (weather == Current && transition == null) return;
            Current = weather;
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Transition(weather == Weather.Storm ? 1f : 0f));
        }

        IEnumerator Transition(float target)
        {
            if (target > 0f)
            {
                if (stormRoot != null) stormRoot.SetActive(true);
                if (rain != null && !rain.isPlaying) rain.Play();
                if (rainAudio != null && !rainAudio.isPlaying)
                {
                    rainAudio.clip = rainClip != null ? rainClip : MenuAudioSynth.Rain;
                    rainAudio.loop = true;
                    rainAudio.Play();
                }
                if (lightning == null) lightning = StartCoroutine(LightningLoop());
            }
            else
            {
                if (rain != null) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (lightning != null) { StopCoroutine(lightning); lightning = null; }
                SetFlash(0f);
            }

            float start = storm;
            for (float t = 0f; t < transitionDuration; t += Time.unscaledDeltaTime)
            {
                ApplyBlend(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / transitionDuration)));
                yield return null;
            }
            ApplyBlend(target);

            if (target <= 0f)
            {
                if (rain != null) rain.Clear(true);
                if (rainAudio != null) rainAudio.Stop();
                if (thunderAudio != null) thunderAudio.Stop();
                if (stormRoot != null) stormRoot.SetActive(false);
                for (int i = 0; i < driftingClouds.Length; i++)
                    driftingClouds[i].localPosition = cloudStart[i];
            }
            transition = null;
        }

        void Update()
        {
            // Bulutlar sadece fırtına görünürken kayar.
            if (storm <= 0f) return;
            for (int i = 0; i < driftingClouds.Length; i++)
            {
                Transform cloud = driftingClouds[i];
                float x = cloud.localPosition.x + cloudDriftPixelsPerSecond / PixelsPerUnit * Time.unscaledDeltaTime * (i % 2 == 0 ? 1f : 0.6f);
                if (x > cloudStart[i].x + 3f) x = cloudStart[i].x - 3f;
                cloud.localPosition = new Vector3(x, cloud.localPosition.y, cloud.localPosition.z);
            }
        }

        void ApplyBlend(float k)
        {
            storm = k;
            foreach (SpriteRenderer layer in clearLayers) SetAlpha(layer, 1f - k);
            foreach (SpriteRenderer layer in stormLayers) SetAlpha(layer, k);
            SetAlpha(droplets, k * 0.85f);
            if (roomTint != null) roomTint.color = Color.Lerp(clearTint, stormTint, k);
            if (rainAudio != null) rainAudio.volume = rainVolume * k;
        }

        IEnumerator LightningLoop()
        {
            yield return new WaitForSecondsRealtime(Random.Range(firstLightningDelay.x, firstLightningDelay.y));
            while (true)
            {
                if (lightningBolt != null) lightningBolt.enabled = true;
                for (float t = 0f; t < 0.08f; t += Time.unscaledDeltaTime) { SetFlash(t / 0.08f); yield return null; }
                for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
                {
                    SetFlash(1f - t / 0.45f);
                    if (lightningBolt != null && t > 0.2f) lightningBolt.enabled = false;
                    yield return null;
                }
                SetFlash(0f);
                if (lightningBolt != null) lightningBolt.enabled = false;

                // Ses ışıktan sonra gelir.
                yield return new WaitForSecondsRealtime(Random.Range(0.4f, 1.1f));
                if (thunderAudio != null)
                    thunderAudio.PlayOneShot(thunderClip != null ? thunderClip : MenuAudioSynth.Thunder, thunderVolume);
                yield return new WaitForSecondsRealtime(Random.Range(lightningInterval.x, lightningInterval.y));
            }
        }

        void SetFlash(float amount)
        {
            SetAlpha(windowFlash, windowFlashPeak * amount);
            SetAlpha(roomFlash, roomFlashPeak * amount);
            if (amount <= 0f && lightningBolt != null) lightningBolt.enabled = false;
        }

        static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }
    }
}
