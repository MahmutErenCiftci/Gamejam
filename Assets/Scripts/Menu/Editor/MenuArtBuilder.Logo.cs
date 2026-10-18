using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Oyunun adı: "EŞİK" logosu. Kaynak: Source~/logo_esik.jpg (koyu lacivert zemin üstünde piksel logo).
    // Menünün piksel yoğunluğuna çevirip UI/LogoStacked.png'nin üzerine yazar (GUID aynı kaldığı için menüdeki
    // referanslar bozulmaz):
    //  1) Logonun kapladığı alan 294x151 tuvale sığacak şekilde alan ortalamasıyla küçültülür.
    //  2) Her piksel, kaynağın kendi düz renklerinden çıkarılan küçük palete oturtulur (JPEG lekesi ve ara ton
    //     kalmaz).
    //  3) Kenardan dolguyla ulaşılan zemin şeffaf yapılır; harf içindeki koyu boşluklar korunur.
    public static partial class MenuArtBuilder
    {
        const string LogoSource = SourceDir + "logo_esik.jpg";
        const int LogoW = 294, LogoH = 151, PaletteSize = 7;

        [MenuItem("AfetUyanisi/Menü/Logoyu Kur (EŞİK)")]
        public static void BuildLogoOnly()
        {
            string path = Save(Logo(), "UI/LogoStacked");
            AssetDatabase.Refresh();
            ConfigureImporter(path, Center);
            Debug.Log("[MenuArtBuilder] EŞİK logosu yazıldı: " + path);
        }

        // Oyunun .exe simgesi: aynı logo, 256x256 kare tuvalde ortalı, arkası şeffaf. Player Settings'teki
        // varsayılan simge olarak atanır; Windows küçük boyutları bundan üretir.
        const string IconRelative = "UI/AppIcon";
        const int IconSize = 256, IconPad = 8;

        [MenuItem("AfetUyanisi/Menü/Oyun Simgesini Kur (EŞİK)")]
        public static void BuildAppIcon()
        {
            string path = Save(LogoOn(IconSize, IconSize, IconSize - IconPad * 2, IconSize - IconPad * 2), IconRelative);
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(path) }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuArtBuilder] EŞİK oyun simgesi yazıldı ve atandı: " + path);
        }

        static Texture2D Logo() => LogoOn(LogoW, LogoH, LogoW, LogoH);

        // Logoyu canvasW x canvasH tuvalin ortasına, en fazla maxW x maxH kaplayacak şekilde çizer.
        static Texture2D LogoOn(int canvasW, int canvasH, int maxW, int maxH)
        {
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(LogoSource));
            int w = src.width, h = src.height;
            Color32[] px = src.GetPixels32();
            Object.DestroyImmediate(src);
            Color32 ground = px[10 * w + 10];

            // Logonun kapladığı alan (zeminden belirgin ayrılan pikseller)
            int bx0 = w, bx1 = 0, by0 = h, by1 = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (Diff(px[y * w + x], ground) > 40)
                    {
                        bx0 = Mathf.Min(bx0, x); bx1 = Mathf.Max(bx1, x);
                        by0 = Mathf.Min(by0, y); by1 = Mathf.Max(by1, y);
                    }
            int bw = bx1 - bx0 + 1, bh = by1 - by0 + 1;
            float scale = Mathf.Min((float)maxW / bw, (float)maxH / bh);
            int ow = Mathf.Min(maxW, Mathf.RoundToInt(bw * scale)), oh = Mathf.Min(maxH, Mathf.RoundToInt(bh * scale));

            // Palet: kaynağın düz renkli bölgelerinden (komşularıyla aynı tondaki pikseller) k-ortalama.
            var flat = new List<Color32>();
            for (int y = by0 + 2; y < by1 - 2; y += 2)
                for (int x = bx0 + 2; x < bx1 - 2; x += 2)
                {
                    var c = px[y * w + x];
                    if (Diff(c, px[y * w + x + 2]) < 18 && Diff(c, px[(y + 2) * w + x]) < 18 && Diff(c, px[(y - 2) * w + x - 2]) < 18)
                        flat.Add(c);
                }
            Color32[] palette = KMeans(flat, ground, PaletteSize);

            // 1–2) Alan ortalamasıyla küçült, palete oturt (tuval satırı yukarıdan, kaynak alttan)
            var cells = new Color32[ow, oh];
            for (int r = 0; r < oh; r++)
                for (int c = 0; c < ow; c++)
                {
                    float sx0 = bx0 + c / scale, sx1 = bx0 + (c + 1) / scale;
                    float sy0 = by0 + r / scale, sy1 = by0 + (r + 1) / scale;
                    Vector3 sum = Vector3.zero; int n = 0;
                    for (int y = Mathf.FloorToInt(sy0); y < Mathf.CeilToInt(sy1); y++)
                        for (int x = Mathf.FloorToInt(sx0); x < Mathf.CeilToInt(sx1); x++)
                        {
                            if (x < 0 || y < 0 || x >= w || y >= h) continue;
                            sum += V(px[y * w + x]); n++;
                        }
                    Vector3 avg = n > 0 ? sum / n : V(ground);
                    cells[c, r] = Nearest(palette, new Color32((byte)avg.x, (byte)avg.y, (byte)avg.z, 255));
                }

            // 3) Kenardan ulaşılan zemin şeffaf
            var clear = new bool[ow, oh];
            var stack = new Stack<Vector2Int>();
            for (int c = 0; c < ow; c++) { stack.Push(new Vector2Int(c, 0)); stack.Push(new Vector2Int(c, oh - 1)); }
            for (int r = 0; r < oh; r++) { stack.Push(new Vector2Int(0, r)); stack.Push(new Vector2Int(ow - 1, r)); }
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p.x < 0 || p.y < 0 || p.x >= ow || p.y >= oh || clear[p.x, p.y]) continue;
                if (!Same(cells[p.x, p.y], palette[0])) continue;
                clear[p.x, p.y] = true;
                stack.Push(p + Vector2Int.right); stack.Push(p + Vector2Int.left);
                stack.Push(p + Vector2Int.up); stack.Push(p + Vector2Int.down);
            }

            var t = New(canvasW, canvasH);
            int left = (canvasW - ow) / 2, top = (canvasH - oh) / 2;
            for (int r = 0; r < oh; r++)
                for (int c = 0; c < ow; c++)
                    if (!clear[c, r]) Px(t, left + c, top + (oh - 1 - r), cells[c, r]);
            return Done(t);
        }

        static int Diff(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        static Color32 Nearest(Color32[] palette, Color32 c)
        {
            Color32 best = palette[0];
            int bestD = int.MaxValue;
            foreach (var p in palette)
            {
                int dr = p.r - c.r, dg = p.g - c.g, db = p.b - c.b, d = dr * dr + dg * dg + db * db;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        // k-ortalama; ilk merkez zemin rengi ve sabit kalır, diğerleri en uzak noktalardan başlar.
        static Color32[] KMeans(List<Color32> data, Color32 fixedFirst, int k)
        {
            var centers = new List<Vector3> { V(fixedFirst) };
            while (centers.Count < k)
            {
                Vector3 far = V(data[0]); float farD = -1f;
                foreach (var c in data)
                {
                    float d = float.MaxValue;
                    foreach (var m in centers) d = Mathf.Min(d, (V(c) - m).sqrMagnitude);
                    if (d > farD) { farD = d; far = V(c); }
                }
                centers.Add(far);
            }
            for (int iter = 0; iter < 12; iter++)
            {
                var sum = new Vector3[k]; var n = new int[k];
                foreach (var c in data)
                {
                    int bi = 0; float bd = float.MaxValue;
                    for (int i = 0; i < k; i++) { float d = (V(c) - centers[i]).sqrMagnitude; if (d < bd) { bd = d; bi = i; } }
                    sum[bi] += V(c); n[bi]++;
                }
                for (int i = 1; i < k; i++) if (n[i] > 0) centers[i] = sum[i] / n[i];
            }
            var o = new Color32[k];
            for (int i = 0; i < k; i++)
                o[i] = new Color32((byte)Mathf.RoundToInt(centers[i].x), (byte)Mathf.RoundToInt(centers[i].y), (byte)Mathf.RoundToInt(centers[i].z), 255);
            return o;
        }

        static Vector3 V(Color32 c) => new Vector3(c.r, c.g, c.b);
    }
}
