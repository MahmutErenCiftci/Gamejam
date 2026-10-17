using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Kamera takip alanı (ör. sokak): kamera oyuncuyu takip ederken görüntü bu dikdörtgenin dışına çıkmaz.
    /// Dikdörtgen bu objenin konumuna ortalanır; boyutu dünya birimidir (PPU 64 → 1 birim = 64 px).
    /// Alan ekrandan darsa kamera o eksende alanın ortasında kalır. Adım görselinde (StepVisual.cameraBounds) kullanılır.
    /// </summary>
    public class CameraBounds : MonoBehaviour
    {
        [Tooltip("Görüntünün kalacağı alanın boyutu (dünya birimi). 640x360 / PPU 64 → ekran 10 x 5.625 birim.")]
        [SerializeField] private Vector2 size = new Vector2(20f, 8f);

        public Vector2 Size => size;

        /// <summary>Alanın dünya koordinatlarındaki dikdörtgeni.</summary>
        public Rect WorldRect
        {
            get
            {
                Vector2 center = transform.position;
                return new Rect(center - size * 0.5f, size);
            }
        }

        /// <summary>Kodla kurmak için (kurulum scriptleri).</summary>
        public void SetSize(Vector2 value) => size = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            var r = WorldRect;
            Gizmos.DrawWireCube(r.center, new Vector3(r.width, r.height, 0f));
        }
    }
}
