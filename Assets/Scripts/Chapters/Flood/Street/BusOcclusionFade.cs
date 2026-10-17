using System;
using UnityEngine;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Otobüs durakta dururken çocuk gövdenin arkasında kalırsa (durak arka kaldırımda, seçenek B) gövde ve kapı
    /// yarı saydam olur, çocuk görünür; çocuk arkadan çıkınca ya da otobüs kalkınca yavaşça eski hâline döner.
    /// Bus.prefab kökünde, BusController ile birlikte durur. Sadece renk saydamlığını değiştirir (sıralamaya dokunmaz).
    /// </summary>
    [RequireComponent(typeof(BusController))]
    public class BusOcclusionFade : MonoBehaviour
    {
        [Tooltip("Saydamlaşan katmanlar (gövde + kapı). Boşsa Visual altındaki bütün SpriteRenderer'lar.")]
        [SerializeField] private SpriteRenderer[] fadeRenderers = Array.Empty<SpriteRenderer>();
        [Tooltip("Çocuk arkadayken saydamlık")]
        [Range(0f, 1f)] [SerializeField] private float occludedAlpha = 0.45f;
        [Tooltip("Saydamlık değişim hızı (saniyede alfa)")]
        [SerializeField] private float fadeSpeed = 4f;
        [Tooltip("Çocuğun ayağından başına kadar yükseklik (dünya birimi); baş ve ayak noktası sınamada kullanılır")]
        [SerializeField] private float playerHeight = 0.9f;
        [Tooltip("Çocuk otobüsün önündeyse (zemin izinin ön kenarının önünde) saydamlaşmaz: hücre cinsinden pay")]
        [SerializeField] private float frontMarginCells = 0.2f;
        [Tooltip("Zemin izinin grid Y ekseninde yarı derinliği (hücre)")]
        [SerializeField] private float halfDepthCells = 0.625f;

        private BusController bus;
        private IsoPlayerController player;
        private float alpha = 1f;
        private float[] baseAlpha = Array.Empty<float>();

        /// <summary>Şu anki gövde saydamlığı (1 = opak).</summary>
        public float CurrentAlpha => alpha;

        private void Awake()
        {
            bus = GetComponent<BusController>();
            if (fadeRenderers == null || fadeRenderers.Length == 0)
            {
                var visual = transform.Find("Visual");
                fadeRenderers = visual != null ? visual.GetComponentsInChildren<SpriteRenderer>(true) : Array.Empty<SpriteRenderer>();
            }
            baseAlpha = new float[fadeRenderers.Length];
            for (int i = 0; i < fadeRenderers.Length; i++)
                baseAlpha[i] = fadeRenderers[i] != null ? fadeRenderers[i].color.a : 1f;
        }

        private void Start()
        {
            player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
        }

        private void OnDisable()
        {
            alpha = 1f;
            Apply();
        }

        private void LateUpdate()
        {
            float target = bus != null && bus.IsAtStop && !bus.IsMoving && PlayerBehind() ? occludedAlpha : 1f;
            if (Mathf.Approximately(alpha, target)) return;
            alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.deltaTime);
            Apply();
        }

        // Çocuk görünür, otobüsün önünde değil ve ayak ya da baş noktası gövde sprite'ının sınırları içinde mi?
        private bool PlayerBehind()
        {
            if (player == null || !player.isActiveAndEnabled) return false;
            Vector3 feet = player.transform.position;
            Vector2 local = feet - transform.position;
            float gy = 2f * local.y - local.x; // grid Y ofseti (+ = otobüsün arkası, kaldırım tarafı)
            if (gy < -halfDepthCells - frontMarginCells) return false;

            Vector3 head = feet + new Vector3(0f, playerHeight, 0f);
            foreach (var r in fadeRenderers)
            {
                if (r == null || !r.enabled) continue;
                Bounds b = r.bounds;
                if (Inside2D(b, feet) || Inside2D(b, head)) return true;
            }
            return false;
        }

        private static bool Inside2D(Bounds b, Vector3 p) =>
            p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;

        private void Apply()
        {
            for (int i = 0; i < fadeRenderers.Length; i++)
            {
                var r = fadeRenderers[i];
                if (r == null) continue;
                Color c = r.color;
                c.a = baseAlpha.Length > i ? baseAlpha[i] * alpha : alpha;
                r.color = c;
            }
        }
    }
}
