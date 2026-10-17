using System.Collections;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Dünyada E ile kullanılan nokta: yatak ("Kalk"), kapı ("Banyoya git"), lavabo ("Yüzünü yıka")...
    /// Oyuncu zeminde useRadius kadar yaklaşınca "[E] ..." ipucu ve vurgu açılır, uzaklaşınca kapanır.
    /// Bölüm akışı obje adıyla bulur ve bekler: <c>yield return UsePoint("Bed", "Kalk");</c>
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

        public string ActionLabel => actionLabel;

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
                            if (inRange) hint.Show(keyLabel, text);
                            else hint.Hide();
                        }
                        else if (inRange)
                        {
                            Debug.Log($"[{name}]   [{keyLabel}] {text}", this);
                        }
                    }

                    if (inRange && GameInput.InteractPressed) yield break;
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
