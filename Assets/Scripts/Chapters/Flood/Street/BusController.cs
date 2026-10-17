using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Durağa gelen okul otobüsü (Mahmut). Prefab'ın sahnedeki konumu DURAK NOKTASIDIR (zemin izinin merkezi).
    /// Sahne açılınca otobüs görünmez, yolun gerisinde bekler. Arrive(): yol ekseni boyunca (izometrik +X,
    /// hücre başına (0.5, 0.25)) ekran dışından gelir, yavaşlayarak durur (yürürken gövde 1 px sallanır),
    /// kapı açılır ve BoardPoint ("[E] Otobüse bin") kullanılabilir olur. Depart(): kapı kapanır, otobüs
    /// hızlanarak ekran dışına çıkar ve gizlenir. Otobüs katıdır (zemin izi PolygonCollider2D + kinematik
    /// Rigidbody2D): çocuk içinden geçemez. Gövde + kapı bir SortingGroup içindedir (pivot sıralaması, order 0).
    /// Board(): isteğe bağlı biniş yardımcısı; çocuğu kapı hizasına yürütür (otobüsün arkasında kalacak şekilde
    /// sıralamasını geçici değiştirir) ve gizler.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BusController : MonoBehaviour
    {
        private const float PixelsPerUnit = 64f;

        [Header("Parçalar")]
        [Tooltip("Sallanan görsel kök (SortingGroup: gövde + kapı birlikte sıralanır)")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Kapı katmanı (gövdeyle aynı tuval ve pivot); kapalı/açık sprite'ı burada değişir")]
        [SerializeField] private SpriteRenderer door;
        [SerializeField] private Sprite doorClosedSprite;
        [SerializeField] private Sprite doorOpenSprite;
        [Tooltip("İsteğe bağlı ara kareler (kapalıdan açığa doğru); kapanırken ters oynar")]
        [SerializeField] private Sprite[] doorFrames = Array.Empty<Sprite>();
        [Tooltip("\"[E] Otobüse bin\" noktası; kapılar açılınca kullanılabilir olur")]
        [SerializeField] private Interactable boardPoint;
        [Tooltip("Board() ile çocuğun yürüyüp gizlendiği nokta (kapı hizası, otobüsün gövdesinin arkası)")]
        [SerializeField] private Transform boardEntry;
        [Tooltip("Hareket ederken su sıçratan partiküller; miktar hızla orantılı")]
        [SerializeField] private ParticleSystem[] wheelSpray = Array.Empty<ParticleSystem>();
        [Tooltip("Seyir hızında saniyede sıçrama")]
        [SerializeField] private float sprayAtCruise = 14f;

        [Header("Yol")]
        [Tooltip("Yol ekseninde bir hücrelik dünya vektörü: izometrik +X = (0.5, 0.25). Otobüs bu yönde ilerler.")]
        [SerializeField] private Vector2 travelAxis = new Vector2(0.5f, 0.25f);
        [Tooltip("Gelişin başladığı yer: durak noktasından yol ekseni boyunca kaç hücre (negatif = geriden gelir)")]
        [SerializeField] private float arriveFromCells = -16f;
        [Tooltip("Gidişin bittiği yer: durak noktasından kaç hücre ileride")]
        [SerializeField] private float departToCells = 18f;
        [Tooltip("Başlangıç/bitiş noktası kameradan görünüyorsa ekran dışına çıkana kadar uzat")]
        [SerializeField] private bool ensureOffscreen = true;
        [Tooltip("Seyir hızı (hücre / sn)")]
        [SerializeField] private float cruiseSpeed = 6f;
        [Tooltip("Durağa gelirken yavaşlama mesafesi (hücre)")]
        [SerializeField] private float brakeCells = 5f;
        [Tooltip("Kalkışta hızlanma mesafesi (hücre)")]
        [SerializeField] private float accelerateCells = 5f;

        [Header("Gövde ve kapı")]
        [Tooltip("Yürürken gövdenin yukarı-aşağı sallanması (piksel)")]
        [SerializeField] private int bobPixels = 1;
        [Tooltip("Bir sallanma (yukarı + aşağı) kaç hücrede tamamlanır")]
        [SerializeField] private float bobEveryCells = 1.25f;
        [Tooltip("Durduktan sonra kapı açılmadan önceki bekleme (sn)")]
        [SerializeField] private float doorDelay = 0.35f;
        [Tooltip("Kapının açılma/kapanma süresi (sn)")]
        [SerializeField] private float doorSeconds = 0.3f;
        [Tooltip("Kapı kapandıktan sonra kalkıştan önceki bekleme (sn)")]
        [SerializeField] private float departDelay = 0.4f;
        [Tooltip("Gelmeden önce ve gittikten sonra otobüsü gizle (çarpışması da kapanır)")]
        [SerializeField] private bool hideWhenAway = true;

        [Header("Biniş (Board)")]
        [Tooltip("Binerken çocuğun sortingOrder'ı. Durak arka kaldırımdaysa (seçenek B) -1: çocuk otobüsün arkasında kalır.")]
        [SerializeField] private int boardingSortingOrder = -1;
        [Tooltip("Kapıya yürüme hızı (zemin uzaklığı / sn)")]
        [SerializeField] private float boardWalkSpeed = 1.6f;

        [Header("Olaylar (ses kancaları)")]
        [Tooltip("Otobüs gelmeye başladı (motor sesi)")]
        [SerializeField] private UnityEvent onArriving = new UnityEvent();
        [Tooltip("Otobüs durdu ve kapı açıldı")]
        [SerializeField] private UnityEvent onArrived = new UnityEvent();
        [SerializeField] private UnityEvent onDoorsOpened = new UnityEvent();
        [SerializeField] private UnityEvent onDoorsClosed = new UnityEvent();
        [Tooltip("Otobüs kalkıyor")]
        [SerializeField] private UnityEvent onDeparting = new UnityEvent();
        [Tooltip("Otobüs ekrandan çıktı ve gizlendi")]
        [SerializeField] private UnityEvent onDeparted = new UnityEvent();

        private Rigidbody2D body;
        private Collider2D[] colliders = Array.Empty<Collider2D>();
        private Renderer[] renderers = Array.Empty<Renderer>();
        private Vector2 stopPosition;
        private Vector3 visualBaseLocal;
        private bool initialized;
        private bool visible = true;
        private float bobDistance;

        /// <summary>"[E] Otobüse bin" noktası (çocuk "BoardPoint"); kapılar açıkken Available.</summary>
        public Interactable BoardPoint
        {
            get
            {
                Init();
                return boardPoint;
            }
        }

        /// <summary>Kapılar açık mı?</summary>
        public bool DoorsOpen { get; private set; }

        /// <summary>Otobüs yolda hareket ediyor mu?</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Otobüs durakta mı (görünür ve duruyor)?</summary>
        public bool IsAtStop { get; private set; }

        /// <summary>Durak noktası (prefabın sahnedeki konumu).</summary>
        public Vector2 StopPosition
        {
            get
            {
                Init();
                return stopPosition;
            }
        }

        public UnityEvent OnArriving => onArriving;
        public UnityEvent OnArrived => onArrived;
        public UnityEvent OnDoorsOpened => onDoorsOpened;
        public UnityEvent OnDoorsClosed => onDoorsClosed;
        public UnityEvent OnDeparting => onDeparting;
        public UnityEvent OnDeparted => onDeparted;

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (initialized) return;
            initialized = true;

            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None; // konum her karede piksele oturtulur
            colliders = GetComponents<Collider2D>();
            renderers = GetComponentsInChildren<Renderer>(true);
            stopPosition = transform.position;
            if (visualRoot != null) visualBaseLocal = visualRoot.localPosition;
            if (boardPoint == null)
            {
                var t = transform.Find("BoardPoint");
                if (t != null) boardPoint = t.GetComponent<Interactable>();
            }
            if (boardEntry == null) boardEntry = transform.Find("BoardEntry");

            ShowDoor(false);
            DoorsOpen = false;
            if (boardPoint != null) boardPoint.SetAvailable(false);
            SetSpray(0f);

            if (hideWhenAway)
            {
                Teleport(PointAt(arriveFromCells));
                SetVisible(false);
            }
            else
            {
                IsAtStop = true;
            }
        }

        /// <summary>
        /// Otobüs ekran dışından gelir, durak noktasında yavaşlayarak durur, kapıyı açar; kapı açılınca biter
        /// (BoardPoint artık kullanılabilir). Obje kapalıysa (sahne adımı açık değil) hemen durakta belirir.
        /// </summary>
        public IEnumerator Arrive()
        {
            Init();
            if (IsMoving)
            {
                Debug.LogWarning("[BusController] Otobüs zaten yolda, Arrive atlandı.", this);
                yield break;
            }
            if (IsAtStop && visible)
            {
                if (!DoorsOpen) yield return OpenDoors();
                yield break;
            }

            ShowDoor(false);
            DoorsOpen = false;
            if (boardPoint != null) boardPoint.SetAvailable(false);

            if (!gameObject.activeInHierarchy)
            {
                Teleport(stopPosition);
                SetVisible(true);
                IsAtStop = true;
                ShowDoor(true);
                DoorsOpen = true;
                if (boardPoint != null) boardPoint.SetAvailable(true);
                onArrived.Invoke();
                yield break;
            }

            float fromCells = ResolveOffscreen(arriveFromCells, -1f);
            Teleport(PointAt(fromCells));
            SetVisible(true);
            IsMoving = true;
            onArriving.Invoke();
            Debug.Log($"[BusController] {Time.timeSinceLevelLoad:0.0} sn → otobüs geliyor ({Mathf.Abs(fromCells):0.#} hücre)", this);

            float v = Mathf.Max(0.1f, cruiseSpeed);
            float distance = Mathf.Abs(fromCells);
            float brake = Mathf.Clamp(brakeCells, 0f, distance);
            float tCruise = (distance - brake) / v;
            float tBrake = brake > 0f ? 2f * brake / v : 0f; // sabit yavaşlama: v'den 0'a
            float total = tCruise + tBrake;
            float dir = fromCells <= 0f ? 1f : -1f;
            float travelled = 0f;

            for (float t = 0f; t < total;)
            {
                yield return null;
                t = Mathf.Min(total, t + Time.deltaTime);
                float s;
                float speed;
                if (t <= tCruise)
                {
                    s = v * t;
                    speed = v;
                }
                else
                {
                    float u = Mathf.Clamp01((t - tCruise) / tBrake);
                    s = distance - brake + brake * (1f - (1f - u) * (1f - u));
                    speed = v * (1f - u);
                }
                MoveAlong(fromCells + dir * s, s - travelled, speed / v);
                travelled = s;
            }

            MoveAlong(0f, 0f, 0f);
            SettleBody();
            IsMoving = false;
            IsAtStop = true;

            if (doorDelay > 0f) yield return new WaitForSeconds(doorDelay);
            yield return OpenDoors();
            Debug.Log($"[BusController] {Time.timeSinceLevelLoad:0.0} sn → otobüs durakta, kapı açık", this);
            onArrived.Invoke();
        }

        /// <summary>
        /// Kapı kapanır, kısa beklemeden sonra otobüs hızlanarak ekran dışına çıkar ve gizlenir (hideWhenAway).
        /// </summary>
        public IEnumerator Depart()
        {
            Init();
            if (IsMoving)
            {
                Debug.LogWarning("[BusController] Otobüs zaten yolda, Depart atlandı.", this);
                yield break;
            }
            if (!visible)
            {
                onDeparted.Invoke();
                yield break;
            }

            if (DoorsOpen) yield return CloseDoors();
            if (departDelay > 0f) yield return new WaitForSeconds(departDelay);

            IsAtStop = false;
            if (!gameObject.activeInHierarchy)
            {
                Teleport(PointAt(departToCells));
                if (hideWhenAway) SetVisible(false);
                onDeparted.Invoke();
                yield break;
            }

            float toCells = ResolveOffscreen(departToCells, 1f);
            IsMoving = true;
            onDeparting.Invoke();
            Debug.Log($"[BusController] {Time.timeSinceLevelLoad:0.0} sn → otobüs kalkıyor", this);

            float v = Mathf.Max(0.1f, cruiseSpeed);
            float distance = Mathf.Abs(toCells);
            float accel = Mathf.Clamp(accelerateCells, 0f, distance);
            float tAccel = accel > 0f ? 2f * accel / v : 0f; // sabit ivme: 0'dan v'ye
            float total = tAccel + (distance - accel) / v;
            float dir = toCells >= 0f ? 1f : -1f;
            float travelled = 0f;

            for (float t = 0f; t < total;)
            {
                yield return null;
                t = Mathf.Min(total, t + Time.deltaTime);
                float s;
                float speed;
                if (t <= tAccel)
                {
                    float u = t / tAccel;
                    s = accel * u * u;
                    speed = v * u;
                }
                else
                {
                    s = accel + v * (t - tAccel);
                    speed = v;
                }
                s = Mathf.Min(s, distance);
                MoveAlong(dir * s, s - travelled, speed / v);
                travelled = s;
            }

            IsMoving = false;
            SetSpray(0f);
            SettleBody();
            if (hideWhenAway) SetVisible(false);
            Debug.Log($"[BusController] {Time.timeSinceLevelLoad:0.0} sn → otobüs gitti", this);
            onDeparted.Invoke();
        }

        /// <summary>
        /// İsteğe bağlı biniş: çocuğun hareketini kapatır, otobüsle çarpışmasını geçici kapatır, sıralamasını
        /// boardingSortingOrder yapar (otobüsün arkasında kalır), BoardEntry noktasına yürütür ve gizler
        /// (hidePlayer). Sonra sıralamayı ve çarpışmayı eski hâline getirir. Kapı açıkken çağırın.
        /// </summary>
        public IEnumerator Board(IsoPlayerController player, bool hidePlayer = true)
        {
            Init();
            if (player == null) yield break;

            player.CanMove = false;
            var playerBody = player.GetComponent<Rigidbody2D>();
            var playerCollider = player.GetComponent<Collider2D>();
            var playerRenderer = player.GetComponent<SpriteRenderer>();
            var playerAnimator = player.GetComponent<IsoCharacterAnimator>();

            int oldOrder = playerRenderer != null ? playerRenderer.sortingOrder : 0;
            if (playerRenderer != null) playerRenderer.sortingOrder = boardingSortingOrder;
            SetIgnoreCollision(playerCollider, true);
            // Kapı hizası yolun kenarında: yol/birikinti uyarıları senaryolu yürüyüşü durdurmasın.
            var tracker = player.GetComponent<PlayerHazardTracker>();
            if (tracker != null) tracker.Suppressed = true;

            Vector2 from = playerBody != null ? playerBody.position : (Vector2)player.transform.position;
            Vector2 to = boardEntry != null ? (Vector2)boardEntry.position
                : boardPoint != null ? (Vector2)boardPoint.transform.position : from;
            Vector2 delta = to - from;
            float ground = Mathf.Sqrt(delta.x * delta.x + 4f * delta.y * delta.y);
            float seconds = Mathf.Max(0.2f, ground / Mathf.Max(0.1f, boardWalkSpeed));

            for (float t = 0f; t < seconds;)
            {
                yield return null; // oyuncunun Update'inden sonra: yürüme animasyonu bu karede de açık kalsın
                t = Mathf.Min(seconds, t + Time.deltaTime);
                Vector2 p = Vector2.Lerp(from, to, t / seconds);
                if (playerBody != null)
                {
                    playerBody.position = p;
                    playerBody.linearVelocity = Vector2.zero;
                }
                player.transform.position = p;
                if (playerAnimator != null) playerAnimator.SetMotion(t < seconds ? delta : Vector2.zero);
            }

            if (hidePlayer) player.gameObject.SetActive(false);
            if (playerRenderer != null) playerRenderer.sortingOrder = oldOrder;
            SetIgnoreCollision(playerCollider, false);
            if (tracker != null) tracker.Suppressed = false;
        }

        /// <summary>Kapıyı hemen açar/kapatır (test ve özel akışlar için).</summary>
        public void SetDoorsOpenNow(bool open)
        {
            Init();
            ShowDoor(open);
            DoorsOpen = open;
            if (boardPoint != null) boardPoint.SetAvailable(open);
        }

        private IEnumerator OpenDoors()
        {
            yield return PlayDoor(true);
            DoorsOpen = true;
            if (boardPoint != null) boardPoint.SetAvailable(true);
            onDoorsOpened.Invoke();
        }

        private IEnumerator CloseDoors()
        {
            DoorsOpen = false;
            if (boardPoint != null) boardPoint.SetAvailable(false);
            yield return PlayDoor(false);
            onDoorsClosed.Invoke();
        }

        private IEnumerator PlayDoor(bool opening)
        {
            int frames = doorFrames != null ? doorFrames.Length : 0;
            if (door != null && frames > 0 && doorSeconds > 0f)
            {
                float step = doorSeconds / (frames + 1);
                for (int i = 0; i < frames; i++)
                {
                    var frame = doorFrames[opening ? i : frames - 1 - i];
                    if (frame != null) door.sprite = frame;
                    yield return new WaitForSeconds(step);
                }
                yield return new WaitForSeconds(step);
            }
            else if (doorSeconds > 0f)
            {
                yield return new WaitForSeconds(doorSeconds);
            }
            ShowDoor(opening);
        }

        private void ShowDoor(bool open)
        {
            if (door == null) return;
            var sprite = open ? doorOpenSprite : doorClosedSprite;
            if (sprite != null) door.sprite = sprite;
        }

        // Yol ekseninde cells konumuna taşır; gövde gidilen yola göre sallanır, sıçrama hızla orantılı.
        private void MoveAlong(float cells, float deltaCells, float speed01)
        {
            Teleport(PointAt(cells), false);
            bobDistance += Mathf.Abs(deltaCells);
            if (visualRoot != null && bobPixels != 0 && bobEveryCells > 0f)
            {
                float phase = bobDistance / bobEveryCells;
                bool up = phase - Mathf.Floor(phase) >= 0.5f && speed01 > 0.05f;
                visualRoot.localPosition = visualBaseLocal + new Vector3(0f, up ? bobPixels / PixelsPerUnit : 0f, 0f);
            }
            SetSpray(speed01);
        }

        private void SettleBody()
        {
            bobDistance = 0f;
            if (visualRoot != null) visualRoot.localPosition = visualBaseLocal;
            SetSpray(0f);
        }

        private Vector2 PointAt(float cells) => stopPosition + travelAxis * cells;

        private void Teleport(Vector2 position, bool clearSpray = true)
        {
            var p = new Vector2(Snap(position.x), Snap(position.y));
            if (clearSpray)
                foreach (var ps in wheelSpray)
                    if (ps != null) ps.Clear(true);
            if (body != null) body.position = p;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        private void SetVisible(bool value)
        {
            visible = value;
            foreach (var r in renderers)
                if (r != null) r.enabled = value;
            foreach (var c in colliders)
                if (c != null) c.enabled = value;
            if (!value)
            {
                SetSpray(0f);
                foreach (var ps in wheelSpray)
                    if (ps != null) ps.Clear(true);
            }
        }

        private void SetSpray(float speed01)
        {
            foreach (var ps in wheelSpray)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.rateOverTime = visible ? sprayAtCruise * Mathf.Clamp01(speed01) : 0f;
            }
        }

        private void SetIgnoreCollision(Collider2D other, bool ignore)
        {
            if (other == null) return;
            foreach (var c in colliders)
                if (c != null) Physics2D.IgnoreCollision(c, other, ignore);
        }

        // Kamera görüş alanında kalıyorsa başlangıç/bitiş noktasını yol ekseni boyunca uzatır.
        private float ResolveOffscreen(float cells, float fallbackSign)
        {
            if (!ensureOffscreen) return cells;
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return cells;

            float halfH = cam.orthographicSize + 0.5f;
            float halfW = cam.orthographicSize * cam.aspect + 0.5f;
            Vector2 c = cam.transform.position;
            Bounds local = LocalVisualBounds();
            float sign = Mathf.Abs(cells) > 0.001f ? Mathf.Sign(cells) : fallbackSign;

            float result = cells;
            for (int i = 0; i < 400; i++)
            {
                Vector2 center = PointAt(result) + (Vector2)local.center;
                bool overlaps = Mathf.Abs(center.x - c.x) < halfW + local.extents.x &&
                                Mathf.Abs(center.y - c.y) < halfH + local.extents.y;
                if (!overlaps) break;
                result += sign * 0.5f;
            }
            return result;
        }

        // Bütün sprite'ların (gövde, kapı, gölge) köke göre birleşik sınırı.
        private Bounds LocalVisualBounds()
        {
            bool any = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null) continue;
                Bounds sb = sr.sprite.bounds;
                for (int i = 0; i < 4; i++)
                {
                    var corner = new Vector3(i % 2 == 0 ? sb.min.x : sb.max.x, i < 2 ? sb.min.y : sb.max.y, 0f);
                    Vector3 world = sr.transform.TransformPoint(corner);
                    Vector3 local = world - transform.position;
                    if (!any)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }
            if (!any) bounds = new Bounds(Vector3.zero, new Vector3(3f, 2f, 0f));
            return bounds;
        }

        private static float Snap(float v) => Mathf.Round(v * PixelsPerUnit) / PixelsPerUnit;
    }
}
