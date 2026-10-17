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

        /// <summary>Nesneye bağlı ipucu. Eski UI uygulamaları iki parametreli Show ile çalışmaya devam eder.</summary>
        public virtual void Show(string keyLabel, string actionLabel, Transform anchor, Vector3 worldOffset)
            => Show(keyLabel, actionLabel);

        /// <summary>Yalnızca görsel geri bildirim; etkileşimi çağırmaz.</summary>
        public virtual void Press() { }

        public abstract void Hide();
    }
}
