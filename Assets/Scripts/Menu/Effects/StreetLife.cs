using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AfetUyanisi.Menu
{
    // Arka plandaki şehrin sokaklarında yürüyen insanlar ve giden arabalar. Yollar MenuArtBuilder'ın bulduğu, önünde
    // hiçbir şey olmayan şerit ve kaldırım parçalarıdır (sprite'lar şehrin üstünde çizilse de doğru görünür).
    //  Normal: insanlar yürür, arabalar gider; yolun sonuna gelince başa döner.
    //  Panik (DEPREM): insanlar koşar ve titrer, arabalar frene basıp durur, sarsılır.
    //  Sel: insanlar sokaktan çekilir (kaybolur); arabalar durur, su seviyesiyle birlikte yükselip yüzer, akıntıyla
    //  yavaşça sürüklenir ve dalgayla iner kalkar.
    public class StreetLife : MonoBehaviour
    {
        public enum Mode { Normal, Panic, Flood }

        [Serializable]
        public class Walker
        {
            public SpriteRenderer renderer;
            [Tooltip("Yolun başı ve sonu (ekran pikseli, yerel).")]
            public Vector2 from, to;
            [Tooltip("Piksel/sn.")]
            public float speed = 6f;
            [Tooltip("İnsanlar için iki adım karesi; arabalarda boş.")]
            public Sprite stepA, stepB;
        }

        [SerializeField] Walker[] cars;
        [SerializeField] Walker[] people;
        [SerializeField] CityFlood flood;
        [SerializeField] float panicSpeedFactor = 3.2f;
        [SerializeField] float driftPixels = 2.5f;

        const float PixelsPerUnit = 64f;
        Mode mode = Mode.Normal;
        float[] progress, carSpeed, stepTimer;
        float time;

        void Awake()
        {
            progress = new float[cars.Length + people.Length];
            carSpeed = new float[cars.Length];
            stepTimer = new float[people.Length];
            for (int i = 0; i < progress.Length; i++) progress[i] = Random.value;
            for (int i = 0; i < cars.Length; i++) carSpeed[i] = cars[i].speed;
        }

        public void SetMode(Mode next)
        {
            mode = next;
            foreach (Walker w in people) if (w.renderer != null) w.renderer.enabled = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            float level = flood != null ? flood.Level : 0f;

            for (int i = 0; i < cars.Length; i++)
            {
                Walker w = cars[i];
                float target = mode == Mode.Normal ? w.speed : mode == Mode.Flood ? driftPixels : 0f;
                carSpeed[i] = Mathf.MoveTowards(carSpeed[i], target, (mode == Mode.Panic ? 40f : 8f) * dt);
                Vector2 p = Advance(i, w, carSpeed[i] * dt);
                if (mode == Mode.Panic) p += new Vector2(Mathf.Round(Random.Range(-1f, 1f)), 0f);
                // Su arabayı kaldırır (yüzer) ve dalgayla hafifçe iner kalkar.
                if (level > 3f) p.y += level - 3f + Mathf.Round(Mathf.Sin(time * 2.2f + i * 1.7f));
                Place(w.renderer, p);
            }

            for (int i = 0; i < people.Length; i++)
            {
                Walker w = people[i];
                if (w.renderer == null) continue;
                // Sel başlayınca (su bileğe gelince) insanlar sokaktan çekilir.
                bool hidden = mode == Mode.Flood && level >= 3f;
                w.renderer.enabled = !hidden;
                if (hidden) continue;
                float factor = mode == Mode.Panic ? panicSpeedFactor : 1f;
                Vector2 p = Advance(cars.Length + i, w, w.speed * factor * dt);
                if (mode == Mode.Panic) p.y += Mathf.Round(Random.Range(0f, 1f));
                stepTimer[i] += dt * factor;
                if (w.stepA != null) w.renderer.sprite = Mathf.Repeat(stepTimer[i], 0.5f) < 0.25f ? w.stepA : w.stepB;
                Place(w.renderer, p);
            }
        }

        // Yol boyunca ilerler, sona gelince başa döner. Dönüş: ekran konumu (yerel piksel).
        Vector2 Advance(int index, Walker w, float pixels)
        {
            float length = Mathf.Max(1f, Vector2.Distance(w.from, w.to));
            progress[index] = Mathf.Repeat(progress[index] + pixels / length, 1f);
            return Vector2.Lerp(w.from, w.to, progress[index]);
        }

        static void Place(SpriteRenderer r, Vector2 p)
        {
            if (r == null) return;
            r.transform.localPosition = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0f) / PixelsPerUnit;
        }
    }
}
