using System;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// E ile incelenen ev nesnesi (kitaplık, pencere, ayna, masa, çanta...). Aynı objedeki Interactable'ı kullanır
    /// (RoomInteractor en yakın noktayı bulur, "[E] İncele" gösterir); kart gösterimini EarthquakeModeController
    /// RoomInteractor.Register ile bağlar. Başlık/metin/tehlike sınıfı earthquake_mode.json'daki inspectables[id]'den gelir;
    /// JSON'da yoksa buradaki yedek alanlar kullanılır. Oyuncu kontrolcüsüne gömülü değildir.
    /// id boşsa obje adından türetilir: "BedroomWindow" → "bedroom_window".
    /// Tek seferlik inceleme (singleUse, varsayılan açık): BeginInspection() bir kez başarılı olur → IsInspected = true,
    /// Inspected olayı (görev ilerlemesi), Interactable kapanır (E ipucu bir daha çıkmaz). Yalnızca İNCELEME noktaları için;
    /// aynı objeyi mini oyun/mekanizma kullanıyorsa (kitaplık, ocak) onlar MarkInspected'i çağırır ve nokta açık kalır.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class InspectableObject : MonoBehaviour
    {
        [Tooltip("JSON inspectables[].id (ör. bookshelf, mirror, dining_table). Boşsa obje adından türetilir.")]
        [SerializeField] private string id = "";
        [Tooltip("İpucundaki eylem yazısı")]
        [SerializeField] private string actionLabel = "İncele";
        [Tooltip("JSON'da yoksa kullanılacak tehlike sınıfı")]
        [SerializeField] private HazardClass hazardClass = HazardClass.Info;
        [Tooltip("JSON'da yoksa kart başlığı")]
        [SerializeField] private string fallbackTitle = "";
        [Tooltip("JSON'da yoksa kart metni")]
        [SerializeField, TextArea] private string fallbackText = "";
        [Tooltip("İsteğe bağlı: incelendikten sonra açılan küçük işaret (ör. yeşil tik)")]
        [SerializeField] private GameObject inspectedMarker;
        [Tooltip("İşaretliyse bir kez incelenir: kart gösterilir, görev ilerler, E ipucu kalkar ve tekrar incelenemez")]
        [SerializeField] private bool singleUse = true;
        [Tooltip("İnceleme kartı açılırken çağrılır (ses/animasyon kancası)")]
        [SerializeField] private UnityEvent onInspected = new UnityEvent();

        private Interactable point;
        private string title;
        private string text;
        private string afterText;
        private bool dataApplied;

        /// <summary>İlk kez incelendiğinde tetiklenir.</summary>
        public event Action<InspectableObject> Inspected;

        public string Id => string.IsNullOrEmpty(id) ? DeriveId(name) : id;
        public HazardClass Kind => hazardClass;
        public bool IsHazard => hazardClass == HazardClass.Hazard;
        public bool WasInspected { get; private set; }
        /// <summary>İncelendi mi (tek seferlik incelemenin durumu; WasInspected ile aynı).</summary>
        public bool IsInspected => WasInspected;
        public bool SingleUse => singleUse;
        /// <summary>Şu an incelenebilir mi (tek seferlikse ve incelendiyse false).</summary>
        public bool CanInspect => !(singleUse && WasInspected);
        public string ActionLabel => string.IsNullOrEmpty(actionLabel) ? "İncele" : actionLabel;

        public Interactable Point
        {
            get
            {
                if (point == null) point = GetComponent<Interactable>();
                return point;
            }
        }

        public string Title => !string.IsNullOrEmpty(title) ? title : (!string.IsNullOrEmpty(fallbackTitle) ? fallbackTitle : name);
        public string Text => !string.IsNullOrEmpty(text) ? text : fallbackText;

        /// <summary>Sarsıntı sonrası metin (yoksa normal metin).</summary>
        public string AfterText => !string.IsNullOrEmpty(afterText) ? afterText : Text;

        /// <summary>Evreye göre kart metni.</summary>
        public string TextFor(EarthquakePhase phase) => phase == EarthquakePhase.After || phase == EarthquakePhase.Report ? AfterText : Text;

        private void Awake()
        {
            if (inspectedMarker != null) inspectedMarker.SetActive(false);
        }

        /// <summary>JSON verisini uygular (mod kontrolcüsü açılışta çağırır). data null ise yedek alanlar kalır.</summary>
        public void ApplyData(EqInspectableData data)
        {
            dataApplied = data != null;
            if (data == null) return;
            title = data.title;
            text = data.text;
            afterText = data.afterText;
            hazardClass = data.GetHazardClass(hazardClass);
        }

        public bool HasData => dataApplied;

        /// <summary>İncelendi olarak işaretler; ilk kezse true döner ve olaylar tetiklenir.</summary>
        public bool MarkInspected()
        {
            if (onInspected != null) onInspected.Invoke();
            if (WasInspected) return false;
            WasInspected = true;
            if (inspectedMarker != null) inspectedMarker.SetActive(true);
            Inspected?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Ortak tek seferlik inceleme girişi (EarthquakeModeController.Inspect çağırır). İncelenebilirse işaretler,
        /// tek seferlikse E noktasını kapatır ve true döner; zaten incelendiyse hiçbir şey yapmaz, false döner.
        /// </summary>
        public bool BeginInspection()
        {
            if (!CanInspect) return false;
            MarkInspected();
            if (singleUse && Point != null) Point.SetAvailable(false);
            return true;
        }

        /// <summary>"BedroomWindow" → "bedroom_window", "QuakeBag" → "quake_bag".</summary>
        public static string DeriveId(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < objectName.Length; i++)
            {
                char c = objectName[i];
                if (char.IsWhiteSpace(c) || c == '-') { sb.Append('_'); continue; }
                if (char.IsUpper(c) && i > 0 && objectName[i - 1] != '_' && !char.IsUpper(objectName[i - 1])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
