using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Bahçe kapısı, şifre paneli ve ipucu levhasının ortak iskeleti (IFloodEveningInteraction).
    /// Controller Point'i kendi RoomInteractor'ına kaydeder, E'ye basılınca Play()'i bekler; modül E'yi KENDİ dinlemez.
    /// Play: oyuncu hareketini kilitler (önceki değeri saklar) → Hub.NotifyOpened(id) → panel açılır → ilk kare girdi
    /// okunmaz → her kare Tick → Esc/Kapat/uzaklaşma/Interrupt ile ya da iş bitince kapanır → Hub.NotifyClosed(id) →
    /// hareket eski değerine döner. Play'i çalıştıran coroutine dışarıdan durdurulursa Update'teki bekçi paneli kapatır.
    /// Hub.ResetRequested ve ResetModule modülü baştaki durumuna döndürür.
    /// </summary>
    public abstract class FloodPuzzleInteractionBase : MonoBehaviour, IFloodEveningInteraction, IFloodEveningResettable
    {
        [Header("[E] noktası")]
        [Tooltip("Boşsa bu objedeki (ya da çocuklarındaki) Interactable kullanılır; o da yoksa çalışma anında eklenir.")]
        [SerializeField] protected Interactable point;
        [Tooltip("İpucu yazısı (\"[E] ...\"). Boşsa modülün varsayılanı.")]
        [SerializeField] private string actionLabel = "";
        [Tooltip("Boşsa sahnedeki IsoPlayerController.")]
        [SerializeField] protected IsoPlayerController player;
        [Tooltip("Oyuncu noktadan bu zemin uzaklığından fazla uzaklaşırsa panel iptal edilir (kontrol geri verilir).")]
        [SerializeField, Min(0.5f)] private float cancelDistance = 1.8f;

        private bool interruptRequested;
        private int lastTickFrame;
        private bool hadPlayerLock;
        private bool previousCanMove;
        private IsoPlayerController lockedPlayer;

        /// <summary>Panel şu an açık mı (Play sürüyor).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Modülün işi bitti mi (kapı açıldı). İpucu levhası için hep false (tekrar okunabilir).</summary>
        public abstract bool IsDone { get; }

        /// <summary>Hub'a bildirilen mini oyun kimliği ("gate", "code", "clue").</summary>
        protected abstract string HubId { get; }

        protected abstract string DefaultLabel { get; }

        public Interactable Point
        {
            get
            {
                EnsurePoint();
                return point;
            }
        }

        public virtual bool CanUse => isActiveAndEnabled && !IsOpen && !IsDone;

        protected string Label => string.IsNullOrEmpty(actionLabel) ? DefaultLabel : actionLabel;

        protected virtual void Awake()
        {
            EnsurePoint();
        }

        protected virtual void OnEnable()
        {
            FloodEveningHub.ResetRequested += ResetModule;
        }

        protected virtual void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            if (IsOpen) Finish("obje kapandı");
        }

        protected virtual void Update()
        {
            // Play'i çalıştıran coroutine durdurulduysa (StopCoroutine finally çalıştırmaz) paneli burada kapat.
            if (IsOpen && Time.frameCount - lastTickFrame > 3) Finish("Play coroutine'i durdu");
        }

        private void EnsurePoint()
        {
            if (point == null) point = GetComponent<Interactable>();
            if (point == null) point = GetComponentInChildren<Interactable>(true);
            if (point == null) point = gameObject.AddComponent<Interactable>();
            if (string.IsNullOrEmpty(point.ActionLabel) || !string.IsNullOrEmpty(actionLabel) || point.ActionLabel == DefaultLabel)
                point.Setup(Label, "", false, false);
        }

        /// <summary>Etiketi çalışma anında değiştirir (ör. "İpucunu oku (1/3)").</summary>
        protected void RefreshLabel() => point?.Setup(Label, "", false, false);

        public IEnumerator Play()
        {
            if (IsOpen || !CanUse) yield break;

            IsOpen = true;
            interruptRequested = false;
            lastTickFrame = Time.frameCount;
            LockPlayer();
            FloodEveningHub.NotifyOpened(HubId);
            Debug.Log($"[{GetType().Name}] {name} açıldı", this);
            ShowView();

            yield return null; // açan E tuşu aynı karede paneli kapatmasın / işlem yapmasın

            try
            {
                while (IsOpen)
                {
                    lastTickFrame = Time.frameCount;
                    if (interruptRequested) { Finish("Interrupt"); break; }
                    if (PlayerWalkedAway()) { Finish("oyuncu uzaklaştı"); break; }
                    if (!Tick(Time.deltaTime, Time.unscaledDeltaTime)) { Finish("kapandı"); break; }
                    yield return null;
                }
            }
            finally
            {
                if (IsOpen) Finish("Play bitti");
            }
        }

        /// <summary>Açık paneli tamamlamadan kapatır (ör. süre doldu, bölüm sonu). İlerleme korunur.</summary>
        public void Interrupt()
        {
            if (!IsOpen) return;
            interruptRequested = true;
        }

        /// <summary>Modülü baştaki duruma döndürür (kapı kapalı, kod boş, ilerleme 0).</summary>
        public void ResetModule()
        {
            if (IsOpen) Finish("sıfırlama");
            ResetState();
        }

        /// <summary>Her kare (panel açıkken). false dönerse panel kapanır.</summary>
        protected abstract bool Tick(float dt, float unscaledDt);

        protected abstract void ShowView();

        protected abstract void HideView();

        protected abstract void ResetState();

        private void Finish(string reason)
        {
            if (!IsOpen) return;
            IsOpen = false;
            HideView();
            FloodPuzzleUi.ClearSelection();
            UnlockPlayer();
            Debug.Log($"[{GetType().Name}] {name} kapandı ({reason})", this);
            FloodEveningHub.NotifyClosed(HubId);
        }

        private IsoPlayerController ResolvePlayer()
        {
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            return player;
        }

        private void LockPlayer()
        {
            lockedPlayer = ResolvePlayer();
            hadPlayerLock = lockedPlayer != null;
            if (!hadPlayerLock) return;
            previousCanMove = lockedPlayer.CanMove;
            lockedPlayer.CanMove = false;
        }

        private void UnlockPlayer()
        {
            if (hadPlayerLock && lockedPlayer != null) lockedPlayer.CanMove = previousCanMove;
            hadPlayerLock = false;
            lockedPlayer = null;
        }

        private bool PlayerWalkedAway()
        {
            var p = lockedPlayer != null ? lockedPlayer : player;
            if (p == null || !p.isActiveAndEnabled) return false;
            var anchor = point != null ? point.transform.position : transform.position;
            return p.GroundDistanceTo(anchor) > cancelDistance;
        }

        protected static void SetActive(GameObject go, bool value)
        {
            if (go != null && go.activeSelf != value) go.SetActive(value);
        }
    }
}
