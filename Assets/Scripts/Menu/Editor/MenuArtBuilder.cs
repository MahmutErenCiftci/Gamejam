using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Ana menünün kendi piksel sanatını üretir (oda, oyunun izometrik yatak odasıdır: Assets/Art/Props/Bedroom,
    // Mahmut). Tekrar çalıştırılabilir: aynı PNG'lerin üzerine yazar, .meta (GUID) korunduğu için sahnedeki
    // referanslar bozulmaz.
    //  1) Source~ altındaki sayfalardan (beyaz arka plan, 4x4 piksel blok) logo ve düşünce baloncuklarını keser.
    //  2) Rüya bulutlarını, "z" harflerini, saat parıltısını, pencere cam maskesini, UI panellerini ve arka plandaki
    //     izometrik şehirle apartmanı (MenuArtBuilder.City.cs) kodla üretir.
    //  3) Hepsini Point filtre, PPU 64 (odanın sanatıyla aynı yoğunluk), sıkıştırmasız sprite olarak içe aktarır.
    public static partial class MenuArtBuilder
    {
        public const int PixelsPerUnit = 64;
        const string SourceDir = "Assets/Art/Menu/Source~/";
        const string OutDir = "Assets/Art/Menu/";
        const string RoomWindow = "Assets/Art/Props/Bedroom/PROP_Bedroom_Window.png";

        static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // Hücre koordinatları: sayfanın sol üstünden, 4 px'lik blok cinsinden. step 2 = yarı boyut.
        struct Piece
        {
            public string sheet, output;
            public RectInt cells;
            public int step;
            public Vector2 pivot;

            public Piece(string sheet, string output, int x, int y, int w, int h, Vector2 pivot, int step = 1)
            {
                this.sheet = sheet; this.output = output; cells = new RectInt(x, y, w, h);
                this.pivot = pivot; this.step = step;
            }
        }

        static readonly Piece[] Pieces =
        {
            // Düşünce baloncukları (ui_kit.png): düşünce yollarının kaynağı
            new Piece("ui_kit", "UI/ThoughtLarge", 150, 42, 60, 64, Center),
            new Piece("ui_kit", "UI/ThoughtSmall", 272, 63, 30, 38, Center),
            // Logo (logo.png) — tam çözünürlük (menü 960x540 sanal ekranda)
            new Piece("logo", "UI/LogoStacked", 10, 42, 294, 151, Center),
        };

        [MenuItem("AfetUyanisi/Menü/Sanatı Kur (kes + üret + içe aktar)")]
        public static void BuildAll()
        {
            var written = new List<string>();
            var sheets = new Dictionary<string, Sheet>();
            foreach (Piece piece in Pieces)
            {
                if (!sheets.TryGetValue(piece.sheet, out Sheet sheet))
                {
                    sheet = Sheet.Load(SourceDir + piece.sheet + ".png", 4);
                    sheets[piece.sheet] = sheet;
                }
                written.Add(Save(sheet.Extract(piece.cells, piece.step), piece.output));
            }
            written.AddRange(BuildBubbles());
            written.AddRange(BuildThoughtPaths());
            written.AddRange(GenerateProcedural());

            AssetDatabase.Refresh();
            foreach (string path in written)
                ConfigureImporter(path, PivotFor(path));
            BuildAtlas();
            Debug.Log($"[MenuArtBuilder] {written.Count} sprite yazıldı ve içe aktarıldı.");
        }

        // Menü sprite'ları tek atlasa: çizim çağrısı azalır. Point filtre, sıkıştırmasız, mipmap yok.
        // Fx (1x1 Pixel, damla, gökyüzü, maske) atlasa girmez: atlasta 1 piksellik sprite'lar kenardaki boş dolguyu
        // örnekliyor, maskeler tamamen boş kalıyordu.
        static void BuildAtlas()
        {
            const string atlasPath = OutDir + "MenuAtlas.spriteatlasv2";
            AssetDatabase.DeleteAsset(atlasPath); // klasör listesi değişmiş olabilir; her seferinde yeniden kur
            var atlas = new UnityEditor.U2D.SpriteAtlasAsset();
            var folders = new List<Object>();
            foreach (string folder in new[] { "Bubbles", "UI" })
                folders.Add(AssetDatabase.LoadAssetAtPath<DefaultAsset>(OutDir + folder));
            atlas.Add(folders.ToArray());
            UnityEditor.U2D.SpriteAtlasAsset.Save(atlas, atlasPath);
            AssetDatabase.Refresh();
            var importer = (UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);
            importer.packingSettings = new UnityEditor.U2D.SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 2, blockOffset = 1 };
            importer.textureSettings = new UnityEditor.U2D.SpriteAtlasTextureSettings { filterMode = FilterMode.Point, generateMipMaps = false, sRGB = true, readable = false, anisoLevel = 0 };
            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.maxTextureSize = 2048;
            importer.SetPlatformSettings(platform);
            importer.SaveAndReimport();
        }

        // Kodla dizilen sprite'ların pivotları (düşünce yolları: başa en yakın baloncuk).
        static readonly Dictionary<string, Vector2> customPivots = new Dictionary<string, Vector2>();

        // Rüya bulutları: 128x128 hazır piksel sanat (kendi bulut çerçevesiyle). Kenar yumuşatması kaldırılır,
        // seçili hali için şekle uyan turuncu/mavi hale üretilir.
        static IEnumerable<string> BuildBubbles()
        {
            var defs = new (string source, string name, Color32 glow)[]
            {
                ("bubble_quake_128", "Quake", new Color32(255, 150, 50, 255)),
                ("bubble_flood_128", "Flood", new Color32(90, 175, 255, 255)),
            };
            foreach (var d in defs)
            {
                var art = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                art.LoadImage(File.ReadAllBytes(SourceDir + d.source + ".png"));
                var px = art.GetPixels32();
                for (int i = 0; i < px.Length; i++) px[i].a = px[i].a >= 128 ? (byte)255 : (byte)0;
                art.SetPixels32(px);
                art.Apply();
                yield return Save(Halo(art, d.glow, 6), $"Bubbles/Bubble{d.name}Glow");
                yield return Save(art, $"Bubbles/Bubble{d.name}");
            }
        }

        // Şeklin dışına doğru sönen renkli hale (merkez saydam; bulut görseli üstte çizilir).
        static Texture2D Halo(Texture2D art, Color32 color, int radius)
        {
            int w = art.width + radius * 2, h = art.height + radius * 2;
            var src = art.GetPixels32();
            var dst = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float best = 99f;
                    for (int dy = -radius; dy <= radius && best > 0; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int sx = x - radius + dx, sy = y - radius + dy;
                            if (sx < 0 || sy < 0 || sx >= art.width || sy >= art.height || src[sy * art.width + sx].a == 0) continue;
                            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
                        }
                    if (best > radius) continue;
                    // Piksel sanata uygun: yumuşak geçiş yerine 3 basamak
                    float k = best <= 2f ? 1f : best <= 4f ? 0.6f : 0.3f;
                    dst[y * w + x] = new Color32(color.r, color.g, color.b, (byte)(230 * k));
                }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(dst);
            t.Apply();
            return t;
        }

        // Rüya seçimindeki iki düşünce yolu: uyuyan çocuğun başından sol-yukarı DEPREM'e, sağ-yukarı SEL'e. Yeni piksel
        // çizilmez; mevcut düşünce baloncuklarının (UI/ThoughtSmall, UI/ThoughtLarge) baloncukları tek tek kopyalanıp
        // dizilir. Konumlar ilk (en küçük) baloncuğun merkezine göre piksel, y yukarı. Pivot bu baloncuk: belirme
        // animasyonu yolu baştan dışa doğru büyütür. Sahnedeki başlangıç noktaları MenuSceneBuilder'da.
        static IEnumerable<string> BuildThoughtPaths()
        {
            List<Texture2D> small = Puffs(OutDir + "UI/ThoughtSmall.png"), large = Puffs(OutDir + "UI/ThoughtLarge.png");
            Texture2D tiny = small[0], mid = small[1], big = small[2], medium = large[0];
            yield return Save(ThoughtPath("ThoughtPathQuake", (tiny, 0, 0), (mid, -9, 9), (medium, -21, 19), (big, -37, 30)), "UI/ThoughtPathQuake");
            yield return Save(ThoughtPath("ThoughtPathFlood", (tiny, 0, 0), (mid, 9, 9), (medium, 21, 19), (big, 37, 30)), "UI/ThoughtPathFlood");
            foreach (Texture2D t in small) Object.DestroyImmediate(t);
            foreach (Texture2D t in large) Object.DestroyImmediate(t);
        }

        // Görseldeki birbirine değmeyen parçalar (baloncuklar), alandan küçükten büyüğe.
        static List<Texture2D> Puffs(string path)
        {
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(File.ReadAllBytes(path));
            int w = sheet.width, h = sheet.height;
            Color32[] px = sheet.GetPixels32();
            Object.DestroyImmediate(sheet);
            var seen = new bool[px.Length];
            var found = new List<(int area, Texture2D texture)>();
            var stack = new Stack<int>();
            var cells = new List<int>();
            for (int start = 0; start < px.Length; start++)
            {
                if (seen[start] || px[start].a == 0) continue;
                cells.Clear();
                seen[start] = true;
                stack.Push(start);
                int x0 = w, x1 = -1, y0 = h, y1 = -1;
                while (stack.Count > 0)
                {
                    int c = stack.Pop(), cx = c % w, cy = c / w;
                    cells.Add(c);
                    x0 = Mathf.Min(x0, cx); x1 = Mathf.Max(x1, cx); y0 = Mathf.Min(y0, cy); y1 = Mathf.Max(y1, cy);
                    void Visit(int n) { if (!seen[n] && px[n].a > 0) { seen[n] = true; stack.Push(n); } }
                    if (cx > 0) Visit(c - 1);
                    if (cx < w - 1) Visit(c + 1);
                    if (cy > 0) Visit(c - w);
                    if (cy < h - 1) Visit(c + w);
                }
                int pw = x1 - x0 + 1, ph = y1 - y0 + 1;
                var piece = new Color32[pw * ph];
                foreach (int c in cells) piece[(c / w - y0) * pw + c % w - x0] = px[c];
                var texture = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
                texture.SetPixels32(piece);
                texture.Apply();
                found.Add((pw * ph, texture));
            }
            found.Sort((a, b) => a.area.CompareTo(b.area));
            var result = new List<Texture2D>();
            foreach (var f in found) result.Add(f.texture);
            return result;
        }

        static Texture2D ThoughtPath(string name, params (Texture2D puff, int x, int y)[] parts)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in parts)
            {
                int x0 = p.x - p.puff.width / 2, y0 = p.y - p.puff.height / 2;
                minX = Mathf.Min(minX, x0); minY = Mathf.Min(minY, y0);
                maxX = Mathf.Max(maxX, x0 + p.puff.width); maxY = Mathf.Max(maxY, y0 + p.puff.height);
            }
            int w = maxX - minX, h = maxY - minY;
            var dst = new Color32[w * h];
            foreach (var p in parts)
            {
                Color32[] src = p.puff.GetPixels32();
                int ox = p.x - p.puff.width / 2 - minX, oy = p.y - p.puff.height / 2 - minY;
                for (int y = 0; y < p.puff.height; y++)
                    for (int x = 0; x < p.puff.width; x++)
                    {
                        Color32 c = src[y * p.puff.width + x];
                        if (c.a > 0) dst[(oy + y) * w + ox + x] = c;
                    }
            }
            customPivots[name] = new Vector2(-minX / (float)w, -minY / (float)h);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(dst);
            texture.Apply();
            return texture;
        }

        static Vector2 PivotFor(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (customPivots.TryGetValue(name, out Vector2 custom)) return custom;
            foreach (Piece piece in Pieces)
                if (piece.output.EndsWith(name))
                    return piece.pivot;
            if (name.StartsWith("Window")) return Bottom; // odadaki pencere sprite'ıyla aynı pivot
            return Center;
        }

        static string Save(Texture2D texture, string relative)
        {
            string path = OutDir + relative + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return path;
        }

        static void ConfigureImporter(string path, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            // Paneller 9-dilim: köşe ve çerçeve 3 piksel, genişleyince bozulmaz.
            if (Path.GetFileNameWithoutExtension(path).StartsWith("Panel"))
                importer.spriteBorder = new Vector4(3, 3, 3, 3);
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- kaynak sayfa
        class Sheet
        {
            Color32[] pixels;
            int width, height, cellsW, cellsH, block, gridTop;
            bool[] background;

            // block: kaynakta bir sanat pikselinin kaç görsel pikseli olduğu (Codex sayfalarında 4). Izgara alta
            // hizalı: yükseklik bloğun katı değilse artan satırlar üstte kalır.
            public static Sheet Load(string path, int block)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(path));
                var sheet = new Sheet
                {
                    pixels = texture.GetPixels32(),
                    width = texture.width,
                    height = texture.height,
                    block = block,
                    gridTop = texture.height % block,
                    cellsW = texture.width / block,
                    cellsH = texture.height / block,
                };
                Object.DestroyImmediate(texture);
                sheet.background = sheet.FloodWhite(EdgeSeeds(sheet.cellsW, sheet.cellsH));
                return sheet;
            }

            // Hücre merkezindeki renk; y üstten.
            Color32 Cell(int x, int yTop)
            {
                int py = height - 1 - (gridTop + yTop * block + block - 1 - block / 2);
                int px = x * block + block / 2;
                return pixels[py * width + px];
            }

            static bool IsWhite(Color32 p)
            {
                int min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                int max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                return p.a < 128 || (min >= 232 && max - min <= 14);
            }

            static IEnumerable<Vector2Int> EdgeSeeds(int w, int h)
            {
                for (int x = 0; x < w; x++) { yield return new Vector2Int(x, 0); yield return new Vector2Int(x, h - 1); }
                for (int y = 0; y < h; y++) { yield return new Vector2Int(0, y); yield return new Vector2Int(w - 1, y); }
            }

            bool[] FloodWhite(IEnumerable<Vector2Int> seeds)
            {
                var mask = new bool[cellsW * cellsH];
                var queue = new Queue<Vector2Int>(seeds);
                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    if (c.x < 0 || c.y < 0 || c.x >= cellsW || c.y >= cellsH)
                        continue;
                    int i = c.y * cellsW + c.x;
                    if (mask[i] || !IsWhite(Cell(c.x, c.y)))
                        continue;
                    mask[i] = true;
                    queue.Enqueue(new Vector2Int(c.x + 1, c.y));
                    queue.Enqueue(new Vector2Int(c.x - 1, c.y));
                    queue.Enqueue(new Vector2Int(c.x, c.y + 1));
                    queue.Enqueue(new Vector2Int(c.x, c.y - 1));
                }
                return mask;
            }

            // Dikdörtgen içinde, en büyük parçanın en az %15'i kadar olan parçalar tutulur; komşu nesnelerden
            // taşan kırıntılar atılır. 2 hücrelik boşluklar köprülenir.
            bool[] KeepMask(RectInt cells)
            {
                int w = cells.width, h = cells.height;
                var label = new int[w * h];
                var sizes = new List<int> { 0 };
                var stack = new Stack<int>();
                bool Solid(int lx, int ly)
                {
                    int cx = cells.x + lx, cy = cells.y + ly;
                    return cx < cellsW && cy < cellsH && !background[cy * cellsW + cx];
                }
                for (int start = 0; start < w * h; start++)
                {
                    if (label[start] != 0 || !Solid(start % w, start / w)) continue;
                    int id = sizes.Count, size = 0;
                    label[start] = id;
                    stack.Push(start);
                    while (stack.Count > 0)
                    {
                        int c = stack.Pop(); size++;
                        int lx = c % w, ly = c / w;
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int nx = lx + dx, ny = ly + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                int n = ny * w + nx;
                                if (label[n] == 0 && Solid(nx, ny)) { label[n] = id; stack.Push(n); }
                            }
                    }
                    sizes.Add(size);
                }
                int largest = 0;
                foreach (int s in sizes) largest = Mathf.Max(largest, s);
                var keep = new bool[label.Length];
                for (int i = 0; i < keep.Length; i++)
                    keep[i] = label[i] != 0 && sizes[label[i]] >= largest * 0.15f;
                return keep;
            }

            // Kaynakta beyaz zeminle nesne arasında kalan açık gri yumuşatma pikselleri sprite'ın etrafında
            // açık bir hale bırakıyordu. Şeffaf alana komşu, açık ve doygunluğu düşük pikseller iki tur aşındırılır.
            void ErodeLightFringe(RectInt cells, bool[] keep)
            {
                int w = cells.width, h = cells.height;
                for (int pass = 0; pass < 2; pass++)
                {
                    var remove = new List<int>();
                    for (int ly = 0; ly < h; ly++)
                        for (int lx = 0; lx < w; lx++)
                        {
                            int li = ly * w + lx;
                            if (!keep[li]) continue;
                            bool edge = lx == 0 || ly == 0 || lx == w - 1 || ly == h - 1
                                || !keep[li - 1] || !keep[li + 1] || !keep[li - w] || !keep[li + w];
                            if (!edge) continue;
                            int cx = cells.x + lx, cy = cells.y + ly;
                            if (cx >= cellsW || cy >= cellsH) continue;
                            Color32 c = Cell(cx, cy);
                            int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                            int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                            if (c.a < 200 || (min >= 165 && max - min <= 40))
                                remove.Add(li);
                        }
                    foreach (int li in remove) keep[li] = false;
                }
            }

            public Texture2D Extract(RectInt cells, int step)
            {
                bool[] keep = KeepMask(cells);
                ErodeLightFringe(cells, keep);
                int w = Mathf.CeilToInt(cells.width / (float)step);
                int h = Mathf.CeilToInt(cells.height / (float)step);
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var output = new Color32[w * h];
                for (int oy = 0; oy < h; oy++)
                    for (int ox = 0; ox < w; ox++)
                    {
                        int cx = cells.x + ox * step, cy = cells.y + oy * step;
                        if (cx >= cellsW || cy >= cellsH)
                            continue;
                        if (background[cy * cellsW + cx] || !keep[(oy * step) * cells.width + ox * step])
                            continue;
                        Color32 c = Cell(cx, cy);
                        c.a = 255;
                        output[(h - 1 - oy) * w + ox] = c;
                    }
                texture.SetPixels32(output);
                texture.Apply();
                return texture;
            }
        }

        // ---------------------------------------------------------------- kodla çizilenler
        static IEnumerable<string> GenerateProcedural()
        {
            yield return Save(Solid(1, 1, Color.white), "Fx/Pixel");
            yield return Save(RainDrop(), "Fx/RainDrop");
            yield return Save(Rubble(new Color32(120, 110, 104, 255)), "Fx/Rubble");
            foreach (string city in BuildCity()) yield return city;
            yield return Save(Letter(SmallZ), "Fx/ZSmall");
            yield return Save(Letter(BigZ), "Fx/ZBig");
            yield return Save(ClockGlow(), "Fx/ClockGlow");
            yield return Save(WindowLayer(false), "Fx/WindowGlass");
            yield return Save(WindowLayer(true), "Fx/WindowStorm");
            yield return Save(Panel(false), "UI/Panel");
            yield return Save(Panel(true), "UI/PanelLit");
        }

        static Texture2D New(int w, int h) { var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[w * h]); return t; }
        static void Px(Texture2D t, int x, int yTop, Color32 c) { if (x >= 0 && yTop >= 0 && x < t.width && yTop < t.height) t.SetPixel(x, t.height - 1 - yTop, c); }
        static Texture2D Done(Texture2D t) { t.Apply(); return t; }
        static Texture2D Solid(int w, int h, Color32 c) { var t = New(w, h); for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) Px(t, x, y, c); return Done(t); }

        static Texture2D RainDrop()
        {
            var t = New(1, 4);
            for (int y = 0; y < 4; y++) Px(t, 0, y, new Color32(190, 215, 255, (byte)(90 + y * 50)));
            return Done(t);
        }

        static Texture2D Rubble(Color32 c)
        {
            var t = New(3, 3);
            var dark = new Color32((byte)(c.r / 2), (byte)(c.g / 2), (byte)(c.b / 2), 255);
            Px(t, 1, 0, c); Px(t, 0, 1, c); Px(t, 1, 1, c); Px(t, 2, 1, dark); Px(t, 1, 2, dark);
            return Done(t);
        }

        static readonly string[] SmallZ = { "#####", "...#.", "..#..", ".#...", "#####" };
        static readonly string[] BigZ = { "#######", ".....#.", "....#..", "...#...", "..#....", ".#.....", "#######" };

        // Uyku harfi: açık mavi-beyaz, sağ altında 1 piksel gölge.
        static Texture2D Letter(string[] rows)
        {
            int w = rows[0].Length, h = rows.Length;
            var t = New(w + 1, h + 1);
            var ink = new Color32(226, 234, 255, 255);
            var shadow = new Color32(70, 80, 140, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (rows[y][x] == '#') Px(t, x + 1, y + 1, shadow);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (rows[y][x] == '#') Px(t, x, y, ink);
            return Done(t);
        }

        // Açılışta karanlıkta parlayan saatin kırmızı halesi: basamaklı elips.
        static Texture2D ClockGlow()
        {
            const int w = 44, h = 28;
            var t = New(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) continue;
                    byte a = d < 0.45f ? (byte)110 : d < 0.72f ? (byte)60 : (byte)26;
                    Px(t, x, y, new Color32(255, 56, 44, a));
                }
            return Done(t);
        }

        // Odadaki pencerenin (PROP_Bedroom_Window) camı. storm = false: maske (cam pikselleri beyaz; yağmur bunun
        // içine düşer, şimşekte cam parlar). storm = true: camın kendi pikselleri parlaklığına göre fırtına tonlarına
        // boyanır (gün batımı kararır, şehir silüeti korunur). Çerçeve (ahşap), perdeler (mavi) ve tutamaklar
        // (altın) ikisinde de saydamdır. Pencere çizimi değişirse yeniden üretmek yeter.
        static Texture2D WindowLayer(bool storm)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(RoomWindow));
            int w = source.width, h = source.height;
            Color32[] px = source.GetPixels32();
            Object.DestroyImmediate(source);
            bool Wood(Color32 c) => c.r > c.g && c.g > c.b && c.r - c.b >= 40 && c.r + c.g + c.b < 420;
            bool Curtain(Color32 c) => c.b > c.r + 40 && c.g > c.r;
            bool Gold(Color32 c) => c.r > 200 && c.g > 150 && c.b < 80;
            // Pembe-turuncu gökyüzü; güneşin ve tutamakların açık sarısı (g > 170) sayılmaz.
            bool Sunset(Color32 c) => c.r > 180 && c.g < 170 && c.b < 170 && !Gold(c);
            // Camın sınırları: gökyüzü renklerinin kutusu (perde ve tutamaklar dışarıda kalır).
            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c = px[y * w + x];
                    if (c.a == 0 || !Sunset(c)) continue;
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
            var mask = new Color32[w * h];
            var white = new Color32(255, 255, 255, 255);
            Color32[] stormTones =
            {
                new Color32(22, 26, 42, 255), new Color32(34, 40, 64, 255), new Color32(52, 62, 94, 255),
                new Color32(70, 82, 116, 255), new Color32(92, 104, 138, 255),
            };
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Color32 c = px[y * w + x];
                    if (c.a == 0 || Wood(c) || Curtain(c) || Gold(c)) continue;
                    float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
                    int tone = lum > 200 ? 4 : lum > 150 ? 3 : lum > 110 ? 2 : lum > 70 ? 1 : 0;
                    mask[y * w + x] = storm ? stormTones[tone] : white;
                }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(mask);
            t.Apply();
            return t;
        }

        // 9-dilimli menü paneli (12x12, çerçeve 3 piksel): koyu dış çizgi, açık çerçeve, üstte ince parlak şerit,
        // yarı saydam lacivert iç. Seçili hali turuncu çerçeveli ve biraz daha sıcak.
        static Texture2D Panel(bool lit)
        {
            string[] rows =
            {
                "..OOOOOOOO..",
                ".OBBBBBBBBO.",
                "OBHHHHHHHHBO",
                "OBFFFFFFFFBO",
                "OBFFFFFFFFBO",
                "OBFFFFFFFFBO",
                "OBFFFFFFFFBO",
                "OBFFFFFFFFBO",
                "OBFFFFFFFFBO",
                "OBFFFFFFFFBO",
                ".OBBBBBBBBO.",
                "..OOOOOOOO..",
            };
            var outline = new Color32(10, 10, 22, 255);
            var border = lit ? new Color32(255, 178, 84, 255) : new Color32(98, 104, 158, 255);
            var highlight = lit ? new Color32(84, 64, 72, 240) : new Color32(48, 52, 92, 240);
            var fill = lit ? new Color32(40, 34, 58, 240) : new Color32(26, 28, 54, 232);
            var t = New(12, 12);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    char k = rows[y][x];
                    if (k == '.') continue;
                    Px(t, x, y, k == 'O' ? outline : k == 'B' ? border : k == 'H' ? highlight : fill);
                }
            return Done(t);
        }
    }
}
