using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel arayüzünün piksel parçaları (SEL3-30): kodla çizilen küçük sprite'lar ([E] tuş kapağı, kıvılcım, yıldız,
    /// yön oku), 9 dilimli sprite Image'ı, Pixelify metni ve tam piksele oturan yaylanma eğrileri. Renkler deprem
    /// arayüzüyle (EqUi) aynı; sprite'lar Point filtreli, 1 sprite pikseli = 1 referans pikseli (640x360).
    /// </summary>
    public static class FePixel
    {
        public static readonly Color Ink = FeUi.Hex(0x14141F);
        public static readonly Color Navy = FeUi.Hex(0x263559);
        public static readonly Color Cream = FeUi.Hex(0xF4EBD8);
        public static readonly Color CreamShade = FeUi.Hex(0xDDCFB2);
        public static readonly Color Red = FeUi.Hex(0xE8392B);
        public static readonly Color Amber = FeUi.Hex(0xF2A33A);
        public static readonly Color Gold = FeUi.Hex(0xFFD27E);
        public static readonly Color Green = FeUi.Hex(0x18A957);
        public static readonly Color GreenLight = FeUi.Hex(0x5BE38C);
        public static readonly Color ShadowRed = FeUi.Hex(0x5A0F0A);
        public static readonly Color ShadowGreen = FeUi.Hex(0x0A3C1E);

        private static Sprite keyUp, keyDown, spark, star, arrow;

        // [E] tuş kapağı 15x16: krem yüz, sol-üst vurgu, sağ/alt gölge, 2 px derinlik, altta yarı saydam gölge.
        private static readonly string[] KeyUpRows =
        {
            "..KKKKKKKKKKK..",
            ".KhhhhhhhhhhhK.",
            "KhcccccccccccsK",
            "KhcccKKKKKcccsK",
            "KhcccKcccccccsK",
            "KhcccKcccccccsK",
            "KhcccKKKKccccsK",
            "KhcccKcccccccsK",
            "KhcccKcccccccsK",
            "KhcccKKKKKcccsK",
            "KhcccccccccccsK",
            "KsssssssssssssK",
            "KDDDDDDDDDDDDDK",
            "KDDDDDDDDDDDDDK",
            ".KKKKKKKKKKKKK.",
            "..xxxxxxxxxxx..",
        };

        public static Sprite KeyUp => keyUp != null ? keyUp : keyUp = Make(KeyUpRows);

        /// <summary>Basılı kapak: yüz 2 px iner, derinlik kaybolur.</summary>
        public static Sprite KeyDown
        {
            get
            {
                if (keyDown != null) return keyDown;
                var rows = new List<string> { "...............", "..............." };
                for (int i = 0; i <= 11; i++) rows.Add(KeyUpRows[i]);
                rows.Add(KeyUpRows[14]);
                rows.Add(KeyUpRows[15]);
                return keyDown = Make(rows.ToArray());
            }
        }

        /// <summary>3x3 beyaz kare (renk Image.color ile verilir).</summary>
        public static Sprite Spark => spark != null ? spark : spark = Make("www", "www", "www");

        /// <summary>5x5 beyaz artı yıldız (renk Image.color ile).</summary>
        public static Sprite Star => star != null ? star : star = Make("..w..", "..w..", "wwwww", "..w..", "..w..");

        /// <summary>5x7 sağ ok (görev şeridi: "bu yönde ilerle").</summary>
        public static Sprite Arrow => arrow != null ? arrow : arrow = Make("KK...", "KaK..", "KaaK.", "KaaaK", "KaaK.", "KaK..", "KK...");

        private static Sprite Make(params string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FePixel" };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, h - 1 - y, ColorOf(rows[y][x]));
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Color ColorOf(char c)
        {
            switch (c)
            {
                case 'K': return Ink;
                case 'h': return Color.white;
                case 'c': return Cream;
                case 's': return CreamShade;
                case 'D': return FeUi.Hex(0x9A8C70);
                case 'x': return FeUi.A(Ink, 0.45f);
                case 'a': return Amber;
                case 'w': return Color.white;
                default: return Color.clear;
            }
        }

        /// <summary>FeUi canvas'ı + tam sayı piksel ölçeği (FePixelCanvasScaler: 1080p = 3x, 480x270 = 1x).</summary>
        public static Canvas CreateCanvas(Transform owner, string name, int order, out CanvasGroup group)
        {
            var canvas = FeUi.CreateCanvas(owner, name, order, out group);
            canvas.pixelPerfect = true;
            canvas.gameObject.AddComponent<FePixelCanvasScaler>();
            return canvas;
        }

        /// <summary>Sprite'lı Image; border'ı varsa 9 dilimli (1 sprite pikseli = 1 referans pikseli).</summary>
        public static Image Sliced(string name, Transform parent, Sprite sprite, Color? color = null)
        {
            var img = FeUi.Img(name, parent, color ?? Color.white);
            img.sprite = sprite;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.pixelsPerUnitMultiplier = 1f;
            }
            return img;
        }

        /// <summary>Doğal boyutlu sprite Image'ı (ikon, tuş kapağı).</summary>
        public static Image Icon(string name, Transform parent, Sprite sprite, Color? color = null)
        {
            var img = Sliced(name, parent, sprite, color);
            if (sprite != null) img.rectTransform.sizeDelta = sprite.rect.size;
            return img;
        }

        /// <summary>Pixelify metni; gölge 1 px sağ-alt (deprem arayüzü gibi).</summary>
        public static Text Txt(string name, Transform parent, Font font, int size, Color color, TextAnchor align, Color shadow)
        {
            var t = FeUi.Txt(name, parent, font, size, color, align, false);
            t.lineSpacing = 1f;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = shadow;
            sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        /// <summary>Hafif taşan giriş (0→1, sonda ~%10 aşıp geri gelir).</summary>
        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Düşüp sekme (0→1).</summary>
        public static float OutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            const float n = 7.5625f, d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
            if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
            t -= 2.625f / d;
            return n * t * t + 0.984375f;
        }

        /// <summary>Sönümlenen sarsıntı (tam piksel): t saniye, süre dur, genlik amp.</summary>
        public static float Shake(float t, float dur, float amp, float freq = 38f)
        {
            if (t >= dur) return 0f;
            return Mathf.Round(Mathf.Sin(t * freq) * amp * (1f - t / dur));
        }
    }

    /// <summary>
    /// Arayüz içi küçük piksel parçacıkları (kıvılcım, yıldız): havuzlu Image'lar, yerçekimi, tam piksel konum, sönme.
    /// Sahibi her karede Tick(dt) çağırır; parçacıklar raycast almaz.
    /// </summary>
    public sealed class FePixelBurst
    {
        private sealed class P { public Image img; public Vector2 pos, vel; public float life, max; public bool on; }

        private readonly RectTransform root;
        private readonly List<P> pool = new List<P>();
        private readonly float gravity;

        public FePixelBurst(RectTransform parent, string name, float gravity = -260f)
        {
            root = FeUi.Rect(name, parent);
            FeUi.Stretch(root);
            this.gravity = gravity;
        }

        /// <summary>origin (root'a göre, sol-alt orijinli değil: root merkezli) çevresinde count parçacık.</summary>
        public void Emit(Vector2 origin, int count, Sprite sprite, Color[] colors, Vector2 dir, float spreadDeg, float speedMin, float speedMax, float life)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Get(sprite);
                float ang = Mathf.Atan2(dir.y, dir.x) + Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad;
                float sp = Random.Range(speedMin, speedMax);
                p.pos = origin;
                p.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
                p.max = p.life = life * Random.Range(0.75f, 1.15f);
                p.img.color = colors[Random.Range(0, colors.Length)];
                p.on = true;
                p.img.enabled = true;
                Place(p);
            }
        }

        public void Tick(float dt)
        {
            foreach (var p in pool)
            {
                if (!p.on) continue;
                p.life -= dt;
                if (p.life <= 0f) { p.on = false; p.img.enabled = false; continue; }
                p.vel.y += gravity * dt;
                p.pos += p.vel * dt;
                var c = p.img.color;
                c.a = Mathf.Clamp01(p.life / p.max * 2.5f); // ömrünün son %40ında söner
                p.img.color = c;
                Place(p);
            }
        }

        public void Clear()
        {
            foreach (var p in pool) { p.on = false; p.img.enabled = false; }
        }

        private P Get(Sprite sprite)
        {
            foreach (var p in pool)
                if (!p.on && p.img.sprite == sprite) return p;
            var img = FePixel.Icon("P", root, sprite);
            img.enabled = false;
            var np = new P { img = img };
            pool.Add(np);
            return np;
        }

        private static void Place(P p) =>
            p.img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.pos.x), Mathf.Round(p.pos.y));
    }
}
