using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Çevresel su görünümü (LEAD): "WaterAreas" kökündeki su sprite'larını ortak ilerlemeyle (FloodEveningHub.Progress01)
    /// belirginleştirir; su YALNIZ bu tehlike alanlarında yükselir, yürüme alanına taşmaz (yalnız görsel, collider'a dokunmaz).
    /// - Her SpriteRenderer'ın saydamlığı minAlpha → kendi alfa değeri arasında artar ve yüzey hafifçe dalgalanır (≤1 px).
    /// - Adı "Stage1"/"Stage2"/"Stage3" ile başlayan çocuk objeler ilerleme 0 / stage2At / stage3At'i geçince açılır
    ///   (ENV su kademelerini böyle verebilir: daha geniş birikinti, kaldırım kenarına vuran su vb.).
    /// </summary>
    public class FloodEveningWaterRise : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.45f;
        [SerializeField, Range(0f, 1f)] private float stage2At = 0.33f;
        [SerializeField, Range(0f, 1f)] private float stage3At = 0.66f;
        [Tooltip("Yüzey dalgalanması (dünya birimi; 1/64 = 1 px)")]
        [SerializeField] private float bob = 1f / 64f;
        [Tooltip("Yalnız test için: Hub yerine bu ilerleme kullanılır (< 0 kapalı)")]
        [SerializeField, Range(-1f, 1f)] private float previewProgress = -1f;

        private struct Item { public SpriteRenderer r; public float alpha; public Vector3 basePos; }
        private readonly List<Item> items = new List<Item>();
        private readonly List<GameObject> stage2 = new List<GameObject>();
        private readonly List<GameObject> stage3 = new List<GameObject>();
        private float applied = -1f;

        private void Awake()
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                items.Add(new Item { r = r, alpha = r.color.a, basePos = r.transform.localPosition });
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                if (t.name.StartsWith("Stage2")) stage2.Add(t.gameObject);
                else if (t.name.StartsWith("Stage3")) stage3.Add(t.gameObject);
            }
            Apply(0f, true);
        }

        private void Update()
        {
            float p = previewProgress >= 0f ? previewProgress : FloodEveningHub.Progress01;
            Apply(p, false);
            if (bob > 0f)
            {
                float w = Mathf.Round(Mathf.Sin(Time.time * 1.7f) * 0.5f + 0.5f) * bob * Mathf.Clamp01(p * 2f);
                foreach (var it in items)
                    if (it.r != null) it.r.transform.localPosition = it.basePos + new Vector3(0f, w, 0f);
            }
        }

        private void Apply(float p, bool force)
        {
            p = Mathf.Clamp01(p);
            if (!force && Mathf.Abs(p - applied) < 0.005f) return;
            applied = p;
            foreach (var it in items)
            {
                if (it.r == null) continue;
                var c = it.r.color;
                c.a = Mathf.Lerp(Mathf.Min(minAlpha, it.alpha), it.alpha, p);
                it.r.color = c;
            }
            foreach (var g in stage2) if (g != null && g.activeSelf != (p >= stage2At)) g.SetActive(p >= stage2At);
            foreach (var g in stage3) if (g != null && g.activeSelf != (p >= stage3At)) g.SetActive(p >= stage3At);
        }
    }
}
