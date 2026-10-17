using AfetUyanisi.Core;
using AfetUyanisi.Shared;

namespace AfetUyanisi.Chapters.Rooms
{
    /// <summary>
    /// Odalar hangi bölümün içinde oynanıyor? GameSession.CurrentChapter'dan okunur; hiçbir şey seçilmediyse
    /// (oda sahnesi doğrudan Play ile açıldıysa) Sel sayılır. Diyalog ID'lerinin bölüm eki buradan gelir.
    /// </summary>
    public static class RoomChapter
    {
        public static ChapterId Current => GameSession.CurrentChapter ?? ChapterId.Flood;

        public static bool IsEarthquake => Current == ChapterId.Earthquake;

        /// <summary>Diyalog ID eki: Deprem "_E", diğer her durumda (Sel) "_F".</summary>
        public static string Suffix => IsEarthquake ? "_E" : "_F";

        /// <summary>"ROOM_WAKE" → "ROOM_WAKE_F" / "ROOM_WAKE_E".</summary>
        public static string WithSuffix(string baseId) => string.IsNullOrEmpty(baseId) ? baseId : baseId + Suffix;

        /// <summary>Bölüm seçilmeden oda açıldıysa Sel'i başlatır (banyoya geçince de bölüm bilinsin).</summary>
        public static void EnsureChapter()
        {
            if (!GameSession.CurrentChapter.HasValue)
                GameSession.StartChapter(ChapterId.Flood);
        }
    }
}
