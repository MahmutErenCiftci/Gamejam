using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Fareyle BASILI TUTULAN arayüz hedefi (ör. kitaplık sabitleme noktası). Basılıyken Held true olur;
    /// fare bırakılınca ya da hedefin dışına çıkınca false. Tıklama EventSystem + InputSystemUIInputModule'den gelir.
    /// Aynı objede raycastTarget'ı açık bir Graphic (Image) olmalı.
    /// </summary>
    public class EqHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        public event Action<bool> HeldChanged;

        public bool Held { get; private set; }
        public bool Hovered { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Set(true);
        }

        public void OnPointerUp(PointerEventData eventData) => Set(false);

        public void OnPointerEnter(PointerEventData eventData) => Hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            Hovered = false;
            Set(false);
        }

        private void OnDisable()
        {
            Hovered = false;
            Set(false);
        }

        private void Set(bool held)
        {
            if (Held == held) return;
            Held = held;
            HeldChanged?.Invoke(held);
        }
    }
}
