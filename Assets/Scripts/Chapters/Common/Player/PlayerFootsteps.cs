using AfetUyanisi.Chapters.Earthquake;
using AfetUyanisi.Chapters.FloodEvening;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Cenk'in ayak sesleri. Her sahne yüklenince IsoPlayerController'lara kendiliğinden eklenir (sahne/prefab değişmez).
    /// Liderin gerçek kayıtları "steps" kanalında döngü olarak çalar (GameAudio.PlayLoop), yalnız Cenk oyuncunun girdisiyle
    /// gerçekten yürürken; durunca, diyalog/sinematik/mini oyun/sarsıntıda (CanMove = false), duraklatmada ve sahne
    /// değişince kısa sönümle durur (StopLoop):
    ///  - İç mekân (deprem evi ve odaları): SFX_Footstep_Wood_Loop (ahşap zemin).
    ///  - Dış mekân (FloodSchool, FloodEvening, depremde toplanma alanı): SFX_Footstep_Outdoor_Loop (kaldırım).
    ///    Suda (FloodWaterField.PlayerDepth > 0,02, FloodWaterField'ın suda yürüme döngüsü çalarken) susar.
    ///    Dış kayıt yoksa yedek: SFX_Footstep_Wet_01..03 tek tek (aralık yürüme hızından, perde ±%5).
    /// Savrulma/itme (ExternalVelocity) ya da sinematikte yürütülme sessizdir.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerFootsteps : MonoBehaviour
    {
        public enum Surface { Auto, Indoor, Outdoor }

        [Tooltip("Auto: sahne adında 'Flood' varsa ya da deprem toplanma alanındaysa dış mekân, yoksa iç mekân")]
        [SerializeField] private Surface surface = Surface.Auto;
        [Header("Döngüler (liderin kayıtları)")]
        [SerializeField] private string indoorLoop = "SFX_Footstep_Wood_Loop";
        [SerializeField, Range(0f, 1f)] private float indoorVolume = 0.45f;
        [SerializeField] private string outdoorLoop = "SFX_Footstep_Outdoor_Loop";
        [SerializeField, Range(0f, 1f)] private float outdoorVolume = 0.45f;
        [SerializeField] private float loopFadeIn = 0.08f;
        [SerializeField] private float loopFadeOut = 0.12f;
        [Header("Yedek: dış kayıt yoksa tek adımlar")]
        [SerializeField] private string[] wetSteps = { "SFX_Footstep_Wet_01", "SFX_Footstep_Wet_02", "SFX_Footstep_Wet_03" };
        [SerializeField, Range(0f, 1f)] private float wetVolume = 0.35f;
        [SerializeField, Range(0f, 0.2f)] private float pitchJitter = 0.05f;
        [Tooltip("Normal yürüme hızında (referenceSpeed) iki adım arası (sn). 0,5: yürüme animasyonu (6 kare, 6 fps = 2 adım/sn) ve ahşap kaydı (~0,5 sn) ile aynı")]
        [SerializeField] private float baseInterval = 0.5f;
        [SerializeField] private float referenceSpeed = 2f;
        [Header("Koşullar")]
        [Tooltip("Bu zemin hızının altında adım sesi yok (birim/sn)")]
        [SerializeField] private float minGroundSpeed = 0.5f;
        [Tooltip("Su bu derinliğin üstündeyse adım sesi susar: FloodWaterField suda yürüme döngüsünü (SFX_FE_Wade_Loop) depth > 0.02 iken çalar")]
        [SerializeField] private float silentWaterDepth = 0.02f;
        [SerializeField] private bool logSteps = true;

        public const string LoopChannel = "steps";

        /// <summary>Test: yedek tek adım sayısı, döngünün kaç kez başladığı ve şu an çalan döngü (oyun boyunca).</summary>
        public static int WetStepsPlayed { get; private set; }
        public static int LoopStarts { get; private set; }
        public static bool LoopPlaying => CurrentLoop != null;
        public static string CurrentLoop { get; private set; }

        private IsoPlayerController player;
        private AudioSource source;
        private AudioClip[] wetClips;
        private bool outdoorLoopMissing;
        private FloodWaterField water;
        private EarthquakeModeController quake;
        private bool floodScene;
        private string looping;
        private float timer;
        private float lastWalk = -1f;
        private int lastIndex = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            WetStepsPlayed = 0; LoopStarts = 0; CurrentLoop = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var p in FindObjectsByType<IsoPlayerController>(FindObjectsInactive.Include))
                if (p.gameObject.scene == scene && p.GetComponent<PlayerFootsteps>() == null)
                    p.gameObject.AddComponent<PlayerFootsteps>();
        }

        private void Awake()
        {
            player = GetComponent<IsoPlayerController>();
            floodScene = (gameObject.scene.name ?? "").Contains("Flood");
        }

        private void Start()
        {
            water = FindAnyObjectByType<FloodWaterField>(FindObjectsInactive.Include);
            quake = FindAnyObjectByType<EarthquakeModeController>(FindObjectsInactive.Include);
            outdoorLoopMissing = GameAudio.LoadClip(outdoorLoop) == null;
            if (outdoorLoopMissing)
            {
                Debug.LogWarning($"[Adım] {outdoorLoop} yok; dış mekânda yedek tek adımlar çalar.", this);
                wetClips = new AudioClip[wetSteps.Length];
                for (int i = 0; i < wetSteps.Length; i++) wetClips[i] = GameAudio.LoadClip(wetSteps[i]);
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 160;
            }
        }

        private bool Outdoor()
        {
            if (surface != Surface.Auto) return surface == Surface.Outdoor;
            if (floodScene) return true;
            var room = quake != null ? quake.CurrentRoom : null;
            return room != null && room.Id == EarthquakeRoom.RoomId.Assembly;
        }

        private void Update()
        {
            if (player == null) return;
            bool canWalk = player.isActiveAndEnabled && player.CanMove && Time.timeScale > 0.01f && !GameInput.GameplayLocked;
            Vector2 v = player.Velocity;
            float ground = Mathf.Sqrt(v.x * v.x + 4f * v.y * v.y); // 2:1 izometrik zemin hızı
            bool walking = canWalk && GameInput.MoveVector.sqrMagnitude > 0.01f && ground >= minGroundSpeed;
            if (water != null && water.isActiveAndEnabled && water.PlayerDepth > silentWaterDepth) walking = canWalk = false;
            // dönüşte hız bir kare düşerse döngü kesilip baştan başlamasın: 0,1 sn tolerans (CanMove kapanınca hemen durur)
            if (walking) lastWalk = Time.unscaledTime;
            bool on = canWalk && lastWalk >= 0f && Time.unscaledTime - lastWalk < 0.1f;
            bool outdoor = Outdoor();

            if (outdoor && outdoorLoopMissing)
            {
                SetLoop(null);
                if (!walking) { timer = Mathf.Min(timer, 0.1f); return; }
                timer -= Time.deltaTime;
                if (timer > 0f) return;
                PlayWet();
                timer = baseInterval * Mathf.Clamp(referenceSpeed / Mathf.Max(0.01f, ground), 0.6f, 2.2f);
                return;
            }
            SetLoop(on ? (outdoor ? outdoorLoop : indoorLoop) : null);
        }

        private void SetLoop(string id)
        {
            if (id == looping) return;
            looping = id;
            CurrentLoop = id;
            if (id != null)
            {
                LoopStarts++;
                GameAudio.PlayLoop(id, LoopChannel, id == outdoorLoop ? outdoorVolume : indoorVolume, loopFadeIn);
                if (logSteps) Debug.Log($"[Adım] döngü başladı: {id} ({gameObject.scene.name})", this);
            }
            else
            {
                GameAudio.StopLoop(LoopChannel, loopFadeOut);
                if (logSteps) Debug.Log("[Adım] döngü durdu", this);
            }
        }

        private void PlayWet()
        {
            if (source == null || wetClips == null || wetClips.Length == 0) return;
            int n = wetClips.Length;
            int i = Random.Range(0, n);
            if (n > 1 && i == lastIndex) i = (i + 1 + Random.Range(0, n - 1)) % n;
            lastIndex = i;
            var clip = wetClips[i];
            if (clip == null) return;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clip, Mathf.Clamp01(wetVolume * GameAudio.SfxLevel));
            WetStepsPlayed++;
            if (logSteps && WetStepsPlayed % 10 == 1) Debug.Log($"[Adım] yedek tek adım #{WetStepsPlayed} ({clip.name})", this);
        }

        private void OnDisable()
        {
            if (looping != null) SetLoop(null);
        }
    }
}
