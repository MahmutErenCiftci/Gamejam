using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Çök–Kapan–Tutun mini oyunu (sarsıntı sırasında). Run(oyuncuKonumu):
    ///  • Yalnızca sarsıntı başlangıcında hemen erişilebilir masaya yakınsa altına geçer; uzaktaysa yerinde korunur.
    ///  • Değilse olduğu yerde aynı adımlar (Yerinde Ç-K; masaya koşma yok, ödüllendirilmez).
    ///  • E ile Çök/Kapan, ardından kısa talimat (SPACE'e basınca başlar) ve A/D + Space ile 16 sn denge alıştırması;
    ///    bitişte kısa sonuç ("Sarsıntı bitti — iyi tutundun!"). Space bırakılınca poz bozulmaz.
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
            public bool balancePlayed;
            public int balanceScore;
            public float heldRatio;
        }

        /// <summary>Otomatik test girdisi.</summary>
        public enum AutoInput { Manual, Respond, RespondWithRelease, NoInput }

        [Tooltip("ÇÖK / KAPAN / TUTUN göstergesi (UI agent'ının ProtectionView'ı)")]
        [SerializeField] private ProtectionViewBase view;
        [Tooltip("Oyuncu (pozlar ve masa altına geçiş için)")]
        [SerializeField] private IsoPlayerController player;
        [Tooltip("Boşsa sahnedeki bütün SturdyTable'lar")]
        [SerializeField] private SturdyTable[] tables = Array.Empty<SturdyTable>();
        [Tooltip("Eski sahnelerden gelen minimum tutuş süresi; denge alıştırması toplam süresi aşağıdaki evrelerden gelir.")]
        [SerializeField] private float holdSeconds = 6f;
        [Header("Denge alıştırması (A/D + Space)")]
        [SerializeField] private EarthquakeBalanceSettings balanceSettings = new EarthquakeBalanceSettings();
        [Tooltip("Denge başlamadan talimatın en az görünme süresi (sn). SPACE'e basılınca bu süreden sonra başlar.")]
        [SerializeField, Min(0f)] private float introMinSeconds = 1.2f;
        [Tooltip("Oyuncu SPACE'e hiç basmazsa denge yine de bu süreden sonra başlar (sn).")]
        [SerializeField, Min(0f)] private float introMaxSeconds = 4f;
        [Tooltip("Bitişte sonucun (\"Sarsıntı bitti — iyi tutundun!\") gösterildiği süre (sn).")]
        [SerializeField, Min(0f)] private float resultSeconds = 1.5f;
        [Tooltip("Tutunma görselinin işarete göre en fazla eğilme açısı (derece)")]
        [SerializeField, Range(0f, 10f)] private float braceTilt = 6f;
        [SerializeField, Range(0f, 0.15f)] private float cameraZoom = 0.06f;
        [SerializeField] private EarthquakeEventController quakeEvents;
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
        private Camera focusCamera;
        private UnityEngine.Rendering.Universal.PixelPerfectCamera pixelCamera;
        private bool pixelCameraWasEnabled;
        private float originalZoom;
        private SpriteRenderer originalRenderer, braceRenderer;
        private bool originalRendererEnabled;
        private GameObject braceVisual;

        public bool IsRunning { get; private set; }

        /// <summary>Oyuncu Çök ile masanın altına geçti mi (sarsıntı sonrası RestoreAfter'a kadar).</summary>
        public bool IsUnderTable => slidUnder;
        public Result LastResult { get; private set; }

        /// <summary>Otomatik test girdisi (Manual = gerçek tuşlar).</summary>
        public AutoInput Auto { get; set; } = AutoInput.Manual;
        /// <summary>Otomatik testte adımlar arası bekleme (sn).</summary>
        public float AutoStepDelay { get; set; } = 0.6f;
        /// <summary>Metinler (mod kontrolcüsü JSON'dan verir).</summary>
        public EqProtectionTexts Texts { get; set; }

        public ProtectionViewBase View { get => view; set => view = value; }
        public IsoPlayerController Player { get => player; set { player = value; characterAnimator = null; } }
        public float HoldSeconds => balanceSettings.Duration;
        public EarthquakeBalanceSettings BalanceSettings => balanceSettings;
        public EarthquakeBalanceModel Balance { get; private set; }

        // Denge ayarlarının sürümü. Eski (zor/anlaşılmaz) varsayılanlarla kaydedilmiş ya da editörde bellekte eski
        // değerleri taşıyan bileşenler bir kez yeni varsayılanlara yükseltilir; sonra Inspector ayarı korunur.
        private const int BalanceTuningVersion = 2;
        [SerializeField, HideInInspector] private int balanceTuning;

        private void UpgradeBalanceTuning()
        {
            if (balanceTuning >= BalanceTuningVersion) return;
            balanceTuning = BalanceTuningVersion;
            balanceSettings = new EarthquakeBalanceSettings();
        }

        private void OnValidate() => UpgradeBalanceTuning();

        private void Awake()
        {
            UpgradeBalanceTuning();
            if (quakeEvents == null) quakeEvents = FindAnyObjectByType<EarthquakeEventController>();
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
            if (IsRunning) yield break;
            IsRunning = true;
            Balance = null;
            bool completedNormally = false;
            try
            {
            if (player != null) player.CanMove = false;
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
            Warn((texts.balance ?? new EqBalanceTexts()).warning);
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
            RestoreFeedback();
            LastResult = result;
            completedNormally = true;
            onDone?.Invoke(result);
            }
            finally
            {
                IsRunning = false;
                RestoreFeedback();
                if (!completedNormally) RestoreAfter();
                if (view != null) view.Hide();
            }
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
            RestoreFeedback();
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
            result.balancePlayed = true;
            Balance = new EarthquakeBalanceModel(balanceSettings);
            bool warnedRelease = false;
            Pose(result.table != null ? Or(tableHoldPose, holdPose) : holdPose);
            BeginFeedback();
            if (view != null) view.BeginBalance(balanceSettings, texts.balance ?? new EqBalanceTexts());

            // Kısa talimat: gösterge durur, "işareti yeşilde tut / SPACE'e bas ve basılı tut" görünür.
            // SPACE'e basılınca (en az introMinSeconds sonra) ya da en geç introMaxSeconds sonra denge başlar.
            float introStart = Time.time;
            if (quakeEvents != null) quakeEvents.SetIntensity(0.35f);
            while (true)
            {
                float t = Time.time - introStart;
                if (t >= introMaxSeconds || (t >= introMinSeconds && Held(introStart, 0f))) break;
                if (view != null) view.SetBalance(Balance);
                StepFeedback();
                yield return null;
            }

            float stepStart = Time.time;
            while (!Balance.Finished)
            {
                bool holding = Held(stepStart, Balance.Elapsed * Balance.HeldRatio);
                float correction = Auto == AutoInput.Manual ? GameInput.MoveX : Auto == AutoInput.NoInput ? 0 : Balance.AutoCorrection;
                Balance.Tick(Time.deltaTime, correction, holding);
                if (!holding && Balance.ReleasedSeconds > balanceSettings.releaseGrace)
                {
                    result.releasedDuringHold = true;
                    if (!warnedRelease)
                    {
                        warnedRelease = true;
                        if (onReleaseWarning != null) onReleaseWarning.Invoke();
                    }
                }
                else if (holding) warnedRelease = false;
                if (view != null)
                {
                    view.SetHoldProgress(Balance.Progress);
                    view.SetBalance(Balance);
                }
                if (quakeEvents != null) quakeEvents.SetIntensity(Balance.Intensity);
                StepFeedback();
                yield return null;
            }
            result.balanceScore = Mathf.RoundToInt(Balance.Score);
            result.heldRatio = Balance.HeldRatio;
            result.holdCompleted = Balance.HeldRatio >= EarthquakeBalanceModel.GoodHeldRatio
                && Balance.Elapsed * Balance.HeldRatio >= Mathf.Min(holdSeconds, balanceSettings.Duration * EarthquakeBalanceModel.GoodHeldRatio);
            Debug.Log($"[EQ] denge alıştırması {result.balanceScore}/100, tutuş %{result.heldRatio * 100:0}; korunma pozu sarsıntı bitene kadar sürer.", this);

            // Sonuç: gösterge "Sarsıntı bitti — iyi tutundun!" / "Biraz zorlandın…" der; poz korunur, sarsıntı durulur.
            if (quakeEvents != null) quakeEvents.SetIntensity(0f);
            for (float t = 0f; t < resultSeconds; t += Time.deltaTime)
            {
                if (view != null) view.SetBalance(Balance);
                StepFeedback();
                yield return null;
            }
        }

        private void StepFeedback()
        {
            if (focusCamera != null) focusCamera.orthographicSize = Mathf.Lerp(focusCamera.orthographicSize, originalZoom * (1f - cameraZoom), Time.deltaTime * 4f);
            if (braceRenderer != null && Balance != null)
            {
                braceRenderer.sprite = originalRenderer.sprite;
                braceVisual.transform.localRotation = Quaternion.Euler(0, 0, -Balance.Position * braceTilt);
            }
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
                case AutoInput.Manual: return GameInput.ProtectionHeld;
                case AutoInput.NoInput: return false;
                case AutoInput.RespondWithRelease:
                    // Yarıda 1 sn bırakır (uyarı testi), sonra devam eder.
                    float since = Time.time - stepStart - AutoStepDelay;
                    if (since < 0f) return false;
                    float half = balanceSettings.Duration * 0.5f;
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

        private void BeginFeedback()
        {
            focusCamera = Camera.main;
            if (focusCamera != null)
            {
                var shake = focusCamera.GetComponent<EarthquakeCameraShake>();
                if (shake != null && shake.ReducedMotion) focusCamera = null;
            }
            if (quakeEvents != null && player != null) quakeEvents.FocusProtection(player.transform.position);
            if (focusCamera != null)
            {
                originalZoom = focusCamera.orthographicSize;
                pixelCamera = focusCamera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
                if (pixelCamera != null) { pixelCameraWasEnabled = pixelCamera.enabled; pixelCamera.enabled = false; }
            }
            if (player == null) return;
            originalRenderer = player.GetComponent<SpriteRenderer>();
            if (originalRenderer == null) return;
            originalRendererEnabled = originalRenderer.enabled;
            braceVisual = new GameObject("ProtectionBraceVisual");
            braceVisual.transform.SetParent(player.transform, false);
            braceRenderer = braceVisual.AddComponent<SpriteRenderer>();
            braceRenderer.sprite = originalRenderer.sprite;
            braceRenderer.sharedMaterial = originalRenderer.sharedMaterial;
            braceRenderer.color = originalRenderer.color;
            braceRenderer.flipX = originalRenderer.flipX;
            braceRenderer.sortingLayerID = originalRenderer.sortingLayerID;
            braceRenderer.sortingOrder = originalRenderer.sortingOrder;
            braceRenderer.spriteSortPoint = originalRenderer.spriteSortPoint;
            originalRenderer.enabled = false;
        }

        private void RestoreFeedback()
        {
            if (focusCamera != null) focusCamera.orthographicSize = originalZoom;
            if (pixelCamera != null) pixelCamera.enabled = pixelCameraWasEnabled;
            focusCamera = null; pixelCamera = null;
            if (originalRenderer != null) originalRenderer.enabled = originalRendererEnabled;
            if (braceVisual != null) Destroy(braceVisual);
            braceVisual = null; braceRenderer = null; originalRenderer = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            IsRunning = false;
            if (view != null) view.Hide();
            RestoreAfter();
        }

        private static string Or(string a, string b) => string.IsNullOrEmpty(a) ? b : a;
        private static Sprite Or(Sprite a, Sprite b) => a != null ? a : b;
    }
}
