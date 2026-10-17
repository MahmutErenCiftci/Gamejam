using System;
using System.Collections;
using System.Collections.Generic;
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
    /// "[E] Otobüse bin" → otobüs gider. Sonrası (F06+) eski akışla devam eder.
    /// Mutfaktaki objeler adlarıyla bulunur: Mother, TV, TVPoint, CoatRack, UmbrellaStand, QuakeBag, Coat, Umbrella, ExitDoor.
    /// Sokaktakiler: BusStopZone (trigger collider), Poster (Interactable), BoardPoint (otobüsün kapısı).
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

        protected override ChapterId Chapter => ChapterId.Flood;

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
            yield return Ask("F_D1");
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

            // F06 (2:30–3:10) söylentiler + karar 2
            EnterStep("F06_RUMORS");
            yield return Say("F06_RUMORS");
            yield return ShowNews("NEWS_F06_RUMORS");
            var road = new DecisionResult();
            yield return Ask("F_D2", road, ShowRiskAfterWrongChoice);
            if (road.FinalChoice != null && road.FinalChoice.id == "F_D2_D")
                yield return ShowNews("NEWS_F06_OFFICIAL_ROAD");

            // F07 (3:10–3:40) riskli bölge, yolun kenarından
            yield return EnterRoom("F07_RISK");
            yield return Say("F07_RISK");
            yield return UsePoint("Sign", "Tabelayı incele");

            // F08 (3:40–4:10) güvenli rota → okul kapısı
            yield return EnterRoom("F08_SAFEROUTE");
            yield return Say("F08_SAFEROUTE");

            // F09 (4:10–4:30) sonuç
            EnterStep("F09_RESULT");
            yield return ShowResult();
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
            yield return BoardBus();
            var depart = bus != null ? StartCoroutine(bus.Depart()) : null;
            yield return Say("F05_BOARD");
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
            pendingWarning = hazard.WarningId;
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

        // F_D2'de yanlış seçilirse çocuk suya GİRMEDEN durur, riskli bölge gösterilir, karar tekrar sorulur.
        private IEnumerator ShowRiskAfterWrongChoice(ChoiceData choice)
        {
            if (choice.correct) yield break;

            yield return EnterRoom("F07_RISK");
            yield return Say("F07_RISK_WARNING");
            yield return EnterRoom("F06_RUMORS");
        }
    }
}
