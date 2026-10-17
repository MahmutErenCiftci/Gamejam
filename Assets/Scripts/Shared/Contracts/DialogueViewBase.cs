using UnityEngine;

namespace AfetUyanisi.Shared
{
    /// <summary>
    /// Diyalog kutusunun görünümü. Berk türetir (ör. UI/DialoguePanel), Arda'nın DialogueRunner'ı kullanır.
    /// </summary>
    public abstract class DialogueViewBase : MonoBehaviour
    {
        /// <summary>Satırı gösterir. speaker boşsa isim alanı gizlenir.</summary>
        public abstract void ShowLine(string speaker, string text);

        public abstract void Hide();

        /// <summary>Yazı harf harf açılıyorsa true. Daktilo efekti yoksa hep false kalır.</summary>
        public virtual bool IsRevealing => false;

        /// <summary>Daktilo efektini atlayıp satırın tamamını gösterir.</summary>
        public virtual void RevealAll() { }
    }
}
