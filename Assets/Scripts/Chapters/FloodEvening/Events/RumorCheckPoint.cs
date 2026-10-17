using System;
using System.Collections;
using AfetUyanisi.News;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Söylenti ve kaynak kontrolü (PROMPT §D). RumorSpot'taki komşu: "[E] Komşuyu dinle".
    /// Play: kısa diyalog (FE_RUMOR_INTRO: komşu mesaj grubundaki söylentiyi okur) → Eren'in RumorCards'ı yeni veriyle
    /// (Assets/Data/FloodEvening/flood_evening_rumor.json: söylenti "Kestirme açıkmış, herkes oradan gidiyor." kaynak bilinmiyor
    /// + resmî duyuru). Söylenti seçilirse kart DOĞRULANMAMIŞ olur, neden güvenilmeyeceği yazılır, oyuncu tekrar seçer
    /// (doğru seçenek değildir). Resmî duyuru kapalı alçak yolu ve güvenli rotayı söyler; ardından revealOnChecked
    /// objeleri (yön okları, güvenli rota işaretleri) açılır ve FE_RUMOR_CHECKED oynar.
    /// Hub: NotifyOpened/Closed("rumor"), yanlışta ReportMistake("rumor_trusted"), sonda Complete("rumor_checked").
    /// E tuşunu dinlemez; controller Point'i RoomInteractor'a kaydedip Play()'i çalıştırır.
    /// </summary>
    public class RumorCheckPoint : MonoBehaviour, IFloodEveningInteraction, IFloodEveningResettable
    {
        public const string MinigameId = "rumor";
        public const string StepId = "rumor_checked";
        public const string MistakeId = "rumor_trusted";

        [SerializeField] private Interactable point;
        [SerializeField] private string actionLabel = "Komşuyu dinle";
        [Tooltip("Eren'in RumorCards prefab örneği (bu objenin çocuğu). Boşsa çocuklarda aranır.")]
        [SerializeField] private RumorCards cards;
        [Tooltip("Boşsa sahnedeki DialogueRunnerBase kullanılır. Yoksa satırlar Console'a yazılır.")]
        [SerializeField] private DialogueRunnerBase dialogue;
        [SerializeField] private string introSceneId = "FE_RUMOR_INTRO";
        [SerializeField] private string checkedSceneId = "FE_RUMOR_CHECKED";
        [Tooltip("Kaynak kontrol edilince açılır (güvenli rota okları, kapalı yol işareti vurgusu...).")]
        [SerializeField] private GameObject[] revealOnChecked = Array.Empty<GameObject>();
        [Tooltip("Kaynak kontrol edilince kapanır (ör. komşunun üstündeki '!' balonu).")]
        [SerializeField] private GameObject[] hideOnChecked = Array.Empty<GameObject>();

        private bool playing;

        public Interactable Point => point;
        public bool CanUse => !IsChecked && !playing;

        /// <summary>Resmî duyuru kontrol edildi mi?</summary>
        public bool IsChecked { get; private set; }

        /// <summary>Oyuncu önce söylentiye güvendi mi (en az bir yanlış seçim)?</summary>
        public bool TrustedRumor { get; private set; }

        private void Awake()
        {
            if (point == null) point = GetComponentInChildren<Interactable>(true);
            if (cards == null) cards = GetComponentInChildren<RumorCards>(true);
            if (point != null) point.Setup(actionLabel, "", false, false);
            ApplyChecked(false);
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable() => FloodEveningHub.ResetRequested -= ResetModule;

        public IEnumerator Play()
        {
            if (!CanUse) yield break;
            playing = true;
            FloodEveningHub.NotifyOpened(MinigameId);
            try
            {
                yield return null; // [E]'nin aynı karesi diyaloğu geçirmesin

                var d = ResolveDialogue();
                if (d != null && !string.IsNullOrEmpty(introSceneId)) yield return d.Play(introSceneId);
                else Debug.Log("[RumorCheckPoint] (diyalog yok) " + introSceneId, this);
                yield return null;

                int wrong = 0;
                if (cards != null)
                {
                    bool finished = false;
                    Action<int> onFinished = w => { finished = true; wrong = w; };
                    cards.Finished += onFinished;
                    try
                    {
                        cards.Play();
                        if (cards.IsPlaying) while (!finished) yield return null;
                        else Debug.LogWarning("[RumorCheckPoint] Söylenti kartları açılmadı (JSON?).", this);
                    }
                    finally
                    {
                        cards.Finished -= onFinished;
                    }
                }
                else Debug.LogWarning("[RumorCheckPoint] RumorCards bağlı değil.", this);

                if (wrong > 0)
                {
                    TrustedRumor = true;
                    FloodEveningHub.ReportMistake(MistakeId);
                }
                IsChecked = true;
                ApplyChecked(true);
                FloodEveningHub.Complete(StepId);

                yield return null;
                if (d != null && !string.IsNullOrEmpty(checkedSceneId)) yield return d.Play(checkedSceneId);
            }
            finally
            {
                playing = false;
                FloodEveningHub.NotifyClosed(MinigameId);
            }
        }

        public void ResetModule()
        {
            playing = false;
            IsChecked = false;
            TrustedRumor = false;
            ApplyChecked(false);
        }

        private void ApplyChecked(bool done)
        {
            foreach (var go in revealOnChecked) if (go != null) go.SetActive(done);
            foreach (var go in hideOnChecked) if (go != null) go.SetActive(!done);
            if (point != null) point.SetAvailable(!done);
        }

        private DialogueRunnerBase ResolveDialogue()
        {
            if (dialogue == null) dialogue = FindAnyObjectByType<DialogueRunnerBase>();
            return dialogue;
        }
    }
}
