using System.Collections;
using AfetUyanisi.Shared;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>Sel bölümü akışı — docs/OYUN_AKISI.md §6 (Mahmut).</summary>
    public class FloodChapterController : ChapterControllerBase
    {
        protected override ChapterId Chapter => ChapterId.Flood;

        protected override IEnumerator RunChapter()
        {
            // F01–F02 (0:00–0:50) ortak açılış: uyanış + sabah rutini
            yield return PlayMorningIntro("F01_WAKE", "F02_WASH");

            // F03 (0:50–1:20) mutfak, TV haberi
            yield return EnterRoom("F03_NEWS");
            yield return ShowNews("NEWS_F03_TV");
            yield return Say("F03_NEWS");

            // F04 (1:20–1:50) anne, karar 1, resmî duyuru, eşyalar
            EnterStep("F04_MOTHER");
            yield return Say("F04_MOTHER");
            yield return Ask("F_D1");
            yield return Say("F04_CHECK");
            yield return ShowNews("NEWS_F04_OFFICIAL");
            yield return Say("F04_PREPARE");
            // TODO(Mahmut): alınan eşya HUD ikonu (P1)
            yield return UsePoint("Coat", "Yağmurluğu al");
            yield return UsePoint("Umbrella", "Şemsiyeyi al");
            yield return UsePoint("ExitDoor", "Dışarı çık");

            // F05 (1:50–2:30) yağmurlu sokak
            yield return EnterRoom("F05_STREET");
            // TODO(Mahmut): yağmur 5 sn içinde şiddetlenir
            yield return Say("F05_STREET");
            yield return UsePoint("WalkEnd", "Yürümeye devam et");

            // F06 (2:30–3:10) söylentiler + karar 2
            EnterStep("F06_RUMORS");
            yield return Say("F06_RUMORS");
            yield return ShowNews("NEWS_F06_RUMORS");
            var road = new DecisionResult();
            yield return Ask("F_D2", road, ShowRiskAfterWrongChoice);
            if (road.FinalChoice != null && road.FinalChoice.id == "F_D2_D")
                yield return ShowNews("NEWS_F06_OFFICIAL_ROAD");

            // F07 (3:10–3:40) riskli bölge, yolun kenarından
            yield return EnterRoom("F07_RISK");
            yield return Say("F07_RISK");
            yield return UsePoint("Sign", "Tabelayı incele");

            // F08 (3:40–4:10) güvenli rota → okul kapısı
            yield return EnterRoom("F08_SAFEROUTE");
            yield return Say("F08_SAFEROUTE");

            // F09 (4:10–4:30) sonuç
            EnterStep("F09_RESULT");
            yield return ShowResult();
        }

        // F_D2'de yanlış seçilirse çocuk suya GİRMEDEN durur, riskli bölge gösterilir, karar tekrar sorulur.
        private IEnumerator ShowRiskAfterWrongChoice(ChoiceData choice)
        {
            if (choice.correct) yield break;

            yield return EnterRoom("F07_RISK");
            yield return Say("F07_RISK_WARNING");
            yield return EnterRoom("F06_RUMORS");
        }
    }
}
