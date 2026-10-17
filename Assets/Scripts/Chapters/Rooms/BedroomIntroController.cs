using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.Rooms
{
    /// <summary>
    /// Yatak odası açılışı (OYUN_AKISI §5.1, vuruş 1–7), Bedroom sahnesinin Managers objesinde:
    /// siyah ekran → alarm çalar → 1,5 sn açılış → saat 07:00 kırmızı yanıp söner, çocuk yatakta uyur →
    /// "[E] Kalk" (her yerden) → kısa kararma → yatak boşalır, çocuk yatağın yanında güneye bakar → iç ses balonu →
    /// serbest dolaşma. Alarm, oyuncu saatte kapatana kadar çalar.
    /// Noktalar: saat "Alarmı kapat" (tek sefer), pencere "Dışarı bak", masa "Çantaya bak",
    /// şemsiye "Şemsiyeyi al" (sadece Sel, tek sefer), kapı "Banyoya git" (→ Bathroom sahnesi).
    /// Etiket ve diyalog ID'leri koddan kurulur (Interactable'lardaki Inspector değerlerinin yerine geçer);
    /// replikler Assets/Data/Story/rooms.json'dadır.
    /// Yatak: bedRenderer'ın sprite'ı bedSleeping ↔ bedEmpty değişir; ayrıca (veya bunun yerine)
    /// sleepingKid objesi (yatakta uyuyan çocuk katmanı) uyanınca gizlenir. İkisi de isteğe bağlıdır.
    /// </summary>
    public class BedroomIntroController : RoomControllerBase
    {
        private const string WakeId = "ROOM_WAKE";       // + _F / _E
        private const string ClockId = "ROOM_CLOCK";
        private const string WindowId = "ROOM_WINDOW";   // + _F / _E
        private const string DeskId = "ROOM_DESK";
        private const string UmbrellaId = "ROOM_UMBRELLA";

        [Header("Yatak ve uyanış")]
        [Tooltip("Yatağın SpriteRenderer'ı (uyuyan çocuklu ↔ boş sprite değişir)")]
        [SerializeField] private SpriteRenderer bedRenderer;
        [SerializeField] private Sprite bedSleeping;
        [SerializeField] private Sprite bedEmpty;
        [Tooltip("İsteğe bağlı: yatakta uyuyan çocuk ayrı bir obje ise (uyanınca gizlenir)")]
        [SerializeField] private GameObject sleepingKid;
        [Tooltip("Kalkınca çocuğun belireceği nokta (yatağın yanı)")]
        [SerializeField] private Transform wakeSpawn;
        [Tooltip("Kalkınca bakacağı yön (ekran yönü; (0,-1) = güney, kameraya doğru)")]
        [SerializeField] private Vector2 wakeFacing = new Vector2(0f, -1f);
        [SerializeField] private AlarmClock clock;
        [Tooltip("İsteğe bağlı: yataktaki Interactable. Serbest dolaşmada kullanılmaz (kapatılır).")]
        [SerializeField] private Interactable bedPoint;

        [Header("Etkileşim noktaları")]
        [SerializeField] private Interactable clockPoint;
        [SerializeField] private Interactable windowPoint;
        [SerializeField] private Interactable deskPoint;
        [Tooltip("Sadece Sel'de var; Deprem'de umbrellaRoot gizlenir")]
        [SerializeField] private Interactable umbrellaPoint;
        [SerializeField] private Interactable doorPoint;
        [Tooltip("Şemsiyenin görseli + noktası (alınınca/Deprem'de gizlenir). Boşsa umbrellaPoint'in objesi kullanılır.")]
        [SerializeField] private GameObject umbrellaRoot;

        [Header("Geçişler")]
        [SerializeField] private string bathroomScene = GameSceneRouter.BathroomScene;
        [SerializeField] private float fadeInSeconds = 1.5f;
        [SerializeField] private float wakeFadeSeconds = 0.25f;
        [SerializeField] private float exitFadeSeconds = 0.35f;

        protected override string LogTag => "Bedroom";

        protected override void Awake()
        {
            base.Awake();

            if (bedPoint != null) bedPoint.SetAvailable(false);
            SetupPoint(clockPoint, "Alarmı kapat", ClockId, false, true, before: StopAlarm);
            SetupPoint(windowPoint, "Dışarı bak", WindowId, true, false);
            SetupPoint(deskPoint, "Çantaya bak", DeskId, false, false);
            SetupPoint(umbrellaPoint, "Şemsiyeyi al", UmbrellaId, false, true, before: TakeUmbrella);
            SetupPoint(doorPoint, "Banyoya git", "", false, false, before: GoToBathroom);

            // Şemsiye sadece Sel'de; bu oynanışta zaten alındıysa da görünmez.
            bool umbrellaHere = !RoomChapter.IsEarthquake && !GameSession.UmbrellaTaken;
            SetUmbrellaVisible(umbrellaHere);
            if (umbrellaPoint != null) umbrellaPoint.SetAvailable(umbrellaHere);
        }

        protected override IEnumerator RunRoom()
        {
            // 1. Siyah ekran, çocuk yatakta uyuyor, oyuncu görünmez
            Log($"siyah ekran ({RoomChapter.Current})");
            SetBlack();
            SetFreeRoam(false);
            SetPlayerVisible(false);
            SetBedSleeping(true);

            // 2. Alarm çalar, oda açılır
            if (clock != null) clock.StartAlarm();
            Log("alarm çalıyor, açılış");
            yield return FadeIn(fadeInSeconds);

            // 3. [E] Kalk (her yerden)
            Log("[E] Kalk bekleniyor");
            yield return WaitForInteract("Kalk");

            // 4. Kısa kararma: yatak boşalır, çocuk yatağın yanında belirir
            Log("kalkış");
            yield return FadeOut(wakeFadeSeconds);
            SetBedSleeping(false);
            PlacePlayer(wakeSpawn, wakeFacing);
            yield return FadeIn(wakeFadeSeconds);

            // 5. İç ses
            string wakeLine = RoomChapter.WithSuffix(WakeId);
            Log($"iç ses {wakeLine}");
            yield return Say(wakeLine);

            // 6. Serbest dolaşma (alarm hâlâ çalıyor)
            Log("serbest dolaşma");
            SetFreeRoam(true);

            if (AutoTour) yield return Tour();
        }

        // Otomatik test: bütün noktaları sırayla kullanır, en son kapıdan banyoya geçer.
        private IEnumerator Tour()
        {
            yield return AutoUse(clockPoint);
            yield return AutoUse(windowPoint);
            yield return AutoUse(deskPoint);
            if (umbrellaPoint != null && umbrellaPoint.Available) yield return AutoUse(umbrellaPoint);
            Log($"otomatik tur bitti (alarm çalıyor mu: {(clock != null && clock.IsRinging)}, şemsiye: {GameSession.UmbrellaTaken})");
            yield return AutoUse(doorPoint);
        }

        private IEnumerator StopAlarm()
        {
            if (clock != null) clock.StopAlarm();
            Log("alarm kapatıldı");
            yield break;
        }

        private IEnumerator TakeUmbrella()
        {
            SetUmbrellaVisible(false);
            GameSession.MarkUmbrellaTaken();
            Log("şemsiye alındı");
            yield break;
        }

        private IEnumerator GoToBathroom() => ExitTo(bathroomScene, exitFadeSeconds);

        private void SetUmbrellaVisible(bool visible)
        {
            if (umbrellaRoot != null) umbrellaRoot.SetActive(visible);
            else if (umbrellaPoint != null) umbrellaPoint.gameObject.SetActive(visible);
        }

        private void SetBedSleeping(bool sleeping)
        {
            if (bedRenderer != null)
            {
                var sprite = sleeping ? bedSleeping : bedEmpty;
                if (sprite != null) bedRenderer.sprite = sprite;
            }
            if (sleepingKid != null) sleepingKid.SetActive(sleeping);
        }
    }
}
