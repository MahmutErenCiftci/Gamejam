using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Uyuyan çocuğun başından süzülen "z" harfleri: harfler sırayla (küçükten büyüğe) çıkar, sağ-yukarı
    // süzülürken hafifçe sallanır ve söner. Konumlar piksele yuvarlanır (Pixel Perfect Camera ile titremesin).
    public class SleepZzz : MonoBehaviour
    {
        [Tooltip("Küçükten büyüğe; sırayla kullanılır.")]
        [SerializeField] Sprite[] letters;
        [SerializeField] Material material;
        [SerializeField] int sortingOrder = 60;
        [SerializeField] float interval = 0.9f;
        [SerializeField] float lifetime = 2.6f;
        [Tooltip("Bir harfin ömrü boyunca aldığı yol (piksel).")]
        [SerializeField] Vector2 travelPixels = new Vector2(12f, 26f);
        [SerializeField] float wobblePixels = 2f;

        const float PixelsPerUnit = 64f;

        SpriteRenderer[] pool;
        float[] ages;
        int next;
        int letterIndex;
        float timer;

        void Awake()
        {
            int count = Mathf.CeilToInt(lifetime / interval) + 1;
            pool = new SpriteRenderer[count];
            ages = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Z" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                if (material != null) sr.sharedMaterial = material;
                sr.sortingOrder = sortingOrder;
                sr.enabled = false;
                pool[i] = sr;
                ages[i] = -1f;
            }
        }

        void OnDisable()
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++) { pool[i].enabled = false; ages[i] = -1f; }
            timer = 0f;
        }

        void Update()
        {
            if (letters == null || letters.Length == 0) return;
            float dt = Time.unscaledDeltaTime;
            timer += dt;
            if (timer >= interval)
            {
                timer = 0f;
                SpriteRenderer sr = pool[next];
                sr.sprite = letters[letterIndex];
                sr.enabled = true;
                ages[next] = 0f;
                next = (next + 1) % pool.Length;
                letterIndex = (letterIndex + 1) % letters.Length;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                if (ages[i] < 0f) continue;
                ages[i] += dt;
                float t = ages[i] / lifetime;
                if (t >= 1f) { ages[i] = -1f; pool[i].enabled = false; continue; }
                float x = travelPixels.x * t + Mathf.Sin(t * Mathf.PI * 2f) * wobblePixels;
                float y = travelPixels.y * t;
                pool[i].transform.localPosition = new Vector3(Mathf.Round(x) / PixelsPerUnit, Mathf.Round(y) / PixelsPerUnit, 0f);
                // ilk %15'te belirir, son %40'ta söner
                float alpha = t < 0.15f ? t / 0.15f : t > 0.6f ? (1f - t) / 0.4f : 1f;
                Color c = pool[i].color;
                c.a = alpha;
                pool[i].color = c;
            }
        }
    }
}
