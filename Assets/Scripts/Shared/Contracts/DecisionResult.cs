using System.Collections.Generic;

namespace AfetUyanisi.Shared
{
    /// <summary>Bir kararın oynanış sonucu. DecisionRunner doldurur, bölüm akışı okur.</summary>
    public sealed class DecisionResult
    {
        public string DecisionId;

        /// <summary>Kararı bitiren (son bulunan doğru) seçenek.</summary>
        public ChoiceData FinalChoice;

        public readonly List<ChoiceData> CorrectChoices = new List<ChoiceData>();

        /// <summary>Oyuncunun seçtiği yanlışlar; sonuç ekranındaki "yanlış davranışlar" buradan gelir.</summary>
        public readonly List<ChoiceData> WrongChoices = new List<ChoiceData>();

        public int TimeoutCount;
    }
}
