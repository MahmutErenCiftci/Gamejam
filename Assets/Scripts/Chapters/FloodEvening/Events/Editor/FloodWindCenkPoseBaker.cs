using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening.EditorTools
{
    /// <summary>
    /// Cenk'in rüzgâra yaslanan yürüme karelerini üretir (Tools/Afet Uyanışı/Rüzgâr: Cenk karelerini üret).
    /// Kaynak: Cenk'in 8 yönlü yürüme sayfaları (okul çantalı, çantasız, deprem çantalı, yağmurluklu; 6 kare × 96 px).
    /// Her yön için bir sayfa yazılır: sütun = kare, satır = yaslanma (-6..-1, 1..6 piksel; ayak satırı sabit, eğim
    /// yukarı doğru u^BendPower ile artar). Pivotlar kaynak karelerle aynı; FE_WindCenkPoses varlığı doldurulur.
    /// Cenk'in çizimi değişince yeniden çalıştırmak yeterli (aynı adlı dilimlerin kimliği korunur).
    /// </summary>
    public static class FloodWindCenkPoseBaker
    {
        private const string Src = "Assets/Art/Characters/Cenk";
        private const string Dst = "Assets/Art/FloodEvening/Events/Wind/Cenk";
        private const string LibPath = "Assets/Data/FloodEvening/FE_WindCenkPoses.asset";
        private const int F = 96;
        private const int Frames = 6;
        private const float BendPower = 1.35f;

        private static readonly CenkOutfit.Look[] Looks = { CenkOutfit.Look.SchoolBag, CenkOutfit.Look.NoBag, CenkOutfit.Look.QuakeBag, CenkOutfit.Look.Raincoat };

        private struct Sheet
        {
            public CenkOutfit.Look Look;
            public IsoCharacterAnimator.Direction Dir;
            public string SourcePath, Path;
            public Vector2[] Pivots;
        }

        [MenuItem("Tools/Afet Uyanışı/Rüzgâr: Cenk karelerini üret")]
        public static void Bake()
        {
            Directory.CreateDirectory(Dst);
            var sheets = new List<Sheet>();
            foreach (var look in Looks)
            foreach (IsoCharacterAnimator.Direction dir in System.Enum.GetValues(typeof(IsoCharacterAnimator.Direction)))
            {
                string sp = SourcePath(look, dir.ToString());
                if (!File.Exists(sp)) { Debug.LogWarning("[WindPoses] Kaynak yok: " + sp); continue; }
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                src.LoadImage(File.ReadAllBytes(sp));
                if (src.width != F * Frames || src.height != F) { Debug.LogWarning("[WindPoses] Beklenmeyen boyut: " + sp); Object.DestroyImmediate(src); continue; }
                var px = src.GetPixels32();
                Object.DestroyImmediate(src);

                var pivots = new Vector2[Frames];
                var srcSprites = LoadSlices(sp);
                var sheet = new Texture2D(F * Frames, F * FloodWindCenkPoses.LeanCount, TextureFormat.RGBA32, false);
                sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
                for (int f = 0; f < Frames; f++)
                {
                    var frame = Crop(px, F * Frames, f * F);
                    Bounds(frame, out int yFeet, out int yTop);
                    pivots[f] = srcSprites[f] != null ? srcSprites[f].pivot / F : new Vector2(0.5f, 0f);
                    for (int l = 0; l < FloodWindCenkPoses.LeanCount; l++)
                        sheet.SetPixels32(f * F, RowY(l), F, F, Lean(frame, ShearOf(l), yFeet, yTop));
                }
                string suffix = look == CenkOutfit.Look.SchoolBag ? "" : "_" + look;
                string path = $"{Dst}/CHR_Cenk_WindLean_{dir}{suffix}.png";
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                sheets.Add(new Sheet { Look = look, Dir = dir, SourcePath = sp, Path = path, Pivots = pivots });
            }
            AssetDatabase.Refresh();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            foreach (var s in sheets) Slice(factory, s);

            var entries = new List<FloodWindCenkPoses.Entry>();
            foreach (var s in sheets)
            {
                var srcSprites = LoadSlices(s.SourcePath);
                var lean = new Dictionary<string, Sprite>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(s.Path))
                    if (o is Sprite spr) lean[spr.name] = spr;
                string baseName = Path.GetFileNameWithoutExtension(s.Path);
                for (int f = 0; f < Frames; f++)
                {
                    if (srcSprites[f] == null) continue;
                    var arr = new Sprite[FloodWindCenkPoses.LeanCount];
                    for (int l = 0; l < arr.Length; l++) lean.TryGetValue(SliceName(baseName, f, l), out arr[l]);
                    entries.Add(new FloodWindCenkPoses.Entry { source = srcSprites[f], look = s.Look, direction = s.Dir, frame = f, lean = arr });
                }
            }

            var lib = AssetDatabase.LoadAssetAtPath<FloodWindCenkPoses>(LibPath);
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibPath));
                lib = ScriptableObject.CreateInstance<FloodWindCenkPoses>();
                AssetDatabase.CreateAsset(lib, LibPath);
            }
            lib.Entries = entries.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WindPoses] {sheets.Count} sayfa, {entries.Count} kare → {LibPath}");
        }

        private static string SourcePath(CenkOutfit.Look look, string dir)
        {
            switch (look)
            {
                case CenkOutfit.Look.NoBag: return $"{Src}/NoBag/CHR_Cenk_Walk_{dir}_NoBag.png";
                case CenkOutfit.Look.QuakeBag: return $"{Src}/QuakeBag/CHR_Cenk_Walk_{dir}_QuakeBag.png";
                case CenkOutfit.Look.Raincoat: return $"{Src}/Raincoat/CHR_Cenk_Walk_{dir}_Raincoat.png";
                default: return $"{Src}/CHR_Cenk_Walk_{dir}.png";
            }
        }

        // Dilimleri x konumuna göre sırala (ad sırası 10'dan sonra bozulabilir).
        private static Sprite[] LoadSlices(string path)
        {
            var arr = new Sprite[Frames];
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s)
                {
                    int f = Mathf.RoundToInt(s.rect.x / F);
                    if (f >= 0 && f < Frames) arr[f] = s;
                }
            return arr;
        }

        private static int ShearOf(int l) => l < FloodWindCenkPoses.MaxShear ? l - FloodWindCenkPoses.MaxShear : l - FloodWindCenkPoses.MaxShear + 1;
        private static int RowY(int l) => (FloodWindCenkPoses.LeanCount - 1 - l) * F;
        private static string SliceName(string baseName, int f, int l) => $"{baseName}_{f}_{(ShearOf(l) < 0 ? "m" : "p")}{Mathf.Abs(ShearOf(l))}";

        private static Color32[] Crop(Color32[] px, int width, int x0)
        {
            var o = new Color32[F * F];
            for (int y = 0; y < F; y++)
            for (int x = 0; x < F; x++)
                o[y * F + x] = px[y * width + x0 + x];
            return o;
        }

        private static void Bounds(Color32[] px, out int yFeet, out int yTop)
        {
            yFeet = F; yTop = 0;
            for (int y = 0; y < F; y++)
            for (int x = 0; x < F; x++)
                if (px[y * F + x].a > 0) { yFeet = Mathf.Min(yFeet, y); yTop = Mathf.Max(yTop, y); }
            if (yTop < yFeet) { yFeet = 0; yTop = F - 1; }
        }

        private static Color32[] Lean(Color32[] src, int shear, int yFeet, int yTop)
        {
            var o = new Color32[F * F];
            int h = Mathf.Max(1, yTop - yFeet);
            for (int y = 0; y < F; y++)
            {
                float u = Mathf.Clamp01((y - yFeet) / (float)h);
                int sh = Mathf.RoundToInt(shear * Mathf.Pow(u, BendPower));
                for (int x = 0; x < F; x++)
                {
                    int sx = x - sh;
                    if (sx >= 0 && sx < F) o[y * F + x] = src[y * F + sx];
                }
            }
            return o;
        }

        private static void Slice(SpriteDataProviderFactories factory, Sheet s)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(s.Path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = 64;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();

            var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
            dp.InitSpriteEditorDataProvider();
            var old = new Dictionary<string, GUID>();
            foreach (var r in dp.GetSpriteRects()) old[r.name] = r.spriteID;
            string baseName = Path.GetFileNameWithoutExtension(s.Path);
            var rects = new List<SpriteRect>();
            for (int f = 0; f < Frames; f++)
            for (int l = 0; l < FloodWindCenkPoses.LeanCount; l++)
            {
                string n = SliceName(baseName, f, l);
                rects.Add(new SpriteRect
                {
                    name = n,
                    rect = new Rect(f * F, RowY(l), F, F),
                    alignment = SpriteAlignment.Custom,
                    pivot = s.Pivots[f],
                    spriteID = old.TryGetValue(n, out var id) ? id : GUID.Generate(),
                });
            }
            dp.SetSpriteRects(rects.ToArray());
            var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null)
            {
                var pairs = new List<SpriteNameFileIdPair>();
                foreach (var r in rects) pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
                names.SetNameFileIdPairs(pairs);
            }
            dp.Apply();
            imp.SaveAndReimport();
        }
    }
}
