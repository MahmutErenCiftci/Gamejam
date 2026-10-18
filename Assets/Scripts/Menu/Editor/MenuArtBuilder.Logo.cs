using UnityEditor;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Oyunun adı: "EŞİK" logosu (UI/LogoStacked.png'nin üzerine yazar; GUID aynı kaldığı için menüdeki referanslar
    // bozulmaz). Çocuk kitabı kapağı gibi: her harf kâğıttan kesilmiş ayrı bir parça; uçları yuvarlak kalın
    // hatlar, makasla kesilmiş gibi hafif dalgalı kenar, her biri biraz farklı açıda ve yükseklikte yapıştırılmış.
    // Pastel guaj renkler (içinde kısa fırça izleri), beyaz kâğıt kenarı, sayfaya düşen yumuşak gölge; etrafta
    // birkaç küçük yıldız (uyuyan çocuk / rüya). 147x75 sanat pikseli, 2 kat büyütülüp 294x151'e yazılır.
    public static partial class MenuArtBuilder
    {
        const int ArtW = 147, ArtH = 75, ArtScale = 2;
        const int LogoW = ArtW * ArtScale, LogoH = ArtH * ArtScale + 1;
        const float PaperEdge = 1.8f;

        static readonly Color32 Paper = C(255, 248, 236), PaperShade = C(236, 222, 204);
        static readonly Color32 PaperShadow = new Color32(28, 16, 40, 120);
        static readonly Color32 Star = C(255, 240, 196), StarCore = C(255, 255, 255);

        // Bir harf: kalın hatlar (x0,y0,x1,y1; yerel, y aşağı), noktalar (x,y,yarıçap), renk (açık, ana, koyu),
        // tuvaldeki merkezi ve dönüş açısı.
        class PaperLetter
        {
            public Vector4[] strokes;
            public Vector3[] dots = new Vector3[0];
            public float width = 4.5f;
            public Color32 light, main, dark;
            public Color32? dotColor;
            public Vector2 center, pivot;
            public float angle;
        }

        static PaperLetter[] Letters() => new[]
        {
            // E — mercan
            new PaperLetter
            {
                strokes = new[] { new Vector4(5, 5, 5, 38), new Vector4(5, 5, 26, 5), new Vector4(5, 21.5f, 22, 21.5f), new Vector4(5, 38, 27, 38) },
                light = C(250, 172, 148), main = C(242, 131, 107), dark = C(206, 94, 78),
                pivot = new Vector2(16, 21), center = new Vector2(28, 37), angle = -5f,
            },
            // Ş — tereyağı sarısı; S tek akışlı yol, altında çengel
            new PaperLetter
            {
                strokes = StrokePath(26, 9, 20, 4, 11, 4, 5, 9, 5, 15, 11, 19.5f, 20, 22.5f, 26, 27, 26, 33, 20, 38.5f, 11, 38.5f, 4, 33),
                dots = new[] { new Vector3(15, 44, 2.2f), new Vector3(16.5f, 47, 2f), new Vector3(13.5f, 49.5f, 2f) },
                light = C(255, 231, 160), main = C(255, 205, 94), dark = C(222, 160, 58),
                pivot = new Vector2(15, 21), center = new Vector2(64, 41), angle = 4f,
            },
            // İ — nane yeşili gövde, sarı nokta
            new PaperLetter
            {
                strokes = new[] { new Vector4(6, 13, 6, 44) },
                dots = new[] { new Vector3(6, 2.5f, 4.5f) },
                dotColor = C(255, 205, 94),
                light = C(172, 228, 205), main = C(126, 204, 172), dark = C(84, 166, 136),
                pivot = new Vector2(6, 26), center = new Vector2(92, 33), angle = -3f,
            },
            // K — gök mavisi
            new PaperLetter
            {
                strokes = new[] { new Vector4(5, 5, 5, 38), new Vector4(25, 5, 9, 22), new Vector4(13, 20, 26, 38) },
                light = C(170, 210, 244), main = C(126, 182, 234), dark = C(88, 140, 200),
                pivot = new Vector2(16, 21), center = new Vector2(119, 38), angle = 6f,
            },
        };

        static Vector4[] StrokePath(params float[] p)
        {
            var s = new Vector4[p.Length / 2 - 1];
            for (int i = 0; i < s.Length; i++) s[i] = new Vector4(p[i * 2], p[i * 2 + 1], p[i * 2 + 2], p[i * 2 + 3]);
            return s;
        }

        // Yıldızlar: tuval x, y, boy (1 = küçük artı, 2 = dört kollu).
        static readonly Vector3Int[] Stars =
        {
            new Vector3Int(9, 14, 2), new Vector3Int(46, 8, 1), new Vector3Int(137, 12, 2), new Vector3Int(141, 30, 1),
            new Vector3Int(79, 66, 1),
        };

        [MenuItem("AfetUyanisi/Menü/Logoyu Kur (EŞİK)")]
        public static void BuildLogoOnly()
        {
            string path = Save(Logo(), "UI/LogoStacked");
            AssetDatabase.Refresh();
            ConfigureImporter(path, Center);
            Debug.Log("[MenuArtBuilder] EŞİK logosu yazıldı: " + path);
        }

        static Texture2D Logo()
        {
            var letters = Letters();
            var owner = new int[ArtW, ArtH];   // 0 = boş, i+1 = harf, -(i+1) = harfin noktası
            var paper = new bool[ArtW, ArtH];

            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                    for (int i = 0; i < letters.Length; i++)
                    {
                        var L = letters[i];
                        // Makasla kesilmiş kenar: yavaş değişen ±0.5 piksellik oynama.
                        float wobble = (Mathf.PerlinNoise(x * 0.23f + i * 17f, y * 0.23f) - 0.5f) * 1.1f;
                        Vector2 p = ToLocal(L, x + 0.5f, y + 0.5f);
                        float d = StrokeDistance(L, p) - L.width - wobble;
                        float dd = DotDistance(L, p) - wobble;
                        float inside = Mathf.Min(d, dd);
                        if (inside < PaperEdge) paper[x, y] = true;
                        if (inside < 0f && owner[x, y] == 0) owner[x, y] = dd < d ? -(i + 1) : i + 1;
                    }

            var t = New(LogoW, LogoH);
            void Put(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= ArtW || y >= ArtH) return;
                for (int sy = 0; sy < ArtScale; sy++)
                    for (int sx = 0; sx < ArtScale; sx++)
                        Px(t, x * ArtScale + sx, y * ArtScale + sy, c);
            }

            // Sayfaya düşen gölge: kâğıdın sağ-aşağı kaydırılmışı.
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    int sx = x - 1, sy = y - 2;
                    if (!paper[x, y] && sx >= 0 && sy >= 0 && paper[sx, sy]) Put(x, y, PaperShadow);
                }

            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    if (!paper[x, y]) continue;
                    int o = owner[x, y];
                    if (o == 0)
                    {
                        // Kâğıt kenarı: alt kenarı bir ton gölgeli
                        bool lower = y + 1 < ArtH && !paper[x, y + 1];
                        Put(x, y, lower ? PaperShade : Paper);
                        continue;
                    }
                    var L = letters[Mathf.Abs(o) - 1];
                    bool isDot = o < 0 && L.dotColor.HasValue;
                    Color32 main = isDot ? L.dotColor.Value : L.main;
                    Color32 light = isDot ? Mix(L.dotColor.Value, Paper, 0.45f) : L.light;
                    Color32 dark = isDot ? Shade(L.dotColor.Value, 0.82f) : L.dark;

                    bool edgeUp = y == 0 || owner[x, y - 1] != o;
                    bool edgeDown = y + 1 >= ArtH || owner[x, y + 1] != o || (y + 2 < ArtH && owner[x, y + 2] != o);
                    Color32 c = main;
                    if (edgeDown) c = dark;
                    else if (edgeUp) c = light;
                    else if (Brush(x, y, Mathf.Abs(o))) c = light;
                    Put(x, y, c);
                }

            foreach (var s in Stars) DrawStar(Put, s.x, s.y, s.z);
            return Done(t);
        }

        static Vector2 ToLocal(PaperLetter L, float x, float y)
        {
            float a = -L.angle * Mathf.Deg2Rad;
            float dx = x - L.center.x, dy = y - L.center.y;
            return new Vector2(dx * Mathf.Cos(a) - dy * Mathf.Sin(a), dx * Mathf.Sin(a) + dy * Mathf.Cos(a)) + L.pivot;
        }

        static float StrokeDistance(PaperLetter L, Vector2 p)
        {
            float best = 999f;
            foreach (var s in L.strokes)
            {
                Vector2 a = new Vector2(s.x, s.y), b = new Vector2(s.z, s.w), ab = b - a;
                float u = ab.sqrMagnitude < 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * u));
            }
            return best;
        }

        static float DotDistance(PaperLetter L, Vector2 p)
        {
            float best = 999f;
            foreach (var d in L.dots) best = Mathf.Min(best, Vector2.Distance(p, new Vector2(d.x, d.y)) - d.z);
            return best;
        }

        // Guaj fırça izi: satır boyunca kısa, açık tonlu çizgiler.
        static bool Brush(int x, int y, int letter)
        {
            int run = Mathf.FloorToInt((x + Hash(y, letter) * 9f) / 5f);
            return Hash(run, y, letter, 3) < 0.07f;
        }

        static void DrawStar(System.Action<int, int, Color32> put, int x, int y, int size)
        {
            put(x, y, StarCore);
            for (int k = 1; k <= size; k++)
            {
                Color32 c = k == size ? Star : StarCore;
                put(x - k, y, c); put(x + k, y, c); put(x, y - k, c); put(x, y + k, c);
            }
        }
    }
}
