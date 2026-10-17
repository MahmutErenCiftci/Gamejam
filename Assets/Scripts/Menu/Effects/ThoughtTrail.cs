using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Çocuğun başından rüya bulutlarına doğru sırayla beliren küçük düşünce baloncukları.
    public class ThoughtTrail : MonoBehaviour
    {
        [Tooltip("Küçükten büyüğe, başa en yakından başlayarak.")]
        [SerializeField] SpriteRenderer[] dots;
        [SerializeField] float interval = 0.12f;
        [SerializeField] float popDuration = 0.15f;

        Coroutine routine;

        public float TotalDuration => dots.Length * interval + popDuration;

        void Awake() => SetAll(0f);

        public void Show()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine());
        }

        public void Hide()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            SetAll(0f);
        }

        IEnumerator ShowRoutine()
        {
            SetAll(0f);
            for (int i = 0; i < dots.Length; i++)
            {
                for (float t = 0f; t < popDuration; t += Time.unscaledDeltaTime)
                {
                    Set(dots[i], t / popDuration);
                    yield return null;
                }
                Set(dots[i], 1f);
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, interval - popDuration));
            }
            routine = null;
        }

        void SetAll(float v) { foreach (SpriteRenderer dot in dots) Set(dot, v); }

        static void Set(SpriteRenderer dot, float v)
        {
            if (dot == null) return;
            Color c = dot.color;
            c.a = v;
            dot.color = c;
            dot.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, v);
        }
    }
}
