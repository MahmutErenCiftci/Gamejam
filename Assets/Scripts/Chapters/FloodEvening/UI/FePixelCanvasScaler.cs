using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Piksel arayüz için tam sayı ölçek (SEL3-30): canvas'ı 640x360 referansına göre en büyük TAM katla büyütür
    /// (1080p = 3x, 720p = 2x, 480x270 = 1x; en az 1x). Böylece her sprite/yazı pikseli eşit kare ekran piksellerine düşer,
    /// kesirli ölçekteki bulanıklık ve bozuk piksel yazı oluşmaz. Ekran boyutu değişince kendini günceller.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class FePixelCanvasScaler : MonoBehaviour
    {
        [SerializeField] private Vector2 reference = new Vector2(640f, 360f);

        private CanvasScaler scaler;
        private int lastW = -1, lastH = -1;

        /// <summary>Şu anki tam sayı ölçek.</summary>
        public int Scale { get; private set; } = 1;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Apply();
        }

        private void OnEnable()
        {
            if (scaler != null) Apply();
        }

        private void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) Apply();
        }

        private void Apply()
        {
            lastW = Screen.width;
            lastH = Screen.height;
            Scale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(lastW / reference.x, lastH / reference.y) + 0.001f));
            scaler.scaleFactor = Scale;
        }
    }
}
