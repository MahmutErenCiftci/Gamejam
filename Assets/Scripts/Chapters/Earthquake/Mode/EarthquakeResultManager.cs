using System.Collections;
using System.Text;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// "AFET FARKINDALIK RAPORU": EarthquakeStats'tan puan (0–100), etiket ve yanlış açıklamalarını üretir ve
    /// ResultReportViewBase ile gösterir. Puan ağırlıkları Inspector'dan; çoğu doğru kararlardan ve tehlike tanımadan gelir,
    /// süre (ceza kaybı) küçük bir kısımdır. Etiketler JSON results.labels (85/65/40/0).
    /// Görünüm yoksa rapor Console'a yazılır; oyuncu E'ye basınca "Retry" seçilir (otomatik testte seçim yapılmaz).
    /// </summary>
    public class EarthquakeResultManager : MonoBehaviour
    {
        [Tooltip("Rapor ekranı (UI agent'ının ResultReportView'ı)")]
        [SerializeField] private ResultReportViewBase view;

        [Header("Puan ağırlıkları (toplam ≈ 100)")]
        [Tooltip("Bütün sorular doğruysa (oransal)")]
        [SerializeField] private int answersWeight = 40;
        [Tooltip("Masa altında Ç-K-T")]
        [SerializeField] private int tableProtection = 25;
        [Tooltip("Yerinde Ç-K")]
        [SerializeField] private int inPlaceProtection = 18;
        [Tooltip("Yerinde Ç-K ama cam/devrilecek eşya yakınında")]
        [SerializeField] private int exposedProtection = 10;
        [Tooltip("Tutunma yarıda kaldıysa korunma puanından düşülen")]
        [SerializeField] private int incompleteHoldMinus = 5;
        [Tooltip("Bütün tehlikeler incelendiyse (oransal)")]
        [SerializeField] private int hazardsWeight = 15;
        [Tooltip("Deprem çantası alındı")]
        [SerializeField] private int bagPoints = 5;
        [Tooltip("Asansör denenmedi, merdiven kullanıldı")]
        [SerializeField] private int noElevatorPoints = 5;
        [Tooltip("Süre: hiç süre cezası yoksa tam puan, cezalar arttıkça azalır")]
        [SerializeField] private int timeWeight = 10;
        [Tooltip("Süre puanının sıfırlandığı toplam ceza (sn)")]
        [SerializeField] private int timePenaltyForZero = 60;
        [Tooltip("Her gereksiz maruziyet için eksi")]
        [SerializeField] private int exposureMinus = 5;
        [Tooltip("Maruziyet eksisinin üst sınırı")]
        [SerializeField] private int exposureMinusMax = 15;
        [Tooltip("Bu orandan az tehlike incelendiyse 'fewHazards' ipucu eklenir")]
        [SerializeField, Range(0f, 1f)] private float fewHazardsRatio = 0.5f;
        [Tooltip("Rapordaki 'Öğrenilecekler' listesinin en fazla madde sayısı (konu başına bir kısa madde)")]
        [SerializeField, Range(1, 8)] private int maxLessons = 5;
        [Tooltip("Sabitlenmemiş kitaplık devrildiyse (oyuncu uzaktaydı) eksi")]
        [SerializeField] private int fallenShelfMinus = 5;
        [Tooltip("Kitaplık oyuncunun üstüne devrildiyse puanın üst sınırı (bölüm başarısız)")]
        [SerializeField] private int injuredScoreCap = 15;

        public ResultReportViewBase View { get => view; set => view = value; }

        /// <summary>true ise görünüm çağrılmaz (otomatik test).</summary>
        public bool BypassBlockingViews { get; set; }

        /// <summary>Raporu hesaplar.</summary>
        public EarthquakeReport Build(EarthquakeStats s, EarthquakeModeData data)
        {
            data = data ?? EarthquakeModeData.CreateDefault();
            var res = data.results;
            var r = new EarthquakeReport
            {
                title = res.title,
                correctAnswers = s.CorrectAnswers,
                wrongAnswers = s.WrongAnswers,
                timeouts = s.Timeouts,
                hazardsFound = s.HazardsFound,
                hazardsTotal = s.hazardsTotal,
                exposures = s.Exposures,
                protection = s.protection,
                bagTaken = s.bagTaken,
                emergencyBagMinigame = s.emergencyBagMinigame,
                emergencyBagPrepared = s.emergencyBagPrepared,
                elevatorTried = s.elevatorTried,
                acceleratedReady = s.acceleratedReady,
                remainingAtQuake = s.acceleratedReady ? s.remainingAtAccelerate : s.remainingAtQuake,
                inspectedCount = s.InspectedCount,
                totalPenaltySeconds = s.penaltySeconds,
                note = res.note
            };
            r.prepDone = s.prepTasksDone.Count;
            r.prepTotal = s.prepTasksTotal;
            r.prepMissed.AddRange(s.prepTasksMissedLabels);

            // --- Puan ---
            float score = 0f;
            int qTotal = s.QuestionsCounted; // sayaç bittiği için sorulamayan sorular puandan düşmez
            score += answersWeight * (float)s.CorrectAnswers / qTotal;
            switch (s.protection)
            {
                case ProtectionOutcome.TableDropCoverHold: score += tableProtection; break;
                // Masadan uzaktayken yerinde güvenli biçimde korunmak daha az doğru değildir.
                case ProtectionOutcome.InPlaceDropCover: score += Mathf.Max(inPlaceProtection, tableProtection); break;
                case ProtectionOutcome.Exposed: score += exposedProtection; break;
            }
            if (s.protection != ProtectionOutcome.None && s.protectionIncomplete) score -= incompleteHoldMinus;
            if (s.hazardsTotal > 0) score += hazardsWeight * Mathf.Clamp01((float)s.HazardsFound / s.hazardsTotal);
            if (s.bagTaken) score += bagPoints;
            if (!s.elevatorTried && s.usedStairs) score += noElevatorPoints;
            score += timeWeight * (1f - Mathf.Clamp01((float)s.penaltySeconds / Mathf.Max(1, timePenaltyForZero)));
            score -= Mathf.Min(exposureMinusMax, exposureMinus * s.Exposures);
            if (s.bookshelfFell && !s.hitByBookshelf) score -= fallenShelfMinus;
            r.score = Mathf.Clamp(Mathf.RoundToInt(score), 0, 100);
            r.label = res.LabelFor(r.score);
            r.labelMessage = res.MessageFor(r.score);
            if (s.hitByBookshelf)
            {
                // Kitaplık üstüne devrildi: bölüm başarısız sayılır (puan tavanı), oyuncu tekrar denemeye yönlendirilir.
                r.score = Mathf.Min(r.score, injuredScoreCap);
                r.label = "YARALANDIN";
                r.labelMessage = "Sabitlenmemiş kitaplık üstüne devrildi. Tekrar dene: önce sabitle, sarsıntıda uzak dur.";
            }

            // --- Kararlar ---
            int correct = s.CorrectAnswers;
            int wrong = s.WrongAnswers + s.Timeouts;
            if (s.protection == ProtectionOutcome.TableDropCoverHold || s.protection == ProtectionOutcome.InPlaceDropCover) correct++;
            else wrong++;
            if (s.bagTaken) correct++; else wrong++;
            if (s.bookshelfSecured) correct++; else wrong++;
            if (s.elevatorTried) wrong++; else if (s.usedStairs) correct++;
            wrong += s.Exposures;
            r.correctDecisions = correct;
            r.wrongDecisions = wrong;

            // --- Metinler ---
            r.protectionText = ProtectionText(s.protection);
            r.remainingText = s.acceleratedReady ? res.readyText : FormatTime(r.remainingAtQuake);

            BuildLessons(r, s, data);
            if (s.protectionBalancePlayed)
                r.tips.Add(string.Format((data.protection.balance ?? new EqBalanceTexts()).score, s.protectionBalanceScore));
            if (r.prepTotal > 0 && r.prepMissed.Count > 0)
            {
                int shownMissing = Mathf.Min(3, r.prepMissed.Count);
                string missing = string.Join(", ", r.prepMissed.GetRange(0, shownMissing)) + (r.prepMissed.Count > shownMissing ? "…" : "");
                r.tips.Add($"Eksik hazırlık ({r.prepDone}/{r.prepTotal}): {missing}.");
            }
            return r;
        }

        /// <summary>Raporu gösterir; bitince report.choice dolu olur ("Retry"/"Menu"; otomatik testte boş kalabilir).</summary>
        public IEnumerator Show(EarthquakeReport report, bool autoPlay)
        {
            Debug.Log(Describe(report), this);
            if (view != null && !BypassBlockingViews)
            {
                yield return view.Show(report);
                yield break;
            }
            if (autoPlay || BypassBlockingViews)
            {
                yield return null;
                yield break;
            }
            // Görünüm yok: E/Space/Enter ile tekrar dene (Esc menü).
            yield return null;
            while (true)
            {
                if (GameInput.ContinuePressed) { report.choice = "Retry"; break; }
                if (GameInput.BackPressed) { report.choice = "Menu"; break; }
                yield return null;
            }
        }

        /// <summary>Raporun Console metni.</summary>
        public static string Describe(EarthquakeReport r)
        {
            var sb = new StringBuilder();
            sb.Append($"[EQ] RAPOR {r.title}: PUAN {r.score} → {r.label}");
            if (!string.IsNullOrEmpty(r.labelMessage)) sb.Append($" ({r.labelMessage})");
            sb.Append($"\n  doğru cevap {r.correctAnswers}, yanlış {r.wrongAnswers}, süre doldu {r.timeouts} · doğru karar {r.correctDecisions}, yanlış karar {r.wrongDecisions}");
            sb.Append($"\n  tehlike {r.hazardsFound}/{r.hazardsTotal}, incelenen {r.inspectedCount}, maruziyet {r.exposures}, korunma {r.protection} ({r.protectionText})");
            if (r.prepTotal > 0) sb.Append($"\n  hazırlık görevleri {r.prepDone}/{r.prepTotal}" + (r.prepMissed.Count > 0 ? " (eksik: " + string.Join(", ", r.prepMissed) + ")" : ""));
            sb.Append($"\n  çanta {(r.emergencyBagMinigame ? (r.emergencyBagPrepared ? "hazırlandı, " : "hazırlanmadı, ") : "")}{(r.bagTaken ? "alındı" : "alınmadı")}, asansör {(r.elevatorTried ? "denendi" : "denenmedi")}, kalan süre {r.remainingText}, ceza toplamı {r.totalPenaltySeconds} sn");
            foreach (var m in r.mistakes) sb.Append($"\n  ✗ {m}");
            foreach (var t in r.tips) sb.Append($"\n  ipucu: {t}");
            return sb.ToString();
        }

        public static string ProtectionText(ProtectionOutcome p)
        {
            switch (p)
            {
                case ProtectionOutcome.TableDropCoverHold: return "Masa altında Ç-K-T";
                case ProtectionOutcome.InPlaceDropCover: return "Yerinde Ç-K";
                case ProtectionOutcome.Exposed: return "Yerinde Ç-K (tehlikeye yakın)";
                default: return "Yapılmadı";
            }
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        /// <summary>
        /// "Öğrenilecekler": her yanlış davranış bir KONUya bağlanır (protect, glass, elevator, after...), her konu raporda
        /// tek kısa madde olur ("Konu: doğru davranış"), en fazla maxLessons madde. Önem sırası: korunma, sorular,
        /// maruziyetler, asansör, çanta, hazırlık. (Uzun açıklamalar soru geri bildiriminde gösterilir.)
        /// </summary>
        private void BuildLessons(EarthquakeReport r, EarthquakeStats s, EarthquakeModeData data)
        {
            var lessons = data.results.lessons ?? EqLessons.CreateDefault();
            var fallback = EqLessons.CreateDefault();
            var seen = new System.Collections.Generic.HashSet<string>();
            void Add(string topic, string text = null)
            {
                if (r.mistakes.Count >= maxLessons) return;
                string key = EarthquakeModeData.NormalizeId(string.IsNullOrEmpty(topic) ? text : topic);
                if (string.IsNullOrEmpty(key) || !seen.Add(key)) return;
                string line = !string.IsNullOrEmpty(text) ? text : lessons.For(topic) ?? fallback.For(topic);
                if (!string.IsNullOrEmpty(line) && !r.mistakes.Contains(line)) r.mistakes.Add(line);
            }

            if (s.hitByBookshelf) Add("bookshelfHit", "Kitaplık üstüne devrildi: uzun mobilyaları depremden ÖNCE duvara sabitle, sarsıntıda yanlarında durma.");
            if (s.protection == ProtectionOutcome.None) Add("protect");
            foreach (var a in s.answers)
            {
                if (a.correct) continue;
                var q = data.FindQuestion(a.questionId);
                string topic = q != null && !string.IsNullOrEmpty(q.topic) ? q.topic : a.questionId;
                string text = q != null && !string.IsNullOrEmpty(q.lesson) ? q.lesson : lessons.For(topic);
                if (string.IsNullOrEmpty(text)) text = !string.IsNullOrEmpty(a.correctText) ? a.correctText + "." : null;
                Add(topic, text);
            }
            if (s.protection != ProtectionOutcome.None && s.protectionIncomplete) Add("hold");
            foreach (var key in s.exposureKeys)
            {
                string k = EarthquakeModeData.NormalizeId(key);
                if (k.StartsWith("after")) Add("glassAfter");
                else if (k.Contains("bookshelf") || k.Contains("wardrobe") || k.Contains("cabinet")) Add("furniture");
                else Add("glass");
            }
            if (s.mirrorPenaltyZones.Count > 0) Add("glass");
            if (s.elevatorTried) Add("elevator");
            if (s.emergencyBagMinigame && !s.emergencyBagPrepared) Add("bagPrep", "Afet çantası: depremden ÖNCE hazırla (su, yiyecek, ilk yardım, fener).");
            else if (!s.bagTaken) Add("bag");
            if (!s.bookshelfSecured) Add("bookshelf", "Kitaplık: depremden ÖNCE bir büyüğünle duvara ankraj ve kayışla sabitle.");
            if (s.hazardsTotal > 0 && (float)s.HazardsFound / s.hazardsTotal < fewHazardsRatio) Add("hazards");
            // r.tips boş kalır: kısa maddeler zaten konuyu ve doğru davranışı söylüyor (rapor ekranı en fazla maxLessons madde gösterir).
        }

        /// <summary>Rapordaki seçimi uygular: Retry → RestartCurrentChapter, Menu → ReturnToMenu.</summary>
        public static void ApplyChoice(string choice)
        {
            if (choice == "Retry")
            {
                if (!GameSession.CurrentChapter.HasValue) GameSession.StartChapter(ChapterId.Earthquake);
                GameSceneRouter.RestartCurrentChapter();
            }
            else if (choice == "Menu") GameSceneRouter.ReturnToMenu();
        }
    }
}
