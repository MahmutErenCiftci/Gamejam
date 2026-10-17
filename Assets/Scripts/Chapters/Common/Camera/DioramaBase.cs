using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Odanın izometrik zemininin altına kalın bir kesit (maket tabanı) ve altına yumuşak, dither'lı gölge çizer:
    /// oda masada duran bir maket gibi görünür (Mahmut). "Floor" adlı iso Tilemap'e GameCameraSettings tarafından
    /// eklenir; oda ilk açıldığında zeminin dolu hücrelerinden (Tilemap.cellBounds) tek bir sprite üretir, böylece
    /// sahne/prefab düzenlemesi gerekmez. Sorting order -200: zeminin (-100) altında, arka planın (-1000) üstünde;
    /// CustomAxis (0,1,0) sıralaması etkilenmez. Kendi gökyüzü olan dış mekânlarda (sokak, okul yolu, toplanma alanı)
    /// ve iso olmayan ızgaralarda hiçbir şey çizmez.
    /// Kesit desenleri: Assets/Resources/Backdrop/DIO_{Wood|Concrete}_{L|R}.png (64x24, okunabilir, Point).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap))]
    public class DioramaBase : MonoBehaviour
    {
        public const int SlabOrder = -200;
        /// <summary>Bu değerin altında sorting order'ı olan renderer varsa oda "kendi arka planı olan" dış mekân sayılır.</summary>
        public const int OwnBackdropOrder = -100;

        private const int Ppu = 64;
        private const int Depth = 24;       // kesit kalınlığı (piksel)
        private const int ShadowRows = 12;  // gölge yüksekliği (piksel)
        private const int ShadowSpread = 8; // gölgenin iki uçtan taşması (piksel)
        private static readonly Color32 Outline = new Color32(0x14, 0x14, 0x1F, 255);
        private static readonly Color32 Shadow = new Color32(0x0A, 0x0A, 0x12, 150);

        private static readonly float[] Bayer4 =
        {
            0f / 16f, 8f / 16f, 2f / 16f, 10f / 16f,
            12f / 16f, 4f / 16f, 14f / 16f, 6f / 16f,
            3f / 16f, 11f / 16f, 1f / 16f, 9f / 16f,
            15f / 16f, 7f / 16f, 13f / 16f, 5f / 16f,
        };

        private bool built;
        private GameObject slab;

        /// <summary>Sahnedeki "Floor" adlı tilemap'lere bileşeni ekler (oda açılınca kendini kurar).</summary>
        public static void AttachAll(Scene scene)
        {
            foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
            {
                if (tm == null || tm.gameObject.scene != scene || tm.name != "Floor") continue;
                if (tm.GetComponent<DioramaBase>() == null) tm.gameObject.AddComponent<DioramaBase>();
            }
        }

        public static void EnsureBuiltUnder(Transform root)
        {
            if (root == null) return;
            foreach (var d in root.GetComponentsInChildren<DioramaBase>(true)) d.EnsureBuilt();
        }

        public static void EnsureBuiltInScene(Scene scene)
        {
            if (!scene.IsValid()) return;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var d in go.GetComponentsInChildren<DioramaBase>(true)) d.EnsureBuilt();
        }

        private void Start() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (built) return;
            built = true;
            try { Build(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        private void Build()
        {
            var tm = GetComponent<Tilemap>();
            var grid = GetComponentInParent<Grid>(true);
            if (tm == null || grid == null || grid.cellLayout != GridLayout.CellLayout.Isometric) return;
            if (Mathf.Abs(grid.cellSize.x - 1f) > 0.01f || Mathf.Abs(grid.cellSize.y - 0.5f) > 0.01f) return;
            Transform room = grid.transform.parent;
            if (room != null ? RoomFraming.HasOwnBackdrop(room) : SceneHasOwnBackdrop(gameObject.scene)) return;

            // Her sütun için zeminin en alttaki pikseli (dünya pikseli) ve o pikselin hangi yüze (sol/sağ) ait olduğu.
            var low = new Dictionary<int, int>();
            var leftFace = new Dictionary<int, bool>();
            var b = tm.cellBounds;
            int cells = 0;
            for (int z = b.zMin; z < b.zMax; z++)
            for (int y = b.yMin; y < b.yMax; y++)
            for (int x = b.xMin; x < b.xMax; x++)
            {
                var cell = new Vector3Int(x, y, z);
                if (!tm.HasTile(cell)) continue;
                cells++;
                Vector3 w = tm.CellToWorld(cell); // hücrenin alt köşesi
                int bx = Mathf.RoundToInt(w.x * Ppu), by = Mathf.RoundToInt(w.y * Ppu);
                // 64x32 elmas karo: opak sütunlar -31..30; alt kenar sol yarıda 15-floor((rx+31)/2), sağda floor((rx+1)/2)
                for (int rx = -31; rx <= 30; rx++)
                {
                    int yb = by + (rx <= 0 ? 15 - FloorDiv(rx + 31, 2) : FloorDiv(rx + 1, 2));
                    int col = bx + rx;
                    if (!low.TryGetValue(col, out int cur) || yb < cur)
                    {
                        low[col] = yb;
                        leftFace[col] = rx < 0;
                    }
                }
            }
            if (cells == 0 || low.Count == 0) return;

            int minCol = int.MaxValue, maxCol = int.MinValue, minLow = int.MaxValue, maxLow = int.MinValue;
            foreach (var kv in low)
            {
                minCol = Mathf.Min(minCol, kv.Key); maxCol = Mathf.Max(maxCol, kv.Key);
                minLow = Mathf.Min(minLow, kv.Value); maxLow = Mathf.Max(maxLow, kv.Value);
            }

            int ox = minCol - ShadowSpread - 1;
            int oy = minLow - Depth - 1 - ShadowRows - 1;
            int w2 = (maxCol + ShadowSpread + 1) - ox + 1;
            int h2 = (maxLow - 1) - oy + 1;
            var px = new Color32[w2 * h2];
            var solid = new bool[w2 * h2];

            bool wood = UsesWood(room != null ? room.name : gameObject.scene.name);
            var swL = Swatch(wood ? "Backdrop/DIO_Wood_L" : "Backdrop/DIO_Concrete_L", wood, true);
            var swR = Swatch(wood ? "Backdrop/DIO_Wood_R" : "Backdrop/DIO_Concrete_R", wood, false);

            // 1) kesit yüzleri (üst satır açık, sol yüz orta, sağ yüz koyu — desenlerde)
            foreach (var kv in low)
            {
                int col = kv.Key, top = kv.Value - 1;
                var sw = leftFace[col] ? swL : swR;
                for (int d = 0; d < Depth; d++)
                {
                    int i = Index(col - ox, top - d - oy, w2, h2);
                    if (i < 0) continue;
                    px[i] = sw.Get(Mod(col, sw.width), d);
                    solid[i] = true;
                }
            }
            // ön köşe (sol yüzden sağ yüze geçiş) dikey ışık çizgisi
            foreach (var kv in low)
            {
                int col = kv.Key;
                if (!leftFace[col] || !leftFace.TryGetValue(col + 1, out bool nextLeft) || nextLeft) continue;
                for (int d = 1; d < Depth - 1; d++)
                {
                    int i = Index(col - ox, kv.Value - 1 - d - oy, w2, h2);
                    if (i >= 0) px[i] = Color32.Lerp(px[i], swL.Get(0, 0), 0.6f);
                }
            }
            // 2) 1 piksel kontur (yüzlerin altı ve yanları; üstte zemin var)
            var outline = new List<int>();
            for (int y = 0; y < h2; y++)
            for (int x = 0; x < w2; x++)
            {
                int i = y * w2 + x;
                if (solid[i]) continue;
                bool edge = (x > 0 && solid[i - 1]) || (x < w2 - 1 && solid[i + 1]) || (y < h2 - 1 && solid[i + w2]);
                if (edge) outline.Add(i);
            }
            foreach (int i in outline) { px[i] = Outline; solid[i] = true; }

            // 3) altta dither'lı yumuşak gölge (uçlarda biraz taşar)
            for (int x = 0; x < w2; x++)
            {
                int col = x + ox;
                int nearest = Mathf.Clamp(col, minCol, maxCol);
                if (!low.TryGetValue(nearest, out int l)) continue;
                int outside = col < minCol ? minCol - col : (col > maxCol ? col - maxCol : 0);
                float hFade = 1f - outside / (float)(ShadowSpread + 1);
                int start = l - Depth - 2 - oy; // alt konturun hemen altı
                for (int k = 0; k < ShadowRows; k++)
                {
                    int y = start - k;
                    if (y < 0 || y >= h2) continue;
                    int i = y * w2 + x;
                    if (solid[i]) continue;
                    float density = (1f - (float)k / ShadowRows) * hFade * 0.95f;
                    if (density > Bayer4[(y & 3) * 4 + (x & 3)]) px[i] = Shadow;
                }
            }

            var tex = new Texture2D(w2, h2, TextureFormat.RGBA32, false)
            {
                name = "DioramaBase_" + (room != null ? room.name : gameObject.scene.name),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w2, h2), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            sprite.name = tex.name;

            slab = new GameObject("DioramaBase");
            slab.transform.SetParent(transform, false);
            slab.transform.position = new Vector3((float)ox / Ppu, (float)oy / Ppu, transform.position.z);
            var sr = slab.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = GetComponent<TilemapRenderer>() != null ? GetComponent<TilemapRenderer>().sortingLayerID : 0;
            sr.sortingOrder = SlabOrder;
        }

        private static bool SceneHasOwnBackdrop(Scene scene)
        {
            if (!scene.IsValid()) return false;
            foreach (var go in scene.GetRootGameObjects())
                if (RoomFraming.HasOwnBackdrop(go.transform)) return true;
            return false;
        }

        private static bool UsesWood(string roomName)
        {
            if (string.IsNullOrEmpty(roomName)) return true;
            string n = roomName.ToLowerInvariant();
            return !(n.Contains("bath") || n.Contains("stair") || n.Contains("kitchen") || n.Contains("bus"));
        }

        private static int Index(int x, int y, int w, int h) => x < 0 || y < 0 || x >= w || y >= h ? -1 : y * w + x;
        private static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);
        private static int Mod(int a, int m) => ((a % m) + m) % m;

        /// <summary>Kesit deseni: satır d (0 = üst). Desen yoksa ya da okunamıyorsa düz renkli yedek.</summary>
        private struct SwatchData
        {
            public Color32[] pixels;
            public int width, height;
            public Color32 fallbackTop, fallbackBody;

            public Color32 Get(int x, int d)
            {
                if (pixels == null) return d == 0 ? fallbackTop : fallbackBody;
                int row = height - 1 - Mathf.Clamp(d, 0, height - 1); // doku satırları alttan başlar
                return pixels[row * width + Mathf.Clamp(x, 0, width - 1)];
            }
        }

        private static readonly Dictionary<string, SwatchData> Cache = new Dictionary<string, SwatchData>();

        private static SwatchData Swatch(string path, bool wood, bool left)
        {
            if (Cache.TryGetValue(path, out var s)) return s;
            s = new SwatchData
            {
                width = 64,
                height = Depth,
                fallbackTop = wood ? (left ? new Color32(0xB4, 0x7C, 0x4A, 255) : new Color32(0x8A, 0x5A, 0x33, 255))
                                   : (left ? new Color32(0xB4, 0xB4, 0xC2, 255) : new Color32(0x82, 0x82, 0x9A, 255)),
                fallbackBody = wood ? (left ? new Color32(0x69, 0x43, 0x2A, 255) : new Color32(0x4A, 0x2E, 0x1A, 255))
                                    : (left ? new Color32(0x6C, 0x6C, 0x82, 255) : new Color32(0x4C, 0x4C, 0x64, 255)),
            };
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null && tex.isReadable)
            {
                s.pixels = tex.GetPixels32();
                s.width = tex.width;
                s.height = tex.height;
            }
            else Debug.LogWarning($"[DioramaBase] '{path}' bulunamadı ya da okunamıyor; düz renk kullanılıyor.");
            Cache[path] = s;
            return s;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => Cache.Clear();
    }
}
