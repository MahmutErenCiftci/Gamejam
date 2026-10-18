using UnityEditor;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Oyunun adı: "EŞİK" logosu (UI/LogoStacked.png'nin üzerine yazar; GUID aynı kaldığı için menüdeki referanslar
    // bozulmaz). 2x2 piksellik hücrelerle çizilir (147x75 hücre → 294x151, eski logoyla aynı boyut).
    // Harflerin üstü gün batımı sıcaklığında, alt üçte biri basamaklı bir su çizgisinin altında sele gömülü mavi:
    // eşik = suyun yükseldiği çizgi. Koyu dış hat + aşağı-sağa blok gölge menüdeki diğer piksel sanatla uyumlu.
    public static partial class MenuArtBuilder
    {
        const int LogoW = 147, LogoH = 75;
        const int CapTop = 13, CapH = 42;
        const int WaterLine = 29; // büyük harf tepesinden hücre
        const int Depth = 4;

        static readonly Color32 LogoOutline = C(26, 16, 34);
        static readonly Color32 LogoShadow = new Color32(12, 6, 20, 130);
        static readonly Color32[] Warm = { C(255, 227, 163), C(255, 200, 97), C(255, 166, 61), C(242, 128, 46) };
        static readonly Color32 WarmLight = C(255, 248, 225), WarmDark = C(201, 86, 42);
        static readonly Color32 Foam = C(233, 247, 255);
        static readonly Color32[] Water = { C(124, 200, 236), C(84, 160, 216), C(61, 122, 190) };
        static readonly Color32 WaterLight = C(168, 222, 246), WaterDark = C(42, 86, 150);
        static readonly Color32[] WarmSide = { C(122, 51, 64), C(84, 32, 52) };
        static readonly Color32[] WaterSide = { C(35, 70, 120), C(24, 48, 88) };

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
            var face = new bool[LogoW, LogoH];
            int x = 11;
            LetterE(face, x); x += 30 + 5;
            LetterS(face, x); x += 33 + 5;
            LetterI(face, x); x += 11 + 5;
            LetterK(face, x);

            var cell = new Color32[LogoW, LogoH];
            var filled = new bool[LogoW, LogoH];

            // Blok gölge: önce derin katman, üstüne yakın katman.
            for (int k = Depth; k >= 1; k--)
                for (int y = 0; y < LogoH; y++)
                    for (int cx = 0; cx < LogoW; cx++)
                    {
                        if (!face[cx, y]) continue;
                        int tx = cx + k / 2, ty = y + k;
                        if (tx >= LogoW || ty >= LogoH || face[tx, ty]) continue;
                        bool under = y - CapTop > WaterAt(cx);
                        cell[tx, ty] = (under ? WaterSide : WarmSide)[k <= 2 ? 0 : 1];
                        filled[tx, ty] = true;
                    }

            for (int y = 0; y < LogoH; y++)
                for (int cx = 0; cx < LogoW; cx++)
                    if (face[cx, y])
                    {
                        cell[cx, y] = FaceColor(face, cx, y);
                        filled[cx, y] = true;
                    }

            // Dış hat (8 komşu): yapıştırma etiketi gibi kalın ve net.
            var outline = new bool[LogoW, LogoH];
            for (int y = 0; y < LogoH; y++)
                for (int cx = 0; cx < LogoW; cx++)
                {
                    if (filled[cx, y]) continue;
                    for (int dy = -1; dy <= 1 && !outline[cx, y]; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < LogoW && ny < LogoH && filled[nx, ny]) { outline[cx, y] = true; break; }
                        }
                }

            var t = New(LogoW * 2, LogoH * 2 + 1);
            for (int y = 0; y < LogoH; y++)
                for (int cx = 0; cx < LogoW; cx++)
                {
                    bool solid = filled[cx, y] || outline[cx, y];
                    if (!solid)
                    {
                        // Yarı saydam düşen gölge: logo kalabalık şehir ve koyu gökyüzü üstünde de ayrışsın.
                        int sx0 = cx - 1, sy0 = y - 2;
                        if (sx0 >= 0 && sy0 >= 0 && (filled[sx0, sy0] || outline[sx0, sy0]))
                            for (int sy = 0; sy < 2; sy++)
                                for (int sx = 0; sx < 2; sx++)
                                    Px(t, cx * 2 + sx, y * 2 + sy, LogoShadow);
                        continue;
                    }
                    Color32 c = filled[cx, y] ? cell[cx, y] : LogoOutline;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            Px(t, cx * 2 + sx, y * 2 + sy, c);
                }
            return Done(t);
        }

        // Su çizgisinin büyük harf tepesine göre yüksekliği: kısa, basamaklı dalga.
        static int WaterAt(int cx) => WaterLine + Mathf.RoundToInt(Mathf.Sin(cx * Mathf.PI * 2f / 14f) * 1.2f);

        static Color32 FaceColor(bool[,] face, int cx, int y)
        {
            int yr = y - CapTop, wl = WaterAt(cx);
            bool top = y == 0 || !face[cx, y - 1];
            bool bottom = y == LogoH - 1 || !face[cx, y + 1];
            bool right = cx == LogoW - 1 || !face[cx + 1, y];

            Color32 c;
            if (yr == wl) return Foam;
            if (yr < wl)
            {
                c = Warm[Mathf.Clamp(yr < 0 ? 0 : yr / 8, 0, Warm.Length - 1)];
                if (top) return WarmLight;
                if (bottom) return WarmDark;
            }
            else
            {
                int d = yr - wl;
                c = Water[d <= 2 ? 0 : d <= 7 ? 1 : 2];
                if (top) return WaterLight;
                if (bottom) return WaterDark;
            }
            return right ? Shade(c, 0.86f) : c;
        }

        // ---- Harfler (hücre; x sol kenar, y büyük harf tepesine göre)

        const int Bar = 8, Stem = 10, Mid = 17;

        static void LetterE(bool[,] m, int x)
        {
            Box(m, x, 0, x + Stem, CapH);
            Box(m, x, 0, x + 30, Bar);
            Box(m, x, Mid, x + 25, Mid + Bar);
            Box(m, x, CapH - Bar, x + 30, CapH);
            ChamferTL(m, x, 0, 3);
            ChamferBL(m, x, CapH - 1, 3);
        }

        static void LetterS(bool[,] m, int x)
        {
            const int w = 33;
            Box(m, x, 0, x + w, Bar);                  // üst
            Box(m, x, 0, x + Stem, Mid + Bar);         // sol üst gövde
            Box(m, x + w - Stem, Bar, x + w, Bar + 4); // üst uç
            Box(m, x, Mid, x + w, Mid + Bar);           // orta
            Box(m, x + w - Stem, Mid, x + w, CapH);    // sağ alt gövde
            Box(m, x, CapH - Bar, x + w, CapH);        // alt
            Box(m, x, CapH - Bar - 4, x + Stem, CapH - Bar); // alt uç
            ChamferTL(m, x, 0, 4);
            ChamferBR(m, x + w - 1, CapH - 1, 4);
            // Çengel: alt çubuktan iner, sağa kıvrılır, sola döner.
            Box(m, x + 15, CapH, x + 19, CapH + 3);
            Box(m, x + 17, CapH + 3, x + 21, CapH + 5);
            Box(m, x + 12, CapH + 5, x + 20, CapH + 8);
        }

        static void LetterI(bool[,] m, int x)
        {
            Box(m, x, 0, x + 11, CapH);
            Box(m, x, -11, x + 11, -3); // nokta
            ChamferTL(m, x, -11, 2);
        }

        static void LetterK(bool[,] m, int x)
        {
            Box(m, x, 0, x + Stem, CapH);
            Poly(m, x, new Vector2(9, 15), new Vector2(20, 0), new Vector2(33, 0), new Vector2(15, 25), new Vector2(9, 25));
            Poly(m, x, new Vector2(12, 19), new Vector2(21, 19), new Vector2(33, 37), new Vector2(33, 42), new Vector2(21, 42), new Vector2(9, 26));
        }

        static void Box(bool[,] m, int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y < y1; y++)
                for (int cx = x0; cx < x1; cx++)
                    Set(m, cx, y, true);
        }

        // Dolu çokgen (hücre merkezi içindeyse dolu); noktalar harfin sol üstüne göre.
        static void Poly(bool[,] m, int ox, params Vector2[] pts)
        {
            for (int y = -12; y < CapH + 12; y++)
                for (int cx = 0; cx < 40; cx++)
                {
                    float px = cx + 0.5f, py = y + 0.5f;
                    bool inside = false;
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                        if ((pts[i].y > py) != (pts[j].y > py) &&
                            px < (pts[j].x - pts[i].x) * (py - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                            inside = !inside;
                    if (inside) Set(m, ox + cx, y, true);
                }
        }

        static void ChamferTL(bool[,] m, int x, int y, int n) { for (int i = 0; i < n; i++) for (int j = 0; j < n - i; j++) Set(m, x + i, y + j, false); }
        static void ChamferBL(bool[,] m, int x, int y, int n) { for (int i = 0; i < n; i++) for (int j = 0; j < n - i; j++) Set(m, x + i, y - j, false); }
        static void ChamferBR(bool[,] m, int x, int y, int n) { for (int i = 0; i < n; i++) for (int j = 0; j < n - i; j++) Set(m, x - i, y - j, false); }

        static void Set(bool[,] m, int cx, int y, bool v)
        {
            int cy = y + CapTop;
            if (cx >= 0 && cy >= 0 && cx < LogoW && cy < LogoH) m[cx, cy] = v;
        }
    }
}
