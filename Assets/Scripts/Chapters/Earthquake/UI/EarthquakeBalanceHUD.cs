using AfetUyanisi.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>Kamera sarsıntısından bağımsız, 640x360 pixel HUD. Input veya oyun durumu yönetmez.</summary>
    public class EarthquakeBalanceHUD : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform marker, progress, controls, banner;
        private TextMeshProUGUI stage, score, warning, controlsText;
        private Image markerImage;
        private Image controlsImage;
        private EqBalanceTexts texts;
        private EarthquakeBalanceSettings settings;
        private float started;
        private int previousStage = -1;
        private int previousScore = -1;
        private int previousMessage = -1;
        private readonly Color navy = new Color32(10, 25, 44, 255);
        private readonly Color cream = new Color32(243, 236, 221, 255);
        private readonly Color cyan = new Color32(131, 217, 241, 255);
        private readonly Color amber = new Color32(246, 198, 125, 255);
        private readonly Color red = new Color32(190, 59, 61, 255);

        public void Begin(EarthquakeBalanceSettings configuration, EqBalanceTexts content)
        {
            settings = configuration;
            texts = content ?? new EqBalanceTexts();
            if (canvas == null) Build();
            canvas.gameObject.SetActive(true);
            previousStage = previousScore = previousMessage = -1;
            started = Time.unscaledTime;
            controlsText.text = texts.controls;
        }

        private void Build()
        {
            canvas = new GameObject("EQ_BalanceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 91;
            canvas.pixelPerfect = true;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 360);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = Box("BalancePanel", canvas.transform, new Color32(47, 106, 140, 255));
            Place(panel, new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(330, 96));
            var fill = Box("Fill", panel, navy); Stretch(fill, 1);
            Label("Title", panel, texts.title, 12, cream, new Vector2(0, -12), new Vector2(310, 18));
            stage = Label("Stage", panel, "", 10, cyan, new Vector2(0, -29), new Vector2(310, 14));
            var bar = Box("BalanceBar", panel, cream);
            Place(bar, new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(300, 16));
            var redZone = Box("Danger", bar, red); Stretch(redZone, 1);
            var amberZone = Box("Recover", bar, new Color32(166, 111, 56, 255));
            Place(amberZone, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(298 * settings.redThreshold, 14));
            var greenZone = Box("Stable", bar, new Color32(34, 126, 84, 255));
            Place(greenZone, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(298 * settings.greenZone, 14));
            var center = Box("Center", bar, cream);
            Place(center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1, 22));
            marker = Box("Indicator", bar, cyan);
            markerImage = marker.GetComponent<Image>();
            Place(marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 22));
            var tip = Box("Pointer", marker, cyan);
            Place(tip, new Vector2(0.5f, 0), new Vector2(0, -3), new Vector2(7, 3));
            score = Label("Score", panel, "", 10, cream, new Vector2(0, -73), new Vector2(310, 15));
            progress = Box("SequenceProgress", panel, cyan);
            Place(progress, new Vector2(0, 0), new Vector2(1, 2), new Vector2(1, 2));
            progress.pivot = Vector2.zero;

            controls = Box("Controls", canvas.transform, navy);
            controlsImage = controls.GetComponent<Image>();
            Place(controls, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(372, 27));
            controlsText = Label("Keys", controls, texts.controls, 11, cream, Vector2.zero, new Vector2(360, 24));
            controlsText.rectTransform.anchorMin = controlsText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            controlsText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            controlsText.rectTransform.anchoredPosition = Vector2.zero;
            var note = Label("Note", canvas.transform, texts.note, 9, cream, new Vector2(0, 6), new Vector2(600, 16));
            note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(0.5f, 0);
            note.rectTransform.pivot = new Vector2(0.5f, 0);

            banner = Box("ProtectReminder", canvas.transform, navy);
            Place(banner, new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(390, 25));
            warning = Label("Message", banner, "", 11, amber, new Vector2(0, -2), new Vector2(380, 22));
        }

        public void Draw(EarthquakeBalanceModel state)
        {
            if (canvas == null) return;
            marker.anchoredPosition = new Vector2(Mathf.Round(state.Position * 148), 0);
            markerImage.color = Mathf.Abs(state.Position) > settings.redThreshold ? red : cyan;
            progress.sizeDelta = new Vector2(Mathf.Round(state.Progress * 328), 2);
            if (state.Stage != previousStage)
            {
                previousStage = state.Stage;
                stage.text = state.Stage == 0 ? texts.initial : state.Stage == 1 ? texts.strong : texts.irregular;
            }
            int value = Mathf.RoundToInt(state.Score);
            if (value != previousScore)
            {
                previousScore = value;
                score.SetText(texts.score, value);
            }
            int message = !state.Holding && state.ReleasedSeconds > settings.releaseGrace ? 1 : Mathf.Abs(state.Position) > settings.redThreshold || state.Score < 35 ? 2 : Time.unscaledTime - started < 0.8f ? 3 : 0;
            if (message != previousMessage)
            {
                previousMessage = message;
                banner.gameObject.SetActive(message != 0);
                warning.text = message == 1 ? texts.release : message == 2 ? texts.unstable : texts.warning;
                warning.color = message == 3 ? red : amber;
            }
            controlsImage.color = state.Holding ? navy : Color.Lerp(navy, amber, 0.15f);
        }

        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }
        private void OnDisable() => Hide();

        private TextMeshProUGUI Label(string objectName, Transform parent, string text, int size, Color color, Vector2 position, Vector2 dimensions)
        {
            var label = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            var fontPrefab = Resources.Load<InteractionPromptUI>("InteractionUI/InteractionPromptUI");
            label.font = fontPrefab != null ? fontPrefab.Font : TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.richText = false;
            Place(label.rectTransform, new Vector2(0.5f, 1), position, dimensions);
            return label;
        }

        private static RectTransform Box(string objectName, Transform parent, Color color)
        {
            var image = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }
    }
}
