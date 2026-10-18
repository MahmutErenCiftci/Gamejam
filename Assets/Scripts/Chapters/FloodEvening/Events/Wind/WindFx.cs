using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Rüzgâr koridorunun görsel etkisi: savrulan yapraklar + rüzgâr çizgileri. Kamera görüş alanında (oyuncunun çevresinde)
    /// havuzlanmış SpriteRenderer'larla çalışır; yön (-1 sol / +1 sağ) ve şiddet WindCorridor'dan gelir. "Önizleme" (uyarı) sırasında
    /// az ve yavaş yaprak döner, gerçek esintide şiddete göre artar. Calm() yavaşça söndürür, Stop() hemen gizler.
    /// </summary>
    public class WindFx : MonoBehaviour
    {
        [SerializeField] private Sprite[] leafSprites = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite streakSprite;
        [SerializeField] private Transform follow;
        [Tooltip("Görünür alan (oyuncunun çevresinde, dünya birimi)")]
        [SerializeField] private Vector2 area = new Vector2(8f, 4.6f);
        [SerializeField] private int maxLeaves = 42;
        [SerializeField] private int maxStreaks = 16;
        [SerializeField] private int sortingOrder = 400;

        private class Bit { public SpriteRenderer sr; public Vector2 v; public float life, age, spin, wob; public bool streak; }
        private readonly List<Bit> bits = new List<Bit>();
        private int dir = 1;
        private float targetRate, rate, speed = 3f, spawnAcc;
        private bool running;

        private void Awake()
        {
            if (follow == null) { var p = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include); if (p != null) follow = p.transform; }
            for (int i = 0; i < maxLeaves + maxStreaks; i++)
            {
                var go = new GameObject(i < maxLeaves ? "Leaf" : "Streak");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = sortingOrder;
                sr.enabled = false;
                bits.Add(new Bit { sr = sr, streak = i >= maxLeaves });
            }
        }

        /// <summary>Rüzgâr yönü (-1 sol, +1 sağ) ve şiddeti; preview = uyarı/sakin (az ve yavaş).</summary>
        public void SetWind(int pushDir, WindCorridor.Strength strength, bool preview)
        {
            dir = pushDir >= 0 ? 1 : -1;
            running = true;
            int s = (int)strength;
            targetRate = preview ? 6f : 14f + s * 12f;      // saniyede parçacık
            speed = preview ? 2.2f : 3.6f + s * 1.6f;
        }

        public void Calm() { targetRate = 0f; }

        public void Stop()
        {
            running = false; targetRate = rate = 0f;
            foreach (var b in bits) { b.sr.enabled = false; b.life = 0f; }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            rate = Mathf.MoveTowards(rate, targetRate, dt * 30f);
            Vector2 c = follow != null ? (Vector2)follow.position : (Vector2)transform.position;
            if (running && rate > 0.01f)
            {
                spawnAcc += rate * dt;
                while (spawnAcc >= 1f) { spawnAcc -= 1f; Spawn(c, Random.value < 0.35f); }
            }
            foreach (var b in bits)
            {
                if (b.life <= 0f) continue;
                b.age += dt;
                if (b.age >= b.life) { b.life = 0f; b.sr.enabled = false; continue; }
                var t = b.sr.transform;
                Vector2 v = b.v;
                if (!b.streak) { v.y += Mathf.Sin(b.age * 7f + b.wob) * 0.6f; t.Rotate(0f, 0f, b.spin * dt); }
                t.position += (Vector3)(v * dt);
                float k = b.age / b.life;
                var col = b.sr.color;
                col.a = (b.streak ? 0.55f : 1f) * Mathf.Clamp01(Mathf.Min(k * 6f, (1f - k) * 4f));
                b.sr.color = col;
            }
        }

        private void Spawn(Vector2 c, bool streak)
        {
            Bit b = null;
            foreach (var x in bits) if (x.life <= 0f && x.streak == streak) { b = x; break; }
            if (b == null) return;
            Sprite sp = streak ? streakSprite : (leafSprites.Length > 0 ? leafSprites[Random.Range(0, leafSprites.Length)] : null);
            if (sp == null) return;
            b.sr.sprite = sp;
            b.sr.flipX = dir < 0;
            b.sr.enabled = true;
            b.sr.color = Color.white;
            float x0 = c.x - dir * (area.x * 0.5f + 0.3f) + Random.Range(-0.6f, 0.6f);
            float y0 = c.y + Random.Range(-area.y * 0.5f, area.y * 0.5f);
            b.sr.transform.position = new Vector3(x0, y0, 0f);
            b.sr.transform.rotation = streak ? Quaternion.identity : Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float sp2 = speed * (streak ? 1.5f : Random.Range(0.75f, 1.15f));
            b.v = new Vector2(dir * sp2, Random.Range(-0.35f, 0.35f));
            b.life = (area.x + 1.2f) / sp2;
            b.age = 0f;
            b.spin = Random.Range(-540f, 540f);
            b.wob = Random.Range(0f, 6.28f);
        }
    }
}
