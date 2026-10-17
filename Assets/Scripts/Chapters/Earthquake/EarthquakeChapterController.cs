using System.Collections;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>Deprem bölümü akışı — docs/OYUN_AKISI.md §7 (Mahmut).</summary>
    public class EarthquakeChapterController : ChapterControllerBase
    {
        [Header("Deprem")]
        [SerializeField] private EarthquakeCameraShake cameraShake;
        [Tooltip("Tutun (D) kaç saniye basılı tutulunca sarsıntı biter")]
        [SerializeField] private float holdSeconds = 4f;

        protected override ChapterId Chapter => ChapterId.Earthquake;

        protected override IEnumerator RunChapter()
        {
            // E01–E02 (0:00–0:50) ortak açılış: uyanış + sabah rutini
            yield return PlayMorningIntro("E01_WAKE", "E02_ROUTINE");

            // E03 (0:50–1:30) kahvaltı, TV haberi; anne çantayı ve toplanma alanını hatırlatıp işe çıkar
            yield return EnterRoom("E03_NEWS");
            yield return UsePoint("Table", "Kahvaltı yap");
            yield return ShowNews("NEWS_E03_TV");
            yield return Say("E03_NEWS");

            // E04 (1:30–1:45) ani sarsıntı + 5 sn'lik karar
            EnterStep("E04_QUAKE");
            SetShaking(true);
            yield return Say("E04_QUAKE");
            yield return Ask("E_D1");

            // E05 (1:45–2:05) Çök – Kapan – Tutun
            EnterStep("E05_DROPCOVER");
            yield return DropCoverHold();
            SetShaking(false);

            // E06 (2:05–2:45) sarsıntı sonrası: çanta + ocak (2 doğru)
            yield return EnterRoom("E06_AFTER", 0.6f);
            yield return Say("E06_AFTER");
            yield return Ask("E_D2");

            // E07 (2:45–3:15) binadan çıkış: merdiven mi asansör mü
            yield return EnterRoom("E07_EXIT");
            yield return Say("E07_EXIT");
            yield return Ask("E_D3");

            // E08 (3:15–3:45) toplanma alanı
            yield return EnterRoom("E08_ASSEMBLY");
            yield return Say("E08_ASSEMBLY");

            // E09 (3:45–4:05) sonuç
            EnterStep("E09_RESULT");
            yield return ShowResult();
        }

        // ÇÖK (S) → KAPAN (A) → TUTUN (D basılı, sarsıntı bitene kadar). Erken bırakılırsa uyarır, süre durur.
        private IEnumerator DropCoverHold()
        {
            yield return WaitForKey("S", "Çök", () => GameInput.DropPressed);
            // TODO(Mahmut): çökme pozu
            yield return WaitForKey("A", "Kapan", () => GameInput.CoverPressed);
            // TODO(Mahmut): başını koruma pozu

            ShowHint("D", "Tutun (basılı tut)");
            float held = 0f;
            bool warned = false;
            while (held < holdSeconds)
            {
                if (AutoAdvance || GameInput.HoldHeld)
                {
                    if (warned)
                    {
                        ShowHint("D", "Tutun (basılı tut)");
                        warned = false;
                    }
                    held += Time.deltaTime;
                }
                else if (held > 0f && !warned)
                {
                    ShowHint("D", "Sarsıntı bitene kadar tutun!");
                    warned = true;
                }
                yield return null;
            }
            HideHint();
        }

        private void SetShaking(bool shaking)
        {
            if (cameraShake != null) cameraShake.Shaking = shaking;
            // TODO(Mahmut): deprem gürlemesi sesi, lambanın sallanması
        }
    }
}
