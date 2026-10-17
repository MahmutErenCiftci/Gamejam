using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using AfetUyanisi.Chapters.Rooms;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Chapters.Earthquake.Editor
{
    [InitializeOnLoad]
    public static class StoveFireChecks
    {
        const string Pending = "Afet.StoveChecks";
        static bool running;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static StoveFireChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false); Run();
            };
        }
        [MenuItem("Afet Uyanisi/Stove/Run integrated checks")]
        public static void StartChecks()
        {
            if (running) return;
            if (EditorApplication.isPlaying) Run();
            else { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
        }
        static async void Run()
        {
            if (running) return; running = true;
            var report = new StringBuilder(); var oldKeyboard = Keyboard.current;
            Keyboard keyboard = null; Action restore = null;
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 1;
                restore = (Action)typeof(EarthquakeBalanceChecks).GetMethod("ReferenceResolution", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                await Wait(150);
                var stove = await LoadPrep();
                var point = stove.GetComponent<Interactable>();
                var quests = UnityEngine.Object.FindAnyObjectByType<PreparationQuestManager>();
                var timer = UnityEngine.Object.FindAnyObjectByType<EmergencyCountdownManager>();
                var interactor = UnityEngine.Object.FindAnyObjectByType<RoomInteractor>();
                var player = UnityEngine.Object.FindAnyObjectByType<IsoPlayerController>();
                Check(stove.State == StoveFireController.StoveState.On && stove.HeatRenderer.enabled, "Actual kitchen starts ON with visible heat", report);
                Check(quests.TotalCount == 7 && quests.Find(StoveFireController.TaskId).source == PrepTaskSource.External,
                    "Existing task id reused; seven tasks; inspection alone cannot complete", report);
                var firstFrame = stove.HeatRenderer.sprite; await Wait(160);
                Check(firstFrame != stove.HeatRenderer.sprite, "Eight frame heat loop advances", report);
                ScreenCapture.CaptureScreenshot("Logs/stove-on.png"); await Wait(100);
                int completions = 0;
                quests.TaskCompleted += id => { if (id == StoveFireController.TaskId) completions++; };
                await Until(() => interactor.Current == point, 3);
                keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                float before = timer.GetRemainingTime();
                Keys(keyboard, Key.E);
                await Until(() => stove.State == StoveFireController.StoveState.TurningOff, 3);
                Keys(keyboard);
                Check(!point.Available && completions == 0 && !quests.IsDone(StoveFireController.TaskId), "E commits shutoff; task waits for animation", report);
                await Wait(100);
                Check(!timer.IsPaused && timer.GetRemainingTime() < before && player.CanMove && !interactor.IsBusy,
                    "Countdown and movement continue while stove alone is locked", report);
                Keys(keyboard, Key.E); await Wait(40); Keys(keyboard);
                await Until(() => stove.State == StoveFireController.StoveState.Off, 3);
                Check(completions == 1 && quests.IsDone(StoveFireController.TaskId) && !stove.HeatRenderer.enabled,
                    "OFF completes once and removes heat/prompt", report);
                stove.enabled = false; await Wait(60); stove.enabled = true; await Wait(60);
                Check(stove.State == StoveFireController.StoveState.Off && !point.Available && completions == 1, "OFF survives disable/re-enable", report);
                ScreenCapture.CaptureScreenshot("Logs/stove-off.png"); await Wait(100);
                Keys(keyboard); InputSystem.RemoveDevice(keyboard); keyboard = null;

                stove = await LoadPrep();
                quests = UnityEngine.Object.FindAnyObjectByType<PreparationQuestManager>();
                timer = UnityEngine.Object.FindAnyObjectByType<EmergencyCountdownManager>();
                Check(stove.State == StoveFireController.StoveState.On && !quests.IsDone(StoveFireController.TaskId), "Fresh chapter resets ON and incomplete", report);
                var tint = stove.StoveRenderer.color;
                stove.StoveRenderer.color = new Color(.15f, .2f, .25f, .5f); await Wait(60);
                Check(Mathf.Abs(stove.HeatRenderer.color.r - .15f) < .01f && stove.HeatRenderer.color.a <= .5f, "Heat inherits darkness/tint", report);
                stove.StoveRenderer.enabled = false; await Wait(60);
                Check(!stove.HeatRenderer.enabled, "Hidden stove cannot leak heat", report);
                stove.StoveRenderer.enabled = true; stove.StoveRenderer.color = tint;
                int flushed = 0;
                quests.TaskCompleted += id => { if (id == StoveFireController.TaskId) flushed++; };
                timer.StartTimer(.18f);
                stove.StartCoroutine(stove.Use());
                await Until(() => timer.HasExpired && quests.IsLocked, 3);
                Check(stove.State == StoveFireController.StoveState.Off && quests.IsDone(StoveFireController.TaskId) && flushed == 1,
                    "Expiry resolves committed OFF before task snapshot locks", report);
                await Wait(600);
                stove.StartCoroutine(stove.Use());
                Check(flushed == 1 && !stove.GetComponent<Interactable>().Available, "No delayed or duplicate completion after expiry", report);

                stove = await LoadPrep(); quests = UnityEngine.Object.FindAnyObjectByType<PreparationQuestManager>();
                quests.CompleteTask(StoveFireController.TaskId); await Wait(50);
                Check(stove.State == StoveFireController.StoveState.Off && !stove.HeatRenderer.enabled, "Authoritative completed task restores OFF immediately", report);
                stove = await LoadPrep(); quests = UnityEngine.Object.FindAnyObjectByType<PreparationQuestManager>();
                stove.StartCoroutine(stove.Use()); stove.enabled = false; await Wait(50);
                Check(stove.State == StoveFireController.StoveState.Off && quests.IsDone(StoveFireController.TaskId),
                    "Disabling during committed shutdown resolves OFF safely", report);
                stove.enabled = true;
                report.AppendLine("PASS: STOVE PLAY MODE CHECKS COMPLETE");
            }
            catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
                restore?.Invoke(); Time.timeScale = previousScale;
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/stove-play-checks.txt", report.ToString());
                Debug.Log("[Stove Checks]\n" + report); running = false;
            }
        }
        static async Task<StoveFireController> LoadPrep()
        {
            GameSceneRouter.LoadChapter(ChapterId.Earthquake);
            await Until(() => SceneManager.GetActiveScene().name == "EarthquakeHome", 15);
            await Wait(150);
            var mode = UnityEngine.Object.FindAnyObjectByType<EarthquakeModeController>(); mode.StopAllCoroutines();
            foreach (var bubble in UnityEngine.Object.FindObjectsByType<AfetUyanisi.Dialogue.SpeechBubble>(FindObjectsInactive.Exclude)) bubble.Hide();
            typeof(EarthquakeModeController).GetField("phase", Private).SetValue(mode, EarthquakePhase.Prep);
            var stove = UnityEngine.Object.FindAnyObjectByType<StoveFireController>();
            if (stove == null) throw new InvalidOperationException("Kitchen stove not integrated");
            var room = stove.GetComponentInParent<EarthquakeRoom>();
            typeof(EarthquakeModeController).GetMethod("EnterRoom", Private).Invoke(mode, new object[] { room });
            var player = UnityEngine.Object.FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            player.gameObject.SetActive(true); player.PlaceAt(stove.transform.position, Vector2.up);
            typeof(EarthquakeModeController).GetMethod("SetFreeRoam", Private).Invoke(mode, new object[] { true });
            var fader = typeof(EarthquakeModeController).GetField("fader", Private).GetValue(mode);
            fader.GetType().GetMethod("SetOpaque").Invoke(fader, new object[] { false });
            UnityEngine.Object.FindAnyObjectByType<EmergencyCountdownManager>().StartTimer(60);
            await Wait(200);
            return stove;
        }
        static void Keys(Keyboard keyboard, params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static async Task Wait(int ms) { await Task.Delay(ms); if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play stopped"); }
        static async Task Until(Func<bool> condition, float seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (!condition()) { if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(); await Wait(20); }
        }
        static void Check(bool ok, string text, StringBuilder report) { if (!ok) throw new InvalidOperationException(text); report.AppendLine("PASS: " + text); }
    }
}
