using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Core
{
    /// <summary>Sahneler arasında taşınan küçük oyun durumu: şu an hangi bölüm oynanıyor.</summary>
    public static class GameSession
    {
        public static ChapterId? CurrentChapter { get; private set; }

        public static void StartChapter(ChapterId chapter) => CurrentChapter = chapter;

        public static void Clear() => CurrentChapter = null;

        // Play'ler arasında static değer kalmasın (domain reload kapatılırsa diye).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => CurrentChapter = null;
    }
}
