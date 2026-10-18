using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Cenk'in rüzgâra tepkisi (Cenk'e eklenir; FloodWindAmbience'ı okur). Cenk'in kendi 8 yönlü izometrik yürüme
    /// karelerinin rüzgâra yaslanmış halleri (FloodWindCenkPoses) kullanılır; yeni poz çizilmez, yön ve kare aynı kalır:
    ///  • Sürekli: hangi yöne yürürse yürüsün gövdesi rüzgârın geldiği yana, rüzgârın gücüyle orantılı yaslanır
    ///    (taban rüzgârda ~1 piksel, bastırmada 5–6 piksel). Yaslanma yay-sönümle izlenir, rüzgârın titreşimiyle kıpırdar.
    ///  • İrkilme: bastırma cephesi Cenk'e değdiği an gövde bir an rüzgârın gittiği yana itilir, sonra rüzgâra yaslanır.
    ///  • Siper alma (sert bastırma): Cenk durur, yüzünü rüzgârın geldiği izometrik yöne çevirir, ayakları yerinde
    ///    rüzgâra yaslanıp dayanır; ara sıra dengesini korumak için küçük bir adım atar. Rüzgâr dinince yürümeye döner.
    ///  • Sendeleme (sert bastırmada rüzgâra karşı yürürken): rüzgâra dönük geri geri iki adım sendeler (yürüme
    ///    kareleri tersten), gövde önce geriye savrulur sonra rüzgâra yaslanarak toparlanır. Küçük geri itme Rigidbody
    ///    hızıyla verilir (collider'lara saygılı, yavaşlamayla söner); stumbleShove = 0 ile kapatılır.
    /// Bastırma, rüzgâr üstündeki güce bakılarak önceden sezilir (cephe frontSpeed ile ilerler). Siper/sendeleme
    /// sırasında hareket kısa süre kilitlenir (IsoPlayerController.CanMove); serbest dolaşım biterse (diyalog) tepki
    /// hemen bırakılır. LateUpdate'te CenkOutfit'ten sonra çalışır, giyilen görünümün karesini yaslar.
    /// </summary>
    [DefaultExecutionOrder(1010)]
    [RequireComponent(typeof(SpriteRenderer))]
    public class FloodWindCenkReaction : MonoBehaviour
    {
        [Tooltip("Boşsa sahnedeki FloodWindAmbience.")]
        [SerializeField] private FloodWindAmbience wind;
        [SerializeField] private FloodWindCenkPoses poses;
        [Tooltip("İşaretliyse siper/sendeleme sadece Hub.FreeRoam true iken olur (bölüm). Test için kapatılabilir.")]
        [SerializeField] private bool requireFreeRoam = true;

        [Header("Yaslanma")]
        [Tooltip("Tepe bastırmada yürürken en fazla yaslanma (piksel).")]
        [SerializeField, Range(0, FloodWindCenkPoses.MaxShear)] private int maxLean = 5;
        [Tooltip("Siperde yaslanma (piksel).")]
        [SerializeField, Range(1, FloodWindCenkPoses.MaxShear)] private int braceLean = 6;
        [Tooltip("Bu güçte yaslanma başlar, +1 güçte en fazlaya ulaşır.")]
        [SerializeField] private float leanFrom = 0.3f;

        [Header("Eşikler (FloodWindAmbience.StrengthAt; taban ~0.4)")]
        [SerializeField] private float flinchAt = 0.8f;
        [SerializeField] private float braceAt = 1.1f;
        [Tooltip("Bu güçte rüzgâra karşı yürürken sendeler.")]
        [SerializeField] private float stumbleAt = 1.2f;
        [SerializeField] private float releaseAt = 0.8f;
        [Tooltip("Rüzgâra karşı sayılan en büyük yön benzerliği (hareket · rüzgâr).")]
        [SerializeField, Range(-1f, 0f)] private float againstDot = -0.3f;
        [SerializeField, Min(0f)] private float cooldown = 2.5f;

        [Header("Süreler")]
        [SerializeField, Min(0.3f)] private float minBraceSeconds = 0.9f;
        [SerializeField, Min(0.5f)] private float maxBraceSeconds = 2.4f;
        [SerializeField, Min(0.4f)] private float stumbleSeconds = 1.1f;
        [Tooltip("Sendelemede her adımın geri itme hızı (dünya birimi/sn). 0 = yerinde sendeler.")]
        [SerializeField, Range(0f, 2f)] private float stumbleShove = 0.8f;
        [SerializeField] private bool lockMovement = true;

        private enum State { Idle, Brace, Stumble }

        private SpriteRenderer sr;
        private IsoPlayerController player;
        private IsoCharacterAnimator charAnim;
        private CenkOutfit outfit;
        private Rigidbody2D body;

        private State state;
        private float t, cool, prevS, sm, lean, leanVel, nextShuffle, shuffleT;
        private bool locked, posing;
        private int poseFrame = -1;
        private IsoCharacterAnimator.Direction faceDir;
        private Vector2 up = Vector2.right;

        /// <summary>Siper ya da sendeleme oynuyor mu.</summary>
        public bool IsReacting => state != State.Idle;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            player = GetComponent<IsoPlayerController>();
            charAnim = GetComponent<IsoCharacterAnimator>();
            outfit = GetComponent<CenkOutfit>();
            body = GetComponent<Rigidbody2D>();
        }

        private void OnDisable()
        {
            Stop();
            lean = leanVel = 0f;
            if (poses != null && sr != null) sr.sprite = poses.SourceOf(sr.sprite);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (wind == null) wind = FindAnyObjectByType<FloodWindAmbience>();
            if (poses == null) return;
            bool hasWind = wind != null && wind.isActiveAndEnabled;
            bool allowed = !requireFreeRoam || FloodEveningHub.FreeRoam;
            if (state != State.Idle && (!allowed || !hasWind)) Stop();

            Vector2 pos = transform.position;
            float s = hasWind ? wind.StrengthAt(pos) : 0f;
            sm = Mathf.Lerp(sm, s, 1f - Mathf.Exp(-dt * 6f));
            cool -= dt;
            if (hasWind) up = -wind.Direction;
            float side = Mathf.Sign(up.x == 0f ? 1f : up.x) * Mathf.Min(1f, Mathf.Abs(up.x) * 1.25f + 0.2f);

            // Bastırma cephesi değdi: gövde bir an rüzgârın gittiği yana itilir; sert bastırmada siper / sendeleme.
            if (hasWind && s >= flinchAt && prevS < flinchAt)
            {
                leanVel -= 26f;
                if (state == State.Idle && allowed && cool <= 0f && (player == null || player.CanMove)) Decide(pos, s);
            }
            prevS = s;

            float k = Mathf.Clamp01(sm - leanFrom);
            float jitter = (Mathf.PerlinNoise(Time.time * 6f, 0.37f) - 0.5f) * 1.6f * k;
            if (state != State.Idle) t += dt;
            switch (state)
            {
                case State.Idle:
                    Spring(maxLean * k + jitter, 10f, 0.4f, dt);
                    break;

                case State.Brace:
                    Spring(Mathf.Lerp(maxLean, braceLean, k) * Mathf.Max(k, 0.5f) + jitter, 12f, 0.45f, dt);
                    // Denge adımı: ara sıra bir kare öne basıp geri döner.
                    if (t >= nextShuffle) { shuffleT = 0.16f; nextShuffle = t + Random.Range(0.6f, 1.1f); }
                    shuffleT -= dt;
                    ShowFrame(shuffleT > 0f ? 1 : 0);
                    if ((t >= minBraceSeconds && s < releaseAt) || t >= maxBraceSeconds) Finish();
                    break;

                case State.Stumble:
                    UpdateStumble(dt);
                    if (t >= stumbleSeconds) Finish();
                    break;
            }

            int shear = Mathf.Clamp(Mathf.RoundToInt(lean * side), -FloodWindCenkPoses.MaxShear, FloodWindCenkPoses.MaxShear);
            var leaned = poses.Lean(sr.sprite, shear);
            if (leaned != null && leaned != sr.sprite) sr.sprite = leaned;
        }

        private void Decide(Vector2 pos, float s)
        {
            // Rüzgâr üstündeki güç = birazdan buraya gelecek güç.
            float peak = s;
            for (int i = 1; i <= 3; i++) peak = Mathf.Max(peak, wind.StrengthAt(pos + up * (0.9f * i)));
            if (peak < braceAt) return;

            Vector2 move = GameInput.MoveVector;
            bool walking = player != null && player.CanMove && move.sqrMagnitude > 0.04f;
            bool against = walking && Vector2.Dot(move.normalized, wind.Direction) <= againstDot;
            Begin(peak >= stumbleAt && against ? State.Stumble : State.Brace);
        }

        private void Begin(State next)
        {
            state = next;
            t = 0f;
            nextShuffle = Random.Range(0.5f, 0.8f);
            shuffleT = 0f;
            poseFrame = -1;
            faceDir = IsoCharacterAnimator.ToDirection(up);
            if (charAnim != null) charAnim.Face(up);
            if (lockMovement && player != null && !locked && player.CanMove)
            {
                player.CanMove = false;
                locked = true;
            }
        }

        // Geri geri iki adım: önce geriye savrulur, sonra rüzgâra yaslanarak toparlanır.
        private static readonly float[] StumbleKeys = { 0f, 0f, 0.15f, -4f, 0.35f, -2f, 0.55f, -3f, 0.75f, 2f, 1f, 4f };
        private static readonly int[] StumbleFrames = { 0, 5, 4, 3, 2, 1, 0, 0 };

        private void UpdateStumble(float dt)
        {
            float u = t / stumbleSeconds;
            Spring(Keyed(StumbleKeys, u), 20f, 0.5f, dt);
            int f = StumbleFrames[Mathf.Min(StumbleFrames.Length - 1, Mathf.FloorToInt(u * StumbleFrames.Length))];
            if (f != poseFrame && (f == 5 || f == 2) && body != null && stumbleShove > 0f)
                body.linearVelocity = wind.Direction * stumbleShove;
            ShowFrame(f);
        }

        private void ShowFrame(int frame)
        {
            if (frame == poseFrame) return;
            var look = outfit != null ? outfit.CurrentLook : CenkOutfit.Look.SchoolBag;
            var sprite = poses.Frame(look, faceDir, frame);
            if (sprite == null) return;
            poseFrame = frame;
            posing = true;
            if (charAnim != null) charAnim.ShowPose(sprite);
            else sr.sprite = sprite;
        }

        private static float Keyed(float[] keys, float u)
        {
            for (int i = 2; i < keys.Length; i += 2)
            {
                if (u > keys[i]) continue;
                float a = Mathf.InverseLerp(keys[i - 2], keys[i], u);
                return Mathf.Lerp(keys[i - 1], keys[i + 1], EventsUi.Smooth01(a));
            }
            return keys[keys.Length - 1];
        }

        private void Spring(float target, float w, float zeta, float dt)
        {
            float h = dt / 2f;
            for (int i = 0; i < 2; i++)
            {
                leanVel += (w * w * (target - lean) - 2f * zeta * w * leanVel) * h;
                lean += leanVel * h;
            }
        }

        private void Finish()
        {
            Stop();
            cool = cooldown;
        }

        private void Stop()
        {
            state = State.Idle;
            t = 0f;
            poseFrame = -1;
            if (posing)
            {
                posing = false;
                if (charAnim != null) charAnim.ClearPose();
            }
            if (locked)
            {
                locked = false;
                // Diyalog vb. hareketi kendisi kilitlediyse ona dokunma.
                if (player != null && (!requireFreeRoam || FloodEveningHub.FreeRoam)) player.CanMove = true;
            }
        }
    }
}
