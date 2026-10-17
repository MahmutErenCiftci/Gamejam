using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Sel bölümü akışı — docs/OYUN_AKISI.md §6 (Mahmut).
    /// Mutfak (World/Kitchen, Kitchen.prefab): anneyle sohbet → TV jingle'ı → haber → anne tekrar sorar (F_D1) →
    /// resmî duyuru → yağmurluk + şemsiye → "[E] Dışarı çık". Sokak (World/Street): hafif yağmur → durağa serbest
    /// yürüyüş (su birikintisi / rögar uyarıları, isteğe bağlı afiş) → sundurmada sağanak → otobüs gelir →
    /// "[E] Otobüse bin" → otobüs gider. Otobüs içi (World/BusInterior): arkadaşlar, söylenti kartları, F_D2 (yanlışta
    /// okul yolunun riskli kısmı gösterilir, karar tekrar sorulur), okul durağında "[E] Otobüsten in". Okul yolu
    /// (World/SchoolRoute): Okul Caddesi su altında, tabela (F07), köprü üstü yaya yolu → öğretmen (F08), sonuç (F09).
    /// Mutfaktaki objeler adlarıyla bulunur: Mother, TV, TVPoint, CoatRack, UmbrellaStand, QuakeBag, Coat, Umbrella, ExitDoor.
    /// Sokaktakiler: BusStopZone (trigger collider), Poster (Interactable), BoardPoint (otobüsün kapısı).
    /// Otobüs içindekiler: Friend1..3, Door (SpriteRenderer), Led (SpriteRenderer), ExitDoor (Interactable).
    /// Okul yolundakiler: Sign (Interactable), StuckCar, GateZone (trigger collider), Teacher.
    /// </summary>
    public class FloodChapterController : ChapterControllerBase
    {
        /// <summary>Mutfakta yağmurluk alındı bayrağı (GameSession).</summary>
        public const string RaincoatFlag = "raincoat_taken";

        [Header("Mutfak (F03–F04)")]
        [Tooltip("TV'den haber jingle'ı çaldığı an (ses/efekt kancası; ses dosyası şart değil)")]
        [SerializeField] private UnityEvent onTvJingle = new UnityEvent();
        [Tooltip("İsteğe bağlı jingle sesi. Boşsa mutfaktaki TV objesinin AudioSource'u kullanılır; klibi yoksa sessiz geçer.")]
        [SerializeField] private AudioSource tvJingleAudio;
        [Tooltip("Haber yayını sırasında TV'nin sprite'ı (mutfaktaki \"TV\" objesi)")]
        [SerializeField] private Sprite tvNewsSprite;
        [Tooltip("Yağmurluk alınınca askının sprite'ı (\"CoatRack\")")]
        [SerializeField] private Sprite coatRackEmptySprite;
        [Tooltip("Şemsiye alınınca ya da yatak odasında alındıysa şemsiyeliğin sprite'ı (\"UmbrellaStand\")")]
        [SerializeField] private Sprite umbrellaStandEmptySprite;
        [Tooltip("Jingle'dan sonra haber açılmadan önceki kısa bekleme (sn)")]
        [SerializeField] private float tvTurnPause = 0.35f;

        [Header("Sokak ve durak (F05)")]
        [Tooltip("Sokağın yağmuru (World/Street altında; sağanakta sahneyi kendisi karartır)")]
        [SerializeField] private RainController rain;
        [Tooltip("Durağa gelen otobüs (World/Street altında; konumu durak noktasıdır)")]
        [SerializeField] private BusController bus;
        [Tooltip("Evden çıkınca yağmurun şiddeti (0..1)")]
        [SerializeField] private float exitRain = 0.35f;
        [Tooltip("Durağa yürürken yağmurun çıktığı şiddet")]
        [SerializeField] private float walkRain = 0.5f;
        [Tooltip("walkRain'e çıkış süresi (sn)")]
        [SerializeField] private float walkRainSeconds = 8f;
        [Tooltip("Sundurmanın altında sağanağa dönüş süresi (sn)")]
        [SerializeField] private float downpourSeconds = 5f;
        [Tooltip("Aynı uyarı balonu (su birikintisi / rögar) en az kaç saniyede bir tekrar edebilir")]
        [SerializeField] private float warningCooldown = 6f;
        [Tooltip("Afiş okununca açılan büyük afiş görseli (UI overlay, normalde kapalı)")]
        [SerializeField] private GameObject posterOverlay;
        [Tooltip("Sağanak repliğinden sonra otobüs gelene kadar sundurmada beklenen süre (sn; afiş okurken sayılmaz)")]
        [SerializeField] private float busWaitSeconds = 3f;
        [Tooltip("Otobüs giderken ekranın kararma süresi (sn)")]
        [SerializeField] private float boardFadeSeconds = 0.6f;
        [Tooltip("Ekran karardıktan sonra yağmurun dinme süresi (sn)")]
        [SerializeField] private float rainStopSeconds = 1.5f;

        [Header("Otobüs içi (F06)")]
        [Tooltip("Pencerelerden geçen manzara (World/BusInterior); otobüs durakta durunca durur")]
        [SerializeField] private BusWindowScroller busScenery;
        [Tooltip("Okul durağında açık kapı sprite'ı (BusInterior \"Door\")")]
        [SerializeField] private Sprite busDoorOpenSprite;
        [Tooltip("Okul durağına gelince LED tabelanın sprite'ı (BusInterior \"Led\": \"DURAK: OKUL\")")]
        [SerializeField] private Sprite busLedStopSprite;

        [Header("Okul yolu (F07–F08)")]
        [Tooltip("Okul durağındaki otobüs (World/SchoolRoute altında, Bus.prefab); çocuk inince kalkar")]
        [SerializeField] private BusController routeBus;
        [Tooltip("Okul yolunun yağmuru (World/SchoolRoute altında)")]
        [SerializeField] private RainController routeRain;
        [Tooltip("Okul durağında yağmurun şiddeti (0..1)")]
        [SerializeField] private float routeRainArrive = 0.35f;
        [Tooltip("Güvenli rotada (F08) yağmurun hafiflediği şiddet")]
        [SerializeField] private float routeRainLight = 0.15f;
        [Tooltip("Tabela incelenince açılan büyük tabela (UI overlay, normalde kapalı)")]
        [SerializeField] private GameObject signOverlay;

        [Header("Sonuç (F09)")]
        [Tooltip("Geçici Sel sonuç ekranı (ResultViewBase); bölüme özel ayrıntıları (eşyalar, uyarılar) bu gösterir")]
        [SerializeField] private FloodResultView floodResult;

        // Sokakta serbest yürüyüşte E ile kullanılabilen nokta (afiş, otobüs kapısı).
        private sealed class WalkPoint
        {
            public Interactable Point;
            public string Label;
            public Func<IEnumerator> OnUse;
            public bool EndsWalk;
        }

        private Sprite tvDefaultSprite;
        private SpriteRenderer tvRenderer;

        // Sokakta su birikintisi / rögar uyarıları (PuddleHazard.Triggered → serbest yürüyüşte balon)
        private readonly Dictionary<string, float> lastWarningTime = new Dictionary<string, float>();
        private string pendingWarning;
        private bool streetWalking;
        private int waterWarnings;                       // su birikintisi / suya yaklaşma geri itmeleri (sonuç ekranı)
        private readonly DecisionResult d1Result = new DecisionResult();
        private readonly DecisionResult d2Result = new DecisionResult();

        protected override ChapterId Chapter => ChapterId.Flood;

        // Görev paneli: Resources/Objectives/objectives_flood.json (odalardan gelen ilerleme GameSession'da).
        protected override bool UsesObjectives => true;

        private void OnEnable() => PuddleHazard.Triggered += OnHazardTriggered;

        private void OnDisable() => PuddleHazard.Triggered -= OnHazardTriggered;

        protected override IEnumerator RunChapter()
        {
            // F01–F02 (0:00–0:50) ortak açılış: uyanış + sabah rutini (oda sahnelerinde oynandıysa atlanır)
            yield return PlayMorningIntro("F01_WAKE", "F02_WASH");

            // F03 (0:50–1:35) mutfak: anneyle sabah sohbeti → TV'den haber jingle'ı → haber
            yield return EnterRoom("F03_NEWS");
            LookAtEachOther();
            yield return Say("F03_MOTHER_CHAT");

            PlayTvJingle();
            yield return Say("F03_TV_JINGLE");
            LookAtTv();
            if (!AutoAdvance && tvTurnPause > 0f) yield return new WaitForSeconds(tvTurnPause);
            yield return ShowNews("NEWS_F03_TV");   // TV mutfaktan çıkana kadar haber kanalında kalır
            yield return Say("F03_NEWS");

            // F04 (1:35–2:25) anne tekrar sorar, karar 1, resmî duyuru, eşyalar
            EnterStep("F04_MOTHER");
            LookAtEachOther();
            yield return Say("F04_MOTHER");
            yield return Ask("F_D1", d1Result);
            yield return Say("F04_CHECK");
            yield return ShowNews("NEWS_F04_OFFICIAL");
            yield return Say("F04_PREPARE");
            // TODO(Mahmut): alınan eşya HUD ikonu (P1)
            yield return UsePoint("Coat", "Yağmurluğu al");
            TakeRaincoat();
            // Şemsiye yatak odasında alındıysa şemsiyelik zaten boş, tekrar sorulmaz
            if (!GameSession.UmbrellaTaken)
            {
                yield return UsePoint("Umbrella", "Şemsiyeyi al");
                TakeUmbrella();
            }
            yield return UsePoint("ExitDoor", "Dışarı çık");

            // F05 (2:25–3:35) dışarı → durağa yürüyüş → sağanak → otobüs
            yield return RunStreet();

            // F06 (3:35–4:35) otobüs içi: arkadaşlar, söylentiler (telefon kartları), karar 2, okul durağında iniş
            yield return RunBus();

            // F07 (4:35–5:05) okul durağı: Okul Caddesi su altında; tabela (bariyerin arkasından, uzaktan)
            // F08 (5:05–5:35) köprü üstü yaya yolu → okul kapısı, öğretmen
            yield return RunSchoolRoute();

            // F09 sonuç ekranı (bölüme özel ayrıntılar: kararlar, eşyalar, suya yaklaşma uyarıları)
            EnterStep("F09_RESULT");
            if (floodResult != null)
                floodResult.SetDetails(BuildResultDetails());
            yield return ShowResult();
        }

        // ---------- F06: otobüs içi ----------

        private IEnumerator RunBus()
        {
            // Ekran kara (otobüs gitti). Otobüsün içi açılır: Cenk koridorda, üç arkadaşı yanında, otobüs ilerliyor.
            yield return EnterRoom("F06_RUMORS", 0.6f);
            if (busScenery != null) busScenery.SetMoving(true, true);
            LookAtFriends();
            yield return Say("F05_BOARD");

            // Arkadaşlar Okul Caddesi hakkında farklı şeyler söyler → söylenti kartları (telefon) → karar 2
            yield return Say("F06_RUMORS");
            yield return ShowNews("NEWS_F06_RUMORS");
            yield return Ask("F_D2", d2Result, ShowRiskAfterWrongChoice);
            if (d2Result.FinalChoice != null && d2Result.FinalChoice.id == "F_D2_D")
                yield return ShowNews("NEWS_F06_OFFICIAL_ROAD");
            LookAtFriends();
            yield return Say("F06_DECIDED");

            // Okul durağı: otobüs yavaşlayıp durur, LED tabela değişir, kapı açılır → "[E] Otobüsten in"
            EnterStep("F06_ARRIVE");
            if (busScenery != null)
            {
                busScenery.SetMoving(false);
                while (busScenery.IsMoving && !AutoAdvance) yield return null;
            }
            SetRoomSprite("Led", busLedStopSprite);
            yield return Say("F06_ARRIVE");
            SetRoomSprite("Door", busDoorOpenSprite);
            yield return UsePoint("ExitDoor", "Otobüsten in");
        }

        // Cenk arkadaşlarına döner (ortalarına).
        private void LookAtFriends()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var name in new[] { "Friend1", "Friend2", "Friend3" })
            {
                var f = FindRoomObject(name);
                if (f == null) continue;
                sum += f.transform.position;
                n++;
            }
            if (n > 0) FacePlayerTowards(sum / n);
        }

        // ---------- F07–F08: okul yolu ----------

        private IEnumerator RunSchoolRoute()
        {
            // Durakta bekleyen otobüs (kapalı kök: Arrive onu hemen durağa, kapısı açık koyar); çocuk kapının önünde iner.
            if (routeBus != null) StartCoroutine(routeBus.Arrive());
            yield return EnterRoom("F07_RISK", 0.5f);
            var depart = routeBus != null ? StartCoroutine(routeBus.Depart()) : null;
            yield return Say("F07_ARRIVE");

            // Serbest yürüyüş: tabelayı incele (suya yaklaşınca uyarı + geri itme, PuddleHazard)
            var sign = FindPoint("Sign");
            if (sign != null && !AutoAdvance)
                yield return StreetWalk(_ => false, new WalkPoint { Point = sign, Label = "Tabelayı incele", EndsWalk = true });
            else
                yield return WaitForInteract("Tabelayı incele");
            NotifyObjective("event", "F07_SIGN");
            if (sign != null) FacePlayerTowards(sign.transform.position);
            if (signOverlay != null) signOverlay.SetActive(true);
            yield return Say("F07_RISK");
            if (signOverlay != null) signOverlay.SetActive(false);
            if (depart != null) yield return depart;

            // F08: yağmur hafifler; köprü üstü yaya yolundan okul kapısına
            EnterStep("F08_SAFEROUTE");
            if (routeRain != null) routeRain.SetIntensity(routeRainLight, 4f);
            yield return Say("F08_BRIDGE");
            var gate = FindRoomComponent<Collider2D>("GateZone");
            yield return StreetWalk(_ => IsPlayerInZone(gate));
            NotifyObjective("event", "F08_GATE"); // köprüden geçildi, okul kapısı
            if (Objectives != null) yield return Objectives.WaitForFeedback(0.6f, 3f); // tik öğretmen konuşmadan görünsün
            var teacher = FindRoomObject("Teacher");
            if (teacher != null) FacePlayerTowards(teacher.transform.position);
            yield return Say("F08_SAFEROUTE");
        }

        private FloodResultView.Details BuildResultDetails()
        {
            var d = new FloodResultView.Details
            {
                decisionCount = 2,
                wrongDecisionCount = d1Result.WrongChoices.Count + d2Result.WrongChoices.Count,
                raincoat = GameSession.HasFlag(RaincoatFlag),
                umbrella = GameSession.UmbrellaTaken,
                waterWarnings = waterWarnings,
            };
            if (Objectives != null)
                Objectives.GetSummary(out d.objectivesDone, out d.objectivesTotal, out d.bonusDone, out d.bonusTotal);
            foreach (var c in d1Result.WrongChoices.Concat(d2Result.WrongChoices))
            {
                string title = WrongExampleOf(c);
                if (d.mistakes.Exists(m => m.title == title)) continue;
                d.mistakes.Add(new FloodResultView.Mistake { title = title, why = c.feedback });
            }
            return d;
        }

        // F05: evin önü → durak → sağanak → otobüs. Bitince ekran karanlıktır.
        private IEnumerator RunStreet()
        {
            // F05_EXIT: kararma → evin önündeki kaldırım, hafif yağmur zaten başlamış (OnStepEntered)
            yield return EnterRoom("F05_EXIT");
            bool gear = GameSession.HasFlag(RaincoatFlag) || GameSession.UmbrellaTaken;
            yield return Say(gear && HasStoryScene("F05_EXIT_GEAR") ? "F05_EXIT_GEAR" : "F05_EXIT");

            // F05_STREET: yağmur hızlanır; durağa serbest yürüyüş (birikinti/rögar uyarıları, isteğe bağlı afiş)
            EnterStep("F05_STREET");
            if (rain != null) rain.SetIntensity(walkRain, walkRainSeconds);
            yield return Say("F05_STREET");
            var poster = PosterPoint();
            var stopZone = FindRoomComponent<Collider2D>("BusStopZone");
            yield return StreetWalk(_ => IsPlayerInZone(stopZone), poster);

            // F05_BUSSTOP: sundurmanın altına girince yağmur sağanağa döner (sahne kararır, birikintiler büyür)
            EnterStep("F05_BUSSTOP");
            var downpour = rain != null ? StartCoroutine(rain.StartDownpour(downpourSeconds)) : null;
            FacePlayer(new Vector2(0.4472f, -0.8944f)); // yola doğru
            yield return Say("F05_DOWNPOUR");
            yield return StreetWalk(t => t >= busWaitSeconds, poster);
            if (downpour != null) yield return downpour;

            // F05_BUS: otobüs gelir, durur, kapısı açılır
            EnterStep("F05_BUS");
            if (bus != null)
            {
                FacePlayerTowards(bus.StopPosition);
                yield return bus.Arrive();
            }
            yield return Say("F05_BUS_ARRIVE");

            // "[E] Otobüse bin": çocuk kapıya yürür, otobüsün arkasında kaybolur; kapı kapanır, otobüs gider
            var board = bus != null ? bus.BoardPoint : FindPoint("BoardPoint");
            if (board != null && !AutoAdvance)
                yield return StreetWalk(_ => false, new WalkPoint { Point = board, Label = "Otobüse bin", EndsWalk = true });
            else
                yield return WaitForInteract("Otobüse bin");
            NotifyObjective("event", "F05_BOARD");
            yield return BoardBus();
            // "Günaydın! İçerisi kupkuru." repliği (F05_BOARD) otobüsün içinde söylenir (RunBus).
            var depart = bus != null ? StartCoroutine(bus.Depart()) : null;
            if (depart != null) yield return depart;
            yield return FadeOut(boardFadeSeconds);
            // Ekran karardı: çocuk otobüste. Yağmur karanlıkta diner (F06 sokak kökünü kullanır ama tam yağmurla/sesle sürmesin).
            if (rain != null) rain.StopRain(rainStopSeconds);
        }

        // Çocuk kapıya yürüyüp kaybolur (bordür çarpışması ve tehlike alanları bu kısa yürüyüşte devre dışı).
        private IEnumerator BoardBus()
        {
            SetPlayerControl(false);
            if (bus == null || Player == null)
            {
                SetPlayerVisible(false);
                yield break;
            }
            var body = Player.GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;
            yield return bus.Board(Player, true);
            if (body != null) body.simulated = true;
            SetPlayerVisible(false);
        }

        // ---------- mutfak ----------

        // Oda açılırken (kararma altında) odanın o anki durumu kurulur.
        protected override void OnStepEntered(string stepId)
        {
            if (stepId == "F03_NEWS") PrepareKitchen();
            else if (stepId == "F05_EXIT")
            {
                // Dışarı çıkınca hafif yağmur zaten yağıyor.
                if (rain != null) rain.SetIntensity(exitRain, 0f);
                lastWarningTime.Clear();
                pendingWarning = null;
            }
            else if (stepId == "F07_RISK" || stepId == "F07_RISK_WARNING")
            {
                // Okul yolunda yağmur sürüyor ama sağanak dinmiş.
                if (routeRain != null) routeRain.SetIntensity(routeRainArrive, 0f);
                lastWarningTime.Clear();
                pendingWarning = null;
                if (signOverlay != null) signOverlay.SetActive(false);
            }
        }

        // Mutfak: yağmurluk askıda, şemsiye (yatak odasında alınmadıysa) şemsiyelikte, deprem çantası Sel'de yok,
        // TV normal yayında.
        private void PrepareKitchen()
        {
            SetRoomObjectVisible("QuakeBag", false);
            tvRenderer = FindRoomComponent<SpriteRenderer>("TV");
            if (tvRenderer != null && tvDefaultSprite == null) tvDefaultSprite = tvRenderer.sprite;
            if (GameSession.UmbrellaTaken)
            {
                SetRoomSprite("UmbrellaStand", umbrellaStandEmptySprite);
                SetRoomObjectVisible("Umbrella", false);
            }
        }

        private void PlayTvJingle()
        {
            SetTvNews(true);
            if (tvJingleAudio == null) tvJingleAudio = FindRoomComponent<AudioSource>("TV");
            if (tvJingleAudio != null && tvJingleAudio.clip != null) tvJingleAudio.Play();
            Debug.Log("[Flood] TV: haber jingle'ı");
            onTvJingle?.Invoke();
        }

        private void SetTvNews(bool on)
        {
            if (tvRenderer == null) tvRenderer = FindRoomComponent<SpriteRenderer>("TV");
            if (tvRenderer == null) return;
            if (tvDefaultSprite == null) tvDefaultSprite = tvRenderer.sprite;
            var sprite = on ? tvNewsSprite : tvDefaultSprite;
            if (sprite != null) tvRenderer.sprite = sprite;
        }

        private void TakeRaincoat()
        {
            GameSession.SetFlag(RaincoatFlag);
            SetRoomSprite("CoatRack", coatRackEmptySprite);
            Debug.Log("[Flood] yağmurluk alındı");
        }

        private void TakeUmbrella()
        {
            GameSession.MarkUmbrellaTaken();
            SetRoomSprite("UmbrellaStand", umbrellaStandEmptySprite);
            Debug.Log("[Flood] şemsiye alındı");
        }

        private void SetRoomSprite(string objectName, Sprite sprite)
        {
            if (sprite == null) return;
            var sr = FindRoomComponent<SpriteRenderer>(objectName);
            if (sr != null) sr.sprite = sprite;
        }

        // Anne ile çocuk birbirine döner (sohbet).
        private void LookAtEachOther()
        {
            var mother = FindRoomObject("Mother");
            if (mother == null || PlayerObject == null) return;
            FacePlayerTowards(mother.transform.position);
            FaceMother(PlayerObject.transform.position);
        }

        // İkisi de TV'ye döner (haber).
        private void LookAtTv()
        {
            var tv = FindRoomObject("TVPoint") ?? FindRoomObject("TV");
            if (tv == null) return;
            FacePlayerTowards(tv.transform.position);
            FaceMother(tv.transform.position);
        }

        private void FaceMother(Vector3 worldPoint)
        {
            var mother = FindRoomComponent<IsoCharacterAnimator>("Mother");
            if (mother != null) mother.Face(worldPoint - mother.transform.position);
        }

        // ---------- sokak ----------

        private WalkPoint PosterPoint()
        {
            var poster = FindPoint("Poster");
            return poster == null ? null : new WalkPoint { Point = poster, Label = "Afişi oku", OnUse = ReadPoster };
        }

        private IEnumerator ReadPoster()
        {
            var poster = FindPoint("Poster");
            if (poster != null) FacePlayerTowards(poster.transform.position);
            if (posterOverlay != null) posterOverlay.SetActive(true);
            NotifyObjective("event", "F05_POSTER");
            yield return Say("F05_POSTER");
            if (posterOverlay != null) posterOverlay.SetActive(false);
        }

        /// <summary>
        /// Sokakta serbest yürüyüş: done(serbest geçen süre) true olana kadar oyuncu yürür; menzildeki noktanın
        /// "[E] ..." ipucu görünür, E ile kullanılır (EndsWalk ise yürüyüş biter).
        /// </summary>
        private IEnumerator StreetWalk(Func<float, bool> done, params WalkPoint[] points)
        {
            if (AutoAdvance) yield break;

            float walked = 0f;
            WalkPoint shown = null;
            pendingWarning = null;
            streetWalking = true;
            SetPlayerControl(true);
            yield return null; // önceki adımı bitiren tuş bu adımı da geçirmesin
            try
            {
                while (!done(walked))
                {
                    // Su birikintisine / rögara basıldı: geri itme biter, uyarı balonu, sonra yürümeye devam
                    if (pendingWarning != null)
                    {
                        string warning = pendingWarning;
                        if (shown != null) shown.Point.SetHighlighted(false);
                        shown = null;
                        HideHint();
                        SetPlayerControl(false);
                        var tracker = Player != null ? Player.GetComponent<PlayerHazardTracker>() : null;
                        while (tracker != null && tracker.IsPushing) yield return null;
                        yield return Say(warning);
                        pendingWarning = null;
                        SetPlayerControl(true);
                        yield return null; // balonu kapatan tuş başka bir şeyi tetiklemesin
                        continue;
                    }

                    var near = NearestPoint(points);
                    if (near != shown)
                    {
                        if (shown != null) shown.Point.SetHighlighted(false);
                        shown = near;
                        if (shown != null)
                        {
                            shown.Point.SetHighlighted(true);
                            ShowHint(shown.Point.KeyLabel, string.IsNullOrEmpty(shown.Point.ActionLabel) ? shown.Label : shown.Point.ActionLabel);
                        }
                        else HideHint();
                    }

                    if (shown != null && GameInput.InteractPressed)
                    {
                        var used = shown;
                        used.Point.SetHighlighted(false);
                        shown = null;
                        HideHint();
                        if (used.EndsWalk) yield break;
                        SetPlayerControl(false);
                        if (used.OnUse != null) yield return used.OnUse();
                        SetPlayerControl(true);
                        yield return null; // diyaloğu kapatan E yeniden kullanmasın
                        continue;
                    }

                    walked += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                streetWalking = false;
                pendingWarning = null;
                if (shown != null) shown.Point.SetHighlighted(false);
                HideHint();
                SetPlayerControl(false);
            }
        }

        // PuddleHazard: çocuk birikintiye / rögara bastı (alan onu zaten durdurup geri itiyor). Serbest yürüyüşteyse
        // uyarı balonu sıraya girer; aynı uyarı warningCooldown içinde tekrar etmez (o zaman sadece geri itilir).
        private void OnHazardTriggered(PuddleHazard hazard)
        {
            if (!streetWalking || hazard == null || string.IsNullOrEmpty(hazard.WarningId) || pendingWarning != null) return;
            if (lastWarningTime.TryGetValue(hazard.WarningId, out float last) && Time.time - last < warningCooldown) return;
            lastWarningTime[hazard.WarningId] = Time.time;
            waterWarnings++; // sonuç ekranı: gösterilen uyarı balonları (geri itmeler değil)
            pendingWarning = hazard.WarningId;
            NotifyObjective("event", hazard.WarningId); // "Sudan uzak dur" uyarı alır
            hazard.CancelInputRestore(); // hareketi uyarı bitince bu akış açar
        }

        private WalkPoint NearestPoint(WalkPoint[] points)
        {
            if (points == null || Player == null) return null;
            WalkPoint best = null;
            float bestDistance = float.MaxValue;
            foreach (var p in points)
            {
                if (p?.Point == null || !p.Point.Available || !p.Point.isActiveAndEnabled) continue;
                float d = Player.GroundDistanceTo(p.Point.transform.position);
                if (d <= p.Point.UseRadius && d < bestDistance)
                {
                    best = p;
                    bestDistance = d;
                }
            }
            return best;
        }

        // Oyuncunun ayağı alanın (ör. sundurmanın altı, trigger collider) içinde mi? Alan yoksa hemen true.
        private bool IsPlayerInZone(Collider2D zone)
        {
            if (zone == null || PlayerObject == null) return true;
            return zone.OverlapPoint(PlayerObject.transform.position);
        }

        // F_D2'de yanlış seçilirse: o yolun sonu gösterilir — çocuk Okul Caddesi'nin kenarında, suya GİRMEDEN durur
        // (bariyer, tabela, suda mahsur araç), uyarı repliği; sonra otobüse dönülür ve karar tekrar sorulur.
        private IEnumerator ShowRiskAfterWrongChoice(ChoiceData choice)
        {
            if (choice.correct) yield break;

            // Bu kısa gösterimde çocuk zaten suyun kenarında duruyor: su kenarı alanları onu geri itmesin
            // (geri itme sürerken otobüse dönülürse çocuğu tekrar buraya taşırdı).
            var tracker = Player != null ? PlayerHazardTracker.For(Player) : null;
            if (tracker != null) tracker.Suppressed = true;
            yield return EnterRoom("F07_RISK_WARNING", 0.5f);
            var car = FindRoomObject("StuckCar");
            if (car != null) FacePlayerTowards(car.transform.position);
            yield return Say("F07_RISK_WARNING");
            yield return EnterRoom("F06_RUMORS", 0.5f);
            if (tracker != null) tracker.Suppressed = false;
            LookAtFriends();
        }
    }
}
