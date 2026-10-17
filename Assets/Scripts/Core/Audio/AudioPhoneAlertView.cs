using System.Collections;
using AfetUyanisi.Chapters.Earthquake;
using UnityEngine;

namespace AfetUyanisi.Core
{
    /// <summary>Public PhoneAlertController.View üzerinden bildirimin gerçek Show çağrısını seslendirir.</summary>
    [DisallowMultipleComponent]
    public sealed class AudioPhoneAlertView : PhoneAlertViewBase
    {
        public PhoneAlertViewBase Inner { get; set; }

        public override IEnumerator Show(string app, string title, string body, string note, string button)
        {
            if (Inner == null) yield break;
            GameAudio.PlaySfx("SFX_Phone_Notify", 0.7f);
            yield return Inner.Show(app, title, body, note, button);
        }
    }
}
