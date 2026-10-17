using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.TempUI
{
    /// <summary>
    /// GEÇİCİ "[E] Kalk" ipucu: arayüz ekibinin gerçek prefabı gelene kadar ekranın altında küçük bir kutu.
    /// Kendi Canvas'ını kodla kurar; bölüm akışı hint bağlı değilse bunu otomatik oluşturur.
    /// </summary>
    public class TempInteractionHint : InteractionHintBase
    {
        [SerializeField] private Font font;

        private GameObject panel;
        private Text keyText;
        private Text labelText;

        private void Awake()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = new GameObject("TempHintCanvas", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = NewImage("Hint", canvas.transform, new Color(0.106f, 0.141f, 0.255f, 0.92f));
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 48f);
            rt.sizeDelta = new Vector2(420f, 76f);

            var key = NewImage("Key", panel.transform, new Color(0.969f, 0.706f, 0.357f, 1f));
            var keyRt = (RectTransform)key.transform;
            keyRt.anchorMin = keyRt.anchorMax = new Vector2(0f, 0.5f);
            keyRt.pivot = new Vector2(0f, 0.5f);
            keyRt.anchoredPosition = new Vector2(14f, 0f);
            keyRt.sizeDelta = new Vector2(56f, 56f);
            keyText = NewText("KeyText", key.transform, 36, new Color(0.106f, 0.141f, 0.255f));

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(panel.transform, false);
            var labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(84f, 0f);
            labelRt.offsetMax = new Vector2(-14f, 0f);
            labelText = NewText("LabelText", label.transform, 34, Color.white);
            labelText.alignment = TextAnchor.MiddleLeft;

            panel.SetActive(false);
        }

        public override void Show(string keyLabel, string actionLabel)
        {
            keyText.text = keyLabel;
            labelText.text = actionLabel;
            panel.SetActive(true);
        }

        public override void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private static GameObject NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text NewText(string name, Transform parent, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }
    }
}
