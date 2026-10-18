using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Çevre tehlikelerinin (yüzen enkaz çarpması, elektrik çarpması) Cenk üzerindeki tek ortak tepkisi.
    /// - Geri itme: IsoPlayerController.ExternalVelocity kısa süre verilir; Rigidbody2D çarpışmaları geçerli kalır (duvardan geçmez).
    /// - Kısa hareket kilidi: CanMove kapatılır, bitince bölüm akışının o anki serbest durumu (FloodEveningHub.FreeRoam) geri verilir.
    /// - Cenk'te kısa renk vurgusu (tam ekran beyaz YOK), hafif kamera sarsıntısı.
    /// Aynı anda tek tepki; tepki sürerken yenisi başlamaz (zincirleme olmasın). Sel sayacını durdurmaz, tek başına başarısızlık değildir.
    /// Sel sayacı durunca (avlu / süre doldu) ya da Hub.RequestReset'te tepki hemen kesilir ve her şey eski haline döner.
    /// </summary>
    public class FloodPlayerReaction : MonoBehaviour, IFloodEveningResettable
    {
        public enum Kind { Bump, Shock }

        [SerializeField] private IsoPlayerController player;
        [Tooltip("Elektrik çarpması: Cenk'in kısa süre aldığı soluk mavi-beyaz ton (ekran değil, yalnız karakter)")]
        [SerializeField] private Color shockTint = new Color(0.72f, 0.92f, 1f, 1f);
        [SerializeField] private Color bumpTint = new Color(1f, 0.88f, 0.78f, 1f);
        [SerializeField] private float shakeAmount = 0.025f;

        private SpriteRenderer playerRenderer;
        private ChapterCamera cam;
        private Coroutine routine;
        private Color savedColor = Color.white;
        private bool colorSaved;
        private Vector2 lastExternal;
        private bool locked;

        /// <summary>Şu an bir tepki oynuyor mu.</summary>
        public bool Busy => routine != null;
        /// <summary>Yeni tepki başlatılabilir mi: serbest yürüyüş, sayaç akıyor, başka tepki yok.</summary>
        public bool CanReact => routine == null && player != null && FloodEveningHub.FreeRoam && FloodEveningHub.FloodStarted && !FloodEveningHub.FloodStopped;
        public IsoPlayerController Player => player;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (player != null) playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable() { FloodEveningHub.ResetRequested -= ResetModule; Cancel(); }

        private void Update()
        {
            if (routine != null && FloodEveningHub.FloodStopped) Cancel();
        }

        /// <summary>
        /// Tepkiyi başlatır. dirGround: zemin ölçüsünde yön (dünya vektörü; y ekseni 2:1 izometriye göre yarım); target verilirse
        /// itme o noktaya doğru yapılır ve varınca durur. groundSpeed: zemin birimi / sn. Başlatılamazsa false.
        /// </summary>
        public bool React(Kind kind, Vector2 dirGround, float groundSpeed, float pushSeconds, float lockSeconds, Vector2? target = null)
        {
            if (!CanReact) return false;
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
            if (playerRenderer != null && !colorSaved) { savedColor = playerRenderer.color; colorSaved = true; }
            Color tint = kind == Kind.Shock ? shockTint : bumpTint;
            float total = Mathf.Max(pushSeconds, lockSeconds);
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
                SetExternal(v);
                // vurgu: çarpmada 3 kısa yanıp sönme (~0,05 sn), çarpmada tek yumuşak geçiş; ekran değil yalnız Cenk
                if (playerRenderer != null)
                {
                    float k = kind == Kind.Shock ? ((Mathf.FloorToInt(t / 0.06f) % 2 == 0 && t < 0.36f) ? 1f : 0f) : Mathf.Clamp01(1f - t / 0.25f);
                    playerRenderer.color = Color.Lerp(savedColor, savedColor * tint, k);
                }
                // hafif sarsıntı (1-2 piksel)
                if (cam != null)
                {
                    float a = shakeAmount * (kind == Kind.Shock ? 1f : 0.7f) * Mathf.Clamp01(1f - t / 0.25f);
                    cam.ShakeOffset = new Vector2(Random.Range(-a, a), Random.Range(-a, a) * 0.5f);
                }
                yield return null;
            }
            Restore();
            routine = null;
        }

        private void SetExternal(Vector2 v)
        {
            if (player == null) return;
            // başka bir sistemin (apartman finali) verdiği hıza dokunma: yalnız bizim verdiğimizi değiştir
            if (player.ExternalVelocity != lastExternal && lastExternal != Vector2.zero) { lastExternal = Vector2.zero; return; }
            player.ExternalVelocity = v;
            lastExternal = v;
        }

        private void Restore()
        {
            if (player != null)
            {
                if (lastExternal != Vector2.zero && player.ExternalVelocity == lastExternal) player.ExternalVelocity = Vector2.zero;
                lastExternal = Vector2.zero;
                if (locked) player.CanMove = FloodEveningHub.FreeRoam; // bölüm akışının o anki kararı (diyalog/panel açıksa kapalı kalır)
            }
            locked = false;
            if (playerRenderer != null && colorSaved) playerRenderer.color = savedColor;
            colorSaved = false;
            if (cam != null) cam.ShakeOffset = Vector2.zero;
        }

        /// <summary>Süren tepkiyi keser ve her şeyi geri verir.</summary>
        public void Cancel()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (locked || colorSaved || lastExternal != Vector2.zero) Restore();
        }

        public void ResetModule() => Cancel();
    }
}
