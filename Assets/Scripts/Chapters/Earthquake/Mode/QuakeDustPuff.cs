using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Sarsıntıda kare kare oynayan küçük toz bulutu (FX_EQ_Dust: 6 kare 24x16, büyür → yükselir → söner).
    /// Fizik ve partikül sistemi yok: tek SpriteRenderer, kareler sabit hızda değişir, konum 1/64 ızgaraya oturur (piksel net).
    /// EarthquakeEventController BeginShake/EndShake çağırır (sahnedeki bütün QuakeDustPuff'ları kendisi bulur).
    /// Sarsıntı sürdükçe her tur arasında rastgele kısa bekleme olur; sarsıntı bitince o anki tur tamamlanır ve gizlenir.
    /// Eşyaların dibine (kitaplık, gardırop, dolap...) yerleştirin; sortingOrder eşyaların (0) üstünde kalmalı.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class QuakeDustPuff : MonoBehaviour
    {
        [Tooltip("Animasyon kareleri (FX_EQ_Dust_0..5)")]
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [Tooltip("Kare hızı (kare/sn)")]
        [SerializeField] private float fps = 11f;
        [Tooltip("Sarsıntı başladıktan sonra ilk tura kadar bekleme (sn): min, max")]
        [SerializeField] private Vector2 startDelay = new Vector2(0.1f, 0.8f);
        [Tooltip("Turlar arası bekleme (sn): min, max")]
        [SerializeField] private Vector2 loopDelay = new Vector2(0.2f, 0.9f);
        [Tooltip("Her turda rastgele konum kayması (piksel, ±)")]
        [SerializeField] private Vector2Int jitterPixels = new Vector2Int(3, 1);
        [Tooltip("Sprite sıralama değeri (eşyalar 0; toz onların üstünde)")]
        [SerializeField] private int sortingOrder = 6;

        private const float Ppu = 64f;
        private SpriteRenderer sr;
        private Vector3 restLocalPos;
        private bool shaking;
        private bool playing;
        private float timer;
        private int frame;

        public bool IsPlaying => playing;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            restLocalPos = transform.localPosition;
            sr.sortingOrder = sortingOrder;
            sr.enabled = false;
        }

        /// <summary>Sarsıntı başladı: kısa bir gecikmeden sonra turlar başlar.</summary>
        public void BeginShake()
        {
            if (frames == null || frames.Length == 0) return;
            shaking = true;
            playing = false;
            timer = Random.Range(startDelay.x, startDelay.y);
        }

        /// <summary>Sarsıntı bitti: oynayan tur tamamlanır, yenisi başlamaz.</summary>
        public void EndShake()
        {
            shaking = false;
            if (!playing && sr != null) sr.enabled = false;
        }

        private void Update()
        {
            if (!shaking && !playing) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            if (!playing)
            {
                if (!shaking) return;
                playing = true;
                frame = 0;
                int jx = Random.Range(-jitterPixels.x, jitterPixels.x + 1);
                int jy = Random.Range(-jitterPixels.y, jitterPixels.y + 1);
                transform.localPosition = Snap(restLocalPos) + new Vector3(jx / Ppu, jy / Ppu, 0f);
                sr.flipX = Random.value < 0.5f;
                sr.sprite = frames[0];
                sr.enabled = true;
                timer = 1f / Mathf.Max(1f, fps);
                return;
            }

            frame++;
            if (frame < frames.Length)
            {
                sr.sprite = frames[frame];
                timer = 1f / Mathf.Max(1f, fps);
                return;
            }

            // tur bitti
            playing = false;
            sr.enabled = false;
            timer = Random.Range(loopDelay.x, loopDelay.y);
        }

        private static Vector3 Snap(Vector3 v) =>
            new Vector3(Mathf.Round(v.x * Ppu) / Ppu, Mathf.Round(v.y * Ppu) / Ppu, v.z);
    }
}
