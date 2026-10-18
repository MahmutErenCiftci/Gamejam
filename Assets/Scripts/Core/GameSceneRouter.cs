using System;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// Sahne geçişlerinin tek kapısı. Menü bölüm başlatırken, bölüm sonuç ekranından dönerken bunu çağırır;
    /// başka yerde SceneManager.LoadScene kullanılmaz. Sahne adlarını ve build listesini sadece Mahmut değiştirir.
    /// Bölümler arası sıra (Deprem ↔ Sel, oyun sonu) GameFlow'dadır; burası yalnız sahneyi açar.
    /// Bütün yüklemeler tek yerden geçer (Load): yükleme sürerken gelen ikinci istek (çift tıklama, aynı karede iki girdi)
    /// yok sayılır; yeni sahne açılınca kilit kalkar. Her geçişte Time.timeScale = 1 olur ve SceneChanging olayı çalışır
    /// (bölümlerin static durumları — ör. sel sayacı — bir sonraki bölüme sızmasın).
    /// </summary>
    public static class GameSceneRouter
    {
        public const string MainMenuScene = "MainMenu";
        /// <summary>Sel bölümü: akşam mahalle seli (Assets/Scenes/Chapters/Flood/FloodEvening.unity). Eski sabah akışının yerine geçer.</summary>
        public const string FloodScene = "FloodEvening";
        /// <summary>Eski sabah sel akışının bölüm sahnesi (Bedroom → Bathroom → FloodChapter). Artık oyunda kullanılmıyor, silinmedi.</summary>
        public const string LegacyFloodScene = "FloodChapter";
        /// <summary>Sel bölümünün ilk sahnesi: okul çıkışı + köprü (Assets/Scenes/Chapters/Flood/FloodSchool.unity); bitince FloodEvening'e geçer.</summary>
        public const string FloodSchoolScene = "FloodSchool";
        public const string EarthquakeScene = "EarthquakeChapter";
        /// <summary>Deprem modu (120 sn farkındalık simülasyonu) sahnesi: Assets/Scenes/Chapters/Earthquake/EarthquakeHome.unity.</summary>
        public const string EarthquakeHomeScene = "EarthquakeHome";
        /// <summary>Oyun sonu ekranı sahnesi (iki afet de bitince, GameFlow.ContinueAfter): Assets/Scenes/GameEnd.unity.</summary>
        public const string GameEndScene = "GameEnd";
        public const string BedroomScene = "Bedroom";
        public const string BathroomScene = "Bathroom";

        /// <summary>Bir sahne geçişi başlamadan hemen önce (yükleme isteği kabul edilince) çağrılır.</summary>
        public static event Action SceneChanging;

        /// <summary>Bir sahne yüklemesi istendi ve yeni sahne henüz açılmadı (ikinci istekler yok sayılır).</summary>
        public static bool IsLoading => loadPending && Time.realtimeSinceStartup - loadRequestedAt < LoadGuardSeconds;

        // Yükleme kilidi: sahne açılınca (sceneLoaded) kalkar; bir şey ters giderse en geç LoadGuardSeconds sonra kendiliğinden düşer.
        private const float LoadGuardSeconds = 5f;
        private static bool loadPending;
        private static float loadRequestedAt;
        private static string pendingScene;

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
        /// Bölümü başlatır (FirstSceneOf'un verdiği ilk sahne). Kilitli bölümde hiçbir şey yapmaz (menü "Yakında" gösterir).
        /// Bölüm sırasını bilmez: menü GameFlow.BeginNewGame, sonuç ekranları GameFlow.ContinueAfter üzerinden çağırır.
        /// </summary>
        public static void LoadChapter(ChapterId chapter)
        {
            if (!IsUnlocked(chapter))
            {
                Debug.LogWarning($"[GameSceneRouter] {chapter} kilitli, yüklenmedi.");
                return;
            }
            if (IsLoading)
            {
                Debug.Log($"[GameSceneRouter] '{pendingScene}' zaten yükleniyor, {chapter} isteği yok sayıldı.");
                return;
            }

            string scene = FirstSceneOf(chapter);
            GameSession.StartChapter(chapter);
            Debug.Log($"[GameSceneRouter] {chapter} başlıyor: '{scene}'.");
            Load(scene);
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
            return Load(scene);
        }

        /// <summary>
        /// LoadChapter'ın açacağı ilk sahnenin adı (menü build listesini bununla kontrol eder). Sel → FloodSchool (okul çıkışı;
        /// yoksa FloodEvening), Deprem → EarthquakeHome; bunlar build listesinde yoksa eski akış: Bedroom, o da yoksa doğrudan bölüm sahnesi.
        /// </summary>
        public static string FirstSceneOf(ChapterId chapter)
        {
            // Sel: önce okul çıkışı + köprü (FloodSchool), oradan akşam mahalle seline (FloodEvening, LoadRoom) geçilir.
            if (chapter == ChapterId.Flood && Application.CanStreamedLevelBeLoaded(FloodSchoolScene)) return FloodSchoolScene;
            if (chapter == ChapterId.Flood && Application.CanStreamedLevelBeLoaded(FloodScene)) return FloodScene;
            if (chapter == ChapterId.Earthquake && Application.CanStreamedLevelBeLoaded(EarthquakeHomeScene)) return EarthquakeHomeScene;
            if (Application.CanStreamedLevelBeLoaded(BedroomScene)) return BedroomScene;
            return chapter == ChapterId.Flood ? LegacyFloodScene : SceneNameOf(chapter);
        }

        /// <summary>Eski API: sıra artık GameFlow'da (menüde hangi afet seçildiyse ona göre). GameFlow.NextChapter'a yönlenir.</summary>
        [Obsolete("GameFlow.NextChapter kullanın.")]
        public static ChapterId? NextChapterAfter(ChapterId finished) => GameFlow.NextChapter(finished);

        /// <summary>Eski API: bölümü tamamlanmış sayıp GameFlow.ContinueAfter'a yönlenir (sıradaki afet ya da oyun sonu).</summary>
        [Obsolete("GameFlow.CompleteChapter + GameFlow.ContinueAfter kullanın.")]
        public static bool ContinueToNextChapter(ChapterId finished)
        {
            GameFlow.CompleteChapter(finished);
            GameFlow.ContinueAfter(finished);
            return true;
        }

        /// <summary>Sonuç ekranındaki "Tekrar Dene": şu anki bölümü baştan açar (GameFlow durumu değişmez; Sel tekrarı depremi başlatmaz).</summary>
        public static void RestartCurrentChapter()
        {
            // Sel: okul çıkışı (FloodSchool) başarısızlıkla bitmez; akşam selinde (FloodEvening) süre dolunca tekrar
            // okula dönülmez, akşam sel sahnesi baştan açılır (başlık kartıyla).
            if (GameSession.CurrentChapter == ChapterId.Flood && SceneManager.GetActiveScene().name == FloodScene &&
                Application.CanStreamedLevelBeLoaded(FloodScene))
            {
                if (IsLoading) return;
                GameSession.StartChapter(ChapterId.Flood);
                Debug.Log($"[GameSceneRouter] Sel tekrar: '{FloodScene}' baştan.");
                Load(FloodScene);
                return;
            }
            if (GameSession.CurrentChapter.HasValue)
                LoadChapter(GameSession.CurrentChapter.Value);
            else
                Load(SceneManager.GetActiveScene().name);
        }

        /// <summary>Sonuç ekranındaki "Menüye Dön": oyun (GameFlow) ve bölüm durumu sıfırlanır; menüden seçilen afet yeni oyun başlatır.</summary>
        public static void ReturnToMenu()
        {
            if (IsLoading)
            {
                Debug.Log($"[GameSceneRouter] '{pendingScene}' zaten yükleniyor, menü isteği yok sayıldı.");
                return;
            }
            GameFlow.ResetRun();
            GameSession.Clear();
            Load(MainMenuScene);
        }

        /// <summary>İki afet bitti: oyun sonu ekranı. Sahne yoksa menüye döner.</summary>
        public static void LoadGameEnd()
        {
            if (Application.CanStreamedLevelBeLoaded(GameEndScene))
            {
                Debug.Log($"[GameSceneRouter] İki afet de bitti → '{GameEndScene}'.");
                GameSession.Clear();
                Load(GameEndScene);
                return;
            }
            Debug.LogWarning($"[GameSceneRouter] '{GameEndScene}' sahnesi yok → menü.");
            ReturnToMenu();
        }

        /// <summary>
        /// Bölüm içinde bir odadan / parçadan diğerine geçer (yatak odası → banyo → ...). Bölüm bilgisi (GameSession) korunur.
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
            Load(sceneName);
        }

        /// <summary>Bütün sahne yüklemelerinin tek noktası (çift yükleme kilidi + geçiş temizliği).</summary>
        private static bool Load(string sceneName)
        {
            if (IsLoading)
            {
                Debug.Log($"[GameSceneRouter] '{pendingScene}' zaten yükleniyor, '{sceneName}' isteği yok sayıldı.");
                return false;
            }
            loadPending = true;
            loadRequestedAt = Time.realtimeSinceStartup;
            pendingScene = sceneName;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Time.timeScale = 1f;
            try { SceneChanging?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            SceneManager.LoadScene(sceneName);
            return true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            loadPending = false;
            pendingScene = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loadPending = false;
            pendingScene = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // SceneChanging burada silinmez: aboneler (ör. FloodEveningHub) kendi SubsystemRegistration'larında -=/+= ile bağlanır.
        }
    }
}
