using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Fermuar tutamağı: basılı tutulup çantanın ağzı boyunca (hafif kavisli yatay yol) sürüklenir.
    /// İlerleme 0 (açık) … 1 (kapalı). Tutamak imleci yol üzerinde izler; bırakınca ilerleme korunur (yeniden tutulabilir).
    /// Yol, track RectTransform'unun yerel koordinatlarında start → end; arc &gt; 0 ise ortası yukarı doğru kavislenir.
    /// </summary>
    public class EmergencyBagZipperController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform track;
        private RectTransform handle;
        private Vector2 start;
        private Vector2 end;
        private float arc;

        public float Progress { get; private set; }
        public bool Held { get; private set; }
        public bool Interactable { get; set; }

        /// <summary>Tutamak tutulunca.</summary>
        public event Action Grabbed;
        /// <summary>İlerleme değişince (0..1).</summary>
        public event Action<float> ProgressChanged;
        /// <summary>Tutamak bırakılınca (ilerleme ile).</summary>
        public event Action<float> Released;

        public void Configure(RectTransform trackRect, RectTransform handleRect, Vector2 startLocal, Vector2 endLocal, float arcHeight)
        {
            track = trackRect;
            handle = handleRect;
            start = startLocal;
            end = endLocal;
            arc = arcHeight;
            SetProgress(Progress, false);
        }

        /// <summary>Yol üzerindeki nokta (track yerel).</summary>
        public Vector2 PointAt(float t)
        {
            t = Mathf.Clamp01(t);
            var p = Vector2.Lerp(start, end, t);
            p.y += arc * Mathf.Sin(t * Mathf.PI);
            return p;
        }

        public void SetProgress(float value, bool notify = true)
        {
            float v = Mathf.Clamp01(value);
            bool changed = !Mathf.Approximately(v, Progress);
            Progress = v;
            if (handle != null) handle.anchoredPosition = EqUi.Snap(PointAt(v));
            if (changed && notify) ProgressChanged?.Invoke(v);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable || eventData.button != PointerEventData.InputButton.Left) return;
            Held = true;
            Grabbed?.Invoke();
            Follow(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Interactable && !Held && eventData.button == PointerEventData.InputButton.Left)
            {
                Held = true;
                Grabbed?.Invoke();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Held && Interactable) Follow(eventData);
        }

        public void OnEndDrag(PointerEventData eventData) => Release();

        public void OnPointerUp(PointerEventData eventData) => Release();

        /// <summary>Mini oyun kesilince: tutma bırakılır, olay tetiklenmez.</summary>
        public void Cancel() => Held = false;

        private void Release()
        {
            if (!Held) return;
            Held = false;
            Released?.Invoke(Progress);
        }

        private void Follow(PointerEventData eventData)
        {
            if (track == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out var local))
                return;
            float span = end.x - start.x;
            float t = Mathf.Abs(span) < 0.001f ? 0f : (local.x - start.x) / span;
            SetProgress(t);
        }

        private void OnDisable() => Held = false;
    }
}
