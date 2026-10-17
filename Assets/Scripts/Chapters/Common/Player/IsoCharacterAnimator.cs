using System;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// İzometrik karakter için 8 yönlü animasyon. Asıl yol Animator Controller'dır:
    /// float "FaceX"/"FaceY" (bakılan yön, 8 birim vektörden birine yuvarlanmış) ve bool "IsMoving"
    /// parametrelerini yazar; "Idle" ve "Walk" 2D Simple Directional blend tree'leri bunlarla yön seçer.
    /// Yönler ekran yönüdür (0° = doğu/sağ, kuzey = ekranın yukarısı). Karakter durunca son yön korunur.
    /// Animator (veya controller'ı) yoksa eski yöntem kullanılır: <c>walk</c> dizisindeki kareler elle değiştirilir.
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

        private const float Diagonal = 0.70710678f;
        private static readonly int FaceXHash = Animator.StringToHash("FaceX");
        private static readonly int FaceYHash = Animator.StringToHash("FaceY");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        [Tooltip("FaceX/FaceY/IsMoving parametreli Animator. Boşsa aynı objedeki Animator alınır.")]
        [SerializeField] private Animator animator;
        [Tooltip("Başlangıçta bakılan yön")]
        [SerializeField] private Direction startFacing = Direction.South;

        [Header("Eski yöntem (Animator yoksa)")]
        [Tooltip("Animator Controller yoksa kullanılan yön başına yürüme kareleri")]
        [SerializeField] private DirectionFrames[] walk = Array.Empty<DirectionFrames>();
        [SerializeField] private float framesPerSecond = 6f;

        private SpriteRenderer spriteRenderer;
        private Direction facing;
        private bool moving;
        private Sprite pose;

        // Animator'a en son yazılan değerler (sadece değişince yazmak için).
        private bool hasSent;
        private Vector2 sentFace;
        private bool sentMoving;
        private bool pendingPush;

        // Eski yöntem kare sayacı.
        private float frameTimer;
        private int frameIndex;

        public Direction Facing => facing;
        public bool IsMoving => moving;

        private bool UsesAnimator => animator != null && animator.runtimeAnimatorController != null;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            // İzometrik derinlik sıralaması ayak pivotuna göre (Renderer2D özel eksen (0,1,0) ile sıralar).
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            if (animator == null) animator = GetComponent<Animator>();
            facing = startFacing;
            Refresh(true);
        }

        private void OnEnable()
        {
            hasSent = false;
            pendingPush = true;
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
            Refresh(false);
        }

        /// <summary>Karakteri yürütmeden verilen yöne çevirir (ör. oda değişince güneye, anneye dönünce batıya).</summary>
        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            facing = ToDirection(direction);
            Refresh(false);
        }

        /// <summary>Animasyonun yerine sabit bir poz gösterir (ör. Çök–Kapan–Tutun). ClearPose ile geri döner.</summary>
        public void ShowPose(Sprite sprite)
        {
            pose = sprite;
            if (pose == null)
            {
                ClearPose();
                return;
            }
            if (animator != null) animator.enabled = false;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = pose;
        }

        /// <summary>Pozu kaldırır; Animator yeniden açılır ve parametreler tekrar yazılır.</summary>
        public void ClearPose()
        {
            pose = null;
            if (animator != null && !animator.enabled)
            {
                animator.enabled = true;
                hasSent = false;
            }
            Refresh(true);
        }

        private void Update()
        {
            if (pose != null) return;
            if (UsesAnimator)
            {
                if (pendingPush) PushToAnimator(!hasSent);
                return;
            }

            if (!moving) return;
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

        private void Refresh(bool force)
        {
            if (pose != null) return;
            if (UsesAnimator)
            {
                PushToAnimator(force);
                return;
            }

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            var frames = FramesFor(facing);
            if (frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[moving ? frameIndex % frames.Length : 0];
        }

        /// <summary>FaceX/FaceY/IsMoving'i sadece değiştiyse yazar. Animator henüz hazır değilse sonraki Update'e bırakır.</summary>
        private void PushToAnimator(bool force)
        {
            if (!animator.isActiveAndEnabled || !animator.isInitialized)
            {
                pendingPush = true;
                return;
            }
            pendingPush = false;

            Vector2 face = ToVector(facing);
            bool all = force || !hasSent;
            if (all || face.x != sentFace.x) animator.SetFloat(FaceXHash, face.x);
            if (all || face.y != sentFace.y) animator.SetFloat(FaceYHash, face.y);
            if (all || moving != sentMoving) animator.SetBool(IsMovingHash, moving);
            sentFace = face;
            sentMoving = moving;
            hasSent = true;
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

        /// <summary>Yönün ekran birim vektörü (blend tree'deki FaceX/FaceY noktaları).</summary>
        public static Vector2 ToVector(Direction direction)
        {
            switch (direction)
            {
                case Direction.East: return new Vector2(1f, 0f);
                case Direction.NorthEast: return new Vector2(Diagonal, Diagonal);
                case Direction.North: return new Vector2(0f, 1f);
                case Direction.NorthWest: return new Vector2(-Diagonal, Diagonal);
                case Direction.West: return new Vector2(-1f, 0f);
                case Direction.SouthWest: return new Vector2(-Diagonal, -Diagonal);
                case Direction.South: return new Vector2(0f, -1f);
                default: return new Vector2(Diagonal, -Diagonal);
            }
        }
    }
}
