using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// İzometrik oyuncu: W/A/S/D (veya oklar) ile 8 yöne yürür. Rigidbody2D sayesinde duvar ve eşya
    /// collider'larına çarpar, kaymadan durur. Diyalog, haber ve karar sırasında bölüm akışı CanMove = false yapar.
    /// Pivot ayaktadır: derinlik sıralaması ve etkileşim mesafesi ayak noktasına göre hesaplanır.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class IsoPlayerController : MonoBehaviour
    {
        [Tooltip("Yürüme hızı (dünya birimi / sn)")]
        [SerializeField] private float speed = 2f;
        [Tooltip("Dikey hız çarpanı: izometrik zeminde yukarı-aşağı yürüme daha kısa görünür (1 = yatayla eşit)")]
        [SerializeField] private float verticalSpeedScale = 0.75f;
        [SerializeField] private IsoCharacterAnimator characterAnimator;

        private Rigidbody2D body;
        private Vector2 velocity;

        /// <summary>false iken oyuncu yerinde durur (diyalog, karar, geçişler).</summary>
        public bool CanMove { get; set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (characterAnimator == null) characterAnimator = GetComponent<IsoCharacterAnimator>();
        }

        private void Update()
        {
            Vector2 input = CanMove ? GameInput.MoveVector : Vector2.zero;
            velocity = new Vector2(input.x, input.y * verticalSpeedScale) * speed;
            if (characterAnimator != null) characterAnimator.SetMotion(input);
        }

        private void FixedUpdate()
        {
            body.linearVelocity = velocity;
        }

        private void OnDisable()
        {
            velocity = Vector2.zero;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        /// <summary>Oyuncuyu ışınlar ve yüzünü verilen yöne çevirir (oda değişince).</summary>
        public void PlaceAt(Vector3 position, Vector2 facing)
        {
            velocity = Vector2.zero;
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
