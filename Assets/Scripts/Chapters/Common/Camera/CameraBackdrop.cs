using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Kameraya göre duran arka plan (ör. sokaktaki bulutlu gökyüzü bandı): dikeyde kamerayla birlikte gider,
    /// yatayda parallax kadar geride kalır (0 = kamerayla aynı, 1 = dünyada sabit). Konum piksel ızgarasına yuvarlanır.
    /// Odanın kökünün altında durur; oda kapalıyken çalışmaz.
    /// </summary>
    public class CameraBackdrop : MonoBehaviour
    {
        [Tooltip("Kamera merkezine göre konum (dünya birimi)")]
        [SerializeField] private Vector2 offset = new Vector2(0f, -1.5f);
        [Tooltip("Yatay parallax: 0 = kamerayla birlikte, 1 = dünyada sabit")]
        [Range(0f, 1f)]
        [SerializeField] private float parallaxX = 0.3f;
        [Tooltip("Parallax'ın sıfır olduğu kamera x konumu (dünya)")]
        [SerializeField] private float referenceX;
        [SerializeField] private float pixelsPerUnit = 64f;

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            float x = c.x + offset.x - (c.x - referenceX) * parallaxX;
            float y = c.y + offset.y;
            if (pixelsPerUnit > 0f)
            {
                x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
                y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
            }
            transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
