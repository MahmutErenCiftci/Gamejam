using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>İncelenebilir nesnenin/kartın türü (kart rengi ve rapor sayımı için).</summary>
    public enum HazardClass { Info, Hazard, Protective, Safety, Exit }

    /// <summary>Sarsıntı sırasındaki korunma sonucu.</summary>
    public enum ProtectionOutcome { None, TableDropCoverHold, InPlaceDropCover, Exposed }

    /// <summary>Sağ üst 120 sn sayaç + ceza animasyonu. 120–61 beyaz, 60–31 amber, 30–11 kırmızı, ≤10 hafif nabız.</summary>
    public abstract class CountdownViewBase : MonoBehaviour
    {
        /// <summary>Sayacı göster/gizle.</summary>
        public abstract void SetVisible(bool v);
        /// <summary>Kalan süreyi yaz (asla negatif gelmez).</summary>
        public abstract void SetTime(float secondsRemaining);
        /// <summary>"-10 SANİYE" uçan yazı + hafif sarsılma.</summary>
        public abstract void ShowPenalty(int seconds, string reason);
        /// <summary>Durum yazısı (ör. "SARSINTI!"); null/boş = süreyi göster.</summary>
        public abstract void SetStatusText(string text);
    }

    /// <summary>Telefon bildirimi ekranı: kayarak gelir, BAŞLA bekler (E/Enter/Space/fare), kapanır.</summary>
    public abstract class PhoneAlertViewBase : MonoBehaviour
    {
        public abstract IEnumerator Show(string app, string title, string body, string note, string button);
    }

    /// <summary>İnceleme kartı (engelleyen) ve uyarı toast'ı (engellemeyen).</summary>
    public abstract class InfoCardViewBase : MonoBehaviour
    {
        /// <summary>Kartı gösterir; E/Space/fare ile kapanınca biter.</summary>
        public abstract IEnumerator ShowCard(string title, string body, HazardClass kind);
        /// <summary>Engellemeyen uyarı (ör. "CAM TEHLİKESİ!").</summary>
        public abstract void ShowToast(string title, string body, float seconds);
    }

    /// <summary>Anne portresi, "ANNE", soru, 3–4 şık (1–4 tuşları + fare), 5 sn çubuğu.</summary>
    public abstract class TimedQuestionViewBase : MonoBehaviour
    {
        public abstract void Open(string speaker, Sprite portrait, string question, string[] options, float timeLimit);
        public abstract void SetTimeLeft(float seconds);
        /// <summary>Oyuncu seçtiyse true döner (her seçim bir kez).</summary>
        public abstract bool TryGetChoice(out int index);
        /// <summary>"SÜRE DOLDU!" / "DOĞRU!" / "YANLIŞ" + açıklama; E/Space ya da 2.5 sn ile biter.</summary>
        public abstract IEnumerator ShowFeedback(bool correct, bool timedOut, string text, int penaltySeconds);
        public abstract void Close();
    }

    /// <summary>ÇÖK / KAPAN / TUTUN adım göstergesi + basılı tutma çubuğu.</summary>
    public abstract class ProtectionViewBase : MonoBehaviour
    {
        public abstract void Show(string[] steps, int currentStep, string prompt);
        public abstract void SetHoldProgress(float t01);
        public abstract void Warn(string text);
        public abstract void Hide();
    }

    /// <summary>"AFET FARKINDALIK RAPORU" ekranı. Butona basınca biter; report.choice = "Retry" / "Menu".</summary>
    public abstract class ResultReportViewBase : MonoBehaviour
    {
        public abstract IEnumerator Show(EarthquakeReport report);
    }

    /// <summary>Sonuç raporu verisi (EarthquakeResultManager doldurur, ResultReportViewBase gösterir).</summary>
    [System.Serializable]
    public class EarthquakeReport
    {
        public string title, label;
        public int correctAnswers, wrongAnswers, timeouts, hazardsFound, hazardsTotal, exposures;
        public ProtectionOutcome protection;
        public bool bagTaken, elevatorTried, acceleratedReady;
        public float remainingAtQuake;
        public List<string> mistakes = new();
        public List<string> tips = new();
        public int score;
        public string choice;

        // --- Ek alanlar (sözleşmeye eklendi, yeniden adlandırma yok) ---
        /// <summary>Toplam doğru karar (doğru cevap + doğru korunma + çanta + merdiven vb.).</summary>
        public int correctDecisions;
        /// <summary>Toplam yanlış karar (yanlış/süre dolu cevap + asansör + korunmama vb.).</summary>
        public int wrongDecisions;
        /// <summary>İncelenen nesne sayısı (tüm türler).</summary>
        public int inspectedCount;
        /// <summary>Toplam süre cezası (sn).</summary>
        public int totalPenaltySeconds;
        /// <summary>Korunma sonucunun okunur metni ("Masa altında Ç-K-T" vb.).</summary>
        public string protectionText;
        /// <summary>Kalan süre metni ("01:12" veya "Hazırlık tamam").</summary>
        public string remainingText;
        /// <summary>Etikete göre kısa mesaj (results.labelMessages).</summary>
        public string labelMessage;
        /// <summary>Alt not ("Bu bir farkındalık simülasyonudur...").</summary>
        public string note;
    }
}
