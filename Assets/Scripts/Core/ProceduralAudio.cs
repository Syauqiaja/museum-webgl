using System.Collections.Generic;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The lobby gallery's sounds, synthesised at first use rather than shipped as clips. The
    /// project carries no gamelan recordings — the only audio on disk is a nature pack — and a
    /// few seconds of additive sine is a smaller download than any recording would be, so the
    /// gong, the gasing's hum and the slendro phrase are all made here from partials.
    /// </summary>
    /// <remarks>
    /// Everything is cached per process; a clip is a few hundred kilobytes of floats and each
    /// is built once. Slendro is approximated as five equal steps to the octave, which is
    /// close enough to read as gamelan and far enough from a piano to feel like one.
    /// </remarks>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>A gong ageng: low fundamental, inharmonic partials, four seconds of decay.</summary>
        public static AudioClip Gong() => Cached("gong", () =>
        {
            float[] freqs = { 92f, 138f, 196f, 291f, 412f };
            float[] gains = { 1.0f, 0.55f, 0.35f, 0.2f, 0.1f };
            float[] decays = { 0.45f, 0.7f, 0.9f, 1.3f, 1.8f };
            return Render(4.5f, t =>
            {
                float s = 0f;
                for (int i = 0; i < freqs.Length; i++)
                {
                    s += gains[i] * Mathf.Sin(2f * Mathf.PI * freqs[i] * t) * Mathf.Exp(-decays[i] * t);
                }

                // The slow beating a real gong has between its two closest partials.
                s *= 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 3.1f * t);
                return s * 0.5f * Attack(t, 0.008f);
            });
        });

        /// <summary>One second of a spinning-top hum, meant to loop with its pitch driven by the spin speed.</summary>
        public static AudioClip Whir() => Cached("whir", () =>
        {
            var rng = new System.Random(7);
            return Render(1f, t =>
            {
                float s = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.6f
                          + Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.25f
                          + Mathf.Sin(2f * Mathf.PI * 331f * t) * 0.1f
                          + ((float)rng.NextDouble() * 2f - 1f) * 0.08f;
                return s * 0.35f;
            });
        });

        /// <summary>A short bright chime for a step landed; <paramref name="degree"/> picks the slendro note.</summary>
        public static AudioClip Chime(int degree) => Cached($"chime{degree}", () =>
        {
            float f = Slendro(degree + 5);
            return Render(0.6f, t => Bell(f, t) * 0.5f * Attack(t, 0.004f));
        });

        /// <summary>Three rising notes for a completed course.</summary>
        public static AudioClip Fanfare() => Cached("fanfare", () =>
            Melody(new[] { (5, 0.18f), (7, 0.18f), (9, 0.18f), (10, 0.6f) }, 1.2f));

        /// <summary>
        /// A dolanan-style phrase in slendro, the shape of "Jamuran": call and answer, ending
        /// low. Roughly seven seconds.
        /// </summary>
        public static AudioClip DolananPhrase() => Cached("dolanan", () => Melody(new[]
        {
            (2, 0.3f), (2, 0.3f), (3, 0.3f), (4, 0.6f), (3, 0.3f), (2, 0.6f),
            (1, 0.3f), (1, 0.3f), (2, 0.3f), (3, 0.6f), (2, 0.3f), (1, 0.6f),
            (4, 0.3f), (4, 0.3f), (3, 0.3f), (2, 0.3f), (1, 0.3f), (2, 0.6f),
            (3, 0.3f), (3, 0.3f), (2, 0.3f), (1, 0.3f), (0, 0.3f), (1, 0.9f),
        }, 1.5f));

        /// <summary>Plays a clip on a source, replacing whatever it was playing.</summary>
        public static void Play(AudioSource source, AudioClip clip, float volume = 1f)
        {
            if (source == null || clip == null) return;
            source.Stop();
            source.loop = false;
            source.pitch = 1f;
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }

        // --- synthesis -----------------------------------------------------------------

        /// <summary>Slendro degree to frequency: five equal steps from C4, degree 0.</summary>
        private static float Slendro(int degree) => 261.63f * Mathf.Pow(2f, degree / 5f);

        /// <summary>A metallophone-ish strike: fundamental plus one inharmonic partial, decaying.</summary>
        private static float Bell(float f, float t)
        {
            return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-4.5f * t)
                   + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-9f * t);
        }

        private static float Attack(float t, float seconds) => Mathf.Clamp01(t / seconds);

        /// <summary>Notes as (slendro degree, seconds); each rings on past its own slot.</summary>
        private static AudioClip Melody((int degree, float seconds)[] notes, float tail)
        {
            var starts = new float[notes.Length];
            float total = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                starts[i] = total;
                total += notes[i].seconds;
            }

            return Render(total + tail, t =>
            {
                float s = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float dt = t - starts[i];
                    if (dt < 0f || dt > 2.5f) continue;
                    s += Bell(Slendro(notes[i].degree), dt) * Attack(dt, 0.005f);
                }

                return s * 0.4f;
            });
        }

        private static AudioClip Render(float seconds, System.Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            float fade = Mathf.Max(1f, count - SampleRate * 0.05f);
            for (int i = 0; i < count; i++)
            {
                float v = sample(i / (float)SampleRate);
                // A short fade-out so a clip never ends on a click.
                if (i > fade) v *= (count - i) / (count - fade);
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }

            var clip = AudioClip.Create("procedural", count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Cached(string key, System.Func<AudioClip> build)
        {
            if (Cache.TryGetValue(key, out AudioClip clip) && clip != null) return clip;
            clip = build();
            clip.name = key;
            Cache[key] = clip;
            return clip;
        }
    }
}
