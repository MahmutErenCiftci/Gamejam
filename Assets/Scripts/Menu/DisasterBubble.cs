using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace AfetUyanisi.Menu
{
    // Afet seçimindeki tek bir rüya bulutu (dünya uzayında sprite). Durumlar: gizli → beliriyor → bekliyor
    // (hafif yüzer) ↔ üzerine gelindi / seçili → çıkıyor. Seçili görünüm, parlayan ikinci sprite'ın
    // (turuncu/mavi hale) açılmasıyla verilir. Fare olayları için kamerada Physics2DRaycaster gerekir.
    [RequireComponent(typeof(Collider2D))]
    public class DisasterBubble : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] MenuChapterLoader.Chapter chapter;

        [Header("Görseller")]
        [Tooltip("Ölçeklenen ve yüzen kök (bulut + içindeki efektler).")]
        [SerializeField] Transform visualRoot;
        [SerializeField] SpriteRenderer art;
        [Tooltip("Seçiliyken görünen parlayan sürüm (DEPREM turuncu, SEL mavi).")]
        [SerializeField] SpriteRenderer glow;
        [Tooltip("Bulut görünürken çalışan iç efektler (yağmur, moloz). Gizliyken kapalı.")]
        [SerializeField] GameObject[] activeWhileVisible;

        [Header("Zamanlama")]
        [SerializeField] float appearDuration = 0.35f;
        [SerializeField] float exitDuration = 0.25f;
        [SerializeField] float hoverScale = 1.04f;
        [SerializeField] float selectedScale = 1.08f;
        [SerializeField] float hoverGlow = 0.45f;

        [Header("Yüzme")]
        [Tooltip("Piksel cinsinden yukarı-aşağı genlik.")]
        [SerializeField] int floatPixels = 2;
        [SerializeField] float floatPeriod = 2.4f;
        [SerializeField] float floatPhase;

        public UnityEvent onSelected;
        public event Action<DisasterBubble> Clicked;

        public MenuChapterLoader.Chapter Chapter => chapter;
        public bool IsVisible { get; private set; }
        public bool IsSelected { get; private set; }

        const float PixelsPerUnit = 64f;
        Vector3 basePosition;
        float appear;          // 0 gizli, 1 tam görünür
        bool hovered;
        float currentScale = 1f;
        Coroutine routine;

        void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;
            basePosition = visualRoot.localPosition;
            SetVisibleImmediate(false);
        }

        public void Show(float delay)
        {
            if (routine != null) StopCoroutine(routine);
            gameObject.SetActive(true);
            routine = StartCoroutine(AppearRoutine(delay));
        }

        public void Hide()
        {
            if (!gameObject.activeInHierarchy) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ExitRoutine());
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selected) onSelected?.Invoke();
            ApplyGlow();
        }

        public void SetVisibleImmediate(bool visible)
        {
            IsVisible = visible;
            appear = visible ? 1f : 0f;
            hovered = false;
            IsSelected = false;
            SetEffects(visible);
            ApplyVisuals();
            if (!visible) gameObject.SetActive(false);
        }

        IEnumerator AppearRoutine(float delay)
        {
            appear = 0f;
            ApplyVisuals();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            IsVisible = true;
            SetEffects(true);
            for (float t = 0f; t < appearDuration; t += Time.unscaledDeltaTime)
            {
                appear = t / appearDuration;
                ApplyVisuals();
                yield return null;
            }
            appear = 1f;
            ApplyVisuals();
            routine = null;
        }

        IEnumerator ExitRoutine()
        {
            IsVisible = false;
            hovered = false;
            IsSelected = false;
            float start = appear;
            for (float t = 0f; t < exitDuration; t += Time.unscaledDeltaTime)
            {
                appear = Mathf.Lerp(start, 0f, t / exitDuration);
                ApplyVisuals();
                yield return null;
            }
            SetVisibleImmediate(false);
            routine = null;
        }

        // Sadece görünürken çalışır; bulut başına tek sinüs, ayırma yok.
        void Update()
        {
            if (appear <= 0f) return;
            float target = IsSelected ? selectedScale : hovered ? hoverScale : 1f;
            currentScale = Mathf.MoveTowards(currentScale, target, Time.unscaledDeltaTime * 0.8f);
            ApplyVisuals();
        }

        void ApplyVisuals()
        {
            // Belirirken küçükten büyür (hafif taşma), çıkarken söner.
            float pop = appear < 1f ? Mathf.Sin(appear * Mathf.PI * 0.5f) * (1f + 0.08f * Mathf.Sin(appear * Mathf.PI)) : 1f;
            visualRoot.localScale = Vector3.one * (pop * currentScale);

            float offset = floatPeriod > 0f ? Mathf.Sin((Time.unscaledTime / floatPeriod + floatPhase) * Mathf.PI * 2f) * floatPixels : 0f;
            // Piksel ızgarasına oturt: yarım piksel kaymalar titreme yaratır.
            visualRoot.localPosition = basePosition + new Vector3(0f, Mathf.Round(offset) / PixelsPerUnit, 0f);

            SetAlpha(art, appear);
            ApplyGlow();
        }

        void ApplyGlow()
        {
            if (glow == null) return;
            float g = IsSelected ? 1f : hovered ? hoverGlow : 0f;
            SetAlpha(glow, g * appear);
        }

        void SetEffects(bool on)
        {
            if (activeWhileVisible == null) return;
            foreach (GameObject effect in activeWhileVisible)
                if (effect != null) effect.SetActive(on);
        }

        static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }

        public void OnPointerEnter(PointerEventData eventData) { if (IsVisible) { hovered = true; ApplyGlow(); } }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; ApplyGlow(); }
        public void OnPointerClick(PointerEventData eventData) { if (IsVisible) Clicked?.Invoke(this); }
    }
}
