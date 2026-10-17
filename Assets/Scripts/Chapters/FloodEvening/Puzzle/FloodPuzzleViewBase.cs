using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Bulmaca arayüzlerinin (kapı kolu, şifre paneli, ipucu kartı) ortak tabanı. Görünüm AYRI PREFAB'dadır
    /// (Assets/Prefabs/FloodEvening/UI/FE_*View.prefab): kök = Screen Space Overlay Canvas (640x360 referans) + bu bileşen.
    /// Prefab'daki hiyerarşi elle güzelleştirilebilir; bileşen yalnızca Inspector'da bağlı referansları ve metin alanlarını kullanır.
    /// Referanslar boşsa (prefab yok / yeni obje) Build() aynı düzeni kodla kurar.
    /// Mantık sınıfları (GateHandleMinigame, CodePanelMinigame, CodeClueSign) görünümü Resolve ile alır:
    /// sahnedeki örnek ise doğrudan, prefab asset'i ise ilk açılışta Instantiate, hiç yoksa kodla kurulmuş yedek.
    /// </summary>
    public abstract class FloodPuzzleViewBase : MonoBehaviour
    {
        [Header("Kök")]
        [SerializeField] protected Canvas canvas;
        [SerializeField] protected CanvasGroup group;
        [SerializeField] protected RectTransform panel;
        [Tooltip("Türkçe karakterli font. Boşsa Resources/Objectives/Nunito-Black.")]
        [SerializeField] protected Font font;

        private float appear;
        private bool wired;

        /// <summary>Canvas ölçeği (ekran pikseli / referans pikseli).</summary>
        public float ScaleFactor => canvas != null ? canvas.scaleFactor : 1f;

        public bool IsBuilt => canvas != null && panel != null;

        /// <summary>Referanslar boşsa düzeni kodla kurar (editörde prefab üretimi ve çalışma anı yedeği).</summary>
        public void EnsureBuilt()
        {
            if (IsBuilt) return;
            canvas = FloodPuzzleUi.SetupCanvas(gameObject, out group);
            Build(FloodPuzzleUi.ResolveFont(font), (RectTransform)canvas.transform);
        }

        protected abstract void Build(Font f, RectTransform root);

        /// <summary>Düğme dinleyicilerini bağlar (ilk Show'da bir kez; prefab'a kaydolmaz).</summary>
        protected abstract void WireButtons();

        protected static void Wire(UnityEngine.UI.Button b, System.Action a)
        {
            if (b == null) return;
            b.onClick.AddListener(() => { FloodPuzzleUi.ClearSelection(); a?.Invoke(); });
        }

        protected static void SetText(UnityEngine.UI.Text t, string s) { if (t != null) t.text = s ?? ""; }

        public virtual void Show()
        {
            EnsureBuilt();
            if (!wired) { wired = true; WireButtons(); }
            FloodPuzzleUi.EnsureEventSystem();
            appear = 0f;
            if (group != null) group.alpha = 0f;
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>Açılış animasyonu (gerçek zaman); her kare çağrılır.</summary>
        public void TickAppear(float unscaledDt)
        {
            appear = Mathf.Min(1f, appear + unscaledDt / 0.12f);
            float e = FloodPuzzleUi.SmoothStep01(appear);
            if (group != null) group.alpha = e;
            if (panel != null) panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, e);
        }

        /// <summary>
        /// Görünüm örneğini döndürür: sahnedeki obje → kendisi; prefab asset → owner altında Instantiate;
        /// null → owner altında yeni obje + T, kodla kurulur.
        /// </summary>
        public static T Resolve<T>(T assigned, ref T instance, Component owner, string fallbackName) where T : FloodPuzzleViewBase
        {
            if (instance != null) return instance;
            if (assigned != null && assigned.gameObject.scene.IsValid()) instance = assigned;
            else if (assigned != null) instance = Instantiate(assigned, owner.transform);
            else
            {
                var go = new GameObject(fallbackName);
                go.transform.SetParent(owner.transform, false);
                go.SetActive(false);
                instance = go.AddComponent<T>();
            }
            instance.EnsureBuilt();
            instance.Hide();
            return instance;
        }
    }
}
