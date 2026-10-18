using UnityEngine;

namespace AfetUyanisi.Chapters.Visibility
{
    /// <summary>
    /// İsteğe bağlı obje bazlı ayar (oda görünürlüğü). Bu objenin ve çocuklarının renderer'larına uygulanır; en yakın
    /// üstteki RoomVisualGroup geçerlidir. Hiç konmazsa her renderer bulunduğu odanın kökünden kendiliğinden ayarlanır.
    /// Örnekler: binanın dış cephesi (darken = false), oyuncuyu kapatabilecek ön duvar (cutaway = Always),
    /// saydamlaşmaması gereken dekor (cutaway = Never), iki oda arasındaki duvarı belirli odaya bağlamak (zone).
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomVisualGroup : MonoBehaviour
    {
        /// <summary>Duvar kesme davranışı.</summary>
        public enum CutawayMode
        {
            /// <summary>Uzun ve oyuncunun önünde çiziliyorsa (otomatik sınama) saydamlaşır.</summary>
            Auto,
            /// <summary>Hiç saydamlaşmaz.</summary>
            Never,
            /// <summary>Ön duvar: boyuna bakılmadan sınanır (oyuncunun önünde ve üst gövdesini örtüyorsa saydamlaşır).</summary>
            Always,
        }

        [Tooltip("Bu objeler oda karartmasından etkilensin mi? (dış cephe, gökyüzü, efekt: kapalı)")]
        [SerializeField] private bool darken = true;
        [SerializeField] private CutawayMode cutaway = CutawayMode.Auto;
        [Tooltip("İsteğe bağlı: bu objeler bu odaya ait sayılsın (hiyerarşide başka yerde dursa da)")]
        [SerializeField] private RoomVisibilityZone zone;
        [Tooltip("Saydamken opaklık; negatifse ayarlardaki değer")]
        [Range(-1f, 1f)] [SerializeField] private float cutawayAlphaOverride = -1f;

        public bool Darken => darken;
        public CutawayMode Cutaway => cutaway;
        public RoomVisibilityZone Zone => zone;
        public float CutawayAlphaOverride => cutawayAlphaOverride;

        /// <summary>Kodla kurulum.</summary>
        public void Configure(bool darkenWithRoom, CutawayMode cutawayMode, RoomVisibilityZone ownerZone = null, float alphaOverride = -1f)
        {
            darken = darkenWithRoom;
            cutaway = cutawayMode;
            zone = ownerZone;
            cutawayAlphaOverride = alphaOverride;
        }
    }
}
