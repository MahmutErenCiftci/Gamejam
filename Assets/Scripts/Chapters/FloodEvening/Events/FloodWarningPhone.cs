using System;
using System.Collections;
using AfetUyanisi.News;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel bölümünün telefon uyarısı (PROMPT §A). Eski sel bölümündeki bildirimin aynısı: Eren'in
    /// AlertNotification prefab'ı (Assets/Prefabs/News/AlertNotification.prefab) yeni veriyle
    /// (Assets/Data/FloodEvening/flood_evening_alert.json) oynatılır; görünüm değiştirilmez.
    /// Bu bölümde otomatik kapanma yoktur: prefab örneğinde holdDuration çok büyük verilir, bildirim ancak
    /// [E] / Space / Enter / sol tık ile kapanır. Kapanış kayması bitip bir kare daha geçtikten sonra döner;
    /// bildirimi kapatan girdi sonraki etkileşimi tetiklemez. Bildirim kurgudur (kurum logosu yok, "Kurgusal" notu var).
    /// FloodEveningController Warning durumunda ShowWarning()'i bekler; bitince Hub.Complete("warning_read").
    /// </summary>
    public class FloodWarningPhone : MonoBehaviour, IFloodEveningWarning, IFloodEveningResettable
    {
        public const string StepId = "warning_read";

        [Tooltip("Eren'in AlertNotification prefab örneği (bu objenin çocuğu). Boşsa çocuklarda aranır.")]
        [SerializeField] private AlertNotification alert;
        [Tooltip("Bildirim kapandıktan sonra kontrol geri verilmeden önceki kısa bekleme (sn, gerçek zaman).")]
        [SerializeField, Min(0f)] private float afterCloseDelay = 0.15f;

        /// <summary>Bildirim şu an ekranda mı?</summary>
        public bool IsShowing { get; private set; }

        /// <summary>Uyarı okundu (kapatıldı) mı?</summary>
        public bool IsRead { get; private set; }

        /// <summary>Uyarı kapanıp kontrol geri verilmeden hemen önce tetiklenir.</summary>
        public event Action Read;

        private void Awake()
        {
            if (alert == null) alert = GetComponentInChildren<AlertNotification>(true);
        }

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;
        private void OnDisable() => FloodEveningHub.ResetRequested -= ResetModule;

        public IEnumerator ShowWarning()
        {
            if (IsShowing) yield break;
            IsShowing = true;

            // Önceki adımı bitiren tuş bildirimi hemen kapatmasın.
            yield return null;

            if (alert == null)
            {
                Debug.LogWarning("[FloodWarningPhone] AlertNotification bağlı değil; uyarı gösterilmeden geçildi.", this);
            }
            else
            {
                bool finished = false;
                Action onFinished = () => finished = true;
                alert.Finished += onFinished;
                try
                {
                    alert.Play();
                    if (!alert.IsPlaying)
                        Debug.LogWarning("[FloodWarningPhone] Bildirim başlamadı (JSON eksik ya da zaten oynuyor).", this);
                    else
                        while (!finished) yield return null;
                }
                finally
                {
                    alert.Finished -= onFinished;
                }
            }

            // Kapatan tık/tuş aynı karede başka bir şeyi tetiklemesin.
            yield return null;
            for (float t = 0f; t < afterCloseDelay; t += Time.unscaledDeltaTime) yield return null;

            IsShowing = false;
            IsRead = true;
            Read?.Invoke();
            FloodEveningHub.Complete(StepId);
        }

        public void ResetModule()
        {
            StopAllCoroutines();
            if (alert != null) alert.Hide();
            IsShowing = false;
            IsRead = false;
        }
    }
}
