using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Objectives
{
    /// <summary>
    /// Sol üst görev paneli (640×360 referans, 168 birim genişlik, en çok 4 satır), koddan kurulur, fareyi engellemez.
    /// Başlık "SEL · SOKAK" + "HAZIRLIK %NN" + bölmeli çubuk. Satır durumları: yapılacak (boş kutu), etkin (açık zemin),
    /// bitti (yeşil tik + üstü çizili), uyarı (sarı "!" + "N UYARI"), ders (kırmızı × + "DERS"), bonus (altın yıldız),
    /// sayaç (kutucuklar). Tamamlanma: tik büyüyüp oturur (0–180 ms), üstü çizilir (80–300 ms), "+%N" yükselir, çubuk dolar
    /// (150–600 ms), satır 1,4 sn sonra sola kayarak çıkar, sıradaki "YENİ" etiketiyle gelir. Ekran sarsıntısı yok.
    /// İkonlar piksel maskelerinden üretilir (Point filtre). Ses kancaları UnityEvent'lerdir (ses dosyası şart değil).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectiveHudView : ObjectiveViewBase
    {
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 40;
        [Tooltip("Panelin sol üst köşesi (640×360 referans birimi)")]
        [SerializeField] private Vector2 panelPosition = new Vector2(8f, 8f);

        [Header("Ses kancaları")]
        [Tooltip("Görev tamamlandı (parametre: art arda tamamlanma sayısı, 2 sn içinde)")]
        [SerializeField] private UnityEvent<int> onObjectiveCompleted = new UnityEvent<int>();
        [SerializeField] private UnityEvent onBonusCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onWarned = new UnityEvent();
        [SerializeField] private UnityEvent onLesson = new UnityEvent();
        [Tooltip("Sayaç arttı (parametre: yeni sayı)")]
        [SerializeField] private UnityEvent<int> onCounterProgress = new UnityEvent<int>();
        [SerializeField] private UnityEvent onNewObjective = new UnityEvent();
        [SerializeField] private UnityEvent onGroupCompleted = new UnityEvent();

        private const float Width = 168f, HeaderH = 23f, RowH = 13f, RowX = 3f, RowW = 162f, BannerH = 13f;
        private const float BarX = 8f, BarY = 16f, BarW = 153f, BarH = 4f;
        private const int MaxRows = 4, FontSize = 8;

        private static readonly Color Navy = C(0x263559, 0.94f), RowHi = C(0x34477A, 0.95f), Ink = C(0x14141F),
            Cream = C(0xF4EBD8), Muted = C(0x9C98A0), Gold = C(0xFFD27E), Amber = C(0xF2A33A), Green = C(0x18A957),
            GreenFlash = C(0x1F7A55, 0.95f), Red = C(0xE8392B), PipDim = C(0x45537D), PipExtra = C(0x323E63),
            Grey = C(0x8A8FA3), BarBack = C(0x141A2C);

        private sealed class RowVisual
        {
            public ObjectiveRowState Data;
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Bg;
            public Image Icon;
            public Text Label;
            public Image Strike;
            public Text Tag;
            public RectTransform PipRoot;
            public readonly List<Image> Pips = new List<Image>();
            public Coroutine Animation;
            public bool Finishing;
            public float Height = RowH;
            public float Y = float.NaN;
            public float NewUntil;
            public float Slide;
        }

        private readonly List<ObjectiveRowState> source = new List<ObjectiveRowState>();
        private readonly List<RowVisual> visible = new List<RowVisual>();
        private readonly HashSet<string> dismissed = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Image> segments = new List<Image>();
        private readonly List<Image> segmentFills = new List<Image>();
        private RectTransform canvasRoot;
        private RectTransform panel;
        private RectTransform barRoot;
        private CanvasGroup visibility;
        private Text titleLabel;
        private Text progressLabel;
        private RectTransform bannerRoot;
        private Text bannerLabel;
        private Coroutine fillAnimation;
        private Coroutine bannerAnimation;
        private float fillValue;
        private float targetProgress;
        private float pendingGlowUntil;
        private int lastPercentGain;
        private int segmentCount;
        private bool headerSet;
        private bool rowsSet;
        private bool wantVisible;
        private float panelHeight = HeaderH + 3f;
        private float lastCompleteAt = float.NegativeInfinity;
        private int combo;

        private static Sprite boxSprite, tickSprite, warnSprite, lessonSprite, starSprite, dotSprite;

        public UnityEvent<int> OnObjectiveCompleted => onObjectiveCompleted;
        public UnityEvent OnBonusCompleted => onBonusCompleted;
        public UnityEvent OnWarned => onWarned;
        public UnityEvent OnLesson => onLesson;
        public UnityEvent<int> OnCounterProgress => onCounterProgress;
        public UnityEvent OnNewObjective => onNewObjective;
        public UnityEvent OnGroupCompleted => onGroupCompleted;
        public int VisibleRowCount => visible.Count;

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (canvasRoot != null) Destroy(canvasRoot.gameObject);
        }

        // ------------------------------------------------------------------ ObjectiveViewBase

        public override void SetHeader(string title, float progress01)
        {
            Build();
            titleLabel.text = (title ?? "GÖREVLER").ToUpper(new System.Globalization.CultureInfo("tr-TR"));
            float next = Mathf.Clamp01(progress01);
            lastPercentGain = Mathf.Max(0, Mathf.RoundToInt(next * 100f) - Mathf.RoundToInt(targetProgress * 100f));
            if (headerSet && Mathf.Approximately(targetProgress, next)) return;
            targetProgress = next;
            if (fillAnimation != null) StopCoroutine(fillAnimation);
            fillAnimation = null;
            if (!headerSet || !isActiveAndEnabled) SetFill(next);
            else fillAnimation = StartCoroutine(AnimateFill(next));
            headerSet = true;
        }

        public override void SetSegments(int count)
        {
            Build();
            count = Mathf.Clamp(count, 0, 40);
            if (count == segmentCount && segments.Count > 0) return;
            segmentCount = count;
            foreach (var s in segments) if (s != null) Destroy(s.gameObject);
            segments.Clear(); segmentFills.Clear();
            int n = Mathf.Max(1, count);
            int gap = n > 1 ? 1 : 0;
            int usable = Mathf.RoundToInt(BarW) - gap * (n - 1);
            int x = 0;
            for (int i = 0; i < n; i++)
            {
                // Kalan pikseller ilk bölmelere dağıtılır: bütün bölmeler tam piksel.
                int w = usable / n + (i < usable % n ? 1 : 0);
                var seg = Rect("Segment" + i, barRoot, new Vector2(x, 0f), new Vector2(w, BarH));
                segments.Add(Box(seg, BarBack));
                var fillRect = Rect("Fill", seg, Vector2.zero, new Vector2(0f, BarH));
                segmentFills.Add(Box(fillRect, Green));
                x += w + gap;
            }
            SetFill(fillValue);
        }

        public override void SetRows(IReadOnlyList<ObjectiveRowState> rows)
        {
            Build();
            source.Clear();
            if (rows != null)
                foreach (var row in rows)
                {
                    if (row == null || string.IsNullOrEmpty(row.id)) continue;
                    source.Add(row.Copy());
                    // Aynı sahnede GameSession sıfırlandıysa bu ID yeniden gösterilebilir.
                    if (row.state != ObjectiveState.Done) dismissed.Remove(row.id);
                }
            Reconcile();
            rowsSet = true;
        }

        public override void PlayComplete(string id)
        {
            Build();
            var row = Promote(id);
            if (row == null || row.Finishing || dismissed.Contains(id)) return;
            if (row.Animation != null) StopCoroutine(row.Animation);
            row.Finishing = true;
            row.Data.state = ObjectiveState.Done;
            combo = Time.unscaledTime - lastCompleteAt <= 2f ? combo + 1 : 1;
            lastCompleteAt = Time.unscaledTime;
            onObjectiveCompleted.Invoke(combo);
            if (row.Data.kind == ObjectiveKind.Bonus) onBonusCompleted.Invoke();
            row.Animation = StartCoroutine(AnimateComplete(row, lastPercentGain));
        }

        public override void PlayWarn(string id)
        {
            Build();
            var row = Promote(id);
            if (row == null || row.Finishing) return;
            if (row.Animation != null) StopCoroutine(row.Animation);
            var data = source.Find(x => x.id == id);
            if (data != null) row.Data = data.Copy();
            Style(row);
            if (row.Data.state == ObjectiveState.Failed) onLesson.Invoke();
            else onWarned.Invoke();
            row.Animation = StartCoroutine(AnimateWarn(row));
        }

        public override void ShowBanner(string text)
        {
            Build();
            if (bannerAnimation != null) StopCoroutine(bannerAnimation);
            bannerLabel.text = text ?? "";
            bannerAnimation = StartCoroutine(AnimateBanner());
        }

        public override void SetVisible(bool show)
        {
            Build();
            // Objeyi kapatmıyoruz: animasyonlar gizliyken de sonlanabilir; alfa LateUpdate'te yumuşakça değişir.
            wantVisible = show;
        }

        // ------------------------------------------------------------------ kurulum

        private void Build()
        {
            if (canvasRoot != null) return;
            if (font == null) font = AfetUyanisi.UI.GameFonts.Objective; // görev paneli: Pixelify Sans
            BuildSprites();

            canvasRoot = new GameObject("ObjectiveHudCanvas", typeof(RectTransform)).GetComponent<RectTransform>();
            canvasRoot.SetParent(transform, false);
            canvasRoot.gameObject.layer = 5;
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            visibility = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            visibility.alpha = 0f;
            visibility.blocksRaycasts = false;
            visibility.interactable = false;

            panel = Rect("ObjectivePanel", canvasRoot, new Vector2(panelPosition.x, -panelPosition.y), new Vector2(Width, panelHeight));
            Box(panel, Navy, true);
            var accent = Rect("Accent", panel, Vector2.zero, new Vector2(2f, panelHeight));
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.sizeDelta = new Vector2(2f, 0f);
            Box(accent, Gold);

            titleLabel = Label("GroupTitle", panel, new Vector2(8f, -2f), new Vector2(100f, 13f), Gold, TextAnchor.MiddleLeft);
            progressLabel = Label("Preparation", panel, new Vector2(60f, -2f), new Vector2(101f, 13f), Cream, TextAnchor.MiddleRight);
            barRoot = Rect("ProgressBar", panel, new Vector2(BarX, -BarY), new Vector2(BarW, BarH));
            SetSegments(0);

            bannerRoot = Rect("GroupBanner", panel, new Vector2(RowX, -HeaderH), new Vector2(RowW, BannerH));
            Box(bannerRoot, Gold);
            bannerRoot.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            bannerLabel = Label("Text", bannerRoot, Vector2.zero, new Vector2(RowW, BannerH), Ink, TextAnchor.MiddleCenter);
            var starL = Rect("StarL", bannerRoot, new Vector2(4f, -2f), new Vector2(9f, 9f));
            Icon(starL, starSprite);
            var starR = Rect("StarR", bannerRoot, new Vector2(RowW - 13f, -2f), new Vector2(9f, 9f));
            Icon(starR, starSprite);
            bannerRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ satırlar

        private void Reconcile()
        {
            // Sıra takipçiden gelir; çıkış animasyonundaki satırlar bulundukları yerde kalır (zıplama olmasın).
            var wanted = new List<ObjectiveRowState>();
            foreach (var data in source)
                if (!dismissed.Contains(data.id) && !wanted.Exists(x => x.id == data.id)) wanted.Add(data);
            for (int i = 0; i < visible.Count; i++)
            {
                var row = visible[i];
                if (!row.Finishing) continue;
                int at = wanted.FindIndex(x => x.id == row.Data.id);
                if (at >= 0) wanted[at] = row.Data;
                else wanted.Insert(Mathf.Min(i, wanted.Count), row.Data);
            }
            for (int i = wanted.Count - 1; i >= 0 && wanted.Count > MaxRows; i--)
            {
                var id = wanted[i].id;
                if (!visible.Exists(x => x.Finishing && x.Data.id == id)) wanted.RemoveAt(i);
            }
            for (int i = visible.Count - 1; i >= 0; i--)
                if (!wanted.Exists(x => x.id == visible[i].Data.id)) Remove(visible[i]);
            foreach (var data in wanted)
            {
                var row = visible.Find(x => x.Data.id == data.id);
                if (row == null) visible.Add(CreateRow(data));
                else if (!row.Finishing) UpdateRow(row, data);
            }
            visible.Sort((a, b) => wanted.FindIndex(x => x.id == a.Data.id).CompareTo(wanted.FindIndex(x => x.id == b.Data.id)));
            UpdateHighlight();
        }

        private RowVisual Promote(string id)
        {
            var found = visible.Find(x => x.Data.id == id);
            if (found != null) return found;
            var data = source.Find(x => x.id == id);
            if (data == null || dismissed.Contains(id)) return null;
            if (visible.Count >= MaxRows)
            {
                var replace = visible.FindLast(x => !x.Finishing);
                if (replace == null) return null; // Takipçi 1,65 sn aralıkla sunduğu için normalde oluşmaz.
                Remove(replace);
            }
            found = CreateRow(data);
            visible.Insert(0, found);
            UpdateHighlight();
            return found;
        }

        private RowVisual CreateRow(ObjectiveRowState data)
        {
            var row = new RowVisual();
            row.Root = Rect("Objective_" + data.id, panel, new Vector2(RowX, -HeaderH), new Vector2(RowW, RowH));
            row.Bg = Box(row.Root, new Color(0f, 0f, 0f, 0f));
            row.Group = row.Root.gameObject.AddComponent<CanvasGroup>();
            row.Group.blocksRaycasts = false;
            var icon = Rect("Icon", row.Root, new Vector2(7.5f, -6.5f), new Vector2(9f, 9f));
            icon.pivot = new Vector2(0.5f, 0.5f);
            row.Icon = Icon(icon, boxSprite);
            row.Label = Label("TaskText", row.Root, new Vector2(15f, 0f), new Vector2(110f, RowH), Cream, TextAnchor.MiddleLeft);
            row.Strike = Box(Rect("Strike", row.Root, new Vector2(15f, -7f), new Vector2(0f, 1f)), Muted);
            row.Tag = Label("Tag", row.Root, new Vector2(RowW - 63f, 0f), new Vector2(60f, RowH), Gold, TextAnchor.MiddleRight);
            row.PipRoot = Rect("Pips", row.Root, Vector2.zero, new Vector2(0f, 4f));

            bool isNew = rowsSet && seen.Add(data.id);
            if (!rowsSet) seen.Add(data.id);
            if (isNew)
            {
                row.NewUntil = Time.unscaledTime + 1.4f;
                row.Slide = -8f;
                row.Group.alpha = 0f;
                onNewObjective.Invoke();
            }
            row.Data = data.Copy();
            Style(row);
            return row;
        }

        private void UpdateRow(RowVisual row, ObjectiveRowState data)
        {
            var old = row.Data;
            row.Data = data.Copy();
            Style(row);
            if (old != null && data.count > old.count && data.kind == ObjectiveKind.Counter)
            {
                onCounterProgress.Invoke(data.count);
                StartCoroutine(PipPop(row, old.count, data.count));
            }
        }

        // Satırın bütün görünümünü Data'dan kurar (ikon, yazı, etiket, kutucuklar, yükseklik).
        private void Style(RowVisual row)
        {
            var data = row.Data;
            bool done = data.state == ObjectiveState.Done;
            bool warned = data.state == ObjectiveState.Warned || (data.warnings > 0 && !done);
            bool lesson = data.state == ObjectiveState.Failed;

            row.Icon.sprite = lesson ? lessonSprite : warned ? warnSprite : done ? tickSprite
                : data.kind == ObjectiveKind.Bonus ? starSprite : data.kind == ObjectiveKind.Keep ? dotSprite : boxSprite;
            if (done && data.kind == ObjectiveKind.Bonus) row.Icon.sprite = starSprite;

            string tag = "";
            Color tagColor = Gold;
            if (Time.unscaledTime < row.NewUntil && !done) tag = "YENİ";
            else if (lesson) { tag = "DERS"; tagColor = Red; }
            else if (data.warnings > 0) { tag = (data.warnings > 9 ? "9+" : data.warnings.ToString()) + " UYARI"; tagColor = Amber; }
            else if (data.kind == ObjectiveKind.Bonus) tag = "BONUS";
            row.Tag.text = tag;
            row.Tag.color = tagColor;

            float pipWidth = BuildPips(row);
            float tagWidth = tag.Length > 0 ? Mathf.Ceil(row.Tag.preferredWidth) + 4f : 0f;
            float reserve = Mathf.Max(pipWidth > 0f ? pipWidth + 4f : 0f, tagWidth);
            if (pipWidth > 0f && tagWidth > 0f) reserve = pipWidth + tagWidth + 4f;
            float labelWidth = RowW - 15f - 3f - reserve;
            row.Label.text = data.text;
            row.Label.color = done ? Muted : Cream;
            row.Label.rectTransform.sizeDelta = new Vector2(labelWidth, 40f);
            float textH = Mathf.Ceil(row.Label.preferredHeight);
            row.Height = Mathf.Max(RowH, textH + 3f);
            row.Root.sizeDelta = new Vector2(RowW, row.Height);
            row.Label.rectTransform.sizeDelta = new Vector2(labelWidth, row.Height);
            row.Tag.rectTransform.sizeDelta = new Vector2(60f, row.Height);
            row.Tag.rectTransform.anchoredPosition = new Vector2(RowW - 63f - (pipWidth > 0f && tagWidth > 0f ? pipWidth + 4f : 0f), 0f);
            row.Icon.rectTransform.anchoredPosition = new Vector2(7.5f, -Mathf.Round(row.Height / 2f) + 0.5f);
            row.PipRoot.anchoredPosition = new Vector2(RowW - 3f - pipWidth, -Mathf.Round(row.Height / 2f - 2f));
            row.Strike.rectTransform.anchoredPosition = new Vector2(15f, -Mathf.Round(row.Height / 2f));
            bool struck = done && !(data.kind == ObjectiveKind.Counter && data.count < data.max);
            if (!row.Finishing) row.Strike.rectTransform.sizeDelta = new Vector2(struck ? StrikeWidth(row) : 0f, 1f);
        }

        private float StrikeWidth(RowVisual row) =>
            Mathf.Min(Mathf.Ceil(row.Label.preferredWidth), row.Label.rectTransform.sizeDelta.x);

        // Sayaç kutucukları: 4×4, 1 boşluk; hedefin ötesindekiler daha soluk. Genişliği döner (sayaç değilse 0).
        private float BuildPips(RowVisual row)
        {
            var data = row.Data;
            int n = data.kind == ObjectiveKind.Counter ? Mathf.Clamp(data.max, 0, 12) : 0;
            while (row.Pips.Count < n)
                row.Pips.Add(Box(Rect("Pip" + row.Pips.Count, row.PipRoot, new Vector2(row.Pips.Count * 5f, 0f), new Vector2(4f, 4f)), PipDim));
            for (int i = 0; i < row.Pips.Count; i++)
            {
                var pip = row.Pips[i];
                pip.gameObject.SetActive(i < n);
                if (i >= n) continue;
                pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                pip.rectTransform.anchoredPosition = new Vector2(i * 5f + 2f, -2f);
                int state = data.pips != null && i < data.pips.Length ? data.pips[i] : i < data.count ? ObjectivePip.Filled : ObjectivePip.Empty;
                pip.color = state == ObjectivePip.Good ? Green : state == ObjectivePip.Bad ? Red : state == ObjectivePip.Dropped ? Grey
                    : state == ObjectivePip.Filled ? (data.state == ObjectiveState.Done ? Green : Amber)
                    : i < Mathf.Max(1, data.target) ? PipDim : PipExtra;
            }
            row.PipRoot.sizeDelta = new Vector2(n * 5f - 1f, 4f);
            return n > 0 ? n * 5f - 1f : 0f;
        }

        // Panelin ilk bitmemiş satırı vurgulu (etkin görev).
        private void UpdateHighlight()
        {
            bool marked = false;
            foreach (var row in visible)
            {
                bool active = !marked && !row.Finishing && row.Data.state != ObjectiveState.Done && row.Data.state != ObjectiveState.Failed
                              && row.Data.kind != ObjectiveKind.Bonus && row.Data.kind != ObjectiveKind.Keep;
                if (active) marked = true;
                if (!row.Finishing) row.Bg.color = active ? RowHi : new Color(0f, 0f, 0f, 0f);
            }
        }

        private void Remove(RowVisual row)
        {
            if (row.Animation != null) StopCoroutine(row.Animation);
            visible.Remove(row);
            if (row.Root != null) Destroy(row.Root.gameObject);
        }

        // ------------------------------------------------------------------ yerleşim (her kare)

        private void LateUpdate()
        {
            if (canvasRoot == null) return;
            float dt = Time.unscaledDeltaTime;
            visibility.alpha = Mathf.MoveTowards(visibility.alpha, wantVisible ? 1f : 0f, dt / 0.15f);

            float y = HeaderH;
            foreach (var row in visible)
            {
                if (float.IsNaN(row.Y)) row.Y = y;
                row.Y = Mathf.Abs(row.Y - y) < 0.5f ? y : Mathf.Lerp(row.Y, y, 1f - Mathf.Exp(-dt * 14f));
                if (row.Slide < 0f && !row.Finishing)
                {
                    row.Slide = Mathf.Min(0f, row.Slide + dt * 40f);
                    row.Group.alpha = Mathf.Min(1f, row.Group.alpha + dt / 0.2f);
                }
                else if (!row.Finishing) row.Group.alpha = 1f;
                row.Root.anchoredPosition = new Vector2(Mathf.Round(RowX + row.Slide), -Mathf.Round(row.Y));
                if (row.NewUntil > 0f && Time.unscaledTime >= row.NewUntil && !row.Finishing)
                {
                    row.NewUntil = 0f;
                    Style(row);
                }
                y += row.Height;
            }
            if (bannerRoot.gameObject.activeSelf)
            {
                bannerRoot.anchoredPosition = new Vector2(RowX, -Mathf.Round(y + 1f));
                y += BannerH + 2f;
            }
            float target = y + 3f;
            panelHeight = Mathf.Abs(panelHeight - target) < 0.5f ? target : Mathf.Lerp(panelHeight, target, 1f - Mathf.Exp(-dt * 14f));
            panel.sizeDelta = new Vector2(Width, Mathf.Round(panelHeight));
            if (Time.unscaledTime < pendingGlowUntil || fillAnimation != null) SetFill(fillValue);
        }

        // ------------------------------------------------------------------ animasyonlar

        private IEnumerator AnimateComplete(RowVisual row, int percentGain)
        {
            bool stays = row.Data.kind == ObjectiveKind.Counter && row.Data.count < row.Data.max;
            Text gain = null;
            var iconRect = row.Icon.rectTransform;
            row.Icon.sprite = row.Data.kind == ObjectiveKind.Bonus ? starSprite : tickSprite;
            row.Tag.text = row.Data.kind == ObjectiveKind.Bonus ? "BONUS" : "";
            row.Bg.color = GreenFlash;
            float strikeTo = StrikeWidth(row);
            if (percentGain > 0) pendingGlowUntil = Time.unscaledTime + 0.7f;
            float end = stays ? 0.6f : 1.58f;
            for (float t = 0f; t < end; t += Time.unscaledDeltaTime)
            {
                float punch = Mathf.Clamp01(t / 0.18f);
                iconRect.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(punch * Mathf.PI));
                row.Bg.color = Color.Lerp(GreenFlash, new Color(RowHi.r, RowHi.g, RowHi.b, 0f), Mathf.Clamp01((t - 0.25f) / 0.4f));
                if (!stays) row.Strike.rectTransform.sizeDelta = new Vector2(Mathf.Round(strikeTo * Mathf.Clamp01((t - 0.08f) / 0.22f)), 1f);
                if (t >= 0.3f) row.Label.color = Muted;
                if (percentGain > 0 && t >= 0.15f && gain == null)
                {
                    gain = Label("ProgressGain", panel, new Vector2(Width + 4f, -10f), new Vector2(40f, 13f), Gold, TextAnchor.MiddleLeft);
                    gain.text = "+%" + percentGain;
                    gain.gameObject.AddComponent<Outline>().effectColor = Ink;
                }
                if (gain != null)
                {
                    float rise = Mathf.Clamp01((t - 0.15f) / 0.6f);
                    gain.rectTransform.anchoredPosition = new Vector2(Width + 4f, Mathf.Round(-10f + 8f * rise));
                    gain.color = new Color(Gold.r, Gold.g, Gold.b, rise < 0.6f ? 1f : 1f - (rise - 0.6f) / 0.4f);
                }
                if (t >= 1.4f)
                {
                    float exit = Mathf.Clamp01((t - 1.4f) / 0.18f);
                    row.Slide = -12f * exit;
                    row.Group.alpha = 1f - exit;
                }
                yield return null;
            }
            iconRect.localScale = Vector3.one;
            if (gain != null) Destroy(gain.gameObject);
            row.Animation = null;
            if (stays)
            {
                // Hedefe ulaşan sayaç panelde kalır, saymaya devam eder (ör. 4/7 → 7/7).
                row.Finishing = false;
                row.Bg.color = new Color(0f, 0f, 0f, 0f);
                Style(row);
                UpdateHighlight();
                yield break;
            }
            dismissed.Add(row.Data.id);
            Remove(row);
            Reconcile();
        }

        private IEnumerator AnimateWarn(RowVisual row)
        {
            var iconRect = row.Icon.rectTransform;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                row.Slide = Mathf.Round(Mathf.Sin(t * 70f) * 2f * (1f - t / 0.3f));
                iconRect.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI));
                yield return null;
            }
            row.Slide = 0f;
            iconRect.localScale = Vector3.one;
            row.Animation = null;
        }

        private IEnumerator PipPop(RowVisual row, int from, int to)
        {
            var popped = new List<Image>();
            for (int i = from; i < to && i < row.Pips.Count; i++) popped.Add(row.Pips[i]);
            Text floatText = null;
            if (row.Root != null)
            {
                floatText = Label("CounterGain", panel, new Vector2(Width + 4f, -(row.Y + 0f)), new Vector2(80f, 13f), Amber, TextAnchor.MiddleLeft);
                int delta = to - from;
                floatText.text = "+" + delta + (string.IsNullOrEmpty(row.Data.unit) ? "" : " " + row.Data.unit);
                floatText.gameObject.AddComponent<Outline>().effectColor = Ink;
            }
            float baseY = row.Y;
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.22f);
                foreach (var p in popped) if (p != null) p.rectTransform.localScale = Vector3.one * (1f + 0.8f * Mathf.Sin(k * Mathf.PI));
                if (floatText != null)
                {
                    float rise = Mathf.Clamp01(t / 0.9f);
                    floatText.rectTransform.anchoredPosition = new Vector2(Width + 4f, Mathf.Round(-baseY + 6f * rise));
                    floatText.color = new Color(Amber.r, Amber.g, Amber.b, rise < 0.6f ? 1f : 1f - (rise - 0.6f) / 0.4f);
                }
                yield return null;
            }
            foreach (var p in popped) if (p != null) p.rectTransform.localScale = Vector3.one;
            if (floatText != null) Destroy(floatText.gameObject);
        }

        private IEnumerator AnimateFill(float target)
        {
            float start = fillValue;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                SetFill(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 0.45f))));
                yield return null;
            }
            SetFill(target);
            fillAnimation = null;
        }

        private IEnumerator AnimateBanner()
        {
            var group = bannerRoot.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            // Son görevin tiki ve çizgisi önce görünsün, şerit satırların altında sonra gelsin.
            yield return new WaitForSecondsRealtime(0.35f);
            bannerRoot.gameObject.SetActive(true);
            onGroupCompleted.Invoke();
            for (float t = 0f; t < 2.6f; t += Time.unscaledDeltaTime)
            {
                group.alpha = t < 0.15f ? t / 0.15f : t > 2.3f ? 1f - (t - 2.3f) / 0.3f : 1f;
                yield return null;
            }
            bannerRoot.gameObject.SetActive(false);
            bannerAnimation = null;
        }

        // Bölme dolumu: dolan kısım yeşil; yeni kazanılan (henüz dolmamış) bölmeler kısa süre krem renkte parlar.
        private void SetFill(float value)
        {
            fillValue = value;
            progressLabel.text = "HAZIRLIK %" + Mathf.RoundToInt(value * 100f); // çubukla birlikte sayar
            int n = segmentFills.Count;
            if (n == 0) return;
            bool glow = Time.unscaledTime < pendingGlowUntil;
            for (int i = 0; i < n; i++)
            {
                float segW = segments[i].rectTransform.sizeDelta.x;
                float k = Mathf.Clamp01(value * n - i);
                segmentFills[i].rectTransform.sizeDelta = new Vector2(Mathf.Round(segW * k), BarH);
                bool pending = glow && i < Mathf.RoundToInt(targetProgress * n) && k < 1f;
                segments[i].color = pending ? Cream : BarBack;
            }
        }

        // ------------------------------------------------------------------ küçük yardımcılar

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, Color color, TextAnchor align)
        {
            var rect = Rect(name, parent, position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = FontSize;
            label.color = color;
            label.alignment = align;
            label.supportRichText = false;
            label.lineSpacing = 0.85f;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Box(RectTransform rect, Color color, bool outline = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (outline)
            {
                var border = rect.gameObject.AddComponent<Outline>();
                border.effectColor = Ink;
                border.effectDistance = new Vector2(1f, -1f);
            }
            return image;
        }

        private static Image Icon(RectTransform rect, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Color C(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        // 9×9 piksel ikonlar ('.' saydam). Point filtre: ölçeklenince keskin kalır.
        private static void BuildSprites()
        {
            if (boxSprite != null) return;
            var palette = new Dictionary<char, Color>
            {
                { 'c', Cream }, { 'n', C(0x1C2742) }, { 'G', C(0x0D7A3D) }, { 'g', Green }, { 'A', C(0xA8661A) }, { 'a', Amber },
                { 'k', Ink }, { 'R', C(0xA82418) }, { 'r', Red }, { 'O', C(0xB8862E) }, { 'y', Gold },
            };
            boxSprite = MakeSprite(palette,
                "ccccccccc", "cnnnnnnnc", "cnnnnnnnc", "cnnnnnnnc", "cnnnnnnnc", "cnnnnnnnc", "cnnnnnnnc", "cnnnnnnnc", "ccccccccc");
            dotSprite = MakeSprite(palette,
                "ccccccccc", "cnnnnnnnc", "cnnnnnnnc", "cnncccnnc", "cnncccnnc", "cnncccnnc", "cnnnnnnnc", "cnnnnnnnc", "ccccccccc");
            tickSprite = MakeSprite(palette,
                "GGGGGGGGG", "GgggggggG", "GggggggcG", "GgggggccG", "GcgggccgG", "GccgccggG", "GgcccgggG", "GggcggggG", "GGGGGGGGG");
            warnSprite = MakeSprite(palette,
                "AAAAAAAAA", "AaaakaaaA", "AaaakaaaA", "AaaakaaaA", "AaaakaaaA", "AaaaaaaaA", "AaaakaaaA", "AaaaaaaaA", "AAAAAAAAA");
            lessonSprite = MakeSprite(palette,
                "RRRRRRRRR", "RrrrrrrrR", "RrcrrrcrR", "RrrcrcrrR", "RrrrcrrrR", "RrrcrcrrR", "RrcrrrcrR", "RrrrrrrrR", "RRRRRRRRR");
            starSprite = MakeSprite(palette,
                "....O....", "...OyO...", "OOOOyOOOO", "OyyyyyyyO", ".OyyyyyO.", "..OyyyO..", ".OyyOyyO.", ".OyO.OyO.", ".OO...OO.");
        }

        private static Sprite MakeSprite(Dictionary<char, Color> palette, params string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char ch = x < rows[y].Length ? rows[y][x] : '.';
                    tex.SetPixel(x, h - 1 - y, palette.TryGetValue(ch, out var col) ? col : new Color(0f, 0f, 0f, 0f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
