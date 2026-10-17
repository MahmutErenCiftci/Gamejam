using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Menu
{
    // GEÇİCİ: Mahmut'un GameSceneRouter / ChapterId kodu (CORE-01) gelene kadar menünün
    // bölüm yükleme noktası. CORE-01 birleşince bu sınıfın içi GameSceneRouter'a yönlendirilecek;
    // menünün geri kalanı sadece bu sınıfı çağırır.
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

        // INTERFACE_CONTRACTS.md'deki sahne adları.
        static string SceneName(Chapter chapter)
        {
            switch (chapter)
            {
                case Chapter.Flood: return "FloodChapter";
                case Chapter.Earthquake: return "EarthquakeChapter";
                default: return null;
            }
        }

        // Bölüm sahnesi build listesinde yoksa false döner.
        public static bool TryLoad(Chapter chapter)
        {
            string sceneName = SceneName(chapter);
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[Menu] '{sceneName}' sahnesi build listesinde yok; bölüm henüz yüklenemiyor.");
                return false;
            }

            SceneManager.LoadScene(sceneName);
            return true;
        }
    }
}
