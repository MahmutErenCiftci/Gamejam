using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Uzun yolda köşeden sonraki ağaç düşüşü (SEL3-10, ZIGZAG §9). TreeStopSpot alanına (trigger) eklenen prefabın kökü
    /// TreeFallSpot'tur. Oyuncu alana girince TEK SEFER: Cenk güvenli noktada durur, hareket kısa süre kilitlenir
    /// (Hub.NotifyOpened/Closed("treefall")), kırılma sesi, ağaç kısa dönme + kayma animasyonuyla ilerideki yola düşer
    /// (fizik yok), yaprak/toz/kıymık çıkar, yolu kapatan collider'lar görselin yere değdiği karede açılır, kontrol geri
    /// gelir. Sonra Hub.Complete("tree_fell") ve bağlı dallarda Arm() çağrılır (Arda'nın BranchObstacle'ı).
    /// Büyük gövde dekordur; geçidi kapatan ve itilen şey küçük dal. Düşüşten önce düşme alanına girişi katı bir
    /// FallZoneBlock collider'ı kapatır, ağaç Cenk'in üstüne düşmez. Collider mesajı yerine her FixedUpdate'te geometrik
    /// örtüşme kontrol edilir (TreeStopSpot başka bir objede de olabilir). Hub.FreeRoam false iken (diyalog, panel) çalışmaz.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class FloodTreeFallEvent : MonoBehaviour, IFloodEveningResettable
    {
        public const string MinigameId = "treefall";
        public const string StepId = "tree_fell";

        [Header("Tetik")]
        [Tooltip("TreeStopSpot alanı (trigger). Boşsa çocuklarda 'TreeStopSpot' aranır.")]
        [SerializeField] private Collider2D stopZone;
        [Tooltip("Boşsa sahnedeki IsoPlayerController.")]
        [SerializeField] private IsoPlayerController player;
        [Tooltip("İşaretliyse sadece Hub.FreeRoam true iken tetiklenir (bölüm). Test için kapatılabilir.")]
        [SerializeField] private bool requireFreeRoam = true;

        [Header("Ağaç")]
        [Tooltip("Dönen parça; pivotu kırılma noktası (gövdenin dibi).")]
        [SerializeField] private Transform tree;
        [Tooltip("Düşmüş hâldeki Z açısı (negatif = sağa düşer).")]
        [SerializeField] private float fallenAngle = -84f;
        [Tooltip("Düşerken pivotun kayması (yerel, dünya birimi).")]
        [SerializeField] private Vector2 fallSlide = new Vector2(0.18f, -0.1f);
        [SerializeField, Min(0.1f)] private float fallSeconds = 0.75f;
        [Tooltip("Kırılma sesinden sonra ağacın sallandığı süre.")]
        [SerializeField, Min(0f)] private float creakSeconds = 0.55f;
        [Tooltip("Yere değince geri sekme açısı (derece).")]
        [SerializeField] private float bounceAngle = 5f;
        [SerializeField, Min(0f)] private float settleSeconds = 0.35f;
        [Tooltip("Düştükten sonra kontrol geri verilmeden önce bekleme.")]
        [SerializeField, Min(0f)] private float afterSeconds = 0.6f;

        [Header("Çarpışma")]
        [Tooltip("Düşüşten ÖNCE düşme alanına girişi kapatan katı collider. Yere değince kapanır.")]
        [SerializeField] private Collider2D fallZoneBlock;
        [Tooltip("Ağaç yere değdiği karede açılır (gövdenin yolu kapatan kısmı).")]
        [SerializeField] private Collider2D[] closeOnImpact = Array.Empty<Collider2D>();
        [Tooltip("Düşüş bitince Arm() çağrılacak objeler (Arda'nın FE_BranchObstacle'ı).")]
        [SerializeField] private GameObject[] armOnFallen = Array.Empty<GameObject>();
        [Tooltip("Yere değince görünür (yerdeki yaprak/dal döküntüsü).")]
        [SerializeField] private GameObject[] showOnImpact = Array.Empty<GameObject>();

        [Header("Cenk")]
        [Tooltip("Cenk'in döneceği nokta (genelde ağacın yere düşeceği yer).")]
        [SerializeField] private Transform lookAt;
        [Tooltip("Cenk'in başında beliren '!' (başta kapalı).")]
        [SerializeField] private SpriteRenderer alertIcon;
        [SerializeField] private Vector2 alertOffset = new Vector2(0f, 1.25f);

        [Header("Efekt")]
        [SerializeField] private Sprite leafSprite;
        [SerializeField] private Sprite dustSprite;
        [SerializeField] private Sprite chipSprite;
        [Tooltip("Yere değme hattı: düşen gövdenin uzandığı çizgi (yerel). Toz ve yapraklar bu hat boyunca çıkar.")]
        [SerializeField] private Vector2 impactFrom = new Vector2(0.4f, -0.1f);
        [SerializeField] private Vector2 impactTo = new Vector2(2.6f, -0.3f);
        [SerializeField, Range(0, 60)] private int leafCount = 26;
        [SerializeField, Range(0, 30)] private int dustCount = 12;
        [SerializeField, Range(0, 20)] private int chipCount = 7;
        [Tooltip("Efekt sprite'larının sıralaması (ağacın önünde).")]
        [SerializeField] private int fxSortingOrder = 5;

        [Header("Ses")]
        [Tooltip("Kırılma sesi (geçici; GameAudio'da ağaç sesi yok).")]
        [SerializeField] private string crackSfx = "SFX_Glass_Crack";
        [SerializeField] private string impactSfx = "SFX_Books_Fall";

        [Header("Metin (isteğe bağlı)")]
        [Tooltip("Düştükten sonra Hub.Say ile istenen satır (flood_evening.json). Boşsa hiçbir şey söylenmez.")]
        [SerializeField] private string afterLineId = "FE_TREE_FELL";
        [SerializeField, TextArea] private string afterFallback = "Ağaç yola devrildi. İyi ki uzakta durdum.";

        private Rigidbody2D body;
        private Collider2D playerCollider;
        private Coroutine routine;
        private bool lockedPlayer, prevCanMove;
        private Vector3 treeBasePos;
        private Quaternion treeBaseRot;
        private bool baseCaptured;
        private Transform fxRoot;
        private readonly List<Particle> particles = new List<Particle>();

        /// <summary>Olay oynadı mı (ağaç yerde). Reset'e kadar tekrar oynamaz.</summary>
        public bool HasFallen { get; private set; }
        /// <summary>Animasyon şu an sürüyor mu.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Ağaç yere değdiğinde (collider'lar açılırken).</summary>
        public event Action Impact;
        /// <summary>Olay bitti, kontrol geri verildi.</summary>
        public event Action Finished;

        private sealed class Particle
        {
            public SpriteRenderer Sr;
            public Vector2 Pos, Vel;
            public float Spin, Life, Age, Gravity, Drag, Sway, Phase, StartScale, EndScale, Alpha = 1f;
            public bool Fade;
        }

        private void Awake()
        {
            if (stopZone == null)
            {
                var t = transform.Find("TreeStopSpot");
                if (t != null) stopZone = t.GetComponent<Collider2D>();
            }
            if (stopZone != null) stopZone.isTrigger = true;
            CaptureBase();
            if (alertIcon != null) alertIcon.gameObject.SetActive(false);
            ApplyState(false);
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            // Sahne çıkışı / obje kapanırken kilit ve panel açık kalmasın.
            if (IsPlaying) Abort();
        }

        private void CaptureBase()
        {
            if (baseCaptured || tree == null) return;
            treeBasePos = tree.localPosition;
            treeBaseRot = tree.localRotation;
            baseCaptured = true;
        }

        private bool ResolvePlayer()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            if (player == null) return false;
            if (body == null) body = player.GetComponent<Rigidbody2D>();
            if (playerCollider == null) playerCollider = player.GetComponent<Collider2D>();
            return body != null;
        }

        private void FixedUpdate()
        {
            if (HasFallen || IsPlaying || stopZone == null || !stopZone.enabled) return;
            if (!ResolvePlayer() || !player.isActiveAndEnabled) return;
            if (requireFreeRoam && !FloodEveningHub.FreeRoam) return;
            bool inside = playerCollider != null && playerCollider.enabled
                ? stopZone.Distance(playerCollider).isOverlapped
                : stopZone.OverlapPoint(body.position);
            if (inside) Play();
        }

        /// <summary>Olayı hemen başlatır (tek sefer). Test ve akış için de çağrılabilir.</summary>
        public void Play()
        {
            if (HasFallen || IsPlaying) return;
            routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            IsPlaying = true;
            CaptureBase();
            FloodEveningHub.NotifyOpened(MinigameId);
            Debug.Log($"[FloodTreeFallEvent] {name}: ağaç düşüşü başladı", this);

            // 1) Cenk güvenli noktada durur, ağaca döner.
            ResolvePlayer();
            if (player != null)
            {
                prevCanMove = player.CanMove;
                lockedPlayer = true;
                player.CanMove = false;
                if (body != null) body.linearVelocity = Vector2.zero;
                var anim = player.GetComponent<IsoCharacterAnimator>();
                Vector3 target = lookAt != null ? lookAt.position : transform.TransformPoint((impactFrom + impactTo) * 0.5f);
                if (anim != null) anim.Face(target - player.transform.position);
            }

            // 2) Kırılma sesi + gövde titrer, kıymıklar sıçrar, Cenk'in başında "!".
            if (!string.IsNullOrEmpty(crackSfx)) GameAudio.PlaySfx(crackSfx);
            ShowAlert(true);
            SpawnChips();
            for (float t = 0f; t < creakSeconds; t += Time.deltaTime)
            {
                float k = t / creakSeconds;
                float wob = Mathf.Sin(t * 38f) * 1.6f * (1f - k * 0.5f) + Mathf.Lerp(0f, fallenAngle * 0.04f, k);
                SetTree(wob, 0f);
                yield return null;
            }

            // 3) Düşüş: yerçekimi hissi için hızlanan dönme (fizik yok) + hafif kayma.
            float startAngle = fallenAngle * 0.04f;
            for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
            {
                float k = t / fallSeconds;
                float e = k * k * (1.4f - 0.4f * k); // ease-in, sonda biraz yumuşar
                SetTree(Mathf.LerpUnclamped(startAngle, fallenAngle, e), e);
                yield return null;
            }
            SetTree(fallenAngle, 1f);

            // 4) Yere değdi: collider görselle aynı karede yolu kapatır, toz/yaprak.
            ApplyState(true);
            if (!string.IsNullOrEmpty(impactSfx)) GameAudio.PlaySfx(impactSfx);
            SpawnImpact();
            Impact?.Invoke();
            ShakeCamera();

            // Kısa geri sekme + oturma
            for (float t = 0f; t < settleSeconds; t += Time.deltaTime)
            {
                float k = t / settleSeconds;
                float b = Mathf.Sin(k * Mathf.PI) * bounceAngle * (1f - k * 0.6f);
                SetTree(fallenAngle - Mathf.Sign(fallenAngle) * b, 1f);
                yield return null;
            }
            SetTree(fallenAngle, 1f);

            for (float t = 0f; t < afterSeconds; t += Time.deltaTime) yield return null;
            ShowAlert(false);

            // 5) Kontrol geri gelir, sonra dal kurulur.
            HasFallen = true;
            Finish();
            FloodEveningHub.Complete(StepId);
            ArmBranches();
            Debug.Log($"[FloodTreeFallEvent] {name}: ağaç yolu kapattı, kontrol geri verildi", this);
            Finished?.Invoke();
            if (!string.IsNullOrEmpty(afterLineId) || !string.IsNullOrEmpty(afterFallback))
                FloodEveningHub.Say(afterLineId, afterFallback);
        }

        private void Finish()
        {
            IsPlaying = false;
            routine = null;
            if (lockedPlayer && player != null)
            {
                // Bölüm akışı (FreeRoam) kontrolü kendisi açar; test sahnesinde önceki hâline döner.
                player.CanMove = prevCanMove || (!requireFreeRoam || FloodEveningHub.FreeRoam);
            }
            lockedPlayer = false;
            FloodEveningHub.NotifyClosed(MinigameId);
        }

        private void Abort()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            ShowAlert(false);
            if (IsPlaying)
            {
                IsPlaying = false;
                if (lockedPlayer && player != null) player.CanMove = prevCanMove;
                lockedPlayer = false;
                FloodEveningHub.NotifyClosed(MinigameId);
            }
        }

        public void ResetModule()
        {
            Abort();
            HasFallen = false;
            ClearParticles();
            if (tree != null && baseCaptured)
            {
                tree.localPosition = treeBasePos;
                tree.localRotation = treeBaseRot;
            }
            ApplyState(false);
        }

        private void ApplyState(bool fallen)
        {
            if (fallZoneBlock != null) fallZoneBlock.enabled = !fallen;
            foreach (var c in closeOnImpact) if (c != null) c.enabled = fallen;
            foreach (var go in showOnImpact) if (go != null) go.SetActive(fallen);
        }

        private void ArmBranches()
        {
            foreach (var go in armOnFallen)
                if (go != null) go.SendMessage("Arm", SendMessageOptions.DontRequireReceiver);
        }

        private void SetTree(float angle, float slide01)
        {
            if (tree == null) return;
            tree.localRotation = treeBaseRot * Quaternion.Euler(0f, 0f, angle);
            tree.localPosition = treeBasePos + (Vector3)(fallSlide * slide01);
        }

        private void ShowAlert(bool on)
        {
            if (alertIcon == null) return;
            alertIcon.gameObject.SetActive(on);
            if (on && player != null) alertIcon.transform.position = player.transform.position + (Vector3)alertOffset;
        }

        private void ShakeCamera()
        {
            var cam = Camera.main;
            if (cam != null) StartCoroutine(Shake(cam.transform));
        }

        // Kamera takip scripti her karede konumu yeniden yazabilir; sarsıntı LateUpdate sonrası küçük ofset olarak uygulanır.
        private IEnumerator Shake(Transform cam)
        {
            Vector3 applied = Vector3.zero;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                yield return new WaitForEndOfFrame();
                if (cam == null) yield break;
                cam.position -= applied;
                float a = (1f - t / 0.25f) * (2f / 64f);
                applied = new Vector3(Mathf.Round(Mathf.Sin(t * 90f)) * a, Mathf.Round(Mathf.Cos(t * 70f)) * a * 0.5f, 0f);
                cam.position += applied;
            }
            if (cam != null) cam.position -= applied;
        }

        // =====================================================================
        // Efekt (basit sprite parçacıkları; ParticleSystem yok, kare hızından bağımsız)
        // =====================================================================

        private void Update()
        {
            if (particles.Count == 0) return;
            float dt = Time.deltaTime;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Age += dt;
                float k = p.Age / p.Life;
                if (k >= 1f)
                {
                    if (p.Sr != null) Destroy(p.Sr.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }
                p.Vel.y -= p.Gravity * dt;
                p.Vel *= Mathf.Clamp01(1f - p.Drag * dt);
                Vector2 sway = new Vector2(Mathf.Sin(p.Age * 5f + p.Phase) * p.Sway, 0f);
                p.Pos += (p.Vel + sway) * dt;
                if (p.Sr == null) continue;
                var tr = p.Sr.transform;
                tr.localPosition = new Vector3(Mathf.Round(p.Pos.x * 64f) / 64f, Mathf.Round(p.Pos.y * 64f) / 64f, 0f);
                tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(p.Spin * p.Age / 45f) * 45f);
                tr.localScale = Vector3.one * Mathf.Lerp(p.StartScale, p.EndScale, k);
                if (p.Fade)
                {
                    var c = p.Sr.color;
                    c.a = p.Alpha * (k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                    p.Sr.color = c;
                }
            }
        }

        private Transform FxRoot()
        {
            if (fxRoot != null) return fxRoot;
            fxRoot = new GameObject("FX").transform;
            fxRoot.SetParent(transform, false);
            return fxRoot;
        }

        private Particle Spawn(Sprite sprite, Vector2 pos, Vector2 vel, float life)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(FxRoot(), false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = fxSortingOrder;
            var p = new Particle { Sr = sr, Pos = pos, Vel = vel, Life = life, StartScale = 1f, EndScale = 1f, Fade = true };
            particles.Add(p);
            return p;
        }

        private void SpawnChips()
        {
            if (tree == null) return;
            Vector2 at = treeBasePos + new Vector3(0f, 0.08f, 0f);
            for (int i = 0; i < chipCount; i++)
            {
                var p = Spawn(chipSprite, at, new Vector2(UnityEngine.Random.Range(-0.9f, 1.2f), UnityEngine.Random.Range(0.8f, 1.8f)), UnityEngine.Random.Range(0.45f, 0.7f));
                if (p == null) return;
                p.Gravity = 6f; p.Spin = UnityEngine.Random.Range(-720f, 720f);
            }
        }

        private void SpawnImpact()
        {
            // Toz: gövde hattı boyunca yerden yükselip yanlara açılan, büyüyüp sönen bulutlar (dizi gibi durmasın diye dağınık).
            for (int i = 0; i < dustCount; i++)
            {
                float k = (i + UnityEngine.Random.Range(0.1f, 0.9f)) / Mathf.Max(1, dustCount);
                Vector2 at = Vector2.Lerp(impactFrom, impactTo, k) + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), UnityEngine.Random.Range(-0.2f, 0.1f));
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                var p = Spawn(dustSprite, at, new Vector2(side * UnityEngine.Random.Range(0.3f, 0.9f), UnityEngine.Random.Range(0.1f, 0.5f)), UnityEngine.Random.Range(0.8f, 1.3f));
                if (p == null) break;
                p.Drag = 3f; p.StartScale = UnityEngine.Random.Range(0.8f, 1.1f); p.EndScale = UnityEngine.Random.Range(1.8f, 2.6f);
                p.Alpha = UnityEngine.Random.Range(0.45f, 0.65f);
            }
            for (int i = 0; i < leafCount; i++)
            {
                float k = UnityEngine.Random.Range(0.35f, 1f);
                Vector2 at = Vector2.Lerp(impactFrom, impactTo, k) + new Vector2(0f, UnityEngine.Random.Range(0f, 0.35f));
                var p = Spawn(leafSprite, at, new Vector2(UnityEngine.Random.Range(-0.8f, 0.8f), UnityEngine.Random.Range(0.6f, 1.6f)), UnityEngine.Random.Range(1.1f, 1.8f));
                if (p == null) break;
                p.Gravity = 1.6f; p.Drag = 1.8f; p.Sway = UnityEngine.Random.Range(0.15f, 0.35f); p.Phase = UnityEngine.Random.Range(0f, 6.28f);
                p.Spin = UnityEngine.Random.Range(-360f, 360f);
                if (UnityEngine.Random.value < 0.35f) p.Sr.color = new Color(0.75f, 0.8f, 0.55f);
            }
        }

        private void ClearParticles()
        {
            foreach (var p in particles) if (p.Sr != null) Destroy(p.Sr.gameObject);
            particles.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(transform.TransformPoint(impactFrom), transform.TransformPoint(impactTo));
        }
    }
}
