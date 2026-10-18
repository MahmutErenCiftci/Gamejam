using System;
using System.Collections;
using AfetUyanisi.News;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Eren'in haber bileşenlerini (NewsBroadcast, RumorCards, AlertNotification) NewsPlayerBase sözleşmesine bağlar.
    /// Inspector'da her haber ID'si (NEWS_F03_TV ...) sahnedeki bileşenle eşleştirilir; Play bileşen bitene kadar bekler.
    /// </summary>
    public class NewsPlayerAdapter : NewsPlayerBase
    {
        [Serializable]
        public class Entry
        {
            public string newsId;
            [Tooltip("NewsBroadcast, RumorCards ya da AlertNotification")]
            public MonoBehaviour player;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>Son söylenti kartlarında oyuncunun kaç kez söylentiyi seçtiği (sonuç ekranı için).</summary>
        public int LastRumorWrongAttempts { get; private set; }

        public override IEnumerator Play(string newsId)
        {
            var entry = entries == null ? null : Array.Find(entries, e => e != null && e.newsId == newsId);
            if (entry == null || entry.player == null)
            {
                Debug.Log($"[NewsPlayerAdapter] (bağlı haber yok) {newsId}");
                yield break;
            }

            bool done = false;
            switch (entry.player)
            {
                case NewsBroadcast tv:
                {
                    Action onDone = () => done = true;
                    tv.Finished += onDone;
                    tv.Play();
                    if (!tv.IsPlaying) done = true; // veri okunamadıysa takılma
                    while (!done) yield return null;
                    tv.Finished -= onDone;
                    break;
                }
                case RumorCards cards:
                {
                    Action<int> onDone = wrong => { LastRumorWrongAttempts = wrong; done = true; };
                    cards.Finished += onDone;
                    cards.Play();
                    if (!cards.IsPlaying) done = true;
                    while (!done) yield return null;
                    cards.Finished -= onDone;
                    break;
                }
                case AlertNotification alert:
                {
                    Action onDone = () => done = true;
                    alert.Finished += onDone;
                    alert.Play();
                    if (!alert.IsPlaying) done = true;
                    while (!done) yield return null;
                    alert.Finished -= onDone;
                    break;
                }
                default:
                    Debug.LogWarning($"[NewsPlayerAdapter] {newsId}: desteklenmeyen bileşen {entry.player.GetType().Name}");
                    break;
            }
        }
    }
}
