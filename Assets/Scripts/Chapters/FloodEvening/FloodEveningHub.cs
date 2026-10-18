using System;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel bölümünün modülleri (rüzgâr, dal, kapı, şifre, söylenti, bariyer) ile FloodEveningController arasındaki
    /// ortak nokta. Modüller bunu çağırır/dinler; controller'a doğrudan bağlanmaz.
    /// </summary>
    public static class FloodEveningHub
    {
        /// <summary>0..1 ortak ilerleme (süre = su seviyesi = hava yoğunluğu). Controller yazar.</summary>
        public static float Progress01 { get; set; }
        /// <summary>Süre/ilerleme şu an duruyor mu (diyalog, kart, geçiş). Controller yazar.</summary>
        public static bool Paused { get; set; }
        /// <summary>Oyuncu serbest yürüyebilir mi (diyalog/panel/mini oyun kapalı). Controller yazar.</summary>
        public static bool FreeRoam { get; set; }
        /// <summary>Sel sayacı (120 sn) başladı mı. Telefon uyarısı kapanınca bir kez true olur; controller yazar.</summary>
        public static bool FloodStarted { get; set; }
        /// <summary>Sel sayacı durdu mu (güvenli bölgeye varıldı ya da süre doldu): su ve yağmur bu seviyede kalır. Controller yazar.</summary>
        public static bool FloodStopped { get; set; }
        /// <summary>Sel sayacının toplam süresi (sn). Controller yazar; ekran süreyi buradan hesaplar.</summary>
        public static float FloodSeconds { get; set; } = 120f;
        /// <summary>Dış ortam seslerinin (yağmur, akan su) çarpanı 0..1: Cenk eve girerken controller/final yavaşça düşürür.</summary>
        public static float OutdoorAudio01 { get; set; } = 1f;

        public static event Action<string> MinigameOpened;     // id: "gate", "code", "branch", "rumor"
        public static event Action<string> MinigameClosed;
        public static event Action<string> StepCompleted;      // id: "warning_read","rumor_checked","branch_cleared","gate_opened","code_opened","safe_zone"
        public static event Action<string> Mistake;            // id: "low_road","rumor_trusted","wrong_code" (bilgi amaçlı)
        public static event Action<string, string> SayRequest; // (lineId, fallbackText) — controller diyalogla gösterir
        public static event Action ResetRequested;             // bölüm yeniden başlarken modüller kendini sıfırlar

        public static void NotifyOpened(string id) => MinigameOpened?.Invoke(id);
        public static void NotifyClosed(string id) => MinigameClosed?.Invoke(id);
        public static void Complete(string id) => StepCompleted?.Invoke(id);
        public static void ReportMistake(string id) => Mistake?.Invoke(id);
        public static void Say(string lineId, string fallback) => SayRequest?.Invoke(lineId, fallback);
        public static void RequestReset() => ResetRequested?.Invoke();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Progress01 = 0f; Paused = false; FreeRoam = false; FloodStarted = false; FloodStopped = false; FloodSeconds = 120f; OutdoorAudio01 = 1f;
            MinigameOpened = null; MinigameClosed = null; StepCompleted = null; Mistake = null; SayRequest = null; ResetRequested = null;
        }
    }
}
