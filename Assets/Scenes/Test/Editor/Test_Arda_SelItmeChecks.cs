using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AfetUyanisi.Chapters;
using AfetUyanisi.Chapters.FloodEvening;
using AfetUyanisi.Chapters.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AfetUyanisi.Dialogue.Editor
{
    /// <summary>SEL3-20: gerçek RoomInteractor ve GameInput ile Arda'nın ayrı test sahnesinde regresyon.</summary>
    [InitializeOnLoad]
    public static class Test_Arda_SelItmeChecks
    {
        private const string Pending = "Arda.SelItme.Checks";
        private const string ScenePath = "Assets/Scenes/Test/Test_Arda_SelItme.unity";
        private const string PrefabPath = "Assets/Prefabs/FloodEvening/FE_BranchObstacle.prefab";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static Test_Arda_SelItmeChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                Run();
            };
        }

        [MenuItem("Afet Uyanisi/Arda/Prepare Sel Itme Test Scene")]
        public static void Prepare()
        {
            // Prefab bu görevde Arda'ya devredildi; sahne dosyalarında yalnız kendi kopyamız yazılır.
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var branch = root.GetComponent<BranchObstacle>();
                var settings = new SerializedObject(branch);
                settings.FindProperty("mode").intValue = 2;
                settings.FindProperty("startArmed").boolValue = false;
                settings.FindProperty("requiredPresses").intValue = 12;
                settings.FindProperty("branchRoot").objectReferenceValue = root.transform.Find("Branch");
                settings.FindProperty("storyJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Story/flood_evening.json");
                settings.ApplyModifiedPropertiesWithoutUndo();
                // Eski alternatif geçit dekoru yalnız bu Mash prefab'ında kullanılmaz. Eski kip kodu korunur.
                root.transform.Find("AltPassage").gameObject.SetActive(false);
                root.transform.Find("AltSign").gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                if (!AssetDatabase.CopyAsset("Assets/Scenes/Test/Test_FloodEvening_EVENTS.unity", ScenePath))
                    throw new InvalidOperationException("Arda test scene could not be copied");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var go in scene.GetRootGameObjects())
                    foreach (var harness in go.GetComponentsInChildren<FloodEveningEventsTestHarness>(true))
                    {
                        var settings = new SerializedObject(harness);
                        settings.FindProperty("showWarningOnStart").boolValue = false;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Afet Uyanisi/Arda/Run Sel Itme Checks")]
        public static void StartChecks()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before starting checks");
            if (!Application.isBatchMode)
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Save your open scenes before running checks");
            Prepare();
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        private static async void Run()
        {
            var report = new StringBuilder();
            Keyboard keyboard = null;
            var oldKeyboard = Keyboard.current;
            var oldInputSettings = InputSystem.settings;
            bool oldBackground = Application.runInBackground;
            int exitCode = 1;
            try
            {
                // Yalnız test oturumunda: batch GameView odağı olmadığı için sanal girdiyi oyuna yönlendir.
                InputSystem.settings = UnityEngine.Object.Instantiate(oldInputSettings);
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                Application.runInBackground = true;
                await Wait(300);
                var harness = UnityEngine.Object.FindAnyObjectByType<FloodEveningEventsTestHarness>();
                harness.StopAllCoroutines();
                harness.enabled = false;
                var branch = UnityEngine.Object.FindAnyObjectByType<BranchObstacle>();
                var player = UnityEngine.Object.FindAnyObjectByType<IsoPlayerController>();
                var interactor = UnityEngine.Object.FindAnyObjectByType<RoomInteractor>();
                interactor.Configure(player, null, null);
                interactor.AutoAdvance = true;
                interactor.Register(branch.Point, branch.Play);
                foreach (var point in UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include))
                    if (point != branch.Point) point.SetAvailable(false);
                FloodEveningHub.Paused = false;
                FloodEveningHub.FreeRoam = true;
                interactor.InteractionEnabled = true;
                player.CanMove = true;
                var tree = (Transform)Get(branch, "branchRoot");
                Vector3 start = tree.localPosition;
                var collider = tree.GetComponent<Collider2D>();
                player.PlaceAt(branch.Point.transform.position, Vector2.up);
                keyboard = InputSystem.AddDevice<Keyboard>();
                keyboard.MakeCurrent();
                await Tap(keyboard);
                Check(!branch.CanUse && branch.Progress == 0 && !tree.gameObject.activeSelf,
                    "Before Arm: point unavailable, branch hidden, E has no effect", report);
                branch.Arm();
                await Until(() => interactor.Current == branch.Point);
                Keys(keyboard, Key.E);
                await Until(() => branch.Progress > 0);
                float onePress = branch.Progress;
                await Wait(500);
                Check(Mathf.Approximately(onePress, 1f / 12f) && branch.Progress == onePress,
                    "Holding E for 500ms adds exactly one of twelve presses", report);
                Keys(keyboard);
                await Wait(100);
                Check(!interactor.IsBusy && player.CanMove, "Movement unlocks between presses", report);

                await Tap(keyboard);
                Check(Mathf.Approximately(branch.Progress, 2f / 12f), "A separate E press adds exactly one", report);
                float saved = branch.Progress;
                branch.Arm();
                Check(branch.Progress == saved, "Repeated Arm preserves progress", report);
                player.PlaceAt(branch.Point.transform.position + Vector3.right * 8f, Vector2.up);
                await Tap(keyboard);
                Check(branch.Progress == saved, "Out of range: E adds nothing", report);
                player.PlaceAt(branch.Point.transform.position, Vector2.up);
                FloodEveningHub.Paused = true;
                await Tap(keyboard);
                Check(branch.Progress == saved && !branch.CanUse, "Paused: E adds nothing", report);
                FloodEveningHub.Paused = false;
                await Wait(100);
                Check(branch.Progress == saved, "Leaving and returning preserves progress", report);

                var otherObject = new GameObject("OtherInteraction");
                otherObject.transform.position = branch.Point.transform.position + Vector3.right * 0.2f;
                var other = otherObject.AddComponent<Interactable>();
                other.Setup("Other", "", false, false);
                int otherUses = 0;
                interactor.Register(other, () => CountOther(() => otherUses++));
                await Tap(keyboard);
                Check(Mathf.Approximately(branch.Progress, 3f / 12f) && otherUses == 0,
                    "One E is routed only to the nearest interaction", report);
                other.SetAvailable(false);

                int completed = 0, opened = 0, closed = 0, pathLines = 0;
                FloodEveningHub.StepCompleted += id => { if (id == BranchObstacle.StepId) completed++; };
                FloodEveningHub.MinigameOpened += id => { if (id == BranchObstacle.MinigameId) opened++; };
                FloodEveningHub.MinigameClosed += id => { if (id == BranchObstacle.MinigameId) closed++; };
                FloodEveningHub.SayRequest += (id, fallback) => { if (id == "FE_PATH_OPEN") pathLines++; };
                for (int i = 0; i < 8; i++) await Tap(keyboard);
                Check(!branch.IsCleared && completed == 0 && Mathf.Approximately(branch.Progress, 11f / 12f),
                    "Eleven presses do not clear the obstacle", report);
                Keys(keyboard, Key.E);
                await Until(() => branch.IsOpen);
                Keys(keyboard);
                await Wait(50);
                Check(completed == 0 && collider.enabled && tree.localPosition != start,
                    "Final press starts slide; solid collider follows visible branch before completion", report);
                await Until(() => branch.IsCleared && !interactor.IsBusy);
                Check(Vector3.Distance(tree.localPosition, start + (Vector3)Get(branch, "clearOffset")) < 0.001f
                    && collider.enabled && !branch.Point.Available && completed == 1 && pathLines == 1 && opened == 1 && closed == 1,
                    "Slide completes at target with collider; one completion, one path line, balanced panel notifications", report);
                await Tap(keyboard);
                Check(completed == 1 && player.CanMove, "Cleared obstacle cannot retrigger; player regains movement", report);

                FloodEveningHub.RequestReset();
                Check(!branch.IsCleared && branch.Progress == 0 && !branch.CanUse && !tree.gameObject.activeSelf
                    && tree.localPosition == start, "Reset restores transform, progress and unarmed state", report);
                Set(branch, "requiredPresses", 1);
                branch.Arm();
                await Tap(keyboard);
                await Until(() => branch.IsCleared);
                Check(completed == 2, "Inspector press count is configurable (one press)", report);

                FloodEveningHub.RequestReset();
                branch.Arm();
                Keys(keyboard, Key.E);
                await Until(() => branch.IsOpen);
                Keys(keyboard);
                branch.enabled = false;
                await Until(() => !interactor.IsBusy);
                Check(opened == closed && player.CanMove, "Disable during slide releases notification and movement", report);
                branch.enabled = true;
                FloodEveningHub.RequestReset();
                Check(!branch.IsCleared && tree.localPosition == start, "Reset after interrupted slide restores branch", report);

                // Eski sayısal kipler ve basılı tutma yolu korunur; gerçek E-held ile iki kip denenir.
                foreach (BranchObstacle.Mode mode in new[] { BranchObstacle.Mode.AlternativePassage, BranchObstacle.Mode.SmallBranch })
                {
                    var obj = new GameObject("LegacyBranch");
                    obj.SetActive(false);
                    obj.AddComponent<Interactable>();
                    var legacy = obj.AddComponent<BranchObstacle>();
                    Set(legacy, "mode", mode);
                    Set(legacy, "startArmed", true);
                    Set(legacy, "doneSceneId", "");
                    obj.SetActive(true);
                    interactor.Register(legacy.Point, legacy.Play);
                    branch.Point.SetAvailable(false);
                    player.PlaceAt(legacy.Point.transform.position, Vector2.up);
                    await Until(() => interactor.Current == legacy.Point);
                    Keys(keyboard, Key.E);
                    await Until(() => legacy.IsCleared);
                    Keys(keyboard);
                    await Until(() => !interactor.IsBusy);
                    Check(legacy.IsCleared, "Legacy hold mode preserved: " + mode, report);
                    UnityEngine.Object.Destroy(obj);
                    await Wait(50);
                }
                report.AppendLine("UNTESTED: human visual QA at 480x270/1920x1080, actual walking through final map, Eren tree wiring, Berk final UI, menu/earthquake smoke, Windows build.");
                report.AppendLine("PASS: SEL3-20 PLAY MODE CHECKS COMPLETE");
                exitCode = 0;
            }
            catch (Exception ex) { report.AppendLine("FAIL: " + ex); Debug.LogException(ex); }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
                var testInputSettings = InputSystem.settings;
                // Input System kendi ürettiği varsayılan settings'i değiştirirken yok edebilir.
                InputSystem.settings = oldInputSettings != null ? oldInputSettings : ScriptableObject.CreateInstance<InputSettings>();
                if (testInputSettings != null && testInputSettings != InputSystem.settings) UnityEngine.Object.Destroy(testInputSettings);
                Application.runInBackground = oldBackground;
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/arda-sel-itme-checks.txt", report.ToString());
                Debug.Log("[Arda Sel Itme]\n" + report);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
                else EditorApplication.isPlaying = false;
            }
        }

        private static System.Collections.IEnumerator CountOther(Action count) { count(); yield break; }
        private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static void Keys(Keyboard keyboard, params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static async Task Tap(Keyboard keyboard)
        {
            Keys(keyboard, Key.E); await Wait(100);
            Keys(keyboard); await Wait(100);
        }
        private static async Task Wait(int milliseconds)
        {
            await Task.Delay(milliseconds);
            if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play Mode stopped");
        }
        private static async Task Until(Func<bool> condition)
        {
            double end = EditorApplication.timeSinceStartup + 8;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Play Mode condition timed out");
                await Wait(20);
            }
        }
        private static void Check(bool condition, string message, StringBuilder report)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.AppendLine("PASS: " + message);
        }
    }
}
