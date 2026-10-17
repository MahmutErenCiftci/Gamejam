using AfetUyanisi.Chapters.Rooms;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// YALNIZCA TEST SAHNESİ İÇİN (Test_FloodEvening_PUZZLE): FloodEveningController yokken onun entegrasyonunu taklit eder.
    /// Sahnedeki IFloodEveningInteraction'ları RoomInteractor'a kaydeder (E → Play), FreeRoam'u açar, mini oyun açılınca
    /// kapatır, Hub olaylarını Console'a yazar. Bölüm sahnesinde KULLANILMAZ (orada controller bu işi yapar).
    /// </summary>
    public class FloodPuzzleTestRig : MonoBehaviour
    {
        [SerializeField] private RoomInteractor interactor;
        [SerializeField] private IsoPlayerController player;
        [SerializeField] private InteractionHintBase hint;

        /// <summary>Son Hub olayı (sürücü/test için).</summary>
        public static string LastEvent { get; private set; } = "";

        private void OnEnable()
        {
            FloodEveningHub.MinigameOpened += OnOpened;
            FloodEveningHub.MinigameClosed += OnClosed;
            FloodEveningHub.StepCompleted += OnStep;
            FloodEveningHub.Mistake += OnMistake;
        }

        private void OnDisable()
        {
            FloodEveningHub.MinigameOpened -= OnOpened;
            FloodEveningHub.MinigameClosed -= OnClosed;
            FloodEveningHub.StepCompleted -= OnStep;
            FloodEveningHub.Mistake -= OnMistake;
        }

        private void Start()
        {
            if (interactor == null) interactor = FindAnyObjectByType<RoomInteractor>();
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            if (interactor == null) { Debug.LogError("[FloodPuzzleTestRig] RoomInteractor yok", this); return; }
            interactor.Configure(player, null, hint);
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (!(mb is IFloodEveningInteraction it) || it.Point == null) continue;
                interactor.Register(it.Point, () => it.Play());
                Debug.Log($"[FloodPuzzleTestRig] kayıt: {mb.name} → [{it.Point.KeyLabel}] {it.Point.ActionLabel}", mb);
            }
            interactor.InteractionEnabled = true;
            FloodEveningHub.FreeRoam = true;
            if (player != null) player.CanMove = true;
        }

        private static void Log(string s)
        {
            LastEvent = s;
            Debug.Log("[FloodPuzzleTestRig] Hub " + s);
        }

        private void OnOpened(string id) { FloodEveningHub.FreeRoam = false; Log("MinigameOpened " + id); }
        private void OnClosed(string id) { FloodEveningHub.FreeRoam = true; Log("MinigameClosed " + id); }
        private void OnStep(string id) => Log("StepCompleted " + id);
        private void OnMistake(string id) => Log("Mistake " + id);
    }
}
