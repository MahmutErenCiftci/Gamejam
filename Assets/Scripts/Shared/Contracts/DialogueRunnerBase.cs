using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Shared
{
    /// <summary>
    /// Diyalog oynatıcısı. Arda türetir (Dialogue/DialogueRunner); Mahmut'un bölüm akışı
    /// <c>yield return dialogue.Play("F04_MOTHER");</c> şeklinde çağırır.
    /// </summary>
    public abstract class DialogueRunnerBase : MonoBehaviour
    {
        /// <summary>
        /// Story JSON'daki sahnenin satırlarını sırayla gösterir. Oyuncu her satırı GameInput.ContinuePressed
        /// ile geçer; son satırdan sonra kutu kapanır ve coroutine biter.
        /// </summary>
        public abstract IEnumerator Play(string storySceneId);
    }
}
