using System;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>Deterministic placeholder ambience (spec #3 §5): seamless mono loops from fixed seeds.</summary>
    public static class AmbienceSynth
    {
        public const int SampleRate = 22050;
        public const float LoopFadeSeconds = 0.5f;
        const float Peak = 0.8f;

        static int FadeSamples => (int)(LoopFadeSeconds * SampleRate);

        /// <summary>Wind with gusts and short bird calls.</summary>
        public static float[] Day(float seconds = 20f)
        {
            var rng = new Random(311);
            var raw = new float[(int)(seconds * SampleRate) + FadeSamples];
            AddWind(raw, rng, 3f, 500f);
            AddBirds(raw, rng, Math.Max(1, (int)(seconds * 0.8f)));
            return Finish(raw);
        }

        /// <summary>Crickets over a low wind (fallback when the CC0 cricket loop is missing).</summary>
        public static float[] Night(float seconds = 16f)
        {
            var rng = new Random(733);
            var raw = new float[(int)(seconds * SampleRate) + FadeSamples];
            AddWind(raw, rng, 2f, 300f);
            AddCrickets(raw, rng, 6);
            return Finish(raw);
        }

        /// <summary>Low beating sines with a slow swell and dark noise.</summary>
        public static float[] Drone(float seconds = 12f)
        {
            var rng = new Random(977);
            var raw = new float[(int)(seconds * SampleRate) + FadeSamples];
            for (int i = 0; i < raw.Length; i++)
            {
                double t = (double)i / SampleRate;
                double swell = 0.7 + 0.3 * Math.Sin(2.0 * Math.PI * t / 9.0);
                raw[i] += (float)(swell * (0.5 * Math.Sin(2.0 * Math.PI * 55.0 * t)
                    + 0.35 * Math.Sin(2.0 * Math.PI * 58.3 * t)
                    + 0.12 * Math.Sin(2.0 * Math.PI * 110.5 * t)));
            }
            AddWind(raw, rng, 1.5f, 150f);
            return Finish(raw);
        }

        /// <summary>Turns n + fade samples into a seamless n-sample loop by cross-fading the tail into the head.</summary>
        public static float[] Loop(float[] raw, int fade)
        {
            int n = raw.Length - fade;
            var output = new float[n];
            Array.Copy(raw, output, n);
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                output[i] = raw[i] * w + raw[n + i] * (1f - w);
            }
            return output;
        }

        public static void Normalize(float[] samples, float peak)
        {
            float max = 0f;
            foreach (var s in samples) max = Math.Max(max, Math.Abs(s));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < samples.Length; i++) samples[i] *= k;
        }

        static float[] Finish(float[] raw)
        {
            var loop = Loop(raw, FadeSamples);
            Normalize(loop, Peak);
            return loop;
        }

        static void AddWind(float[] buffer, Random rng, float amplitude, float cutoffHz)
        {
            float a = 1f - (float)Math.Exp(-2.0 * Math.PI * cutoffHz / SampleRate);
            float low = 0f, low2 = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += a * (white - low);
                low2 += a * (low - low2);
                float gust = 0.6f + 0.4f * (float)Math.Sin(2.0 * Math.PI * i / (SampleRate * 7.3));
                buffer[i] += low2 * gust * amplitude;
            }
        }

        static void AddBirds(float[] buffer, Random rng, int calls)
        {
            for (int c = 0; c < calls; c++)
            {
                int start = rng.Next(0, Math.Max(1, buffer.Length - SampleRate));
                int notes = 2 + rng.Next(3);
                float baseHz = 2200f + (float)rng.NextDouble() * 1800f;
                for (int k = 0; k < notes; k++)
                {
                    int noteStart = start + k * (int)(0.16f * SampleRate);
                    int length = (int)((0.06f + 0.08f * (float)rng.NextDouble()) * SampleRate);
                    float sweep = 600f + 900f * (float)rng.NextDouble();
                    double phase = 0;
                    for (int i = 0; i < length && noteStart + i < buffer.Length; i++)
                    {
                        float t = (float)i / length;
                        phase += 2.0 * Math.PI * (baseHz + sweep * t) / SampleRate;
                        float env = (float)Math.Sin(Math.PI * t);
                        buffer[noteStart + i] += 0.25f * env * env * (float)Math.Sin(phase);
                    }
                }
            }
        }

        static void AddCrickets(float[] buffer, Random rng, int crickets)
        {
            for (int c = 0; c < crickets; c++)
            {
                double carrier = 4200.0 + 600.0 * rng.NextDouble();
                double pulseHz = 28.0 + 8.0 * rng.NextDouble();
                double chirpSeconds = 0.12 + 0.08 * rng.NextDouble();
                double periodSeconds = 0.5 + 0.5 * rng.NextDouble();
                float level = 0.08f + 0.08f * (float)rng.NextDouble();
                int offset = rng.Next(SampleRate);
                for (int i = 0; i < buffer.Length; i++)
                {
                    double t = (double)(i + offset) / SampleRate;
                    double inPeriod = t % periodSeconds;
                    if (inPeriod > chirpSeconds) continue;
                    double env = Math.Sin(Math.PI * inPeriod / chirpSeconds);
                    double pulse = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * pulseHz * t);
                    buffer[i] += (float)(level * env * pulse * pulse * Math.Sin(2.0 * Math.PI * carrier * t));
                }
            }
        }
    }
}
