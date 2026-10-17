using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Pixelify metninin kelime aralığı (SEL3-30): Pixelify'da boşluk ~2 px; canvas 1x ölçekteyken (480x270) harfler
    /// yapışır ("Güvenligüzergâhı"). Bu bileşen yalnız 1x'te her boşluğun yanına görünmez bir nokta ekleyip aralığı açar,
    /// 2x ve üstünde metni olduğu gibi bırakır. Metni Text.text yerine Value (ya da Text.SetPixelText) ile ver.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class FePixelLabel : MonoBehaviour
    {
        private const string WideSpace = " <color=#00000000>.</color>";

        private Text text;
        private FePixelCanvasScaler scaler;
        private string raw = "";
        private int appliedScale = -1;

        public string Value
        {
            get => raw;
            set
            {
                raw = value ?? "";
                Refresh(true);
            }
        }

        private void Awake() => Resolve();

        private void LateUpdate() => Refresh(false);

        private void Resolve()
        {
            if (text == null) text = GetComponent<Text>();
            if (scaler == null) scaler = GetComponentInParent<FePixelCanvasScaler>(true);
        }

        private void Refresh(bool force)
        {
            Resolve();
            int scale = scaler != null ? scaler.Scale : 2;
            if (!force && scale == appliedScale) return;
            appliedScale = scale;
            text.supportRichText = true;
            text.text = scale <= 1 ? raw.Replace(" ", WideSpace) : raw;
        }
    }

    public static class FePixelLabelExtensions
    {
        /// <summary>Metni ölçeğe uygun kelime aralığıyla verir (FePixelLabel yoksa ekler).</summary>
        public static void SetPixelText(this Text t, string value)
        {
            var label = t.GetComponent<FePixelLabel>();
            if (label == null) label = t.gameObject.AddComponent<FePixelLabel>();
            label.Value = value;
        }
    }
}
