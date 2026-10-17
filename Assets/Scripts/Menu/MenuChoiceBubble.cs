using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfetUyanisi.Menu
{
    /// <summary>
    /// Cenk'in kafasından yükselen rüya bulutlarından biri (UI). Açık bulut (Deprem, Sel) seçilince altın hale
    /// ve 2 px yükselme alır, etiket plakası amber olur. Kilitli bulut gri durur, tıklanınca hafifçe sallanır.
    /// Bütün kaymalar tam piksel: bulanık ölçekleme yok.
    /// </summary>
    public class MenuChoiceBubble : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private MenuChapterLoader.Chapter chapter;
        [SerializeField] private bool locked;

        [Header("Görseller")]
        [Tooltip("Yüzen kök (bulut + etiket).")]
        [SerializeField] private RectTransform visual;
        [SerializeField] private Image glow;
        [SerializeField] private Image labelPlate;
        [SerializeField] private Text label;
        [SerializeField] private Sprite plateIdle;
        [SerializeField] private Sprite plateSelected;
        [SerializeField] private Color labelIdle = new Color32(0xF4, 0xEB, 0xD8, 0xFF);
        [SerializeField] private Color labelSelected = new Color32(0x14, 0x14, 0x1F, 0xFF);

        [Header("Hareket")]
        [SerializeField] private float floatPeriod = 2.6f;
        [SerializeField] private float floatPhase;
        [SerializeField] private int floatPixels = 1;
        [SerializeField] private int selectedLift = 2;

        public event Action<MenuChoiceBubble> Hovered;
        public event Action<MenuChoiceBubble> Clicked;

        public MenuChapterLoader.Chapter Chapter => chapter;
        public bool Locked => locked;
        public bool Selected { get; private set; }

        private Vector2 basePos;
        private float nudgeTime = -1f;

        private void Awake()
        {
            if (visual == null) visual = (RectTransform)transform;
            basePos = visual.anchoredPosition;
            Apply();
        }

        public void SetSelected(bool selected)
        {
            Selected = selected;
            Apply();
        }

        /// <summary>Kilitli buluta tıklanınca kısa yatay sallanma.</summary>
        public void Nudge() => nudgeTime = Time.unscaledTime;

        private void Apply()
        {
            if (glow != null) glow.enabled = Selected;
            if (labelPlate != null && plateIdle != null)
                labelPlate.sprite = Selected && plateSelected != null ? plateSelected : plateIdle;
            if (label != null) label.color = Selected ? labelSelected : labelIdle;
        }

        private void Update()
        {
            float y = floatPeriod > 0f
                ? Mathf.Round(Mathf.Sin((Time.unscaledTime / floatPeriod + floatPhase) * Mathf.PI * 2f) * floatPixels)
                : 0f;
            if (Selected) y += selectedLift;

            float x = 0f;
            if (nudgeTime >= 0f)
            {
                float t = Time.unscaledTime - nudgeTime;
                if (t > 0.3f) nudgeTime = -1f;
                else x = Mathf.Round(Mathf.Sin(t * 50f) * 2f * (1f - t / 0.3f));
            }
            visual.anchoredPosition = basePos + new Vector2(x, y);
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Clicked?.Invoke(this);
        }
    }
}
