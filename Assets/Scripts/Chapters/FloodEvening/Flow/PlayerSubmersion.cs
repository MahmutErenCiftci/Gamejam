using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Sel suyunun Cenk'in bacaklarına gerçekten çıkması (P-WADE, 18 Eki). FloodWaterField'ın yalnız okunan durumunu kullanır
    /// (PlayerDepth, WaterVisible); sel mantığına, yavaşlamaya ve başarısızlığa dokunmaz.
    /// - Su çizgisi: derinlik → piksel yüksekliği (ayak noktasından yukarı): birikinti ayak bileği (~5 px), orta su baldır (~8 px),
    ///   derin su / yüksek sel dizler (~11–13 px; şortun altı). Değişim yumuşatılır ve tam piksele oturtulur (titreme yok: küçük salınımlar yok sayılır).
    /// - Çizginin altı: SpriteMask ile gizlenir (Cenk'in bütün sprite'ları, şok siluetı dahil, VisibleOutsideMask); aynı karenin
    ///   koyu, yarı saydam bir kopyası suyun altında soluk görünür (bulanık su).
    /// - Çizgide: önde 1 px açık yüzey çizgisi + koyu yarı saydam su gövdesi ve köpük pikselleri olan halkanın ön yayı,
    ///   arkada (Cenk'in arkasında) halkanın arka yayı. Yürürken halka 1 px dalgalanır, kenarlardan küçük sıçrama pikselleri kalkar.
    /// - Sudan çıkınca maske ve katmanlar kapanır, Cenk'in sprite'larının maske ayarı eski haline döner.
    /// Sahnede FloodWaterField varsa sahne yüklenince kendiliğinden eklenir (sahne dosyası değişmez).
    /// Derin suyun görünümü tehlikeyi anlatır: Cenk yavaşlar ve suya gömülür; suya girmek güvenli gösterilmez.
    /// </summary>
    [DefaultExecutionOrder(1100)] // CenkOutfit (1000) sprite'ı değiştirdikten sonra
    public class PlayerSubmersion : MonoBehaviour
    {
        private const float Ppu = 64f;
        private const int TexW = 40, TexH = 44, CenterX = 20, FeetRow = 4; // dokuda ayak noktası: x 20, satır 4

        [Header("Derinlik → su çizgisi (px, ayak noktasından yukarı)")]
        [Tooltip("PlayerDepth eşikleri (FloodWaterField: 0 kuru, ~0.32 derin)")]
        [SerializeField] private float[] depthKeys = { 0.015f, 0.06f, 0.16f, 0.30f, 0.45f };
        [Tooltip("Eşiklerdeki su çizgisi yüksekliği (px). Cenk karesi: ayakkabı 0–8, baldır 9–11, şort 11–19 px; ayak bileği ~5, baldır ~8, diz ~11–13")]
        [SerializeField] private float[] pixelKeys = { 2f, 5f, 8f, 11f, 13f };
        [SerializeField] private float riseSpeed = 16f;
        [SerializeField] private float fallSpeed = 28f;

        [Header("Görünüm (FE_FloodWater.shader renkleriyle uyumlu)")]
        [SerializeField] private Color deepColor = new Color(0.1f, 0.22f, 0.45f, 0.66f);
        [SerializeField] private Color midColor = new Color(0.22f, 0.43f, 0.68f, 0.82f);
        [SerializeField] private Color crestColor = new Color(0.62f, 0.8f, 0.95f, 0.92f);
        [SerializeField] private Color foamColor = new Color(0.86f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color underwaterTint = new Color(0.28f, 0.42f, 0.62f, 0.3f);

        private FloodWaterField water;
        private IsoPlayerController player;
        private SpriteRenderer body;
        private Transform root;
        private SpriteMask mask;
        private SpriteRenderer front, back, ghost;
        private SpriteRenderer feetFx; // FloodWaterField'ın eski ayak bandı (halka varken gizlenir, yoksa suyun altında açık çizgi kalır)
        private Texture2D frontTex, backTex;
        private Color32[] frontPx, backPx;
        private float smoothPx, depthLp;
        private int shownPx;
        private bool active;
        private float refreshTimer;
        private readonly List<SpriteRenderer> owned = new List<SpriteRenderer>();
        private readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> saved = new Dictionary<SpriteRenderer, SpriteMaskInteraction>();
        private readonly List<SpriteRenderer> scratch = new List<SpriteRenderer>();
        private Vector3 lastPos;

        private class Drop { public float x, y, vx, vy, life; }
        private readonly List<Drop> drops = new List<Drop>();
        private float dropAcc;

        /// <summary>Test: şu an gösterilen su çizgisi (px, ayak noktasından yukarı; 0 = suyun dışında).</summary>
        public int WaterlinePixels => shownPx;
        /// <summary>Test: maske ve katmanlar açık mı.</summary>
        public bool Active => active;

        // ---------------------------------------------------------------- kendiliğinden ekleme
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var w in FindObjectsByType<FloodWaterField>(FindObjectsInactive.Include))
            {
                if (w.gameObject.scene != scene) continue;
                if (FindAnyObjectByType<PlayerSubmersion>(FindObjectsInactive.Include) != null) return;
                var go = new GameObject("PlayerSubmersion");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<PlayerSubmersion>();
                return;
            }
        }

        // ---------------------------------------------------------------- kurulum
        private void Awake()
        {
            water = FindAnyObjectByType<FloodWaterField>(FindObjectsInactive.Include);
            player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (player != null) body = player.GetComponentInChildren<SpriteRenderer>(true);
            if (water == null || player == null || body == null) { enabled = false; return; }
            root = transform;
            var feetT = water.transform.Find("FeetWater");
            if (feetT != null) feetFx = feetT.GetComponent<SpriteRenderer>();

            var maskGo = new GameObject("WaterlineMask");
            maskGo.transform.SetParent(root, false);
            mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = MakeBlock();
            maskGo.SetActive(false);

            ghost = NewRenderer("Underwater", body.sharedMaterial);
            ghost.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            ghost.color = underwaterTint;

            frontTex = NewTex("WaterlineFront"); backTex = NewTex("WaterlineBack");
            frontPx = new Color32[TexW * TexH]; backPx = new Color32[TexW * TexH];
            // Sıralama noktası ayakta: ön katman 0.25 px önde, arka katman 0.25 px arkada (dokunun satırları yine tam piksele oturur).
            front = NewRenderer("WaterlineFront", body.sharedMaterial);
            front.sprite = Sprite.Create(frontTex, new Rect(0, 0, TexW, TexH), new Vector2(CenterX / (float)TexW, (FeetRow - 0.25f) / TexH), Ppu, 0, SpriteMeshType.FullRect);
            back = NewRenderer("WaterlineBack", body.sharedMaterial);
            back.sprite = Sprite.Create(backTex, new Rect(0, 0, TexW, TexH), new Vector2(CenterX / (float)TexW, (FeetRow + 0.25f) / TexH), Ppu, 0, SpriteMeshType.FullRect);
            front.spriteSortPoint = back.spriteSortPoint = SpriteSortPoint.Pivot;
            for (int i = 0; i < 14; i++) drops.Add(new Drop());
            lastPos = player.transform.position;
        }

        private SpriteRenderer NewRenderer(string n, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (mat != null) sr.sharedMaterial = mat;
            sr.enabled = false;
            owned.Add(sr);
            return sr;
        }

        private static Texture2D NewTex(string n)
        {
            return new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = n };
        }

        private static Sprite MakeBlock()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "WaterlineMaskBlock" };
            var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px); t.Apply(false);
            return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 1f), 4f); // 1 birim kare, pivot üst kenarın ortası
        }

        private void OnDisable() => Deactivate();

        private void OnDestroy()
        {
            Deactivate();
            if (frontTex != null) Destroy(frontTex);
            if (backTex != null) Destroy(backTex);
            if (front != null && front.sprite != null) Destroy(front.sprite);
            if (back != null && back.sprite != null) Destroy(back.sprite);
            if (mask != null && mask.sprite != null) { Destroy(mask.sprite.texture); Destroy(mask.sprite); }
        }

        // ---------------------------------------------------------------- kare
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool visible = body != null && body.enabled && body.gameObject.activeInHierarchy && water.WaterVisible && body.sprite != null;
            // Su verisi hücreden hücreye ±0.05 oynar: derinlik önce alçak geçiren süzgeçten geçer (yürürken çizgi zıplamasın)
            depthLp = Mathf.Lerp(depthLp, water.PlayerDepth, 1f - Mathf.Exp(-dt / 0.3f));
            if (water.PlayerDepth <= 0f && depthLp < 0.01f) depthLp = 0f;
            float target = visible ? DepthToPixels(depthLp) : 0f;
            smoothPx = Mathf.MoveTowards(smoothPx, target, (target > smoothPx ? riseSpeed : fallSpeed) * dt);
            if (!visible) smoothPx = 0f;
            // Histerezis: çizgi ancak 0.75 px'ten fazla kayınca bir piksel oynar (sığ kenarlarda titreme yok)
            if (smoothPx < 0.5f) shownPx = 0;
            else if (Mathf.Abs(smoothPx - shownPx) >= 0.75f) shownPx = Mathf.RoundToInt(smoothPx);

            Vector3 pos = player.transform.position;
            float speed = dt > 0f ? ((Vector2)(pos - lastPos)).magnitude / dt : 0f;
            lastPos = pos;
            bool moving = speed > 0.25f && player.Velocity.sqrMagnitude > 0.04f;

            if (shownPx <= 0) { Deactivate(); return; }
            Activate();
            if (feetFx != null) feetFx.enabled = false; // FloodWaterField her Update'te açar; çizimden önce burada kapanır

            refreshTimer -= dt;
            if (refreshTimer <= 0f) { refreshTimer = 0.25f; ApplyMaskInteraction(); }

            Transform bt = body.transform;
            Vector3 bp = bt.position;
            Vector3 scale = bt.lossyScale;
            float unitY = scale.y / Ppu;
            float lineY = bp.y + shownPx * unitY;

            // Maske: çizginin altını (ayakların 10 px altına kadar) kapatır
            var mt = mask.transform;
            mt.position = new Vector3(bp.x, lineY, bp.z);
            mt.localScale = new Vector3(1.6f * Mathf.Abs(scale.x), (shownPx + 10) * unitY, 1f);

            // Suyun altındaki soluk kopya (aynı kare)
            ghost.sprite = body.sprite;
            ghost.flipX = body.flipX; ghost.flipY = body.flipY;
            ghost.transform.SetPositionAndRotation(bp, bt.rotation);
            ghost.transform.localScale = scale;
            ghost.sortingLayerID = body.sortingLayerID; ghost.sortingOrder = body.sortingOrder;
            ghost.spriteSortPoint = body.spriteSortPoint;

            // Halka katmanları: ayak noktasına göre (sıralama Cenk'le aynı noktadan)
            front.transform.position = new Vector3(bp.x, bp.y - 0.25f * unitY, bp.z);
            back.transform.position = new Vector3(bp.x, bp.y + 0.25f * unitY, bp.z);
            front.transform.localScale = back.transform.localScale = new Vector3(Mathf.Abs(scale.x), scale.y, 1f);
            front.sortingLayerID = back.sortingLayerID = body.sortingLayerID;
            front.sortingOrder = back.sortingOrder = body.sortingOrder;

            UpdateDrops(dt, moving);
            Draw(moving);
        }

        private float DepthToPixels(float depth)
        {
            if (depthKeys == null || pixelKeys == null || depthKeys.Length == 0 || depth < depthKeys[0]) return 0f;
            int n = Mathf.Min(depthKeys.Length, pixelKeys.Length);
            for (int i = 1; i < n; i++)
                if (depth < depthKeys[i])
                    return Mathf.Lerp(pixelKeys[i - 1], pixelKeys[i], Mathf.InverseLerp(depthKeys[i - 1], depthKeys[i], depth));
            return pixelKeys[n - 1];
        }

        private void Activate()
        {
            if (active) return;
            active = true;
            mask.gameObject.SetActive(true);
            front.enabled = back.enabled = ghost.enabled = true;
            refreshTimer = 0f;
        }

        private void Deactivate()
        {
            if (!active && saved.Count == 0) return;
            active = false;
            if (mask != null) mask.gameObject.SetActive(false);
            foreach (var sr in owned) if (sr != null) sr.enabled = false;
            foreach (var kv in saved) if (kv.Key != null) kv.Key.maskInteraction = kv.Value;
            saved.Clear();
            foreach (var d in drops) d.life = 0f;
        }

        // Cenk'in bütün sprite'ları (çanta yok: tek sprite; şok siluetı / arklar çocuk obje) suyun altında görünmez
        private void ApplyMaskInteraction()
        {
            player.GetComponentsInChildren(true, scratch);
            foreach (var sr in scratch)
            {
                if (owned.Contains(sr) || saved.ContainsKey(sr)) continue;
                saved[sr] = sr.maskInteraction;
                sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            }
            scratch.Clear();
        }

        // ---------------------------------------------------------------- çizim
        private int HalfWidth(bool moving)
        {
            int a = 9 + Mathf.RoundToInt(shownPx * 0.2f); // ayak bileği ~10, diz ~13 px
            if (moving && Mathf.Sin(Time.time * 11f) > 0.3f) a += 1; // yürürken halka dalgalanır
            return a;
        }

        private void Draw(bool moving)
        {
            System.Array.Clear(frontPx, 0, frontPx.Length);
            System.Array.Clear(backPx, 0, backPx.Length);
            int L = shownPx;
            int a = HalfWidth(moving);
            float b = Mathf.Max(2f, a * 0.42f);
            if (moving) b += Mathf.Sin(Time.time * 13f) > 0f ? 0.6f : 0f;
            int tick = Mathf.FloorToInt(Time.time * 8f);
            int surf = FeetRow + L; // dokuda su çizgisinin ilk görünen satırı

            for (int c = -a; c < a; c++)
            {
                float xn = (c + 0.5f) / a;
                float h = b * Mathf.Sqrt(Mathf.Max(0f, 1f - xn * xn));
                int hl = Mathf.Max(1, Mathf.RoundToInt(h));
                int hu = Mathf.Max(1, Mathf.RoundToInt(h * 0.8f));
                int x = CenterX + c;
                bool edge = Mathf.Abs(c + 0.5f) > a - 2.5f;

                // ön yarı (Cenk'in önünde): yüzey çizgisi + koyu gövde + ön yay
                for (int k = 1; k <= hl; k++)
                {
                    int y = surf - k;
                    Color col;
                    if (k == hl) col = Hash(x, tick) > 0.72f ? foamColor : crestColor;  // ön yay (köpüklü)
                    else if (k == 1) col = edge ? midColor : crestColor;                 // bacakları kesen açık yüzey çizgisi
                    else if (k == 2) col = midColor;
                    else col = deepColor;
                    Put(frontPx, x, y, col);
                }
                // arka yarı (Cenk'in arkasında): yüzey + arka yay
                for (int k = 0; k < hu; k++)
                {
                    int y = surf + k;
                    Color col = k == hu - 1 ? (Hash(x + 7, tick) > 0.8f ? foamColor : crestColor) : midColor;
                    if (k < hu - 1) col.a *= 0.75f;
                    Put(backPx, x, y, col);
                }
            }

            // yürürken dışta ikinci, kesik halka (genişleyen dalga)
            if (moving && L >= 4)
            {
                int ao = a + 2 + (tick & 1);
                float bo = ao * 0.42f;
                for (int c = -ao; c < ao; c++)
                {
                    if (((c + tick) & 3) == 0) continue;
                    float xn = (c + 0.5f) / ao;
                    int h = Mathf.Max(1, Mathf.RoundToInt(bo * Mathf.Sqrt(Mathf.Max(0f, 1f - xn * xn))));
                    var col = crestColor; col.a *= 0.55f;
                    Put(frontPx, CenterX + c, surf - h - 1, col);
                    if (Mathf.Abs(xn) > 0.55f) Put(backPx, CenterX + c, surf + h, col);
                }
            }

            foreach (var d in drops)
                if (d.life > 0f) Put(frontPx, Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.y), d.life > 0.15f ? foamColor : crestColor);

            frontTex.SetPixels32(frontPx); frontTex.Apply(false);
            backTex.SetPixels32(backPx); backTex.Apply(false);
        }

        private void UpdateDrops(float dt, bool moving)
        {
            int L = shownPx;
            if (moving && L >= 5 && dt > 0f)
            {
                dropAcc += dt * (8f + L * 0.6f);
                while (dropAcc >= 1f)
                {
                    dropAcc -= 1f;
                    Drop d = null; foreach (var x in drops) if (x.life <= 0f) { d = x; break; }
                    if (d == null) break;
                    float side = Random.value < 0.5f ? -1f : 1f;
                    int a = HalfWidth(false);
                    d.x = CenterX + side * Random.Range(a * 0.35f, a * 0.95f);
                    d.y = FeetRow + L - Random.Range(0f, 2f);
                    d.vx = side * Random.Range(4f, 14f);
                    d.vy = Random.Range(22f, 40f);
                    d.life = Random.Range(0.25f, 0.4f);
                }
            }
            foreach (var d in drops)
            {
                if (d.life <= 0f) continue;
                d.life -= dt;
                d.vy -= 160f * dt;
                d.x += d.vx * dt; d.y += d.vy * dt;
                if (d.y < FeetRow + L - 3) d.life = 0f; // yüzeye geri düştü
            }
        }

        private static void Put(Color32[] px, int x, int y, Color col)
        {
            if (x < 0 || y < 0 || x >= TexW || y >= TexH) return;
            px[y * TexW + x] = col;
        }

        private static float Hash(int x, int t)
        {
            uint h = (uint)(x * 374761393 + t * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
