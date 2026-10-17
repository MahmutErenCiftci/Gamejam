using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Kapı / geçiş noktası (aynı objedeki Interactable ile kullanılır). EarthquakeModeController kaydeder:
    /// E'ye basılınca sayaç durur → ekran kararır → oyuncu hedef odanın spawn noktasına konur → kamera hedef odaya → açılır.
    ///  • Room: oda kapısı (yatak odası ↔ salon), her evrede açık.
    ///  • ApartmentExit: daire kapısı. Sarsıntıdan ÖNCE kilitli: "Sarsıntı sırasında dışarı koşulmaz" bilgisi (after.doorLockedBefore);
    ///    SONRA açılır → koridor (çanta alınmadıysa ilk denemede hatırlatma).
    ///  • StairsDown: koridordaki merdiven (sadece sarsıntı sonrası) → toplanma alanı.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class EarthquakeDoor : MonoBehaviour
    {
        /// <summary>Kapı türü.</summary>
        public enum DoorKind { Room, ApartmentExit, StairsDown }

        [SerializeField] private DoorKind kind = DoorKind.Room;
        [Tooltip("Gidilecek oda")]
        [SerializeField] private EarthquakeRoom targetRoom;
        [Tooltip("Hedef odadaki giriş noktası (boşsa odanın varsayılan spawn'ı)")]
        [SerializeField] private Transform targetSpawn;
        [Tooltip("Girişte bakılacak yön (sıfırsa odanın varsayılanı)")]
        [SerializeField] private Vector2 arriveFacing;
        [Tooltip("İpucu yazısı (kapı açıkken)")]
        [SerializeField] private string label = "Geç";
        [Tooltip("İpucu yazısı (kapı kilitliyken; ApartmentExit sarsıntı öncesi)")]
        [SerializeField] private string lockedLabel = "Kapıya bak";

        private Interactable point;

        public DoorKind Kind => kind;
        public EarthquakeRoom TargetRoom => targetRoom;
        public Transform TargetSpawn => targetSpawn != null ? targetSpawn : (targetRoom != null ? targetRoom.DefaultSpawn : null);
        public Vector2 ArriveFacing => arriveFacing.sqrMagnitude > 0.0001f ? arriveFacing : (targetRoom != null ? targetRoom.DefaultFacing : new Vector2(0f, -1f));
        public string Label => label;
        public string LockedLabel => lockedLabel;

        public Interactable Point
        {
            get
            {
                if (point == null) point = GetComponent<Interactable>();
                return point;
            }
        }

        /// <summary>Kodla kurulum (sahne kurulum scripti için).</summary>
        public void Configure(DoorKind doorKind, EarthquakeRoom room, Transform spawn, string openLabel)
        {
            kind = doorKind;
            targetRoom = room;
            targetSpawn = spawn;
            if (!string.IsNullOrEmpty(openLabel)) label = openLabel;
        }
    }
}
