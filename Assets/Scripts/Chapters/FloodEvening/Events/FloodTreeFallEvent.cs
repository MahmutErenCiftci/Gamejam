using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Uzun yolda köşeden sonraki ağaç düşüşü (SEL3-10, ZIGZAG §9). TreeStopSpot alanına (trigger) eklenen prefabın kökü
    /// TreeFallSpot'tur (ağacın dibi). Oyuncu alana girince TEK SEFER:
    /// (Olaydan önce ağaç fırtınada hafifçe salınır.) Cenk güvenli noktada durur, hareket kilitlenir
    /// (Hub.NotifyOpened/Closed("treefall")) → ilk çatırtı, kütükte kıymıklar belirir, ağaç gıcırdayarak yavaşça eğilir,
    /// yaprak döker → ikinci, sert kırılma; Cenk irkilip yarım adım geri çekilir → ağaç kırılma noktasından devrilen
    /// gerçek bir gövde gibi önce ağır, sonra hızlanarak yola düşer (θ'' ∝ sin θ eğrisi; fizik motoru yok), kırık uç
    /// kütükten kayıp yere iner, gölgesi çarpma yerine kayar → yere değdiği karede yolu kapatan collider açılır, dönen
    /// gövdenin yerine yatık ağaç görseli geçer (toz/sarsıntı örter), gövde bir iki kez seker, dallar yere ezilip esner
    /// (ayrı taç varsa o geride kalır ve ezilir), toz yerde yuvarlanarak yayılır, dal parçaları uçuşur, yapraklar süzülüp
    /// yere konar, kamera sarsılır → kontrol geri gelir, Hub.Complete("tree_fell"), bağlı dallarda Arm() (Arda'nın
    /// BranchObstacle'ı).
    /// Büyük gövde dekordur; geçidi kapatan ve itilen şey küçük dal. Düşüşten önce düşme alanına girişi katı bir
    /// FallZoneBlock collider'ı kapatır, ağaç Cenk'in üstüne düşmez. Collider mesajı yerine her FixedUpdate'te geometrik
    /// örtüşme kontrol edilir (TreeStopSpot başka bir objede de olabilir). Hub.FreeRoam false iken (diyalog, panel) çalışmaz.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class FloodTreeFallEvent : MonoBehaviour, IFloodEveningResettable
    {
        public const string MinigameId = "treefall";
        public const string StepId = "tree_fell";

        [Header("Tetik")]
        [Tooltip("TreeStopSpot alanı (trigger). Boşsa çocuklarda 'TreeStopSpot' aranır.")]
        [SerializeField] private Collider2D stopZone;
        [Tooltip("Boşsa sahnedeki IsoPlayerController.")]
        [SerializeField] private IsoPlayerController player;
        [Tooltip("İşaretliyse sadece Hub.FreeRoam true iken tetiklenir (bölüm). Test için kapatılabilir.")]
        [SerializeField] private bool requireFreeRoam = true;

        [Header("Ağaç")]
        [Tooltip("Dönen parça; pivotu kırılma noktası (gövdenin dibi).")]
        [SerializeField] private Transform tree;
        [Tooltip("İsteğe bağlı: ağacın çocuğu olan ayrı taç; düşerken geride kalır, yere değince ezilip esner.")]
        [SerializeField] private Transform canopy;
        [Tooltip("Yerde yatan ağaç görseli (başta kapalı). Yere değdiği karede dönen gövdenin yerine geçer ve ezilip esner; " +
                 "pivotu kırık uç, konumu = gövde pivotu + fallSlide.")]
        [SerializeField] private Transform fallenTree;
        [Tooltip("Kütüğün kırık üstündeki açık renk kıymıklar (başta kapalı): ilk çatırtıda belirir, ikinci kırılmada tam görünür.")]
        [SerializeField] private SpriteRenderer stumpBreak;
        [Tooltip("Yerdeki gölge (dönmez; düşüşle birlikte çarpma yerine kayar).")]
        [SerializeField] private SpriteRenderer shadow;
        [Tooltip("Düşmüş hâldeki Z açısı (negatif = sağa düşer).")]
        [SerializeField] private float fallenAngle = -84f;
        [Tooltip("Düşerken pivotun kayması (yerel, dünya birimi): kırık uç kütükten kayıp yere iner.")]
        [SerializeField] private Vector2 fallSlide = new Vector2(0.12f, -0.06f);
        [Tooltip("Olaydan önce fırtınada hafif salınım (derece). 0 = durgun.")]
        [SerializeField, Range(0f, 3f)] private float idleSway = 0.8f;

        [Header("Zamanlama")]
        [Tooltip("Cenk durduktan sonra ilk çatırtıya kadar.")]
        [SerializeField, Min(0f)] private float beatSeconds = 0.25f;
        [Tooltip("İlk çatırtıdan sonra ağacın gıcırdayarak eğildiği süre.")]
        [SerializeField, Min(0.1f)] private float leanSeconds = 0.75f;
        [Tooltip("Eğilme açısı (derece, düşüş yönünde).")]
        [SerializeField, Range(1f, 15f)] private float leanAngle = 6f;
        [Tooltip("İkinci kırılmadan düşüşe kadar kısa duraksama.")]
        [SerializeField, Min(0f)] private float snapSeconds = 0.12f;
        [Tooltip("Eğik hâlden yere kadar düşüş süresi (yavaş başlar, hızlanır).")]
        [SerializeField, Min(0.3f)] private float fallSeconds = 1.15f;
        [Tooltip("Yere değince sekme ve oturma süresi.")]
        [SerializeField, Min(0.1f)] private float settleSeconds = 0.8f;
        [Tooltip("İlk sekme açısı (derece).")]
        [SerializeField] private float bounceAngle = 5f;
        [Tooltip("Oturduktan sonra kontrol geri verilmeden önce.")]
        [SerializeField, Min(0f)] private float afterSeconds = 0.45f;

        [Header("Taç")]
        [Tooltip("Düşerken tacın geride kalma oranı (açısal hız × bu, derece).")]
        [SerializeField, Range(0f, 0.05f)] private float canopyLag = 0.022f;
        [SerializeField, Range(0f, 20f)] private float canopyMaxLag = 9f;
        [Tooltip("Yere değince tacın yassılma miktarı (0..1).")]
        [SerializeField, Range(0f, 0.5f)] private float canopySquash = 0.2f;
        [Tooltip("Yerde kalıcı yassılık: dallar ezildiği için yatan taç dik hâlinden biraz basık durur (1 = hiç).")]
        [SerializeField, Range(0.6f, 1f)] private float canopyRestScale = 0.88f;

        [Header("Çarpışma")]
        [Tooltip("Düşüşten ÖNCE düşme alanına girişi kapatan katı collider. Yere değince kapanır.")]
        [SerializeField] private Collider2D fallZoneBlock;
        [Tooltip("Ağaç yere değdiği karede açılır (gövde + yerdeki taç izi).")]
        [SerializeField] private Collider2D[] closeOnImpact = Array.Empty<Collider2D>();
        [Tooltip("Düşüş bitince Arm() çağrılacak objeler (Arda'nın FE_BranchObstacle'ı).")]
        [SerializeField] private GameObject[] armOnFallen = Array.Empty<GameObject>();
        [Tooltip("Yere değince görünür (yerdeki yaprak/dal döküntüsü).")]
        [SerializeField] private GameObject[] showOnImpact = Array.Empty<GameObject>();

        [Header("Cenk")]
        [Tooltip("Cenk'in döneceği nokta (genelde ağacın yere düşeceği yer).")]
        [SerializeField] private Transform lookAt;
        [Tooltip("Cenk'in başında beliren '!' (başta kapalı).")]
        [SerializeField] private SpriteRenderer alertIcon;
        [SerializeField] private Vector2 alertOffset = new Vector2(0f, 1.25f);
        [Tooltip("İkinci kırılmada Cenk'in ağaçtan uzağa irkilme mesafesi (engel varsa daha az).")]
        [SerializeField, Min(0f)] private float flinchDistance = 0.14f;
        [Tooltip("İsteğe bağlı (P-FLOOD): ikinci kırılmadan kontrol geri gelene kadar Cenk'in irkilme pozu (ör. CHR_Cenk_Startled_SouthEast)")]
        [SerializeField] private Sprite startledPose;

        [Header("Efekt")]
        [SerializeField] private Sprite leafSprite;
        [SerializeField] private Sprite leafSprite2;
        [SerializeField] private Sprite twigSprite;
        [SerializeField] private Sprite dustSprite;
        [SerializeField] private Sprite chipSprite;
        [Tooltip("Yere değme hattı: düşen gövdenin uzandığı çizgi (yerel). Toz ve yapraklar bu hat boyunca çıkar.")]
        [SerializeField] private Vector2 impactFrom = new Vector2(0.9f, 0.3f);
        [SerializeField] private Vector2 impactTo = new Vector2(2.9f, 0.4f);
        [Tooltip("Taç içinde yaprak dökülen bölge (tacın yerel uzayında, yarım boyut).")]
        [SerializeField] private Vector2 canopyLeafArea = new Vector2(1.1f, 0.8f);
        [SerializeField] private Vector2 canopyLeafCenter = new Vector2(0f, 0.95f);
        [SerializeField, Range(0, 120)] private int impactLeaves = 70;
        [SerializeField, Range(0, 40)] private int impactDust = 22;
        [SerializeField, Range(0, 30)] private int impactTwigs = 10;
        [SerializeField, Range(0, 30)] private int crackChips = 12;
        [Tooltip("Düşüş sırasında saniyede dökülen en fazla yaprak (açısal hızla orantılı).")]
        [SerializeField, Range(0f, 200f)] private float fallLeafRate = 70f;
        [Tooltip("Efekt sprite'larının sıralaması (SortingGroup içinde, ağacın önünde).")]
        [SerializeField] private int fxSortingOrder = 5;
        [Tooltip("Çarpmada kamera sarsıntısı (piksel) ve süre.")]
        [SerializeField] private float shakePixels = 4f;
        [SerializeField] private float shakeSeconds = 0.5f;

        [Header("Ses (geçici; GameAudio'da ağaç sesi yok)")]
        [SerializeField] private string crackSfx = "SFX_Glass_Crack";
        [SerializeField] private string creakSfx = "SFX_Quake_ObjectsRattle";
        [SerializeField] private string impactSfx = "SFX_Quake_Rumble";
        [SerializeField] private string impactSfx2 = "SFX_Books_Fall";
        [SerializeField] private string splashSfx = "SFX_Water_Splash";
        [Tooltip("İkinci kırılmada başlayan düşüş sesi (P-FLOOD: SFX_FE_Tree_Fall; klibin çarpması ~0,9 sn'de, düşüş süresiyle eşleşir)")]
        [SerializeField] private string fallSfx = "";

        [Header("Metin (isteğe bağlı)")]
        [Tooltip("Düştükten sonra Hub.Say ile istenen satır (flood_evening.json). Boşsa hiçbir şey söylenmez.")]
        [SerializeField] private string afterLineId = "FE_TREE_FELL";
        [SerializeField, TextArea] private string afterFallback = "Ağaç yola devrildi. İyi ki uzakta durdum.";

        private const int CurveSamples = 96;

        private Rigidbody2D body;
        private Collider2D playerCollider;
        private Coroutine routine;
        private bool lockedPlayer, prevCanMove;
        private Vector3 treeBasePos, canopyBasePos, canopyBaseScale, shadowBasePos, shadowBaseScale, fallenBasePos, fallenBaseScale;
        private Quaternion treeBaseRot, canopyBaseRot, fallenBaseRot;
        private Color shadowBaseColor;
        private bool baseCaptured;
        private Transform fxRoot;
        private readonly List<Particle> particles = new List<Particle>();
        private float[] fallCurve;      // normalize zaman → normalize açı (0 = eğik, 1 = yerde)
        private float fallCurveRate;    // eğrinin sonundaki eğim (yere değerken açısal hız oranı)

        // Taç yay-sönüm durumu
        private float canopyAngle, canopyVel, canopyScale = 1f, canopyScaleVel;
        private bool canopySpring;

        // Kamera sarsıntısı (yalnız çizim sırasında uygulanır; kamera takip scriptiyle çakışmaz)
        private float shakeTime, shakeDuration, shakeAmp;
        private Camera shakeCam;
        private Vector3 shakeApplied;

        /// <summary>Olay oynadı mı (ağaç yerde). Reset'e kadar tekrar oynamaz.</summary>
        public bool HasFallen { get; private set; }
        /// <summary>Animasyon şu an sürüyor mu.</summary>
        public bool IsPlaying { get; private set; }
        /// <summary>Kameranın sinematikte baktığı nokta: ağacın yere düşeceği hattın ortası (P-FLOOD: bölüm kamerası buraya kayar).</summary>
        public Vector3 FocusPoint => LookTarget();

        /// <summary>
        /// P-FLOOD (ritimle it/çek): yatan gövdede birkaç yaprak ve ince dal parçası savrulur (her iyi vuruşta). Ağacın kendi
        /// parçacık sistemi ve sprite'ları kullanılır; ağaç düşmeden hiçbir şey yapmaz. offset = gövdenin o anki kayması (dünya).
        /// </summary>
        public void EmitShoveDebris(int leaves, int twigs, Vector2 offset)
        {
            if (!HasFallen) return;
            Vector2 local = transform.InverseTransformVector(offset);
            for (int i = 0; i < leaves; i++)
            {
                Vector2 at = Vector2.Lerp(impactFrom, impactTo, R(0.35f, 1f)) + local + new Vector2(0f, R(0f, 0.35f));
                var p = Spawn(RandomLeaf(), at, new Vector2(R(-0.8f, 0.8f), R(0.6f, 1.5f)), R(1.4f, 2.2f));
                if (p == null) break;
                p.Gravity = 1.7f; p.Drag = 1.9f; p.Sway = R(0.15f, 0.4f); p.Phase = R(0f, 6.28f); p.Spin = R(-360f, 360f);
                p.GroundY = at.y - R(0.1f, 0.45f);
            }
            for (int i = 0; i < twigs; i++)
            {
                Vector2 at = Vector2.Lerp(impactFrom, impactTo, R(0.2f, 0.9f)) + local + new Vector2(0f, R(0f, 0.2f));
                var p = Spawn(twigSprite, at, new Vector2(R(-1f, 1f), R(0.7f, 1.4f)), R(1.0f, 1.6f));
                if (p == null) break;
                p.Gravity = 6f; p.Drag = 0.8f; p.Spin = R(-600f, 600f);
                p.GroundY = at.y - R(0.1f, 0.35f);
            }
        }

        /// <summary>Ağaç yere değdiğinde (collider'lar açılırken).</summary>
        public event Action Impact;
        /// <summary>Olay bitti, kontrol geri verildi.</summary>
        public event Action Finished;

        private sealed class Particle
        {
            public SpriteRenderer Sr;
            public Vector2 Pos, Vel;
            public float Spin, Angle, Life, Age, Gravity, Drag, Sway, Phase, StartScale, EndScale, Alpha = 1f, GroundY = float.NegativeInfinity;
            public bool Landed;
        }

        private void Awake()
        {
            if (stopZone == null)
            {
                var t = transform.Find("TreeStopSpot");
                if (t != null) stopZone = t.GetComponent<Collider2D>();
            }
            if (stopZone != null) stopZone.isTrigger = true;
            CaptureBase();
            BuildFallCurve();
            if (alertIcon != null) alertIcon.gameObject.SetActive(false);
            ShowFallen(false);
            SetBreak(0f);
            ApplyState(false);
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
            shakeTime = shakeDuration = 0f;
            // Sahne çıkışı / obje kapanırken kilit ve panel açık kalmasın.
            if (IsPlaying) Abort();
        }

        private void CaptureBase()
        {
            if (baseCaptured || tree == null) return;
            treeBasePos = tree.localPosition;
            treeBaseRot = tree.localRotation;
            if (canopy != null)
            {
                canopyBasePos = canopy.localPosition;
                canopyBaseRot = canopy.localRotation;
                canopyBaseScale = canopy.localScale;
            }
            if (shadow != null)
            {
                shadowBasePos = shadow.transform.localPosition;
                shadowBaseScale = shadow.transform.localScale;
                shadowBaseColor = shadow.color;
            }
            if (fallenTree != null)
            {
                fallenBasePos = fallenTree.localPosition;
                fallenBaseRot = fallenTree.localRotation;
                fallenBaseScale = fallenTree.localScale;
            }
            baseCaptured = true;
        }

        /// <summary>Ayakta (dönen gövde görünür) ya da yerde (yatık görsel görünür) hâli.</summary>
        private void ShowFallen(bool fallen) => BlendFallen(fallen ? 1f : 0f);

        /// <summary>
        /// Dönen gövdeden yatık görsele çapraz geçiş (0 = ayakta, 1 = yerde). Geniş taç yere çarpıp dağılırken
        /// solar, yatık görsel belirir; geçişi çarpma tozu ve yaprak savrulması örter.
        /// </summary>
        private void BlendFallen(float k)
        {
            if (fallenTree == null) return;
            fallenTree.gameObject.SetActive(k > 0f);
            foreach (var sr in fallenTree.GetComponentsInChildren<SpriteRenderer>(true)) { var c = sr.color; c.a = k; sr.color = c; }
            if (tree == null) return;
            // Dönen gövdenin görselleri söner; transform (ve varsa çocuk collider'lar) yerinde kalır.
            foreach (var sr in tree.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.enabled = k < 1f;
                var c = sr.color; c.a = 1f - k; sr.color = c;
            }
        }

        private void SetBreak(float alpha)
        {
            if (stumpBreak == null) return;
            stumpBreak.gameObject.SetActive(alpha > 0f);
            var c = stumpBreak.color; c.a = alpha; stumpBreak.color = c;
        }

        /// <summary>
        /// Dibinden devrilen katı bir gövde: θ'' = k·sin θ (θ dikeyden açı). Eğik başlangıçtan yere kadar sayısal olarak
        /// bir kez çözülür, normalize zaman → normalize açı tablosuna çevrilir. Böylece düşüş başta ağır, sonda hızlı olur
        /// ve süresi fallSeconds ile ayarlanır.
        /// </summary>
        private void BuildFallCurve()
        {
            float th0 = Mathf.Abs(leanAngle) * Mathf.Deg2Rad;
            float th1 = Mathf.Max(th0 + 0.1f, Mathf.Abs(fallenAngle) * Mathf.Deg2Rad);
            var ts = new List<float>(); var ths = new List<float>();
            float th = th0, w = 0.15f, t = 0f;
            const float dt = 0.0005f;
            ts.Add(0f); ths.Add(th);
            while (th < th1 && t < 20f)
            {
                w += Mathf.Sin(th) * dt;
                th += w * dt;
                t += dt;
                ts.Add(t); ths.Add(Mathf.Min(th, th1));
            }
            float total = t;
            fallCurve = new float[CurveSamples + 1];
            int j = 0;
            for (int i = 0; i <= CurveSamples; i++)
            {
                float target = total * i / CurveSamples;
                while (j < ts.Count - 2 && ts[j + 1] < target) j++;
                float k = Mathf.InverseLerp(ts[j], ts[j + 1], target);
                fallCurve[i] = Mathf.InverseLerp(th0, th1, Mathf.Lerp(ths[j], ths[j + 1], k));
            }
            fallCurve[CurveSamples] = 1f;
            fallCurveRate = (fallCurve[CurveSamples] - fallCurve[CurveSamples - 1]) * CurveSamples;
        }

        private float FallAt(float t01)
        {
            if (fallCurve == null) BuildFallCurve();
            float f = Mathf.Clamp01(t01) * CurveSamples;
            int i = Mathf.Min(CurveSamples - 1, (int)f);
            return Mathf.Lerp(fallCurve[i], fallCurve[i + 1], f - i);
        }

        private bool ResolvePlayer()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            if (player == null) return false;
            if (body == null) body = player.GetComponent<Rigidbody2D>();
            if (playerCollider == null) playerCollider = player.GetComponent<Collider2D>();
            return body != null;
        }

        private void FixedUpdate()
        {
            if (HasFallen || IsPlaying || stopZone == null || !stopZone.enabled) return;
            if (!ResolvePlayer() || !player.isActiveAndEnabled) return;
            if (requireFreeRoam && !FloodEveningHub.FreeRoam) return;
            bool inside = playerCollider != null && playerCollider.enabled
                ? stopZone.Distance(playerCollider).isOverlapped
                : stopZone.OverlapPoint(body.position);
            if (inside) Play();
        }

        /// <summary>Olayı hemen başlatır (tek sefer). Test ve akış için de çağrılabilir.</summary>
        public void Play()
        {
            if (HasFallen || IsPlaying) return;
            routine = StartCoroutine(Run());
        }

        private float Dir => Mathf.Sign(fallenAngle == 0f ? -1f : fallenAngle);

        private IEnumerator Run()
        {
            IsPlaying = true;
            CaptureBase();
            if (fallCurve == null) BuildFallCurve();
            FloodEveningHub.NotifyOpened(MinigameId);
            Debug.Log($"[FloodTreeFallEvent] {name}: ağaç düşüşü başladı", this);

            // 1) Cenk güvenli noktada durur, ağaca döner.
            ResolvePlayer();
            if (player != null)
            {
                prevCanMove = player.CanMove;
                lockedPlayer = true;
                player.CanMove = false;
                if (body != null) body.linearVelocity = Vector2.zero;
                FacePlayer();
            }
            for (float t = 0f; t < beatSeconds; t += Time.deltaTime) yield return null;

            // 2) İlk çatırtı: kıymıklar, "!", ağaç gıcırdayarak eğilir, birkaç yaprak düşer.
            Sfx(crackSfx, 0.6f);
            Sfx(creakSfx, 0.35f);
            SpawnChips(crackChips / 2);
            SetBreak(0.55f);
            StartCoroutine(PopAlert());
            float lean = leanAngle * Dir;
            float leafAcc = 0f;
            float sway0 = SwayAngle(Time.time); // salınımın içinden başla (sıçrama olmasın)
            for (float t = 0f; t < leanSeconds; t += Time.deltaTime)
            {
                float k = t / leanSeconds;
                float ease = k * k * (3f - 2f * k);
                // Liflerin teker teker kopması: düzensiz küçük sarsıntılar
                float jitter = (Mathf.Sin(t * 47f) * 0.5f + Mathf.Sin(t * 83f + 1.3f) * 0.3f) * (1f - k * 0.4f) * 0.9f;
                SetTree(lean * ease + jitter * Dir + sway0 * (1f - ease), 0f);
                SetCanopy(-jitter * 1.4f * Dir, 1f);
                leafAcc += Time.deltaTime * 10f;
                while (leafAcc >= 1f) { leafAcc -= 1f; SpawnFallingLeaf(); }
                UpdateShadow(0f);
                yield return null;
            }

            // 3) İkinci, sert kırılma: Cenk irkilip yarım adım geri çekilir.
            Sfx(crackSfx, 1f);
            Sfx(fallSfx, 1f);
            SpawnChips(crackChips);
            SetBreak(1f);
            Shake(1.2f, 0.15f);
            StartCoroutine(Flinch());
            SetPose(true);
            for (float t = 0f; t < snapSeconds; t += Time.deltaTime)
            {
                SetTree(lean + Mathf.Sin(t * 60f) * 0.8f * Dir, 0f);
                yield return null;
            }

            // 4) Düşüş: θ'' ∝ sin θ eğrisi (ağır başlar, hızlanır), taç geride kalır, yaprak döker, gölge kayar.
            float fallSpan = fallenAngle - lean;
            float prevAngle = lean;
            leafAcc = 0f;
            for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
            {
                float p = FallAt(t / fallSeconds);
                float angle = lean + fallSpan * p;
                float omega = Time.deltaTime > 0f ? (angle - prevAngle) / Time.deltaTime : 0f; // derece/sn (işaretli)
                prevAngle = angle;
                SetTree(angle, p);
                float lag = Mathf.Clamp(-omega * canopyLag, -canopyMaxLag, canopyMaxLag);
                SetCanopy(lag, 1f);
                canopyAngle = lag;
                UpdateShadow(p);
                float rate = fallLeafRate * Mathf.Clamp01(Mathf.Abs(omega) / 160f);
                leafAcc += rate * Time.deltaTime;
                while (leafAcc >= 1f) { leafAcc -= 1f; SpawnFallingLeaf(); }
                yield return null;
            }
            float impactOmega = fallSpan * fallCurveRate / fallSeconds;
            SetTree(fallenAngle, 1f);
            UpdateShadow(1f);

            // 5) Yere değdi: collider görselle aynı karede yolu kapatır; dönen gövde yerine yatık görsel geçer
            // (çarpma tozu, sarsıntı ve ezilme esnemesi geçişi örter); toz, yaprak, dal, sarsıntı, ses.
            if (fallenTree != null)
            {
                fallenTree.localPosition = fallenBasePos;
                BlendFallen(0.35f);
            }
            ApplyState(true);
            Sfx(impactSfx, 0.9f);
            Sfx(impactSfx2, 0.8f);
            Sfx(splashSfx, 0.45f);
            SpawnImpact();
            Shake(shakePixels, shakeSeconds);
            Impact?.Invoke();
            // Taç momentumla öne savrulur ve yere ezilir; sonra yay gibi toparlanır.
            if (canopy == null) { canopyAngle = 0f; canopyScale = 1f; }
            canopyVel = impactOmega * 0.35f;
            canopyScaleVel = -canopySquash * 28f;
            canopySpring = true;

            // Gövde sekmesi: sönümlenen birkaç küçük zıplama (yatık görsel de kırık ucu etrafında aynı sekmeyi yapar);
            // bu sırada dönen gövdeden yatık görsele çapraz geçiş.
            const float blendSeconds = 0.22f;
            for (float t = 0f; t < settleSeconds; t += Time.deltaTime)
            {
                float b = bounceAngle * Mathf.Exp(-t * 5.5f) * Mathf.Abs(Mathf.Sin(t * Mathf.PI / 0.24f));
                SetTree(fallenAngle - Dir * b, 1f);
                if (fallenTree != null)
                {
                    BlendFallen(Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(t / blendSeconds)));
                    fallenTree.localRotation = fallenBaseRot * Quaternion.Euler(0f, 0f, -Dir * b + canopyAngle * 0.3f);
                }
                yield return null;
            }
            SetTree(fallenAngle, 1f);
            if (fallenTree != null) { BlendFallen(1f); fallenTree.localRotation = fallenBaseRot; }

            for (float t = 0f; t < afterSeconds; t += Time.deltaTime) yield return null;
            ShowAlert(false);

            // 6) Kontrol geri gelir, sonra dal kurulur.
            HasFallen = true;
            Finish();
            FloodEveningHub.Complete(StepId);
            ArmBranches();
            Debug.Log($"[FloodTreeFallEvent] {name}: ağaç yolu kapattı, kontrol geri verildi", this);
            Finished?.Invoke();
            if (!string.IsNullOrEmpty(afterLineId) || !string.IsNullOrEmpty(afterFallback))
                FloodEveningHub.Say(afterLineId, afterFallback);
        }

        private void SetPose(bool on)
        {
            if (startledPose == null || player == null) return;
            var anim = player.GetComponent<IsoCharacterAnimator>();
            if (anim == null) return;
            if (on) anim.ShowPose(startledPose); else anim.ClearPose();
        }

        private void Finish()
        {
            SetPose(false);
            IsPlaying = false;
            routine = null;
            if (lockedPlayer && player != null)
            {
                // Bölüm akışı (FreeRoam) kontrolü kendisi açar; test sahnesinde önceki hâline döner.
                player.CanMove = prevCanMove || (!requireFreeRoam || FloodEveningHub.FreeRoam);
            }
            lockedPlayer = false;
            FloodEveningHub.NotifyClosed(MinigameId);
        }

        private void Abort()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            ShowAlert(false);
            if (IsPlaying) SetPose(false);
            if (IsPlaying)
            {
                IsPlaying = false;
                if (lockedPlayer && player != null) player.CanMove = prevCanMove;
                lockedPlayer = false;
                FloodEveningHub.NotifyClosed(MinigameId);
            }
        }

        public void ResetModule()
        {
            StopAllCoroutines();
            Abort();
            HasFallen = false;
            ClearParticles();
            canopySpring = false;
            canopyAngle = canopyVel = canopyScaleVel = 0f;
            canopyScale = 1f;
            shakeTime = shakeDuration = 0f;
            if (baseCaptured)
            {
                if (tree != null) { tree.localPosition = treeBasePos; tree.localRotation = treeBaseRot; }
                if (canopy != null) { canopy.localPosition = canopyBasePos; canopy.localRotation = canopyBaseRot; canopy.localScale = canopyBaseScale; }
                if (shadow != null) { shadow.transform.localPosition = shadowBasePos; shadow.transform.localScale = shadowBaseScale; shadow.color = shadowBaseColor; }
                if (fallenTree != null) { fallenTree.localPosition = fallenBasePos; fallenTree.localRotation = fallenBaseRot; fallenTree.localScale = fallenBaseScale; }
            }
            ShowFallen(false);
            SetBreak(0f);
            ApplyState(false);
        }

        private void ApplyState(bool fallen)
        {
            if (fallZoneBlock != null) fallZoneBlock.enabled = !fallen;
            foreach (var c in closeOnImpact) if (c != null) c.enabled = fallen;
            foreach (var go in showOnImpact) if (go != null) go.SetActive(fallen);
        }

        private void ArmBranches()
        {
            foreach (var go in armOnFallen)
                if (go != null) go.SendMessage("Arm", SendMessageOptions.DontRequireReceiver);
        }

        private void SetTree(float angle, float slide01)
        {
            if (tree == null) return;
            tree.localRotation = treeBaseRot * Quaternion.Euler(0f, 0f, angle);
            tree.localPosition = treeBasePos + (Vector3)(fallSlide * slide01);
        }

        private float RestScale => canopy != null ? canopyRestScale : 1f;

        private void SetCanopy(float angle, float squash)
        {
            if (canopy == null)
            {
                // Ayrı taç yoksa yere değme esnemesi yatık görselde: ekranda dikey yassılma, gövde boyunca hafif uzama,
                // kırık uç etrafında küçük savrulma.
                if (fallenTree == null || !fallenTree.gameObject.activeSelf) return;
                fallenTree.localRotation = fallenBaseRot * Quaternion.Euler(0f, 0f, angle * 0.3f);
                fallenTree.localScale = new Vector3(fallenBaseScale.x * (1f + (1f - squash) * 0.25f), fallenBaseScale.y * squash, fallenBaseScale.z);
                return;
            }
            canopy.localRotation = canopyBaseRot * Quaternion.Euler(0f, 0f, angle);
            // Yassılma tacın gövdeye dik ekseninde (yerel X; ağaç yattığında ekranda dikey), hacim korunsun diye gövde
            // ekseni (yerel Y) biraz uzar.
            canopy.localScale = new Vector3(canopyBaseScale.x * squash, canopyBaseScale.y * (1f + (1f - squash) * 0.35f), canopyBaseScale.z);
        }

        private void UpdateShadow(float p)
        {
            if (shadow == null) return;
            Vector2 fallenCenter = (impactFrom + impactTo) * 0.5f;
            float len = (impactTo - impactFrom).magnitude;
            var tr = shadow.transform;
            tr.localPosition = Vector3.Lerp(shadowBasePos, new Vector3(fallenCenter.x, fallenCenter.y - 0.08f, shadowBasePos.z), p * p);
            float sx = Mathf.Lerp(shadowBaseScale.x, shadowBaseScale.x * Mathf.Max(1f, len / 1.75f), p);
            float sy = Mathf.Lerp(shadowBaseScale.y, shadowBaseScale.y * 1.35f, p);
            tr.localScale = new Vector3(sx, sy, shadowBaseScale.z);
            var c = shadowBaseColor;
            c.a = shadowBaseColor.a * Mathf.Lerp(0.8f, 1.25f, p);
            shadow.color = c;
        }

        private void FacePlayer()
        {
            if (player == null) return;
            var anim = player.GetComponent<IsoCharacterAnimator>();
            if (anim != null) anim.Face(LookTarget() - player.transform.position);
        }

        private Vector3 LookTarget() => lookAt != null ? lookAt.position : transform.TransformPoint((impactFrom + impactTo) * 0.5f);

        // Cenk ağaçtan uzağa kısa bir geri adım atar (yalnız boş yere; collider'a girmez), yüzü ağaçta kalır.
        private IEnumerator Flinch()
        {
            if (player == null || body == null || flinchDistance <= 0f) yield break;
            Vector2 away = (Vector2)(player.transform.position - LookTarget());
            away.y *= 2f; // izometrik zemin
            if (away.sqrMagnitude < 0.0001f) away = Vector2.down;
            away.Normalize();
            away.y *= 0.5f;
            float dist = flinchDistance;
            var hits = new RaycastHit2D[4];
            int n = body.Cast(away, hits, flinchDistance + 0.02f);
            for (int i = 0; i < n; i++) if (!hits[i].collider.isTrigger) dist = Mathf.Min(dist, Mathf.Max(0f, hits[i].distance - 0.02f));
            Vector2 from = body.position, to = from + away * dist;
            for (float t = 0f; t < 0.18f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                if (body == null || !lockedPlayer) yield break;
                float k = 1f - (1f - t / 0.18f) * (1f - t / 0.18f);
                body.MovePosition(Vector2.Lerp(from, to, k));
            }
            FacePlayer();
        }

        private IEnumerator PopAlert()
        {
            if (alertIcon == null) yield break;
            ShowAlert(true);
            var tr = alertIcon.transform;
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                float k = t / 0.22f;
                float s = k < 0.6f ? Mathf.Lerp(0f, 1.35f, k / 0.6f) : Mathf.Lerp(1.35f, 1f, (k - 0.6f) / 0.4f);
                tr.localScale = Vector3.one * s;
                yield return null;
            }
            tr.localScale = Vector3.one;
        }

        private void ShowAlert(bool on)
        {
            if (alertIcon == null) return;
            alertIcon.gameObject.SetActive(on);
            if (on && player != null) alertIcon.transform.position = player.transform.position + (Vector3)alertOffset;
        }

        private static void Sfx(string id, float vol)
        {
            if (!string.IsNullOrEmpty(id)) GameAudio.PlaySfx(id, vol);
        }

        // =====================================================================
        // Kamera sarsıntısı: yalnız çizim anında ofset (kameranın gerçek konumuna dokunmaz)
        // =====================================================================

        private void Shake(float pixels, float seconds)
        {
            if (seconds <= 0f || pixels <= 0f) return;
            if (shakeTime < shakeDuration && pixels < shakeAmp * (1f - shakeTime / shakeDuration)) return;
            shakeAmp = pixels; shakeDuration = seconds; shakeTime = 0f;
        }

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (shakeTime >= shakeDuration || cam != Camera.main) return;
            float k = 1f - shakeTime / shakeDuration;
            float a = shakeAmp * k * k / 64f;
            float t = Time.time * 60f;
            shakeApplied = new Vector3(Mathf.Round(Mathf.Sin(t * 1.7f) * 1.2f) * a, Mathf.Round(Mathf.Cos(t * 2.3f)) * a * 0.6f, 0f);
            shakeCam = cam;
            cam.transform.position += shakeApplied;
        }

        private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != shakeCam) return;
            cam.transform.position -= shakeApplied;
            shakeApplied = Vector3.zero;
            shakeCam = null;
        }

        // =====================================================================
        // Efekt (basit sprite parçacıkları; ParticleSystem yok, kare hızından bağımsız)
        // =====================================================================

        // Fırtınada ayakta duran ağacın salınımı: iki yavaş dalga + ara sıra sert rüzgâr.
        private float SwayAngle(float t)
        {
            if (idleSway <= 0f) return 0f;
            float gust = Mathf.Max(0f, Mathf.Sin(t * 0.37f)) * 0.6f;
            return (Mathf.Sin(t * 1.3f) * 0.7f + Mathf.Sin(t * 2.9f + 1.1f) * 0.3f) * idleSway * (1f + gust);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (shakeTime < shakeDuration) shakeTime += dt;
            if (!IsPlaying && !HasFallen && tree != null && baseCaptured && idleSway > 0f) SetTree(SwayAngle(Time.time), 0f);

            // Taç yayı (yere değdikten sonra): öne savrulma + yassılma, sönümlenerek düzelir.
            if (canopySpring && (canopy != null || fallenTree != null))
            {
                float rest = RestScale;
                canopyVel += (-canopyAngle * 260f - canopyVel * 13f) * dt;
                canopyAngle += canopyVel * dt;
                canopyScaleVel += ((rest - canopyScale) * 320f - canopyScaleVel * 11f) * dt;
                canopyScale += canopyScaleVel * dt;
                canopyScale = Mathf.Clamp(canopyScale, rest - canopySquash * 1.5f, 1.1f);
                SetCanopy(canopyAngle, canopyScale);
                if (Mathf.Abs(canopyAngle) < 0.05f && Mathf.Abs(canopyVel) < 0.5f && Mathf.Abs(rest - canopyScale) < 0.003f && Mathf.Abs(canopyScaleVel) < 0.02f)
                {
                    canopySpring = false;
                    SetCanopy(0f, rest);
                }
            }

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Age += dt;
                float k = p.Age / p.Life;
                if (k >= 1f)
                {
                    if (p.Sr != null) Destroy(p.Sr.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }
                if (!p.Landed)
                {
                    p.Vel.y -= p.Gravity * dt;
                    p.Vel *= Mathf.Clamp01(1f - p.Drag * dt);
                    Vector2 sway = new Vector2(Mathf.Sin(p.Age * 4.2f + p.Phase) * p.Sway, 0f);
                    p.Pos += (p.Vel + sway) * dt;
                    p.Angle += p.Spin * dt;
                    // Yere konan yaprak/dal: durur, dönmesi biter, sonra söner.
                    if (p.Pos.y <= p.GroundY && p.Vel.y < 0f)
                    {
                        p.Pos.y = p.GroundY;
                        p.Landed = true;
                        p.Age = Mathf.Max(p.Age, p.Life - 1.2f);
                    }
                }
                if (p.Sr == null) continue;
                var tr = p.Sr.transform;
                tr.localPosition = new Vector3(Mathf.Round(p.Pos.x * 64f) / 64f, Mathf.Round(p.Pos.y * 64f) / 64f, 0f);
                tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(p.Angle / 45f) * 45f);
                tr.localScale = Vector3.one * Mathf.Lerp(p.StartScale, p.EndScale, k);
                var c = p.Sr.color;
                c.a = p.Alpha * (k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                p.Sr.color = c;
            }
        }

        private Transform FxRoot()
        {
            if (fxRoot != null) return fxRoot;
            fxRoot = new GameObject("FX").transform;
            fxRoot.SetParent(transform, false);
            return fxRoot;
        }

        private Particle Spawn(Sprite sprite, Vector2 pos, Vector2 vel, float life, int orderOffset = 0)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(FxRoot(), false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = fxSortingOrder + orderOffset;
            var p = new Particle { Sr = sr, Pos = pos, Vel = vel, Life = life, StartScale = 1f, EndScale = 1f, Angle = UnityEngine.Random.Range(0f, 360f) };
            particles.Add(p);
            return p;
        }

        private static float R(float a, float b) => UnityEngine.Random.Range(a, b);

        private Sprite RandomLeaf() => leafSprite2 != null && UnityEngine.Random.value < 0.4f ? leafSprite2 : leafSprite;

        // Tacın içinden kopan yaprak: süzülerek yere iner (yer = çarpma hattının biraz önü).
        private void SpawnFallingLeaf()
        {
            var src = canopy != null ? canopy : tree; // ayrı taç yoksa alan ağacın (dönen gövdenin) uzayında
            if (src == null) return;
            Vector3 local = new Vector3(canopyLeafCenter.x + R(-canopyLeafArea.x, canopyLeafArea.x), canopyLeafCenter.y + R(-canopyLeafArea.y, canopyLeafArea.y), 0f);
            Vector2 at = transform.InverseTransformPoint(src.TransformPoint(local));
            var p = Spawn(RandomLeaf(), at, new Vector2(R(-0.3f, 0.3f), R(-0.2f, 0.25f)), R(3.0f, 4.2f));
            if (p == null) return;
            p.Gravity = 1.4f; p.Drag = 2.0f; p.Sway = R(0.15f, 0.4f); p.Phase = R(0f, 6.28f); p.Spin = R(-260f, 260f);
            p.GroundY = Mathf.Lerp(impactFrom.y, impactTo.y, R(0f, 1f)) - R(0.05f, 0.6f);
            if (UnityEngine.Random.value < 0.3f) p.Sr.color = new Color(0.8f, 0.85f, 0.65f);
        }

        private void SpawnChips(int count)
        {
            if (tree == null) return;
            Vector2 at = treeBasePos + new Vector3(0f, 0.05f, 0f);
            for (int i = 0; i < count; i++)
            {
                var p = Spawn(chipSprite, at + new Vector2(R(-0.12f, 0.12f), R(0f, 0.1f)), new Vector2(R(-1.2f, 1.4f), R(0.9f, 2.2f)), R(0.6f, 1.0f));
                if (p == null) return;
                p.Gravity = 7f; p.Spin = R(-900f, 900f); p.GroundY = treeBasePos.y - R(0.05f, 0.35f);
            }
        }

        private void SpawnImpact()
        {
            // Toz: yere değen alt kenardan (gövde altı + yatan tacın alt silueti) kalkar, zemin boyunca dışa doğru
            // yuvarlanır, yavaşça yükselip büyüyerek söner. Bir kısmı tacın arkasında kalır (derinlik).
            Vector2 mid = (impactFrom + impactTo) * 0.5f;
            for (int i = 0; i < impactDust; i++)
            {
                float k = (i + R(0.1f, 0.9f)) / Mathf.Max(1, impactDust);
                bool underCanopy = k > 0.4f;
                Vector2 at = Vector2.Lerp(impactFrom, impactTo, k) + new Vector2(R(-0.15f, 0.15f), underCanopy ? R(-0.75f, -0.25f) : R(-0.22f, -0.08f));
                Vector2 outward = (at - mid); outward.y = 0f;
                float dirX = Mathf.Abs(outward.x) < 0.05f ? (UnityEngine.Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(outward.x);
                bool behind = UnityEngine.Random.value < 0.35f;
                var p = Spawn(dustSprite, at, new Vector2(dirX * R(0.4f, 1.3f), R(-0.15f, 0.25f)), R(1.3f, 2.2f), behind ? -4 : 1);
                if (p == null) break;
                p.Gravity = -0.12f; // sıcak hava gibi çok yavaş yükselir
                p.Drag = 2.2f; p.StartScale = R(0.6f, 1.0f); p.EndScale = R(2.4f, 3.4f);
                p.Alpha = behind ? R(0.25f, 0.4f) : R(0.3f, 0.5f); p.Spin = R(-30f, 30f);
            }
            // Yapraklar: yukarı savrulur, süzülerek yere konar.
            for (int i = 0; i < impactLeaves; i++)
            {
                float k = R(0.25f, 1f);
                Vector2 at = Vector2.Lerp(impactFrom, impactTo, k) + new Vector2(0f, R(-0.1f, 0.5f));
                var p = Spawn(RandomLeaf(), at, new Vector2(R(-1.1f, 1.1f), R(0.8f, 2.4f)), R(2.2f, 3.6f));
                if (p == null) break;
                p.Gravity = 1.7f; p.Drag = 1.9f; p.Sway = R(0.15f, 0.45f); p.Phase = R(0f, 6.28f); p.Spin = R(-420f, 420f);
                p.GroundY = at.y - R(0.1f, 0.7f);
                if (UnityEngine.Random.value < 0.3f) p.Sr.color = new Color(0.78f, 0.84f, 0.6f);
            }
            // Kırılan ince dallar: daha ağır, hızlı düşer.
            for (int i = 0; i < impactTwigs; i++)
            {
                Vector2 at = Vector2.Lerp(impactFrom, impactTo, R(0.3f, 1f)) + new Vector2(0f, R(0f, 0.3f));
                var p = Spawn(twigSprite, at, new Vector2(R(-1.4f, 1.4f), R(1.0f, 2.2f)), R(1.6f, 2.4f));
                if (p == null) break;
                p.Gravity = 6f; p.Drag = 0.8f; p.Spin = R(-700f, 700f);
                p.GroundY = at.y - R(0.15f, 0.6f);
            }
            SpawnChips(crackChips / 2);
        }

        private void ClearParticles()
        {
            foreach (var p in particles) if (p.Sr != null) Destroy(p.Sr.gameObject);
            particles.Clear();
        }

        private void OnValidate() => fallCurve = null;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(transform.TransformPoint(impactFrom), transform.TransformPoint(impactTo));
        }
    }
}
