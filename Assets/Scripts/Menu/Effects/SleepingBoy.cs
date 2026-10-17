using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Yatakta uyuyan çocuk: nefes kareleri (ileri-geri döngü), ara sıra kıpırdanma karesi ve battaniyenin
    // 1 piksel inip kalkması. Deprem önizlemesinde PlayStartled() ile kısa süre irkilir, sonra uykuya döner.
    // Kareler Inspector'dan değiştirilebilir.
    public class SleepingBoy : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [Tooltip("Nefes döngüsü (ileri-geri oynatılır).")]
        [SerializeField] Sprite[] breathFrames;
        [SerializeField] float breathFps = 2.5f;
        [Tooltip("Ara sıra araya giren yüz ifadesi kareleri.")]
        [SerializeField] Sprite[] stirFrames;
        [SerializeField] Vector2 stirInterval = new Vector2(6f, 11f);
        [SerializeField] float stirHold = 0.8f;
        [Tooltip("İrkilme: sırayla gösterilir, sonra uykuya döner.")]
        [SerializeField] Sprite[] startledFrames;
        [SerializeField] float startledFrameTime = 0.35f;

        [Header("Battaniye")]
        [SerializeField] Transform blanket;
        [SerializeField] int blanketRisePixels = 1;

        const float PixelsPerUnit = 16f;
        Vector3 blanketBase;
        int frame;
        int direction = 1;
        float frameTimer;
        float nextStir;
        bool overridden;

        void Awake()
        {
            if (blanket != null) blanketBase = blanket.localPosition;
            ScheduleStir();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            overridden = false;
        }

        void Update()
        {
            if (overridden || breathFrames == null || breathFrames.Length == 0) return;

            frameTimer += Time.unscaledDeltaTime;
            if (frameTimer >= 1f / breathFps)
            {
                frameTimer = 0f;
                if (breathFrames.Length > 1)
                {
                    if (frame + direction >= breathFrames.Length || frame + direction < 0) direction = -direction;
                    frame += direction;
                }
                body.sprite = breathFrames[frame];
                // Nefes alırken (döngünün üst yarısında) battaniye 1 piksel yükselir.
                if (blanket != null)
                {
                    bool inhale = frame >= breathFrames.Length / 2;
                    blanket.localPosition = blanketBase + new Vector3(0f, inhale ? blanketRisePixels / PixelsPerUnit : 0f, 0f);
                }
            }

            if (stirFrames != null && stirFrames.Length > 0 && Time.unscaledTime >= nextStir)
                StartCoroutine(Sequence(stirFrames, stirHold));
        }

        public void PlayStartled()
        {
            if (startledFrames == null || startledFrames.Length == 0) return;
            StopAllCoroutines();
            StartCoroutine(Sequence(startledFrames, startledFrameTime));
        }

        IEnumerator Sequence(Sprite[] frames, float hold)
        {
            overridden = true;
            foreach (Sprite sprite in frames)
            {
                body.sprite = sprite;
                yield return new WaitForSecondsRealtime(hold);
            }
            overridden = false;
            ScheduleStir();
        }

        void ScheduleStir() => nextStir = Time.unscaledTime + Random.Range(stirInterval.x, stirInterval.y);
    }
}
