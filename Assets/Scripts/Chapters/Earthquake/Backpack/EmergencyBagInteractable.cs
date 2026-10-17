using System;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Yatak odasındaki afet çantası (dünyadaki obje). Aynı objedeki Interactable'ı EarthquakeModeController kaydeder:
    ///  • Hazırlıkta (sayaç akarken) E → afet çantası mini oyunu (EmergencyBagMinigameController); sayaç DURMAZ.
    ///  • Hazırlandıktan sonra çanta kapalı/dolu görünür, üstünde küçük yeşil tik süzülür.
    ///  • Sarsıntıdan SONRA E → hazırsa çanta alınır (bagTaken), hazır değilse "çanta boş" dersi.
    /// Sarsıntı sırasında çanta kullanılamaz (öncelik Çök–Kapan–Tutun). Tek çanta hikâyesi için eski deprem çantası
    /// objeleri (replacedBags, ör. salondaki "QuakeBag") deprem modunda gizlenir.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class EmergencyBagInteractable : MonoBehaviour
    {
        [SerializeField] private EmergencyBagMinigameController minigame;

        [Header("Görünüm")]
        [Tooltip("Çantanın SpriteRenderer'ı (boşsa bu objede)")]
        [SerializeField] private SpriteRenderer bagRenderer;
        [Tooltip("Hazırlanmamış (açık, boş) çanta")]
        [SerializeField] private Sprite unpreparedSprite;
        [Tooltip("Hazırlanmış (kapalı, dolu) çanta")]
        [SerializeField] private Sprite preparedSprite;
        [Tooltip("Yaklaşınca açılan piksel kontur (Interactable.highlight bu objeyi açıp kapatır)")]
        [SerializeField] private SpriteRenderer outlineRenderer;
        [SerializeField] private Sprite unpreparedOutline;
        [SerializeField] private Sprite preparedOutline;
        [Tooltip("Hazırlanınca çantanın üstünde süzülen yeşil tik")]
        [SerializeField] private Transform readyMarker;
        [SerializeField] private float markerBob = 0.03f;
        [Tooltip("Deprem modunda gizlenecek eski çanta objeleri (tek çanta hikâyesi)")]
        [SerializeField] private GameObject[] replacedBags = Array.Empty<GameObject>();

        [Header("Etiketler")]
        [SerializeField] private string prepLabel = "Afet çantasını hazırla";
        [SerializeField] private string readyLabel = "Afet çantası (hazır)";
        [SerializeField] private string takeLabel = "Afet çantanı al";
        [SerializeField] private string emptyLabel = "Afet çantasına bak";

        [Header("Kart metinleri")]
        [SerializeField] private string title = "Afet Çantası";
        [SerializeField, TextArea] private string readyText =
            "Çantan hazır ve kapalı. Sarsıntı sırasında çantayla uğraşma; önce Çök–Kapan–Tutun. Sarsıntı bitince al.";
        [SerializeField, TextArea] private string emptyAfterText =
            "Çantan boş! Su, yiyecek, ilk yardım seti ve el feneri depremden ÖNCE hazırlanmalıydı.";
        [SerializeField, TextArea] private string reminderPrepared = "Çıkmadan önce odandaki afet çantanı al.";
        [SerializeField, TextArea] private string reminderUnprepared =
            "Afet çantanı hazırlamamıştın. Bir dahaki sefere sarsıntıdan ÖNCE hazırla ve kolay ulaşılan bir yerde tut.";
        [SerializeField, TextArea] private string takenToast = "Afet çantanı aldın: su, yiyecek, ilk yardım seti, el feneri ve düdük yanında.";

        private Interactable point;
        private Vector3 markerBase;
        private bool markerBaseSet;

        public Interactable Point
        {
            get
            {
                if (point == null) point = GetComponent<Interactable>();
                return point;
            }
        }

        public EmergencyBagMinigameController Minigame => minigame;
        public bool IsPrepared { get; private set; }
        public bool IsTaken { get; private set; }
        public bool EmptyChecked { get; private set; }
        public bool MinigameOpen => minigame != null && minigame.IsOpen;
        public string Title => title;
        public string ReadyText => readyText;
        public string EmptyAfterText => emptyAfterText;
        public string TakenToast => takenToast;
        public string ExitReminder => IsPrepared ? reminderPrepared : reminderUnprepared;

        /// <summary>Hazır durumu değişince (true = hazırlandı).</summary>
        public event Action<EmergencyBagInteractable> Prepared;

        private void Awake()
        {
            if (bagRenderer == null) bagRenderer = GetComponent<SpriteRenderer>();
            if (minigame == null) minigame = FindAnyObjectByType<EmergencyBagMinigameController>(FindObjectsInactive.Include);
            foreach (var go in replacedBags)
                if (go != null && go != gameObject) go.SetActive(false);
            if (readyMarker != null)
            {
                markerBase = readyMarker.localPosition;
                markerBaseSet = true;
            }
            ApplyVisual();
        }

        private void Update()
        {
            if (readyMarker == null || !readyMarker.gameObject.activeSelf || !markerBaseSet) return;
            float y = Mathf.Round(Mathf.Sin(Time.time * 3f) * markerBob * 64f) / 64f;
            readyMarker.localPosition = markerBase + new Vector3(0f, y, 0f);
        }

        /// <summary>Evreye göre ipucu yazısı ("[E] ...").</summary>
        public void ApplyLabel(EarthquakePhase phase)
        {
            if (Point == null) return;
            string label;
            switch (phase)
            {
                case EarthquakePhase.After:
                case EarthquakePhase.Report:
                    label = IsPrepared ? takeLabel : emptyLabel;
                    break;
                default:
                    label = IsPrepared ? readyLabel : prepLabel;
                    break;
            }
            Point.Setup(label, "", false, false);
        }

        /// <summary>Mini oyunu açar (hazırlanmışsa ya da mini oyun yoksa false). onClosed(tamamlandı mı) kapanınca çağrılır.</summary>
        public bool OpenMinigame(Action<bool> onClosed)
        {
            if (IsPrepared || minigame == null) return false;
            return minigame.Open(transform, completed =>
            {
                if (completed) MarkPrepared();
                onClosed?.Invoke(completed);
            });
        }

        /// <summary>Çanta hazır: kapalı/dolu sprite + yeşil tik. (Mini oyun ya da otomatik test çağırır.)</summary>
        public void MarkPrepared()
        {
            if (IsPrepared) return;
            IsPrepared = true;
            ApplyVisual();
            Prepared?.Invoke(this);
        }

        /// <summary>Sarsıntıdan sonra hazır çanta alındı: dünyadan kalkar.</summary>
        public void MarkTaken()
        {
            IsTaken = true;
            if (bagRenderer != null) bagRenderer.enabled = false;
            if (outlineRenderer != null) outlineRenderer.enabled = false;
            if (readyMarker != null) readyMarker.gameObject.SetActive(false);
            Point.SetAvailable(false);
        }

        /// <summary>Sarsıntıdan sonra hazırlanmamış çantaya bakıldı (ders kartı).</summary>
        public void MarkEmptyChecked() => EmptyChecked = true;

        private void ApplyVisual()
        {
            var sprite = IsPrepared ? preparedSprite : unpreparedSprite;
            if (bagRenderer != null && sprite != null) bagRenderer.sprite = sprite;
            var outline = IsPrepared ? preparedOutline : unpreparedOutline;
            if (outlineRenderer != null && outline != null) outlineRenderer.sprite = outline;
            if (readyMarker != null) readyMarker.gameObject.SetActive(IsPrepared && !IsTaken);
        }
    }
}
