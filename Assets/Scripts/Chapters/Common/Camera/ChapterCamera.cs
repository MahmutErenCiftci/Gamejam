using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Bölüm kamerası (Main Camera'ya eklenir). Odalar aynı sahnede farklı dünya konumlarında durur; adıma girilince
    /// ChapterControllerBase kamerayı ya sabit bir noktaya (oda: StepVisual.cameraAnchor) koyar ya da oyuncuyu bir alan
    /// içinde takip ettirir (sokak: StepVisual.cameraBounds). Oda değişimi kararma altında olduğundan kesme (cut)
    /// kullanılır, aynı oda içinde yumuşak kayar. Konum piksel ızgarasına (1 / pixelsPerUnit) yuvarlanır, böylece
    /// PixelPerfectCamera ile görüntü titremez. Sabit noktaya varınca kamerayı bir daha yazmaz (ör. sarsıntı bileşeni
    /// aynı kamerayı oynatabilsin).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ChapterCamera : MonoBehaviour
    {
        private enum Mode { Idle, Fixed, Follow }

        [Tooltip("Sprite'ların PPU değeri; konum 1/PPU'ya yuvarlanır")]
        [SerializeField] private float pixelsPerUnit = 64f;
        [Tooltip("Takipte kameranın oyuncuya yetişme süresi (sn, SmoothDamp)")]
        [SerializeField] private float followSmoothTime = 0.2f;
        [Tooltip("Aynı oda içinde sabit noktaya kayma süresi (sn, SmoothDamp)")]
        [SerializeField] private float panSmoothTime = 0.45f;
        [Tooltip("Takipte oyuncunun ayak noktasına eklenen kayma (gövde ekranın ortasına gelsin)")]
        [SerializeField] private Vector2 followOffset = new Vector2(0f, 0.5f);

        private Camera cam;
        private Mode mode;
        private Transform target;
        private CameraBounds area;
        private Vector2 fixedPoint;
        private Vector2 logical;   // yuvarlanmamış konum (yumuşak hareket bunun üstünde)
        private Vector2 velocity;
        private bool cutPending;

        /// <summary>Takip edilen hedef (takip modunda değilse null).</summary>
        public Transform Target => mode == Mode.Follow ? target : null;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            logical = transform.position;
        }

        /// <summary>Kamerayı sabit bir noktaya ortalar. cut: hemen geç (kararma altında); değilse yumuşak kayar.</summary>
        public void ShowPoint(Vector3 point, bool cut)
        {
            mode = Mode.Fixed;
            target = null;
            area = null;
            fixedPoint = point;
            Begin(cut);
        }

        /// <summary>Hedefi (oyuncu) takip eder; bounds verilirse görüntü o alanın dışına çıkmaz.</summary>
        public void Follow(Transform followTarget, CameraBounds bounds, bool cut)
        {
            if (followTarget == null)
            {
                Release();
                return;
            }
            mode = Mode.Follow;
            target = followTarget;
            area = bounds;
            Begin(cut);
        }

        /// <summary>Kamerayı bırakır: bulunduğu yerde kalır, bu bileşen artık konumu yazmaz.</summary>
        public void Release()
        {
            mode = Mode.Idle;
            target = null;
            area = null;
        }

        private void Begin(bool cut)
        {
            logical = transform.position;
            velocity = Vector2.zero;
            cutPending = cut;
            if (cut) Step(0f); // aynı karede yerleşsin (kararma açılmadan önce)
        }

        private void LateUpdate()
        {
            if (mode == Mode.Idle) return;
            Step(Time.deltaTime);
        }

        private void Step(float dt)
        {
            Vector2 desired = mode == Mode.Follow && target != null
                ? (Vector2)target.position + followOffset
                : fixedPoint;
            desired = Clamp(desired);

            if (cutPending || dt <= 0f)
            {
                logical = desired;
                velocity = Vector2.zero;
                cutPending = false;
            }
            else
            {
                float smooth = mode == Mode.Follow ? followSmoothTime : panSmoothTime;
                logical = Vector2.SmoothDamp(logical, desired, ref velocity, Mathf.Max(0.01f, smooth), Mathf.Infinity, dt);
            }

            Vector2 snapped = Snap(logical);
            transform.position = new Vector3(snapped.x, snapped.y, transform.position.z);

            // Sabit noktaya varıldı: bir daha yazma (sarsıntı vb. kamerayı serbestçe oynatabilsin).
            if (mode == Mode.Fixed && (logical - desired).sqrMagnitude < 0.000001f)
                mode = Mode.Idle;
        }

        // Takip alanı: görüntünün yarı boyutu kadar içeriden sınırla; alan ekrandan darsa ortala.
        private Vector2 Clamp(Vector2 p)
        {
            if (mode != Mode.Follow || area == null || cam == null) return p;
            Rect r = area.WorldRect;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            p.x = r.width <= 2f * halfW ? r.center.x : Mathf.Clamp(p.x, r.xMin + halfW, r.xMax - halfW);
            p.y = r.height <= 2f * halfH ? r.center.y : Mathf.Clamp(p.y, r.yMin + halfH, r.yMax - halfH);
            return p;
        }

        private Vector2 Snap(Vector2 p)
        {
            if (pixelsPerUnit <= 0f) return p;
            return new Vector2(Mathf.Round(p.x * pixelsPerUnit) / pixelsPerUnit,
                Mathf.Round(p.y * pixelsPerUnit) / pixelsPerUnit);
        }
    }
}
