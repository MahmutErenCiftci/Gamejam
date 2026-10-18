using System;

namespace AfetUyanisi.Chapters.Earthquake.Mode
{
    /// <summary>
    /// Deprem modundaki mini oyunların (afet çantası, kitaplık sabitleme...) ortak açılış/kapanış ve görev bildirim noktası.
    /// HUD bunu dinler (kompakt mod), mini oyunlar bunu çağırır. Sayaç bitince EarthquakeModeController RequestInterruptAll() çağırır.
    /// </summary>
    public static class EarthquakeMinigameHub
    {
        public static event Action<string> Opened;
        public static event Action<string> Closed;
        public static event Action<string> TaskCompleted;
        public static event Action InterruptRequested;

        public static int OpenCount { get; private set; }
        public static bool AnyOpen => OpenCount > 0;

        public static void NotifyOpened(string minigameId) { OpenCount++; Opened?.Invoke(minigameId); }
        public static void NotifyClosed(string minigameId) { if (OpenCount > 0) OpenCount--; Closed?.Invoke(minigameId); }
        public static void CompleteTask(string taskId) => TaskCompleted?.Invoke(taskId);
        public static void RequestInterruptAll() => InterruptRequested?.Invoke();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Opened = null; Closed = null; TaskCompleted = null; InterruptRequested = null; OpenCount = 0; }
    }
}
