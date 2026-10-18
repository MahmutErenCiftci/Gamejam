using System;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Objectives;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Yoldaki enkaz ve sel %30'u geçince yüzmeye başlayan hafif cisimler (LEAD 18 Eki).
    /// - Başta hepsi yolda durur (su katmanının ALTINDA, order -89): su yükseldikçe sel shader'ı onları kendiliğinden örter.
    ///   Yürünebilirler (katı değiller), güvenli yolu kesmezler.
    /// - Hafif cisimler (tahta, plastik kasa, şişe, dal): FloodEveningHub.Progress01 eşiği (0,30) aşılıp (%30 ve altı: hareketsiz) bulundukları yerde su varken
    ///   yüzeye çıkar (order -87), FloodWaterField.FlowAt akışıyla sürüklenir, hafifçe yüzer (1 px), her birinin hızı/evresi farklıdır.
    ///   Duvar, bina, kapanan yol ve dal engelinden geçmez (CircleCast, tetikleyici olmayan collider'lar), sudan çıkmaz (DepthAt);
    ///   takılır / çok uzaklaşırsa söner ve oyuncu uzaktayken başladığı yerde yeniden yüzeye çıkar.
    /// - Ağır cisimler (taş, moloz) yüzmez: yerinde kalır, derin ve hızlı suda arada 1 px yuvarlanır (en çok 3 px).
    /// - Çarpma: yüzen cisim Cenk'e değerse kısa sendeleme + ~1 karo geri itme (FloodPlayerReaction), hafif sarsıntı, ses; bekleme süresi
    ///   zincirleme çarpmayı önler. Tek başına başarısızlık değildir; sayaç durmaz. Görev paneline "debris_bump" uyarısı gider.
    /// - Sayaç durunca (avlu / süre doldu) çarpma kapanır, sürüklenme yavaşça durur; Hub.RequestReset hepsini başa alır.
    /// </summary>
    public class FloodDebrisField : MonoBehaviour, IFloodEveningResettable
    {
        public enum DebrisKind { Plank, Crate, Bottle, Branch, Stone, Rubble }

        [Serializable]
        public class Item
        {
            public DebrisKind kind;
            public Transform anchor;
            public Sprite sprite;
        }

        [SerializeField] private Item[] items = Array.Empty<Item>();
        [SerializeField] private FloodWaterField water;
        [SerializeField] private FloodPlayerReaction reaction;
        [Tooltip("Hafif cisimler bu sel ilerlemesinden sonra yüzer")]
        [Range(0f, 1f)] [SerializeField] private float floatFrom = 0.30f;
        [Tooltip("Yüzmek için gereken en az su derinliği")]
        [SerializeField] private float minDepth = 0.04f;
        [Tooltip("Sürüklenme hızı aralığı (dünya birimi / sn)")]
        [SerializeField] private Vector2 driftSpeed = new Vector2(0.2f, 0.45f);
        [SerializeField] private float maxTravel = 7f;
        [Header("Çarpma")]
        [Tooltip("Zemin ölçüsünde çarpma yarıçapı")]
        [SerializeField] private float bumpRadius = 0.3f;
        [SerializeField] private float bumpCooldown = 1.4f;
        [SerializeField] private float bumpSpeed = 3.0f;
        [SerializeField] private float bumpPush = 0.2f;
        [SerializeField] private float bumpLock = 0.3f;
        [SerializeField] private string bumpSfx = "SFX_FE_Debris_Bump";
        [Header("Çizim")]
        [SerializeField] private int groundOrder = -89;
        [SerializeField] private int floatOrder = -87;
        [SerializeField] private Color submergedTint = new Color(0.55f, 0.66f, 0.8f, 1f);
        [SerializeField] private bool logEvents = true;

        private enum State { Resting, Rising, Floating, Fading, Waiting }
        private class D
        {
            public Item def; public SpriteRenderer sr; public Vector2 origin, pos, vel, lastCheckPos; public State state;
            public float t, phase, speed, wait, checkT, cooldown, rollT; public int rollPx; public bool light;
        }
        private readonly List<D> list = new List<D>();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[6];
        private ContactFilter2D filter;
        private IsoPlayerController player;
        private Rigidbody2D playerBody;
        private FloodEveningHud hud;
        private float globalCooldown, stopFade = 1f;
        private bool toastFloat, toastBump;

        public int FloatingCount { get; private set; }
        public int BumpCount { get; private set; }
        public int LightCount { get; private set; }
        public int Count => list.Count;

        private void Awake()
        {
            if (water == null) water = FindAnyObjectByType<FloodWaterField>(FindObjectsInactive.Include);
            if (reaction == null) reaction = FindAnyObjectByType<FloodPlayerReaction>(FindObjectsInactive.Include);
            player = reaction != null && reaction.Player != null ? reaction.Player : FindAnyObjectByType<IsoPlayerController>(FindObjectsInactive.Include);
            if (player != null) playerBody = player.GetComponent<Rigidbody2D>();
            hud = FindAnyObjectByType<FloodEveningHud>(FindObjectsInactive.Include);
            filter = new ContactFilter2D { useTriggers = false };
            filter.NoFilter(); filter.useTriggers = false;
            var playerSr = player != null ? player.GetComponentInChildren<SpriteRenderer>(true) : null;
            int i = 0;
            foreach (var it in items)
            {
                if (it == null || it.anchor == null || it.sprite == null) continue;
                var go = new GameObject("Debris_" + it.kind + "_" + i++);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = it.sprite;
                if (playerSr != null) sr.sortingLayerID = playerSr.sortingLayerID;
                float h = Mathf.Abs(Mathf.Sin(i * 12.9898f + 4.1f) * 43758.5453f) % 1f;
                var d = new D { def = it, sr = sr, origin = it.anchor.position, light = it.kind <= DebrisKind.Branch, phase = h * 6.28f,
                    speed = Mathf.Lerp(driftSpeed.x, driftSpeed.y, (h * 3.7f) % 1f) };
                sr.flipX = h > 0.5f;
                if (d.light) LightCount++;
                list.Add(d);
                Rest(d);
            }
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable() => FloodEveningHub.ResetRequested -= ResetModule;

        private void Rest(D d)
        {
            d.state = State.Resting; d.pos = d.origin; d.vel = Vector2.zero; d.t = 0f; d.rollPx = 0; d.cooldown = 0f;
            d.sr.sortingOrder = groundOrder; d.sr.color = Color.white; d.sr.enabled = true;
            Place(d, 0f);
        }

        private void Place(D d, float bobPx)
        {
            Vector2 p = d.pos;
            d.sr.transform.position = new Vector3(Mathf.Round(p.x * 64f) / 64f, Mathf.Round(p.y * 64f + bobPx) / 64f, 0f);
        }

        private float Depth(Vector2 p) => water != null ? water.DepthAt(p) : -1f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float progress = FloodEveningHub.FloodStarted ? FloodEveningHub.Progress01 : 0f;
            bool stopped = FloodEveningHub.FloodStopped;
            stopFade = stopped ? Mathf.MoveTowards(stopFade, 0f, dt / 1.5f) : 1f;
            globalCooldown -= dt;
            Vector2 feet = player != null ? (Vector2)player.transform.position : new Vector2(9999f, 9999f);
            int floating = 0;
            foreach (var d in list)
            {
                d.cooldown -= dt;
                switch (d.state)
                {
                    case State.Resting:
                        if (!d.light) { UpdateHeavy(d, dt); break; }
                        if (!stopped && progress > floatFrom && Depth(d.pos) > minDepth) { d.state = State.Rising; d.t = 0f; d.sr.sortingOrder = floatOrder; LogE("yüzmeye başladı: " + d.sr.name); }
                        break;
                    case State.Rising:
                    {
                        d.t += dt;
                        float k = Mathf.Clamp01(d.t / 0.8f);
                        d.sr.color = Color.Lerp(submergedTint * new Color(1f, 1f, 1f, 0.6f), Color.white, k);
                        Place(d, Mathf.Round(k));
                        if (k >= 1f) { d.state = State.Floating; d.t = 0f; d.lastCheckPos = d.pos; d.checkT = 0f; ToastFloat(d); }
                        floating++;
                        break;
                    }
                    case State.Floating:
                        Drift(d, dt, progress);
                        if (!stopped) TryBump(d, feet);
                        floating++;
                        break;
                    case State.Fading:
                    {
                        d.t += dt;
                        float k = Mathf.Clamp01(1f - d.t / 0.6f);
                        var c = d.sr.color; c.a = k; d.sr.color = c;
                        if (k <= 0f) { d.state = State.Waiting; d.sr.enabled = false; d.wait = UnityEngine.Random.Range(3f, 6f); }
                        break;
                    }
                    case State.Waiting:
                        d.wait -= dt;
                        if (d.wait > 0f || stopped) break;
                        if (FloodPlayerReaction.GroundDistance(feet, d.origin) < 3f || Depth(d.origin) <= minDepth) { d.wait = 1f; break; }
                        d.pos = d.origin; d.vel = Vector2.zero; d.sr.enabled = true; d.sr.sortingOrder = floatOrder;
                        d.state = State.Rising; d.t = 0f;
                        break;
                }
            }
            FloatingCount = floating;
        }

        private void UpdateHeavy(D d, float dt)
        {
            if (water == null || d.rollPx >= 3) return;
            float depth = Depth(d.origin);
            if (depth < 0.15f) return;
            Vector2 flow = water.FlowAt(d.pos, out float spd);
            if (spd < 0.35f) return;
            d.rollT += dt;
            if (d.rollT < 2.5f) return;
            d.rollT = UnityEngine.Random.Range(-1f, 0.5f);
            d.rollPx++;
            Vector2 step = new Vector2(Mathf.Sign(flow.x) * (Mathf.Abs(flow.x) > 0.3f ? 1f : 0f), Mathf.Sign(flow.y) * (Mathf.Abs(flow.y) > 0.3f ? 1f : 0f)) / 64f;
            d.pos += step;
            Place(d, 0f);
        }

        private void Drift(D d, float dt, float progress)
        {
            d.t += dt;
            float spd = 0f;
            Vector2 flow = water != null ? water.FlowAt(d.pos, out spd) : Vector2.zero;
            Vector2 side = new Vector2(-flow.y, flow.x);
            float wander = (Mathf.PerlinNoise(d.phase, d.t * 0.35f) - 0.5f) * 0.16f;
            Vector2 target = (flow * d.speed * Mathf.Lerp(0.6f, 1.25f, spd) * (0.75f + 0.5f * progress) + side * wander) * stopFade;
            d.vel = Vector2.MoveTowards(d.vel, target, dt * 0.9f);
            Vector2 step = d.vel * dt;
            float dist = step.magnitude;
            if (dist > 1e-5f)
            {
                Vector2 dir = step / dist;
                Vector2 nextPos = d.pos + step;
                bool blocked = Depth(nextPos) < minDepth * 0.5f;
                Vector2 normal = -dir;
                if (!blocked)
                {
                    int n = Physics2D.CircleCast(d.pos, 0.07f, dir, filter, hits, dist + 0.03f);
                    for (int i = 0; i < n; i++)
                    {
                        var h = hits[i];
                        if (h.collider == null || h.collider.isTrigger) continue;
                        if (playerBody != null && h.rigidbody == playerBody) continue;
                        if (h.distance <= 0f && Vector2.Dot(dir, h.normal) > 0f) continue; // içinden dışarı çıkıyor
                        blocked = true; normal = h.normal; break;
                    }
                }
                if (blocked) d.vel = Vector2.Reflect(d.vel, normal.sqrMagnitude > 0.01f ? normal.normalized : -dir) * 0.45f;
                else d.pos = nextPos;
            }
            float bob = Mathf.Round(Mathf.Sin(d.t * 2.4f + d.phase));
            Place(d, bob);
            // takıldı / çok uzaklaştı / su çekildi → sön, sonra baştan yüzeye çık
            d.checkT += dt;
            if (d.checkT >= 3f)
            {
                bool stuck = (d.pos - d.lastCheckPos).sqrMagnitude < 0.0004f && stopFade > 0.9f;
                d.checkT = 0f; d.lastCheckPos = d.pos;
                if (stuck) Fade(d, "takıldı");
            }
            if (d.state == State.Floating && ((d.pos - d.origin).magnitude > maxTravel || Depth(d.pos) < minDepth * 0.5f)) Fade(d, "uzaklaştı");
        }

        private void Fade(D d, string why)
        {
            if (d.state != State.Floating) return;
            d.state = State.Fading; d.t = 0f;
        }

        private void TryBump(D d, Vector2 feet)
        {
            if (reaction == null || globalCooldown > 0f || d.cooldown > 0f || !reaction.CanReact) return;
            if (FloodPlayerReaction.GroundDistance(feet, d.pos) > bumpRadius) return;
            Vector2 away = feet - d.pos;
            if (away.sqrMagnitude < 1e-5f) away = d.vel.sqrMagnitude > 1e-5f ? d.vel : Vector2.right;
            if (!reaction.React(FloodPlayerReaction.Kind.Bump, away, bumpSpeed, bumpPush, bumpLock)) return;
            globalCooldown = bumpCooldown; d.cooldown = bumpCooldown * 1.8f;
            d.vel = -FloodPlayerReaction.GroundToWorld(away, 0.35f);
            BumpCount++;
            GameAudio.PlaySfx(bumpSfx, 0.75f);
            ObjectiveTracker.Emit("event", "debris_bump");
            LogE($"enkaz çarptı: {d.def.kind} (toplam {BumpCount})");
            if (!toastBump && hud != null)
            {
                toastBump = true;
                hud.ShowToast("YÜZEN ENKAZ ÇARPTI", "Akıntı tahta ve kasaları sürüklüyor. Yüzen cisimlerden uzak dur.", FloodEveningHud.ToastKind.Warning, 3f);
            }
        }

        private void ToastFloat(D d)
        {
            if (toastFloat || hud == null || !FloodEveningHub.FreeRoam) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 vp = cam.WorldToViewportPoint(d.pos);
            if (vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.05f || vp.y > 0.95f) return;
            toastFloat = true;
            hud.ShowToast("SU ENKAZ SÜRÜKLÜYOR", "Tahta, kasa ve dallar akıntıyla geliyor. Uzak dur, yolun kuru kenarından ilerle.", FloodEveningHud.ToastKind.Warning, 3.5f);
        }

        public void ResetModule()
        {
            if (FloodEveningHub.FloodStopped) return; // süre doldu: görüntü kalsın (Tekrar Dene sahneyi baştan kurar)
            globalCooldown = 0f; stopFade = 1f;
            foreach (var d in list) Rest(d);
        }

        private void LogE(string m) { if (logEvents) Debug.Log("[FE] " + m, this); }
    }
}
