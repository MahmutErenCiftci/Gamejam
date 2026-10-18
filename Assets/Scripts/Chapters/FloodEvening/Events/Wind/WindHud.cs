using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Rüzgâr koridoru arayüzü (piksel stil, 480x270 tabanında tam sayı katla ölçeklenir):
    /// • Uyarı paneli: "DİKKAT! KUVVETLİ RÜZGÂR" tabelası + üç kısa satır + [A]/[D] örneği + "[E] Anladım" (tıklanabilir).
    /// • Üst ortada rüzgâr göstergesi: büyük ok (rüzgârın ittiği yön), şiddet etiketi, ters tuş ipucu ve denge durumu.
    /// • Uyarı (yön değişecek): "!" ve yeni yönün oku yanıp söner. Avluda kısa "rüzgâr kesildi" bildirimi.
    /// Tuş okumaz; durumu WindCorridor verir. Metinler Inspector'da (Arda JSON'a taşıyabilir).
    /// </summary>
    public class WindHud : MonoBehaviour
    {
        public enum Balance { Calm, Good, Hold, Wrong }

        [Header("Sprite'lar (Assets/Art/FloodEvening/Wind/UI)")]
        [SerializeField] private Sprite banner;
        [SerializeField] private Sprite arrowLeft, arrowRight, hintA, hintD, alert, chevLeft, chevRight;
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 66;

        [Header("Metinler")]
        [SerializeField] private string subtitle = "KUVVETLİ RÜZGÂR";
        [SerializeField, TextArea] private string infoBody = "Ok, rüzgârın seni ittiği yönü gösterir.\nTers yöndeki tuşa basarak dengeni koru.\nAynı yöne basarsan geriye savrulursun.";
        [SerializeField] private string infoButton = "[E] Anladım";
        [SerializeField] private string lightLabel = "HAFİF RÜZGÂR", mediumLabel = "ORTA RÜZGÂR", strongLabel = "ŞİDDETLİ RÜZGÂR";
        [SerializeField] private string goodLabel = "DENGEDESİN", holdLabel = "TERS TUŞA BAS!", wrongLabel = "SAVRULDUN!", calmLabel = "SAKİN · İLERLE", warnLabel = "RÜZGÂR DÖNÜYOR!";
        [SerializeField] private string shelterText = "Rüzgâr kesildi. Burası korunaklı!";

        private static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 255), Navy = new Color32(0x14, 0x1E, 0x3C, 245), NavyEdge = new Color32(0x2E, 0x4C, 0x8E, 255),
            Green = new Color32(0x7F, 0xE0, 0x9A, 255), Amber = new Color32(0xF5, 0xB7, 0x31, 255), Red = new Color32(0xFF, 0x6B, 0x5A, 255), Soft = new Color32(0xA9, 0xC4, 0xF0, 255);

        private Canvas canvas; private CanvasScaler scaler;
        private RectTransform info, gauge, toast;
        private Image arrowImg, hintImg, alertImg, chevImg;
        private Text strengthText, stateText, toastText;
        private bool clicked, built, warning;
        private int warnDir; private float warnT, toastT, pulseT;
        private Balance balance = Balance.Calm;

        private void Build()
        {
            if (built) return; built = true;
            var go = new GameObject("FE_WindCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortingOrder; canvas.pixelPerfect = true;
            scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // --- uyarı paneli ---
            info = Rect("Info", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(232f, 142f));
            var body = Rect("Body", info, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(232f, 98f));
            var edge = body.gameObject.AddComponent<Image>(); edge.color = NavyEdge;
            var inner = Rect("Inner", body, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(228f, 94f)); inner.gameObject.AddComponent<Image>().color = Navy;
            var ban = Img("Banner", info, banner, new Vector2(0.5f, 1f), new Vector2(0f, -23f));
            var sub = Txt("Subtitle", ban.rectTransform, subtitle, 8, Cream, TextAnchor.MiddleLeft); Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(41f + 46f, -31.5f), new Vector2(92f, 12f));
            var bt = Txt("Body", inner, infoBody, 8, Cream, TextAnchor.UpperCenter); Place(bt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(222f, 36f));
            bt.lineSpacing = 1.05f;
            var ha = Img("HintA", inner, hintA, new Vector2(0.5f, 0f), new Vector2(-46f, 34f)); ha.rectTransform.localScale = Vector3.one * 0.75f;
            var hd = Img("HintD", inner, hintD, new Vector2(0.5f, 0f), new Vector2(46f, 34f)); hd.rectTransform.localScale = Vector3.one * 0.75f;
            var btn = Txt("Ok", inner, infoButton, 8, Amber, TextAnchor.MiddleCenter); Place(btn.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(120f, 12f));
            var button = info.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None; button.navigation = new Navigation { mode = Navigation.Mode.None };
            info.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            button.onClick.AddListener(() => clicked = true);
            info.gameObject.SetActive(false);

            // --- üst gösterge ---
            gauge = Rect("Gauge", go.transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(120f, 70f));
            arrowImg = Img("Arrow", gauge, arrowRight, new Vector2(0.5f, 1f), new Vector2(0f, -18f));
            alertImg = Img("Alert", gauge, alert, new Vector2(0.5f, 1f), new Vector2(-44f, -18f));
            chevImg = Img("Chev", gauge, chevRight, new Vector2(0.5f, 1f), new Vector2(0f, -18f));
            strengthText = Txt("Strength", gauge, lightLabel, 8, Soft, TextAnchor.MiddleCenter); Place(strengthText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(140f, 10f));
            hintImg = Img("Hint", gauge, hintA, new Vector2(0.5f, 1f), new Vector2(0f, -58f)); hintImg.rectTransform.localScale = Vector3.one * 0.75f;
            stateText = Txt("State", gauge, "", 8, Green, TextAnchor.MiddleCenter); Place(stateText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(160f, 10f));
            gauge.gameObject.SetActive(false);

            // --- avlu bildirimi ---
            toast = Rect("Toast", go.transform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(190f, 22f));
            toast.gameObject.AddComponent<Image>().color = NavyEdge;
            var ti = Rect("Inner", toast, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(186f, 18f)); ti.gameObject.AddComponent<Image>().color = Navy;
            toastText = Txt("Text", ti, shelterText, 8, Green, TextAnchor.MiddleCenter); Place(toastText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 14f));
            toast.gameObject.SetActive(false);
            ApplyScale();
        }

        private void ApplyScale()
        {
            if (scaler == null) return;
            scaler.scaleFactor = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.width / 480f, Screen.height / 270f)));
        }

        public void ShowInfo(bool on) { Build(); clicked = false; info.gameObject.SetActive(on); }

        /// <summary>Paneldeki tıklamayı bir kez okur (fareyle "Anladım").</summary>
        public bool ConsumeClick() { bool c = clicked; clicked = false; return c; }

        public void ShowGauge(bool on) { Build(); gauge.gameObject.SetActive(on); if (!on) warning = false; }

        /// <summary>Yön değişecek: yeni yönün oku ve "!" yanıp söner.</summary>
        public void Warn(int nextPush, WindCorridor.Strength s)
        {
            Build(); warning = true; warnDir = nextPush; warnT = 0f;
            arrowImg.sprite = nextPush > 0 ? arrowRight : arrowLeft; Native(arrowImg);
            strengthText.text = warnLabel; strengthText.color = Amber;
            hintImg.gameObject.SetActive(false);
            stateText.text = "";
        }

        public void SetGust(int push, WindCorridor.Strength s)
        {
            Build(); warning = false;
            bool calm = push == 0;
            arrowImg.gameObject.SetActive(!calm);
            alertImg.gameObject.SetActive(false);
            chevImg.gameObject.SetActive(calm);
            hintImg.gameObject.SetActive(!calm);
            if (!calm)
            {
                arrowImg.sprite = push > 0 ? arrowRight : arrowLeft; Native(arrowImg);
                arrowImg.color = Color.white;
                float sc = s == WindCorridor.Strength.Hafif ? 0.8f : s == WindCorridor.Strength.Orta ? 1f : 1.25f;
                arrowImg.rectTransform.localScale = Vector3.one * sc;
                hintImg.sprite = push > 0 ? hintA : hintD; Native(hintImg);   // sağa itiyorsa A ile sola karşı koy
                strengthText.text = s == WindCorridor.Strength.Hafif ? lightLabel : s == WindCorridor.Strength.Orta ? mediumLabel : strongLabel;
                strengthText.color = s == WindCorridor.Strength.Siddetli ? Red : s == WindCorridor.Strength.Orta ? Amber : Soft;
            }
            else
            {
                chevImg.sprite = chevRight; Native(chevImg); chevImg.color = new Color(1f, 1f, 1f, 0.35f);
                strengthText.text = ""; stateText.text = calmLabel; stateText.color = Soft;
            }
        }

        public void SetBalance(Balance b)
        {
            if (!built || warning) return;
            if (b == balance && b != Balance.Wrong) return;
            balance = b; pulseT = 0f;
            switch (b)
            {
                case Balance.Good: stateText.text = goodLabel; stateText.color = Green; break;
                case Balance.Hold: stateText.text = holdLabel; stateText.color = Amber; break;
                case Balance.Wrong: stateText.text = wrongLabel; stateText.color = Red; break;
                default: stateText.text = calmLabel; stateText.color = Soft; break;
            }
        }

        public void ShowSheltered() { Build(); toast.gameObject.SetActive(true); toastT = 2.2f; }

        /// <summary>Üst orta şu an rüzgâr göstergesi/bildirimi ile dolu mu (sel uyarı şeridi o sırada alta iner, üst üste binmesin).</summary>
        public bool TopBusy => built && (gauge.gameObject.activeSelf || toast.gameObject.activeSelf);

        public void HideAll()
        {
            if (!built) return;
            info.gameObject.SetActive(false); gauge.gameObject.SetActive(false); toast.gameObject.SetActive(false); warning = false;
        }

        private void Update()
        {
            if (!built) return;
            ApplyScale();
            float dt = Time.unscaledDeltaTime;
            pulseT += dt;
            if (warning)
            {
                warnT += dt;
                bool on = Mathf.FloorToInt(warnT * 8f) % 2 == 0;
                arrowImg.gameObject.SetActive(true); chevImg.gameObject.SetActive(false);
                arrowImg.color = new Color(1f, 1f, 1f, on ? 1f : 0.25f);
                alertImg.gameObject.SetActive(on);
            }
            else if (balance == Balance.Hold && stateText != null)
            {
                float k = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(pulseT * 6f)); var c = Amber; c.a = k; stateText.color = c;
                if (hintImg.gameObject.activeSelf) hintImg.rectTransform.localScale = Vector3.one * (0.75f + 0.06f * Mathf.Abs(Mathf.Sin(pulseT * 6f)));
            }
            else if (hintImg != null) hintImg.rectTransform.localScale = Vector3.one * 0.75f;
            if (arrowImg != null && arrowImg.gameObject.activeSelf && !warning)
            {
                // ok rüzgâr yönünde hafifçe "akar" (1-2 piksel)
                var p = arrowImg.rectTransform.anchoredPosition; p.x = Mathf.Round(Mathf.Sin(pulseT * 5f) * 1.5f); arrowImg.rectTransform.anchoredPosition = p;
            }
            if (toastT > 0f) { toastT -= dt; if (toastT <= 0f) toast.gameObject.SetActive(false); }
        }

        // ---- yardımcılar ----
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); Place(rt, anchor, pos, size); return rt;
        }
        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
        private Image Img(string name, Transform parent, Sprite sp, Vector2 anchor, Vector2 pos)
        {
            var rt = Rect(name, parent, anchor, pos, sp != null ? sp.rect.size : new Vector2(8f, 8f));
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = sp; img.raycastTarget = false; return img;
        }
        private static void Native(Image img) { if (img.sprite != null) img.rectTransform.sizeDelta = img.sprite.rect.size; }
        private Text Txt(string name, Transform parent, string text, int size, Color color, TextAnchor align)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 10f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.color = color; t.alignment = align; t.text = text; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
