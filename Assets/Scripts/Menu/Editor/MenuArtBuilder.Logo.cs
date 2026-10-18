using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Oyunun adı: "EŞİK" logosu (UI/LogoStacked.png'nin üzerine yazar; GUID aynı kaldığı için menüdeki referanslar
    // bozulmaz). 294x151, menünün piksel yoğunluğunda (1 doku pikseli = 960x540 sanal ekranda 1 piksel).
    // Eşik = evin kapı eşiği: kalın, sağa yatık harfler taş bir eşiğin üstünde durur. İ bir kapıdır, noktası
    // kapının üstündeki lamba. Sel suyu eşiğe dayanmış (sel), taş eşik çatlamış ve bir yanı kaymış (deprem).
    // Renkler menünün gün batımı paleti: üst krem, alt turuncu (eski logoyla devamlılık), dış hat menü
    // panellerindeki koyu lacivert-mor; aşağı-sola blok gölge.
    public static partial class MenuArtBuilder
    {
        const int LogoW = 294, LogoH = 151;
        const int CapH = 80;          // büyük harf yüksekliği (yerel koordinat, y aşağı)
        const float Italic = 0.2f;    // her satır yukarıda bu kadar sağa
        const int LogoX = 30, LogoY = 33; // yerel (0,0) = ilk harfin sol üstü (eğimsiz)
        const int Outline = 3, Extrude = 6;
        const int SillTop = LogoY + CapH + 1, SillBot = SillTop + 13, SillFace = 3; // eşik: üst yüz 3 px, ön yüz
        const int CrackX = 214;      // eşikteki çatlağın x'i

        static readonly Color32 Ink = C(30, 22, 49), InkDepth = C(51, 40, 79);
        static readonly Color32 TopFace = C(255, 243, 220), TopRim = C(255, 255, 255), TopEdge = C(234, 211, 180);
        static readonly Color32 LowFace = C(255, 179, 71), LowScratch = C(240, 146, 47), LowEdge = C(217, 112, 42), LowRim = C(255, 213, 138);
        static readonly Color32 Door = C(184, 105, 58), DoorPanel = C(142, 75, 42), DoorLight = C(214, 140, 84), Knob = C(255, 216, 107);
        static readonly Color32 Lamp = C(255, 216, 107), LampRim = C(255, 248, 210), LampEdge = C(240, 168, 60);
        static readonly Color32 SillTopC = C(216, 203, 190), SillRim = C(241, 232, 222), SillFront = C(168, 151, 138),
            SillDark = C(135, 119, 108), SillSpeck = C(154, 137, 125);
        static readonly Color32 WaterC = C(78, 155, 214), WaterDeep = C(59, 125, 191), WaterFoam = C(230, 246, 255), WaterLite = C(126, 190, 232);

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
        static Vector2[] LampGlyph;

        static Vector2[] Glyph(float ox, params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(ox + xy[i * 2], xy[i * 2 + 1]);
            Glyphs.Add(p);
            return p;
        }

        static void BuildGlyphs()
        {
            Glyphs.Clear();
            // E — üst çubuğun ucu eğik kesik, orta çubuk kısa
            Glyph(0, 0, 4, 4, 0, 56, 0, 52, 17, 21, 17, 21, 31, 47, 31, 45, 48, 21, 48, 21, 63, 55, 63, 55, 80, 4, 80, 0, 76);
            // Ş — köşeleri pahlı, açıklıkları sağ üst / sol altta
            Glyph(61, 12, 0, 60, 0, 60, 17, 24, 17, 22, 19, 22, 29, 24, 31, 48, 31, 60, 39, 60, 68, 50, 80, 0, 80, 0, 63,
                36, 63, 38, 61, 38, 51, 36, 49, 12, 49, 0, 41, 0, 12);
            // Ş çengeli: alt çubuktan iner, sağa kıvrılır, sola döner (eşiğin önüne sarkar)
            Glyph(61, 22, 80, 33, 80, 33, 83, 37, 86, 36, 91, 30, 93, 17, 93, 17, 89, 27, 88, 27, 86, 22, 84);
            // İ = kapı; noktası kapının üstündeki lamba
            Glyph(126, 0, 0, 28, 0, 28, 80, 0, 80);
            LampGlyph = Glyph(126, 3, -24, 25, -24, 28, -20, 28, -9, 0, -9, 0, -20);
            // K — gövde, kol, bacak
            Glyph(160, 0, 0, 21, 0, 21, 80, 0, 80);
            Glyph(160, 19, 30, 39, 0, 63, 0, 35, 45, 19, 55);
            Glyph(160, 24, 46, 38, 37, 63, 80, 38, 80);
        }

        static void Local(int x, int y, out float lx, out float ly)
        {
            ly = y + 0.5f - LogoY;
            lx = x + 0.5f - LogoX - (CapH - ly) * Italic;
        }

        static Texture2D Logo()
        {
            BuildGlyphs();
            var face = new bool[LogoW, LogoH];
            var sill = new bool[LogoW, LogoH];
            var water = new bool[LogoW, LogoH];
            for (int y = 0; y < LogoH; y++)
                for (int x = 0; x < LogoW; x++)
                {
                    face[x, y] = InGlyph(x, y);
                    sill[x, y] = InSill(x, y);
                    water[x, y] = InWater(x, y);
                }

            // Siluet = hepsi + kalın dış hat; blok gölge harflerin ve eşiğin aşağı-sola kaydırılmışı.
            var all = new bool[LogoW, LogoH];
            var solid = new bool[LogoW, LogoH];
            for (int y = 0; y < LogoH; y++)
                for (int x = 0; x < LogoW; x++)
                {
                    all[x, y] = face[x, y] || sill[x, y] || water[x, y];
                    solid[x, y] = face[x, y] || sill[x, y];
                }
            var sil = Dilate(all, Outline);
            var faceRim = Dilate(face, 2); // harfler eşiğin ve suyun önünde de koyu hatla ayrışsın
            var depth = new bool[LogoW, LogoH];
            var solidSil = Dilate(solid, Outline);
            for (int y = 0; y < LogoH; y++)
                for (int x = 0; x < LogoW; x++)
                {
                    if (!solidSil[x, y]) continue;
                    for (int k = 1; k <= Extrude; k++)
                    {
                        int tx = x - k / 2, ty = y + k;
                        if (tx >= 0 && ty < LogoH && !sil[tx, ty]) depth[tx, ty] = true;
                    }
                }

            var rng = new System.Random(7);
            var scratch = new bool[LogoW, LogoH];
            for (int i = 0; i < 60; i++)
            {
                int sx = rng.Next(LogoX, LogoX + 240), sy = rng.Next(LogoY + 46, LogoY + CapH);
                int len = rng.Next(4, 12);
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
                    if (depth[x, y]) c = InkDepth;
                    if (sil[x, y]) c = Ink;
                    if (sill[x, y]) c = SillColor(sill, x, y);
                    if (water[x, y]) c = WaterColor(water, x, y);
                    if (faceRim[x, y] && !face[x, y]) c = Ink;
                    if (face[x, y]) c = FaceColor(face, scratch, x, y);
                    if (c.HasValue) Px(t, x, y, c.Value);
                }
            // Lambanın ışığı: dış hattın dışında kısa ışık çizgileri.
            float lampX = LogoX + 140f + (CapH + 16.5f) * Italic, lampY = LogoY - 16.5f;
            foreach (var (dx, dy, from, to) in new[] { (-1f, 0f, 20, 24), (1f, 0f, 20, 24), (-0.7f, -0.7f, 17, 20), (0.7f, -0.7f, 17, 20) })
                for (int s = from; s <= to; s++)
                {
                    int rx = Mathf.RoundToInt(lampX + dx * s), ry = Mathf.RoundToInt(lampY + dy * s);
                    if (rx >= 0 && ry >= 0 && rx < LogoW && ry < LogoH && !sil[rx, ry]) Px(t, rx, ry, LampRim);
                }
            return Done(t);
        }

        static bool InGlyph(int x, int y)
        {
            Local(x, y, out float lx, out float ly);
            foreach (var g in Glyphs)
                if (Inside(g, lx, ly)) return true;
            return false;
        }

        // Taş eşik: harflerin eğimiyle aynı pahlı uçlar. Çatlağın sağındaki parça 2 piksel aşağı oturmuş.
        static bool InSill(int x, int y)
        {
            int top = x > CrackAt(y) ? SillTop + 2 : SillTop;
            int bot = x > CrackAt(y) ? SillBot + 2 : SillBot;
            if (y < top || y >= bot) return false;
            float shift = (bot - y) * Italic;
            if (x < 16 + shift || x > 266 + shift) return false;
            return Mathf.Abs(x + 0.5f - CrackAt(y)) > 1f;
        }

        // Çatlak eşiği yukarıdan aşağı zikzakla geçer.
        static float CrackAt(int y)
        {
            int d = y - SillTop;
            return CrackX + (d < 3 ? -d : d < 7 ? -3 + (d - 3) * 2 : 5 - (d - 7));
        }

        // Sel suyu: eşiğin ön yüzüne dayanmış dalgalı bant, uçlarda incelir.
        static float WaterTopAt(int x) => SillTop + 10 + Mathf.Sin(x / 9f) * 1.6f + Mathf.Sin(x / 4.3f + 1f) * 0.8f;
        static float WaterBotAt(int x) => SillBot + 9 + Mathf.Sin(x / 13f + 2f) * 1.5f;

        static bool InWater(int x, int y)
        {
            if (x < 10 || x > 286) return false;
            float taper = Mathf.Min(Mathf.Clamp01((x - 10) / 34f), Mathf.Clamp01((286 - x) / 34f));
            float top = WaterTopAt(x), bot = WaterBotAt(x);
            float mid = (top + bot) * 0.5f, half = (bot - top) * 0.5f * Mathf.Sqrt(taper);
            return y + 0.5f >= mid - half && y + 0.5f < mid + half;
        }

        static bool Inside(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
        }

        static Color32 FaceColor(bool[,] face, bool[,] scratch, int x, int y)
        {
            bool F(int ax, int ay) => ax >= 0 && ay >= 0 && ax < LogoW && ay < LogoH && face[ax, ay];
            Local(x, y, out float lx, out float ly);
            bool top = !F(x, y - 1) || !F(x, y - 2);
            bool rightEdge = !F(x + 1, y);
            bool bottomEdge = !F(x, y + 1) || !F(x - 1, y + 1);

            // Lamba (İ'nin noktası): sıcak sarı ışık.
            if (Inside(LampGlyph, lx, ly))
                return top ? LampRim : rightEdge || bottomEdge ? LampEdge : Lamp;

            // Kapı (İ'nin gövdesi): pervaz harfin kendisi, içinde iki panelli ahşap kapı ve tokmak.
            float dx = lx - 126f;
            if (dx >= 6f && dx < 22f && ly >= 20f && ly < CapH)
            {
                if (dx >= 18f && dx < 20f && ly >= 50f && ly < 52f) return Knob;
                bool panel = dx >= 9f && dx < 19f && ((ly >= 25f && ly < 46f) || (ly >= 55f && ly < 76f));
                bool panelEdge = panel && (dx < 10f || ly < 26f || (ly >= 55f && ly < 56f));
                if (panelEdge) return DoorPanel;
                if (dx < 7f || ly < 21f) return DoorPanel;
                return panel ? DoorLight : Door;
            }

            // Üst/alt ayrımı sağa doğru hafifçe yükselir: üst krem, alt gün batımı turuncusu.
            float split = LogoY + 45 - (x - LogoX) * 0.05f;
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

        static Color32 SillColor(bool[,] sill, int x, int y)
        {
            int top = x > CrackAt(y) ? SillTop + 2 : SillTop;
            int d = y - top;
            if (d == 0) return SillRim;
            if (d < SillFace) return SillTopC;
            if (y == (x > CrackAt(y) ? SillBot + 1 : SillBot - 1)) return SillDark;
            return Hash(x, y, 11) < 0.12f ? SillSpeck : SillFront;
        }

        static Color32 WaterColor(bool[,] water, int x, int y)
        {
            bool top = y == 0 || !water[x, y - 1];
            bool top2 = y < 2 || !water[x, y - 2];
            bool bottom = y == LogoH - 1 || !water[x, y + 1];
            if (top) return WaterFoam;
            if (top2) return WaterLite;
            if (bottom) return WaterDeep;
            // Kısa yatay ışık çizgileri
            return Hash(x / 5, y, 23) < 0.12f ? WaterLite : WaterC;
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
