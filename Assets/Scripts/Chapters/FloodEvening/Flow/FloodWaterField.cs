using System;
using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Haritadaki gerçek zamanlı su (120 sn sel sayacı, LEAD). Tek kaynak FloodEveningHub.Progress01'dir; bu bileşen yalnız okur.
    /// - Su katmanı: haritayı kaplayan tek sprite + FE_FloodWater.shader. FE_FloodData (tools/sel_aksam_harita/flood.js) her noktanın
    ///   dolma eşiğini, akış yönünü/hızını ve maskesini taşır: kısa yol ve kanal kıyısı önce, uzun yol giderek, rüzgâr koridoru en son
    ///   dolar; avlu kuru kalır. Katman zemin (order -100) ile süs/duvar/karakter arasındadır (order -88), izometrik sıralama bozulmaz.
    /// - Oyuncu: ayağının altındaki derinliğe göre halka/sıçrama, suya gömülen ayak çizgisi ve derin suda hafif yavaşlama (yalnız serbest
    ///   yürüyüşte; rüzgâr koridoru gibi dış hız verilirken dokunmaz).
    /// - Sürüklenen enkaz (yaprak, tahta, şişe, köpük) derin ve akan suda, kamera çevresinde havuzlanmış sprite'larla.
    /// - Yol kapanmaları: önceden tasarlanmış kapılar (kısa yol girişi, geri dönüş sokağı) eşikte bariyer + collider ile kapanır;
    ///   eşikten biraz önce bariyer yanıp söner. Oyuncu kapının arkasında (o bölgede) kalacaksa kapı KAPANMAZ — sel sistemi hedefe
    ///   giden yolu asla kesmez. Kapanmada oyuncu yakınsa üst şeritte uyarı.
    /// - Ses: akan su döngüsü ilerlemeyle ve oyuncunun yanındaki suyla yükselir (OutdoorAudio01 ile kısılır).
    /// - Test (Editor / Development): F11 su katmanını aç/kapa; sahne görünümünde kapılar ve yasak bölgeler çizilir.
    /// </summary>
    public class FloodWaterField : MonoBehaviour, IFloodEveningResettable
    {
        [Serializable]
        public class Closure
        {
            public string id = "short_road";
            [Tooltip("Kapı dikdörtgeni (ızgara: gx0, gy0, gx1, gy1) — collider bu alana konur")]
            public Vector4 gate;
            [Tooltip("Bariyer sıralarının ızgara konumu: kapının uzun kenarı boyunca bu hizada birkaç bariyer dizilir")]
            public Vector2 barrierAt;
            [Range(0f, 1f)] public float threshold = 0.5f;
            [Tooltip("Oyuncu bu dikdörtgenlerden birindeyse kapanma ertelenir (ızgara: gx0, gy0, gx1, gy1)")]
            public Vector4[] forbidIfPlayerIn = Array.Empty<Vector4>();
            public string toastTitle = "YOL SU ALTINDA";
            public string toastBody = "Bu sokak suyla kapandı. Başka yoldan ilerle.";
            [NonSerialized] public bool closed;
            [NonSerialized] public GameObject root;
            [NonSerialized] public SpriteRenderer barrier;
            [NonSerialized] public readonly List<SpriteRenderer> barriers = new List<SpriteRenderer>();
        }

        [Header("Su katmanı")]
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Texture2D floodData;
        [Tooltip("Izgara sınırları (gx0, gy0, W, H) — flood.js çıktısıyla aynı")]
        [SerializeField] private Vector4 grid = new Vector4(-26f, -16f, 106f, 60f);
        [Tooltip("Haritanın kök objesi (EveningNeighborhood); boşsa dünya orijini")]
        [SerializeField] private Transform mapRoot;
        [SerializeField] private int sortingOrder = -88;
        [Tooltip("Tam derinlik (shader _DeepAt ile aynı)")]
        [SerializeField] private float deepAt = 0.32f;

        [Header("Oyuncu")]
        [SerializeField] private IsoPlayerController player;
        [SerializeField] private Sprite[] rippleFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] splashFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite feetWater;
        [Tooltip("Bu derinlikten sonra yavaşlama (0.85) ve daha derinde (0.72)")]
        [SerializeField] private Vector2 slowDepths = new Vector2(0.22f, 0.4f);

        [Header("Enkaz")]
        [SerializeField] private Sprite[] debrisSprites = Array.Empty<Sprite>();
        [SerializeField] private int maxDebris = 16;

        [Header("Yol kapanmaları")]
        [SerializeField] private Sprite barrierSprite;
        [SerializeField] private Closure[] closures = Array.Empty<Closure>();
        [SerializeField] private float closureToastRadius = 7f;

        [Header("Ses")]
        [SerializeField] private string waterLoop = "AMB_Flood_Water_Loop";
        [SerializeField] private string waterChannel = "flood_water";

        private SpriteRenderer waterRenderer;
        private Material runtimeMat;
        private Color[] data;
        private int dataW, dataH;
        private float progress, lastSoundVol = -1f;
        private SpriteRenderer feetRenderer, playerRenderer;
        private readonly List<SpriteRenderer> ripples = new List<SpriteRenderer>();
        private readonly List<float> rippleAge = new List<float>();
        private float rippleTimer;
        private bool wasDeep, slowing;
        private class Debris { public SpriteRenderer sr; public Vector2 v; public float life, age, spin; }
        private readonly List<Debris> debris = new List<Debris>();
        private float debrisAcc;
        private FloodEveningHud hud;
        private Camera cam;
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int RainId = Shader.PropertyToID("_RainAmount");
        private static readonly int RootId = Shader.PropertyToID("_Root");
        private static readonly int GridId = Shader.PropertyToID("_Grid");

        /// <summary>Oyuncunun ayağındaki su derinliği (0 = kuru; ~0.32 = derin). Testler ve diğer modüller için.</summary>
        public float PlayerDepth { get; private set; }
        public bool IsClosed(string id) { foreach (var c in closures) if (c.id == id) return c.closed; return false; }
        public bool WaterVisible => waterRenderer != null && waterRenderer.enabled;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (player != null) playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);
            hud = FindAnyObjectByType<FloodEveningHud>(FindObjectsInactive.Include);
            ReadData();
            BuildWater();
            BuildPlayerFx();
            for (int i = 0; i < maxDebris; i++)
            {
                var sr = NewSprite("Debris" + i, transform, sortingOrder + 1);
                debris.Add(new Debris { sr = sr });
            }
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            if (slowing && player != null) player.SpeedMultiplier = 1f;
            slowing = false;
            GameAudio.StopLoop(waterChannel, 0.5f);
            lastSoundVol = -1f;
        }

        private void OnDestroy()
        {
            // çalışma anında üretilen malzeme/doku/sprite sahneyle silinmez (Tekrar Dene'de birikmesin)
            if (runtimeMat != null) Destroy(runtimeMat);
            if (waterRenderer != null && waterRenderer.sprite != null) { Destroy(waterRenderer.sprite.texture); Destroy(waterRenderer.sprite); }
        }

        private void ReadData()
        {
            if (floodData == null) { Debug.LogWarning("[FloodWaterField] FE_FloodData bağlı değil; su katmanı kapalı.", this); return; }
            try
            {
                data = floodData.GetPixels();
                dataW = floodData.width; dataH = floodData.height;
            }
            catch (Exception e) { Debug.LogWarning("[FloodWaterField] FE_FloodData okunamadı (Read/Write açık mı?): " + e.Message, this); data = null; }
        }

        private void BuildWater()
        {
            if (waterMaterial == null || floodData == null) return;
            runtimeMat = new Material(waterMaterial) { name = "FE_FloodWater (runtime)" };
            runtimeMat.SetTexture("_FloodData", floodData);
            runtimeMat.SetVector(GridId, grid);
            Vector2 root = mapRoot != null ? (Vector2)mapRoot.position : Vector2.zero;
            runtimeMat.SetVector(RootId, new Vector4(root.x, root.y, 0f, 0f));
            runtimeMat.SetFloat("_DeepAt", deepAt);

            // Izgara sınırlarının dünya karşılığını kaplayan tek sprite (4x4 beyaz doku, ölçeklenir)
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "FloodWaterQuad" };
            var white = new Color32[16]; for (int i = 0; i < 16; i++) white[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(white); tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            Vector2 a = GridToWorld(grid.x, grid.y), b = GridToWorld(grid.x + grid.z, grid.y), c = GridToWorld(grid.x, grid.y + grid.w), d = GridToWorld(grid.x + grid.z, grid.y + grid.w);
            Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d)), max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            waterRenderer = NewSprite("FloodWater", transform, sortingOrder);
            waterRenderer.sprite = sprite;
            waterRenderer.sharedMaterial = runtimeMat;
            waterRenderer.transform.position = (Vector3)((min + max) * 0.5f);
            waterRenderer.transform.localScale = new Vector3(max.x - min.x, max.y - min.y, 1f);
            waterRenderer.enabled = true;
            Apply(0f);
        }

        private void BuildPlayerFx()
        {
            for (int i = 0; i < 4; i++) { ripples.Add(NewSprite("Ripple" + i, transform, sortingOrder + 1)); rippleAge.Add(99f); }
            feetRenderer = NewSprite("FeetWater", transform, 0);
            feetRenderer.sprite = feetWater;
            feetRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            if (playerRenderer != null) { feetRenderer.sortingLayerID = playerRenderer.sortingLayerID; feetRenderer.sortingOrder = playerRenderer.sortingOrder; }
        }

        private SpriteRenderer NewSprite(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (playerRenderer != null) sr.sortingLayerID = playerRenderer.sortingLayerID;
            sr.enabled = false;
            return sr;
        }

        // =====================================================================

        private void Update()
        {
            if (GameInput.DevKeyPressed(UnityEngine.InputSystem.Key.F11) && waterRenderer != null) waterRenderer.enabled = !waterRenderer.enabled;
            float p = FloodEveningHub.Progress01;
            if (!Mathf.Approximately(p, progress)) Apply(p);
            if (cam == null) cam = Camera.main;
            UpdatePlayer(Time.deltaTime);
            UpdateDebris(Time.deltaTime);
            UpdateClosures();
            UpdateSound();
        }

        private void Apply(float p)
        {
            progress = Mathf.Clamp01(p);
            if (runtimeMat == null) return;
            runtimeMat.SetFloat(ProgressId, progress);
            runtimeMat.SetFloat(RainId, Mathf.Lerp(0.15f, 1f, progress));
        }

        /// <summary>Dünya noktasındaki su derinliği (≤ 0 kuru / ıslak).</summary>
        public float DepthAt(Vector2 world)
        {
            if (data == null) return -1f;
            Vector2 g = WorldToGrid(world);
            float u = (g.x - grid.x) / grid.z, v = (g.y - grid.y) / grid.w;
            if (u <= 0f || v <= 0f || u >= 1f || v >= 1f) return -1f;
            float fx = u * dataW - 0.5f, fy = v * dataH - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, dataW - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, dataH - 1);
            int x1 = Mathf.Min(dataW - 1, x0 + 1), y1 = Mathf.Min(dataH - 1, y0 + 1);
            float tx = Mathf.Clamp01(fx - x0), ty = Mathf.Clamp01(fy - y0);
            Color c = Color.Lerp(Color.Lerp(data[y0 * dataW + x0], data[y0 * dataW + x1], tx), Color.Lerp(data[y1 * dataW + x0], data[y1 * dataW + x1], tx), ty);
            if (c.a < 0.5f) return -1f;
            float t = c.r > 0.995f ? 2f : c.r * (255f / 254f);
            return progress - t;
        }

        private Vector2 FlowAt(Vector2 world, out float speed)
        {
            speed = 0f;
            if (data == null) return Vector2.zero;
            Vector2 g = WorldToGrid(world);
            int x = Mathf.Clamp(Mathf.FloorToInt((g.x - grid.x) / grid.z * dataW), 0, dataW - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((g.y - grid.y) / grid.w * dataH), 0, dataH - 1);
            Color c = data[y * dataW + x];
            float ang = c.g * Mathf.PI * 2f;
            speed = c.b;
            Vector2 dg = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            return new Vector2((dg.x - dg.y) * 0.5f, (dg.x + dg.y) * 0.25f).normalized;
        }

        private Vector2 WorldToGrid(Vector2 w)
        {
            Vector2 r = w - (mapRoot != null ? (Vector2)mapRoot.position : Vector2.zero);
            return new Vector2(r.x + 2f * r.y, 2f * r.y - r.x);
        }

        private Vector2 GridToWorld(float gx, float gy)
        {
            Vector2 root = mapRoot != null ? (Vector2)mapRoot.position : Vector2.zero;
            return root + new Vector2((gx - gy) * 0.5f, (gx + gy) * 0.25f);
        }

        // ---------------------------------------------------------------- oyuncu
        private void UpdatePlayer(float dt)
        {
            if (player == null) return;
            Vector2 feet = player.transform.position;
            float depth = DepthAt(feet);
            PlayerDepth = Mathf.Max(0f, depth);
            bool inWater = depth > 0.015f;
            bool moving = player.Velocity.sqrMagnitude > 0.04f;

            // suya gömülen ayak çizgisi (oyuncunun hemen önünde y-sıralanır)
            bool showFeet = depth > 0.1f && feetWater != null;
            feetRenderer.enabled = showFeet;
            if (showFeet)
            {
                feetRenderer.transform.position = new Vector3(feet.x, feet.y - 0.012f, 0f);
                var c = feetRenderer.color; c.a = Mathf.Clamp01((depth - 0.1f) / 0.12f); feetRenderer.color = c;
                if (playerRenderer != null) { feetRenderer.sortingLayerID = playerRenderer.sortingLayerID; feetRenderer.sortingOrder = playerRenderer.sortingOrder; }
            }

            // halkalar: suda yürürken sık, dururken seyrek
            rippleTimer -= dt;
            if (inWater && rippleFrames.Length > 0 && rippleTimer <= 0f)
            {
                rippleTimer = moving ? 0.28f : 0.9f;
                int idx = 0; // en eski halka yeniden kullanılır (dizi kopyası yok: kare başına çöp üretmesin)
                for (int i = 1; i < rippleAge.Count; i++) if (rippleAge[i] > rippleAge[idx]) idx = i;
                rippleAge[idx] = 0f;
                ripples[idx].transform.position = new Vector3(feet.x, feet.y, 0f);
            }
            for (int i = 0; i < ripples.Count; i++)
            {
                rippleAge[i] += dt;
                float k = rippleAge[i] / 0.6f;
                var sr = ripples[i];
                if (k >= 1f || rippleFrames.Length == 0) { sr.enabled = false; continue; }
                sr.enabled = true;
                sr.sprite = rippleFrames[Mathf.Min(rippleFrames.Length - 1, Mathf.FloorToInt(k * rippleFrames.Length))];
            }

            // derin suya girişte sıçrama sesi (bir kez)
            bool deep = depth > slowDepths.x;
            if (deep && !wasDeep && moving) GameAudio.PlaySfx("SFX_Water_Splash", 0.45f);
            wasDeep = deep;

            // derin suda hafif yavaşlama: yalnız serbest yürüyüşte ve dış hız yokken
            bool canSlow = FloodEveningHub.FreeRoam && player.CanMove && player.ExternalVelocity.sqrMagnitude < 0.0001f && !FloodEveningHub.FloodStopped;
            float mul = !canSlow ? 1f : depth > slowDepths.y ? 0.72f : depth > slowDepths.x ? 0.85f : 1f;
            if (canSlow && (mul < 1f || slowing)) { player.SpeedMultiplier = mul; slowing = mul < 1f; }
            else if (slowing && !canSlow) { player.SpeedMultiplier = 1f; slowing = false; }
        }

        // ---------------------------------------------------------------- enkaz
        private void UpdateDebris(float dt)
        {
            if (cam == null || debrisSprites.Length == 0) return;
            float rate = progress < 0.35f ? 0f : Mathf.Lerp(1f, 7f, (progress - 0.35f) / 0.65f);
            debrisAcc += rate * dt;
            Vector2 c = cam.transform.position;
            float hh = cam.orthographic ? cam.orthographicSize : 3f, hw = hh * cam.aspect;
            int tries = 0;
            while (debrisAcc >= 1f && tries++ < 6)
            {
                debrisAcc -= 1f;
                Vector2 pos = c + new Vector2(UnityEngine.Random.Range(-hw, hw), UnityEngine.Random.Range(-hh, hh));
                if (DepthAt(pos) < 0.16f) continue;
                Debris d = null; foreach (var x in debris) if (x.life <= 0f) { d = x; break; }
                if (d == null) break;
                Vector2 dir = FlowAt(pos, out float spd);
                d.sr.sprite = debrisSprites[UnityEngine.Random.Range(0, debrisSprites.Length)];
                d.sr.transform.position = pos;
                d.sr.flipX = UnityEngine.Random.value < 0.5f;
                d.v = dir * Mathf.Lerp(0.15f, 0.7f, spd) * (0.6f + progress);
                d.life = UnityEngine.Random.Range(3f, 6f); d.age = 0f;
                d.sr.enabled = true;
            }
            foreach (var d in debris)
            {
                if (d.life <= 0f) continue;
                d.age += dt;
                Vector2 p = d.sr.transform.position;
                if (d.age >= d.life || DepthAt(p) < 0.06f) { d.life = 0f; d.sr.enabled = false; continue; }
                p += d.v * dt + new Vector2(0f, Mathf.Sin(d.age * 3f) * 0.002f);
                d.sr.transform.position = new Vector3(Mathf.Round(p.x * 64f) / 64f, Mathf.Round(p.y * 64f) / 64f, 0f);
                float k = d.age / d.life;
                var col = d.sr.color; col.a = Mathf.Clamp01(Mathf.Min(k * 5f, (1f - k) * 4f)); d.sr.color = col;
            }
        }

        // ---------------------------------------------------------------- yol kapanmaları
        private void UpdateClosures()
        {
            if (player == null) return;
            Vector2 pg = WorldToGrid(player.transform.position);
            foreach (var c in closures)
            {
                if (c.closed) continue;
                // sayaç durduysa (güvenli bölge / süre doldu) kapanmayacak kapının bariyeri yanıp sönmeye devam etmesin
                bool warn = progress >= c.threshold - 0.05f && !FloodEveningHub.FloodStopped;
                if (warn && c.root == null) BuildClosure(c);
                if (c.barriers.Count > 0)
                {
                    float a = warn ? 0.35f + 0.25f * Mathf.Sin(Time.time * 6f) : 0f;
                    foreach (var b in c.barriers) { var col = b.color; col.a = a; b.color = col; }
                }
                if (progress < c.threshold || FloodEveningHub.FloodStopped) continue;
                bool blocked = false;
                foreach (var r in c.forbidIfPlayerIn) if (pg.x >= r.x && pg.x < r.z && pg.y >= r.y && pg.y < r.w) { blocked = true; break; }
                if (blocked) continue; // oyuncu o tarafta: kapanmayı ertele (hedefe giden yol kesilmez)
                Close(c);
            }
        }

        private void BuildClosure(Closure c)
        {
            c.root = new GameObject("Closure_" + c.id);
            c.root.transform.SetParent(transform, false);
            c.barriers.Clear();
            if (barrierSprite == null) return;
            // Kapı sokağın bütün genişliğini keser: tek bariyer "görünmez duvar" gibi duruyordu → uzun kenar boyunca bir sıra bariyer
            // (ortadaki barrierAt'ta; aralık ~1.6 hücre, kenarlardan 0.6 hücre içeride).
            Vector4 r = c.gate;
            bool alongX = (r.z - r.x) >= (r.w - r.y);
            float from = (alongX ? r.x : r.y) + 0.6f, to = (alongX ? r.z : r.w) - 0.6f;
            int n = Mathf.Max(1, Mathf.RoundToInt((to - from) / 1.6f) + 1);
            for (int i = 0; i < n; i++)
            {
                float k = n == 1 ? (alongX ? c.barrierAt.x : c.barrierAt.y) : Mathf.Lerp(from, to, i / (float)(n - 1));
                var go = new GameObject("Barrier" + i);
                go.transform.SetParent(c.root.transform, false);
                go.transform.position = alongX ? GridToWorld(k, c.barrierAt.y) : GridToWorld(c.barrierAt.x, k);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = barrierSprite;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                if (playerRenderer != null) { sr.sortingLayerID = playerRenderer.sortingLayerID; sr.sortingOrder = playerRenderer.sortingOrder; }
                sr.color = new Color(1f, 1f, 1f, 0f);
                c.barriers.Add(sr);
            }
            c.barrier = c.barriers[n / 2];
        }

        private void Close(Closure c)
        {
            if (c.root == null) BuildClosure(c);
            c.closed = true;
            foreach (var b in c.barriers) b.color = Color.white;
            var col = c.root.AddComponent<PolygonCollider2D>();
            Vector4 r = c.gate;
            col.SetPath(0, new[] { GridToWorld(r.x, r.y), GridToWorld(r.z, r.y), GridToWorld(r.z, r.w), GridToWorld(r.x, r.w) });
            Debug.Log($"[FE] yol kapandı: {c.id} (ilerleme {progress:0.00})", this);
            if (hud != null && player != null && Vector2.Distance(player.transform.position, c.barrier != null ? (Vector2)c.barrier.transform.position : GridToWorld(c.barrierAt.x, c.barrierAt.y)) <= closureToastRadius)
                hud.ShowToast(c.toastTitle, c.toastBody, FloodEveningHud.ToastKind.Danger, 3f);
        }

        // ---------------------------------------------------------------- ses
        private void UpdateSound()
        {
            if (!FloodEveningHub.FloodStarted) return;
            float near = Mathf.Clamp01(PlayerDepth / deepAt);
            float vol = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, (progress - 0.15f) / 0.85f) * 0.8f + near * 0.35f) * FloodEveningHub.OutdoorAudio01;
            vol = Mathf.Round(vol * 20f) / 20f;
            if (Mathf.Approximately(vol, lastSoundVol)) return;
            lastSoundVol = vol;
            if (vol <= 0.001f) GameAudio.StopLoop(waterChannel, 0.6f);
            else GameAudio.PlayLoop(waterLoop, waterChannel, vol, 0.8f);
        }

        public void ResetModule()
        {
            if (FloodEveningHub.FloodStopped) return; // süre doldu / güvenli bölge: görüntü kritik seviyede kalsın (Tekrar Dene sahneyi baştan kurar)
            foreach (var c in closures)
            {
                c.closed = false;
                if (c.root != null) Destroy(c.root);
                c.root = null; c.barrier = null; c.barriers.Clear();
            }
            foreach (var d in debris) { d.life = 0f; if (d.sr != null) d.sr.enabled = false; }
            if (slowing && player != null) player.SpeedMultiplier = 1f;
            slowing = false;
            Apply(FloodEveningHub.Progress01);
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var c in closures)
            {
                Gizmos.color = c.closed ? Color.red : new Color(1f, 0.6f, 0.1f);
                DrawRect(c.gate);
                Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
                foreach (var r in c.forbidIfPlayerIn) DrawRect(r);
            }
        }

        private void DrawRect(Vector4 r)
        {
            Vector3 a = GridToWorld(r.x, r.y), b = GridToWorld(r.z, r.y), c = GridToWorld(r.z, r.w), d = GridToWorld(r.x, r.w);
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }
    }
}
