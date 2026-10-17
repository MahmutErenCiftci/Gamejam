using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// İnceleme kartı (maket: S/eqD_mock_extra.png): lacivert başlık bandında tür ikonu + başlık, krem gövdede açıklama,
    /// "[E] Kapat". Engeller; E / Space / Enter / tık ile kapanır. Yükseklik metne göre ayarlanır.
    /// Uyarı toast'ı: üst ortada kırmızı şerit ("CAM TEHLİKESİ!"), kayarak gelir, süre sonunda kaybolur, engellemez.
    /// Arayüzü kodla kurar (Canvas sıralaması 88).
    /// </summary>
    public class InfoCardView : InfoCardViewBase
    {
        [SerializeField] private EarthquakeUISkin skin;
        [SerializeField] private int sortingOrder = 88;
        [Tooltip("Kart açıkken arka planın kararma oranı (0 = yok, 1 = tam karartma sprite'ı).")]
        [SerializeField, Range(0f, 1f)] private float dimAmount = 0.35f;
        [Tooltip("Kartın sol-üst köşesi (640x360 referans pikseli).")]
        [SerializeField] private Vector2 cardPosition = new Vector2(17f, 71f);
        [SerializeField] private float cardWidth = 230f;
        [SerializeField] private string closeHint = "[E] Kapat";

        private const float BodyTop = 24f;
        private const float ToastW = 260f, ToastMinH = 32f;
        private const float ToastRestY = -10f;

        private bool built;
        private Font font;
        private Canvas canvas;
        private CanvasGroup group;

        // Kart
        private RectTransform cardRoot;
        private CanvasGroup cardGroup;
        private Image dim;
        private Image cardImage;
        private Image kindIcon;
        private Text kindText, titleText, bodyText, hintText;
        private EqButton cardButton;
        private bool cardClicked;
        private bool cardOpen;

        /// <summary>Bilgi kartı ekranda mı? (HUD "görev tamamlandı" toast'ı kart kapanana kadar bekler.)</summary>
        public bool IsCardOpen => cardOpen;

        // Toast
        private RectTransform toastRoot;
        private CanvasGroup toastGroup;
        private Text toastTitle, toastBody;
        private Image toastIcon;
        private Coroutine toastRoutine;

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;
            skin = EqUi.ResolveSkin(this, skin);
            font = EqUi.ResolveFont(skin);
            canvas = EqUi.CreateCanvas(this, "EQ_InfoCanvas", sortingOrder, out group);
            BuildCard();
            BuildToast();
        }

        private void BuildCard()
        {
            cardRoot = EqUi.Stretch(EqUi.Rect("Card", canvas.transform));
            cardGroup = cardRoot.gameObject.AddComponent<CanvasGroup>();

            dim = EqUi.DimImage(cardRoot, skin);

            cardImage = EqUi.Img("InfoCard", cardRoot, EqUi.S(skin, s => s.InfoCard), EqUi.Ink, skin, true);
            EqUi.TL(cardImage.rectTransform, cardPosition.x, cardPosition.y, cardWidth, 70f);
            EqUi.Fallback(cardImage, EqUi.Ink, EqUi.Cream, skin, 1f, 17f, EqUi.Navy);
            var panel = cardImage.rectTransform;

            kindIcon = EqUi.Img("KindIcon", panel, null, EqUi.Cream, skin);
            EqUi.TL(kindIcon.rectTransform, 5f, 2f, 16f, 16f);

            titleText = EqUi.Txt("Title", panel, font, 9, EqUi.Cream, TextAnchor.MiddleLeft, EqUi.Ink);
            EqUi.TopBand(titleText.rectTransform, 26f, 60f, 2f, 15f);
            EqUi.BestFit(titleText, 7);

            kindText = EqUi.Txt("Kind", panel, font, 6, EqUi.Amber, TextAnchor.MiddleRight, EqUi.Ink);
            EqUi.TopBand(kindText.rectTransform, 120f, 7f, 2f, 15f);

            bodyText = EqUi.Txt("Body", panel, font, 7, EqUi.Ink, TextAnchor.UpperLeft);
            EqUi.TopBand(bodyText.rectTransform, 8f, 8f, BodyTop, 30f);
            bodyText.lineSpacing = 1.05f;

            hintText = EqUi.Txt("Hint", panel, font, 6, EqUi.Muted, TextAnchor.LowerRight);
            var hRt = hintText.rectTransform;
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.pivot = new Vector2(0.5f, 0f);
            hRt.offsetMin = new Vector2(8f, 5f);
            hRt.offsetMax = new Vector2(-8f, 14f);
            hintText.text = closeHint;

            cardButton = cardImage.gameObject.AddComponent<EqButton>();
            cardButton.Init();
            cardButton.Clicked += () => cardClicked = true;

            cardRoot.gameObject.SetActive(false);
        }

        private void BuildToast()
        {
            var img = EqUi.Img("Toast", canvas.transform, EqUi.S(skin, s => s.Toast), EqUi.Ink, skin);
            toastRoot = img.rectTransform;
            EqUi.Place(toastRoot, 0.5f, 1f, new Vector2(0f, 50f), new Vector2(ToastW, ToastMinH));
            if (img.sprite == null)
            {
                EqUi.Fallback(img, EqUi.Ink, EqUi.Red, skin);
                var badge = EqUi.Img("Badge", toastRoot, null, EqUi.Cream, skin);
                EqUi.TL(badge.rectTransform, 3f, 3f, 18f, 16f);
            }
            toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;

            toastIcon = EqUi.Img("Icon", toastRoot, EqUi.S(skin, s => s.IconHazard), EqUi.Red, skin);
            EqUi.TL(toastIcon.rectTransform, 4f, 3f, 16f, 16f);
            if (toastIcon.sprite == null)
            {
                EqUi.TL(toastIcon.rectTransform, 6f, 5f, 12f, 12f);
                var ex = EqUi.Txt("Mark", toastIcon.transform, font, 9, EqUi.Cream, TextAnchor.MiddleCenter);
                ex.text = "!";
            }

            toastTitle = EqUi.Txt("Title", toastRoot, font, 9, EqUi.White, TextAnchor.MiddleLeft, EqUi.ShadowRed);
            EqUi.TopBand(toastTitle.rectTransform, 25f, 6f, 3f, 12f);
            EqUi.BestFit(toastTitle, 7);
            toastBody = EqUi.Txt("Body", toastRoot, font, 6, EqUi.Cream, TextAnchor.UpperLeft, EqUi.ShadowRed);
            EqUi.TopBand(toastBody.rectTransform, 25f, 6f, 15f, 10f);

            toastRoot.gameObject.SetActive(false);
        }

        public override IEnumerator ShowCard(string title, string body, HazardClass kind)
        {
            EnsureBuilt();
            while (cardOpen) yield return null; // iç içe çağrı: öncekini bekle
            cardOpen = true;

            Sprite ks = skin != null ? skin.KindIcon(kind) : null;
            EqUi.Skin(kindIcon, ks, KindColor(kind), skin);
            EqUi.TL(kindIcon.rectTransform, ks != null ? 5f : 8f, ks != null ? 2f : 5f, ks != null ? 16f : 10f, ks != null ? 16f : 10f);
            kindText.text = KindLabel(kind);
            kindText.color = KindColor(kind) == EqUi.Red ? EqUi.Hex(0xFF7A62) : KindColor(kind);
            titleText.text = title ?? "";
            bodyText.text = body ?? "";

            // Yükseklik: başlık bandı + metin + ipucu
            float w = cardWidth - 16f;
            float bodyH = Mathf.Ceil(EqUi.PreferredHeight(bodyText, bodyText.text, w)) + 2f;
            EqUi.TopBand(bodyText.rectTransform, 8f, 8f, BodyTop, bodyH);
            float h = Mathf.Clamp(BodyTop + bodyH + 18f, 64f, 300f);
            EqUi.TL(cardImage.rectTransform, cardPosition.x, cardPosition.y, cardWidth, h);

            cardClicked = false;
            cardButton.Interactable = false;
            cardRoot.gameObject.SetActive(true);
            var panel = cardImage.rectTransform;
            Vector2 rest = panel.anchoredPosition;
            for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                float k = EqUi.OutCubic(t / 0.18f);
                cardGroup.alpha = k;
                EqUi.SetDim(dim, dimAmount * k);
                panel.anchoredPosition = EqUi.Snap(rest + new Vector2(-12f * (1f - k), 0f));
                yield return null;
            }
            cardGroup.alpha = 1f;
            EqUi.SetDim(dim, dimAmount);
            panel.anchoredPosition = rest;

            cardButton.Interactable = true;
            int openFrame = Time.frameCount;
            float wait = 0f;
            float pulse = 0f;
            while (true)
            {
                wait += Time.unscaledDeltaTime;
                pulse += Time.unscaledDeltaTime;
                hintText.color = Color.Lerp(EqUi.Muted, EqUi.Navy, 0.5f + 0.5f * Mathf.Sin(pulse * 4f));
                if (cardClicked) break;
                // Kartı açan E basışı aynı karede kartı kapatmasın.
                if (Time.frameCount > openFrame && wait > 0.2f && GameInput.ContinuePressed) break;
                yield return null;
            }
            cardButton.Interactable = false;

            for (float t = 0f; t < 0.14f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / 0.14f;
                cardGroup.alpha = k;
                yield return null;
            }
            cardRoot.gameObject.SetActive(false);
            cardOpen = false;
            // Kapatan tuş aynı karede başka bir etkileşimi tetiklemesin.
            yield return null;
        }

        public override void ShowToast(string title, string body, float seconds)
        {
            EnsureBuilt();
            toastTitle.text = title ?? "";
            toastBody.text = body ?? "";
            float bodyH = string.IsNullOrEmpty(body) ? 0f : Mathf.Ceil(EqUi.PreferredHeight(toastBody, toastBody.text, ToastW - 31f)) + 1f;
            EqUi.TopBand(toastBody.rectTransform, 25f, 6f, 15f, Mathf.Max(10f, bodyH));
            toastRoot.sizeDelta = new Vector2(ToastW, Mathf.Max(ToastMinH, 15f + bodyH + 8f));

            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = null;
            if (!isActiveAndEnabled)
            {
                toastRoot.gameObject.SetActive(true);
                toastRoot.anchoredPosition = new Vector2(0f, ToastRestY);
                toastGroup.alpha = 1f;
                return;
            }
            toastRoutine = StartCoroutine(ToastRoutine(Mathf.Max(0.8f, seconds)));
        }

        private IEnumerator ToastRoutine(float seconds)
        {
            bool wasShown = toastRoot.gameObject.activeSelf;
            float hidden = toastRoot.sizeDelta.y + 6f;
            float fromY = wasShown ? toastRoot.anchoredPosition.y : hidden;
            if (!wasShown) toastGroup.alpha = 0f;
            toastRoot.gameObject.SetActive(true);
            float a0 = toastGroup.alpha;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = EqUi.OutCubic(t / 0.25f);
                toastRoot.anchoredPosition = EqUi.Snap(new Vector2(0f, Mathf.Lerp(fromY, ToastRestY, k)));
                toastGroup.alpha = Mathf.Lerp(a0, 1f, k);
                yield return null;
            }
            toastRoot.anchoredPosition = new Vector2(0f, ToastRestY);
            toastGroup.alpha = 1f;

            // İkonda yumuşak nabız (yanıp sönme yok)
            var iconRt = toastIcon.rectTransform;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float s = 1f + 0.08f * Mathf.Sin(t * Mathf.PI * 2f * 0.9f);
                iconRt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            iconRt.localScale = Vector3.one;

            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.25f;
                toastRoot.anchoredPosition = EqUi.Snap(new Vector2(0f, Mathf.Lerp(ToastRestY, hidden, EqUi.InCubic(k))));
                toastGroup.alpha = 1f - k;
                yield return null;
            }
            toastRoot.gameObject.SetActive(false);
            toastRoutine = null;
        }

        private static Color KindColor(HazardClass kind)
        {
            switch (kind)
            {
                case HazardClass.Hazard: return EqUi.Red;
                case HazardClass.Protective: return EqUi.Green;
                case HazardClass.Safety: return EqUi.Green;
                case HazardClass.Exit: return EqUi.Amber;
                default: return EqUi.Blue;
            }
        }

        private static string KindLabel(HazardClass kind)
        {
            switch (kind)
            {
                case HazardClass.Hazard: return "TEHLİKE";
                case HazardClass.Protective: return "KORUNMA YERİ";
                case HazardClass.Safety: return "GÜVENLİK";
                case HazardClass.Exit: return "ÇIKIŞ";
                default: return "BİLGİ";
            }
        }
    }
}
