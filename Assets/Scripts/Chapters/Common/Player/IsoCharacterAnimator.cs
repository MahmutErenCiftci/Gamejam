using System;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// İzometrik karakter için 8 yönlü sprite animasyonu. Her yönün yürüme kareleri ayrı tutulur;
    /// karakter durunca baktığı yönün ilk karesi gösterilir. Yönler ekran yönüdür (0° = doğu/sağ).
    /// Kareler Assets/Art/Characters/CHR_&lt;Karakter&gt;_Walk_&lt;Yön&gt;.png sheet'lerinden gelir.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class IsoCharacterAnimator : MonoBehaviour
    {
        public enum Direction { East, NorthEast, North, NorthWest, West, SouthWest, South, SouthEast }

        [Serializable]
        public class DirectionFrames
        {
            public Direction direction;
            public Sprite[] frames;
        }

        [SerializeField] private DirectionFrames[] walk = Array.Empty<DirectionFrames>();
        [SerializeField] private float framesPerSecond = 6f;
        [SerializeField] private Direction startFacing = Direction.South;

        private SpriteRenderer spriteRenderer;
        private Direction facing;
        private bool moving;
        private float frameTimer;
        private int frameIndex;
        private Sprite pose;

        public Direction Facing => facing;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            facing = startFacing;
            Refresh();
        }

        /// <summary>Hareket yönüne göre yürür ya da durur (vektör sıfıra yakınsa durur, yön korunur).</summary>
        public void SetMotion(Vector2 direction)
        {
            bool nowMoving = direction.sqrMagnitude > 0.0001f;
            if (nowMoving) facing = ToDirection(direction);
            if (nowMoving != moving)
            {
                moving = nowMoving;
                frameIndex = 0;
                frameTimer = 0f;
            }
            if (!moving) Refresh();
        }

        /// <summary>Karakteri verilen yöne çevirir (ör. oda değişince güneye, anneye dönünce batıya).</summary>
        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            facing = ToDirection(direction);
            Refresh();
        }

        /// <summary>Animasyonun yerine sabit bir poz gösterir (ör. Çök–Kapan–Tutun). ClearPose ile geri döner.</summary>
        public void ShowPose(Sprite sprite)
        {
            pose = sprite;
            Refresh();
        }

        public void ClearPose()
        {
            pose = null;
            Refresh();
        }

        private void Update()
        {
            if (pose != null || !moving) return;
            var frames = FramesFor(facing);
            if (frames == null || frames.Length == 0) return;

            frameTimer += Time.deltaTime;
            float frameTime = 1f / Mathf.Max(0.01f, framesPerSecond);
            while (frameTimer >= frameTime)
            {
                frameTimer -= frameTime;
                frameIndex = (frameIndex + 1) % frames.Length;
            }
            spriteRenderer.sprite = frames[frameIndex];
        }

        private void Refresh()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (pose != null)
            {
                spriteRenderer.sprite = pose;
                return;
            }
            var frames = FramesFor(facing);
            if (frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[moving ? frameIndex % frames.Length : 0];
        }

        private Sprite[] FramesFor(Direction direction)
        {
            if (walk == null) return null;
            foreach (var entry in walk)
                if (entry != null && entry.direction == direction) return entry.frames;
            return null;
        }

        /// <summary>Vektörü en yakın 8 yöne çevirir: 0° doğu, saat yönünün tersine 45°'lik dilimler.</summary>
        public static Direction ToDirection(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(angle / 45f);
            return (Direction)(((index % 8) + 8) % 8);
        }
    }
}
