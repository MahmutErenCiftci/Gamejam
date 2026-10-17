using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Menünün bölüm yükleme noktası. Sahne seçimi GameSceneRouter'dadır:
    // Sel → Bedroom (odalar) → FloodChapter, Deprem → EarthquakeHome.
    // Menünün geri kalanı sadece bu sınıfı çağırır.
    public static class MenuChapterLoader
    {
        // Menüdeki görsel sıra: Deprem, Sel. Sahnede enum indeksiyle kayıtlı; sıra değiştirilmemeli.
        public enum Chapter { Earthquake, Flood }

        public static string DisplayName(Chapter chapter)
        {
            switch (chapter)
            {
                case Chapter.Earthquake: return "Deprem";
                case Chapter.Flood: return "Sel";
                default: return chapter.ToString();
            }
        }

        static ChapterId ToChapterId(Chapter chapter)
        {
            return chapter == Chapter.Flood ? ChapterId.Flood : ChapterId.Earthquake;
        }

        // Bölümün ilk sahnesi build listesinde yoksa false döner.
        public static bool TryLoad(Chapter chapter)
        {
            string firstScene = FirstSceneName(chapter);
            if (!Application.CanStreamedLevelBeLoaded(firstScene))
            {
                Debug.LogWarning($"[Menu] '{firstScene}' sahnesi build listesinde yok; bölüm henüz yüklenemiyor.");
                return false;
            }

            GameSceneRouter.LoadChapter(ToChapterId(chapter));
            return true;
        }

        // GameSceneRouter.LoadChapter'ın açacağı ilk sahne (aynı yedekleme sırasıyla).
        static string FirstSceneName(Chapter chapter)
        {
            if (chapter == Chapter.Earthquake && Application.CanStreamedLevelBeLoaded(GameSceneRouter.EarthquakeHomeScene))
                return GameSceneRouter.EarthquakeHomeScene;
            if (Application.CanStreamedLevelBeLoaded("Bedroom"))
                return "Bedroom";
            return GameSceneRouter.SceneNameOf(ToChapterId(chapter));
        }
    }
}
