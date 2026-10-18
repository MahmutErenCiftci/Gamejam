using System;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Tehlike bölgesi (ayna önü, pencere önü, kitaplık yanı, sarsıntı sonrası cam kırıkları).
    /// Bölge: bu objedeki Collider2D (isTrigger önerilir; oyuncunun ayak noktası OverlapPoint ile denetlenir) ya da
    /// collider yoksa zemin yarıçapı (radius). Fizik olayına bağlı değildir, her karede yalnızca konum denetlenir.
    /// Davranış (evreye göre):
    ///  • Girince uyarı toast'ı (warningId → JSON warnings; aynı ziyarette bir kez, toastCooldown ile).
    ///  • Prep'te penalizeInPrep işaretliyse: uyarıdan sonra warningGrace sn daha içerde kalırsa BİR KEZ ceza
    ///    (TimePenaltySystem, anahtar "zone:id"); çıkıp tekrar girmek yeni ceza VERMEZ. Sayaç duraklatılmışken süre saymaz.
    ///  • Quake'te içerdeyse maruziyet (ContainsPlayer'ı mod kontrolcüsü sorar; Exposed olayı da bir kez tetiklenir).
    ///  • After'da sadece uyarı + Entered olayı (cam kırıkları: ceza yok, kayıt).
    /// </summary>
    public class HazardZone : MonoBehaviour
    {
        [Tooltip("Bölge kimliği (ceza anahtarı). Boşsa obje adı.")]
        [SerializeField] private string id = "";
        [Tooltip("JSON warnings[].id: mirror / window / bookshelf / glass_after")]
        [SerializeField] private string warningId = "mirror";
        [Tooltip("Maruziyet türü (protection.exposed metni için): window / mirror / bookshelf")]
        [SerializeField] private string exposureKind = "mirror";
        [Tooltip("Zemin yarıçapı (Collider2D yoksa kullanılır)")]
        [SerializeField] private float radius = 0.8f;

        [Header("Evreler")]
        [SerializeField] private bool activeInPrep = true;
        [SerializeField] private bool activeInQuake = true;
        [SerializeField] private bool activeInAfter;

        [Header("Uyarı ve ceza")]
        [Tooltip("Hazırlıkta girince uyarı göster")]
        [SerializeField] private bool warnInPrep = true;
        [Tooltip("Sarsıntı sonrasında girince uyarı göster (cam kırıkları)")]
        [SerializeField] private bool warnInAfter = true;
        [Tooltip("Hazırlıkta uyarıdan sonra uzun kalınca bir kerelik ceza (ayna: işaretli, pencere/kitaplık: kapalı)")]
        [SerializeField] private bool penalizeInPrep = true;
        [Tooltip("Uyarıdan sonra cezaya kadar beklenecek süre (sn)")]
        [SerializeField] private float warningGrace = 3f;
        [Tooltip("Ceza türü")]
        [SerializeField] private TimePenaltySystem.PenaltyType penaltyType = TimePenaltySystem.PenaltyType.MirrorExposure;
        [Tooltip("Toast ekranda kalma süresi (sn)")]
        [SerializeField] private float toastSeconds = 3f;
        [Tooltip("Aynı bölgenin uyarısı en az bu kadar arayla tekrar gösterilir (sn)")]
        [SerializeField] private float toastCooldown = 5f;
        [Tooltip("Yedek uyarı başlığı (JSON'da warningId yoksa)")]
        [SerializeField] private string fallbackTitle = "DİKKAT!";
        [Tooltip("Yedek uyarı metni")]
        [SerializeField] private string fallbackText = "Deprem sırasında cam ve devrilebilecek eşyalardan uzak dur.";

        [Header("Kancalar")]
        [SerializeField] private UnityEvent onEntered = new UnityEvent();
        [SerializeField] private UnityEvent onPenalized = new UnityEvent();

        private Collider2D zoneCollider;
        private IsoPlayerController player;
        private TimePenaltySystem penalties;
        private EmergencyCountdownManager countdown;
        private InfoCardViewBase cards;
        private Func<EarthquakePhase> phaseGetter;
        private Func<bool> blockedGetter;
        private string title;
        private string text;

        private bool inside;
        private float graceTimer;
        private bool penalized;
        private bool exposedRaised;
        private float lastToastTime = -999f;

        /// <summary>Oyuncu bölgeye girince (evre bilgisiyle).</summary>
        public event Action<HazardZone, EarthquakePhase> Entered;
        /// <summary>Ceza verildiğinde (bir kez).</summary>
        public event Action<HazardZone> Penalized;
        /// <summary>Sarsıntı sırasında oyuncu içerdeyse (bir kez).</summary>
        public event Action<HazardZone> Exposed;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string WarningId => warningId;
        public string ExposureKind => exposureKind;
        public bool IsPenalized => penalized;
        public bool PlayerInside => inside;
        public string WarningTitle => !string.IsNullOrEmpty(title) ? title : fallbackTitle;
        public string WarningText => !string.IsNullOrEmpty(text) ? text : fallbackText;

        /// <summary>Kurulum (mod kontrolcüsü çağırır). blocked: true iken (kart/soru/geçiş) süre saymaz ve uyarı çıkmaz.</summary>
        public void Bind(IsoPlayerController playerController, TimePenaltySystem penaltySystem, EmergencyCountdownManager timer,
            InfoCardViewBase cardView, Func<EarthquakePhase> phase, Func<bool> blocked, EqWarningData warning)
        {
            player = playerController;
            penalties = penaltySystem;
            countdown = timer;
            cards = cardView;
            phaseGetter = phase;
            blockedGetter = blocked;
            if (warning != null)
            {
                title = warning.title;
                text = warning.text;
            }
        }

        /// <summary>
        /// true iken bölge hiçbir evrede çalışmaz (uyarı, ceza, maruziyet yok). Ör. kitaplık mini oyunda sabitlenince
        /// kitaplık bölgesi kapatılır (BookshelfAnchorMinigame).
        /// </summary>
        public bool Suppressed { get; set; }

        public bool IsActiveIn(EarthquakePhase phase)
        {
            if (Suppressed) return false;
            switch (phase)
            {
                case EarthquakePhase.Prep: return activeInPrep;
                case EarthquakePhase.Quake: return activeInQuake;
                case EarthquakePhase.After: return activeInAfter;
                default: return false;
            }
        }

        /// <summary>Oyuncunun ayak noktası bölgenin içinde mi (evreden bağımsız)?</summary>
        public bool ContainsPlayer()
        {
            if (player == null || !player.isActiveAndEnabled) return false;
            return Contains(player.transform.position);
        }

        /// <summary>Nokta bölgenin içinde mi?</summary>
        public bool Contains(Vector3 point)
        {
            if (zoneCollider == null) zoneCollider = GetComponent<Collider2D>();
            if (zoneCollider != null && zoneCollider.enabled) return zoneCollider.OverlapPoint(point);
            Vector2 d = point - transform.position;
            return Mathf.Sqrt(d.x * d.x + 4f * d.y * d.y) <= radius;
        }

        /// <summary>Otomatik test için bölgenin içindeki bir nokta.</summary>
        public Vector3 InnerPoint()
        {
            if (zoneCollider == null) zoneCollider = GetComponent<Collider2D>();
            if (zoneCollider != null)
            {
                Vector3 c = zoneCollider.bounds.center;
                c.z = transform.position.z;
                if (zoneCollider.OverlapPoint(c)) return c;
            }
            return transform.position;
        }

        private void Awake()
        {
            zoneCollider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            if (phaseGetter == null || player == null) return;
            var phase = phaseGetter();
            bool blocked = blockedGetter != null && blockedGetter();
            bool nowInside = IsActiveIn(phase) && ContainsPlayer();

            if (nowInside && !inside)
            {
                inside = true;
                graceTimer = 0f;
                OnEnter(phase, blocked);
            }
            else if (!nowInside && inside)
            {
                inside = false;
                graceTimer = 0f;
            }

            if (!inside) return;

            if (phase == EarthquakePhase.Quake && !exposedRaised)
            {
                exposedRaised = true;
                Exposed?.Invoke(this);
            }

            if (phase == EarthquakePhase.Prep && penalizeInPrep && !penalized && !blocked
                && (countdown == null || !countdown.IsPaused))
            {
                graceTimer += Time.deltaTime;
                if (graceTimer >= warningGrace)
                {
                    penalized = true;
                    Debug.Log($"[EQ] tehlike bölgesi '{Id}': uyarıdan sonra {warningGrace:0.#} sn kalındı → ceza", this);
                    if (penalties != null) penalties.Apply(penaltyType, "zone:" + Id, WarningTitle);
                    if (onPenalized != null) onPenalized.Invoke();
                    Penalized?.Invoke(this);
                }
            }
        }

        private void OnEnter(EarthquakePhase phase, bool blocked)
        {
            Debug.Log($"[EQ] tehlike bölgesine girildi: {Id} ({phase})", this);
            if (onEntered != null) onEntered.Invoke();
            Entered?.Invoke(this, phase);

            bool warn = (phase == EarthquakePhase.Prep && warnInPrep) || (phase == EarthquakePhase.After && warnInAfter);
            if (warn && !blocked && Time.time - lastToastTime >= toastCooldown)
            {
                lastToastTime = Time.time;
                EqSfx.Play(EqSfx.HazardWarn, 0.7f); // SES: tehlike uyarısı
                if (cards != null) cards.ShowToast(WarningTitle, WarningText, toastSeconds);
                else Debug.Log($"[EQ] UYARI: {WarningTitle} — {WarningText}", this);
            }
        }

        // Sahne görünümünde bölgeyi çizer (collider yoksa izometrik elips)
        private void OnDrawGizmosSelected()
        {
            if (GetComponent<Collider2D>() != null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            Vector3 c = transform.position;
            const int segments = 32;
            Vector3 previous = c + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var next = c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.5f, 0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
