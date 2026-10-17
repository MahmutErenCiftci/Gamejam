using System;
using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Shared
{
    /// <summary>
    /// Karar oynatıcısı. Arda türetir (Dialogue/DecisionRunner); Mahmut'un bölüm akışı
    /// <c>yield return decisions.Run("F_D2", sonuc, ...);</c> şeklinde çağırır.
    /// Kurallar (docs/OYUN_AKISI.md §6.2, §7.2):
    /// - Seçim 1–5 tuşları (GameInput.ChoiceKeyPressed) veya fareyle yapılır.
    /// - Doğru seçim işaretli kalır; karar <see cref="DecisionData.GetRequiredCorrect"/> kadar doğru bulununca biter.
    /// - Yanlış seçimde geri bildirim gösterilir ve karar tekrar sorulur (cezasız).
    /// - time_limit &gt; 0 ise sayaç çalışır; süre dolarsa timeout_feedback gösterilir, sayaç sıfırlanıp tekrar sorulur.
    /// </summary>
    public abstract class DecisionRunnerBase : MonoBehaviour
    {
        /// <param name="result">Doldurulacak sonuç: doğru/yanlış seçimler, süre aşımı sayısı, kararı bitiren seçenek.</param>
        /// <param name="afterEachChoice">Varsa her seçimin geri bildiriminden sonra beklenir
        /// (ör. Sel'de yanlış seçimden sonra riskli bölge gösterilir), sonra karar devam eder.</param>
        public abstract IEnumerator Run(string decisionId, DecisionResult result,
            Func<ChoiceData, IEnumerator> afterEachChoice = null);
    }
}
