using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Shared
{
    /// <summary>
    /// Karar panelinin görünümü (2–5 seçenek, isteğe bağlı sayaç). Berk türetir (ör. UI/DecisionPanel),
    /// Arda'nın DecisionRunner'ı kullanır. Seçenekler ekranda 1'den başlayarak numaralanır.
    /// </summary>
    public abstract class ChoiceViewBase : MonoBehaviour
    {
        /// <summary>Oyuncu bir seçeneğe tıkladığında, seçeneğin 0'dan başlayan sırasıyla tetiklenir.</summary>
        public event Action<int> OptionClicked;

        protected void RaiseOptionClicked(int index) => OptionClicked?.Invoke(index);

        public abstract void ShowChoices(string prompt, IReadOnlyList<string> options);

        /// <summary>Süreli kararda kalan süre: remaining01 1→0 akar (çubuk), secondsLeft ekranda yazılır.</summary>
        public abstract void SetTimer(float remaining01, float secondsLeft);

        public abstract void HideTimer();

        /// <summary>Seçeneği doğru (yeşil/tik) ya da yanlış (kırmızı/çarpı) olarak işaretler.</summary>
        public abstract void MarkOption(int index, bool correct);

        /// <summary>Geri bildirim gösterilirken butonları kilitlemek için.</summary>
        public abstract void SetOptionsInteractable(bool interactable);

        public abstract void ShowFeedback(string text, bool correct);

        public abstract void HideFeedback();

        public abstract void Hide();
    }
}
