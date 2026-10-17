using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Arka plandaki şehirde pencereler ara sıra yanıp söner. Her katman sönük pencerelerin küçük bir kısmının yanık
    // halidir (MenuArtBuilder üretir); katmanlar birbirinden bağımsız, rastgele aralıklarla yumuşakça açılıp kapanır.
    public class CityLights : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] layers;
        [Tooltip("Bir katmanın açık ya da kapalı kalma süresi (sn).")]
        [SerializeField] Vector2 holdSeconds = new Vector2(3f, 10f);
        [SerializeField] float fadeSeconds = 0.5f;

        float[] timers;
        bool[] on;

        void Awake()
        {
            timers = new float[layers.Length];
            on = new bool[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                on[i] = Random.value < 0.5f;
                timers[i] = Random.Range(holdSeconds.x, holdSeconds.y);
                SetAlpha(layers[i], on[i] ? 1f : 0f);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                timers[i] -= dt;
                if (timers[i] <= 0f)
                {
                    on[i] = !on[i];
                    timers[i] = Random.Range(holdSeconds.x, holdSeconds.y);
                }
                float alpha = Mathf.MoveTowards(layers[i].color.a, on[i] ? 1f : 0f, dt / Mathf.Max(0.01f, fadeSeconds));
                SetAlpha(layers[i], alpha);
            }
        }

        static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }
    }
}
