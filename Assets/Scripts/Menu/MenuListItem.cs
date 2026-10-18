using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Menu
{
    /// <summary>
    /// Ana menüdeki tek satırlık seçenek (Yeni Oyun / Ayarlar / Çıkış, ayar satırları).
    /// Odakta değilken sadece yazı görünür; odaktayken lacivert plaka + altın yazı + sol işaret.
    /// Klavye odağını MainMenuScreen yönetir; fare olayları buradan bildirilir.
    /// </summary>
    public class MenuListItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private Image plate;
        [SerializeField] private Image marker;
        [SerializeField] private Text label;
        [SerializeField] private Color idleColor = new Color32(0xF4, 0xEB, 0xD8, 0xD9);
        [SerializeField] private Color focusColor = new Color32(0xFF, 0xD2, 0x7E, 0xFF);

        public event Action<MenuListItem> Hovered;
        public event Action<MenuListItem> Clicked;

        public bool Focused { get; private set; }

        public string Text
        {
            get => label != null ? label.text : "";
            set { if (label != null) label.text = value; }
        }

        private void Awake() => Apply();

        public void SetFocused(bool focused)
        {
            Focused = focused;
            Apply();
        }

        private void Apply()
        {
            if (plate != null) plate.enabled = Focused;
            if (marker != null) marker.enabled = Focused;
            if (label != null) label.color = Focused ? focusColor : idleColor;
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Clicked?.Invoke(this);
        }
    }
}
