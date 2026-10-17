using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// Sahne geçişlerinin tek kapısı. Menü bölüm başlatırken, bölüm sonuç ekranından dönerken bunu çağırır;
    /// başka yerde SceneManager.LoadScene kullanılmaz. Sahne adlarını ve build listesini sadece Mahmut değiştirir.
    /// </summary>
    public static class GameSceneRouter
    {
        public const string MainMenuScene = "MainMenu";
        /// <summary>Sel bölümü: akşam mahalle seli (Assets/Scenes/Chapters/Flood/FloodEvening.unity). Eski sabah akışının yerine geçer.</summary>
        public const string FloodScene = "FloodEvening";
        /// <summary>Eski sabah sel akışının bölüm sahnesi (Bedroom → Bathroom → FloodChapter). Artık oyunda kullanılmıyor, silinmedi.</summary>
        public const string LegacyFloodScene = "FloodChapter";
        public const string EarthquakeScene = "EarthquakeChapter";
        /// <summary>Deprem modu (120 sn farkındalık simülasyonu) sahnesi: Assets/Scenes/Chapters/Earthquake/EarthquakeHome.unity.</summary>
        public const string EarthquakeHomeScene = "EarthquakeHome";

        /// <summary>Jam kapsamında sadece Sel ve Deprem açık; diğerleri menüde "Yakında".</summary>
        public static bool IsUnlocked(ChapterId chapter) =>
            chapter == ChapterId.Flood || chapter == ChapterId.Earthquake;

        public static string SceneNameOf(ChapterId chapter)
        {
            switch (chapter)
            {
                case ChapterId.Flood: return FloodScene;
                case ChapterId.Earthquake: return EarthquakeScene;
                default: return null;
            }
        }

        /// <summary>
        /// Bölümü başlatır. Kilitli bölümde hiçbir şey yapmaz (menü "Yakında" gösterir).
        /// Sel → FloodEvening (akşam mahalle seli), Deprem → EarthquakeHome. Bu sahneler build listesinde yoksa eski akış:
        /// Bedroom → Bathroom → bölüm sahnesi (mutfaktan, bkz. ContinueChapterAfterRooms), o da yoksa doğrudan bölüm sahnesi.
        /// </summary>
        public static void LoadChapter(ChapterId chapter)
        {
            if (!IsUnlocked(chapter))
            {
                Debug.LogWarning($"[GameSceneRouter] {chapter} kilitli, yüklenmedi.");
                return;
            }

            GameSession.StartChapter(chapter);

            // Sel: akşam mahalle seli kendi sahnesinde baştan sona oynar (yatak odası/banyo açılışı yok).
            if (chapter == ChapterId.Flood && Application.CanStreamedLevelBeLoaded(FloodScene))
            {
                Debug.Log($"[GameSceneRouter] Sel bölümü başlıyor: '{FloodScene}'.");
                SceneManager.LoadScene(FloodScene);
                return;
            }

            // Deprem modu kendi sahnesinde baştan sona oynar (yatak odası + salon + koridor + toplanma alanı).
            if (chapter == ChapterId.Earthquake && Application.CanStreamedLevelBeLoaded(EarthquakeHomeScene))
            {
                Debug.Log($"[GameSceneRouter] Deprem modu başlıyor: '{EarthquakeHomeScene}'.");
                SceneManager.LoadScene(EarthquakeHomeScene);
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(BedroomScene))
            {
                Debug.Log($"[GameSceneRouter] {chapter} başlıyor: önce oda sahnesi '{BedroomScene}'.");
                SceneManager.LoadScene(BedroomScene);
                return;
            }

            string direct = chapter == ChapterId.Flood ? LegacyFloodScene : SceneNameOf(chapter);
            Debug.Log($"[GameSceneRouter] {chapter} başlıyor: '{BedroomScene}' Build Settings'te yok, " +
                      $"doğrudan '{direct}' açılıyor (sabah açılışı bölüm sahnesinde oynar).");
            SceneManager.LoadScene(direct);
        }

        /// <summary>Oda sahnelerinden sonra açılacak bölüm sahnesi (bölüm seçili değilse ya da yüklenemiyorsa null).</summary>
        public static string ChapterSceneAfterRooms()
        {
            if (!GameSession.CurrentChapter.HasValue) return null;
            var current = GameSession.CurrentChapter.Value;
            // Odalar yalnız eski sabah akışında kullanılır: Sel için eski bölüm sahnesine devam eder.
            string scene = current == ChapterId.Flood ? LegacyFloodScene : SceneNameOf(current);
            return !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene) ? scene : null;
        }

        /// <summary>
        /// Banyodaki "[E] Mutfağa git": sabah açılışının odalarda bittiğini kaydeder (GameSession.MorningDoneInRooms)
        /// ve şu anki bölümün sahnesini açar; bölüm kendi açılışını atlayıp mutfaktan (F03/E03) başlar.
        /// Bölüm sahnesi yüklenemiyorsa hata yazar, hiçbir şey yapmaz ve false döner.
        /// </summary>
        public static bool ContinueChapterAfterRooms()
        {
            string scene = ChapterSceneAfterRooms();
            if (scene == null)
            {
                Debug.LogError($"[GameSceneRouter] Odalardan sonra bölüm sahnesi açılamadı (bölüm: " +
                               $"{(GameSession.CurrentChapter.HasValue ? GameSession.CurrentChapter.Value.ToString() : "yok")}); " +
                               "sahne Build Settings listesinde mi?");
                return false;
            }

            GameSession.MarkMorningDoneInRooms();
            Debug.Log($"[GameSceneRouter] Odalar bitti → '{scene}' (mutfaktan devam).");
            SceneManager.LoadScene(scene);
            return true;
        }

        /// <summary>LoadChapter'ın açacağı ilk sahnenin adı (menü build listesini bununla kontrol eder).</summary>
        public static string FirstSceneOf(ChapterId chapter)
        {
            if (chapter == ChapterId.Flood && Application.CanStreamedLevelBeLoaded(FloodScene)) return FloodScene;
            if (chapter == ChapterId.Earthquake && Application.CanStreamedLevelBeLoaded(EarthquakeHomeScene)) return EarthquakeHomeScene;
            if (Application.CanStreamedLevelBeLoaded(BedroomScene)) return BedroomScene;
            return chapter == ChapterId.Flood ? LegacyFloodScene : SceneNameOf(chapter);
        }

        /// <summary>Bölüm sırası kesindir: DEPREM → SEL. Bitirilen bölümden sonraki bölüm (yoksa null).</summary>
        public static ChapterId? NextChapterAfter(ChapterId finished) =>
            finished == ChapterId.Earthquake ? ChapterId.Flood : (ChapterId?)null;

        private static bool nextChapterRequested;

        /// <summary>
        /// Deprem raporundaki "Devam: Sel Bölümü": menüye dönmeden sıradaki bölümü başlatır. Yalnız bir kez çalışır
        /// (çift tıklama / aynı karede iki girdi ikinci yüklemeyi başlatmaz; yeni sahne yüklenince sıfırlanır).
        /// Sonraki bölüm yoksa ya da sahnesi yüklenemiyorsa false döner.
        /// </summary>
        public static bool ContinueToNextChapter(ChapterId finished)
        {
            var next = NextChapterAfter(finished);
            if (!next.HasValue || !Application.CanStreamedLevelBeLoaded(FirstSceneOf(next.Value))) return false;
            if (nextChapterRequested)
            {
                Debug.Log("[GameSceneRouter] Sonraki bölüm zaten yükleniyor, ikinci istek yok sayıldı.");
                return true;
            }
            nextChapterRequested = true;
            SceneManager.sceneLoaded -= ClearNextRequest;
            SceneManager.sceneLoaded += ClearNextRequest;
            Debug.Log($"[GameSceneRouter] {finished} bitti → {next.Value} (menüsüz geçiş).");
            LoadChapter(next.Value);
            return true;
        }

        private static void ClearNextRequest(Scene scene, LoadSceneMode mode)
        {
            nextChapterRequested = false;
            SceneManager.sceneLoaded -= ClearNextRequest;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => nextChapterRequested = false;

        /// <summary>Sonuç ekranındaki "Tekrar Dene": şu anki bölümü baştan açar (Sel → FloodEvening; Sel tekrarı depremi başlatmaz).</summary>
        public static void RestartCurrentChapter()
        {
            if (GameSession.CurrentChapter.HasValue)
                LoadChapter(GameSession.CurrentChapter.Value);
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Sonuç ekranındaki "Menüye Dön".</summary>
        public static void ReturnToMenu()
        {
            GameSession.Clear();
            SceneManager.LoadScene(MainMenuScene);
        }

        public const string BedroomScene = "Bedroom";
        public const string BathroomScene = "Bathroom";

        /// <summary>
        /// Bölüm içinde bir odadan diğerine geçer (yatak odası → banyo → ...). Bölüm bilgisi (GameSession) korunur.
        /// Sahne Build Settings listesinde yoksa hata yazar ve hiçbir şey yapmaz.
        /// </summary>
        public static void LoadRoom(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameSceneRouter] Oda sahnesi '{sceneName}' Build Settings listesinde yok, yüklenmedi.");
                return;
            }

            Debug.Log($"[GameSceneRouter] Oda yükleniyor: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
    }
}
