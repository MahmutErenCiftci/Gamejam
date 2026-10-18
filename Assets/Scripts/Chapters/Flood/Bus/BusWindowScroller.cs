using UnityEngine;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Otobüs içi (F06): pencerelerden geçen manzara şeridini otobüs ilerliyormuş gibi geriye (-grid X) kaydırır.
    /// Şerit bir SpriteMask (pencere camları) içinde görünür; görseli periyodiktir, her periyotta başa sarar.
    /// Konum her karede piksele oturtulur (izometrik eğim: 2 px yatay = 1 px dikey).
    /// </summary>
    public class BusWindowScroller : MonoBehaviour
    {
        private const float PixelsPerUnit = 64f;

        [Tooltip("Kayan manzara (TILE_Bus_Scenery). Boşsa bu obje kayar.")]
        [SerializeField] private Transform strip;
        [Tooltip("Manzaranın tekrar periyodu (piksel, duvar boyunca yatay)")]
        [SerializeField] private int periodPixels = 352;
        [Tooltip("Seyir hızı (yatay piksel / sn)")]
        [SerializeField] private float cruiseSpeed = 110f;
        [Tooltip("Hızlanma / yavaşlama süresi (sn)")]
        [SerializeField] private float rampSeconds = 1.6f;
        [SerializeField] private bool moveOnEnable;

        private Vector3 basePosition;
        private bool initialized;
        private float offset;   // yatay piksel, 0..period
        private float speed;
        private float target;

        /// <summary>Manzara hâlâ kayıyor mu (yavaşlama dahil)?</summary>
        public bool IsMoving => speed > 0.5f || target > 0f;

        private void Init()
        {
            if (initialized) return;
            initialized = true;
            if (strip == null) strip = transform;
            basePosition = strip.localPosition;
        }

        private void OnEnable()
        {
            Init();
            if (moveOnEnable) SetMoving(true, true);
        }

        /// <summary>Otobüs ilerliyor (true) ya da durağa yaklaşıp duruyor (false). immediate: hızı hemen ayarla.</summary>
        public void SetMoving(bool moving, bool immediate = false)
        {
            Init();
            target = moving ? cruiseSpeed : 0f;
            if (immediate) speed = target;
        }

        private void Update()
        {
            Init();
            float accel = cruiseSpeed / Mathf.Max(0.05f, rampSeconds);
            speed = Mathf.MoveTowards(speed, target, accel * Time.deltaTime);
            if (speed <= 0f) return;
            offset += speed * Time.deltaTime;
            if (periodPixels > 0) offset %= periodPixels;
            int px = Mathf.FloorToInt(offset / 2f) * 2;   // çift piksel: dikey adım tam piksel olsun
            strip.localPosition = basePosition - new Vector3(px / PixelsPerUnit, px * 0.5f / PixelsPerUnit, 0f);
        }
    }
}
