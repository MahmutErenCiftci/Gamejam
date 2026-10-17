using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AfetUyanisi.Dialogue;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Eren'in karar menüsünü (DialogueRunner.ShowDecision) DecisionRunnerBase sözleşmesine bağlar.
    /// Eren'in menüsü tek seferliktir (seçim → doğru cevap yeşil → devam); bu adaptör OYUN_AKISI kuralını uygular:
    /// yanlış seçimde ya da süre dolunca karar tekrar sorulur, gerekli sayıda doğru bulununca biter.
    /// </summary>
    public class DecisionRunnerAdapter : DecisionRunnerBase
    {
        [SerializeField] private DialogueRunner runner;

        public override IEnumerator Run(string decisionId, DecisionResult result,
            Func<ChoiceData, IEnumerator> afterEachChoice = null)
        {
            var decision = runner == null || runner.Data?.decisions == null
                ? null
                : Array.Find(runner.Data.decisions, d => d.id == decisionId);
            if (decision?.choices == null)
            {
                Debug.LogWarning($"[DecisionRunnerAdapter] '{decisionId}' kararı bulunamadı (runner ya da veri eksik), atlandı.");
                yield break;
            }

            int correctAvailable = decision.choices.Count(c => c.correct);
            int required = Mathf.Clamp(result.RequiredCorrect, 1, Mathf.Max(1, correctAvailable));
            var found = new HashSet<string>();

            while (found.Count < required)
            {
                while (runner.IsBusy) yield return null;

                bool done = false;
                StoryChoice picked = null;
                runner.ShowDecision(decisionId, c => { picked = c; done = true; });
                while (!done) yield return null;

                if (picked == null)
                {
                    result.TimeoutCount++; // süre doldu: geri bildirim gösterildi, tekrar sor
                    continue;
                }

                var choice = ToChoiceData(decision, picked);
                if (picked.correct)
                {
                    if (found.Add(picked.id))
                    {
                        result.CorrectChoices.Add(choice);
                        result.FinalChoice = choice;
                    }
                }
                else
                {
                    result.WrongChoices.Add(choice);
                }

                if (afterEachChoice != null)
                    yield return afterEachChoice(choice);
            }
        }

        private static ChoiceData ToChoiceData(StoryDecision decision, StoryChoice picked) => new ChoiceData
        {
            id = picked.id,
            key = Array.IndexOf(decision.choices, picked) + 1,
            text = picked.text,
            correct = picked.correct,
            feedback = picked.feedback
        };
    }
}
