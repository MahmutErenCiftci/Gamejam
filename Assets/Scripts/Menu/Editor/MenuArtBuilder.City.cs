using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AfetUyanisi.Menu.EditorTools
{
    // Ana menünün arka planı: şafakta 2B izometrik bir şehir ve odanın altında uzanan apartman cephesi. Hepsi kodla,
    // piksel piksel çizilir (z-buffer'lı küçük bir izometrik çizici): gökyüzü ve doğan güneş, ufukta cami ve
    // minareli uzak siluet, sokaklar, kaldırımlar, sokak lambaları, ağaçlar, park, arabalar; balkonlu pastel
    // apartmanlar ve kiremit çatılı alçak evler, çatılarda su deposu, güneş paneli, anten. Uzaklaştıkça şehir şafak
    // pusuna karışır.
    //
    // Rüya seçimindeki afet önizlemeleri için de buradan üretilir:
    //  - Deprem: yıkılacak binaların tek başına çizimi + şehirde görünen siluetleri (maske) ve yıkılmış şehrin farkı
    //    (enkaz yığınları, kırık duvarlar). Sahnede binalar silueti içinde aşağı çöker, altından enkaz görünür.
    //  - Sel: yükselen su seviyeleri ve dalga kareleri (zemin, arabalar ve zemin katlar su altında kalır).
    //
    // İzometrik eşleme (oda karolarıyla aynı 2:1): zemin noktası (gx, gy), yükseklik h →
    //   ekran x = OX + gx - gy,  ekran y = OY + (gx + gy) / 2 + h   (piksel, sol alttan)
    // gx sağ-yukarı, gy sol-yukarı uzaklaşır; aynı piksele düşen noktalardan gx + gy - h'si küçük olan öndedir.
    public static partial class MenuArtBuilder
    {
        public const int WorldWidth = 960, WorldHeight = 540;
        // Apartman cephesi, oda zemininin ön uç noktasına (pivot) göre: sol kenar 6 karo (192 px), sağ kenar 7 karo
        // (224 px) boyunca, uçtan 300 px aşağı (ekranın altına kadar).
        public const int ApartmentLeft = 192, ApartmentRight = 224, ApartmentDepth = 300;
        public const int CityLightLayers = 4;
        // Sel: 1, 3, 5 … 19 piksel yükselen 10 seviye (ilk seviyeler birikinti), sonra 20 pikselde dalga kareleri.
        public const int FloodRiseFrames = 10, FloodWaveFrames = 3;
        public static float FloodLevel(int frame) => 1f + frame * 2f;
        public const float FloodWaveLevel = 20f;
        public const string CollapseData = OutDir + "Fx/Collapse/collapse.txt";
        public const string StreetData = OutDir + "Fx/Street/street.txt";
        public const int CarColorCount = 5, PersonColorCount = 4;
        const int CitySeed = 11, MaxCollapses = 12;

        static IEnumerable<string> BuildCity()
        {
            var intact = new CityPainter(WorldWidth, WorldHeight, CitySeed);
            intact.Paint();
            List<int> chosen = intact.ChooseCollapses(MaxCollapses);
            var ruined = new CityPainter(WorldWidth, WorldHeight, CitySeed, ruined: new HashSet<int>(chosen));
            ruined.Paint();

            yield return Save(intact.ToTexture(), "Fx/City");
            for (int i = 0; i < CityLightLayers; i++)
                yield return Save(intact.LightsLayer(i, CityLightLayers, chosen), "Fx/CityLights" + i);
            var chosenSet = new HashSet<int>(chosen);
            yield return Save(intact.Difference(ruined, chosenSet, true), "Fx/CityRuins");
            yield return Save(intact.Difference(ruined, chosenSet, false), "Fx/CityCracks");

            // Yıkılan binalar: tek başına çizim orta katlardan ikiye bölünür (üst parça devrilir, alt parça çöker) +
            // şehirdeki görünür silueti (maske); hepsi aynı kutuda, pivot sol alt.
            Directory.CreateDirectory(Path.GetDirectoryName(CollapseData));
            // Önceki kurulumlardan kalan (eski biçim ya da artan) dosyalar silinir; kalanların GUID'i korunur.
            for (int k = 0; k < 64; k++)
            {
                var stale = new List<string> { "Collapse" + k };
                if (k >= chosen.Count) stale.AddRange(new[] { "CollapseMask" + k, "CollapseTop" + k, "CollapseBottom" + k });
                foreach (string name in stale)
                {
                    string path = OutDir + "Fx/Collapse/" + name + ".png";
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                }
            }
            var data = new StringBuilder();
            for (int k = 0; k < chosen.Count; k++)
            {
                int id = chosen[k];
                var solo = new CityPainter(WorldWidth, WorldHeight, CitySeed, solo: id);
                solo.Paint();
                RectInt box = solo.DrawnBounds();
                float breakH = solo.BreakHeight(id);
                int dir = Hash(id, 72) < 0.5f ? -1 : 1;
                Vector2 hinge = solo.Hinge(id, breakH, dir);
                foreach (string name in new[] { "CollapseTop" + k, "CollapseBottom" + k, "CollapseMask" + k })
                    customPivots[name] = Vector2.zero;
                yield return Save(solo.SplitCrop(box, breakH, true), "Fx/Collapse/CollapseTop" + k);
                yield return Save(solo.SplitCrop(box, breakH, false), "Fx/Collapse/CollapseBottom" + k);
                yield return Save(intact.OwnerMask(id, box), "Fx/Collapse/CollapseMask" + k);
                Vector2 b = intact.BaseOf(id);
                data.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11}",
                    k, box.x, box.y, box.width, box.height, b.x, b.y, hinge.x, hinge.y, dir, breakH, solo.HeightOf(id) - breakH));
            }
            File.WriteAllText(CollapseData, data.ToString());

            for (int i = 0; i < FloodRiseFrames; i++)
                yield return Save(intact.FloodLayer(FloodLevel(i), 0), "Fx/Flood" + i);
            for (int i = 0; i < FloodWaveFrames; i++)
                yield return Save(intact.FloodLayer(FloodWaveLevel, i), "Fx/FloodWave" + i);

            // Sokak hayatı: görünür şerit/kaldırım yolları, araba ve insan sprite'ları.
            Directory.CreateDirectory(Path.GetDirectoryName(StreetData));
            var street = new StringBuilder();
            foreach (var p in intact.FindPaths())
                street.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5}",
                    p.car ? "car" : "person", p.axis, p.a.x, p.a.y, p.b.x, p.b.y));
            File.WriteAllText(StreetData, street.ToString());
            Color32[] carColors = { C(204, 64, 56), C(236, 236, 240), C(64, 98, 170), C(240, 200, 72), C(70, 150, 120) };
            for (int c = 0; c < CarColorCount; c++)
                for (int axis = 0; axis < 2; axis++)
                {
                    var painter = new CityPainter(WorldWidth, WorldHeight, CitySeed, solo: -2, fog: false);
                    string name = "Car" + (axis == 0 ? "X" : "Y") + c;
                    var tex = painter.CarSprite(axis, carColors[c], out Vector2 pivot);
                    customPivots[name] = pivot;
                    yield return Save(tex, "Fx/Street/" + name);
                }
            for (int c = 0; c < PersonColorCount; c++)
                for (int f = 0; f < 2; f++)
                {
                    customPivots["Person" + c + "_" + f] = new Vector2(0.5f, 0f);
                    yield return Save(Person(c, f), "Fx/Street/Person" + c + "_" + f);
                }

            yield return Save(Apartment(), "Fx/Apartment");
            yield return Save(ShadeLeft(), "Fx/ShadeLeft");
            yield return Save(Dust(), "Fx/Dust");
            yield return Save(Bird(false), "Fx/Bird0");
            yield return Save(Bird(true), "Fx/Bird1");
        }

        // Arayüzün arkasını karartan soldan sağa sönen şerit (yazılar şehrin üstünde okunsun).
        static Texture2D ShadeLeft()
        {
            const int w = 32;
            var t = New(w, 1);
            for (int x = 0; x < w; x++)
            {
                float k = 1f - x / (float)(w - 1);
                Px(t, x, 0, new Color32(10, 10, 28, (byte)(170 * k * k)));
            }
            return Done(t);
        }

        // Yıkılan binanın toz bulutu parçacığı: basamaklı, kenarı saydamlaşan yuvarlak.
        static Texture2D Dust()
        {
            const int s = 8;
            var t = New(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f, d = Mathf.Sqrt(dx * dx + dy * dy) / (s / 2f);
                    if (d >= 1f) continue;
                    Px(t, x, y, new Color32(255, 255, 255, (byte)(d < 0.5f ? 255 : d < 0.8f ? 170 : 80)));
                }
            return Done(t);
        }

        // Sokaktaki küçük insan (3x7): saç, yüz, gömlek, pantolon; iki adım karesi (bacaklar açık / kapalı).
        static Texture2D Person(int variant, int frame)
        {
            Color32[] shirts = { C(220, 84, 72), C(72, 128, 200), C(240, 196, 80), C(96, 170, 110) };
            Color32[] hairs = { C(46, 32, 34), C(90, 60, 40), C(30, 26, 30), C(160, 110, 60) };
            string[] rows = frame == 0
                ? new[] { ".H.", ".S.", "CCC", "CCC", ".P.", "P.P", "P.P" }
                : new[] { ".H.", ".S.", "CCC", "CCC", ".P.", ".P.", ".P." };
            var t = New(3, 7);
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 3; x++)
                {
                    char k = rows[y][x];
                    if (k == '.') continue;
                    Color32 c = k == 'H' ? hairs[variant] : k == 'S' ? C(236, 188, 152) : k == 'C' ? shirts[variant] : C(58, 58, 92);
                    if (k == 'C' && x == 2) c = Shade(c, 0.7f);
                    Px(t, x, y, c);
                }
            return Done(t);
        }

        // Uzakta uçan kuş: kanat yukarı / aşağı iki kare.
        static Texture2D Bird(bool down)
        {
            string[] rows = down ? new[] { ".....", ".###.", "#.#.#" } : new[] { "#...#", ".#.#.", "..#.." };
            var t = New(5, 3);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 5; x++)
                    if (rows[y][x] == '#') Px(t, x, y, new Color32(46, 34, 66, 255));
            return Done(t);
        }

        // ---------------------------------------------------------------- renk
        static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        // Gölge: kararırken mora kayar (şafakta gölgeler soğuk).
        static Color32 Shade(Color32 c, float k)
        {
            float s = 1f - k;
            return C(Clamp(c.r * (1f - s * 0.9f) + 26f * s), Clamp(c.g * (1f - s * 0.95f) + 18f * s), Clamp(c.b * (1f - s * 0.6f) + 40f * s));
        }

        static Color32 Mix(Color32 a, Color32 b, float k) =>
            C(Clamp(a.r + (b.r - a.r) * k), Clamp(a.g + (b.g - a.g) * k), Clamp(a.b + (b.b - a.b) * k));

        static int Clamp(float v) => Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);

        static float Hash(int a, int b = 0, int c = 0, int d = 0)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + c * 1274126177 + d * 1911520717) ^ 0x9E3779B9u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // ---------------------------------------------------------------- apartman
        // Odanın bulunduğu en üst katın altındaki katlar (oda ölçeğinde: kat 96 px duvar + 8 px döşeme). Sol cephe
        // hafif aydınlık, sağ cephe gölgede. Sol cephede çiçek saksılı iki pencere, çanak anten ve yağmur borusu;
        // sağ cephede pencere, klima ve saksılı, çamaşır ipli balkon. Pencerelerde perde, yanan odalarda sıcak ışık.
        static Texture2D Apartment()
        {
            int L = ApartmentLeft, R = ApartmentRight, D = ApartmentDepth;
            int w = L + R, h = D + R / 2 + 2;
            var t = New(w, h);
            var px = new Color32[w * h];
            Color32 plaster = C(236, 196, 162);
            Color32 slab = C(214, 206, 206);
            const int Unit = 104, Wall = 96;
            for (int ty = 0; ty < h; ty++)
                for (int tx = 0; tx < w; tx++)
                {
                    float X = tx - L + 0.5f, Y = ty - D + 0.5f;
                    bool left = X < 0f;
                    float a = left ? -X : X;              // ön köşeden cephe boyunca
                    float hh = Y - a / 2f;                // zemin seviyesine göre (0 = oda zemini)
                    int len = left ? L : R;
                    if (hh >= 1f || a >= len) continue;
                    int v = Mathf.FloorToInt(-hh);       // üstten aşağı
                    float k = left ? 0.86f : 0.6f;
                    int ai = Mathf.FloorToInt(a);
                    Color32 c;
                    if (v < 10)
                    {
                        // Odanın döşemesi: profilli beton kuşak (üstte parlak çizgi, ortada oluk, altta gölge).
                        float kk = v == 0 ? k + 0.14f : v == 3 ? k - 0.12f : v == 9 ? k - 0.3f : k;
                        c = Shade(slab, kk);
                    }
                    else
                    {
                        int vv = v - 10, floor = vv / Unit, local = vv % Unit, up = Wall - local;
                        if (local >= Wall)
                        {
                            int sv = local - Wall;
                            c = Shade(slab, sv == 0 ? k + 0.1f : sv == 7 ? k - 0.28f : k - 0.04f);
                        }
                        else
                        {
                            c = Shade(plaster, k);
                            if (Hash(tx, ty, 3) < 0.06f) c = Shade(plaster, k - 0.05f); // sıva dokusu
                            if (local < 3) c = Shade(c, 0.86f);                          // döşemenin altındaki gölge
                            c = ApartmentFacade(c, left, ai, up, floor, k, len);
                        }
                    }
                    if (ai == 0) c = Mix(c, C(255, 232, 200), left ? 0.28f : 0.12f);      // köşe
                    if (ai >= len - 1) c = Shade(c, 0.72f);
                    c = Shade(c, 1f - Mathf.Min(0.3f, Mathf.Floor(v / 36f) * 0.035f));
                    px[ty * w + tx] = c;
                }
            t.SetPixels32(px);
            customPivots["Apartment"] = new Vector2(L / (float)w, D / (float)h);
            return Done(t);
        }

        static Color32 ApartmentFacade(Color32 wall, bool left, int a, int up, int floor, float k, int len)
        {
            Color32 frame = C(240, 236, 230);
            Color32 r;
            // Yağmur borusu (cephenin uç kenarında)
            if (a >= len - 7 && a < len - 4)
                return Shade(C(176, 170, 182), a == len - 7 ? k + 0.15f : k);
            if (left)
            {
                if ((r = ApWindow(a, up, 30, 48, 26, 80, floor, 1, k, true, true)).a > 0) return r;
                if ((r = ApWindow(a, up, 112, 48, 26, 80, floor, 2, k, true, true)).a > 0) return r;
                // Çanak anten
                float dx = a - 176, dy = (up - 64) * 1.2f;
                if (dx * dx + dy * dy < 49f) return Shade(dx * dx + dy * dy < 6f ? C(150, 150, 166) : C(232, 232, 238), dx < 0 ? k + 0.1f : k - 0.05f);
                if (a == 176 && up >= 54 && up < 58) return Shade(C(90, 88, 104), k);
                return wall;
            }
            if ((r = ApWindow(a, up, 24, 44, 26, 80, floor, 3, k, true, false)).a > 0) return r;
            // Klima
            if (a >= 80 && a < 102 && up >= 30 && up < 48)
            {
                bool grille = (a - 80) % 3 == 0 && up > 32 && up < 46;
                return Shade(grille ? C(150, 150, 162) : C(220, 220, 228), up == 47 ? k + 0.15f : k);
            }
            // Balkon: saksılar ve çamaşır ipi, korkuluğun arkasında; korkuluk en önde.
            if (a >= 110 && a < 208 && up < 38)
            {
                if (up < 5) return Shade(C(206, 198, 200), up == 4 ? k + 0.18f : k - 0.05f);   // balkon döşemesi
                if (up >= 33) return Shade(C(156, 150, 174), up == 37 ? k + 0.2f : k + 0.05f);  // küpeşte
                if ((a - 110) % 6 < 2) return Shade(C(70, 66, 92), k + 0.1f);                  // korkuluk
                Color32 pot;
                if ((pot = Pot(a - 120, up)).a > 0 || (pot = Pot(a - 196, up)).a > 0) return Shade(pot, k + 0.12f);
            }
            if (a >= 110 && a < 208 && up < 52)
            {
                Color32 pot;
                if ((pot = Pot(a - 120, up)).a > 0 || (pot = Pot(a - 196, up)).a > 0) return Shade(pot, k + 0.12f);
            }
            // Çamaşır ipi ve çamaşırlar (balkon kapısının önünde)
            if (a >= 132 && a < 194)
            {
                if (up == 72) return Shade(C(220, 220, 228), k + 0.1f);
                Color32[] cloth = { C(220, 90, 80), C(90, 140, 210), C(240, 230, 210), C(240, 190, 80) };
                int slot = (a - 136) / 13, offset = (a - 136) % 13;
                if (a >= 136 && slot < 4 && offset < 9 && up < 72 && up >= 72 - (slot % 2 == 0 ? 16 : 11))
                    return Shade(cloth[slot], offset == 0 ? k + 0.05f : k + 0.15f);
            }
            if ((r = ApWindow(a, up, 130, 60, 4, 84, floor, 4, k, false, false)).a > 0) return r; // balkon kapısı
            return wall;
        }

        // Balkon saksısı: kiremit rengi saksı, üstünde yuvarlak yeşillik ve birkaç çiçek; merkez a = 0.
        static Color32 Pot(int a, int up)
        {
            if (a < -5 || a > 5) return default;
            if (up >= 5 && up < 12 && Mathf.Abs(a) <= 3 + (up - 5) / 4) return up == 11 ? C(206, 120, 90) : C(176, 92, 70);
            float dx = a, dy = up - 19;
            if (up >= 11 && dx * dx + dy * dy * 1.3f < 40f)
            {
                if (Hash(a + 50, up, 21) < 0.14f) return Hash(a, up, 22) < 0.5f ? C(240, 90, 110) : C(255, 220, 90);
                return dx - dy * 0.5f < -2f ? C(120, 170, 96) : C(72, 120, 78);
            }
            return default;
        }

        // Oda ölçeğinde pencere: çerçeve, ortada kayıt, perdeler, yanan odada sıcak ışık; flowers: altında çiçek kutusu.
        static Color32 ApWindow(int a, int up, int a0, int width, int bottom, int top, int floor, int id, float k, bool cross, bool flowers)
        {
            if (a < a0 - 3 || a >= a0 + width + 3 || up < bottom - 12 || up >= top + 3) return default;
            bool lit = Hash(floor, id, 9) < 0.45f;
            if (flowers && up < bottom - 1)
            {
                // Çiçek kutusu (denizliğin altında asılı) ve üstüne taşan çiçekler
                if (up >= bottom - 11 && up < bottom - 5 && a >= a0 - 1 && a < a0 + width + 1)
                    return Shade(up == bottom - 6 ? C(200, 110, 84) : C(168, 86, 66), k + 0.1f);
                if (up >= bottom - 5 && a >= a0 && a < a0 + width)
                {
                    float f = Hash(a, up, floor + id * 7);
                    if (f < 0.3f) return Shade(f < 0.12f ? C(236, 86, 110) : f < 0.2f ? C(255, 214, 90) : C(250, 250, 250), k + 0.2f);
                    return Shade(Hash(a, up, 5) < 0.5f ? C(84, 136, 80) : C(60, 104, 70), k + 0.15f);
                }
                return default;
            }
            if (up < bottom - 4) return default;
            if (up < bottom - 1) return Shade(C(246, 242, 236), k + 0.1f);                       // denizlik
            if (up < bottom) return default;
            if (a < a0 || a >= a0 + width || up >= top) return Shade(C(240, 236, 230), k);       // çerçeve
            if (cross && (a == a0 + width / 2 || a == a0 + width / 2 - 1)) return Shade(C(240, 236, 230), k);
            int rel = a - a0, fromRight = a0 + width - 1 - a;
            int curtainW = lit ? 7 : 11;
            if (rel < curtainW || fromRight < curtainW)
            {
                bool fold = (Mathf.Min(rel, fromRight) % 3) == 0;
                Color32 cloth = lit ? C(226, 132, 96) : C(150, 128, 170);
                return Shade(cloth, (lit ? 1f : k + 0.1f) - (fold ? 0.12f : 0f));
            }
            if (lit)
            {
                bool core = Mathf.Abs(a - (a0 + width / 2)) < width / 5 && up > bottom + (top - bottom) / 3;
                return core ? C(255, 232, 166) : C(255, 204, 124);
            }
            int s = (rel + (up - bottom)) % 22;
            return s < 3 ? Shade(C(96, 90, 136), k + 0.15f) : Shade(C(44, 46, 84), k + 0.12f);
        }

        // ---------------------------------------------------------------- şehir
        class CityPainter
        {
            const float OX = 480f, OY = -260f;
            const float Horizon = 445f;                    // ekranda zeminin bittiği çizgi
            const float SkylineCap = 24f;                  // binalar ufkun en fazla bu kadar üstüne çıkar (gökyüzü görünsün)
            const float FarDepth = 2f * (Horizon - OY);    // ufuktaki zemin derinliği (gx + gy)
            const float HazeStart = 760f;
            const int Pitch = 120, StreetHalf = 12, Walk = 6;

            static readonly Color32[] Plasters =
            {
                C(236, 214, 176), C(232, 182, 150), C(196, 116, 92), C(214, 186, 138), C(164, 184, 206),
                C(216, 212, 222), C(226, 168, 170), C(166, 198, 172), C(184, 170, 196), C(238, 226, 200),
            };
            static readonly Color32[] SkyBands =
            {
                C(236, 150, 128), C(214, 126, 126), C(186, 104, 124), C(150, 84, 120), C(116, 66, 112),
                C(84, 52, 102), C(58, 40, 90), C(38, 30, 74), C(26, 24, 60),
            };
            static readonly Color32[] CarColors = { C(196, 60, 54), C(232, 232, 236), C(60, 92, 160), C(236, 196, 70), C(70, 70, 80), C(90, 150, 120) };

            readonly int W, H, seed, solo;
            readonly HashSet<int> ruined;
            readonly Color32[] color;
            readonly float[] depth, fog;
            readonly int[] window, owner;
            readonly List<Color32> windowLitColor = new List<Color32>();
            readonly List<bool> windowLit = new List<bool>();
            readonly List<int> windowBuilding = new List<int>();
            readonly Dictionary<long, int> windowIds = new Dictionary<long, int>();
            readonly Dictionary<int, Rect> footprints = new Dictionary<int, Rect>();
            int currentOwner = -1;

            bool Solo => solo >= 0;
            bool Damaged => ruined.Count > 0;   // deprem sonrası şehir: çatlaklar, kırık camlar, elektrik yok

            readonly float[] heightAt;                      // görünen yüzeyin zeminden yüksekliği
            readonly Dictionary<int, int> floorsOf = new Dictionary<int, int>();
            readonly bool fogEnabled;

            public CityPainter(int w, int h, int seed, HashSet<int> ruined = null, int solo = -1, bool fog = true)
            {
                W = w; H = h; this.seed = seed; this.solo = solo; fogEnabled = fog;
                heightAt = new float[w * h];
                for (int i = 0; i < heightAt.Length; i++) heightAt[i] = -50f;
                this.ruined = ruined ?? new HashSet<int>();
                color = new Color32[w * h];
                depth = new float[w * h];
                this.fog = new float[w * h];
                window = new int[w * h];
                owner = new int[w * h];
                for (int i = 0; i < depth.Length; i++) { depth[i] = float.MaxValue; window[i] = -1; owner[i] = -1; }
            }

            public void Paint()
            {
                if (!Solo)
                {
                    PaintSky();
                    PaintSkyline();
                    PaintGround();
                }
                for (int bi = -2; bi <= 10; bi++)
                    for (int bj = -2; bj <= 10; bj++)
                        PaintBlock(bi, bj);
            }

            public Texture2D ToTexture() => Tex(color, W, H);

            static Texture2D Tex(Color32[] px, int w, int h)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.SetPixels32(px);
                t.Apply();
                return t;
            }

            // Sönük pencerelerin bir kısmı bu katmanlarda yanar; sahnede katmanlar sırayla açılıp kapanır. Yıkılacak
            // binaların pencereleri katılmaz (çöken binanın yerinde ışık asılı kalmasın).
            public Texture2D LightsLayer(int layer, int layers, List<int> exclude)
            {
                var visible = new int[windowLit.Count];
                foreach (int id in window) if (id >= 0) visible[id]++;
                var px = new Color32[W * H];
                for (int i = 0; i < px.Length; i++)
                {
                    int id = window[i];
                    if (id < 0 || windowLit[id] || visible[id] < 6 || exclude.Contains(windowBuilding[id])) continue;
                    if (Hash(id, seed, 77) >= 0.16f || (int)(Hash(id, seed, 78) * layers) != layer) continue;
                    px[i] = Haze(windowLitColor[id], fog[i] * 0.6f, i / W);
                }
                return Tex(px, W, H);
            }

            // Bu çizimle diğeri (hasarlı şehir) arasında değişen pikseller. collapsed = true: yalnızca yıkılan binaların
            // bu çizimdeki silueti içi (çöken binanın altından görünen enkaz); false: geri kalanı (çatlaklar, kırık
            // camlar, sönen ışıklar, sokağa saçılan parçalar).
            public Texture2D Difference(CityPainter other, HashSet<int> collapsedIds, bool collapsed)
            {
                var px = new Color32[W * H];
                for (int i = 0; i < px.Length; i++)
                {
                    if (collapsedIds.Contains(owner[i]) != collapsed) continue;
                    Color32 a = color[i], b = other.color[i];
                    if (a.r != b.r || a.g != b.g || a.b != b.b) px[i] = b;
                }
                return Tex(px, W, H);
            }

            // Yıkılacak binalar: şehirde yeterince görünen, sisin çok koyulaşmadığı, rastgele seçilmiş binalar.
            public List<int> ChooseCollapses(int max)
            {
                var count = new Dictionary<int, int>();
                foreach (int o in owner) if (o >= 0) count[o] = count.TryGetValue(o, out int n) ? n + 1 : 1;
                var candidates = new List<(int id, int pixels)>();
                foreach (var kv in count)
                {
                    Rect f = footprints[kv.Key];
                    float d = f.center.x + f.center.y, sx = OX + f.center.x - f.center.y;
                    float sy = OY + d / 2f;
                    if (kv.Value < 900 || d > 1300f) continue;
                    if (sy < 45f) continue;                       // ekranın altından taşanlar
                    if (sx > 380f && sx < 910f) continue;        // rüya bulutlarının ve apartmanın arkasında kalanlar
                    candidates.Add((kv.Key, kv.Value));
                }
                candidates.Sort((a, b) => b.pixels.CompareTo(a.pixels));
                var result = new List<int>();
                for (int i = 0; i < candidates.Count && result.Count < max; i++) result.Add(candidates[i].id);
                return result;
            }

            public RectInt DrawnBounds()
            {
                int x0 = W, y0 = H, x1 = -1, y1 = -1;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (color[y * W + x].a > 0) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                return x1 < 0 ? new RectInt(0, 0, 1, 1) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            }

            public Texture2D Crop(RectInt r)
            {
                var px = new Color32[r.width * r.height];
                for (int y = 0; y < r.height; y++)
                    for (int x = 0; x < r.width; x++)
                        px[y * r.width + x] = color[(r.y + y) * W + r.x + x];
                return Tex(px, r.width, r.height);
            }

            public Texture2D OwnerMask(int id, RectInt r)
            {
                var px = new Color32[r.width * r.height];
                var white = new Color32(255, 255, 255, 255);
                for (int y = 0; y < r.height; y++)
                    for (int x = 0; x < r.width; x++)
                        if (owner[(r.y + y) * W + r.x + x] == id) px[y * r.width + x] = white;
                return Tex(px, r.width, r.height);
            }

            // Binanın tabanının ortası (ekran pikseli): toz bulutu buradan kalkar.
            public Vector2 BaseOf(int id)
            {
                Rect f = footprints[id];
                return new Vector2(OX + f.center.x - f.center.y, OY + (f.center.x + f.center.y) / 2f);
            }

            // Sel: su yüzeyi zeminden level piksel yukarıda. Yüzeyin önündeki her şey (arabalar, kaldırımlar, binaların
            // alt kısımları) suyun altında kalır; daha yüksek olanlar sudan çıkar. Su alçakken önce çukurlarda
            // birikinti olarak belirir (gürültü maskesi), yükseldikçe her yeri kaplar. Sığ suyun altındaki arabalar,
            // ağaç dipleri, bordürler bulanık görünür; derinleştikçe kaybolur. Su çizgisinde köpük, yüzeyde dalga.
            public Texture2D FloodLayer(float level, int phase)
            {
                var px = new Color32[W * H];
                var water = new bool[W * H];
                const float PuddleUntil = 7f;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float d = 2f * (y + 0.5f - OY - level);
                        if (d > FarDepth) continue;
                        int i = y * W + x;
                        if (depth[i] <= d - level) continue;
                        if (level < PuddleUntil)
                        {
                            float X = x + 0.5f - OX, gx = (X + d) / 2f, gy = (d - X) / 2f;
                            if (ValueNoise(gx / 16f, gy / 16f) > 0.08f + 0.92f * level / PuddleUntil) continue;
                        }
                        water[i] = true;
                    }
                // Doğal su rengi: mavi-yeşil, açık dalga çizgileri, beyaz köpük.
                Color32 surface = C(64, 112, 150), light = C(150, 196, 224), foam = C(228, 240, 248), dark = C(44, 84, 122);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * W + x;
                        if (!water[i]) continue;
                        float d = 2f * (y + 0.5f - OY - level);
                        Color32 c = surface;
                        float X = x + 0.5f - OX, gx = (X + d) / 2f, gy = (d - X) / 2f;
                        // Dalga çizgileri: yatay kısa parlak çizgiler, her karede kayar.
                        int row = y + phase * 2;
                        if (row % 5 == 0 && Hash((x + phase * 3) / 6, row, 31) < 0.35f) c = light;
                        else if (row % 5 == 2 && Hash((x - phase * 2) / 7, row, 32) < 0.2f) c = dark;
                        // Su çizgisi köpüğü: hemen üstünde suya batmamış bir yüzey (bina, araba) varsa.
                        bool edge = level >= 4f && y + 1 < H && !water[i + W] && depth[i + W] < 1e6f && heightAt[i + W] > 0.5f;
                        if (edge) c = Hash(x, phase, 33) < 0.7f ? foam : light;
                        float f = Mathf.Clamp01((d - HazeStart) / (FarDepth - HazeStart));
                        c = Haze(c, Mathf.Floor(f * 10f) / 10f * 0.85f, y);
                        // Sığ su yarı saydam: altındaki yüzey (yol, araba, ağaç) suyun içinden seçilir.
                        float below = level - heightAt[i];
                        float k = edge ? 1f : Mathf.Clamp01(0.42f + below / 16f);
                        k = Mathf.Ceil(k * 6f) / 6f;                                 // basamaklı, piksel sanata uygun
                        px[i] = Mix(Mix(color[i], C(40, 76, 108), 0.35f), c, k);
                    }
                return Tex(px, W, H);
            }

            // Yumuşak değer gürültüsü (0-1): birikintilerin şekli.
            static float ValueNoise(float x, float y)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
                float a = Hash(x0, y0, 81), b = Hash(x0 + 1, y0, 81), c = Hash(x0, y0 + 1, 81), e = Hash(x0 + 1, y0 + 1, 81);
                return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, e, fx), fy);
            }

            // ------------------------------------------------ gökyüzü, güneş, uzak siluet
            Color32 HazeColor(int y)
            {
                float k = Mathf.Clamp01((y - 140f) / (Horizon - 140f));
                return Mix(C(124, 86, 132), SkyBands[0], k);
            }

            Color32 Haze(Color32 c, float f, int y) => f <= 0f ? c : Mix(c, HazeColor(y), f);

            void PaintSky()
            {
                const float sunX = 384f, sunY = Horizon + 22f, sunR = 18f;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        Color32 c;
                        if (y < Horizon) c = HazeColor(y);
                        else
                        {
                            int band = Mathf.Min(SkyBands.Length - 1, (int)((y - Horizon) / ((H - Horizon) / SkyBands.Length)));
                            c = SkyBands[band];
                        }
                        float dx = x + 0.5f - sunX, dy = (y + 0.5f - sunY) * 1.15f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < sunR) c = d < sunR - 3f ? C(255, 240, 196) : C(255, 214, 150);
                        else if (d < sunR + 70f)
                        {
                            float g = d < sunR + 10f ? 0.55f : d < sunR + 26f ? 0.34f : d < sunR + 46f ? 0.18f : 0.08f;
                            c = Mix(c, C(255, 196, 140), g);
                        }
                        // Güneşin önünden geçen ince bulut şeritleri
                        if (y > Horizon + 30 && y < Horizon + 60 && Mathf.Repeat(y * 7f + Mathf.Floor(x / 40f) * 3f, 11f) < 1f && Hash(x / 23, y, 41) < 0.6f)
                            c = Mix(c, C(255, 190, 160), 0.35f);
                        color[y * W + x] = c;
                    }
            }

            // Ufuktaki düz siluet: iki kat (arkadaki açık, öndeki koyu), cami kubbeleri, minareler, birkaç ışık.
            void PaintSkyline()
            {
                var rng = new System.Random(seed);
                for (int layer = 0; layer < 2; layer++)
                {
                    Color32 tone = layer == 0 ? C(212, 128, 128) : C(176, 104, 124);
                    int x = -rng.Next(10);
                    while (x < W)
                    {
                        int bw = 6 + rng.Next(layer == 0 ? 18 : 14);
                        int bh = (layer == 0 ? 6 : 2) + rng.Next(layer == 0 ? 20 : 14);
                        for (int xx = x; xx < x + bw; xx++)
                            for (int y = (int)Horizon - 4; y < Horizon + bh; y++)
                                Sil(xx, y, tone, layer == 1 && y < Horizon + bh - 2 && Hash(xx, y, layer) < 0.03f);
                        x += bw + (rng.Next(4) == 0 ? 3 : 0);
                    }
                }
                foreach (int mx in new[] { 236, 640, 872 })
                    Mosque2D(mx, (int)Horizon + 6, C(160, 96, 120));
            }

            void Mosque2D(int cx, int baseY, Color32 tone)
            {
                for (int y = baseY - 10; y < baseY + 12; y++)
                    for (int x = cx - 14; x <= cx + 14; x++)
                    {
                        float dx = x - cx, dy = y - baseY;
                        bool body = y < baseY;
                        bool dome = dy >= 0 && dx * dx + dy * dy * 1.6f < 110f;
                        if (body || dome) Sil(x, y, tone, false);
                    }
                foreach (int side in new[] { -1, 1 })
                {
                    int mx = cx + side * 17;
                    for (int y = baseY - 10; y < baseY + 30; y++)
                    {
                        Sil(mx, y, tone, false);
                        Sil(mx + 1, y, tone, false);
                        if (y == baseY + 20) { Sil(mx - 1, y, tone, false); Sil(mx + 2, y, tone, false); }
                    }
                    Sil(mx, baseY + 30, tone, false);
                    Sil(mx, baseY + 31, tone, false);
                }
            }

            void Sil(int x, int y, Color32 tone, bool light)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x;
                color[i] = light ? C(255, 214, 150) : tone;
                depth[i] = 1e7f;
            }

            // ------------------------------------------------ zemin
            void PaintGround()
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float X = x + 0.5f - OX, d = 2f * (y + 0.5f - OY);
                        if (d > FarDepth) continue;
                        float gx = (X + d) / 2f, gy = (d - X) / 2f;
                        Plot(x, y, d, GroundColor(gx, gy), d, -1);
                    }
            }

            Color32 GroundColor(float gx, float gy)
            {
                int bi = Mathf.FloorToInt(gx / Pitch), bj = Mathf.FloorToInt(gy / Pitch);
                float u = gx - bi * Pitch, v = gy - bj * Pitch;
                bool streetU = u < StreetHalf || u >= Pitch - StreetHalf;
                bool streetV = v < StreetHalf || v >= Pitch - StreetHalf;
                Color32 c;
                if (streetU || streetV)
                {
                    c = C(50, 46, 70);
                    if (Hash(Mathf.FloorToInt(gx), Mathf.FloorToInt(gy), 5) < 0.05f) c = C(58, 54, 78);
                    // Orta şerit kesik çizgileri (kavşakta yok).
                    bool centerU = (u < 1f || u >= Pitch - 1f) && !streetV && Mathf.Repeat(gy, 16f) < 8f;
                    bool centerV = (v < 1f || v >= Pitch - 1f) && !streetU && Mathf.Repeat(gx, 16f) < 8f;
                    if (centerU || centerV) c = C(170, 150, 120);
                    // Yaya geçidi: kavşağa yakın beyaz çizgiler.
                    bool crossU = streetU && !streetV && (v < StreetHalf + 5 || v >= Pitch - StreetHalf - 5) && Mathf.Repeat(u, 4f) < 2f;
                    bool crossV = streetV && !streetU && (u < StreetHalf + 5 || u >= Pitch - StreetHalf - 5) && Mathf.Repeat(v, 4f) < 2f;
                    if (crossU || crossV) c = C(150, 140, 150);
                }
                else
                {
                    float iu = Mathf.Min(u - StreetHalf, Pitch - StreetHalf - u), iv = Mathf.Min(v - StreetHalf, Pitch - StreetHalf - v);
                    float edge = Mathf.Min(iu, iv);
                    if (edge < 1f) c = C(128, 118, 132);                    // bordür
                    else if (edge < Walk) c = Mathf.Repeat(Mathf.Floor(gx / 3f) + Mathf.Floor(gy / 3f), 2f) < 1f ? C(92, 84, 104) : C(98, 90, 110); // parke taşı
                    else if (BlockKind(bi, bj) == Kind.Park)
                    {
                        c = Hash(Mathf.FloorToInt(gx / 2), Mathf.FloorToInt(gy / 2), 6) < 0.2f ? C(70, 98, 72) : C(58, 84, 64);
                        // Parkın çapraz yürüyüş yolu
                        if (Mathf.Abs(u - v) < 3f || Mathf.Abs(u + v - Pitch) < 3f) c = C(150, 126, 104);
                    }
                    else c = C(66, 60, 78);                                 // avlu
                }
                // Hasarlı şehir: yollarda ve kaldırımlarda yarıklar.
                if (Damaged && (streetU || streetV || BlockKind(bi, bj) != Kind.Park))
                {
                    float n = gy + 5f * Mathf.Sin(gx * 0.23f) + 2f * Hash(Mathf.FloorToInt(gx / 3f), 7);
                    if (Mathf.Repeat(n, 61f) < 0.9f && Hash(Mathf.FloorToInt(gx / 24f), Mathf.FloorToInt(n / 61f), 65) < 0.45f) return C(26, 22, 34);
                    float m = gx + 5f * Mathf.Sin(gy * 0.19f) + 2f * Hash(Mathf.FloorToInt(gy / 3f), 8);
                    if (Mathf.Repeat(m, 67f) < 0.9f && Hash(Mathf.FloorToInt(gy / 24f), Mathf.FloorToInt(m / 67f), 66) < 0.45f) return C(26, 22, 34);
                }
                // Sokak lambalarının zemindeki sıcak ışığı (depremde elektrik yok).
                if (!Damaged)
                    foreach (Vector2 lamp in Lamps(bi, bj))
                {
                    float dx = gx - lamp.x, dy = gy - lamp.y, d2 = dx * dx + dy * dy;
                    if (d2 < 36f) return Mix(c, C(255, 196, 120), 0.5f);
                    if (d2 < 120f) c = Mix(c, C(255, 196, 120), 0.25f);
                }
                return c;
            }

            IEnumerable<Vector2> Lamps(int bi, int bj)
            {
                float x0 = bi * Pitch + StreetHalf + 3f, x1 = (bi + 1) * Pitch - StreetHalf - 3f;
                float y0 = bj * Pitch + StreetHalf + 3f, y1 = (bj + 1) * Pitch - StreetHalf - 3f;
                if (Hash(bi, bj, seed, 1) < 0.7f) yield return new Vector2(x0, y0);
                if (Hash(bi, bj, seed, 2) < 0.5f) yield return new Vector2(x1, y0);
                if (Hash(bi, bj, seed, 3) < 0.5f) yield return new Vector2(x0, y1);
            }

            // ------------------------------------------------ bloklar
            enum Kind { Buildings, Park, Mosque }

            Kind BlockKind(int bi, int bj)
            {
                if (bi == 4 && bj == 5) return Kind.Mosque;
                return Hash(bi, bj, seed, 9) < 0.09f ? Kind.Park : Kind.Buildings;
            }

            void PaintBlock(int bi, int bj)
            {
                float cx = bi * Pitch + Pitch / 2f, cy = bj * Pitch + Pitch / 2f;
                float sx = OX + cx - cy, d = cx + cy;
                // Zemini tamamen ufkun ötesinde kalan bloklar çizilmez (gökyüzü, güneş ve uzak siluet görünsün); kısmen
                // kalanlarda yalnızca tabanı zeminde olan binalar çizilir (Building).
                float frontDepth = bi * Pitch + bj * Pitch + 2 * StreetHalf;
                if (sx < -160 || sx > W + 160 || frontDepth > FarDepth || OY + d / 2f < -260f) return;
                int x0 = bi * Pitch + StreetHalf + Walk, x1 = (bi + 1) * Pitch - StreetHalf - Walk;
                int y0 = bj * Pitch + StreetHalf + Walk, y1 = (bj + 1) * Pitch - StreetHalf - Walk;

                if (!Solo)
                {
                    foreach (Vector2 lamp in Lamps(bi, bj))
                    {
                        if (lamp.x + lamp.y > FarDepth - 2f) continue;
                        Box(lamp.x, lamp.y, lamp.x + 1, lamp.y + 1, 0, 13, C(60, 56, 76), -1);
                        Box(lamp.x - 1, lamp.y - 1, lamp.x + 2, lamp.y + 1, 13, 15, Damaged ? C(90, 86, 100) : C(255, 220, 150), -1, glow: !Damaged);
                    }
                    Cars(bi, bj);
                    // Kaldırım ağaçları
                    for (int k = 0; k < 6; k++)
                        if (Hash(bi, bj, k, 31) < 0.35f)
                        {
                            float along = 20f + k * 14f;
                            if (k % 2 == 0) Tree(x0 + along * 0.8f, y0 - Walk / 2f, 3f + Hash(bi, bj, k, 32) * 2f);
                            else Tree(x0 - Walk / 2f, y0 + along * 0.8f, 3f + Hash(bi, bj, k, 33) * 2f);
                        }
                }

                switch (BlockKind(bi, bj))
                {
                    case Kind.Park:
                        if (Solo) return;
                        for (int k = 0; k < 9; k++)
                            Tree(x0 + 8 + Hash(bi, bj, k, 41) * (x1 - x0 - 16), y0 + 8 + Hash(bi, bj, k, 42) * (y1 - y0 - 16), 5f + Hash(bi, bj, k, 43) * 4f);
                        Box(cx - 3, cy - 3, cx + 3, cy + 3, 0, 3, C(170, 168, 180), -1); // çeşme
                        return;
                    case Kind.Mosque:
                        if (!Solo) Mosque(x0, y0, x1, y1);
                        return;
                }

                // Parseller: 1, 2 ya da 4 bina; aralarında 2 birimlik boşluk.
                int split = (int)(Hash(bi, bj, seed, 10) * 4f);
                if (OY + d / 2f > Horizon - 150f) split = 3; // ufka yakın bloklar küçük parsellere bölünür (geniş düz çatılar olmasın)
                int mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
                var lots = new List<RectInt>();
                if (split == 0) lots.Add(new RectInt(x0, y0, x1 - x0, y1 - y0));
                else if (split == 1) { lots.Add(new RectInt(x0, y0, mx - x0 - 1, y1 - y0)); lots.Add(new RectInt(mx + 1, y0, x1 - mx - 1, y1 - y0)); }
                else if (split == 2) { lots.Add(new RectInt(x0, y0, x1 - x0, my - y0 - 1)); lots.Add(new RectInt(x0, my + 1, x1 - x0, y1 - my - 1)); }
                else
                {
                    lots.Add(new RectInt(x0, y0, mx - x0 - 1, my - y0 - 1)); lots.Add(new RectInt(mx + 1, y0, x1 - mx - 1, my - y0 - 1));
                    lots.Add(new RectInt(x0, my + 1, mx - x0 - 1, y1 - my - 1)); lots.Add(new RectInt(mx + 1, my + 1, x1 - mx - 1, y1 - my - 1));
                }
                for (int i = 0; i < lots.Count; i++)
                    Building(bi * 1000 + bj * 10 + i, lots[i], d);
            }

            void Building(int id, RectInt lot, float blockDepth)
            {
                if (Solo && id != solo) return;
                // Önde (ekranın altında) kalan binalar alçak tutulur; arkadakiler daha yüksek olabilir.
                int maxFloors = blockDepth < 640f ? 5 : blockDepth < 900f ? 8 : 11;
                int floors = 3 + (int)(Hash(id, 1) * (maxFloors - 2));
                if (Hash(id, 2) < 0.07f && blockDepth > 900f) floors += 4;
                // Ufka yakın binalar alçalır: arka köşenin tepesi ufuk + SkylineCap'i geçmez, gökyüzü ve güneş görünür.
                float backBase = OY + (lot.xMax + lot.yMax) / 2f;
                int maxByHorizon = Mathf.FloorToInt((Horizon + SkylineCap - backBase - 17f) / 12f);
                if (maxByHorizon < 2 || lot.xMax + lot.yMax > FarDepth) return;
                floors = Mathf.Min(floors, maxByHorizon);
                bool pitched = floors <= 4 && Hash(id, 13) < 0.45f;
                int height = 14 + floors * 12 + (pitched ? 1 : 3);
                Color32 plaster = Plasters[(int)(Hash(id, 3) * Plasters.Length)];
                var style = new Style
                {
                    id = id, plaster = plaster, height = height, floors = floors, pitched = pitched,
                    winW = Hash(id, 4) < 0.5f ? 3 : 4,
                    period = Hash(id, 5) < 0.5f ? 7 : 8,
                    banded = Hash(id, 6) < 0.5f,
                    balconies = !pitched && Hash(id, 14) < 0.5f,
                    awning = Hash(id, 7) < 0.4f ? new Color32[] { C(196, 60, 60), C(60, 120, 90), C(60, 90, 160), C(220, 150, 60) }[(int)(Hash(id, 8) * 4)] : default,
                    roof = new Color32[] { C(118, 108, 126), C(96, 90, 110), C(140, 128, 132) }[(int)(Hash(id, 12) * 3)],
                    lit = Damaged ? 0f : 0.22f + Hash(id, 11) * 0.22f,   // depremde elektrik kesilir
                };
                float x0 = lot.xMin + 1, y0 = lot.yMin + 1, x1 = lot.xMax - 1, y1 = lot.yMax - 1;
                footprints[id] = new Rect(x0, y0, x1 - x0, y1 - y0);
                floorsOf[id] = floors;
                currentOwner = id;
                if (ruined.Contains(id)) Rubble(x0, y0, x1, y1, style);
                else
                {
                    BuildingBox(x0, y0, x1, y1, style);
                    if (pitched) HipRoof(x0, y0, x1, y1, style);
                    else Roof(x0, y0, x1, y1, style);
                }
                currentOwner = -1;
            }

            struct Style
            {
                public int id, height, floors, winW, period;
                public Color32 plaster, awning, roof;
                public bool banded, balconies, pitched;
                public float lit;
            }

            void Roof(float x0, float y0, float x1, float y1, Style s)
            {
                float h = s.height;
                var rng = new System.Random(s.id * 7 + seed);
                float Rx(float m) => x0 + 4 + (float)rng.NextDouble() * Mathf.Max(1f, x1 - x0 - 8 - m);
                float Ry(float m) => y0 + 4 + (float)rng.NextDouble() * Mathf.Max(1f, y1 - y0 - 8 - m);
                if (rng.NextDouble() < 0.35)
                {
                    float px = Rx(10), py = Ry(9);
                    Box(px, py, px + 10, py + 9, h, h + 9, s.plaster, -1);   // merdiven kulesi
                }
                int tanks = rng.Next(3);
                for (int i = 0; i < tanks; i++)
                {
                    float px = Rx(5), py = Ry(5);
                    Box(px, py, px + 4, py + 4, h, h + 6, C(214, 214, 224), -1);
                }
                if (rng.NextDouble() < 0.45)
                {
                    // Güneş enerjili su ısıtıcısı: mavi panel, arkasında beyaz depo.
                    float px = Rx(10), py = Ry(8);
                    Box(px, py, px + 10, py + 5, h, h + 2, C(70, 100, 176), -1);
                    Box(px, py + 5, px + 10, py + 8, h, h + 5, C(230, 230, 236), -1);
                }
                if (rng.NextDouble() < 0.6)
                {
                    float px = Rx(1), py = Ry(1);
                    Box(px, py, px + 1, py + 1, h, h + 9 + rng.Next(6), C(80, 76, 96), -1);
                    Box(px - 2, py, px + 3, py + 1, h + 7, h + 8, C(80, 76, 96), -1);
                }
            }

            // Kiremit kırma çatı: dört eğimli yüz, saçak 1 birim taşar; yüzler güneşe göre aydınlanır, kiremit sıraları.
            void HipRoof(float x0, float y0, float x1, float y1, Style s)
            {
                Color32 tile = Hash(s.id, 15) < 0.7f ? C(196, 92, 70) : C(170, 80, 66);
                float ex = 1f, slope = 0.55f;
                for (float gx = x0 - ex; gx < x1 + ex; gx += 0.5f)
                    for (float gy = y0 - ex; gy < y1 + ex; gy += 0.5f)
                    {
                        float a = gx - (x0 - ex), b = x1 + ex - gx, c = gy - (y0 - ex), d = y1 + ex - gy;
                        float e = Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
                        float h = s.height + e * slope;
                        float k = e == d ? 1.06f : e == a ? 0.86f : e == b ? 0.72f : 0.6f;
                        if (Mathf.Repeat(e, 3f) < 0.5f) k -= 0.1f;
                        if (e < 0.5f) k -= 0.15f;
                        int px = Mathf.FloorToInt(OX + gx - gy), py = Mathf.FloorToInt(OY + (gx + gy) / 2f + h);
                        Plot(px, py, gx + gy - h, Shade(tile, Mathf.Min(1.06f, k)), gx + gy, -1);
                    }
                if (Hash(s.id, 16) < 0.6f)
                {
                    float cx = x0 + (x1 - x0) * 0.3f, cy = y0 + (y1 - y0) * 0.6f;
                    float e = Mathf.Min(Mathf.Min(cx - x0, x1 - cx), Mathf.Min(cy - y0, y1 - cy));
                    Box(cx, cy, cx + 3, cy + 3, s.height, s.height + e * slope + 5, C(150, 96, 84), -1); // baca
                }
            }

            // Yıkılmış bina: alt katlarda kırık duvarlar (üst kenarı dişli), içinde ve önünde enkaz yığını.
            void Rubble(float x0, float y0, float x1, float y1, Style s)
            {
                var broken = s;
                broken.lit = 0f;
                broken.awning = default;
                broken.pitched = true; // kırık duvarda parapet yok
                broken.height = 14 + 2 * 12;
                float stub = 14 + Mathf.Min(2, s.floors) * 12;
                ForEachBoxPixel(x0, y0, x1, y1, 0, broken.height, (face, gx, gy, h, x, y) =>
                {
                    if (face == 0) return (default, -1);
                    bool left = face == 1;
                    float a = left ? gy - y0 : gx - x0, len = left ? y1 - y0 : x1 - x0;
                    // Dişli kırık üst kenar: köşelerde yüksek, ortada çökük.
                    float corner = Mathf.Min(a, len - a) / Mathf.Max(1f, len);
                    float jag = stub * (1f - corner * 1.6f) + Hash(Mathf.FloorToInt(a / 2f), s.id, left ? 1 : 2) * 7f;
                    if (h > jag) return (default, -1);
                    Color32 c = FaceColor(broken, left, a, len, h, left ? 0.82f : 0.6f, out _);
                    if (h > jag - 2f) c = Shade(c, 0.7f);                                   // kırık kenar
                    if (Hash(x, y, s.id) < 0.08f) c = Shade(c, 0.75f);                      // çatlak, is
                    return (c, -1);
                });
                // Enkaz yığını
                float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f, rx = (x1 - x0) / 2f + 9f, ry = (y1 - y0) / 2f + 9f;
                float peak = 16f + s.floors * 3.6f;
                Color32[] debris = { C(150, 144, 150), C(120, 112, 124), C(96, 88, 100), Shade(s.plaster, 0.9f), C(172, 166, 168) };
                for (float gx = cx - rx; gx < cx + rx; gx += 0.5f)
                    for (float gy = cy - ry; gy < cy + ry; gy += 0.5f)
                    {
                        float nx = (gx - cx) / rx, ny = (gy - cy) / ry, r = Mathf.Sqrt(nx * nx + ny * ny);
                        if (r >= 1f) continue;
                        int cxi = Mathf.FloorToInt(gx / 2f), cyi = Mathf.FloorToInt(gy / 2f);
                        float h = peak * (1f - Mathf.Pow(r, 1.4f)) + Hash(cxi, cyi, s.id) * 4f;
                        Color32 c = debris[(int)(Hash(cxi, cyi, s.id + 1) * debris.Length)];
                        float k = nx < -0.2f ? 0.95f : ny < -0.2f ? 0.66f : 0.82f;
                        if (Hash(cxi, cyi, s.id + 2) < 0.06f) c = C(70, 60, 64);           // demir, boşluk
                        int px = Mathf.FloorToInt(OX + gx - gy), py = Mathf.FloorToInt(OY + (gx + gy) / 2f + h);
                        Plot(px, py, gx + gy - h, Shade(c, k), gx + gy, -1);
                    }
                // Sokağa saçılmış parçalar
                var rng = new System.Random(s.id);
                for (int i = 0; i < 10; i++)
                {
                    float px = cx + (float)(rng.NextDouble() * 2 - 1) * (rx + 6f), py = cy + (float)(rng.NextDouble() * 2 - 1) * (ry + 6f);
                    float sz = 1f + (float)rng.NextDouble() * 2f;
                    Box(px, py, px + sz, py + sz, 0, sz, debris[rng.Next(debris.Length)], -1);
                }
            }

            void Mosque(float x0, float y0, float x1, float y1)
            {
                Color32 stone = C(228, 222, 230);
                float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
                Box(cx - 22, cy - 22, cx + 22, cy + 22, 0, 18, stone, -1);
                Box(cx - 14, cy - 14, cx + 14, cy + 14, 18, 22, stone, -1);
                Dome(cx, cy, 22, 15f, C(150, 150, 172));
                foreach (float s in new[] { -1f, 1f })
                {
                    float mx = cx + s * 27f, my = cy - s * 27f;
                    Box(mx - 2, my - 2, mx + 2, my + 2, 0, 66, stone, -1);
                    Box(mx - 3, my - 3, mx + 3, my + 3, 46, 49, stone, -1);
                    Box(mx - 1.5f, my - 1.5f, mx + 1.5f, my + 1.5f, 66, 72, C(150, 150, 172), -1);
                    Box(mx - 1, my - 1, mx + 1, my + 1, 72, 78, C(150, 150, 172), -1);
                    Box(mx - 0.5f, my - 0.5f, mx + 0.5f, my + 0.5f, 78, 84, C(150, 150, 172), -1);
                }
            }

            // Kubbe: ekranda yarım daire + taban elipsi; ışık soldan.
            void Dome(float gx, float gy, float baseH, float r, Color32 lead)
            {
                float sx = OX + gx - gy, sy = OY + (gx + gy) / 2f + baseH;
                float z = gx + gy - baseH - r;
                for (int y = Mathf.FloorToInt(sy - r / 2f); y <= sy + r; y++)
                    for (int x = Mathf.FloorToInt(sx - r); x <= sx + r; x++)
                    {
                        float dx = (x + 0.5f - sx) / r, dy = (y + 0.5f - sy) / r;
                        bool inside = dy >= 0 ? dx * dx + dy * dy < 1f : dx * dx + dy * dy * 4f < 1f;
                        if (!inside) continue;
                        float light = dx < -0.35f ? 1.05f : dx < 0.2f ? 0.88f : 0.68f;
                        if (dy > 0.75f) light = 1.1f;
                        Plot(x, y, z, Shade(lead, Mathf.Min(1f, light)), gx + gy, -1);
                    }
                Box(gx - 0.5f, gy - 0.5f, gx + 0.5f, gy + 0.5f, baseH + r, baseH + r + 5, C(220, 180, 90), -1);
            }

            void Tree(float gx, float gy, float r)
            {
                if (gx + gy > FarDepth - 2f) return;
                Box(gx, gy, gx + 1, gy + 1, 0, 4, C(84, 60, 52), -1);
                float sx = OX + gx - gy, sy = OY + (gx + gy) / 2f + 4f + r;
                float z = gx + gy - 4f - r * 2f;
                for (int y = Mathf.FloorToInt(sy - r); y <= sy + r; y++)
                    for (int x = Mathf.FloorToInt(sx - r); x <= sx + r; x++)
                    {
                        float dx = (x + 0.5f - sx) / r, dy = (y + 0.5f - sy) / r;
                        if (dx * dx + dy * dy >= 1f) continue;
                        Color32 c = dx + -dy * 0.6f < -0.45f ? C(110, 146, 92) : dx - dy * 0.4f > 0.35f ? C(40, 66, 58) : C(68, 104, 74);
                        if (Hash(x, y, 13) < 0.12f) c = Shade(c, 0.85f);
                        Plot(x, y, z, c, gx + gy, -1);
                    }
            }

            void Cars(int bi, int bj)
            {
                for (int k = 0; k < 3; k++)
                {
                    if (Hash(bi, bj, k, 51) > 0.55f) continue;
                    Color32 body = CarColors[(int)(Hash(bi, bj, k, 52) * CarColors.Length)];
                    float t = StreetHalf + 6 + Hash(bi, bj, k, 53) * (Pitch - 2 * StreetHalf - 20);
                    if (k % 2 == 0)
                    {
                        // gx yönünde giden sokakta (bloğun önü), şeritte
                        float gy = bj * Pitch + (Hash(bi, bj, k, 54) < 0.5f ? 4f : -8f), gx = bi * Pitch + t;
                        Car(gx, gy, gx + 10, gy + 5, body);
                    }
                    else
                    {
                        float gx = bi * Pitch + (Hash(bi, bj, k, 54) < 0.5f ? 4f : -8f), gy = bj * Pitch + t;
                        Car(gx, gy, gx + 5, gy + 10, body);
                    }
                }
            }

            void Car(float x0, float y0, float x1, float y1, Color32 body)
            {
                if (x1 + y1 > FarDepth - 2f) return;
                Box(x0, y0, x1, y1, 0, 3, body, -1);
                bool alongX = x1 - x0 > y1 - y0;
                if (alongX) Box(x0 + 3, y0 + 1, x1 - 2, y1 - 1, 3, 5, Mix(body, C(60, 70, 110), 0.45f), -1);
                else Box(x0 + 1, y0 + 3, x1 - 1, y1 - 2, 3, 5, Mix(body, C(60, 70, 110), 0.45f), -1);
            }

            // ------------------------------------------------ kutu çizimi
            // Bina gövdesi: ön cepheler (sol: gx = x0, sağ: gy = y0) pencereli, üstte parapet ve çatı.
            void BuildingBox(float x0, float y0, float x1, float y1, Style s)
            {
                ForEachBoxPixel(x0, y0, x1, y1, 0, s.height, (face, gx, gy, h, x, y) =>
                {
                    int win = -1;
                    Color32 c;
                    if (face == 0)
                    {
                        float edge = Mathf.Min(Mathf.Min(gx - x0, x1 - gx), Mathf.Min(gy - y0, y1 - gy));
                        c = edge < 2f ? Shade(s.plaster, edge < 1f ? 1.06f : 0.9f) : s.roof;
                        if (edge < 1f) c = Mix(s.plaster, C(255, 230, 200), 0.3f);
                        else if (edge >= 2f && Hash(Mathf.FloorToInt(gx / 4f), Mathf.FloorToInt(gy / 4f), s.id) < 0.08f) c = Shade(s.roof, 0.92f);
                    }
                    else
                    {
                        bool left = face == 1;
                        float k = left ? 0.82f : 0.6f;
                        float a = left ? gy - y0 : gx - x0, len = left ? y1 - y0 : x1 - x0;
                        c = FaceColor(s, left, a, len, h, k, out win);
                        // Hasarlı şehir: cephelerde çapraz çatlaklar.
                        if (Damaged && h > 14f && Hash(s.id, left ? 1 : 2, 61) < 0.6f)
                        {
                            float line = a + h * 0.9f + Hash(s.id, 62) * 40f + Mathf.Sin(h * 0.7f) * 1.5f;
                            if (Mathf.Abs(Mathf.Repeat(line, 37f) - 18f) < 0.6f && Hash(Mathf.FloorToInt(line / 37f), s.id, 63) < 0.5f)
                                c = C(34, 28, 44);
                        }
                        if (a < 1f) c = Mix(c, C(255, 230, 200), left ? 0.22f : 0.1f);      // köşe ışığı
                        if (left && a >= len - 1f) c = Shade(c, 0.8f);
                    }
                    return (c, win);
                });
            }

            Color32 FaceColor(Style s, bool left, float a, float len, float h, float k, out int win)
            {
                win = -1;
                int ai = Mathf.FloorToInt(a), hi = Mathf.FloorToInt(h);
                Color32 wall = Shade(s.plaster, k);
                if (!s.pitched && hi >= s.height - 3) return Shade(s.plaster, hi == s.height - 1 ? k + 0.15f : k + 0.05f);
                if (hi < 14)
                {
                    // Zemin kat: dükkân vitrinleri ve tente.
                    int m = ai % 14;
                    if (hi >= 11 && hi < 13 && s.awning.a > 0) return Shade(s.awning, k + (m % 4 < 2 ? 0.1f : -0.05f));
                    if (m >= 2 && m < 12 && hi >= 2 && hi < 10)
                    {
                        win = WindowId(s.id, left ? 1 : 2, ai / 14, -1, s.lit > 0f ? s.lit + 0.15f : 0f, out bool on, out Color32 litColor);
                        return on ? litColor : Shade(C(40, 40, 70), k + 0.15f);
                    }
                    return Shade(wall, 0.92f);
                }
                int vv = hi - 14, floor = vv / 12, vl = vv % 12;
                if (s.banded && vl == 11) return Shade(wall, 0.9f);
                const int margin = 3;
                if (ai < margin || a >= len - margin) return wall;
                int col = (ai - margin) / s.period, within = (ai - margin) % s.period;
                bool inColumn = within < s.winW && ai + (s.winW - within) <= len - margin + 0.01f;
                // Balkon: pencerenin altında döşeme, demir korkuluk ve küpeşte (her iki pencereden birinde).
                if (s.balconies && inColumn && col % 2 == 0 && vl < 3 && floor < s.floors)
                {
                    if (vl == 2) return Shade(C(176, 172, 190), k + 0.2f);
                    if (vl == 0) return Shade(s.plaster, k + 0.15f);
                    return ai % 2 == 0 ? Shade(C(70, 66, 92), k + 0.1f) : Shade(wall, 0.85f);
                }
                if (!inColumn) return wall;
                if (vl == 2) return Shade(s.plaster, k + 0.12f);                 // denizlik
                if (vl < 3 || vl >= 9) return wall;
                win = WindowId(s.id, left ? 1 : 2, col, floor, s.lit, out bool lit, out Color32 lc);
                if (lit) return vl == 8 ? Shade(lc, 0.9f) : lc;
                if (Damaged && Hash(s.id * 64 + col, floor, left ? 1 : 2, 64) < 0.3f) return C(18, 16, 28); // kırık cam
                return vl == 8 ? Shade(C(100, 88, 132), k + 0.2f) : Shade(C(42, 44, 80), k + 0.2f);
            }

            int WindowId(int building, int face, int col, int floor, float litRatio, out bool lit, out Color32 litColor)
            {
                long key = ((long)building * 16 + face) * 4096 + (col + 64) * 32 + floor + 2;
                if (!windowIds.TryGetValue(key, out int id))
                {
                    id = windowLit.Count;
                    windowIds[key] = id;
                    float r = Hash(building, face, col, floor + 100);
                    windowLit.Add(r < litRatio);
                    windowBuilding.Add(building);
                    float tone = Hash(building, face, col, floor + 200);
                    windowLitColor.Add(tone < 0.06f ? C(170, 196, 255) : tone < 0.5f ? C(255, 206, 124) : C(255, 186, 100));
                }
                lit = windowLit[id];
                litColor = windowLitColor[id];
                return id;
            }

            // Düz renkli kutu (lamba, depo, araba, minare...): üst aydınlık, sol yarı, sağ gölge.
            void Box(float x0, float y0, float x1, float y1, float h0, float h1, Color32 c, int win, bool glow = false)
            {
                ForEachBoxPixel(x0, y0, x1, y1, h0, h1, (face, gx, gy, h, x, y) =>
                    (glow ? c : face == 0 ? Shade(c, 1.02f) : face == 1 ? Shade(c, 0.82f) : Shade(c, 0.6f), win), glow);
            }

            // Gölgelendirici rengi saydam (a = 0) dönerse o piksel çizilmez (yıkık duvarın kırık kenarı).
            delegate (Color32 color, int window) FaceShader(int face, float gx, float gy, float h, int x, int y);

            void ForEachBoxPixel(float x0, float y0, float x1, float y1, float h0, float h1, FaceShader shader, bool noFog = false)
            {
                int sxMin = Mathf.FloorToInt(OX + x0 - y1) - 1, sxMax = Mathf.CeilToInt(OX + x1 - y0) + 1;
                int syMin = Mathf.FloorToInt(OY + (x0 + y0) / 2f + h0) - 1, syMax = Mathf.CeilToInt(OY + (x1 + y1) / 2f + h1) + 1;
                sxMin = Mathf.Max(0, sxMin); syMin = Mathf.Max(0, syMin);
                sxMax = Mathf.Min(W - 1, sxMax); syMax = Mathf.Min(H - 1, syMax);
                for (int y = syMin; y <= syMax; y++)
                    for (int x = sxMin; x <= sxMax; x++)
                    {
                        float X = x + 0.5f - OX, Y = y + 0.5f - OY;
                        // üst yüz
                        float s2 = 2f * (Y - h1);
                        float gx = (X + s2) / 2f, gy = (s2 - X) / 2f, h;
                        int face;
                        if (gx >= x0 && gx < x1 && gy >= y0 && gy < y1) { face = 0; h = h1; }
                        else
                        {
                            gx = x0; gy = x0 - X; h = Y - (gx + gy) / 2f;
                            if (gy >= y0 && gy < y1 && h >= h0 && h < h1) face = 1;
                            else
                            {
                                gy = y0; gx = X + y0; h = Y - (gx + gy) / 2f;
                                if (gx >= x0 && gx < x1 && h >= h0 && h < h1) face = 2;
                                else continue;
                            }
                        }
                        float z = gx + gy - h;
                        int i = y * W + x;
                        if (z >= depth[i]) continue;
                        var (c, win) = shader(face, gx, gy, h, x, y);
                        if (c.a == 0) continue;
                        float f = noFog ? 0f : FogAt(gx + gy);
                        bool litWin = win >= 0 && windowLit[win];
                        color[i] = Haze(c, litWin ? f * 0.6f : f, y);
                        depth[i] = z;
                        fog[i] = f;
                        window[i] = win;
                        owner[i] = currentOwner;
                        heightAt[i] = h;
                    }
            }

            float FogAt(float d)
            {
                if (!fogEnabled) return 0f;
                float f = Mathf.Clamp01((d - HazeStart) / (FarDepth - HazeStart));
                return Mathf.Floor(f * 10f) / 10f * 0.9f;
            }

            void Plot(int x, int y, float z, Color32 c, float d, int win)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x;
                if (z >= depth[i]) return;
                float f = fogEnabled ? Mathf.Clamp01((d - HazeStart) / (FarDepth - HazeStart)) : 0f;
                f = Mathf.Floor(f * 10f) / 10f;
                color[i] = Haze(c, f, y);
                depth[i] = z;
                fog[i] = f;
                window[i] = win;
                owner[i] = currentOwner;
                heightAt[i] = d - z;
            }

            // ------------------------------------------------ yıkılan binanın ikiye kırılması
            // Binanın kırılma yüksekliği (piksel): orta katlar.
            public float BreakHeight(int id) => 14f + Mathf.Max(1, floorsOf[id] / 2) * 12f + 4f;

            // Kırık çizgisinin üstü (top = true) ya da altı; çizgi her sütunda biraz oynar (dişli kırık).
            public Texture2D SplitCrop(RectInt r, float breakH, bool top)
            {
                var px = new Color32[r.width * r.height];
                for (int y = 0; y < r.height; y++)
                    for (int x = 0; x < r.width; x++)
                    {
                        int i = (r.y + y) * W + r.x + x;
                        if (color[i].a == 0) continue;
                        float jag = breakH + (Hash((r.x + x) / 2, 71) - 0.5f) * 7f;
                        if ((heightAt[i] >= jag) != top) continue;
                        Color32 c = color[i];
                        px[y * r.width + x] = c;
                    }
                return Tex(px, r.width, r.height);
            }

            // Üst parçanın devrileceği menteşe: kırık çizgisinin dış ucu (sola düşerse sol köşe, sağa düşerse sağ).
            public Vector2 Hinge(int id, float breakH, int dir)
            {
                Rect f = footprints[id];
                return dir < 0
                    ? new Vector2(OX + f.xMin - f.yMax, OY + (f.xMin + f.yMax) / 2f + breakH)
                    : new Vector2(OX + f.xMax - f.yMin, OY + (f.xMax + f.yMin) / 2f + breakH);
            }

            public int HeightOf(int id) => 14 + floorsOf[id] * 12 + 3;

            // ------------------------------------------------ sokaktaki hareketli insanlar ve arabalar
            // Önünde hiçbir şey olmayan (bina, ağaç, park etmiş araba, lamba) şerit ve kaldırım parçaları: hareketli
            // sprite'lar bu yollarda şehrin üstünde çizilse de doğru görünür. Arabalar yüzerken de (su seviyesi kadar
            // yukarıda) görünür kalsın diye yukarıya doğru da kontrol edilir.
            public List<(bool car, int axis, Vector2 a, Vector2 b)> FindPaths()
            {
                var paths = new List<(bool, int, Vector2, Vector2)>();
                for (int axis = 0; axis < 2; axis++)
                    for (int line = -2; line <= 12; line++)
                        foreach (float offset in new[] { -5f, 5f, -(StreetHalf + 3f), StreetHalf + 3f })
                        {
                            bool car = Mathf.Abs(offset) < StreetHalf;
                            float across = line * Pitch + offset;
                            float runStart = float.NaN;
                            for (float along = -200f; along <= 1700f; along += 2f)
                            {
                                float gx = axis == 0 ? along : across, gy = axis == 0 ? across : along;
                                bool ok = PathVisible(gx, gy, car ? 28f : 9f);
                                if (ok && float.IsNaN(runStart)) runStart = along;
                                if ((!ok || along >= 1700f) && !float.IsNaN(runStart))
                                {
                                    float runEnd = along - 2f;
                                    if (runEnd - runStart >= (car ? 70f : 44f))
                                    {
                                        Vector2 Screen(float t) => axis == 0 ? new Vector2(OX + t - across, OY + (t + across) / 2f) : new Vector2(OX + across - t, OY + (across + t) / 2f);
                                        paths.Add((car, axis, Screen(runStart + 4f), Screen(runEnd - 4f)));
                                    }
                                    runStart = float.NaN;
                                }
                            }
                        }
                return paths;
            }

            bool PathVisible(float gx, float gy, float upTo)
            {
                float d = gx + gy;
                if (d > 1150f) return false;                                   // pusta kaybolan uzak yollar
                for (float h = 0f; h <= upTo; h += 3f)
                {
                    int x = Mathf.FloorToInt(OX + gx - gy), y = Mathf.FloorToInt(OY + d / 2f + h);
                    if (x < 4 || y < 4 || x >= W - 4 || y >= H - 4) return false;
                    if (depth[y * W + x] < d - h - 1.5f) return false;           // önünde bir şey var
                }
                return true;
            }

            // Tek başına, sissiz araba sprite'ı (axis 0: gx boyunca uzun, 1: gy boyunca). Pivot: tabanın ortası.
            public Texture2D CarSprite(int axis, Color32 body, out Vector2 pivot)
            {
                const float gx0 = 300f, gy0 = 300f;
                if (axis == 0) Car(gx0, gy0, gx0 + 10, gy0 + 5, body); else Car(gx0, gy0, gx0 + 5, gy0 + 10, body);
                float cx = gx0 + (axis == 0 ? 5f : 2.5f), cy = gy0 + (axis == 0 ? 2.5f : 5f);
                RectInt r = DrawnBounds();
                pivot = new Vector2((OX + cx - cy - r.x) / r.width, (OY + (cx + cy) / 2f - r.y) / r.height);
                return Crop(r);
            }
        }
    }
}
