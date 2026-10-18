using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// İki afetli oyun akışı (lider kararı 18 Eki): oyuncu menüde hangi afeti seçerse önce o oynanır, bitince diğeri
    /// başlar, ikisi de bitince oyun sonu ekranı (GameEnd sahnesi) gelir; hiçbir afet üçüncü kez başlamaz.
    ///   Deprem seçilirse: Deprem → toplanma alanı → Sel → apartman/PIN → oyun sonu.
    ///   Sel seçilirse:    Sel → apartman/PIN → Deprem → toplanma alanı → oyun sonu.
    /// Bölümler başarıyla bitince CompleteChapter çağırır (deprem: toplanma alanı + rapor; sel: eve girildi), sonuç
    /// ekranındaki birincil buton ("Devam: … Bölümü" / "Oyunu Bitir", metni ContinueLabel) ContinueAfter'ı çağırır
    /// (ya da tek adımda FinishChapter). Menü yeni oyunu BeginNewGame ile başlatır; menüye dönmek (ReturnToMenu) oyunu
    /// sıfırlar. "Tekrar Dene" (RestartCurrentChapter) tamamlanma bilgisini değiştirmez.
    /// Sahne yüklemelerini GameSceneRouter yapar; çift tıklama / aynı karede iki girdi ikinci yüklemeyi başlatmaz.
    /// </summary>
    public static class GameFlow
    {
        /// <summary>Menüde ilk seçilen afet (yeni oyun başlayınca yazılır).</summary>
        public static ChapterId? FirstSelectedDisaster { get; private set; }
        /// <summary>Şu an oynanan afet.</summary>
        public static ChapterId? CurrentDisaster => GameSession.CurrentChapter;
        public static bool EarthquakeCompleted { get; private set; }
        public static bool FloodCompleted { get; private set; }
        /// <summary>İki afet de bitti (oyun sonu ekranı gösterildi / gösterilecek).</summary>
        public static bool GameCompleted => EarthquakeCompleted && FloodCompleted;
        /// <summary>Şu anki görev metni (bilgi amaçlı; görev sistemi yazar).</summary>
        public static string CurrentObjective { get; set; }

        /// <summary>Bu denemede (bölüm sahnesi açıldığından beri) bölümün başarıyla bittiğini gösteren GameSession bayrağı.
        /// Bölüm (yeniden) başlayınca GameSession.StartChapter bunu siler; böylece "Tekrar Dene"den sonraki başarısız
        /// denemede "Devam" butonu görünmez.</summary>
        private const string CompletedFlagPrefix = "flow_completed_";

        public static bool IsCompleted(ChapterId chapter) =>
            chapter == ChapterId.Earthquake ? EarthquakeCompleted : chapter == ChapterId.Flood && FloodCompleted;

        // P-UI (18 Eki): sel sonuç ekranı başarısızlıkta da "Deprem Bölümüne Geç" sunar. Tamamlanmadan ContinueAfter ile
        // geçilen bölüm "geçildi" sayılır: tamamlanmış olmaz (oyun sonu ekranı öyle gösterir) ama sıradaki afet seçiminde
        // oynanmış sayılır; böylece hiçbir afet üçüncü kez başlamaz.
        private static bool earthquakeSkipped, floodSkipped;

        /// <summary>Bölüm bu oyunda oynandı mı: tamamlandı ya da başarısız bitirilip "Devam" ile geçildi.</summary>
        public static bool IsAttempted(ChapterId chapter) => IsCompleted(chapter) ||
            (chapter == ChapterId.Earthquake ? earthquakeSkipped : chapter == ChapterId.Flood && floodSkipped);

        /// <summary>Bölüm bu denemede başarıyla bitti mi (sonuç ekranı "Devam"ı bununla gösterir).</summary>
        public static bool CompletedThisAttempt(ChapterId chapter) => GameSession.HasFlag(CompletedFlagPrefix + chapter);

        /// <summary>Menüden yeni oyun: tamamlanma bilgisi silinir, seçilen afet başlar.</summary>
        public static void BeginNewGame(ChapterId first)
        {
            if (!GameSceneRouter.IsUnlocked(first))
            {
                Debug.LogWarning($"[GameFlow] {first} kilitli, yeni oyun başlamadı.");
                return;
            }
            if (GameSceneRouter.IsLoading)
            {
                Debug.Log("[GameFlow] Sahne zaten yükleniyor, yeni oyun isteği yok sayıldı.");
                return;
            }
            ResetRun();
            FirstSelectedDisaster = first;
            Debug.Log($"[GameFlow] yeni oyun: önce {first}");
            GameSceneRouter.LoadChapter(first);
        }

        /// <summary>Bölüm başarıyla bitti (deprem: toplanma alanı; sel: eve girildi). Birden çok çağrılabilir.</summary>
        public static void CompleteChapter(ChapterId chapter)
        {
            if (chapter == ChapterId.Earthquake) EarthquakeCompleted = true;
            else if (chapter == ChapterId.Flood) FloodCompleted = true;
            else return;
            if (!FirstSelectedDisaster.HasValue) FirstSelectedDisaster = chapter;
            GameSession.SetFlag(CompletedFlagPrefix + chapter);
            Debug.Log($"[GameFlow] {chapter} tamamlandı (deprem={EarthquakeCompleted}, sel={FloodCompleted})");
        }

        /// <summary>Sıradaki afet (henüz oynanmamış olan); ikisi de oynandıysa (tamamlandı ya da geçildi) null.</summary>
        public static ChapterId? NextChapter(ChapterId finished)
        {
            var other = finished == ChapterId.Earthquake ? ChapterId.Flood : ChapterId.Earthquake;
            return IsAttempted(other) ? (ChapterId?)null : other;
        }

        /// <summary>Sonuç ekranının birincil butonu açılabilir mi: bölüm bitti ve sıradaki bölümün (ya da oyun sonunun) sahnesi var.</summary>
        /// <param name="evenIfIncomplete">true: bölüm başarısız bittiyse de (sel sonuç ekranının Devam butonu).</param>
        public static bool CanContinueAfter(ChapterId finished, bool evenIfIncomplete = false)
        {
            if (!evenIfIncomplete && !IsCompleted(finished)) return false;
            var next = NextChapter(finished);
            // Oyun sonu sahnesi yoksa LoadGameEnd menüye döner; "Oyunu Bitir" yine anlamlıdır.
            return !next.HasValue || Application.CanStreamedLevelBeLoaded(GameSceneRouter.FirstSceneOf(next.Value));
        }

        /// <summary>Birincil butonun metni: "Devam: Sel Bölümü" / "Devam: Deprem Bölümü" / ikisi de bittiyse "Oyunu Bitir".</summary>
        public static string ContinueLabel(ChapterId finished)
        {
            var next = NextChapter(finished);
            if (!next.HasValue) return "Oyunu Bitir";
            return next.Value == ChapterId.Flood ? "Devam: Sel Bölümü" : "Devam: Deprem Bölümü";
        }

        /// <summary>Bitirilen bölümden sonra: sıradaki afeti yükler, yoksa oyun sonu ekranına geçer.</summary>
        public static void ContinueAfter(ChapterId finished)
        {
            if (!IsCompleted(finished))
            {
                // Başarısız bitirilip geçildi: tamamlanmış sayılmaz ama bir daha sıraya girmez.
                if (finished == ChapterId.Earthquake) earthquakeSkipped = true;
                else if (finished == ChapterId.Flood) floodSkipped = true;
                Debug.Log($"[GameFlow] {finished} tamamlanmadan geçildi (bir daha başlamaz).");
            }
            var next = NextChapter(finished);
            Debug.Log($"[GameFlow] {finished} sonrası → {(next.HasValue ? next.Value.ToString() : "oyun sonu")}");
            if (next.HasValue) GameSceneRouter.LoadChapter(next.Value);
            else GameSceneRouter.LoadGameEnd();
        }

        /// <summary>CompleteChapter + ContinueAfter.</summary>
        public static void FinishChapter(ChapterId chapter)
        {
            CompleteChapter(chapter);
            ContinueAfter(chapter);
        }

        /// <summary>Oyun durumunu siler (menüye dönüş, yeni oyun). Sahne yüklemez.</summary>
        public static void ResetRun()
        {
            FirstSelectedDisaster = null;
            EarthquakeCompleted = FloodCompleted = false;
            earthquakeSkipped = floodSkipped = false;
            CurrentObjective = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetRun();
    }
}
