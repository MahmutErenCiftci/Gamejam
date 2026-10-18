using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Apartman giriş kapısının dünya görseli (FE_ApartmentFinal/Door). Kanat, sol menteşesine doğru içeri açılır: her açı için
    /// önceden çizilmiş kare (izometrik izdüşüm, Assets/Art/FloodEvening/Final/World/PROP_FE_Apt_Door*.png). Kanat cephenin
    /// (deliği olan PROP_FE_Apt_Facade) arkasında çizildiği için yalnız kapı boşluğundan görünür; açıldıkça arkadaki sıcak hol
    /// (ışıktan etkilenmeyen iç görsel) ve zemine taşan ışık (nokta + sprite ışığı) belirir.
    /// Kapalıyken kol ve tuş takımı ayrı katmanlardır (kol aşağı/yukarı, tuş takımı ışığı: bekleme / kırmızı / yeşil).
    /// Holün otomatik lambası doğru koddan sonra yanar (cam sıcak parlar). Kapı yalnız Unlatch → Ajar → SwingOpen ile açılır;
    /// bu bileşen kendi başına hiçbir şeyi tetiklemez, sırayı ApartmentFinale yönetir.
    /// </summary>
    public class ApartmentDoor : MonoBehaviour, IFloodEveningResettable
    {
        public enum KeypadState { Off, Idle, Red, Green }

        [Header("Kanat (açıya göre kareler; 0 = kapalı)")]
        [SerializeField] private SpriteRenderer leaf;
        [SerializeField] private Sprite[] leafFrames = Array.Empty<Sprite>();
        [Tooltip("Her karenin kanat açısı (derece, leafFrames ile aynı sıra)")]
        [SerializeField] private float[] frameAngles = Array.Empty<float>();
        [Tooltip("Camın sıcak parlaması (ışıktan etkilenmez), kanat kareleriyle aynı sıra")]
        [SerializeField] private SpriteRenderer glassGlow;
        [SerializeField] private Sprite[] glowFrames = Array.Empty<Sprite>();

        [Header("Kapalı kanadın üstündekiler")]
        [SerializeField] private SpriteRenderer handle;
        [SerializeField] private Sprite handleUp;
        [SerializeField] private Sprite handleDown;
        [SerializeField] private SpriteRenderer keypad;
        [SerializeField] private Sprite keypadOff;
        [SerializeField] private Sprite keypadIdle;
        [SerializeField] private Sprite keypadRed;
        [SerializeField] private Sprite keypadGreen;

        [Header("Hol ve ışık")]
        [Tooltip("Kapının arkasındaki hol (Sprite-Unlit; lamba kapalıyken koyu)")]
        [SerializeField] private SpriteRenderer interior;
        [SerializeField] private Color interiorDark = new Color(0.10f, 0.12f, 0.20f, 1f);
        [Tooltip("Zemine düşen sıcak ışık havuzu (Sprite-Unlit, yarı saydam)")]
        [SerializeField] private SpriteRenderer spillGlow;
        [SerializeField, Range(0f, 1f)] private float spillGlowAlpha = 0.5f;
        [SerializeField] private Light2D doorLight;
        [SerializeField] private float doorLightMax = 1.15f;
        [Tooltip("Kapı kapalı ve hol lambası yanıkken camdan sızan ışık oranı")]
        [SerializeField, Range(0f, 1f)] private float closedLeak = 0.15f;
        [SerializeField] private Light2D spillLight;
        [SerializeField] private float spillLightMax = 1.5f;
        [Tooltip("Işıkların hedefe yaklaşma hızı (1/sn)")]
        [SerializeField, Min(0.5f)] private float lightLerp = 6f;

        [Header("Hareket")]
        [SerializeField] private float unlatchAngle = 3f;
        [SerializeField] private float ajarAngle = 13f;
        [SerializeField] private float openAngle = 90f;
        [SerializeField, Min(0.05f)] private float unlatchSeconds = 0.18f;
        [SerializeField, Min(0.1f)] private float ajarSeconds = 0.8f;
        [SerializeField, Min(0.1f)] private float swingSeconds = 1.05f;

        [Header("Ses (Resources/Audio/SFX)")]
        [SerializeField] private string beepSfx = "SFX_Final_LockBeep";
        [SerializeField] private string wrongSfx = "SFX_Wrong";
        [SerializeField] private string okSfx = "SFX_Correct";
        [SerializeField] private string creakSfx = "SFX_Final_DoorCreak";
        [SerializeField] private string openSfx = "SFX_Final_DoorOpen";
        [SerializeField] private string closeSfx = "SFX_Final_DoorClose";

        private float angle;
        private float hall01, hallTarget;
        private float light01;
        private KeypadState keypadState = KeypadState.Off;
        private Coroutine keypadRoutine;
        private bool isHandleDown;

        public float Angle => angle;
        public bool IsClosed => angle < 0.5f;
        public bool IsOpen => angle >= openAngle - 0.5f;
        public bool HallLightOn => hallTarget > 0.5f;
        public KeypadState Keypad => keypadState;
        public bool HandleIsDown => isHandleDown;

        private void Awake() => ApplyAll(true);

        private void OnEnable() => FloodEveningHub.ResetRequested += ResetModule;

        private void OnDisable()
        {
            FloodEveningHub.ResetRequested -= ResetModule;
            if (keypadRoutine != null) { StopCoroutine(keypadRoutine); keypadRoutine = null; }
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-lightLerp * Time.deltaTime);
            hall01 = Mathf.Lerp(hall01, hallTarget, k);
            float open01 = Mathf.Clamp01(angle / Mathf.Max(1f, openAngle));
            float target = hall01 * Mathf.Lerp(closedLeak, 1f, Mathf.Pow(open01, 0.6f));
            light01 = Mathf.Lerp(light01, target, k);
            ApplyLights(hall01, open01);
        }

        // =====================================================================
        // Kilit / kol / hol lambası
        // =====================================================================

        public void SetKeypad(KeypadState state)
        {
            if (keypadRoutine != null) { StopCoroutine(keypadRoutine); keypadRoutine = null; }
            ShowKeypad(state);
        }

        /// <summary>Hatalı: kısa kırmızı yanıp sönme (+ hata bipi), sonra beklemeye döner. withSound=false: kilit yakın planı
        /// kendi sesini (SFX_Final_PinWrong) zaten çalıyor, dünya yalnız ışıkla eşlik eder.</summary>
        public void FlashWrong(bool withSound = true)
        {
            if (withSound) { GameAudio.PlaySfx(beepSfx, 0.45f); GameAudio.PlaySfx(wrongSfx, 0.3f); }
            if (keypadRoutine != null) StopCoroutine(keypadRoutine);
            keypadRoutine = StartCoroutine(Blink(KeypadState.Red, 3, KeypadState.Idle));
        }

        /// <summary>Doğru kod: yeşil (+ onay sesi); hol lambası yanar (cam sıcak parlar). Kapı AÇILMAZ.
        /// withSound=false: yakın plan kendi sesini (SFX_Final_PinOk) çalıyor.</summary>
        public void ShowAccepted(bool withSound = true)
        {
            SetKeypad(KeypadState.Green);
            if (withSound) { GameAudio.PlaySfx(beepSfx, 0.4f); GameAudio.PlaySfx(okSfx, 0.45f); }
            SetHallLight(true);
        }

        public void SetHallLight(bool on) => hallTarget = on ? 1f : 0f;

        public void SetHandleDown(bool down)
        {
            isHandleDown = down;
            if (handle != null) handle.sprite = down && handleDown != null ? handleDown : handleUp;
        }

        private IEnumerator Blink(KeypadState color, int times, KeypadState after)
        {
            for (int i = 0; i < times; i++)
            {
                ShowKeypad(color);
                for (float t = 0f; t < 0.14f; t += Time.deltaTime) yield return null;
                ShowKeypad(KeypadState.Off);
                for (float t = 0f; t < 0.1f; t += Time.deltaTime) yield return null;
            }
            ShowKeypad(after);
            keypadRoutine = null;
        }

        private void ShowKeypad(KeypadState state)
        {
            keypadState = state;
            if (keypad == null) return;
            keypad.sprite = state == KeypadState.Red ? keypadRed : state == KeypadState.Green ? keypadGreen : state == KeypadState.Idle ? keypadIdle : keypadOff;
            keypad.enabled = IsClosed && keypad.sprite != null;
        }

        // =====================================================================
        // Açılış / kapanış
        // =====================================================================

        /// <summary>Mandal geri çekilir: tık + gıcırtının başı (SFX_Final_DoorCreak), kol yukarı döner, kanat birkaç derece oynar.</summary>
        public IEnumerator Unlatch()
        {
            GameAudio.PlaySfx(creakSfx, 0.8f);
            yield return Animate(angle, unlatchAngle, unlatchSeconds, Ease.Out);
            SetHandleDown(false);
        }

        /// <summary>Gıcırdayarak aralanır (gıcırtı Unlatch'ta başladı): kapı boşluğunda ince sıcak ışık çizgisi.</summary>
        public IEnumerator Ajar()
        {
            yield return Animate(angle, ajarAngle, ajarSeconds, Ease.InOut);
        }

        /// <summary>Menteşesine doğru tamamen açılır; ışık zemine taşar.</summary>
        public IEnumerator SwingOpen()
        {
            GameAudio.PlaySfx(openSfx, 0.85f);
            yield return Animate(angle, openAngle, swingSeconds, Ease.Out);
        }

        /// <summary>Yavaşça kapanır (sonunda kapanma sesi), dışarı taşan ışık çekilir; camdan hafif sızıntı kalır.</summary>
        public IEnumerator Close(float seconds)
        {
            GameAudio.PlaySfx(closeSfx, 0.8f);
            yield return Animate(angle, 0f, seconds, Ease.InOut);
            SetHandleDown(false);
        }

        private enum Ease { Out, InOut }

        private IEnumerator Animate(float from, float to, float seconds, Ease ease)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / seconds);
                k = ease == Ease.Out ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k * (3f - 2f * k);
                SetAngle(Mathf.Lerp(from, to, k));
                yield return null;
            }
            SetAngle(to);
        }

        /// <summary>Kanadı verilen açıya getirir (en yakın kare). Kapalıyken kol ve tuş takımı katmanları görünür.</summary>
        public void SetAngle(float degrees)
        {
            angle = Mathf.Clamp(degrees, 0f, openAngle);
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < frameAngles.Length && i < leafFrames.Length; i++)
            {
                float d = Mathf.Abs(frameAngles[i] - angle);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (leaf != null && leafFrames.Length > 0) leaf.sprite = leafFrames[Mathf.Clamp(best, 0, leafFrames.Length - 1)];
            if (glassGlow != null && glowFrames.Length > 0) glassGlow.sprite = glowFrames[Mathf.Clamp(best, 0, glowFrames.Length - 1)];
            bool closedFrame = best == 0;
            if (handle != null) handle.enabled = closedFrame;
            if (keypad != null) keypad.enabled = closedFrame && keypad.sprite != null;
        }

        private void ApplyLights(float hall, float open01)
        {
            if (glassGlow != null) { var c = glassGlow.color; c.a = hall; glassGlow.color = c; }
            if (interior != null) interior.color = Color.Lerp(interiorDark, Color.white, hall);
            float spill = hall * Mathf.SmoothStep(0f, 1f, open01);
            if (spillGlow != null) { var c = spillGlow.color; c.a = spillGlowAlpha * spill; spillGlow.color = c; spillGlow.enabled = c.a > 0.003f; }
            if (doorLight != null) { doorLight.intensity = doorLightMax * light01; doorLight.enabled = light01 > 0.003f; }
            if (spillLight != null) { spillLight.intensity = spillLightMax * spill; spillLight.enabled = spill > 0.003f; }
        }

        private void ApplyAll(bool instant)
        {
            SetHandleDown(false);
            ShowKeypad(KeypadState.Off);
            SetAngle(0f);
            hallTarget = 0f;
            if (instant) { hall01 = 0f; light01 = 0f; }
            ApplyLights(hall01, 0f);
        }

        public void ResetModule()
        {
            if (keypadRoutine != null) { StopCoroutine(keypadRoutine); keypadRoutine = null; }
            ApplyAll(true);
        }
    }
}
