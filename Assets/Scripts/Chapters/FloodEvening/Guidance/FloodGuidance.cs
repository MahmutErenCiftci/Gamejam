using System.Collections.Generic;
using System.Reflection;
using AfetUyanisi.Chapters.Flood;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// SEL YÖNLENDİRMESİ (P-GUIDE, 18 Eki) — deprem yönlendirmesinin (EarthquakeGuidance) sel karşılığı: oyuncuya NEREYE
    /// gideceğini gösterir, kararı asla vermez.
    ///  • FloodSchool: okul kapısı → köprü başı (karar anında HİÇBİR ipucu yok) → seçimden sonra köprünün ucu → kaldırım
    ///    (su kenarını sıyırmadan) → çıkış.
    ///  • FloodEvening: Cenk'in çevresinde, rota çapalarına (başlangıç → köşeler → kavşak) doğru dönen piksel ok. Kavşakta
    ///    (KISA YOL / UZUN YOL) ok YOK ve hiçbir tercih gösterilmez: oyuncu bir yola girince o yolun sıradaki çapasını gösterir.
    ///    Kısa yolda kapalı bariyerden sonra kavşağa geri, sonra uzun yol. Ağaç düşünce dalın [E] noktası, sonra avlu, sonra
    ///    apartman finalinin sırası (kilit → posta kutusu → kilit/kapı). Rota çapaları elektrik bölgelerinin (Zone_1..3)
    ///    yanından geçer: ok düz çizgide tehlikeye değil, rota boyunca bir sonraki noktaya bakar.
    ///  • Kullanılabilir [E] noktalarının (dal, posta kutusu, kilit, tabela) altında yumuşak nabız atan izometrik halka.
    ///  • Oyuncu idleSeconds sn ilerlemezse ok bir kez büyüyüp küçülür.
    /// Diyalog, kart, sinematik (ağaç düşüşü, sele kapılma), mini oyun (ritim, PIN, mektup), duraklatma ve sonuç ekranında
    /// hepsi gizlidir. Sahne düzenlemesi gerekmez: FloodSchool / FloodEvening yüklenince kendini ekler. Süreye dokunmaz.
    /// Görev HUD'unda "ŞİMDİ" satırı yok (sel görev şeridi desteklemiyor), bu yüzden eklenmedi.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloodGuidance : MonoBehaviour
    {
        [SerializeField] private int sortingOrder = 45;
        [Tooltip("Ok, Cenk'in merkezinden bu kadar referans pikseli uzakta döner")]
        [SerializeField] private float ringRadius = 30f;
        [Tooltip("Okların tam sayı büyütmesi (piksel net kalır)")]
        [SerializeField] private int arrowScale = 2;
        [Tooltip("Rota üzerinde ileri bakış (ızgara birimi)")]
        [SerializeField] private float lookAhead = 2.4f;
        [SerializeField] private float bounceHeight = 3f;
        [SerializeField] private float bouncePeriod = 0.7f;
        [SerializeField] private float idleSeconds = 12f;
        [SerializeField] private float idlePulseScale = 1.75f;
        [SerializeField] private float idlePulseSeconds = 0.7f;
        [Tooltip("Oyuncuya bu zemin uzaklığındaki kullanılabilir [E] noktalarının altında halka")]
        [SerializeField] private float glowRadius = 3.2f;
        [SerializeField] private float glowPeriod = 1.4f;

        // ---------------- Rota (ızgara: dünya x = (gx-gy)/2, y = (gx+gy)/4; tools/sel_aksam_harita/layout.js, map.js v3) ----------------
        // Başlangıç → A (Zone_1'in arka şeridinden) → köşe → B → köşe → C → kavşak merkezi.
        // A→B köşesi (14,3.4): direk sırasının (gy 4.55, Pole_A4 gx 12.6) altından döner, oku izleyen direğe çarpmaz.
        private static readonly Vector2[] LegStart = { V(-16f, 2.5f), V(-7.5f, 2.6f), V(1.0f, 2.8f), V(3.6f, 4.0f), V(8.9f, 4.0f), V(11.8f, 3.2f), V(14.0f, 3.4f), V(13.5f, 13.0f), V(15.0f, 14.5f), V(27f, 14.5f) };
        // Kısa yol: kavşak → D → köşe → E → bariyer (BarrierZone gx 31.6..33.6).
        private static readonly Vector2[] LegShort = { V(27f, 14.5f), V(27.5f, 10.5f), V(27.5f, 5.5f), V(28.6f, 4.5f), V(32.2f, 4.5f) };
        // Kısa yoldan dönüş: bariyer → köşe → D → kavşak merkezi.
        private static readonly Vector2[] LegBack = { V(32.2f, 4.5f), V(28.6f, 4.5f), V(27.5f, 5.5f), V(27.5f, 10.5f), V(27f, 14.5f) };
        // Uzun yol: kavşak → F → köşe → G (Zone_2'nin ön şeridinden) → köşe → H (Zone_3'ün sol şeridinden) → köşe → I → TreeStopSpot.
        private static readonly Vector2[] LegLong = { V(27f, 14.5f), V(27.5f, 19.0f), V(27.5f, 27.2f), V(28.8f, 28.5f), V(37.6f, 28.5f), V(38.9f, 27.1f), V(41.6f, 27.1f), V(43.4f, 26.0f), V(43.5f, 24.4f), V(42.3f, 23.4f), V(42.3f, 20.2f), V(43.5f, 19.2f), V(43.5f, 16.8f), V(44.8f, 15.5f), V(47.6f, 15.5f) };
        // Ağaç açıldıktan sonra: ön kenardaki geçit → I'nin sonu → köşe → J → avlu (SafeSpot).
        private static readonly Vector2[] LegFinal = { V(47.6f, 15.5f), V(49.6f, 13.7f), V(53.0f, 13.6f), V(56.2f, 13.7f), V(57.5f, 12.4f), V(57.5f, 1.0f), V(58f, -4.6f) };

        // Çalışma anında sahne çapalarıyla (Fork, TreeStopSpot, SafeSpot) düzeltilmiş kopyalar: harita değişirse uçlar kaymaz.
        private Vector2[] legStart, legShort, legBack, legLong, legFinal;

        private static Vector2 V(float gx, float gy) => new Vector2(gx, gy);
        private static Vector2 GridToWorld(Vector2 g) => new Vector2((g.x - g.y) * 0.5f, (g.x + g.y) * 0.25f);
        private static Vector2 WorldToGrid(Vector2 w) => new Vector2(2f * w.y + w.x, 2f * w.y - w.x);
        private static bool InRect(Vector2 g, float x0, float x1, float y0, float y1) => g.x >= x0 && g.x < x1 && g.y >= y0 && g.y < y1;

        // Ok çizimi (13x11, aşağı bakar) — EarthquakeGuidance ile aynı. X = koyu kontur, Y = amber, H = parlak, D = gölge
        private static readonly string[] DownRows =
        {
            "....XXXXX....",
            "....XHYDX....",
            "....XHYDX....",
            "....XHYDX....",
            "XXXXXHYDXXXXX",
            ".XHHHHYYYYDX.",
            "..XHHYYYYDX..",
            "...XHYYYDX...",
            "....XHYDX....",
            ".....XYX.....",
            "......X......",
        };

        private static readonly Color Ink = Hex(0x1B1F2E), Amber = Hex(0xFFC23C), Hi = Hex(0xFFF0A8), Shade = Hex(0xE07E1C);

        private enum Mode { None, School, Evening }

        private Mode mode;
        private FloodSchoolController school;
        private FloodEveningController evening;
        private IsoPlayerController player;
        private AfetUyanisi.Dialogue.DialogueRunner dialogueRunner;
        private FloodEveningResultView resultView;
        private FloodWaterField water;
        private ApartmentFinale finale;
        private DoorLockInteraction doorLock;
        private MailboxInteraction mailbox;
        private BranchObstacle branch;
        private readonly List<IFloodEveningInteraction> modules = new List<IFloodEveningInteraction>();
        private readonly HashSet<string> openPanels = new HashSet<string>();
        private FieldInfo stepField;

        // Okul çapaları
        private Collider2D gateZone, bridgeHead, bridgeEnd, exitZone;
        private Interactable schoolSign;
        private int schoolStage;

        // UI
        private Canvas canvas;
        private RectTransform canvasRt, ringRt, downRt;
        private Image ringImg;
        private CanvasGroup ringGroup, downGroup;
        private readonly Sprite[] dirSprites = new Sprite[16];
        private Sprite glowSprite;
        private readonly List<Image> glowPool = new List<Image>();

        // Hedef
        private Vector2[] leg;
        private string legName;
        private Vector3? goalWorld;     // sıçrayan ok bunun üstünde (ekrandaysa)
        private Interactable goalPoint; // menzildeyse ok gizlenir
        private Vector2 aimWorld;       // halka okunun baktığı dünya noktası
        private bool hasAim;
        private float clock, idleT, pulseT = -1f, bestRemain = float.MaxValue;
        private string lastKey;

        // --- Test / sürücü için okunur durum ---
        public bool Active { get; private set; }
        public bool ArrowVisible { get; private set; }
        public bool RingVisible { get; private set; }
        public bool BounceVisible { get; private set; }
        public string TargetName { get; private set; } = "";
        /// <summary>Okun gösterdiği dünya noktası (halka oku: rota üstünde ileri nokta; sıçrayan ok: hedef).</summary>
        public Vector2 AimWorld => aimWorld;
        public Interactable GoalPoint => goalPoint;
        public int GlowCount { get; private set; }
        public int IdlePulses { get; private set; }

        // =====================================================================
        // Kendini sahneye ekleme
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryAttach(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode) => TryAttach(scene);

        private static void TryAttach(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            bool flood = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<FloodGuidance>(true) != null) return;
                if (root.GetComponentInChildren<FloodEveningController>(true) != null || root.GetComponentInChildren<FloodSchoolController>(true) != null) flood = true;
            }
            if (!flood) return;
            var go = new GameObject("FloodGuidance");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<FloodGuidance>();
        }

        // =====================================================================
        // Kurulum
        // =====================================================================

        private void Start()
        {
            var sc = gameObject.scene;
            school = FindInScene<FloodSchoolController>(sc);
            evening = FindInScene<FloodEveningController>(sc);
            mode = evening != null ? Mode.Evening : school != null ? Mode.School : Mode.None;
            if (mode == Mode.None) { enabled = false; return; }
            player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            dialogueRunner = FindAnyObjectByType<AfetUyanisi.Dialogue.DialogueRunner>(FindObjectsInactive.Include);
            resultView = FindAnyObjectByType<FloodEveningResultView>(FindObjectsInactive.Include);
            if (mode == Mode.Evening)
            {
                water = FindAnyObjectByType<FloodWaterField>(FindObjectsInactive.Include);
                finale = FindAnyObjectByType<ApartmentFinale>(FindObjectsInactive.Include);
                doorLock = FindAnyObjectByType<DoorLockInteraction>(FindObjectsInactive.Include);
                mailbox = FindAnyObjectByType<MailboxInteraction>(FindObjectsInactive.Include);
                branch = FindAnyObjectByType<BranchObstacle>(FindObjectsInactive.Include);
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                    if (mb is IFloodEveningInteraction fi && fi.Point != null) modules.Add(fi);
                FloodEveningHub.MinigameOpened += OnOpened;
                FloodEveningHub.MinigameClosed += OnClosed;
                ResolveLegs(sc);
            }
            else
            {
                gateZone = ZoneOf(sc, "SchoolGate");
                bridgeHead = ZoneOf(sc, "BridgeHead");
                bridgeEnd = ZoneOf(sc, "BridgeEnd");
                exitZone = ZoneOf(sc, "ExitZone");
                var sign = Named(sc, "Sign");
                schoolSign = sign != null ? sign.GetComponent<Interactable>() : null;
                stepField = typeof(ChapterControllerBase).GetField("currentStep", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            Build();
            Debug.Log($"[FE-GUIDE] yönlendirme açıldı ({mode}, {modules.Count} [E] modülü)", this);
        }

        private void OnDestroy()
        {
            FloodEveningHub.MinigameOpened -= OnOpened;
            FloodEveningHub.MinigameClosed -= OnClosed;
        }

        private void OnOpened(string id) { if (!string.IsNullOrEmpty(id)) openPanels.Add(id); }
        private void OnClosed(string id) { if (!string.IsNullOrEmpty(id)) openPanels.Remove(id); }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static GameObject Named(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == objectName) return t.gameObject;
            return null;
        }

        /// <summary>
        /// Rota uçlarını sahnedeki çapalara bağlar (Unused altındakiler hariç): kavşak = "Fork", ağaç durağı = "TreeStopSpot",
        /// avlu = "SafeSpot". Çapa yoksa çizimdeki değer kalır. Ara köşeler haritanın sokak şeritlerinden gelir.
        /// </summary>
        private void ResolveLegs(Scene sc)
        {
            legStart = (Vector2[])LegStart.Clone(); legShort = (Vector2[])LegShort.Clone(); legBack = (Vector2[])LegBack.Clone();
            legLong = (Vector2[])LegLong.Clone(); legFinal = (Vector2[])LegFinal.Clone();
            string log = "";
            if (AnchorGrid(sc, "Fork", out var fork))
            {
                legStart[legStart.Length - 1] = legShort[0] = legBack[legBack.Length - 1] = legLong[0] = fork;
                log += $" Fork={fork:F1}";
            }
            if (AnchorGrid(sc, "TreeStopSpot", out var tree))
            {
                legLong[legLong.Length - 1] = legFinal[0] = tree;
                log += $" TreeStopSpot={tree:F1}";
            }
            if (AnchorGrid(sc, "SafeSpot", out var safe))
            {
                legFinal[legFinal.Length - 1] = safe;
                log += $" SafeSpot={safe:F1}";
            }
            Debug.Log("[FE-GUIDE] rota çapaları (ızgara):" + (log.Length > 0 ? log : " yok, çizimdeki değerler"), this);
        }

        private static bool AnchorGrid(Scene scene, string objectName, out Vector2 grid)
        {
            grid = default;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != objectName || IsUnused(t)) continue;
                    Vector2 w = t.position;
                    var col = t.GetComponent<Collider2D>();
                    if (col != null && col.bounds.size.sqrMagnitude > 0.0001f) w = col.bounds.center;
                    else if (col is PolygonCollider2D poly && poly.points.Length > 0)
                    {
                        Vector2 sum = Vector2.zero;
                        foreach (var p in poly.points) sum += p;
                        w = t.TransformPoint(sum / poly.points.Length + poly.offset);
                    }
                    grid = WorldToGrid(w);
                    return true;
                }
            return false;
        }

        private static bool IsUnused(Transform t)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == "Unused") return true;
            return false;
        }

        private static Collider2D ZoneOf(Scene scene, string objectName)
        {
            var go = Named(scene, objectName);
            return go != null ? go.GetComponent<Collider2D>() : null;
        }

        private void Build()
        {
            var go = new GameObject("FE_GuidanceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            go.AddComponent<FePixelCanvasScaler>();
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            canvasRt = (RectTransform)go.transform;

            glowSprite = BuildGlowSprite();
            for (int i = 0; i < 16; i++) dirSprites[i] = BuildDirSprite(i);

            ringRt = NewImage("RouteArrow", dirSprites[0], out ringImg);
            ringGroup = ringRt.gameObject.AddComponent<CanvasGroup>();
            ringGroup.alpha = 0f;
            downRt = NewImage("TargetArrow", BuildDownSprite(), out _);
            downRt.pivot = new Vector2(0.5f, 0f);
            downGroup = downRt.gameObject.AddComponent<CanvasGroup>();
            downGroup.alpha = 0f;
        }

        private RectTransform NewImage(string name, Sprite sprite, out Image img)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            return rt;
        }

        // ---------------- Piksel çizimler ----------------

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        private static Sprite MakeSprite(Color[] px, int w, int h, string name, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 1f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildDownSprite()
        {
            int w = DownRows[0].Length, h = DownRows.Length;
            var px = new Color[w * h];
            for (int row = 0; row < h; row++)
                for (int col = 0; col < w; col++)
                {
                    char c = DownRows[row][col];
                    px[(h - 1 - row) * w + col] = c == 'X' ? Ink : c == 'Y' ? Amber : c == 'H' ? Hi : c == 'D' ? Shade : Color.clear;
                }
            return MakeSprite(px, w, h, "FE_GuideArrowDown", new Vector2(0.5f, 0f));
        }

        /// <summary>16 yönden biri için ok (17x17): ok her açıda piksel piksel çizilir (döndürülmez), 1 px koyu kontur.</summary>
        private static Sprite BuildDirSprite(int index)
        {
            const int n = 17;
            float a = index * Mathf.PI / 8f;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), p = new Vector2(-d.y, d.x);
            var fill = new int[n * n]; // 0 boş, 1 amber, 2 parlak, 3 gölge
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 q = new Vector2(x - 8f, y - 8f);
                    float u = Vector2.Dot(q, d), v = Vector2.Dot(q, p);
                    bool head = u >= -0.5f && u <= 6.6f && Mathf.Abs(v) <= (6.6f - u) * 0.85f;
                    bool shaft = u >= -6.2f && u < 0f && Mathf.Abs(v) <= 1.6f;
                    if (!head && !shaft) continue;
                    fill[y * n + x] = v > 0.9f ? 2 : v < -0.9f ? 3 : 1;
                }
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int f = fill[y * n + x];
                    if (f == 1) px[y * n + x] = Amber;
                    else if (f == 2) px[y * n + x] = Hi;
                    else if (f == 3) px[y * n + x] = Shade;
                    else
                    {
                        bool edge = false;
                        for (int k = 0; k < 4 && !edge; k++)
                        {
                            int xx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), yy = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                            edge = xx >= 0 && yy >= 0 && xx < n && yy < n && fill[yy * n + xx] != 0;
                        }
                        px[y * n + x] = edge ? Ink : Color.clear;
                    }
                }
            return MakeSprite(px, n, n, "FE_GuideArrow_" + index, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Zemin halkası (izometrik eşkenar dörtgen, 2:1): amber halka + koyu dış kontur.</summary>
        private static Sprite BuildGlowSprite()
        {
            const int w = 25, h = 13;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x - 12f) / 12f + Mathf.Abs(y - 6f) / 6f;
                    px[y * w + x] = d <= 0.70f ? Color.clear : d <= 0.88f ? Amber : d <= 1.0f ? Hi : d <= 1.12f ? Ink : Color.clear;
                }
            return MakeSprite(px, w, h, "FE_GuideGlow", new Vector2(0.5f, 0.5f));
        }

        // =====================================================================
        // Kare güncellemesi
        // =====================================================================

        private void LateUpdate()
        {
            if (canvas == null || player == null) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;

            bool can = CanGuide();
            leg = null; legName = null; goalWorld = null; goalPoint = null; hasAim = false;
            string key = "";
            if (mode == Mode.Evening) key = ChooseEvening(can);
            else key = ChooseSchool(can);
            Active = can && (hasAim || goalWorld.HasValue);

            if (key != lastKey)
            {
                lastKey = key;
                idleT = 0f;
                bestRemain = float.MaxValue;
                TargetName = key;
                if (!string.IsNullOrEmpty(key)) Debug.Log($"[FE-GUIDE] ok → {key}", this);
            }
            UpdateIdle(dt);
            UpdateArrows(dt);
            UpdateGlows(can);
        }

        private bool CanGuide()
        {
            if (!player.isActiveAndEnabled || !player.CanMove || Time.timeScale <= 0f) return false;
            if (AfetUyanisi.UI.PauseMenu.IsPaused) return false;
            if (dialogueRunner != null && dialogueRunner.IsBusy) return false;
            if (resultView != null && resultView.IsShown) return false;
            if (Camera.main == null) return false;
            if (mode == Mode.Evening)
            {
                if (!FloodEveningHub.FreeRoam || openPanels.Count > 0) return false;
                var s = evening.State;
                if (s == FloodEveningState.Intro || s == FloodEveningState.Warning || s == FloodEveningState.Success || s == FloodEveningState.Failed) return false;
                if (finale != null && (finale.Entering || finale.IsComplete)) return false;
            }
            else if (school.Leaving) return false;
            return true;
        }

        // ---------------- FloodEvening hedefi ----------------

        private string ChooseEvening(bool can)
        {
            var s = evening.State;
            Vector2 g = WorldToGrid(player.transform.position);

            if (s == FloodEveningState.Finale)
            {
                if (finale == null) return "";
                Interactable p = null; string key = "";
                if (!finale.LockChecked) { p = doorLock != null ? doorLock.Point : null; key = "Finale/Kilit"; }
                else if (!finale.PinFound) { p = mailbox != null ? mailbox.Point : null; key = "Finale/PostaKutusu"; }
                else if (!finale.HandleTurned) { p = doorLock != null ? doorLock.Point : null; key = "Finale/KilitKapı"; }
                if (p != null) SetGoalPoint(p);
                return key;
            }
            if (s == FloodEveningState.FinalApproach)
            {
                var fin = legFinal ?? LegFinal;
                Follow(fin, "Avlu", g);
                if (InRect(g, 53f, 63f, -8f, 2f)) SetGoalWorld(GridToWorld(fin[fin.Length - 1]));
                return "Rota/Avlu";
            }
            if (s == FloodEveningState.Obstacle)
            {
                if (branch != null && !branch.IsCleared && branch.Point != null)
                {
                    SetGoalPoint(branch.Point);
                    return "Engel/Dal";
                }
                Follow(legFinal ?? LegFinal, "Avlu", g);
                return "Rota/Avlu";
            }
            if (s == FloodEveningState.LongRoad)
            {
                Follow(legLong ?? LegLong, "UzunYol", g);
                return "Rota/UzunYol";
            }
            // RouteChoice: kavşağa kadar rota; kavşakta tercih yok.
            bool shortClosed = evening.ShortRoadTried || (water != null && water.IsClosed("short_road"));
            bool inShort = InRect(g, 25f, 30f, -1f, 10f) || InRect(g, 30f, 36f, -1f, 7.5f);
            bool inFork = InRect(g, 23f, 31f, 10f, 19f);
            bool inF = InRect(g, 24.5f, 30.5f, 19f, 32f);
            if (inShort)
            {
                if (shortClosed) { Follow(legBack ?? LegBack, "Kavşak", g); return "Rota/KavşağaDönüş"; }
                Follow(legShort ?? LegShort, "KısaYol", g);
                return "Rota/KısaYol";
            }
            if (inF) { Follow(legLong ?? LegLong, "UzunYol", g); return "Rota/UzunYol"; }
            if (inFork)
            {
                if (shortClosed) { Follow(legLong ?? LegLong, "UzunYol", g); return "Rota/UzunYol(kısa kapalı)"; }
                return "Kavşak/Tercih"; // ok yok: karar oyuncunun
            }
            Follow(legStart ?? LegStart, "Kavşak", g);
            return "Rota/Kavşak";
        }

        /// <summary>
        /// Rota çizgisini izler: oyuncunun çizgi üstündeki en yakın noktasından lookAhead kadar ilerideki noktaya bakar
        /// (köşeleri rota boyunca döner; düz çizgide suya/elektriğe kesmez). Oyuncu çizgiden uzaksa önce çizgiye döner.
        /// </summary>
        private void Follow(Vector2[] pts, string name, Vector2 g)
        {
            leg = pts; legName = name;
            float best = float.MaxValue, bestS = 0f, acc = 0f, total = 0f;
            for (int i = 0; i + 1 < pts.Length; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], ab = b - a;
                float len = ab.magnitude;
                float t = len > 0.0001f ? Mathf.Clamp01(Vector2.Dot(g - a, ab) / (len * len)) : 0f;
                float d = Vector2.Distance(g, a + ab * t);
                if (d <= best + 0.05f) { best = d; bestS = acc + t * len; } // eşitlikte ileriki parça
                acc += len;
            }
            // Çizgiden saptıkça ileri bakış kısalır (önce rotaya döner); ok bir sonraki köşeyi en fazla 0.5 geçer, yani
            // köşenin iç tarafını (direk, duvar, elektrik şeridi) kesip geçmez.
            float ahead = best > 1.6f ? 0.6f : Mathf.Clamp(lookAhead - 3.5f * best, 0.5f, lookAhead);
            float sAim = Mathf.Min(total, bestS + ahead);
            float vertexS = 0f;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                vertexS += Vector2.Distance(pts[i], pts[i + 1]);
                if (vertexS > bestS + 0.05f) { sAim = Mathf.Min(sAim, vertexS + 0.5f); break; }
            }
            Vector2 aim = PointAt(pts, sAim);
            float remain = total - bestS;
            if (remain < bestRemain - 0.75f) { bestRemain = remain; idleT = 0f; }
            if (remain < 0.6f && best < 1.2f) return; // bacağın sonuna varıldı (ör. kavşak): ok yok
            aimWorld = GridToWorld(aim);
            hasAim = true;
        }

        private static Vector2 PointAt(Vector2[] pts, float s)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                float len = Vector2.Distance(pts[i], pts[i + 1]);
                if (s <= len) return Vector2.Lerp(pts[i], pts[i + 1], len > 0.0001f ? s / len : 0f);
                s -= len;
            }
            return pts[pts.Length - 1];
        }

        private void SetGoalPoint(Interactable p)
        {
            goalPoint = p;
            goalWorld = p.PromptAnchor.position + p.PromptOffset + new Vector3(0f, 0.2f, 0f);
            aimWorld = p.transform.position;
            hasAim = true;
            float d = player.GroundDistanceTo(p.transform.position);
            if (d < bestRemain - 0.5f) { bestRemain = d; idleT = 0f; }
        }

        private void SetGoalWorld(Vector2 world)
        {
            goalWorld = new Vector3(world.x, world.y + 0.9f, 0f);
        }

        // ---------------- FloodSchool hedefi ----------------

        private string ChooseSchool(bool can)
        {
            string step = stepField != null ? stepField.GetValue(school) as string : null;
            Vector2 pos = player.transform.position;
            // Aşamalar yalnız ileri gider: 0 kapı, 1 köprü başı, 2 karar (ipucu yok), 3 köprü ucu, 4 köprüden kaldırıma iniş, 5 çıkış.
            if (schoolStage == 0 && gateZone != null && !gateZone.OverlapPoint(pos)) schoolStage = 1;
            if (schoolStage == 0 && gateZone == null) schoolStage = 1;
            if (schoolStage <= 1 && bridgeHead != null && bridgeHead.OverlapPoint(pos)) schoolStage = 2;
            if (schoolStage == 2 && (step == "FS_BRIDGE" || step == "FS_HOME")) schoolStage = 3;
            if (schoolStage == 3 && (step == "FS_HOME" || (bridgeEnd != null && bridgeEnd.OverlapPoint(pos)))) schoolStage = 4;
            // Köprü ucundan çıkışa düz çizgi karşı kaldırımın su kenarını (WaterEdge_East) sıyırır: önce köprü ekseninde
            // biraz daha ilerideki kaldırım noktası (bankPoint), oraya varınca/geçince çıkış.
            Vector2 bankPoint = Vector2.zero, bridgeDir = Vector2.zero;
            bool hasBank = bridgeHead != null && bridgeEnd != null;
            if (hasBank)
            {
                bridgeDir = ((Vector2)bridgeEnd.bounds.center - (Vector2)bridgeHead.bounds.center).normalized;
                bankPoint = (Vector2)bridgeEnd.bounds.center + bridgeDir * 0.3f;
            }
            if (schoolStage == 4 && (!hasBank || Vector2.Distance(pos, bankPoint) < 0.3f || Vector2.Dot(pos - bankPoint, bridgeDir) > 0f)) schoolStage = 5;

            switch (schoolStage)
            {
                case 0:
                {
                    // Okul kapısı: kapı alanının köprü başına bakan kenarı.
                    Vector2 toward = bridgeHead != null ? (Vector2)bridgeHead.bounds.center : pos + Vector2.right;
                    Vector2 gate = ExitPointOf(gateZone, pos, toward);
                    SchoolAim(gate, "Okul/Kapı");
                    return "Okul/Kapı";
                }
                case 1:
                    if (bridgeHead == null) return "";
                    SchoolAim(bridgeHead.bounds.center, "Okul/KöprüBaşı");
                    return "Okul/KöprüBaşı";
                case 2:
                    return "Okul/Karar"; // köprü başında karar: hiçbir ipucu yok
                case 3:
                    if (bridgeEnd == null) return "";
                    SchoolAim(bridgeEnd.bounds.center, "Okul/KöprüUcu");
                    return "Okul/KöprüUcu";
                case 4:
                    // Yalnız halka oku (sıçrayan ok çıkışın üstünde dursa oyuncu kestirip suya yürürdü).
                    aimWorld = bankPoint;
                    hasAim = true;
                    {
                        float d = Vector2.Distance(pos, bankPoint);
                        if (d < bestRemain - 0.5f) { bestRemain = d; idleT = 0f; }
                    }
                    return "Okul/KöprüİnişKaldırım";
                case 5:
                    if (exitZone == null) return "";
                    SchoolAim(exitZone.bounds.center, "Okul/Çıkış");
                    return "Okul/Çıkış";
            }
            return "";
        }

        /// <summary>Kapı alanından dışarı çıkılan nokta: içerden hedefe doğru ışın, alanın sınırı.</summary>
        private static Vector2 ExitPointOf(Collider2D zone, Vector2 from, Vector2 toward)
        {
            if (zone == null) return toward;
            Vector2 dir = (toward - from);
            float len = dir.magnitude;
            if (len < 0.001f) return toward;
            dir /= len;
            Vector2 last = from;
            for (float s = 0f; s <= len; s += 0.05f)
            {
                Vector2 q = from + dir * s;
                if (!zone.OverlapPoint(q)) return q;
                last = q;
            }
            return last;
        }

        private void SchoolAim(Vector2 world, string name)
        {
            aimWorld = world;
            hasAim = true;
            goalWorld = new Vector3(world.x, world.y + 0.9f, 0f);
            float d = Vector2.Distance(player.transform.position, world);
            if (d < bestRemain - 0.5f) { bestRemain = d; idleT = 0f; }
        }

        // ---------------- Ok çizimi ----------------

        private void UpdateIdle(float dt)
        {
            if (!Active) return;
            if (goalPoint != null && goalPoint.IsPlayerInRange(player)) { idleT = 0f; return; }
            idleT += dt;
            if (idleT >= idleSeconds)
            {
                idleT = 0f;
                pulseT = 0f;
                IdlePulses++;
                Debug.Log($"[FE-GUIDE] {idleSeconds:0} sn ilerleme yok → ok büyüyüp küçülüyor ({TargetName})", this);
            }
        }

        private void UpdateArrows(float dt)
        {
            var cam = Camera.main;
            float k = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector2 size = canvasRt.rect.size;
            bool inRange = goalPoint != null && goalPoint.IsPlayerInRange(player);

            float scale = 1f;
            if (pulseT >= 0f)
            {
                pulseT += dt;
                float u = pulseT / Mathf.Max(0.1f, idlePulseSeconds);
                if (u >= 1f) pulseT = -1f;
                else scale = Mathf.Round((1f + (idlePulseScale - 1f) * Mathf.Sin(u * Mathf.PI)) * 4f) / 4f;
            }
            float bounce = Mathf.Round(Mathf.Abs(Mathf.Sin(clock * Mathf.PI / Mathf.Max(0.1f, bouncePeriod))) * bounceHeight);

            // Sıçrayan ok: hedef (kapı, dal, avlu, posta kutusu, kilit) ekrandaysa üstünde.
            bool showDown = false;
            if (Active && goalWorld.HasValue && !inRange)
            {
                Vector3 sp = cam.WorldToScreenPoint(goalWorld.Value);
                Vector2 local = new Vector2(sp.x / k, sp.y / k);
                if (sp.z > 0f && local.x > 36f && local.x < size.x - 36f && local.y > 20f && local.y < size.y - 44f)
                {
                    showDown = true;
                    downRt.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y + bounce));
                    downRt.localScale = new Vector3(scale * arrowScale, scale * arrowScale, 1f);
                }
            }
            downGroup.alpha = showDown ? 1f : 0f;
            BounceVisible = showDown;

            // Halka oku: Cenk'in çevresinde, rota üstündeki ileri noktaya doğru. Sıçrayan ok görünürken gösterilmez.
            bool showRing = false;
            if (Active && hasAim && !showDown && !inRange)
            {
                Vector3 center = player.transform.position + new Vector3(0f, 0.45f, 0f);
                Vector3 c = cam.WorldToScreenPoint(center);
                Vector3 t = cam.WorldToScreenPoint(new Vector3(aimWorld.x, aimWorld.y, 0f));
                Vector2 dir = new Vector2(t.x - c.x, t.y - c.y);
                if (dir.sqrMagnitude > 4f)
                {
                    float ang = Mathf.Atan2(dir.y, dir.x);
                    int idx = ((Mathf.RoundToInt(ang / (Mathf.PI / 8f)) % 16) + 16) % 16;
                    ringImg.sprite = dirSprites[idx];
                    float a = idx * Mathf.PI / 8f;
                    float r = ringRadius + Mathf.Round(Mathf.Abs(Mathf.Sin(clock * Mathf.PI / Mathf.Max(0.1f, bouncePeriod))) * 2f);
                    Vector2 local = new Vector2(c.x / k, c.y / k) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    local.x = Mathf.Clamp(local.x, 20f, size.x - 20f);
                    local.y = Mathf.Clamp(local.y, 20f, size.y - 20f);
                    ringRt.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));
                    ringRt.localScale = new Vector3(scale * arrowScale, scale * arrowScale, 1f);
                    showRing = true;
                }
            }
            ringGroup.alpha = showRing ? 1f : 0f;
            RingVisible = showRing;
            ArrowVisible = showDown || showRing;
        }

        // ---------------- Halka (kullanılabilir [E] noktaları) ----------------

        private void UpdateGlows(bool can)
        {
            int count = 0;
            float s = 0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 2f / Mathf.Max(0.2f, glowPeriod));
            float alpha = Mathf.Round(Mathf.Lerp(0.35f, 0.95f, s) * 8f) / 8f;
            if (can && Camera.main != null)
            {
                if (mode == Mode.Evening)
                {
                    foreach (var m in modules)
                    {
                        var p = m.Point;
                        if (p == null || !m.CanUse || !p.Available || !p.isActiveAndEnabled) continue;
                        if (player.GroundDistanceTo(p.transform.position) > glowRadius) continue;
                        PlaceGlow(count++, p.transform.position, alpha);
                    }
                }
                else if (schoolSign != null && schoolSign.Available && schoolSign.isActiveAndEnabled && schoolStage != 2
                         && player.GroundDistanceTo(schoolSign.transform.position) <= glowRadius)
                    PlaceGlow(count++, schoolSign.transform.position, alpha);
            }
            for (int i = count; i < glowPool.Count; i++) glowPool[i].enabled = false;
            GlowCount = count;
        }

        private void PlaceGlow(int i, Vector3 world, float alpha)
        {
            while (glowPool.Count <= i)
            {
                NewImage("Glow_" + glowPool.Count, glowSprite, out var img);
                img.transform.SetAsFirstSibling();
                glowPool.Add(img);
            }
            var g = glowPool[i];
            float k = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector3 sp = Camera.main.WorldToScreenPoint(world);
            g.rectTransform.anchoredPosition = new Vector2(Mathf.Round(sp.x / k), Mathf.Round(sp.y / k));
            g.color = new Color(1f, 1f, 1f, alpha);
            g.enabled = true;
        }
    }
}
