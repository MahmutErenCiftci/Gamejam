using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Çevre tehlikelerinin (yüzen enkaz çarpması, elektrik çarpması) Cenk üzerindeki tek ortak tepkisi.
    /// - Geri itme: IsoPlayerController.ExternalVelocity kısa süre verilir; Rigidbody2D çarpışmaları geçerli kalır (duvardan geçmez).
    /// - Kısa hareket kilidi: CanMove kapatılır, bitince bölüm akışının o anki serbest durumu (FloodEveningHub.FreeRoam) geri verilir.
    /// - Geri bildirim (P-FLOOD 18 Eki): kısa kamera sarsıntısı (~0,25 sn, 2-3 piksel) +
    ///   elektrik: Cenk'in siluetinde mavi-beyaz titreşen parlama + çevresinde kısa piksel arklar (ses: FloodElectricHazards SFX_FE_Shock);
    ///   enkaz: siluette kısa kırmızı parlama + acı sesi "SFX_Cenk_Hurt" (yoksa SFX_EQ_Injured; ses: FloodDebrisField SFX_FE_Debris_Bump).
    ///   Işığa duyarlılık: tam ekran parlama YOK; yalnız karakterin pikselleri, en çok ~8 Hz, en çok 0,36 sn.
    /// Aynı anda tek tepki; tepki sürerken yenisi başlamaz (zincirleme olmasın). Sel sayacını durdurmaz, tek başına başarısızlık değildir.
    /// Sel sayacı durunca (eve girildi / süre doldu) ya da Hub.RequestReset'te tepki hemen kesilir ve her şey eski haline döner.
    /// </summary>
    [DefaultExecutionOrder(1200)] // LateUpdate'te siluet, CenkOutfit'in (1000) kıyafet eşlemesinden SONRA kopyalansın
    public class FloodPlayerReaction : MonoBehaviour, IFloodEveningResettable
    {
        public enum Kind { Bump, Shock }

        [SerializeField] private IsoPlayerController player;
        [Tooltip("Siluet parlaması (Assets/Art/FloodEvening/ENV/Hazards/FE_SpriteFlash.mat). Boşsa yalnız renk tonu kullanılır.")]
        [SerializeField] private Material flashMaterial;
        [Tooltip("Elektrik: siluetin titreşen iki tonu (mavi-beyaz)")]
        [SerializeField] private Color shockFlashA = new Color(0.62f, 0.88f, 1f, 0.78f);
        [SerializeField] private Color shockFlashB = new Color(1f, 1f, 1f, 0.7f);
        [Tooltip("Enkaz: kısa kırmızı parlama")]
        [SerializeField] private Color hurtFlash = new Color(1f, 0.18f, 0.12f, 0.6f);
        [Tooltip("Malzeme yoksa yedek: Cenk'in kısa süre aldığı ton")]
        [SerializeField] private Color shockTint = new Color(0.72f, 0.92f, 1f, 1f);
        [SerializeField] private Color bumpTint = new Color(1f, 0.6f, 0.55f, 1f);
        [Header("Sarsıntı")]
        [SerializeField] private float shakeAmount = 0.045f;
        [SerializeField] private float shakeSeconds = 0.25f;
        [Header("Ses")]
        [Tooltip("Enkaz çarpınca Cenk'in acı sesi (Resources/Audio; yoksa yedek)")]
        [SerializeField] private string hurtSfx = "SFX_Cenk_Hurt";
        [SerializeField] private string hurtFallbackSfx = "SFX_EQ_Injured";
        [Tooltip("Yetişkin sesi; Cenk için biraz tiz")]
        [SerializeField] private float hurtPitch = 1.15f;
        [SerializeField, Range(0f, 1f)] private float hurtVolume = 0.7f;

        private SpriteRenderer playerRenderer;
        private ChapterCamera cam;
        private Coroutine routine;
        private Color savedColor = Color.white;
        private bool colorSaved;
        private Vector2 lastExternal;
        private bool locked;
        private SpriteRenderer flash;
        private readonly List<SpriteRenderer> arcs = new List<SpriteRenderer>();
        private static Sprite[] arcSprites;
        private AudioSource voice;

        /// <summary>Şu an bir tepki oynuyor mu.</summary>
        public bool Busy => routine != null;
        /// <summary>Yeni tepki başlatılabilir mi: serbest yürüyüş, sayaç akıyor, başka tepki yok.</summary>
        public bool CanReact => routine == null && player != null && FloodEveningHub.FreeRoam && FloodEveningHub.FloodStarted && !FloodEveningHub.FloodStopped;
        public IsoPlayerController Player => player;
        /// <summary>Test: son tepkinin türü ve sayısı.</summary>
        public Kind LastKind { get; private set; }
        public int ReactionCount { get; private set; }
        /// <summary>Test: siluet parlaması şu an görünüyor mu.</summary>
        public bool FlashVisible => flash != null && flash.enabled;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (player != null) playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);
            if (flashMaterial == null)
            {
                var sh = Shader.Find("AfetUyanisi/FloodEvening/SpriteFlash");
                if (sh != null) flashMaterial = new Material(sh) { name = "FE_SpriteFlash (runtime)" };
            }
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable() { FloodEveningHub.ResetRequested -= ResetModule; Cancel(); }

        private void Update()
        {
            if (routine != null && FloodEveningHub.FloodStopped) Cancel();
        }

        private void LateUpdate()
        {
            // siluet kopyası Cenk'in o anki karesini izler
            if (flash == null || !flash.enabled || playerRenderer == null) return;
            flash.sprite = playerRenderer.sprite;
            flash.flipX = playerRenderer.flipX;
            flash.flipY = playerRenderer.flipY;
            flash.sortingLayerID = playerRenderer.sortingLayerID;
            flash.sortingOrder = playerRenderer.sortingOrder + 1;
        }

        /// <summary>
        /// Tepkiyi başlatır. dirGround: zemin ölçüsünde yön (dünya vektörü; y ekseni 2:1 izometriye göre yarım); target verilirse
        /// itme o noktaya doğru yapılır ve varınca durur. groundSpeed: zemin birimi / sn. Başlatılamazsa false.
        /// </summary>
        public bool React(Kind kind, Vector2 dirGround, float groundSpeed, float pushSeconds, float lockSeconds, Vector2? target = null)
        {
            if (!CanReact) return false;
            LastKind = kind;
            ReactionCount++;
            if (kind == Kind.Bump) PlayHurt();
            routine = StartCoroutine(Run(kind, dirGround, groundSpeed, pushSeconds, lockSeconds, target));
            return true;
        }

        /// <summary>Zemin ölçüsünde (dx, 2·dy) birim yön → dünya hızı.</summary>
        public static Vector2 GroundToWorld(Vector2 dir, float groundSpeed)
        {
            float g = Mathf.Sqrt(dir.x * dir.x + 4f * dir.y * dir.y);
            return g < 1e-5f ? Vector2.zero : dir / g * groundSpeed;
        }

        public static float GroundDistance(Vector2 a, Vector2 b)
        {
            Vector2 d = a - b;
            return Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y);
        }

        private IEnumerator Run(Kind kind, Vector2 dirGround, float groundSpeed, float pushSeconds, float lockSeconds, Vector2? target)
        {
            if (cam == null && Camera.main != null) cam = Camera.main.GetComponent<ChapterCamera>();
            locked = true;
            player.CanMove = false;
            bool useFlash = EnsureFlash();
            if (!useFlash && playerRenderer != null && !colorSaved) { savedColor = playerRenderer.color; colorSaved = true; }
            Color tint = kind == Kind.Shock ? shockTint : bumpTint;
            float fxSeconds = kind == Kind.Shock ? 0.4f : 0.22f;
            float total = Mathf.Max(pushSeconds, lockSeconds, fxSeconds, shakeSeconds);
            float arcT = 0f;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                // itme
                Vector2 v = Vector2.zero;
                if (t < pushSeconds)
                {
                    Vector2 dir = dirGround;
                    if (target.HasValue)
                    {
                        dir = target.Value - (Vector2)player.transform.position;
                        if (GroundDistance(target.Value, player.transform.position) < 0.08f) pushSeconds = t;
                    }
                    if (t < pushSeconds) v = GroundToWorld(dir, groundSpeed);
                }
                if (t < Mathf.Max(pushSeconds, lockSeconds)) SetExternal(v);
                else if (locked) ReleaseControl();

                // parlama: elektrikte ~0,06 sn'de bir mavi-beyaz ↔ beyaz titreşim (0,36 sn), enkazda tek kısa kırmızı sönüş
                if (useFlash)
                {
                    if (kind == Kind.Shock)
                    {
                        bool on = t < 0.36f;
                        flash.enabled = on;
                        if (on) flash.color = Mathf.FloorToInt(t / 0.06f) % 2 == 0 ? shockFlashA : FeAlpha(shockFlashB, 0.35f);
                        arcT -= Time.deltaTime;
                        if (arcT <= 0f) { arcT = 0.05f; PlaceArcs(t < fxSeconds); }
                    }
                    else
                    {
                        float k = Mathf.Clamp01(1f - t / fxSeconds);
                        flash.enabled = k > 0f;
                        flash.color = FeAlpha(hurtFlash, hurtFlash.a * k);
                    }
                }
                else if (playerRenderer != null)
                {
                    float k = kind == Kind.Shock ? ((Mathf.FloorToInt(t / 0.06f) % 2 == 0 && t < 0.36f) ? 1f : 0f) : Mathf.Clamp01(1f - t / 0.25f);
                    playerRenderer.color = Color.Lerp(savedColor, savedColor * tint, k);
                }
                // kısa sarsıntı (tam piksele oturur: ChapterCamera.Snap)
                if (cam != null)
                {
                    float a = shakeAmount * (kind == Kind.Shock ? 1f : 0.8f) * Mathf.Clamp01(1f - t / shakeSeconds);
                    cam.ShakeOffset = a > 0f ? new Vector2(Random.Range(-a, a), Random.Range(-a, a) * 0.5f) : Vector2.zero;
                }
                yield return null;
            }
            Restore();
            routine = null;
        }

        private static Color FeAlpha(Color c, float a) { c.a = a; return c; }

        private void SetExternal(Vector2 v)
        {
            if (player == null) return;
            // başka bir sistemin (apartman finali) verdiği hıza dokunma: yalnız bizim verdiğimizi değiştir
            if (player.ExternalVelocity != lastExternal && lastExternal != Vector2.zero) { lastExternal = Vector2.zero; return; }
            player.ExternalVelocity = v;
            lastExternal = v;
        }

        // itme/kilit bitti ama efekt sürüyor: kontrol erken geri verilir (efekt hareketi kilitlemez)
        private void ReleaseControl()
        {
            if (player == null) return;
            if (lastExternal != Vector2.zero && player.ExternalVelocity == lastExternal) player.ExternalVelocity = Vector2.zero;
            lastExternal = Vector2.zero;
            if (locked) player.CanMove = FloodEveningHub.FreeRoam;
            locked = false;
        }

        private void Restore()
        {
            ReleaseControl();
            locked = false;
            if (playerRenderer != null && colorSaved) playerRenderer.color = savedColor;
            colorSaved = false;
            if (flash != null) flash.enabled = false;
            foreach (var a in arcs) if (a != null) a.enabled = false;
            if (cam != null) cam.ShakeOffset = Vector2.zero;
        }

        /// <summary>Süren tepkiyi keser ve her şeyi geri verir.</summary>
        public void Cancel()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (locked || colorSaved || lastExternal != Vector2.zero || (flash != null && flash.enabled)) Restore();
        }

        public void ResetModule() => Cancel();

        // ---------------------------------------------------------------- görsel / ses

        private bool EnsureFlash()
        {
            if (flashMaterial == null || playerRenderer == null) return false;
            if (flash != null) return true;
            flash = NewSprite("HitFlash");
            flash.spriteSortPoint = playerRenderer.spriteSortPoint;
            for (int i = 0; i < 3; i++) arcs.Add(NewSprite("ShockArc" + i));
            return true;
        }

        private SpriteRenderer NewSprite(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(player.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = flashMaterial;
            sr.sortingLayerID = playerRenderer.sortingLayerID;
            sr.sortingOrder = playerRenderer.sortingOrder + 2;
            sr.enabled = false;
            return sr;
        }

        // Cenk'in gövdesi çevresinde kısa, kırık piksel arklar (her 0,05 sn'de yer/şekil değişir)
        private void PlaceArcs(bool on)
        {
            if (arcSprites == null) arcSprites = BuildArcSprites();
            Bounds b = playerRenderer.bounds;
            for (int i = 0; i < arcs.Count; i++)
            {
                var a = arcs[i];
                a.enabled = on && Random.value < 0.8f;
                if (!a.enabled) continue;
                a.sprite = arcSprites[Random.Range(0, arcSprites.Length)];
                a.flipX = Random.value < 0.5f;
                a.flipY = Random.value < 0.5f;
                a.color = i % 2 == 0 ? new Color(0.8f, 0.95f, 1f, 0.95f) : new Color(0.45f, 0.8f, 1f, 0.9f);
                float side = (i % 2 == 0 ? -1f : 1f) * Random.Range(0.55f, 0.95f);
                Vector2 p = new Vector2(b.center.x + side * b.extents.x, Random.Range(b.min.y + b.extents.y * 0.3f, b.max.y - b.extents.y * 0.15f));
                a.transform.position = new Vector3(Mathf.Round(p.x * 64f) / 64f, Mathf.Round(p.y * 64f) / 64f, 0f);
                a.sortingLayerID = playerRenderer.sortingLayerID;
                a.sortingOrder = playerRenderer.sortingOrder + 2;
            }
        }

        // 4 küçük kırık çizgi (zikzak) sprite'ı, 10x12 piksel, beyaz (renk SpriteRenderer.color ile)
        private static Sprite[] BuildArcSprites()
        {
            string[][] shapes =
            {
                new[] { "....w.....", "....w.....", "...w......", "..ww......", "...ww.....", ".....w....", "....w.....", "...w......", "...ww.....", ".....w....", "......w...", "......w..." },
                new[] { ".w........", "..w.......", "..ww......", "....w.....", "...w......", "...ww.....", ".....ww...", "......w...", ".....w....", "......w...", "........w.", ".........w" },
                new[] { "..........", "w.........", ".ww.......", "...w......", "..w.......", "..www.....", ".....ww...", "....w.....", "....w.ww..", ".......w..", "........w.", ".........." },
                new[] { ".....w....", "....w.....", "....w.....", ".....w....", "......ww..", ".....w....", "....w.....", "...w......", "..w.......", "...w......", "..........", ".........." },
            };
            var list = new Sprite[shapes.Length];
            for (int s = 0; s < shapes.Length; s++)
            {
                var rows = shapes[s];
                int h = rows.Length, w = rows[0].Length;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FE_ShockArc" };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        tex.SetPixel(x, h - 1 - y, rows[y][x] == 'w' ? Color.white : Color.clear);
                tex.Apply(false, true);
                list[s] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
            }
            return list;
        }

        // Acı sesi: GameAudio'da perde ayarı olmadığı için kendi 2D kaynağıyla (efekt seviyesi GameAudio ile aynı)
        private void PlayHurt()
        {
            var clip = GameAudio.LoadClip(hurtSfx);
            bool fallback = clip == null;
            if (fallback) clip = GameAudio.LoadClip(hurtFallbackSfx);
            if (clip == null) return;
            if (voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
            }
            voice.pitch = fallback ? 1f : hurtPitch;
            voice.PlayOneShot(clip, Mathf.Clamp01(hurtVolume) * GameAudio.SfxLevel);
        }
    }
}
