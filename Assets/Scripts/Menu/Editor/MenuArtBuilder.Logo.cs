using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Oyunun adı: "EŞİK" logosu (UI/LogoStacked.png'nin üzerine yazar; GUID aynı kaldığı için menüdeki referanslar
    // bozulmaz). 294x151, menünün piksel yoğunluğunda (1 doku pikseli = 960x540 sanal ekranda 1 piksel).
    // Kalın, sağa yatık harfler tek parça siyah dış hat + aşağı-sola blok gölgeyle sarılır; üst yarı sıcak beyaz,
    // alt yarı fırça çizikli soğuk gri-mavi. Kelimeyi parlayan bir fay çatlağı yarar ve sağa sivrilerek uzar
    // (deprem); harflerin altından su damlar (sel). Dış hatta turuncu baskı kayması.
    public static partial class MenuArtBuilder
    {
        const int LogoW = 294, LogoH = 151;
        const int CapH = 80;          // büyük harf yüksekliği (yerel koordinat, y aşağı)
        const float Italic = 0.2f;    // her satır yukarıda bu kadar sağa
        const int LogoX = 33, LogoY = 33; // yerel (0,0) = ilk harfin sol üstü (eğimsiz)
        const int Outline = 3, Extrude = 6;

        static readonly Color32 Ink = C(20, 12, 28);
        static readonly Color32 Misprint = C(255, 122, 61);
        static readonly Color32 TopFace = C(255, 245, 228), TopRim = C(255, 255, 255), TopEdge = C(230, 214, 195);
        static readonly Color32 LowFace = C(174, 187, 216), LowScratch = C(147, 160, 194), LowEdge = C(126, 138, 178), LowRim = C(205, 214, 236);
        static readonly Color32 CrackCore = C(255, 240, 190), CrackGlow = C(255, 138, 61), CrackDeep = C(214, 74, 38);

        [MenuItem("AfetUyanisi/Menü/Logoyu Kur (EŞİK)")]
        public static void BuildLogoOnly()
        {
            string path = Save(Logo(), "UI/LogoStacked");
            AssetDatabase.Refresh();
            ConfigureImporter(path, Center);
            Debug.Log("[MenuArtBuilder] EŞİK logosu yazıldı: " + path);
        }

        // ---- Harfler: yerel koordinat (x sağa, y aşağı, 0 = büyük harf tepesi, CapH = taban çizgisi)

        static readonly List<Vector2[]> Glyphs = new List<Vector2[]>();

        static void Glyph(float ox, params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(ox + xy[i * 2], xy[i * 2 + 1]);
            Glyphs.Add(p);
        }

        static void BuildGlyphs()
        {
            Glyphs.Clear();
            // E — üst çubuğun ucu eğik kesik, orta çubuk kısa
            Glyph(0, 0, 4, 4, 0, 56, 0, 52, 17, 21, 17, 21, 31, 47, 31, 45, 48, 21, 48, 21, 63, 55, 63, 55, 80, 4, 80, 0, 76);
            // Ş — köşeleri pahlı, açıklıkları sağ üst / sol altta
            Glyph(61, 12, 0, 60, 0, 60, 17, 24, 17, 22, 19, 22, 29, 24, 31, 48, 31, 60, 39, 60, 68, 50, 80, 0, 80, 0, 63,
                36, 63, 38, 61, 38, 51, 36, 49, 12, 49, 0, 41, 0, 12);
            // Ş çengeli: alt çubuktan iner, sağa kıvrılır, sola döner
            Glyph(61, 22, 80, 34, 80, 34, 84, 40, 89, 39, 97, 31, 101, 15, 101, 15, 95, 28, 94, 29, 90, 22, 86);
            // İ + nokta
            Glyph(126, 0, 0, 22, 0, 22, 80, 0, 80);
            Glyph(126, 0, -24, 22, -24, 22, -9, 0, -9);
            // K — gövde, kol, bacak
            Glyph(154, 0, 0, 21, 0, 21, 80, 0, 80);
            Glyph(154, 19, 30, 39, 0, 63, 0, 35, 45, 19, 55);
            Glyph(154, 24, 46, 38, 37, 63, 80, 38, 80);
        }

        // Damlalar (sel): taban çizgisinden düz aşağı akar. x = yerel, genişlik, uzunluk.
        static readonly Vector3Int[] Drips =
        {
            new Vector3Int(9, 5, 11), new Vector3Int(43, 4, 6), new Vector3Int(108, 5, 12),
            new Vector3Int(137, 5, 9), new Vector3Int(160, 5, 13), new Vector3Int(206, 4, 7),
        };

        // Fay çatlağı (deprem): tuval koordinatı. Sol uçta kısa başlar, sağda harflerin dışına sivrilerek uzar.
        static readonly Vector2[] Crack =
        {
            new Vector2(18, 85), new Vector2(44, 80), new Vector2(52, 89), new Vector2(67, 84), new Vector2(90, 74),
            new Vector2(95, 80), new Vector2(121, 72), new Vector2(133, 66), new Vector2(139, 75), new Vector2(163, 63),
            new Vector2(176, 58), new Vector2(181, 67), new Vector2(208, 55), new Vector2(224, 52), new Vector2(230, 58),
            new Vector2(290, 36),
        };

        static Texture2D Logo()
        {
            BuildGlyphs();
            var face = new bool[LogoW, LogoH];
            for (int y = 0; y < LogoH; y++)
                for (int x = 0; x < LogoW; x++)
                    {
                    // Fay kayması: çatlağın altındaki parça 2 piksel sola, 1 piksel aşağı kaymış.
                    bool below = y + 0.5f > CrackY(x + 0.5f);
                    int sx = below ? x + 2 : x, sy = below ? y - 1 : y;
                    face[x, y] = InGlyph(sx, sy) || InDrip(sx, sy);
                }

            // Çatlak harfleri yarar: çevresindeki 3 piksel boşalır (dış hat rengiyle dolar).
            var crackD = new float[LogoW, LogoH];
            var crackT = new float[LogoW, LogoH];
            for (int y = 0; y < LogoH; y++)
                for (int x = 0; x < LogoW; x++)
                {
                    CrackDistance(x + 0.5f, y + 0.5f, out crackD[x, y], out crackT[x, y]);
                    if (crackD[x, y] < 0.75f * CrackWidth(crackT[x, y]) + 1.5f) face[x, y] = false;
                }

            // Siluet = yüz + kalın dış hat; blok gölge siluetin aşağı-sola kaydırılmışı.
            var sil = Dilate(face, Outline);
            var ink = new bool[LogoW, LogoH];
            for (int y = 0; y < LogoH; y++)
                for (int x = 0; x < LogoW; x++)
                {
                    if (!sil[x, y]) continue;
                    for (int k = 0; k <= Extrude; k++)
                    {
                        int tx = x - k / 2, ty = y + k;
                        if (tx >= 0 && ty < LogoH) ink[tx, ty] = true;
                    }
                }

            var rng = new System.Random(7);
            var scratch = new bool[LogoW, LogoH];
            for (int i = 0; i < 70; i++)
            {
                int sx = rng.Next(LogoX, LogoX + 240), sy = rng.Next(LogoY + 44, LogoY + CapH);
                int len = rng.Next(4, 13);
                for (int j = 0; j < len; j++)
                {
                    int px = sx + j, py = sy - (j * 3) / 10;
                    if (px < LogoW && py >= 0) scratch[px, py] = true;
                }
            }

            var t = New(LogoW, LogoH);
            for (int y = 0; y < LogoH; y++)
                for (int x = 0; x < LogoW; x++)
                {
                    Color32? c = null;
                    // Baskı kayması: siyah kütlenin sağ-üstünde ince turuncu şerit.
                    int mx = x - 3, my = y + 2;
                    if (mx >= 0 && my < LogoH && ink[mx, my]) c = Misprint;
                    if (ink[x, y]) c = Ink;
                    if (face[x, y]) c = FaceColor(face, scratch, x, y);

                    // Çatlağın parlayan çekirdeği her yerde; turuncu hale harflerin dışında.
                    float w = CrackWidth(crackT[x, y]);
                    if (crackD[x, y] < 0.75f * w) c = CrackCore;
                    else if (crackD[x, y] < 1.6f * w && !face[x, y]) c = crackD[x, y] < 1.2f * w ? CrackGlow : CrackDeep;

                    if (c.HasValue) Px(t, x, y, c.Value);
                }
            return Done(t);
        }

        static bool InGlyph(int x, int y)
        {
            float ly = y + 0.5f - LogoY;
            float lx = x + 0.5f - LogoX - (CapH - ly) * Italic;
            foreach (var g in Glyphs)
                if (Inside(g, lx, ly)) return true;
            return false;
        }

        static bool InDrip(int x, int y)
        {
            float ly = y + 0.5f - LogoY, lx = x + 0.5f - LogoX;
            foreach (var d in Drips)
            {
                float half = d.y * 0.5f, cx = d.x;
                if (ly >= CapH - 2 && ly <= CapH + d.z && Mathf.Abs(lx - cx) <= half) return true;
                // Uçta yuvarlak damla
                float bx = lx - cx, by = ly - (CapH + d.z);
                if (bx * bx + by * by <= (half + 1f) * (half + 1f)) return true;
            }
            return false;
        }

        static bool Inside(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
        }

        static void CrackDistance(float x, float y, out float dist, out float along)
        {
            dist = 999f; along = 0f;
            float total = 0f, run = 0f;
            for (int i = 1; i < Crack.Length; i++) total += Vector2.Distance(Crack[i - 1], Crack[i]);
            var p = new Vector2(x, y);
            for (int i = 1; i < Crack.Length; i++)
            {
                Vector2 a = Crack[i - 1], b = Crack[i];
                float len = Vector2.Distance(a, b);
                float u = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (len * len));
                float d = Vector2.Distance(p, a + (b - a) * u);
                if (d < dist) { dist = d; along = (run + u * len) / total; }
                run += len;
            }
        }

        // Çatlağın x'teki yüksekliği (çizgi soldan sağa ilerler).
        static float CrackY(float x)
        {
            if (x <= Crack[0].x) return Crack[0].y;
            for (int i = 1; i < Crack.Length; i++)
                if (x <= Crack[i].x) return Mathf.Lerp(Crack[i - 1].y, Crack[i].y, (x - Crack[i - 1].x) / (Crack[i].x - Crack[i - 1].x));
            return Crack[Crack.Length - 1].y;
        }

        // Çatlak kalınlığı: başta incecik, ortada tam, sağ uçta bıçak gibi sivrilir.
        static float CrackWidth(float t) => Mathf.Min(Mathf.Clamp01(t / 0.08f), Mathf.Clamp01((1f - t) / 0.22f)) * 2f + 0.2f;

        static Color32 FaceColor(bool[,] face, bool[,] scratch, int x, int y)
        {
            bool F(int ax, int ay) => ax >= 0 && ay >= 0 && ax < LogoW && ay < LogoH && face[ax, ay];
            // Üst/alt ayrımı sağa doğru hafifçe yükselir.
            float split = LogoY + 45 - (x - LogoX) * 0.05f;
            bool top = !F(x, y - 1) || !F(x, y - 2);
            bool rightEdge = !F(x + 1, y);
            bool bottomEdge = !F(x, y + 1) || !F(x - 1, y + 1);
            if (y < split)
            {
                if (top) return TopRim;
                if (rightEdge || bottomEdge) return TopEdge;
                return TopFace;
            }
            if (y < split + 1f) return LowRim;
            if (bottomEdge || !F(x, y + 2)) return LowEdge;
            if (rightEdge) return LowScratch;
            return scratch[x, y] ? LowScratch : LowFace;
        }

        static bool[,] Dilate(bool[,] m, int r)
        {
            var o = new bool[LogoW, LogoH];
            for (int y = 0; y < LogoH; y++)
                for (int x = 0; x < LogoW; x++)
                {
                    if (!m[x, y]) continue;
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (dx * dx + dy * dy > r * r + 1) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < LogoW && ny < LogoH) o[nx, ny] = true;
                        }
                }
            return o;
        }
    }
}
