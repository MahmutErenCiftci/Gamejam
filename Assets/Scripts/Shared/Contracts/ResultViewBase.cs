using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Shared
{
    /// <summary>
    /// Bölüm sonu ekranı (docs/OYUN_AKISI.md §8). Berk türetir (ör. UI/ResultPanel),
    /// Mahmut'un bölüm akışı gösterir. Butonlar sadece olay tetikler; sahne geçişini bölüm akışı yapar.
    /// </summary>
    public abstract class ResultViewBase : MonoBehaviour
    {
        public event Action RetryClicked;
        public event Action MenuClicked;

        protected void RaiseRetryClicked() => RetryClicked?.Invoke();
        protected void RaiseMenuClicked() => MenuClicked?.Invoke();

        /// <param name="mistakes">Bu oynanışta yapılan yanlış davranışlar; boşsa "yanlışlar" kutusu gizlenir.</param>
        /// <param name="note">Alt bilgi notu ("Bu bir farkındalık oyunudur...").</param>
        public abstract void Show(string title, IReadOnlyList<string> learned, IReadOnlyList<string> mistakes, string note);

        public abstract void Hide();
    }
}
