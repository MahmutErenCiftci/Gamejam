using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Sel sokağının piksel yağmuru (Mahmut). Kamerayı izler, kamera kayarken de ekranı kaplar.
    /// Yoğunluk 0..1: 0 = yağmur yok, ~0.35 = hafif yağmur (F05_EXIT), 1 = sağanak (F05_BUSSTOP).
    /// Katmanlar: düşen damla çizgileri (dünya uzayında partikül, order 500), yere çarpma sıçramaları
    /// (zemin seviyesi, order -85: duvar, eşya ve karakterlerin arkasında kalır), sağanakta zemin buğusu,
    /// yoğunlukla beliren sağanak perdesi (kayan döşeli sprite, order 501+) ve sahnenin global Light2D'si ile
    /// hafif karartma (StopRain'de eski hâline döner).
    /// ŞİMŞEK / FLAŞ YOK (OYUN_AKISI §6): bütün parlaklık değişimleri yumuşak geçer.
    /// Ses: UnityEvent kancaları + isteğe bağlı AudioSource (klibi yoksa sessiz kalır).
    /// Örnek: <c>rain.SetIntensity(0.35f, 2f); yield return rain.StartDownpour(5f); rain.StopRain();</c>
    /// </summary>
    [DefaultExecutionOrder(1000)] // kamera takip scriptlerinden sonra (LateUpdate'te kameraya oturur)
    public class RainController : MonoBehaviour
    {
        /// <summary>Düşen damla katmanı: her damla çizimi (A/B/C) ayrı bir partikül sistemidir.</summary>
        [Serializable]
        public class DropLayer
        {
            [Tooltip("Damla partikül sistemi (dünya uzayı, kutu şekli; miktarı/hızı/kutuyu bu bileşen ayarlar)")]
            public ParticleSystem system;
            [Tooltip("Toplam damla miktarından bu katmanın payı")]
            [Range(0f, 1f)] public float share = 0.33f;
            [Tooltip("Düşüş hızı çarpanı (uzun damla = yakın = biraz daha hızlı)")]
            public float speedScale = 1f;
        }

        /// <summary>Sağanak perdesi katmanı: kamerayı kaplayan, kayan döşeli sprite.</summary>
        [Serializable]
        public class SheetLayer
        {
            [Tooltip("Draw Mode = Tiled, sprite pivotu sol-alt (0,0) olan SpriteRenderer")]
            public SpriteRenderer renderer;
            [Tooltip("Desenin kayma hızı (dünya birimi / sn); damlaların eğimiyle aynı yön (-1,-3)")]
            public Vector2 velocity = new Vector2(-1.5f, -4.5f);
            [Tooltip("Tam yoğunlukta saydamlık")]
            [Range(0f, 1f)] public float maxAlpha = 0.35f;
            [Tooltip("Perde bu yoğunluktan sonra belirmeye başlar")]
            [Range(0f, 1f)] public float fadeStart = 0.45f;
        }

        private const float Epsilon = 0.0005f;
        private const float PixelsPerUnit = 64f;

        [Header("Yoğunluk")]
        [Tooltip("Sahne açılınca yoğunluk (0 = yağmur yok)")]
        [Range(0f, 1f)] [SerializeField] private float startIntensity;
        [Tooltip("Damla, sıçrama ve sesin yoğunluğa göre miktarı (x: yoğunluk 0..1, y: miktar 0..1)")]
        [SerializeField] private AnimationCurve amountCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Kamera")]
        [Tooltip("İzlenen kamera; boşsa Camera.main")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Görüş alanının dışında her yönde bırakılan pay (dünya birimi); kamera kayınca kenarlar boş kalmasın")]
        [SerializeField] private float viewMargin = 1f;

        [Header("Damlalar")]
        [SerializeField] private DropLayer[] dropLayers = Array.Empty<DropLayer>();
        [Tooltip("Tam yoğunlukta ekranın birim alanı başına ortalama damla sayısı")]
        [SerializeField] private float dropsPerUnitArea = 5f;
        [Tooltip("Düşüş yönü; damla çizimlerinin eğimiyle aynı olmalı (1 px sola / 3 px aşağı)")]
        [SerializeField] private Vector2 fallDirection = new Vector2(-1f, -3f);
        [Tooltip("Hafif yağmurda düşüş hızı (dünya birimi / sn)")]
        [SerializeField] private float fallSpeedLight = 7f;
        [Tooltip("Sağanakta düşüş hızı (dünya birimi / sn)")]
        [SerializeField] private float fallSpeedHeavy = 10f;
        [Tooltip("Damla saydamlığı: x hafif yağmurda, y sağanakta")]
        [SerializeField] private Vector2 dropAlpha = new Vector2(0.65f, 0.9f);

        [Header("Sıçramalar")]
        [SerializeField] private ParticleSystem splashes;
        [Tooltip("Tam yoğunlukta saniyede birim alan başına sıçrama")]
        [SerializeField] private float splashesPerUnitArea = 2.5f;
        [Tooltip("İsteğe bağlı zemin alanı (ör. sokak zemininin trigger poligonu): sıçrama ve buğu sadece bunun içinde doğar. " +
                 "Boşsa görüş alanının her yerinde doğar (duvar, eşya ve karakterlerin arkasında kalır; boşlukta da görünür).")]
        [SerializeField] private Collider2D groundArea;

        [Header("Zemin buğusu (sağanak)")]
        [SerializeField] private ParticleSystem groundHaze;
        [Tooltip("Buğu bu yoğunluktan sonra başlar")]
        [Range(0f, 1f)] [SerializeField] private float hazeStart = 0.6f;
        [Tooltip("Tam yoğunlukta saniyede birim alan başına buğu bulutu")]
        [SerializeField] private float hazePerUnitArea = 0.05f;

        [Header("Sağanak perdesi")]
        [SerializeField] private SheetLayer[] sheetLayers = Array.Empty<SheetLayer>();

        [Header("Karartma")]
        [Tooltip("Sahnenin global Light2D'si; boşsa aynı sahnedeki global ışık kullanılır")]
        [SerializeField] private Light2D globalLight;
        [Tooltip("Karartma bu yoğunluktan sonra başlar")]
        [Range(0f, 1f)] [SerializeField] private float darkenStart = 0.15f;
        [Tooltip("Tam yoğunlukta ışık şiddetinden düşülen oran (0.3 = %30 daha karanlık)")]
        [Range(0f, 0.8f)] [SerializeField] private float darkenAtFull = 0.3f;
        [Tooltip("Tam yoğunlukta ışık rengi bu renkle çarpılır (soğuk gri-mavi)")]
        [SerializeField] private Color stormTint = new Color(0.8f, 0.86f, 0.97f, 1f);

        [Header("Ses (isteğe bağlı)")]
        [Tooltip("Yağmur döngüsü; klibi yoksa sessiz kalır. Ses seviyesi yoğunlukla artar.")]
        [SerializeField] private AudioSource rainAudio;
        [Range(0f, 1f)] [SerializeField] private float maxVolume = 0.8f;

        [Header("Olaylar (ses kancaları)")]
        [Tooltip("Yağmur 0'dan başladığında")]
        [SerializeField] private UnityEvent onRainStarted = new UnityEvent();
        [Tooltip("StartDownpour çağrıldığında (sağanak başlıyor)")]
        [SerializeField] private UnityEvent onDownpourStarted = new UnityEvent();
        [Tooltip("Yağmur tamamen durduğunda")]
        [SerializeField] private UnityEvent onRainStopped = new UnityEvent();

        private float intensity;
        private float appliedIntensity = -1f;
        private bool tweening;
        private float tweenFrom;
        private float tweenTo;
        private float tweenDuration;
        private float tweenElapsed;
        private int requestToken;

        private bool started;
        private bool lightSearched;
        private bool lightModified;
        private float splashRate;
        private float hazeRate;
        private float splashBudget;
        private float hazeBudget;
        private float lightBaseIntensity;
        private Color lightBaseColor = Color.white;

        private Color[] dropBaseColors = Array.Empty<Color>();
        private Color splashBaseColor = Color.white;
        private Color hazeBaseColor = Color.white;
        private Color[] sheetBaseColors = Array.Empty<Color>();
        private Vector2[] sheetTiles = Array.Empty<Vector2>();
        private Vector2 viewSize = new Vector2(10f, 5.625f);
        private bool layoutDirty = true;

        /// <summary>Şu anki yoğunluk (0..1).</summary>
        public float Intensity => intensity;

        /// <summary>Geçiş sürüyorsa varılacak yoğunluk, yoksa şu anki.</summary>
        public float TargetIntensity => tweening ? tweenTo : intensity;

        /// <summary>Yağmur yağıyor mu (yoğunluk > 0)?</summary>
        public bool IsRaining => intensity > Epsilon;

        /// <summary>Kullanılan global ışık (yoksa null).</summary>
        public Light2D GlobalLight => ResolveLight();

        public UnityEvent OnRainStarted => onRainStarted;
        public UnityEvent OnDownpourStarted => onDownpourStarted;
        public UnityEvent OnRainStopped => onRainStopped;

        private void Awake()
        {
            CacheBaseColors();
            if (rainAudio != null)
            {
                rainAudio.loop = true;
                rainAudio.playOnAwake = false;
            }
            intensity = Mathf.Clamp01(startIntensity);
        }

        private void Start()
        {
            // Sahne yüklenirken (OnEnable) diğer objeler henüz etkin değildi: global ışığı şimdi ara ve yeniden uygula.
            started = true;
            lightSearched = false;
            appliedIntensity = -1f;
            if (intensity > Epsilon) onRainStarted.Invoke();
        }

        private void OnEnable()
        {
            appliedIntensity = -1f; // ışık karartması ve miktarlar yeniden uygulansın
            layoutDirty = true;
            FollowCamera();
            Apply();
        }

        private void OnDisable()
        {
            // Yağmur kökü kapanınca (adım değişimi) sahne karanlık kalmasın; açılınca yeniden uygulanır.
            RestoreLight();
            if (rainAudio != null && rainAudio.isPlaying) rainAudio.Stop();
        }

        /// <summary>
        /// Yoğunluğu seconds saniyede target01'e taşır (0 = hemen). Obje kapalıysa hemen ayarlanır.
        /// 0'dan başlıyorsa onRainStarted tetiklenir.
        /// </summary>
        public void SetIntensity(float target01, float seconds)
        {
            float target = Mathf.Clamp01(target01);
            bool wasRaining = intensity > Epsilon;
            requestToken++;

            if (seconds <= 0f || !isActiveAndEnabled)
            {
                tweening = false;
                intensity = target;
                if (isActiveAndEnabled) Apply();
            }
            else
            {
                tweening = true;
                tweenFrom = intensity;
                tweenTo = target;
                tweenDuration = seconds;
                tweenElapsed = 0f;
            }

            if (!wasRaining && target > Epsilon) onRainStarted.Invoke();
            if (wasRaining && !tweening && intensity <= Epsilon) onRainStopped.Invoke();
        }

        /// <summary>UnityEvent'ten bağlamak için: yoğunluğu hemen ayarlar.</summary>
        public void SetIntensityNow(float target01) => SetIntensity(target01, 0f);

        /// <summary>
        /// Sağanak: onDownpourStarted tetiklenir, yoğunluk seconds saniyede 1'e çıkar; 1'e varınca biter.
        /// Bu arada başka bir SetIntensity/StopRain çağrılırsa beklemeyi bırakır. Obje kapalıysa hemen 1 olur.
        /// </summary>
        public IEnumerator StartDownpour(float seconds = 5f)
        {
            onDownpourStarted.Invoke();
            SetIntensity(1f, seconds);
            int token = requestToken;
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds) + 2f;
            while (token == requestToken && intensity < 0.999f && isActiveAndEnabled &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            if (token == requestToken && intensity < 1f)
            {
                tweening = false;
                intensity = 1f;
                if (isActiveAndEnabled) Apply();
            }
        }

        /// <summary>
        /// Yağmuru hemen durdurur: damlalar silinir, perde kapanır, ışık eski hâline döner, ses durur.
        /// Ani değişim olmaması için kararma (fade) altında çağırın ya da StopRain(saniye) kullanın.
        /// </summary>
        public void StopRain()
        {
            bool wasRaining = intensity > Epsilon || tweening;
            requestToken++;
            tweening = false;
            intensity = 0f;
            foreach (var layer in dropLayers)
                if (layer != null && layer.system != null) layer.system.Clear(true);
            if (splashes != null) splashes.Clear(true);
            if (groundHaze != null) groundHaze.Clear(true);
            appliedIntensity = -1f;
            Apply();
            RestoreLight();
            if (rainAudio != null && rainAudio.isPlaying) rainAudio.Stop();
            if (wasRaining) onRainStopped.Invoke();
        }

        /// <summary>Yağmuru seconds saniyede yavaşça dindirir (ışık da yavaşça eski hâline döner).</summary>
        public void StopRain(float seconds) => SetIntensity(0f, seconds);

        private void Update()
        {
            if (!tweening) return;

            tweenElapsed += Time.deltaTime;
            float t = tweenDuration > 0f ? Mathf.Clamp01(tweenElapsed / tweenDuration) : 1f;
            intensity = Mathf.Lerp(tweenFrom, tweenTo, t);
            if (t < 1f) return;

            tweening = false;
            intensity = tweenTo;
            if (intensity <= Epsilon)
            {
                intensity = 0f;
                onRainStopped.Invoke();
            }
        }

        private void LateUpdate()
        {
            FollowCamera();
            Apply();
            ScrollSheets();
            EmitOnGround(splashes, splashRate, ref splashBudget);
            EmitOnGround(groundHaze, hazeRate, ref hazeBudget);
        }

        private Camera ResolveCamera()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            return targetCamera;
        }

        // Kök kameranın üstüne oturur; görüş alanı değişince kutular ve perde boyutu yeniden hesaplanır.
        private void FollowCamera()
        {
            var cam = ResolveCamera();
            if (cam == null) return;

            Vector3 c = cam.transform.position;
            transform.position = new Vector3(c.x, c.y, 0f);

            float h = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            var size = new Vector2(h * cam.aspect, h);
            if ((size - viewSize).sqrMagnitude > 0.0001f)
            {
                viewSize = size;
                layoutDirty = true;
            }
        }

        // Yoğunluk ya da görüş alanı değiştiyse bütün katmanları ayarlar.
        private void Apply()
        {
            bool changed = Mathf.Abs(intensity - appliedIntensity) > 0.0001f;
            if (!changed && !layoutDirty) return;
            appliedIntensity = intensity;
            bool relayout = layoutDirty;
            layoutDirty = false;

            float amount = intensity > Epsilon ? Mathf.Clamp01(amountCurve.Evaluate(intensity)) : 0f;
            float heavy = Mathf.Clamp01(intensity);

            ApplyDrops(amount, heavy);
            ApplySplashes(amount);
            ApplyHaze(Smooth(hazeStart, 1f, intensity));
            ApplySheets(relayout);
            ApplyLight(Smooth(darkenStart, 1f, intensity));
            ApplyAudio(amount);
        }

        private void ApplyDrops(float amount, float heavy)
        {
            Vector2 dir = fallDirection.sqrMagnitude > 0.0001f ? fallDirection.normalized : Vector2.down;
            for (int i = 0; i < dropLayers.Length; i++)
            {
                var layer = dropLayers[i];
                if (layer == null || layer.system == null) continue;
                var ps = layer.system;
                var main = ps.main;

                float speed = Mathf.Lerp(fallSpeedLight, fallSpeedHeavy, heavy) * Mathf.Max(0.1f, layer.speedScale);
                Vector2 velocity = dir * speed;
                float life = Mathf.Max(0.05f, main.startLifetime.constantMax);
                Vector2 travel = velocity * life;

                // Kutu: görüş alanı + pay + damlanın ömrü boyunca aldığı yol (yukarı akıştan gelenler de görünsün).
                var box = new Vector2(viewSize.x + 2f * viewMargin + Mathf.Abs(travel.x),
                                      viewSize.y + 2f * viewMargin + Mathf.Abs(travel.y));
                var shape = ps.shape;
                shape.position = new Vector3(-travel.x * 0.5f, -travel.y * 0.5f, 0f);
                shape.scale = new Vector3(box.x, box.y, 0.001f);

                var vol = ps.velocityOverLifetime;
                vol.enabled = true;
                vol.space = ParticleSystemSimulationSpace.World;
                vol.x = new ParticleSystem.MinMaxCurve(velocity.x);
                vol.y = new ParticleSystem.MinMaxCurve(velocity.y);
                vol.z = new ParticleSystem.MinMaxCurve(0f);

                float alive = amount * layer.share * dropsPerUnitArea * box.x * box.y;
                var emission = ps.emission;
                emission.rateOverTime = alive / life;
                int needed = Mathf.CeilToInt(alive * 1.3f) + 50;
                if (main.maxParticles < needed) main.maxParticles = needed;

                Color c = i < dropBaseColors.Length ? dropBaseColors[i] : Color.white;
                c.a *= Mathf.Lerp(dropAlpha.x, dropAlpha.y, heavy);
                main.startColor = c;
            }
        }

        private void ApplySplashes(float amount)
        {
            if (splashes == null) return;
            var box = new Vector2(viewSize.x + 2f * viewMargin, viewSize.y + 2f * viewMargin);
            var shape = splashes.shape;
            shape.position = Vector3.zero;
            shape.scale = new Vector3(box.x, box.y, 0.001f);
            float rate = amount * splashesPerUnitArea * box.x * box.y;
            var emission = splashes.emission;
            emission.rateOverTime = groundArea != null ? 0f : rate;
            splashRate = groundArea != null ? rate : 0f;
            var main = splashes.main;
            Color c = splashBaseColor;
            c.a *= Mathf.Lerp(0.7f, 1f, intensity);
            main.startColor = c;
        }

        private void ApplyHaze(float amount)
        {
            if (groundHaze == null) return;
            var box = new Vector2(viewSize.x + 2f * viewMargin, viewSize.y + 2f * viewMargin);
            var shape = groundHaze.shape;
            shape.position = Vector3.zero;
            shape.scale = new Vector3(box.x, box.y, 0.001f);
            float rate = amount * hazePerUnitArea * box.x * box.y;
            var emission = groundHaze.emission;
            emission.rateOverTime = groundArea != null ? 0f : rate;
            hazeRate = groundArea != null ? rate : 0f;
            var main = groundHaze.main;
            main.startColor = hazeBaseColor;
        }

        // groundArea varken sıçrama/buğu elle doğar: görüş alanında rastgele nokta, alanın içindeyse doğar
        // (dışarıdakiler atlanır, böylece zemindeki yoğunluk alan olmadan olduğu gibi kalır).
        private void EmitOnGround(ParticleSystem ps, float rate, ref float budget)
        {
            if (ps == null || groundArea == null || rate <= 0f || !groundArea.isActiveAndEnabled)
            {
                budget = 0f;
                return;
            }
            budget += rate * Time.deltaTime;
            int count = Mathf.Min((int)budget, 64);
            budget -= (int)budget;
            if (count <= 0) return;

            Vector2 center = transform.position;
            Vector2 half = viewSize * 0.5f + new Vector2(viewMargin, viewMargin);
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector2(center.x + UnityEngine.Random.Range(-half.x, half.x),
                                      center.y + UnityEngine.Random.Range(-half.y, half.y));
                if (!groundArea.OverlapPoint(pos)) continue;
                p.position = new Vector3(Snap(pos.x), Snap(pos.y), 0f);
                ps.Emit(p, 1);
            }
        }

        private void ApplySheets(bool relayout)
        {
            for (int i = 0; i < sheetLayers.Length; i++)
            {
                var layer = sheetLayers[i];
                if (layer == null || layer.renderer == null) continue;
                var r = layer.renderer;

                float a = layer.maxAlpha * Smooth(layer.fadeStart, 1f, intensity);
                bool show = a > 0.002f;
                if (r.enabled != show) r.enabled = show;
                Color baseColor = i < sheetBaseColors.Length ? sheetBaseColors[i] : Color.white;
                r.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * a);

                if (!relayout || i >= sheetTiles.Length) continue;
                Vector2 tile = sheetTiles[i];
                if (tile.x <= 0f || tile.y <= 0f || r.drawMode == SpriteDrawMode.Simple) continue;
                // Görüş alanı + pay + bir döşeme (desen her karede bir döşeme içinde kaydırılır).
                float w = (Mathf.Ceil((viewSize.x + 2f * viewMargin) / tile.x) + 1f) * tile.x;
                float h = (Mathf.Ceil((viewSize.y + 2f * viewMargin) / tile.y) + 1f) * tile.y;
                r.size = new Vector2(w, h);
            }
        }

        // Perdeyi dünyaya sabit bir desen gibi kaydırır: desen velocity ile akar, kamera kayınca desen yerinde kalır.
        private void ScrollSheets()
        {
            if (sheetLayers.Length == 0) return;
            Vector2 center = transform.position;
            Vector2 min = center - viewSize * 0.5f - new Vector2(viewMargin, viewMargin);
            float time = Time.time;
            for (int i = 0; i < sheetLayers.Length && i < sheetTiles.Length; i++)
            {
                var layer = sheetLayers[i];
                if (layer == null || layer.renderer == null || !layer.renderer.enabled) continue;
                Vector2 tile = sheetTiles[i];
                if (tile.x <= 0f || tile.y <= 0f) continue;

                Vector2 scroll = layer.velocity * time;
                scroll = new Vector2(Mod(scroll.x, tile.x), Mod(scroll.y, tile.y));
                float x = Mathf.Floor((min.x - scroll.x) / tile.x) * tile.x + scroll.x;
                float y = Mathf.Floor((min.y - scroll.y) / tile.y) * tile.y + scroll.y;
                layer.renderer.transform.position = new Vector3(Snap(x), Snap(y), 0f);
            }
        }

        private void ApplyLight(float darkness)
        {
            var light = ResolveLight();
            if (light == null) return;
            if (darkness <= Epsilon)
            {
                RestoreLight();
                return;
            }

            if (!lightModified)
            {
                lightBaseIntensity = light.intensity;
                lightBaseColor = light.color;
                lightModified = true;
            }
            light.intensity = lightBaseIntensity * (1f - darkenAtFull * darkness);
            Color tinted = new Color(lightBaseColor.r * stormTint.r, lightBaseColor.g * stormTint.g,
                lightBaseColor.b * stormTint.b, lightBaseColor.a);
            light.color = Color.Lerp(lightBaseColor, tinted, darkness);
        }

        private void RestoreLight()
        {
            if (!lightModified) return;
            lightModified = false;
            if (globalLight == null) return;
            globalLight.intensity = lightBaseIntensity;
            globalLight.color = lightBaseColor;
        }

        private void ApplyAudio(float amount)
        {
            if (rainAudio == null) return;
            rainAudio.volume = maxVolume * amount;
            if (amount > Epsilon && rainAudio.clip != null && !rainAudio.isPlaying && rainAudio.isActiveAndEnabled)
                rainAudio.Play();
            else if (amount <= Epsilon && rainAudio.isPlaying)
                rainAudio.Stop();
        }

        private Light2D ResolveLight()
        {
            if (globalLight != null) return globalLight;
            if (lightSearched || !started) return null; // Start'tan önce aranmaz (ışık henüz etkin olmayabilir)
            lightSearched = true;
            globalLight = FindGlobalLight(gameObject.scene);
            for (int i = 0; globalLight == null && i < SceneManager.sceneCount; i++)
                globalLight = FindGlobalLight(SceneManager.GetSceneAt(i));
            return globalLight;
        }

        private static Light2D FindGlobalLight(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light2D>(true))
                    if (light.lightType == Light2D.LightType.Global && light.enabled && light.gameObject.activeInHierarchy)
                        return light;
            return null;
        }

        private void CacheBaseColors()
        {
            dropBaseColors = new Color[dropLayers.Length];
            for (int i = 0; i < dropLayers.Length; i++)
                dropBaseColors[i] = dropLayers[i] != null && dropLayers[i].system != null
                    ? dropLayers[i].system.main.startColor.color
                    : Color.white;
            if (splashes != null) splashBaseColor = splashes.main.startColor.color;
            if (groundHaze != null) hazeBaseColor = groundHaze.main.startColor.color;

            sheetBaseColors = new Color[sheetLayers.Length];
            sheetTiles = new Vector2[sheetLayers.Length];
            for (int i = 0; i < sheetLayers.Length; i++)
            {
                var r = sheetLayers[i] != null ? sheetLayers[i].renderer : null;
                if (r == null) continue;
                sheetBaseColors[i] = r.color;
                if (r.sprite != null) sheetTiles[i] = r.sprite.rect.size / r.sprite.pixelsPerUnit;
                r.enabled = false;
            }
        }

        private static float Smooth(float from, float to, float x)
        {
            float t = to > from ? Mathf.Clamp01((x - from) / (to - from)) : (x >= to ? 1f : 0f);
            return t * t * (3f - 2f * t);
        }

        private static float Mod(float a, float m) => a - Mathf.Floor(a / m) * m;

        private static float Snap(float v) => Mathf.Round(v * PixelsPerUnit) / PixelsPerUnit;
    }
}
