using UnityEngine;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>Basit sprite döngüsü: camdaki yağmur, sel suyunun akışı, aracın dörtlü flaşörü.</summary>
    public class SpriteFrameLoop : MonoBehaviour
    {
        [Tooltip("Boşsa bu objedeki SpriteRenderer")]
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [Tooltip("Bir karenin süresi (sn)")]
        [SerializeField] private float frameSeconds = 0.15f;
        [Tooltip("Başlangıç fazı (sn), aynı animasyonlar aynı anda oynamasın diye")]
        [SerializeField] private float phase;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (target == null || frames == null || frames.Length == 0 || frameSeconds <= 0f) return;
            int i = Mathf.FloorToInt((Time.time + phase) / frameSeconds) % frames.Length;
            if (frames[i] != null && target.sprite != frames[i]) target.sprite = frames[i];
        }
    }
}
