using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Deprem arayüzünün fareyle tıklanan düğmesi (uGUI Button + üzerine gelme bilgisi).
    /// Tıklama EventSystem + InputSystemUIInputModule üzerinden gelir; klavye kısayollarını View'lar GameInput ile okur.
    /// Görsel durumları (normal / üzerinde / seçili) sahibi View çizer, bu sınıf yalnızca olayları bildirir.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class EqButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action Clicked;
        public event Action<bool> HoverChanged;

        public bool Hovered { get; private set; }
        public bool Interactable { get; set; } = true;

        private Button button;

        private void Awake() => Init();

        public void Init()
        {
            if (button != null) return;
            button = GetComponent<Button>();
            if (button == null) button = gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.targetGraphic = GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                if (Interactable) Clicked?.Invoke();
            });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered = true;
            HoverChanged?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hovered = false;
            HoverChanged?.Invoke(false);
        }

        private void OnDisable()
        {
            if (!Hovered) return;
            Hovered = false;
            HoverChanged?.Invoke(false);
        }
    }
}
