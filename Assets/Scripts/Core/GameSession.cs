using System.Collections.Generic;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// Sahneler arasında taşınan küçük oyun durumu: şu an hangi bölüm oynanıyor ve odalarda neler yapıldı
    /// (ör. şemsiye alındı mı). Bölüm başlayınca (StartChapter) bayraklar sıfırlanır.
    /// </summary>
    public static class GameSession
    {
        /// <summary>Yatak odasında şemsiye alındı mı bayrağının adı (Sel).</summary>
        public const string UmbrellaFlag = "umbrella_taken";

        /// <summary>
        /// Sabah açılışı (uyanış + banyo) oda sahnelerinde (Bedroom → Bathroom) oynandı bayrağının adı.
        /// Banyodan bölüm sahnesine geçerken GameSceneRouter.ContinueChapterAfterRooms koyar; bölüm sahnesi
        /// bunu görünce kendi açılışını (PlayMorningIntro) atlar ve mutfaktan (F03/E03) başlar.
        /// </summary>
        public const string MorningDoneInRoomsFlag = "morning_done_in_rooms";

        private static readonly HashSet<string> flags = new HashSet<string>();

        public static ChapterId? CurrentChapter { get; private set; }

        /// <summary>Oyuncu yatak odasında şemsiyeyi aldı mı (sadece Sel'de alınabilir).</summary>
        public static bool UmbrellaTaken => HasFlag(UmbrellaFlag);

        /// <summary>Sabah açılışı oda sahnelerinde oynandı mı (bölüm sahnesi mutfaktan başlar).</summary>
        public static bool MorningDoneInRooms => HasFlag(MorningDoneInRoomsFlag);

        /// <summary>Bölümü başlatır; önceki oynanıştan kalan bayraklar (şemsiye vb.) silinir.</summary>
        public static void StartChapter(ChapterId chapter)
        {
            CurrentChapter = chapter;
            flags.Clear();
        }

        public static void Clear()
        {
            CurrentChapter = null;
            flags.Clear();
        }

        /// <summary>Şemsiyenin alındığını kaydeder (yatak odasındaki "[E] Şemsiyeyi al").</summary>
        public static void MarkUmbrellaTaken() => SetFlag(UmbrellaFlag);

        /// <summary>Sabah açılışının odalarda bittiğini kaydeder (banyodaki "[E] Mutfağa git").</summary>
        public static void MarkMorningDoneInRooms() => SetFlag(MorningDoneInRoomsFlag);

        /// <summary>Bu oynanışta bir şeyin yapıldığını kaydeder (bölüm başlayınca silinir).</summary>
        public static void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) flags.Add(flag);
        }

        public static bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        // Play'ler arasında static değer kalmasın (domain reload kapatılırsa diye).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}
