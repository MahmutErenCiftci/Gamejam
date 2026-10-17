using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AfetUyanisi.Menu
{
    // DEPREM seçilince arka plandaki şehirde binalar yıkılır. Önce bütün şehir titrer; sonra binalar rastgele sırayla,
    // tek parça halinde ve her biri kendi tarzında çöker (hepsi aynı görünmesin):
    //  Sink     — bir süre sarsılır, sonra giderek hızlanarak yerine çöker.
    //  Pancake  — katlar üst üste çöker: bina kat kat, sarsılarak aşağı iner; her katta toz patlar.
    //  Crumble  — uzun süre şiddetle sarsılır, sonra hızla ufalanarak çöker.
    // Bina şehirde görünen silueti içinde (maske) çöker; tabanından toz bulutu kalkar, yerinde enkaz kalır. Ayakta
    // kalan binalarda çatlaklar ve kırık camlar belirir, elektrik kesilir. Seçim değişince şehir eski haline döner.
    public class CityQuake : MonoBehaviour
    {
        public enum Style { Sink, Pancake, Crumble }

        [Serializable]
        public class Piece
        {
            public Style style;
            [Tooltip("Alt parça (maskeli, yerine çöker).")]
            public Transform bottom;
            [Tooltip("Üst parçanın döndüğü menteşe (kırık çizgisinin dış köşesi); üst parça bunun çocuğu.")]
            public Transform topPivot;
            [Tooltip("Kırılma yüksekliği (piksel).")]
            public float breakPixels;
            [Tooltip("Üst parçanın yüksekliği (piksel).")]
            public float topPixels;
            [Tooltip("-1 sola, 1 sağa devrilir / yatar.")]
            public float direction = 1f;
            public ParticleSystem dust;
        }

        [Tooltip("Yıkılmış binaların yerindeki enkaz. Deprem boyunca açık (parçaların altında kalır).")]
        [SerializeField] GameObject ruins;
        [Tooltip("Ayakta kalan şehirdeki hasar: çatlaklar, kırık camlar, sönen ışıklar. Sarsıntıyla birlikte belirir.")]
        [SerializeField] SpriteRenderer cracks;
        [SerializeField] float cracksFadeSeconds = 1.4f;
        [Tooltip("Yanıp sönen pencereler; depremde elektrik kesilince kapanır.")]
        [SerializeField] GameObject cityLights;
        [SerializeField] Piece[] pieces;
        [SerializeField] float firstDelay = 0.35f;
        [Tooltip("Binaların yıkılmaya başladığı zaman aralığı (sn); sıra her depremde rastgele.")]
        [SerializeField] float spread = 2.6f;
        [SerializeField] float gravityPixels = 380f;
        [SerializeField] float shakePixels = 1f;

        const float PixelsPerUnit = 64f;
        const float FloorPixels = 12f;
        Coroutine routine;
        Vector3[] pivotRest, topRest;
        SpriteRenderer[] topRenderers;

        void Awake()
        {
            pivotRest = new Vector3[pieces.Length];
            topRest = new Vector3[pieces.Length];
            topRenderers = new SpriteRenderer[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].topPivot == null) continue;
                pivotRest[i] = pieces[i].topPivot.localPosition;
                Transform top = pieces[i].topPivot.GetChild(0);
                topRest[i] = top.localPosition;
                topRenderers[i] = top.GetComponent<SpriteRenderer>();
            }
            Restore();
        }

        public void Play()
        {
            Restore();
            if (ruins != null) ruins.SetActive(true);
            routine = StartCoroutine(Collapse());
        }

        public void Restore()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (ruins != null) ruins.SetActive(false);
            SetCracks(0f);
            if (cityLights != null) cityLights.SetActive(true);
            if (pivotRest == null) return;
            for (int i = 0; i < pieces.Length; i++)
            {
                Piece p = pieces[i];
                if (p.bottom != null) { p.bottom.localPosition = Vector3.zero; p.bottom.localRotation = Quaternion.identity; p.bottom.gameObject.SetActive(true); }
                if (p.topPivot != null)
                {
                    p.topPivot.localPosition = pivotRest[i];
                    p.topPivot.localRotation = Quaternion.identity;
                    p.topPivot.GetChild(0).localPosition = topRest[i];
                    p.topPivot.gameObject.SetActive(true);
                    // Bina kendi silueti içinde çöker (öndeki binaların üstüne taşmaz).
                    if (topRenderers[i] != null) topRenderers[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
                if (p.dust != null) p.dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void OnDisable() => Restore();

        IEnumerator Collapse()
        {
            int n = pieces.Length;
            var start = new float[n];
            var done = new bool[n];
            var dusted = new int[n];
            // Rastgele sıra: soldan sağa düzenli bir dalga gibi görünmesin.
            for (int i = 0; i < n; i++) start[i] = firstDelay + Random.Range(0f, spread);
            if (cityLights != null) cityLights.SetActive(false);
            int remaining = n;
            for (float t = 0f; remaining > 0 || t < cracksFadeSeconds; t += Time.unscaledDeltaTime)
            {
                SetCracks(t / cracksFadeSeconds);
                for (int i = 0; i < n; i++)
                {
                    if (done[i]) continue;
                    float local = t - start[i];
                    bool finished = pieces[i].style switch
                    {
                        Style.Sink => Sink(i, local, ref dusted[i]),
                        Style.Pancake => Pancake(i, local, ref dusted[i]),
                        _ => Crumble(i, local, ref dusted[i]),
                    };
                    if (finished) { done[i] = true; remaining--; }
                }
                yield return null;
            }
            SetCracks(1f);
            routine = null;
        }

        // ---------------------------------------------------------------- tarzlar
        // Her biri yerel zamanı (yıkılmanın başlangıcından beri, öncesi negatif) alır, bitince true döner. Bina hep
        // tek parça hareket eder (üst ve alt parça aynı konumda), kendi silueti içinde çöker.

        // Çökme: bir süre sarsılır, sonra giderek hızlanarak yerine çöker; tabanından toz kalkar.
        bool Sink(int i, float local, ref int dusted)
        {
            Piece p = pieces[i];
            float sink = 0f;
            if (local > 0.3f)
            {
                float fall = local - 0.3f;
                sink = 0.5f * gravityPixels * 0.8f * fall * fall;
                if (dusted == 0) { StartDust(p); Burst(p, 12); dusted = 1; }
            }
            return Move(p, i, new Vector2(Shake(local > 0f ? 1.5f : shakePixels), -sink));
        }

        // Kat kat çöküş: her 0,26 sn'de bir kat aşağı (hızlı düşüş + kısa duraksama), her katta toz.
        bool Pancake(int i, float local, ref int dusted)
        {
            Piece p = pieces[i];
            float sink = 0f;
            if (local > 0f)
            {
                const float step = 0.26f;
                int floors = Mathf.FloorToInt(local / step);
                float inStep = Mathf.Clamp01((local - floors * step) / 0.09f);
                sink = (floors + inStep * inStep) * FloorPixels * 1.15f;
                if (floors >= dusted) { Burst(p, floors == 0 ? 10 : 6); if (dusted == 0) StartDust(p); dusted = floors + 1; }
            }
            return Move(p, i, new Vector2(Shake(local > 0f ? 1.5f : shakePixels), -sink));
        }

        // Ufalanma: uzun ve şiddetli sarsıntı, sonra hızla çöker; sürekli toz.
        bool Crumble(int i, float local, ref int dusted)
        {
            Piece p = pieces[i];
            float sink = 0f;
            if (local > 0.8f)
            {
                float fall = local - 0.8f;
                sink = 0.5f * gravityPixels * 1.2f * fall * fall;
                if (dusted == 0) { StartDust(p); Burst(p, 24); dusted = 1; }
            }
            return Move(p, i, new Vector2(Shake(local > 0f ? 2.5f : shakePixels), -sink));
        }

        // Binayı tek parça kaydırır; tamamen yere girince gizler.
        bool Move(Piece p, int i, Vector2 offset)
        {
            SetTop(i, 0f, offset);
            SetBottom(p, offset);
            return Hide(p, -offset.y > p.breakPixels + p.topPixels + 2f);
        }

        // ---------------------------------------------------------------- yardımcılar
        void SetTop(int i, float angle, Vector2 offsetPixels)
        {
            Piece p = pieces[i];
            if (p.topPivot == null || !p.topPivot.gameObject.activeSelf) return;
            // Piksel sanatta pürüzsüz dönüş titrer; açıyı 3 derecelik basamaklara yuvarla.
            p.topPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(angle / 3f) * 3f);
            p.topPivot.localPosition = pivotRest[i] + new Vector3(Mathf.Round(offsetPixels.x), Mathf.Round(offsetPixels.y), 0f) / PixelsPerUnit;
        }

        static void SetBottom(Piece p, Vector2 offsetPixels)
        {
            if (p.bottom == null || !p.bottom.gameObject.activeSelf) return;
            p.bottom.localPosition = new Vector3(Mathf.Round(offsetPixels.x), Mathf.Round(offsetPixels.y), 0f) / PixelsPerUnit;
        }

        static bool Hide(Piece p, bool gone)
        {
            if (!gone) return false;
            if (p.topPivot != null) p.topPivot.gameObject.SetActive(false);
            if (p.bottom != null) p.bottom.gameObject.SetActive(false);
            return true;
        }

        static float Shake(float pixels) => Mathf.Round(Random.Range(-pixels, pixels));

        static void StartDust(Piece p)
        {
            if (p.dust != null && !p.dust.isPlaying) p.dust.Play();
        }

        static void Burst(Piece p, int count)
        {
            if (p.dust != null) p.dust.Emit(count);
        }

        // Basamaklı belirme (piksel sanata uygun, yumuşak geçiş yerine 4 adım).
        void SetCracks(float k)
        {
            if (cracks == null) return;
            cracks.enabled = k > 0f;
            Color c = cracks.color;
            c.a = Mathf.Ceil(Mathf.Clamp01(k) * 4f) / 4f;
            cracks.color = c;
        }
    }
}
