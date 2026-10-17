using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Earthquake;
using AfetUyanisi.Chapters.Earthquake.Mode;
using AfetUyanisi.Chapters.Flood;
using AfetUyanisi.Chapters.Objectives;
using AfetUyanisi.Chapters.Rooms;
using AfetUyanisi.Dialogue;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// Sahne yüklenince oyunun public API'sine (olaylar, özellikler, View setter'ları) bağlanıp sesleri çalar.
    /// Sahne/prefab düzenlemez, mevcut kodu değiştirmez; private alanlara reflection ile erişmez.
    /// Tarama: sahne yüklenince, bir kare sonra ve sonra 1,5 sn'de bir (sonradan kurulan UI için). Bağlanamayanlar AUDIO_LIST.md'de.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioAutoWiring : MonoBehaviour
    {
        private sealed class RainState { public RainController Source; public bool Downpour; }
        private sealed class TimerState { public EmergencyCountdownManager Source; public int Whole = -1; public float Remaining; }
        private sealed class MarkState { public DecisionChoiceView View; public Text[] Texts; }

        private const float RescanSeconds = 1.5f;

        private readonly HashSet<UnityEngine.Object> bound = new HashSet<UnityEngine.Object>();
        private readonly List<Action> unbind = new List<Action>();
        private readonly List<AlarmClock> alarms = new List<AlarmClock>();
        private readonly List<PhoneAlertController> phones = new List<PhoneAlertController>();
        private readonly List<RainState> rains = new List<RainState>();
        private readonly List<EarthquakeEventController> quakes = new List<EarthquakeEventController>();
        private readonly List<EarthquakeModeController> modes = new List<EarthquakeModeController>();
        private readonly List<TimerState> timers = new List<TimerState>();
        private readonly List<PreparationQuestManager> questManagers = new List<PreparationQuestManager>();
        private readonly List<PreparationQuestHUD> questHuds = new List<PreparationQuestHUD>();
        private readonly List<BusWindowScroller> busInteriors = new List<BusWindowScroller>();
        private readonly List<FloodResultView> floodResults = new List<FloodResultView>();
        private readonly List<MarkState> marks = new List<MarkState>();
        private readonly Dictionary<StoveFireController, StoveFireController.StoveState> stoves =
            new Dictionary<StoveFireController, StoveFireController.StoveState>();
        private readonly List<ProtectionMinigameController> protections = new List<ProtectionMinigameController>();
        private EarthquakeBalanceModel balance;
        private bool balanceRed, balanceDone;
        private float nextBalanceWarnAt;

        private GameAudioRuntime runtime;
        private Scene audibleScene;
        private Coroutine deferredScan;
        private Coroutine quakeDebris;
        private float nextRescanAt;
        private bool alarmPlaying, phonePlaying, quakePlaying;
        private bool floodResultShown, markShown;
        private EarthquakePhase lastPhase = EarthquakePhase.Intro;
        private bool hasPhase;
        private float lastListenerVolume = -1f;
        private bool pendingComplete, pendingBonus, pendingBadge;
        private string musicId, ambienceId;
        private float musicVolume = -1f, ambienceVolume = -1f;
        private float nextSplashAt;

        private void Awake() => runtime = GetComponent<GameAudioRuntime>();

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            PuddleHazard.Triggered += OnPuddle;
            EarthquakeMinigameHub.TaskCompleted += OnHubTaskCompleted;
        }

        private void Start() => BindScene(SceneManager.GetActiveScene());

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            PuddleHazard.Triggered -= OnPuddle;
            EarthquakeMinigameHub.TaskCompleted -= OnHubTaskCompleted;
            ClearBindings();
            GameAudio.StopTransientAudio();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene()) BindScene(scene);
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) => BindScene(newScene);

        private void OnSceneUnloaded(Scene scene)
        {
            // Müzik/ortam burada durdurulmaz: yeni sahne aynı parçayı isterse kesintisiz sürer, farklıysa crossfade yapılır.
            if (scene != audibleScene) return;
            ClearBindings();
            GameAudio.StopTransientAudio();
        }

        private void BindScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (audibleScene == scene && bound.Count > 0) { Scan(scene); return; }
            ClearBindings();
            GameAudio.StopTransientAudio();
            audibleScene = scene;
            alarmPlaying = phonePlaying = quakePlaying = false;
            floodResultShown = markShown = false;
            hasPhase = false;
            lastListenerVolume = AudioListener.volume;
            pendingComplete = pendingBonus = pendingBadge = false;
            nextSplashAt = 0f;
            Scan(scene);
            if (runtime != null) runtime.EnsureListener();
            ApplySoundscape();
            // Bazı kontrolcüler bağımlılıklarını Start'ta kurar; bir kare sonra bir kez daha tara.
            deferredScan = StartCoroutine(ScanNextFrame(scene));
        }

        private IEnumerator ScanNextFrame(Scene scene)
        {
            yield return null;
            if (scene == audibleScene && scene.isLoaded)
            {
                Scan(scene);
                if (runtime != null) runtime.EnsureListener();
            }
            nextRescanAt = Time.unscaledTime + RescanSeconds;
            deferredScan = null;
        }

        private void ClearBindings()
        {
            if (deferredScan != null) StopCoroutine(deferredScan);
            if (quakeDebris != null) StopCoroutine(quakeDebris);
            deferredScan = quakeDebris = null;
            foreach (var cleanup in unbind)
            {
                try { cleanup(); } catch (Exception) { /* nesne sahneyle yok olduysa sorun değil */ }
            }
            unbind.Clear(); bound.Clear(); alarms.Clear(); phones.Clear(); rains.Clear();
            quakes.Clear(); modes.Clear(); timers.Clear(); questManagers.Clear(); questHuds.Clear();
            busInteriors.Clear(); floodResults.Clear(); marks.Clear();
            stoves.Clear(); protections.Clear(); balance = null;
        }

        private static IEnumerable<T> InScene<T>(Scene scene) where T : Component
        {
            foreach (var component in FindObjectsByType<T>(FindObjectsInactive.Include))
                if (component != null && component.gameObject.scene == scene) yield return component;
        }

        /// <summary>Sonradan UI kuran bir sahip çağırabilir; normalde 1,5 sn'lik tarama yeterlidir.</summary>
        public void RefreshSceneBindings() => Scan(audibleScene);

        // ------------------------------------------------------------------ tarama

        private void Scan(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            // --- arayüz ---
            foreach (var button in InScene<Button>(scene))
            {
                if (!bound.Add(button) || button.GetComponent<AudioUiSoundHooks>() != null) continue;
                var hooks = button.gameObject.AddComponent<AudioUiSoundHooks>();
                unbind.Add(() => { if (hooks != null) Destroy(hooks); });
            }
            foreach (var eq in InScene<EqButton>(scene))
            {
                if (!bound.Add(eq)) continue;
                var target = eq;
                Action click = () => GameAudio.PlaySfx("SFX_UI_Click", 0.5f);
                Action<bool> hover = on => { if (on && target != null && target.Interactable) GameAudio.PlaySfx("SFX_UI_Hover", 0.35f); };
                eq.Clicked += click;
                eq.HoverChanged += hover;
                unbind.Add(() => { if (target != null) { target.Clicked -= click; target.HoverChanged -= hover; } });
            }
            foreach (var choice in InScene<DecisionChoiceView>(scene))
            {
                if (!bound.Add(choice)) continue;
                marks.Add(new MarkState { View = choice, Texts = choice.GetComponentsInChildren<Text>(true) });
            }

            // --- Sel ---
            foreach (var clock in InScene<AlarmClock>(scene)) if (bound.Add(clock)) alarms.Add(clock);
            foreach (var rain in InScene<RainController>(scene))
            {
                if (!bound.Add(rain)) continue;
                var state = new RainState { Source = rain, Downpour = rain.TargetIntensity >= 0.9f };
                rains.Add(state);
                Listen(rain.OnRainStarted, () => { state.Downpour = false; });
                Listen(rain.OnDownpourStarted, () => { state.Downpour = true; });
                Listen(rain.OnRainStopped, () => { state.Downpour = false; });
            }
            foreach (var bus in InScene<BusController>(scene))
            {
                if (!bound.Add(bus)) continue;
                Listen(bus.OnArriving, () => GameAudio.PlaySfx("SFX_Bus_Arrive", 0.55f));
                Listen(bus.OnDoorsOpened, () => GameAudio.PlaySfx("SFX_Bus_Door", 0.7f));
                Listen(bus.OnDoorsClosed, () => GameAudio.PlaySfx("SFX_Bus_Door", 0.7f));
                Listen(bus.OnDeparting, () => GameAudio.PlaySfx("SFX_Bus_Depart", 0.55f));
            }
            foreach (var scroller in InScene<BusWindowScroller>(scene)) if (bound.Add(scroller)) busInteriors.Add(scroller);
            foreach (var result in InScene<FloodResultView>(scene)) if (bound.Add(result)) floodResults.Add(result);
            foreach (var source in InScene<AudioSource>(scene))
            {
                // FloodChapterController.PlayTvJingle mutfaktaki "TV" nesnesinin AudioSource'unu klibi varsa çalar.
                if (source.gameObject.name != "TV" || source.clip != null || !bound.Add(source)) continue;
                var clip = GameAudio.LoadClip("SFX_TV_NewsJingle");
                if (clip == null) continue;
                source.clip = clip;
                source.loop = false;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0.6f * GameAudio.SfxLevel;
            }

            // --- görev paneli (ObjectiveHudView: Sel ve odalar) ---
            foreach (var hud in InScene<ObjectiveHudView>(scene))
            {
                if (!bound.Add(hud)) continue;
                Listen(hud.OnObjectiveCompleted, _ => pendingComplete = true);
                Listen(hud.OnBonusCompleted, () => pendingBonus = true);
                Listen(hud.OnGroupCompleted, () => pendingBadge = true);
                Listen(hud.OnWarned, () => GameAudio.PlaySfx("SFX_Wrong", 0.45f));
                Listen(hud.OnLesson, () => GameAudio.PlaySfx("SFX_Wrong", 0.45f));
            }

            // --- Deprem ---
            foreach (var phone in InScene<PhoneAlertController>(scene))
            {
                if (bound.Add(phone)) phones.Add(phone);
                if (phone.View == null || phone.View is AudioPhoneAlertView) continue;
                var original = phone.View;
                var wrapper = phone.gameObject.AddComponent<AudioPhoneAlertView>();
                wrapper.Inner = original;
                phone.View = wrapper;
                var owner = phone;
                unbind.Add(() =>
                {
                    if (owner != null && owner.View == wrapper) owner.View = original;
                    if (wrapper != null) Destroy(wrapper);
                });
            }
            foreach (var decision in InScene<TimedDecisionController>(scene))
            {
                bound.Add(decision);
                if (decision.View == null || decision.View is AudioTimedQuestionView) continue;
                var original = decision.View;
                var wrapper = decision.gameObject.AddComponent<AudioTimedQuestionView>();
                wrapper.Inner = original;
                decision.View = wrapper;
                var owner = decision;
                unbind.Add(() =>
                {
                    if (owner != null && owner.View == wrapper) owner.View = original;
                    if (wrapper != null) Destroy(wrapper);
                });
            }
            foreach (var stove in InScene<StoveFireController>(scene)) if (bound.Add(stove)) stoves[stove] = stove.State;
            foreach (var protection in InScene<ProtectionMinigameController>(scene)) if (bound.Add(protection)) protections.Add(protection);
            foreach (var quake in InScene<EarthquakeEventController>(scene)) if (bound.Add(quake)) quakes.Add(quake);
            foreach (var mode in InScene<EarthquakeModeController>(scene)) if (bound.Add(mode)) modes.Add(mode);
            foreach (var hud in InScene<PreparationQuestHUD>(scene)) if (bound.Add(hud)) questHuds.Add(hud);
            foreach (var quests in InScene<PreparationQuestManager>(scene))
            {
                if (!bound.Add(quests)) continue;
                questManagers.Add(quests);
                var owner = quests;
                Action<string> handler = _ =>
                {
                    // PreparationQuestHUD kendi tamamlama sesini çalıyor; çift ses olmasın.
                    if (questHuds.Count == 0) GameAudio.PlaySfx("SFX_Objective_Complete", 0.7f);
                };
                quests.TaskCompleted += handler;
                unbind.Add(() => { if (owner != null) owner.TaskCompleted -= handler; });
            }
            foreach (var timer in InScene<EmergencyCountdownManager>(scene))
            {
                if (!bound.Add(timer)) continue;
                timers.Add(new TimerState { Source = timer, Remaining = timer.GetRemainingTime() });
                var owner = timer;
                Action<float, string> penalty = (seconds, reason) => { if (seconds > 0f) GameAudio.PlaySfx("SFX_Penalty", 0.6f); };
                Action<float, string> bonus = (seconds, reason) => { if (seconds > 0f) GameAudio.PlaySfx("SFX_Objective_Bonus", 0.6f); };
                timer.OnPenalty += penalty;
                timer.OnBonus += bonus;
                unbind.Add(() => { if (owner != null) { owner.OnPenalty -= penalty; owner.OnBonus -= bonus; } });
            }
        }

        private void Listen(UnityEvent signal, UnityAction action)
        {
            if (signal == null) return;
            signal.AddListener(action);
            unbind.Add(() => signal.RemoveListener(action));
        }

        private void Listen<T>(UnityEvent<T> signal, UnityAction<T> action)
        {
            if (signal == null) return;
            signal.AddListener(action);
            unbind.Add(() => signal.RemoveListener(action));
        }

        // ------------------------------------------------------------------ kare başı (yalnız önbellekteki listeler)

        private void LateUpdate()
        {
            if (deferredScan == null && Time.unscaledTime >= nextRescanAt)
            {
                nextRescanAt = Time.unscaledTime + RescanSeconds;
                Scan(audibleScene);
            }

            bool alarm = alarms.Exists(x => x != null && x.isActiveAndEnabled && x.IsRinging);
            if (alarm != alarmPlaying)
            {
                alarmPlaying = alarm;
                if (alarm) GameAudio.PlayLoop("SFX_Alarm_Clock", "alarm", 0.55f, 0.04f);
                else GameAudio.StopLoop("alarm", 0.1f);
            }
            bool phone = phones.Exists(x => x != null && x.isActiveAndEnabled && x.IsRinging);
            if (phone != phonePlaying)
            {
                phonePlaying = phone;
                if (phone) GameAudio.PlayLoop("SFX_Phone_Ring", "phone", 0.55f, 0.05f);
                else GameAudio.StopLoop("phone", 0.15f);
            }
            bool quake = quakes.Exists(x => x != null && x.isActiveAndEnabled && x.IsShaking);
            if (quake != quakePlaying)
            {
                quakePlaying = quake;
                if (quake)
                {
                    GameAudio.PlayLoop("SFX_Quake_Rumble", "quake", 0.9f, 0.4f);
                    GameAudio.PlaySfx("SFX_Quake_ObjectsRattle", 0.9f);
                    if (quakeDebris != null) StopCoroutine(quakeDebris);
                    quakeDebris = StartCoroutine(QuakeDebris());
                }
                else GameAudio.StopLoop("quake", 1.0f);
            }
            foreach (var timer in timers) UpdateTick(timer);
            UpdateStoves();
            UpdateBalance();
            UpdateDecisionMarks();
            UpdateResults();
            UpdateMenu();

            // Aynı karede gelen görev olaylarından tek ses: rozet > bonus > görev.
            if (pendingBadge) GameAudio.PlaySfx("SFX_Badge", 0.8f);
            else if (pendingBonus) GameAudio.PlaySfx("SFX_Objective_Bonus", 0.75f);
            else if (pendingComplete) GameAudio.PlaySfx("SFX_Objective_Complete", 0.7f);
            pendingBadge = pendingBonus = pendingComplete = false;

            ApplySoundscape();
        }

        // Kitap rafı ve cam: sarsıntı başladıktan kısa süre sonra (ShakeableProp Fall varsayılanı 1,5 sn).
        private IEnumerator QuakeDebris()
        {
            yield return new WaitForSeconds(1.5f);
            if (quakePlaying) GameAudio.PlaySfx("SFX_Books_Fall", 1f);
            yield return new WaitForSeconds(0.8f);
            if (quakePlaying) GameAudio.PlaySfx("SFX_Glass_Crack", 0.6f);
            quakeDebris = null;
        }

        private static void UpdateTick(TimerState state)
        {
            var timer = state.Source;
            if (timer == null || !timer.isActiveAndEnabled) return;
            float remaining = timer.GetRemainingTime();
            int whole = Mathf.CeilToInt(remaining);
            bool running = timer.IsRunning && !timer.IsPaused && !timer.HasExpired;
            // Ceza/hızlandırma sıçramasında kaçırılan saniyeleri peş peşe çalma.
            bool naturalStep = state.Remaining - remaining < 1.5f && remaining <= state.Remaining;
            if (running && whole > 0 && whole <= 10 && whole != state.Whole && naturalStep)
                GameAudio.PlaySfx("SFX_Countdown_Tick", 0.45f);
            state.Whole = whole;
            state.Remaining = remaining;
        }

        // Ocak düğmesi çevrilince (On → TurningOff) düğme tıkı; görev sesi PreparationQuestHUD'dan gelir.
        private readonly List<StoveFireController> stoveKeys = new List<StoveFireController>();
        private void UpdateStoves()
        {
            if (stoves.Count == 0) return;
            stoveKeys.Clear();
            stoveKeys.AddRange(stoves.Keys);
            foreach (var stove in stoveKeys)
            {
                if (stove == null) continue;
                var state = stove.State;
                if (state != stoves[stove] && state == StoveFireController.StoveState.TurningOff)
                    GameAudio.PlaySfx("SFX_UI_Click", 0.7f);
                stoves[stove] = state;
            }
        }

        // Sarsıntıda denge alıştırması: kırmızı bölgeye girince nazik uyarı (en çok 1 sn'de bir), bitince olumlu ses.
        private void UpdateBalance()
        {
            if (protections.Count == 0) return;
            EarthquakeBalanceModel current = null;
            foreach (var p in protections)
                if (p != null && p.Balance != null) { current = p.Balance; break; }
            if (current != balance)
            {
                balance = current;
                balanceRed = false;
                balanceDone = current != null && current.Finished;
            }
            if (balance == null) return;
            bool red = Mathf.Abs(balance.Position) >= 0.65f; // EarthquakeBalanceSettings.redThreshold varsayılanı
            if (red && !balanceRed && Time.unscaledTime >= nextBalanceWarnAt)
            {
                nextBalanceWarnAt = Time.unscaledTime + 1f;
                GameAudio.PlaySfx("SFX_Wrong", 0.35f);
            }
            balanceRed = red;
            if (balance.Finished && !balanceDone) GameAudio.PlaySfx("SFX_Correct", 0.6f);
            balanceDone = balance.Finished;
        }

        // Sel kararları (DialogueRunner): kartlarda ✓/✗ işareti göründüğü an doğru/yanlış sesi.
        private void UpdateDecisionMarks()
        {
            if (marks.Count == 0) return;
            bool wrong = false, right = false;
            foreach (var mark in marks)
            {
                if (mark.View == null || !mark.View.isActiveAndEnabled) continue;
                foreach (var text in mark.Texts)
                {
                    if (text == null || !text.gameObject.activeInHierarchy) continue;
                    if (text.text == "✗") wrong = true;
                    else if (text.text == "✓") right = true;
                }
            }
            bool shown = wrong || right;
            if (shown && !markShown) GameAudio.PlaySfx(wrong ? "SFX_Wrong" : "SFX_Correct", wrong ? 0.6f : 0.7f);
            markShown = shown;
        }

        private void UpdateResults()
        {
            bool flood = floodResults.Exists(x => x != null && x.IsShown);
            if (flood && !floodResultShown) pendingBadge = true;
            floodResultShown = flood;

            var mode = ActiveMode();
            if (mode == null) return;
            if (hasPhase && mode.Phase != lastPhase && mode.Phase == EarthquakePhase.Report) pendingBadge = true;
            lastPhase = mode.Phase;
            hasPhase = true;
        }

        // Menüye özel sınıflara bağlanmaz (menü değişebilir): butonlar genel Button kancasıyla ses verir.
        private void UpdateMenu()
        {
            if (!audibleScene.IsValid() || audibleScene.name != "MainMenu") return;
            // Ses ayarı değişince yeni seviyede kısa bir tık: oyuncu ne kadar yüksek olduğunu duyar.
            if (!Mathf.Approximately(AudioListener.volume, lastListenerVolume)) GameAudio.PlaySfx("SFX_UI_Click", 0.5f);
            lastListenerVolume = AudioListener.volume;
            if (GameInput.BackPressed) GameAudio.PlaySfx("SFX_UI_Back", 0.5f);
        }

        private void OnPuddle(PuddleHazard hazard)
        {
            if (hazard == null || hazard.gameObject.scene != audibleScene || Time.unscaledTime < nextSplashAt) return;
            nextSplashAt = Time.unscaledTime + 0.4f;
            GameAudio.PlaySfx("SFX_Water_Splash", 1f);
        }

        private void OnHubTaskCompleted(string taskId)
        {
            // Normalde PreparationQuestManager bu olayı görev tamamlamaya çevirir (sesi orada); yöneticisiz test sahnesi için.
            if (questManagers.Count == 0 && questHuds.Count == 0) GameAudio.PlaySfx("SFX_Objective_Complete", 0.7f);
        }

        private EarthquakeModeController ActiveMode() => modes.Find(x => x != null && x.isActiveAndEnabled);

        // ------------------------------------------------------------------ müzik + ortam

        private void ApplySoundscape()
        {
            string wantedMusic = null, wantedAmbience = null;
            float music = 1f, ambience = 1f;
            string scene = audibleScene.IsValid() ? audibleScene.name : "";

            // Menü: müzik alçak (~0.35); oda/yağmur ortamını menünün kendi efektleri çalıyor, burada ortam yok.
            if (scene == "MainMenu") { wantedMusic = "MUS_Menu_Loop"; music = 0.7f; }
            else if (scene == "Bedroom" || scene == "Bathroom" || scene == "FloodChapter" ||
                     scene == "EarthquakeHome" || scene == "EarthquakeChapter")
            {
                wantedMusic = "MUS_Calm_Loop";
                wantedAmbience = "AMB_Room_Morning_Loop";
            }

            // Akşam sel bölümü: uyarı okunup süre başlayınca gerilim müziği, ilerledikçe hafif yükselir; yağmur ortamı RainController'dan.
            if (scene == AfetUyanisi.Core.GameSceneRouter.FloodScene)
            {
                float p = AfetUyanisi.Chapters.FloodEvening.FloodEveningHub.Progress01;
                bool active = p > 0f || AfetUyanisi.Chapters.FloodEvening.FloodEveningHub.FreeRoam;
                wantedMusic = active ? "MUS_Tension_Loop" : "MUS_Calm_Loop";
                music = active ? Mathf.Round(Mathf.Lerp(0.55f, 0.9f, p) * 10f) / 10f : 0.5f;
                wantedAmbience = "AMB_Rain_Light_Loop";
                ambience = 0.6f;
            }

            var mode = ActiveMode();
            if (mode != null)
            {
                wantedMusic = "MUS_Calm_Loop";
                wantedAmbience = "AMB_Room_Morning_Loop";
                switch (mode.Phase)
                {
                    case EarthquakePhase.Prep: wantedMusic = "MUS_Tension_Loop"; music = 0.9f; break;
                    case EarthquakePhase.Quake: wantedMusic = "MUS_Tension_Loop"; music = 0.3f; wantedAmbience = null; break;
                    case EarthquakePhase.After: music = 0.7f; break;
                }
            }
            if (quakePlaying) { music = Mathf.Min(music, 0.3f); wantedAmbience = null; }

            // Otobüs içi: dışarıda yağmur, hafif.
            if (busInteriors.Exists(x => x != null && x.isActiveAndEnabled)) { wantedAmbience = "AMB_Rain_Light_Loop"; ambience = 0.45f; }

            foreach (var state in rains)
            {
                if (state.Source == null || !state.Source.isActiveAndEnabled || !state.Source.IsRaining) continue;
                bool heavy = state.Downpour || state.Source.TargetIntensity >= 0.9f;
                wantedAmbience = heavy ? "AMB_Rain_Heavy_Loop" : "AMB_Rain_Light_Loop";
                ambience = heavy ? 1f : Mathf.Round(Mathf.Lerp(0.4f, 0.8f, state.Source.Intensity / 0.9f) * 10f) / 10f;
                if (heavy) { music = Mathf.Min(music, 0.6f); break; }
            }

            if (wantedMusic != musicId || !Mathf.Approximately(music, musicVolume))
            {
                musicId = wantedMusic; musicVolume = music;
                if (wantedMusic == null) GameAudio.StopMusic(0.8f);
                else GameAudio.PlayMusic(wantedMusic, music, 0.8f);
            }
            if (wantedAmbience != ambienceId || !Mathf.Approximately(ambience, ambienceVolume))
            {
                ambienceId = wantedAmbience; ambienceVolume = ambience;
                if (wantedAmbience == null) GameAudio.StopAmbience(0.8f);
                else GameAudio.PlayAmbience(wantedAmbience, ambience, 0.8f);
            }
        }
    }
}
