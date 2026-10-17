using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Menu
{
    // Menü butonunun piksel görünümü: seçilince ya da üzerine gelinince panel turuncu çerçeveliye döner, yazı
    // sıcak renge geçer ve solunda ok belirir. Button'ın renk geçişi yerine kullanılır (koyu panelde renk tonu
    // neredeyse fark edilmiyordu). Pasif buton soluk görünür.
    [RequireComponent(typeof(Button))]
    public class MenuButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Image background;
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite highlightSprite;
        [SerializeField] Text label;
        [SerializeField] Graphic arrow;
        [SerializeField] Color normalText = new Color(0.93f, 0.9f, 0.84f);
        [SerializeField] Color highlightText = new Color(1f, 0.82f, 0.45f);
        [SerializeField] Color disabledText = new Color(0.55f, 0.55f, 0.62f);

        Button button;
        bool selected;
        bool hovered;
        bool lastInteractable;

        void Awake()
        {
            button = GetComponent<Button>();
            lastInteractable = button.interactable;
            Apply();
        }

        void OnEnable() => Apply();

        void Update()
        {
            if (button.interactable != lastInteractable)
            {
                lastInteractable = button.interactable;
                Apply();
            }
        }

        public void OnSelect(BaseEventData eventData) { selected = true; Apply(); }
        public void OnDeselect(BaseEventData eventData) { selected = false; Apply(); }
        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; Apply(); }

        void Apply()
        {
            if (button == null) return;
            bool interactable = button.interactable;
            bool on = interactable && (selected || hovered);
            if (background != null)
            {
                background.sprite = on && highlightSprite != null ? highlightSprite : normalSprite;
                background.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }
            if (label != null) label.color = !interactable ? disabledText : on ? highlightText : normalText;
            if (arrow != null) arrow.enabled = on;
        }
    }
}
