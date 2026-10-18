using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam selinde sürekli esen fırtına rüzgârı (yalnız görsel; oyuncuyu durdurmaz, yavaşlatmaz, itmez). Kamera görüşünde
    /// (isteğe bağlı bir alanla sınırlı) çalışır:
    /// Sürekli taban rüzgârı + akışla taşınan türbülans; aralıklı sert bastırmalar (gust): önce kısa bir durgunluk, sonra
    /// ani yükseliş, titreyen tepe, yavaş sönüş; bazen art arda iki bastırma. Bastırma cephesi rüzgâr yönünde ilerler:
    /// rüzgâr üstündeki objeler önce eğilir, ötekiler sonra (frontSpeed). Varsayılan yön izometrik eksen (2:1).
    /// Görsel katmanlar (pixel art): rüzgâr çizgileri kamerayı izleyen tek bir dokuya, cellPixels büyüklüğünde
    /// kare hücrelerle çizilir (basamaklı kalın çizgiler, ucu spiral kıvrılan "rüzgâr" çizgileri; kuyruk seyrek
    /// hücrelerle söner), dünya ızgarasına kilitli olduğu için kayarken titremez. Bastırmada çizgiler çoğalır, uzar,
    /// daha çok kıvrılır; eğik yağmur perdesi de aynı dokuya çizilir. Yerde yatan blok yaprak/kâğıt/poşet/dal: hafif
    /// rüzgârda yerde kayar, bastırmada havaya kalkar, takla atarak (90° adımlarla döner, yarım boy çevrilir) savrulur,
    /// yere seker ve konar; bastırma cephesinde yerden kalkan su zerresi; sallanan objeler (ağaç, çalı) yay-sönümle
    /// eğilip geri esner; sert bastırmada kamerada çok hafif titreme.
    /// Yön Inspector'dan ya da bağlıysa WindGustZone.WindDirection'dan gelir. Diğer sistemler StrengthAt/Gust01 okuyabilir.
    /// Fizik motoru ve ParticleSystem yok, kare hızından bağımsız.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class FloodWindAmbience : MonoBehaviour, IFloodEveningResettable
    {
        [Header("Alan")]
        [Tooltip("Boşsa Camera.main. Efektler bu kameranın görüşünde yaşar.")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("İsteğe bağlı: efektler yalnız bu alanın sınırları içinde (görüşle kesişimi).")]
        [SerializeField] private Collider2D area;
        [Tooltip("Görüşün dışına taşan pay (dünya birimi): savrulan şeyler ekrana dışarıdan girer.")]
        [SerializeField, Min(0f)] private float viewMargin = 1.2f;

        [Header("Rüzgâr")]
        [Tooltip("Ekran yönü (x sağ, y yukarı). (-1, -0.5) = izometrik eksen boyunca sola-aşağı.")]
        [SerializeField] private Vector2 direction = new Vector2(-1f, -0.5f);
        [Tooltip("Bağlıysa yön her kare bu bölgenin WindDirection'ından alınır.")]
        [SerializeField] private WindGustZone followZone;
        [SerializeField, Range(0f, 1f)] private float baseStrength = 0.4f;
        [Tooltip("Taban rüzgârındaki dalgalanma oranı.")]
        [SerializeField, Range(0f, 1f)] private float turbulence = 0.4f;
        [Tooltip("Bölüm başında (Progress01 = 0) gücün oranı; ilerledikçe 1'e çıkar.")]
        [SerializeField, Range(0f, 1f)] private float strengthAtStart = 0.85f;

        [Header("Sert bastırmalar")]
        [SerializeField] private Vector2 gustInterval = new Vector2(3.5f, 7f);
        [SerializeField] private Vector2 gustPeak = new Vector2(0.65f, 1.05f);
        [Tooltip("Yükseliş süresi (kısa = ani bastırma).")]
        [SerializeField, Min(0.05f)] private float gustRise = 0.4f;
        [SerializeField] private Vector2 gustHold = new Vector2(0.5f, 1.4f);
        [SerializeField, Min(0.1f)] private float gustFall = 1.7f;
        [Tooltip("Bastırmadan hemen önce taban rüzgârının düşme oranı (fırtına öncesi sessizlik).")]
        [SerializeField, Range(0f, 1f)] private float lullDepth = 0.6f;
        [SerializeField, Min(0f)] private float lullSeconds = 0.7f;
        [SerializeField, Range(0f, 1f)] private float doubleGustChance = 0.3f;
        [Tooltip("Bastırma cephesinin ilerleme hızı (dünya birimi/sn).")]
        [SerializeField, Min(0.5f)] private float frontSpeed = 7f;

        [Header("Rüzgâr çizgileri (pixel)")]
        [Tooltip("Bir çizgi hücresinin kenarı (sanat pikseli). 3 = kalın, blok çizgiler.")]
        [SerializeField, Range(1, 4)] private int cellPixels = 3;
        [SerializeField, Range(0, 60)] private int streakCount = 26;
        [SerializeField, Range(4, 40)] private int trailPoints = 26;
        [SerializeField] private float streakSpeed = 6.5f;
        [SerializeField] private Vector2 streakLife = new Vector2(0.9f, 1.7f);
        [SerializeField] private Color streakColor = new Color(0.74f, 0.87f, 0.98f, 0.85f);
        [Tooltip("Çizginin ucunun spiral kıvrılma olasılığı (sakin / bastırma tepesinde).")]
        [SerializeField, Range(0f, 1f)] private float curlChance = 0.18f;
        [SerializeField, Range(0f, 1f)] private float gustCurlChance = 0.45f;
        [SerializeField] private int streakSortingOrder = 900;
        [Tooltip("Çizgi katmanının malzemesi. Sprite-Unlit-Default önerilir: gece ışığı çizgileri asfalt rengine karartmasın.")]
        [SerializeField] private Material lineMaterial;

        [Header("Savrulan döküntü")]
        [SerializeField] private Sprite[] leafSprites = Array.Empty<Sprite>();
        [SerializeField] private Sprite paperSprite;
        [SerializeField] private Sprite bagSprite;
        [SerializeField] private Sprite twigSprite;
        [SerializeField] private Sprite shadowSprite;
        [SerializeField, Range(0, 80)] private int leafCount = 40;
        [SerializeField, Range(0, 12)] private int paperCount = 4;
        [SerializeField, Range(0, 4)] private int bagCount = 1;
        [SerializeField, Range(0, 20)] private int twigCount = 6;
        [Tooltip("Güç 1'deyken havadaki döküntünün taşınma hızı.")]
        [SerializeField] private float carrySpeed = 4.6f;
        [SerializeField] private int airSortingOrder = 870;
        [SerializeField] private int groundSortingOrder = -55;

        [Header("Sis ve yağmur perdesi")]
        [SerializeField] private Sprite mistSprite;
        [Tooltip("Bastırma cephesinde yerden kalkan su zerresi (saniyede, tepe güçte).")]
        [SerializeField, Range(0f, 40f)] private float mistRate = 14f;
        [SerializeField] private Color mistColor = new Color(0.8f, 0.87f, 0.96f, 0.3f);
        [Tooltip("Bastırmada savrulan eğik yağmur damlaları (saniyede, tepe güçte). Sahnede RainController varsa düşük tut.")]
        [SerializeField, Range(0f, 400f)] private float rainRate = 120f;
        [SerializeField] private Color rainColor = new Color(0.75f, 0.85f, 1f, 0.55f);
        [Tooltip("Bu güçten sonra sis ve yağmur perdesi başlar.")]
        [SerializeField, Range(0f, 1.5f)] private float sheetThreshold = 0.75f;

        [Header("Sallanan objeler")]
        [SerializeField] private SwayTarget[] swayTargets = Array.Empty<SwayTarget>();
        [Tooltip("Açılışta sahnede sprite adı bunlardan birini içeren objeler de sallanır (yalnız çalışma anında döndürülür; " +
                 "sahne dosyası değişmez). Pivotları dipte olmalı. Boş = kapalı.")]
        [SerializeField] private string[] autoSwayKeywords = { "Tree", "Pine", "Bush", "Hedge" };
        [Tooltip("Otomatik bulunan objelerde: uzun ağaç (≥120 px) / orta / alçak (çalı) eğilme açısı.")]
        [SerializeField] private Vector3 autoSwayAngles = new Vector3(2.6f, 3.6f, 1.6f);

        [Header("Kamera")]
        [Tooltip("Tepe bastırmada kameranın titreme genliği (piksel). 0 = kapalı.")]
        [SerializeField, Range(0f, 3f)] private float buffetPixels = 0.8f;

        [Serializable]
        public class SwayTarget
        {
            [Tooltip("Pivotu dibinde olmalı (dönme noktası).")]
            public Transform target;
            [Tooltip("Güç 1'deyken eğilme açısı (derece).")]
            [Range(0f, 30f)] public float maxAngle = 5f;
            [Tooltip("Doğal salınım frekansı (Hz): ince direk hızlı, kalın ağaç yavaş.")]
            [Range(0.2f, 6f)] public float frequency = 1.1f;
            [Tooltip("Hızlı titreşim payı (yapraklar, tabela kenarı).")]
            [Range(0f, 1f)] public float flutter = 0.25f;
            [NonSerialized] public float Angle, Vel;
            [NonSerialized] public Quaternion BaseRot;
            [NonSerialized] public bool Captured;
        }

        private const float Step = 1f / 60f;
        private const float Ppu = 64f;

        private struct Gust { public float Start, Peak, Hold, Seed; }

        private sealed class Streak
        {
            public Vector2[] Pts;
            public int Count;
            public float Age, Life, Sleep, Depth, AngOff, CurlAt, CurlDur, CurlSign, CurlAng, Seed;
            public bool Alive;
        }

        private enum Kind { Leaf, Paper, Bag, Twig }

        private sealed class Debris
        {
            public Kind Kind;
            public SpriteRenderer Sr, Shadow;
            public Vector2 Ground, Vel;
            public float H, VH, Angle, Spin, Flip, FlipRate, FlipY = 1f, LiftAt, Seed;
            public bool Air;
        }

        private sealed class Mist
        {
            public SpriteRenderer Sr;
            public Vector2 Pos, Vel;
            public float Age, Life, S0, S1;
        }

        private struct Drop { public Vector2 Pos, Vel; public float Age, Life, Len; }

        private readonly List<Gust> gusts = new List<Gust>();
        private readonly List<Streak> streaks = new List<Streak>();
        private readonly List<Debris> debris = new List<Debris>();
        private readonly List<Mist> mists = new List<Mist>();
        private readonly List<Drop> drops = new List<Drop>();
        private readonly Stack<SpriteRenderer> mistPool = new Stack<SpriteRenderer>();
        private Transform root;
        private Vector2 dir = Vector2.left, perp = Vector2.up;
        private float baseAngle, clock, nextGustAt, refAlong, stepAcc, mistAcc, rainAcc, master, seed;
        private Rect region;
        private bool hasRegion;
        private Vector3 buffetApplied;
        private Camera buffetCam;

        // Pixel çizgi dokusu
        private SpriteRenderer overlay;
        private Texture2D overlayTex;
        private Color32[] buf;
        private int bufW, bufH;
        private Vector2 bufOrigin;

        /// <summary>Görüş merkezindeki bastırma oranı (0 = taban rüzgârı, 1 = tepe bastırma).</summary>
        public float Gust01 { get; private set; }
        /// <summary>Şu anki rüzgâr yönü (ekran, normalize).</summary>
        public Vector2 Direction => dir;
        /// <summary>Bir bastırma görüş merkezine ulaşmaya başladığında.</summary>
        public event Action GustArrived;
        private bool gustWasHigh;

        private float Scale => Mathf.Lerp(strengthAtStart, 1f, Mathf.Clamp01(FloodEveningHub.Progress01));
        private float Cell => cellPixels / Ppu;

        private void Awake()
        {
            seed = UnityEngine.Random.Range(0f, 100f);
            UpdateDirection();
        }

        private void Start()
        {
            Build();
            CollectAutoSway();
            ResetModule();
        }

        private void OnEnable()
        {
            FloodEveningHub.ResetRequested += ResetModule;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            foreach (var s in swayTargets)
                if (s != null && s.target != null && s.Captured) s.target.localRotation = s.BaseRot;
        }

        private void OnDestroy()
        {
            if (overlayTex != null) Destroy(overlayTex);
        }

        public void ResetModule()
        {
            gusts.Clear();
            clock = 0f;
            nextGustAt = UnityEngine.Random.Range(1f, 2.5f);
            gustWasHigh = false;
            UpdateRegion();
            foreach (var s in streaks) { s.Alive = false; s.Count = 0; s.Sleep = UnityEngine.Random.Range(0f, 1.2f); }
            foreach (var d in debris) Respawn(d, true);
            for (int i = mists.Count - 1; i >= 0; i--) ReleaseMist(i);
            drops.Clear();
            foreach (var s in swayTargets)
            {
                if (s == null || s.target == null) continue;
                Capture(s);
                s.Angle = s.Vel = 0f;
                s.target.localRotation = s.BaseRot;
            }
        }

        // =====================================================================
        // Rüzgâr alanı
        // =====================================================================

        /// <summary>p noktasında (dünya) şu anki rüzgâr gücü; ~0.4 taban, ~1+ tepe bastırma.</summary>
        public float StrengthAt(Vector2 p)
        {
            float lt = clock - (Vector2.Dot(p, dir) - refAlong) / frontSpeed;
            float g = 0f;
            for (int i = 0; i < gusts.Count; i++) g += Envelope(gusts[i], lt);
            // Türbülans akışla taşınır: desen rüzgârla birlikte ilerler.
            float n = (Mathf.PerlinNoise(lt * 0.9f + seed, Vector2.Dot(p, perp) * 0.35f) - 0.5f) * 2f;
            return Mathf.Max(0f, baseStrength * (1f + n * turbulence) + g) * Scale;
        }

        private float Envelope(in Gust g, float lt)
        {
            float x = lt - g.Start;
            if (x < -lullSeconds || x > gustRise + g.Hold + gustFall) return 0f;
            if (x < 0f) return -lullDepth * baseStrength * Mathf.Sin((x + lullSeconds) / Mathf.Max(0.01f, lullSeconds) * Mathf.PI);
            if (x < gustRise)
            {
                float u = 1f - x / gustRise;
                return g.Peak * (1f - u * u);
            }
            x -= gustRise;
            if (x < g.Hold) return g.Peak * (1f + 0.14f * Mathf.Sin(x * 11f + g.Seed) * Mathf.Sin(x * 3.1f + g.Seed * 2f));
            return g.Peak * (1f - EventsUi.Smooth01((x - g.Hold) / gustFall));
        }

        private void ScheduleGusts()
        {
            float half = hasRegion ? (region.width + region.height) * 0.5f : 10f;
            float horizon = half / frontSpeed + lullSeconds + 0.5f;
            while (nextGustAt < clock + horizon)
            {
                var g = NewGust(nextGustAt, 1f);
                gusts.Add(g);
                float end = g.Start + gustRise + g.Hold;
                if (UnityEngine.Random.value < doubleGustChance)
                {
                    var g2 = NewGust(g.Start + gustRise + g.Hold * 0.7f + UnityEngine.Random.Range(0.3f, 0.7f), 0.8f);
                    gusts.Add(g2);
                    end = g2.Start + gustRise + g2.Hold;
                }
                nextGustAt = end + gustFall * 0.6f + UnityEngine.Random.Range(gustInterval.x, gustInterval.y);
            }
            for (int i = gusts.Count - 1; i >= 0; i--)
                if (gusts[i].Start + gustRise + gusts[i].Hold + gustFall < clock - horizon) gusts.RemoveAt(i);
        }

        private Gust NewGust(float start, float k) => new Gust
        {
            Start = start,
            Peak = UnityEngine.Random.Range(gustPeak.x, gustPeak.y) * k,
            Hold = UnityEngine.Random.Range(gustHold.x, gustHold.y),
            Seed = UnityEngine.Random.Range(0f, 50f),
        };

        private void UpdateDirection()
        {
            Vector2 d = followZone != null ? followZone.WindDirection : direction;
            if (d.sqrMagnitude < 0.0001f) d = Vector2.left;
            dir = d.normalized;
            perp = new Vector2(-dir.y, dir.x);
            baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        private Camera Cam => targetCamera != null ? targetCamera : Camera.main;

        private void UpdateRegion()
        {
            var cam = Cam;
            Rect r;
            if (cam != null && cam.orthographic)
            {
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector2 c = cam.transform.position;
                r = Rect.MinMaxRect(c.x - w - viewMargin, c.y - h - viewMargin, c.x + w + viewMargin, c.y + h + viewMargin);
            }
            else r = new Rect((Vector2)transform.position - new Vector2(6f, 4f), new Vector2(12f, 8f));
            if (area != null)
            {
                var b = area.bounds;
                r = Rect.MinMaxRect(Mathf.Max(r.xMin, b.min.x), Mathf.Max(r.yMin, b.min.y), Mathf.Min(r.xMax, b.max.x), Mathf.Min(r.yMax, b.max.y));
            }
            hasRegion = r.width > 0.5f && r.height > 0.5f && (area == null || area.enabled);
            region = r;
            refAlong = Vector2.Dot(r.center, dir);
        }

        // =====================================================================
        // Döngü
        // =====================================================================

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            clock += dt;
            UpdateDirection();
            UpdateRegion();
            ScheduleGusts();
            master = Mathf.MoveTowards(master, hasRegion ? 1f : 0f, dt * 2f);

            float sc = hasRegion ? StrengthAt(region.center) : 0f;
            float baseNow = baseStrength * Scale;
            Gust01 = Mathf.Clamp01((sc - baseNow) / Mathf.Max(0.01f, gustPeak.y * Scale));
            bool high = Gust01 > 0.35f;
            if (high && !gustWasHigh) GustArrived?.Invoke();
            gustWasHigh = high;

            stepAcc += dt;
            int guard = 0;
            while (stepAcc >= Step && guard++ < 8)
            {
                stepAcc -= Step;
                for (int i = 0; i < streaks.Count; i++) StepStreak(streaks[i]);
            }

            for (int i = 0; i < debris.Count; i++) UpdateDebris(debris[i], dt);
            SpawnSheets(dt);
            UpdateMist(dt);
            UpdateDrops(dt);
            for (int i = 0; i < swayTargets.Length; i++) UpdateSway(swayTargets[i], dt);
        }

        private void LateUpdate() => DrawOverlay();

        // =====================================================================
        // Rüzgâr çizgileri (simülasyon)
        // =====================================================================

        private void StepStreak(Streak st)
        {
            if (!st.Alive)
            {
                st.Sleep -= Step;
                if (st.Sleep <= 0f && hasRegion) Wake(st);
                return;
            }
            st.Age += Step;
            Vector2 head = st.Pts[0];
            if (st.Age >= st.Life || !region.Contains(head))
            {
                float s0 = StrengthAt(head);
                st.Alive = false;
                st.Count = 0;
                // Güçlü rüzgârda çizgiler sık doğar, sakinde seyrek.
                st.Sleep = UnityEngine.Random.Range(0.05f, 1.1f) / (0.2f + s0 * s0 * 2f);
                return;
            }
            float s = StrengthAt(head);
            float speed = streakSpeed * (0.3f + s) * Mathf.Lerp(0.75f, 1.15f, st.Depth);
            float wob = (Mathf.PerlinNoise(st.Seed, st.Age * 1.1f) - 0.5f) * 10f;
            if (st.CurlDur > 0f && st.Age > st.CurlAt)
            {
                float k = (st.Age - st.CurlAt) / st.CurlDur;
                if (k < 1f)
                {
                    // Spiral: dönerken yavaşlar, halka içe doğru daralır.
                    st.CurlAng += st.CurlSign * 470f / st.CurlDur * Step;
                    speed *= Mathf.Lerp(0.8f, 0.25f, k);
                }
                else speed = 0f; // spiral tamamlandı; kuyruk yetişip söner
            }
            float a = (baseAngle + st.AngOff + wob + st.CurlAng) * Mathf.Deg2Rad;
            head += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Step;
            int n = Mathf.Min(st.Count + 1, st.Pts.Length);
            for (int i = n - 1; i > 0; i--) st.Pts[i] = st.Pts[i - 1];
            st.Pts[0] = head;
            st.Count = n;
        }

        private void Wake(Streak st)
        {
            st.Alive = true;
            st.Age = 0f;
            st.Life = UnityEngine.Random.Range(streakLife.x, streakLife.y);
            st.Depth = UnityEngine.Random.value;
            st.AngOff = UnityEngine.Random.Range(-4f, 4f);
            st.Seed = UnityEngine.Random.Range(0f, 100f);
            st.CurlAng = 0f;
            st.CurlDur = 0f;
            if (UnityEngine.Random.value < curlChance + gustCurlChance * Gust01)
            {
                st.CurlAt = UnityEngine.Random.Range(0.35f, 0.6f) * st.Life;
                st.CurlDur = UnityEngine.Random.Range(0.45f, 0.7f);
                // Rüzgâr sola esiyorsa uç yukarı kıvrılsın (referans ikonlardaki gibi), bazen aşağı.
                st.CurlSign = (UnityEngine.Random.value < 0.7f ? -1f : 1f) * Mathf.Sign(dir.x == 0f ? 1f : dir.x);
                st.Life = Mathf.Max(st.Life, st.CurlAt + st.CurlDur + 0.35f);
            }
            st.Pts[0] = RandomPoint();
            st.Count = 1;
        }

        // =====================================================================
        // Pixel dokusu: çizgiler + yağmur perdesi, dünya ızgarasına kilitli kare hücreler
        // =====================================================================

        private void DrawOverlay()
        {
            var cam = Cam;
            if (overlay == null || cam == null || !cam.orthographic) return;
            float cell = Cell;
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            int needW = Mathf.CeilToInt(w * 2f / cell) + 4, needH = Mathf.CeilToInt(h * 2f / cell) + 4;
            if (overlayTex == null || needW != bufW || needH != bufH) CreateOverlay(needW, needH);
            bufOrigin = new Vector2(Mathf.Floor((c.x - w) / cell) - 2f, Mathf.Floor((c.y - h) / cell) - 2f) * cell;
            overlay.transform.position = new Vector3(bufOrigin.x, bufOrigin.y, 0f);
            Array.Clear(buf, 0, buf.Length);

            if (master > 0f)
            {
                for (int i = 0; i < streaks.Count; i++) DrawStreak(streaks[i]);
                var rc = (Color32)new Color(rainColor.r, rainColor.g, rainColor.b, rainColor.a * master);
                for (int i = 0; i < drops.Count; i++)
                {
                    var d = drops[i];
                    float k = d.Age / d.Life;
                    if (k > 0.8f && ((int)(d.Pos.x * 97f) & 1) == 0) continue;
                    Line(d.Pos, d.Pos - d.Vel.normalized * d.Len, rc, rc, 1, false);
                }
            }
            overlayTex.SetPixels32(buf);
            overlayTex.Apply(false);
        }

        private void DrawStreak(Streak st)
        {
            if (!st.Alive || st.Count < 2) return;
            float s = StrengthAt(st.Pts[0]);
            float env = EventsUi.Smooth01(st.Age / 0.15f) * EventsUi.Smooth01((st.Life - st.Age) / 0.35f);
            float a = streakColor.a * env * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01((s - 0.2f) / 0.6f)) * Mathf.Lerp(0.7f, 1f, st.Depth) * master;
            a = Mathf.Round(a * 4f) / 4f; // kademeli saydamlık (pixel art)
            if (a <= 0f) return;
            var full = (Color32)new Color(streakColor.r, streakColor.g, streakColor.b, a);
            var mid = (Color32)new Color(streakColor.r, streakColor.g, streakColor.b, a * 0.6f);
            // Öndeki (yakın) çizgiler çift hücre kalınlığında.
            int thick = st.Depth > 0.6f ? 2 : 1;
            int n = st.Count - 1;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Mathf.Max(1, st.Pts.Length - 2);
                // Baştan kuyruğa: dolu → yarı saydam → seyrek (dama) hücreler.
                if (u < 0.5f) Line(st.Pts[i], st.Pts[i + 1], full, full, thick, false);
                else if (u < 0.78f) Line(st.Pts[i], st.Pts[i + 1], mid, mid, 1, false);
                else Line(st.Pts[i], st.Pts[i + 1], mid, mid, 1, true);
            }
        }

        // Bresenham, hücre koordinatında. sparse: dama deseniyle her ikinci hücre.
        private void Line(Vector2 p0, Vector2 p1, Color32 c0, Color32 c1, int thick, bool sparse)
        {
            float cell = Cell;
            int x0 = Mathf.FloorToInt((p0.x - bufOrigin.x) / cell), y0 = Mathf.FloorToInt((p0.y - bufOrigin.y) / cell);
            int x1 = Mathf.FloorToInt((p1.x - bufOrigin.x) / cell), y1 = Mathf.FloorToInt((p1.y - bufOrigin.y) / cell);
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            int guard = 0;
            while (guard++ < 400)
            {
                if (!sparse || ((x0 + y0) & 1) == 0)
                {
                    Plot(x0, y0, c0);
                    if (thick > 1) Plot(x0, y0 - 1, c0);
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void Plot(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= bufW || y >= bufH) return;
            int i = y * bufW + x;
            if (buf[i].a < c.a) buf[i] = c;
        }

        private void CreateOverlay(int w, int h)
        {
            if (overlayTex != null) Destroy(overlayTex);
            bufW = w; bufH = h;
            buf = new Color32[w * h];
            overlayTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "WindOverlay" };
            var sprite = Sprite.Create(overlayTex, new Rect(0, 0, w, h), Vector2.zero, Ppu / cellPixels, 0, SpriteMeshType.FullRect);
            sprite.name = "WindOverlay";
            overlay.sprite = sprite;
        }

        // =====================================================================
        // Döküntü (yaprak, kâğıt, poşet, dal)
        // =====================================================================

        private void UpdateDebris(Debris d, float dt)
        {
            float s = StrengthAt(d.Ground);
            float t = clock;
            float resp, lift, grav, drag, slideAt;
            switch (d.Kind)
            {
                case Kind.Paper: resp = 2.4f; lift = 2.4f; grav = 1.9f; drag = 1.4f; slideAt = 0.28f; break;
                case Kind.Bag: resp = 1.7f; lift = 2.4f; grav = 0.7f; drag = 1.8f; slideAt = 0.2f; break;
                case Kind.Twig: resp = 1.3f; lift = 1.2f; grav = 3.4f; drag = 0.8f; slideAt = 0.5f; break;
                default: resp = 3.6f; lift = 2.6f; grav = 1.7f; drag = 1.3f; slideAt = 0.32f; break;
            }
            Vector2 wind = dir * s * carrySpeed;
            if (d.Air)
            {
                // Yapraklar rüzgâra dik yönde de salınır (havada süzülme).
                float sway = d.Kind == Kind.Twig ? 0f : Mathf.Sin(t * (d.Kind == Kind.Bag ? 2.1f : 4.3f) + d.Seed) * 0.9f * s;
                d.Vel += (wind + perp * sway - d.Vel) * resp * dt;
                float draft = Mathf.PerlinNoise(d.Seed, t * 1.3f) * 1.6f - 0.5f; // yukarı/aşağı hava akımı
                d.VH += (lift * Mathf.Max(0f, s - d.LiftAt * 0.55f) * draft * 1.6f - grav) * dt;
                d.VH *= Mathf.Clamp01(1f - drag * dt);
                d.H += d.VH * dt;
                if (d.H > 2.3f) { d.H = 2.3f; d.VH = Mathf.Min(0f, d.VH); }
                d.Angle += d.Spin * (0.3f + s) * dt;
                d.Flip += d.FlipRate * (0.4f + s) * dt;
                if (d.H <= 0f)
                {
                    d.H = 0f;
                    if (d.VH < -0.7f && d.Kind != Kind.Bag) d.VH = -d.VH * 0.28f; // yere seker
                    else
                    {
                        d.Air = false;
                        d.VH = 0f;
                        d.FlipY = Mathf.Cos(d.Flip) >= 0f ? 1f : -1f;
                    }
                }
            }
            else
            {
                if (s > slideAt)
                {
                    // Yerde kayma / sürüklenme; dal yuvarlanır.
                    d.Vel += (wind * 0.32f * (s - slideAt) / Mathf.Max(0.1f, s) - d.Vel) * resp * 0.8f * dt;
                    float v = d.Vel.magnitude;
                    d.Angle += (d.Kind == Kind.Twig ? d.Spin * 0.8f : d.Spin * 0.12f) * v * dt;
                }
                else d.Vel *= Mathf.Clamp01(1f - 6f * dt);
                if (s > d.LiftAt)
                {
                    d.Air = true;
                    d.VH = lift * 0.55f * s * UnityEngine.Random.Range(0.6f, 1.2f);
                }
            }
            d.Ground += d.Vel * dt;

            if (!Expanded(region, 0.6f).Contains(d.Ground)) { Respawn(d, false); return; }

            // Pixel art: yalnız 90° adımlarla döner, takla yarım boy / ters çevirmeyle gösterilir.
            float c = d.Air ? Mathf.Cos(d.Flip) : d.FlipY;
            float flipY = c > 0.6f ? 1f : c > 0f ? 0.5f : c > -0.6f ? -0.5f : -1f;
            var tr = d.Sr.transform;
            Vector2 at = d.Ground + new Vector2(0f, d.H);
            tr.position = new Vector3(Mathf.Round(at.x * Ppu) / Ppu, Mathf.Round(at.y * Ppu) / Ppu, 0f);
            tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(d.Angle / 90f) * 90f);
            if (d.Kind == Kind.Bag)
            {
                bool puff = Mathf.Sin(t * 7f + d.Seed) * (0.3f + s) > 0.25f; // poşet şişip söner (1 piksel)
                tr.localScale = new Vector3(puff ? 1.1f : 1f, puff ? 0.9f : 1f, 1f);
            }
            else tr.localScale = new Vector3(1f, flipY, 1f);
            d.Sr.sortingOrder = d.Air && d.H > 0.08f ? airSortingOrder : groundSortingOrder;
            var col = d.Sr.color; col.a = master; d.Sr.color = col;

            if (d.Shadow != null)
            {
                bool show = d.H > 0.04f;
                d.Shadow.enabled = show;
                if (show)
                {
                    d.Shadow.transform.position = new Vector3(Mathf.Round(d.Ground.x * Ppu) / Ppu, Mathf.Round(d.Ground.y * Ppu) / Ppu, 0f);
                    float k = Mathf.Clamp01(1f - d.H / 2.3f);
                    d.Shadow.transform.localScale = Vector3.one * (d.Kind == Kind.Bag ? 2f : 1f);
                    d.Shadow.color = new Color(0f, 0f, 0f, Mathf.Round(0.3f * k * master * 4f) / 4f);
                }
            }
        }

        private void Respawn(Debris d, bool anywhere)
        {
            d.Vel = Vector2.zero;
            d.Angle = UnityEngine.Random.Range(0, 4) * 90f;
            d.Flip = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            d.FlipY = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            d.Seed = UnityEngine.Random.Range(0f, 100f);
            if (anywhere || !hasRegion)
            {
                d.Ground = RandomPoint();
                d.Air = false;
                d.H = d.VH = 0f;
                return;
            }
            // Rüzgâr üstü kenardan, çoğunlukla havada girer.
            Vector2 p = RandomPoint();
            p -= dir * (ExitDistance(region, p, -dir) + 0.3f);
            d.Ground = p;
            float s = StrengthAt(p);
            d.Air = s > d.LiftAt * 0.8f;
            d.H = d.Air ? UnityEngine.Random.Range(0.1f, 1.6f) : 0f;
            d.VH = 0f;
            d.Vel = dir * s * carrySpeed * (d.Air ? 1f : 0.3f);
        }

        // =====================================================================
        // Sis ve yağmur perdesi (bastırma cephesiyle birlikte gezer)
        // =====================================================================

        private void SpawnSheets(float dt)
        {
            if (!hasRegion || master <= 0f) return;
            float excess = Mathf.Clamp01(Gust01 * 1.4f);
            mistAcc += mistRate * excess * dt;
            rainAcc += rainRate * excess * dt;
            int guard = 0;
            while (mistAcc >= 1f && guard++ < 20)
            {
                mistAcc -= 1f;
                Vector2 p = RandomPoint();
                float s = StrengthAt(p);
                if (s < sheetThreshold || mistSprite == null) continue;
                var sr = mistPool.Count > 0 ? mistPool.Pop() : NewRenderer("WindMist", mistSprite, airSortingOrder + 5);
                sr.enabled = true;
                mists.Add(new Mist
                {
                    Sr = sr, Pos = p, Vel = dir * s * carrySpeed * 1.1f + new Vector2(0f, 0.08f),
                    Life = UnityEngine.Random.Range(1.2f, 2.2f), S0 = 1f, S1 = UnityEngine.Random.value < 0.5f ? 2f : 1f,
                });
            }
            guard = 0;
            while (rainAcc >= 1f && guard++ < 60)
            {
                rainAcc -= 1f;
                Vector2 p = RandomPoint();
                float s = StrengthAt(p);
                if (s < sheetThreshold) continue;
                drops.Add(new Drop
                {
                    Pos = p + new Vector2(0f, UnityEngine.Random.Range(0.3f, 1.2f)),
                    Vel = dir * s * 7.5f + new Vector2(0f, -6f),
                    Life = UnityEngine.Random.Range(0.25f, 0.45f),
                    Len = UnityEngine.Random.Range(3, 6) * Cell,
                });
            }
        }

        private void UpdateDrops(float dt)
        {
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.Age += dt;
                if (d.Age >= d.Life) { drops.RemoveAt(i); continue; }
                d.Pos += d.Vel * dt;
                drops[i] = d;
            }
        }

        private void UpdateMist(float dt)
        {
            for (int i = mists.Count - 1; i >= 0; i--)
            {
                var m = mists[i];
                m.Age += dt;
                float k = m.Age / m.Life;
                if (k >= 1f) { ReleaseMist(i); continue; }
                m.Pos += m.Vel * dt;
                var tr = m.Sr.transform;
                tr.position = new Vector3(Mathf.Round(m.Pos.x * Ppu) / Ppu, Mathf.Round(m.Pos.y * Ppu) / Ppu, 0f);
                tr.localScale = Vector3.one * (k < 0.5f ? m.S0 : m.S1); // tam sayı ölçek: piksel bozulmaz
                var c = mistColor;
                c.a = Mathf.Round(mistColor.a * Mathf.Sin(k * Mathf.PI) * master * 4f) / 4f;
                m.Sr.color = c;
            }
        }

        private void ReleaseMist(int i)
        {
            var m = mists[i];
            mists.RemoveAt(i);
            if (m.Sr == null) return;
            m.Sr.enabled = false;
            mistPool.Push(m.Sr);
        }

        // =====================================================================
        // Sallanan objeler (yay-sönüm: bastırmada eğilir, geçince geri esner)
        // =====================================================================

        private void CollectAutoSway()
        {
            if (autoSwayKeywords == null || autoSwayKeywords.Length == 0) return;
            var list = new List<SwayTarget>(swayTargets);
            var known = new HashSet<Transform>();
            foreach (var s in swayTargets) if (s != null && s.target != null) known.Add(s.target);
            foreach (var r in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude))
            {
                if (r.sprite == null || r.gameObject.scene != gameObject.scene || known.Contains(r.transform) || r.transform.IsChildOf(transform)) continue;
                string n = r.sprite.name;
                bool match = false;
                foreach (var k in autoSwayKeywords) if (!string.IsNullOrEmpty(k) && n.Contains(k)) { match = true; break; }
                if (!match) continue;
                float h = r.sprite.rect.height;
                bool low = h < 70f;
                list.Add(new SwayTarget
                {
                    target = r.transform,
                    maxAngle = h >= 120f ? autoSwayAngles.x : low ? autoSwayAngles.z : autoSwayAngles.y,
                    frequency = h >= 120f ? 0.85f : low ? 1.8f : 1.25f,
                    flutter = low ? 0.55f : 0.35f,
                });
                known.Add(r.transform);
            }
            swayTargets = list.ToArray();
        }

        private void Capture(SwayTarget s)
        {
            if (s.Captured) return;
            s.BaseRot = s.target.localRotation;
            s.Captured = true;
        }

        private void UpdateSway(SwayTarget s, float dt)
        {
            if (s == null || s.target == null) return;
            Capture(s);
            float st = StrengthAt(s.target.position);
            float flutter = (Mathf.PerlinNoise(clock * 5.5f, s.target.position.x) - 0.5f) * 2f * s.flutter * st;
            // Rüzgâr sola (-x) eserse tepe sola eğilir (+Z).
            float target = -dir.x * s.maxAngle * st * (1f + flutter);
            float w = s.frequency * Mathf.PI * 2f;
            const float zeta = 0.22f;
            float sub = dt / 2f;
            for (int k = 0; k < 2; k++)
            {
                s.Vel += (w * w * (target - s.Angle) - 2f * zeta * w * s.Vel) * sub;
                s.Angle += s.Vel * sub;
            }
            s.target.localRotation = s.BaseRot * Quaternion.Euler(0f, 0f, s.Angle);
        }

        // =====================================================================
        // Kamera titremesi (yalnız çizim sırasında; kamera takip scriptiyle çakışmaz)
        // =====================================================================

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (buffetPixels <= 0f || master <= 0f || !Application.isPlaying) return;
            if (cam != Cam) return;
            float k = Mathf.Clamp01((Gust01 - 0.55f) / 0.45f);
            if (k <= 0f) return;
            float a = buffetPixels * k * master / Ppu;
            float t = clock * 9f;
            buffetApplied = new Vector3((Mathf.PerlinNoise(t, 3.1f) - 0.5f) * 2f * a, (Mathf.PerlinNoise(7.7f, t) - 0.5f) * a, 0f);
            buffetCam = cam;
            cam.transform.position += buffetApplied;
        }

        private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != buffetCam) return;
            cam.transform.position -= buffetApplied;
            buffetApplied = Vector3.zero;
            buffetCam = null;
        }

        // =====================================================================
        // Kurulum ve yardımcılar
        // =====================================================================

        private void Build()
        {
            if (root != null) return;
            root = new GameObject("WindFX").transform;
            root.SetParent(transform, false);
            overlay = NewRenderer("WindLines", null, streakSortingOrder);
            if (lineMaterial != null) overlay.sharedMaterial = lineMaterial;

            for (int i = 0; i < streakCount; i++)
                streaks.Add(new Streak { Pts = new Vector2[trailPoints] });

            for (int i = 0; i < leafCount && leafSprites.Length > 0; i++)
            {
                var sp = leafSprites[i % leafSprites.Length];
                if (sp != null) AddDebris(Kind.Leaf, sp, UnityEngine.Random.Range(0.42f, 0.8f));
            }
            for (int i = 0; i < paperCount && paperSprite != null; i++) AddDebris(Kind.Paper, paperSprite, UnityEngine.Random.Range(0.5f, 0.85f));
            for (int i = 0; i < bagCount && bagSprite != null; i++) AddDebris(Kind.Bag, bagSprite, UnityEngine.Random.Range(0.3f, 0.5f));
            for (int i = 0; i < twigCount && twigSprite != null; i++) AddDebris(Kind.Twig, twigSprite, UnityEngine.Random.Range(0.85f, 1.1f));
        }

        private void AddDebris(Kind kind, Sprite sprite, float liftAt)
        {
            var d = new Debris
            {
                Kind = kind,
                Sr = NewRenderer(sprite.name, sprite, groundSortingOrder),
                LiftAt = liftAt,
                Spin = UnityEngine.Random.Range(160f, 420f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * (kind == Kind.Bag ? 0.15f : 1f),
                FlipRate = kind == Kind.Bag || kind == Kind.Twig ? 0f : UnityEngine.Random.Range(4f, 11f),
            };
            if (shadowSprite != null)
            {
                d.Shadow = NewRenderer("Shadow", shadowSprite, groundSortingOrder - 1);
                d.Shadow.enabled = false;
            }
            debris.Add(d);
        }

        private SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        private Vector2 RandomPoint() => new Vector2(
            UnityEngine.Random.Range(region.xMin, region.xMax),
            UnityEngine.Random.Range(region.yMin, region.yMax));

        private static Rect Expanded(Rect r, float m) => Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);

        // p noktasından d yönünde dikdörtgenin kenarına uzaklık.
        private static float ExitDistance(Rect r, Vector2 p, Vector2 d)
        {
            float tx = d.x > 0.0001f ? (r.xMax - p.x) / d.x : d.x < -0.0001f ? (r.xMin - p.x) / d.x : float.MaxValue;
            float ty = d.y > 0.0001f ? (r.yMax - p.y) / d.y : d.y < -0.0001f ? (r.yMin - p.y) / d.y : float.MaxValue;
            return Mathf.Max(0f, Mathf.Min(tx, ty));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.9f);
            Vector3 c = transform.position;
            Vector3 d = (Vector3)(direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left);
            Gizmos.DrawLine(c, c + d * 1.5f);
            Gizmos.DrawLine(c + d * 1.5f, c + d * 1.2f + new Vector3(-d.y, d.x) * 0.2f);
            Gizmos.DrawLine(c + d * 1.5f, c + d * 1.2f - new Vector3(-d.y, d.x) * 0.2f);
            foreach (var s in swayTargets)
                if (s != null && s.target != null) Gizmos.DrawWireSphere(s.target.position, 0.12f);
        }
    }
}
