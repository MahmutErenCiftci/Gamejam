using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Rüzgârlı geçit (dal açıldıktan sonra, J sokağı): kısa rüzgâr hissi → oyun durur, uyarı paneli → rüzgâra karşı denge → korunaklı avlu.
    /// Rüzgâr bölümünde normal yürüyüş kapalıdır (Hub'a "wind" paneli açık bildirilir); Cenk'i koridor ekseni boyunca bu bileşen yürütür:
    /// ok rüzgârın ittiği yönü gösterir, oyuncu TERS yatay tuşu (ok → ise A, ok ← ise D) basılı tutunca öne eğilip ilerler,
    /// hiç basmazsa yerinde sallanır, aynı yöne basarsa sendeler ve koridor boyunca biraz geri kayar. Ölüm / düşme / suya kapılma yok:
    /// tek kayıp konumdur ve geri kayma koridor girişini geçmez. Her yön değişiminden önce kısa uyarı (ok yanıp söner, yapraklar döner).
    /// Korunaklı alana girince rüzgâr kesilir, "wind_braced" adımı tamamlanır ve kontrol bölüm akışına geri verilir.
    /// Sel sayacı: giriş hissi zorunlu kısa sinematiktir ("cine_wind", sayaç durur), uyarı paneli okuma panelidir ("info_wind",
    /// durur), denge bölümü oynanıştır ("wind", sayaç akar). Ayar (QA 18 Eki): doğru oynayan ~22 sn, dalgın ~35-40 sn;
    /// yeni esintinin ilk anında (tepki payı) eski tuş sendeletmez, aynı esintide en çok bir kez sendelenir; dizi sonunda
    /// sakin anları da içeren bölümden tekrar eder (hiç tuşa basmayan da sakinlerde yavaşça ilerler, takılmaz).
    /// </summary>
    public class WindCorridor : MonoBehaviour, IFloodEveningResettable
    {
        public enum Strength { Hafif, Orta, Siddetli }
        public enum Phase { Idle, Intro, Info, Active, Sheltered, Done }

        [Serializable]
        public struct Gust
        {
            [Tooltip("-1: sola iter, 0: sakin, +1: sağa iter (ekran yönü)")]
            [Range(-1, 1)] public int push;
            public Strength strength;
            [Min(0.2f)] public float seconds;
        }

        public const string InfoPanelId = "info_wind";
        public const string CinePanelId = "cine_wind";
        public const string WindPanelId = "wind";
        public const string StartStepId = "wind_start";
        public const string DoneStepId = "wind_braced";

        [Header("Sahne (FE_WindCorridor kurulumunda haritadan bağlanır)")]
        [SerializeField] private IsoPlayerController player;
        [Tooltip("Girince rüzgâr hissi + panel başlar (J sokağının 2-3 hücre içi)")]
        [SerializeField] private Collider2D introArea;
        [Tooltip("Korunaklı avlu (SafeZone)")]
        [SerializeField] private Collider2D shelterArea;
        [Tooltip("Koridor ekseninin başı (köşe tarafı)")]
        [SerializeField] private Transform entry;
        [Tooltip("Koridor ekseninin sonu (avlu girişi)")]
        [SerializeField] private Transform exit;
        [Tooltip("Avluda Cenk'in durduğu nokta")]
        [SerializeField] private Transform shelterSpot;

        [Header("Rüzgâr dizisi (bitince loopFrom'dan koridor bitene kadar tekrar eder)")]
        [SerializeField] private Gust[] gusts =
        {
            new Gust { push = 1, strength = Strength.Hafif, seconds = 3.2f },
            new Gust { push = 0, strength = Strength.Hafif, seconds = 1.2f },
            new Gust { push = -1, strength = Strength.Orta, seconds = 3f },
            new Gust { push = 1, strength = Strength.Orta, seconds = 2.6f },
            new Gust { push = 0, strength = Strength.Hafif, seconds = 1f },
            new Gust { push = 1, strength = Strength.Siddetli, seconds = 2.4f },
            new Gust { push = -1, strength = Strength.Siddetli, seconds = 2.4f },
            new Gust { push = 0, strength = Strength.Hafif, seconds = 1f },
            new Gust { push = -1, strength = Strength.Orta, seconds = 2.8f },
            new Gust { push = 1, strength = Strength.Hafif, seconds = 2.6f },
        };
        [Tooltip("Dizi bitince bu sıradan (0 tabanlı) tekrar eder; sakin anlar tekrarda da olsun")]
        [SerializeField, Min(0)] private int loopFrom = 2;
        [Tooltip("Yön değişmeden önceki uyarı süresi (sn): ok yanıp söner, yapraklar yeni yöne döner")]
        [SerializeField, Min(0.2f)] private float warnSeconds = 1f;
        [Tooltip("Panelden önceki kısa rüzgâr hissi (sn) — zorunlu sinematik, sel sayacı durur")]
        [SerializeField, Min(0.1f)] private float introSeconds = 0.9f;

        [Header("Hareket (dünya birimi / sn)")]
        [SerializeField, Min(0.1f)] private float calmSpeed = 0.4f;
        [SerializeField, Min(0.1f)] private float braceSpeedLight = 0.34f;
        [SerializeField, Min(0.1f)] private float braceSpeedMedium = 0.28f;
        [SerializeField, Min(0.1f)] private float braceSpeedStrong = 0.22f;
        [Tooltip("Rüzgâr kesilince avludaki durma noktasına yürüme hızı")]
        [SerializeField, Min(0.1f)] private float shelterSpeed = 0.75f;
        [Tooltip("Yanlış yönde sendeleyince koridor boyunca geri kayma mesafesi")]
        [SerializeField, Min(0f)] private float stumbleBack = 0.35f;
        [SerializeField, Min(0.1f)] private float stumbleSeconds = 0.35f;
        [SerializeField, Min(0f)] private float recoverSeconds = 0.35f;
        [Tooltip("Yeni esintinin ilk anında eski tuş sendeletmez (tepki payı, sn)")]
        [SerializeField, Min(0f)] private float gustGrace = 0.45f;
        [Tooltip("Yatay giriş eşiği (analog çubuk için)")]
        [SerializeField, Range(0.1f, 0.9f)] private float inputThreshold = 0.3f;
        [Tooltip("Cenk'i koridor eksenine çeken yumuşak düzeltme")]
        [SerializeField, Min(0f)] private float centerPull = 2.2f;

        [Header("Cenk kareleri (Assets/Art/FloodEvening/Wind/Cenk)")]
        [SerializeField] private Sprite[] braceLeanLeft = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] braceLeanRight = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] stumbleLeft = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] stumbleRight = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] recoverLeft = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] recoverRight = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] reactLeft = Array.Empty<Sprite>();
        [SerializeField, Min(0.04f)] private float frameSeconds = 0.12f;

        [Header("Görünüm ve ses")]
        [SerializeField] private WindHud hud;
        [SerializeField] private WindFx fx;
        [SerializeField] private string loopId = "AMB_Wind_Gust_Loop";
        [SerializeField] private string gustSfx = "SFX_Wind_Gust";
        [SerializeField] private string audioChannel = "Wind";

        private Phase phase = Phase.Idle;
        private Coroutine run;
        private IsoCharacterAnimator anim;
        private ChapterCamera cam;
        private bool infoOpen, windOpen, cineOpen;
        private int push;                 // şu anki itme (-1/0/+1)
        private Strength strength;
        private float stumbleT, recoverT, poseT, shakeT;
        private float gustT;              // şu anki esinti (ya da uyarı) başlayalı geçen süre
        private bool stumbledThisGust;    // aynı esintide ikinci kez sendeletme yok
        private int lastPush = 1;

        public Phase CurrentPhase => phase;
        /// <summary>Rüzgârın şu an ittiği yön: -1 sol, 0 sakin, +1 sağ (testler ve arayüz için).</summary>
        public int Push => push;
        public Strength CurrentStrength => strength;
        public bool Telegraphing { get; private set; }
        public bool Stumbling => stumbleT > 0f;
        public int StumbleCount { get; private set; }
        /// <summary>Koridor boyunca ilerleme (0 giriş, 1 avlu girişi).</summary>
        public float Progress01 => Along(PlayerPos);

        private Vector2 PlayerPos => player != null ? (Vector2)player.transform.position : Vector2.zero;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (player != null) anim = player.GetComponent<IsoCharacterAnimator>();
            cam = FindAnyObjectByType<ChapterCamera>();
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            Cleanup();
        }

        private void Update()
        {
            if (phase != Phase.Idle || player == null || introArea == null) return;
            if (!FloodEveningHub.FreeRoam) return;
            if (introArea.OverlapPoint(PlayerPos)) run = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // --- 1) kısa rüzgâr hissi: yapraklar savrulur, esinti sesi, Cenk irkilir ---
            phase = Phase.Intro;
            OpenPanel(WindPanelId, ref windOpen);
            OpenPanel(CinePanelId, ref cineOpen);               // zorunlu kısa an: sel sayacı durur
            player.ExternalVelocity = Vector2.zero;
            int firstPush = FirstPush();
            if (fx != null) fx.SetWind(firstPush, Strength.Hafif, true);
            GameAudio.PlaySfx(gustSfx, 0.8f);
            GameAudio.PlayLoop(loopId, audioChannel, 0.45f, 0.6f);
            for (float t = 0f; t < introSeconds; t += Time.deltaTime) { ShowFrames(reactLeft, t, false); yield return null; }
            ClearPose();

            // --- 2) oyun durur, uyarı paneli ---
            phase = Phase.Info;
            OpenPanel(InfoPanelId, ref infoOpen);
            ClosePanel(CinePanelId, ref cineOpen);
            if (hud != null) hud.ShowInfo(true);
            yield return null;                                   // paneli açan karedeki tuş sayılmasın
            while (!(GameInput.InteractPressed || GameInput.ContinuePressed || (hud != null && hud.ConsumeClick()))) yield return null;
            if (hud != null) hud.ShowInfo(false);
            ClosePanel(InfoPanelId, ref infoOpen);
            FloodEveningHub.Complete(StartStepId);
            yield return null;                                   // aynı E başka etkileşimi tetiklemesin

            // --- 3) rüzgâra karşı denge ---
            phase = Phase.Active;
            if (hud != null) hud.ShowGauge(true);
            int i = 0;
            while (!ReachedExit())
            {
                Gust g = gusts.Length > 0 ? gusts[Mathf.Min(i, gusts.Length - 1)] : new Gust { push = 1, strength = Strength.Orta, seconds = 3f };
                if (g.push != 0 && g.push != push)
                {
                    // uyarı: ok yeni yönü yanıp söner, yapraklar döner; bu sürede rüzgâr hafifler (sakin gibi yürür)
                    Telegraphing = true;
                    push = 0; gustT = 0f; stumbledThisGust = false;
                    if (hud != null) hud.Warn(g.push, g.strength);
                    if (fx != null) fx.SetWind(g.push, Strength.Hafif, true);
                    for (float t = 0f; t < warnSeconds && !ReachedExit(); t += Time.deltaTime) { Step(Time.deltaTime); yield return null; }
                    Telegraphing = false;
                }
                push = g.push; strength = g.strength; gustT = 0f; stumbledThisGust = false;
                if (push != 0) { lastPush = push; GameAudio.PlaySfx(gustSfx, StrengthVolume()); }
                if (hud != null) hud.SetGust(push, strength);
                if (fx != null) fx.SetWind(push != 0 ? push : lastPush, push != 0 ? strength : Strength.Hafif, push == 0);
                for (float t = 0f; t < g.seconds && !ReachedExit(); t += Time.deltaTime) { Step(Time.deltaTime); yield return null; }
                if (++i >= gusts.Length) i = Mathf.Clamp(loopFrom, 0, Mathf.Max(0, gusts.Length - 1));
            }

            // --- 4) korunaklı avlu: rüzgâr kesilir ---
            phase = Phase.Sheltered;
            push = 0; stumbleT = recoverT = 0f;
            if (hud != null) { hud.ShowGauge(false); hud.ShowSheltered(); }
            if (fx != null) fx.Calm();
            GameAudio.StopLoop(audioChannel, 1.2f);
            ClearPose();
            Vector2 target = shelterSpot != null ? (Vector2)shelterSpot.position : (exit != null ? (Vector2)exit.position : PlayerPos);
            for (float t = 0f; t < 2.5f && (shelterArea == null || !shelterArea.OverlapPoint(PlayerPos) || IsoDist(PlayerPos, target) > 0.6f); t += Time.deltaTime)
            {
                Vector2 d = target - PlayerPos;
                player.ExternalVelocity = d.sqrMagnitude > 0.0001f ? IsoDir(d) * shelterSpeed : Vector2.zero;
                yield return null;
            }
            player.ExternalVelocity = Vector2.zero;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) yield return null;
            FloodEveningHub.Complete(DoneStepId);
            ClosePanel(WindPanelId, ref windOpen);
            phase = Phase.Done;
            run = null;
        }

        /// <summary>Bir karelik denge adımı: girişe göre ilerle / sallan / sendele.</summary>
        private void Step(float dt)
        {
            if (player == null) return;
            Vector2 axis = Axis();
            Vector2 back = -axis;
            float x = GameInput.MoveX;
            bool left = x < -inputThreshold, right = x > inputThreshold;
            int input = left == right ? 0 : (left ? -1 : 1);
            float speedAlong = 0f;
            poseT += dt;
            gustT += dt;
            // yanlış yön sendeletir; ama yeni esintinin ilk anında (tepki payı) ve aynı esintide ikinci kez değil → yalnız sallanır
            bool canStumble = gustT >= gustGrace && !stumbledThisGust;
            if (shakeT > 0f) { shakeT -= dt; if (cam != null) cam.ShakeOffset = shakeT > 0f ? new Vector2(Mathf.Sin(shakeT * 90f) * 0.03f, 0f) : Vector2.zero; }

            if (stumbleT > 0f)
            {
                stumbleT -= dt;
                speedAlong = Along(PlayerPos) > 0.01f ? -stumbleBack / stumbleSeconds : 0f;
                ShowFrames(lastPush > 0 ? stumbleLeft : stumbleRight, poseT, false);
                if (stumbleT <= 0f) { recoverT = recoverSeconds; poseT = 0f; }
            }
            else if (recoverT > 0f)
            {
                recoverT -= dt;
                ShowFrames(lastPush > 0 ? recoverLeft : recoverRight, poseT, false);
                if (recoverT <= 0f) ClearPose();
            }
            else if (push == 0)
            {
                speedAlong = calmSpeed;
                ClearPose();
                if (hud != null) hud.SetBalance(WindHud.Balance.Calm);
            }
            else if (input == -push)
            {
                speedAlong = strength == Strength.Hafif ? braceSpeedLight : strength == Strength.Orta ? braceSpeedMedium : braceSpeedStrong;
                ShowFrames(push > 0 ? braceLeanLeft : braceLeanRight, poseT, true);
                if (hud != null) hud.SetBalance(WindHud.Balance.Good);
            }
            else if (input == push && canStumble)
            {
                // yanlış yön: sendele, geri kay (ceza yok, sadece konum kaybı)
                StumbleCount++;
                stumbledThisGust = true;
                stumbleT = stumbleSeconds; poseT = 0f; shakeT = 0.25f;
                GameAudio.PlaySfx(gustSfx, 0.5f);
                if (hud != null) hud.SetBalance(WindHud.Balance.Wrong);
            }
            else
            {
                // tuşa basılmıyor (ya da tepki payında / ikinci kez rüzgâr yönüne basıyor): ilerleyemez, rüzgârda sallanır
                ShowFrames(push > 0 ? stumbleLeft : stumbleRight, 0f, false, 1);
                if (hud != null) hud.SetBalance(WindHud.Balance.Hold);
            }

            // eksene doğru yumuşak çekim (koridor duvarlarına sürtünmesin), geri kayma girişi geçmez
            Vector2 p = PlayerPos;
            Vector2 a = entry != null ? (Vector2)entry.position : p;
            Vector2 rel = p - a;
            Vector2 lateral = rel - axis * Vector2.Dot(rel, axis);
            Vector2 v = axis * speedAlong - lateral * centerPull;
            if (Along(p) <= 0f && Vector2.Dot(v, back) > 0f) v -= back * Vector2.Dot(v, back);
            player.ExternalVelocity = v;
        }

        private void ShowFrames(Sprite[] frames, float t, bool loop, int maxFrames = 0)
        {
            if (anim == null || frames == null || frames.Length == 0) return;
            int n = maxFrames > 0 ? Mathf.Min(maxFrames, frames.Length) : frames.Length;
            int k = Mathf.FloorToInt(t / frameSeconds);
            k = loop ? k % n : Mathf.Min(k, n - 1);
            if (frames[k] != null) anim.ShowPose(frames[k]);
        }

        private void ClearPose() { if (anim != null) anim.ClearPose(); }

        private int FirstPush() { foreach (var g in gusts) if (g.push != 0) return g.push; return 1; }

        private float StrengthVolume() => strength == Strength.Hafif ? 0.45f : strength == Strength.Orta ? 0.7f : 1f;

        private bool ReachedExit() => Along(PlayerPos) >= 0.98f || (shelterArea != null && shelterArea.OverlapPoint(PlayerPos));

        /// <summary>Koridor ekseni yönü (dünya, giriş → çıkış, birim).</summary>
        private Vector2 Axis()
        {
            if (entry == null || exit == null) return Vector2.down;
            Vector2 d = (Vector2)(exit.position - entry.position);
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down;
        }

        private float Along(Vector2 p)
        {
            if (entry == null || exit == null) return 0f;
            Vector2 a = entry.position, b = exit.position;
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            return len2 < 0.0001f ? 0f : Vector2.Dot(p - a, ab) / len2;
        }

        // 2:1 izometrikte zemin uzaklığı ve zemin yönü (dikey fark 2 ile)
        private static float IsoDist(Vector2 a, Vector2 b) { Vector2 d = b - a; return Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y); }
        private static Vector2 IsoDir(Vector2 d) { float m = Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y); return m > 0.0001f ? d / m : Vector2.zero; }

        private static void OpenPanel(string id, ref bool flag) { if (flag) return; flag = true; FloodEveningHub.NotifyOpened(id); }
        private static void ClosePanel(string id, ref bool flag) { if (!flag) return; flag = false; FloodEveningHub.NotifyClosed(id); }

        private void Cleanup()
        {
            if (run != null) { StopCoroutine(run); run = null; }
            if (player != null) player.ExternalVelocity = Vector2.zero;
            if (cam != null) cam.ShakeOffset = Vector2.zero;
            ClearPose();
            if (hud != null) hud.HideAll();
            if (fx != null) fx.Stop();
            GameAudio.StopLoop(audioChannel, 0.3f);
            ClosePanel(CinePanelId, ref cineOpen);
            ClosePanel(InfoPanelId, ref infoOpen);
            ClosePanel(WindPanelId, ref windOpen);
            push = 0; stumbleT = recoverT = gustT = 0f; stumbledThisGust = false; Telegraphing = false;
        }

        public void ResetModule()
        {
            Cleanup();
            phase = Phase.Idle;
            StumbleCount = 0;
        }
    }
}
