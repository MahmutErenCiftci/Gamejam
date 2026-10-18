using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AfetUyanisi.Cutscenes
{
    /// <summary>
    /// Sel F_D2 yanlış seçimi (F_D2_A "Arkadaşlarınla geç") için kısa "sele kapılma" ara sahnesi (Eren).
    /// Kendi küçük izometrik sokağını, kendi kamerasıyla çizer: Cenk kaldırımdan suya iner, akıntıda yalpalar,
    /// dengesini kaybeder, birkaç metre sürüklenir, ekran kararır. Uzun/korkutucu sahne yok; uyarı metni burada
    /// değil, bölüm akışının oynattığı geri bildirim/diyalogdadır (OYUN_AKISI §6.2, AGENTS §5).
    /// Kullanım (Mahmut): prefabı bölüm sahnesinde dünyadan uzak bir yere (ör. x = 200) koy, sonra
    /// <c>yield return sweepCutscene.Play();</c>. Oynarken kendi kamerası (depth 50) ekranı kaplar, bitince kapanır.
    /// Görseller Mahmut'un sprite'larıdır (Inspector'dan bağlı); su, bariyer, araç ve efektler kodla üretilir ve
    /// tek bir atlas dokusunda toplanır.
    /// </summary>
    public class FloodSweepCutscene : MonoBehaviour
    {
        private const float Ppu = 64f;
        private const int ViewWidthPx = 384, ViewHeightPx = 216;

        [Header("Mahmut'un görselleri")]
        [Tooltip("CHR_Cenk_Walk_SouthEast_0..5")]
        [SerializeField] private Sprite[] walkFrames;
        [Tooltip("CHR_Cenk_Startled_SouthEast: suda yalpalarken")]
        [SerializeField] private Sprite startledSouthEast;
        [Tooltip("CHR_Cenk_Startled_SouthWest: akıntıyla sürüklenirken")]
        [SerializeField] private Sprite startledSouthWest;
        [SerializeField] private Sprite[] roadTiles;
        [SerializeField] private Sprite laneTile;
        [SerializeField] private Sprite[] sidewalkTiles;
        [SerializeField] private Sprite curbTile;
        [SerializeField] private Sprite[] facades;
        [SerializeField] private Sprite lampSprite;
        [SerializeField] private Sprite treeSprite;
        [Tooltip("PROP_Street_Sky (pivot alt kenarda)")]
        [SerializeField] private Sprite skySprite;
        [SerializeField] private Sprite[] rainDropSprites;
        [SerializeField] private Sprite rainSplashSprite;
        [SerializeField] private Sprite rainRippleSprite;
        [Tooltip("Sprite-Unlit-Default: sahnenin ışıklarından etkilenmesin (renkleri bu bileşen ayarlar)")]
        [SerializeField] private Material spriteMaterial;

        [Header("Kamera")]
        [SerializeField] private float cameraDepth = 50f;
        [SerializeField] private Color backgroundColor = new Color32(38, 42, 54, 255);
        [Tooltip("Düşüş anındaki hafif sarsıntı (piksel); 0 = kapalı")]
        [SerializeField] private float fallShakePx = 2f;

        [Header("Zamanlama (sn)")]
        [SerializeField] private float walkEnd = 1.4f;
        [SerializeField] private float enterEnd = 2.2f;
        [SerializeField] private float struggleEnd = 3.1f;
        [SerializeField] private float fallEnd = 3.55f;
        [SerializeField] private float sweptEnd = 6.4f;
        [SerializeField] private float fadeStart = 6.5f;
        [SerializeField] private float endTime = 7.2f;

        [Header("Su")]
        [Tooltip("Sürüklenirken suyun üstünde kalan kısım: ayaktan bu kadar piksel su altında (baş ve kollar görünür)")]
        [SerializeField] private float sweptCutPx = 25f;
        [Tooltip("Akıntının en hızlı çizgi hızı (karo/sn)")]
        [SerializeField] private float flowSpeed = 2.4f;

        [Header("Deneme")]
        [Tooltip("Test sahnesi için: Start'ta kendiliğinden oynar")]
        [SerializeField] private bool playOnStart;
        [Tooltip("Test sahnesi için: bitince baştan oynar")]
        [SerializeField] private bool loop;

        /// <summary>Ara sahne bitip ekran karardığında.</summary>
        public event Action Finished;

        public bool IsPlaying { get; private set; }
        public float Duration => endTime;
        /// <summary>Cenk'in suya gömüldüğü an (sn; büyük sıçrama). Ses kancası için (P-FLOOD: FloodSchoolController).</summary>
        public float FallTime => fallEnd;
        /// <summary>Cenk'in suya girdiği an (sn): akıntı sesi buradan başlar.</summary>
        public float EnterWaterTime => walkEnd;

        // Sıralama (hepsi kendi kamerasında, dünyadan uzakta)
        private const int OrderSky = -1000, OrderFacade = -900, OrderGround = -800, OrderWater = -700,
            OrderWaterEdge = -695, OrderReflection = -693, OrderStreak = -690, OrderCrest = -688, OrderRipple = -685,
            OrderDebris = -680, OrderProp = 0, OrderFoamBack = 95, OrderCenk = 100, OrderFoamFront = 105,
            OrderAlarm = 110, OrderCar = 120, OrderGlow = 125, OrderParticle = 130, OrderRainFar = 480,
            OrderRain = 500, OrderStorm = 870, OrderVignette = 880, OrderFade = 900;

        private static readonly Color GroundTint = new Color(0.70f, 0.74f, 0.84f);
        private static readonly Color FacadeTint = new Color(0.66f, 0.70f, 0.82f);
        private static readonly Color CenkTint = new Color(0.92f, 0.93f, 0.98f);
        private static readonly Color32 Outline = new Color32(27, 26, 43, 255);
        private static readonly Color32 Foam = new Color32(214, 212, 192, 255);
        private static readonly Color32 StreakColor = new Color32(160, 156, 130, 255);
        private static readonly Color FadeColor = new Color32(10, 12, 22, 255);
        private static readonly Color StormColor = new Color32(16, 20, 36, 255);

        private sealed class Particle
        {
            public SpriteRenderer Renderer;
            public Vector2 Position, Velocity;
            public float FloorY, Life, MaxLife, Size;
            public bool Alive;
        }

        private sealed class Mover
        {
            public SpriteRenderer Renderer;
            public float Gx, Gy, Speed, Phase;
        }

        // Kodla üretilen bütün görseller (tek doku).
        private sealed class GeneratedSprites
        {
            public Sprite Pixel, Block, Streak, Glow, Vignette, Alarm, Barrier, Car, Plank, Leaf, Bubble, Arm;
            public Sprite[] Crest, FoamRing, EdgeFoam, WaterShallow, WaterMid, WaterDeep;
            public Vector2 CarLightA, CarLightB;
        }

        private GeneratedSprites gen;
        private GameObject diorama;
        private Transform cameraTransform;
        private Camera cutsceneCamera;
        private SpriteRenderer cenk, foamBack, foamFront, alarm, fade, storm, lampGlow, lightning, armLeft, armRight;
        private bool leftHandInWater, rightHandInWater;
        private float flailBudget;
        private SpriteMask cenkMask;
        private readonly List<(SpriteRenderer renderer, Vector3 basePos, float gy)> edgeFoam = new List<(SpriteRenderer, Vector3, float)>();
        private readonly List<(SpriteRenderer renderer, int tone, Vector3 basePos, float gx, float gy)> waterTiles =
            new List<(SpriteRenderer, int, Vector3, float, float)>();
        private float swellFoamBudget;
        private readonly List<SpriteRenderer> reflections = new List<SpriteRenderer>();
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<Particle> ripples = new List<Particle>();
        private readonly List<Particle> wakes = new List<Particle>();
        private readonly List<Particle> bubbles = new List<Particle>();
        private readonly List<Mover> streaks = new List<Mover>();
        private readonly List<Mover> crests = new List<Mover>();
        private readonly List<Mover> debris = new List<Mover>();
        private readonly List<(SpriteRenderer renderer, float speed)> drops = new List<(SpriteRenderer, float)>();
        private readonly List<(SpriteRenderer renderer, float speed)> farDrops = new List<(SpriteRenderer, float)>();
        private SpriteRenderer[] hazardLights, hazardGlows;
        private Vector2 carCenter;
        private readonly float[] splashTimes = new float[9];
        private readonly bool[] splashDone = new bool[9];
        private float rainBudget, wakeBudget, bubbleBudget, carRippleBudget;
        private Vector2 cameraVelocity, cameraBase;

        private void Start()
        {
            if (playOnStart) StartCoroutine(TestLoop());
        }

        private IEnumerator TestLoop()
        {
            do
            {
                yield return Play();
                yield return new WaitForSeconds(0.6f);
            } while (loop);
        }

        /// <summary>Ara sahneyi baştan sona oynatır; bittiğinde ekran kararmış ve kamera kapanmış olur.</summary>
        public IEnumerator Play()
        {
            if (IsPlaying) yield break;
            IsPlaying = true;
            Build();
            ResetState();
            diorama.SetActive(true);

            float t = 0f;
            Tick(0f, 0f);
            while (t < endTime)
            {
                yield return null;
                float dt = Time.deltaTime;
                t += dt;
                Tick(t, dt);
            }

            diorama.SetActive(false);
            IsPlaying = false;
            Finished?.Invoke();
        }

        /// <summary>
        /// Önizleme (editörde de çalışır): sahneyi kurar ve time saniyesine kadar 30 fps adımlarla ilerletir.
        /// Kamera açık kalır; ClearPreview ile kaldırılır. Editörde kurulan objeler sahneye kaydedilmez.
        /// </summary>
        public Camera SimulateTo(float time)
        {
            if (IsPlaying) return cutsceneCamera;
            Build();
            ResetState();
            diorama.SetActive(true);
            const float step = 1f / 30f;
            Tick(0f, 0f);
            for (float t = step; t <= time; t += step) Tick(t, step);
            return cutsceneCamera;
        }

        /// <summary>SimulateTo ile kurulan önizlemeyi kaldırır.</summary>
        public void ClearPreview()
        {
            if (IsPlaying || diorama == null) return;
            if (Application.isPlaying) Destroy(diorama);
            else DestroyImmediate(diorama);
            diorama = null;
            edgeFoam.Clear();
            waterTiles.Clear();
            reflections.Clear();
            particles.Clear();
            ripples.Clear();
            wakes.Clear();
            bubbles.Clear();
            streaks.Clear();
            crests.Clear();
            debris.Clear();
            drops.Clear();
            farDrops.Clear();
        }

        // ---------- kurulum ----------

        private void Build()
        {
            if (diorama != null) return;
            if (gen == null) gen = GenerateSprites();
            diorama = new GameObject("Diorama");
            if (!Application.isPlaying) diorama.hideFlags = HideFlags.DontSave;
            diorama.transform.SetParent(transform, false);
            diorama.SetActive(false);

            BuildCamera();
            BuildStreet();
            BuildWater();
            BuildProps();
            BuildCenk();
            BuildEffects();
        }

        private void BuildCamera()
        {
            var go = new GameObject("CutsceneCamera");
            go.transform.SetParent(diorama.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -10f);
            cameraTransform = go.transform;
            cutsceneCamera = go.AddComponent<Camera>();
            cutsceneCamera.orthographic = true;
            // 384×216 piksel: Mahmut'un 640×360 kamerasından yakın (Cenk iri görünsün); 1080p'de tam 5×.
            cutsceneCamera.orthographicSize = ViewHeightPx / Ppu / 2f;
            cutsceneCamera.depth = cameraDepth;
            cutsceneCamera.clearFlags = CameraClearFlags.SolidColor;
            cutsceneCamera.backgroundColor = backgroundColor;
            var ppc = go.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 64;
            ppc.refResolutionX = ViewWidthPx;
            ppc.refResolutionY = ViewHeightPx;

            if (skySprite != null)
            {
                var sky = Create("Sky", skySprite, OrderSky, cameraTransform, new Color(0.62f, 0.66f, 0.76f));
                sky.transform.localPosition = new Vector3(0f, -1.2f, 10f); // pivot alt kenarda
                sky.transform.localScale = new Vector3(2.25f, 2.25f, 1f);
            }

            storm = Create("StormDim", gen.Pixel, OrderStorm, cameraTransform, Color.clear);
            storm.transform.localPosition = new Vector3(0f, 0f, 10f);
            storm.transform.localScale = new Vector3(12f * Ppu, 7f * Ppu, 1f);

            lightning = Create("Lightning", gen.Pixel, OrderStorm + 1, cameraTransform, Color.clear);
            lightning.transform.localPosition = new Vector3(0f, 0f, 10f);
            lightning.transform.localScale = new Vector3(12f * Ppu, 7f * Ppu, 1f);

            var vignette = Create("Vignette", gen.Vignette, OrderVignette, cameraTransform, Color.white);
            vignette.transform.localPosition = new Vector3(0f, 0f, 10f);
            vignette.transform.localScale = new Vector3((ViewWidthPx + 8f) / 80f, (ViewHeightPx + 6f) / 45f, 1f);

            fade = Create("Fade", gen.Pixel, OrderFade, cameraTransform, FadeColor);
            fade.transform.localPosition = new Vector3(0f, 0f, 10f);
            fade.transform.localScale = new Vector3(12f * Ppu, 7f * Ppu, 1f);
        }

        // Binalar gx = 0 çizgisinde (duvarlar +gx'e bakar), kaldırım gx 0..2, yol gx 2..14 (akıntı +gy yönünde).
        private void BuildStreet()
        {
            var root = Group("Street");
            if (facades != null && facades.Length > 0)
            {
                int k = 0;
                for (int a = -14; a <= 14; a += 4, k++)
                {
                    var s = facades[(k * 7 + 3) % facades.Length];
                    if (s == null) continue;
                    var f = Create("Facade", s, OrderFacade + k, root, FacadeTint);
                    f.transform.localPosition = Snap(Iso(0f, a + 4f));
                }
            }

            for (int i = 0; i <= 14; i++)
            {
                for (int j = -14; j <= 16; j++)
                {
                    Sprite s;
                    if (i == 1 && curbTile != null) s = curbTile;
                    else if (i <= 1) s = Pick(sidewalkTiles, i + j);
                    else if (i == 6 && laneTile != null) s = laneTile;
                    else s = Pick(roadTiles, i * 3 + j);
                    if (s == null) continue;
                    var tile = Create("Tile", s, OrderGround, root, GroundTint);
                    PlaceCentered(tile, Iso(i + 0.5f, j + 0.5f));
                }
            }
        }

        private void BuildWater()
        {
            var root = Group("Water");
            for (int i = 2; i <= 14; i++)
            {
                int tone = i == 2 ? 0 : i <= 4 ? 1 : 2;
                for (int j = -14; j <= 16; j++)
                {
                    var w = Create("WaterTile", WaterFrames(tone)[0], OrderWater, root, Color.white);
                    PlaceCentered(w, Iso(i + 0.5f, j + 0.5f) + Vector2.up * (4f / Ppu));
                    waterTiles.Add((w, tone, w.transform.localPosition, i + 0.5f, j + 0.5f));
                    // Kabaran karonun altında açılan boşluk yolu göstermesin: altta aynı tonda sabit su katmanı
                    var under = Create("WaterUnder", WaterFrames(tone)[1], OrderWater - 1, root, new Color(0.84f, 0.84f, 0.84f));
                    under.transform.localPosition = w.transform.localPosition;
                }
            }

            for (int j = -14; j <= 16; j++)
            {
                var e = Create("EdgeFoam", gen.EdgeFoam[0], OrderWaterEdge, root, Color.white);
                PlaceCentered(e, Iso(2.5f, j + 0.5f) + Vector2.up * (4f / Ppu));
                edgeFoam.Add((e, e.transform.localPosition, j + 0.5f));
            }

            for (int n = 0; n < 110; n++)
                streaks.Add(new Mover { Renderer = Create("Streak", gen.Streak, OrderStreak, root, Color.white) });
            for (int n = 0; n < 56; n++)
                crests.Add(new Mover { Renderer = Create("Crest", gen.Crest[n % 2], OrderCrest, root, Color.white) });

            // Vitrin ve pencere ışıklarının kaldırım dibindeki suya düşen titrek yansımaları
            for (int n = 0; n < 16; n++)
            {
                float gy = -13f + n * 1.75f + (n % 3) * 0.4f;
                var r = Create("WindowReflection", gen.Pixel, OrderReflection, root, new Color(1f, 0.8f, 0.45f, 0.4f));
                r.transform.localPosition = Snap(Iso(2.2f + (n % 2) * 0.25f, gy) + Vector2.up * (4f / Ppu));
                r.transform.localScale = new Vector3(4f + (n % 3) * 2f, 1f, 1f);
                reflections.Add(r);
            }

            for (int n = 0; n < 9; n++)
            {
                bool plank = n % 3 == 0;
                var r = Create("Debris", plank ? gen.Plank : gen.Leaf, OrderDebris, root, Color.white);
                debris.Add(new Mover { Renderer = r });
            }
        }

        // Suda kalmış araç: Cenk'in sürüklenme yolunun dışında, yolun ortasında.
        private const float CarGx = 7.2f, CarGy = 5.2f;

        private void BuildProps()
        {
            var root = Group("Props");

            if (lampSprite != null)
            {
                var lamp = Create("Lamp", lampSprite, OrderProp, root, GroundTint);
                lamp.transform.localPosition = Snap(Iso(0.35f, -2.2f));
                lampGlow = Create("LampGlow", gen.Glow, OrderGlow, root, new Color(1f, 0.85f, 0.55f, 0.35f));
                lampGlow.transform.localPosition = (Vector2)lamp.transform.localPosition + new Vector2(0.32f, 1.48f);
                lampGlow.transform.localScale = new Vector3(2.2f, 1.6f, 1f);
                var pool = Create("LampPool", gen.Glow, OrderWaterEdge, root, new Color(1f, 0.85f, 0.55f, 0.14f));
                pool.transform.localPosition = (Vector2)lamp.transform.localPosition + new Vector2(0.5f, -0.1f);
                pool.transform.localScale = new Vector3(3.2f, 1.4f, 1f);

                // Lambanın suya düşen kırık yansıması
                for (int k = 0; k < 7; k++)
                {
                    var r = Create("LampReflection", gen.Pixel, OrderReflection, root, new Color(1f, 0.84f, 0.5f, 0.5f));
                    r.transform.localPosition = Snap(Iso(2.15f + k * 0.28f, -2.05f + k * 0.12f) + Vector2.up * (4f / Ppu));
                    r.transform.localScale = new Vector3(Mathf.Max(2f, 6f - k * 0.6f), 1f, 1f);
                    reflections.Add(r);
                }
            }

            if (treeSprite != null)
            {
                var tree = Create("Tree", treeSprite, OrderProp, root, GroundTint);
                tree.transform.localPosition = Snap(Iso(0.6f, 3.6f));
            }

            var barrier = Create("Barrier", gen.Barrier, OrderProp + 1, root, Color.white);
            barrier.transform.localPosition = Snap(Iso(1.88f, 1.0f));

            // Suda kalmış araç (dörtlüler yanıp söner): araçlar bile geçemiyor.
            var car = Create("StuckCar", gen.Car, OrderCar, root, new Color(0.85f, 0.86f, 0.92f));
            car.transform.localPosition = Snap(Iso(CarGx, CarGy));
            carCenter = Iso(CarGx + 0.4f, CarGy + 0.8f);
            hazardLights = new SpriteRenderer[2];
            hazardGlows = new SpriteRenderer[2];
            var lights = new[] { gen.CarLightA, gen.CarLightB };
            for (int i = 0; i < 2; i++)
            {
                hazardLights[i] = Create("HazardLight", gen.Pixel, OrderCar + 1, car.transform, new Color(1f, 0.65f, 0.2f));
                hazardLights[i].transform.localPosition = lights[i];
                hazardLights[i].transform.localScale = new Vector3(2f, 2f, 1f);
                hazardGlows[i] = Create("HazardGlow", gen.Glow, OrderGlow, car.transform, new Color(1f, 0.6f, 0.15f, 0.3f));
                hazardGlows[i].transform.localPosition = lights[i];
                hazardGlows[i].transform.localScale = new Vector3(0.9f, 0.6f, 1f);
            }
        }

        private void BuildCenk()
        {
            var root = Group("Cenk");
            foamBack = Create("FoamBack", gen.FoamRing[0], OrderFoamBack, root, Color.white);
            cenk = Create("Cenk", walkFrames != null && walkFrames.Length > 0 ? walkFrames[0] : startledSouthEast, OrderCenk, root, CenkTint);
            cenk.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            foamFront = Create("FoamFront", gen.FoamRing[1], OrderFoamFront, root, Color.white);

            // Su çizgisinin üstünü gösteren maske (pivot alt kenarda): Cenk'in suyun altında kalan kısmı görünmez.
            var maskGo = new GameObject("WaterlineMask");
            maskGo.transform.SetParent(root, false);
            cenkMask = maskGo.AddComponent<SpriteMask>();
            cenkMask.sprite = gen.Block;
            maskGo.transform.localScale = new Vector3(60f, 60f, 1f);

            alarm = Create("Alarm", gen.Alarm, OrderAlarm, root, Color.white);

            // Çırpınan kollar (kodla çizilir): omuzlardan çıkar, gövdenin arkasında; su altında kalan kısmı görünmez.
            armLeft = Create("ArmLeft", gen.Arm, OrderCenk - 1, root, CenkTint);
            armRight = Create("ArmRight", gen.Arm, OrderCenk - 1, root, CenkTint);
            armLeft.maskInteraction = armRight.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            armLeft.enabled = armRight.enabled = false;
        }

        private void BuildEffects()
        {
            var root = Group("Effects");
            for (int n = 0; n < 160; n++)
                particles.Add(new Particle { Renderer = Create("Spray", gen.Pixel, OrderParticle, root, (Color)Foam) });
            for (int n = 0; n < 34; n++)
                ripples.Add(new Particle { Renderer = Create("Ripple", rainRippleSprite != null ? rainRippleSprite : gen.FoamRing[0], OrderRipple, root, Color.white) });
            for (int n = 0; n < 90; n++)
                wakes.Add(new Particle { Renderer = Create("Wake", gen.Streak, OrderStreak + 1, root, (Color)Foam) });
            for (int n = 0; n < 16; n++)
                bubbles.Add(new Particle { Renderer = Create("Bubble", gen.Bubble, OrderFoamFront + 1, root, Color.white) });

            for (int n = 0; n < 150; n++)
            {
                var s = rainDropSprites != null && rainDropSprites.Length > 0 ? rainDropSprites[n % rainDropSprites.Length] : gen.Pixel;
                drops.Add((Create("Drop", s, OrderRain, cameraTransform, new Color(0.78f, 0.83f, 0.95f, 0.75f)), UnityEngine.Random.Range(8.5f, 11f)));
            }
            for (int n = 0; n < 130; n++)
            {
                var r = Create("FarDrop", gen.Pixel, OrderRainFar, cameraTransform, new Color(0.7f, 0.76f, 0.9f, 0.3f));
                r.transform.localScale = new Vector3(1f, 3f, 1f);
                farDrops.Add((r, UnityEngine.Random.Range(5f, 6.5f)));
            }
        }

        private Sprite[] WaterFrames(int tone) => tone == 0 ? gen.WaterShallow : tone == 1 ? gen.WaterMid : gen.WaterDeep;

        private void ResetState()
        {
            splashTimes[0] = walkEnd + 0.2f;
            splashTimes[1] = walkEnd + 0.55f;
            splashTimes[2] = enterEnd + 0.25f;
            splashTimes[3] = struggleEnd - 0.35f;
            splashTimes[4] = struggleEnd + 0.08f;
            splashTimes[5] = fallEnd - 0.05f;   // suya gömülüş (büyük)
            splashTimes[6] = SweptTime(0.4f);   // yüzeye çıkış
            splashTimes[7] = SweptTime(1.95f);  // ikinci çıkış
            splashTimes[8] = SweptTime(2.7f);   // son batış
            Array.Clear(splashDone, 0, splashDone.Length);
            foreach (var p in particles) Kill(p);
            foreach (var p in ripples) Kill(p);
            foreach (var p in wakes) Kill(p);
            foreach (var p in bubbles) Kill(p);
            foreach (var s in streaks) RespawnStreak(s, true);
            foreach (var c in crests) RespawnCrest(c, true);
            for (int i = 0; i < debris.Count; i++)
            {
                var d = debris[i];
                d.Gx = 2.6f + (i * 1.37f) % 6f;
                d.Gy = -12f + i * 3.2f;
                d.Speed = flowSpeed * UnityEngine.Random.Range(0.45f, 0.7f);
                d.Phase = i * 1.7f;
            }
            foreach (var d in drops) d.renderer.transform.localPosition = RandomRainPoint(true);
            foreach (var d in farDrops) d.renderer.transform.localPosition = RandomRainPoint(true);
            rainBudget = wakeBudget = bubbleBudget = carRippleBudget = swellFoamBudget = 0f;
            leftHandInWater = rightHandInWater = false;
            flailBudget = 0f;
            cameraVelocity = Vector2.zero;
            cameraBase = CameraFocus(Iso(State(0f).Gx, State(0f).Gy));
            cameraTransform.localPosition = new Vector3(cameraBase.x, cameraBase.y, -10f);
        }

        // ---------- kare kare ----------

        private struct CenkState
        {
            public float Gx, Gy, CutPx, Angle;
            public Sprite Sprite;
            public bool Alarm;
            public float Flail;   // kolların çırpınma şiddeti (0..1)
            public float Reach;   // yarı batıkken tek kol yukarı uzanır (0..1)
            public float Since;   // fallEnd'den beri geçen süre (çırpınma ritmi için)
        }

        // Cenk tamamen su altında (sprite'ın ayaktan yukarısı ~60 px).
        private const float SubmergedPx = 64f;
        // Büyük kabarmanın Cenk'e çarptığı nokta (struggleEnd anındaki konumu): yolun içinde, kaldırımdan uzakta.
        private const float HitGx = 3.9f, HitGy = 0.6f;

        // Su derinliği: kaldırım dibinde sığ, yolun ortasına doğru derinleşir (Cenk'in suda kalan kısmı, px).
        private static float DepthPx(float gx) => Mathf.Clamp01((gx - 2f) / 2.2f) * 30f;

        // Sürüklenme sırasında batıp çıkma (fallEnd'den itibaren sn, varsayılan 2.85 sn'lik süreye göre;
        // gerçek süreye ölçeklenir). Değer: su altında kalan piksel; base = sweptCutPx (yalnız baş görünür).
        private static readonly (float time, float offset)[] SweptCurve =
        {
            (0f, 99f), (0.15f, 99f),         // devrilip kısa bir an tamamen batar (kabarcıklar)
            (0.45f, -5f), (1.2f, -3f),       // yüzeye çıkar, çırpınır (baş ve omuzlar)
            (1.45f, 10f), (1.8f, 10f),       // akıntı yeniden çeker: baş zar zor görünür, tek kol uzanır
            (2.0f, -5f), (2.5f, -3f),        // bir kez daha çıkar, çırpınır
            (2.8f, 99f)                      // ve suyun altına çekilir; ekran kararır
        };

        private float SweptCut(float since)
        {
            float scale = (sweptEnd - fallEnd) / 2.85f;
            float Value(float offset) => offset > 50f ? SubmergedPx : sweptCutPx + offset;
            if (since <= 0f) return Value(SweptCurve[0].offset);
            for (int i = 1; i < SweptCurve.Length; i++)
            {
                float t1 = SweptCurve[i].time * scale;
                if (since > t1) continue;
                float t0 = SweptCurve[i - 1].time * scale;
                float k = Mathf.SmoothStep(0f, 1f, (since - t0) / Mathf.Max(0.001f, t1 - t0));
                return Mathf.Lerp(Value(SweptCurve[i - 1].offset), Value(SweptCurve[i].offset), k);
            }
            return SubmergedPx;
        }

        private float SweptTime(float fraction) => fallEnd + fraction / 2.85f * (sweptEnd - fallEnd);

        private CenkState State(float t)
        {
            var s = new CenkState();
            int frames = walkFrames != null ? walkFrames.Length : 0;
            if (t < walkEnd)
            {
                float k = t / walkEnd;
                s.Gx = Mathf.Lerp(0.55f, 1.95f, k);
                s.Sprite = frames > 0 ? walkFrames[(int)(t * 10f) % frames] : startledSouthEast;
            }
            else if (t < enterEnd)
            {
                // Kaldırımdan iner, karşıya geçmek için yolun içine yürür; su gitgide derinleşir.
                float k = (t - walkEnd) / (enterEnd - walkEnd);
                s.Gx = Mathf.Lerp(1.95f, 3.3f, k);
                s.Gy = 0.15f * k * k;
                s.Sprite = frames > 0 ? walkFrames[(int)(t * 7f) % frames] : startledSouthEast;
                s.CutPx = DepthPx(s.Gx);
            }
            else if (t < struggleEnd)
            {
                // Akıntıya karşı zorlanarak ilerler, akıntı onu yavaşça aşağı iter; yaklaşan kabarmayı görür.
                float k = (t - enterEnd) / (struggleEnd - enterEnd);
                s.Gx = Mathf.Lerp(3.3f, HitGx, Mathf.Sin(k * Mathf.PI * 0.5f));
                s.Gy = Mathf.Lerp(0.15f, HitGy, k * k);
                s.Sprite = startledSouthEast;
                s.CutPx = DepthPx(s.Gx) + Mathf.Round(Mathf.Sin(t * 6f));
                s.Angle = 3f * Mathf.Sin(t * 7f) + 6f * k; // akıntı +gy yönüne (ekranda sola) iter
                s.Alarm = true;
            }
            else if (t < fallEnd)
            {
                // Büyük kabarma çarpar: devrilir ve suya gömülür.
                float k = (t - struggleEnd) / (fallEnd - struggleEnd);
                s.Gx = HitGx + 0.2f * k;
                s.Gy = HitGy + 0.7f * k;
                s.Sprite = startledSouthEast;
                s.CutPx = Mathf.Lerp(DepthPx(HitGx), SubmergedPx, Mathf.SmoothStep(0f, 1f, k));
                s.Angle = 6f + 12f * Mathf.SmoothStep(0f, 1f, k);
                s.Alarm = k < 0.3f;
                s.Flail = Mathf.SmoothStep(0f, 1f, k * 2f); // düşerken kollar savrulmaya başlar
                s.Since = t - fallEnd;
            }
            else
            {
                // Akıntı onu hızlanarak yolun ortasına ve aşağıya sürükler; batıp çıkar.
                float since = t - fallEnd;
                float duration = sweptEnd - fallEnd;
                float k = Mathf.Clamp01(since / duration);
                // sweptEnd'den sonra aynı hızla devam eder (hız birden değişmesin)
                float extra = Mathf.Max(0f, t - sweptEnd) * 6.2f * 1.35f / duration;
                s.Gx = HitGx + 0.2f + 1.3f * Mathf.SmoothStep(0f, 1f, k);
                s.Gy = HitGy + 0.7f + 6.2f * Mathf.Pow(k, 1.35f) + extra;
                float cut = SweptCut(since);
                // Yüzeyde olma ağırlığı (0..1): geçişlerde çırpınma birden açılıp kapanmasın
                float surfaced = Mathf.Clamp01((sweptCutPx + 3f - cut) / 6f);
                float reach = Mathf.Clamp01((cut - (sweptCutPx + 2f)) / 5f) * Mathf.Clamp01((SubmergedPx - 4f - cut) / 6f);
                // Poz sık değişmez: ilk çıkışta güneydoğuya, akıntı çevirdikten sonra (ikinci çıkış) güneybatıya bakar.
                bool turned = since > 1.45f * duration / 2.85f && startledSouthWest != null;
                s.Sprite = turned ? startledSouthWest : startledSouthEast;
                // Çırpınma: baş ritmik olarak ağız hizasına batıp nefes almak için yükselir, gövde yalpalar,
                // kollar sırayla suya vurur (Tick). Yarı batıkken tek kol yukarı uzanır.
                float bob = 4f * surfaced * Mathf.Sin(since * FlailRate * 2f * Mathf.PI);
                s.CutPx = Mathf.Round(cut + bob);
                s.Angle = surfaced * (7f * Mathf.Sin(since * FlailRate * Mathf.PI) + 3f * Mathf.Sin(since * FlailRate * 2f * Mathf.PI + 1f));
                s.Flail = surfaced;
                s.Reach = reach;
                s.Since = since;
            }
            return s;
        }

        private void Tick(float t, float dt)
        {
            var s = State(t);
            Vector2 feet = Snap(Iso(s.Gx, s.Gy));
            // Kabarma gelince: ayaktayken su etrafında yükselir, sürüklenirken onu kaldırıp indirir.
            float swell = Mathf.Round(SwellPx(s.Gx, s.Gy, t));
            if (t >= fallEnd) feet.y += swell / Ppu;
            else if (s.CutPx > 0.5f) s.CutPx += swell;
            float waterY = feet.y + s.CutPx / Ppu;
            bool inWater = s.CutPx > 0.5f;

            cenk.sprite = s.Sprite;
            // Suyun içindeyken su çizgisi etrafında döner (ayaklar etrafında dönse görünen baş yana kayar)
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Round(s.Angle / 2f) * 2f);
            Vector2 pivot = inWater ? new Vector2(feet.x, waterY) : feet;
            cenk.transform.localPosition = Snap(pivot + (Vector2)(rotation * (feet - pivot)));
            cenk.transform.localRotation = rotation;
            cenkMask.transform.localPosition = new Vector2(feet.x, inWater ? waterY : feet.y - 2f);

            foamBack.enabled = foamFront.enabled = inWater;
            if (inWater)
            {
                int f = (int)(t * 8f) % 2;
                float pulse = 1f + 0.12f * Mathf.Sin(t * 9f) + 0.2f * s.Flail;
                foamBack.sprite = gen.FoamRing[f];
                foamFront.sprite = gen.FoamRing[1 - f];
                foamBack.transform.localPosition = Snap(new Vector2(feet.x, waterY + 1f / Ppu));
                foamFront.transform.localPosition = Snap(new Vector2(feet.x, waterY - 1f / Ppu));
                foamBack.transform.localScale = foamFront.transform.localScale = new Vector3(pulse, 1f, 1f);
            }

            alarm.enabled = s.Alarm && (int)(t * 6f) % 2 == 0;
            alarm.transform.localPosition = Snap(feet + new Vector2(-0.1f, 70f / Ppu));

            for (int i = 0; i < splashTimes.Length; i++)
            {
                if (splashDone[i] || t < splashTimes[i]) continue;
                splashDone[i] = true;
                bool big = i == 5, medium = i == 4 || i >= 6;
                Splash(new Vector2(feet.x, waterY), big ? 30 : medium ? 16 : 7, big ? 1.45f : medium ? 1.05f : 0.85f);
                SpawnRipple(new Vector2(feet.x, waterY), big ? 1.7f : medium ? 1.3f : 1f);
            }

            if (dt > 0f && t >= fallEnd && t < endTime - 0.2f)
            {
                wakeBudget += dt * 22f;
                while (wakeBudget >= 1f)
                {
                    wakeBudget -= 1f;
                    SpawnWake(new Vector2(feet.x + UnityEngine.Random.Range(-0.18f, 0.18f), waterY + UnityEngine.Random.Range(-0.03f, 0.02f)));
                }
                bubbleBudget += dt * (s.CutPx >= SubmergedPx - 4f ? 20f : 5f); // su altındayken kabarcık yoğun
                while (bubbleBudget >= 1f)
                {
                    bubbleBudget -= 1f;
                    SpawnBubble(new Vector2(feet.x + UnityEngine.Random.Range(-0.22f, 0.22f), waterY + UnityEngine.Random.Range(-0.04f, 0.03f)));
                }
            }

            UpdateArms(s, feet, pivot, rotation, waterY, dt);

            // Çırpınırken etrafında sürekli çalkantı
            if (dt > 0f && s.Flail > 0.5f)
            {
                flailBudget += dt * 9f;
                while (flailBudget >= 1f)
                {
                    flailBudget -= 1f;
                    Splash(new Vector2(feet.x + UnityEngine.Random.Range(-0.28f, 0.28f), waterY), 2, 0.55f);
                    if (UnityEngine.Random.value < 0.35f) SpawnRipple(new Vector2(feet.x, waterY), 1.2f);
                }
            }

            UpdateWater(t, dt);
            UpdateParticles(dt);
            UpdateRain(t, dt);

            for (int i = 0; i < edgeFoam.Count; i++)
            {
                var (e, basePos, gy) = edgeFoam[i];
                e.sprite = gen.EdgeFoam[((int)(t * 5f) + i) % 2];
                e.transform.localPosition = basePos + Vector3.up * (Mathf.Round(SwellPx(2.5f, gy, t)) / Ppu);
            }
            bool blink = (int)(t * 2.2f) % 2 == 0;
            for (int i = 0; i < 2; i++) hazardLights[i].enabled = hazardGlows[i].enabled = blink;
            if (lampGlow != null) lampGlow.color = new Color(1f, 0.85f, 0.55f, 0.32f + 0.03f * Mathf.Sin(t * 3f));

            // Kamera: Cenk'i yumuşakça izler; düşüşte birkaç piksel sarsılır.
            Vector2 focus = CameraFocus(feet);
            cameraBase = dt > 0f ? Vector2.SmoothDamp(cameraBase, focus, ref cameraVelocity, 0.6f, 6f, dt) : focus;
            Vector2 shake = Vector2.zero;
            if (fallShakePx > 0f && t > struggleEnd && t < fallEnd + 0.25f)
            {
                float k = 1f - Mathf.Clamp01((t - struggleEnd) / (fallEnd + 0.25f - struggleEnd));
                shake = new Vector2(Mathf.Round(Mathf.Sin(t * 53f) * fallShakePx * k), Mathf.Round(Mathf.Cos(t * 41f) * fallShakePx * 0.5f * k)) / Ppu;
            }
            Vector2 cam = Snap(cameraBase) + shake;
            cameraTransform.localPosition = new Vector3(cam.x, cam.y, -10f);

            // Fırtına zamanla koyulaşır, sonda ekran kararır.
            storm.color = new Color(StormColor.r, StormColor.g, StormColor.b, 0.22f * Mathf.Clamp01(t / sweptEnd));
            lightning.color = new Color(0.85f, 0.9f, 1f, LightningAlpha(t));
            float fadeK = Mathf.Clamp01((t - fadeStart) / Mathf.Max(0.01f, endTime - fadeStart));
            float fadeIn = Mathf.Clamp01(1f - t / 0.4f);
            fade.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, Mathf.SmoothStep(0f, 1f, Mathf.Max(fadeK, fadeIn)));
        }

        // Çırpınma ritmi (tam kol döngüsü/sn) ve kolların gövdeye göre yeri (px, ayaktan)
        private const float FlailRate = 1.6f, ShoulderPx = 38f, ShoulderHalfPx = 9f, ArmLengthPx = 15f;

        // Kollar: çırpınırken sırayla yanlara savrulup suya vurur (vurunca su sıçrar); yarı batıkken
        // sol kol yukarı uzanıp sallanır. Su altında kalan kısımları maskeyle gizlenir.
        private void UpdateArms(CenkState s, Vector2 feet, Vector2 pivot, Quaternion bodyRotation, float waterY, float dt)
        {
            float weight = Mathf.Max(s.Flail, s.Reach);
            armLeft.enabled = armRight.enabled = weight > 0.05f;
            if (weight <= 0.05f)
            {
                leftHandInWater = rightHandInWater = true;
                return;
            }

            float bodyAngle = bodyRotation.eulerAngles.z;
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = side < 0 ? armLeft : armRight;
                Vector2 shoulderLocal = new Vector2(side * ShoulderHalfPx, ShoulderPx) / Ppu;
                Vector2 shoulder = pivot + (Vector2)(bodyRotation * (feet + shoulderLocal - pivot));

                // Dışa doğru açı (derece): 0 = dimdik yukarı, 90 = yana yatay, 130 = suyun içinde aşağıda
                float phase = s.Since * FlailRate * 2f * Mathf.PI + (side < 0 ? 0f : Mathf.PI);
                float flail = 25f + 75f * (0.5f + 0.5f * Mathf.Sin(phase));
                float reachAngle = side < 0 ? 6f + 9f * Mathf.Sin(s.Since * 2.2f * 2f * Mathf.PI) : 130f;
                float angle = Mathf.Lerp(flail, reachAngle, s.Reach / weight);
                angle = Mathf.Lerp(130f, angle, Mathf.Clamp01(weight)); // çırpınma başlarken/biterken kollar sudan çıkar/girer

                var armRotation = Quaternion.Euler(0f, 0f, bodyAngle - side * angle);
                arm.transform.localPosition = Snap(shoulder);
                arm.transform.localRotation = armRotation;

                // El suya vurdu mu? (yukarıdan aşağı geçiş)
                Vector2 hand = shoulder + (Vector2)(armRotation * new Vector2(0f, ArmLengthPx / Ppu));
                bool inWater = hand.y < waterY + 1f / Ppu;
                bool wasInWater = side < 0 ? leftHandInWater : rightHandInWater;
                if (dt > 0f && inWater && !wasInWater && s.Flail > 0.5f)
                {
                    Splash(new Vector2(hand.x, waterY), 7, 0.75f);
                    SpawnRipple(new Vector2(hand.x, waterY), 0.9f);
                }
                if (side < 0) leftHandInWater = inWater; else rightHandInWater = inWater;
            }
        }

        // Şimşek: çift çakma (kısa + uzun), biri başta biri sürüklenirken.
        private static readonly (float start, float length, float alpha)[] Flashes =
            { (0.7f, 0.04f, 0.16f), (0.79f, 0.08f, 0.22f), (4.9f, 0.04f, 0.14f), (4.99f, 0.07f, 0.2f) };

        private static float LightningAlpha(float t)
        {
            foreach (var (start, length, alpha) in Flashes)
                if (t >= start && t < start + length) return alpha * (1f - (t - start) / length * 0.5f);
            return 0f;
        }

        private static Vector2 CameraFocus(Vector2 cenkFeet) => Vector2.Lerp(Iso(3.4f, 1.2f), cenkFeet + new Vector2(-0.2f, 0.35f), 0.8f);

        // Ara ara gelen kabarmalar: suyun bazı kısımları birkaç piksel yükselip iner ve akıntıyla aşağı geçer.
        // time: kabarmanın Cenk'in satırından (HitGy) geçtiği an (sn; eksi = struggleEnd, Cenk'i deviren büyük kabarma).
        private static readonly (float time, float amp)[] Swells = { (1.0f, 5f), (-1f, 9f), (4.6f, 6f), (6.0f, 5f) };
        private const float SwellSpeed = 3.2f, SwellWidth = 1.0f;

        private float SwellPx(float gx, float gy, float t)
        {
            if (gx < 2.1f) return 0f;
            float edge = Mathf.Clamp01((gx - 2.1f) / 0.8f); // kaldırım dibinde sığ: az kabarır
            float sum = 0f;
            for (int k = 0; k < Swells.Length; k++)
            {
                var (time, amp) = Swells[k];
                float center = HitGy + (t - (time < 0f ? struggleEnd : time)) * SwellSpeed;
                float d = (gy - center) / SwellWidth;
                if (d * d > 9f) continue;
                // Yol boyunca parça parça: kabarma her yerde aynı yükseklikte değil (büyük olan Cenk'in hizasında en yüksek)
                float along = 0.55f + 0.45f * Mathf.Cos((gx - HitGx) * 0.9f + (time < 0f ? 0f : k * 1.7f));
                sum += amp * Mathf.Exp(-d * d) * along;
            }
            return sum * edge;
        }

        private void UpdateWater(float t, float dt)
        {
            // Su yüzeyi kıpırdar: bütün karolar aynı anda iki kare arasında gidip gelir (bantlar karo sınırında kopmaz).
            // Kabaran yerler yukarı kalkar ve hafifçe aydınlanır.
            int frame = (int)(t * 3.5f) & 1;
            foreach (var (renderer, tone, basePos, gx, gy) in waterTiles)
            {
                renderer.sprite = WaterFrames(tone)[frame];
                float lift = Mathf.Round(SwellPx(gx, gy, t));
                renderer.transform.localPosition = basePos + Vector3.up * (lift / Ppu);
                float v = 0.84f + 0.16f * Mathf.Clamp01(lift / 5f);
                renderer.color = new Color(v, v, v);
            }

            foreach (var s in streaks)
            {
                s.Gy += s.Speed * dt;
                if (s.Gy > 16f) RespawnStreak(s, false);
                s.Renderer.transform.localPosition = Snap(Iso(s.Gx, s.Gy) + Vector2.up * ((4f + Mathf.Round(SwellPx(s.Gx, s.Gy, t))) / Ppu));
            }

            foreach (var c in crests)
            {
                c.Gy += c.Speed * dt;
                if (c.Gy > 16f) RespawnCrest(c, false);
                float bob = Mathf.Round(Mathf.Sin(t * 5f + c.Phase) + SwellPx(c.Gx, c.Gy, t));
                c.Renderer.transform.localPosition = Snap(Iso(c.Gx, c.Gy) + Vector2.up * ((5f + bob) / Ppu));
            }

            foreach (var d in debris)
            {
                d.Gy += d.Speed * dt;
                if (d.Gy > 16f) { d.Gy = -14f; d.Gx = UnityEngine.Random.Range(2.6f, 8f); }
                float bob = Mathf.Round(Mathf.Sin(t * 3f + d.Phase) + SwellPx(d.Gx, d.Gy, t));
                d.Renderer.transform.localPosition = Snap(Iso(d.Gx, d.Gy) + Vector2.up * ((4f + bob) / Ppu));
            }

            // Kabarmaların sırtında köpük
            if (dt > 0f)
            {
                swellFoamBudget += dt * 90f;
                while (swellFoamBudget >= 1f)
                {
                    swellFoamBudget -= 1f;
                    int k = UnityEngine.Random.Range(0, Swells.Length);
                    float center = HitGy + (t - (Swells[k].time < 0f ? struggleEnd : Swells[k].time)) * SwellSpeed;
                    float gx = UnityEngine.Random.Range(2.6f, 12f);
                    float gy = center + UnityEngine.Random.Range(-0.35f, 0.25f);
                    float lift = SwellPx(gx, gy, t);
                    if (lift < 2f) continue;
                    SpawnWake(Iso(gx, gy) + Vector2.up * ((4f + lift) / Ppu), new Vector2(-0.5f, -0.25f) * (SwellSpeed * 0.6f), UnityEngine.Random.Range(0.35f, 0.6f));
                }
            }

            for (int k = 0; k < reflections.Count; k++)
                reflections[k].enabled = ((int)(t * 7f) + k * 3) % 5 != 0;

            if (dt <= 0f) return;
            rainBudget += dt * 24f;
            while (rainBudget >= 1f)
            {
                rainBudget -= 1f;
                float gx = UnityEngine.Random.Range(0.1f, 10f);
                float gy = UnityEngine.Random.Range(-8f, 10f);
                if (gx > 2.05f) SpawnRipple(Iso(gx, gy) + Vector2.up * (4f / Ppu), 0.7f);
                else if (rainSplashSprite != null) SpawnGroundSplash(Iso(gx, gy));
            }

            carRippleBudget += dt * 2.5f;
            while (carRippleBudget >= 1f)
            {
                carRippleBudget -= 1f;
                SpawnRipple(carCenter + new Vector2(UnityEngine.Random.Range(-0.5f, 0.6f), UnityEngine.Random.Range(-0.3f, 0.1f)), 1.1f);
            }
        }

        private void UpdateParticles(float dt)
        {
            foreach (var p in particles)
            {
                if (!p.Alive) continue;
                p.Velocity.y -= 7f * dt;
                p.Position += p.Velocity * dt;
                p.Renderer.transform.localPosition = Snap(p.Position);
                if (p.Position.y < p.FloorY && p.Velocity.y < 0f) Kill(p);
            }

            foreach (var p in ripples)
            {
                if (!p.Alive) continue;
                p.Life += dt;
                float k = p.Life / p.MaxLife;
                if (k >= 1f) { Kill(p); continue; }
                float scale = Mathf.Round(Mathf.Lerp(0.5f, 1.3f, k) * p.Size * 4f) / 4f;
                p.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
                p.Renderer.color = new Color(1f, 1f, 1f, 0.85f * (1f - k));
            }

            foreach (var p in wakes)
            {
                if (!p.Alive) continue;
                p.Life += dt;
                float k = p.Life / p.MaxLife;
                if (k >= 1f) { Kill(p); continue; }
                p.Position += p.Velocity * dt;
                p.Renderer.transform.localPosition = Snap(p.Position);
                p.Renderer.color = new Color(Foam.r / 255f, Foam.g / 255f, Foam.b / 255f, 0.9f * (1f - k));
            }

            foreach (var p in bubbles)
            {
                if (!p.Alive) continue;
                p.Life += dt;
                if (p.Life >= p.MaxLife) { Kill(p); continue; }
                p.Position += p.Velocity * dt;
                p.Renderer.transform.localPosition = Snap(p.Position);
            }
        }

        private void UpdateRain(float t, float dt)
        {
            float heavier = Mathf.Clamp01(t / sweptEnd);
            Vector2 fall = new Vector2(-0.18f, -1f);
            foreach (var (renderer, speed) in drops)
            {
                var tr = renderer.transform;
                Vector2 p = (Vector2)tr.localPosition + fall * (speed * dt);
                if (p.y < -3.2f) p = RandomRainPoint(false);
                tr.localPosition = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 10f);
            }
            foreach (var (renderer, speed) in farDrops)
            {
                var tr = renderer.transform;
                Vector2 p = (Vector2)tr.localPosition + fall * (speed * dt);
                if (p.y < -3.2f) p = RandomRainPoint(false);
                tr.localPosition = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 10f);
                renderer.color = new Color(0.7f, 0.76f, 0.9f, 0.22f + 0.2f * heavier);
            }
        }

        private static Vector3 RandomRainPoint(bool anywhere) =>
            new Vector3(UnityEngine.Random.Range(-5.6f, 6.2f), anywhere ? UnityEngine.Random.Range(-3.2f, 3.2f) : 3.2f, 10f);

        private void RespawnStreak(Mover s, bool anywhere)
        {
            s.Gx = UnityEngine.Random.value < 0.25f ? UnityEngine.Random.Range(2.05f, 2.4f) : UnityEngine.Random.Range(2.3f, 13f);
            s.Gy = anywhere ? UnityEngine.Random.Range(-14f, 16f) : UnityEngine.Random.Range(-15f, -13f);
            float middle = Mathf.Clamp01((s.Gx - 2f) / 4f);
            s.Speed = flowSpeed * UnityEngine.Random.Range(0.55f, 1f) * Mathf.Lerp(0.55f, 1f, middle);
            float a = UnityEngine.Random.Range(0.35f, 0.8f);
            var c = s.Gx < 2.4f ? Foam : StreakColor;
            s.Renderer.color = new Color(c.r / 255f, c.g / 255f, c.b / 255f, a);
        }

        // Dalga sırtları yolun ortasında, akıntının en hızlı yerinde.
        private void RespawnCrest(Mover c, bool anywhere)
        {
            c.Gx = UnityEngine.Random.Range(2.6f, 9f);
            c.Gy = anywhere ? UnityEngine.Random.Range(-14f, 16f) : UnityEngine.Random.Range(-16f, -13f);
            c.Speed = flowSpeed * UnityEngine.Random.Range(0.95f, 1.2f);
            c.Phase = UnityEngine.Random.Range(0f, 6.28f);
            c.Renderer.color = new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.55f, 0.9f));
        }

        private void Splash(Vector2 at, int count, float power)
        {
            int spawned = 0;
            foreach (var p in particles)
            {
                if (p.Alive) continue;
                p.Alive = true;
                p.Renderer.enabled = true;
                p.Position = at + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0f);
                p.Velocity = new Vector2(UnityEngine.Random.Range(-1.1f, 1.1f) * power, UnityEngine.Random.Range(1.1f, 2.2f) * power);
                p.FloorY = at.y - 0.02f;
                float size = UnityEngine.Random.value < 0.3f ? 2f : 1f;
                p.Renderer.transform.localScale = new Vector3(size, size, 1f);
                if (++spawned >= count) break;
            }
        }

        private void SpawnRipple(Vector2 at, float size) => SpawnFlat(at, rainRippleSprite != null ? rainRippleSprite : gen.FoamRing[0], 0.55f, size);

        private void SpawnGroundSplash(Vector2 at) => SpawnFlat(at, rainSplashSprite, 0.22f, 0.8f);

        private void SpawnFlat(Vector2 at, Sprite sprite, float life, float size)
        {
            foreach (var p in ripples)
            {
                if (p.Alive) continue;
                p.Alive = true;
                p.Renderer.enabled = true;
                p.Renderer.sprite = sprite;
                p.Renderer.transform.localPosition = Snap(at);
                p.Life = 0f;
                p.MaxLife = life;
                p.Size = size;
                return;
            }
        }

        // Cenk'in arkasındaki iz: akıntı yönünde, Cenk'ten yavaş.
        private void SpawnWake(Vector2 at) =>
            SpawnWake(at, new Vector2(-0.5f, -0.25f) * (flowSpeed * 0.35f), UnityEngine.Random.Range(0.45f, 0.8f));

        private void SpawnWake(Vector2 at, Vector2 velocity, float life)
        {
            foreach (var p in wakes)
            {
                if (p.Alive) continue;
                p.Alive = true;
                p.Renderer.enabled = true;
                p.Position = at;
                p.Velocity = velocity;
                p.Life = 0f;
                p.MaxLife = life;
                return;
            }
        }

        private void SpawnBubble(Vector2 at)
        {
            foreach (var p in bubbles)
            {
                if (p.Alive) continue;
                p.Alive = true;
                p.Renderer.enabled = true;
                p.Position = at;
                p.Velocity = new Vector2(-0.15f, 0.08f);
                p.Life = 0f;
                p.MaxLife = UnityEngine.Random.Range(0.25f, 0.45f);
                return;
            }
        }

        private static void Kill(Particle p)
        {
            p.Alive = false;
            p.Renderer.enabled = false;
        }

        // ---------- yardımcılar ----------

        // Izgara → yerel konum: gx ekranda sağ-aşağı, gy sol-aşağı; bir karo 64×32 piksel.
        private static Vector2 Iso(float gx, float gy) => new Vector2((gx - gy) * 0.5f, -(gx + gy) * 0.25f);

        private static Vector2 Snap(Vector2 v) => new Vector2(Mathf.Round(v.x * Ppu) / Ppu, Mathf.Round(v.y * Ppu) / Ppu);

        private static Sprite Pick(Sprite[] set, int index)
        {
            if (set == null || set.Length == 0) return null;
            return set[((index % set.Length) + set.Length) % set.Length];
        }

        private Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(diorama.transform, false);
            return go.transform;
        }

        private SpriteRenderer Create(string name, Sprite sprite, int order, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color;
            if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
            return sr;
        }

        // Sprite'ın görsel merkezini (pivotu ne olursa olsun) verilen noktaya oturtur.
        private static void PlaceCentered(SpriteRenderer sr, Vector2 center)
        {
            if (sr.sprite == null) return;
            sr.transform.localPosition = Snap(center - (Vector2)sr.sprite.bounds.center);
        }

        // ---------- kodla üretilen piksel görseller ----------

        private sealed class PixelCanvas
        {
            public readonly int Width, Height;
            public readonly Color32[] Pixels;

            public PixelCanvas(int width, int height)
            {
                Width = width;
                Height = height;
                Pixels = new Color32[width * height];
            }

            // y aşağı doğru artar (çizim koordinatı); dokuya ters çevrilerek yazılır.
            public void Set(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                Pixels[(Height - 1 - y) * Width + x] = c;
            }

            // Opak piksellere komşu boş piksellere kontur çizer (Mahmut'un sprite'larındaki koyu çerçeve gibi).
            public void AddOutline(Color32 color)
            {
                var src = (Color32[])Pixels.Clone();
                bool Solid(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && src[y * Width + x].a >= 200;
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        if (src[y * Width + x].a == 0 && (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1)))
                            Pixels[y * Width + x] = color;
            }

            public void Poly(Vector2[] pts, Color32 c)
            {
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                var xs = new List<float>();
                for (int y = Mathf.FloorToInt(minY); y < Mathf.CeilToInt(maxY); y++)
                {
                    float yc = y + 0.5f;
                    xs.Clear();
                    for (int i = 0; i < pts.Length; i++)
                    {
                        Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                        if ((a.y <= yc && b.y > yc) || (b.y <= yc && a.y > yc))
                            xs.Add(a.x + (yc - a.y) * (b.x - a.x) / (b.y - a.y));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                        for (int x = Mathf.RoundToInt(xs[k]); x < Mathf.RoundToInt(xs[k + 1]); x++) Set(x, y, c);
                }
            }
        }

        // Bütün tuvalleri tek dokuya dizer (satır satır), her biri için Sprite verir.
        private sealed class AtlasBuilder
        {
            private readonly List<(PixelCanvas canvas, Vector2 pivotPx, string name)> items = new List<(PixelCanvas, Vector2, string)>();
            private Sprite[] sprites;

            public int Add(PixelCanvas canvas, Vector2 pivotPx, string name)
            {
                items.Add((canvas, pivotPx, name));
                return items.Count - 1;
            }

            public Sprite this[int index] => sprites[index];

            public void Pack()
            {
                const int size = 512;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "FX_FloodSweep_Atlas"
                };
                tex.SetPixels32(new Color32[size * size]);

                var order = new List<int>();
                for (int i = 0; i < items.Count; i++) order.Add(i);
                order.Sort((a, b) => items[b].canvas.Height.CompareTo(items[a].canvas.Height));

                var rects = new RectInt[items.Count];
                int x = 1, y = 1, rowHeight = 0;
                foreach (int i in order)
                {
                    var c = items[i].canvas;
                    if (x + c.Width + 1 > size)
                    {
                        x = 1;
                        y += rowHeight + 2;
                        rowHeight = 0;
                    }
                    if (y + c.Height + 1 > size) throw new InvalidOperationException("[FloodSweep] Atlas doldu.");
                    rects[i] = new RectInt(x, y, c.Width, c.Height);
                    tex.SetPixels32(x, y, c.Width, c.Height, c.Pixels);
                    x += c.Width + 2;
                    rowHeight = Mathf.Max(rowHeight, c.Height);
                }
                tex.Apply(false, false);

                sprites = new Sprite[items.Count];
                for (int i = 0; i < items.Count; i++)
                {
                    var (canvas, pivotPx, name) = items[i];
                    var r = rects[i];
                    var pivot = new Vector2(pivotPx.x / canvas.Width, 1f - pivotPx.y / canvas.Height);
                    sprites[i] = Sprite.Create(tex, new Rect(r.x, r.y, r.width, r.height), pivot, Ppu, 0, SpriteMeshType.FullRect);
                    sprites[i].name = name;
                }
            }
        }

        private static GeneratedSprites GenerateSprites()
        {
            var atlas = new AtlasBuilder();
            var g = new GeneratedSprites();

            int pixel = atlas.Add(SolidCanvas(1, 1), new Vector2(0.5f, 0.5f), "FX_Pixel");
            int block = atlas.Add(SolidCanvas(4, 4), new Vector2(2f, 4f), "FX_MaskBlock");
            int streak = atlas.Add(StreakCanvas(), new Vector2(6f, 3f), "FX_Water_Streak");
            int glow = atlas.Add(GlowCanvas(), new Vector2(16f, 16f), "FX_Glow");
            int vignette = atlas.Add(VignetteCanvas(), new Vector2(40f, 22.5f), "FX_Vignette");
            int alarm = atlas.Add(AlarmCanvas(), new Vector2(3f, 13f), "FX_Alarm");
            var barrierCanvas = BarrierCanvas(2.6f, out var barrierPivot);
            int barrier = atlas.Add(barrierCanvas, barrierPivot, "FX_Barrier");
            var carCanvas = CarCanvas(out var carPivot, out g.CarLightA, out g.CarLightB);
            int car = atlas.Add(carCanvas, carPivot, "FX_StuckCar");
            int plank = atlas.Add(PlankCanvas(), new Vector2(12f, 4f), "FX_Plank");
            int leaf = atlas.Add(LeafCanvas(), new Vector2(1.5f, 1f), "FX_Leaf");
            int bubble = atlas.Add(BubbleCanvas(), new Vector2(1.5f, 1.5f), "FX_Bubble");
            int arm = atlas.Add(ArmCanvas(), new Vector2(3.5f, ArmCanvasHeight), "FX_Cenk_Arm");
            int[] crest = { atlas.Add(CrestCanvas(0), new Vector2(12f, 7f), "FX_Water_Crest_0"), atlas.Add(CrestCanvas(1), new Vector2(12f, 7f), "FX_Water_Crest_1") };
            int[] ring = { atlas.Add(FoamRingCanvas(0), new Vector2(15f, 5f), "FX_Water_FoamRing_0"), atlas.Add(FoamRingCanvas(1), new Vector2(15f, 5f), "FX_Water_FoamRing_1") };
            int[] edge = { atlas.Add(EdgeFoamCanvas(0), new Vector2(32f, 16f), "FX_Water_EdgeFoam_0"), atlas.Add(EdgeFoamCanvas(1), new Vector2(32f, 16f), "FX_Water_EdgeFoam_1") };
            int[] WaterPair(Color32 baseColor, Color32 light, Color32 glint, int seed, string name) => new[]
            {
                atlas.Add(WaterCanvas(baseColor, light, glint, seed, 0), new Vector2(32f, 16f), name + "_0"),
                atlas.Add(WaterCanvas(baseColor, light, glint, seed + 101, 2), new Vector2(32f, 16f), name + "_1")
            };
            // Çamurlu sel suyu (Mahmut'un PROP_Street_Puddle_Deep tonları), gökyüzünü yansıtan soğuk açık bantlarla.
            int[] shallow = WaterPair(new Color32(98, 92, 72, 200), new Color32(122, 120, 104, 215), new Color32(196, 196, 178, 235), 11, "FX_Water_Shallow");
            int[] mid = WaterPair(new Color32(82, 78, 62, 218), new Color32(106, 106, 96, 228), new Color32(184, 184, 168, 240), 23, "FX_Water_Mid");
            int[] deep = WaterPair(new Color32(68, 65, 52, 230), new Color32(92, 93, 86, 236), new Color32(170, 172, 160, 242), 37, "FX_Water_Deep");

            atlas.Pack();
            Sprite[] All(int[] ids) => Array.ConvertAll(ids, i => atlas[i]);
            g.Pixel = atlas[pixel];
            g.Block = atlas[block];
            g.Streak = atlas[streak];
            g.Glow = atlas[glow];
            g.Vignette = atlas[vignette];
            g.Alarm = atlas[alarm];
            g.Barrier = atlas[barrier];
            g.Car = atlas[car];
            g.Plank = atlas[plank];
            g.Leaf = atlas[leaf];
            g.Bubble = atlas[bubble];
            g.Arm = atlas[arm];
            g.Crest = All(crest);
            g.FoamRing = All(ring);
            g.EdgeFoam = All(edge);
            g.WaterShallow = All(shallow);
            g.WaterMid = All(mid);
            g.WaterDeep = All(deep);
            return g;
        }

        private static Vector2 IsoPx(Vector2 origin, float x, float y, float z) =>
            new Vector2(origin.x + (x - y) * 32f, origin.y + (x + y) * 16f - z);

        private static void IsoBox(PixelCanvas c, Vector2 o, float x, float y, float w, float d, float z0, float h, Color32 top, Color32 left, Color32 right)
        {
            float z1 = z0 + h;
            c.Poly(new[] { IsoPx(o, x, y + d, z1), IsoPx(o, x + w, y + d, z1), IsoPx(o, x + w, y + d, z0), IsoPx(o, x, y + d, z0) }, left);
            c.Poly(new[] { IsoPx(o, x + w, y, z1), IsoPx(o, x + w, y + d, z1), IsoPx(o, x + w, y + d, z0), IsoPx(o, x + w, y, z0) }, right);
            c.Poly(new[] { IsoPx(o, x, y, z1), IsoPx(o, x + w, y, z1), IsoPx(o, x + w, y + d, z1), IsoPx(o, x, y + d, z1) }, top);
        }

        private static PixelCanvas SolidCanvas(int w, int h)
        {
            var c = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) c.Set(x, y, new Color32(255, 255, 255, 255));
            return c;
        }

        // 64×32 izometrik su karosu: akıntı yönünde (ekranda sol-aşağı) açık bantlar, dither ve parıltılar.
        // shift: bantların kayması; iki kare arasında değişince bantlar akıyormuş gibi görünür. Karolar birbirine taşmaz.
        private static PixelCanvas WaterCanvas(Color32 baseColor, Color32 light, Color32 glint, int seed, int shift)
        {
            var c = new PixelCanvas(64, 32);
            var rnd = new System.Random(seed);
            var trough = new Color32((byte)(baseColor.r * 0.8f), (byte)(baseColor.g * 0.8f), (byte)(baseColor.b * 0.8f), baseColor.a);
            for (int y = 0; y < 32; y++)
            {
                int half = y < 16 ? 2 * (y + 1) : 2 * (32 - y);
                for (int x = 32 - half; x < 32 + half; x++)
                {
                    // x + 2y sabit çizgiler gy yönündedir: bant = akıntı çizgisi. Bantlar kesik kesiktir
                    // (8 piksellik desen karo sınırında kopmaz), arkasında koyu çukur dalgaya kabartı verir.
                    int v = x + 2 * y + shift;
                    int band = (v % 24 + 24) % 24;
                    int lane = Mathf.FloorToInt(v / 24f);
                    bool dash = ((y - shift + lane * 5) & 7) < 5;
                    bool inBand = dash && (band == 0 || (band == 1 && ((x + y) & 1) == 0));
                    bool inTrough = dash && (band == 2 || band == 3);
                    double r = rnd.NextDouble();
                    var col = r < 0.008 ? glint
                        : inBand || (r < 0.03 && ((x + y) & 1) == 0) ? light
                        : inTrough ? trough : baseColor;
                    c.Set(x, y, col);
                    // Parıltı yatay 2 piksel
                    if (col.Equals(glint) && x + 1 < 32 + half) c.Set(++x, y, glint);
                }
            }
            return c;
        }

        // Kaldırım kenarı boyunca kırık köpük çizgisi (karonun sol-üst kenarı = gx 2 sınırı).
        private static PixelCanvas EdgeFoamCanvas(int phase)
        {
            var c = new PixelCanvas(64, 32);
            for (int i = 0; i < 16; i++)
            {
                if ((i + phase * 2) % 5 == 4) continue;
                int x = 31 - 2 * i, y = i;
                c.Set(x, y, Foam);
                c.Set(x - 1, y, Foam);
                c.Set(x, y + 1, new Color32(Foam.r, Foam.g, Foam.b, 170));
                if ((i + phase) % 3 == 0) c.Set(x + 1, y + 1, new Color32(Foam.r, Foam.g, Foam.b, 150));
                // Köpüğün su tarafında koyu ıslak çizgi: kaldırım ile su arasındaki seviye farkı okunsun.
                c.Set(x + 1, y + 2, new Color32(30, 28, 22, 150));
                c.Set(x + 2, y + 2, new Color32(30, 28, 22, 110));
            }
            return c;
        }

        // Akıntı çizgisi: +gy yönünde (ekranda sol-aşağı) 2:1 eğimli 12 piksellik iz, başı parlak, kuyruğu söner.
        private static PixelCanvas StreakCanvas()
        {
            var c = new PixelCanvas(12, 6);
            for (int i = 0; i < 12; i++) c.Set(11 - i, i / 2, new Color32(255, 255, 255, (byte)(60 + i * 16)));
            return c;
        }

        // Köpüklü dalga sırtı: üstte kırık köpük, ortada açık sırt, altında koyu gölge (24×12).
        private static PixelCanvas CrestCanvas(int phase)
        {
            var c = new PixelCanvas(24, 12);
            var shadow = new Color32(36, 34, 27, 190);
            var lip = new Color32(Foam.r, Foam.g, Foam.b, 200);
            for (int i = 0; i < 24; i++)
            {
                int x = 23 - i, y = 1 + i / 2;
                bool gap = (i + phase * 3) % 9 == 8;
                if (!gap) c.Set(x, y, i > 5 && i < 20 ? Foam : StreakColor);
                if (i > 4 && i < 19 && (i + phase) % 3 != 0) c.Set(x, y - 1, lip);
                if (i > 8 && i < 15 && (i + phase) % 2 == 0) c.Set(x - 1, y - 1, Foam);
                c.Set(x, y + 1, shadow);
                if (i > 3 && i < 21) c.Set(x - 1, y + 1, new Color32(36, 34, 27, 110));
            }
            return c;
        }

        private static PixelCanvas FoamRingCanvas(int phase)
        {
            var c = new PixelCanvas(30, 10);
            for (int i = 0; i < 36; i++)
            {
                if ((i + phase) % 4 == 0) continue;
                float a = i * Mathf.PI * 2f / 36f;
                c.Set(Mathf.RoundToInt(14.5f + 13.5f * Mathf.Cos(a)), Mathf.RoundToInt(4.5f + 4.2f * Mathf.Sin(a)), Foam);
            }
            for (int x = 9; x < 21; x++) if ((x + phase) % 3 != 0) c.Set(x, 5, Foam);
            return c;
        }

        // Başın üstünde yanıp sönen ünlem.
        private static PixelCanvas AlarmCanvas()
        {
            var c = new PixelCanvas(6, 13);
            for (int y = 0; y < 13; y++)
                for (int x = 0; x < 6; x++)
                    if (y != 9) c.Set(x, y, Outline);
            var white = new Color32(255, 255, 255, 255);
            for (int y = 1; y < 8; y++) { c.Set(2, y, white); c.Set(3, y, white); }
            for (int y = 10; y < 12; y++) { c.Set(2, y, white); c.Set(3, y, white); }
            return c;
        }

        // Kırmızı-beyaz bariyer: gy yönünde length karo boyunca (ekranda sağ-yukarıdan sol-aşağıya).
        private static PixelCanvas BarrierCanvas(float length, out Vector2 pivot)
        {
            int w = Mathf.CeilToInt(length * 32f) + 6, h = Mathf.CeilToInt(length * 16f) + 22;
            var c = new PixelCanvas(w, h);
            var red = new Color32(214, 64, 56, 255);
            var white = new Color32(232, 228, 216, 255);
            float x0 = w - 3, y0 = 19;
            int steps = Mathf.RoundToInt(length * 32f);
            for (int i = 0; i <= steps; i++)
            {
                int x = Mathf.RoundToInt(x0 - i);
                int y = Mathf.RoundToInt(y0 + i * 0.5f);
                c.Set(x, y - 16, Outline);
                for (int r = 0; r < 4; r++) c.Set(x, y - 15 + r, (i / 6) % 2 == 0 ? red : white);
                c.Set(x, y - 11, Outline);
            }
            foreach (float s in new[] { 0f, 0.5f, 1f })
            {
                int i = Mathf.RoundToInt(steps * s);
                int x = Mathf.RoundToInt(x0 - i), y = Mathf.RoundToInt(y0 + i * 0.5f);
                for (int yy = y - 16; yy <= y; yy++) { c.Set(x, yy, Outline); c.Set(x + 1, yy, Outline); }
                c.Set(x - 1, y, Outline);
                c.Set(x + 2, y, Outline);
            }
            pivot = new Vector2(x0, y0);
            return c;
        }

        // Suda kalmış küçük araç: gövde su yüzeyinden (z = 4) yukarısı çizilir. Uzun kenarı gy (yol) boyunca.
        private static PixelCanvas CarCanvas(out Vector2 pivot, out Vector2 lightA, out Vector2 lightB)
        {
            var c = new PixelCanvas(84, 70);
            var o = new Vector2(52f, 26f);
            const float w = 0.85f, d = 1.55f;
            var dark = new Color32(130, 44, 45, 255);
            var darker = new Color32(98, 34, 36, 255);
            IsoBox(c, o, 0f, 0f, w, d, 4f, 9f, new Color32(176, 64, 62, 255), dark, darker);
            IsoBox(c, o, 0.08f, 0.38f, w - 0.16f, 0.78f, 13f, 9f, new Color32(168, 58, 58, 255), new Color32(104, 136, 158, 255), new Color32(82, 108, 128, 255));
            // Cam direkleri ve tavan çizgisi
            for (int z = 13; z < 22; z++)
            {
                var a = IsoPx(o, 0.08f + (w - 0.16f) * 0.5f, 0.38f + 0.78f, z);
                c.Set(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), dark);
                var b = IsoPx(o, w - 0.08f, 0.38f + 0.39f, z);
                c.Set(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y), darker);
            }
            // Camlarda çapraz parıltı
            var glass = new Color32(170, 198, 214, 255);
            for (int k = 0; k < 6; k++)
            {
                var p = IsoPx(o, 0.18f + k * 0.035f, 0.38f + 0.78f, 14f + k);
                c.Set(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), glass);
                var q = IsoPx(o, w - 0.08f, 0.5f + k * 0.035f, 14f + k);
                c.Set(Mathf.RoundToInt(q.x), Mathf.RoundToInt(q.y), glass);
            }
            // Gövdede parlak şerit
            for (float s = 0.05f; s <= d - 0.05f; s += 1f / 32f)
            {
                var p = IsoPx(o, w, s, 11f);
                c.Set(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), new Color32(196, 92, 86, 255));
            }
            // Su çizgisi (köpük)
            for (float s = 0f; s <= d; s += 1f / 32f)
            {
                var p = IsoPx(o, w, s, 4f);
                c.Set(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y) - 1, Foam);
            }
            for (float s = 0f; s <= w; s += 1f / 32f)
            {
                var p = IsoPx(o, s, d, 4f);
                c.Set(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y) - 1, Foam);
            }
            c.AddOutline(Outline);
            Vector2 piv = IsoPx(o, 0f, 0f, 0f);
            Vector2 ToLocal(Vector2 px) => new Vector2((px.x - piv.x) / Ppu, -(px.y - piv.y) / Ppu);
            lightA = ToLocal(IsoPx(o, 0.1f, d, 10f));
            lightB = ToLocal(IsoPx(o, w - 0.1f, d, 10f));
            pivot = piv;
            return c;
        }

        private static PixelCanvas PlankCanvas()
        {
            var c = new PixelCanvas(20, 12);
            IsoBox(c, new Vector2(12f, 4f), 0f, 0f, 0.1f, 0.4f, 0f, 2f, new Color32(140, 106, 66, 255), new Color32(104, 78, 46, 255), new Color32(86, 64, 38, 255));
            c.AddOutline(Outline);
            return c;
        }

        private static PixelCanvas LeafCanvas()
        {
            var c = new PixelCanvas(3, 2);
            var green = new Color32(98, 112, 52, 255);
            c.Set(0, 1, green);
            c.Set(1, 0, green);
            c.Set(1, 1, new Color32(120, 136, 64, 255));
            c.Set(2, 0, green);
            return c;
        }

        // Cenk'in kolu (yukarı bakar, pivot omuzda): beyaz tişört kolu, ten rengi önkol, lacivert bileklik, el.
        private const int ArmCanvasHeight = 18;

        private static PixelCanvas ArmCanvas()
        {
            var c = new PixelCanvas(7, ArmCanvasHeight);
            var skin = new Color32(240, 188, 150, 255);
            var skinShade = new Color32(206, 146, 112, 255);
            var band = new Color32(58, 66, 96, 255);
            var sleeve = new Color32(232, 228, 220, 255);
            var sleeveShade = new Color32(190, 186, 182, 255);
            // El (üstte)
            for (int x = 2; x <= 4; x++) c.Set(x, 0, Outline);
            for (int y = 1; y <= 3; y++)
            {
                c.Set(1, y, Outline);
                c.Set(5, y, Outline);
                for (int x = 2; x <= 4; x++) c.Set(x, y, x == 4 ? skinShade : skin);
            }
            // Bileklik
            for (int y = 4; y <= 5; y++)
            {
                c.Set(1, y, Outline);
                c.Set(5, y, Outline);
                for (int x = 2; x <= 4; x++) c.Set(x, y, band);
            }
            // Önkol
            for (int y = 6; y <= 12; y++)
            {
                c.Set(1, y, Outline);
                c.Set(5, y, Outline);
                for (int x = 2; x <= 4; x++) c.Set(x, y, x == 4 ? skinShade : skin);
            }
            // Tişört kolu (omuz)
            for (int y = 13; y < ArmCanvasHeight; y++)
            {
                c.Set(0, y, Outline);
                c.Set(6, y, Outline);
                for (int x = 1; x <= 5; x++) c.Set(x, y, x >= 4 ? sleeveShade : sleeve);
            }
            return c;
        }

        private static PixelCanvas BubbleCanvas()
        {
            var c = new PixelCanvas(3, 3);
            c.Set(1, 0, Foam);
            c.Set(0, 1, Foam);
            c.Set(2, 1, Foam);
            c.Set(1, 2, Foam);
            return c;
        }

        // Basamaklı (piksel) yumuşak ışık lekesi.
        private static PixelCanvas GlowCanvas()
        {
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
                    if (r >= 1f) continue;
                    byte a = r < 0.35f ? (byte)255 : r < 0.65f ? (byte)150 : (byte)70;
                    c.Set(x, y, new Color32(255, 255, 255, a));
                }
            return c;
        }

        // Kenarları basamaklı karartan vinyet (80×45 piksel, kameranın görüşüne gerilir).
        private static PixelCanvas VignetteCanvas()
        {
            var c = new PixelCanvas(80, 45);
            for (int y = 0; y < 45; y++)
                for (int x = 0; x < 80; x++)
                {
                    float dx = (x + 0.5f - 40f) / 40f, dy = (y + 0.5f - 22.5f) / 22.5f;
                    float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 0.9f);
                    byte a = r < 0.8f ? (byte)0 : r < 0.95f ? (byte)40 : r < 1.1f ? (byte)80 : (byte)120;
                    if (a > 0) c.Set(x, y, new Color32(8, 9, 16, a));
                }
            return c;
        }
    }
}
