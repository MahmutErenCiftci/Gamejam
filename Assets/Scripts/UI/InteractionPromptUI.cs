using AfetUyanisi.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.UI
{
    /// <summary>Tek sunumsal ipucu; hedef seçimi ve tuş okuma mevcut etkileşim sisteminde kalır.</summary>
    [DefaultExecutionOrder(100)]
    public class InteractionPromptUI : InteractionHintBase
    {
        [Header("Yazı tipi (Minecraft sağlanmadıysa OFL Pixelify Sans)")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Min(8)] private float fontSize = 12f;
        [Header("640 x 360 referans piksel")]
        [SerializeField, Min(1)] private float padding = 5f;
        [SerializeField, Min(1)] private float keySize = 18f;
        [SerializeField, Min(1)] private float gap = 5f;
        [SerializeField, Min(1)] private float minimumWidth = 65f;
        [SerializeField, Min(80)] private float maximumWidth = 300f;
        [SerializeField, Min(1)] private float screenMargin = 6f;
        [SerializeField] private float verticalOffset = 6f;
        [SerializeField, Range(1, 8)] private int pointerSize = 4;
        [Header("Renkler")]
        [SerializeField] private Color backgroundColor = new Color32(10, 25, 44, 255);
        [SerializeField] private Color borderColor = new Color32(47, 106, 140, 255);
        [SerializeField] private Color textColor = new Color32(243, 236, 221, 255);
        [SerializeField] private Color keyColor = new Color32(246, 198, 125, 255);
        [SerializeField] private Color pointerColor = new Color32(131, 217, 241, 255);
        [Header("Geçişler")]
        [SerializeField, Min(0)] private float appearSeconds = 0.15f;
        [SerializeField, Min(0)] private float disappearSeconds = 0.12f;
        [SerializeField] private float travelPixels = 4f;

        private Canvas canvas;
        private RectTransform canvasRect, panel, keyRect, labelRect, pointer;
        private CanvasGroup group;
        private TextMeshProUGUI keyText, labelText;
        private Image keyImage, borderImage;
        private Transform target;
        private Camera worldCamera;
        private Vector3 targetOffset;
        private bool anchored, requested;
        private float visibility, pressRemaining;
        private Vector2 previousCanvasSize;
        private Vector2 lastPoint;
        private string shownKey, shownLabel;

        public Vector2 PanelSize => panel != null ? panel.sizeDelta : Vector2.zero;
        public RectTransform Panel => panel;
        public TMP_FontAsset Font => font;
        public bool IsVisible => group != null && group.alpha > 0f;

        private void Awake() => EnsureBuilt();
        public void SetFont(TMP_FontAsset value) => font = value;

        private void EnsureBuilt()
        {
            if (canvas != null) return;
            canvas = new GameObject("InteractionPromptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            canvas.pixelPerfect = true;
            canvasRect = (RectTransform)canvas.transform;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = ImageRect("Prompt", canvas.transform, borderColor);
            borderImage = panel.GetComponent<Image>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var fill = ImageRect("Fill", panel, backgroundColor);
            Stretch(fill, 1f);
            // Bir piksellik köşe adımları; bulanık sprite/gölge yok.
            CutCorner("TopLeft", panel, new Vector2(0, 1));
            CutCorner("TopRight", panel, Vector2.one);
            CutCorner("BottomLeft", panel, Vector2.zero);
            CutCorner("BottomRight", panel, new Vector2(1, 0));

            pointer = new GameObject("Pointer", typeof(RectTransform)).GetComponent<RectTransform>();
            pointer.SetParent(panel, false);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0);
            pointer.pivot = new Vector2(0.5f, 1);
            for (int i = 0; i < pointerSize; i++)
            {
                var row = ImageRect("Pixel" + i, pointer, pointerColor);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
                row.pivot = new Vector2(0.5f, 1);
                row.sizeDelta = new Vector2((pointerSize - i) * 2f - 1f, 1f);
                row.anchoredPosition = new Vector2(0, -i);
            }
            keyRect = ImageRect("Keycap", panel, keyColor);
            keyImage = keyRect.GetComponent<Image>();
            keyRect.anchorMin = keyRect.anchorMax = new Vector2(0, 0.5f);
            keyRect.pivot = new Vector2(0, 0.5f);
            keyText = Text("Key", keyRect, backgroundColor);
            keyText.alignment = TextAlignmentOptions.Center;
            Stretch(keyText.rectTransform, 0);
            labelText = Text("Label", panel, textColor);
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelRect = labelText.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0, 0.5f);
            labelRect.pivot = new Vector2(0, 0.5f);
            HideImmediate();
        }

        public override void Show(string keyLabel, string actionLabel)
            => Show(keyLabel, actionLabel, null, Vector3.zero);

        public override void Show(string keyLabel, string actionLabel, Transform anchor, Vector3 worldOffset)
        {
            EnsureBuilt();
            bool labelChanged = shownKey != keyLabel || shownLabel != actionLabel;
            bool targetChanged = target != anchor;
            shownKey = keyLabel;
            shownLabel = actionLabel;
            target = anchor;
            targetOffset = worldOffset;
            anchored = anchor != null;
            if (worldCamera == null || targetChanged) worldCamera = Camera.main;
            if (labelChanged)
            {
                keyText.SetText(keyLabel ?? "");
                labelText.SetText(actionLabel ?? "");
                Resize();
            }
            requested = !string.IsNullOrEmpty(actionLabel);
        }

        public override void Hide() => requested = false;
        public override void Press() => pressRemaining = 0.08f;

        public void HideImmediate()
        {
            requested = false;
            visibility = 0;
            if (group != null) group.alpha = 0;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (canvas == null) return;
            bool valid = requested && (!anchored || (target != null && target.gameObject.activeInHierarchy));
            if (canvasRect.rect.size != previousCanvasSize) Resize();
            Vector2 point = anchored ? lastPoint : new Vector2(0, screenMargin + pointerSize);
            if (anchored && target != null)
            {
                if (worldCamera == null || !worldCamera.isActiveAndEnabled) worldCamera = Camera.main;
                if (worldCamera == null) valid = false;
                else
                {
                    Vector3 screen = worldCamera.WorldToScreenPoint(target.position + targetOffset);
                    valid &= screen.z > 0f && worldCamera.pixelRect.Contains(screen);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out point);
                    point.y -= canvasRect.rect.yMin;
                    point.x -= canvasRect.rect.center.x;
                    point.y += verticalOffset + pointerSize;
                    lastPoint = point;
                }
            }
            float duration = valid ? appearSeconds : disappearSeconds;
            visibility = Mathf.MoveTowards(visibility, valid ? 1f : 0f, duration > 0 ? Time.unscaledDeltaTime / duration : 1f);
            panel.gameObject.SetActive(visibility > 0);
            group.alpha = visibility;
            if (visibility <= 0) return;
            float half = panel.sizeDelta.x * 0.5f;
            float xLimit = Mathf.Max(0, canvasRect.rect.width * 0.5f - half - screenMargin);
            float x = Mathf.Clamp(point.x, -xLimit, xLimit);
            float top = Mathf.Max(screenMargin + pointerSize, canvasRect.rect.height - panel.sizeDelta.y - screenMargin);
            float y = Mathf.Clamp(point.y - Mathf.Round((1f - visibility) * travelPixels), screenMargin + pointerSize, top);
            panel.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            pointer.anchoredPosition = new Vector2(Mathf.Round(Mathf.Clamp(point.x - x, -half + pointerSize + 2, half - pointerSize - 2)), 0);
            pointer.gameObject.SetActive(anchored);
            pressRemaining = Mathf.Max(0, pressRemaining - Time.unscaledDeltaTime);
            bool pressed = pressRemaining > 0;
            keyRect.anchoredPosition = new Vector2(padding, pressed ? -1 : 0);
            keyImage.color = pressed ? Color.Lerp(keyColor, backgroundColor, 0.25f) : Color.Lerp(keyColor, textColor, Mathf.Sin(Time.unscaledTime * 3f) * 0.035f + 0.035f);
            borderImage.color = pressed ? pointerColor : borderColor;
        }

        private void Resize()
        {
            previousCanvasSize = canvasRect.rect.size;
            float fixedWidth = padding * 2f + keySize + gap;
            float available = Mathf.Max(fixedWidth + fontSize, Mathf.Min(maximumWidth, canvasRect.rect.width - screenMargin * 2f));
            float textWidth = Mathf.Max(fontSize, available - fixedWidth);
            Vector2 preferred = labelText.GetPreferredValues(shownLabel ?? "", textWidth, Mathf.Infinity);
            float width = Mathf.Min(available, Mathf.Max(minimumWidth, Mathf.Ceil(preferred.x) + fixedWidth));
            Vector2 wrapped = labelText.GetPreferredValues(shownLabel ?? "", width - fixedWidth, Mathf.Infinity);
            float height = Mathf.Ceil(Mathf.Max(keySize, wrapped.y) + padding * 2f);
            panel.sizeDelta = new Vector2(width, height);
            keyRect.sizeDelta = new Vector2(keySize, keySize);
            keyRect.anchoredPosition = new Vector2(padding, 0);
            labelRect.sizeDelta = new Vector2(width - fixedWidth, height - padding * 2f);
            labelRect.anchoredPosition = new Vector2(padding + keySize + gap, 0);
        }

        private TextMeshProUGUI Text(string objectName, Transform parent, Color color)
        {
            var text = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            return text;
        }

        private static RectTransform ImageRect(string objectName, Transform parent, Color color)
        {
            var image = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        private void CutCorner(string objectName, Transform parent, Vector2 anchor)
        {
            var corner = ImageRect(objectName, parent, backgroundColor);
            corner.anchorMin = corner.anchorMax = corner.pivot = anchor;
            corner.sizeDelta = Vector2.one;
        }

        private void OnDisable() => HideImmediate();
    }
}
