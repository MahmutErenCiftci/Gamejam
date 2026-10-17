using System;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Sokaktaki su birikintisi / rögar tehlike alanı (Mahmut). Bir trigger Collider2D ister (zemin izi elipsi).
    /// Çocuk (IsoPlayerController taşıyan collider) alana adım atınca: hareketi kısa süre kapanır (~0.4 sn, sonra
    /// önceki değerine döner), alanın dışındaki son güvenli noktaya geri itilir ve Triggered olayı tetiklenir.
    /// Uyarı diyaloğunu (WarningId: F05_PUDDLE_WARNING ya da rögar için F05_DRAIN_WARNING) bölüm akışı oynatır;
    /// bu bileşenin içinde diyalog yoktur. Ceza yok, eğitici geri bildirim (OYUN_AKISI §6, AGENTS §5).
    /// İsteğe bağlı görsel: birikinti sprite'ı sağanakta büyür, yağmur yağarken yüzeyde halkalar açılır.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PuddleHazard : MonoBehaviour
    {
        public const string PuddleWarningId = "F05_PUDDLE_WARNING";
        public const string DrainWarningId = "F05_DRAIN_WARNING";

        [Tooltip("Bölüm akışının oynatacağı uyarı diyaloğu: su birikintisi F05_PUDDLE_WARNING, rögar F05_DRAIN_WARNING")]
        [SerializeField] private string warningId = PuddleWarningId;
        [Tooltip("Kapalıysa alan çocuğu durdurmaz")]
        [SerializeField] private bool hazardEnabled = true;

        [Header("Geri itme")]
        [Tooltip("Çocuğun hareketinin kapalı kaldığı süre (sn); sonra önceki değerine döner")]
        [SerializeField] private float blockSeconds = 0.4f;
        [Tooltip("Geri itme mesafesi: girdiği yerden en az bu kadar geriye (zemin uzaklığı, dünya birimi)")]
        [SerializeField] private float pushDistance = 0.35f;
        [Tooltip("Geri kayma süresi (sn)")]
        [SerializeField] private float pushSeconds = 0.15f;

        [Header("Zemin izi (collider)")]
        [Tooltip("Elips zemin izi (hücre): x = grid X ekseni boyunca, y = grid Y ekseni boyunca. Bağlam menüsünden ya da FitColliderToFootprint ile PolygonCollider2D'ye yazılır.")]
        [SerializeField] private Vector2 footprintCells = new Vector2(1f, 0.55f);
        [Tooltip("Trigger elipsinin zemin izine oranı. 0.85: çocuk suya gerçekten basınca tetiklenir, kenarını sıyırınca değil " +
                 "(çocuğun ayak collider'ı ~0.56 hücre genişliğinde bir geçit ister).")]
        [Range(0.3f, 1.2f)] [SerializeField] private float colliderScale = 0.85f;

        [Header("Görsel (isteğe bağlı)")]
        [Tooltip("Birikinti sprite'ı (order -90); sağanakta büyür. Rögar alanında boş bırakılabilir.")]
        [SerializeField] private SpriteRenderer visual;
        [Tooltip("Yağmur yoğunluğu 1'de görselin büyüme oranı (0.15 = %15); 0.5 yoğunluktan sonra başlar")]
        [SerializeField] private float growAtFullRain = 0.15f;
        [Tooltip("Yağmur halkaları (order -89). Kendisi doğurmaz; bu bileşen yağmura göre doğurur.")]
        [SerializeField] private ParticleSystem ripples;
        [Tooltip("Tam yoğunlukta saniyede açılan halka")]
        [SerializeField] private float ripplesAtFullRain = 4f;
        [Tooltip("Çocuk adım atınca adım noktasında açılan halka")]
        [SerializeField] private int stepRipples = 2;
        [Tooltip("Yağmur; boşsa sahnede aranır")]
        [SerializeField] private RainController rain;

        [Header("Olaylar")]
        [Tooltip("Çocuk alana adım attığında (ses kancası: şıp)")]
        [SerializeField] private UnityEvent onTriggered = new UnityEvent();

        /// <summary>Çocuk herhangi bir tehlike alanına adım attığında (geri itme başlarken) tetiklenir.</summary>
        public static event Action<PuddleHazard> Triggered;

        private Collider2D area;
        private Vector3 visualBaseScale = Vector3.one;
        private float appliedGrow = -1f;
        private float rippleBudget;

        /// <summary>Bölüm akışının oynatacağı uyarı diyaloğunun ID'si.</summary>
        public string WarningId => warningId;

        /// <summary>Alan açık mı? Kapalıyken çocuğu durdurmaz.</summary>
        public bool HazardEnabled
        {
            get => hazardEnabled;
            set => hazardEnabled = value;
        }

        /// <summary>Alanın trigger collider'ı.</summary>
        public Collider2D Area
        {
            get
            {
                if (area == null) area = GetComponent<Collider2D>();
                return area;
            }
        }

        /// <summary>Alanı en son tetikleyen çocuk (yoksa null).</summary>
        public IsoPlayerController LastPlayer { get; private set; }

        /// <summary>Bu alan kaç kez tetiklendi.</summary>
        public int TriggerCount { get; private set; }

        public UnityEvent OnTriggered => onTriggered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Triggered = null;
        }

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            area = GetComponent<Collider2D>();
            if (area != null && !area.isTrigger)
            {
                Debug.LogWarning($"[PuddleHazard] {name}: collider trigger değildi, trigger yapıldı.", this);
                area.isTrigger = true;
            }
            if (visual != null) visualBaseScale = visual.transform.localScale;
        }

        private void Start()
        {
            if (rain == null) rain = FindAnyObjectByType<RainController>();
            // İzleyici ilk adımdan önce kayıt tutmaya başlasın (son güvenli nokta hazır olsun).
            var player = FindAnyObjectByType<IsoPlayerController>();
            if (player != null) PlayerHazardTracker.For(player);
        }

        private void OnDisable()
        {
            if (LastPlayer != null)
            {
                var tracker = LastPlayer.GetComponent<PlayerHazardTracker>();
                if (tracker != null) tracker.NotifyExit(this);
            }
        }

        private void Update()
        {
            float r = rain != null && rain.isActiveAndEnabled ? rain.Intensity : 0f;
            UpdateGrowth(r);
            if (ripples == null || r <= 0f || ripplesAtFullRain <= 0f) return;

            rippleBudget += ripplesAtFullRain * r * Time.deltaTime;
            while (rippleBudget >= 1f)
            {
                rippleBudget -= 1f;
                EmitRipple(RandomPointOnWater());
            }
        }

        private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);

        // Alan açıldığında çocuk zaten içindeyse ya da itme bittiğinde hâlâ içerideyse de çalışsın.
        private void OnTriggerStay2D(Collider2D other) => HandleContact(other);

        private void OnTriggerExit2D(Collider2D other)
        {
            var player = PlayerFrom(other);
            if (player == null) return;
            var tracker = player.GetComponent<PlayerHazardTracker>();
            if (tracker != null) tracker.NotifyExit(this);
        }

        private void HandleContact(Collider2D other)
        {
            var player = PlayerFrom(other);
            if (player == null) return;
            var tracker = PlayerHazardTracker.For(player);
            tracker.NotifyEnter(this);
            if (!hazardEnabled || !isActiveAndEnabled || tracker.IsPushing || tracker.Suppressed) return;
            if (!tracker.PushBack(this, blockSeconds, pushDistance, pushSeconds)) return;

            LastPlayer = player;
            TriggerCount++;
            for (int i = 0; i < stepRipples; i++)
                EmitRipple((Vector2)player.transform.position + UnityEngine.Random.insideUnitCircle * 0.06f);
            Debug.Log($"[PuddleHazard] {name} → {warningId} ({TriggerCount}. kez)", this);
            onTriggered.Invoke();
            Triggered?.Invoke(this);
        }

        /// <summary>
        /// Son tetiklemedeki hareket kilidi bitince CanMove geri açılmasın: uyarı diyaloğu sırasında çocuğu
        /// durdurmak isteyen bölüm akışı Triggered içinde çağırır, sonra CanMove'u kendisi açar.
        /// </summary>
        public void CancelInputRestore()
        {
            if (LastPlayer == null) return;
            var tracker = LastPlayer.GetComponent<PlayerHazardTracker>();
            if (tracker != null) tracker.CancelRestore();
        }

        /// <summary>footprintCells × colliderScale elipsini (16 köşe) PolygonCollider2D'ye yazar ve trigger yapar.</summary>
        [ContextMenu("Collider'ı zemin izine göre kur")]
        public void FitColliderToFootprint()
        {
            var poly = GetComponent<PolygonCollider2D>();
            if (poly == null) return;
            Vector2 ax = new Vector2(0.5f, 0.25f) * (footprintCells.x * 0.5f * colliderScale);
            Vector2 ay = new Vector2(-0.5f, 0.25f) * (footprintCells.y * 0.5f * colliderScale);
            const int corners = 16;
            var points = new Vector2[corners];
            for (int i = 0; i < corners; i++)
            {
                float a = i * Mathf.PI * 2f / corners;
                points[i] = ax * Mathf.Cos(a) + ay * Mathf.Sin(a);
            }
            poly.points = points;
            poly.isTrigger = true;
        }

        private void UpdateGrowth(float rainIntensity)
        {
            if (visual == null || growAtFullRain == 0f) return;
            float t = Mathf.Clamp01((rainIntensity - 0.5f) / 0.5f);
            float grow = growAtFullRain * t * t * (3f - 2f * t);
            if (Mathf.Abs(grow - appliedGrow) < 0.002f) return;
            appliedGrow = grow;

            // Piksel boyutları tam kalsın: genişlik/yükseklik tam piksele yuvarlanır.
            Vector2 px = visual.sprite != null ? visual.sprite.rect.size : new Vector2(32f, 16f);
            float sx = Mathf.Round(px.x * (1f + grow)) / Mathf.Max(1f, px.x);
            float sy = Mathf.Round(px.y * (1f + grow)) / Mathf.Max(1f, px.y);
            visual.transform.localScale = new Vector3(visualBaseScale.x * sx, visualBaseScale.y * sy, visualBaseScale.z);
        }

        private Vector2 RandomPointOnWater()
        {
            Bounds b = visual != null ? visual.bounds : Area.bounds;
            Vector2 e = b.extents * 0.7f;
            Vector2 u = UnityEngine.Random.insideUnitCircle;
            return (Vector2)b.center + new Vector2(u.x * e.x, u.y * e.y);
        }

        private void EmitRipple(Vector2 position)
        {
            if (ripples == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = new Vector3(Mathf.Round(position.x * 64f) / 64f, Mathf.Round(position.y * 64f) / 64f, 0f),
                applyShapeToPosition = false
            };
            ripples.Emit(p, 1);
        }

        private static IsoPlayerController PlayerFrom(Collider2D other)
        {
            if (other == null) return null;
            var rb = other.attachedRigidbody;
            var player = rb != null ? rb.GetComponent<IsoPlayerController>() : null;
            if (player == null) player = other.GetComponentInParent<IsoPlayerController>();
            return player;
        }

        // Sahne görünümünde zemin izini (izometrik elips) çizer.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = hazardEnabled ? new Color(0.2f, 0.6f, 1f) : Color.gray;
            Vector2 ax = new Vector2(0.5f, 0.25f) * (footprintCells.x * 0.5f * colliderScale);
            Vector2 ay = new Vector2(-0.5f, 0.25f) * (footprintCells.y * 0.5f * colliderScale);
            Vector3 c = transform.position;
            Vector3 previous = c + (Vector3)ax;
            for (int i = 1; i <= 32; i++)
            {
                float a = i * Mathf.PI * 2f / 32f;
                Vector3 next = c + (Vector3)(ax * Mathf.Cos(a) + ay * Mathf.Sin(a));
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
