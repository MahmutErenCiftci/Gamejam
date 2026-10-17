using System.Collections;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Komodindeki telefon: titreşim (1 px adımlı, piksel-mükemmel), zil ses kancası, kameranın telefona hafif kayması
    /// ve PhoneAlertViewBase bildirimi ("AFET FARKINDALIK SİMÜLASYONU" ... [BAŞLA]).
    /// Görünüm yoksa bildirim Console'a yazılır ve "[E] BAŞLA" ipucuyla tuş beklenir (fallbackTimeout sn sonra kendiliğinden geçer).
    /// </summary>
    public class PhoneAlertController : MonoBehaviour
    {
        [Tooltip("Titreyecek telefon görseli (komodindeki Phone objesi ya da çocuğu)")]
        [SerializeField] private Transform phoneProp;
        [Tooltip("Bildirim ekranı (UI agent'ının PhoneAlertView'ı)")]
        [SerializeField] private PhoneAlertViewBase view;
        [Tooltip("İsteğe bağlı zil sesi (klibi varsa döngüde çalar)")]
        [SerializeField] private AudioSource ringAudio;
        [Tooltip("Her titreşim patlamasında çağrılır (zil/titreşim sesi kancası)")]
        [SerializeField] private UnityEvent onRing = new UnityEvent();
        [Tooltip("Bildirim ekrana gelince (bildirim sesi kancası)")]
        [SerializeField] private UnityEvent onNotification = new UnityEvent();
        [Tooltip("Titreşim genliği (piksel)")]
        [SerializeField] private int vibratePixels = 1;
        [Tooltip("Titreşim adımı (sn)")]
        [SerializeField] private float vibrateStep = 0.04f;
        [Tooltip("Bir titreşim patlaması (sn)")]
        [SerializeField] private float burstSeconds = 0.6f;
        [Tooltip("Patlamalar arası sessizlik (sn)")]
        [SerializeField] private float pauseSeconds = 0.35f;
        [Tooltip("Kamera telefona ne kadar kaysın (0 = hiç, 1 = tam telefona)")]
        [SerializeField, Range(0f, 1f)] private float cameraFocus = 0.3f;
        [Tooltip("Kamera kayma süresi (sn)")]
        [SerializeField] private float cameraFocusSeconds = 0.6f;
        [Tooltip("Görünüm yokken bildirimin en fazla bekleyeceği süre (sn)")]
        [SerializeField] private float fallbackTimeout = 8f;

        private Vector3 restLocal;
        private bool hasRest;
        private Coroutine vibrateRoutine;

        public bool IsRinging { get; private set; }
        public Transform PhoneProp { get => phoneProp; set { phoneProp = value; hasRest = false; } }
        public PhoneAlertViewBase View { get => view; set => view = value; }

        /// <summary>true ise görünüm çağrılmaz (Console + kısa bekleme) — otomatik test için.</summary>
        public bool BypassBlockingViews { get; set; }
        /// <summary>Görünüm yokken ipucu göstermek için (mod kontrolcüsü verir).</summary>
        public InteractionHintBase Hint { get; set; }

        /// <summary>Telefon titremeye ve çalmaya başlar (engellemez).</summary>
        public void StartRinging()
        {
            if (IsRinging) return;
            IsRinging = true;
            Debug.Log("[EQ] telefon çalıyor (titreşim + zil)", this);
            if (ringAudio != null && ringAudio.clip != null)
            {
                ringAudio.loop = true;
                ringAudio.Play();
            }
            if (phoneProp != null)
            {
                if (!hasRest) { restLocal = phoneProp.localPosition; hasRest = true; }
                vibrateRoutine = StartCoroutine(Vibrate());
            }
            else if (onRing != null) onRing.Invoke();
        }

        /// <summary>Titreşim ve zil durur, telefon yerine döner.</summary>
        public void StopRinging()
        {
            if (!IsRinging) return;
            IsRinging = false;
            if (vibrateRoutine != null) StopCoroutine(vibrateRoutine);
            vibrateRoutine = null;
            if (phoneProp != null && hasRest) phoneProp.localPosition = restLocal;
            if (ringAudio != null) ringAudio.Stop();
        }

        private IEnumerator Vibrate()
        {
            float px = vibratePixels / 64f;
            while (IsRinging)
            {
                if (onRing != null) onRing.Invoke();
                int sign = 1;
                for (float t = 0f; t < burstSeconds && IsRinging; t += vibrateStep)
                {
                    phoneProp.localPosition = restLocal + new Vector3(sign * px, 0f, 0f);
                    sign = -sign;
                    yield return new WaitForSeconds(vibrateStep);
                }
                phoneProp.localPosition = restLocal;
                yield return new WaitForSeconds(pauseSeconds);
            }
        }

        /// <summary>Kamerayı odanın noktasından telefona doğru hafifçe kaydırır (ChapterCamera varsa yumuşak, yoksa doğrudan).</summary>
        public IEnumerator FocusCamera(Vector3 roomAnchor)
        {
            if (phoneProp == null || cameraFocus <= 0f) yield break;
            Vector3 target = Vector3.Lerp(roomAnchor, phoneProp.position, cameraFocus);
            yield return EarthquakeCameraUtil.MoveTo(target, false, cameraFocusSeconds);
        }

        /// <summary>Bildirimi gösterir ve BAŞLA'ya basılana kadar bekler.</summary>
        public IEnumerator ShowAlert(EqPhoneData data)
        {
            if (data == null) data = EarthquakeModeData.CreateDefault().phone;
            Debug.Log($"[EQ] TELEFON BİLDİRİMİ: {data.app} / {data.title} / {data.body} / ({data.note}) [{data.button}]", this);
            if (onNotification != null) onNotification.Invoke();

            if (view != null && !BypassBlockingViews)
            {
                yield return view.Show(data.app, data.title, data.body, data.note, data.button);
                yield break;
            }
            if (BypassBlockingViews)
            {
                yield return new WaitForSeconds(0.5f);
                yield break;
            }

            if (Hint != null) Hint.Show("E", data.button);
            yield return null;
            for (float t = 0f; t < fallbackTimeout && !GameInput.ContinuePressed; t += Time.deltaTime)
                yield return null;
            if (Hint != null) Hint.Hide();
        }
    }
}
