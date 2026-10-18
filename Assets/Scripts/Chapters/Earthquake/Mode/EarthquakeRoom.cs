using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Evdeki bir alan (Rooms/Bedroom, Rooms/Living, Rooms/Stairwell, Rooms/Assembly). Odalar aynı sahnede farklı
    /// dünya konumlarında durur; kapıdan geçince ekran kararır, oyuncu spawn noktasına konur, kamera cameraAnchor'a
    /// kesilir (ya da cameraBounds verilmişse oyuncuyu bu alanda takip eder).
    /// </summary>
    public class EarthquakeRoom : MonoBehaviour
    {
        /// <summary>Alan kimliği.</summary>
        public enum RoomId { Bedroom, Living, Stairwell, Assembly }

        [SerializeField] private RoomId roomId = RoomId.Bedroom;
        [Tooltip("Kameranın ortalanacağı nokta (boşsa bu obje; altındaki 'CameraAnchor' adlı obje de bulunur)")]
        [SerializeField] private Transform cameraAnchor;
        [Tooltip("İsteğe bağlı: verilirse kamera oyuncuyu bu alan içinde takip eder (ChapterCamera gerekir)")]
        [SerializeField] private CameraBounds cameraBounds;
        [Tooltip("Varsayılan giriş noktası (boşsa altındaki 'Spawn' adlı obje, o da yoksa oda merkezi)")]
        [SerializeField] private Transform defaultSpawn;
        [Tooltip("Girişte bakılacak yön (ekran yönü)")]
        [SerializeField] private Vector2 defaultFacing = new Vector2(0f, -1f);

        public RoomId Id => roomId;
        public CameraBounds Bounds => cameraBounds;
        public Vector2 DefaultFacing => defaultFacing;

        public Transform CameraAnchor
        {
            get
            {
                if (cameraAnchor == null) cameraAnchor = FindChild("CameraAnchor");
                return cameraAnchor != null ? cameraAnchor : transform;
            }
        }

        public Transform DefaultSpawn
        {
            get
            {
                if (defaultSpawn == null) defaultSpawn = FindChild("Spawn");
                return defaultSpawn != null ? defaultSpawn : transform;
            }
        }

        /// <summary>Altındaki adı verilen objeyi (kapalılar dahil, derinlemesine) bulur; yoksa null.</summary>
        public Transform FindChild(string childName)
        {
            if (string.IsNullOrEmpty(childName)) return null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name == childName) return t;
            return null;
        }

        /// <summary>Verilen nokta bu odanın altında mı?</summary>
        public bool Owns(Component c) => c != null && c.transform.IsChildOf(transform);
    }
}
