using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Sağlam masa işareti (çalışma masası, yemek masası). Sarsıntı başladığında oyuncu bu masaya zemin
    /// uzaklığı olarak radius kadar yakınsa Çök–Kapan–Tutun masanın altında yapılır (koşmak gerekmez; uzaksa yerinde).
    /// hideSpot: Cenk'in masanın altına geçeceği nokta (boşsa masanın kendi konumu).
    /// approachPoint: otomatik testte oyuncunun bekleyeceği yer (boşsa masanın 0.5 birim önü).
    /// </summary>
    public class SturdyTable : MonoBehaviour
    {
        [Tooltip("Oyuncu bu zemin uzaklığı içindeyse masa altında korunur (dünya birimi)")]
        [SerializeField] private float radius = 1.2f;
        [Tooltip("Masanın altındaki nokta (Cenk buraya kayar). Boşsa masanın konumu.")]
        [SerializeField] private Transform hideSpot;
        [Tooltip("Masa altında bakılacak yön (ekran yönü)")]
        [SerializeField] private Vector2 hideFacing = new Vector2(0f, -1f);
        [Tooltip("Otomatik test: oyuncunun sarsıntıyı bekleyeceği nokta. Boşsa masanın 0.5 birim önü (güney).")]
        [SerializeField] private Transform approachPoint;

        public float Radius => radius;
        public Vector2 HideFacing => hideFacing;
        public Vector3 HidePosition => hideSpot != null ? hideSpot.position : transform.position;
        public Vector3 ApproachPosition => approachPoint != null ? approachPoint.position : transform.position + new Vector3(0f, -0.5f, 0f);

        /// <summary>İzometrik zemin uzaklığı (2:1'de dikey fark 2 ile çarpılır; IsoPlayerController.GroundDistanceTo ile aynı).</summary>
        public float GroundDistance(Vector3 point)
        {
            Vector2 d = point - transform.position;
            return Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y);
        }

        public bool IsNear(Vector3 point) => GroundDistance(point) <= radius;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Vector3 c = transform.position;
            const int segments = 32;
            Vector3 previous = c + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var next = c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.5f, 0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
            Gizmos.DrawWireSphere(HidePosition, 0.08f);
        }
    }
}
