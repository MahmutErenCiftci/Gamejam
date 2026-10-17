using System;
using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Rüzgârla mücadele (PROMPT §F). WindPass'teki trigger alanına (area: açık, kuru geçit) eklenir.
    /// Rüzgâr yönü belirli aralıklarla yumuşakça değişir; alanın içinde ekranın üstünde yön ikonu + kısa açıklama,
    /// alanda rüzgâr çizgileri görünür. Oyuncu rüzgâra karşı / yan yürürken IsoPlayerController.SpeedMultiplier hafifçe
    /// düşer (en az minAgainst); rüzgârla aynı yönde yürümek HIZLANDIRMAZ (1'in üstüne çıkmaz) ve bileşen hiçbir zaman
    /// kuvvet/hız EKLEMEZ: oyuncu suya ya da tehlikeye itilmez. Hub.FreeRoam false iken (diyalog, panel) ve oyuncu
    /// alandan çıkınca etki hemen sıfırlanır (SpeedMultiplier = 1). "Rüzgârı takip et" mesajı verilmez; açıklama güvenli
    /// rota işaretlerini esas alır.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class WindGustZone : MonoBehaviour, IFloodEveningResettable
    {
        [Tooltip("Rüzgâr alanı (trigger). Boşsa bu objedeki Collider2D.")]
        [SerializeField] private Collider2D area;
        [Tooltip("Boşsa sahnedeki IsoPlayerController.")]
        [SerializeField] private IsoPlayerController player;

        [Header("Yön")]
        [Tooltip("Ekran yönleri (x sağ, y yukarı); sırayla dolaşılır.")]
        [SerializeField] private Vector2[] directions = { new Vector2(-1f, -0.5f), new Vector2(-1f, 0.5f), new Vector2(1f, -0.5f), new Vector2(0f, -1f) };
        [SerializeField, Min(1f)] private float changeEverySeconds = 4.5f;
        [SerializeField, Min(0.05f)] private float turnSeconds = 0.9f;

        [Header("Yavaşlatma")]
        [Tooltip("Rüzgâra tam karşı yürürken hız çarpanı (Progress01 = 1'de).")]
        [SerializeField, Range(0.5f, 1f)] private float minAgainst = 0.72f;
        [Tooltip("Rüzgâra dik yürürken hız çarpanı (Progress01 = 1'de).")]
        [SerializeField, Range(0.5f, 1f)] private float minAcross = 0.9f;
        [Tooltip("Bölüm başında (Progress01 = 0) etkinin oranı; ilerledikçe 1'e çıkar.")]
        [SerializeField, Range(0f, 1f)] private float strengthAtStart = 0.75f;
        [SerializeField, Min(0.1f)] private float response = 6f;

        [Header("Gösterge")]
        [SerializeField] private Font font;
        [Tooltip("Sağa bakan ok (UI_FE_WindArrow). Boşsa yazıyla '>' çizilir.")]
        [SerializeField] private Sprite arrowIcon;
        [SerializeField] private string title = "SERT RÜZGÂR";
        [SerializeField] private string body = "Rüzgâr seni yavaşlatabilir. Dikkatli yürü, işaretli güvenli rotada kal.";
        [SerializeField] private int sortingOrder = 41;
        [SerializeField] private float topMargin = 30f;

        [Header("Rüzgâr çizgileri (dünya)")]
        [SerializeField] private Sprite streakSprite;
        [SerializeField, Range(0, 40)] private int streakCount = 14;
        [SerializeField] private float streakSpeed = 2.4f;
        [SerializeField] private int streakSortingOrder = 900;
        [SerializeField] private Color streakColor = new Color(0.85f, 0.95f, 1f, 0.55f);

        [Header("Bir kez söylenecek satır (isteğe bağlı)")]
        [SerializeField] private string firstEnterLineId = "FE_WIND_ENTER";
        [SerializeField] private string firstEnterFallback = "Rüzgâr çok sert. Acele etmeden, işaretli rotadan ilerlemeliyim.";

        private float dirTimer, turnT = 1f;
        private int dirIndex;
        private Vector2 fromDir, toDir, windDir = Vector2.left;
        private float currentMultiplier = 1f;
        private bool applied, saidOnce, inside;
        private float hudAlpha;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform arrow;
        private readonly List<Transform> streaks = new List<Transform>();
        private readonly List<SpriteRenderer> streakRenderers = new List<SpriteRenderer>();
        private float[] streakPhase = Array.Empty<float>();

        /// <summary>Etki şu an açık mı (oyuncu alanda, serbest dolaşımda).</summary>
        public bool IsActive { get; private set; }
        /// <summary>Şu anki rüzgâr yönü (ekran, normalize).</summary>
        public Vector2 WindDirection => windDir;
        /// <summary>Bu bileşenin oyuncuya uyguladığı hız çarpanı (etki kapalıyken 1).</summary>
        public float AppliedMultiplier => applied ? currentMultiplier : 1f;

        private void Awake()
        {
            if (area == null) area = GetComponent<Collider2D>();
            if (area != null) area.isTrigger = true;
            if (directions == null || directions.Length == 0) directions = new[] { Vector2.left };
            windDir = toDir = fromDir = directions[0].normalized;
        }

        private void Start()
        {
            BuildHud();
            BuildStreaks();
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            Release();
            if (group != null) group.alpha = hudAlpha = 0f;
        }

        private void Update()
        {
            UpdateDirection(Time.deltaTime);

            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            inside = player != null && player.isActiveAndEnabled && area != null && area.enabled && area.OverlapPoint(player.transform.position);
            IsActive = inside && FloodEveningHub.FreeRoam && player.CanMove;

            if (IsActive)
            {
                if (!saidOnce && !string.IsNullOrEmpty(firstEnterLineId))
                {
                    saidOnce = true;
                    Release();
                    FloodEveningHub.Say(firstEnterLineId, firstEnterFallback);
                }
                else
                {
                    float target = TargetMultiplier(GameInput.MoveVector);
                    currentMultiplier = Mathf.MoveTowards(currentMultiplier, target, response * Time.deltaTime);
                    player.SpeedMultiplier = currentMultiplier;
                    applied = true;
                }
            }
            else Release();

            float wantHud = inside && FloodEveningHub.FreeRoam ? 1f : 0f;
            hudAlpha = Mathf.MoveTowards(hudAlpha, wantHud, Time.unscaledDeltaTime * 5f);
            if (group != null)
            {
                group.alpha = hudAlpha;
                if (canvas.gameObject.activeSelf != hudAlpha > 0f) canvas.gameObject.SetActive(hudAlpha > 0f);
            }
            if (arrow != null)
            {
                float ang = Mathf.Atan2(windDir.y, windDir.x) * Mathf.Rad2Deg;
                arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(ang / 15f) * 15f);
            }
            UpdateStreaks(Time.deltaTime);
        }

        // Rüzgâra karşı: 1 → minAgainst, dik: minAcross, rüzgârla aynı yön: 1 (hızlanma yok).
        private float TargetMultiplier(Vector2 move)
        {
            if (move.sqrMagnitude < 0.01f) return 1f;
            float s = Mathf.Lerp(strengthAtStart, 1f, Mathf.Clamp01(FloodEveningHub.Progress01));
            float dot = Vector2.Dot(move.normalized, windDir);
            float slow = dot <= 0f
                ? (1f - minAgainst) * -dot + (1f - minAcross) * (1f + dot)
                : (1f - minAcross) * (1f - dot);
            return Mathf.Clamp(1f - s * slow, minAgainst, 1f);
        }

        private void Release()
        {
            currentMultiplier = 1f;
            if (!applied) return;
            applied = false;
            if (player != null) player.SpeedMultiplier = 1f;
        }

        private void UpdateDirection(float dt)
        {
            dirTimer += dt;
            if (dirTimer >= changeEverySeconds && directions.Length > 1)
            {
                dirTimer = 0f;
                dirIndex = (dirIndex + 1) % directions.Length;
                fromDir = windDir;
                toDir = directions[dirIndex].normalized;
                turnT = 0f;
            }
            if (turnT < 1f)
            {
                turnT = Mathf.Min(1f, turnT + dt / turnSeconds);
                float a0 = Mathf.Atan2(fromDir.y, fromDir.x), a1 = Mathf.Atan2(toDir.y, toDir.x);
                float a = Mathf.LerpAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg, EventsUi.Smooth01(turnT)) * Mathf.Deg2Rad;
                windDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        public void ResetModule()
        {
            Release();
            saidOnce = false;
            dirTimer = 0f;
            dirIndex = 0;
            turnT = 1f;
            windDir = toDir = fromDir = directions[0].normalized;
            hudAlpha = 0f;
            if (group != null) group.alpha = 0f;
        }

        private void BuildHud()
        {
            if (canvas != null) return;
            var f = EventsUi.ResolveFont(font);
            canvas = EventsUi.CreateCanvas(this, "FE_WindCanvas", sortingOrder, out group);
            var p = EventsUi.Panel("WindPanel", canvas.transform, EventsUi.TealDark, EventsUi.Teal);
            var rt = EventsUi.Place(p.rectTransform, 0.5f, 1f, new Vector2(0f, -topMargin), new Vector2(236f, 40f));

            var badge = EventsUi.Img("ArrowBack", rt, EventsUi.PanelLight);
            EventsUi.TL(badge.rectTransform, 6f, 7f, 28f, 28f);
            if (arrowIcon != null)
            {
                var a = EventsUi.Img("Arrow", badge.transform, EventsUi.Teal, arrowIcon);
                arrow = EventsUi.Stretch(a.rectTransform, 5f);
            }
            else
            {
                var a = EventsUi.Txt("Arrow", badge.transform, f, 16, EventsUi.Teal, TextAnchor.MiddleCenter);
                a.text = ">";
                arrow = EventsUi.Stretch(a.rectTransform);
            }

            var t = EventsUi.Txt("Title", rt, f, 10, EventsUi.Teal, TextAnchor.MiddleLeft);
            EventsUi.TL(t.rectTransform, 40f, 5f, 190f, 13f);
            t.text = title;
            var b = EventsUi.Txt("Body", rt, f, 8, EventsUi.Cream, TextAnchor.UpperLeft);
            EventsUi.TL(b.rectTransform, 40f, 18f, 190f, 20f);
            b.text = body;

            group.alpha = 0f;
            group.blocksRaycasts = false;
            canvas.gameObject.SetActive(false);
        }

        private void BuildStreaks()
        {
            if (streakSprite == null || area == null || streakCount <= 0) return;
            streakPhase = new float[streakCount];
            for (int i = 0; i < streakCount; i++)
            {
                var go = new GameObject("WindStreak" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                sr.sortingOrder = streakSortingOrder;
                sr.color = new Color(streakColor.r, streakColor.g, streakColor.b, 0f);
                streaks.Add(go.transform);
                streakRenderers.Add(sr);
                streakPhase[i] = UnityEngine.Random.value;
                go.transform.position = RandomPointInArea();
            }
        }

        private Vector3 RandomPointInArea()
        {
            var b = area.bounds;
            for (int k = 0; k < 12; k++)
            {
                var p = new Vector2(UnityEngine.Random.Range(b.min.x, b.max.x), UnityEngine.Random.Range(b.min.y, b.max.y));
                if (area.OverlapPoint(p)) return p;
            }
            return b.center;
        }

        private void UpdateStreaks(float dt)
        {
            if (streaks.Count == 0) return;
            float intensity = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(FloodEveningHub.Progress01));
            float ang = Mathf.Atan2(windDir.y, windDir.x) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, ang);
            Vector3 step = (Vector3)(windDir * streakSpeed * intensity * dt);
            for (int i = 0; i < streaks.Count; i++)
            {
                streakPhase[i] += dt / 0.9f;
                var tr = streaks[i];
                if (streakPhase[i] >= 1f || !area.OverlapPoint(tr.position))
                {
                    streakPhase[i] = 0f;
                    tr.position = RandomPointInArea();
                }
                tr.position += step;
                tr.rotation = rot;
                float k = streakPhase[i];
                float a = Mathf.Sin(k * Mathf.PI) * streakColor.a * intensity;
                var c = streakColor;
                c.a = a;
                streakRenderers[i].color = c;
            }
        }
    }
}
