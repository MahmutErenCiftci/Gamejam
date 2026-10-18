using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AfetUyanisi.Menu
{
    /// <summary>
    /// Ayarlar ekranındaki satır/düğme/sekme için fare olaylarını MenuSettingsScreen'e iletir (sol tuş).
    /// Girdi okumaz; EventSystem (InputSystemUIInputModule) olaylarını aktarır.
    /// </summary>
    public class SettingsPointer : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Action<PointerEventData> Entered, Pressed, Dragged, Released, Clicked;

        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke(e);

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Pressed?.Invoke(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Dragged?.Invoke(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Released?.Invoke(e);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke(e);
        }
    }
}
