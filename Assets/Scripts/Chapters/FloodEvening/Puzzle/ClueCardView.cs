using System;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Şifre ipucu kartı arayüzü (prefab: Assets/Prefabs/FloodEvening/UI/FE_ClueCardView.prefab). İçeriği CodeClueSign verir.
    /// </summary>
    public class ClueCardView : FloodPuzzleViewBase
    {
        [Header("Parçalar")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text clueText;
        [SerializeField] private Text footerText;
        [SerializeField] private Button okButton;

        [Header("Metinler")]
        [Tooltip("{0} = levha numarası, {1} = toplam levha")]
        [SerializeField] private string titleFormat = "ŞİFRE İPUCU {0}/{1}";
        [SerializeField] private string okLabel = "Tamam (E)";
        [SerializeField, Min(8)] private int clueFontSize = 20;
        [SerializeField, Min(8)] private int clueFontSizeMultiLine = 17;

        public event Action OkClicked;

        public void Show(int index, int count, string clue, int clueLines, string footer)
        {
            base.Show();
            SetText(titleText, string.Format(titleFormat, index, count));
            if (clueText != null)
            {
                clueText.text = clue ?? "";
                clueText.fontSize = clueLines > 1 ? clueFontSizeMultiLine : clueFontSize;
            }
            SetText(footerText, footer);
            if (okButton != null) SetText(okButton.GetComponentInChildren<Text>(true), okLabel);
        }

        protected override void WireButtons() => Wire(okButton, () => OkClicked?.Invoke());

        protected override void Build(Font f, RectTransform root)
        {
            var dim = FloodPuzzleUi.Img("Dim", root, FloodPuzzleUi.Hex(0x0B1220, 0.5f), null, true);
            FloodPuzzleUi.Stretch(dim.rectTransform);
            panel = FloodPuzzleUi.Box("Panel", root, FloodPuzzleUi.Turquoise, FloodPuzzleUi.Panel, 0f, 0f, 300f, 160f, true).rectTransform;
            FloodPuzzleUi.At(FloodPuzzleUi.Img("TitleBand", panel, FloodPuzzleUi.PanelLight).rectTransform, 0f, 66f, 298f, 26f);
            titleText = FloodPuzzleUi.TxtAt("Title", panel, f, 13, FloodPuzzleUi.Turquoise, TextAnchor.MiddleCenter, 0f, 66f, 280f, 20f);
            clueText = FloodPuzzleUi.TxtAt("Clue", panel, f, clueFontSize, FloodPuzzleUi.Cream, TextAnchor.MiddleCenter, 0f, 22f, 280f, 52f);
            footerText = FloodPuzzleUi.TxtAt("Footer", panel, f, 9, FloodPuzzleUi.Muted, TextAnchor.MiddleCenter, 0f, -24f, 280f, 26f);
            okButton = FloodPuzzleUi.Btn("OkButton", panel, f, okLabel, 9, 0f, -60f, 96f, 18f,
                FloodPuzzleUi.Turquoise, FloodPuzzleUi.TurquoiseDark, FloodPuzzleUi.Cream);
        }
    }
}
