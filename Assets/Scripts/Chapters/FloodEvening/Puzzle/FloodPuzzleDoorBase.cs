using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Kalıcı açılan kapı (bahçe kapısı, şifreli giriş). ENV çapasına eklenir: `Closed` / `Open` çocukları ve kapalıyken
    /// yolu kesen collider'lar. Açılınca Open açık, Closed ve yol kesen collider'lar kapalı, [E] noktası kullanılamaz,
    /// Hub.Complete(StepId) çağrılır. ResetModule kapıyı tekrar kapatır.
    /// </summary>
    public abstract class FloodPuzzleDoorBase : FloodPuzzleInteractionBase
    {
        [Header("Kapı görseli / yol kesici")]
        [Tooltip("Kapalı kapı görseli. Boşsa \"Closed\" adlı çocuk.")]
        [SerializeField] private GameObject closedVisual;
        [Tooltip("Açık kapı görseli. Boşsa \"Open\" adlı çocuk.")]
        [SerializeField] private GameObject openVisual;
        [Tooltip("Kapalıyken yolu kesen collider'lar. Boşsa: bu objedeki, Closed altındaki ve \"Blocker\" adlı çocuktaki trigger olmayan Collider2D'ler.")]
        [SerializeField] private Collider2D[] blockers = Array.Empty<Collider2D>();

        /// <summary>Kapı kalıcı olarak açıldığında (bir kez).</summary>
        public event Action DoorOpened;

        /// <summary>Kapı açık mı (kalıcı).</summary>
        public bool Opened { get; private set; }

        public override bool IsDone => Opened;

        /// <summary>Hub.Complete'e verilen adım ("gate_opened", "code_opened").</summary>
        protected abstract string StepId { get; }

        protected override void Awake()
        {
            base.Awake();
            ResolveParts();
            ApplyDoorState(false);
        }

        private void ResolveParts()
        {
            if (closedVisual == null) { var t = transform.Find("Closed"); if (t != null) closedVisual = t.gameObject; }
            if (openVisual == null) { var t = transform.Find("Open"); if (t != null) openVisual = t.gameObject; }
            if (blockers == null || blockers.Length == 0)
            {
                var list = new List<Collider2D>();
                foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) list.Add(c);
                if (closedVisual != null)
                    foreach (var c in closedVisual.GetComponentsInChildren<Collider2D>(true)) if (!c.isTrigger && !list.Contains(c)) list.Add(c);
                var b = transform.Find("Blocker");
                if (b != null)
                    foreach (var c in b.GetComponentsInChildren<Collider2D>(true)) if (!c.isTrigger && !list.Contains(c)) list.Add(c);
                blockers = list.ToArray();
            }
        }

        private void ApplyDoorState(bool open)
        {
            SetActive(closedVisual, !open);
            SetActive(openVisual, open);
            if (blockers != null)
                foreach (var c in blockers) if (c != null) c.enabled = !open;
        }

        /// <summary>Kapıyı kalıcı açar, noktayı kapatır, adımı Hub'a bildirir. İkinci çağrı etkisiz.</summary>
        protected void OpenDoorPermanently()
        {
            if (Opened) return;
            Opened = true;
            ApplyDoorState(true);
            if (Point != null) Point.SetAvailable(false);
            Debug.Log($"[{GetType().Name}] {name} açıldı → Hub.Complete(\"{StepId}\")", this);
            FloodEveningHub.Complete(StepId);
            DoorOpened?.Invoke();
        }

        protected override void ResetState()
        {
            Opened = false;
            ApplyDoorState(false);
            if (Point != null) Point.SetAvailable(true);
        }
    }
}
