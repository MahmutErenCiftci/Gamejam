using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Shared
{
    /// <summary>
    /// Ekranı karartıp açan geçiş perdesi. Berk türetir (ör. UI/FadePanel); menü ve bölüm akışları kullanır.
    /// </summary>
    public abstract class ScreenFaderBase : MonoBehaviour
    {
        /// <summary>Ekranı karartır (coroutine olarak beklenir).</summary>
        public abstract IEnumerator FadeOut(float seconds);

        /// <summary>Kararmış ekranı açar.</summary>
        public abstract IEnumerator FadeIn(float seconds);

        /// <summary>Anında tamamen siyah (true) ya da tamamen açık (false) yapar.</summary>
        public abstract void SetOpaque(bool black);
    }
}
