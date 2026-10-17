using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Şafak gökyüzünde ağır ağır geçen kuş sürüsü: kanat çırpar (iki kare), hafifçe iner çıkar, ekrandan çıkınca
    // diğer kenardan yeniden girer. Konumlar piksele yuvarlanır.
    public class BirdFlock : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] birds;
        [SerializeField] Sprite wingsUp, wingsDown;
        [SerializeField] float speedPixels = 14f;
        [Tooltip("Sürünün dolaştığı yatay aralık (yerel piksel).")]
        [SerializeField] float wrapWidth = 1040f;
        [SerializeField] float flapSeconds = 0.22f;

        const float PixelsPerUnit = 64f;
        Vector2[] start;
        float[] phase;
        float time;

        void Awake()
        {
            start = new Vector2[birds.Length];
            phase = new float[birds.Length];
            for (int i = 0; i < birds.Length; i++)
            {
                start[i] = birds[i].transform.localPosition * PixelsPerUnit;
                phase[i] = Random.value * 10f;
            }
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;
            for (int i = 0; i < birds.Length; i++)
            {
                float x = Mathf.Repeat(start[i].x + wrapWidth / 2f + time * speedPixels, wrapWidth) - wrapWidth / 2f;
                float y = start[i].y + Mathf.Sin(time * 0.8f + phase[i]) * 3f;
                birds[i].transform.localPosition = new Vector3(Mathf.Round(x), Mathf.Round(y), 0f) / PixelsPerUnit;
                birds[i].sprite = Mathf.Repeat(time + phase[i], flapSeconds * 2f) < flapSeconds ? wingsUp : wingsDown;
            }
        }
    }
}
