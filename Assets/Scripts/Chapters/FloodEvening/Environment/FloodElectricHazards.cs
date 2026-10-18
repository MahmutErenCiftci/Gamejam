using System;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Objectives;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Kopuk elektrik telleri ve çevresindeki elektrikli su bölgeleri (LEAD 18 Eki). Teller/direkler haritada (hazards.js + layout.js);
    /// bu bileşen her kopuk telin suya değdiği nokta (World/Hazards/Zone_n) için çalışma anı parçalarını kurar:
    /// - Kablo ucunda kıvılcım (her zaman; kopuk tel suyla da susuz da tehlikelidir), kontrollü mavi parıltı (2D nokta ışığı),
    ///   su varken (FloodWaterField.DepthAt > ~0,02) su yüzeyinde kısa arklar, yakınken yanıp sönen ⚡ uyarı simgesi, yaklaştıkça artan çıtırtı.
    /// - Bölge: kablo ucunun çevresi (core, her zaman) + su varken daha geniş su bölgesi. Bölgeler sokağın tamamını kapatmaz (açık şerit kalır).
    /// - Girilirse: kısa çarpılma tepkisi (Cenk'te kısa renk vurgusu — tam ekran beyaz YOK), ~0,4 sn hareket kilidi, bölgenin dışındaki
    ///   önceden belirlenmiş güvenli noktaya doğru geri itme (duvarlardan geçmez), bekleme süresi (döngü olmaz); ilerlemeyi asla kesmez,
    ///   tek başına başarısızlık değildir. Sonuç ekranına "power_shock" (düzeltilen yanlış), görev paneline uyarı gider.
    /// - Gerçek hayat dersi: "Sarkan kabloya ve yakınındaki suya yaklaşma." (ilk yaklaşımda üst şerit + sonuç ekranı).
    /// </summary>
    public class FloodElectricHazards : MonoBehaviour, IFloodEveningResettable
    {
        [Serializable]
        public class Zone
        {
            public string id = "Zone_1";
            [Tooltip("Kablo ucunun suya değdiği nokta")]
            public Transform contact;
            [Tooltip("Geri itme hedefleri (bölgenin dışında, açık yolda)")]
            public Transform[] safePoints = Array.Empty<Transform>();
            [Tooltip("Kablo ucu çevresi (zemin ölçüsü, birim): her zaman tehlikeli")]
            public float coreRadius = 0.3f;
            [Tooltip("Su varken etkin bölge yarıçapı (zemin ölçüsü, birim)")]
            public float waterRadius = 0.72f;
        }

        [SerializeField] private Zone[] zones = Array.Empty<Zone>();
        [SerializeField] private FloodWaterField water;
        [SerializeField] private FloodPlayerReaction reaction;
        [SerializeField] private Sprite[] sparkFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] arcFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite warnIcon;
        [Tooltip("Parıltı için kopyalanacak 2D nokta ışığı (boşsa sahnedeki ilk nokta ışığı)")]
        [SerializeField] private Light2D glowTemplate;
        [SerializeField] private float activeDepth = 0.02f;
        [Header("Çarpılma")]
        [SerializeField] private float shockCooldown = 1.6f;
        [SerializeField] private float shockLock = 0.4f;
        [SerializeField] private float knockSpeed = 3.2f;
        [SerializeField] private float knockMax = 0.5f;
        [SerializeField] private string shockSfx = "SFX_FE_Shock";
        [Header("Uyarı ve ses")]
        [SerializeField] private string crackleLoop = "AMB_FE_Electric_Crackle_Loop";
        [SerializeField] private string crackleChannel = "fe_electric";
        [Tooltip("Çıtırtının duyulduğu mesafe (dünya birimi)")]
        [SerializeField] private float hearRadius = 4.5f;
        [Tooltip("⚡ simgesinin göründüğü mesafe (dünya birimi)")]
        [SerializeField] private float warnRadius = 6f;
        [Tooltip("İlk yaklaşımda üst şeridin çıktığı mesafe (dünya birimi)")]
        [SerializeField] private float toastRadius = 3.4f;
        [SerializeField] private bool logEvents = true;

        private class Z
        {
            public Zone def; public Vector2 contact; public SpriteRenderer spark, icon; public SpriteRenderer[] arcs; public float[] arcT;
            public Light2D glow; public bool active; public float sparkT, burstT, flashT, cooldown, lastShock; public int frame; public bool sparkOn = true; public int repeat;
        }
        private readonly List<Z> list = new List<Z>();
        private IsoPlayerController player;
        private FloodEveningHud hud;
        private float lastVol = -1f;
        private bool toastNear, toastShock;

        public int ShockCount { get; private set; }
        public int ZoneCount => list.Count;
        public bool IsActive(int i) => i >= 0 && i < list.Count && list[i].active;
        public Vector2 ContactOf(int i) => i >= 0 && i < list.Count ? list[i].contact : Vector2.zero;

        private void Awake()
        {
            if (water == null) water = FindAnyObjectByType<FloodWaterField>(FindObjectsInactive.Include);
            if (reaction == null) reaction = FindAnyObjectByType<FloodPlayerReaction>(FindObjectsInactive.Include);
            player = reaction != null && reaction.Player != null ? reaction.Player : FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            hud = FindAnyObjectByType<FloodEveningHud>(FindObjectsInactive.Include);
            if (glowTemplate == null)
                foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include)) if (l.lightType == Light2D.LightType.Point) { glowTemplate = l; break; }
            var playerSr = player != null ? player.GetComponentInChildren<SpriteRenderer>(true) : null;
            int layer = playerSr != null ? playerSr.sortingLayerID : 0;
            foreach (var zd in zones)
            {
                if (zd == null || zd.contact == null) continue;
                // editör betiğiyle (arraySize++) eklenen öğelerde alan varsayılanları 0 gelir → güvenli varsayılanlar
                if (zd.coreRadius <= 0f) zd.coreRadius = 0.3f;
                if (zd.waterRadius <= 0f) zd.waterRadius = 0.72f;
                var root = new GameObject("Electric_" + zd.id).transform;
                root.SetParent(transform, false);
                var z = new Z { def = zd, contact = zd.contact.position, arcs = new SpriteRenderer[3], arcT = new float[3] };
                z.spark = MakeSprite("Spark", root, layer, 0, z.contact + new Vector2(0f, 0.002f));
                z.spark.spriteSortPoint = SpriteSortPoint.Pivot;
                z.icon = MakeSprite("Warn", root, layer, 90, z.contact + new Vector2(0f, 1.05f));
                z.icon.sprite = warnIcon; z.icon.enabled = false;
                for (int i = 0; i < z.arcs.Length; i++) { z.arcs[i] = MakeSprite("Arc" + i, root, layer, -87, z.contact); z.arcs[i].enabled = false; }
                if (glowTemplate != null)
                {
                    z.glow = Instantiate(glowTemplate, root);
                    z.glow.name = "Glow";
                    z.glow.transform.position = z.contact + new Vector2(0f, 0.05f);
                    z.glow.color = new Color(0.55f, 0.85f, 1f, 1f);
                    z.glow.pointLightInnerRadius = 0.05f;
                    z.glow.pointLightOuterRadius = 1.1f;
                    z.glow.intensity = 0.3f;
                }
                list.Add(z);
            }
        }

        private static SpriteRenderer MakeSprite(string name, Transform parent, int layer, int order, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(Mathf.Round(pos.x * 64f) / 64f, Mathf.Round(pos.y * 64f) / 64f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = layer; sr.sortingOrder = order;
            return sr;
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            GameAudio.StopLoop(crackleChannel, 0.3f);
            lastVol = -1f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector2 feet = player != null ? (Vector2)player.transform.position : new Vector2(9999f, 9999f);
            bool started = FloodEveningHub.FloodStarted, stopped = FloodEveningHub.FloodStopped;
            float nearest = float.MaxValue; bool nearestActive = false;
            foreach (var z in list)
            {
                z.cooldown -= dt;
                z.active = started && water != null && water.DepthAt(z.contact) > activeDepth;
                Animate(z, dt, feet);
                float d = Vector2.Distance(feet, z.contact);
                if (d < nearest) { nearest = d; nearestActive = z.active; }
                if (!toastNear && d < toastRadius && started && FloodEveningHub.FreeRoam && hud != null)
                {
                    toastNear = true;
                    hud.ShowToast("ELEKTRİK TEHLİKESİ", "Sarkan kabloya ve yakınındaki suya yaklaşma. Uzaktan dolaş.", FloodEveningHud.ToastKind.Danger, 3.5f);
                }
                if (stopped || z.cooldown > 0f || reaction == null || !reaction.CanReact) continue;
                float g = FloodPlayerReaction.GroundDistance(feet, z.contact);
                if (g < z.def.coreRadius || (z.active && g < z.def.waterRadius)) Shock(z, feet);
            }
            UpdateAudio(nearest, nearestActive);
        }

        private void Animate(Z z, float dt, Vector2 feet)
        {
            // kıvılcım: kısa patlamalar halinde (yaklaşık %70 açık), 12 kare/sn
            z.burstT -= dt;
            if (z.burstT <= 0f) { z.sparkOn = !z.sparkOn || UnityEngine.Random.value < 0.6f; z.burstT = z.sparkOn ? UnityEngine.Random.Range(0.25f, 0.9f) : UnityEngine.Random.Range(0.12f, 0.45f); }
            z.sparkT += dt;
            if (z.sparkT >= 1f / 12f) { z.sparkT = 0f; z.frame = (z.frame + 1 + UnityEngine.Random.Range(0, 2)) % Mathf.Max(1, sparkFrames.Length); }
            z.spark.enabled = z.sparkOn && sparkFrames.Length > 0;
            if (z.spark.enabled) { z.spark.sprite = sparkFrames[z.frame]; z.spark.flipX = (z.frame & 1) == 1; }
            // parıltı: kıvılcımla titrer, çarpılmada kısa süre güçlenir (kontrollü)
            z.flashT -= dt;
            if (z.glow != null)
            {
                float baseI = z.sparkOn ? UnityEngine.Random.Range(0.35f, 0.7f) : 0.12f;
                z.glow.intensity = Mathf.MoveTowards(z.glow.intensity, baseI * (z.active ? 1.15f : 0.85f) + (z.flashT > 0f ? 0.6f : 0f), dt * 8f);
                z.glow.pointLightOuterRadius = z.active ? 1.35f : 0.95f;
            }
            // su yüzeyinde kısa arklar (yalnız su varken, yalnız suyun olduğu noktalarda)
            for (int i = 0; i < z.arcs.Length; i++)
            {
                var a = z.arcs[i];
                z.arcT[i] -= dt;
                if (z.arcT[i] > 0f) continue;
                if (a.enabled || !z.active || arcFrames.Length == 0) { a.enabled = false; z.arcT[i] = UnityEngine.Random.Range(0.15f, 0.45f); continue; }
                float ang = UnityEngine.Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(UnityEngine.Random.value) * z.def.waterRadius * 0.85f;
                Vector2 p = z.contact + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * 0.5f);
                if (water != null && water.DepthAt(p) < 0.01f) { z.arcT[i] = 0.1f; continue; }
                a.transform.position = new Vector3(Mathf.Round(p.x * 64f) / 64f, Mathf.Round(p.y * 64f) / 64f, 0f);
                a.sprite = arcFrames[UnityEngine.Random.Range(0, arcFrames.Length)];
                a.flipX = UnityEngine.Random.value < 0.5f;
                a.enabled = true;
                z.arcT[i] = UnityEngine.Random.Range(0.07f, 0.14f);
            }
            // ⚡ uyarı simgesi: yakınken yanıp söner (0,45 sn açık / 0,3 sn kapalı)
            bool near = Vector2.Distance(feet, z.contact) < warnRadius && FloodEveningHub.FloodStarted;
            z.icon.enabled = near && warnIcon != null && Mathf.Repeat(Time.time, 0.75f) < 0.45f;
        }

        private void Shock(Z z, Vector2 feet)
        {
            // güvenli nokta: oyuncunun bölgeye göre bulunduğu tarafa en uygun olan (aynı yöndeki); art arda çarpılmada en yakını
            Vector2 radial = feet - z.contact; if (radial.sqrMagnitude < 1e-6f) radial = Vector2.down;
            bool repeat = Time.time - z.lastShock < 3.5f;
            Transform best = null; float bestScore = float.MinValue;
            foreach (var s in z.def.safePoints)
            {
                if (s == null) continue;
                Vector2 to = (Vector2)s.position - z.contact;
                float score = repeat ? -FloodPlayerReaction.GroundDistance(s.position, feet)
                                     : Vector2.Dot(Ground(to), Ground(radial)) - 0.15f * FloodPlayerReaction.GroundDistance(s.position, feet);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            Vector2 dir = best != null ? (Vector2)best.position - feet : radial;
            Vector2? target = best != null ? (Vector2)best.position : (Vector2?)null;
            if (!reaction.React(FloodPlayerReaction.Kind.Shock, dir, knockSpeed, knockMax, shockLock, target)) return;
            z.cooldown = repeat ? shockCooldown * 2f : shockCooldown;
            z.lastShock = Time.time;
            z.flashT = 0.2f;
            ShockCount++;
            GameAudio.PlaySfx(shockSfx, 0.8f);
            // görev paneli uyarısı: ReportMistake → FloodEveningController.OnMistake → objectives.Notify (çift sayılmasın)
            FloodEveningHub.ReportMistake("power_shock");
            if (logEvents) Debug.Log($"[FE] elektrik çarptı: {z.def.id} (su {(z.active ? "var" : "yok")}, hedef {(best != null ? best.name : "-")}, toplam {ShockCount})", this);
            if (!toastShock && hud != null)
            {
                toastShock = true;
                hud.ShowToast("ELEKTRİK ÇARPTI!", "Sarkan kablonun değdiği su elektrik iletir. Hemen uzaklaş, başka yoldan dolaş.", FloodEveningHud.ToastKind.Danger, 3.5f);
            }
        }

        private static Vector2 Ground(Vector2 w) { var g = new Vector2(w.x, 2f * w.y); return g.sqrMagnitude > 1e-8f ? g.normalized : Vector2.zero; }

        private void UpdateAudio(float nearest, bool active)
        {
            float vol = 0f;
            if (FloodEveningHub.FloodStarted && nearest < hearRadius)
                vol = Mathf.Clamp01(1f - nearest / hearRadius) * (active ? 0.9f : 0.5f) * FloodEveningHub.OutdoorAudio01;
            vol = Mathf.Round(vol * 20f) / 20f;
            if (Mathf.Approximately(vol, lastVol)) return;
            lastVol = vol;
            if (vol <= 0.001f) GameAudio.StopLoop(crackleChannel, 0.4f);
            else GameAudio.PlayLoop(crackleLoop, crackleChannel, vol, 0.3f);
        }

        public void ResetModule()
        {
            foreach (var z in list) { z.cooldown = 0f; z.lastShock = -99f; z.flashT = 0f; }
        }
    }
}
