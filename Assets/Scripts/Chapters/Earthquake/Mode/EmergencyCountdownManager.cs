using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// 60 sn eğitim sayacı (sağ üst). Sinema, oda geçişi, bilgi kartı ve soru sırasında durur:
    /// iç içe duraklatma için PushPause(token)/PopPause(token) kullanılır (aynı token bir kez sayılır),
    /// PauseTimer/ResumeTimer elle duraklatmadır. Süre asla negatife düşmez; 0'a inince (duraklatılmamışsa)
    /// OnTimerExpired bir kez tetiklenir. Ceza ApplyTimePenalty ile düşülür, görünüm CountdownViewBase'e yazılır.
    /// Son tickFromSeconds saniyede her tam saniyede onTick (ses kancası) çağrılır.
    /// </summary>
    public class EmergencyCountdownManager : MonoBehaviour
    {
        /// <summary>UnityEvent&lt;int&gt; (Inspector'da görünsün diye).</summary>
        [Serializable] public class IntEvent : UnityEvent<int> { }

        [Tooltip("Varsayılan başlangıç süresi (sn). StartTimer'a değer verilirse o kullanılır (JSON countdownSeconds).")]
        [SerializeField] private float startSeconds = 60f;
        [Tooltip("Sağ üst sayaç görünümü (UI agent'ının CountdownView'ı). Boşsa sayaç sadece Console'a yazar.")]
        [SerializeField] private CountdownViewBase view;
        [Tooltip("Son kaç saniyede tik sesi çalsın")]
        [SerializeField] private int tickFromSeconds = 10;
        [Tooltip("Son saniyelerde her tam saniyede çağrılır (tik sesi kancası; parametre: kalan tam saniye)")]
        [SerializeField] private IntEvent onTick = new IntEvent();
        [Tooltip("Ceza uygulanınca çağrılır (ceza sesi kancası; parametre: düşülen saniye)")]
        [SerializeField] private IntEvent onPenaltySound = new IntEvent();
        [Tooltip("Süre bitince çağrılır (deprem başlangıcı ses/efekt kancası)")]
        [SerializeField] private UnityEvent onExpired = new UnityEvent();
        [Tooltip("Her 10 sn'de bir Console'a kalan süreyi yazar (test için)")]
        [SerializeField] private bool logEveryTenSeconds = true;

        private readonly HashSet<object> pauseTokens = new HashSet<object>();
        private readonly object manualToken = new object();
        private float remaining;
        private float initial;
        private int lastTickSecond = -1;
        private int lastLoggedTen = -1;

        /// <summary>Süre bittiğinde bir kez tetiklenir.</summary>
        public event Action OnTimerExpired;
        /// <summary>Ceza uygulanınca: (düşülen saniye, sebep).</summary>
        public event Action<float, string> OnPenalty;
        /// <summary>Süre eklenince: (eklenen saniye, sebep).</summary>
        public event Action<float, string> OnBonus;

        public bool IsRunning { get; private set; }
        public bool HasExpired { get; private set; }
        public bool IsPaused => pauseTokens.Count > 0;
        public float InitialSeconds => initial;

        /// <summary>Görünüm (mod kontrolcüsü boşsa sahnede bulup verir).</summary>
        public CountdownViewBase View
        {
            get => view;
            set => view = value;
        }

        private void Awake()
        {
            remaining = startSeconds;
            initial = startSeconds;
        }

        private void Start()
        {
            if (view != null && !IsRunning) view.SetVisible(false);
        }

        /// <summary>Sayacı baştan başlatır (seconds &lt;= 0 ise Inspector'daki startSeconds). Duraklatmalar temizlenir.</summary>
        public void StartTimer(float seconds = -1f)
        {
            initial = seconds > 0f ? seconds : startSeconds;
            remaining = initial;
            pauseTokens.Clear();
            HasExpired = false;
            IsRunning = true;
            lastTickSecond = -1;
            lastLoggedTen = Mathf.CeilToInt(remaining / 10f);
            if (view != null)
            {
                view.SetVisible(true);
                view.SetStatusText(null);
                view.SetTime(remaining);
            }
            Debug.Log($"[EQ] sayaç başladı: {remaining:0} sn", this);
        }

        /// <summary>Sayacı durdurur (bitirmeden). OnTimerExpired çağrılmaz.</summary>
        public void StopTimer()
        {
            IsRunning = false;
        }

        /// <summary>Elle duraklat (PushPause ile aynı yığına girer).</summary>
        public void PauseTimer() => PushPause(manualToken);

        /// <summary>Elle duraklatmayı kaldırır.</summary>
        public void ResumeTimer() => PopPause(manualToken);

        /// <summary>İç içe duraklatma: her token bir kez sayılır, hepsi Pop edilince sayaç devam eder.</summary>
        public void PushPause(object token)
        {
            if (token == null) return;
            pauseTokens.Add(token);
        }

        /// <summary>PushPause ile verilen token'ı kaldırır (olmayan token sessizce yok sayılır).</summary>
        public void PopPause(object token)
        {
            if (token == null) return;
            pauseTokens.Remove(token);
        }

        /// <summary>Bütün duraklatmaları kaldırır.</summary>
        public void ClearPauses() => pauseTokens.Clear();

        public float GetRemainingTime() => Mathf.Max(0f, remaining);

        /// <summary>
        /// Ceza: süreden seconds düşer (asla 0'ın altına inmez). Gerçekten düşülen miktarı döner.
        /// Sayaç çalışmıyorsa (deprem sonrası) süre değişmez, 0 döner.
        /// </summary>
        public float ApplyTimePenalty(float seconds, string reason)
        {
            if (seconds <= 0f) return 0f;
            if (!IsRunning || HasExpired)
            {
                Debug.Log($"[EQ] ceza kaydedildi ama sayaç çalışmıyor (-{seconds:0} sn, {reason})", this);
                return 0f;
            }
            float before = remaining;
            remaining = Mathf.Max(0f, remaining - seconds);
            float applied = before - remaining;
            Debug.Log($"[EQ] CEZA -{seconds:0} sn ({reason}) → kalan {remaining:0.0}", this);
            if (view != null)
            {
                view.ShowPenalty(Mathf.RoundToInt(seconds), reason);
                view.SetTime(remaining);
            }
            if (onPenaltySound != null) onPenaltySound.Invoke(Mathf.RoundToInt(seconds));
            OnPenalty?.Invoke(seconds, reason);
            return applied;
        }

        /// <summary>Süre ekler (tasarım isterse; şu an akışta kullanılmıyor).</summary>
        public void AddTimeBonus(float seconds, string reason = null)
        {
            if (seconds <= 0f || !IsRunning || HasExpired) return;
            remaining += seconds;
            if (view != null) view.SetTime(remaining);
            OnBonus?.Invoke(seconds, reason);
        }

        /// <summary>Hızlandırma: kalan süre toSeconds'tan fazlaysa toSeconds'a indirir (ceza değildir). Değiştiyse true.</summary>
        public bool Accelerate(float toSeconds)
        {
            if (!IsRunning || HasExpired || remaining <= toSeconds) return false;
            Debug.Log($"[EQ] hızlandırma: {remaining:0.0} → {toSeconds:0} sn", this);
            remaining = Mathf.Max(0f, toSeconds);
            if (view != null) view.SetTime(remaining);
            return true;
        }

        /// <summary>Durum yazısı ("SARSINTI!"); null/boş = süre gösterilir.</summary>
        public void SetStatusText(string text)
        {
            if (view != null) view.SetStatusText(text);
        }

        public void SetVisible(bool visible)
        {
            if (view != null) view.SetVisible(visible);
        }

        private void Update()
        {
            if (!IsRunning || HasExpired) return;
            if (IsPaused) return;

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (view != null) view.SetTime(remaining);

            int whole = Mathf.CeilToInt(remaining);
            if (whole <= tickFromSeconds && whole > 0 && whole != lastTickSecond)
            {
                lastTickSecond = whole;
                if (onTick != null) onTick.Invoke(whole);
            }

            if (logEveryTenSeconds)
            {
                int ten = Mathf.CeilToInt(remaining / 10f);
                if (ten != lastLoggedTen)
                {
                    lastLoggedTen = ten;
                    Debug.Log($"[EQ] sayaç {remaining:0.0} sn", this);
                }
            }

            if (remaining <= 0f) Expire();
        }

        private void Expire()
        {
            if (HasExpired) return;
            HasExpired = true;
            IsRunning = false;
            remaining = 0f;
            if (view != null) view.SetTime(0f);
            Debug.Log("[EQ] sayaç bitti → deprem", this);
            if (onExpired != null) onExpired.Invoke();
            OnTimerExpired?.Invoke();
        }
    }
}
