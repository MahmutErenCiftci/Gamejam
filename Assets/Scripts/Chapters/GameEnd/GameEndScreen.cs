using System.Collections;
using AfetUyanisi.Chapters.FloodEvening;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using AfetUyanisi.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.GameEnd
{
    /// <summary>
    /// OYUN SONU ekranı (GameEnd sahnesi; iki afet de bitince GameFlow.ContinueAfter → GameSceneRouter.LoadGameEnd açar).
    /// Arka planda şafak vakti mahalle (solda Cenk'in apartmanı, sağda toplanma alanı tabelası), ortada Cenk ve annesi;
    /// üstte menüdeki "AFET UYANIŞI" logosu + "OYUN TAMAMLANDI", altta iki bölümün kısa özeti (oynanış sırasıyla),
    /// 3 güvenlik hatırlatması ve menüyle aynı görünümlü [Ana Menü] [Tekrar Oyna] butonları (Panel / PanelLit, ▶ işareti,
    /// Pixelify Sans). Siyahtan sıcak bir kararma ile açılır.
    /// Klavye: ←/→ (A/D) seçim, Enter/Space/E onay, 1/2 kısayol, Esc ana menü; fare ile tıklanır.
    /// Ana Menü → GameSceneRouter.ReturnToMenu (oyun sıfırlanır); Tekrar Oyna → GameFlow.BeginNewGame (ilk seçilen afetle).
    /// Arayüzü kodla kurar (640x360 referans); sahnede yalnız kamera + bu bileşen vardır. Görseller boşsa düz renk kullanılır.
    /// </summary>
    public class GameEndScreen : MonoBehaviour
    {
        [Header("Görseller (boşsa düz renk)")]
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite logo;
        [Tooltip("Menünün panel sprite'ı (Assets/Art/Menu/UI/Panel.png, 9-slice)")]
        [SerializeField] private Sprite panel;
        [Tooltip("Menünün seçili panel sprite'ı (Assets/Art/Menu/UI/PanelLit.png, 9-slice)")]
        [SerializeField] private Sprite panelLit;
        [SerializeField] private Sprite cenk;
        [SerializeField] private Sprite mother;
        [Tooltip("Karakter sprite'larının büyütmesi (640x360 referans pikseli başına sprite pikseli; 4/3 → 1080p'de tam 4 piksel)")]
        [SerializeField] private float characterScale = 4f / 3f;
        [Tooltip("Menünün yazı tipi (Pixelify Sans Regular); boşsa akşam sel arayüzünün yazı tipi")]
        [SerializeField] private Font font;
        [Tooltip("Başlıklar için (Pixelify Sans Bold); boşsa font")]
        [SerializeField] private Font boldFont;
        [SerializeField] private int sortingOrder = 50;

        [Header("Geçiş")]
        [SerializeField] private float fadeInSeconds = 1.8f;
        [SerializeField] private float fadeOutSeconds = 0.6f;
        [Tooltip("Ekran açıldıktan sonra tuşların yok sayıldığı süre (önceki ekranı kapatan tuş butona basmasın)")]
        [SerializeField] private float inputDelay = 0.6f;

        [Header("Metinler")]
        [SerializeField] private string title = "OYUN TAMAMLANDI";
        [SerializeField] private string subtitle = "İki afeti de atlattın. Annen de sen de güvendesiniz.";
        [SerializeField] private string chaptersHeader = "BÖLÜMLER";
        [SerializeField] private string quakeTitle = "DEPREM · Toplanma alanına ulaştın";
        [SerializeField] private string quakeText = "Sarsıntıda Çök-Kapan-Tutun yaptın, sonra merdivenle güvenli alana indin.";
        [SerializeField] private string floodTitle = "SEL · Eve güvenle ulaştın";
        [SerializeField] private string floodText = "Suya girmeden, kapalı yolları kullanmadan güvenli yoldan eve vardın.";
        [SerializeField] private string remindersHeader = "UNUTMA";
        [SerializeField] private string[] reminders =
        {
            "Sarsıntıda ÇÖK – KAPAN – TUTUN; sonra asansörü değil merdiveni kullan.",
            "Suyla kaplı ya da kapatılmış yollara asla girme; güvenli yüksek bölgeye yönel.",
            "Söylentiye değil, resmî kurumların duyurularına güven; afet çantan hazır olsun."
        };
        [SerializeField] private string note = "Acil bir durumda 112'yi ara.";
        [SerializeField] private string menuLabel = "Ana Menü";
        [SerializeField] private string replayLabel = "Tekrar Oyna";

        // Menünün renkleri (MenuSceneBuilder: Ink / Warm / Muted) ve panel sprite'larının iç renkleri.
        private static readonly Color Cream = new Color(0.95f, 0.92f, 0.85f);
        private static readonly Color Warm = new Color(1f, 0.74f, 0.4f);
        private static readonly Color Muted = new Color(0.7f, 0.7f, 0.86f);
        private static readonly Color Navy = FeUi.Hex(0x1A1C36);
        private static readonly Color Lavender = FeUi.Hex(0x62689E);
        private static readonly Color Plum = FeUi.Hex(0x28223A);
        private static readonly Color Orange = FeUi.Hex(0xFFB254);
        private static readonly Color Dawn = FeUi.Hex(0xF2A33A);
        private static readonly Color ShadowInk = new Color(0.02f, 0.02f, 0.06f, 0.9f);

        private Canvas canvas;
        private RectTransform titleBlock, bottomPanel, buttonRow, characters;
        private CanvasGroup titleGroup, bottomGroup, buttonGroup, charGroup;
        private Image fade;
        private readonly Image[] buttons = new Image[2];
        private readonly Text[] arrows = new Text[2];
        private readonly Text[] labels = new Text[2];
        private readonly FeButton[] feButtons = new FeButton[2];
        private readonly RectTransform[] figures = new RectTransform[2];
        private readonly float[] figureY = new float[2];
        private int focus;
        private int clicked = -1;
        private bool chosen;

        /// <summary>Oyuncu bir butonu seçti (sahne geçişi başladı).</summary>
        public bool HasChosen => chosen;

        private void Awake()
        {
            Time.timeScale = 1f;
            // Menüyle aynı yazı tipleri: başlık, bölüm başlıkları ve butonlar Press Start 2P (8 px ızgara), metinler Pixel Operator.
            if (GameFonts.Story != null) font = GameFonts.Story;
            if (GameFonts.Menu != null) boldFont = GameFonts.Menu;
            if (font == null) font = FeUi.DefaultFont();
            if (boldFont == null) boldFont = font;
            Build();
        }

        private void Start() => StartCoroutine(Run());

        private void Update()
        {
            // Karakterler hafifçe nefes alır (1 piksellik, birbirinden kaymış sallanma).
            for (int i = 0; i < figures.Length; i++)
            {
                if (figures[i] == null) continue;
                float s = Mathf.Sin(Time.unscaledTime * 2.2f + i * 1.7f);
                var p = figures[i].anchoredPosition;
                p.y = figureY[i] + (s > 0.35f ? 1f : 0f);
                figures[i].anchoredPosition = p;
            }
        }

        private void Build()
        {
            canvas = FeUi.CreateCanvas(transform, "GameEndCanvas", sortingOrder, out _);

            var bg = FeUi.Img("Background", canvas.transform, background != null ? Color.white : FeUi.Panel);
            bg.sprite = background;
            FeUi.Stretch(bg.rectTransform);

            // Ortada Cenk ve annesi (ayakları kaldırımda, güneşin önünde).
            characters = FeUi.Rect("Characters", canvas.transform);
            FeUi.TL(characters, 260f, 116f, 120f, 100f);
            charGroup = characters.gameObject.AddComponent<CanvasGroup>();
            AddCharacter(0, "Mother", mother, 74f);
            AddCharacter(1, "Cenk", cenk, 44f);

            // Üst: menü logosu + başlık + alt başlık
            titleBlock = FeUi.Rect("Title", canvas.transform);
            FeUi.TL(titleBlock, 120f, 6f, 400f, 112f);
            titleGroup = titleBlock.gameObject.AddComponent<CanvasGroup>();
            float textTop = 6f;
            if (logo != null)
            {
                var lg = FeUi.Img("Logo", titleBlock, Color.white);
                lg.sprite = logo;
                lg.preserveAspect = true;
                FeUi.TL(lg.rectTransform, 126.5f, 0f, 147f, 76f);
                textTop = 76f;
            }
            var t = Label("Header", titleBlock, boldFont, 16, Warm, TextAnchor.MiddleCenter);
            FeUi.TL(t.rectTransform, 0f, textTop, 400f, 20f);
            t.text = title;
            var st = Label("Subtitle", titleBlock, font, 10, Cream, TextAnchor.MiddleCenter);
            FeUi.TL(st.rectTransform, 0f, textTop + 19f, 400f, 14f);
            FeUi.BestFit(st, 8);
            st.text = subtitle;

            // Alt panel (menü paneli): bölümler | unutma
            var bp = PanelImage("BottomPanel", canvas.transform, false);
            bp.color = new Color(1f, 1f, 1f, 0.96f);
            bottomPanel = bp.rectTransform;
            FeUi.TL(bottomPanel, 16f, 222f, 608f, 92f);
            bottomGroup = bp.gameObject.AddComponent<CanvasGroup>();

            var ch = Label("ChaptersHeader", bottomPanel, boldFont, 8, Warm, TextAnchor.MiddleLeft);
            FeUi.TL(ch.rectTransform, 12f, 6f, 280f, 14f);
            ch.text = chaptersHeader;
            bool floodFirst = GameFlow.FirstSelectedDisaster == ChapterId.Flood;
            AddChapterRow(0, floodFirst ? ChapterId.Flood : ChapterId.Earthquake, 22f);
            AddChapterRow(1, floodFirst ? ChapterId.Earthquake : ChapterId.Flood, 56f);

            var sep = FeUi.Img("Sep", bottomPanel, FeUi.A(Lavender, 0.6f));
            FeUi.TL(sep.rectTransform, 304f, 8f, 1f, 76f);

            var rh = Label("RemindersHeader", bottomPanel, boldFont, 8, Warm, TextAnchor.MiddleLeft);
            FeUi.TL(rh.rectTransform, 316f, 6f, 280f, 14f);
            rh.text = remindersHeader;
            var rl = Label("Reminders", bottomPanel, font, 9, Cream, TextAnchor.UpperLeft);
            FeUi.TL(rl.rectTransform, 318f, 23f, 280f, 64f);
            FeUi.BestFit(rl, 7);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < reminders.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append("<color=#FFBD66>▶</color> ").Append(reminders[i]);
            }
            rl.text = sb.ToString();

            // Butonlar (menüdeki gibi: Panel → seçiliyken PanelLit + ▶)
            buttonRow = FeUi.Rect("Buttons", canvas.transform);
            FeUi.TL(buttonRow, 170f, 318f, 300f, 24f);
            buttonGroup = buttonRow.gameObject.AddComponent<CanvasGroup>();
            BuildButton(0, menuLabel, 8f);
            BuildButton(1, replayLabel, 156f);

            // Not şeridi: yolun şerit çizgileri yazının arkasında kalmasın.
            var nb = FeUi.Img("NoteBand", canvas.transform, FeUi.A(Navy, 0.9f));
            FeUi.TL(nb.rectTransform, 0f, 344f, 640f, 16f);
            var n = Label("Note", canvas.transform, font, 8, Muted, TextAnchor.MiddleCenter);
            FeUi.TL(n.rectTransform, 40f, 346f, 560f, 12f);
            FeUi.BestFit(n, 7);
            n.text = note;

            fade = FeUi.Img("Fade", canvas.transform, Color.black, true);
            FeUi.Stretch(fade.rectTransform);

            titleGroup.alpha = bottomGroup.alpha = buttonGroup.alpha = charGroup.alpha = 0f;
        }

        private Text Label(string name, Transform parent, Font f, int size, Color color, TextAnchor align)
        {
            var t = FeUi.Txt(name, parent, f, size, color, align, false);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = ShadowInk;
            sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        /// <summary>Menünün 9-slice paneli (sprite yoksa aynı renklerde düz çerçeve).</summary>
        private Image PanelImage(string name, Transform parent, bool lit, bool raycast = false)
        {
            Sprite s = lit ? panelLit : panel;
            if (s == null) return FeUi.Framed(name, parent, lit ? Orange : Lavender, lit ? Plum : Navy, raycast);
            var img = FeUi.Img(name, parent, Color.white, raycast);
            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        private void SetPanelLit(Image img, bool lit)
        {
            if (panel != null && panelLit != null) { img.sprite = lit ? panelLit : panel; return; }
            FeUi.SetFrameColors(img, lit ? Orange : Lavender, lit ? Plum : Navy);
        }

        private void AddCharacter(int index, string name, Sprite sprite, float centerX)
        {
            if (sprite == null) return;
            var img = FeUi.Img(name, characters, Color.white);
            img.sprite = sprite;
            Vector2 size = sprite.rect.size * characterScale;
            // Sprite'ın pivotu ayak hizasıdır: pivot noktası kutunun altına (y = 100) gelir.
            Vector2 pivot = sprite.pivot / sprite.rect.size;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(Mathf.Round(centerX), -100f);
            figures[index] = rt;
            figureY[index] = -100f;
        }

        private void AddChapterRow(int index, ChapterId chapter, float y)
        {
            bool quake = chapter == ChapterId.Earthquake;
            bool done = GameFlow.IsCompleted(chapter);
            var badge = FeUi.Badge("Badge" + index, bottomPanel, font, done ? FeUi.Green : Muted, (index + 1).ToString());
            FeUi.TL(badge.rectTransform, 12f, y + 1f, 14f, 14f);
            var head = Label("ChapterTitle" + index, bottomPanel, font, 10, Warm, TextAnchor.MiddleLeft);
            FeUi.TL(head.rectTransform, 32f, y, 266f, 14f);
            FeUi.BestFit(head, 8);
            head.text = (index + 1) + ". " + (quake ? quakeTitle : floodTitle);
            var body = Label("ChapterText" + index, bottomPanel, font, 9, Cream, TextAnchor.UpperLeft);
            FeUi.TL(body.rectTransform, 32f, y + 14f, 266f, 20f);
            FeUi.BestFit(body, 7);
            body.text = quake ? quakeText : floodText;
        }

        private void BuildButton(int i, string label, float x)
        {
            var img = PanelImage(i == 0 ? "MenuButton" : "ReplayButton", buttonRow, false, true);
            FeUi.TL(img.rectTransform, x, 1f, 136f, 22f);
            var t = Label("Label", img.transform, boldFont, 8, Cream, TextAnchor.MiddleCenter);
            FeUi.Stretch(t.rectTransform, 2f);
            t.rectTransform.offsetMin = new Vector2(14f, 1f);
            t.text = label;
            var arrow = Label("Arrow", img.transform, font, 10, Warm, TextAnchor.MiddleCenter);
            FeUi.TL(arrow.rectTransform, 5f, 1f, 12f, 20f);
            arrow.text = "▶";
            var b = img.gameObject.AddComponent<FeButton>();
            b.Init();
            int idx = i;
            b.Clicked += () => clicked = idx;
            b.HoverChanged += hov =>
            {
                if (!hov || focus == idx || chosen) return;
                focus = idx;
                Refresh();
                GameAudio.PlaySfx("SFX_UI_Hover", 0.5f);
            };
            buttons[i] = img;
            arrows[i] = arrow;
            labels[i] = t;
            feButtons[i] = b;
        }

        private void Refresh()
        {
            for (int i = 0; i < 2; i++)
            {
                bool on = focus == i;
                SetPanelLit(buttons[i], on);
                arrows[i].enabled = on;
                labels[i].color = on ? Color.white : Cream;
            }
        }

        private IEnumerator Run()
        {
            Debug.Log($"[GameEnd] oyun sonu (ilk afet: {(GameFlow.FirstSelectedDisaster.HasValue ? GameFlow.FirstSelectedDisaster.Value.ToString() : "-")}, " +
                      $"deprem={GameFlow.EarthquakeCompleted}, sel={GameFlow.FloodCompleted})");
            for (int i = 0; i < 2; i++) feButtons[i].Interactable = false;
            focus = 0;
            Refresh();

            // Siyah → sıcak şafak ışığı → sahne (yumuşak, sıcak geçiş).
            fade.raycastTarget = true;
            float dur = Mathf.Max(0.1f, fadeInSeconds);
            for (float k = 0f; k < dur; k += Time.unscaledDeltaTime)
            {
                float e = k / dur;
                Color c = Color.Lerp(Color.black, Dawn, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e * 1.6f)));
                c.a = 1f - FeUi.OutCubic(e);
                fade.color = c;
                charGroup.alpha = Mathf.Clamp01((e - 0.35f) / 0.4f);
                yield return null;
            }
            fade.color = new Color(0f, 0f, 0f, 0f);
            fade.raycastTarget = false;
            fade.gameObject.SetActive(false);
            charGroup.alpha = 1f;

            yield return Reveal(titleBlock, titleGroup, 6f, 0.4f);
            GameAudio.PlaySfx("SFX_Badge", 0.7f);
            yield return Reveal(bottomPanel, bottomGroup, 222f, 0.35f);
            yield return Reveal(buttonRow, buttonGroup, 318f, 0.25f);

            for (float k = 0f; k < inputDelay; k += Time.unscaledDeltaTime) yield return null;
            for (int i = 0; i < 2; i++) feButtons[i].Interactable = true;

            int frame = Time.frameCount;
            int choice = -1;
            while (choice < 0)
            {
                if (clicked >= 0) choice = clicked;
                else if (Time.frameCount > frame)
                {
                    int key = GameInput.ChoiceKeyPressed;
                    if (key == 1 || key == 2) choice = key - 1;
                    else if (GameInput.BackPressed) choice = 0;
                    else if (GameInput.LeftPressed && focus != 0) { focus = 0; Refresh(); GameAudio.PlaySfx("SFX_UI_Hover", 0.5f); }
                    else if (GameInput.RightPressed && focus != 1) { focus = 1; Refresh(); GameAudio.PlaySfx("SFX_UI_Hover", 0.5f); }
                    else if (GameInput.ConfirmPressed || GameInput.InteractPressed) choice = focus;
                }
                yield return null;
            }
            chosen = true;
            for (int i = 0; i < 2; i++) feButtons[i].Interactable = false;
            focus = choice;
            Refresh();
            GameAudio.PlaySfx("SFX_UI_Confirm", 0.8f); // oyun sonu onayı (P-UI; önce SFX_UI_Click)
            var rt = buttons[choice].rectTransform;
            Vector2 p0 = rt.anchoredPosition;
            rt.anchoredPosition = p0 + new Vector2(0f, -1f);
            for (float k = 0f; k < 0.12f; k += Time.unscaledDeltaTime) yield return null;
            rt.anchoredPosition = p0;

            fade.gameObject.SetActive(true);
            fade.raycastTarget = true;
            float outDur = Mathf.Max(0.05f, fadeOutSeconds);
            for (float k = 0f; k < outDur; k += Time.unscaledDeltaTime)
            {
                fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(k / outDur));
                yield return null;
            }
            fade.color = Color.black;

            if (choice == 0)
            {
                Debug.Log("[GameEnd] → ana menü");
                GameSceneRouter.ReturnToMenu();
            }
            else
            {
                var first = GameFlow.FirstSelectedDisaster ?? ChapterId.Earthquake;
                Debug.Log($"[GameEnd] → tekrar oyna ({first})");
                GameFlow.BeginNewGame(first);
            }
        }

        private static IEnumerator Reveal(RectTransform rt, CanvasGroup g, float y, float seconds)
        {
            float x = rt.anchoredPosition.x;
            for (float k = 0f; k < seconds; k += Time.unscaledDeltaTime)
            {
                float e = FeUi.OutCubic(k / seconds);
                g.alpha = e;
                rt.anchoredPosition = new Vector2(x, -Mathf.Round(y + Mathf.Lerp(10f, 0f, e)));
                yield return null;
            }
            g.alpha = 1f;
            rt.anchoredPosition = new Vector2(x, -y);
        }
    }
}
