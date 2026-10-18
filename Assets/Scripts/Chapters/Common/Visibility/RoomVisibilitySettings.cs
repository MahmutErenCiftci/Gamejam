using UnityEngine;

namespace AfetUyanisi.Chapters.Visibility
{
    /// <summary>
    /// Oda görünürlüğü (Project Zomboid tarzı karartma) ve dinamik duvar kesme ayarları (Mahmut).
    /// Varsayılan kopya: Assets/Resources/RoomVisibility/RoomVisibilitySettings.asset — Inspector'dan değiştir.
    /// Asset yoksa koddaki varsayılanlar kullanılır (oyun bozulmaz).
    /// </summary>
    [CreateAssetMenu(fileName = "RoomVisibilitySettings", menuName = "Afet Uyanisi/Oda Gorunurlugu Ayarlari")]
    public class RoomVisibilitySettings : ScriptableObject
    {
        public const string ResourcePath = "RoomVisibility/RoomVisibilitySettings";

        [Header("Genel")]
        [Tooltip("Sahne yüklenince sistem kendiliğinden kurulsun (sahne düzenlemesi gerekmez)")]
        [SerializeField] private bool autoInstall = true;
        [Tooltip("Hiç kurulmayan sahneler")]
        [SerializeField] private string[] excludedScenes = { "MainMenu" };
        [Tooltip("Oda değişimlerini Console'a yaz")]
        [SerializeField] private bool debugLog = true;

        [Header("Oda karartması")]
        [SerializeField] private bool darknessEnabled = true;
        [Tooltip("Karartma rengi (koyu lacivert). Renderer rengi beyazdan bu renge doğru çarpılır.")]
        [SerializeField] private Color darknessColor = new Color(0x08 / 255f, 0x14 / 255f, 0x21 / 255f, 1f); // #081421
        [Tooltip("Aktif odaya komşu, daha önce girilmiş oda (0 = normal, 1 = tam karartma rengi)")]
        [Range(0f, 1f)] [SerializeField] private float adjacentDarkness = 0.6f;
        [Tooltip("Daha önce girilmiş, komşu olmayan oda")]
        [Range(0f, 1f)] [SerializeField] private float visitedDarkness = 0.68f;
        [Tooltip("Hiç girilmemiş oda")]
        [Range(0f, 1f)] [SerializeField] private float unexploredDarkness = 0.8f;
        [Tooltip("Başka binanın içi / dışarıdayken bina içi (dış cephe RoomVisualGroup ile hariç tutulur)")]
        [Range(0f, 1f)] [SerializeField] private float otherBuildingDarkness = 0.85f;
        [Tooltip("Oda kararırken geçiş süresi (sn)")]
        [Range(0.05f, 1f)] [SerializeField] private float darkenSeconds = 0.32f;
        [Tooltip("Odaya girince aydınlanma süresi (sn)")]
        [Range(0.05f, 1f)] [SerializeField] private float brightenSeconds = 0.3f;
        [Tooltip("İki bölge sınırı bu kadar (dünya birimi) yakınsa komşu sayılır (elle komşu verilmemişse)")]
        [SerializeField] private float adjacencyDistance = 0.5f;

        [Header("Duvar kesme (cutaway)")]
        [SerializeField] private bool cutawayEnabled = true;
        [Tooltip("Oyuncuyu kapatan ön duvar/uzun eşyanın opaklığı (0–0.25 önerilir; siluet kalsın)")]
        [Range(0f, 1f)] [SerializeField] private float cutawayAlpha = 0.22f;
        [Tooltip("Saydamlaşma süresi (sn)")]
        [Range(0.05f, 1f)] [SerializeField] private float cutawayFadeSeconds = 0.18f;
        [Tooltip("Geri gelme süresi (sn)")]
        [Range(0.05f, 1f)] [SerializeField] private float cutawayRestoreSeconds = 0.24f;
        [Tooltip("Cenk'in ayaktan başa yüksekliği (dünya birimi)")]
        [SerializeField] private float playerBodyHeight = 0.9f;
        [Tooltip("Cenk'in gövde yarı genişliği (dünya birimi)")]
        [SerializeField] private float playerBodyHalfWidth = 0.16f;
        [Tooltip("Örtme sınamasında üst gövdenin başladığı oran (0 ayak, 1 baş). Sandalye/masa gibi sadece bacağı örtenler saydamlaşmaz.")]
        [Range(0f, 0.9f)] [SerializeField] private float upperBodyStart = 0.4f;
        [Tooltip("Üst gövdenin bu oranı örtülünce saydamlaşır")]
        [Range(0.05f, 1f)] [SerializeField] private float coverageEnter = 0.35f;
        [Tooltip("Saydamken örtme bu oranın altına inince geri gelir (histerezis: titreme olmaz)")]
        [Range(0f, 1f)] [SerializeField] private float coverageExit = 0.15f;
        [Tooltip("Önde/arkada sınamasında Y payı (dünya birimi; sarsıntı titreşimi ±2 px'i yutar)")]
        [SerializeField] private float depthHysteresis = 0.04f;
        [Tooltip("Bundan kısa sprite'lar (sıkı sınır, dünya birimi) kendiliğinden saydamlaşmaz")]
        [SerializeField] private float minOccluderHeight = 0.5f;

        [Header("Etkileşim")]
        [Tooltip("Aktif olmayan odadaki etkileşim noktaları ipucu göstermesin")]
        [SerializeField] private bool interactionOnlyInActiveRoom = true;

        public bool AutoInstall => autoInstall;
        public string[] ExcludedScenes => excludedScenes;
        public bool DebugLog => debugLog;
        public bool DarknessEnabled => darknessEnabled;
        public Color DarknessColor => darknessColor;
        public float AdjacentDarkness => adjacentDarkness;
        public float VisitedDarkness => visitedDarkness;
        public float UnexploredDarkness => unexploredDarkness;
        public float OtherBuildingDarkness => otherBuildingDarkness;
        public float DarkenSeconds => darkenSeconds;
        public float BrightenSeconds => brightenSeconds;
        public float AdjacencyDistance => adjacencyDistance;
        public bool CutawayEnabled => cutawayEnabled;
        public float CutawayAlpha => cutawayAlpha;
        public float CutawayFadeSeconds => cutawayFadeSeconds;
        public float CutawayRestoreSeconds => cutawayRestoreSeconds;
        public float PlayerBodyHeight => playerBodyHeight;
        public float PlayerBodyHalfWidth => playerBodyHalfWidth;
        public float UpperBodyStart => upperBodyStart;
        public float CoverageEnter => coverageEnter;
        public float CoverageExit => Mathf.Min(coverageExit, coverageEnter);
        public float DepthHysteresis => depthHysteresis;
        public float MinOccluderHeight => minOccluderHeight;
        public bool InteractionOnlyInActiveRoom => interactionOnlyInActiveRoom;

        private static RoomVisibilitySettings cached;

        /// <summary>Resources'taki ayar asset'i; yoksa varsayılan değerli geçici kopya.</summary>
        public static RoomVisibilitySettings Load()
        {
            if (cached != null) return cached;
            cached = Resources.Load<RoomVisibilitySettings>(ResourcePath);
            if (cached == null)
            {
                cached = CreateInstance<RoomVisibilitySettings>();
                cached.name = "RoomVisibilitySettings (varsayılan)";
                cached.hideFlags = HideFlags.DontSave;
            }
            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cached = null;
    }
}
