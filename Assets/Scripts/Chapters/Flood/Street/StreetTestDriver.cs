using System.Collections;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// SADECE test sahnesi (Assets/Scenes/Test/Test_Mahmut_Rain.unity) için: yağmur, otobüs ve su birikintisi
    /// sistemlerini klavyeden dener; bölüm sahnelerinde KULLANILMAZ.
    /// Tuşlar: 1 hafif yağmur (0.35) · 2 sağanak (5 sn) · 3 yağmuru durdur · 4 otobüs gelsin · 5 otobüs gitsin.
    /// Otobüs kapısı açıkken BoardPoint'te E → Board → Depart. Birikinti tetiklenince uyarı ID'si Console'a ve
    /// ipucuna yazılır. Kamera isteğe bağlı olarak çocuğu izler (hareketli kamerada yağmur testi için).
    /// </summary>
    public class StreetTestDriver : MonoBehaviour
    {
        [SerializeField] private RainController rain;
        [SerializeField] private BusController bus;
        [SerializeField] private IsoPlayerController player;
        [Tooltip("İsteğe bağlı \"[E] ...\" ipucu (TempInteractionHint)")]
        [SerializeField] private InteractionHintBase hint;

        [Header("Kamera")]
        [SerializeField] private Camera followCamera;
        [SerializeField] private bool followPlayer = true;
        [Tooltip("Kameranın çocuğa göre kayması (dünya birimi)")]
        [SerializeField] private Vector2 followOffset = new Vector2(0f, 0.6f);
        [Tooltip("Takip yumuşaklığı (büyük = daha sıkı)")]
        [SerializeField] private float followSharpness = 6f;

        private Coroutine rainRoutine;
        private Coroutine busRoutine;
        private bool boarding;

        /// <summary>Test komutları için: kaç kez birikinti/rögar tetiklendi.</summary>
        public int HazardTriggers { get; private set; }

        /// <summary>Son tetiklenen uyarı ID'si.</summary>
        public string LastWarningId { get; private set; } = "";

        /// <summary>Son sağanak geçişinin süresi (sn; -1 = henüz yok).</summary>
        public float LastDownpourSeconds { get; private set; } = -1f;

        /// <summary>Son otobüs adımı (test kayıtları için).</summary>
        public string BusState { get; private set; } = "bekliyor";

        private void OnEnable()
        {
            PuddleHazard.Triggered += OnHazardTriggered;
        }

        private void OnDisable()
        {
            PuddleHazard.Triggered -= OnHazardTriggered;
        }

        private void Start()
        {
            if (followCamera == null) followCamera = Camera.main;
            Debug.Log("[StreetTest] 1 hafif yağmur · 2 sağanak · 3 durdur · 4 otobüs gelsin · 5 otobüs gitsin · E otobüse bin");
        }

        private void Update()
        {
            switch (GameInput.ChoiceKeyPressed)
            {
                case 1: LightRain(); break;
                case 2: Downpour(); break;
                case 3: StopRain(); break;
                case 4: BusArrive(); break;
                case 5: BusDepart(); break;
            }
        }

        private void LateUpdate()
        {
            if (!followPlayer || followCamera == null || player == null || !player.isActiveAndEnabled) return;
            Vector3 cam = followCamera.transform.position;
            Vector2 target = (Vector2)player.transform.position + followOffset;
            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            Vector2 p = Vector2.Lerp(cam, target, k);
            followCamera.transform.position = new Vector3(p.x, p.y, cam.z);
        }

        public void LightRain()
        {
            if (rain == null) return;
            rain.SetIntensity(0.35f, 1.5f);
            Debug.Log("[StreetTest] hafif yağmur → 0.35 (1.5 sn)");
        }

        public void Downpour()
        {
            if (rain == null) return;
            if (rainRoutine != null) StopCoroutine(rainRoutine);
            rainRoutine = StartCoroutine(DownpourRoutine());
        }

        public void StopRain()
        {
            if (rain == null) return;
            if (rainRoutine != null) StopCoroutine(rainRoutine);
            rain.StopRain();
            Debug.Log("[StreetTest] yağmur durdu");
        }

        public void BusArrive()
        {
            if (bus == null || busRoutine != null) return;
            busRoutine = StartCoroutine(ArriveRoutine());
        }

        public void BusDepart()
        {
            if (bus == null || busRoutine != null) return;
            busRoutine = StartCoroutine(DepartRoutine(false));
        }

        private IEnumerator DownpourRoutine()
        {
            float t0 = Time.time;
            Debug.Log($"[StreetTest] sağanak başlıyor (yoğunluk {rain.Intensity:0.00})");
            yield return rain.StartDownpour(5f);
            LastDownpourSeconds = Time.time - t0;
            Debug.Log($"[StreetTest] sağanak tamam: {LastDownpourSeconds:0.00} sn, yoğunluk {rain.Intensity:0.00}");
            rainRoutine = null;
        }

        private IEnumerator ArriveRoutine()
        {
            BusState = "geliyor";
            yield return bus.Arrive();
            var point = bus.BoardPoint;
            BusState = "durakta";
            Debug.Log($"[StreetTest] otobüs durakta: DoorsOpen={bus.DoorsOpen} BoardPoint.Available={(point != null && point.Available)}");
            busRoutine = null;

            // Gerçek akıştaki gibi: çocuk kapıya gelip E'ye basana kadar bekle, sonra bin ve otobüs gitsin.
            if (point == null || player == null) yield break;
            yield return point.WaitForUse(player, hint, "Otobüse bin");
            if (!bus.DoorsOpen || busRoutine != null) yield break;
            busRoutine = StartCoroutine(DepartRoutine(true));
        }

        private IEnumerator DepartRoutine(bool board)
        {
            if (board && player != null)
            {
                boarding = true;
                BusState = "biniyor";
                Debug.Log("[StreetTest] otobüse biniliyor");
                yield return bus.Board(player);
                boarding = false;
            }
            BusState = "kalkıyor";
            yield return bus.Depart();
            BusState = "gitti";
            Debug.Log("[StreetTest] otobüs gitti");
            busRoutine = null;
        }

        private void OnHazardTriggered(PuddleHazard hazard)
        {
            HazardTriggers++;
            LastWarningId = hazard.WarningId;
            Debug.Log($"[StreetTest] Triggered: {hazard.name} → {hazard.WarningId} (toplam {HazardTriggers})");
            if (hint != null && !boarding) StartCoroutine(FlashHint(hazard.WarningId));
        }

        private IEnumerator FlashHint(string warningId)
        {
            hint.Show("!", warningId);
            yield return new WaitForSeconds(1.2f);
            hint.Hide();
        }
    }
}
