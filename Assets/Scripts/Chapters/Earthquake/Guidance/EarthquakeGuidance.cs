using System;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Earthquake.Mode;
using AfetUyanisi.Chapters.Visibility;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// DEPREM YÖNLENDİRMESİ — oyuncuya NEREYE gideceğini ve NEYLE etkileşeceğini gösterir; bulmacaların cevabını ya da
    /// doğru seçeneği asla göstermez (mini oyunlar, anne sorusu, korunma yeri seçimi oyuncuda kalır).
    ///  • Zıplayan piksel ok: sıradaki bitmemiş hazırlık görevinin eşyası (PreparationQuestManager sırası: çanta, kitaplık,
    ///    pencere, masa; sonra salondaki ocak). Hedef başka odadaysa o odaya giden kapı. Sarsıntıdan sonra: oda kapısı →
    ///    daire kapısı → merdiven → toplanma alanında anne. Oyuncu hedefin [E] menzilindeyse ok gizlenir (ipucu görünür).
    ///  • Yakındaki (glowRadius) bütün kullanılabilir noktaların eşyasında yumuşak nabız atan 1 piksel kontur (eşyanın arkasında).
    ///  • Görev HUD'unda tek satır "ŞİMDİ: …" (PreparationQuestHUD.SetNowText); sarsıntıdan sonra küçük etiket.
    ///  • Oyuncu idleSeconds sn ilerlemezse ok bir kez büyüyüp küçülür.
    ///  • Hazırlığın başında kısa kontrol ipucu (WASD/oklar: yürü · E: kullan); oyuncu yürüyüp bir kez E'ye basınca kaybolur.
    /// Mini oyun, kart, diyalog, soru, oda geçişi ve sarsıntı sırasında hepsi kapalıdır. Sayaç 120 sn kalır (bu sınıf süreye dokunmaz).
    /// EarthquakeModeController Awake'te sahnede yoksa kendine ekler; sahne düzenlemesi gerekmez.
    /// </summary>
    [DisallowMultipleComponent]
    public class EarthquakeGuidance : MonoBehaviour
    {
        [Header("Bağlantılar (boşsa sahnede bulunur)")]
        [SerializeField] private EarthquakeModeController mode;
        [SerializeField] private PreparationQuestHUD hud;
        [SerializeField] private EarthquakeUISkin skin;
        [Tooltip("Boşsa Resources/EqGuidanceSilhouette (kontur çizimi). Yoksa kontur çizilmez, ok yine çalışır.")]
        [SerializeField] private Shader silhouetteShader;
        [SerializeField] private int sortingOrder = 84;

        [Header("Ok")]
        [Tooltip("Ok, hedefin [E] ipucu noktasının bu kadar üstünde (dünya birimi)")]
        [SerializeField] private float arrowLift = 0.12f;
        [Tooltip("Okun piksel ölçeği (1 çizim pikseli = bu kadar referans pikseli; tam sayı)")]
        [SerializeField] private float arrowScale = 2f;
        [Tooltip("Zıplama yüksekliği (referans pikseli, 640x360)")]
        [SerializeField] private float bounceHeight = 3f;
        [SerializeField] private float bouncePeriod = 0.7f;
        [Tooltip("Oyuncu bu kadar sn ilerlemezse ok bir kez büyüyüp küçülür")]
        [SerializeField] private float idleSeconds = 12f;
        [SerializeField] private float idlePulseScale = 1.8f;
        [SerializeField] private float idlePulseSeconds = 0.7f;

        [Header("Kontur / parıltı")]
        [Tooltip("Oyuncunun ayağına bu zemin uzaklığındaki kullanılabilir noktalar parlar (≈ birkaç karo)")]
        [SerializeField] private float glowRadius = 2.4f;
        [SerializeField] private Color glowColor = new Color(1f, 0.87f, 0.4f, 1f);
        [SerializeField, Range(0f, 1f)] private float glowMinAlpha = 0.3f;
        [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.9f;
        [SerializeField] private float glowPeriod = 1.4f;

        [Header("Kontrol ipucu (hazırlığın başı)")]
        [SerializeField] private bool showControlsHint = true;
        [Tooltip("Oyuncu yürüyüp E'ye basmasa da bu kadar sn sonra kaybolur")]
        [SerializeField] private float controlsHintMaxSeconds = 14f;
        [SerializeField] private string moveText = "Yürü";
        [SerializeField] private string useText = "Kullan / incele";

        [Header("ŞİMDİ satırı")]
        [SerializeField] private string readyNow = "Hazırsın! [Boşluk] hızlandır";
        [SerializeField] private string toAssemblyNow = "Toplanma alanına git";
        [SerializeField] private string findMotherNow = "Annenin yanına git";

        // Ok çizimi (13x11, aşağı bakar). X = koyu kontur, Y = amber, H = parlak, D = gölge
        private static readonly string[] ArrowRows =
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

        private sealed class Glow
        {
            public SpriteRenderer source;
            public readonly SpriteRenderer[] copies = new SpriteRenderer[4];
            public bool shown;
        }

        private static readonly Vector2[] GlowDirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

        private IsoPlayerController player;
        private Canvas canvas;
        private RectTransform canvasRt;
        private RectTransform arrowRt;
        private CanvasGroup arrowGroup;
        private RectTransform hintRt;
        private CanvasGroup hintGroup;
        private Material glowMaterial;
        private Transform glowRoot;
        private readonly List<Interactable> points = new List<Interactable>();
        private readonly List<InspectableObject> inspectables = new List<InspectableObject>();
        private readonly Dictionary<Interactable, SpriteRenderer> visuals = new Dictionary<Interactable, SpriteRenderer>();
        private readonly Dictionary<Interactable, Glow> glows = new Dictionary<Interactable, Glow>();
        private BookshelfAnchorMinigame shelf;
        private bool built;

        private Transform target;
        private Interactable targetPoint;
        private SpriteRenderer targetVisual;
        private string nowText;
        private float clock, idleT, pulseT = -1f;
        private int lastDone = -1;
        private EarthquakeRoom lastRoom;
        private EarthquakePhase lastPhase;

        private bool hintStarted, hintDone, usedE;
        private float hintShownT;
        private Vector3 hintStartPos;

        // --- Test / sürücü için okunur durum ---
        public Interactable CurrentPoint => targetPoint;
        public Transform CurrentTarget => target;
        public string NowText => nowText;
        public bool Active { get; private set; }
        public bool ArrowVisible { get; private set; }
        public bool OffScreen { get; private set; }
        public int GlowCount { get; private set; }
        public bool ControlsHintVisible => hintGroup != null && hintGroup.alpha > 0.5f;
        public int IdlePulses { get; private set; }

        private void Start()
        {
            if (mode == null) mode = GetComponent<EarthquakeModeController>();
            if (mode == null) mode = FindAnyObjectByType<EarthquakeModeController>(FindObjectsInactive.Include);
            if (hud == null) hud = FindAnyObjectByType<PreparationQuestHUD>(FindObjectsInactive.Include);
            player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            shelf = FindAnyObjectByType<BookshelfAnchorMinigame>(FindObjectsInactive.Include);
            points.AddRange(FindObjectsByType<Interactable>(FindObjectsInactive.Include));
            inspectables.AddRange(FindObjectsByType<InspectableObject>(FindObjectsInactive.Include));
            if (mode != null && mode.Interactor != null) mode.Interactor.Used += OnUsed;
            Build();
        }

        private void OnDestroy()
        {
            if (mode != null && mode.Interactor != null) mode.Interactor.Used -= OnUsed;
            if (glowMaterial != null) Destroy(glowMaterial);
            if (glowRoot != null) Destroy(glowRoot.gameObject);
        }

        private void OnUsed(Interactable p) => usedE = true;

        // =====================================================================
        // Kurulum
        // =====================================================================

        private void Build()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            var font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_GuidanceCanvas", sortingOrder, out var group);
            group.blocksRaycasts = false;
            group.interactable = false;
            canvasRt = (RectTransform)canvas.transform;

            // Ok
            arrowRt = EqUi.Rect("GuideArrow", canvas.transform);
            arrowRt.anchorMin = arrowRt.anchorMax = Vector2.zero;
            arrowRt.pivot = new Vector2(0.5f, 0f);
            arrowRt.sizeDelta = new Vector2(ArrowRows[0].Length, ArrowRows.Length);
            var img = arrowRt.gameObject.AddComponent<Image>();
            img.sprite = BuildArrowSprite();
            img.raycastTarget = false;
            arrowGroup = arrowRt.gameObject.AddComponent<CanvasGroup>();
            arrowGroup.blocksRaycasts = false;
            arrowGroup.alpha = 0f;

            // Kontrol ipucu: alt orta, [W][A][S][D] Yürü · [E] Kullan / incele
            hintRt = EqUi.Rect("ControlsHint", canvas.transform);
            var bg = EqUi.Img("Bg", hintRt, EqUi.S(skin, s => s.TimerPanel), EqUi.Navy, skin);
            EqUi.Stretch(bg.rectTransform);
            if (bg.sprite == null) EqUi.Fallback(bg, EqUi.Ink, EqUi.NavyDark, skin);
            float x = 6f;
            foreach (var k in new[] { "W", "A", "S", "D" })
            {
                EqUi.TL(EqUi.KeyCap(hintRt, k, font, skin), x, 5f, 11f, 11f);
                x += 12f;
            }
            var move = EqUi.Txt("Move", hintRt, font, 8, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.Ink);
            move.horizontalOverflow = HorizontalWrapMode.Overflow;
            move.text = moveText;
            float moveW = Mathf.Ceil(EqUi.PreferredWidth(move, moveText));
            EqUi.TL(move.rectTransform, x + 2f, 4f, moveW + 2f, 13f);
            x += moveW + 14f;
            EqUi.TL(EqUi.KeyCap(hintRt, "E", font, skin), x, 5f, 11f, 11f);
            x += 14f;
            var use = EqUi.Txt("Use", hintRt, font, 8, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.Ink);
            use.horizontalOverflow = HorizontalWrapMode.Overflow;
            use.text = useText;
            float useW = Mathf.Ceil(EqUi.PreferredWidth(use, useText));
            EqUi.TL(use.rectTransform, x, 4f, useW + 2f, 13f);
            x += useW + 8f;
            EqUi.Place(hintRt, 0.5f, 0f, new Vector2(0f, 14f), new Vector2(Mathf.Round(x), 21f));
            hintGroup = hintRt.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = false;
            hintGroup.alpha = 0f;

            // Kontur malzemesi (tek renk siluet)
            if (silhouetteShader == null) silhouetteShader = Resources.Load<Shader>("EqGuidanceSilhouette");
            if (silhouetteShader == null) silhouetteShader = Shader.Find("AfetUyanisi/EqGuidanceSilhouette");
            if (silhouetteShader != null && silhouetteShader.isSupported)
            {
                glowMaterial = new Material(silhouetteShader) { name = "EQ_GuidanceGlow (runtime)" };
                glowRoot = new GameObject("EQ_GuidanceGlow").transform;
            }
            else Debug.LogWarning("[EQ-GUIDE] kontur shader'ı yok; yalnızca ok gösterilecek", this);
        }

        private static Sprite BuildArrowSprite()
        {
            int w = ArrowRows[0].Length, h = ArrowRows.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "EQ_GuideArrow" };
            Color ink = EqUi.Hex(0x1B1F2E), amber = EqUi.Hex(0xFFC23C), hi = EqUi.Hex(0xFFF0A8), shade = EqUi.Hex(0xE07E1C);
            var px = new Color[w * h];
            for (int row = 0; row < h; row++)
                for (int col = 0; col < w; col++)
                {
                    char c = ArrowRows[row][col];
                    px[(h - 1 - row) * w + col] = c == 'X' ? ink : c == 'Y' ? amber : c == 'H' ? hi : c == 'D' ? shade : Color.clear;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 1f, 0, SpriteMeshType.FullRect);
        }

        // =====================================================================
        // Kare güncellemesi
        // =====================================================================

        private void LateUpdate()
        {
            if (!built || mode == null) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;

            var phase = mode.Phase;
            bool phaseOk = (phase == EarthquakePhase.Prep && !mode.PrepTimeOver) || phase == EarthquakePhase.After;
            Active = phaseOk && mode.GuidanceFree && player != null && player.isActiveAndEnabled;

            if (phaseOk) ChooseTarget(phase);
            else { SetTarget(null, null, null, null); nowText = null; }
            if (hud != null) hud.SetNowText(phaseOk ? nowText : null, phase == EarthquakePhase.After && Active);

            UpdateIdle(dt, phase);
            UpdateArrow(dt);
            UpdateGlows();
            UpdateControlsHint(dt, phase);
        }

        private void UpdateIdle(float dt, EarthquakePhase phase)
        {
            int done = mode.Quests != null ? mode.Quests.DoneCount : 0;
            if (done != lastDone || mode.CurrentRoom != lastRoom || phase != lastPhase)
            {
                lastDone = done; lastRoom = mode.CurrentRoom; lastPhase = phase;
                idleT = 0f;
            }
            if (Active && target != null && !(targetPoint != null && mode.Interactor != null && mode.Interactor.Current == targetPoint))
            {
                idleT += dt;
                if (idleT >= idleSeconds)
                {
                    idleT = 0f;
                    pulseT = 0f;
                    IdlePulses++;
                    Debug.Log($"[EQ-GUIDE] {idleSeconds:0} sn ilerleme yok → ok büyüyüp küçülüyor ({DescribeTarget()})", this);
                }
            }
        }

        private void UpdateArrow(float dt)
        {
            bool inRange = targetPoint != null && mode.Interactor != null && mode.Interactor.Current == targetPoint;
            bool show = Active && target != null && !inRange && Camera.main != null;
            ArrowVisible = show;
            arrowGroup.alpha = show ? 1f : 0f;
            if (!show) { pulseT = -1f; return; }

            Vector3 world = ArrowWorldPoint();
            var cam = Camera.main;
            Vector3 sp = cam.WorldToScreenPoint(world);
            float k = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector2 local = new Vector2(sp.x / k, sp.y / k);
            Vector2 size = canvasRt.rect.size;
            float s0 = Mathf.Max(1f, arrowScale);
            float aw = ArrowRows[0].Length * s0, ah = ArrowRows.Length * s0;
            float bounce = Mathf.Round(Mathf.Abs(Mathf.Sin(clock * Mathf.PI / Mathf.Max(0.1f, bouncePeriod))) * bounceHeight);

            // Hedef ekran dışındaysa: ok ekran kenarında, hedefe doğru döner (45°'lik adımlar, piksel dostu).
            float margin = ah + 6f;
            bool offscreen = sp.z < 0f || local.x < 6f || local.x > size.x - 6f || local.y < 6f || local.y > size.y - 6f;
            float angle = 0f;
            Vector2 bounceDir = Vector2.up;
            if (offscreen)
            {
                Vector2 c = size * 0.5f, dir = local - c;
                if (sp.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
                float t = Mathf.Min(Mathf.Abs((c.x - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x))), Mathf.Abs((c.y - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y))));
                local = c + dir * t;
                angle = Mathf.Round((Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f) / 45f) * 45f;
                bounceDir = -(Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.down);
            }
            else
            {
                // Görev paneli okun üstüne binmesin: panelin altına it.
                if (hud != null && hud.TryGetPanelScreenRect(out Rect hr))
                {
                    var r = new Rect(hr.x / k - 4f, hr.y / k - 3f, hr.width / k + 8f, hr.height / k + 6f);
                    if (local.x + aw * 0.5f > r.xMin && local.x - aw * 0.5f < r.xMax && local.y + ah + bounceHeight > r.yMin)
                        local.y = r.yMin - ah - bounceHeight;
                }
                local.x = Mathf.Clamp(local.x, aw * 0.5f + 4f, size.x - aw * 0.5f - 4f);
                local.y = Mathf.Clamp(local.y, 4f, size.y - ah - bounceHeight - 4f);
            }
            OffScreen = offscreen;

            float scale = s0;
            if (pulseT >= 0f)
            {
                pulseT += dt;
                float u = pulseT / Mathf.Max(0.1f, idlePulseSeconds);
                if (u >= 1f) pulseT = -1f;
                else scale = s0 * (1f + (idlePulseScale - 1f) * Mathf.Sin(u * Mathf.PI));
                scale = Mathf.Round(scale * 2f) / 2f; // kademeli (piksel dostu)
            }
            arrowRt.anchoredPosition = EqUi.Snap(local + bounceDir * bounce);
            arrowRt.localRotation = Quaternion.Euler(0f, 0f, angle);
            arrowRt.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Okun ucu: hedefin [E] ipucu noktasının hemen üstü (tasarımcının eşyanın üstüne koyduğu nokta);
        /// nokta yoksa (anne) görselin üst kenarı.</summary>
        private Vector3 ArrowWorldPoint()
        {
            if (targetPoint != null) return targetPoint.PromptAnchor.position + targetPoint.PromptOffset + new Vector3(0f, arrowLift, 0f);
            if (targetVisual != null && targetVisual.enabled && targetVisual.gameObject.activeInHierarchy && targetVisual.sprite != null)
            {
                var b = targetVisual.bounds;
                return new Vector3(b.center.x, b.max.y + arrowLift, 0f);
            }
            return target.position + new Vector3(0f, 1.2f, 0f);
        }

        // ---------------- Hedef seçimi ----------------

        private void ChooseTarget(EarthquakePhase phase)
        {
            var room = mode.CurrentRoom;
            if (phase == EarthquakePhase.Prep)
            {
                var quests = mode.Quests;
                if (quests != null)
                    foreach (var t in quests.Tasks)
                    {
                        if (t == null || quests.IsDone(t.id) || t.source == PrepTaskSource.AnswerAll) continue;
                        var p = PointForTask(t, room);
                        if (p == null) continue;
                        var pr = p.GetComponentInParent<EarthquakeRoom>();
                        if (pr != null && room != null && pr != room)
                        {
                            var door = DoorToward(room, pr);
                            if (door == null) continue;
                            SetTarget(door.transform, door, VisualOf(door), DoorLabel(door));
                            return;
                        }
                        SetTarget(p.transform, p, VisualOf(p), t.label);
                        return;
                    }
                SetTarget(null, null, null, readyNow);
                return;
            }
            // Sarsıntıdan sonra: toplanma alanına giden yol, orada anne.
            if (!mode.ReachedAssembly)
            {
                var assembly = mode.RoomOfId(EarthquakeRoom.RoomId.Assembly);
                var door = room != null && assembly != null ? DoorToward(room, assembly) : null;
                SetTarget(door != null ? door.transform : null, door, door != null ? VisualOf(door) : null, toAssemblyNow);
                return;
            }
            var mother = mode.AssemblyMeetTarget;
            SetTarget(mother, null, mother != null ? mother.GetComponentInChildren<SpriteRenderer>() : null, findMotherNow);
        }

        private void SetTarget(Transform t, Interactable p, SpriteRenderer visual, string now)
        {
            nowText = now;
            if (t == target && p == targetPoint) return;
            target = t;
            targetPoint = p;
            targetVisual = visual;
            idleT = 0f;
            if (t != null) Debug.Log($"[EQ-GUIDE] ok → {DescribeTarget()} · ŞİMDİ: {now}", this);
        }

        private string DescribeTarget() =>
            target == null ? "(yok)" : target.name + (targetPoint != null ? " (" + targetPoint.ActionLabel + ")" : "");

        private static string DoorLabel(Interactable door) =>
            string.IsNullOrEmpty(door.ActionLabel) ? "Kapıya git" : door.ActionLabel;

        private Interactable PointForTask(PrepTaskDefinition t, EarthquakeRoom room)
        {
            if (t.id == "emergency_bag")
            {
                var bag = mode.EmergencyBag;
                return bag != null && !bag.IsPrepared && Usable(bag.Point) ? bag.Point : null;
            }
            if (t.id == BookshelfAnchorMinigame.MinigameId)
            {
                if (shelf == null || shelf.Secured || shelf.Fell) return null;
                var insp = shelf.GetComponent<InspectableObject>();
                var p = insp != null ? insp.Point : shelf.GetComponent<Interactable>();
                return Usable(p) ? p : null;
            }
            if (t.keys == null) return null;
            Interactable best = null;
            foreach (var key in t.keys)
                foreach (var insp in inspectables)
                {
                    if (insp == null || insp.Id != key || !insp.CanInspect || !Usable(insp.Point)) continue;
                    if (room != null && room.Owns(insp)) return insp.Point;
                    if (best == null) best = insp.Point;
                }
            return best;
        }

        private static bool Usable(Interactable p) => p != null && p.Available && p.isActiveAndEnabled;

        /// <summary>Bulunulan odadan hedef odaya bir adım yaklaştıran kapı (oda sırası: yatak odası → salon → merdiven → toplanma).</summary>
        private Interactable DoorToward(EarthquakeRoom from, EarthquakeRoom to)
        {
            if (from == null || to == null || from == to) return null;
            int goal = (int)to.Id, here = (int)from.Id;
            Interactable best = null;
            int bestGap = Mathf.Abs(goal - here);
            foreach (var d in mode.Doors)
            {
                if (d == null || d.TargetRoom == null || !from.Owns(d) || !Usable(d.Point)) continue;
                int gap = Mathf.Abs(goal - (int)d.TargetRoom.Id);
                if (gap < bestGap) { bestGap = gap; best = d.Point; }
            }
            return best;
        }

        // ---------------- Görsel eşleme ----------------

        /// <summary>
        /// Noktanın eşya görseli: noktanın kendi SpriteRenderer'ı, yoksa aynı odada aynı adlı (oda ön eki atılmış:
        /// BedroomWindow → Window) ya da takma adlı (EmergencyBag → Backpack) SpriteRenderer. Bulunamazsa null (kontur yok).
        /// </summary>
        private SpriteRenderer VisualOf(Interactable p)
        {
            if (p == null) return null;
            if (visuals.TryGetValue(p, out var cached)) return cached;
            SpriteRenderer found = p.GetComponent<SpriteRenderer>();
            var room = p.GetComponentInParent<EarthquakeRoom>();
            if (found == null && room != null)
            {
                var names = new List<string> { p.name };
                string stripped = p.name;
                foreach (var prefix in new[] { "Bedroom", "Living", "Stairwell", "Assembly" })
                    if (stripped.StartsWith(prefix) && stripped.Length > prefix.Length) { stripped = stripped.Substring(prefix.Length); break; }
                if (stripped != p.name) names.Add(stripped);
                if (p.name == "EmergencyBag") names.Add("Backpack");
                var all = room.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var n in names)
                {
                    foreach (var sr in all)
                        if (sr.gameObject.name == n && !sr.transform.IsChildOf(p.transform)) { found = sr; break; }
                    if (found != null) break;
                }
            }
            visuals[p] = found;
            return found;
        }

        // ---------------- Kontur ----------------

        private void UpdateGlows()
        {
            int count = 0;
            float a = 0f;
            if (Active && glowMaterial != null)
            {
                float s = 0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 2f / Mathf.Max(0.2f, glowPeriod));
                a = Mathf.Round(Mathf.Lerp(glowMinAlpha, glowMaxAlpha, s) * 8f) / 8f; // kademeli nabız
            }
            var room = mode.CurrentRoom;
            foreach (var p in points)
            {
                if (p == null) continue;
                bool want = Active && glowMaterial != null && Usable(p) && RoomVisibilityManager.AllowsInteraction(p)
                            && (room == null || room.Owns(p) || p.GetComponentInParent<EarthquakeRoom>() == null)
                            && player.GroundDistanceTo(p.transform.position) <= glowRadius;
                var visual = want ? VisualOf(p) : null;
                want = want && visual != null && visual.enabled && visual.gameObject.activeInHierarchy && visual.sprite != null;
                glows.TryGetValue(p, out var g);
                if (!want)
                {
                    if (g != null && g.shown) ShowGlow(g, false);
                    continue;
                }
                if (g == null || g.source != visual) g = CreateGlow(p, visual);
                SyncGlow(g, a);
                count++;
            }
            GlowCount = count;
        }

        private Glow CreateGlow(Interactable p, SpriteRenderer source)
        {
            if (glows.TryGetValue(p, out var old) && old != null)
                foreach (var c in old.copies) if (c != null) Destroy(c.gameObject);
            var g = new Glow { source = source };
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Glow_" + p.name + "_" + i);
                go.transform.SetParent(glowRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = glowMaterial;
                sr.enabled = false;
                g.copies[i] = sr;
            }
            glows[p] = g;
            return g;
        }

        private void SyncGlow(Glow g, float alpha)
        {
            var src = g.source;
            var st = src.transform;
            float ppu = src.sprite.pixelsPerUnit > 0f ? src.sprite.pixelsPerUnit : 64f;
            Vector3 scale = st.lossyScale;
            for (int i = 0; i < 4; i++)
            {
                var c = g.copies[i];
                var ct = c.transform;
                Vector2 o = GlowDirs[i] / ppu;
                ct.SetPositionAndRotation(st.position + st.rotation * new Vector3(o.x * Mathf.Abs(scale.x), o.y * Mathf.Abs(scale.y), 0f), st.rotation);
                ct.localScale = scale;
                c.sprite = src.sprite;
                c.flipX = src.flipX;
                c.flipY = src.flipY;
                c.drawMode = src.drawMode;
                if (src.drawMode != SpriteDrawMode.Simple) c.size = src.size;
                c.sortingLayerID = src.sortingLayerID;
                c.sortingOrder = src.sortingOrder - 1;
                c.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
                c.enabled = true;
            }
            g.shown = true;
        }

        private static void ShowGlow(Glow g, bool on)
        {
            foreach (var c in g.copies) if (c != null) c.enabled = on;
            g.shown = on;
        }

        // ---------------- Kontrol ipucu ----------------

        private void UpdateControlsHint(float dt, EarthquakePhase phase)
        {
            if (!showControlsHint || hintDone) { hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, 0f, dt / 0.25f); return; }
            if (!hintStarted)
            {
                if (phase != EarthquakePhase.Prep || !Active) return;
                hintStarted = true;
                hintStartPos = player.transform.position;
                usedE = false;
                Debug.Log("[EQ-GUIDE] kontrol ipucu açıldı (WASD/oklar: yürü · E: kullan)", this);
            }
            bool moved = (player.transform.position - hintStartPos).sqrMagnitude > 0.6f * 0.6f;
            if (Active) hintShownT += dt;
            if ((moved && usedE) || hintShownT >= controlsHintMaxSeconds || phase != EarthquakePhase.Prep)
            {
                hintDone = true;
                Debug.Log($"[EQ-GUIDE] kontrol ipucu kapandı (yürüdü={moved}, E={usedE}, {hintShownT:0.0} sn)", this);
                return;
            }
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, Active ? 1f : 0f, dt / 0.2f);
        }
    }
}
