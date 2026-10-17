using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace AfetUyanisi.Chapters.Visibility
{
    /// <summary>
    /// Bir oda / alan (oda görünürlüğü, Mahmut). Oda kökünün üstünde durur; altındaki SpriteRenderer + TilemapRenderer'lar
    /// odanın görselidir (zemin, duvar, eşya, dekor). Oyuncunun ayağı sınırın içindeyse oda aktiftir (tam aydınlık);
    /// değilse ziyaret durumuna göre karartılır (RoomVisibilitySettings). Sınır: verilen Collider2D (Polygon/Box/
    /// Composite → OverlapPoint; RoomBounds gibi kapalı EdgeCollider2D → noktalarından poligon), yoksa altındaki
    /// "RoomBounds"/"WalkBounds" collider'ı, o da yoksa "Floor" tilemap'inin kapladığı iso eşkenar dörtgen.
    /// Sahneye elle konabilir; konmamışsa RoomVisibilityManager oda köklerine kendiliğinden ekler.
    /// Renk hiçbir zaman SpriteRenderer.color'a yazılmaz: MaterialPropertyBlock "_Color" (shader'da vertex rengiyle çarpılır).
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomVisibilityZone : MonoBehaviour
    {
        /// <summary>Keşif hafızası.</summary>
        public enum VisibilityState { Unvisited, VisitedInactive, Active }

        /// <summary>Bir renderer'ın görünürlük durumu (karartma + duvar kesme saydamlığı aynı _Color'da birleşir).</summary>
        public sealed class Target
        {
            internal static readonly int ColorId = Shader.PropertyToID("_Color");

            public readonly Renderer Renderer;
            public readonly SpriteRenderer Sprite;
            public readonly SortingGroup Group;
            public readonly bool Darken;
            public readonly RoomVisualGroup.CutawayMode Cutaway;
            public readonly bool CutawayAllowed;
            public readonly float AlphaOverride;

            internal float dark;
            internal float alpha = 1f;
            internal float cutProgress;  // 0 = opak, 1 = tam kesik
            internal bool occluded;
            private bool clean = true;

            internal Target(Renderer renderer, RoomVisualGroup group, bool cutawayAllowed)
            {
                Renderer = renderer;
                Sprite = renderer as SpriteRenderer;
                Group = renderer.GetComponentInParent<SortingGroup>(true);
                Darken = group == null || group.Darken;
                Cutaway = group != null ? group.Cutaway : RoomVisualGroup.CutawayMode.Auto;
                CutawayAllowed = cutawayAllowed && Cutaway != RoomVisualGroup.CutawayMode.Never;
                AlphaOverride = group != null ? group.CutawayAlphaOverride : -1f;
            }

            /// <summary>Şu anki karartma (0..1, odanınkiyle aynı) ve saydamlık (1 = opak).</summary>
            public float Dark => Darken ? dark : 0f;
            public float Alpha => alpha;
            public bool Occluded => occluded;

            internal void Apply(MaterialPropertyBlock block, Color darkColor)
            {
                if (Renderer == null) return;
                float d = Darken ? dark : 0f;
                if (d <= 0.0005f && alpha >= 0.9995f)
                {
                    if (!clean)
                    {
                        Renderer.SetPropertyBlock(null); // projede başka MaterialPropertyBlock kullanan yok
                        clean = true;
                    }
                    return;
                }
                var c = Color.LerpUnclamped(Color.white, darkColor, d);
                c.a = alpha;
                Renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, c);
                Renderer.SetPropertyBlock(block);
                clean = false;
            }
        }

        [Tooltip("Oda kimliği (boşsa obje adı)")]
        [SerializeField] private string roomId = "";
        [Tooltip("Bina kimliği: farklı binaların içi daha koyu görünür. Dış mekânda boş bırak.")]
        [SerializeField] private string buildingId = "Building";
        [Tooltip("Dış mekân (sokak, okul yolu, toplanma alanı): hiç kararmaz")]
        [SerializeField] private bool outdoor;
        [Tooltip("Bölgeler çakışırsa büyük olan kazanır (eşitse oyuncunun bulunduğu oda kalır, sonra küçük alan)")]
        [SerializeField] private int priority;
        [Tooltip("İsteğe bağlı sınır (trigger). Boşsa altındaki RoomBounds/WalkBounds ya da Floor tilemap kullanılır.")]
        [SerializeField] private Collider2D boundary;
        [Tooltip("İsteğe bağlı komşu odalar (boşsa sınırları birbirine yakın odalar komşu sayılır)")]
        [SerializeField] private RoomVisibilityZone[] neighbors = Array.Empty<RoomVisibilityZone>();
        [Tooltip("Oyun başında ziyaret edilmiş sayılsın")]
        [SerializeField] private bool startVisited;

        private readonly List<Target> targets = new List<Target>();
        private readonly List<Target> cutawayCandidates = new List<Target>();
        private bool collected;
        private bool sceneWide;
        private Scene ownerScene;

        private Vector2[] polygon;
        private Rect boundaryRect;
        private bool hasBoundary;
        private bool boundaryBuilt;
        private Rect contentRect;
        private bool hasContent;

        private float current, from, to, t, duration;
        private bool animating, hasValue;
        private RoomVisibilityManager manager;

        public string RoomId => string.IsNullOrEmpty(roomId) ? name : roomId;
        public string BuildingId => buildingId ?? "";
        public bool IsOutdoor => outdoor;
        public bool IsSceneWide => sceneWide;
        public int Priority => priority;
        public IReadOnlyList<RoomVisibilityZone> ExplicitNeighbors => neighbors;
        public VisibilityState State { get; internal set; } = VisibilityState.Unvisited;
        public bool StartVisited => startVisited;
        /// <summary>Şu anki karartma (0 = normal).</summary>
        public float Darkness => current;
        /// <summary>Gidilen karartma değeri.</summary>
        public float TargetDarkness => to;
        public bool IsAnimating => animating;
        public IReadOnlyList<Target> Targets => targets;
        internal List<Target> CutawayCandidates => cutawayCandidates;
        public bool Collected => collected;

        /// <summary>Kodla kurulum (RoomVisibilityManager'ın otomatik bölgeleri için). Bileşen eklendikten hemen sonra çağrılır.</summary>
        public void ConfigureAuto(string id, string building, bool isOutdoor, Collider2D bounds, int zonePriority = 0)
        {
            roomId = id ?? "";
            buildingId = building ?? "";
            outdoor = isOutdoor;
            boundary = bounds;
            priority = zonePriority;
            boundaryBuilt = false;
        }

        /// <summary>Bütün sahneyi kapsayan tek bölge (tek odalı sahneler: Bedroom, Bathroom).</summary>
        internal void ConfigureSceneWide(Scene scene)
        {
            sceneWide = true;
            ownerScene = scene;
            roomId = scene.name;
            buildingId = "Building";
            outdoor = false;
        }

        private void OnEnable()
        {
            var m = RoomVisibilityManager.ForScene(gameObject.scene);
            if (m != null) m.Register(this);
        }

        private void OnDisable()
        {
            if (manager != null) manager.Unregister(this);
        }

        internal void Bind(RoomVisibilityManager owner) => manager = owner;
        internal void Unbind(RoomVisibilityManager owner) { if (manager == owner) manager = null; }

        // ---------------- Renderer toplama ----------------

        /// <summary>Renderer listesini (kapalılar dahil) bir kez toplar. Çalışma anında eklenen içerik için Recollect.</summary>
        internal void Collect(RoomVisibilityManager owner, List<RoomVisualGroup> externalGroups)
        {
            if (collected) return;
            collected = true;
            targets.Clear();
            cutawayCandidates.Clear();
            var seen = new HashSet<Renderer>();

            if (sceneWide)
            {
                if (ownerScene.IsValid())
                    foreach (var root in ownerScene.GetRootGameObjects())
                        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r.GetComponentInParent<RoomVisibilityZone>(true) != null) continue; // başka bölgenin
                            TryAdd(owner, r, seen, false);
                        }
            }
            else
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponentInParent<RoomVisibilityZone>(true) != this) continue; // iç içe alt bölgenin
                    TryAdd(owner, r, seen, false);
                }
            }

            if (externalGroups != null)
                foreach (var g in externalGroups)
                    if (g != null && g.Zone == this)
                        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                            TryAdd(owner, r, seen, true);

            ComputeContentRect();
        }

        /// <summary>Çalışma anında odaya eklenen objelerden sonra listeyi yeniden kurar.</summary>
        public void Recollect()
        {
            foreach (var target in targets)
            {
                target.dark = 0f;
                target.alpha = 1f;
                if (manager != null) target.Apply(manager.Block, manager.Settings.DarknessColor);
            }
            collected = false;
            boundaryBuilt = false;
            if (manager != null) manager.RecollectZone(this);
        }

        /// <summary>Oda kökü taşındıysa sınırı yeniden hesaplar (sınır dünya koordinatında önbelleklenir).</summary>
        public void RefreshBoundary() => boundaryBuilt = false;

        private void TryAdd(RoomVisibilityManager owner, Renderer r, HashSet<Renderer> seen, bool external)
        {
            if (r == null || !(r is SpriteRenderer || r is TilemapRenderer)) return;
            if (!seen.Add(r)) return;
            var group = r.GetComponentInParent<RoomVisualGroup>(true);
            if (!external && group != null && group.Zone != null && group.Zone != this) return;
            if (!owner.Accepts(r, out bool cutawayAllowed)) return;
            var target = new Target(r, group, cutawayAllowed);
            targets.Add(target);
            bool candidate = target.CutawayAllowed &&
                             (target.Sprite != null || target.Cutaway == RoomVisualGroup.CutawayMode.Always);
            if (candidate) cutawayCandidates.Add(target);
        }

        private void ComputeContentRect()
        {
            hasContent = false;
            foreach (var target in targets)
            {
                var r = target.Renderer;
                if (r == null || !r.gameObject.activeInHierarchy || !r.enabled) continue;
                if (target.Sprite != null && target.Sprite.sprite == null) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude <= 0f) continue;
                var rect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                if (!hasContent) { contentRect = rect; hasContent = true; }
                else contentRect = Rect.MinMaxRect(Mathf.Min(contentRect.xMin, rect.xMin), Mathf.Min(contentRect.yMin, rect.yMin),
                    Mathf.Max(contentRect.xMax, rect.xMax), Mathf.Max(contentRect.yMax, rect.yMax));
            }
        }

        // ---------------- Sınır ----------------

        private void BuildBoundary()
        {
            if (boundaryBuilt) return;
            boundaryBuilt = true;
            polygon = null;
            hasBoundary = false;
            if (sceneWide) return;

            if (boundary == null) boundary = FindBoundsCollider(transform);
            if (boundary is EdgeCollider2D edge && edge.pointCount >= 3)
            {
                var pts = edge.points;
                var list = new List<Vector2>(pts.Length);
                foreach (var p in pts)
                {
                    Vector2 w = edge.transform.TransformPoint(p + edge.offset);
                    if (list.Count > 0 && (list[list.Count - 1] - w).sqrMagnitude < 0.000001f) continue;
                    list.Add(w);
                }
                if (list.Count > 2 && (list[0] - list[list.Count - 1]).sqrMagnitude < 0.000001f) list.RemoveAt(list.Count - 1);
                if (list.Count >= 3) SetPolygon(list.ToArray());
                return;
            }
            if (boundary != null && !(boundary is EdgeCollider2D))
            {
                var b = boundary.bounds;
                boundaryRect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                hasBoundary = boundaryRect.width > 0f && boundaryRect.height > 0f;
                return;
            }

            var floor = FindFloor(transform);
            if (floor != null)
            {
                var cb = floor.cellBounds;
                if (cb.size.x > 0 && cb.size.y > 0)
                {
                    SetPolygon(new Vector2[]
                    {
                        floor.CellToWorld(new Vector3Int(cb.xMin, cb.yMin, 0)),
                        floor.CellToWorld(new Vector3Int(cb.xMax, cb.yMin, 0)),
                        floor.CellToWorld(new Vector3Int(cb.xMax, cb.yMax, 0)),
                        floor.CellToWorld(new Vector3Int(cb.xMin, cb.yMax, 0)),
                    });
                }
            }
        }

        private void SetPolygon(Vector2[] pts)
        {
            polygon = pts;
            float xMin = pts[0].x, yMin = pts[0].y, xMax = pts[0].x, yMax = pts[0].y;
            foreach (var p in pts)
            {
                xMin = Mathf.Min(xMin, p.x); yMin = Mathf.Min(yMin, p.y);
                xMax = Mathf.Max(xMax, p.x); yMax = Mathf.Max(yMax, p.y);
            }
            boundaryRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            hasBoundary = true;
        }

        /// <summary>Altındaki (kapalılar dahil) RoomBounds / WalkBounds adlı collider.</summary>
        public static Collider2D FindBoundsCollider(Transform root)
        {
            if (root == null) return null;
            foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
                if (c.name == "RoomBounds" || c.name == "WalkBounds") return c;
            return null;
        }

        /// <summary>Altındaki "Floor" adlı tilemap.</summary>
        public static Tilemap FindFloor(Transform root)
        {
            if (root == null) return null;
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
                if (tm.name == "Floor") return tm;
            return null;
        }

        /// <summary>Nokta (oyuncunun ayağı) bu odanın sınırı içinde mi?</summary>
        public bool Contains(Vector2 p)
        {
            if (sceneWide) return true;
            BuildBoundary();
            if (polygon != null) return boundaryRect.Contains(p) && PointInPolygon(polygon, p);
            if (boundary != null) return boundary.enabled && boundary.gameObject.activeInHierarchy ? boundary.OverlapPoint(p) : boundaryRect.Contains(p);
            return hasContent && contentRect.Contains(p);
        }

        /// <summary>Sınırın biraz dışı (kapı eşiği, spawn noktası): içerik dikdörtgeni + pay.</summary>
        public bool LooselyContains(Vector2 p, float margin)
        {
            if (sceneWide) return true;
            var r = WorldRect;
            r.xMin -= margin; r.yMin -= margin; r.xMax += margin; r.yMax += margin;
            return r.width > 0f && r.Contains(p);
        }

        /// <summary>Odanın dünya dikdörtgeni (sınır, yoksa içerik).</summary>
        public Rect WorldRect
        {
            get
            {
                BuildBoundary();
                if (hasBoundary) return boundaryRect;
                return hasContent ? contentRect : new Rect(transform.position, Vector2.zero);
            }
        }

        public float Area { get { var r = WorldRect; return r.width * r.height; } }

        private static bool PointInPolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        // ---------------- Karartma ----------------

        internal void SetDarkness(float value, bool instant, float seconds)
        {
            if (!hasValue || instant)
            {
                current = from = to = value;
                animating = false;
                hasValue = true;
                ApplyAll();
                return;
            }
            if (Mathf.Abs(value - to) < 0.0001f) return;
            from = current; // hızlı geçişte mevcut değerden devam (zıplama yok)
            to = value;
            t = 0f;
            duration = Mathf.Max(0.01f, seconds);
            animating = true;
        }

        internal void Tick(float dt)
        {
            if (!animating) return;
            t += dt / duration;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            current = Mathf.Lerp(from, to, k);
            if (t >= 1f)
            {
                current = to;
                animating = false;
            }
            ApplyAll();
        }

        internal void ApplyAll()
        {
            if (manager == null) return;
            var block = manager.Block;
            var color = manager.Settings.DarknessColor;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                target.dark = current;
                target.Apply(block, color);
            }
        }

        /// <summary>Bütün renderer'ları normale döndürür (sistem kapanırken).</summary>
        internal void ResetVisuals()
        {
            current = from = to = 0f;
            animating = false;
            hasValue = false;
            if (manager == null) return;
            foreach (var target in targets)
            {
                target.dark = 0f;
                target.alpha = 1f;
                target.cutProgress = 0f;
                target.occluded = false;
                target.Apply(manager.Block, manager.Settings.DarknessColor);
            }
        }

        private void OnDrawGizmosSelected()
        {
            BuildBoundary();
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            if (polygon != null)
            {
                for (int i = 0; i < polygon.Length; i++)
                    Gizmos.DrawLine(polygon[i], polygon[(i + 1) % polygon.Length]);
            }
            else
            {
                var r = WorldRect;
                Gizmos.DrawWireCube(r.center, r.size);
            }
            if (!Application.isPlaying) boundaryBuilt = false;
        }
    }
}
