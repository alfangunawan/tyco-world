using System;
using TycoWorld.DayCycle.Authoring;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Deterministic placeholder motorbike sounds (spec #5 §7), used when no CC0 recording was found.</summary>
    public static class EngineSynth
    {
        public const int SampleRate = AmbienceSynth.SampleRate;
        const float Peak = 0.8f;
        const double BurstWidth = 0.15;
        const double BurstMean = BurstWidth * 0.5;

        /// <summary>Single-cylinder idle at 55 Hz (smooth firing bursts, two harmonics, a little noise) as a seamless loop.</summary>
        public static float[] EngineLoop(float seconds = 1f)
        {
            var rng = new Random(4021);
            int fade = SampleRate / 20;
            var raw = new float[(int)Math.Round(seconds * SampleRate) + fade];
            const double f = 55.0;
            for (int i = 0; i < raw.Length; i++)
            {
                double t = (double)i / SampleRate;
                double phase = t * f - Math.Floor(t * f);
                raw[i] = (float)(0.7 * Burst(phase) + 0.25 * Math.Sin(2.0 * Math.PI * f * t)
                                 + 0.15 * Math.Sin(4.0 * Math.PI * f * t) + 0.04 * (rng.NextDouble() * 2.0 - 1.0));
            }
            var loop = AmbienceSynth.Loop(raw, fade);
            RemoveDc(loop);
            AmbienceSynth.Normalize(loop, Peak);
            return loop;
        }

        /// <summary>Zero-mean firing bursts that speed up from 18 to 55 Hz, fade in quickly and fade out to silence at the end.</summary>
        public static float[] Starter(float seconds = 0.9f)
        {
            var rng = new Random(733);
            var samples = new float[(int)Math.Round(seconds * SampleRate)];
            double cycles = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                double k = (double)i / samples.Length;
                cycles += (18.0 + 37.0 * k) / SampleRate;
                double phase = cycles - Math.Floor(cycles);
                double envelope = Math.Min(1.0, k * 4.0) * (k > 0.85 ? (1.0 - k) / 0.15 : 1.0);
                samples[i] = (float)(envelope * (0.8 * (Burst(phase) - BurstMean) + 0.1 * (rng.NextDouble() * 2.0 - 1.0)));
            }
            AmbienceSynth.Normalize(samples, Peak);
            return samples;
        }

        /// <summary>"Tin-tin": two 0.22 s two-tone beeps (420 + 525 Hz, softly clipped) with a 0.08 s gap.</summary>
        public static float[] Horn()
        {
            int beep = (int)(0.22f * SampleRate);
            int gap = (int)(0.08f * SampleRate);
            var samples = new float[beep * 2 + gap];
            AddBeep(samples, 0, beep);
            AddBeep(samples, beep + gap, beep);
            AmbienceSynth.Normalize(samples, Peak);
            return samples;
        }

        /// <summary>A low thud: 70 Hz sine and a noise burst, both decaying fast.</summary>
        public static float[] Bump(float seconds = 0.3f)
        {
            var rng = new Random(977);
            var samples = new float[(int)Math.Round(seconds * SampleRate)];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                samples[i] = (float)(Math.Exp(-t * 18.0) * Math.Sin(2.0 * Math.PI * 70.0 * t)
                                     + 0.5 * Math.Exp(-t * 40.0) * (rng.NextDouble() * 2.0 - 1.0));
            }
            AmbienceSynth.Normalize(samples, Peak);
            return samples;
        }

        /// <summary>Raised-cosine firing burst over the first 15 % of a cycle, zero elsewhere: continuous, so the loop has no clicks.</summary>
        static double Burst(double phase) => phase < BurstWidth ? 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * phase / BurstWidth)) : 0.0;

        static void AddBeep(float[] samples, int start, int length)
        {
            int edge = SampleRate / 100;
            for (int i = 0; i < length && start + i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = Math.Min(1.0, Math.Min(i, length - 1 - i) / (double)edge);
                double tone = Math.Tanh(3.0 * Math.Sin(2.0 * Math.PI * 420.0 * t)) + Math.Tanh(3.0 * Math.Sin(2.0 * Math.PI * 525.0 * t));
                samples[start + i] += (float)(0.5 * envelope * tone);
            }
        }

        static void RemoveDc(float[] samples)
        {
            double mean = 0;
            foreach (var s in samples) mean += s;
            mean /= samples.Length;
            for (int i = 0; i < samples.Length; i++) samples[i] -= (float)mean;
        }
    }
}
