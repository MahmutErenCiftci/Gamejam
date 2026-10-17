using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel arayüzünde belli süre görünüp kaybolan kutuların ortak tabanı ("Yol açıldı.", görev şeridi, kısa yol
    /// uyarısı): kendi canvas'ını kurar, taşarak/sekerek kayıp girer (EntryEase), süre dolunca (seconds &lt;= 0 ise Hide'a
    /// kadar) solarak çıkar. Alt sınıf Animate içinde Jolt ile sarsıntı/zıplama ekler, Age ile zamanlar.
    /// Genişlik canvas'a göre kısılır (dar ekranda kenarlardan 16 px pay). Süreler gerçek zamanlıdır (Time.timeScale'den bağımsız).
    /// </summary>
    public abstract class FeTimedBanner : MonoBehaviour
    {
        [SerializeField] protected Font font;
        [SerializeField] protected int sortingOrder = 62;
        [Tooltip("Kutunun çapası (0..1): (0.5, 1) üst orta, (0.5, 0.5) ekran ortası.")]
        [SerializeField] protected Vector2 anchor = new Vector2(0.5f, 1f);
        [Tooltip("Çapaya göre konum (640x360 referans pikseli).")]
        [SerializeField] protected Vector2 offset = new Vector2(0f, -8f);
        [Tooltip("Girişte kayma (piksel; + yukarıdan iner, - aşağıdan çıkar).")]
        [SerializeField] protected float slideFrom = 10f;

        private const float FadeIn = 0.35f, FadeOut = 0.22f;

        private bool built;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform canvasRect;
        private float fadeK, remaining, lastWidth = -1f;
        private bool showing, timed;

        protected RectTransform Root { get; private set; }
        /// <summary>Tasarım genişliği (canvas daha darsa kısılır).</summary>
        protected abstract Vector2 Size { get; }
        /// <summary>Show(text) için süre belirtilmezse kullanılan süre.</summary>
        protected abstract float DefaultSeconds { get; }

        public bool IsVisible => built && (showing || fadeK > 0f);

        private void Awake() => EnsureBuilt();

        private void OnDisable()
        {
            if (!built) return;
            showing = false;
            fadeK = 0f;
            Apply();
            canvas.gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (!built) return;
            showing = false;
        }

        /// <summary>Kutuyu açar; seconds &lt;= 0 ise Hide çağrılana kadar kalır. Açıkken çağrılırsa süre yenilenir.</summary>
        protected void Open(float seconds)
        {
            EnsureBuilt();
            if (!isActiveAndEnabled) return;
            canvas.gameObject.SetActive(true);
            showing = true;
            timed = seconds > 0f;
            remaining = seconds;
            Age = 0f;
            Jolt = Vector2.zero;
            OnOpened();
            Apply();
        }

        protected void EnsureBuilt()
        {
            if (built) return;
            built = true;
            if (font == null) font = FeUi.DefaultFont();
            canvas = FePixel.CreateCanvas(transform, name + "Canvas", sortingOrder, out group);
            canvasRect = (RectTransform)canvas.transform;
            group.blocksRaycasts = false;
            group.interactable = false;
            Root = FeUi.Rect("Box", canvas.transform);
            FeUi.Place(Root, anchor.x, anchor.y, offset, Size);
            BuildContent(Root);
            fadeK = 0f;
            Apply();
            canvas.gameObject.SetActive(false);
        }

        /// <summary>İçeriği Root altına kurar (bir kez).</summary>
        protected abstract void BuildContent(RectTransform root);

        /// <summary>Her açılışta (animasyon sayaçlarını sıfırlamak için).</summary>
        protected virtual void OnOpened() { }

        /// <summary>Görünürken her kare (ikon sallanması vb.).</summary>
        protected virtual void Animate(float dt) { }

        private void Update()
        {
            if (!built || (!showing && fadeK <= 0f)) return;
            float dt = Time.unscaledDeltaTime;
            if (showing && timed)
            {
                remaining -= dt;
                if (remaining <= 0f) showing = false;
            }
            fadeK = showing ? Mathf.Min(1f, fadeK + dt / FadeIn) : Mathf.Max(0f, fadeK - dt / FadeOut);
            if (!Mathf.Approximately(canvasRect.rect.width, lastWidth)) Layout();
            Age += dt;
            Animate(dt);
            Apply();
            if (!showing && fadeK <= 0f) canvas.gameObject.SetActive(false);
        }

        private void Layout()
        {
            lastWidth = canvasRect.rect.width;
            float w = lastWidth > 0f ? Mathf.Round(Mathf.Min(Size.x, lastWidth - 32f)) : Size.x;
            Root.sizeDelta = new Vector2(w, Size.y);
        }

        private void Apply()
        {
            group.alpha = Mathf.Clamp01(fadeK * 2f);
            // Girişte EntryEase ile (taşarak/sekerek) yerine oturur, çıkışta düz kayar. Jolt: sarsıntı/zıplama eki.
            float k = showing ? EntryEase(fadeK) : fadeK;
            Root.anchoredPosition = new Vector2(Mathf.Round(offset.x + Jolt.x), Mathf.Round(offset.y + (1f - k) * slideFrom + Jolt.y));
        }

        /// <summary>Giriş eğrisi (0→1). Varsayılan hafif taşma.</summary>
        protected virtual float EntryEase(float t) => FePixel.OutBack(t);

        /// <summary>Kutunun anlık ek kayması (piksel); Animate içinde ayarlanır.</summary>
        protected Vector2 Jolt { get; set; }

        /// <summary>Açıldığından beri geçen süre (sn, gerçek zaman).</summary>
        protected float Age { get; private set; }
    }
}
