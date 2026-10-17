using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Odadaki top: oyuncu (IsoPlayerController) çarpınca yürüdüğü yöne doğru yuvarlanır, yavaşlar ve durur.
    /// Duvar ve eşya collider'larından zemin (2:1 izometrik) uzayında yansıyarak seker; böylece izometrik
    /// duvara dik gelen top dümdüz geri döner. Piksel sanatı döndürülmez: yuvarlanma hissi ayna çevirme
    /// (flipX) ile verilir, vuruşta kısa bir "ezilme" olur, durunca konum 1/64 birime (1 piksel) oturur.
    /// Pivot topun yere değdiği noktadadır; collider bu noktada yatay bir kapsüldür (ayak izi).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class KickableBall : MonoBehaviour
    {
        [Header("Fizik")]
        [Tooltip("Topun kütlesi; oyuncuya göre çok hafif olmalı ki oyuncu takılmasın")]
        [SerializeField] private float mass = 0.1f;
        [Tooltip("Yuvarlanırken yavaşlama (Rigidbody2D.linearDamping)")]
        [SerializeField] private float rollDamping = 2.5f;
        [Tooltip("Duvar/eşyadan sekme katsayısı (0 = yapışır, 1 = hiç hız kaybetmez). Zemin uzayında uygulanır.")]
        [SerializeField, Range(0f, 1f)] private float wallBounciness = 0.6f;
        [Tooltip("Ayak izi collider'ı (yatay kapsül), dünya birimi")]
        [SerializeField] private Vector2 footprintSize = new Vector2(0.22f, 0.11f);

        [Header("Vuruş")]
        [Tooltip("Top hızı = oyuncunun zemin hızı × bu çarpan")]
        [SerializeField] private float kickMultiplier = 1.8f;
        [Tooltip("En düşük / en yüksek vuruş hızı (zemin birimi / sn)")]
        [SerializeField] private float minKickSpeed = 1.2f;
        [SerializeField] private float maxKickSpeed = 4.5f;
        [Tooltip("Oyuncu yönü ile 'oyuncudan uzağa' yönünün karışımı (0 = sadece yürüme yönü, 1 = sadece uzağa)")]
        [SerializeField, Range(0f, 1f)] private float awayBlend = 0.3f;
        [Tooltip("İki vuruş arası en kısa süre (oyuncu topa yaslanıp yürürken)")]
        [SerializeField] private float kickCooldown = 0.2f;

        [Header("Görsel")]
        [Tooltip("Bu kadar zemin yolu gidince bir sonraki yuvarlanma karesine geçilir (flipX/flipY)")]
        [SerializeField] private float rollStep = 0.12f;
        [Tooltip("Vuruşta kısa ezilme (piksel ölçüsüne yuvarlanmış ölçek)")]
        [SerializeField] private bool squashOnKick = true;
        [Tooltip("Bu hızın altında top durur ve konumu piksele oturur")]
        [SerializeField] private float stopSpeed = 0.06f;

        private const float PixelsPerUnit = 64f;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Vector2 lastVelocity;
        private float lastKickTime = -10f;
        private float rolledDistance;
        private int rollFrame;
        private Vector3 baseScale;
        private Coroutine squashRoutine;

        /// <summary>Topun anlık hızı (testler için).</summary>
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        /// <summary>Top şu an yuvarlanıyor mu?</summary>
        public bool IsRolling => body != null && body.linearVelocity.sqrMagnitude > 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.mass = mass;
            body.linearDamping = rollDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var footprint = GetComponent<CapsuleCollider2D>();
            footprint.direction = CapsuleDirection2D.Horizontal;
            footprint.size = footprintSize;
            footprint.offset = Vector2.zero;
            footprint.isTrigger = false;
            // Sekme zemin uzayında elle hesaplanır; malzeme sürtünmesiz ve sekmesiz olmalı ki
            // Box2D'nin ekran uzayı yansıması ve oyuncuyla temas titreşim yaratmasın.
            footprint.sharedMaterial = new PhysicsMaterial2D("KickableBall_Runtime") { friction = 0f, bounciness = 0f };
        }

        private void FixedUpdate()
        {
            Vector2 v = body.linearVelocity;
            if (v.sqrMagnitude > 0f && Time.time - lastKickTime > 0.05f && GroundLength(v) < stopSpeed)
            {
                body.linearVelocity = Vector2.zero;
                body.position = Snap(body.position);
                v = Vector2.zero;
            }
            lastVelocity = v;
        }

        private void Update()
        {
            if (spriteRenderer == null || rollStep <= 0f || body == null) return;
            rolledDistance += GroundLength(body.linearVelocity) * Time.deltaTime;
            while (rolledDistance >= rollStep)
            {
                rolledDistance -= rollStep;
                rollFrame = (rollFrame + 1) % 2;
                // Sadece flipX: pivot yatayda ortada olduğu için top yerinden oynamaz. flipY kullanılmaz,
                // pivot alttan yukarıda (ayak noktası) olduğundan dikey ayna topu birkaç piksel aşağı kaydırır.
                spriteRenderer.flipX = rollFrame == 1;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (TryKick(collision)) return;
            Bounce(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryKick(collision);
        }

        private bool TryKick(Collision2D collision)
        {
            var player = collision.collider.GetComponentInParent<IsoPlayerController>();
            if (player == null) return false;
            if (Time.time - lastKickTime < kickCooldown) return true;

            Vector2 playerVelocity = player.Velocity;
            Vector2 away = ToGround((Vector2)transform.position - (Vector2)player.transform.position);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;

            Vector2 moveGround = ToGround(playerVelocity);
            float playerGroundSpeed = moveGround.magnitude;
            Vector2 dir;
            if (playerGroundSpeed > 0.3f)
            {
                Vector2 move = moveGround / playerGroundSpeed;
                // Oyuncu topa doğru yürümüyorsa (yanından sürtünüyor/uzaklaşıyor) vuruş yok
                if (Vector2.Dot(move, away) < 0.1f) return true;
                dir = (move * (1f - awayBlend) + away * awayBlend).normalized;
            }
            else
            {
                if (collision.relativeVelocity.sqrMagnitude < 0.01f) return true;
                dir = away;
            }

            float kickSpeed = Mathf.Clamp(playerGroundSpeed * kickMultiplier, minKickSpeed, maxKickSpeed);
            body.linearVelocity = FromGround(dir * kickSpeed);
            body.WakeUp();
            lastKickTime = Time.time;
            if (squashOnKick && isActiveAndEnabled)
            {
                if (squashRoutine != null) StopCoroutine(squashRoutine);
                squashRoutine = StartCoroutine(Squash());
            }
            return true;
        }

        /// <summary>Duvar/eşyadan zemin uzayında yansıma: izometrik duvara dik gelen top dümdüz geri döner.</summary>
        private void Bounce(Collision2D collision)
        {
            if (collision.contactCount == 0) return;
            Vector2 vg = ToGround(lastVelocity);
            if (vg.sqrMagnitude < 0.0001f) return;

            Vector2 n = collision.GetContact(0).normal;
            // Ekran uzayı normali -> zemin uzayı normali (teğet (-ny, nx) zeminde (-ny, 2nx) olur)
            Vector2 ng = new Vector2(2f * n.x, n.y).normalized;
            float into = Vector2.Dot(vg, ng);
            if (into > 0f) { ng = -ng; into = -into; }
            if (into > -0.0001f) return;

            Vector2 reflected = vg - (1f + wallBounciness) * into * ng;
            body.linearVelocity = FromGround(reflected);
        }

        private IEnumerator Squash()
        {
            // 18 px top: 20×16 px'e ezilir, sonra 19×17, sonra eski hali (tam piksel ölçüleri)
            float w = spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.rect.width : 18f;
            float h = spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.rect.height : 18f;
            transform.localScale = Vector3.Scale(baseScale, new Vector3((w + 2f) / w, (h - 2f) / h, 1f));
            yield return new WaitForSeconds(0.06f);
            transform.localScale = Vector3.Scale(baseScale, new Vector3((w + 1f) / w, (h - 1f) / h, 1f));
            yield return new WaitForSeconds(0.06f);
            transform.localScale = baseScale;
            squashRoutine = null;
        }

        private void OnDisable()
        {
            if (squashRoutine != null) { StopCoroutine(squashRoutine); squashRoutine = null; }
            transform.localScale = baseScale;
        }

        private static Vector2 ToGround(Vector2 screen) => new Vector2(screen.x, screen.y * 2f);
        private static Vector2 FromGround(Vector2 ground) => new Vector2(ground.x, ground.y * 0.5f);
        private static float GroundLength(Vector2 screen) => ToGround(screen).magnitude;

        private static Vector2 Snap(Vector2 p) =>
            new Vector2(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit);
    }
}
