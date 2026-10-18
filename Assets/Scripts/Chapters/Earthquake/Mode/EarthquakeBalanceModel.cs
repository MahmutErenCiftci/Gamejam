using System;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    [Serializable]
    public class EarthquakeBalanceSettings
    {
        // Toplam ≈ 7 sn (lider isteği, 18 Eki): kısa ama okunur — hafif başlangıç, güçlü orta, düzensiz son.
        [Min(0.1f)] public float initialSeconds = 1.5f;
        [Min(0.1f)] public float strongSeconds = 3f;
        [Min(0.1f)] public float irregularSeconds = 2.5f;
        [Tooltip("Yeşil (güvenli) bölgenin yarı genişliği; 0.35 = ortadan iki yana %35")]
        [Range(0.1f, 0.6f)] public float greenZone = 0.35f;
        [Tooltip("Kırmızı bölge eşiği (AudioAutoWiring uyarı sesi de 0.65 kullanır)")]
        [Range(0.4f, 0.9f)] public float redThreshold = 0.65f;
        [Tooltip("Sarsıntının işareti itme gücü. Girdisiz kalınca işaret yer yer kırmızıya kayar; A/D ile kolayca geri gelir.")]
        [Min(0)] public float waveForce = 4.5f;
        [Tooltip("Düşük = yavaş, okunabilir itmeler (ilk kez oynayan da yetişir)")]
        [Min(0.1f)] public float waveFrequency = 0.75f;
        [Tooltip("A/D düzeltme gücü; en güçlü itmeden belirgin şekilde büyük olmalı")]
        [Min(0.1f)] public float correctionStrength = 6.5f;
        [Tooltip("Yüksek = tuş bırakılınca işaret hemen durur (savrulma / aşırı düzeltme olmaz)")]
        [Min(0.1f)] public float damping = 8f;
        // Kayıplar 7 sn'ye ölçeklendi (eski 16 sn'lik turla aynı toplam zorluk: ≈ ×2.2)
        [Min(0)] public float amberDrain = 2.2f;
        [Min(0)] public float redDrain = 11f;
        [Min(0)] public float releaseDrain = 6.5f;
        [Min(0)] public float releaseGrace = 0.25f;
        public int seed = 19;
        public float Duration => Mathf.Max(0.1f, initialSeconds) + Mathf.Max(0.1f, strongSeconds) + Mathf.Max(0.1f, irregularSeconds);
    }

    /// <summary>Oyun alıştırması; sismik simülasyon veya hayatta kalma olasılığı değildir. Sabit alt adımlarla kararlı.</summary>
    public sealed class EarthquakeBalanceModel
    {
        /// <summary>"İyi tutundun" için sürenin en az bu oranında SPACE basılı olmalı (holdCompleted ile aynı).</summary>
        public const float GoodHeldRatio = 0.65f;
        /// <summary>"İyi tutundun" için en düşük denge puanı (EarthquakeModeController sonuç kartıyla aynı eşik).</summary>
        public const float GoodScore = 35f;

        private readonly EarthquakeBalanceSettings settings;
        private double accumulator;
        private float heldSeconds, releaseSeconds;
        private const float Step = 1f / 120f;
        public float Position { get; private set; }
        public float Velocity { get; private set; }
        public float Score { get; private set; } = 100f;
        public float Elapsed { get; private set; }
        public bool Holding { get; private set; }
        public bool Finished => Elapsed >= settings.Duration;
        public float Progress => Mathf.Clamp01(Elapsed / settings.Duration);
        public float HeldRatio => Elapsed > 0 ? Mathf.Clamp01(heldSeconds / Elapsed) : 0;
        public float ReleasedSeconds => releaseSeconds;
        public int Stage => Elapsed < settings.initialSeconds ? 0 : Elapsed < settings.initialSeconds + settings.strongSeconds ? 1 : 2;
        public float Intensity => (Stage == 0 ? 0.3f : Stage == 1 ? 0.72f : 0.9f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Progress - 0.92f) / 0.08f)));
        public float Force => Wave(Elapsed);
        /// <summary>Denge ilerlemeye başladı mı (talimat bitti, ilk Tick geldi).</summary>
        public bool Started => Elapsed > 0f;
        public float Duration => settings.Duration;
        public float RemainingSeconds => Mathf.Max(0f, settings.Duration - Elapsed);
        /// <summary>İşaretin bölgesi: 0 yeşil, 1 amber, 2 kırmızı.</summary>
        public int Zone => Mathf.Abs(Position) > settings.redThreshold ? 2 : Mathf.Abs(Position) > settings.greenZone ? 1 : 0;
        /// <summary>Bitiş değerlendirmesi: yeterince tutundu ve dengeyi çoğunlukla korudu.</summary>
        public bool WentWell => HeldRatio >= GoodHeldRatio && Score >= GoodScore;

        public EarthquakeBalanceModel(EarthquakeBalanceSettings configuration) => settings = configuration ?? new EarthquakeBalanceSettings();

        public void Tick(float deltaTime, float correction, bool holding)
        {
            Holding = holding;
            if (deltaTime <= 0 || Finished) return;
            accumulator += deltaTime;
            while (accumulator + 0.00000001 >= Step && !Finished)
            {
                accumulator -= Step;
                float dt = Mathf.Min(Step, settings.Duration - Elapsed);
                Velocity += (Wave(Elapsed) + Mathf.Clamp(correction, -1, 1) * settings.correctionStrength) * dt;
                Velocity *= Mathf.Exp(-settings.damping * dt);
                Position = Mathf.Clamp(Position + Velocity * dt, -1f, 1f);
                if (Mathf.Abs(Position) >= 1f) Velocity = 0;
                Elapsed = Mathf.Min(settings.Duration, Elapsed + dt);
                if (holding) { heldSeconds += dt; releaseSeconds = 0; }
                else releaseSeconds += dt;
                float distance = Mathf.Abs(Position);
                float drain = distance > settings.redThreshold ? settings.redDrain : distance > settings.greenZone ? settings.amberDrain : 0;
                if (!holding && releaseSeconds > settings.releaseGrace) drain += settings.releaseDrain;
                Score = Mathf.Clamp(Score - drain * dt, 0, 100);
            }
        }

        /// <summary>Yalnızca otomatik QA; gerçek oyunda düzeltme GameInput.MoveX'ten gelir.</summary>
        public float AutoCorrection => Mathf.Clamp((-Force - Position * 7f - Velocity * 2f) / Mathf.Max(0.1f, settings.correctionStrength), -1, 1);

        private float Wave(float time)
        {
            float p = (settings.seed % 97) * 0.17f;
            float t = time * settings.waveFrequency;
            float slow = Mathf.Sin(t * 2.4f + p) + 0.3f * Mathf.Sin(t * 5.1f + p * 0.5f);
            float amplitude = time < settings.initialSeconds ? 0.35f : time < settings.initialSeconds + settings.strongSeconds ? 0.75f : 1f;
            if (time >= settings.initialSeconds + settings.strongSeconds)
                slow = (Mathf.Sin(t * 3.1f + p) + 0.45f * Mathf.Sin(t * 7.3f)) * (0.55f + 0.45f * Mathf.Sin(t * 1.2f) * Mathf.Sin(t * 1.2f));
            return slow * amplitude * settings.waveForce;
        }
    }
}
