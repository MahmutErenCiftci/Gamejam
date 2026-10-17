using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Dünya uzayındaki bir grup sprite'ın ortak saydamlığı (UI'daki CanvasGroup'un sprite karşılığı).
    // Intro karanlığı, logo ve düşünce baloncukları bunu kullanır. Renderer'lar Awake'te bir kez toplanır.
    public class SpriteFader : MonoBehaviour
    {
        [Range(0f, 1f)]
        [SerializeField] float alpha = 1f;

        SpriteRenderer[] renderers;
        float[] baseAlphas;

        public float Alpha
        {
            get => alpha;
            set { alpha = Mathf.Clamp01(value); Apply(); }
        }

        void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseAlphas[i] = renderers[i].color.a;
            Apply();
        }

        void Apply()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = renderers[i].color;
                c.a = baseAlphas[i] * alpha;
                renderers[i].color = c;
            }
        }
    }
}
