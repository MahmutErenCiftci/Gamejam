using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// Mevcut Button'un public onClick'i + fareyle üstüne gelme / klavyeyle seçilme olayları; girdi okumaz.
    /// Adında "back"/"geri" geçen butonlar geri sesi, diğerleri tık sesi verir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class AudioUiSoundHooks : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
    {
        private Button button;
        private bool isBack;
        private bool pointerInside;

        private void Awake()
        {
            button = GetComponent<Button>();
            string n = gameObject.name.ToLowerInvariant();
            isBack = n.Contains("back") || n.Contains("geri");
        }

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(Clicked);
        }

        private void OnDisable() { if (button != null) button.onClick.RemoveListener(Clicked); }

        private void Clicked() => GameAudio.PlaySfx(isBack ? "SFX_UI_Back" : "SFX_UI_Click", 0.5f);

        public void OnPointerEnter(PointerEventData data) { pointerInside = true; Hover(); }
        public void OnPointerExit(PointerEventData data) => pointerInside = false;
        // Fareyle tıklamak da seçer; o durumda ikinci hover sesi çalma.
        public void OnSelect(BaseEventData data) { if (!pointerInside) Hover(); }

        private void Hover()
        {
            if (button != null && button.IsInteractable()) GameAudio.PlaySfx("SFX_UI_Hover", 0.35f);
        }
    }
}
