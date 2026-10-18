using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.UI
{
    /// <summary>
    /// uGUI Text için harf harf giriş efekti (piksel yazı tipleri için). Zamanı dışarıdan Elapsed ile sürülür (0 = ilk harf başlar).
    /// Drop: harfler soldan sağa sırayla yukarıdan düşer, yere değince 1 piksel sekip oturur (yağmur damlası gibi).
    /// Rise: yazı aşağıdan birkaç piksel yükselerek belirir (stagger 0 ise bütün harfler birlikte).
    /// Kayma her zaman tam font pikseline yuvarlanır (pixel = bir font pikselinin referans birimi), yazı bulanıklaşmaz.
    /// Brighten (0..1) harfleri anlık olarak beyaza yaklaştırır (ör. uzak şimşek).
    /// Gölge (Shadow) bu bileşenden SONRA eklenmeli: gölge harflerin hareketli halini kopyalasın.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class PixelLetterFx : BaseMeshEffect
    {
        public enum Motion { Drop, Rise }

        [SerializeField] private Motion motion = Motion.Drop;
        [Tooltip("Harfler arası gecikme (sn); 0 = hepsi birlikte")]
        [SerializeField] private float stagger = 0.07f;
        [Tooltip("Tek harfin giriş süresi (sn)")]
        [SerializeField] private float duration = 0.38f;
        [Tooltip("Başlangıç uzaklığı (font pikseli)")]
        [SerializeField] private float distance = 6f;
        [Tooltip("Bir font pikseli kaç referans birimi (Pixel Operator 16 px'te 1, 48 px'te 3)")]
        [SerializeField] private float pixel = 1f;

        private float time = 1e6f; // atanmazsa yazı normal görünür
        private float brighten;
        private int letters;

        public void Setup(Motion m, float letterStagger, float letterDuration, float fromPixels, float pixelUnits)
        {
            motion = m;
            stagger = Mathf.Max(0f, letterStagger);
            duration = Mathf.Max(0.01f, letterDuration);
            distance = fromPixels;
            pixel = Mathf.Max(0.01f, pixelUnits);
            Dirty();
        }

        /// <summary>Efekt zamanı (sn). Bütün harfler oturduktan sonra değişse de mesh yeniden kurulmaz.</summary>
        public float Elapsed
        {
            get => time;
            set
            {
                if (Mathf.Approximately(value, time)) return;
                bool same = Settled(time) && Settled(value);
                time = value;
                if (!same) Dirty();
            }
        }

        public float Brighten
        {
            get => brighten;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, brighten)) return;
                brighten = v;
                Dirty();
            }
        }

        /// <summary>Son harfin de oturduğu an (sn).</summary>
        public float EndTime => Mathf.Max(0, letters - 1) * stagger + duration;

        private bool Settled(float t) => t >= EndTime;

        private void Dirty()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            int quads = vh.currentVertCount / 4; // Text her görünen harf için 4 köşe üretir (boşluk üretmez)
            letters = quads;
            var v = new UIVertex();
            var white = Color.white;
            for (int q = 0; q < quads; q++)
            {
                Evaluate((time - q * stagger) / duration, out float y, out float a);
                float off = Mathf.Round(y) * pixel;
                for (int k = 0; k < 4; k++)
                {
                    int i = q * 4 + k;
                    vh.PopulateUIVertex(ref v, i);
                    v.position.y += off;
                    Color c = v.color;
                    if (brighten > 0f)
                    {
                        white.a = c.a;
                        c = Color.Lerp(c, white, brighten);
                    }
                    c.a *= a;
                    v.color = c;
                    vh.SetUIVertex(v, i);
                }
            }
        }

        /// <summary>p: harfin kendi ilerlemesi (0..1). y: font pikseli (yukarı +), a: alfa çarpanı.</summary>
        private void Evaluate(float p, out float y, out float a)
        {
            if (p <= 0f)
            {
                y = motion == Motion.Drop ? distance : -distance;
                a = 0f;
                return;
            }
            if (p >= 1f)
            {
                y = 0f;
                a = 1f;
                return;
            }
            if (motion == Motion.Drop)
            {
                a = Mathf.Clamp01(p / 0.3f);
                if (p < 0.7f)
                {
                    float f = p / 0.7f;
                    y = distance * (1f - f * f); // hızlanarak düşer
                }
                else if (p < 0.85f) y = Mathf.Lerp(0f, 1f, (p - 0.7f) / 0.15f); // 1 piksel seker
                else y = Mathf.Lerp(1f, 0f, (p - 0.85f) / 0.15f);
            }
            else
            {
                float f = 1f - (1f - p) * (1f - p) * (1f - p);
                y = -distance * (1f - f);
                a = Mathf.Clamp01(p / 0.6f);
            }
        }
    }
}
