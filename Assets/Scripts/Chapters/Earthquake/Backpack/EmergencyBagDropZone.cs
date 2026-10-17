using UnityEngine;
using UnityEngine.EventSystems;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Açık çantanın bırakma alanı (çanta gövdesinin Image'ı). Sürüklenen eşya üstüne gelince çanta vurgulanır,
    /// bırakılınca EmergencyBagMinigameController.DropOnBag eşyayı doğrular ve bir yuvaya yerleştirir.
    /// Yuvalar bu objenin çocuklarıdır; yuvaya bırakılan eşyanın Drop olayı hiyerarşide buraya çıkar.
    /// </summary>
    public class EmergencyBagDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private EmergencyBagMinigameController owner;

        public bool PointerInside { get; private set; }

        public void Bind(EmergencyBagMinigameController minigame) => owner = minigame;

        public void OnDrop(PointerEventData eventData)
        {
            if (owner != null) owner.DropOnBag(eventData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerInside = true;
            if (owner != null) owner.OnBagHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerInside = false;
            if (owner != null) owner.OnBagHover(false);
        }

        private void OnDisable() => PointerInside = false;
    }
}
