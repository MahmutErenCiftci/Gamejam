using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Shared
{
    /// <summary>
    /// TV haberi, resmî duyuru kartı ve söylenti kartlarının oynatıcısı. Eren türetir (News/...);
    /// Mahmut'un bölüm akışı <c>yield return news.Play("NEWS_F03_TV");</c> şeklinde çağırır.
    /// ID'ler ve içerikler: docs/OYUN_AKISI.md §6.3, §7.3.
    /// </summary>
    public abstract class NewsPlayerBase : MonoBehaviour
    {
        /// <summary>Haberi/kartı gösterir; oyuncu kapatınca (GameInput.ContinuePressed) ya da süre bitince biter.</summary>
        public abstract IEnumerator Play(string newsId);
    }
}
