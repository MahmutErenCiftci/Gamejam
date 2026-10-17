using UnityEngine;
using UnityEngine.Events;

namespace AfetUyanisi.Chapters.Rooms
{
    /// <summary>
    /// Yatak odasındaki çalar saat: çalarken ekranı ("07:00", kırmızı) flashInterval aralıkla yanıp söner,
    /// kapatılınca sabit yanar. Ses için isteğe bağlı AudioSource (klibi varsa döngüde çalar; klip şart değil).
    /// onAlarmStarted / onAlarmStopped ileride ses ve efekt bağlamak için.
    /// </summary>
    public class AlarmClock : MonoBehaviour
    {
        [Tooltip("Saatin ekranı (yanıp sönen sprite). Boşsa bu objedeki SpriteRenderer alınır.")]
        [SerializeField] private SpriteRenderer display;
        [Tooltip("Yanık hali: kırmızı 07:00. Boşsa ekranın mevcut sprite'ı kullanılır.")]
        [SerializeField] private Sprite onSprite;
        [Tooltip("Sönük hali. Boşsa yanıp sönme, ekranın rengi offTint ile karartılarak yapılır.")]
        [SerializeField] private Sprite offSprite;
        [Tooltip("offSprite yoksa sönük karede ekranın rengi")]
        [SerializeField] private Color offTint = new Color(1f, 1f, 1f, 0.25f);
        [Tooltip("Yanıp sönme aralığı (sn)")]
        [SerializeField] private float flashInterval = 0.5f;
        [Tooltip("İsteğe bağlı alarm sesi. Klibi yoksa sessiz çalışır.")]
        [SerializeField] private AudioSource alarmAudio;
        [Tooltip("Sahne başlar başlamaz çalsın mı (normalde BedroomIntroController başlatır)")]
        [SerializeField] private bool ringOnStart;

        [SerializeField] private UnityEvent onAlarmStarted = new UnityEvent();
        [SerializeField] private UnityEvent onAlarmStopped = new UnityEvent();

        private Color baseColor = Color.white;
        private float timer;
        private bool lit = true;

        public bool IsRinging { get; private set; }

        private void Awake()
        {
            if (display == null) display = GetComponent<SpriteRenderer>();
            if (display != null)
            {
                baseColor = display.color;
                if (onSprite == null) onSprite = display.sprite;
            }
            ApplyLit(true);
        }

        private void Start()
        {
            if (ringOnStart) StartAlarm();
        }

        /// <summary>Alarm çalmaya başlar: ekran yanıp söner, ses (varsa) döngüde çalar.</summary>
        public void StartAlarm()
        {
            if (IsRinging) return;
            IsRinging = true;
            timer = 0f;
            ApplyLit(true);
            if (alarmAudio != null && alarmAudio.clip != null)
            {
                alarmAudio.loop = true;
                alarmAudio.Play();
            }
            Debug.Log("[AlarmClock] Alarm çalıyor (07:00).", this);
            if (onAlarmStarted != null) onAlarmStarted.Invoke();
        }

        /// <summary>Alarmı kapatır: ekran sabit yanar, ses durur.</summary>
        public void StopAlarm()
        {
            if (!IsRinging) return;
            IsRinging = false;
            ApplyLit(true);
            if (alarmAudio != null) alarmAudio.Stop();
            Debug.Log("[AlarmClock] Alarm kapatıldı.", this);
            if (onAlarmStopped != null) onAlarmStopped.Invoke();
        }

        private void Update()
        {
            if (!IsRinging) return;
            timer += Time.deltaTime;
            float interval = Mathf.Max(0.05f, flashInterval);
            while (timer >= interval)
            {
                timer -= interval;
                ApplyLit(!lit);
            }
        }

        private void ApplyLit(bool on)
        {
            lit = on;
            if (display == null) return;
            if (offSprite != null)
            {
                display.sprite = on ? onSprite : offSprite;
                display.color = baseColor;
            }
            else
            {
                if (onSprite != null) display.sprite = onSprite;
                display.color = on ? baseColor : baseColor * offTint;
            }
        }
    }
}
