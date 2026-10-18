using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Chapters.Earthquake.Editor
{
    [InitializeOnLoad]
    public static class EarthquakeBalanceChecks
    {
        private const string Pending = "AfetUyanisi.BalanceChecks";
        private static bool running;
        static EarthquakeBalanceChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                RunPlayChecks();
            };
        }

        [MenuItem("Afet Uyanisi/Earthquake balance/Run model checks")]
        public static void RunModelChecks()
        {
            var report = new StringBuilder();
            try { Models(report); report.AppendLine("PASS: MODEL CHECKS COMPLETE"); }
            catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/earthquake-balance-model-checks.txt", report.ToString());
            Debug.Log(report);
        }

        private static void Models(StringBuilder report)
        {
            var noWave = new EarthquakeBalanceSettings { waveForce = 0 };
            var left = new EarthquakeBalanceModel(noWave); var right = new EarthquakeBalanceModel(noWave);
            left.Tick(0.3f, -1, true); right.Tick(0.3f, 1, true);
            Check(left.Position < 0 && right.Position > 0, "A sola, D sağa düzeltir", report);
            var slow = Simulate(1f / 30); var fast = Simulate(1f / 120);
            Check(Mathf.Abs(slow.Position - fast.Position) < 0.001f && Mathf.Abs(slow.Score - fast.Score) < 0.01f,
                "30/120 FPS sabit alt adımlı deterministik dalgalar", report);
            var stable = new EarthquakeBalanceModel(new EarthquakeBalanceSettings());
            while (!stable.Finished) stable.Tick(1f / 120, stable.AutoCorrection, true);
            Check(stable.Score >= 95 && stable.HeldRatio > 0.99f, "Tüm dalgalar dengelenebilir", report);
            var failed = new EarthquakeBalanceModel(new EarthquakeBalanceSettings());
            while (!failed.Finished) failed.Tick(1f / 60, 1, false);
            Check(failed.Position >= -1 && failed.Position <= 1 && failed.Score >= 0 && failed.Score <= 100,
                "Denge/puan sınırları ve düşük puanda ani bitiş olmaması", report);
            var pause = new EarthquakeBalanceModel(noWave); pause.Tick(0, 1, true);
            Check(pause.Elapsed == 0 && pause.Position == 0, "Pause sırasında fizik ilerlemez", report);
            pause.Tick(1, 0, false); float score = pause.Score; pause.Tick(1, 0, true);
            Check(pause.Score == score && pause.HeldRatio > 0.4f, "Space bırakma sonrası kayıp durur, toparlanma mümkün", report);

            var defaults = new EarthquakeBalanceSettings();
            var idle = new EarthquakeBalanceModel(defaults);
            float idleMax = 0;
            while (!idle.Finished) { idle.Tick(1f / 60, 0, true); idleMax = Mathf.Max(idleMax, Mathf.Abs(idle.Position)); }
            Check(idleMax > defaults.greenZone, $"Girdisiz kalınca sarsıntı işareti yeşilden çıkarır; A/D anlamlı (en fazla {idleMax:0.00})", report);
            var novice = Novice(defaults, 0.45f, 0.4f, out float noviceRed);
            Check(novice.WentWell && noviceRed < 0.5f && novice.Score >= 80,
                $"İlk kez oynayan (0.45 sn gecikmeli basit A/D) kırmızıya girmeden başarır: {novice.Score:0}/100, kırmızı {noviceRed:0.0} sn", report);
            var child = Novice(defaults, 0.6f, 0.5f, out float childRed);
            Check(child.WentWell && child.Score >= 70,
                $"Geç tepki veren çocuk oyuncu (0.6 sn) da başarır: {child.Score:0}/100, kırmızı {childRed:0.0} sn", report);
        }

        /// <summary>İnsan benzeri oyuncu: işareti gecikmeli görür, eşiği aşınca karşı tuşa basar, ortaya yaklaşınca bırakır.</summary>
        private static EarthquakeBalanceModel Novice(EarthquakeBalanceSettings settings, float delay, float threshold, out float redSeconds)
        {
            var model = new EarthquakeBalanceModel(settings);
            const float dt = 1f / 60;
            var seen = new System.Collections.Generic.Queue<float>();
            int lag = Mathf.Max(1, Mathf.RoundToInt(delay / dt));
            float input = 0; redSeconds = 0;
            while (!model.Finished)
            {
                seen.Enqueue(model.Position);
                float p = seen.Count > lag ? seen.Dequeue() : 0;
                if (p > threshold) input = -1; else if (p < -threshold) input = 1; else if (Mathf.Abs(p) < threshold * 0.5f) input = 0;
                model.Tick(dt, input, true);
                if (Mathf.Abs(model.Position) > settings.redThreshold) redSeconds += dt;
            }
            return model;
        }

        private static EarthquakeBalanceModel Simulate(float dt)
        {
            var model = new EarthquakeBalanceModel(new EarthquakeBalanceSettings());
            int guard = 10000;
            while (!model.Finished && guard-- > 0) model.Tick(dt, 0, true);
            if (guard <= 0) throw new InvalidOperationException("Model sonlanmadı.");
            return model;
        }

        [MenuItem("Afet Uyanisi/Earthquake balance/Run integrated Play Mode checks")]
        public static void StartPlayChecks()
        {
            if (running) return;
            if (EditorApplication.isPlaying) RunPlayChecks();
            else { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
        }

        private static async void RunPlayChecks()
        {
            if (running) return;
            running = true;
            var report = new StringBuilder();
            var oldKeyboard = Keyboard.current;
            Keyboard keyboard = null;
            float oldTimeScale = Time.timeScale;
            int quakeStarts = 0, timerExpirations = 0;
            Application.LogCallback logger = (message, trace, type) => { if (message.StartsWith("[EQ] SARSINTI başladı")) quakeStarts++; };
            Application.logMessageReceived += logger;
            EarthquakeEventController events = null;
            ProtectionMinigameController protection = null;
            Action restoreGameView = null;
            try
            {
                Models(report);
                restoreGameView = ReferenceResolution();
                await Until(() => Screen.width == 640 && Screen.height == 360, 5);
                Check(Screen.width == 640 && Screen.height == 360, "Game View native 640x360", report);
                GameSceneRouter.LoadChapter(ChapterId.Earthquake);
                await Until(() => SceneManager.GetActiveScene().name == "EarthquakeHome", 15);
                await Wait(100);
                var mode = UnityEngine.Object.FindAnyObjectByType<EarthquakeModeController>();
                Check(mode != null, "Gerçek EarthquakeHome sahnesi yüklendi", report);
                mode.StopAllCoroutines();
                foreach (var bubble in UnityEngine.Object.FindObjectsByType<AfetUyanisi.Dialogue.SpeechBubble>(FindObjectsInactive.Exclude)) bubble.Hide();
                var settings = new SerializedObject(mode);
                settings.FindProperty("debugAutoPlay").boolValue = true;
                settings.FindProperty("debugScenario").enumValueIndex = 0;
                settings.FindProperty("autoPlayUseViews").boolValue = false;
                settings.FindProperty("debugTimeScale").floatValue = 6;
                settings.FindProperty("autoNavigateAtEnd").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Invoke(mode, "ApplyAutoPlaySettings");
                UnityEngine.Object.FindAnyObjectByType<AfetUyanisi.Chapters.Rooms.RoomInteractor>().AutoAdvance = true;
                var timer = UnityEngine.Object.FindAnyObjectByType<EmergencyCountdownManager>();
                timer.OnTimerExpired += () => timerExpirations++;
                mode.StartCoroutine((IEnumerator)Invoke(mode, "Run"));
                protection = UnityEngine.Object.FindAnyObjectByType<ProtectionMinigameController>();
                events = UnityEngine.Object.FindAnyObjectByType<EarthquakeEventController>();
                await Until(() => protection.Balance != null && protection.Balance.Progress > 0.35f, 70);
                ScreenCapture.CaptureScreenshot("Logs/earthquake-balance-integrated.png");
                Check(!UnityEngine.Object.FindAnyObjectByType<AfetUyanisi.Chapters.Rooms.RoomInteractor>().InteractionEnabled,
                    "Sarsıntıda hazırlık etkileşimleri kapalı", report);
                await Until(() => mode.IsFinished, 60);
                Check(timerExpirations == 1 && quakeStarts == 1, "Hazırlık sayacı ve sarsıntı tam bir kez tetiklendi", report);
                Check(mode.Stats.protectionBalancePlayed && mode.Stats.protectionBalanceScore >= 35,
                    "Sayaç → gerçek denge oyunu → korunma sonucu", report);
                Check(mode.Stats.emergencyBagPrepared && mode.Stats.bagTaken, "Çanta hazırlığı sarsıntı boyunca korunup sonrasında alındı", report);
                Check(mode.LastReport != null && !events.IsShaking, "Sonrası ve rapor akışı tamamlandı, sarsıntı kapandı", report);

                Time.timeScale = 1;
                var player = UnityEngine.Object.FindAnyObjectByType<IsoPlayerController>();
                var table = protection.Tables[0];
                player.PlaceAt(table.ApproachPosition, Vector2.down);
                protection.Auto = ProtectionMinigameController.AutoInput.Manual;
                protection.BalanceSettings.initialSeconds = 1;
                protection.BalanceSettings.strongSeconds = 1;
                protection.BalanceSettings.irregularSeconds = 1;
                protection.BalanceSettings.waveForce = 0;
                float zoom = Camera.main.orthographicSize;
                var renderer = player.GetComponent<SpriteRenderer>(); bool rendererEnabled = renderer.enabled;
                keyboard = InputSystem.AddDevice<Keyboard>();
                ProtectionMinigameController.Result manualResult = null;
                int callbacks = 0;
                events.BeginShake();
                protection.StartCoroutine(protection.Run(player.transform.position, r => { manualResult = r; callbacks++; }));
                await Wait(150); Keys(keyboard, Key.E); await Wait(80); Keys(keyboard); await Wait(350);
                Keys(keyboard, Key.E); await Wait(80); Keys(keyboard); await Wait(700);
                var balanceTexts = (protection.Texts != null ? protection.Texts.balance : null) ?? new EqBalanceTexts();
                var hud = protection.View is ProtectionView protectionView ? protectionView.BalanceHUD : null;
                Check(protection.IsRunning && protection.Balance != null && !protection.Balance.Started,
                    "Talimat: SPACE'e basılmadan denge başlamaz", report);
                Check(hud != null && hud.IsShown && hud.StatusText == balanceTexts.controls && hud.HoldText == balanceTexts.startHint,
                    "Talimat ekranı: işareti yeşilde tut + SPACE'e bas ve basılı tut", report);
                Keys(keyboard, Key.Space, Key.A);
                await Until(() => protection.Balance != null && protection.Balance.Elapsed > 0.2f, 5);
                Check(protection.Balance.Velocity < 0, "Gerçek Input System: A + Space sola düzeltir", report);
                Keys(keyboard, Key.Space, Key.D); await Wait(400);
                Check(protection.Balance.Velocity > 0, "Gerçek Input System: D + Space sağa düzeltir", report);
                Vector3 protectedPosition = player.transform.position;
                Keys(keyboard); await Wait(450);
                Check(protection.IsRunning && player.transform.position == protectedPosition && !player.CanMove,
                    "Space bırakılınca oyuncu ayağa kalkmaz, yerinden çıkmaz; oyun sürer", report);
                Keys(keyboard, Key.Space);
                await Until(() => protection.Balance.Finished, 6);
                await Wait(100);
                Check(protection.IsRunning && (hud.StatusText == balanceTexts.resultGood || hud.StatusText == balanceTexts.resultHard),
                    "Bitişte sonuç metni görünür (iyi tutundun / zorlandın)", report);
                await Until(() => !protection.IsRunning, 6);
                events.EndShake(); await Wait(100);
                Check(callbacks == 1 && manualResult != null && manualResult.releasedDuringHold && manualResult.holdCompleted,
                    "Space yeniden tutularak toparlanır; tek sonuç üretir", report);
                protection.RestoreAfter();
                Check(Mathf.Approximately(Camera.main.orthographicSize, zoom) && renderer.enabled == rendererEnabled,
                    "Zoom ve karakter görünümü geri yüklendi", report);

                var results = UnityEngine.Object.FindAnyObjectByType<EarthquakeResultManager>();
                var under = new EarthquakeStats { protection = ProtectionOutcome.TableDropCoverHold };
                var inPlace = new EarthquakeStats { protection = ProtectionOutcome.InPlaceDropCover };
                Check(results.Build(under, mode.Data).score == results.Build(inPlace, mode.Data).score,
                    "Masadan uzakta yerinde güvenli korunmaya puan cezası yok", report);
                Keys(keyboard);
                report.AppendLine("PASS: INTEGRATED PLAY MODE CHECKS COMPLETE");
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL: " + exception); Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= logger;
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
                if (protection != null) { protection.StopAllCoroutines(); protection.RestoreAfter(); }
                if (events != null) events.EndShake();
                restoreGameView?.Invoke();
                Time.timeScale = oldTimeScale;
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/earthquake-balance-play-checks.txt", report.ToString());
                Debug.Log("[Earthquake Balance Checks]\n" + report);
                running = false;
            }
        }

        private static object Invoke(object owner, string name) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);

        // Yalnızca QA: geçici sabit çözünürlük, bitince önceki Game View seçimi geri gelir.
        private static Action ReferenceResolution()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            var entry = Activator.CreateInstance(sizeType, new[] { Enum.Parse(kindType, "FixedResolution"), (object)640, 360, "AFET QA temporary" });
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            var selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int previous = (int)selected.GetValue(view);
            int builtin = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            int custom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { entry });
            selected.SetValue(view, builtin + custom);
            view.Show(); view.Focus(); view.Repaint();
            return () =>
            {
                if (view != null) selected.SetValue(view, previous);
                // Unity expects the absolute index, including the built-in entries.
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtin + custom });
            };
        }
        private static void Keys(Keyboard keyboard, params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static async Task Wait(int milliseconds)
        {
            await Task.Delay(milliseconds);
            if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play Mode durduruldu.");
        }
        private static async Task Until(Func<bool> condition, float seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Play Mode adımı zaman aşımına uğradı.");
                await Wait(50);
            }
        }
        private static void Check(bool condition, string label, StringBuilder report)
        {
            if (!condition) throw new InvalidOperationException(label);
            report.AppendLine("PASS: " + label);
        }
    }
}
