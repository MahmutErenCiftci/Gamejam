using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Oyunun adı: "EŞİK" logosu (UI/LogoStacked.png'nin üzerine yazar; GUID aynı kaldığı için menüdeki referanslar
    // bozulmaz). Harfler aşağıdaki piksel haritalarından (Glyph*) çizilir: 98x50 sanat pikseli, 3 kat büyütülüp
    // 294x151'e yazılır. Satır başına kaydırmayla sağa yatık, ışık sol üstten: üst kenar parlak, sol kenar açık,
    // alt/sağ kenar koyu; açık-orta geçişi tek sıra damalı. Altında 2 piksel kalınlık, dışında tek koyu hat.
    // Renk takımları LogoPalettes'ta; LogoPalette seçer.
    public static partial class MenuArtBuilder
    {
        const int ArtW = 98, ArtH = 50, ArtScale = 3;
        const int GlyphTop = 12, GlyphLeft = 4, CapRows = 26;
        const int LogoPalette = 2;

        // Işık → gölge: parlak, açık, orta, koyu, kalınlık, dış hat
        public static readonly Color32[][] LogoPalettes =
        {
            // 0: altın (gün batımı) — mor dış hat
            new[] { C(255, 246, 201), C(255, 211, 92), C(246, 163, 58), C(216, 105, 42), C(155, 59, 42), C(43, 26, 46) },
            // 1: krem kâğıt — kırmızı kalınlık
            new[] { C(255, 255, 255), C(255, 240, 214), C(243, 211, 176), C(213, 154, 123), C(181, 55, 46), C(42, 26, 40) },
            // 2: su yeşili — lacivert kalınlık
            new[] { C(232, 255, 248), C(159, 240, 224), C(79, 201, 196), C(46, 143, 166), C(31, 77, 122), C(22, 26, 51) },
        };

        static readonly string[] GlyphE =
        {
            ".XXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXX",
            "XXXXXX..............", "XXXXXX..............", "XXXXXX..............", "XXXXXX..............", "XXXXXX..............", "XXXXXX..............",
            "XXXXXXXXXXXXXXXX....", "XXXXXXXXXXXXXXXX....", "XXXXXXXXXXXXXXXX....", "XXXXXXXXXXXXXXXX....", "XXXXXXXXXXXXXXXX....",
            "XXXXXX..............", "XXXXXX..............", "XXXXXX..............", "XXXXXX..............", "XXXXXX..............",
            "XXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXX",
            ".XXXXXXXXXXXXXXXXXXX",
        };

        static readonly string[] GlyphS =
        {
            "..XXXXXXXXXXXXXXXXXXX",
            ".XXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXXX",
            "XXXXXX.........XXXXXX", "XXXXXX.........XXXXXX",
            "XXXXXX...............", "XXXXXX...............", "XXXXXX...............",
            "XXXXXXXXXXXXXXXXXXXX.",
            "XXXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXXX",
            ".XXXXXXXXXXXXXXXXXXXX",
            "...............XXXXXX", "...............XXXXXX", "...............XXXXXX", "...............XXXXXX",
            "XXXXXX.........XXXXXX", "XXXXXX.........XXXXXX",
            "XXXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXXX", "XXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXX.",
            "XXXXXXXXXXXXXXXXXXX..",
            // çengel
            ".........XX..........",
            "..........XX.........",
            "..........XX.........",
            ".......XXXX..........",
        };

        // İ'nin noktası: köşeleri kırık kare. Satır -7'den başlar.
        static readonly string[] GlyphIDot =
        {
            ".XXXX.",
            "XXXXXX",
            "XXXXXX",
            "XXXXXX",
            ".XXXX.",
        };

        [MenuItem("AfetUyanisi/Menü/Logoyu Kur (EŞİK)")]
        public static void BuildLogoOnly()
        {
            string path = Save(Logo(), "UI/LogoStacked");
            AssetDatabase.Refresh();
            ConfigureImporter(path, Center);
            Debug.Log("[MenuArtBuilder] EŞİK logosu yazıldı: " + path);
        }

        static Texture2D Logo() => LogoTexture(LogoPalette);

        /// <summary>Logoyu verilen renk takımıyla çizer (önizleme için de kullanılır).</summary>
        public static Texture2D LogoTexture(int palette)
        {
            Color32[] pal = LogoPalettes[Mathf.Clamp(palette, 0, LogoPalettes.Length - 1)];
            var face = new bool[ArtW, ArtH];
            var row = new int[ArtW, ArtH]; // harfin büyük harf tepesine göre satırı (damalı geçiş için)

            void Stamp(string[] rows, int left, int top)
            {
                for (int r = 0; r < rows.Length; r++)
                    for (int c = 0; c < rows[r].Length; c++)
                    {
                        if (rows[r][c] != 'X') continue;
                        int ly = top + r;                       // büyük harf tepesine göre
                        int x = GlyphLeft + left + c + (CapRows - 1 - ly) / 4, y = GlyphTop + ly;
                        if (x < 0 || y < 0 || x >= ArtW || y >= ArtH) continue;
                        face[x, y] = true;
                        row[x, y] = ly;
                    }
            }

            Stamp(GlyphE, 0, 0);
            Stamp(GlyphS, 23, 0);
            Stamp(Block(6, CapRows), 47, 0);
            Stamp(GlyphIDot, 47, -7);
            Stamp(GlyphK(), 56, 0);

            bool F(int x, int y) => x >= 0 && y >= 0 && x < ArtW && y < ArtH && face[x, y];

            var art = new Color32?[ArtW, ArtH];
            // Kalınlık: harfin 2 piksel altı
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                    if (!face[x, y] && (F(x, y - 1) || F(x, y - 2))) art[x, y] = pal[4];
            // Dış hat: harf + kalınlığın çevresi (4 komşu + köşeler)
            var body = new bool[ArtW, ArtH];
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                    body[x, y] = face[x, y] || art[x, y].HasValue;
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    if (body[x, y]) continue;
                    for (int dy = -1; dy <= 1 && !art[x, y].HasValue; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < ArtW && ny < ArtH && body[nx, ny]) { art[x, y] = pal[5]; break; }
                        }
                }
            // Yüz: ışık sol üstten
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    if (!face[x, y]) continue;
                    bool up = F(x, y - 1), down = F(x, y + 1), left = F(x - 1, y), right = F(x + 1, y);
                    int ly = row[x, y];
                    Color32 c;
                    if (!up) c = !left ? C(255, 255, 255) : pal[0];
                    else if (!down) c = pal[3];
                    else if (!left) c = pal[1];
                    else if (!right) c = pal[3];
                    else if (ly < 11) c = pal[1];
                    else if (ly < 13) c = ((x + y) & 1) == 0 ? pal[1] : pal[2]; // tek sıra damalı geçiş
                    else c = pal[2];
                    art[x, y] = c;
                }

            var t = New(LogoW, LogoH);
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    if (!art[x, y].HasValue) continue;
                    for (int sy = 0; sy < ArtScale; sy++)
                        for (int sx = 0; sx < ArtScale; sx++)
                            Px(t, x * ArtScale + sx, y * ArtScale + sy, art[x, y].Value);
                }
            return Done(t);
        }

        const int LogoW = ArtW * ArtScale, LogoH = ArtH * ArtScale + 1;

        static string[] Block(int w, int h)
        {
            var rows = new string[h];
            for (int i = 0; i < h; i++) rows[i] = new string('X', w);
            return rows;
        }

        // K: 6 piksellik gövde; kol ve bacak 45° (her satır 1 piksel) çapraz.
        static string[] GlyphK()
        {
            var rows = new List<string>();
            for (int y = 0; y < CapRows; y++)
            {
                var r = new char[25];
                for (int c = 0; c < r.Length; c++) r[c] = c < 6 ? 'X' : '.';
                int a0, a1;
                if (y <= 8) { a0 = 14 - y; a1 = 19 - y; }
                else if (y <= 12) { a0 = 6; a1 = 11; }
                else { a0 = 7 + (y - 13); a1 = 12 + (y - 13); }
                for (int c = a0; c <= a1 && c < r.Length; c++) r[c] = 'X';
                rows.Add(new string(r));
            }
            return rows.ToArray();
        }
    }
}
