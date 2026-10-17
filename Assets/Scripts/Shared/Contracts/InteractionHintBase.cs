using UnityEngine;

namespace AfetUyanisi.Shared
{
    /// <summary>
    /// "[E] Kalk" gibi tuş ipucu. Berk türetir (ör. UI/InteractionHint), bölüm akışları kullanır.
    /// </summary>
    public abstract class InteractionHintBase : MonoBehaviour
    {
        /// <param name="keyLabel">Tuş yazısı: "E", "S", "1"...</param>
        /// <param name="actionLabel">Eylem: "Kalk", "Yüzünü yıka"...</param>
        public abstract void Show(string keyLabel, string actionLabel);

        public abstract void Hide();
    }
}
