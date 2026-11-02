using System;
using System.Collections.Generic;

namespace Game.Client
{
    // A tune that loops while the player is somewhere: one for each biome, and one for a boss fight.
    enum Tune
    {
        Cave,
        Mushroom,
        Ice,
        Crystal,
        Lava,
        Boss,
        Abyss,
    }

    // Sound that loops under the music: the cave's wind and drips, the lava's rumble and bubbles, and the hum of a
    // running machine.
    enum Ambience
    {
        Cave,
        Lava,
        Hum,
    }

    // Background music and ambience, made from code like the sound effects (SoundArt): bass, a quiet arpeggio and a
    // melody on chiptune waves over a chord progression, with drums for the lava fields and the boss. The melody is
    // written by a fixed scramble from the tune, so a tune always comes out the same. Everything wraps round the end
    // of its loop, so the loop has no seam. Mono, at SoundArt.SampleRate. See Docs/ArtPipeline.md.
    static class MusicArt
    {
        public const int SampleRate = SoundArt.SampleRate;
        // Loops are normalised to this peak, below the sound effects, which play over them.
        const float Peak = 0.5f;

        enum Wave
        {
            Square,
            Pulse,
            Triangle,
            Saw,
            Sine,
        }

        // How a tune goes: its speed, key, scale, chords and instruments.
        sealed class Style
        {
            public float Bpm;
            public int Root;
            public int[] Scale;
            // Scale degrees of the chords, each lasting BarsPerChord bars, round and round for Bars bars.
            public int[] Chords;
            public int BarsPerChord = 2;
            public int Bars = 8;
            public Wave Bass = Wave.Triangle;
            // Bass notes a bar: 1 or 2 held, 8 driving.
            public int BassNotes = 2;
            public Wave Arp = Wave.Pulse;
            // Arpeggio notes a beat, and how loud.
            public int ArpRate = 2;
            public float ArpVolume = 0.12f;
            public int ArpOctave = 1;
            public Wave Lead = Wave.Square;
            public int LeadOctave = 1;
            public float LeadVolume = 0.22f;
            // The share of melody eighths left silent.
            public float Rest = 0.4f;
            public bool Drums;
            public float Echo = 0.3f;
            public int Seed;
        }

        static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };
        static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };
        static readonly int[] Harmonic = { 0, 2, 3, 5, 7, 8, 11 };

        static Style StyleOf(Tune tune) => tune switch
        {
            Tune.Mushroom => new Style
            {
                Bpm = 84, Root = 50, Scale = Dorian, Chords = new[] { 0, 3, 0, 6 }, Lead = Wave.Triangle, LeadVolume = 0.3f,
                Arp = Wave.Pulse, Rest = 0.35f, Seed = 2,
            },
            Tune.Ice => new Style
            {
                Bpm = 66, Root = 52, Scale = Lydian, Chords = new[] { 0, 1, 0, 4 }, Lead = Wave.Sine, LeadOctave = 2, LeadVolume = 0.25f,
                Arp = Wave.Sine, ArpRate = 4, ArpOctave = 2, ArpVolume = 0.1f, BassNotes = 1, Rest = 0.5f, Echo = 0.45f, Seed = 3,
            },
            Tune.Crystal => new Style
            {
                Bpm = 76, Root = 49, Scale = Harmonic, Chords = new[] { 0, 3, 5, 4 }, Lead = Wave.Pulse, LeadVolume = 0.2f,
                Arp = Wave.Triangle, ArpRate = 4, ArpOctave = 2, ArpVolume = 0.14f, Rest = 0.45f, Echo = 0.4f, Seed = 4,
            },
            Tune.Lava => new Style
            {
                Bpm = 92, Root = 40, Scale = Phrygian, Chords = new[] { 0, 1, 0, 6 }, Bass = Wave.Saw, BassNotes = 8, Lead = Wave.Square,
                LeadVolume = 0.2f, Arp = Wave.Pulse, ArpVolume = 0.08f, Rest = 0.45f, Drums = true, Echo = 0.2f, Seed = 5,
            },
            // Slow and cold: a sine melody high over a held bass, wide echoes.
            Tune.Abyss => new Style
            {
                Bpm = 60, Root = 47, Scale = Phrygian, Chords = new[] { 0, 1, 5, 6 }, Lead = Wave.Sine, LeadOctave = 2, LeadVolume = 0.24f,
                Arp = Wave.Triangle, ArpRate = 2, ArpOctave = 1, ArpVolume = 0.1f, BassNotes = 1, Rest = 0.55f, Echo = 0.5f, Seed = 7,
            },
            Tune.Boss => new Style
            {
                Bpm = 140, Root = 45, Scale = Harmonic, Chords = new[] { 0, 5, 3, 4 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Saw,
                BassNotes = 8, Lead = Wave.Square, LeadVolume = 0.22f, Arp = Wave.Pulse, ArpRate = 4, ArpVolume = 0.1f, Rest = 0.25f,
                Drums = true, Echo = 0.15f, Seed = 6,
            },
            _ => new Style
            {
                Bpm = 70, Root = 45, Scale = Minor, Chords = new[] { 0, 5, 2, 6 }, Lead = Wave.Triangle, LeadVolume = 0.3f,
                Arp = Wave.Pulse, ArpVolume = 0.1f, BassNotes = 1, Rest = 0.5f, Echo = 0.4f, Seed = 1,
            },
        };

        public static float[] Make(Tune tune)
        {
            var style = StyleOf(tune);
            float beat = 60 / style.Bpm;
            float bar = beat * 4;
            var buffer = new float[Samples(bar * style.Bars)];
            var lead = new float[buffer.Length];
            var random = new Scramble(style.Seed);

            for (int b = 0; b < style.Bars; b++)
            {
                int degree = style.Chords[b / style.BarsPerChord % style.Chords.Length];
                var chord = new[] { Pitch(style, degree, 0), Pitch(style, degree + 2, 0), Pitch(style, degree + 4, 0) };
                float start = b * bar;

                // Bass: the chord's root, held or driving in eighths with the octave on the off-beats.
                float bassLength = bar / style.BassNotes;
                for (int i = 0; i < style.BassNotes; i++)
                {
                    int note = chord[0] - 12 + (style.BassNotes == 8 && i % 2 == 1 ? 12 : 0);
                    Note(buffer, style.Bass, start + i * bassLength, bassLength * 0.9f, note, 0.32f, decay: style.BassNotes == 8 ? 4 : 1.2f);
                }

                // Arpeggio: up and down the chord.
                int steps = style.ArpRate * 4;
                float arpLength = beat / style.ArpRate;
                int[] order = { 0, 1, 2, 1 };
                for (int i = 0; i < steps; i++)
                {
                    int note = chord[order[i % order.Length]] + 12 * style.ArpOctave;
                    Note(buffer, style.Arp, start + i * arpLength, arpLength * 0.8f, note, style.ArpVolume, decay: 3);
                }

                if (style.Drums) Drums(buffer, start, beat, tune == Tune.Boss);
            }

            // The melody: two-bar phrases A B A C, where C is A ending on the root. Bars past eight repeat them.
            var phraseA = Phrase(style, random);
            var phraseB = Phrase(style, random);
            var phraseC = new List<(int Slot, int Note, int Length)>(phraseA);
            if (phraseC.Count > 0)
            {
                var last = phraseC[phraseC.Count - 1];
                phraseC[phraseC.Count - 1] = (last.Slot, Pitch(style, 0, style.LeadOctave), last.Length);
            }
            var phrases = new[] { phraseA, phraseB, phraseA, phraseC };
            float eighth = beat / 2;
            for (int p = 0; p < style.Bars / 2; p++)
            {
                // Each phrase is played over the chords of its own two bars, moved to fit them.
                foreach (var (slot, note, length) in phrases[p % phrases.Length])
                {
                    int degree = style.Chords[(p * 2 + slot / 8) / style.BarsPerChord % style.Chords.Length];
                    int fitted = Fit(style, note, degree, slot % 8 == 0);
                    Note(lead, style.Lead, p * 2 * bar + slot * eighth, length * eighth * 0.85f, fitted, style.LeadVolume, decay: 1.5f,
                        vibrato: style.Lead == Wave.Sine || style.Lead == Wave.Triangle ? 0.006f : 0);
                }
            }
            // A dotted-eighth echo on the melody, wrapping round like everything else.
            int delay = Samples(eighth * 1.5f);
            for (int i = 0; i < lead.Length; i++)
            {
                buffer[i] += lead[i];
                buffer[(i + delay) % buffer.Length] += lead[i] * style.Echo;
                buffer[(i + delay * 2) % buffer.Length] += lead[i] * style.Echo * style.Echo;
            }
            return Normalised(buffer, Peak);
        }

        public static float[] Make(Ambience ambience)
        {
            switch (ambience)
            {
                case Ambience.Lava:
                {
                    float seconds = 20;
                    var buffer = Wind(seconds, smoothing: 0.006f, volume: 1, swell: 0.4f, seed: 11);
                    var random = new Scramble(12);
                    for (float t = 0.3f; t < seconds; t += 0.25f + random.Next() * 1.1f)
                    {
                        // A bubble rising and popping.
                        float from = 140 + random.Next() * 120;
                        Sweep(buffer, Wave.Sine, t, 0.09f, from, from * 2.6f, 0.35f + random.Next() * 0.25f);
                    }
                    return Normalised(buffer, 0.45f);
                }
                case Ambience.Hum:
                {
                    // Two seconds holds a whole number of cycles of both tones, and ticks every half second.
                    float seconds = 2;
                    var buffer = new float[Samples(seconds)];
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        double t = i / (double)SampleRate;
                        double saw = 2 * (t * 55 - Math.Floor(t * 55 + 0.5));
                        double square = Math.Sin(2 * Math.PI * 110 * t) >= 0 ? 1 : -1;
                        buffer[i] = (float)(saw * 0.5 + square * 0.12);
                    }
                    var smooth = LowPass(buffer, 0.08f);
                    for (int k = 0; k < 4; k++) Click(smooth, k * 0.5f, 0.04f, 0.6f, seed: 20 + k);
                    return Normalised(smooth, 0.4f);
                }
                default:
                {
                    float seconds = 24;
                    var buffer = Wind(seconds, smoothing: 0.02f, volume: 0.6f, swell: 0.7f, seed: 13);
                    var random = new Scramble(14);
                    for (float t = 0.8f; t < seconds; t += 1.2f + random.Next() * 2.8f)
                    {
                        // A drip far off in the cave, with its echoes.
                        float pitch = 1500 + random.Next() * 900;
                        float volume = 0.5f + random.Next() * 0.5f;
                        for (int echo = 0; echo < 3; echo++)
                        {
                            Sweep(buffer, Wave.Sine, t + echo * 0.21f, 0.06f, pitch, pitch * 0.55f, volume * (float)Math.Pow(0.35, echo));
                        }
                    }
                    return Normalised(buffer, 0.4f);
                }
            }
        }

        // ---- Notes ----

        // The note in the scale degree steps up from the root, octaves up.
        static int Pitch(Style style, int degree, int octaves)
        {
            int n = style.Scale.Length;
            int octave = (int)Math.Floor(degree / (double)n);
            int step = degree - octave * n;
            return style.Root + 12 * (octave + octaves) + style.Scale[step];
        }

        // Two bars of melody in eighths: (slot 0-15, note, length in eighths). Notes walk the scale in small steps
        // and land on the chord on strong beats.
        static List<(int Slot, int Note, int Length)> Phrase(Style style, Scramble random)
        {
            var notes = new List<(int, int, int)>();
            int degree = (int)(random.Next() * 5);
            int slot = 0;
            while (slot < 16)
            {
                int length = random.Next() < 0.3f ? 2 : random.Next() < 0.15f ? 3 : 1;
                length = Math.Min(length, 16 - slot);
                bool strong = slot % 4 == 0;
                if (!strong && random.Next() < style.Rest)
                {
                    slot += length;
                    continue;
                }
                float r = random.Next();
                degree += r < 0.35f ? 1 : r < 0.7f ? -1 : r < 0.85f ? 2 : -2;
                degree = Math.Clamp(degree, -2, 9);
                notes.Add((slot, Pitch(style, degree, style.LeadOctave), length));
                slot += length;
            }
            return notes;
        }

        // On a strong beat, the nearest note of the chord; otherwise the note as written.
        static int Fit(Style style, int note, int degree, bool strong)
        {
            if (!strong) return note;
            int best = note;
            int distance = int.MaxValue;
            foreach (int tone in new[] { degree, degree + 2, degree + 4 })
            {
                for (int octave = -1; octave <= 3; octave++)
                {
                    int candidate = Pitch(style, tone, octave);
                    int d = Math.Abs(candidate - note);
                    if (d >= distance) continue;
                    distance = d;
                    best = candidate;
                }
            }
            return best;
        }

        static double Frequency(int note) => 440 * Math.Pow(2, (note - 69) / 12.0);

        // A note added into the loop, wrapping past its end: a quick attack, then a decay over its length.
        static void Note(float[] buffer, Wave wave, float start, float seconds, int note, float volume, float decay = 2, float vibrato = 0)
        {
            double frequency = Frequency(note);
            int first = Samples(start);
            int count = Samples(seconds);
            double phase = 0;
            // The decay as a factor a sample, so the loop multiplies instead of taking an exponential: the web build
            // makes a tune the first time it is needed, while the game runs.
            float fall = 1;
            float fallStep = (float)Math.Exp(-decay / (seconds * SampleRate));
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                phase += (vibrato > 0 ? frequency * (1 + vibrato * Math.Sin(2 * Math.PI * 5.5 * t)) : frequency) / SampleRate;
                float envelope = Math.Min(1, t / 0.006f) * fall * Math.Min(1, (seconds - t) / 0.01f);
                fall *= fallStep;
                buffer[(first + i) % buffer.Length] += Value(wave, phase) * volume * Math.Max(0, envelope);
            }
        }

        // A tone swept in pitch with a sharp attack and a fast fall: drips and bubbles.
        static void Sweep(float[] buffer, Wave wave, float start, float seconds, float from, float to, float volume)
        {
            int first = Samples(start);
            int count = Samples(seconds);
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                phase += from * Math.Pow(to / from, t / seconds) / SampleRate;
                float envelope = Math.Min(1, t / 0.003f) * (float)Math.Exp(-4 * t / seconds) * Math.Min(1, (seconds - t) / 0.005f);
                buffer[(first + i) % buffer.Length] += Value(wave, phase) * volume * Math.Max(0, envelope);
            }
        }

        static float Value(Wave wave, double phase)
        {
            double p = phase - Math.Floor(phase);
            return wave switch
            {
                Wave.Square => p < 0.5 ? 0.6f : -0.6f,
                Wave.Pulse => p < 0.25 ? 0.5f : -0.5f,
                Wave.Triangle => (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p),
                Wave.Saw => (float)(2 * p - 1) * 0.6f,
                _ => (float)Math.Sin(2 * Math.PI * p),
            };
        }

        // Kick on the beats (all four in a boss fight, one and three otherwise), a snare on two and four, hats on
        // the eighths.
        static void Drums(float[] buffer, float start, float beat, bool driving)
        {
            for (int i = 0; i < 4; i++)
            {
                float at = start + i * beat;
                if (driving || i % 2 == 0) Sweep(buffer, Wave.Sine, at, 0.16f, 130, 42, 0.55f);
                if (i % 2 == 1) Click(buffer, at, 0.12f, 0.3f, seed: 3 + i);
                Click(buffer, at, 0.03f, 0.1f, seed: 9 + i, hiss: true);
                Click(buffer, at + beat / 2, 0.03f, driving ? 0.12f : 0.07f, seed: 17 + i, hiss: true);
            }
        }

        // A burst of noise: a snare or a tick, or with hiss a hat.
        static void Click(float[] buffer, float start, float seconds, float volume, int seed, bool hiss = false)
        {
            int first = Samples(start);
            int count = Samples(seconds);
            var random = new Scramble(seed);
            float filtered = 0;
            for (int i = 0; i < count; i++)
            {
                float white = random.Next() * 2 - 1;
                filtered += 0.35f * (white - filtered);
                // A hat keeps only the hiss: the noise less its smoothed self.
                float value = hiss ? white - filtered : filtered;
                float t = i / (float)SampleRate;
                buffer[(first + i) % buffer.Length] += value * volume * (float)Math.Exp(-5 * t / seconds);
            }
        }

        // Low noise that swells and falls a few times over the loop, its end crossfaded into its start so it runs on
        // without a seam.
        static float[] Wind(float seconds, float smoothing, float volume, float swell, int seed)
        {
            int length = Samples(seconds);
            int fade = Samples(1);
            var raw = new float[length + fade];
            var random = new Scramble(seed);
            float filtered = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                filtered += smoothing * (random.Next() * 2 - 1 - filtered);
                double t = i / (double)SampleRate;
                // Three swells a loop, so the swell itself wraps.
                float gain = 1 - swell * 0.5f * (1 + (float)Math.Sin(2 * Math.PI * 3 * t / seconds));
                raw[i] = filtered * gain * volume * 6;
            }
            var buffer = new float[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = i < fade ? raw[i] * i / fade + raw[length + i] * (1 - i / (float)fade) : raw[i];
            }
            return buffer;
        }

        static float[] LowPass(float[] buffer, float smoothing)
        {
            var smooth = new float[buffer.Length];
            // Twice round, so the start already carries the end's filter state.
            float filtered = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    filtered += smoothing * (buffer[i] - filtered);
                    if (pass == 1) smooth[i] = filtered;
                }
            }
            return smooth;
        }

        static float[] Normalised(float[] buffer, float peak)
        {
            float loudest = 0;
            foreach (float sample in buffer) loudest = Math.Max(loudest, Math.Abs(sample));
            if (loudest <= 0) return buffer;
            float scale = peak / loudest;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
            return buffer;
        }

        static int Samples(float seconds) => (int)Math.Round(seconds * SampleRate);

        // A fixed stream of numbers from 0 to 1, the same for the same seed.
        sealed class Scramble
        {
            uint _state;

            public Scramble(int seed) => _state = 0x9E3779B9u * (uint)(seed + 1) + 0x7F4A7C15u;

            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
