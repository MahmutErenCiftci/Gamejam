using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Sarsıntı sonrası ortam değişimi işareti (etiket yerine; ProjectSettings'e tag eklemek gerekmez ve kapalı objelerde de çalışır).
    /// EarthquakeEventController.ApplyAftermath() sahnedeki bütün AftermathToggle'ları (kapalılar dahil) uygular:
    ///  • ShowAfterQuake: obje başta kapalıdır, sonra açılır (yerdeki kitaplar, cam kırıkları, devrik saksı).
    ///  • HideAfterQuake: obje başta açıktır, sonra kapanır (sağlam ayna, düzgün çerçeve).
    ///  • SwapSprite: SpriteRenderer'ın sprite'ı afterSprite olur (eğik çerçeve, çatlak cam).
    /// Ayrıca obje adı "Aftermath" ile başlayanlar açılır, "BeforeQuake" ile başlayanlar kapanır (bileşensiz kısa yol).
    /// </summary>
    public class AftermathToggle : MonoBehaviour
    {
        /// <summary>Değişim türü.</summary>
        public enum Mode { ShowAfterQuake, HideAfterQuake, SwapSprite }

        [SerializeField] private Mode mode = Mode.ShowAfterQuake;
        [Tooltip("SwapSprite: değişecek renderer (boşsa bu objedeki)")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("SwapSprite: sonrası sprite'ı")]
        [SerializeField] private Sprite afterSprite;
        [Tooltip("ShowAfterQuake: sahne başında objeyi kapat (işaretli önerilir)")]
        [SerializeField] private bool hideOnStart = true;

        private bool applied;

        public Mode ToggleMode => mode;
        public bool Applied => applied;

        private void Awake()
        {
            // Apply() objeyi açınca Awake ilk kez burada çalışabilir; o durumda tekrar kapatma.
            if (!applied && mode == Mode.ShowAfterQuake && hideOnStart) gameObject.SetActive(false);
        }

        /// <summary>Sonrası durumunu uygular.</summary>
        public void Apply()
        {
            applied = true;
            switch (mode)
            {
                case Mode.ShowAfterQuake:
                    gameObject.SetActive(true);
                    break;
                case Mode.HideAfterQuake:
                    gameObject.SetActive(false);
                    break;
                case Mode.SwapSprite:
                    if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null && afterSprite != null) spriteRenderer.sprite = afterSprite;
                    break;
            }
        }
    }
}
