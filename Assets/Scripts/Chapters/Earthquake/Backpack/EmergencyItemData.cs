using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Afet çantası mini oyunundaki bir eşya (ya da eşya grubu: "El feneri + pil", "Powerbank + kablo"...).
    /// Required: çantaya girmeden fermuar kapanmaz. Comfort: (oyuncak ayı) yanlış sayılmaz, önceliği düşüktür; ön cep varsa oraya girer.
    /// Unnecessary: (süs eşyası, oyun konsolu) bir yuva kaplar, amber uyarı verir, "Çıkar" ile masaya döner.
    /// Varlıklar: Assets/Data/Earthquake/EmergencyBag/BAGITEM_*.asset (EmergencyBagMinigameController.items listesine sürüklenir).
    /// </summary>
    [CreateAssetMenu(fileName = "BAGITEM_", menuName = "Afet Uyanisi/Deprem/Afet Çantası Eşyası")]
    public class EmergencyItemData : ScriptableObject
    {
        public enum ItemCategory { Required, Comfort, Unnecessary }

        [Tooltip("Benzersiz kimlik (ör. water, flashlight_batteries, teddy)")]
        [SerializeField] private string id = "";
        [Tooltip("Masadaki kısa ad (en fazla ~12 harf)")]
        [SerializeField] private string displayName = "";
        [Tooltip("Geri bildirimdeki tam ad (boşsa kısa ad)")]
        [SerializeField] private string fullName = "";
        [SerializeField] private Sprite icon;
        [SerializeField] private ItemCategory category = ItemCategory.Required;
        [Tooltip("Çantaya girince gösterilen kısa bilgi (gerekli) ya da uyarı (gereksiz). Boşsa varsayılan metin.")]
        [SerializeField, TextArea] private string feedback = "";
        [Tooltip("Kapladığı yuva sayısı. Bu sürümde her eşya 1 yuva kaplar.")]
        [SerializeField, Min(1)] private int slotCost = 1;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? Id : displayName;
        public string FullName => string.IsNullOrEmpty(fullName) ? DisplayName : fullName;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public bool IsRequired => category == ItemCategory.Required;
        public string Feedback => feedback;
        public int SlotCost => Mathf.Max(1, slotCost);

        /// <summary>Kurulum betiği / testler için (Inspector alanlarının yerine geçer).</summary>
        public void Configure(string itemId, string shortName, string longName, Sprite itemIcon, ItemCategory itemCategory, string feedbackText)
        {
            id = itemId;
            displayName = shortName;
            fullName = longName;
            icon = itemIcon;
            category = itemCategory;
            feedback = feedbackText;
            slotCost = 1;
        }
    }
}
