using System.Collections.Generic;

namespace AfetUyanisi.Shared
{
    /// <summary>Bir kararın oynanış sonucu. DecisionRunner doldurur, bölüm akışı okur.</summary>
    public sealed class DecisionResult
    {
        public string DecisionId;

        /// <summary>Kaç farklı doğru seçenek bulununca karar biter (bölüm akışı veriden doldurur; E_D2 = 2).</summary>
        public int RequiredCorrect = 1;

        /// <summary>Kararı bitiren (son bulunan doğru) seçenek.</summary>
        public ChoiceData FinalChoice;

        public readonly List<ChoiceData> CorrectChoices = new List<ChoiceData>();

        /// <summary>Oyuncunun seçtiği yanlışlar; sonuç ekranındaki "yanlış davranışlar" buradan gelir.</summary>
        public readonly List<ChoiceData> WrongChoices = new List<ChoiceData>();

        public int TimeoutCount;
    }
}
