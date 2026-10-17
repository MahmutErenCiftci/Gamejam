using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Sel sonuç ekranı (F09, OYUN_AKISI §8) — GEÇİCİ görünüm (Mahmut). ResultViewBase sözleşmesini uygular; Berk kendi
    /// panelini yapınca ChapterController'ın resultView alanına onu bağlamak yeterli. Arayüz kodla kurulur (640x360
    /// referans), deprem raporunun paletini ve UI_EQ_* sprite'larını kullanır; sprite yoksa düz renklerle çizilir.
    /// Bölüme özel ayrıntılar (kararlar, eşyalar, suya yaklaşma uyarıları, yanlışların kısa açıklaması) Show'dan önce
    /// SetDetails ile verilir; verilmezse sadece sözleşmedeki başlık/öğrenilenler/yanlışlar gösterilir.
    /// Tuşlar: ← / → (A / D) seç, Enter / Space onayla, 1 = Tekrar Dene, 2 = Menüye Dön; fareyle tıklama.
    /// </summary>
    public class FloodResultView : ResultViewBase
    {
        [Serializable]
        public class Mistake
        {
            public string title;   // "Sel uyarısını ciddiye almamak"
            public string why;     // seçeneğin geri bildirimi
        }

        public class Details
        {
            public int decisionCount;
            public int wrongDecisionCount;
            public bool raincoat;
            public bool umbrella;
            public int waterWarnings;
            /// <summary>Görev paneli özeti (ObjectiveTracker.GetSummary); objectivesTotal 0 ise satır gizlenir.</summary>
            public int objectivesDone, objectivesTotal, bonusDone, bonusTotal;
            public readonly List<Mistake> mistakes = new List<Mistake>();
        }

        [Header("Görünüm")]
        [Tooltip("Assets/Art/Dialogue/Fonts/Nunito-Black.ttf (boşsa LegacyRuntime)")]
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 300;
        [SerializeField] private string header = "AFET FARKINDALIK RAPORU · SEL";
        [SerializeField] private Sprite dim;          // UI_EQ_Dim
        [SerializeField] private Sprite panel;        // UI_EQ_ReportPanel (9-slice, başlık bandı 33 px)
        [SerializeField] private Sprite check;        // UI_EQ_Check
        [SerializeField] private Sprite cross;        // UI_EQ_Cross
        [SerializeField] private Sprite infoIcon;     // UI_EQ_Icon_Info
        [SerializeField] private Sprite buttonGreen;  // UI_EQ_ButtonStart
        [SerializeField] private Sprite buttonGreenHover;
        [SerializeField] private Sprite buttonNavy;   // UI_EQ_Button
        [SerializeField] private Sprite buttonNavyHover;
        [Tooltip("Gösterildikten sonra tuşların dikkate alınmadığı süre (diyaloğu kapatan tuş butona basmasın)")]
        [SerializeField] private float inputDelay = 0.6f;

        private static readonly Color Ink = Hex(0x14141F), Navy = Hex(0x263559), NavyDark = Hex(0x1C2742), Cream = Hex(0xF4EBD8),
            CreamShade = Hex(0xDDCFB2), Red = Hex(0xE8392B), RedDark = Hex(0xA82418), Amber = Hex(0xF2A33A), Green = Hex(0x18A957),
            GreenDark = Hex(0x0D7A3D), Muted = Hex(0x5A6080), GreenTint = Hex(0xE3F1DF), RedTint = Hex(0xF8E0DA);

        private const float RefPpu = 100f;
        private const float PanelW = 560f, PanelH = 334f;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panelRect;
        private readonly Image[] buttons = new Image[2];
        private readonly Sprite[][] buttonSprites = new Sprite[2][];
        private int focus;
        private float shownAt;
        private bool visible;
        private Details details;
        private readonly List<GameObject> dynamicRows = new List<GameObject>();
        private Text titleText;
        private Text[] statTexts = new Text[3];
        private Image[] statIcons = new Image[3];
        private RectTransform greenBox, redBox;
        private RectTransform objectivesRow;
        private Text objectivesText;
        private Image objectivesIcon;
        private Text noteText;

        public bool IsShown => visible;

        /// <summary>Bölüme özel ayrıntılar; Show'dan önce çağrılır.</summary>
        public void SetDetails(Details value) => details = value;

        private void Awake() => Build();

        public override void Show(string title, IReadOnlyList<string> learned, IReadOnlyList<string> mistakes, string note)
        {
            Build();
            foreach (var go in dynamicRows) if (go != null) Destroy(go);
            dynamicRows.Clear();

            bool anyMistake = (details != null ? details.mistakes.Count : mistakes?.Count ?? 0) > 0;
            int warnings = details?.waterWarnings ?? 0;
            bool showRed = anyMistake || warnings > 0;

            titleText.text = title;
            titleText.color = anyMistake ? Hex(0xB86E1E) : GreenDark;

            // İstatistik şeridi
            if (details != null)
            {
                int right = Mathf.Max(0, details.decisionCount);
                SetStat(0, details.wrongDecisionCount == 0,
                    details.wrongDecisionCount == 0 ? $"Kararlar: {right}/{right} ilk denemede doğru"
                                                   : $"Kararlar: {details.wrongDecisionCount} yanlış deneme, sonra doğru");
                SetStat(1, details.raincoat || details.umbrella,
                    details.raincoat && details.umbrella ? "Yağmurluk ve şemsiye alındı"
                    : details.raincoat ? "Yağmurluk alındı, şemsiye yok"
                    : details.umbrella ? "Şemsiye alındı, yağmurluk yok" : "Yağmurluk ve şemsiye alınmadı");
                SetStat(2, warnings == 0, warnings == 0 ? "Suya hiç yaklaşmadın" : $"Suya yaklaşma uyarısı: {warnings}");
            }
            for (int i = 0; i < 3; i++) statTexts[i].transform.parent.gameObject.SetActive(details != null);

            // Görev paneli özeti: "GÖREVLER 13/15 · BONUS 2/4"
            bool hasObjectives = details != null && details.objectivesTotal > 0;
            objectivesRow.gameObject.SetActive(hasObjectives);
            if (hasObjectives)
            {
                bool allDone = details.objectivesDone >= details.objectivesTotal;
                objectivesText.text = $"GÖREVLER {details.objectivesDone}/{details.objectivesTotal}" +
                                      (details.bonusTotal > 0 ? $"   ·   BONUS {details.bonusDone}/{details.bonusTotal}" : "");
                // Hepsi bittiyse yeşil tik, değilse bilgi ikonu (sprite yoksa düz renk)
                var icon = allDone ? check : infoIcon;
                objectivesIcon.sprite = icon;
                objectivesIcon.color = icon != null ? Color.white : allDone ? Green : Hex(0x3A8DDE);
            }

            // Kutular: yanlış yoksa yeşil kutu tüm genişliği kaplar
            float top = details != null ? (hasObjectives ? 100f : 84f) : 64f;
            float h = details != null ? (hasObjectives ? 180f : 196f) : 216f;
            TL(greenBox, 16f, top, showRed ? 258f : 528f, h);
            TL(redBox, 286f, top, 258f, h);
            redBox.gameObject.SetActive(showRed);

            float y = 24f;
            float gw = (showRed ? 258f : 528f) - 30f;
            if (learned != null)
                foreach (var item in learned)
                {
                    var row = Row(greenBox, item, 8, Ink, check, Green, 8f, y, gw, out float rh);
                    dynamicRows.Add(row);
                    y += Mathf.Max(13f, rh + 3f);
                }
            float contentBottom = y;

            if (showRed)
            {
                y = 24f;
                var list = new List<Mistake>();
                if (details != null) list.AddRange(details.mistakes);
                else if (mistakes != null) foreach (var m in mistakes) list.Add(new Mistake { title = m });
                if (warnings > 0)
                    list.Add(new Mistake
                    {
                        title = $"Suya yaklaştın ({warnings} kez)",
                        why = "Derinliği ve akıntısı belli olmayan suya, birikintiye ve rögara yaklaşma."
                    });
                foreach (var m in list)
                {
                    var row = Row(redBox, m.title, 8, RedDark, cross, Red, 8f, y, 228f, out float rh);
                    dynamicRows.Add(row);
                    y += rh + 1f;
                    if (!string.IsNullOrEmpty(m.why))
                    {
                        var why = Txt("Why", redBox, StripTags(m.why), 7, Muted, TextAnchor.UpperLeft);
                        TL(why.rectTransform, 22f, y, 228f, 30f);
                        float wh = why.preferredHeight;
                        TL(why.rectTransform, 22f, y, 228f, wh + 1f);
                        dynamicRows.Add(why.gameObject);
                        y += wh + 5f;
                    }
                }
                contentBottom = Mathf.Max(contentBottom, y);
            }

            // Kutular içeriğe göre kısalır (boş alan kalmasın), en çok 196 birim
            float boxH = Mathf.Clamp(contentBottom + 6f, 70f, h);
            TL(greenBox, 16f, top, showRed ? 258f : 528f, boxH);
            TL(redBox, 286f, top, 258f, boxH);

            noteText.text = note;
            focus = 0;
            UpdateFocus();
            canvas.gameObject.SetActive(true);
            visible = true;
            shownAt = Time.unscaledTime;
            StopAllCoroutines();
            StartCoroutine(Appear());
        }

        public override void Hide()
        {
            visible = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!visible || Time.unscaledTime - shownAt < inputDelay) return;
            if (GameInput.LeftPressed) { focus = 0; UpdateFocus(); }
            if (GameInput.RightPressed) { focus = 1; UpdateFocus(); }
            int key = GameInput.ChoiceKeyPressed;
            if (key == 1) Click(0);
            else if (key == 2) Click(1);
            else if (GameInput.ConfirmPressed) Click(focus);
        }

        private void Click(int index)
        {
            if (!visible || Time.unscaledTime - shownAt < inputDelay) return;
            visible = false;
            Debug.Log($"[FloodResultView] {(index == 0 ? "Tekrar Dene" : "Menüye Dön")}");
            if (index == 0) RaiseRetryClicked();
            else RaiseMenuClicked();
        }

        private IEnumerator Appear()
        {
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.35f;
                group.alpha = k;
                panelRect.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Lerp(-16f, 0f, 1f - (1f - k) * (1f - k))));
                yield return null;
            }
            group.alpha = 1f;
            panelRect.anchoredPosition = Vector2.zero;
        }

        // ------------------------------------------------------------ kurulum

        private bool built;

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var cgo = new GameObject("FloodResultCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = RefPpu;
            group = cgo.GetComponent<CanvasGroup>();

            var dimImg = Img("Dim", cgo.transform, dim, new Color(0.04f, 0.05f, 0.09f, 0.72f));
            if (dim != null) dimImg.color = Color.white;
            Stretch(dimImg.rectTransform);
            dimImg.raycastTarget = true;

            var p = Img("Panel", cgo.transform, panel, Cream);
            panelRect = p.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelW, PanelH);
            if (panel == null)
            {
                Outline(p, Ink);
                var band = Img("HeaderBand", panelRect, null, Navy);
                TL(band.rectTransform, 0f, 0f, PanelW, 29f);
                var line = Img("AmberLine", panelRect, null, Amber);
                TL(line.rectTransform, 0f, 29f, PanelW, 2f);
            }

            var head = Txt("Header", panelRect, header, 12, Cream, TextAnchor.MiddleCenter, Ink);
            TL(head.rectTransform, 10f, 3f, PanelW - 20f, 24f);

            titleText = Txt("Title", panelRect, "", 14, GreenDark, TextAnchor.MiddleCenter);
            TL(titleText.rectTransform, 16f, 37f, PanelW - 32f, 20f);

            for (int i = 0; i < 3; i++)
            {
                var chip = new GameObject("Stat" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                chip.SetParent(panelRect, false);
                TL(chip, 16f + i * 178f, 60f, 172f, 18f);
                var bg = Img("Bg", chip, null, Hex(0xEADFC6));
                Stretch(bg.rectTransform);
                statIcons[i] = Img("Icon", chip, check, Green);
                TL(statIcons[i].rectTransform, 3f, 3f, 12f, 12f);
                statTexts[i] = Txt("Text", chip, "", 7, Navy, TextAnchor.MiddleLeft);
                TL(statTexts[i].rectTransform, 18f, 0f, 152f, 18f);
            }

            objectivesRow = new GameObject("Objectives", typeof(RectTransform)).GetComponent<RectTransform>();
            objectivesRow.SetParent(panelRect, false);
            TL(objectivesRow, 16f, 81f, PanelW - 32f, 16f);
            var objBg = Img("Bg", objectivesRow, null, Hex(0xF6E3B4));
            Stretch(objBg.rectTransform);
            objectivesIcon = Img("Icon", objectivesRow, check, Green);
            TL(objectivesIcon.rectTransform, 4f, 2f, 12f, 12f);
            objectivesText = Txt("Text", objectivesRow, "", 9, Navy, TextAnchor.MiddleLeft);
            TL(objectivesText.rectTransform, 22f, 0f, PanelW - 60f, 16f);
            objectivesRow.gameObject.SetActive(false);

            greenBox = Box("Learned", "ÖĞRENİLENLER", GreenTint, GreenDark);
            redBox = Box("Mistakes", "YANLIŞ DAVRANIŞLARIN", RedTint, RedDark);

            var sep = Img("Separator", panelRect, null, CreamShade);
            TL(sep.rectTransform, 16f, 284f, PanelW - 32f, 1f);
            var info = Img("NoteIcon", panelRect, infoIcon, Hex(0x3A8DDE));
            TL(info.rectTransform, 16f, 287f, 11f, 11f);
            noteText = Txt("Note", panelRect, "", 7, Muted, TextAnchor.MiddleLeft);
            TL(noteText.rectTransform, 31f, 286f, PanelW - 50f, 13f);

            buttonSprites[0] = new[] { buttonGreen, buttonGreenHover };
            buttonSprites[1] = new[] { buttonNavy, buttonNavyHover };
            buttons[0] = MakeButton(0, "Tekrar Dene  [1]", PanelW / 2f - 150f, Green);
            buttons[1] = MakeButton(1, "Menüye Dön  [2]", PanelW / 2f + 10f, Navy);

            group.alpha = 0f;
            cgo.SetActive(false);
        }

        private RectTransform Box(string name, string caption, Color tint, Color accent)
        {
            var bg = Img(name, panelRect, null, tint);
            Outline(bg, accent);
            var bar = Img("Bar", bg.rectTransform, null, accent);
            TL(bar.rectTransform, 0f, 0f, 600f, 17f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 17f);
            var cap = Txt("Caption", bar.rectTransform, caption, 8, Color.white, TextAnchor.MiddleLeft, Ink);
            Stretch(cap.rectTransform);
            cap.rectTransform.offsetMin = new Vector2(8f, 0f);
            return bg.rectTransform;
        }

        private Image MakeButton(int index, string label, float x, Color fallback)
        {
            var img = Img("Button" + index, panelRect, buttonSprites[index][0], fallback);
            TL(img.rectTransform, x, 302f, 140f, 23f);
            if (buttonSprites[index][0] == null) Outline(img, Ink);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            int i = index;
            btn.onClick.AddListener(() => { focus = i; Click(i); });
            var t = Txt("Label", img.rectTransform, label, 9, Color.white, TextAnchor.MiddleCenter, Ink);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(0f, 2f);
            return img;
        }

        private void UpdateFocus()
        {
            for (int i = 0; i < 2; i++)
            {
                if (buttons[i] == null) continue;
                var s = buttonSprites[i];
                bool on = i == focus;
                if (s[0] != null) buttons[i].sprite = on && s[1] != null ? s[1] : s[0];
                buttons[i].rectTransform.localScale = on ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
                var o = buttons[i].GetComponent<UnityEngine.UI.Outline>();
                if (o == null) o = buttons[i].gameObject.AddComponent<UnityEngine.UI.Outline>();
                o.effectColor = on ? Hex(0xFFD27E) : new Color(0f, 0f, 0f, 0f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        private void SetStat(int i, bool good, string text)
        {
            statTexts[i].text = text;
            statIcons[i].sprite = good ? check : cross;
            statIcons[i].color = (good ? check : cross) != null ? Color.white : (good ? Green : Red);
        }

        private GameObject Row(RectTransform parent, string text, int size, Color color, Sprite icon, Color iconFallback,
            float x, float y, float width, out float height)
        {
            var root = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            var ic = Img("Icon", root, icon, iconFallback);
            TL(ic.rectTransform, 0f, 1f, 10f, 10f);
            var t = Txt("Text", root, StripTags(text), size, color, TextAnchor.UpperLeft);
            TL(t.rectTransform, 14f, 0f, width - 14f, 40f);
            height = Mathf.Max(11f, t.preferredHeight);
            TL(t.rectTransform, 14f, 0f, width - 14f, height + 1f);
            TL(root, x, y, width, height);
            return root.gameObject;
        }

        // ------------------------------------------------------------ küçük yardımcılar

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        private static string StripTags(string s) => string.IsNullOrEmpty(s) ? s : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

        private Image Img(string name, Transform parent, Sprite sprite, Color fallback)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                if (sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = RefPpu / Mathf.Max(1f, sprite.pixelsPerUnit);
            }
            else img.color = fallback;
            return img;
        }

        private Text Txt(string name, Transform parent, string text, int size, Color color, TextAnchor align, Color? shadow = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 0.95f;
            if (shadow.HasValue)
            {
                var s = go.AddComponent<Shadow>();
                s.effectColor = shadow.Value;
                s.effectDistance = new Vector2(1f, -1f);
            }
            return t;
        }

        private static void Outline(Image img, Color c)
        {
            var o = img.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(1f, -1f);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // Üst-sol köşeye göre yerleştir (y aşağı doğru).
        private static void TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
