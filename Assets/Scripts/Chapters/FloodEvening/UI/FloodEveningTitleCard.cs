using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Sel bölümünün açılış kapağı "EVE DÖNÜŞ" (Berk): okul çıkışında bastıran sağanak. Karanlık, yağmurlu okul sokağı
    /// (Assets/Art/UI/FloodTitle, 320x180 piksel çizim, 640x360 tuvale 2 kat): uzakta okul kapısı ve binalar, kaldırımda
    /// otobüs durağı, suda yansıyan sokak lambaları, ön planda sırt çantalı çocuk silüeti. Ortada başlık (flood_evening.json
    /// FE_TITLE konuşmacısı), amber vurgu çizgisi (menünün sıcak vurgusu, sahnedeki lamba ışığı), altında küçük "Sel Bölümü" (chapterLabel) ve alt yazı (FE_TITLE metni).
    /// Açılışta yağmur yavaşça başlar, su birikintileri hafifçe büyür, uzakta soluk bir şimşek ve gecikmeli gök gürültüsü.
    /// Yazılar (PixelLetterFx, piksel ızgarasında): başlık harf harf yağmur damlası gibi düşüp seker, çizgi ortadan açılır ve
    /// üstünden bir parıltı geçer, "Sel Bölümü" ve alt yazı aşağıdan yükselir; şimşekte başlık bir an aydınlanır.
    /// Tuş yönlendirmesi/buton yok: holdSeconds sonra kendiliğinden kararır (E/Space/Enter/tık minSeconds sonra sessizce geçer).
    /// Çizimler atanmamışsa eski siyah kart gibi davranır. FloodSchool ve FloodEvening aynı prefabı (FE_TitleCard) kullanır.
    /// Canvas 120 (ekran perdesinin üstünde).
    /// </summary>
    public class FloodEveningTitleCard : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 120;
        [Tooltip("Kapak bu süre görünür, sonra kendiliğinden kararır (sn)")]
        [SerializeField] private float holdSeconds = 4.2f;
        [SerializeField] private float minSeconds = 0.9f;

        [Header("Kapak: \"Eve Dönüş\" (Assets/Art/UI/FloodTitle)")]
        [Tooltip("Başlığın altındaki küçük bölüm adı")]
        [SerializeField] private string chapterLabel = "Sel Bölümü";
        [Tooltip("FT_Background: gökyüzü, okul, durak, lambalar, yol (320x180, Point filtre)")]
        [SerializeField] private Sprite background;
        [Tooltip("FT_Puddles_0..7: büyüyen su birikintileri ve suda lamba yansımaları (sırayla oynar)")]
        [SerializeField] private Sprite[] puddleFrames;
        [Tooltip("FT_Child: ön planda sırt çantalı çocuk silüeti")]
        [SerializeField] private Sprite child;
        [Tooltip("FT_Flash: uzak şimşekte aydınlanan bulut kenarları")]
        [SerializeField] private Sprite cloudFlash;
        [SerializeField] private int nearDrops = 70;
        [SerializeField] private int farDrops = 60;
        [Tooltip("Uzak şimşek (sn, kapak açıldıktan sonra); gök gürültüsü mesafe gereği daha geç gelir")]
        [SerializeField] private float flashAt = 1.35f;
        [SerializeField] private float thunderAt = 2.1f;
        [SerializeField] private string thunderSfx = "SFX_FE_Thunder_2";
        [SerializeField, Range(0f, 1f)] private float thunderVolume = 0.6f;

        private const float ArtW = 640f, ArtH = 360f;   // çizim 320x180, 2 kat
        private const float ArtIn = 0.8f, FadeOut = 0.6f;
        private const float PuddleFrom = 0.4f, PuddleTo = 3.4f;
        private const float LineWidth = 200f;
        // Yazı girişi (sn, kapak açıldıktan sonra): başlık harf harf düşer → çizgi ortadan açılır, üstünden parıltı geçer →
        // "Sel Bölümü" ve alt yazı aşağıdan yükselir. Hepsi ~2,1 sn'de yerinde, okumaya en az 2 sn kalır.
        private const float TitleStart = 0.5f, TitleStagger = 0.07f, TitleDuration = 0.38f;
        private const float LineStart = 1.15f, LineDuration = 0.45f, GlintStart = 1.65f, GlintDuration = 0.4f;
        private const float ChapterStart = 1.45f, SubtitleStart = 1.65f, RiseDuration = 0.45f;
        private const float FlashBrighten = 0.55f;
        private static readonly Color NearRain = new Color(0.66f, 0.74f, 0.84f, 0.55f);
        private static readonly Color FarRain = new Color(0.50f, 0.58f, 0.68f, 0.35f);

        private bool built;
        private CanvasGroup group, textGroup, artGroup;
        private RectTransform line, rainRoot;
        private Canvas canvas;
        private Text titleText, chapterText, subtitleText, footText;
        private AfetUyanisi.UI.PixelLetterFx titleFx, chapterFx, subtitleFx;
        private Image puddles, flash, glint;

        private struct Drop
        {
            public RectTransform rt;
            public Vector2 pos;
            public float speed, endY;
            public bool near, live;
        }

        private Drop[] drops;
        private Image[] splashes;
        private float[] splashAge;
        private int nextSplash;
        private Texture2D[] ownedTextures;

        private bool HasArt => background != null;

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (ownedTextures == null) return;
            foreach (var t in ownedTextures) if (t != null) Destroy(t);
        }

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FeUi.CreateCanvas(transform, "FE_TitleCardCanvas", sortingOrder, out group);
            var black = FeUi.Img("Black", canvas.transform, Color.black, true);
            FeUi.Stretch(black.rectTransform);
            if (HasArt) BuildArt();

            var textRoot = FeUi.Rect("Text", canvas.transform);
            FeUi.Stretch(textRoot);
            textGroup = textRoot.gameObject.AddComponent<CanvasGroup>();

            // Başlık: Pixel Operator ızgarası 16 px; 48 = 3 kat, pikseller eşit ve net. Gölge 1 font pikseli.
            // Harf efektleri gölgeden ÖNCE eklenir: gölge harflerin hareketli halini kopyalar.
            var t = FeUi.Txt("Title", textRoot, font, 30, FeUi.Cream, TextAnchor.MiddleCenter, false);
            t.fontSize = HasArt ? 48 : t.fontSize;
            float titlePx = HasArt ? 3f : 1f;
            titleFx = t.gameObject.AddComponent<AfetUyanisi.UI.PixelLetterFx>();
            titleFx.Setup(AfetUyanisi.UI.PixelLetterFx.Motion.Drop, TitleStagger, TitleDuration, 6f, titlePx);
            AddShadow(t, titlePx);
            FeUi.Place(t.rectTransform, 0.5f, 0.5f, new Vector2(0f, HasArt ? 34f : 22f), new Vector2(600f, 60f));
            titleText = t;
            line = FeUi.Img("Line", textRoot, FeUi.Amber).rectTransform;
            FeUi.Place(line, 0.5f, 0.5f, new Vector2(0f, HasArt ? 2f : 0f), new Vector2(LineWidth, 2f));
            glint = FeUi.Img("Glint", textRoot, FeUi.Cream);
            FeUi.Place(glint.rectTransform, 0.5f, 0.5f, line.anchoredPosition, new Vector2(8f, 2f));
            glint.enabled = false;
            var c = FeUi.Txt("Chapter", textRoot, font, 12, FeUi.Amber, TextAnchor.MiddleCenter, false);
            if (HasArt) c.fontSize = 16;
            chapterFx = c.gameObject.AddComponent<AfetUyanisi.UI.PixelLetterFx>();
            chapterFx.Setup(AfetUyanisi.UI.PixelLetterFx.Motion.Rise, 0f, RiseDuration, 3f, 2f);
            AddShadow(c, 1f);
            FeUi.Place(c.rectTransform, 0.5f, 0.5f, new Vector2(0f, -14f), new Vector2(560f, 20f));
            chapterText = c;
            var s = FeUi.Txt("Subtitle", textRoot, font, 12, HasArt ? FeUi.Cream : FeUi.Amber, TextAnchor.MiddleCenter, false);
            if (HasArt) s.fontSize = 16;
            subtitleFx = s.gameObject.AddComponent<AfetUyanisi.UI.PixelLetterFx>();
            subtitleFx.Setup(AfetUyanisi.UI.PixelLetterFx.Motion.Rise, 0f, RiseDuration, 3f, 2f);
            AddShadow(s, 1f);
            FeUi.Place(s.rectTransform, 0.5f, 0.5f, new Vector2(0f, HasArt ? -32f : -20f), new Vector2(560f, 22f));
            FeUi.BestFit(s, 9);
            subtitleText = s;
            var f = FeUi.Txt("Footnote", textRoot, font, 7, FeUi.Muted, TextAnchor.MiddleCenter);
            FeUi.Place(f.rectTransform, 0.5f, 0f, new Vector2(0f, 14f), new Vector2(560f, 12f));
            footText = f;

            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        /// <summary>Sahne katmanları: çizim, şimşek, su birikintileri, çocuk, yağmur, yazının arkasında koyu hale.</summary>
        private void BuildArt()
        {
            var art = FeUi.Rect("Art", canvas.transform);
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0.5f);
            art.sizeDelta = new Vector2(ArtW, ArtH);
            var fit = art.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // 16:9 dışında da ekranı doldurur
            fit.aspectRatio = ArtW / ArtH;
            artGroup = art.gameObject.AddComponent<CanvasGroup>();

            Layer("Background", art, background);
            flash = Layer("Flash", art, cloudFlash);
            flash.color = new Color(1f, 1f, 1f, 0f);
            puddles = Layer("Puddles", art, puddleFrames != null && puddleFrames.Length > 0 ? puddleFrames[0] : null);
            Layer("Child", art, child);

            rainRoot = FeUi.Rect("Rain", art);
            rainRoot.anchorMin = rainRoot.anchorMax = rainRoot.pivot = new Vector2(0.5f, 0.5f);
            rainRoot.sizeDelta = new Vector2(ArtW, ArtH);
            var nearTex = PixelTex(3, 7, new[] { 2, 2, 2, 1, 1, 1, 0 });  // 1/3 eğimli çizgi, alt ucu parlak
            var farTex = PixelTex(2, 4, new[] { 1, 1, 1, 0 });
            var splashTex = SplashTex();
            var nearSprite = Sprite.Create(nearTex, new Rect(0, 0, 3, 7), new Vector2(0.5f, 0f), 1f);
            var farSprite = Sprite.Create(farTex, new Rect(0, 0, 2, 4), new Vector2(0.5f, 0f), 1f);
            var splashSprite = Sprite.Create(splashTex, new Rect(0, 0, 5, 2), new Vector2(0.5f, 0f), 1f);

            drops = new Drop[Mathf.Max(0, farDrops) + Mathf.Max(0, nearDrops)];
            for (int i = 0; i < drops.Length; i++)
            {
                bool near = i >= farDrops;
                var img = FeUi.Img(near ? "Drop" : "FarDrop", rainRoot, near ? NearRain : FarRain);
                img.sprite = near ? nearSprite : farSprite;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = near ? new Vector2(6f, 14f) : new Vector2(4f, 8f);
                img.gameObject.SetActive(false);
                drops[i] = new Drop { rt = rt, near = near };
            }
            splashes = new Image[16];
            splashAge = new float[splashes.Length];
            for (int i = 0; i < splashes.Length; i++)
            {
                var img = FeUi.Img("Splash", rainRoot, NearRain);
                img.sprite = splashSprite;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(10f, 4f);
                img.gameObject.SetActive(false);
                splashes[i] = img;
                splashAge[i] = 1f;
            }

            // Başlığın arkasında, kenarlara doğru basamaklı azalan koyu hale: lambalar yazının altında da okunurluğu bozmaz.
            var scrimTex = ScrimTex(48, 24);
            ownedTextures = new[] { nearTex, farTex, splashTex, scrimTex };
            var scrim = FeUi.Img("Scrim", art, Color.white);
            scrim.sprite = Sprite.Create(scrimTex, new Rect(0, 0, scrimTex.width, scrimTex.height), new Vector2(0.5f, 0.5f), 1f);
            FeUi.Place(scrim.rectTransform, 0.5f, 0.5f, new Vector2(0f, 6f), new Vector2(480f, 160f));
        }

        /// <summary>FeUi.Txt'nin gölgesiyle aynı; uzaklık 1 font pikseli.</summary>
        private static void AddShadow(Text t, float px)
        {
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = FeUi.A(FeUi.Ink, 0.9f);
            sh.effectDistance = new Vector2(px, -px);
        }

        private static Image Layer(string name, RectTransform parent, Sprite sprite)
        {
            var img = FeUi.Img(name, parent, Color.white);
            img.sprite = sprite;
            img.enabled = sprite != null;
            FeUi.Stretch(img.rectTransform);
            return img;
        }

        /// <summary>Kartı oynatır; döndüğünde kart kaybolmuştur ve arkadaki ekran perdesi (fader) siyahtır.</summary>
        public IEnumerator Play(string title, string subtitle, string footnote, bool skipInput = true)
        {
            Build();
            titleText.text = title ?? "";
            chapterText.text = chapterLabel ?? "";
            subtitleText.text = subtitle ?? "";
            footText.text = footnote ?? "";
            ResetScene();
            UpdateText(0f);
            titleFx.Brighten = 0f;
            // Sahne yüklenirken ilk karelerin uzun süresi kartı atlatmasın: birkaç sabit kare bekle, kare süresini sınırla.
            for (int i = 0; i < 3; i++) yield return null;
            canvas.gameObject.SetActive(true);
            group.alpha = 1f;
            textGroup.alpha = 0f;
            if (artGroup != null) artGroup.alpha = 0f;

            int startFrame = Time.frameCount;
            bool thundered = false, leaving = false;
            float k = 0f, leaveAt = 0f;
            while (true)
            {
                float dt = Dt;
                k += dt;
                if (!leaving && (k >= holdSeconds ||
                                 skipInput && k >= minSeconds && Time.frameCount > startFrame && GameInput.ContinuePressed))
                {
                    leaving = true;
                    leaveAt = k;
                }
                float fade = leaving ? 1f - Mathf.Clamp01((k - leaveAt) / FadeOut) : 1f;

                textGroup.alpha = fade;
                UpdateText(k);

                if (artGroup != null)
                {
                    artGroup.alpha = FeUi.OutCubic(k / ArtIn) * fade;
                    UpdatePuddles(k);
                    titleFx.Brighten = UpdateFlash(k - flashAt) * FlashBrighten; // başlık şimşekle bir an aydınlanır
                    UpdateRain(dt, RainAmount(k));
                    if (!thundered && k >= thunderAt)
                    {
                        thundered = true;
                        if (!string.IsNullOrEmpty(thunderSfx)) GameAudio.PlaySfx(thunderSfx, thunderVolume);
                    }
                }
                if (leaving && fade <= 0f) break;
                yield return null;
            }
            textGroup.alpha = 0f;
            yield return null; // kartı geçen tuş bir sonraki adımı tetiklemesin
        }

        private static float Dt => Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        /// <summary>Kartın siyah zeminini de kaldırır (arkada fader siyah kalmalı).</summary>
        public void Hide()
        {
            if (!built) return;
            group.alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ yazı animasyonu

        /// <summary>Başlık harf harf düşer, çizgi ortadan iki yana açılır ve üstünden bir parıltı geçer, alt yazılar yükselir.</summary>
        private void UpdateText(float k)
        {
            titleFx.Elapsed = k - TitleStart;
            chapterFx.Elapsed = k - ChapterStart;
            subtitleFx.Elapsed = k - SubtitleStart;

            float l = FeUi.OutCubic((k - LineStart) / LineDuration);
            line.sizeDelta = new Vector2(Mathf.Round(LineWidth * l / 4f) * 4f, 2f); // iki yana eşit, 2 birimlik adım

            float g = (k - GlintStart) / GlintDuration;
            bool showGlint = g > 0f && g < 1f;
            glint.enabled = showGlint;
            if (showGlint)
            {
                float x = Mathf.Lerp(-LineWidth * 0.5f + 4f, LineWidth * 0.5f - 4f, g * g * (3f - 2f * g));
                glint.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x / 2f) * 2f, line.anchoredPosition.y);
                glint.color = FeUi.A(FeUi.Cream, Mathf.Sin(g * Mathf.PI) * 0.9f);
            }
        }

        // ------------------------------------------------------------------ sahne animasyonu

        private void ResetScene()
        {
            if (drops == null) return;
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i].live = false;
                drops[i].rt.gameObject.SetActive(false);
            }
            for (int i = 0; i < splashes.Length; i++)
            {
                splashAge[i] = 1f;
                splashes[i].gameObject.SetActive(false);
            }
            flash.color = new Color(1f, 1f, 1f, 0f);
            UpdatePuddles(0f);
        }

        /// <summary>Yağmur yavaş başlar: ilk 0.15 sn yok, ~2 sn'de tam şiddet.</summary>
        private static float RainAmount(float k)
        {
            float x = Mathf.Clamp01((k - 0.15f) / 2.0f);
            return x * x * (3f - 2f * x);
        }

        private void UpdatePuddles(float k)
        {
            if (puddles == null || puddleFrames == null || puddleFrames.Length == 0) return;
            float p = Mathf.Clamp01((k - PuddleFrom) / (PuddleTo - PuddleFrom));
            p = 1f - (1f - p) * (1f - p); // başta hızlı, sonra yavaşlayarak büyür
            var frame = puddleFrames[Mathf.Min(puddleFrames.Length - 1, Mathf.FloorToInt(p * puddleFrames.Length))];
            if (frame != null && puddles.sprite != frame) puddles.sprite = frame;
        }

        /// <summary>Uzak şimşek: kısa, soluk bir çift parıltı (korkutucu değil); bulut kenarları aydınlanır.
        /// Döner: parıltının gücü (0..1), başlığın aydınlanması için.</summary>
        private float UpdateFlash(float t)
        {
            if (flash == null || !flash.enabled) return 0f;
            float a;
            if (t < 0f) a = 0f;
            else if (t < 0.05f) a = t / 0.05f * 0.8f;
            else if (t < 0.14f) a = Mathf.Lerp(0.8f, 0.2f, (t - 0.05f) / 0.09f);
            else if (t < 0.2f) a = Mathf.Lerp(0.2f, 0.5f, (t - 0.14f) / 0.06f);
            else if (t < 0.75f) a = Mathf.Lerp(0.5f, 0f, (t - 0.2f) / 0.55f);
            else a = 0f;
            var c = flash.color;
            c.a = a;
            flash.color = c;
            return a / 0.8f;
        }

        private void UpdateRain(float dt, float amount)
        {
            if (drops == null) return;
            int liveFar = Mathf.RoundToInt(farDrops * amount), liveNear = Mathf.RoundToInt(nearDrops * amount);
            for (int i = 0; i < drops.Length; i++)
            {
                ref Drop d = ref drops[i];
                bool want = d.near ? i - farDrops < liveNear : i < liveFar;
                if (!d.live)
                {
                    if (!want) continue;
                    Spawn(ref d);
                }
                // 1/3 eğim: çizimdeki uzak yağmur perdeleriyle aynı yön (sağa yatık)
                d.pos.y -= d.speed * dt;
                d.pos.x += d.speed * dt / 3f;
                if (d.pos.y <= d.endY)
                {
                    if (d.near) Splash(new Vector2(d.pos.x, d.endY));
                    if (want) Spawn(ref d);
                    else
                    {
                        d.live = false;
                        d.rt.gameObject.SetActive(false);
                        continue;
                    }
                }
                d.rt.anchoredPosition = Snap(d.pos);
            }
            for (int i = 0; i < splashes.Length; i++)
            {
                if (splashAge[i] >= 1f) continue;
                splashAge[i] += dt / 0.14f;
                if (splashAge[i] >= 1f) splashes[i].gameObject.SetActive(false);
            }
        }

        private void Spawn(ref Drop d)
        {
            d.live = true;
            d.rt.gameObject.SetActive(true);
            d.speed = d.near ? Random.Range(470f, 560f) : Random.Range(300f, 360f);
            d.pos = new Vector2(Random.Range(-ArtW * 0.5f - 90f, ArtW * 0.5f), ArtH * 0.5f + Random.Range(4f, 60f));
            // Yakın damlalar yol/kaldırımda biter (çizimde y 116..180 → -52..-180), uzaktakiler ufukta kaybolur.
            d.endY = d.near ? Random.Range(-176f, -56f) : Random.Range(-60f, -40f);
        }

        private void Splash(Vector2 at)
        {
            var img = splashes[nextSplash];
            splashAge[nextSplash] = 0f;
            nextSplash = (nextSplash + 1) % splashes.Length;
            img.rectTransform.anchoredPosition = Snap(at);
            img.gameObject.SetActive(true);
        }

        /// <summary>Çizim pikseli = 2 referans birimi: yağmur da aynı ızgarada durur (yarım piksel kayması yok).</summary>
        private static Vector2 Snap(Vector2 p) => new Vector2(Mathf.Round(p.x / 2f) * 2f, Mathf.Round(p.y / 2f) * 2f);

        // ------------------------------------------------------------------ kodla üretilen küçük dokular (Point filtre)

        /// <summary>Tek piksel kalın yağmur çizgisi: xs[satır] o satırdaki piksel (satır 0 = alt uç, en parlak).</summary>
        private static Texture2D PixelTex(int w, int h, int[] xs)
        {
            var tex = NewTex(w, h);
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Lerp(1f, 0.45f, y / (float)(h - 1));
                tex.SetPixel(xs[y], y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D SplashTex()
        {
            var tex = NewTex(5, 2);
            tex.SetPixel(1, 0, Color.white); tex.SetPixel(2, 0, Color.white); tex.SetPixel(3, 0, Color.white);
            tex.SetPixel(0, 1, new Color(1f, 1f, 1f, 0.7f)); tex.SetPixel(4, 1, new Color(1f, 1f, 1f, 0.7f));
            tex.Apply();
            return tex;
        }

        /// <summary>Elips biçimli, dört basamakta kenara doğru azalan koyu hale (yumuşak geçiş değil, piksel kademeleri).</summary>
        private static Texture2D ScrimTex(int w, int h)
        {
            var tex = NewTex(w, h);
            var ink = FeUi.Ink;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f), dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = d < 0.45f ? 0.5f : d < 0.65f ? 0.38f : d < 0.82f ? 0.24f : d < 1f ? 0.11f : 0f;
                tex.SetPixel(x, y, new Color(ink.r, ink.g, ink.b, a));
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D NewTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var clear = new Color32[w * h];
            tex.SetPixels32(clear);
            return tex;
        }
    }
}
