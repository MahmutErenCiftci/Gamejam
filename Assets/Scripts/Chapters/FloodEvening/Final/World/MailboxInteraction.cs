using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Apartman girişindeki posta kutusu ([E] "Posta Kutusunu Kontrol Et"). Kilit kontrol edilmeden kullanılamaz (CanUse false).
    /// İlk kullanım: Cenk kutuya döner, uzanır → kapak açılır (zarf görünür) → zarfı alır (kutu açık ve boş kalır) → zarfa bakar
    /// → mektup yakın planı (ApartmentLetterView, kod ApartmentFinalData.pin'den) → kapanınca pin_found (bir kez).
    /// Sonraki kullanımlar: boş kutuya bakar, mektubu yeniden açar (kod yine görünür). Controller Point'i kaydeder ve Play() boyunca
    /// hareketi kilitler; bu bileşen E tuşunu dinlemez.
    /// </summary>
    public class MailboxInteraction : MonoBehaviour, IFloodEveningInteraction, IFloodEveningResettable
    {
        public const string PanelId = "apt_mailbox";

        [SerializeField] private ApartmentFinale finale;
        [SerializeField] private Interactable point;
        [Tooltip("Cenk'in kutunun önünde durduğu nokta")]
        [SerializeField] private Transform stand;

        [Header("Kapak (Cenk'in kutusu)")]
        [SerializeField] private SpriteRenderer lid;
        [SerializeField] private Sprite lidClosed;
        [SerializeField] private Sprite lidOpenFull;
        [SerializeField] private Sprite lidOpenEmpty;
        [Tooltip("Kapalı kutunun aralığından görünen zarf ucu (kilit kontrolünden sonra görünür)")]
        [SerializeField] private GameObject envelopeTip;

        [Header("Ses")]
        [SerializeField] private string openSfx = "SFX_Final_Mailbox";

        private bool playing;

        public Interactable Point => point;
        public bool CanUse => finale != null && finale.CanUseMailbox && !playing;
        public bool IsOpen => playing;
        public bool EnvelopeTaken { get; private set; }
        public bool LidOpen { get; private set; }

        private void Awake()
        {
            if (finale == null) finale = GetComponentInParent<ApartmentFinale>();
            if (point == null) point = GetComponentInChildren<Interactable>(true);
            ApplyLid();
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable() => FloodEveningHub.ResetRequested -= ResetModule;

        private void Update()
        {
            if (envelopeTip != null)
            {
                bool show = finale != null && finale.LockChecked && !LidOpen;
                if (envelopeTip.activeSelf != show) envelopeTip.SetActive(show);
            }
        }

        public IEnumerator Play()
        {
            if (!CanUse) yield break;
            playing = true;
            FloodEveningHub.NotifyOpened(PanelId);
            try
            {
                yield return finale.StepTo(stand);
                finale.FaceWall();
                yield return null;

                if (!EnvelopeTaken)
                {
                    // uzan → kapak açılır, zarf görünür → zarfı al → zarfa bak
                    finale.HoldPose(ApartmentFinale.PoseKind.ReachMailbox, 0);
                    yield return Wait(0.3f);
                    LidOpen = true;
                    ApplyLid();
                    GameAudio.PlaySfx(openSfx, 0.85f);
                    finale.HoldPose(ApartmentFinale.PoseKind.ReachMailbox, 1);
                    yield return Wait(0.45f);
                    EnvelopeTaken = true;
                    ApplyLid();
                    yield return finale.Pose(ApartmentFinale.PoseKind.TakeEnvelope, 0.5f);
                    yield return finale.Pose(ApartmentFinale.PoseKind.ReadEnvelope, 0.35f);
                }
                else
                {
                    // boş kutuya bakar, mektubu yeniden açar
                    finale.HoldPose(ApartmentFinale.PoseKind.ReachMailbox, 0);
                    yield return Wait(0.3f);
                    yield return finale.Pose(ApartmentFinale.PoseKind.ReadEnvelope, 0.3f);
                }

                finale.HoldPose(ApartmentFinale.PoseKind.ReadEnvelope, finale.PoseLength(ApartmentFinale.PoseKind.ReadEnvelope) - 1);
                yield return finale.ShowLetter();
                finale.MarkPinFound();
            }
            finally
            {
                finale.ClearPose();
                playing = false;
                FloodEveningHub.NotifyClosed(PanelId);
            }
        }

        private void ApplyLid()
        {
            if (lid == null) return;
            lid.sprite = !LidOpen ? lidClosed : EnvelopeTaken ? lidOpenEmpty : lidOpenFull;
        }

        public void ResetModule()
        {
            playing = false;
            EnvelopeTaken = false;
            LidOpen = false;
            ApplyLid();
            if (envelopeTip != null) envelopeTip.SetActive(false);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
