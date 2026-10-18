using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Süre cezaları (Inspector'dan ayarlı). Apply(tür, benzersizAnahtar): aynı anahtar yalnızca bir kez ceza verir
    /// (ör. "zone:mirror", "q:Q1"), böylece aynı olay üst üste ya da her karede ceza vermez. Doğru cevap ve
    /// inceleme hiçbir zaman ceza değildir. Her ceza kayıt listesine (Records) yazılır; süreyi EmergencyCountdownManager düşer.
    /// </summary>
    public class TimePenaltySystem : MonoBehaviour
    {
        /// <summary>Ceza türleri.</summary>
        public enum PenaltyType { WrongAnswer, Timeout, MirrorExposure, IgnoredWarning, Custom }

        /// <summary>Bir ceza kaydı.</summary>
        [Serializable]
        public class PenaltyRecord
        {
            public PenaltyType type;
            public string key;
            public int seconds;
            public string reason;
            public float atRemaining;
        }

        [Tooltip("Sayaç (ceza buradan düşülür). Boşsa mod kontrolcüsü verir.")]
        [SerializeField] private EmergencyCountdownManager countdown;
        [Tooltip("Yanlış cevap cezası (sn)")]
        [SerializeField] private int wrongAnswer = 30;
        [Tooltip("Soru süresi dolunca ceza (sn)")]
        [SerializeField] private int timeout = 30;
        [Tooltip("Ayna (cam) tehlike bölgesinde uyarıdan sonra uzun kalma cezası (sn, bir kez)")]
        [SerializeField] private int mirror = 10;
        [Tooltip("Açık bir uyarıyı yok sayma cezası (sn, bir kez)")]
        [SerializeField] private int ignoredWarning = 10;
        [Tooltip("İşaretliyse soru verisindeki 'penalty' değeri yanlış/süre dolu cezası yerine kullanılır")]
        [SerializeField] private bool useQuestionPenaltyFromData = true;

        private readonly HashSet<string> usedKeys = new HashSet<string>();
        private readonly List<PenaltyRecord> records = new List<PenaltyRecord>();

        /// <summary>Ceza uygulanınca (kayıt).</summary>
        public event Action<PenaltyRecord> OnApplied;

        public IReadOnlyList<PenaltyRecord> Records => records;
        public bool UseQuestionPenaltyFromData => useQuestionPenaltyFromData;

        public EmergencyCountdownManager Countdown
        {
            get => countdown;
            set => countdown = value;
        }

        /// <summary>Türün Inspector'daki ceza miktarı.</summary>
        public int AmountOf(PenaltyType type)
        {
            switch (type)
            {
                case PenaltyType.WrongAnswer: return wrongAnswer;
                case PenaltyType.Timeout: return timeout;
                case PenaltyType.MirrorExposure: return mirror;
                case PenaltyType.IgnoredWarning: return ignoredWarning;
                default: return 0;
            }
        }

        /// <summary>Bu anahtarla daha önce ceza verildi mi?</summary>
        public bool WasApplied(string uniqueKey) => !string.IsNullOrEmpty(uniqueKey) && usedKeys.Contains(uniqueKey);

        /// <summary>
        /// Cezayı uygular. Aynı uniqueKey ikinci kez gelirse hiçbir şey yapmaz ve 0 döner.
        /// overrideSeconds &gt;= 0 verilirse türün miktarı yerine o kullanılır. Döner: kaydedilen ceza (sn).
        /// </summary>
        public int Apply(PenaltyType type, string uniqueKey, string reason = null, int overrideSeconds = -1)
        {
            string key = string.IsNullOrEmpty(uniqueKey) ? type + ":" + records.Count : uniqueKey;
            if (usedKeys.Contains(key))
            {
                Debug.Log($"[EQ] ceza tekrar edilmedi ({key})", this);
                return 0;
            }
            usedKeys.Add(key);

            int seconds = Mathf.Max(0, overrideSeconds >= 0 ? overrideSeconds : AmountOf(type));
            var record = new PenaltyRecord
            {
                type = type,
                key = key,
                seconds = seconds,
                reason = reason ?? type.ToString(),
                atRemaining = countdown != null ? countdown.GetRemainingTime() : 0f
            };
            records.Add(record);
            if (seconds > 0 && countdown != null) countdown.ApplyTimePenalty(seconds, record.reason);
            OnApplied?.Invoke(record);
            return seconds;
        }

        /// <summary>Toplam ceza (sn).</summary>
        public int TotalSeconds()
        {
            int total = 0;
            foreach (var r in records) total += r.seconds;
            return total;
        }

        /// <summary>Türüne göre ceza sayısı.</summary>
        public int CountOf(PenaltyType type)
        {
            int n = 0;
            foreach (var r in records)
                if (r.type == type) n++;
            return n;
        }

        /// <summary>Yeni oynanış için kayıtları siler.</summary>
        public void ResetAll()
        {
            usedKeys.Clear();
            records.Clear();
        }
    }
}
