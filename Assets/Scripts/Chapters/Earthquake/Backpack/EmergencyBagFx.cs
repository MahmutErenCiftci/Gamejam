using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Afet çantası mini oyununun küçük, hafif animasyonları (0.1–0.5 sn, ölçeksiz zaman): büyüyüp oturma, nabız,
    /// uçuş, kıvılcım parçacıkları. Hepsi coroutine; sahibi MonoBehaviour StartCoroutine ile çalıştırır.
    /// </summary>
    internal static class EmergencyBagFx
    {
        public static float Dt => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        /// <summary>from ölçeğinden 1'e "OutBack" ile oturur.</summary>
        public static IEnumerator Pop(Transform t, float from = 1.3f, float seconds = 0.22f)
        {
            if (t == null) yield break;
            for (float x = 0f; x < seconds; x += Dt)
            {
                if (t == null) yield break;
                float k = EqUi.OutBack(x / seconds, 2.2f);
                float s = Mathf.LerpUnclamped(from, 1f, k);
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        /// <summary>Yumuşak zıplama: 1 → peak → 1 (yatayda hafif ezilme).</summary>
        public static IEnumerator Bounce(Transform t, float peak = 1.08f, float seconds = 0.3f)
        {
            if (t == null) yield break;
            for (float x = 0f; x < seconds; x += Dt)
            {
                if (t == null) yield break;
                float k = Mathf.Sin(x / seconds * Mathf.PI);
                float sy = 1f + (peak - 1f) * k;
                float sx = 1f - (peak - 1f) * 0.5f * k;
                t.localScale = new Vector3(sx, sy, 1f);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        /// <summary>Renk nabzı: count kez base ↔ pulse.</summary>
        public static IEnumerator Pulse(Graphic g, Color pulse, Color end, int count = 2, float seconds = 0.5f)
        {
            if (g == null) yield break;
            Color base0 = g.color;
            for (float x = 0f; x < seconds; x += Dt)
            {
                if (g == null) yield break;
                float k = Mathf.Abs(Mathf.Sin(x / seconds * Mathf.PI * count));
                g.color = Color.Lerp(base0, pulse, k);
                yield return null;
            }
            if (g != null) g.color = end;
        }

        /// <summary>RectTransform'u (aynı ebeveynde) hedef konuma uçurur; bitince onDone.</summary>
        public static IEnumerator Fly(RectTransform rt, Vector2 to, float seconds, System.Action onDone, float endScale = 1f)
        {
            if (rt == null) { onDone?.Invoke(); yield break; }
            Vector2 from = rt.anchoredPosition;
            Vector3 s0 = rt.localScale;
            for (float x = 0f; x < seconds; x += Dt)
            {
                if (rt == null) yield break;
                float k = EqUi.OutCubic(x / seconds);
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                float s = Mathf.Lerp(s0.x, endScale, k);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (rt != null)
            {
                rt.anchoredPosition = to;
                rt.localScale = new Vector3(endScale, endScale, 1f);
            }
            onDone?.Invoke();
        }

        public static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float seconds)
        {
            if (g == null) yield break;
            for (float x = 0f; x < seconds; x += Dt)
            {
                if (g == null) yield break;
                g.alpha = Mathf.Lerp(from, to, EqUi.OutCubic(x / seconds));
                yield return null;
            }
            if (g != null) g.alpha = to;
        }

        public static IEnumerator FadeGraphic(Graphic g, float from, float to, float seconds)
        {
            if (g == null) yield break;
            for (float x = 0f; x < seconds; x += Dt)
            {
                if (g == null) yield break;
                var c = g.color;
                c.a = Mathf.Lerp(from, to, x / seconds);
                g.color = c;
                yield return null;
            }
            if (g != null)
            {
                var c = g.color;
                c.a = to;
                g.color = c;
            }
        }

        /// <summary>Küçük piksel kıvılcımları: merkezden saçılır, düşerek söner, sonra yok edilir.</summary>
        public static IEnumerator Sparkles(RectTransform parent, Vector2 center, Sprite sprite, Color color, int count, float speed, float seconds)
        {
            if (parent == null) yield break;
            var parts = new RectTransform[count];
            var vel = new Vector2[count];
            var imgs = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var img = EqUi.Img("Spark", parent, sprite, color, null);
                img.color = color;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float size = sprite != null ? sprite.rect.width * 2f : 4f;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = center;
                float a = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.6f, 1.1f);
                parts[i] = rt;
                imgs[i] = img;
            }
            for (float x = 0f; x < seconds; x += Dt)
            {
                float dt = Dt;
                for (int i = 0; i < count; i++)
                {
                    if (parts[i] == null) continue;
                    vel[i].y -= speed * 2.2f * dt;
                    parts[i].anchoredPosition = EqUi.Snap(parts[i].anchoredPosition + vel[i] * dt);
                    var c = imgs[i].color;
                    c.a = 1f - x / seconds;
                    imgs[i].color = c;
                }
                yield return null;
            }
            foreach (var p in parts)
                if (p != null) Object.Destroy(p.gameObject);
        }
    }
}
