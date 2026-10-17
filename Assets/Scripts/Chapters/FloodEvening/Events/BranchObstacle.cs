using System;
using System.Collections;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Düşen dal engeli, güvenli uyarlama (PROMPT §E). BranchSpot'a eklenir. Varsayılan kip AlternativePassage:
    /// yolu kesen dala dokunulmaz ("Engeli aşmaya çalışma"), yanındaki işaretli alternatif geçidin mandalı açılır:
    /// [E] → panel + kısa ilerleme çubuğu (E ya da fareyle "Basılı tut"; bırakınca yavaşça geri iner, Esc/Vazgeç ile
    /// iptal edilir, nokta açık kalır) → AltPassage'ın yolu kesen collider'ları kapanır, açık görsel gelir → bir kez,
    /// kalıcı. SmallBranch kipi: kuru ve korunaklı geçitteki küçük, hafif dal kenara çekilir (aynı çubuk).
    /// Hub: NotifyOpened/Closed("branch"), sonunda Complete("branch_cleared"). E tuşunu "kullanmak" için dinlemez;
    /// controller Point'i RoomInteractor'a kaydedip Play()'i çalıştırır (Play içinde E basılı tutma ilerletir).
    /// </summary>
    public class BranchObstacle : MonoBehaviour, IFloodEveningInteraction, IFloodEveningResettable
    {
        public enum Mode { AlternativePassage, SmallBranch }

        public const string MinigameId = "branch";
        public const string StepId = "branch_cleared";

        [SerializeField] private Mode mode = Mode.AlternativePassage;
        [SerializeField] private Interactable point;
        [SerializeField] private string alternativeLabel = "Alternatif geçidi aç";
        [SerializeField] private string smallBranchLabel = "Küçük dalı kenara çek";

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

        private Canvas canvas;
        private CanvasGroup group;
        private Text titleText, bodyText, percentText;
        private RectTransform fill;
        private HoldArea holdArea;
        private bool cancelClicked, playing;

        public Interactable Point => point;
        public bool CanUse => !IsCleared && !playing;
        public bool IsCleared { get; private set; }
        public bool IsOpen => playing;
        /// <summary>0..1 ilerleme (panel açıkken).</summary>
        public float Progress { get; private set; }

        private void Awake()
        {
            if (point == null) point = GetComponentInChildren<Interactable>(true);
            if (point != null) point.Setup(mode == Mode.SmallBranch ? smallBranchLabel : alternativeLabel, "", false, false);
            Apply(false);
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            SetPanel(false);
        }

        public IEnumerator Play()
        {
            if (!CanUse) yield break;
            playing = true;
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
                FloodEveningHub.NotifyClosed(MinigameId);
            }
        }

        public void ResetModule()
        {
            SetPanel(false);
            playing = false;
            IsCleared = false;
            Progress = 0f;
            Apply(false);
        }

        private void Apply(bool open)
        {
            foreach (var c in blockers) if (c != null) c.enabled = !open;
            foreach (var go in showWhenOpen) if (go != null) go.SetActive(open);
            foreach (var go in hideWhenOpen) if (go != null) go.SetActive(!open);
            if (point != null) point.SetAvailable(!open);
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
