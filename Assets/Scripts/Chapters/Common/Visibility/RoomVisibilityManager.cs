using System;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Earthquake;
using AfetUyanisi.Chapters.Flood;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace AfetUyanisi.Chapters.Visibility
{
    /// <summary>
    /// Project Zomboid tarzı oda görünürlüğü (Mahmut): oyuncunun bulunduğu oda tam aydınlık, diğer odalar karartılmış
    /// siluet (komşu/ziyaretli/hiç girilmemiş/başka bina kademeleri). Sahne düzenlemesi gerekmez: her sahne yüklenince
    /// (GameCameraSettings'teki gibi sceneLoaded) kendiliğinden kurulur ve bölgeleri bulur:
    /// <list type="number">
    /// <item>Sahnede elle konmuş RoomVisibilityZone varsa yalnızca onlar.</item>
    /// <item>Yoksa EarthquakeRoom kökleri (EarthquakeHome: Bedroom, Living, Stairwell, Assembly).</item>
    /// <item>Yoksa "RoomBounds"/"WalkBounds" collider'larının ve "Floor" tilemap'lerinin oda kökleri (FloodChapter: Kitchen,
    /// Street, BusInterior, SchoolRoute). Tek kök bulunursa (Bedroom, Bathroom) bütün sahne tek bölgedir.</item>
    /// </list>
    /// Aktif oda olaya dayalı değişir: oyuncunun ayağı yeni bir odanın sınırına girince (ucuz poligon testi, sadece
    /// oyuncu hareket edince). Karartma yalnızca geçişlerde güncellenir. Duvar kesme: aynı objedeki DynamicWallCutaway.
    /// Etkileşim: aktif olmayan odadaki noktalar RoomInteractor'da ipucu göstermez (AllowsInteraction).
    /// </summary>
    [DefaultExecutionOrder(100)] // oyuncu ve kamera hareketinden sonra
    [DisallowMultipleComponent]
    public class RoomVisibilityManager : MonoBehaviour
    {
        private static readonly List<RoomVisibilityManager> All = new List<RoomVisibilityManager>();

        [Tooltip("Boşsa Resources/RoomVisibility/RoomVisibilitySettings (o da yoksa varsayılanlar)")]
        [SerializeField] private RoomVisibilitySettings settings;
        [Tooltip("Boşsa sahnedeki IsoPlayerController (Cenk)")]
        [SerializeField] private IsoPlayerController player;

        private readonly List<RoomVisibilityZone> zones = new List<RoomVisibilityZone>();
        private readonly List<RoomVisibilityZone> pending = new List<RoomVisibilityZone>();
        private readonly List<RoomVisualGroup> externalGroups = new List<RoomVisualGroup>();
        private readonly Dictionary<Interactable, RoomVisibilityZone> interactableZones = new Dictionary<Interactable, RoomVisibilityZone>();
        private readonly Dictionary<RoomVisibilityZone, bool> adjacencyCache = new Dictionary<RoomVisibilityZone, bool>();
        private MaterialPropertyBlock block;
        private RoomVisibilityZone active;
        private RoomVisibilityZone adjacencyFor;
        private DynamicWallCutaway cutaway;
        private Vector2 lastPlayerPos = new Vector2(float.NaN, float.NaN);
        private bool discovered;
        private bool dirty;
        private float nextPlayerSearch;

        /// <summary>Aktif oda değişince (yeni, önceki).</summary>
        public event Action<RoomVisibilityZone, RoomVisibilityZone> ActiveRoomChanged;

        public RoomVisibilitySettings Settings => settings != null ? settings : (settings = RoomVisibilitySettings.Load());
        public MaterialPropertyBlock Block => block ?? (block = new MaterialPropertyBlock());
        public RoomVisibilityZone ActiveZone => active;
        public IReadOnlyList<RoomVisibilityZone> Zones => zones;
        public IsoPlayerController Player => player;

        /// <summary>Son etkinleşen yönetici (tek sahne yüklü olduğunda sahnenin yöneticisi).</summary>
        public static RoomVisibilityManager Current => All.Count > 0 ? All[All.Count - 1] : null;

        /// <summary>Sahnenin yöneticisi (yoksa null).</summary>
        public static RoomVisibilityManager ForScene(Scene scene)
        {
            for (int i = All.Count - 1; i >= 0; i--)
                if (All[i] != null && All[i].gameObject.scene == scene) return All[i];
            return null;
        }

        /// <summary>
        /// RoomInteractor bu noktanın ipucunu gösterebilir mi? Nokta aktif olmayan bir odadaysa false.
        /// Sistem yoksa, tek oda varsa ya da nokta hiçbir odaya ait değilse her zaman true (eski davranış).
        /// </summary>
        public static bool AllowsInteraction(Interactable point)
        {
            if (point == null) return true;
            var m = ForScene(point.gameObject.scene);
            return m == null || m.InteractionAllowed(point);
        }

        // ---------------- Otomatik kurulum ----------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            All.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try { Install(scene); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Sahneye yöneticiyi ekler (zaten varsa ya da sahnede oyuncu yoksa hiçbir şey yapmaz).</summary>
        public static RoomVisibilityManager Install(Scene scene)
        {
            if (!scene.IsValid()) return null;
            var s = RoomVisibilitySettings.Load();
            if (!s.AutoInstall) return null;
            foreach (var n in s.ExcludedScenes)
                if (string.Equals(n, scene.name, StringComparison.OrdinalIgnoreCase)) return null;

            bool hasPlayer = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<RoomVisibilityManager>(true) != null) return null;
                if (!hasPlayer && root.GetComponentInChildren<IsoPlayerController>(true) != null) hasPlayer = true;
            }
            if (!hasPlayer) return null;

            var go = new GameObject("RoomVisibility");
            SceneManager.MoveGameObjectToScene(go, scene);
            var m = go.AddComponent<RoomVisibilityManager>();
            go.AddComponent<DynamicWallCutaway>();
            return m;
        }

        // ---------------- Yaşam döngüsü ----------------

        private void OnEnable()
        {
            All.Remove(this);
            All.Add(this);
            if (cutaway == null) cutaway = GetComponent<DynamicWallCutaway>();
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void OnDestroy()
        {
            foreach (var z in zones)
                if (z != null) { z.ResetVisuals(); z.Unbind(this); }
        }

        private void Start()
        {
            if (cutaway == null) cutaway = GetComponent<DynamicWallCutaway>(); // Install'da yöneticiden sonra eklenir
            Discover();
        }

        /// <summary>Bölgeleri bulur / oluşturur (Start'ta bir kez).</summary>
        private void Discover()
        {
            if (discovered) return;
            discovered = true;
            var scene = gameObject.scene;
            FindPlayer(true);

            externalGroups.Clear();
            foreach (var g in FindObjectsByType<RoomVisualGroup>(FindObjectsInactive.Include))
                if (g.gameObject.scene == scene && g.Zone != null) externalGroups.Add(g);

            var explicitZones = new List<RoomVisibilityZone>();
            foreach (var z in FindObjectsByType<RoomVisibilityZone>(FindObjectsInactive.Include))
                if (z.gameObject.scene == scene) explicitZones.Add(z);

            string how;
            if (explicitZones.Count > 0)
            {
                how = "sahnedeki RoomVisibilityZone";
            }
            else
            {
                var roots = new List<Transform>();
                bool sceneWide = false;
                foreach (var room in FindObjectsByType<EarthquakeRoom>(FindObjectsInactive.Include))
                    if (room.gameObject.scene == scene && !roots.Contains(room.transform)) roots.Add(room.transform);
                if (roots.Count > 0) how = "EarthquakeRoom";
                else
                {
                    how = "RoomBounds/Floor işaretleri";
                    CollectMarkerRoots(scene, roots, ref sceneWide);
                    if (roots.Count <= 1) sceneWide = true;
                }

                if (sceneWide)
                {
                    how += " → tek oda (bütün sahne)";
                    var go = new GameObject("RoomVisibility_SceneZone");
                    go.SetActive(false);
                    go.transform.SetParent(transform, false);
                    go.AddComponent<RoomVisibilityZone>().ConfigureSceneWide(scene);
                    go.SetActive(true);
                }
                else
                {
                    foreach (var root in roots)
                    {
                        var eqRoom = root.GetComponent<EarthquakeRoom>();
                        bool outdoor = RoomFraming.HasOwnBackdrop(root);
                        string id = eqRoom != null ? eqRoom.Id.ToString() : root.name;
                        var zone = root.gameObject.AddComponent<RoomVisibilityZone>();
                        zone.ConfigureAuto(id, outdoor ? "" : "Home", outdoor, RoomVisibilityZone.FindBoundsCollider(root));
                    }
                }
            }

            // AddComponent sırasında OnEnable çalıştı (kayıt); kapalı köklerdekiler açılınca kaydolur.
            foreach (var z in FindObjectsByType<RoomVisibilityZone>(FindObjectsInactive.Include))
                if (z.gameObject.scene == scene && z.isActiveAndEnabled) Register(z);

            if (Settings.DebugLog)
            {
                var names = new System.Text.StringBuilder();
                foreach (var z in FindObjectsByType<RoomVisibilityZone>(FindObjectsInactive.Include))
                    if (z.gameObject.scene == scene)
                        names.Append(names.Length > 0 ? ", " : "").Append(z.RoomId).Append(z.IsOutdoor ? "(dış)" : "")
                            .Append(z.isActiveAndEnabled ? "" : "(kapalı)");
                Debug.Log($"[OdaGörünürlüğü] {scene.name}: bölgeler {how}: {names}", this);
            }
            ProcessPending();
        }

        private static void CollectMarkerRoots(Scene scene, List<Transform> roots, ref bool sceneWide)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var c in go.GetComponentsInChildren<Collider2D>(true))
                {
                    if (c.name != "RoomBounds" && c.name != "WalkBounds") continue;
                    AddRoot(c.transform.parent, roots, ref sceneWide);
                }
                foreach (var tm in go.GetComponentsInChildren<Tilemap>(true))
                {
                    if (tm.name != "Floor") continue;
                    var grid = tm.GetComponentInParent<Grid>(true);
                    var holder = grid != null ? grid.transform : tm.transform;
                    AddRoot(holder.parent, roots, ref sceneWide);
                }
            }
            // Bir kök diğerinin atasıysa (aynı odanın iki işareti) derindekini tut.
            for (int i = roots.Count - 1; i >= 0; i--)
                for (int j = 0; j < roots.Count; j++)
                    if (i != j && roots[j] != roots[i] && roots[j].IsChildOf(roots[i])) { roots.RemoveAt(i); break; }
        }

        private static void AddRoot(Transform root, List<Transform> roots, ref bool sceneWide)
        {
            if (root == null) { sceneWide = true; return; } // işaret sahne kökünde: tek odalı sahne
            if (!roots.Contains(root)) roots.Add(root);
        }

        // ---------------- Kayıt ----------------

        internal void Register(RoomVisibilityZone zone)
        {
            if (zone == null || zones.Contains(zone)) return;
            zones.Add(zone);
            zone.Bind(this);
            if (!pending.Contains(zone)) pending.Add(zone);
            interactableZones.Clear();
            adjacencyFor = null;
            dirty = true;
        }

        internal void Unregister(RoomVisibilityZone zone)
        {
            if (zone == null) return;
            zones.Remove(zone);
            pending.Remove(zone);
            if (cutaway != null) cutaway.ZoneRemoved(zone);
            interactableZones.Clear();
            adjacencyFor = null;
            if (active == zone)
            {
                var previous = active;
                active = null;
                ActiveRoomChanged?.Invoke(null, previous);
            }
            dirty = true;
        }

        internal void RecollectZone(RoomVisibilityZone zone)
        {
            if (cutaway != null) cutaway.ZoneRemoved(zone);
            if (zones.Contains(zone) && !pending.Contains(zone)) pending.Add(zone);
            dirty = true;
        }

        /// <summary>Bu renderer görünürlük sistemine girer mi? (oyuncu, kamera arka planı, yağmur vb. hariç)</summary>
        internal bool Accepts(Renderer r, out bool cutawayAllowed)
        {
            cutawayAllowed = false;
            if (r.GetComponentInParent<IsoPlayerController>(true) != null) return false;
            if (r.GetComponentInParent<Camera>(true) != null) return false;          // RoomBackdrop vb. kamera çocukları
            if (r.GetComponentInParent<Canvas>(true) != null) return false;
            if (r.GetComponentInParent<CameraBackdrop>(true) != null) return false;  // sokak gökyüzü bandı
            if (r.GetComponentInParent<RainController>(true) != null) return false;  // hava efekti
            var parent = r.transform.parent;
            if (parent != null && parent.GetComponentInParent<DioramaBase>(true) != null) return false; // maket tabanı
            var mat = r.sharedMaterial;
            if (mat == null || !mat.HasProperty(RoomVisibilityZone.Target.ColorId)) return false;

            cutawayAllowed = r.GetComponentInParent<BusOcclusionFade>(true) == null     // otobüsün kendi solması var
                             && r.GetComponentInParent<IsoCharacterAnimator>(true) == null // karakterler saydamlaşmaz
                             && r.GetComponentInParent<AfetUyanisi.Dialogue.DialogueSpeaker>(true) == null
                             && r.GetComponentInParent<QuakeDustPuff>(true) == null;
            return true;
        }

        // ---------------- Güncelleme ----------------

        private void LateUpdate()
        {
            if (!discovered) return;
            if (player == null) FindPlayer(false);
            if (pending.Count > 0) ProcessPending();

            bool playerVisible = player != null && player.gameObject.activeInHierarchy;
            if (player != null) // gizliyken de (uyurken, ara sahnede) son konumu geçerli
            {
                Vector2 p = player.transform.position;
                if (dirty || (p - lastPlayerPos).sqrMagnitude > 0.0001f)
                {
                    lastPlayerPos = p;
                    dirty = false;
                    var zone = Detect(p);
                    if (zone != null && zone != active) SetActive(zone, false);
                }
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < zones.Count; i++) zones[i].Tick(dt);

            if (cutaway != null) cutaway.Tick(this, active, playerVisible ? player : null, dt);
        }

        private void ProcessPending()
        {
            if (pending.Count == 0) return;
            var batch = pending.ToArray();
            pending.Clear();
            foreach (var z in batch)
            {
                if (z == null || !zones.Contains(z)) continue;
                z.Collect(this, externalGroups);
                if (z.StartVisited && z.State == RoomVisibilityZone.VisibilityState.Unvisited)
                    z.State = RoomVisibilityZone.VisibilityState.VisitedInactive;
            }
            if (player != null)
            {
                var zone = Detect(player.transform.position);
                if (zone != null && zone != active)
                {
                    SetActive(zone, true);
                    return;
                }
            }
            // yeni açılan oda (oyuncu içinde değil): hemen kendi karanlığında başlasın
            foreach (var z in batch)
                if (z != null && zones.Contains(z)) z.SetDarkness(TargetDarknessOf(z), true, 0f);
        }

        /// <summary>Oyuncunun ayağının olduğu oda. Hiçbirinin sınırında değilse içerik dikdörtgenine bakar, o da yoksa mevcut oda.</summary>
        private RoomVisibilityZone Detect(Vector2 p)
        {
            RoomVisibilityZone best = null;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null || !z.Contains(p)) continue;
                if (best == null || Better(z, best)) best = z;
            }
            if (best != null) return best;
            if (active != null && active.isActiveAndEnabled && active.LooselyContains(p, 0.75f)) return active;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z != null && z.LooselyContains(p, 0.75f) && (best == null || Better(z, best))) best = z;
            }
            return best != null ? best : (active != null && active.isActiveAndEnabled ? active : null);
        }

        // Çakışmada deterministik öncelik: priority > mevcut oda (yapışkan, kapı eşiğinde titremez) > küçük alan > sahne geneli değil.
        private bool Better(RoomVisibilityZone a, RoomVisibilityZone b)
        {
            if (a.Priority != b.Priority) return a.Priority > b.Priority;
            if (a == active) return true;
            if (b == active) return false;
            if (a.IsSceneWide != b.IsSceneWide) return !a.IsSceneWide;
            return a.Area < b.Area;
        }

        /// <summary>Aktif odayı elle değiştirir (test / ara sahne). instant: geçişsiz.</summary>
        public void SetActive(RoomVisibilityZone zone, bool instant)
        {
            if (zone == null || zone == active) return;
            var previous = active;
            if (previous != null && previous.State == RoomVisibilityZone.VisibilityState.Active)
                previous.State = RoomVisibilityZone.VisibilityState.VisitedInactive;
            active = zone;
            zone.State = RoomVisibilityZone.VisibilityState.Active;
            adjacencyFor = null;
            interactableZones.Clear();

            var s = Settings;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                float target = TargetDarknessOf(z);
                bool brighter = target < z.Darkness;
                z.SetDarkness(target, instant, brighter ? s.BrightenSeconds : s.DarkenSeconds);
            }
            if (s.DebugLog)
                Debug.Log($"[OdaGörünürlüğü] Aktif oda: {zone.RoomId}" + (previous != null ? $" (önceki: {previous.RoomId})" : ""), zone);
            ActiveRoomChanged?.Invoke(zone, previous);
        }

        /// <summary>Kimliğe göre oda (yoksa null).</summary>
        public RoomVisibilityZone FindZone(string roomId)
        {
            foreach (var z in zones)
                if (z != null && z.RoomId == roomId) return z;
            return null;
        }

        /// <summary>Bir odanın aktif odaya göre gitmesi gereken karartması.</summary>
        public float TargetDarknessOf(RoomVisibilityZone z)
        {
            var s = Settings;
            if (!s.DarknessEnabled || z == null || z.IsOutdoor || z.IsSceneWide) return 0f;
            if (active == null) return 0f; // oyuncunun odası bilinmiyorsa hiçbir şeyi karartma (güvenli)
            if (z == active) return 0f;
            bool otherBuilding = active.IsOutdoor || (!string.IsNullOrEmpty(z.BuildingId) && !string.IsNullOrEmpty(active.BuildingId)
                                                      && z.BuildingId != active.BuildingId);
            if (otherBuilding) return s.OtherBuildingDarkness;
            if (z.State == RoomVisibilityZone.VisibilityState.Unvisited) return s.UnexploredDarkness;
            return IsAdjacent(active, z) ? s.AdjacentDarkness : s.VisitedDarkness;
        }

        /// <summary>İki oda komşu mu? (elle verilmiş komşular, yoksa sınır dikdörtgenleri ayarlardaki mesafe içinde)</summary>
        public bool IsAdjacent(RoomVisibilityZone a, RoomVisibilityZone b)
        {
            if (a == null || b == null || a == b) return false;
            if (adjacencyFor != a) { adjacencyCache.Clear(); adjacencyFor = a; }
            if (adjacencyCache.TryGetValue(b, out bool cachedResult)) return cachedResult;

            bool result;
            if (a.ExplicitNeighbors.Count > 0 || b.ExplicitNeighbors.Count > 0)
                result = Contains(a.ExplicitNeighbors, b) || Contains(b.ExplicitNeighbors, a);
            else
            {
                float d = Settings.AdjacencyDistance;
                Rect ra = a.WorldRect, rb = b.WorldRect;
                result = ra.xMin - d <= rb.xMax && rb.xMin - d <= ra.xMax && ra.yMin - d <= rb.yMax && rb.yMin - d <= ra.yMax;
            }
            adjacencyCache[b] = result;
            return result;
        }

        private static bool Contains(IReadOnlyList<RoomVisibilityZone> list, RoomVisibilityZone z)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == z) return true;
            return false;
        }

        private bool InteractionAllowed(Interactable point)
        {
            if (!Settings.InteractionOnlyInActiveRoom || active == null || zones.Count < 2) return true;
            if (!interactableZones.TryGetValue(point, out var zone))
            {
                zone = point.GetComponentInParent<RoomVisibilityZone>(true);
                interactableZones[point] = zone;
            }
            return zone == null || zone == active || zone.IsSceneWide;
        }

        private void FindPlayer(bool force)
        {
            if (!force && Time.unscaledTime < nextPlayerSearch) return;
            nextPlayerSearch = Time.unscaledTime + 0.5f;
            foreach (var p in FindObjectsByType<IsoPlayerController>(FindObjectsInactive.Include))
                if (p.gameObject.scene == gameObject.scene) { player = p; dirty = true; return; }
        }
    }
}
