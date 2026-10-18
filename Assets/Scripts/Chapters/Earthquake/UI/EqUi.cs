using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Deprem arayüzünün kodla kurulan uGUI parçaları için ortak yardımcılar: Canvas (640x360 referans),
    /// Image/Text üretimi, 9-slice ayarı (1 sprite pikseli = 1 referans pikseli), palet, yumuşatma eğrileri.
    /// Koordinatlar sanat maketleriyle aynı: sol-üst köşe orijinli referans pikseli (TL).
    /// </summary>
    internal static class EqUi
    {
        public const float RefPixelsPerUnit = 100f;

        // Palet (S/eqD_manifest.json → palette)
        public static readonly Color Ink = Hex(0x14141F);
        public static readonly Color Navy = Hex(0x263559);
        public static readonly Color NavyDark = Hex(0x1C2742);
        public static readonly Color NavyLight = Hex(0x3A4D7D);
        public static readonly Color Cream = Hex(0xF4EBD8);
        public static readonly Color CreamShade = Hex(0xDDCFB2);
        public static readonly Color Red = Hex(0xE8392B);
        public static readonly Color RedDark = Hex(0xA82418);
        public static readonly Color Amber = Hex(0xF2A33A);
        public static readonly Color AmberDark = Hex(0xB86E1E);
        public static readonly Color Green = Hex(0x18A957);
        public static readonly Color GreenDark = Hex(0x0D7A3D);
        public static readonly Color Blue = Hex(0x3A8DDE);
        public static readonly Color Muted = Hex(0x5A6080);
        public static readonly Color Gold = Hex(0xFFD27E);
        public static readonly Color White = Color.white;
        public static readonly Color ShadowRed = Hex(0x5A0F0A);
        public static readonly Color ShadowGreen = Hex(0x0A3C1E);
        public const float DimAlpha = 0.72f;

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static Color A(Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static string HexOf(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static EarthquakeUISkin ResolveSkin(Component owner, EarthquakeUISkin assigned)
        {
            if (assigned != null) return assigned;
            var s = owner.GetComponentInParent<EarthquakeUISkin>(true);
            if (s != null) return s;
            return Object.FindAnyObjectByType<EarthquakeUISkin>(FindObjectsInactive.Include);
        }

        public static Font ResolveFont(EarthquakeUISkin skin)
        {
            if (skin != null && skin.Font != null) return skin.Font;
            return AfetUyanisi.UI.GameFonts.Story;
        }

        public static Sprite S(EarthquakeUISkin skin, System.Func<EarthquakeUISkin, Sprite> pick) =>
            skin != null ? pick(skin) : null;

        /// <summary>Screen Space Overlay canvas, 640x360 referans, match 0.5. View bir Canvas altındaysa sahne köküne kurulur.</summary>
        public static Canvas CreateCanvas(Component owner, string name, int order, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.layer = 5;
            bool underCanvas = owner.GetComponentInParent<Canvas>(true) != null;
            if (underCanvas)
                Debug.LogWarning("[EQ-UI] " + owner.GetType().Name + " bir Canvas altında; kendi Canvas'ı sahne köküne kuruldu. View'ları Canvas dışındaki boş bir objeye koyun.", owner);
            else
                go.transform.SetParent(owner.transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.pixelPerfect = false;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = RefPixelsPerUnit;

            group = go.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Fare tıklamaları için sahnede EventSystem yoksa InputSystemUIInputModule ile kurar.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("[EQ-UI] Sahnede EventSystem yoktu; InputSystemUIInputModule ile kuruldu.", es);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f) =>
            StretchOffsets(rt, inset, inset, inset, inset);

        public static RectTransform StretchOffsets(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>anchor = pivot = (ax, ay), konum ve boyut referans pikselinde.</summary>
        public static RectTransform Place(RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = Snap(pos);
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Sol-üst orijinli (maket koordinatı) yerleşim: ebeveynin sol üstünden x sağa, y aşağı.</summary>
        public static RectTransform TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Snap(new Vector2(x, -y));
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Ebeveyn genişliğine yayılan şerit: sol/sağ boşluk, üstten y, yükseklik h.</summary>
        public static RectTransform TopBand(RectTransform rt, float left, float right, float y, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -y - h);
            rt.offsetMax = new Vector2(-right, -y);
            return rt;
        }

        public static Vector2 Snap(Vector2 v) => new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));

        public static Image Img(string name, Transform parent, Sprite sprite, Color fallback, EarthquakeUISkin skin, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = raycast;
            Skin(img, sprite, fallback, skin);
            return img;
        }

        /// <summary>Sprite varsa (beyaz ya da verilen tint) 9-slice ise Sliced + 1:1 piksel; yoksa düz renk.</summary>
        public static void Skin(Image img, Sprite sprite, Color fallback, EarthquakeUISkin skin, Color? tintWhenSprite = null)
        {
            img.sprite = sprite;
            if (sprite == null)
            {
                img.type = Image.Type.Simple;
                img.color = fallback;
                return;
            }
            img.color = tintWhenSprite ?? Color.white;
            if (sprite.border.sqrMagnitude > 0f)
            {
                float scale = skin != null ? skin.PixelScale : 1f;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.pixelsPerUnitMultiplier = RefPixelsPerUnit / (sprite.pixelsPerUnit * scale);
            }
            else
            {
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
        }

        /// <summary>
        /// Sprite yoksa yedek görünüm: img = kenar rengi, içine "Fill" (ve istenirse üstte "Band" başlık şeridi).
        /// Yedek parçalar ilk kardeşler olarak eklenir, içerik üstlerinde çizilir.
        /// </summary>
        public static void Fallback(Image img, Color border, Color fill, EarthquakeUISkin skin,
            float bandTop = 0f, float bandHeight = 0f, Color? band = null)
        {
            if (img.sprite != null) return;
            img.color = border;
            if (bandHeight > 0f)
            {
                var b = Img("Band", img.transform, null, band ?? Navy, skin);
                TopBand(b.rectTransform, 1f, 1f, bandTop, bandHeight);
                b.transform.SetAsFirstSibling();
            }
            var f = Img("Fill", img.transform, null, fill, skin);
            Stretch(f.rectTransform, 1f);
            f.transform.SetAsFirstSibling();
        }

        /// <summary>Yedek görünümde dolgu rengini değiştirir (sprite varsa bir şey yapmaz).</summary>
        public static void FallbackColors(Image img, Color border, Color fill)
        {
            if (img.sprite != null) return;
            img.color = border;
            var f = img.transform.Find("Fill");
            if (f != null) f.GetComponent<Image>().color = fill;
        }

        public static Image DimImage(Transform parent, EarthquakeUISkin skin)
        {
            var img = Img("Dim", parent, skin != null ? skin.Dim : null, A(Ink, 0f), skin, true);
            img.preserveAspect = false;
            img.type = Image.Type.Simple;
            Stretch(img.rectTransform);
            SetDim(img, 0f);
            return img;
        }

        /// <summary>k = 0..1; sprite'ın alfası gömülü (0.72), yoksa mürekkep rengi 0.72 alfa.</summary>
        public static void SetDim(Image img, float k)
        {
            k = Clamp01(k);
            img.color = img.sprite != null ? new Color(1f, 1f, 1f, k) : A(Ink, DimAlpha * k);
        }

        public static Text Txt(string name, Transform parent, Font font, int size, Color color, TextAnchor align, Color? shadow = null)
        {
            var rt = Rect(name, parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = AfetUyanisi.UI.GameFonts.SizeFor(font, size);
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1f;
            if (shadow.HasValue)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = shadow.Value;
                sh.effectDistance = new Vector2(1f, -1f);
            }
            return t;
        }

        /// <summary>Sığmazsa küçülsün (dikeyde taşma kesilir).</summary>
        public static void BestFit(Text t, int min)
        {
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Min(AfetUyanisi.UI.GameFonts.MinSizeFor(t.font, min), t.fontSize);
            t.resizeTextMaxSize = t.fontSize;
        }

        /// <summary>Metnin verilen genişlikte tercih edilen yüksekliği (referans pikseli).</summary>
        public static float PreferredHeight(Text t, string s, float width)
        {
            var settings = t.GetGenerationSettings(new Vector2(width, 0f));
            settings.resizeTextForBestFit = false;
            settings.fontSize = t.fontSize;
            settings.scaleFactor = 1f;
            return t.cachedTextGeneratorForLayout.GetPreferredHeight(s ?? "", settings);
        }

        public static float PreferredWidth(Text t, string s)
        {
            var settings = t.GetGenerationSettings(Vector2.zero);
            settings.resizeTextForBestFit = false;
            settings.fontSize = t.fontSize;
            settings.scaleFactor = 1f;
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(s ?? "", settings);
        }

        /// <summary>Küçük tuş rozeti ("E", "1"...): amber plaka + koyu yazı.</summary>
        public static RectTransform KeyCap(Transform parent, string key, Font font, EarthquakeUISkin skin, int size = 7)
        {
            var img = Img("Key_" + key, parent, skin != null ? skin.KeyCap : null, Amber, skin);
            if (img.sprite == null) Fallback(img, Ink, Amber, skin);
            var t = Txt("Label", img.transform, font, size, Ink, TextAnchor.MiddleCenter);
            StretchOffsets(t.rectTransform, 0f, 1f, 0f, 0f);
            t.text = key;
            return img.rectTransform;
        }

        public static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);

        public static float OutCubic(float x)
        {
            x = Clamp01(x);
            float f = 1f - x;
            return 1f - f * f * f;
        }

        public static float InCubic(float x)
        {
            x = Clamp01(x);
            return x * x * x;
        }

        public static float OutBack(float x, float overshoot = 1.2f)
        {
            x = Clamp01(x);
            float c3 = overshoot + 1f;
            float f = x - 1f;
            return 1f + c3 * f * f * f + overshoot * f * f;
        }

        public static float InBack(float x, float overshoot = 1.2f)
        {
            x = Clamp01(x);
            float c3 = overshoot + 1f;
            return c3 * x * x * x - overshoot * x * x;
        }

        /// <summary>"mm:ss" (yukarı yuvarlanmış saniye).</summary>
        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds - 0.0001f));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        /// <summary>Süre çubuğu rengi: &gt;%60 yeşil, %30–60 amber, &lt;%30 kırmızı (geçişler yumuşak).</summary>
        public static Color BarColor(float k)
        {
            if (k > 0.6f) return Color.Lerp(Amber, Green, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.1f));
            if (k > 0.3f) return Color.Lerp(Red, Amber, Mathf.SmoothStep(0f, 1f, (k - 0.3f) / 0.1f));
            return Red;
        }
    }
}
