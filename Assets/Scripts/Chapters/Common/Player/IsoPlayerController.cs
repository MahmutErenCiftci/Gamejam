using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// İzometrik oyuncu: W/A/S/D (veya oklar) ile 8 yöne yürür; hız ivmelenerek artar ve yavaşlayarak durur.
    /// Rigidbody2D sayesinde duvar ve eşya collider'larına çarpar. Diyalog, haber ve karar sırasında bölüm akışı
    /// CanMove = false yapar. Pivot ayaktadır: derinlik sıralaması ve etkileşim mesafesi ayak noktasına göre hesaplanır.
    /// Animasyon yönü hızdan değil girdiden gelir; duvara doğru basılınca da o yöne bakıp yürür.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class IsoPlayerController : MonoBehaviour
    {
        [Tooltip("Yürüme hızı (dünya birimi / sn)")]
        [SerializeField] private float speed = 2f;
        [Tooltip("Dikey hız çarpanı. 0.5 ile çapraz girdi tam 2:1 izometrik eksen boyunca ilerler ve zemindeki hız 8 yönde de eşit olur (1 = yatayla eşit ekran hızı)")]
        [SerializeField] private float verticalSpeedScale = 0.5f;
        [Tooltip("Hedef hıza doğru ivmelenme (birim / sn²)")]
        [SerializeField] private float acceleration = 30f;
        [Tooltip("Tuş bırakılınca yavaşlama (birim / sn²)")]
        [SerializeField] private float deceleration = 40f;
        [SerializeField] private IsoCharacterAnimator characterAnimator;
        [Tooltip("Açıksa oyuncu sahne başlar başlamaz yürüyebilir (test sahneleri için). Bölümlerde kapalı kalır, CanMove'u bölüm akışı açar.")]
        [SerializeField] private bool controlOnStart;

        private Rigidbody2D body;
        private Vector2 targetVelocity;

        /// <summary>false iken oyuncu yavaşlayıp durur ve bekleme animasyonuna geçer (diyalog, karar, geçişler).</summary>
        public bool CanMove { get; set; }

        /// <summary>Hız çarpanı (1 = normal). Ör. akşam sel bölümünde rüzgâr geçidi hafifçe yavaşlatır; etkiyi veren geri 1'e koyar.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Dışarıdan verilen hız (dünya birimi / sn), girdiden gelen hıza eklenir; CanMove false iken de uygulanır.
        /// Ör. akşam sel rüzgâr koridoru Cenk'i koridor boyunca yürütür ya da geri savurur. Çarpışmalar geçerli kalır; etkiyi veren geri sıfırlar.</summary>
        public Vector2 ExternalVelocity { get; set; }

        /// <summary>Rigidbody'nin anlık hızı (sadece okuma; testler için).</summary>
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (characterAnimator == null) characterAnimator = GetComponent<IsoCharacterAnimator>();
        }

        private void Start()
        {
            if (controlOnStart) CanMove = true;
        }

        private void Update()
        {
            Vector2 input = CanMove ? GameInput.MoveVector : Vector2.zero;
            targetVelocity = new Vector2(input.x, input.y * verticalSpeedScale) * (speed * Mathf.Clamp(SpeedMultiplier, 0f, 2f)) + ExternalVelocity;
            Vector2 motion = input;
            if (motion.sqrMagnitude <= 0.0001f && ExternalVelocity.sqrMagnitude > 0.0001f)
                motion = new Vector2(ExternalVelocity.x, ExternalVelocity.y / Mathf.Max(0.01f, verticalSpeedScale));
            if (characterAnimator != null) characterAnimator.SetMotion(motion);
        }

        private void FixedUpdate()
        {
            Vector2 current = body.linearVelocity;
            float rate = targetVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            body.linearVelocity = Vector2.MoveTowards(current, targetVelocity, rate * Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            targetVelocity = Vector2.zero;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        /// <summary>Oyuncuyu ışınlar ve yüzünü verilen yöne çevirir (oda değişince).</summary>
        public void PlaceAt(Vector3 position, Vector2 facing)
        {
            targetVelocity = Vector2.zero;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = position;
            }
            transform.position = position;
            if (characterAnimator != null)
            {
                characterAnimator.SetMotion(Vector2.zero);
                characterAnimator.Face(facing);
            }
        }

        /// <summary>Ayak noktası ile hedef arasındaki zemin uzaklığı (2:1 izometrikte dikey fark 2 ile çarpılır).</summary>
        public float GroundDistanceTo(Vector3 point)
        {
            Vector2 d = point - transform.position;
            return Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y);
        }
    }
}
