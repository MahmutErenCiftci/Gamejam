using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Chapters.FloodEvening;
using AfetUyanisi.Core;
using AfetUyanisi.Cutscenes;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// SEL bölümünün başlangıcı — OKUL ÇIKIŞI (Assets/Scenes/Chapters/Flood/FloodSchool.unity, Mahmut).
    /// Eski sabah akışının okul yolu (SchoolRoute.prefab: okul, su basmış Okul Caddesi, köprü üstü yaya yolu, öğretmen)
    /// ters yönde oynanır: "SEL BÖLÜMÜ" kartı → Cenk okul kapısında, öğretmen uğurlar → "Okuldan çık" →
    /// "Köprü bölgesine ilerle" → köprü başında Cenk su basmış kestirmeye bakıp düşünür: sudan mı, köprüden mi
    /// (karar FS_D1, Cenk'in kendi düşüncesi; yanında kimse yok) → "Köprüyü geç"
    /// (akıntı köprünün altından akar, FloodCurrentFlow) → karşı kaldırımda "Mevcut sel güzergâhına bağlan" → Cenk yürüyerek
    /// kadrajdan çıkar, kararma → FloodEvening (aynı yağmur, aynı yön; FloodEvening kendi kartını göstermez).
    /// Yanlış seçimde (FS_D1_A "Kestirmeden, akan suyun içinden geç") Eren'in sele kapılma ara sahnesi (FloodSweepCutscene) BİR KEZ
    /// oynar, ardından Cenk köprü başındaki güvenli noktada uyarı repliğini söyler ve karar yeniden sorulur (ölüm yok,
    /// ara sahne tekrar etmez). Okul bölümünde süre/başarısızlık yoktur; 120 sn sel sayacı ve telefon uyarısı
    /// FloodEvening'de, Cenk sokağa vardıktan sonra başlar (OYUN_AKISI §6.0).
    /// Sudan uzak durma: karşı kaldırımın su kenarları (PuddleHazard, F07_WATER_WARNING) Cenk'i durdurup geri iter.
    /// Sahne çapaları adla bulunur (Zones altında): SchoolGate (okul kapısının içi), BridgeHead, BridgeMid, BridgeEnd, ExitZone, SafeSpot,
    /// ShortcutView (karar anında Cenk'in baktığı su basmış kestirme), Teacher; isteğe bağlı "Sign" (Interactable) bonus tabela.
    /// </summary>
    public class FloodSchoolController : ChapterControllerBase
    {
        /// <summary>Sahne adı (GameSceneRouter.FloodSchoolScene ile aynı).</summary>
        public const string SceneName = "FloodSchool";

        [Header("Açılış kartı (FloodEvening ile aynı kart ve metin)")]
        [SerializeField] private FloodEveningTitleCard titleCard;
        [Tooltip("Assets/Data/Story/flood_evening.json — kart başlığı/alt yazısı (FE_TITLE) ve dipnot (ui.card_note) tek kaynaktan")]
        [SerializeField] private TextAsset floodEveningJson;

        [Header("Hava")]
        [SerializeField] private RainController rain;
        [Tooltip("Okul çıkışında yağmur şiddeti (0..1); FloodEvening aynı şiddetle devam eder")]
        [SerializeField] private float rainIntensity = 0.3f;

        [Header("Köprü ve karar")]
        [Tooltip("Yanlış seçimde (FS_D1_A) bir kez oynayan sele kapılma ara sahnesi (Prefabs/Cutscenes/FloodSweepCutscene; " +
                 "dünyadan uzakta, kendi kamerası var). Boşsa atlanır.")]
        [SerializeField] private FloodSweepCutscene sweepCutscene;
        [SerializeField] private string decisionId = "FS_D1";
        [Tooltip("Ara sahneden sonra Cenk'in konduğu güvenli nokta (köprü başı; boşsa 'SafeSpot')")]
        [SerializeField] private Transform safeSpot;
        [SerializeField] private Vector2 safeFacing = new Vector2(1f, -0.5f);
        [Tooltip("Karar anında Cenk'in baktığı nokta: su basmış kestirme (boşsa 'ShortcutView'; o da yoksa yön değişmez)")]
        [SerializeField] private Transform shortcutView;
        [Tooltip("Aynı su kenarı uyarısı en az kaç saniyede bir tekrar edebilir")]
        [SerializeField] private float warningCooldown = 5f;
        [Tooltip("Tabela incelenince açılan büyük tabela (FloodSignOverlay, normalde kapalı)")]
        [SerializeField] private GameObject signOverlay;

        [Header("Çıkış ve geçiş")]
        [Tooltip("Okul bölümünden sonra açılacak sahne")]
        [SerializeField] private string nextScene = GameSceneRouter.FloodScene;
        [Tooltip("Çıkışta Cenk'in yürüdüğü yön (dünya; FloodEvening'deki Spawn yönüyle aynı: sağ yukarı)")]
        [SerializeField] private Vector2 exitDirection = new Vector2(1f, 0.5f);
        [SerializeField] private float exitWalkSpeed = 1.5f;
        [SerializeField] private float exitWalkSeconds = 1.6f;
        [SerializeField] private float exitFadeSeconds = 1.1f;

        // Çapalar
        private CameraBounds cameraBounds;
        private Collider2D gateZone, bridgeHead, bridgeMid, bridgeEnd, exitZone;
        private GameObject teacher;
        private Interactable sign;

        // Durum
        private readonly DecisionResult d1Result = new DecisionResult();
        private readonly Dictionary<string, float> lastWarningTime = new Dictionary<string, float>();
        private string pendingWarning;
        private bool walking;
        private bool swept;
        private bool bridgeLineSaid;
        private bool signRead;
        private float startedAt;

        protected override ChapterId Chapter => ChapterId.Flood;

        // Görev paneli: Resources/Objectives/objectives_flood_school.json (ObjectiveTracker sahne adından seçer).
        protected override bool UsesObjectives => true;

        /// <summary>Testler için: yanlış seçimde ara sahne oynadı mı.</summary>
        public bool Swept => swept;

        /// <summary>Testler için: okul bölümü bitti, sonraki sahne yükleniyor.</summary>
        public bool Leaving { get; private set; }

        private void OnEnable() => PuddleHazard.Triggered += OnHazardTriggered;

        private void OnDisable() => PuddleHazard.Triggered -= OnHazardTriggered;

        protected override void Awake()
        {
            base.Awake();
            FloodSchoolHandoff.ResetRun(); // Sel okuldan yeniden başladı: önceki geliş/tekrar bilgisi kalmasın
            gateZone = Zone("SchoolGate");
            bridgeHead = Zone("BridgeHead");
            bridgeMid = Zone("BridgeMid");
            bridgeEnd = Zone("BridgeEnd");
            exitZone = Zone("ExitZone");
            var cb = Named("CameraBounds");
            cameraBounds = cb != null ? cb.GetComponent<CameraBounds>() : null;
            teacher = Named("Teacher");
            if (shortcutView == null) { var v = Named("ShortcutView"); if (v != null) shortcutView = v.transform; }
            if (safeSpot == null) { var s = Named("SafeSpot"); if (s != null) safeSpot = s.transform; }
            var signGo = Named("Sign");
            sign = signGo != null ? signGo.GetComponent<Interactable>() : null;
            if (rain == null) rain = FindAnyObjectByType<RainController>(FindObjectsInactive.Include);
            if (titleCard == null) titleCard = FindAnyObjectByType<FloodEveningTitleCard>(FindObjectsInactive.Include);
            if (signOverlay != null) signOverlay.SetActive(false);
        }

        protected override IEnumerator RunChapter()
        {
            startedAt = Time.time;
            // ---------- Kart: "SEL BÖLÜMÜ" (FloodEvening'in kartı ve metni) ----------
            SetBlack();
            EnterStep("FS_START"); // okul kapısı, Cenk yerleşir, kamera takip
            if (rain != null) rain.SetIntensityNow(rainIntensity);
            using (Objectives != null ? Objectives.DeferFeedback() : null)
            {
                NotifyObjective("event", "FS_START");
                if (titleCard != null)
                {
                    var texts = FloodEveningTexts.Load(floodEveningJson);
                    texts.FirstLine("FE_TITLE", out string cardTitle, out string cardSubtitle);
                    yield return titleCard.Play(cardTitle, cardSubtitle, texts.Ui(FloodEveningTexts.Keys.CardNote));
                    titleCard.Hide(); // arkada perde siyah
                }

                // ---------- Okul kapısı: öğretmen uğurlar ----------
                FacePlayerTowards(TeacherPos());
                yield return FadeIn(1.0f);
                yield return Say("FS_TEACHER");
            }

            // ---------- "Okuldan çık" ----------
            yield return Walk(_ => gateZone == null || !InZone(gateZone));
            NotifyObjective("event", "FS_EXITED");

            // ---------- "Köprü bölgesine ilerle" → Cenk kestirmeye bakar, karar ----------
            yield return Walk(_ => InZone(bridgeHead));
            NotifyObjective("event", "FS_BRIDGE_REACHED");
            if (Objectives != null) yield return Objectives.WaitForFeedback(0.5f, 2f);
            LookAtShortcut();
            yield return Say("FS_SHORTCUT");
            yield return Ask(decisionId, d1Result, AfterChoice);
            yield return Say("FS_DECIDED");

            // ---------- "Köprüyü geç" (ortada bir kez replik) ----------
            EnterStep("FS_BRIDGE");
            yield return Walk(_ => InZone(bridgeEnd));
            NotifyObjective("event", "FS_CROSSED");
            yield return Say("FS_CROSSED");

            // ---------- "Mevcut sel güzergâhına bağlan" (isteğe bağlı tabela) ----------
            EnterStep("FS_HOME");
            yield return Walk(_ => InZone(exitZone));
            NotifyObjective("event", "FS_LEFT");
            if (Objectives != null) yield return Objectives.WaitForFeedback(0.5f, 2.5f);

            // ---------- Çıkış: Cenk yürüyerek kadrajdan çıkar, kararma, FloodEvening ----------
            yield return ExitWalk();
            Leaving = true;
            float rainNow = rain != null ? rain.Intensity : rainIntensity;
            Debug.Log($"[FloodSchool] okul bölümü bitti ({Time.time - startedAt:0.0} sn, ara sahne {(swept ? "oynadı" : "yok")}) → {nextScene}");
            FloodSchoolHandoff.MarkArrival(rainNow);
            if (Application.CanStreamedLevelBeLoaded(nextScene)) GameSceneRouter.LoadRoom(nextScene);
            else
            {
                // Sahne build listesinde yoksa siyah ekranda kalınmasın.
                Debug.LogError($"[FloodSchool] '{nextScene}' Build Settings'te yok → menü.");
                GameSceneRouter.ReturnToMenu();
            }
        }

        // ---------------- karar ----------------

        // Yanlış seçim: ara sahne yalnız ilk seferde; sonra güvenli noktada uyarı repliği, karar yeniden sorulur.
        private IEnumerator AfterChoice(ChoiceData choice)
        {
            if (choice == null || choice.correct) yield break;
            NotifyObjective("event", "FS_SWEPT", false);
            if (swept || sweepCutscene == null) yield break; // geri bildirim kararda gösterildi; ara sahne tekrar etmez
            swept = true;
            var tracker = Player != null ? PlayerHazardTracker.For(Player) : null;
            if (tracker != null) tracker.Suppressed = true;
            yield return PlaySweepCutscene();
            // Ekran siyah: Cenk köprü başındaki güvenli noktada (suya hiç girmedi; ara sahne olabilecek olanı gösterdi).
            if (safeSpot != null && Player != null) Player.PlaceAt(safeSpot.position, safeFacing);
            if (ChapterCam != null && PlayerObject != null) ChapterCam.Follow(PlayerObject.transform, cameraBounds, true);
            yield return FadeIn(0.6f);
            if (tracker != null) tracker.Suppressed = false;
            yield return Say("FS_SWEPT");
            LookAtShortcut();
        }

        // Sele kapılma ara sahnesi (FloodChapterController ile aynı kurgu): perde kararır, ara sahne kendi kamerasıyla
        // açılır, sonunda kendisi kararır; kamerası kapanmadan perde siyaha alınır ki dünya bir an görünmesin.
        private IEnumerator PlaySweepCutscene()
        {
            SetPlayerControl(false);
            yield return FadeOut(0.4f);
            HideHint();
            StartCoroutine(sweepCutscene.Play());
            float sweepStart = Time.time; // ara sahnenin kendi saati (ses kancası)
            GameAudio.PlaySfx("SFX_FE_Sweep_Dream", 0.8f); // Cenk bunu hayal ediyor: rüya geçişi
            yield return null;
            yield return FadeIn(0.2f);
            // P-FLOOD (18 Eki): ses kancası — suya girince akıntı döngüsü, düşüşte büyük sıçrama, bitince döngü susar.
            const string currentChannel = "fe_sweep_current";
            bool current = false, splash = false;
            float blackAt = sweepCutscene.Duration - 0.1f;
            try
            {
                for (float elapsed = 0f; sweepCutscene.IsPlaying && elapsed < blackAt; elapsed += Time.deltaTime)
                {
                    if (!current && Time.time - sweepStart >= sweepCutscene.EnterWaterTime) { current = true; GameAudio.PlayLoop("SFX_FE_Sweep_Current", currentChannel, 0.75f, 0.4f); }
                    if (!splash && Time.time - sweepStart >= sweepCutscene.FallTime - 0.05f) { splash = true; GameAudio.PlaySfx("SFX_FE_Sweep_Splash", 0.9f); GameAudio.PlaySfx("SFX_FE_Sweep_Scream", 0.6f); }
                    yield return null;
                }
                SetBlack();
                while (sweepCutscene.IsPlaying) yield return null;
            }
            finally
            {
                GameAudio.StopLoop(currentChannel, 0.6f);
            }
        }

        // ---------------- serbest yürüyüş ----------------

        /// <summary>
        /// done(serbest geçen süre) true olana kadar oyuncu yürür. Su kenarına basılırsa geri itme biter, uyarı repliği,
        /// yürüyüş sürer. Köprünün ortasında bir kez replik. Tabela (bonus) menzildeyken "[E] Tabelayı incele".
        /// </summary>
        private IEnumerator Walk(Func<float, bool> done)
        {
            if (AutoAdvance) yield break;
            float walked = 0f;
            bool hinted = false;
            pendingWarning = null;
            walking = true;
            SetPlayerControl(true);
            yield return null; // önceki adımı bitiren tuş bu adımı da geçirmesin
            try
            {
                while (!done(walked))
                {
                    if (pendingWarning != null)
                    {
                        string warning = pendingWarning;
                        if (hinted) { HideHint(); hinted = false; if (sign != null) sign.SetHighlighted(false); }
                        SetPlayerControl(false);
                        var tracker = Player != null ? Player.GetComponent<PlayerHazardTracker>() : null;
                        while (tracker != null && tracker.IsPushing) yield return null;
                        yield return Say(warning);
                        pendingWarning = null;
                        SetPlayerControl(true);
                        yield return null;
                        continue;
                    }

                    if (!bridgeLineSaid && InZone(bridgeMid) && CurrentStep == "FS_BRIDGE")
                    {
                        bridgeLineSaid = true;
                        Debug.Log("[FloodSchool] köprünün ortası: akıntı repliği");
                        if (hinted) { HideHint(); hinted = false; }
                        SetPlayerControl(false);
                        yield return Say("FS_BRIDGE");
                        SetPlayerControl(true);
                        yield return null;
                        continue;
                    }

                    bool signNear = SignInRange();
                    if (signNear != hinted)
                    {
                        hinted = signNear;
                        if (sign != null) sign.SetHighlighted(signNear);
                        if (signNear) ShowHint(sign.KeyLabel, sign.ActionLabel);
                        else HideHint();
                    }
                    if (signNear && GameInput.InteractPressed)
                    {
                        hinted = false;
                        HideHint();
                        SetPlayerControl(false);
                        yield return ReadSign();
                        SetPlayerControl(true);
                        yield return null;
                        continue;
                    }

                    walked += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                walking = false;
                pendingWarning = null;
                if (hinted) { HideHint(); if (sign != null) sign.SetHighlighted(false); }
                SetPlayerControl(false);
            }
        }

        private bool SignInRange()
        {
            if (signRead || sign == null || Player == null || !sign.Available || !sign.isActiveAndEnabled) return false;
            return Player.GroundDistanceTo(sign.transform.position) <= sign.UseRadius;
        }

        private IEnumerator ReadSign()
        {
            signRead = true;
            sign.SetHighlighted(false);
            sign.SetAvailable(false);
            FacePlayerTowards(sign.transform.position);
            NotifyObjective("event", "FS_SIGN");
            if (signOverlay != null) signOverlay.SetActive(true);
            yield return Say("FS_SIGN");
            if (signOverlay != null) signOverlay.SetActive(false);
        }

        // PuddleHazard: çocuk su kenarına bastı (alan onu zaten durdurup geri itiyor). Serbest yürüyüşteyse uyarı
        // repliği sıraya girer; aynı uyarı warningCooldown içinde tekrar etmez (o zaman yalnız geri itilir).
        private void OnHazardTriggered(PuddleHazard hazard)
        {
            if (!walking || hazard == null || string.IsNullOrEmpty(hazard.WarningId) || pendingWarning != null) return;
            if (hazard.gameObject.scene != gameObject.scene) return;
            if (lastWarningTime.TryGetValue(hazard.WarningId, out float last) && Time.time - last < warningCooldown) return;
            lastWarningTime[hazard.WarningId] = Time.time;
            pendingWarning = hazard.WarningId;
            NotifyObjective("event", hazard.WarningId); // "Suya girme" uyarı alır
            hazard.CancelInputRestore(); // hareketi uyarı bitince bu akış açar
        }

        // ---------------- çıkış ----------------

        // Cenk çıkış yönünde yürümeye devam eder (sınır çizgisi bu kısa yürüyüşte devre dışı), ekran yavaşça kararır.
        private IEnumerator ExitWalk()
        {
            SetPlayerControl(false);
            HideHint();
            if (Player == null) { yield return FadeOut(exitFadeSeconds); yield break; }
            var body = Player.GetComponent<Rigidbody2D>();
            var animator = Player.GetComponent<IsoCharacterAnimator>();
            if (body != null) { body.linearVelocity = Vector2.zero; body.simulated = false; }
            if (ChapterCam != null) ChapterCam.Release(); // kamera durur, Cenk kadrajdan çıkar
            Vector2 d = exitDirection.sqrMagnitude > 0.0001f ? exitDirection.normalized : new Vector2(0.894f, 0.447f);
            Vector2 screen = new Vector2(d.x, d.y / 0.5f); // animasyon yönü (ekran): dikey hız yarı ölçekli
            Coroutine fade = null;
            for (float t = 0f; t < exitWalkSeconds; t += Time.deltaTime)
            {
                if (fade == null && t >= exitWalkSeconds - exitFadeSeconds) fade = StartCoroutine(FadeOut(exitFadeSeconds));
                Player.transform.position += (Vector3)(d * exitWalkSpeed * Time.deltaTime);
                if (animator != null) animator.SetMotion(screen);
                yield return null;
            }
            if (fade != null) yield return fade;
            else yield return FadeOut(0.3f);
            if (animator != null) animator.SetMotion(Vector2.zero);
        }

        // ---------------- yardımcılar ----------------

        // Cenk su basmış kestirmeye döner (karar onun kendi düşüncesi).
        private void LookAtShortcut()
        {
            if (shortcutView != null) FacePlayerTowards(shortcutView.position);
        }

        private Vector3 TeacherPos() => teacher != null ? teacher.transform.position : (PlayerObject != null ? PlayerObject.transform.position + Vector3.left : Vector3.zero);

        private bool InZone(Collider2D zone)
        {
            if (zone == null || PlayerObject == null) return true;
            return zone.OverlapPoint(PlayerObject.transform.position);
        }

        private Collider2D Zone(string objectName)
        {
            var go = Named(objectName);
            if (go == null) { Debug.LogWarning($"[FloodSchool] '{objectName}' alanı sahnede yok; o adım beklemeden geçer."); return null; }
            return go.GetComponent<Collider2D>();
        }

        private GameObject Named(string objectName)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == objectName) return t.gameObject;
            return null;
        }
    }
}
