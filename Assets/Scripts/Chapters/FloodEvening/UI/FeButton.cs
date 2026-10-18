using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>Akşam sel arayüzünün fareyle tıklanan düğmesi (uGUI Button + üzerine gelme). Klavyeyi sahibi View GameInput ile okur.</summary>
    [RequireComponent(typeof(Image))]
    public class FeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action Clicked;
        public event Action<bool> HoverChanged;

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
            button.onClick.AddListener(() => { if (Interactable) Clicked?.Invoke(); });
        }

        public void OnPointerEnter(PointerEventData eventData) => HoverChanged?.Invoke(true);
        public void OnPointerExit(PointerEventData eventData) => HoverChanged?.Invoke(false);
    }
}
