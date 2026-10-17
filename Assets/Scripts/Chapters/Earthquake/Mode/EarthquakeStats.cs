using System;
using System.Collections.Generic;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Bir oynanışın ham kayıtları (EarthquakeModeController doldurur, EarthquakeResultManager rapora çevirir).
    /// Her kayıt bir kez yazılır (anahtar kümeleri), böylece aynı olay rapora iki kez girmez.
    /// </summary>
    [Serializable]
    public class EarthquakeStats
    {
        /// <summary>Bir sorunun sonucu.</summary>
        [Serializable]
        public class AnswerRecord
        {
            public string questionId;
            public string question;
            public int chosenIndex;
            public bool correct;
            public bool timedOut;
            public string correctText;
            public string explanation;
        }

        public List<AnswerRecord> answers = new List<AnswerRecord>();
        public List<string> inspectedIds = new List<string>();
        public List<string> inspectedHazardIds = new List<string>();
        public int hazardsTotal;
        public int questionsTotal = 4;
        /// <summary>Gereksiz tehlike maruziyeti anahtarları (ör. "quake:mirror", "after:glass_after").</summary>
        public List<string> exposureKeys = new List<string>();
        /// <summary>Maruziyetlerin okunur açıklamaları (rapordaki yanlışlar listesine girer).</summary>
        public List<string> exposureTexts = new List<string>();
        public List<string> mirrorPenaltyZones = new List<string>();
        public ProtectionOutcome protection = ProtectionOutcome.None;
        public bool protectionUnderTable;
        public bool protectionHoldReleased;
        public bool protectionIncomplete;
        public bool bagTaken;
        public bool bagInspected;
        /// <summary>Kitaplık mini oyunu: doğru malzeme + iki bağlantı + TEST ET başarılı (deprem ÖNCESİ hazırlık).</summary>
        public bool bookshelfSecured;
        /// <summary>Afet çantası mini oyunu sahnede var (EmergencyBagInteractable).</summary>
        public bool emergencyBagMinigame;
        /// <summary>Afet çantası sarsıntıdan ÖNCE hazırlandı (gerekli eşyalar + fermuar kapandı).</summary>
        public bool emergencyBagPrepared;
        public bool elevatorTried;
        public bool usedStairs;
        public bool acceleratedReady;
        public float remainingAtAccelerate;
        public float remainingAtQuake;
        public int penaltySeconds;
        /// <summary>Sayaç bittiği için hiç sorulamayan hazırlık soruları (ceza ve puan kaybı yok).</summary>
        public List<string> droppedQuestions = new List<string>();
        /// <summary>Diğer yanlış davranış notları (ör. "Asansörü kullanmaya çalıştın").</summary>
        public List<string> extraMistakes = new List<string>();

        // --- Hazırlık görevleri (PreparationQuestManager yazar, sonuç raporu okur) ---
        /// <summary>Hazırlık görevi sayısı (HUD'daki satırlar).</summary>
        public int prepTasksTotal;
        /// <summary>Tamamlanan hazırlık görevlerinin id'leri (ör. emergency_bag, secure_bookshelf).</summary>
        public List<string> prepTasksDone = new List<string>();
        public List<string> prepTasksDoneLabels = new List<string>();
        public List<string> prepTasksMissedLabels = new List<string>();

        public int CorrectAnswers => answers.FindAll(a => a.correct).Count;
        public int WrongAnswers => answers.FindAll(a => !a.correct && !a.timedOut).Count;
        public int Timeouts => answers.FindAll(a => a.timedOut).Count;
        public int InspectedCount => inspectedIds.Count;
        public int HazardsFound => inspectedHazardIds.Count;
        public int Exposures => exposureKeys.Count;

        /// <summary>Puanlamada sayılan soru sayısı (düşürülen sorular hariç, en az 1).</summary>
        public int QuestionsCounted => Math.Max(1, questionsTotal - droppedQuestions.Count);

        public bool WasAsked(string questionId) => answers.Exists(a => a.questionId == questionId);

        /// <summary>İncelemeyi kaydeder; ilk kez inceleniyorsa true.</summary>
        public bool AddInspected(string id, bool isHazard)
        {
            if (string.IsNullOrEmpty(id) || inspectedIds.Contains(id)) return false;
            inspectedIds.Add(id);
            if (isHazard && !inspectedHazardIds.Contains(id)) inspectedHazardIds.Add(id);
            return true;
        }

        /// <summary>Maruziyeti kaydeder; aynı anahtar ikinci kez sayılmaz. İlk kezse true.</summary>
        public bool AddExposure(string key, string text)
        {
            if (string.IsNullOrEmpty(key) || exposureKeys.Contains(key)) return false;
            exposureKeys.Add(key);
            if (!string.IsNullOrEmpty(text)) exposureTexts.Add(text);
            return true;
        }

        public void AddMistake(string text)
        {
            if (!string.IsNullOrEmpty(text) && !extraMistakes.Contains(text)) extraMistakes.Add(text);
        }
    }
}
