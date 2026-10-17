using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Ana menünün modüler piksel sanatını üretir. Tekrar çalıştırılabilir: aynı PNG'lerin üzerine yazar,
    // .meta (GUID) korunduğu için sahnedeki referanslar bozulmaz.
    //  1) Source~ altındaki sayfaları (beyaz arka plan, 4x4 piksel blok) nesne nesne keser; her bloğu tek
    //     piksele indirir, kenardan bağlı beyazı şeffaf yapar.
    //  2) Sayfalarda olmayanları (duvar/zemin tile'ları, saat, yağmur, damla, şimşek, gökyüzü) kodla çizer.
    //  3) Hepsini Point filtre, PPU 16, sıkıştırmasız sprite olarak içe aktarır.
    public static class MenuArtBuilder
    {
        public const int PixelsPerUnit = 16;
        const string SourceDir = "Assets/Art/Menu/Source~/";
        const string OutDir = "Assets/Art/Menu/";

        static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 Top = new Vector2(0.5f, 1f);
        static readonly Vector2 BottomLeft = Vector2.zero;

        // Hücre koordinatları: sayfanın sol üstünden, 4 px'lik blok cinsinden. step 2 = yarı boyut.
        struct Piece
        {
            public string sheet, output;
            public RectInt cells;
            public int step;
            public Vector2 pivot;
            public bool punchCenter; // ortadaki kapalı beyaz alanı da şeffaf yap (pencere camı)

            public Piece(string sheet, string output, int x, int y, int w, int h, Vector2 pivot, int step = 1, bool punchCenter = false)
            {
                this.sheet = sheet; this.output = output; cells = new RectInt(x, y, w, h);
                this.pivot = pivot; this.step = step; this.punchCenter = punchCenter;
            }
        }

        static readonly Piece[] Pieces =
        {
            // Mobilya (furniture.png). Yatak artık önden görünen çocuk karelerinin içinde (boy_front_frames.png).
            new Piece("furniture", "Room/Chair", 247, 146, 58, 86, Bottom),
            new Piece("furniture", "Room/Nightstand", 11, 169, 71, 58, Bottom),
            new Piece("furniture", "Room/Desk", 92, 157, 144, 70, Bottom),
            new Piece("furniture", "Room/Bookshelf", 8, 230, 97, 62, Bottom),
            new Piece("furniture", "Room/Backpack", 106, 244, 48, 51, Bottom),
            new Piece("furniture", "Room/Plant", 227, 237, 46, 55, Bottom),
            new Piece("furniture", "Room/Rug", 156, 258, 80, 36, Center),
            new Piece("furniture", "Room/DeskLamp", 276, 243, 30, 50, Bottom),

            // Pencere ve dekor (room_decor.png)
            new Piece("room_decor", "Window/WindowFrame", 7, 11, 90, 128, Center, punchCenter: true),
            new Piece("room_decor", "Window/CurtainLeft", 97, 11, 39, 130, Top),
            new Piece("room_decor", "Window/CurtainRight", 145, 11, 36, 130, Top),
            new Piece("room_decor", "Window/CitySunrise", 184, 13, 120, 61, Center),
            new Piece("room_decor", "Window/CityStorm", 184, 77, 120, 61, Center),
            new Piece("room_decor", "Decor/PosterRocket", 8, 201, 41, 56, Center),
            new Piece("room_decor", "Decor/PosterSaturn", 49, 203, 34, 47, Center),
            new Piece("room_decor", "Decor/PosterMoon", 84, 203, 36, 50, Center),
            new Piece("room_decor", "Decor/WorldMap", 122, 204, 43, 40, Center),
            new Piece("room_decor", "Decor/PhotoWall", 166, 204, 51, 43, Center),
            new Piece("room_decor", "Decor/PlantPot", 219, 204, 36, 43, Bottom),
            new Piece("room_decor", "Decor/PlantVine", 252, 207, 40, 59, Bottom),
            new Piece("room_decor", "Decor/PlantTiny", 291, 219, 18, 26, Bottom),
            new Piece("room_decor", "Decor/WallShelves", 7, 256, 124, 55, Center),
            new Piece("room_decor", "Decor/Globe", 136, 251, 33, 46, Bottom),
            new Piece("room_decor", "Decor/Astronaut", 228, 257, 20, 33, Bottom),
            new Piece("room_decor", "Decor/StarFrame", 250, 257, 21, 28, Center),
            new Piece("room_decor", "Decor/HangingPlant", 272, 250, 39, 61, Top),
            new Piece("room_decor", "Decor/BookStack", 171, 283, 36, 22, Bottom),
            new Piece("room_decor", "Decor/MoonLamp", 208, 279, 21, 27, Bottom),

            // Arayüz parçaları (ui_kit.png)
            new Piece("ui_kit", "UI/ClockBig", 15, 39, 124, 76, Center),
            new Piece("ui_kit", "UI/ThoughtLarge", 150, 42, 60, 64, Center),
            new Piece("ui_kit", "UI/ThoughtMedium", 219, 56, 41, 49, Center),
            new Piece("ui_kit", "UI/ThoughtSmall", 272, 63, 30, 38, Center),
            new Piece("ui_kit", "UI/ButtonBar", 43, 245, 227, 43, Center),
            new Piece("ui_kit", "UI/IconHome", 96, 185, 55, 55, Center),
            new Piece("ui_kit", "UI/IconSettings", 164, 185, 55, 55, Center),

            // Rüya bulutları 128x128 hazır piksel sanattan gelir (BuildBubbles).

            // Logo (logo.png) — yarı boyut
            new Piece("logo", "UI/LogoStacked", 10, 42, 294, 151, Center, step: 2),
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
                written.Add(Save(sheet.Extract(piece.cells, piece.step, piece.punchCenter), piece.output));
            }
            written.AddRange(ExtractBoyFrames(Sheet.Load(SourceDir + "boy_front_frames.png", 6, alignTop: true)));
            written.AddRange(BuildBubbles());
            written.AddRange(GenerateProcedural());

            AssetDatabase.Refresh();
            foreach (string path in written)
                ConfigureImporter(path, PivotFor(path));
            BuildAtlas();
            Debug.Log($"[MenuArtBuilder] {written.Count} sprite yazıldı ve içe aktarıldı.");
        }

        // Menü sprite'ları tek atlasa: çizim çağrısı azalır. Point filtre, sıkıştırmasız, mipmap yok.
        // Fx (1x1 Pixel, damlalar) ve Window (1 px genişlikte esnetilen gökyüzü gradyanları) atlasa girmez:
        // atlasta 1 piksellik sprite'lar kenardaki boş dolguyu örnekliyor, pencere maskesi tamamen boş kalıyordu.
        static void BuildAtlas()
        {
            const string atlasPath = OutDir + "MenuAtlas.spriteatlasv2";
            AssetDatabase.DeleteAsset(atlasPath); // klasör listesi değişmiş olabilir; her seferinde yeniden kur
            var atlas = new UnityEditor.U2D.SpriteAtlasAsset();
            var folders = new List<Object>();
            foreach (string folder in new[] { "Room", "Decor", "Boy", "Bubbles", "UI", "Tiles" })
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

        // Kare başına hesaplanan pivotlar (çocuk kareleri yatağın alt-ortasından hizalanır).
        static readonly Dictionary<string, Vector2> customPivots = new Dictionary<string, Vector2>();

        // Önden görünen çocuk kareleri: 4x2 ızgara, kaynakta 6 px'lik bloklar (kare ~64 sanat pikseli).
        // Bu boyutta yatak komodinden küçük kalıyordu; kareler tam 2 kat büyütülür (piksel bozulmaz).
        // zzz ve ses işaretleri küçültülünce kırık noktalara dönüştüğü için atılır; hizalama yalnızca yatak
        // gövdesine göre yapılır ki kareler değişirken yatak yerinde dursun.
        const int BoyScale = 2;
        static int bedWidthCells;

        static IEnumerable<string> ExtractBoyFrames(Sheet sheet)
        {
            const int columnCells = 64, rowCells = 85;
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                {
                    var region = new RectInt(i * columnCells, row * rowCells, columnCells, rowCells);
                    Texture2D frame = sheet.ExtractFrame(region, BoyScale, out Vector2 pivot, out int bedWidth);
                    if (row == 0 && i == 0) bedWidthCells = bedWidth;
                    string name = row == 0 ? $"Boy/BoySleep{i + 1}" : $"Boy/BoyWake{i + 1}";
                    customPivots[name.Substring(name.IndexOf('/') + 1)] = pivot;
                    yield return Save(frame, name);
                }
            yield return Save(BedFront(bedWidthCells), "Room/BedFront");
        }

        // Yatağın önü (karelerde yok): ahşap ray ve iki bacak. Çocuk kareleriyle aynı yoğunlukta (2x) çizilir.
        static Texture2D BedFront(int widthCells)
        {
            int w = Mathf.Max(8, widthCells), h = 11;
            var small = New(w, h);
            var light = new Color32(156, 96, 56, 255);
            var wood = new Color32(116, 64, 38, 255);
            var dark = new Color32(66, 36, 24, 255);
            for (int x = 0; x < w; x++)
            {
                Px(small, x, 0, x == 0 || x == w - 1 ? dark : light);
                for (int y = 1; y < 4; y++) Px(small, x, y, x == 0 || x == w - 1 ? dark : ((x + y) % 9 == 0 ? light : wood));
                Px(small, x, 4, dark);
            }
            foreach (int lx in new[] { 1, w - 5 })
                for (int y = 5; y < h; y++)
                    for (int x = lx; x < lx + 4; x++)
                        Px(small, x, y, x == lx ? light : x == lx + 3 ? dark : wood);
            small.Apply();
            var big = Upscale(small, BoyScale);
            Object.DestroyImmediate(small);
            return big;
        }

        static Texture2D Upscale(Texture2D src, int scale)
        {
            var dst = new Texture2D(src.width * scale, src.height * scale, TextureFormat.RGBA32, false);
            var sp = src.GetPixels32();
            var dp = new Color32[dst.width * dst.height];
            for (int y = 0; y < dst.height; y++)
                for (int x = 0; x < dst.width; x++)
                    dp[y * dst.width + x] = sp[(y / scale) * src.width + x / scale];
            dst.SetPixels32(dp);
            dst.Apply();
            return dst;
        }

        // Rüya bulutları: 128x128 hazır piksel sanat (kendi bulut çerçevesiyle). Kenar yumuşatması kaldırılır,
        // seçili hali için şekle uyan turuncu/mavi hale ve altına piksel yazılı etiket üretilir.
        static IEnumerable<string> BuildBubbles()
        {
            var defs = new (string source, string name, Color32 glow, string label)[]
            {
                ("bubble_quake_128", "Quake", new Color32(255, 150, 50, 255), "DEPREM"),
                ("bubble_flood_128", "Flood", new Color32(90, 175, 255, 255), "SEL"),
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
                yield return Save(Label(d.label), $"Bubbles/Label{d.name}");
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

        // 5x7 piksel yazıyla etiket: koyu lacivert bant, krem çerçeve.
        static Texture2D Label(string text)
        {
            var glyphs = new Dictionary<char, string[]>
            {
                ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
                ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
                ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
                ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
                ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
                ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
                ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            };
            int textW = text.Length * 6 - 1;
            int w = textW + 12, h = 15;
            var t = New(w, h);
            var border = new Color32(232, 222, 196, 255);
            var fill = new Color32(18, 26, 44, 255);
            var ink = new Color32(246, 240, 224, 255);
            var shadow = new Color32(70, 84, 112, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                    if (corner) continue;
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    Px(t, x, y, edge ? border : fill);
                }
            for (int c = 0; c < text.Length; c++)
            {
                string[] g = glyphs[text[c]];
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                        if (g[gy][gx] == '#')
                        {
                            int x = 6 + c * 6 + gx, y = 4 + gy;
                            Px(t, x, y + 1, shadow);
                            Px(t, x, y, ink);
                        }
            }
            return Done(t);
        }

        static Vector2 PivotFor(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (customPivots.TryGetValue(name, out Vector2 custom)) return custom;
            if (name == "BedFront") return Bottom;
            foreach (Piece piece in Pieces)
                if (piece.output.EndsWith(name))
                    return piece.pivot;
            if (name.StartsWith("Boy")) return BottomLeft;
            if (name.StartsWith("Tile")) return Center; // Tilemap sprite'ı hücre merkezine yerleştirir
            if (name == "PendantLamp") return Top;
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
            importer.wrapMode = path.Contains("/Tiles/") || path.Contains("Gradient") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            // Buton çerçevesi 9-dilim: genişleyince köşeler bozulmasın.
            if (Path.GetFileNameWithoutExtension(path) == "ButtonBar")
                importer.spriteBorder = new Vector4(14, 12, 14, 12);
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- kaynak sayfa
        class Sheet
        {
            Color32[] pixels;
            int width, height, cellsW, cellsH, block, gridTop;
            bool[] background;

            // block: kaynakta bir sanat pikselinin kaç görsel pikseli olduğu (Codex sayfaları 4, önden çocuk sayfası 6).
            // alignTop: ızgara görselin üstünden başlar. Codex sayfalarında ızgara alta hizalı (yükseklik bloğun katı
            // değilse artan satırlar üstte kalır); önden çocuk sayfasında üste hizalı, alta hizalamak bir blok kaydırıyordu.
            public static Sheet Load(string path, int block, bool alignTop = false)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(path));
                var sheet = new Sheet
                {
                    pixels = texture.GetPixels32(),
                    width = texture.width,
                    height = texture.height,
                    block = block,
                    gridTop = alignTop ? 0 : texture.height % block,
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

            bool[] FloodWhite(IEnumerable<Vector2Int> seeds, RectInt? limit = null)
            {
                var mask = new bool[cellsW * cellsH];
                var queue = new Queue<Vector2Int>(seeds);
                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    if (c.x < 0 || c.y < 0 || c.x >= cellsW || c.y >= cellsH)
                        continue;
                    if (limit.HasValue && !limit.Value.Contains(c))
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
            // taşan kırıntılar (yastık kenarı, yan karedeki çizgi) atılır. 2 hücrelik boşluklar köprülenir.
            bool[] KeepMask(RectInt cells, bool[] hole)
            {
                int[] label = Components(cells, hole, out List<int> sizes);
                int largest = 0;
                foreach (int s in sizes) largest = Mathf.Max(largest, s);
                var keep = new bool[label.Length];
                for (int i = 0; i < keep.Length; i++)
                    keep[i] = label[i] != 0 && sizes[label[i]] >= largest * 0.15f;
                return keep;
            }

            // Dikdörtgendeki dolu hücrelerin bağlı parçaları ('bridge' hücre uzaklığa kadar boşluklar köprülenir;
            // 1 = sadece komşu hücreler). sizes[0] kullanılmaz.
            int[] Components(RectInt cells, bool[] hole, out List<int> sizes, int bridge = 2)
            {
                int w = cells.width, h = cells.height;
                var label = new int[w * h];
                sizes = new List<int> { 0 };
                var stack = new Stack<int>();
                bool Solid(int lx, int ly)
                {
                    int cx = cells.x + lx, cy = cells.y + ly;
                    if (cx >= cellsW || cy >= cellsH) return false;
                    int i = cy * cellsW + cx;
                    return !background[i] && (hole == null || !hole[i]);
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
                        for (int dy = -bridge; dy <= bridge; dy++)
                            for (int dx = -bridge; dx <= bridge; dx++)
                            {
                                int nx = lx + dx, ny = ly + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                int n = ny * w + nx;
                                if (label[n] == 0 && Solid(nx, ny)) { label[n] = id; stack.Push(n); }
                            }
                    }
                    sizes.Add(size);
                }
                return label;
            }

            // zzz ve ses işaretleri: zemine değen küçük lacivert ya da koyu gri lekeler. Bazıları başlığa/saça bitişik
            // olduğu için parça boyutuyla ayrılamıyor; delik olarak işaretlenir. Saç, ten ve ahşap sıcak renkli olduğundan
            // dokunulmaz; gözler yüzün, yastık başlığın içinde kaldığından zemine değmez. Açık gri kenar halesi sayılmaz:
            // çizimin çevresini saran tek parça olup işaretleri büyük bir lekeye bağlıyordu.
            bool[] MarkCells(RectInt region)
            {
                const int MaxMarkCells = 60;
                int w = region.width, h = region.height;
                var marks = new bool[cellsW * cellsH];
                var seen = new bool[w * h];
                var stack = new Stack<int>();
                var blob = new List<int>();
                bool Inside(int lx, int ly) => lx >= 0 && ly >= 0 && lx < w && ly < h && region.x + lx < cellsW && region.y + ly < cellsH;
                bool Background(int lx, int ly) => !Inside(lx, ly) || background[(region.y + ly) * cellsW + region.x + lx];
                bool Cool(int lx, int ly)
                {
                    if (Background(lx, ly)) return false;
                    Color32 c = Cell(region.x + lx, region.y + ly);
                    bool gray = Mathf.Abs(c.r - c.b) <= 16 && Mathf.Abs(c.r - c.g) <= 16 && c.r + c.g + c.b < 480;
                    return c.b > c.r + 20 || gray;
                }
                for (int start = 0; start < w * h; start++)
                {
                    if (seen[start] || !Cool(start % w, start / w)) continue;
                    blob.Clear();
                    bool touchesBackground = false;
                    seen[start] = true;
                    stack.Push(start);
                    while (stack.Count > 0)
                    {
                        int c = stack.Pop();
                        blob.Add(c);
                        int lx = c % w, ly = c / w;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = lx + dx, ny = ly + dy;
                                if (Background(nx, ny)) { touchesBackground = true; continue; }
                                int n = ny * w + nx;
                                if (!seen[n] && Cool(nx, ny)) { seen[n] = true; stack.Push(n); }
                            }
                    }
                    if (!touchesBackground || blob.Count > MaxMarkCells) continue;
                    foreach (int c in blob)
                        marks[(region.y + c / w) * cellsW + region.x + c % w] = true;
                }
                return marks;
            }

            // Bir animasyon karesini keser: küçük parçalar (zzz, ses işaretleri) atılır, kare içerik kutusuna göre
            // kırpılır ve 'scale' kat büyütülür. Pivot en büyük parçanın (yatağın) alt-ortasıdır; böylece kareler
            // arasında yatak kıpırdamaz. bedWidth: yatağın hücre cinsinden genişliği.
            public Texture2D ExtractFrame(RectInt region, int scale, out Vector2 pivot, out int bedWidth)
            {
                // Köprüleme yok: zzz harfleri başlığa tek hücrelik boşlukla duruyor, köprülenirse yatağa katılıyordu.
                bool[] marks = MarkCells(region);
                int[] label = Components(region, marks, out List<int> sizes, bridge: 1);
                int largestId = 1;
                for (int id = 1; id < sizes.Count; id++)
                    if (sizes[id] > sizes[largestId]) largestId = id;
                int w = region.width, h = region.height;
                // Kaynaktaki ince zzz/ses çizgileri bir bloktan ince; örneklenince kırık noktalara dönüşüyordu. Yatak +
                // çocuk (en büyük parça) ve ona yakın büyüklükteki parçalar tutulur, kırıntılar atılır.
                var keep = new bool[w * h];
                for (int i = 0; i < keep.Length; i++)
                    keep[i] = label[i] != 0 && sizes[label[i]] >= sizes[largestId] * 0.1f;
                ErodeLightFringe(region, null, keep);

                int bx0 = w, bx1 = -1, by1 = -1, ux0 = w, ux1 = -1, uy0 = h, uy1 = -1;
                for (int ly = 0; ly < h; ly++)
                    for (int lx = 0; lx < w; lx++)
                    {
                        int i = ly * w + lx;
                        if (!keep[i]) continue;
                        ux0 = Mathf.Min(ux0, lx); ux1 = Mathf.Max(ux1, lx); uy0 = Mathf.Min(uy0, ly); uy1 = Mathf.Max(uy1, ly);
                        if (label[i] == largestId) { bx0 = Mathf.Min(bx0, lx); bx1 = Mathf.Max(bx1, lx); by1 = Mathf.Max(by1, ly); }
                    }
                int uw = ux1 - ux0 + 1, uh = uy1 - uy0 + 1;
                bedWidth = bx1 - bx0 + 1;
                pivot = new Vector2(((bx0 + bx1 + 1) / 2f - ux0) / uw, (uy1 - by1) / (float)uh);

                var texture = new Texture2D(uw * scale, uh * scale, TextureFormat.RGBA32, false);
                var output = new Color32[texture.width * texture.height];
                for (int ly = uy0; ly <= uy1; ly++)
                    for (int lx = ux0; lx <= ux1; lx++)
                    {
                        if (!keep[ly * w + lx]) continue;
                        Color32 c = Cell(region.x + lx, region.y + ly);
                        c.a = 255;
                        int ox = (lx - ux0) * scale, oyTop = (ly - uy0) * scale;
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                output[(texture.height - 1 - (oyTop + sy)) * texture.width + ox + sx] = c;
                    }
                texture.SetPixels32(output);
                texture.Apply();
                return texture;
            }

            // Kaynakta beyaz zeminle nesne arasında kalan açık gri yumuşatma pikselleri sprite'ın etrafında
            // açık bir hale bırakıyordu. Şeffaf alana komşu, açık ve doygunluğu düşük pikseller iki tur aşındırılır
            // (doygun parıltılar, ör. bulutların turuncu/mavi halesi, korunur).
            void ErodeLightFringe(RectInt cells, bool[] hole, bool[] keep)
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

            public Texture2D Extract(RectInt cells, int step, bool punchCenter)
            {
                bool[] hole = punchCenter
                    ? FloodWhite(new[] { new Vector2Int(cells.x + cells.width / 2, cells.y + cells.height / 2) }, cells)
                    : null;
                bool[] keep = KeepMask(cells, hole);
                ErodeLightFringe(cells, hole, keep);
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
                        int i = cy * cellsW + cx;
                        if (background[i] || (hole != null && hole[i]) || !keep[(oy * step) * cells.width + ox * step])
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
            yield return Save(WallTile(), "Tiles/TileWall");
            yield return Save(WallTopTile(), "Tiles/TileWallTop");
            yield return Save(BaseboardTile(), "Tiles/TileBaseboard");
            yield return Save(FloorTile(), "Tiles/TileFloor");
            yield return Save(RoomClock(), "Room/RoomClock");
            yield return Save(Solid(1, 1, Color.white), "Fx/Pixel");
            yield return Save(RainDrop(), "Fx/RainDrop");
            yield return Save(GlassDroplets(), "Fx/GlassDroplets");
            yield return Save(LightningBolt(), "Fx/LightningBolt");
            yield return Save(Gradient(new Color32(250, 170, 90, 255), new Color32(120, 80, 150, 255)), "Window/SkyGradientSunrise");
            yield return Save(Gradient(new Color32(40, 50, 80, 255), new Color32(14, 18, 36, 255)), "Window/SkyGradientStorm");
            yield return Save(Sun(), "Window/Sun");
            yield return Save(StormCloud(), "Window/StormCloud");
            yield return Save(Rubble(new Color32(120, 110, 104, 255)), "Fx/Rubble");
            yield return Save(PendantLamp(), "Room/PendantLamp");
        }

        // Tavandan sarkan lamba: ince kablo + kubbe abajur + sıcak ampul. 16x40, pivot üstte (sallanır).
        static Texture2D PendantLamp()
        {
            var t = New(16, 40);
            var cord = new Color32(30, 26, 34, 255);
            var shade = new Color32(176, 96, 52, 255);
            var shadeDark = new Color32(120, 60, 36, 255);
            var shadeLight = new Color32(222, 140, 74, 255);
            var bulb = new Color32(255, 230, 160, 255);
            for (int y = 0; y < 28; y++) Px(t, 7, y, cord);
            Px(t, 6, 27, cord); Px(t, 8, 27, cord);
            for (int y = 28; y < 36; y++)
            {
                int half = 2 + (y - 28);
                for (int x = 8 - half; x < 8 + half; x++)
                    Px(t, x, y, x == 8 - half ? shadeDark : x == 8 + half - 1 ? shadeDark : (y == 29 && x < 8) ? shadeLight : shade);
            }
            for (int x = 5; x < 11; x++) Px(t, x, 36, bulb);
            for (int x = 6; x < 10; x++) Px(t, x, 37, new Color32(255, 214, 140, 180));
            return Done(t);
        }

        static Texture2D New(int w, int h) { var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[w * h]); return t; }
        static void Px(Texture2D t, int x, int yTop, Color32 c) { if (x >= 0 && yTop >= 0 && x < t.width && yTop < t.height) t.SetPixel(x, t.height - 1 - yTop, c); }
        static Texture2D Done(Texture2D t) { t.Apply(); return t; }
        static Texture2D Solid(int w, int h, Color32 c) { var t = New(w, h); for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) Px(t, x, y, c); return Done(t); }

        // Lacivert-mor duvar kâğıdı, ince dikey desen. 16x16, kesintisiz tekrar eder.
        static Texture2D WallTile()
        {
            var t = New(16, 16);
            var baseC = new Color32(52, 46, 78, 255);
            var dark = new Color32(44, 39, 68, 255);
            var light = new Color32(60, 54, 88, 255);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    Color32 c = baseC;
                    if (x == 0 || x == 8) c = dark;
                    else if ((x == 4 || x == 12) && (y % 4 == 1)) c = light;
                    Px(t, x, y, c);
                }
            return Done(t);
        }

        // Tavana yakın koyu şerit.
        static Texture2D WallTopTile()
        {
            var t = New(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    Px(t, x, y, y < 10 ? new Color32(30, 26, 46, 255) : y < 12 ? new Color32(70, 52, 48, 255) : new Color32(46, 41, 70, 255));
            return Done(t);
        }

        static Texture2D BaseboardTile()
        {
            var t = New(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    Color32 c = y < 6 ? new Color32(52, 46, 78, 255)
                        : y == 6 ? new Color32(140, 86, 52, 255)
                        : y < 14 ? new Color32(104, 62, 40, 255)
                        : new Color32(70, 40, 28, 255);
                    Px(t, x, y, c);
                }
            return Done(t);
        }

        // Yandan görünen ahşap zemin: yatay tahtalar (dikey derz yok; derzler tuğla duvar gibi görünüyordu),
        // ince damar noktaları ve tahtaların üst kenarında sıcak ışık.
        static Texture2D FloorTile()
        {
            var t = New(16, 16);
            Color32[] planks = { new Color32(112, 64, 40, 255), new Color32(100, 56, 35, 255), new Color32(106, 60, 38, 255), new Color32(96, 53, 33, 255) };
            var seam = new Color32(58, 32, 22, 255);
            var light = new Color32(146, 88, 52, 255);
            var grain = new Color32(88, 48, 30, 255);
            for (int y = 0; y < 16; y++)
            {
                int row = y / 4;
                for (int x = 0; x < 16; x++)
                {
                    Color32 c = planks[row];
                    if (y % 4 == 3) c = seam;
                    else if (y % 4 == 0 && (x + row * 5) % 9 < 3) c = light;
                    else if (y % 4 == 1 && (x * 7 + row * 3) % 11 == 0) c = grain;
                    Px(t, x, y, c);
                }
            }
            return Done(t);
        }

        // Komodin saati: koyu gövde, kırmızı 7 segment "07:00". 30x14.
        static Texture2D RoomClock()
        {
            var t = New(30, 14);
            var body = new Color32(28, 26, 34, 255);
            var rim = new Color32(70, 64, 80, 255);
            var screen = new Color32(16, 8, 10, 255);
            var led = new Color32(255, 52, 40, 255);
            for (int y = 1; y < 13; y++)
                for (int x = 0; x < 30; x++)
                    Px(t, x, y, (y == 1 || y == 12 || x == 0 || x == 29) ? rim : body);
            for (int y = 3; y < 11; y++)
                for (int x = 2; x < 28; x++)
                    Px(t, x, y, screen);
            Px(t, 4, 13, rim); Px(t, 25, 13, rim);
            // 3x6 rakamlar: 0 7 : 0 0
            void Digit(int ox, string[] rows) { for (int y = 0; y < rows.Length; y++) for (int x = 0; x < rows[y].Length; x++) if (rows[y][x] == '#') Px(t, ox + x, 4 + y, led); }
            string[] zero = { "###", "#.#", "#.#", "#.#", "#.#", "###" };
            string[] seven = { "###", "..#", "..#", "..#", "..#", "..#" };
            Digit(4, zero); Digit(9, seven);
            Px(t, 14, 5, led); Px(t, 14, 8, led);
            Digit(17, zero); Digit(22, zero);
            return Done(t);
        }

        static Texture2D RainDrop()
        {
            var t = New(1, 4);
            for (int y = 0; y < 4; y++) Px(t, 0, y, new Color32(190, 215, 255, (byte)(90 + y * 50)));
            return Done(t);
        }

        // Camda damlalar: seyrek, kesintisiz tekrar eden 32x32.
        static Texture2D GlassDroplets()
        {
            var t = New(32, 32);
            var rng = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                int x = rng.Next(32), y = rng.Next(32), len = 1 + rng.Next(3);
                for (int k = 0; k < len; k++) Px(t, x, (y + k) % 32, new Color32(200, 225, 255, 150));
                Px(t, x, (y + len) % 32, new Color32(240, 250, 255, 220));
            }
            return Done(t);
        }

        static Texture2D LightningBolt()
        {
            var t = New(12, 34);
            Vector2Int[] pts = { new Vector2Int(6, 0), new Vector2Int(3, 10), new Vector2Int(7, 14), new Vector2Int(2, 25), new Vector2Int(6, 27), new Vector2Int(4, 33) };
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector2Int a = pts[i], b = pts[i + 1];
                    int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
                    for (int s = 0; s <= steps; s++)
                    {
                        float k = steps == 0 ? 0 : s / (float)steps;
                        int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, k)), y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, k));
                        if (pass == 0) { for (int dx = -1; dx <= 2; dx++) Px(t, x + dx, y, new Color32(160, 195, 255, 140)); }
                        else { Px(t, x, y, new Color32(245, 248, 255, 255)); Px(t, x + 1, y, new Color32(245, 248, 255, 255)); }
                    }
                }
            return Done(t);
        }

        // 1x64 dikey gradyan (pencere gökyüzü); y üstten.
        static Texture2D Gradient(Color32 bottom, Color32 top)
        {
            var t = New(1, 64);
            for (int y = 0; y < 64; y++)
            {
                // 4'lük bantlar: piksel sanatta yumuşak geçiş yerine basamaklı geçiş
                float k = Mathf.Floor((63 - y) / 4f) / 15f;
                Px(t, 0, y, Color32.Lerp(bottom, top, k));
            }
            return Done(t);
        }

        static Texture2D Sun()
        {
            var t = New(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8, 8));
                    if (d < 5) Px(t, x, y, new Color32(255, 236, 170, 255));
                    else if (d < 6.5f) Px(t, x, y, new Color32(255, 196, 110, 200));
                    else if (d < 8) Px(t, x, y, new Color32(255, 170, 90, 90));
                }
            return Done(t);
        }

        // Fırtına bulutu: birkaç daireden oluşan koyu küme. 48x20.
        static Texture2D StormCloud()
        {
            var t = New(48, 20);
            Vector3[] blobs = { new Vector3(10, 12, 7), new Vector3(20, 8, 9), new Vector3(31, 10, 8), new Vector3(40, 13, 6) };
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 48; x++)
                {
                    float best = 99;
                    foreach (Vector3 b in blobs) best = Mathf.Min(best, Vector2.Distance(new Vector2(x, y), new Vector2(b.x, b.y)) - b.z);
                    if (best < 0) Px(t, x, y, y < 9 + (x % 5 == 0 ? 1 : 0) ? new Color32(70, 76, 104, 255) : new Color32(44, 48, 72, 255));
                }
            return Done(t);
        }

        static Texture2D Rubble(Color32 c)
        {
            var t = New(3, 3);
            var dark = new Color32((byte)(c.r / 2), (byte)(c.g / 2), (byte)(c.b / 2), 255);
            Px(t, 1, 0, c); Px(t, 0, 1, c); Px(t, 1, 1, c); Px(t, 2, 1, dark); Px(t, 1, 2, dark);
            return Done(t);
        }
    }
}
