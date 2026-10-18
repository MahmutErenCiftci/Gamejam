using System;
using System.Collections;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Ana menünün açılışı: karanlıkta kırmızı 07:00 → oda ve uyuyan çocuk belirir → "AFET UYANIŞI" →
    // butonlar. Rüya bulutları burada yok; Yeni Oyun'dan sonra gelir. Her adım bir UI grubunu (CanvasGroup)
    // ya da sprite grubunu (SpriteFader) belirli bir zamanda açar/kapatır; sıra ve süreler Inspector'dan
    // ayarlanır. Space, Enter veya tıklama ile atlanır.
    public class MenuIntroController : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            [Tooltip("UI grubu (butonlar). Sprite grubu kullanılıyorsa boş bırakılır.")]
            public CanvasGroup group;
            [Tooltip("Dünya uzayındaki sprite grubu (karanlık, logo).")]
            public SpriteFader sprites;
            [Tooltip("Intro başından itibaren saniye.")]
            public float startTime;
            public float duration = 0.4f;
            [Tooltip("İşaretliyse grup kaybolur, değilse görünür hale gelir.")]
            public bool fadeOut;

            public float EndTime => startTime + duration;
            public float TargetAlpha => fadeOut ? 0f : 1f;
            public UnityEngine.Object Target => group != null ? (UnityEngine.Object)group : sprites;
        }

        [SerializeField] MainMenuController menu;
        [SerializeField] Step[] steps;

        [Tooltip("Bölümden menüye dönüldüğünde intro tekrar oynatılmaz.")]
        [SerializeField] bool playOnlyOncePerSession = true;

        static bool hasPlayed;
        const float MaxFrameStep = 1f / 20f;

        // Editörde domain reload kapalıysa statik alan Play oturumları arasında kalır; her oyun başında sıfırla.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => hasPlayed = false;

        float elapsed;
        float totalDuration;
        bool isPlaying;

        // Awake'te çalışır: menü girdisi her Start'tan önce kapatılmış olmalı.
        void Awake()
        {
            // Aynı grubun birden fazla adımı varsa Update'te en son başlayan adım en son uygulansın.
            Array.Sort(steps, (a, b) => a.startTime.CompareTo(b.startTime));
            foreach (Step step in steps)
                totalDuration = Mathf.Max(totalDuration, step.EndTime);

            if (playOnlyOncePerSession && hasPlayed)
            {
                Finish();
                return;
            }

            // Her grup, kendisine ait ilk adımın başlangıç değeriyle açılır.
            foreach (Step step in steps)
            {
                if (step.Target != null && FirstStepOf(step.Target) == step)
                    SetAlpha(step, 1f - step.TargetAlpha);
            }

            isPlaying = true;
            if (menu != null)
                menu.InputEnabled = false;
        }

        void Update()
        {
            if (!isPlaying)
                return;

            if (SkipPressed())
            {
                Finish();
                return;
            }

            // Sahne yüklendikten sonraki ilk kare(ler)in süresi saniyeleri bulabiliyor; sınırlanmazsa intro
            // ilk karede ileri sıçrıyor ve karanlık/saat hiç görünmüyordu.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
            foreach (Step step in steps)
            {
                if (step.Target == null || elapsed < step.startTime)
                    continue;
                float t = step.duration > 0f ? Mathf.Clamp01((elapsed - step.startTime) / step.duration) : 1f;
                SetAlpha(step, Mathf.Lerp(1f - step.TargetAlpha, step.TargetAlpha, t));
            }

            if (elapsed >= totalDuration)
                Finish();
        }

        static bool SkipPressed() => GameInput.SkipPressed || GameInput.ClickPressed;

        // Her grubu, ona ait en son biten adımın hedef değerine getirir ve menü girdisini açar.
        void Finish()
        {
            foreach (Step step in steps)
            {
                if (step.Target != null && LastStepOf(step.Target) == step)
                    SetAlpha(step, step.TargetAlpha);
            }

            isPlaying = false;
            hasPlayed = true;
            if (menu != null)
            {
                menu.InputEnabled = true;
                StartCoroutine(FocusNextFrame());
            }
        }

        // Intro'yu atlayan Space/tık aynı karede yeni odaklanan butonu tetiklemesin.
        IEnumerator FocusNextFrame()
        {
            yield return null;
            menu.OnIntroFinished();
        }

        Step FirstStepOf(UnityEngine.Object target)
        {
            Step first = null;
            foreach (Step step in steps)
                if (step.Target == target && (first == null || step.startTime < first.startTime))
                    first = step;
            return first;
        }

        Step LastStepOf(UnityEngine.Object target)
        {
            Step last = null;
            foreach (Step step in steps)
                if (step.Target == target && (last == null || step.EndTime >= last.EndTime))
                    last = step;
            return last;
        }

        // Tam görünmeyen UI grubu tıklanamaz; interactable'a dokunulmaz (butonlar gri tonla belirmesin).
        static void SetAlpha(Step step, float alpha)
        {
            if (step.group != null)
            {
                step.group.alpha = alpha;
                step.group.blocksRaycasts = alpha > 0.99f;
            }
            else if (step.sprites != null)
            {
                step.sprites.Alpha = alpha;
            }
        }
    }
}
