using System;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    [Serializable]
    public class EarthquakeBalanceSettings
    {
        [Min(0.1f)] public float initialSeconds = 4f;
        [Min(0.1f)] public float strongSeconds = 6f;
        [Min(0.1f)] public float irregularSeconds = 6f;
        [Range(0.1f, 0.6f)] public float greenZone = 0.3f;
        [Range(0.4f, 0.9f)] public float redThreshold = 0.65f;
        [Min(0)] public float waveForce = 2.2f;
        [Min(0.1f)] public float waveFrequency = 1f;
        [Min(0.1f)] public float correctionStrength = 5f;
        [Min(0.1f)] public float damping = 3f;
        [Min(0)] public float amberDrain = 1f;
        [Min(0)] public float redDrain = 5f;
        [Min(0)] public float releaseDrain = 3f;
        [Min(0)] public float releaseGrace = 0.25f;
        public int seed = 19;
        public float Duration => Mathf.Max(0.1f, initialSeconds) + Mathf.Max(0.1f, strongSeconds) + Mathf.Max(0.1f, irregularSeconds);
    }

    /// <summary>Oyun alıştırması; sismik simülasyon veya hayatta kalma olasılığı değildir. Sabit alt adımlarla kararlı.</summary>
    public sealed class EarthquakeBalanceModel
    {
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
