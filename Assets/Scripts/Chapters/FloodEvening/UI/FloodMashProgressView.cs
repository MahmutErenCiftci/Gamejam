using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// "[E] Art arda bas — Engeli kenara it" ipucu + yatay ilerleme çubuğu (SEL3-30, referans kare 11). Alt ortada koyu panel:
    /// solda amber [E] tuş kapağı, yanında ipucu, altında yeşil dolgulu çubuk. SetProgress artınca panel birkaç piksel
    /// zıplar, tuş kapağı basılır ve dolgu beyaz parlar; dolunca çubuk turkuaza döner. Panel genişliği ekran genişliğine
    /// göre kısılır, böylece 480x270'te de ekran içinde kalır. Arayüz kodla kurulur (Canvas 65).
    /// </summary>
    public class FloodMashProgressView : MashProgressViewBase
    {
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 65;
        [SerializeField] private string keyLabel = "E";
        [Tooltip("Tuş kapağının yanındaki ipucu (Arda'nın JSON metni SetHint ile verilebilir).")]
        [SerializeField] private string hint = "Art arda bas — Engeli kenara it";
        [Tooltip("Panelin ekran altından uzaklığı (640x360 referans pikseli).")]
        [SerializeField] private float bottomMargin = 40f;
        [SerializeField] private float maxWidth = 236f;
        [Tooltip("Dolgunun hedefe yetişme hızı (birim/sn).")]
        [SerializeField] private float fillSpeed = 3f;

        private const float PanelH = 46f, Side = 8f, BarH = 10f, HopPx = 3f, HopTime = 0.16f, FlashTime = 0.2f, KeyDownTime = 0.09f;

        private bool built, visible;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform canvasRect, panel, keycap, bar, fill;
        private Image fillImage, flashImage, keyDepth, panelEdge;
        private Text hintText;
        private float target, shown, fadeK, pulseT = 99f;
        private float lastWidth = -1f;

        public override bool IsVisible => visible;
        /// <summary>Son verilen ilerleme (0..1).</summary>
        public float Progress => target;

        private void Awake() => Build();

        private void OnDisable()
        {
            if (!built) return;
            visible = false;
            fadeK = 0f;
            ApplyFade();
        }

        public override void Show()
        {
            Build();
            if (visible) return;
            visible = true;
            canvas.gameObject.SetActive(true);
            shown = target;
            pulseT = 99f;
            Layout();
        }

        public override void Hide()
        {
            Build();
            visible = false;
        }

        public override void SetProgress(float progress01)
        {
            Build();
            float v = Mathf.Clamp01(progress01);
            bool bump = visible && v > target + 0.0001f;
            target = v;
            if (bump) pulseT = 0f;
            else if (!visible || v < shown) shown = v; // geri alma / gizliyken: animasyonsuz
            ApplyFill();
        }

        /// <summary>İpucu metnini değiştirir (ör. flood_evening.json'dan).</summary>
        public void SetHint(string text)
        {
            Build();
            hint = text ?? "";
            hintText.text = hint;
        }

        private void Update()
        {
            if (!built) return;
            float dt = Time.unscaledDeltaTime;

            float wanted = visible ? 1f : 0f;
            if (!Mathf.Approximately(fadeK, wanted))
            {
                fadeK = Mathf.MoveTowards(fadeK, wanted, dt / 0.15f);
                ApplyFade();
                if (!visible && fadeK <= 0f) canvas.gameObject.SetActive(false);
            }
            if (!visible && fadeK <= 0f) return;

            if (!Mathf.Approximately(canvasRect.rect.width, lastWidth)) Layout();

            if (shown < target) shown = Mathf.Min(target, shown + fillSpeed * dt);
            pulseT += dt;
            ApplyFill();
            ApplyPulse();
        }

        private void ApplyFade()
        {
            group.alpha = FeUi.OutCubic(fadeK);
            group.blocksRaycasts = visible;
            ApplyPulse();
        }

        // Zıplama + giriş kayması (tam piksel), tuş kapağı basılması, dolgu parlaması.
        private void ApplyPulse()
        {
            float hop = pulseT < HopTime ? Mathf.Sin(pulseT / HopTime * Mathf.PI) * HopPx : 0f;
            float slide = (1f - FeUi.OutCubic(fadeK)) * 8f;
            panel.anchoredPosition = new Vector2(0f, Mathf.Round(bottomMargin + hop - slide));

            bool keyDown = pulseT < KeyDownTime;
            keycap.anchoredPosition = new Vector2(Side, -(keyDown ? 8f : 6f));
            keyDepth.enabled = !keyDown;

            float flash = pulseT < FlashTime ? 1f - pulseT / FlashTime : 0f;
            flashImage.color = FeUi.A(Color.white, 0.75f * flash);
            panelEdge.color = Color.Lerp(FeUi.TealDark, FeUi.Teal, flash);
        }

        private void ApplyFill()
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(shown), 1f);
            fillImage.color = target >= 1f ? FeUi.Teal : FeUi.Green;
        }

        // Panel genişliği: en çok maxWidth, dar ekranda canvas genişliği - 2x16.
        private void Layout()
        {
            lastWidth = canvasRect.rect.width;
            float w = lastWidth > 0f ? Mathf.Round(Mathf.Min(maxWidth, lastWidth - 32f)) : maxWidth;
            panel.sizeDelta = new Vector2(w, PanelH);
            ApplyPulse();
        }

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FeUi.CreateCanvas(transform, "FE_MashCanvas", sortingOrder, out group);
            canvasRect = (RectTransform)canvas.transform;
            group.interactable = false;

            panel = FeUi.Rect("MashPanel", canvas.transform);
            FeUi.Place(panel, 0.5f, 0f, new Vector2(0f, bottomMargin), new Vector2(maxWidth, PanelH));
            panelEdge = FeUi.Framed("Frame", panel, FeUi.TealDark, FeUi.A(FeUi.Panel, 0.94f), true);
            FeUi.Stretch(panelEdge.rectTransform);
            panelEdge.gameObject.AddComponent<ClickRelay>().Owner = this;
            var accent = FeUi.Img("Accent", panel, FeUi.Amber);
            var art = accent.rectTransform;
            art.anchorMin = new Vector2(0f, 1f); art.anchorMax = new Vector2(1f, 1f); art.pivot = new Vector2(0.5f, 1f);
            art.offsetMin = new Vector2(1f, -3f); art.offsetMax = new Vector2(-1f, -1f);

            // [E] tuş kapağı: amber yüz + altta 2 px koyu "derinlik"; basılınca 2 px iner, derinlik kaybolur.
            keycap = FeUi.Rect("Keycap", panel);
            FeUi.TL(keycap, Side, 6f, 18f, 18f);
            keyDepth = FeUi.Img("Depth", keycap, FeUi.AmberDark);
            FeUi.TL(keyDepth.rectTransform, 0f, 2f, 18f, 18f);
            var face = FeUi.Framed("Face", keycap, FeUi.Ink, FeUi.Amber);
            FeUi.TL(face.rectTransform, 0f, 0f, 18f, 18f);
            var key = FeUi.Txt("Key", face.transform, font, 12, FeUi.Ink, TextAnchor.MiddleCenter, false);
            key.text = keyLabel;

            hintText = FeUi.Txt("Hint", panel, font, 12, FeUi.Cream, TextAnchor.MiddleLeft);
            var hrt = hintText.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f); hrt.pivot = new Vector2(0f, 1f);
            hrt.offsetMin = new Vector2(Side + 24f, -24f); hrt.offsetMax = new Vector2(-Side, -5f);
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            FeUi.BestFit(hintText, 9);
            hintText.text = hint;

            // Çubuk: 2 px koyu kenar (480x270'te de görünür), iz, dolgu, beyaz parlama katmanı.
            bar = FeUi.Rect("Bar", panel);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(1f, 0f); bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = new Vector2(Side, 8f); bar.offsetMax = new Vector2(-Side, 8f + BarH);
            var barFrame = FeUi.Framed("Frame", bar, FeUi.Ink, FeUi.Hex(0x0B1626), false, 2f);
            FeUi.Stretch(barFrame.rectTransform);
            fillImage = FeUi.Img("Fill", barFrame.transform.Find("Fill"), FeUi.Green);
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            var shine = FeUi.Img("Shine", fill, FeUi.A(Color.white, 0.28f));
            var srt = shine.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.offsetMin = new Vector2(0f, -2f); srt.offsetMax = Vector2.zero;
            flashImage = FeUi.Img("Flash", fill, FeUi.A(Color.white, 0f));
            FeUi.Stretch(flashImage.rectTransform);

            visible = false;
            fadeK = 0f;
            ApplyFill();
            ApplyFade();
            canvas.gameObject.SetActive(false);
        }

        /// <summary>Panele sol tıklama → Clicked (E eşdeğeri, modül isterse kullanır).</summary>
        private sealed class ClickRelay : MonoBehaviour, IPointerDownHandler
        {
            public FloodMashProgressView Owner;

            public void OnPointerDown(PointerEventData e)
            {
                if (e.button == PointerEventData.InputButton.Left && Owner != null && Owner.visible) Owner.RaiseClicked();
            }
        }
    }
}
