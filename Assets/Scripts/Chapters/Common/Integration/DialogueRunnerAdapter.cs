using System;
using System.Collections;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Eren'in diyalog sistemini (AfetUyanisi.Dialogue.DialogueRunner: konuşma balonları + alt kutu)
    /// bölüm akışının DialogueRunnerBase sözleşmesine bağlar. ChapterController'ın "dialogue" alanına bu verilir.
    /// </summary>
    public class DialogueRunnerAdapter : DialogueRunnerBase
    {
        [SerializeField] private AfetUyanisi.Dialogue.DialogueRunner runner;

        public AfetUyanisi.Dialogue.DialogueRunner Runner => runner;

        public override IEnumerator Play(string storySceneId)
        {
            if (runner == null)
            {
                Debug.LogWarning("[DialogueRunnerAdapter] DialogueRunner bağlı değil.");
                yield break;
            }

            var data = runner.Data;
            if (data?.scenes == null || !Array.Exists(data.scenes, s => s.id == storySceneId))
            {
                Debug.LogWarning($"[DialogueRunnerAdapter] '{storySceneId}' hikâye verisinde yok, atlandı.");
                yield break;
            }

            while (runner.IsBusy) yield return null;
            bool done = false;
            runner.PlayScene(storySceneId, () => done = true);
            while (!done) yield return null;
        }
    }
}
