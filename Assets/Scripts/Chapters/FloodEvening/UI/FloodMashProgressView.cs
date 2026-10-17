using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// "[E] Art arda bas — Engeli kenara it" ipucu + yatay ilerleme çubuğu (SEL3-30, referans kare 11). Alt ortada deprem
    /// görev paneli çerçevesi (UI_EQ_QuestPanel), solda piksel [E] tuş kapağı, yanında ipucu, altında deprem tutma çubuğu
    /// (UI_EQ_HoldBar_Back/Fill). Canlılık: basılmadığında tuş kapağı "bas" der gibi iner kalkar ve çubukta ışık gezer;
    /// her artışta kapak basılır, panel zıplar ve sarsılır, dolgu parlar, ucundan piksel kıvılcımlar saçılır; dolunca
    /// altın patlama. Panel genişliği ekran genişliğine göre kısılır. Arayüz kodla kurulur (Canvas 65).
    /// </summary>
    public class FloodMashProgressView : MashProgressViewBase
    {
        [Header("Yazı")]
        [Tooltip("Assets/Fonts/PixelifySans/PixelifySans-Regular.ttf (Bold Unity'de delikli çiziliyor).")]
        [SerializeField] private Font font;
        [SerializeField] private string title = "Art arda bas";
        [SerializeField] private string subtitle = "Engeli kenara it";

        [Header("Sprite'lar (Assets/Art/UI/Earthquake)")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite barBackSprite;
        [SerializeField] private Sprite barFillSprite;

        [Header("Yerleşim (640x360 referans pikseli)")]
        [SerializeField] private int sortingOrder = 65;
        [SerializeField] private float bottomMargin = 38f;
        [SerializeField] private float maxWidth = 200f;
        [Tooltip("Dolgunun hedefe yetişme hızı (birim/sn).")]
        [SerializeField] private float fillSpeed = 4f;

        private const float PanelH = 46f, Side = 8f, BarH = 12f, BarBottom = 6f;

        private bool built, visible;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform canvasRect, panel, barRect, fillRect, flashRect, glintRect;
        private Image keycap, fillImage, flashImage, glintImage;
        private Text titleText, subtitleText;
        private FePixelBurst burst;
        private float target, shown, fadeK, lastWidth = -1f;
        private float sincePress = 99f, flashT = 99f, glintT, idleT, fullT = -1f;
        private float hopAmp;
        private bool wasFull;

        public override bool IsVisible => visible;
        /// <summary>Son verilen ilerleme (0..1).</summary>
        public float Progress => target;

        private void Awake() => Build();

        private void OnDisable()
        {
            if (!built || burst == null) return; // editörde oyun açıkken derleme sonrası referanslar boşalabilir
            visible = false;
            fadeK = 0f;
            burst.Clear();
            Apply();
            canvas.gameObject.SetActive(false);
        }

        public override void Show()
        {
            Build();
            if (visible) return;
            visible = true;
            canvas.gameObject.SetActive(true);
            shown = target;
            wasFull = target >= 1f;
            sincePress = flashT = 99f;
            idleT = 0f;
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
            if (bump) OnPress();
            else if (!visible || v < shown) shown = v; // geri alma / gizliyken: animasyonsuz
            if (v < 1f) { wasFull = false; fullT = -1f; }
            ApplyFill();
        }

        /// <summary>İpucunu değiştirir. "Başlık — alt satır" verilirse iki satıra bölünür.</summary>
        public void SetHint(string text)
        {
            Build();
            text = (text ?? "").Trim();
            if (text.StartsWith("[E]")) text = text.Substring(3).Trim();
            int dash = text.IndexOf('—');
            title = dash >= 0 ? text.Substring(0, dash).Trim() : text;
            subtitle = dash >= 0 ? text.Substring(dash + 1).Trim() : "";
            titleText.SetPixelText(title);
            subtitleText.SetPixelText(subtitle);
        }

        private void OnPress()
        {
            sincePress = 0f;
            flashT = 0f;
            hopAmp = 2f;
            if (burst == null) return;
            float barW = barRect.rect.width - 2f;
            var tip = TipPosition(barW * target);
            burst.Emit(tip, 6, FePixel.Spark, new[] { FePixel.Gold, FePixel.GreenLight, Color.white }, Vector2.up, 75f, 80f, 150f, 0.5f);
            burst.Emit(tip, 2, FePixel.Star, new[] { FePixel.Gold, Color.white }, Vector2.up, 40f, 60f, 110f, 0.5f);
            if (target >= 1f && !wasFull)
            {
                wasFull = true;
                fullT = 0f;
                hopAmp = 4f;
                burst.Emit(tip, 10, FePixel.Spark, new[] { FePixel.Gold, FePixel.Amber, Color.white }, Vector2.up, 85f, 90f, 170f, 0.55f);
                burst.Emit(new Vector2(0f, BarBottom + BarH * 0.5f - PanelH * 0.5f), 6, FePixel.Star, new[] { FePixel.Gold, Color.white }, Vector2.up, 80f, 70f, 140f, 0.6f);
            }
        }

        // Dolgunun ucunun panel merkezine göre konumu.
        private Vector2 TipPosition(float fillW) =>
            new Vector2(-panel.rect.width * 0.5f + Side + 1f + fillW, -PanelH * 0.5f + BarBottom + BarH * 0.5f);

        private void Update()
        {
            if (!built || burst == null) return;
            float dt = Time.unscaledDeltaTime;

            float wanted = visible ? 1f : 0f;
            if (!Mathf.Approximately(fadeK, wanted))
                fadeK = Mathf.MoveTowards(fadeK, wanted, dt / (visible ? 0.3f : 0.15f));
            if (!visible && fadeK <= 0f)
            {
                burst.Clear();
                canvas.gameObject.SetActive(false);
                return;
            }

            if (!Mathf.Approximately(canvasRect.rect.width, lastWidth)) Layout();
            if (shown < target) shown = Mathf.Min(target, shown + fillSpeed * dt);
            sincePress += dt;
            flashT += dt;
            glintT += dt;
            idleT += dt;
            if (fullT >= 0f) fullT += dt;
            burst.Tick(dt);
            ApplyFill();
            Apply();
        }

        private void Apply()
        {
            group.alpha = Mathf.Clamp01(fadeK * 1.5f);

            // Giriş: aşağıdan taşarak yükselme; basış: zıplama + yatay sarsıntı.
            float rise = visible ? (1f - FePixel.OutBack(fadeK)) * 14f : (1f - fadeK) * 8f;
            float hop = sincePress < 0.16f ? Mathf.Sin(sincePress / 0.16f * Mathf.PI) * hopAmp : 0f;
            float shake = FePixel.Shake(sincePress, 0.12f, 1f);
            panel.anchoredPosition = new Vector2(shake, Mathf.Round(bottomMargin - rise + hop));

            // Tuş kapağı: basışta 0,09 sn basılı; boştayken (0,9 sn basılmadıysa) her 0,5 sn'de kısa "bas" ipucu.
            bool down = sincePress < 0.09f || (sincePress > 0.9f && target < 1f && Mathf.Repeat(idleT, 0.5f) < 0.12f);
            keycap.sprite = down ? FePixel.KeyDown : FePixel.KeyUp;

            // Başlık boştayken hafifçe Gold ↔ Amber nabız atar.
            titleText.color = target >= 1f ? Color.white : Color.Lerp(FePixel.Gold, FePixel.Amber, 0.5f + 0.5f * Mathf.Sin(idleT * 6f));

            // Parlama: basışta beyaz, doluyken altın yanıp söner.
            float flash = flashT < 0.15f ? 1f - flashT / 0.15f : 0f;
            if (fullT >= 0f) flash = Mathf.Max(flash, Mathf.Repeat(fullT, 0.3f) < 0.15f ? 0.55f : 0.2f);
            flashImage.color = FeUi.A(fullT >= 0f ? FePixel.Gold : Color.white, 0.8f * flash);
        }

        private void ApplyFill()
        {
            if (fillRect == null) return;
            float barW = barRect.rect.width - 2f;
            float w = Mathf.Round(barW * Mathf.Clamp01(shown));
            bool any = w >= 1f;
            fillImage.enabled = any;
            flashImage.enabled = any;
            w = any ? Mathf.Max(4f, w) : 0f; // dolgu sprite'ının kenarları 2+2 px
            fillRect.sizeDelta = new Vector2(w, fillRect.sizeDelta.y);
            flashRect.sizeDelta = new Vector2(w, flashRect.sizeDelta.y);

            // Gezen ışık: dolgu üstünde 2 px'lik sütun, 1,6 sn'de bir soldan sağa.
            float k = Mathf.Repeat(glintT, 1.6f) / 0.5f;
            glintImage.enabled = any && k < 1f;
            glintRect.anchoredPosition = new Vector2(Mathf.Round(k * (w - 2f)), 0f);
        }

        private void Layout()
        {
            lastWidth = canvasRect.rect.width;
            float w = lastWidth > 0f ? Mathf.Round(Mathf.Min(maxWidth, lastWidth - 32f)) : maxWidth;
            panel.sizeDelta = new Vector2(w, PanelH);
            ApplyFill();
        }

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FePixel.CreateCanvas(transform, "FE_MashCanvas", sortingOrder, out group);
            canvasRect = (RectTransform)canvas.transform;
            group.interactable = false;

            panel = FeUi.Rect("MashPanel", canvas.transform);
            FeUi.Place(panel, 0.5f, 0f, new Vector2(0f, bottomMargin), new Vector2(maxWidth, PanelH));
            var frame = panelSprite != null
                ? FePixel.Sliced("Frame", panel, panelSprite)
                : FeUi.Framed("Frame", panel, FePixel.Ink, FePixel.Navy);
            FeUi.Stretch(frame.rectTransform);
            frame.raycastTarget = true;
            frame.gameObject.AddComponent<ClickRelay>().Owner = this;

            keycap = FePixel.Icon("Keycap", panel, FePixel.KeyUp);
            FeUi.TL(keycap.rectTransform, 9f, 7f, 15f, 16f);

            titleText = FePixel.Txt("Title", panel, font, 10, FePixel.Gold, TextAnchor.MiddleLeft, FePixel.Ink);
            Row(titleText.rectTransform, 30f, 5f, 13f);
            titleText.SetPixelText(title);
            subtitleText = FePixel.Txt("Subtitle", panel, font, 8, FePixel.Cream, TextAnchor.MiddleLeft, FePixel.Ink);
            Row(subtitleText.rectTransform, 30f, 17f, 10f);
            subtitleText.SetPixelText(subtitle);

            // Çubuk: arka (9 dilim), dolgu soldan büyür, üstünde parlama ve gezen ışık.
            barRect = FeUi.Rect("Bar", panel);
            barRect.anchorMin = new Vector2(0f, 0f); barRect.anchorMax = new Vector2(1f, 0f); barRect.pivot = new Vector2(0.5f, 0f);
            barRect.offsetMin = new Vector2(Side, BarBottom); barRect.offsetMax = new Vector2(-Side, BarBottom + BarH);
            var back = barBackSprite != null ? FePixel.Sliced("Back", barRect, barBackSprite) : FeUi.Framed("Back", barRect, FePixel.Ink, FeUi.Hex(0x1C2742));
            FeUi.Stretch(back.rectTransform);

            fillImage = barFillSprite != null ? FePixel.Sliced("Fill", barRect, barFillSprite) : FeUi.Img("Fill", barRect, FePixel.Green);
            fillRect = LeftRect(fillImage.rectTransform);
            flashImage = FeUi.Img("Flash", barRect, FeUi.A(Color.white, 0f));
            flashRect = LeftRect(flashImage.rectTransform);
            glintImage = FeUi.Img("Glint", fillRect, FeUi.A(Color.white, 0.55f));
            glintRect = glintImage.rectTransform;
            glintRect.anchorMin = new Vector2(0f, 0f); glintRect.anchorMax = new Vector2(0f, 1f); glintRect.pivot = new Vector2(0f, 0.5f);
            glintRect.sizeDelta = new Vector2(2f, -4f);

            burst = new FePixelBurst(panel, "Sparks");

            visible = false;
            fadeK = 0f;
            ApplyFill();
            Apply();
            canvas.gameObject.SetActive(false);
        }

        // Çubuğun içinde (1 px içeride) soldan büyüyen 10 px yüksek alan.
        private static RectTransform LeftRect(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(0f, BarH - 2f);
            return rt;
        }

        // Panel genişliğine uyan metin satırı (sol x, üstten y, yükseklik h; sağda 8 px pay).
        private static void Row(RectTransform rt, float x, float y, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(x, -(y + h)); rt.offsetMax = new Vector2(-Side, -y);
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
