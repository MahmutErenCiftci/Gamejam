using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Çök–Kapan–Tutun mini oyunu (sarsıntı sırasında). Run(oyuncuKonumu):
    ///  • Oyuncu bir SturdyTable'ın yarıçapındaysa masanın altında: ÇÖK (E) → Cenk masanın altına kayar → KAPAN (E) → TUTUN (E basılı, holdSeconds).
    ///  • Değilse olduğu yerde aynı adımlar (Yerinde Ç-K; masaya koşma yok, ödüllendirilmez).
    ///  • TUTUN'da E bırakılırsa uyarı ("Sarsıntı bitene kadar tutun!"), ilerleme durur, yeniden basınca devam eder (tuş ezme yok).
    ///  • Bir adımda noResponseHintAfter sn tepki yoksa ipucu. maxSeconds içinde bitmezse: en az KAPAN yapıldıysa sonuç sayılır
    ///    (tutunma eksik notuyla), yoksa "Korunma yapılmadı" (None).
    /// Pozlar IsoCharacterAnimator.ShowPose ile (sprite boşsa poz değişmez). Sonuç: LastResult / onDone.
    /// </summary>
    public class ProtectionMinigameController : MonoBehaviour
    {
        /// <summary>Korunma sonucu.</summary>
        public class Result
        {
            public ProtectionOutcome outcome = ProtectionOutcome.None;
            public SturdyTable table;
            public int completedSteps;
            public bool holdCompleted;
            public bool releasedDuringHold;
            public float seconds;
        }

        /// <summary>Otomatik test girdisi.</summary>
        public enum AutoInput { Manual, Respond, RespondWithRelease, NoInput }

        [Tooltip("ÇÖK / KAPAN / TUTUN göstergesi (UI agent'ının ProtectionView'ı)")]
        [SerializeField] private ProtectionViewBase view;
        [Tooltip("Oyuncu (pozlar ve masa altına geçiş için)")]
        [SerializeField] private IsoPlayerController player;
        [Tooltip("Boşsa sahnedeki bütün SturdyTable'lar")]
        [SerializeField] private SturdyTable[] tables = Array.Empty<SturdyTable>();
        [Tooltip("TUTUN için E'nin basılı tutulacağı toplam süre (sn)")]
        [SerializeField] private float holdSeconds = 6f;
        [Tooltip("Bir adımda bu kadar sn tepki yoksa ipucu gösterilir")]
        [SerializeField] private float noResponseHintAfter = 3f;
        [Tooltip("Sarsıntının en uzun süresi (sn); bu sürede bitmezse korunma eksik/yapılmadı sayılır")]
        [SerializeField] private float maxSeconds = 16f;
        [Tooltip("Masanın altına kayma süresi (sn)")]
        [SerializeField] private float slideSeconds = 0.25f;

        [Header("Pozlar (isteğe bağlı sprite'lar)")]
        [SerializeField] private Sprite dropPose;
        [SerializeField] private Sprite coverPose;
        [SerializeField] private Sprite holdPose;
        [Tooltip("Masa altında çökme pozu (boşsa dropPose)")]
        [SerializeField] private Sprite tableDropPose;
        [Tooltip("Masa altında kapanma pozu (boşsa coverPose)")]
        [SerializeField] private Sprite tableCoverPose;
        [Tooltip("Masa altında tutunma pozu (boşsa holdPose)")]
        [SerializeField] private Sprite tableHoldPose;

        [Header("Kancalar")]
        [SerializeField] private UnityEvent onStepCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onReleaseWarning = new UnityEvent();

        private IsoCharacterAnimator characterAnimator;

        public bool IsRunning { get; private set; }
        public Result LastResult { get; private set; }

        /// <summary>Otomatik test girdisi (Manual = gerçek tuşlar).</summary>
        public AutoInput Auto { get; set; } = AutoInput.Manual;
        /// <summary>Otomatik testte adımlar arası bekleme (sn).</summary>
        public float AutoStepDelay { get; set; } = 0.6f;
        /// <summary>Metinler (mod kontrolcüsü JSON'dan verir).</summary>
        public EqProtectionTexts Texts { get; set; }

        public ProtectionViewBase View { get => view; set => view = value; }
        public IsoPlayerController Player { get => player; set { player = value; characterAnimator = null; } }
        public float HoldSeconds => holdSeconds;

        private void Awake()
        {
            if (tables == null || tables.Length == 0)
                tables = FindObjectsByType<SturdyTable>(FindObjectsInactive.Exclude);
        }

        /// <summary>Konuma en yakın, yarıçapı içindeki sağlam masa (yoksa null).</summary>
        public SturdyTable FindTable(Vector3 position)
        {
            SturdyTable best = null;
            float bestD = float.MaxValue;
            foreach (var t in tables)
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                float d = t.GroundDistance(position);
                if (d <= t.Radius && d < bestD) { best = t; bestD = d; }
            }
            return best;
        }

        public SturdyTable[] Tables => tables;

        /// <summary>Korunmayı oynatır; bitince onDone(Result). Sarsıntıyı mod kontrolcüsü başlatıp bitirir.</summary>
        public IEnumerator Run(Vector3 playerPos, Action<Result> onDone = null)
        {
            IsRunning = true;
            var texts = Texts ?? EarthquakeModeData.CreateDefault().protection;
            var result = new Result();
            var table = FindTable(playerPos);
            result.table = table;
            bool underTable = table != null;
            Debug.Log(underTable ? $"[EQ] korunma: masa altında ({table.name})" : "[EQ] korunma: masa uzakta → olduğun yerde", this);

            string[] steps = texts.steps != null && texts.steps.Length >= 3 ? texts.steps : new[] { "ÇÖK", "KAPAN", "TUTUN" };
            string promptDrop = underTable ? texts.promptDrop : Or(texts.promptDropInPlace, texts.promptDrop);
            string promptCover = underTable ? texts.promptCover : Or(texts.promptCoverInPlace, texts.promptCover);
            string promptHold = texts.promptHold;

            float start = Time.time;
            float Elapsed() => Time.time - start;

            // 1) ÇÖK
            bool ok = false;
            yield return WaitPress(steps, 0, promptDrop, texts.noResponseHint, Elapsed, r => ok = r);
            if (ok)
            {
                result.completedSteps = 1;
                if (onStepCompleted != null) onStepCompleted.Invoke();
                Debug.Log("[EQ] ÇÖK", this);
                if (underTable) yield return SlideUnder(table);
                Pose(underTable ? Or(tableDropPose, dropPose) : dropPose);

                // 2) KAPAN
                yield return WaitPress(steps, 1, promptCover, texts.noResponseHint, Elapsed, r => ok = r);
                if (ok)
                {
                    result.completedSteps = 2;
                    if (onStepCompleted != null) onStepCompleted.Invoke();
                    Debug.Log("[EQ] KAPAN", this);
                    Pose(underTable ? Or(tableCoverPose, coverPose) : coverPose);

                    // 3) TUTUN (basılı tut)
                    yield return Hold(steps, promptHold, texts, Elapsed, result);
                    if (result.holdCompleted)
                    {
                        result.completedSteps = 3;
                        if (onStepCompleted != null) onStepCompleted.Invoke();
                        Debug.Log("[EQ] TUTUN tamam", this);
                    }
                }
            }

            if (view != null) view.Hide();

            if (result.completedSteps >= 2)
                result.outcome = underTable ? ProtectionOutcome.TableDropCoverHold : ProtectionOutcome.InPlaceDropCover;
            else
                result.outcome = ProtectionOutcome.None;
            result.seconds = Elapsed();
            Debug.Log($"[EQ] korunma sonucu: {result.outcome} (adım {result.completedSteps}/3, bırakma: {result.releasedDuringHold}, {result.seconds:0.0} sn)", this);

            IsRunning = false;
            LastResult = result;
            onDone?.Invoke(result);
        }

        /// <summary>Pozu kaldırır (sarsıntı sonrası mod kontrolcüsü çağırır).</summary>
        public void ClearPose()
        {
            var anim = Animator();
            if (anim != null) anim.ClearPose();
        }

        /// <summary>
        /// Sarsıntı sonrası: pozu kaldırır; masanın altına geçildiyse oyuncuyu eski yerine (masanın önü) koyar ve
        /// masa altındayken kapatılan çarpışmayı geri açar.
        /// </summary>
        public void RestoreAfter()
        {
            ClearPose();
            if (!slidUnder || player == null) return;
            slidUnder = false;
            player.PlaceAt(preSlidePosition, new Vector2(0f, -1f));
            foreach (var c in disabledColliders)
                if (c != null) c.enabled = true;
            disabledColliders = Array.Empty<Collider2D>();
        }

        private bool slidUnder;
        private Vector3 preSlidePosition;
        private Collider2D[] disabledColliders = Array.Empty<Collider2D>();

        private IEnumerator WaitPress(string[] steps, int step, string prompt, string hint, Func<float> elapsed, Action<bool> done)
        {
            Show(steps, step, prompt);
            yield return null; // önceki adımın tuşu bu adımı geçirmesin
            float stepStart = Time.time;
            bool hinted = false;
            while (elapsed() < maxSeconds)
            {
                if (Pressed(stepStart)) { done(true); yield break; }
                if (!hinted && Time.time - stepStart >= noResponseHintAfter)
                {
                    hinted = true;
                    Warn(hint);
                }
                yield return null;
            }
            done(false);
        }

        private IEnumerator Hold(string[] steps, string prompt, EqProtectionTexts texts, Func<float> elapsed, Result result)
        {
            Show(steps, 2, prompt);
            float held = 0f;
            bool wasHolding = false;
            bool warnedThisRelease = false;
            bool hinted = false;
            float stepStart = Time.time;
            bool posed = false;
            while (held < holdSeconds && elapsed() < maxSeconds + holdSeconds)
            {
                bool holding = Held(stepStart, held);
                if (holding)
                {
                    if (!posed)
                    {
                        posed = true;
                        Pose(result.table != null ? Or(tableHoldPose, holdPose) : holdPose);
                    }
                    held += Time.deltaTime;
                    warnedThisRelease = false;
                    if (view != null) view.SetHoldProgress(Mathf.Clamp01(held / holdSeconds));
                }
                else
                {
                    // KAPAN için kısa basılan E bu adıma taşabilir: 0.3 sn'den kısa bir tutuş "bıraktın" sayılmaz.
                    if (wasHolding && held < 0.3f)
                    {
                        held = 0f;
                        if (view != null) view.SetHoldProgress(0f);
                    }
                    else if (wasHolding && !warnedThisRelease)
                    {
                        warnedThisRelease = true;
                        result.releasedDuringHold = true;
                        Debug.Log("[EQ] TUTUN bırakıldı → uyarı", this);
                        Warn(texts.releaseWarning);
                        if (onReleaseWarning != null) onReleaseWarning.Invoke();
                    }
                    else if (held <= 0f && !hinted && Time.time - stepStart >= noResponseHintAfter)
                    {
                        hinted = true;
                        Warn(texts.noResponseHint);
                    }
                    // Sarsıntının en uzun süresi geçti ve hiç tutulmadıysa bırak.
                    if (held <= 0f && elapsed() >= maxSeconds) break;
                }
                wasHolding = holding;
                yield return null;
            }
            result.holdCompleted = held >= holdSeconds;
        }

        // --- Girdi (gerçek ya da otomatik) ---

        private bool Pressed(float stepStart)
        {
            switch (Auto)
            {
                case AutoInput.Manual: return GameInput.InteractPressed;
                case AutoInput.NoInput: return false;
                default: return Time.time - stepStart >= AutoStepDelay;
            }
        }

        private bool Held(float stepStart, float held)
        {
            switch (Auto)
            {
                case AutoInput.Manual: return GameInput.InteractHeld;
                case AutoInput.NoInput: return false;
                case AutoInput.RespondWithRelease:
                    // Yarıda 1 sn bırakır (uyarı testi), sonra devam eder.
                    float since = Time.time - stepStart - AutoStepDelay;
                    if (since < 0f) return false;
                    float half = holdSeconds * 0.5f;
                    return !(since >= half && since < half + 1f);
                default: return Time.time - stepStart >= AutoStepDelay;
            }
        }

        // --- Görünüm (yoksa Console) ---

        private void Show(string[] steps, int step, string prompt)
        {
            if (view != null)
            {
                view.Show(steps, step, prompt);
                view.SetHoldProgress(0f);
            }
            else Debug.Log($"[EQ] korunma adımı {step + 1}/3 {steps[Mathf.Clamp(step, 0, steps.Length - 1)]}: {prompt}", this);
        }

        private void Warn(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (view != null) view.Warn(text);
            else Debug.Log($"[EQ] korunma uyarısı: {text}", this);
        }

        private IEnumerator SlideUnder(SturdyTable table)
        {
            if (player == null || table == null) yield break;
            Vector3 from = player.transform.position;
            // Masanın altında masa collider'ı oyuncuyu dışarı itmesin: geçici olarak oyuncunun (tetik olmayan) collider'ları kapanır.
            preSlidePosition = from;
            slidUnder = true;
            var list = new System.Collections.Generic.List<Collider2D>();
            foreach (var c in player.GetComponents<Collider2D>())
                if (c.enabled && !c.isTrigger) { c.enabled = false; list.Add(c); }
            disabledColliders = list.ToArray();
            Vector3 to = table.HidePosition;
            for (float t = 0f; t < slideSeconds; t += Time.deltaTime)
            {
                player.PlaceAt(Vector3.Lerp(from, to, t / Mathf.Max(0.01f, slideSeconds)), table.HideFacing);
                yield return null;
            }
            player.PlaceAt(to, table.HideFacing);
        }

        private void Pose(Sprite sprite)
        {
            if (sprite == null) return;
            var anim = Animator();
            if (anim != null) anim.ShowPose(sprite);
        }

        private IsoCharacterAnimator Animator()
        {
            if (characterAnimator == null && player != null) characterAnimator = player.GetComponent<IsoCharacterAnimator>();
            return characterAnimator;
        }

        private static string Or(string a, string b) => string.IsNullOrEmpty(a) ? b : a;
        private static Sprite Or(Sprite a, Sprite b) => a != null ? a : b;
    }
}
