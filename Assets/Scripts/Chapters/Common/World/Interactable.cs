using System;
using System.Collections;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Dünyada E ile kullanılan nokta: yatak ("Kalk"), kapı ("Banyoya git"), lavabo ("Yüzünü yıka")...
    /// Oyuncu zeminde useRadius kadar yaklaşınca "[E] ..." ipucu ve vurgu açılır, uzaklaşınca kapanır.
    /// Bölüm akışı obje adıyla bulur ve bekler: <c>yield return UsePoint("Bed", "Kalk");</c>
    /// Odalarda (Rooms/RoomInteractor) serbest dolaşmada da kullanılır: en yakın Available nokta ipucunu gösterir,
    /// E ile kullanılınca Used/onUsed tetiklenir, dialogueId varsa o diyalog oynar, oneShot ise bir daha kullanılamaz.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [Tooltip("İpucundaki eylem; boşsa bölüm akışının verdiği yazı kullanılır")]
        [SerializeField] private string actionLabel = "";
        [SerializeField] private string keyLabel = "E";
        [Tooltip("Oyuncunun ayağı ile bu nokta arasındaki en fazla zemin uzaklığı (dünya birimi)")]
        [SerializeField] private float useRadius = 0.8f;
        [Tooltip("İsteğe bağlı: sadece kullanılabilirken açık olan vurgu (bu objenin bir çocuğu)")]
        [SerializeField] private GameObject highlight;

        [Header("Etkileşim ipucu")]
        [Tooltip("Boşsa nesnenin Transform'u kullanılır; görselin üstüne ayrı bir anchor konabilir.")]
        [SerializeField] private Transform promptAnchor;
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 0.4f, 0f);

        [Header("Oda etkileşimi (RoomInteractor)")]
        [Tooltip("Kapalıysa RoomInteractor bu noktayı yok sayar (ör. lavabo kullanılmadan mutfak kapısı)")]
        [SerializeField] private bool available = true;
        [Tooltip("Kullanılınca oynatılacak Story JSON sahnesi (ör. ROOM_DESK). Boşsa diyalog yok.")]
        [SerializeField] private string dialogueId = "";
        [Tooltip("İşaretliyse dialogueId'nin sonuna bölüm eki eklenir: Sel _F, Deprem _E (ROOM_WINDOW → ROOM_WINDOW_F)")]
        [SerializeField] private bool appendChapterSuffix;
        [Tooltip("İşaretliyse bir kez kullanılınca Available kapanır (alarm, şemsiye, lavabo)")]
        [SerializeField] private bool oneShot;
        [Tooltip("Kullanıldığı anda (diyalogdan önce) çağrılır: ses, efekt...")]
        [SerializeField] private UnityEvent onUsed = new UnityEvent();

        /// <summary>Kullanıldığı anda (diyalogdan önce) tetiklenir.</summary>
        public event Action<Interactable> Used;

        public string ActionLabel => actionLabel;
        public string KeyLabel => keyLabel;
        public float UseRadius => useRadius;
        public bool Available => available;
        public string DialogueId => dialogueId;
        public bool AppendChapterSuffix => appendChapterSuffix;
        public bool OneShot => oneShot;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;
        public Vector3 PromptOffset { get => promptOffset; set => promptOffset = value; }

        /// <summary>RoomInteractor'ın bu noktayı önerip önermeyeceği.</summary>
        public void SetAvailable(bool value)
        {
            available = value;
            if (!value) SetHighlight(false);
        }

        /// <summary>Oda akışının noktayı koddan kurması için (Inspector'daki etiket/diyalog/oneShot değerlerinin yerine geçer).</summary>
        public void Setup(string label, string dialogue, bool chapterSuffix, bool isOneShot)
        {
            actionLabel = label ?? "";
            dialogueId = dialogue ?? "";
            appendChapterSuffix = chapterSuffix;
            oneShot = isOneShot;
        }

        /// <summary>Vurguyu açar/kapatır (RoomInteractor en yakın noktayı vurgular).</summary>
        public void SetHighlighted(bool visible) => SetHighlight(visible);

        /// <summary>Used olayını ve onUsed UnityEvent'ini tetikler (RoomInteractor çağırır).</summary>
        public void RaiseUsed()
        {
            Used?.Invoke(this);
            if (onUsed != null) onUsed.Invoke();
        }

        private void Awake()
        {
            SetHighlight(false);
        }

        /// <summary>Oyuncu kullanma mesafesinde mi? Oyuncu yoksa ya da görünmüyorsa (ör. yatakta uyurken) her zaman true.</summary>
        public bool IsPlayerInRange(IsoPlayerController player)
        {
            if (player == null || !player.isActiveAndEnabled) return true;
            return player.GroundDistanceTo(transform.position) <= useRadius;
        }

        /// <summary>
        /// Oyuncu menzildeyken E'ye basana kadar bekler; menzildeyken ipucu ve vurgu açık, dışındayken kapalıdır.
        /// label boş değilse ipucunda o yazılır (Inspector'daki actionLabel boşsa).
        /// </summary>
        public IEnumerator WaitForUse(IsoPlayerController player, InteractionHintBase hint, string label = null)
        {
            string text = string.IsNullOrEmpty(actionLabel) ? label : actionLabel;
            yield return null; // önceki adımı bitiren tuş bu adımı da geçirmesin

            int shownState = -1;
            try
            {
                while (true)
                {
                    bool inRange = IsPlayerInRange(player);
                    int state = inRange ? 1 : 0;
                    if (state != shownState)
                    {
                        shownState = state;
                        SetHighlight(inRange);
                        if (hint != null)
                        {
                            if (inRange) hint.Show(keyLabel, text, PromptAnchor, PromptOffset);
                            else hint.Hide();
                        }
                        else if (inRange)
                        {
                            Debug.Log($"[{name}]   [{keyLabel}] {text}", this);
                        }
                    }

                    if (inRange && GameInput.InteractPressed)
                    {
                        if (hint != null) hint.Press();
                        yield break;
                    }
                    yield return null;
                }
            }
            finally
            {
                if (hint != null) hint.Hide();
                SetHighlight(false);
            }
        }

        private void SetHighlight(bool visible)
        {
            if (highlight != null && highlight != gameObject && highlight.activeSelf != visible)
                highlight.SetActive(visible);
        }

        // Sahne görünümünde seçiliyken kullanma alanını (izometrik elips) çizer
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 c = transform.position;
            const int segments = 32;
            Vector3 previous = c + new Vector3(useRadius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var next = c + new Vector3(Mathf.Cos(a) * useRadius, Mathf.Sin(a) * useRadius * 0.5f, 0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
