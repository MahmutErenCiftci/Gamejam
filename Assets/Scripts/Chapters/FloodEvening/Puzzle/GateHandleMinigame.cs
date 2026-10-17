using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// BAHÇE KAPISI mantığı (ENV çapası `GardenGate`'e eklenir; arayüz ayrı prefab: FE_GateHandleView).
    /// [E] Kapıyı aç → kapı kolu (çark): fare: sol tuş basılı, çarkın çevresinde SAAT YÖNÜNDE daire çiz;
    /// klavye: D veya → basılı tut. Ters yön ilerletmez (düşürmez de), yalnızca uyarı gösterir. İlerleme çubuğu + yüzde.
    /// Varsayılan 1,5 tur (fareyle ~2 sn, klavyeyle ~1,8 sn). Esc / Kapat / uzaklaşma / Interrupt iptal eder; ilerleme
    /// korunur, tekrar denenebilir. Tamamlanınca: `Open` açık, `Closed` + yol kesen collider kapalı, nokta kapanır,
    /// Hub.Complete("gate_opened"). Mini oyun açıkken süre İŞLER (controller durdurmaz), hareket kilitlidir.
    /// </summary>
    public class GateHandleMinigame : FloodPuzzleDoorBase
    {
        public const string MinigameId = "gate";
        public const string CompletedStepId = "gate_opened";

        [Header("Arayüz")]
        [Tooltip("FE_GateHandleView prefab'ı (ya da sahnedeki örneği). Boşsa aynı düzen kodla kurulur.")]
        [SerializeField] private GateHandleView view;

        [Header("Kol çevirme")]
        [Tooltip("Kapının açılması için gereken toplam dönüş (derece). 540 = 1,5 tur.")]
        [SerializeField, Min(90f)] private float requiredDegrees = 540f;
        [Tooltip("Klavyeyle (D / →) basılı tutunca saniyede dönüş (derece).")]
        [SerializeField, Min(30f)] private float keyboardDegreesPerSecond = 300f;
        [Tooltip("Tek karede sayılan en büyük fare açısı (imleç merkezden geçerken sıçramayı önler).")]
        [SerializeField, Range(10f, 120f)] private float maxMouseStepDegrees = 50f;
        [Tooltip("İmleç çark merkezine bu kadar (referans piksel) yakınsa açı sayılmaz.")]
        [SerializeField, Min(0f)] private float deadZoneReferencePixels = 10f;
        [Tooltip("Tamamlanınca panelin açık kalacağı süre (sn, gerçek zaman).")]
        [SerializeField, Min(0f)] private float successHoldSeconds = 0.7f;

        private GateHandleView viewInstance;
        private float degrees;
        private float lastAngle;
        private float reverseAccum;
        private bool tracking;
        private float successTimer = -1f;
        private float wrongWayTimer;
        private bool closeRequested;

        /// <summary>0..1 kol ilerlemesi (iptalde korunur, ResetModule sıfırlar).</summary>
        public float Progress01 => Mathf.Clamp01(degrees / requiredDegrees);

        /// <summary>Test/hata ayıklama: son karedeki fare açı farkı (derece, + = ters yön) ve ters yön uyarısı açık mı.</summary>
        public float LastMouseDelta { get; private set; }
        public bool WrongWayShown => wrongWayTimer > 0f;

        protected override string HubId => MinigameId;
        protected override string StepId => CompletedStepId;
        protected override string DefaultLabel => "Kapıyı aç";

        private GateHandleView View
        {
            get
            {
                if (viewInstance == null)
                {
                    FloodPuzzleViewBase.Resolve(view, ref viewInstance, this, "FE_GateHandleView");
                    viewInstance.CloseClicked += () => { if (IsOpen) closeRequested = true; };
                }
                return viewInstance;
            }
        }

        protected override void ShowView()
        {
            tracking = false;
            successTimer = -1f;
            wrongWayTimer = 0f;
            closeRequested = false;
            View.Show();
            Render();
        }

        protected override void HideView()
        {
            if (viewInstance != null) viewInstance.Hide();
            tracking = false;
        }

        protected override bool Tick(float dt, float unscaledDt)
        {
            var v = View;
            v.TickAppear(unscaledDt);

            if (successTimer >= 0f)
            {
                successTimer += unscaledDt;
                return successTimer < successHoldSeconds;
            }
            if (closeRequested || GameInput.BackPressed) return false;

            float before = degrees;
            bool wrongWay = false;

            // Klavye: D / → saat yönünde; A / ← ters (yalnızca uyarı).
            float mx = GameInput.MoveX;
            if (mx > 0.5f) degrees += keyboardDegreesPerSecond * dt;
            else if (mx < -0.5f) wrongWay = true;

            // Fare: sol tuş basılıyken çark merkezine göre açı farkı (ekranda saat yönü = açı azalır).
            if (GameInput.ClickHeld)
            {
                Vector2 d = GameInput.PointerPosition - v.WheelScreenCenter;
                float dead = deadZoneReferencePixels * v.ScaleFactor;
                if (d.sqrMagnitude > dead * dead)
                {
                    float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    if (tracking)
                    {
                        float delta = Mathf.Clamp(Mathf.DeltaAngle(lastAngle, angle), -maxMouseStepDegrees, maxMouseStepDegrees);
                        LastMouseDelta = delta;
                        // Kare hızından bağımsız: ters yönde biriken açı 15°'yi geçerse uyar (yüksek FPS'te kare başı açı küçüktür).
                        if (delta < 0f) { degrees -= delta; reverseAccum = 0f; }
                        else if (delta > 0f) { reverseAccum += delta; if (reverseAccum > 15f) wrongWay = true; }
                    }
                    lastAngle = angle;
                    tracking = true;
                }
                else tracking = false;
            }
            else { tracking = false; reverseAccum = 0f; }

            if (wrongWay && degrees <= before) wrongWayTimer = 1.2f;
            else wrongWayTimer = Mathf.Max(0f, wrongWayTimer - unscaledDt);

            if (degrees >= requiredDegrees)
            {
                degrees = requiredDegrees;
                successTimer = 0f;
                OpenDoorPermanently();
            }
            Render();
            return true;
        }

        private void Render()
        {
            if (viewInstance == null) return;
            var status = successTimer >= 0f || Opened ? GateHandleView.Status.Done
                : wrongWayTimer > 0f ? GateHandleView.Status.WrongWay
                : degrees > 0f ? GateHandleView.Status.Turning
                : GateHandleView.Status.Idle;
            viewInstance.Render(Progress01, degrees, status);
        }

        protected override void ResetState()
        {
            base.ResetState();
            degrees = 0f;
            tracking = false;
            successTimer = -1f;
            wrongWayTimer = 0f;
        }
    }
}
