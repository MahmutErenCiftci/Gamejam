using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Rooms;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// SADECE test sahnesi (Assets/Scenes/Test/Test_FloodEvening_EVENTS.unity): FloodEveningController'ın EVENTS
    /// modülleriyle ilgili davranışını taklit eder. Uyarıyı gösterir, sonra serbest dolaşımı açar (Hub.FreeRoam);
    /// IFloodEveningInteraction noktalarını RoomInteractor'a kaydeder (Play sırasında FreeRoam kapalı);
    /// Hub.SayRequest'i diyalogla oynatır (bu sırada FreeRoam kapalı); Hub olaylarını Console'a ve Log listesine yazar.
    /// Bölüm sahnesinde KULLANILMAZ.
    /// </summary>
    public class FloodEveningEventsTestHarness : MonoBehaviour
    {
        [SerializeField] private IsoPlayerController player;
        [SerializeField] private RoomInteractor interactor;
        [SerializeField] private DialogueRunnerBase dialogue;
        [SerializeField] private InteractionHintBase hint;
        [SerializeField] private FloodWarningPhone warning;
        [SerializeField] private bool showWarningOnStart = true;
        [Tooltip("Test için Progress01'i bu sürede 0→1 yapar (0 = sabit).")]
        [SerializeField] private float progressSeconds = 120f;

        public static readonly List<string> Log = new List<string>();
        private readonly Queue<KeyValuePair<string, string>> sayQueue = new Queue<KeyValuePair<string, string>>();
        private readonly List<IFloodEveningInteraction> modules = new List<IFloodEveningInteraction>();
        private bool busy, started;

        public bool Ready => started;
        public bool Busy => busy;

        private void OnEnable()
        {
            FloodEveningHub.StepCompleted += OnStep;
            FloodEveningHub.Mistake += OnMistake;
            FloodEveningHub.MinigameOpened += OnOpened;
            FloodEveningHub.MinigameClosed += OnClosed;
            FloodEveningHub.SayRequest += OnSay;
        }

        private void OnDisable()
        {
            FloodEveningHub.StepCompleted -= OnStep;
            FloodEveningHub.Mistake -= OnMistake;
            FloodEveningHub.MinigameOpened -= OnOpened;
            FloodEveningHub.MinigameClosed -= OnClosed;
            FloodEveningHub.SayRequest -= OnSay;
            FloodEveningHub.FreeRoam = false;
        }

        private IEnumerator Start()
        {
            Log.Clear();
            FloodEveningHub.FreeRoam = false;
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            if (interactor == null) interactor = FindAnyObjectByType<RoomInteractor>();
            if (dialogue == null) dialogue = FindAnyObjectByType<DialogueRunnerBase>();
            if (hint == null) hint = FindAnyObjectByType<InteractionHintBase>();
            if (interactor != null)
            {
                interactor.Configure(player, dialogue, hint);
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
                {
                    if (!(mb is IFloodEveningInteraction m) || m.Point == null) continue;
                    modules.Add(m);
                    var captured = m;
                    interactor.Register(m.Point, () => RunModule(captured));
                }
            }
            if (player != null) player.CanMove = false;
            yield return null;

            if (showWarningOnStart && warning != null)
            {
                Add("warning shown");
                yield return warning.ShowWarning();
            }
            SetFree(true);
            started = true;
            Add("free roam");
        }

        private IEnumerator RunModule(IFloodEveningInteraction m)
        {
            busy = true;
            FloodEveningHub.FreeRoam = false;
            yield return m.Play();
            FloodEveningHub.FreeRoam = true;
            busy = false;
        }

        private void Update()
        {
            foreach (var m in modules) if (m.Point != null) m.Point.SetAvailable(m.CanUse);
            if (progressSeconds > 0f && started && !busy)
                FloodEveningHub.Progress01 = Mathf.Clamp01(FloodEveningHub.Progress01 + Time.deltaTime / progressSeconds);
        }

        private void OnSay(string id, string fallback)
        {
            sayQueue.Enqueue(new KeyValuePair<string, string>(id, fallback));
            Add("say " + id);
            if (sayQueue.Count == 1) StartCoroutine(SayRoutine());
        }

        private IEnumerator SayRoutine()
        {
            busy = true;
            SetFree(false);
            while (sayQueue.Count > 0)
            {
                var kv = sayQueue.Peek();
                if (dialogue != null) yield return dialogue.Play(kv.Key);
                else Debug.Log("[FE-TEST] " + kv.Value);
                sayQueue.Dequeue();
            }
            yield return null;
            SetFree(true);
            busy = false;
        }

        private void SetFree(bool free)
        {
            FloodEveningHub.FreeRoam = free;
            if (player != null) player.CanMove = free;
            if (interactor != null) interactor.InteractionEnabled = free;
        }

        private void OnStep(string id) => Add("step " + id);
        private void OnMistake(string id) => Add("mistake " + id);
        private void OnOpened(string id) => Add("open " + id);
        private void OnClosed(string id) => Add("close " + id);

        private static void Add(string s)
        {
            Log.Add(s);
            Debug.Log("[FE-TEST] " + s);
        }
    }
}
