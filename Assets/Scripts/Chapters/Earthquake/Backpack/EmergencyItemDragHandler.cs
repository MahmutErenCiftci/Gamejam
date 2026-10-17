using UnityEngine;
using UnityEngine.EventSystems;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Masadaki bir eşya kartı: fareyle üzerine gelme (vurgu + hafif büyüme), basma, sürükleme.
    /// Olayları EventSystem (InputSystemUIInputModule) verir; bütün davranışı sahibi EmergencyBagMinigameController uygular
    /// (sürükleme hayaleti, çantaya bırakma, geri dönüş). Kartın kendi Image'ı raycast hedefidir.
    /// </summary>
    public class EmergencyItemDragHandler : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private EmergencyBagMinigameController owner;

        public EmergencyItemData Item { get; private set; }
        public int Index { get; private set; }
        public bool Hovered { get; private set; }
        public bool Dragging { get; private set; }

        public void Bind(EmergencyBagMinigameController minigame, EmergencyItemData item, int index)
        {
            owner = minigame;
            Item = item;
            Index = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered = true;
            if (owner != null) owner.OnCardHover(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hovered = false;
            if (owner != null) owner.OnCardHover(this, false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (owner != null) owner.OnCardPress(this, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (owner != null && !Dragging) owner.OnCardPress(this, false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || owner == null) return;
            Dragging = owner.BeginCardDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Dragging && owner != null) owner.DragCard(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Dragging) return;
            Dragging = false;
            if (owner != null) owner.EndCardDrag(this, eventData);
        }

        /// <summary>Mini oyun kesilince (Esc / sayaç) yarım kalan sürüklemeyi unutturur.</summary>
        public void CancelDrag()
        {
            Dragging = false;
            Hovered = false;
        }

        private void OnDisable() => CancelDrag();
    }
}
