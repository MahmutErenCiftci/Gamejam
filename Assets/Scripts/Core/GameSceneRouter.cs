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

        /// <summary>Bölümü başlatır. Kilitli bölümde hiçbir şey yapmaz (menü "Yakında" gösterir).</summary>
        public static void LoadChapter(ChapterId chapter)
        {
            if (!IsUnlocked(chapter))
            {
                Debug.LogWarning($"[GameSceneRouter] {chapter} kilitli, yüklenmedi.");
                return;
            }

            GameSession.StartChapter(chapter);
            SceneManager.LoadScene(SceneNameOf(chapter));
        }

        /// <summary>Sonuç ekranındaki "Tekrar Dene".</summary>
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
    }
}
