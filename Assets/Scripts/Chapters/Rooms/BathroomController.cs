using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.Rooms
{
    /// <summary>
    /// Banyo (OYUN_AKISI §5.1, vuruş 7–10), Bathroom sahnesinin Managers objesinde:
    /// siyah ekran → çocuk kapının önünde (doorSpawn) → açılış → serbest dolaşma →
    /// lavabo "[E] Yüzünü yıka" (tek sefer, ROOM_WASH_F / ROOM_WASH_E) → mutfak kapısı "[E] Mutfağa git" açılır.
    /// Mutfak kapısı: kitchenScene boşsa bölüm sahnesi (FloodChapter / EarthquakeChapter) açılır ve mutfaktan (F03/E03)
    /// devam eder (GameSceneRouter.ContinueChapterAfterRooms); o da Build Settings'te yoksa "Mutfak yakında" gösterilir.
    /// </summary>
    public class BathroomController : RoomControllerBase
    {
        private const string WashId = "ROOM_WASH"; // + _F / _E

        [Header("Banyo")]
        [Tooltip("Banyoya girince çocuğun belireceği nokta (kapının önü)")]
        [SerializeField] private Transform doorSpawn;
        [Tooltip("Girince bakacağı yön (ekran yönü; (0.7,0.7) = kuzeydoğu)")]
        [SerializeField] private Vector2 spawnFacing = new Vector2(0.7071f, 0.7071f);
        [SerializeField] private Interactable sink;
        [Tooltip("Lavabo kullanılınca açılır")]
        [SerializeField] private Interactable kitchenDoor;

        [Header("Geçişler")]
        [Tooltip("Boş bırak: kapı bölüm sahnesini açar (mutfaktan devam). Ayrı bir mutfak sahnesi olursa adını yaz (Build Settings'e de ekle).")]
        [SerializeField] private string kitchenScene = "";
        [SerializeField] private float fadeInSeconds = 0.35f;
        [SerializeField] private float exitFadeSeconds = 0.35f;
        [Tooltip("P1: su efekti süresi (şimdilik sadece bekleme)")]
        [SerializeField] private float washSeconds = 1f;
        [Tooltip("\"Mutfak yakında\" ipucunun ekranda kalma süresi")]
        [SerializeField] private float comingSoonSeconds = 1.5f;

        protected override string LogTag => "Bathroom";

        protected override void Awake()
        {
            base.Awake();

            SetupPoint(sink, "Yüzünü yıka", WashId, true, true, before: Wash, after: OpenKitchenDoor, objectiveKey: "Sink");
            SetupPoint(kitchenDoor, "Mutfağa git", "", false, false, before: GoToKitchen, objectiveKey: "KitchenDoor");
            if (kitchenDoor != null) kitchenDoor.SetAvailable(false);
        }

        protected override IEnumerator RunRoom()
        {
            Log($"siyah ekran ({RoomChapter.Current})");
            SetBlack();
            SetFreeRoam(false);
            PlacePlayer(doorSpawn, spawnFacing);
            yield return FadeIn(fadeInSeconds);

            Log("serbest dolaşma");
            SetFreeRoam(true);

            if (AutoTour)
            {
                yield return AutoUse(sink);
                yield return AutoUse(kitchenDoor);
                Log("otomatik tur bitti");
            }
        }

        private IEnumerator Wash()
        {
            // TODO(Mahmut, P1): 1 sn su efekti (flaş yok) + su sesi
            Log("yüz yıkanıyor");
            if (!AutoAdvance && washSeconds > 0f) yield return new WaitForSeconds(washSeconds);
        }

        private IEnumerator OpenKitchenDoor()
        {
            if (kitchenDoor != null) kitchenDoor.SetAvailable(true);
            Log("mutfak kapısı açıldı");
            yield break;
        }

        private IEnumerator GoToKitchen()
        {
            if (!string.IsNullOrEmpty(kitchenScene) && Application.CanStreamedLevelBeLoaded(kitchenScene))
            {
                yield return ExitTo(kitchenScene, exitFadeSeconds);
                yield break;
            }

            // Ayrı mutfak sahnesi yok: bölüm sahnesi (FloodChapter / EarthquakeChapter) mutfaktan (F03/E03) devam eder.
            string chapterScene = GameSceneRouter.ChapterSceneAfterRooms();
            if (chapterScene != null)
            {
                Log($"kapı → {chapterScene} (mutfaktan devam)");
                SetFreeRoam(false);
                yield return Objectives.ObjectiveTracker.Settle(0.45f, 1.5f); // "Mutfağa git" tiki kararmadan görünsün
                yield return FadeOut(exitFadeSeconds);
                if (GameSceneRouter.ContinueChapterAfterRooms()) yield break;
                yield return FadeIn(exitFadeSeconds); // olmadı: odada kal
                Interactor.InteractionEnabled = true; // yürüme, Use bitince RoomInteractor'da geri açılır
            }

            // Bölüm sahnesi de Build Settings'te yok: sadece "Mutfak yakında".
            Debug.LogWarning($"[Bathroom] {RoomChapter.Current} bölüm sahnesi Build Settings'te yok, \"Mutfak yakında\" gösterildi.", this);
            ShowHint("!", "Mutfak yakında");
            if (!AutoAdvance) yield return new WaitForSeconds(comingSoonSeconds);
            HideHint();
        }
    }
}
