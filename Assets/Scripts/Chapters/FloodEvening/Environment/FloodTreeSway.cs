using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Haritadaki ağaçların ve çalıların hafif salınımı (yalnız görsel; oyuncuyu etkileyen rüzgâr YOK).
    /// Kareler tools/sel_aksam_harita/hazards.js ile kaynak sprite'tan üretilir: taç satırları 1 piksellik ızgarada sağa-sola kayar,
    /// yukarı çıktıkça biraz gecikir (dal/yaprak hareketi), gövdenin alt kısmı sabit kalır → piksel bozulmaz, abartılı/çizgi film gibi değil.
    /// Şiddet sel ilerlemesiyle (yağmurla aynı kaynak, FloodEveningHub.Progress01) hafifçe artar: önce "sakin" (en çok 1 px) kareler yavaş,
    /// ilerledikçe hızlanır; eşikten sonra "fırtına" kareleri (tepe 2 px). Her ağacın kendi evresi ve hız farkı vardır.
    /// Harita prefabındaki SpriteRenderer'lar kaynak sprite'larına göre bulunur; bileşen kapanınca özgün sprite'lar geri konur.
    /// </summary>
    public class FloodTreeSway : MonoBehaviour
    {
        [Serializable]
        public class SwaySet
        {
            public Sprite source;
            public Sprite[] calm = Array.Empty<Sprite>();
            public Sprite[] storm = Array.Empty<Sprite>();
        }

        [Tooltip("Ağaçların arandığı kök (EveningNeighborhood); boşsa sahnenin tamamı")]
        [SerializeField] private Transform mapRoot;
        [SerializeField] private SwaySet[] sets = Array.Empty<SwaySet>();
        [Tooltip("Bir salınım turunun süresi (sn): sakin → fırtına")]
        [SerializeField] private Vector2 period = new Vector2(3.6f, 2.0f);
        [Tooltip("Şiddet bu değeri geçince fırtına kareleri (2 px tepe)")]
        [Range(0f, 1f)] [SerializeField] private float stormFrom = 0.6f;
        [Tooltip("Şiddet: sel %0'da / %100'de")]
        [SerializeField] private Vector2 intensityRange = new Vector2(0.25f, 1f);

        private class Tree { public SpriteRenderer sr; public SwaySet set; public Sprite original; public float phase, speed; public int frame = -1; public bool storm; }
        private readonly List<Tree> trees = new List<Tree>();

        /// <summary>Salınan ağaç sayısı (test).</summary>
        public int Count => trees.Count;
        /// <summary>Anlık şiddet 0..1 (test).</summary>
        public float Intensity { get; private set; }

        private void Awake()
        {
            var lookup = new Dictionary<Sprite, SwaySet>();
            foreach (var s in sets) if (s != null && s.source != null && s.calm.Length > 0) lookup[s.source] = s;
            var renderers = mapRoot != null ? mapRoot.GetComponentsInChildren<SpriteRenderer>(true) : FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include);
            foreach (var sr in renderers)
            {
                if (sr.sprite == null || !lookup.TryGetValue(sr.sprite, out var set)) continue;
                Vector2 p = sr.transform.position;
                float h = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f) * 43758.5453f) % 1f;
                trees.Add(new Tree { sr = sr, set = set, original = sr.sprite, phase = h, speed = 0.85f + 0.3f * ((h * 7.31f) % 1f) });
            }
        }

        private void OnDisable()
        {
            foreach (var t in trees) if (t.sr != null) { t.sr.sprite = t.original; t.frame = -1; }
        }

        private void Update()
        {
            float p = FloodEveningHub.FloodStarted ? FloodEveningHub.Progress01 : 0f;
            // hafif, yavaş değişen esinti dalgası (yalnız görüntü)
            float gust = (Mathf.PerlinNoise(Time.time * 0.12f, 0.37f) - 0.5f) * 0.18f;
            Intensity = Mathf.Clamp01(Mathf.Lerp(intensityRange.x, intensityRange.y, Mathf.Pow(p, 1.1f)) + gust);
            float cycle = 1f / Mathf.Lerp(period.x, period.y, Intensity);
            bool storm = Intensity >= stormFrom;
            float dt = Time.deltaTime;
            foreach (var t in trees)
            {
                if (t.sr == null) continue;
                t.phase = (t.phase + dt * cycle * t.speed) % 1f;
                var frames = storm && t.set.storm.Length > 0 ? t.set.storm : t.set.calm;
                int f = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t.phase * frames.Length));
                if (f == t.frame && storm == t.storm) continue;
                t.frame = f; t.storm = storm;
                t.sr.sprite = frames[f];
            }
        }
    }
}
