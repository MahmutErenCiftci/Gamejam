using System;
using System.Collections;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>Berk'in Inspector'dan bağlanan UI bileşeni uygular; input yalnız RoomInteractor'dan gelir.</summary>
    public interface IBranchProgressView
    {
        void Show(string hint);
        void SetProgress(float progress);
        void Press();
        void Hide();
    }

    /// <summary>
    /// Düşen dal engeli. Rhythm kipi (P-FLOOD 18 Eki, akşam selde kullanılan): devrilen ağacın yatan gövdesi ritimle itilip çekilir
    /// (yeşil pencerede E, sırayla İT / ÇEK; ayrıntı RhythmPlay). Mash kipi: RoomInteractor'ın her E basışında Play çağrısı +1 ilerletir.
    /// Arm ağaç düşüşünden sonra açar; ilerleme uzaklaşınca korunur, dal ve collider birlikte kayar.
    /// Eski AlternativePassage kipi:
    /// yolu kesen dala dokunulmaz ("Engeli aşmaya çalışma"), yanındaki işaretli alternatif geçidin mandalı açılır:
    /// [E] → panel + kısa ilerleme çubuğu (E ya da fareyle "Basılı tut"; bırakınca yavaşça geri iner, Esc/Vazgeç ile
    /// iptal edilir, nokta açık kalır) → AltPassage'ın yolu kesen collider'ları kapanır, açık görsel gelir → bir kez,
    /// kalıcı. SmallBranch kipi: kuru ve korunaklı geçitteki küçük, hafif dal kenara çekilir (aynı çubuk).
    /// Hub: NotifyOpened/Closed("branch"), sonunda Complete("branch_cleared"). E tuşunu "kullanmak" için dinlemez;
    /// controller Point'i RoomInteractor'a kaydedip Play()'i çalıştırır (Play içinde E basılı tutma ilerletir).
    /// </summary>
    public class BranchObstacle : MonoBehaviour, IFloodEveningInteraction, IFloodEveningResettable
    {
        public enum Mode { AlternativePassage = 0, SmallBranch = 1, Mash = 2, Rhythm = 3 }

        public const string MinigameId = "branch";
        public const string StepId = "branch_cleared";
        private const string BeatChannel = "fe_branch_push";

        [SerializeField] private Mode mode = Mode.Mash;
        [SerializeField] private Interactable point;
        [SerializeField] private string alternativeLabel = "Alternatif geçidi aç";
        [SerializeField] private string smallBranchLabel = "Küçük dalı kenara çek";

        [Header("Art arda bas (SEL3-20)")]
        [SerializeField] private bool startArmed = false;
        [SerializeField, Min(1)] private int requiredPresses = 12;
        [SerializeField] private IsoPlayerController player;
        [Tooltip("Dal görseli ve collider'ının ortak kökü. Etkileşim noktası bu kökün dışında kalmalı.")]
        [SerializeField] private Transform branchRoot;
        [SerializeField] private Vector3 clearOffset = new Vector3(0.9f, 0.3f, 0f);
        [SerializeField, Min(0.01f)] private float slideSeconds = 0.35f;
        [Tooltip("Berk'in IBranchProgressView uygulayan bileşeni: prefab veya sahne örneği.")]
        [SerializeField] private MonoBehaviour progressView;
        [SerializeField] private TextAsset storyJson;

        private IBranchProgressView mashView;
        private FloodEveningTexts texts;
        private bool armed, panelNotified;
        private int pressCount, lastPressFrame = -1, resetVersion;
        private Vector3 branchStart;
        private float pressFeedbackUntil;

        [Header("Ritimle it / çek (P-FLOOD 18 Eki; Rhythm kipi)")]
        [Tooltip("Ritim göstergesi (FloodRhythmView). Boşsa bu objeye eklenir.")]
        [SerializeField] private FloodRhythmView rhythmView;
        [Tooltip("Yolu açmak için gereken iyi vuruş")]
        [SerializeField, Min(1)] private int requiredBeats = 6;
        [Tooltip("İşaretçinin çubuğu bir uçtan öbürüne geçme süresi (sn) = iki vuruş fırsatı arası")]
        [SerializeField, Min(0.3f)] private float beatSeconds = 0.75f;
        [Tooltip("Yeşil pencerenin yarı genişliği (çubuğun yarısına oranla)")]
        [SerializeField, Range(0.1f, 0.6f)] private float windowHalf = 0.32f;
        [Tooltip("Ritim dışı basışta geri giden vuruş (başarısızlık yok)")]
        [SerializeField, Min(0f)] private float missPenalty = 0.5f;
        [Tooltip("Açılınca yolu kesen gövdenin kayacağı DÜNYA ofseti (ör. sokağın arka kenarına doğru)")]
        [SerializeField] private Vector2 clearOffsetWorld = new Vector2(-0.85f, 0.425f);
        [Tooltip("Her iyi vuruşta gövdenin it/çek sarsılması (piksel)")]
        [SerializeField] private float shovePixels = 3f;
        [Tooltip("Devrilen ağaç: her iyi vuruşta yaprak/dal parçacıkları onun kendi efektleriyle savrulur")]
        [SerializeField] private FloodTreeFallEvent treeFx;
        [Tooltip("Cenk'in itip çekerken öne/geri sarsılması (piksel; yürüme animasyonu oynamaz, yüzü ağaçta kalır)")]
        [SerializeField] private float cenkShovePixels = 2f;
        [SerializeField] private string beatSfx = "SFX_FE_Branch_Push";
        [SerializeField] private string openSfx = "SFX_FE_Passage_Open";

        [Header("Açılınca")]
        [Tooltip("Kapanacak yol kesen collider'lar: AltPassage'ın kapalı collider'ı (Alternatif) ya da küçük dalın collider'ı (Küçük dal).")]
        [SerializeField] private Collider2D[] blockers = Array.Empty<Collider2D>();
        [Tooltip("Açılınca görünür (açık geçit, yeşil ok...).")]
        [SerializeField] private GameObject[] showWhenOpen = Array.Empty<GameObject>();
        [Tooltip("Açılınca gizlenir (kapalı geçit zinciri, küçük dal görseli...).")]
        [SerializeField] private GameObject[] hideWhenOpen = Array.Empty<GameObject>();

        [Header("İlerleme")]
        [SerializeField, Min(0.2f)] private float holdSeconds = 1.2f;
        [Tooltip("Bırakınca saniyede geri inme oranı (0..1)")]
        [SerializeField, Min(0f)] private float decayPerSecond = 0.6f;

        [Header("Diyalog (isteğe bağlı)")]
        [SerializeField] private DialogueRunnerBase dialogue;
        [SerializeField] private string doneSceneId = "FE_BRANCH_DONE";

        [Header("Panel")]
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 70;
        [SerializeField] private string altTitle = "Yol dalla kapanmış";
        [SerializeField, TextArea] private string altBody = "Dala dokunma, üstünden aşmaya çalışma. Yandaki işaretli alternatif geçidin mandalını aç.";
        [SerializeField] private string smallTitle = "Küçük, hafif bir dal";
        [SerializeField, TextArea] private string smallBody = "Kuru ve korunaklı geçitte küçük bir dal yolu kapatıyor. Dikkatlice kenara çek.";
        [SerializeField] private string holdLabel = "Basılı tut";
        [SerializeField] private string hint = "[E] ya da fareyle basılı tut · [Esc] vazgeç";

        private Vector3 rhythmStartWorld;
        private bool rhythmStartCaptured;
        private float beats;

        private Canvas canvas;
        private CanvasGroup group;
        private Text titleText, bodyText, percentText;
        private RectTransform fill;
        private HoldArea holdArea;
        private bool cancelClicked, playing;

        public Interactable Point => point;
        public bool CanUse => isActiveAndEnabled && armed && !IsCleared && !playing && !FloodEveningHub.Paused;
        public bool IsCleared { get; private set; }
        public bool IsOpen => playing;
        /// <summary>0..1 ilerleme (panel açıkken).</summary>
        public float Progress { get; private set; }
        /// <summary>Ritim kipi: iyi vuruş sayısı (ceza yarım düşürebilir) ve test sayaçları.</summary>
        public float Beats => beats;
        public int GoodBeats { get; private set; }
        public int MissBeats { get; private set; }
        /// <summary>QA: işaretçi şu an yeşil pencerede mi (ritim paneli açıkken).</summary>
        public bool InBeatWindow { get; private set; }

        private void Awake()
        {
            if (point == null) point = GetComponentInChildren<Interactable>(true);
            if (player == null)
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    player = root.GetComponentInChildren<IsoPlayerController>(true);
                    if (player != null) break;
                }
            texts = storyJson != null ? FloodEveningTexts.Load(storyJson) : null;
            if (branchRoot != null) branchStart = branchRoot.localPosition;
            if (point != null) point.Setup(mode == Mode.Rhythm ? RhythmLabel : mode == Mode.Mash ? MashLabel : mode == Mode.SmallBranch ? smallBranchLabel : alternativeLabel, "", false, false);
            if (mode == Mode.Rhythm && rhythmView == null) rhythmView = gameObject.AddComponent<FloodRhythmView>();
            if (progressView != null)
            {
                if (!progressView.gameObject.scene.IsValid()) progressView = Instantiate(progressView, transform);
                mashView = progressView as IBranchProgressView;
                if (mashView == null) Debug.LogWarning("[BranchObstacle] progressView, IBranchProgressView uygulamalı.", this);
                else mashView.Hide();
            }
            armed = startArmed;
            Apply(false);
        }

        private void OnEnable()
        {
            FloodEveningHub.ResetRequested += ResetModule;
            if (point != null) point.SetAvailable(CanUse);
        }
        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            SetPanel(false);
            mashView?.Hide();
            playing = false;
            resetVersion++;
            CloseNotification();
            if (point != null) point.SetAvailable(false);
        }

        /// <summary>Eren ağaç düşüşünü bitirince çağırır. Tekrar çağırmak ilerlemeyi sıfırlamaz.</summary>
        [ContextMenu("Test: Arm (Play Mode)")]
        public void Arm()
        {
            armed = true;
            if (mode == Mode.Mash) SetBranchVisible(true);
            if (mode == Mode.Rhythm && branchRoot != null && !rhythmStartCaptured) { rhythmStartWorld = branchRoot.position; rhythmStartCaptured = true; }
            if (point != null) point.SetAvailable(CanUse);
        }

        private string MashLabel => texts != null ? texts.Ui("branch_mash_action") : "Art arda bas: Engeli kenara it";
        private string RhythmLabel => texts != null && texts.HasUi("branch_rhythm_action") ? texts.Ui("branch_rhythm_action") : "Ağacı ritimle it, yolu aç";
        private string MashHint => texts != null ? texts.Ui("branch_mash_hint") : "[E] Art arda bas: Engeli kenara it";
        private bool InRange => player != null && player.isActiveAndEnabled && point != null && point.IsPlayerInRange(player);

        private void LateUpdate()
        {
            if (mode != Mode.Mash) return;
            bool visible = CanUse && FloodEveningHub.FreeRoam && InRange;
            if (mashView != null)
            {
                if (visible) { mashView.Show(MashHint); mashView.SetProgress(Progress); }
                else mashView.Hide();
            }
            else
            {
                if (visible) { Build(); SetFill(Progress); }
                SetPanel(visible);
                if (fill != null) fill.localScale = Time.unscaledTime < pressFeedbackUntil ? new Vector3(1f, 1.15f, 1f) : Vector3.one;
            }
        }

        private IEnumerator MashOnce()
        {
            // RoomInteractor menzili ve tek hedefi seçer; doğrudan çağrılar da aynı güvenlik kapısından geçer.
            // E'nin Pressed kontrolü RoomInteractor'da yapılır; coroutine içinde tekrar tuş okumayız.
            if (!CanUse || !InRange || lastPressFrame == Time.frameCount) yield break;
            lastPressFrame = Time.frameCount;
            pressCount++;
            var animator = player.GetComponent<IsoCharacterAnimator>();
            if (animator != null) animator.Face(point.transform.position - player.transform.position);
            Progress = Mathf.Clamp01((float)pressCount / Mathf.Max(1, requiredPresses));
            mashView?.SetProgress(Progress);
            mashView?.Press();
            pressFeedbackUntil = Time.unscaledTime + 0.1f;
            if (pressCount < Mathf.Max(1, requiredPresses)) yield break;

            playing = true;
            int version = resetVersion;
            panelNotified = true;
            FloodEveningHub.NotifyOpened(MinigameId);
            SetPanel(false);
            mashView?.Hide();
            try
            {
                Vector3 from = branchRoot != null ? branchRoot.localPosition : Vector3.zero;
                Vector3 to = branchStart + clearOffset;
                float elapsed = 0f;
                while (elapsed < slideSeconds)
                {
                    if (version != resetVersion || !isActiveAndEnabled) yield break;
                    if (!FloodEveningHub.Paused)
                    {
                        elapsed += Time.deltaTime;
                        if (branchRoot != null) branchRoot.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / slideSeconds));
                    }
                    yield return null;
                }
                if (version != resetVersion || !isActiveAndEnabled) yield break;
                if (branchRoot != null) branchRoot.localPosition = to;
                IsCleared = true;
                if (point != null) point.SetAvailable(false);
                FloodEveningHub.Say("FE_PATH_OPEN", "Yol açıldı.");
                FloodEveningHub.Complete(StepId);
            }
            finally
            {
                playing = false;
                CloseNotification();
            }
        }

        private void CloseNotification()
        {
            if (!panelNotified) return;
            panelNotified = false;
            FloodEveningHub.NotifyClosed(MinigameId);
        }

        public IEnumerator Play()
        {
            if (!CanUse) yield break;
            if (mode == Mode.Mash) { yield return MashOnce(); yield break; }
            if (mode == Mode.Rhythm) { yield return RhythmPlay(); yield break; }
            playing = true;
            panelNotified = true;
            FloodEveningHub.NotifyOpened(MinigameId);
            bool done = false;
            try
            {
                Build();
                bool small = mode == Mode.SmallBranch;
                titleText.text = small ? smallTitle : altTitle;
                bodyText.text = small ? smallBody : altBody;
                Progress = 0f;
                cancelClicked = false;
                holdArea.Held = false;
                SetFill(0f);
                SetPanel(true);

                yield return null; // [E]'nin aynı karesi iptal/ilerleme sayılmasın (basılı tutmak ilerletir)

                while (true)
                {
                    if (cancelClicked || GameInput.BackPressed) break;
                    bool held = GameInput.InteractHeld || holdArea.Held;
                    Progress = held
                        ? Mathf.Min(1f, Progress + Time.deltaTime / holdSeconds)
                        : Mathf.Max(0f, Progress - decayPerSecond * Time.deltaTime);
                    SetFill(Progress);
                    if (Progress >= 1f) { done = true; break; }
                    yield return null;
                }

                if (done)
                {
                    percentText.text = small ? "Yol açıldı" : "Geçit açıldı";
                    for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) yield return null;
                }
                SetPanel(false);

                if (done)
                {
                    IsCleared = true;
                    Apply(true);
                    Debug.Log($"[BranchObstacle] {name}: {(small ? "küçük dal kenara çekildi" : "alternatif geçit açıldı")}", this);
                    FloodEveningHub.Complete(StepId);
                    yield return null;
                    if (dialogue == null) dialogue = FindAnyObjectByType<DialogueRunnerBase>();
                    if (dialogue != null && !string.IsNullOrEmpty(doneSceneId)) yield return dialogue.Play(doneSceneId);
                }
            }
            finally
            {
                SetPanel(false);
                playing = false;
                CloseNotification();
            }
        }

        // =====================================================================
        // Ritimle it / çek (P-FLOOD): işaretçi çubukta ileri geri gezer; yeşil pencerede E = iyi vuruş (gövde it/çek sarsılır,
        // SFX_FE_Branch_Push), pencere dışında E = yarım vuruş geri + nazik "!" (asla başarısızlık değil). requiredBeats iyi
        // vuruşta gövde sokağın arka kenarına kayar (collider'ı da), SFX_FE_Passage_Open, "branch_cleared". Süre akar (mini oyun).
        // Esc: bırak (ilerleme korunur, nokta yeniden kullanılabilir).
        // =====================================================================

        private IEnumerator RhythmPlay()
        {
            if (!CanUse || !InRange) yield break;
            playing = true;
            panelNotified = true;
            int version = resetVersion;
            FloodEveningHub.NotifyOpened(MinigameId);
            if (branchRoot != null && !rhythmStartCaptured) { rhythmStartWorld = branchRoot.position; rhythmStartCaptured = true; }
            var animator = player != null ? player.GetComponent<IsoCharacterAnimator>() : null;
            Vector2 toTree = branchRoot != null && player != null ? (Vector2)(TreePoint() - player.transform.position) : Vector2.right;
            if (animator != null) animator.Face(toTree);
            Vector2 pushDir = toTree.sqrMagnitude > 1e-5f ? toTree.normalized : Vector2.right;
            bool done = false;
            float phase = 0f, shoveT = 99f, wobbleT = 99f, beatSoundUntil = 0f, cenkShoveT = 99f;
            bool cenkShoving = false;
            var body = player != null ? player.GetComponent<Rigidbody2D>() : null;
            Vector2 cenkBase = body != null ? body.position : Vector2.zero;
            float winW = windowHalf * Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(GoodBeats / Mathf.Max(1f, requiredBeats - 1f)));
            float winC = NextWindow(-1f, winW); // işaretçi sol uçtan başlar
            Vector2 shoveDir = Vector2.zero;
            var cam = Camera.main != null ? Camera.main.GetComponent<ChapterCamera>() : null;
            try
            {
                if (rhythmView != null)
                {
                    rhythmView.Show(requiredBeats, windowHalf);
                    rhythmView.SetPrompt(GoodBeats % 2 == 0);
                    rhythmView.SetBeats(beats);
                    rhythmView.SetWindow(winC, winW);
                }
                yield return null; // başlatan E bu karede vuruş sayılmasın
                while (true)
                {
                    if (version != resetVersion || !isActiveAndEnabled) yield break;
                    if (GameInput.BackPressed || GameInput.RightClickPressed) break;
                    float dt = Time.deltaTime;
                    phase += dt;
                    // üçgen dalga: -1 → 1 → -1; merkez geçişleri (k + 0,5) * beatSeconds anlarında
                    float u = phase / beatSeconds;
                    float tri = Mathf.PingPong(u, 1f) * 2f - 1f;
                    InBeatWindow = Mathf.Abs(tri - winC) <= winW;
                    if (rhythmView != null) rhythmView.SetMarker(tri);
                    if (GameInput.InteractPressed)
                    {
                        if (InBeatWindow)
                        {
                            bool isPush = GoodBeats % 2 == 0;
                            GoodBeats++;
                            beats = Mathf.Min(requiredBeats, beats + 1f);
                            shoveDir = isPush ? pushDir : -pushDir;
                            shoveT = 0f;
                            // SFX_FE_Branch_Push sürekli bir zorlanma sesi (1,6 sn): iyi vuruşlar sürdükçe döngü çalar, ~0,5 sn vuruş gelmezse susar
                            if (!string.IsNullOrEmpty(beatSfx)) GameAudio.PlayLoop(beatSfx, BeatChannel, 0.75f, 0.05f);
                            beatSoundUntil = Time.time + 0.5f;
                            if (animator != null) animator.Face(toTree);
                            if (rhythmView != null) { rhythmView.Good(); rhythmView.SetPrompt(GoodBeats % 2 == 0); }
                            if (cam != null) cam.ShakeOffset = shoveDir * (1f / 64f);
                            if (treeFx != null) treeFx.EmitShoveDebris(3, 1, clearOffsetWorld * (Progress * 0.18f));
                            if (body != null && !cenkShoving) cenkBase = body.position;
                            cenkShoveT = 0f;
                        }
                        else
                        {
                            MissBeats++;
                            beats = Mathf.Max(0f, beats - missPenalty);
                            wobbleT = 0f;
                            if (rhythmView != null) rhythmView.Miss();
                        }
                        if (rhythmView != null) rhythmView.SetBeats(beats);
                        Progress = beats / requiredBeats;
                        // her basıştan sonra pencere yeni yere geçer; yalnız iyi vuruşlar onu biraz daraltır (son vuruşta ~%70)
                        winW = windowHalf * Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(GoodBeats / Mathf.Max(1f, requiredBeats - 1f)));
                        winC = NextWindow(tri, winW);
                        if (rhythmView != null) rhythmView.SetWindow(winC, winW);
                    }
                    if (beatSoundUntil > 0f && Time.time > beatSoundUntil) { beatSoundUntil = 0f; GameAudio.StopLoop(BeatChannel, 0.2f); }
                    // gövde: ilerledikçe biraz kayar + it/çek sarsılması (tam piksel) + ıskada küçük titreme
                    shoveT += dt; wobbleT += dt; cenkShoveT += dt;
                    // Cenk: İT'te gövdeye doğru, ÇEK'te geriye kısa bir adım ve geri dönüş (birkaç piksel; collider'lar geçerli)
                    if (player != null)
                    {
                        bool on = cenkShoveT < 0.16f && body != null;
                        if (on || cenkShoving)
                        {
                            float k = on ? Mathf.Round(Mathf.Sin(cenkShoveT / 0.16f * Mathf.PI) * cenkShovePixels) / 64f : 0f;
                            body.position = cenkBase + shoveDir * k;
                            cenkShoving = on;
                        }
                    }
                    if (cam != null && shoveT > 0.08f && shoveT < 0.2f) cam.ShakeOffset = Vector2.zero;
                    float shove = shoveT < 0.18f ? Mathf.Sin(shoveT / 0.18f * Mathf.PI) * shovePixels / 64f : 0f;
                    float wobble = wobbleT < 0.2f ? Mathf.Round(Mathf.Sin(wobbleT * 60f)) / 64f : 0f;
                    PlaceTree(Progress * 0.18f, shoveDir * shove + new Vector2(wobble, 0f));
                    if (beats >= requiredBeats) { done = true; break; }
                    yield return null;
                }
                if (cam != null) cam.ShakeOffset = Vector2.zero;
                if (cenkShoving && body != null) { body.position = cenkBase; cenkShoving = false; }
                InBeatWindow = false;
                if (!done) { PlaceTree(Progress * 0.18f, Vector2.zero); yield break; }

                // yol açılıyor: gövde arka kenara kayar (collider'ı da), ses, adım
                if (rhythmView != null) rhythmView.Done();
                if (!string.IsNullOrEmpty(openSfx)) GameAudio.PlaySfx(openSfx, 0.85f);
                float from = Progress * 0.18f;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    if (version != resetVersion || !isActiveAndEnabled) yield break;
                    PlaceTree(Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t / 0.6f)), Vector2.zero);
                    yield return null;
                }
                PlaceTree(1f, Vector2.zero);
                for (float t = 0f; t < 0.3f; t += Time.deltaTime) yield return null;
                IsCleared = true;
                Apply(true);
                if (point != null) point.SetAvailable(false);
                Debug.Log($"[BranchObstacle] {name}: ağaç ritimle kenara itildi (iyi {GoodBeats}, ıska {MissBeats})", this);
                FloodEveningHub.Say("FE_PATH_OPEN", "Yol açıldı.");
                FloodEveningHub.Complete(StepId);
            }
            finally
            {
                if (cenkShoving && body != null) body.position = cenkBase;
                GameAudio.StopLoop(BeatChannel, 0.2f);
                if (rhythmView != null) rhythmView.Hide();
                InBeatWindow = false;
                playing = false;
                CloseNotification();
            }
        }

        // Yeni pencere yeri: çubuğun tamamen içinde, işaretçinin şu anki yerinin altında değil (en az 0,55 uzakta)
        private static float NextWindow(float marker, float half)
        {
            float lo = -1f + half, hi = 1f - half;
            for (int i = 0; i < 12; i++)
            {
                float c = UnityEngine.Random.Range(lo, hi);
                if (Mathf.Abs(c - marker) >= 0.55f + half * 0.5f) return c;
            }
            return marker > 0f ? lo : hi;
        }

        private Vector3 TreePoint()
        {
            if (branchRoot == null) return transform.position;
            var sr = branchRoot.GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.bounds.center : branchRoot.position;
        }

        // k = 0 (başta) .. 1 (açık): gövde clearOffsetWorld boyunca kayar; extra = anlık sarsılma (dünya)
        private void PlaceTree(float k, Vector2 extra)
        {
            if (branchRoot == null || !rhythmStartCaptured) return;
            Vector3 p = rhythmStartWorld + (Vector3)(clearOffsetWorld * Mathf.Clamp01(k) + extra);
            branchRoot.position = new Vector3(Mathf.Round(p.x * 64f) / 64f, Mathf.Round(p.y * 64f) / 64f, rhythmStartWorld.z);
        }

        public void ResetModule()
        {
            SetPanel(false);
            mashView?.Hide();
            resetVersion++;
            CloseNotification();
            playing = false;
            IsCleared = false;
            Progress = 0f;
            pressCount = 0;
            lastPressFrame = -1;
            armed = startArmed;
            beats = 0f; GoodBeats = 0; MissBeats = 0; InBeatWindow = false;
            if (rhythmView != null) rhythmView.Hide();
            if (mode == Mode.Rhythm)
            {
                if (branchRoot != null && rhythmStartCaptured) branchRoot.position = rhythmStartWorld;
                rhythmStartCaptured = false;
            }
            else if (branchRoot != null) branchRoot.localPosition = branchStart;
            Apply(false);
        }

        private void Apply(bool open)
        {
            if (mode == Mode.Mash)
            {
                SetBranchVisible(armed);
                if (point != null) point.SetAvailable(CanUse);
                return;
            }
            if (mode == Mode.Rhythm)
            {
                // gövde (branchRoot = devrilen ağacın yatan görseli) gizlenmez; yolu kesen collider'ı onunla birlikte kayar
                foreach (var c in blockers) if (c != null) c.enabled = !open;
                foreach (var go in showWhenOpen) if (go != null) go.SetActive(open);
                foreach (var go in hideWhenOpen) if (go != null) go.SetActive(!open);
                if (point != null) point.SetAvailable(CanUse);
                return;
            }
            foreach (var c in blockers) if (c != null) c.enabled = !open;
            foreach (var go in showWhenOpen) if (go != null) go.SetActive(open);
            foreach (var go in hideWhenOpen) if (go != null) go.SetActive(!open);
            if (point != null) point.SetAvailable(!open && armed);
        }

        private void SetBranchVisible(bool visible)
        {
            if (branchRoot != null && branchRoot != transform && (point == null || !point.transform.IsChildOf(branchRoot)))
                branchRoot.gameObject.SetActive(visible);
        }

        private void SetPanel(bool visible)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(visible);
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            if (!visible && holdArea != null) holdArea.Held = false;
        }

        private void SetFill(float k)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);
            percentText.text = Mathf.RoundToInt(k * 100f) + "%";
        }

        private void Build()
        {
            if (canvas != null) return;
            EventsUi.EnsureEventSystem();
            var f = EventsUi.ResolveFont(font);
            canvas = EventsUi.CreateCanvas(this, "FE_BranchCanvas", sortingOrder, out group);

            if (mode == Mode.Mash)
            {
                // Berk'in prefab'ı gelene kadar tıklamaları engellemeyen sade yedek görünüm.
                var panel = EventsUi.Panel("MashPanel", canvas.transform, EventsUi.TealDark, EventsUi.Teal);
                var rect = EventsUi.Place(panel.rectTransform, 0.5f, 0f, new Vector2(0f, 24f), new Vector2(360f, 44f));
                var label = EventsUi.Txt("Hint", rect, f, 11, EventsUi.Cream, TextAnchor.MiddleCenter);
                EventsUi.TL(label.rectTransform, 8f, 5f, 344f, 18f);
                label.text = MashHint;
                var back = EventsUi.Img("Bar", rect, EventsUi.Ink);
                EventsUi.TL(back.rectTransform, 8f, 27f, 296f, 10f);
                fill = EventsUi.Img("Fill", back.transform, EventsUi.Teal).rectTransform;
                EventsUi.Stretch(fill);
                fill.anchorMax = new Vector2(0f, 1f);
                percentText = EventsUi.Txt("Percent", rect, f, 9, EventsUi.Cream, TextAnchor.MiddleRight);
                EventsUi.TL(percentText.rectTransform, 308f, 25f, 44f, 14f);
                SetPanel(false);
                return;
            }

            var p = EventsUi.Panel("BranchPanel", canvas.transform, EventsUi.TealDark, EventsUi.Teal, true);
            var rt = EventsUi.Place(p.rectTransform, 0.5f, 0f, new Vector2(0f, 46f), new Vector2(280f, 104f));

            titleText = EventsUi.Txt("Title", rt, f, 11, EventsUi.Teal, TextAnchor.MiddleLeft);
            EventsUi.TL(titleText.rectTransform, 10f, 6f, 260f, 15f);
            bodyText = EventsUi.Txt("Body", rt, f, 8, EventsUi.Cream, TextAnchor.UpperLeft);
            EventsUi.TL(bodyText.rectTransform, 10f, 22f, 260f, 22f);

            // İlerleme çubuğu
            var barBack = EventsUi.Img("BarBack", rt, EventsUi.Ink);
            EventsUi.TL(barBack.rectTransform, 10f, 48f, 196f, 14f);
            var barEdge = EventsUi.Img("BarEdge", barBack.transform, EventsUi.TealDark);
            EventsUi.Stretch(barEdge.rectTransform);
            var barInner = EventsUi.Img("BarInner", barBack.transform, EventsUi.Ink);
            EventsUi.Stretch(barInner.rectTransform, 1f);
            var fillImg = EventsUi.Img("Fill", barInner.transform, EventsUi.Teal);
            fill = fillImg.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            percentText = EventsUi.Txt("Percent", rt, f, 9, EventsUi.Cream, TextAnchor.MiddleLeft);
            EventsUi.TL(percentText.rectTransform, 212f, 48f, 62f, 14f);

            // "Basılı tut" (fare) ve "Vazgeç"
            var hold = EventsUi.Img("HoldButton", rt, EventsUi.Teal, null, true);
            EventsUi.TL(hold.rectTransform, 10f, 68f, 120f, 18f);
            var holdInner = EventsUi.Img("Fill", hold.transform, EventsUi.TealDark);
            EventsUi.Stretch(holdInner.rectTransform, 1f);
            var holdText = EventsUi.Txt("Label", hold.transform, f, 9, EventsUi.Cream, TextAnchor.MiddleCenter);
            EventsUi.Stretch(holdText.rectTransform);
            holdText.text = "[E] " + holdLabel;
            holdArea = hold.gameObject.AddComponent<HoldArea>();
            holdArea.Visual = holdInner;

            var cancel = EventsUi.Img("CancelButton", rt, EventsUi.Muted, null, true);
            EventsUi.TL(cancel.rectTransform, 138f, 68f, 68f, 18f);
            var cancelInner = EventsUi.Img("Fill", cancel.transform, EventsUi.PanelLight);
            EventsUi.Stretch(cancelInner.rectTransform, 1f);
            var cancelText = EventsUi.Txt("Label", cancel.transform, f, 9, EventsUi.Cream, TextAnchor.MiddleCenter);
            EventsUi.Stretch(cancelText.rectTransform);
            cancelText.text = "Vazgeç";
            var btn = cancel.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => cancelClicked = true);

            var hintText = EventsUi.Txt("Hint", rt, f, 7, EventsUi.Muted, TextAnchor.MiddleLeft);
            EventsUi.TL(hintText.rectTransform, 10f, 89f, 260f, 12f);
            hintText.text = hint;

            SetPanel(false);
        }

        /// <summary>Fareyle basılı tutma alanı (EventSystem).</summary>
        private sealed class HoldArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public bool Held;
            public Image Visual;

            public void OnPointerDown(PointerEventData e)
            {
                if (e.button == PointerEventData.InputButton.Left) Held = true;
            }

            public void OnPointerUp(PointerEventData e) => Held = false;
            public void OnPointerExit(PointerEventData e) => Held = false;

            private void Update()
            {
                if (Visual != null) Visual.color = Held || GameInput.InteractHeld ? EventsUi.Teal * 0.75f + Color.black * 0.25f : EventsUi.TealDark;
            }

            private void OnDisable() => Held = false;
        }
    }
}
