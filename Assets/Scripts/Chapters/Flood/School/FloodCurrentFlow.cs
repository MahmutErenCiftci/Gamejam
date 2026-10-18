using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Okul Caddesi'ndeki selin AKINTISI (FloodSchool, Mahmut): su bandının üstünde akış yönünde kayan açık renkli
    /// akıntı çizgileri, köprünün akıntıya bakan kenarında biriken köpük ve suyla sürüklenen döküntüler (tahta, yaprak,
    /// şişe). Su sprite'ı (SchoolRoute/FloodWater) yerinde kalır; bu bileşen yalnız hareketi ve yönü okunur kılar.
    /// Görseller kodla, tek küçük dokuda üretilir (piksel, nokta filtre); ışıklı malzeme su sprite'ınınkiyle aynıdır,
    /// böylece akşam ışığı ve yağmur karartması bunları da etkiler. Akış yönü sel kapılma ara sahnesiyle aynıdır
    /// (ekranın sol altına doğru). Akan su sesi (AMB_Flood_Water_Loop) Cenk köprüye yaklaştıkça yükselir.
    /// Su bandı: merkez çizgisi lineOrigin'den geçen, flowDirection eğimindeki şerit; halfWidth ekran-dikey yarı genişlik.
    /// </summary>
    public class FloodCurrentFlow : MonoBehaviour
    {
        private const float Ppu = 64f;

        [Header("Su bandı (dünya birimi)")]
        [Tooltip("Bandın merkez çizgisi üzerindeki bir nokta")]
        [SerializeField] private Vector2 lineOrigin = new Vector2(0f, 4.15f);
        [Tooltip("Akış yönü (2:1 izometrik sokak boyunca; (-2,-1) = sol aşağı)")]
        [SerializeField] private Vector2 flowDirection = new Vector2(-2f, -1f);
        [Tooltip("Merkezden akış yönünde iki tarafa uzunluk")]
        [SerializeField] private float halfLength = 11f;
        [Tooltip("Bandın ekran-dikey yarı genişliği (kıyıya bu kadar yaklaşılır)")]
        [SerializeField] private float halfWidth = 1.2f;

        [Header("Akıntı")]
        [Tooltip("Orta akıntı hızı (dünya birimi / sn); kıyıya doğru yavaşlar")]
        [SerializeField] private float speed = 1.7f;
        [SerializeField] private int streakCount = 120;
        [SerializeField] private int debrisCount = 12;
        [Tooltip("Çizgilerin ömrü (sn): belirir, akar, söner")]
        [SerializeField] private Vector2 streakLife = new Vector2(1.1f, 2.4f);

        [Header("Köprü köpüğü")]
        [Tooltip("Köprü güvertesinin akıntıya bakan kenarı (iki uç, dünya)")]
        [SerializeField] private Vector2 bridgeEdgeA = new Vector2(1.95f, 3.95f);
        [SerializeField] private Vector2 bridgeEdgeB = new Vector2(-0.05f, 4.95f);
        [SerializeField] private int foamCount = 26;

        [Header("Çizim")]
        [Tooltip("Malzeme ve sıralama katmanı bu sprite'tan alınır (SchoolRoute/FloodWater)")]
        [SerializeField] private SpriteRenderer referenceWater;
        [Tooltip("Su sprite'ının üstünde, köprü güvertesinin altında")]
        [SerializeField] private int sortingOrder = -91;

        [Header("Ses")]
        [SerializeField] private string waterLoop = "AMB_Flood_Water_Loop";
        [SerializeField] private string audioChannel = "flood_school_water";
        [Tooltip("Sesin en yüksek olduğu nokta (köprünün ortası)")]
        [SerializeField] private Vector2 audioCenter = new Vector2(0.4f, 4.2f);
        [Tooltip("Ses mesafesi: bu uzaklıkta en kısık (zemin birimi)")]
        [SerializeField] private float audioRange = 7f;
        [SerializeField] private Vector2 audioVolume = new Vector2(0.12f, 0.5f);
        [Tooltip("Sesin uzaklığı ölçülen hedef (boşsa sahnedeki oyuncu)")]
        [SerializeField] private Transform listener;

        private sealed class Mover
        {
            public SpriteRenderer Renderer;
            public float S, V, Speed, Life, MaxLife, Phase;
            public int Kind;
            public Color Tint;
        }

        private readonly List<Mover> streaks = new List<Mover>();
        private readonly List<Mover> foam = new List<Mover>();
        private readonly List<Mover> debris = new List<Mover>();
        private Sprite[] streakSprites, foamSprites, debrisSprites;
        private Texture2D atlas;
        private Vector2 dir;
        private float lastVolume = -1f, nextAudioAt;
        private Transform root;

        private static readonly Color32 Outline = new Color32(27, 26, 43, 255);
        private static readonly Color32 StreakLight = new Color32(236, 232, 212, 255);
        private static readonly Color32 StreakMid = new Color32(188, 180, 150, 255);
        private static readonly Color32 FoamColor = new Color32(222, 220, 200, 255);
        private static readonly Color32 FoamShade = new Color32(176, 172, 150, 255);

        /// <summary>Akış yönü (birim vektör, dünya).</summary>
        public Vector2 FlowDirection => dir;

        /// <summary>Orta akıntı hızı (dünya birimi / sn).</summary>
        public float Speed => speed;

        private void Awake()
        {
            dir = flowDirection.sqrMagnitude > 0.0001f ? flowDirection.normalized : new Vector2(-0.894f, -0.447f);
            BuildSprites();
            root = new GameObject("CurrentFx").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < streakCount; i++) streaks.Add(Spawn(streakSprites[i % streakSprites.Length], i % streakSprites.Length, true));
            for (int i = 0; i < foamCount; i++) foam.Add(Spawn(foamSprites[i % foamSprites.Length], i % foamSprites.Length, false));
            for (int i = 0; i < debrisCount; i++) debris.Add(Spawn(debrisSprites[i % debrisSprites.Length], i % debrisSprites.Length, false));
            foreach (var m in streaks) ResetStreak(m, true);
            foreach (var m in foam) ResetFoam(m, true);
            for (int i = 0; i < debris.Count; i++) ResetDebris(debris[i], true);
        }

        private void OnDisable() => GameAudio.StopLoop(audioChannel, 0.8f);

        private void OnDestroy()
        {
            if (atlas != null) Destroy(atlas);
        }

        private Mover Spawn(Sprite sprite, int kind, bool streak)
        {
            var go = new GameObject(streak ? "Streak" : "Fx");
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (referenceWater != null)
            {
                sr.sharedMaterial = referenceWater.sharedMaterial;
                sr.sortingLayerID = referenceWater.sortingLayerID;
            }
            sr.sortingOrder = sortingOrder;
            return new Mover { Renderer = sr, Kind = kind };
        }

        // ---------------- güncelleme ----------------

        private void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            foreach (var m in streaks)
            {
                m.Life += dt;
                m.S -= m.Speed * dt;
                if (m.Life >= m.MaxLife || m.S < -halfLength) { ResetStreak(m, false); continue; }
                float a = Mathf.Sin(Mathf.PI * Mathf.Clamp01(m.Life / m.MaxLife));
                Place(m, 0f, a * m.Tint.a);
            }
            foreach (var m in foam)
            {
                m.Life += dt;
                m.S -= m.Speed * dt;
                if (m.Life >= m.MaxLife) { ResetFoam(m, false); continue; }
                float k = m.Life / m.MaxLife;
                Place(m, Mathf.Round(Mathf.Sin(t * 9f + m.Phase)) / Ppu, (1f - k) * m.Tint.a);
            }
            foreach (var m in debris)
            {
                m.S -= m.Speed * dt;
                if (m.S < -halfLength) { ResetDebris(m, false); continue; }
                float bob = Mathf.Round(Mathf.Sin(t * 3.1f + m.Phase) * 1.2f) / Ppu;
                float edge = Mathf.Clamp01((halfLength - Mathf.Abs(m.S)) / 1.2f);
                Place(m, bob, edge);
            }
            UpdateAudio();
        }

        private void Place(Mover m, float lift, float alpha)
        {
            Vector2 p = lineOrigin + dir * m.S + Vector2.up * (m.V + lift);
            p.x = Mathf.Round(p.x * Ppu) / Ppu;
            p.y = Mathf.Round(p.y * Ppu) / Ppu;
            m.Renderer.transform.position = new Vector3(p.x, p.y, 0f);
            var c = m.Tint;
            c.a = Mathf.Clamp01(alpha);
            m.Renderer.color = c;
        }

        private float FlowAt(float v)
        {
            float k = Mathf.Clamp01(Mathf.Abs(v) / Mathf.Max(0.01f, halfWidth));
            return speed * (1f - 0.5f * k * k);
        }

        private void ResetStreak(Mover m, bool initial)
        {
            m.V = Random.Range(-halfWidth, halfWidth) * 0.95f;
            m.S = Random.Range(-halfLength, halfLength);
            m.Speed = FlowAt(m.V) * Random.Range(0.85f, 1.15f);
            m.MaxLife = Random.Range(streakLife.x, streakLife.y);
            m.Life = initial ? Random.Range(0f, m.MaxLife) : 0f;
            float bright = Random.Range(0.75f, 1f);
            m.Tint = new Color(1f, 1f, 1f, bright);
            Place(m, 0f, 0f);
        }

        private void ResetFoam(Mover m, bool initial)
        {
            // Köprünün akıntıya bakan kenarında: su güverteye çarpar, köpürür, sonra güvertenin altına akar.
            float u = Random.value;
            Vector2 at = Vector2.Lerp(bridgeEdgeA, bridgeEdgeB, u) + new Vector2(0.18f, 0.09f) * Random.Range(0f, 1.6f);
            // konum = lineOrigin + dir * S + yukarı * V  →  S x'ten, V kalan dikey farktan
            Vector2 rel = at - lineOrigin;
            m.S = Mathf.Abs(dir.x) > 0.001f ? rel.x / dir.x : 0f;
            m.V = rel.y - dir.y * m.S;
            m.Speed = speed * Random.Range(0.08f, 0.22f);
            m.MaxLife = Random.Range(0.45f, 0.95f);
            m.Life = initial ? Random.Range(0f, m.MaxLife) : 0f;
            m.Phase = Random.Range(0f, 6.28f);
            m.Tint = new Color(1f, 1f, 1f, Random.Range(0.7f, 1f));
            Place(m, 0f, 0f);
        }

        private void ResetDebris(Mover m, bool initial)
        {
            m.V = Random.Range(-halfWidth, halfWidth) * 0.7f;
            m.S = initial ? Random.Range(-halfLength, halfLength) : halfLength + Random.Range(0f, 6f);
            m.Speed = FlowAt(m.V) * Random.Range(0.75f, 0.95f);
            m.Phase = Random.Range(0f, 6.28f);
            m.Tint = Color.white;
            Place(m, 0f, 0f);
        }

        private void UpdateAudio()
        {
            if (string.IsNullOrEmpty(waterLoop) || Time.unscaledTime < nextAudioAt) return;
            nextAudioAt = Time.unscaledTime + 0.25f;
            if (listener == null)
            {
                var p = FindAnyObjectByType<IsoPlayerController>();
                if (p != null) listener = p.transform;
            }
            float k = 0.5f;
            if (listener != null)
            {
                Vector2 d = (Vector2)listener.position - audioCenter;
                float dist = Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y);
                k = 1f - Mathf.Clamp01(dist / Mathf.Max(0.1f, audioRange));
            }
            float vol = Mathf.Round(Mathf.Lerp(audioVolume.x, audioVolume.y, k * k) * 20f) / 20f;
            if (Mathf.Approximately(vol, lastVolume)) return;
            lastVolume = vol;
            GameAudio.PlayLoop(waterLoop, audioChannel, vol, 0.6f);
        }

        // ---------------- görseller (kodla, tek doku) ----------------

        private void BuildSprites()
        {
            const int w = 128, h = 24;
            atlas = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FS_CurrentFx" };
            var px = new Color32[w * h];
            void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }

            // Akıntı çizgileri: 2:1 eğimli (sokak boyunca) açık çizgi, akış yönünün tersinde sönen kuyruk.
            // Uzun (0..21), orta (24..37), kısa (40..49); y 0..11 bandı. Akşam ışığında da seçilsin diye uzun ve açık renkli.
            void Streak(int x0, int len)
            {
                for (int i = 0; i < len; i++)
                {
                    int x = x0 + i, y = 1 + i / 2;
                    // baş (sol alt, akış yönü) parlak, kuyruk (sağ üst) koyu
                    Set(x, y, i < len / 3 ? StreakLight : StreakMid);
                    if (i % 2 == 0 && i < len - 2) Set(x, y - 1, new Color32(120, 112, 88, 150));
                }
            }
            Streak(0, 22);
            Streak(24, 14);
            Streak(40, 10);

            // Köpük: 4x2 ve 3x2 kremsi lekeler (y 14..15), 2x1 kırıntı.
            void Blob(int x0, int y0, int bw)
            {
                for (int x = 0; x < bw; x++) { Set(x0 + x, y0 + 1, FoamColor); Set(x0 + x, y0, x == 0 || x == bw - 1 ? FoamShade : FoamColor); }
            }
            Blob(0, 14, 4);
            Blob(6, 14, 3);
            Set(11, 14, FoamColor); Set(12, 14, FoamShade);

            // Döküntü (y 14..17): tahta parçası (12x4), yaprak (4x3), plastik şişe (7x3), dal (11x4); dış hat koyu.
            // Akıntıyla sürüklenen bu parçalar akış yönünü en açık gösteren işarettir, o yüzden belirgin boyutta.
            var wood = new Color32(150, 98, 52, 255);
            for (int x = 0; x < 12; x++) for (int y = 0; y < 4; y++)
                Set(16 + x, 14 + y, (x == 0 || x == 11 || y == 0 || y == 3) ? Outline : (y == 2 ? new Color32(176, 120, 66, 255) : wood));
            Set(19, 15, new Color32(118, 74, 38, 255)); Set(24, 16, new Color32(118, 74, 38, 255));
            var leaf = new Color32(96, 140, 64, 255);
            Set(30, 15, Outline); Set(31, 14, Outline); Set(32, 14, Outline); Set(33, 15, Outline); Set(31, 16, Outline); Set(32, 16, Outline);
            Set(31, 15, leaf); Set(32, 15, new Color32(70, 110, 48, 255));
            for (int x = 0; x < 7; x++)
            {
                Set(36 + x, 14, Outline); Set(36 + x, 16, Outline);
                Set(36 + x, 15, x >= 5 ? new Color32(70, 120, 200, 255) : new Color32(170, 210, 230, 255));
            }
            Set(36, 15, Outline);
            var bark = new Color32(92, 64, 40, 255);
            for (int x = 0; x < 11; x++) { int y = 14 + (x > 5 ? 1 : 0); Set(45 + x, y, Outline); Set(45 + x, y + 1, bark); Set(45 + x, y + 2, Outline); }
            Set(49, 17, bark); Set(48, 17, Outline);

            atlas.SetPixels32(px);
            atlas.Apply(false, false);

            Sprite Cut(int x, int y, int sw, int sh) => Sprite.Create(atlas, new Rect(x, y, sw, sh), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            streakSprites = new[] { Cut(0, 0, 22, 12), Cut(24, 0, 14, 8), Cut(40, 0, 10, 6) };
            foamSprites = new[] { Cut(0, 14, 4, 2), Cut(6, 14, 3, 2), Cut(11, 14, 2, 1) };
            debrisSprites = new[] { Cut(16, 14, 12, 4), Cut(30, 14, 4, 3), Cut(36, 14, 7, 3), Cut(45, 14, 11, 4) };
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 d = flowDirection.sqrMagnitude > 0.0001f ? flowDirection.normalized : Vector2.left;
            Vector2 a = lineOrigin - d * halfLength, b = lineOrigin + d * halfLength;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(a + Vector2.up * halfWidth, b + Vector2.up * halfWidth);
            Gizmos.DrawLine(a - Vector2.up * halfWidth, b - Vector2.up * halfWidth);
            Gizmos.DrawLine(lineOrigin, lineOrigin + d * 1.5f);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(bridgeEdgeA, bridgeEdgeB);
        }
    }
}
