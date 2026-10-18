using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Yalnız Test_Berk_SelUI.unity için: SEL3-30 arayüz parçalarını tuşlarla dener (bölüm sahnesine konmaz).
    /// [E] art arda bas → çubuk ilerler (basılı tutmak ilerletmez; Arda'nın kuralının taklidi), dolunca çubuk kapanır
    /// ve "Yol açıldı." çıkar. [1] çubuğu aç/kapat · [2] "Yol açıldı." · [3] görev şeridi · [4] kısa yol uyarısı · [0] sıfırla.
    /// Panele fareyle tıklamak da bir basış sayılır.
    /// </summary>
    public class FloodSelUiTestDriver : MonoBehaviour
    {
        [SerializeField] private MashProgressViewBase mashView;
        [SerializeField] private FloodRouteNotice notice;
        [SerializeField] private FloodObjectiveStrip objective;
        [SerializeField] private FloodRouteWarningBox warning;
        [SerializeField, Min(1)] private int pressesNeeded = 12;
        [SerializeField] private bool showLegend = true;

        private int presses;
        private float hideAt = -1f;

        private void OnEnable() { if (mashView != null) mashView.Clicked += Press; }
        private void OnDisable() { if (mashView != null) mashView.Clicked -= Press; }

        private void Start()
        {
            if (objective != null) objective.Show();
            if (mashView != null) { mashView.SetProgress(0f); mashView.Show(); }
        }

        private void Update()
        {
            if (GameInput.InteractPressed) Press();

            switch (GameInput.DigitPressed)
            {
                case 1:
                    if (mashView == null) break;
                    if (mashView.IsVisible) mashView.Hide(); else mashView.Show();
                    break;
                case 2: if (notice != null) notice.Show(); break;
                case 3: if (objective != null) objective.Show(); break;
                case 4: if (warning != null) warning.Show(); break;
                case 0:
                    presses = 0;
                    hideAt = -1f;
                    if (mashView != null) { mashView.SetProgress(0f); mashView.Show(); }
                    break;
            }

            if (hideAt > 0f && Time.unscaledTime >= hideAt)
            {
                hideAt = -1f;
                if (mashView != null) mashView.Hide();
                if (notice != null) notice.Show();
            }
        }

        private void Press()
        {
            if (mashView == null || !mashView.IsVisible || presses >= pressesNeeded) return;
            presses++;
            mashView.SetProgress((float)presses / pressesNeeded);
            if (presses >= pressesNeeded) hideAt = Time.unscaledTime + 0.35f;
        }

        private void OnGUI()
        {
            if (!showLegend) return;
            GUI.Label(new Rect(8f, Screen.height - 22f, Screen.width - 16f, 20f),
                $"E: bas ({presses}/{pressesNeeded})   1: çubuk   2: Yol açıldı   3: görev   4: kısa yol uyarısı   0: sıfırla");
        }
    }
}
