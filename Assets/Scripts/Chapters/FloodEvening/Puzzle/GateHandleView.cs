using System;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Bahçe kapısı kolu arayüzü (prefab: Assets/Prefabs/FloodEvening/UI/FE_GateHandleView.prefab).
    /// Mantık GateHandleMinigame'dedir; bu sınıf yalnızca çizer ve "Kapat" tıklamasını bildirir.
    /// Bütün metinler Inspector alanıdır (prefab'da düzenlenir).
    /// </summary>
    public class GateHandleView : FloodPuzzleViewBase
    {
        public enum Status { Idle, Turning, WrongWay, Done }

        [Header("Parçalar")]
        [SerializeField] private RectTransform wheel;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private Text titleText;
        [SerializeField] private Text instructionText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text percentText;
        [SerializeField] private Text keyboardHintText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Sprite wheelSprite;
        [SerializeField] private Sprite arrowSprite;

        [Header("Metinler")]
        [SerializeField] private string title = "BAHÇE KAPISI";
        [SerializeField, TextArea] private string instruction = "Kolu çevirerek kapıyı aç: farenin sol tuşunu basılı tut\nve çarkın çevresinde SAAT YÖNÜNDE daire çiz.";
        [SerializeField] private string keyboardHint = "Klavye: D veya → tuşunu basılı tut";
        [SerializeField] private string closeLabel = "Kapat (Esc)";
        [SerializeField] private string statusIdle = "Kol paslanmış ama dönüyor.";
        [SerializeField] private string statusTurning = "Kol dönüyor, devam et...";
        [SerializeField] private string statusWrongWay = "Ters yön: SAAT YÖNÜNDE çevir (D / →).";
        [SerializeField] private string statusDone = "Kapı açıldı! Güvenli bölgeye devam et.";
        [Tooltip("{0} = yüzde (0-100)")]
        [SerializeField] private string percentFormat = "%{0}";

        public event Action CloseClicked;

        /// <summary>Çark merkezinin ekran konumu (piksel).</summary>
        public Vector2 WheelScreenCenter => wheel != null ? RectTransformUtility.WorldToScreenPoint(null, wheel.position) : new Vector2(Screen.width, Screen.height) * 0.5f;

        public override void Show()
        {
            base.Show();
            SetText(titleText, title);
            SetText(instructionText, instruction);
            SetText(keyboardHintText, keyboardHint);
            if (closeButton != null) SetText(closeButton.GetComponentInChildren<Text>(true), closeLabel);
        }

        public void Render(float progress01, float degrees, Status status)
        {
            if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, 0f, -degrees);
            if (progressFill != null) progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
            SetText(percentText, string.Format(percentFormat, Mathf.FloorToInt(Mathf.Clamp01(progress01) * 100f + 0.0001f)));
            if (statusText == null) return;
            switch (status)
            {
                case Status.Done: statusText.text = statusDone; statusText.color = FloodPuzzleUi.Green; break;
                case Status.WrongWay: statusText.text = statusWrongWay; statusText.color = FloodPuzzleUi.Amber; break;
                case Status.Turning: statusText.text = statusTurning; statusText.color = FloodPuzzleUi.Cream; break;
                default: statusText.text = statusIdle; statusText.color = FloodPuzzleUi.Muted; break;
            }
        }

        protected override void WireButtons() => Wire(closeButton, () => CloseClicked?.Invoke());

        protected override void Build(Font f, RectTransform root)
        {
            var dim = FloodPuzzleUi.Img("Dim", root, FloodPuzzleUi.Hex(0x0B1220, 0.62f), null, true);
            FloodPuzzleUi.Stretch(dim.rectTransform);

            panel = FloodPuzzleUi.Box("Panel", root, FloodPuzzleUi.Turquoise, FloodPuzzleUi.Panel, 0f, 0f, 300f, 252f, true).rectTransform;
            FloodPuzzleUi.At(FloodPuzzleUi.Img("TitleBand", panel, FloodPuzzleUi.PanelLight).rectTransform, 0f, 112f, 298f, 26f);
            titleText = FloodPuzzleUi.TxtAt("Title", panel, f, 13, FloodPuzzleUi.Turquoise, TextAnchor.MiddleCenter, 0f, 112f, 280f, 20f);
            instructionText = FloodPuzzleUi.TxtAt("Instruction", panel, f, 9, FloodPuzzleUi.Cream, TextAnchor.MiddleCenter, 0f, 86f, 280f, 24f);

            var arrow = FloodPuzzleUi.Img("TurnArrow", panel, arrowSprite != null ? Color.white : new Color(0f, 0f, 0f, 0f), arrowSprite);
            FloodPuzzleUi.At(arrow.rectTransform, 0f, 10f, 128f, 128f);
            var wheelImg = FloodPuzzleUi.Img("Wheel", panel, wheelSprite != null ? Color.white : FloodPuzzleUi.Muted, wheelSprite, true);
            wheel = FloodPuzzleUi.At(wheelImg.rectTransform, 0f, 10f, 96f, 96f);
            if (wheelSprite == null) FloodPuzzleUi.At(FloodPuzzleUi.Img("Grip", wheel, FloodPuzzleUi.Amber).rectTransform, 0f, 40f, 12f, 12f);

            statusText = FloodPuzzleUi.TxtAt("Status", panel, f, 9, FloodPuzzleUi.Muted, TextAnchor.MiddleCenter, 0f, -60f, 280f, 14f);
            var bar = FloodPuzzleUi.Box("ProgressBar", panel, FloodPuzzleUi.Turquoise, FloodPuzzleUi.Ink, -16f, -76f, 210f, 10f);
            progressFill = FloodPuzzleUi.Img("ProgressFill", bar.transform, FloodPuzzleUi.Turquoise).rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.pivot = new Vector2(0f, 0.5f);
            progressFill.offsetMin = new Vector2(2f, 2f);
            progressFill.offsetMax = new Vector2(-2f, -2f);
            percentText = FloodPuzzleUi.TxtAt("Percent", panel, f, 10, FloodPuzzleUi.Turquoise, TextAnchor.MiddleLeft, 117f, -76f, 40f, 14f);
            keyboardHintText = FloodPuzzleUi.TxtAt("KeyboardHint", panel, f, 9, FloodPuzzleUi.Muted, TextAnchor.MiddleCenter, 0f, -93f, 280f, 14f);
            closeButton = FloodPuzzleUi.Btn("CloseButton", panel, f, closeLabel, 9, 0f, -111f, 96f, 18f,
                FloodPuzzleUi.TurquoiseDark, FloodPuzzleUi.PanelLight, FloodPuzzleUi.Cream);
        }
    }
}
