using System.Collections;
using AfetUyanisi.Chapters.Earthquake;
using UnityEngine;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// TimedDecisionController.View sarmalayıcısı: her çağrıyı asıl görünüme iletir, geri bildirim açılırken
    /// doğru/yanlış sesini çalar. Süre cezası varsa ceza sesi (OnPenalty) zaten çaldığı için yanlış sesi çalınmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioTimedQuestionView : TimedQuestionViewBase
    {
        public TimedQuestionViewBase Inner { get; set; }

        public override void Open(string speaker, Sprite portrait, string question, string[] options, float timeLimit)
        {
            if (Inner != null) Inner.Open(speaker, portrait, question, options, timeLimit);
        }

        public override void SetTimeLeft(float seconds)
        {
            if (Inner != null) Inner.SetTimeLeft(seconds);
        }

        public override bool TryGetChoice(out int index)
        {
            index = -1;
            return Inner != null && Inner.TryGetChoice(out index);
        }

        public override IEnumerator ShowFeedback(bool correct, bool timedOut, string text, int penaltySeconds)
        {
            if (correct) GameAudio.PlaySfx("SFX_Correct", 0.7f);
            else if (penaltySeconds <= 0) GameAudio.PlaySfx("SFX_Wrong", 0.6f);
            if (Inner != null) yield return Inner.ShowFeedback(correct, timedOut, text, penaltySeconds);
        }

        public override void Close()
        {
            if (Inner != null) Inner.Close();
        }
    }
}
