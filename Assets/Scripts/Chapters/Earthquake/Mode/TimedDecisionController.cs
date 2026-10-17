using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Annenin süreli soruları (Q1–Q4). Ask(q): 120 sn sayacı durur (PushPause), TimedQuestionViewBase açılır,
    /// soru sayacı (varsayılan 5 sn) akar. Doğru: ceza yok + olumlu geri bildirim. Yanlış: ceza (vars. 30 sn) + açıklama.
    /// Süre dolu: "SÜRE DOLDU!" + ceza (vars. 30 sn) + doğru cevabın açıklaması. Ceza TimePenaltySystem'den, anahtar "q:id"
    /// (aynı soru iki kez ceza vermez). Sonuç LastResult'ta ve onDone geri çağrısında.
    /// Görünüm yoksa soru Console'a yazılır, 1–4 tuşları yine çalışır, geri bildirim kısa bekleyip geçer (akış kilitlenmez).
    /// </summary>
    public class TimedDecisionController : MonoBehaviour
    {
        /// <summary>Bir sorunun sonucu.</summary>
        public class Result
        {
            public QuestionData question;
            public int chosenIndex = -1;
            public bool correct;
            public bool timedOut;
            public int penaltySeconds;
            public string feedback;
        }

        [Tooltip("Soru paneli (UI agent'ının TimedQuestionView'ı)")]
        [SerializeField] private TimedQuestionViewBase view;
        [Tooltip("Sayaç (soru sırasında durur)")]
        [SerializeField] private EmergencyCountdownManager countdown;
        [Tooltip("Ceza sistemi")]
        [SerializeField] private TimePenaltySystem penalties;
        [Tooltip("Anne portresi")]
        [SerializeField] private Sprite motherPortrait;
        [Tooltip("Endişeli anne portresi (sarsıntı sonrası soru; PORTRAIT_Mother_Worried)")]
        [SerializeField] private Sprite worriedPortrait;
        [Tooltip("Endişeli portreyle sorulan soru id'leri")]
        [SerializeField] private string[] worriedQuestionIds = { "Q4" };
        [Tooltip("Soru verisinde timeLimit yoksa kullanılan süre (sn)")]
        [SerializeField] private float defaultTimeLimit = 5f;
        [Tooltip("Görünüm yokken geri bildirimin ekranda kalma süresi (sn)")]
        [SerializeField] private float fallbackFeedbackSeconds = 1.5f;
        [Tooltip("Soru açılınca (anne sesi kancası)")]
        [SerializeField] private UnityEvent onQuestionOpened = new UnityEvent();
        [Tooltip("Doğru cevapta (olumlu ses kancası)")]
        [SerializeField] private UnityEvent onCorrect = new UnityEvent();
        [Tooltip("Yanlış cevapta / süre dolunca (uyarı sesi kancası)")]
        [SerializeField] private UnityEvent onWrong = new UnityEvent();

        /// <summary>Otomatik test: soruya hangi şıkkın verileceği (-1 = cevap verme, süre dolsun). null ise oyuncu seçer.</summary>
        public Func<QuestionData, int> AutoChooser { get; set; }
        /// <summary>Otomatik test: cevaptan önce beklenecek süre (sn).</summary>
        public float AutoAnswerDelay { get; set; } = 1f;
        /// <summary>true ise ShowFeedback çağrılmaz (Console'a yazılır, kısa beklenir) — otomatik test için.</summary>
        public bool BypassBlockingViews { get; set; }

        /// <summary>Soru açıkken true.</summary>
        public bool IsAsking { get; private set; }
        public Result LastResult { get; private set; }

        /// <summary>Her sorudan sonra.</summary>
        public event Action<Result> Answered;

        public TimedQuestionViewBase View { get => view; set => view = value; }
        public EmergencyCountdownManager Countdown { get => countdown; set => countdown = value; }
        public TimePenaltySystem Penalties { get => penalties; set => penalties = value; }
        public Sprite MotherPortrait { get => motherPortrait; set => motherPortrait = value; }
        public Sprite WorriedPortrait { get => worriedPortrait; set => worriedPortrait = value; }

        /// <summary>Soruya göre portre: worriedQuestionIds içindeyse (ve atanmışsa) endişeli portre, değilse anne portresi.</summary>
        public Sprite PortraitFor(QuestionData q)
        {
            if (q != null && worriedPortrait != null && worriedQuestionIds != null
                && Array.Exists(worriedQuestionIds, id => EarthquakeModeData.SameId(id, q.id)))
                return worriedPortrait;
            return motherPortrait;
        }

        /// <summary>Soruyu sorar ve cevaplanana/süre dolana kadar bekler. Sonuç: LastResult ve onDone.</summary>
        public IEnumerator Ask(QuestionData q, Action<Result> onDone = null)
        {
            var result = new Result { question = q };
            if (q == null || q.options == null || q.options.Length == 0)
            {
                Debug.LogWarning("[EQ] soru verisi boş, atlandı", this);
                LastResult = result;
                onDone?.Invoke(result);
                yield break;
            }

            IsAsking = true;
            object pauseToken = this;
            if (countdown != null) countdown.PushPause(pauseToken);
            try
            {
                float limit = q.timeLimit > 0f ? q.timeLimit : defaultTimeLimit;
                string[] options = q.OptionTexts();
                Debug.Log($"[EQ] SORU {q.id}: {q.question} [{string.Join(" | ", options)}] ({limit:0} sn)", this);
                if (view != null) view.Open(string.IsNullOrEmpty(q.speaker) ? "ANNE" : q.speaker, PortraitFor(q), q.question, options, limit);
                if (onQuestionOpened != null) onQuestionOpened.Invoke();

                yield return null; // soruyu açan tuş bu karede seçim sayılmasın

                float left = limit;
                float elapsed = 0f;
                int choice = -1;
                int autoChoice = AutoChooser != null ? AutoChooser(q) : -2;
                while (left > 0f)
                {
                    left = Mathf.Max(0f, left - Time.deltaTime);
                    elapsed += Time.deltaTime;
                    if (view != null) view.SetTimeLeft(left);

                    if (autoChoice != -2)
                    {
                        if (autoChoice >= 0 && elapsed >= AutoAnswerDelay) { choice = autoChoice; break; }
                    }
                    else if (view != null)
                    {
                        if (view.TryGetChoice(out int picked) && picked >= 0 && picked < options.Length) { choice = picked; break; }
                    }
                    else
                    {
                        int key = GameInput.ChoiceKeyPressed;
                        if (key >= 1 && key <= options.Length) { choice = key - 1; break; }
                    }
                    if (left <= 0f) break;
                    yield return null;
                }

                result.chosenIndex = choice;
                result.timedOut = choice < 0;
                result.correct = !result.timedOut && q.options[choice] != null && q.options[choice].correct;

                int correctIndex = q.CorrectIndex;
                string correctExplanation = correctIndex >= 0 ? q.options[correctIndex].explanation : "";
                int penaltySeconds = 0;
                if (result.correct)
                {
                    result.feedback = !string.IsNullOrEmpty(q.correctFeedback) ? q.correctFeedback : correctExplanation;
                    if (onCorrect != null) onCorrect.Invoke();
                }
                else
                {
                    var type = result.timedOut ? TimePenaltySystem.PenaltyType.Timeout : TimePenaltySystem.PenaltyType.WrongAnswer;
                    int amount = -1;
                    if (penalties != null && penalties.UseQuestionPenaltyFromData && q.penalty >= 0) amount = q.penalty;
                    // Sarsıntıdan sonra (Q4) sayaç bitmiştir: süre cezası anlamsız, yalnızca yanlış karar kaydı.
                    if (countdown != null && !countdown.IsRunning) amount = 0;
                    string reason = result.timedOut ? "SÜRE DOLDU" : "Yanlış cevap";
                    if (penalties != null) penaltySeconds = penalties.Apply(type, "q:" + q.id, reason, amount);
                    else if (countdown != null)
                    {
                        penaltySeconds = amount >= 0 ? amount : 30;
                        if (penaltySeconds > 0) countdown.ApplyTimePenalty(penaltySeconds, reason);
                    }

                    if (result.timedOut)
                        result.feedback = !string.IsNullOrEmpty(q.timeoutFeedback) ? q.timeoutFeedback : correctExplanation;
                    else
                    {
                        string wrong = q.options[choice] != null ? q.options[choice].explanation : "";
                        result.feedback = string.IsNullOrEmpty(wrong) ? correctExplanation : wrong;
                    }
                    if (onWrong != null) onWrong.Invoke();
                }
                result.penaltySeconds = penaltySeconds;

                string verdict = result.correct ? "DOĞRU" : result.timedOut ? "SÜRE DOLDU" : "YANLIŞ";
                Debug.Log($"[EQ] CEVAP {q.id}: {verdict} (şık {choice + 1}) ceza {penaltySeconds} sn — {result.feedback}", this);

                if (view != null && !BypassBlockingViews)
                    yield return view.ShowFeedback(result.correct, result.timedOut, result.feedback, penaltySeconds);
                else
                    yield return new WaitForSeconds(BypassBlockingViews ? 0.3f : fallbackFeedbackSeconds);

                if (view != null) view.Close();
            }
            finally
            {
                if (countdown != null) countdown.PopPause(pauseToken);
                IsAsking = false;
            }

            LastResult = result;
            Answered?.Invoke(result);
            onDone?.Invoke(result);
        }
    }
}
