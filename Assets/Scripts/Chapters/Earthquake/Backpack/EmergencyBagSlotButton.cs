using UnityEngine;
using UnityEngine.EventSystems;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Çantadaki bir yuvaya tıklama (dolu yuvayı seçer → "Çıkar" düğmesi). Drop olayını işlemez;
    /// yuvaya bırakılan eşya hiyerarşide üstteki EmergencyBagDropZone'a gider.
    /// </summary>
    public class EmergencyBagSlotButton : MonoBehaviour, IPointerClickHandler
    {
        private EmergencyBagMinigameController owner;

        /// <summary>Ana yuva indeksi ya da EmergencyBagInventory.PocketIndex.</summary>
        public int SlotIndex { get; private set; }

        public void Bind(EmergencyBagMinigameController minigame, int slotIndex)
        {
            owner = minigame;
            SlotIndex = slotIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging) return;
            if (owner != null) owner.OnSlotClicked(SlotIndex);
        }
    }
}
