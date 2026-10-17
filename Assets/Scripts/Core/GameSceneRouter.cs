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
        public const string FloodScene = "FloodChapter";
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
        /// Gerçek akış: Bedroom → Bathroom → bölüm sahnesi (mutfaktan, bkz. ContinueChapterAfterRooms).
        /// Bedroom sahnesi Build Settings'te yoksa eskisi gibi doğrudan bölüm sahnesi açılır (açılış orada oynar).
        /// </summary>
        public static void LoadChapter(ChapterId chapter)
        {
            if (!IsUnlocked(chapter))
            {
                Debug.LogWarning($"[GameSceneRouter] {chapter} kilitli, yüklenmedi.");
                return;
            }

            GameSession.StartChapter(chapter);

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

            Debug.Log($"[GameSceneRouter] {chapter} başlıyor: '{BedroomScene}' Build Settings'te yok, " +
                      $"doğrudan '{SceneNameOf(chapter)}' açılıyor (sabah açılışı bölüm sahnesinde oynar).");
            SceneManager.LoadScene(SceneNameOf(chapter));
        }

        /// <summary>Oda sahnelerinden sonra açılacak bölüm sahnesi (bölüm seçili değilse ya da yüklenemiyorsa null).</summary>
        public static string ChapterSceneAfterRooms()
        {
            if (!GameSession.CurrentChapter.HasValue) return null;
            string scene = SceneNameOf(GameSession.CurrentChapter.Value);
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

        /// <summary>Sonuç ekranındaki "Tekrar Dene" (Bedroom varsa bölüm yatak odasından yeniden başlar).</summary>
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
