using System;
using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Kapatılmış alçak yolun bariyeri (PROMPT §C). LowRoad/BarrierZone'daki trigger collider'a (zone) eklenir; zone suya
    /// ulaşmadan ÖNCE durur. Oyuncu alana adım atınca: hareket kısa süre kapanır, alanın dışındaki son güvenli noktaya
    /// geri kaydırılır (suya hiç girmez), engellemeyen tehlike uyarısı (kırmızı kenar + ikon + açıklama) çıkar,
    /// Hub.ReportMistake("low_road") ve bariyer satırı Hub.Say("FE_BARRIER_STOP", "Bu yol kapalı. ...") istenir.
    /// Ödül yok (kısa yol, puan, süre avantajı verilmez); oyuncu her zaman güvenli yola dönebilir.
    /// Hub.FreeRoam false iken (diyalog, panel, geçiş) çalışmaz.
    /// Collider mesajı yerine her FixedUpdate'te geometrik örtüşme kontrol edilir: zone başka bir objede de olabilir.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class BarrierStop : MonoBehaviour, IFloodEveningResettable
    {
        public const string MistakeId = "low_road";
        public const string ExactMessage = "Bu yol kapalı. Suyun derinliğini ve akıntısını bilemezsin. Güvenli rotaya dön.";

        [Tooltip("Bariyer alanı (trigger). Boşsa bu objedeki Collider2D.")]
        [SerializeField] private Collider2D zone;
        [Tooltip("Boşsa sahnedeki IsoPlayerController.")]
        [SerializeField] private IsoPlayerController player;

        [Header("Mesaj")]
        [SerializeField] private string lineId = "FE_BARRIER_STOP";
        [SerializeField, TextArea] private string message = ExactMessage;
        [SerializeField] private DangerToast toast;
        [SerializeField] private string toastTitle = "YOL KAPALI";
        [SerializeField] private string toastBody = "Suyla kaplı yola girme. İşaretli güvenli rotaya dön.";
        [SerializeField] private float toastSeconds = 3.5f;

        [Header("Geri itme")]
        [Tooltip("Girilen yerden en az bu kadar geriye (zemin uzaklığı)")]
        [SerializeField] private float pushDistance = 0.45f;
        [SerializeField] private float pushSeconds = 0.25f;
        [Tooltip("Bu süre içinde tekrar tetiklenirse yeni uyarı/kayıt yapılmaz (sn)")]
        [SerializeField] private float cooldown = 1.2f;
        [Tooltip("İşaretliyse sadece Hub.FreeRoam true iken çalışır (bölüm). Test için kapatılabilir.")]
        [SerializeField] private bool requireFreeRoam = true;

        [Header("Görsel (isteğe bağlı)")]
        [Tooltip("Tetiklenince kısa süre kırmızı yanıp sönen bariyer sprite'ları")]
        [SerializeField] private SpriteRenderer[] flash = Array.Empty<SpriteRenderer>();
        [Tooltip("Tetiklenince bariyerin üstünde zıplayan uyarı ikonu (başta kapalı)")]
        [SerializeField] private GameObject warningIcon;

        private const int HistoryLength = 50;
        private readonly Vector2[] history = new Vector2[HistoryLength];
        private int historyCount, historyHead;
        private Rigidbody2D body;
        private Collider2D playerCollider;
        private float lastTrigger = -999f;
        private Coroutine pushRoutine;
        private Color[] flashBase;

        /// <summary>Bariyer kaç kez oyuncuyu durdurdu.</summary>
        public int StopCount { get; private set; }
        public bool IsPushing { get; private set; }

        /// <summary>Oyuncu durdurulduğunda (geri itme başlarken).</summary>
        public event Action Stopped;

        private void Awake()
        {
            if (zone == null) zone = GetComponent<Collider2D>();
            if (zone != null) zone.isTrigger = true;
            if (toast == null) toast = GetComponentInChildren<DangerToast>(true);
            flashBase = new Color[flash.Length];
            for (int i = 0; i < flash.Length; i++) if (flash[i] != null) flashBase[i] = flash[i].color;
            if (warningIcon != null) warningIcon.SetActive(false);
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            if (IsPushing) EndPush(false);
        }

        private bool ResolvePlayer()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            if (player == null) return false;
            if (body == null) body = player.GetComponent<Rigidbody2D>();
            if (playerCollider == null) playerCollider = player.GetComponent<Collider2D>();
            return body != null;
        }

        private void FixedUpdate()
        {
            if (zone == null || !zone.enabled || IsPushing || !ResolvePlayer() || !player.isActiveAndEnabled) return;
            Vector2 p = body.position;
            if (historyCount > 0 && (p - HistoryAt(0)).sqrMagnitude > 0.36f) ClearHistory(); // ışınlanma

            if (!Overlaps())
            {
                Record(p);
                return;
            }
            if (requireFreeRoam && !FloodEveningHub.FreeRoam) return;
            pushRoutine = StartCoroutine(Push(p));
        }

        private bool Overlaps()
        {
            if (playerCollider != null && playerCollider.enabled)
                return zone.Distance(playerCollider).isOverlapped;
            return zone.OverlapPoint(body.position);
        }

        private IEnumerator Push(Vector2 from)
        {
            IsPushing = true;
            player.CanMove = false;
            body.linearVelocity = Vector2.zero;

            bool fresh = Time.time - lastTrigger > cooldown;
            lastTrigger = Time.time;
            if (fresh)
            {
                StopCount++;
                Debug.Log($"[BarrierStop] {name}: oyuncu kapalı yolda durduruldu ({StopCount}. kez)", this);
                FloodEveningHub.ReportMistake(MistakeId);
                if (toast != null) toast.Show(toastTitle, toastBody, toastSeconds);
                Stopped?.Invoke();
                if (flash.Length > 0 || warningIcon != null) StartCoroutine(Flash());
            }

            Vector2 to = PickSafeTarget(from);
            var anim = player.GetComponent<IsoCharacterAnimator>();
            if (anim != null && (from - to).sqrMagnitude > 0.0001f) anim.Face(from - to); // suya dönük geri adım

            for (float t = 0f; t < pushSeconds;)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                float k = Mathf.Clamp01(t / pushSeconds);
                k = 1f - (1f - k) * (1f - k);
                body.position = Vector2.Lerp(from, to, k);
                body.linearVelocity = Vector2.zero;
            }
            body.position = to;
            body.linearVelocity = Vector2.zero;
            ClearHistory();
            Record(to);
            EndPush(fresh);
        }

        private void EndPush(bool say)
        {
            IsPushing = false;
            pushRoutine = null;
            // Bölüm akışı oyuncuyu serbest bıraktıysa (FreeRoam) hareket geri gelir; diyalog vb. sürüyorsa akışa bırakılır.
            if (say && !string.IsNullOrEmpty(lineId)) FloodEveningHub.Say(lineId, message);
            if (player != null && (!requireFreeRoam || FloodEveningHub.FreeRoam)) player.CanMove = true;
        }

        private IEnumerator Flash()
        {
            if (warningIcon != null) warningIcon.SetActive(true);
            Vector3 iconBase = warningIcon != null ? warningIcon.transform.localPosition : Vector3.zero;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                bool on = ((int)(t * 6f) % 2) == 0;
                for (int i = 0; i < flash.Length; i++)
                    if (flash[i] != null) flash[i].color = on ? Color.Lerp(flashBase[i], EventsUi.Danger, 0.55f) : flashBase[i];
                if (warningIcon != null)
                    warningIcon.transform.localPosition = iconBase + new Vector3(0f, Mathf.Round(Mathf.Abs(Mathf.Sin(t * 7f)) * 4f) / 64f, 0f);
                yield return null;
            }
            for (int i = 0; i < flash.Length; i++) if (flash[i] != null) flash[i].color = flashBase[i];
            if (warningIcon != null)
            {
                warningIcon.transform.localPosition = iconBase;
                warningIcon.SetActive(false);
            }
        }

        // Geçmişte (hepsi alanın dışında) girilen yerden en az pushDistance geride kalan en yeni nokta; yoksa en eski;
        // geçmiş hiç yoksa alanın merkezinden uzağa doğru.
        private Vector2 PickSafeTarget(Vector2 from)
        {
            for (int i = 0; i < historyCount; i++)
                if (Ground(HistoryAt(i), from) >= pushDistance) return HistoryAt(i);
            if (historyCount > 0) return HistoryAt(historyCount - 1);
            Vector2 dir = from - (Vector2)zone.bounds.center;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
            return from + dir.normalized * (pushDistance + Mathf.Max(zone.bounds.extents.x, zone.bounds.extents.y) * 0.5f);
        }

        public void ResetModule()
        {
            if (pushRoutine != null) StopCoroutine(pushRoutine);
            if (IsPushing) EndPush(false);
            StopCount = 0;
            lastTrigger = -999f;
            ClearHistory();
            if (toast != null) toast.Hide();
        }

        private void Record(Vector2 p)
        {
            history[historyHead] = p;
            historyHead = (historyHead + 1) % HistoryLength;
            if (historyCount < HistoryLength) historyCount++;
        }

        private void ClearHistory() { historyCount = 0; historyHead = 0; }

        private Vector2 HistoryAt(int i) => history[((historyHead - 1 - i) % HistoryLength + HistoryLength) % HistoryLength];

        private static float Ground(Vector2 a, Vector2 b)
        {
            Vector2 d = a - b;
            return Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y);
        }
    }
}
