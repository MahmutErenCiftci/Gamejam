using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Kontrollü şimşek (ışığa duyarlılık güvenli). Sel sayacı akarken 10–25 sn'de bir:
    /// - sahne kısa süre aydınlanır: kamerayı izleyen, ekrandan geniş, düşük şiddetli bir 2D nokta ışığı global ışığa EKLENİR (global ışığa
    ///   dokunulmaz; yağmurun karartmasıyla çakışmaz). Yumuşak yükseliş ~0,06 sn, yavaş sönüş ~0,5 sn, en çok +0,3 şiddet, soluk mavi-beyaz
    ///   ton — ekran asla tam beyaz olmaz; çakma başına en çok 2 parıltı (saniyede 3'ün altında);
    /// - gökyüzü bandında uzak, ince bir şimşek çizgisi belirir;
    /// - mesafeye göre 0,7–2,4 sn sonra uzak gök gürültüsü (SFX_FE_Thunder_*; tools/audio/generate_flood_hazards.js).
    /// Oyuncu ayarı FloodEveningSettings.LightningEnabled kapalıysa yalnız ses çalar (oyunda [L] aç/kapa; ilk şimşekte bir kez hatırlatılır). Sayaç durunca (avlu / apartman finali / süre doldu)
    /// yeni çakma olmaz; Hub.RequestReset'te ışık hemen söner.
    /// </summary>
    public class FloodLightning : MonoBehaviour, IFloodEveningResettable
    {
        [Tooltip("Parıltı ışığı (boşsa sahnedeki bir sokak lambası ışığı kopyalanır: aynı sıralama katmanlarını aydınlatır)")]
        [SerializeField] private Light2D flashLight;
        [Tooltip("İki çakma arası (sn)")]
        [SerializeField] private Vector2 interval = new Vector2(10f, 25f);
        [Tooltip("En yüksek ışık artışı (global ışığa eklenen şiddet)")]
        [Range(0f, 0.5f)] [SerializeField] private float maxBoost = 0.3f;
        [SerializeField] private Color flashColor = new Color(0.86f, 0.9f, 1f, 1f);
        [SerializeField] private float attack = 0.06f;
        [SerializeField] private float decay = 0.5f;
        [Header("Gökyüzü")]
        [Tooltip("Gökyüzü bandı (kamerayı izleyen 'Sky'); şimşek çizgisi bunun çocuğu olur")]
        [SerializeField] private Transform sky;
        [SerializeField] private Sprite[] boltSprites = System.Array.Empty<Sprite>();
        [SerializeField] private int boltOrder = -199;
        [Header("Ses")]
        [SerializeField] private string[] thunderSfx = { "SFX_FE_Thunder_1", "SFX_FE_Thunder_2", "SFX_FE_Thunder_3" };
        [SerializeField] private Vector2 thunderDelay = new Vector2(0.7f, 2.4f);
        [SerializeField] private bool logStrikes = true;

        private float timer, next;
        private Coroutine strike;
        private SpriteRenderer bolt;
        private FloodEveningHud hud;
        private bool hintShown;

        public int StrikeCount { get; private set; }
        public float CurrentBoost { get; private set; }
        public bool Striking => strike != null;

        private void Start()
        {
            if (flashLight == null)
            {
                Light2D template = null;
                foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include))
                    if (l.lightType == Light2D.LightType.Point) { template = l; break; }
                if (template != null)
                {
                    flashLight = Instantiate(template, transform);
                    flashLight.name = "LightningFlash";
                }
            }
            if (flashLight != null)
            {
                flashLight.pointLightInnerRadius = 28f;
                flashLight.pointLightOuterRadius = 30f;
                flashLight.color = flashColor;
                flashLight.intensity = 0f;
                flashLight.enabled = false;
            }
            next = Random.Range(interval.x, interval.y);
            hud = FindAnyObjectByType<FloodEveningHud>(FindObjectsInactive.Include);
            if (sky == null) { var s = GameObject.Find("Sky"); if (s != null) sky = s.transform; }
            if (sky != null && boltSprites.Length > 0)
            {
                var go = new GameObject("LightningBolt");
                go.transform.SetParent(sky, false);
                bolt = go.AddComponent<SpriteRenderer>();
                var band = sky.GetComponentInChildren<SpriteRenderer>(true);
                if (band != null) { bolt.sortingLayerID = band.sortingLayerID; bolt.sharedMaterial = band.sharedMaterial; }
                bolt.sortingOrder = boltOrder;
                bolt.enabled = false;
            }
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable() { FloodEveningHub.ResetRequested -= ResetModule; StopStrike(); }

        private void Update()
        {
            // ışığa duyarlılık ayarı: L parlamayı açar/kapatır (PlayerPrefs'te kalır; menü ayarı da aynı anahtarı kullanabilir)
            if (GameInput.LightningTogglePressed && FloodEveningHub.FreeRoam) ToggleSetting();
            if (!FloodEveningHub.FloodStarted || FloodEveningHub.FloodStopped || strike != null) return;
            timer += Time.deltaTime;
            if (timer < next) return;
            timer = 0f;
            next = Random.Range(interval.x, interval.y);
            strike = StartCoroutine(Strike());
        }

        /// <summary>Yalnız test: hemen bir çakma.</summary>
        public void DebugStrike()
        {
            if (strike != null) return;
            timer = 0f;
            strike = StartCoroutine(Strike());
        }

        private IEnumerator Strike()
        {
            StrikeCount++;
            bool visual = FloodEveningSettings.LightningEnabled && flashLight != null && FloodEveningHub.OutdoorAudio01 > 0.5f;
            int flickers = Random.value < 0.5f ? 1 : 2;
            float peak = maxBoost * Random.Range(0.7f, 1f);
            float second = Random.Range(0.14f, 0.22f);
            float delay = Random.Range(thunderDelay.x, thunderDelay.y);
            if (logStrikes) Debug.Log($"[FE] şimşek #{StrikeCount} (parıltı {flickers}, tepe +{peak:0.00}, görsel {(visual ? "açık" : "kapalı")}, gök gürültüsü {delay:0.0} sn sonra)", this);
            if (bolt != null && visual)
            {
                bolt.sprite = boltSprites[Random.Range(0, boltSprites.Length)];
                bolt.transform.localPosition = new Vector3(Mathf.Round(Random.Range(-2.4f, 2.4f) * 64f) / 64f, 0.95f, 0f);
                bolt.flipX = Random.value < 0.5f;
            }
            float end = (flickers == 2 ? second : 0f) + attack + decay;
            float tau = decay / 3f;
            for (float t = 0f; t < end; t += Time.deltaTime)
            {
                float b = Pulse(t, peak, tau);
                if (flickers == 2) b = Mathf.Max(b, Pulse(t - second, peak * 0.55f, tau));
                CurrentBoost = b;
                if (visual && !FloodEveningSettings.LightningEnabled) { visual = false; RestoreLight(); } // L ile anında kapatıldı
                if (visual)
                {
                    var cam = Camera.main;
                    if (cam != null) flashLight.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
                    flashLight.enabled = b > 0.004f;
                    flashLight.intensity = b;
                    if (bolt != null)
                    {
                        bolt.enabled = t < 0.32f && b > peak * 0.25f;
                        bolt.color = new Color(1f, 1f, 1f, Mathf.Clamp01(b / peak));
                    }
                }
                yield return null;
            }
            RestoreLight();
            ShowHintOnce();
            for (float t = end; t < delay; t += Time.deltaTime) yield return null;
            if (thunderSfx.Length > 0 && FloodEveningHub.OutdoorAudio01 > 0.05f)
                GameAudio.PlaySfx(thunderSfx[Random.Range(0, thunderSfx.Length)], Random.Range(0.55f, 0.85f) * FloodEveningHub.OutdoorAudio01);
            strike = null;
        }

        /// <summary>İlk şimşekten sonra bir kez: parlamanın L ile kapatılabileceğini söyler (şerit doluysa sonraki şimşekte).</summary>
        private void ShowHintOnce()
        {
            if (hintShown || hud == null || hud.IsToastShowing || !FloodEveningHub.FreeRoam) return;
            hintShown = true;
            hud.ShowToast("ŞİMŞEK", FloodEveningSettings.LightningEnabled
                ? "Parlamalar rahatsız ediyorsa [L] ile kapatabilirsin."
                : "Şimşek parlaması kapalı. [L] ile açabilirsin.", FloodEveningHud.ToastKind.Info, 3f);
        }

        /// <summary>Işığa duyarlılık ayarını değiştirir; kapatılırsa süren parlama hemen söner.</summary>
        public void ToggleSetting()
        {
            bool on = !FloodEveningSettings.LightningEnabled;
            FloodEveningSettings.LightningEnabled = on;
            if (!on) RestoreLight();
            hintShown = true;
            if (hud != null) hud.ShowToast("ŞİMŞEK PARLAMASI " + (on ? "AÇIK" : "KAPALI"), on ? "[L] ile kapatabilirsin." : "Gök gürültüsü duyulur, ekran aydınlanmaz. [L] ile açabilirsin.", FloodEveningHud.ToastKind.Info, 2.5f);
            if (logStrikes) Debug.Log("[FE] şimşek parlaması " + (on ? "açık" : "kapalı"), this);
        }

        private float Pulse(float x, float peak, float tau)
        {
            if (x < 0f) return 0f;
            if (x < attack) return peak * x / attack;
            return peak * Mathf.Exp(-(x - attack) / tau);
        }

        private void RestoreLight()
        {
            CurrentBoost = 0f;
            if (flashLight != null) { flashLight.intensity = 0f; flashLight.enabled = false; }
            if (bolt != null) bolt.enabled = false;
        }

        private void StopStrike()
        {
            if (strike != null) { StopCoroutine(strike); strike = null; }
            RestoreLight();
        }

        public void ResetModule()
        {
            StopStrike();
            timer = 0f;
            next = Random.Range(interval.x, interval.y);
        }
    }
}
