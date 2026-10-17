using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// PuddleHazard'ın çocuğa kendisinin eklediği yardımcı (elle eklemek gerekmez). Çocuk hiçbir (açık) tehlike
    /// alanında değilken her FixedUpdate'te konumunu kısa bir geçmişe yazar: "son güvenli noktalar".
    /// Tehlikeye adım atınca çocuğu bu geçmişten, girdiği yerden en az pushDistance geride kalan noktaya kaydırır
    /// ve hareketi kısa süre kapatıp sonra önceki değerine döndürür. Aynı anda birden fazla alan tetiklense de
    /// tek bir geri itme ve tek bir kilit çalışır. Işınlanma (oda değişimi, PlaceAt) algılanınca geçmiş silinir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IsoPlayerController))]
    public class PlayerHazardTracker : MonoBehaviour
    {
        private const int HistoryLength = 60;      // ~1.2 sn (0.02 sn fizik adımı)
        private const float TeleportDistance = 0.6f; // bir fizik adımında bundan fazla yer değişimi = ışınlanma

        private readonly Vector2[] history = new Vector2[HistoryLength];
        private readonly List<PuddleHazard> inside = new List<PuddleHazard>();
        private int historyCount;
        private int historyHead; // bir sonraki yazılacak yer

        private IsoPlayerController player;
        private IsoCharacterAnimator characterAnimator;
        private Rigidbody2D body;
        private Coroutine pushRoutine;

        private bool blockActive;
        private bool restoreValue;
        private bool restoreCancelled;
        private float blockUntil;

        /// <summary>Çocuk şu an geri itiliyor mu?</summary>
        public bool IsPushing { get; private set; }

        /// <summary>
        /// true iken tehlike alanları çocuğu durdurmaz (senaryolu yürüyüş: ör. otobüse biniş yolun kenarına iner).
        /// Senaryo bitince false yapılmalı. Çocuk gizlenince (OnDisable) kendiliğinden false olur.
        /// </summary>
        public bool Suppressed { get; set; }

        /// <summary>Hareket kilidi sürüyor mu (itme + bekleme)?</summary>
        public bool IsBlocking => blockActive;

        /// <summary>Çocuk şu an açık bir tehlike alanının içinde mi?</summary>
        public bool IsInsideHazard
        {
            get
            {
                Prune();
                foreach (var h in inside)
                    if (h.HazardEnabled) return true;
                return false;
            }
        }

        /// <summary>Kayıtlı en son güvenli nokta (kayıt yoksa çocuğun şu anki konumu).</summary>
        public Vector2 LastSafePosition => historyCount > 0 ? HistoryAt(0) : CurrentPosition;

        private Vector2 CurrentPosition => body != null ? body.position : (Vector2)transform.position;

        /// <summary>Çocuğun izleyicisini verir; yoksa ekler.</summary>
        public static PlayerHazardTracker For(IsoPlayerController player)
        {
            if (player == null) return null;
            var tracker = player.GetComponent<PlayerHazardTracker>();
            if (tracker == null) tracker = player.gameObject.AddComponent<PlayerHazardTracker>();
            return tracker;
        }

        private void Awake()
        {
            player = GetComponent<IsoPlayerController>();
            characterAnimator = GetComponent<IsoCharacterAnimator>();
            body = GetComponent<Rigidbody2D>();
        }

        private void OnDisable()
        {
            // Çocuk gizlenince (oda değişimi) yarım kalan itme/kilit bırakılır; CanMove'u bölüm akışı yönetir.
            if (pushRoutine != null) StopCoroutine(pushRoutine);
            pushRoutine = null;
            IsPushing = false;
            blockActive = false;
            Suppressed = false;
            inside.Clear();
            ClearHistory();
        }

        private void FixedUpdate()
        {
            if (IsPushing) return;
            Vector2 p = CurrentPosition;
            if (historyCount > 0 && (p - HistoryAt(0)).sqrMagnitude > TeleportDistance * TeleportDistance)
                ClearHistory();
            if (!IsInsideHazard) Record(p);
        }

        private void Update()
        {
            if (blockActive && !IsPushing && Time.time >= blockUntil) EndBlock();
        }

        /// <summary>PuddleHazard çağırır: çocuk alana girdi.</summary>
        public void NotifyEnter(PuddleHazard hazard)
        {
            if (hazard != null && !inside.Contains(hazard)) inside.Add(hazard);
        }

        /// <summary>PuddleHazard çağırır: çocuk alandan çıktı (ya da alan kapandı).</summary>
        public void NotifyExit(PuddleHazard hazard)
        {
            inside.Remove(hazard);
        }

        /// <summary>
        /// Hareketi seconds saniye kapatır; süre bitince (CanMove hâlâ false ise) kilitten önceki değerine döner.
        /// Üst üste çağrılırsa süre uzar, döndürülecek değer ilk çağrıdaki kalır.
        /// </summary>
        public void BlockInput(float seconds)
        {
            if (player == null) return;
            if (!blockActive)
            {
                blockActive = true;
                restoreValue = player.CanMove;
                restoreCancelled = false;
                blockUntil = 0f;
            }
            player.CanMove = false;
            blockUntil = Mathf.Max(blockUntil, Time.time + Mathf.Max(0f, seconds));
        }

        /// <summary>Süren kilidin sonunda CanMove geri açılmasın (hareketi artık bölüm akışı yönetecek).</summary>
        public void CancelRestore()
        {
            restoreCancelled = true;
        }

        /// <summary>
        /// Çocuğu son güvenli noktaya geri kaydırır ve hareketi blockSeconds kapatır.
        /// Zaten itiliyorsa yeni itme başlamaz (false döner).
        /// </summary>
        public bool PushBack(PuddleHazard hazard, float blockSeconds, float pushDistance, float pushSeconds)
        {
            if (IsPushing || body == null) return false;

            BlockInput(Mathf.Max(blockSeconds, pushSeconds));
            Vector2 from = body.position;
            Vector2 to = PickSafeTarget(from, pushDistance, hazard);
            pushRoutine = StartCoroutine(PushRoutine(from, to, pushSeconds));
            return true;
        }

        private IEnumerator PushRoutine(Vector2 from, Vector2 to, float seconds)
        {
            IsPushing = true;
            body.linearVelocity = Vector2.zero;
            // Çocuk suya bakarak geri adım atar.
            if (characterAnimator != null && (from - to).sqrMagnitude > 0.0001f) characterAnimator.Face(from - to);

            float t = 0f;
            while (t < seconds)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                k = 1f - (1f - k) * (1f - k); // yavaşlayarak
                body.position = Vector2.Lerp(from, to, k);
                body.linearVelocity = Vector2.zero;
            }

            body.position = to;
            body.linearVelocity = Vector2.zero;
            ClearHistory();
            Record(to);
            IsPushing = false;
            pushRoutine = null;
        }

        // Geçmişte, girilen yerden en az distance (zemin uzaklığı) geride kalan ve hiçbir açık tehlike alanının İÇİNDE
        // OLMAYAN en yeni nokta; yoksa dışarıdaki en eski nokta; o da yoksa alanın en yakın dış noktası.
        // (Hedef alanın içinde kalırsa OnTriggerStay aynı yere tekrar tekrar iterdi: sonsuz geri itme döngüsü.)
        private Vector2 PickSafeTarget(Vector2 from, float distance, PuddleHazard hazard)
        {
            for (int i = 0; i < historyCount; i++)
            {
                Vector2 sample = HistoryAt(i);
                if (GroundDistance(sample, from) >= distance && !InsideAnyHazard(sample, hazard)) return sample;
            }
            for (int i = historyCount - 1; i >= 0; i--)
            {
                Vector2 sample = HistoryAt(i);
                if (GroundDistance(sample, from) > 0.01f && !InsideAnyHazard(sample, hazard)) return sample;
            }
            return NearestOutside(from, hazard, distance);
        }

        private bool InsideAnyHazard(Vector2 p, PuddleHazard hazard)
        {
            if (hazard != null && hazard.Area != null && hazard.Area.OverlapPoint(p)) return true;
            foreach (var h in inside)
                if (h != null && h.isActiveAndEnabled && h.HazardEnabled && h.Area != null && h.Area.OverlapPoint(p)) return true;
            return false;
        }

        // Çocuk alanın içinde kaldıysa (geçmiş yok/geçmiş de içerde): 16 yönde dışarı yürüyüp en kısa çıkışı seçer.
        private Vector2 NearestOutside(Vector2 from, PuddleHazard hazard, float distance)
        {
            Vector2 best = from;
            float bestSteps = float.MaxValue;
            Vector2 bestDir = Vector2.down;
            for (int d = 0; d < 16; d++)
            {
                float a = d * Mathf.PI * 2f / 16f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f).normalized; // izometrik zemin
                Vector2 p = from;
                int steps = 0;
                while (steps < 120 && InsideAnyHazard(p, hazard)) { p += dir * 0.04f; steps++; }
                if (steps >= 120 || steps >= bestSteps) continue;
                bestSteps = steps;
                best = p;
                bestDir = dir;
            }
            return best + bestDir * Mathf.Max(0.05f, distance * 0.5f);
        }

        private void EndBlock()
        {
            blockActive = false;
            if (!restoreCancelled && player != null && !player.CanMove) player.CanMove = restoreValue;
        }

        private void Prune()
        {
            for (int i = inside.Count - 1; i >= 0; i--)
                if (inside[i] == null || !inside[i].isActiveAndEnabled) inside.RemoveAt(i);
        }

        private void Record(Vector2 p)
        {
            history[historyHead] = p;
            historyHead = (historyHead + 1) % HistoryLength;
            if (historyCount < HistoryLength) historyCount++;
        }

        private void ClearHistory()
        {
            historyCount = 0;
            historyHead = 0;
        }

        // 0 = en yeni kayıt.
        private Vector2 HistoryAt(int index) =>
            history[((historyHead - 1 - index) % HistoryLength + HistoryLength) % HistoryLength];

        private static float GroundDistance(Vector2 a, Vector2 b)
        {
            Vector2 d = a - b;
            return Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y);
        }
    }
}
