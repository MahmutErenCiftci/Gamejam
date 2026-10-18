using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Kapıdaki elektronik kilit + yatay kol ([E] noktası kapının önünde). İki evre:
    ///  • Kod bulunmadan ("Kilidi Kontrol Et"): Cenk tuş takımına uzanır, kilit kırmızı yanıp söner, hata bipi,
    ///    "Kilit sıfırlanmış. Yeni kod gerekli. Posta kutusuna bir bak." → ilk seferde lock_checked. Kapı açılmaz.
    ///  • Kod bulunduktan sonra ("Kodu Gir"): UI ajanının ApartmentLockView.Run(data) yakın planı açılır (fareyle PIN; yanlış → kırmızı,
    ///    doğru → yeşil ve pin_entered; kapı kendiliğinden AÇILMAZ; ardından kol fareyle aşağı çekilir → handle_turned).
    ///    Dünyadaki tuş takımı ve kol aynı olaylarla değişir. Görünüm iptalle kapanırsa nokta açık kalır, tekrar denenir.
    ///    Kol çekildiyse ApartmentFinale.EnterSequence() (kapı açılır, Cenk içeri girer) aynı Play içinde oynar.
    /// </summary>
    public class DoorLockInteraction : MonoBehaviour, IFloodEveningInteraction, IFloodEveningResettable
    {
        public const string CheckPanelId = "apt_lock_check";
        public const string PinPanelId = "apt_lock";

        [SerializeField] private ApartmentFinale finale;
        [SerializeField] private Interactable point;
        [Tooltip("Cenk'in kilidin önünde durduğu nokta")]
        [SerializeField] private Transform stand;
        [SerializeField, Min(0.1f)] private float typeFrameSeconds = 0.22f;

        private bool playing;
        private bool pinOkFired, handleDoneFired;
        private ApartmentLockView boundView;

        public Interactable Point => point;
        public bool CanUse => finale != null && finale.CanUseLock && !playing;
        public bool IsOpen => playing;

        private void Awake()
        {
            if (finale == null) finale = GetComponentInParent<ApartmentFinale>();
            if (point == null) point = GetComponentInChildren<Interactable>(true);
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            Unbind();
        }

        public IEnumerator Play()
        {
            if (!CanUse) yield break;
            playing = true;
            bool enter = false;
            string panel = finale.PinFound ? PinPanelId : CheckPanelId;
            FloodEveningHub.NotifyOpened(panel);
            try
            {
                yield return finale.StepTo(stand);
                finale.FaceWall();
                yield return null;
                if (!finale.PinFound) yield return CheckLock();
                else
                {
                    yield return EnterPin();
                    enter = finale.HandleTurned;
                }
            }
            finally
            {
                Unbind();
                if (!enter) finale.ClearPose();
                playing = false;
                FloodEveningHub.NotifyClosed(panel);
            }
            if (enter) yield return finale.EnterSequence();
        }

        /// <summary>Kod yokken: bak, dene, kırmızı yanıp söner. İlk seferde kilidin "sıfırlanmış" yakın planı (UI ajanının
        /// ShowResetNotice'i: başlık + "Kilit sıfırlanmış. Yeni kod gerekli." + "Posta kutusuna bir bak."), sonrakilerde kısa bilgi şeridi.</summary>
        private IEnumerator CheckLock()
        {
            var door = finale.Door;
            yield return finale.Pose(ApartmentFinale.PoseKind.InspectLock, 0.45f);
            if (door != null) door.FlashWrong();
            yield return finale.Pose(ApartmentFinale.PoseKind.InspectLock, 0.7f, true);
            var view = finale.LockView;
            if (!finale.LockChecked && view != null)
            {
                finale.HoldPose(ApartmentFinale.PoseKind.InspectLock, 0);
                yield return view.ShowResetNotice(finale.Data);
                yield return null; // yakın planı kapatan tuş başka bir etkileşimi tetiklemesin
            }
            else finale.ShowLockResetMessage();
            finale.ClearPose();
            finale.MarkLockChecked();
            yield return Wait(0.25f);
        }

        /// <summary>Kod bulunduysa: kilit yakın planı (PIN + kol). Görünüm açıkken Cenk tuşlara basar, sonra kola uzanır.</summary>
        private IEnumerator EnterPin()
        {
            var view = finale.LockView;
            if (view == null)
            {
                Debug.LogError("[APT] ApartmentLockView yok (FE_ApartmentLockUi sahnede değil) → kilit açılamaz", this);
                yield break;
            }
            var door = finale.Door;
            if (door != null && door.Keypad != ApartmentDoor.KeypadState.Green) door.SetKeypad(ApartmentDoor.KeypadState.Idle);
            Bind(view);

            bool done = false;
            finale.StartCoroutine(RunView(view, finale.Data, () => done = true));
            float t = 0f;
            while (!done)
            {
                t += Time.deltaTime;
                if (handleDoneFired || view.HandleTurned) finale.HoldPose(ApartmentFinale.PoseKind.TurnHandle, 1);
                else if (pinOkFired || finale.PinEntered) finale.HoldPose(ApartmentFinale.PoseKind.TurnHandle, 0);
                else
                {
                    int n = Mathf.Max(1, finale.PoseLength(ApartmentFinale.PoseKind.TypeKeypad));
                    finale.HoldPose(ApartmentFinale.PoseKind.TypeKeypad, Mathf.FloorToInt(t / typeFrameSeconds) % n);
                }
                yield return null;
            }
            Unbind();

            // olaylar kaçtıysa (ör. görünüm durumu önceden doğruydu) son durumdan tamamla
            if (view.PinAccepted && !finale.PinEntered) OnPinOk();
            if (view.HandleTurned && !finale.HandleTurned) OnHandleDone();
            yield return null; // görünümü kapatan tuş başka bir etkileşimi tetiklemesin
        }

        private static IEnumerator RunView(ApartmentLockView view, ApartmentFinalData data, System.Action onDone)
        {
            try { yield return view.Run(data); }
            finally { onDone(); }
        }

        private void Bind(ApartmentLockView view)
        {
            Unbind();
            boundView = view;
            pinOkFired = finale.PinEntered;
            handleDoneFired = false;
            view.PinOk += OnPinOk;
            view.PinWrong += OnPinWrong;
            view.HandleDone += OnHandleDone;
        }

        private void Unbind()
        {
            if (boundView == null) return;
            boundView.PinOk -= OnPinOk;
            boundView.PinWrong -= OnPinWrong;
            boundView.HandleDone -= OnHandleDone;
            boundView = null;
        }

        private void OnPinOk()
        {
            pinOkFired = true;
            if (finale.PinEntered) return;
            if (finale.Door != null) finale.Door.ShowAccepted(false); // yakın plan kendi sesini çalar
            finale.MarkPinEntered();
        }

        private void OnPinWrong()
        {
            if (finale.Door != null) finale.Door.FlashWrong(false); // yakın plan kendi sesini çalar
        }

        private void OnHandleDone()
        {
            handleDoneFired = true;
            if (finale.HandleTurned) return;
            if (!finale.PinEntered) OnPinOk(); // kol ancak doğru koddan sonra çekilebilir; olay sırası kaçtıysa tamamla
            if (finale.Door != null) finale.Door.SetHandleDown(true);
            finale.MarkHandleTurned();
        }

        public void ResetModule()
        {
            Unbind();
            playing = false;
            pinOkFired = handleDoneFired = false;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
