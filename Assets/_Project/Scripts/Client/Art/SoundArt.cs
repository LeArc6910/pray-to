using System;
using System.Collections.Generic;

namespace Game.Client
{
    // Every sound effect in the game.
    enum Sound
    {
        Swing,
        Hit,
        Crit,
        Hurt,
        Dig,
        Break,
        Pickup,
        Place,
        Craft,
        Click,
        Shoot,
        Zap,
        Boom,
        Nova,
        Buff,
        Heal,
        Pop,
        Down,
        Warning,
        Roar,
        Wake,
        Victory,
        Harvest,
    }

    // The sound effects, made from code the way the pixel art is: square, triangle, saw and sine tones swept in
    // pitch, and filtered noise, each shaped by a quick attack and a decay, in the manner of 8-bit consoles. No audio
    // files, so they reach the web demo directly and the preview tool (DevTools/ArtPreview) writes them out as WAV.
    // Mono, at SampleRate.
    static class SoundArt
    {
        public const int SampleRate = 22050;
        // Every sound is scaled so its loudest sample is this times its own Level, leaving room to mix.
        const float Peak = 0.7f;

        enum Wave
        {
            Square,
            Pulse,
            Triangle,
            Saw,
            Sine,
        }

        public static float[] Make(Sound sound)
        {
            var parts = new List<Part>();
            float level = 1;
            switch (sound)
            {
                case Sound.Swing:
                    parts.Add(Noise(0, 0.14f, 1, 0.22f, attack: 0.03f, decay: 3));
                    parts.Add(Tone(Wave.Sine, 0, 0.12f, 320, 160, 0.25f));
                    level = 0.55f;
                    break;
                case Sound.Hit:
                    parts.Add(Tone(Wave.Square, 0, 0.12f, 170, 55, 0.8f, decay: 5));
                    parts.Add(Noise(0, 0.05f, 0.6f, 0.5f, decay: 8));
                    level = 0.8f;
                    break;
                case Sound.Crit:
                    parts.Add(Tone(Wave.Square, 0, 0.12f, 170, 55, 0.8f, decay: 5));
                    parts.Add(Noise(0, 0.05f, 0.6f, 0.5f, decay: 8));
                    parts.Add(Tone(Wave.Pulse, 0.03f, 0.16f, 1320, 1320, 0.35f, decay: 4));
                    level = 0.9f;
                    break;
                case Sound.Hurt:
                    parts.Add(Tone(Wave.Saw, 0, 0.2f, 260, 110, 0.8f, decay: 3));
                    parts.Add(Noise(0, 0.1f, 0.5f, 0.3f, decay: 5));
                    break;
                case Sound.Dig:
                    parts.Add(Noise(0, 0.09f, 1, 0.35f, decay: 5));
                    parts.Add(Tone(Wave.Triangle, 0, 0.08f, 150, 70, 0.6f, decay: 6));
                    level = 0.6f;
                    break;
                case Sound.Break:
                    parts.Add(Noise(0, 0.12f, 1, 0.3f, decay: 4));
                    parts.Add(Noise(0.06f, 0.12f, 0.7f, 0.25f, decay: 4, seed: 2));
                    parts.Add(Noise(0.14f, 0.2f, 0.5f, 0.2f, decay: 4, seed: 3));
                    parts.Add(Tone(Wave.Triangle, 0, 0.3f, 110, 50, 0.5f, decay: 3));
                    level = 0.8f;
                    break;
                case Sound.Pickup:
                    parts.Add(Tone(Wave.Pulse, 0, 0.07f, 880, 880, 0.5f, decay: 1.5f));
                    parts.Add(Tone(Wave.Pulse, 0.07f, 0.1f, 1320, 1320, 0.5f, decay: 2));
                    level = 0.6f;
                    break;
                case Sound.Place:
                    parts.Add(Tone(Wave.Triangle, 0, 0.1f, 260, 150, 0.8f, decay: 6));
                    parts.Add(Noise(0, 0.03f, 0.4f, 0.6f, decay: 6));
                    level = 0.7f;
                    break;
                case Sound.Craft:
                    Arpeggio(parts, Wave.Pulse, 0.07f, 0.1f, 0.4f, 523, 659, 784, 1047);
                    level = 0.6f;
                    break;
                case Sound.Click:
                    parts.Add(Tone(Wave.Pulse, 0, 0.04f, 1200, 1100, 0.3f, decay: 6));
                    level = 0.4f;
                    break;
                case Sound.Shoot:
                    parts.Add(Tone(Wave.Triangle, 0, 0.14f, 720, 250, 0.7f, decay: 4));
                    parts.Add(Noise(0, 0.06f, 0.35f, 0.7f, decay: 6));
                    level = 0.6f;
                    break;
                case Sound.Zap:
                    parts.Add(Tone(Wave.Square, 0, 0.18f, 300, 1200, 0.45f, decay: 2, vibrato: 0.05f, vibratoRate: 40));
                    parts.Add(Tone(Wave.Saw, 0, 0.18f, 600, 2400, 0.15f, decay: 2));
                    level = 0.55f;
                    break;
                case Sound.Boom:
                    parts.Add(Noise(0, 0.45f, 1, 0.12f, attack: 0.002f, decay: 4));
                    parts.Add(Tone(Wave.Sine, 0, 0.4f, 90, 35, 0.9f, decay: 3));
                    break;
                case Sound.Nova:
                    parts.Add(Tone(Wave.Triangle, 0, 0.35f, 220, 880, 0.5f, decay: 1.5f));
                    parts.Add(Noise(0, 0.35f, 0.3f, 0.2f, attack: 0.1f, decay: 2));
                    level = 0.8f;
                    break;
                case Sound.Buff:
                    Arpeggio(parts, Wave.Triangle, 0.06f, 0.14f, 0.5f, 392, 494, 587, 784);
                    level = 0.7f;
                    break;
                case Sound.Heal:
                    parts.Add(Tone(Wave.Sine, 0, 0.4f, 520, 900, 0.5f, decay: 1, vibrato: 0.06f, vibratoRate: 18));
                    parts.Add(Tone(Wave.Sine, 0.05f, 0.35f, 780, 1350, 0.25f, decay: 1.5f));
                    level = 0.6f;
                    break;
                case Sound.Pop:
                    parts.Add(Tone(Wave.Square, 0, 0.16f, 520, 90, 0.6f, decay: 4));
                    parts.Add(Noise(0, 0.05f, 0.3f, 0.5f, decay: 6));
                    level = 0.7f;
                    break;
                case Sound.Down:
                    parts.Add(Tone(Wave.Square, 0, 0.2f, 440, 440, 0.4f, decay: 1.5f));
                    parts.Add(Tone(Wave.Square, 0.2f, 0.2f, 349, 349, 0.4f, decay: 1.5f));
                    parts.Add(Tone(Wave.Square, 0.4f, 0.35f, 262, 247, 0.4f, decay: 2));
                    level = 0.7f;
                    break;
                case Sound.Warning:
                    parts.Add(Tone(Wave.Square, 0, 0.07f, 988, 988, 0.4f, decay: 0.5f));
                    parts.Add(Tone(Wave.Square, 0.12f, 0.07f, 988, 988, 0.4f, decay: 0.5f));
                    level = 0.5f;
                    break;
                case Sound.Roar:
                    parts.Add(Tone(Wave.Saw, 0, 0.9f, 85, 55, 0.8f, attack: 0.08f, decay: 1.5f, vibrato: 0.08f, vibratoRate: 9));
                    parts.Add(Noise(0, 0.9f, 0.6f, 0.08f, attack: 0.1f, decay: 1.5f));
                    break;
                case Sound.Wake:
                    parts.Add(Tone(Wave.Square, 0, 0.4f, 110, 110, 0.5f, decay: 1));
                    parts.Add(Tone(Wave.Square, 0.4f, 0.5f, 104, 98, 0.5f, decay: 1));
                    parts.Add(Tone(Wave.Saw, 0, 0.9f, 55, 49, 0.4f, attack: 0.05f, decay: 1));
                    level = 0.9f;
                    break;
                case Sound.Victory:
                    Arpeggio(parts, Wave.Pulse, 0.1f, 0.12f, 0.4f, 523, 659, 784);
                    parts.Add(Tone(Wave.Pulse, 0.3f, 0.6f, 1047, 1047, 0.45f, decay: 1.5f, vibrato: 0.01f, vibratoRate: 6));
                    level = 0.8f;
                    break;
                case Sound.Harvest:
                    parts.Add(Tone(Wave.Triangle, 0, 0.12f, 660, 660, 0.6f, decay: 4));
                    parts.Add(Tone(Wave.Triangle, 0.05f, 0.13f, 990, 990, 0.5f, decay: 4));
                    level = 0.6f;
                    break;
            }
            return Mix(parts, level);
        }

        // A tone or noise burst placed in time; Render adds it into the buffer.
        sealed class Part
        {
            public float Start;
            public float Seconds;
            public Action<float[], int> Render;
        }

        static void Arpeggio(List<Part> parts, Wave wave, float step, float length, float volume, params float[] notes)
        {
            for (int i = 0; i < notes.Length; i++) parts.Add(Tone(wave, i * step, length, notes[i], notes[i], volume, decay: 3));
        }

        // A wave swept in pitch from one frequency to the other (evenly in octaves), with optional vibrato (a share
        // of the pitch, at a rate in hertz).
        static Part Tone(Wave wave, float start, float seconds, float from, float to, float volume,
            float attack = 0.004f, float decay = 3, float vibrato = 0, float vibratoRate = 0)
        {
            return new Part
            {
                Start = start,
                Seconds = seconds,
                Render = (buffer, offset) =>
                {
                    int count = Samples(seconds);
                    double phase = 0;
                    for (int i = 0; i < count && offset + i < buffer.Length; i++)
                    {
                        float t = i / (float)SampleRate;
                        float along = t / seconds;
                        double frequency = from * Math.Pow(to / from, along) * (1 + vibrato * Math.Sin(2 * Math.PI * vibratoRate * t));
                        phase += frequency / SampleRate;
                        double p = phase - Math.Floor(phase);
                        float value = wave switch
                        {
                            Wave.Square => p < 0.5 ? 1 : -1,
                            Wave.Pulse => p < 0.25 ? 1 : -1,
                            Wave.Triangle => (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p),
                            Wave.Saw => (float)(2 * p - 1),
                            _ => (float)Math.Sin(2 * Math.PI * p),
                        };
                        buffer[offset + i] += value * volume * Envelope(t, seconds, attack, decay);
                    }
                },
            };
        }

        // White noise through a one-pole low-pass filter: smoothing near 1 hisses, near 0 rumbles. Always the same
        // noise for the same seed.
        static Part Noise(float start, float seconds, float volume, float smoothing, float attack = 0.002f, float decay = 3, int seed = 1)
        {
            return new Part
            {
                Start = start,
                Seconds = seconds,
                Render = (buffer, offset) =>
                {
                    int count = Samples(seconds);
                    uint state = 0x9E3779B9u * (uint)seed + 1;
                    float filtered = 0;
                    for (int i = 0; i < count && offset + i < buffer.Length; i++)
                    {
                        state ^= state << 13;
                        state ^= state >> 17;
                        state ^= state << 5;
                        float white = (state & 0xFFFF) / 32767.5f - 1;
                        filtered += smoothing * (white - filtered);
                        float t = i / (float)SampleRate;
                        buffer[offset + i] += filtered * volume * Envelope(t, seconds, attack, decay);
                    }
                },
            };
        }

        // Rises over the attack, then dies away exponentially; the last few milliseconds fade to nothing so a sound
        // never ends with a click.
        static float Envelope(float t, float seconds, float attack, float decay)
        {
            float rise = attack > 0 ? Math.Min(1, t / attack) : 1;
            float fall = (float)Math.Exp(-decay * t / seconds);
            float tail = Math.Min(1, (seconds - t) / 0.005f);
            return rise * fall * Math.Max(0, tail);
        }

        static int Samples(float seconds) => (int)Math.Ceiling(seconds * SampleRate);

        static float[] Mix(List<Part> parts, float level)
        {
            float length = 0;
            foreach (var part in parts) length = Math.Max(length, part.Start + part.Seconds);
            var buffer = new float[Samples(length)];
            foreach (var part in parts) part.Render(buffer, Samples(part.Start));
            float loudest = 0;
            foreach (float sample in buffer) loudest = Math.Max(loudest, Math.Abs(sample));
            if (loudest > 0)
            {
                float scale = Peak * level / loudest;
                for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
            }
            return buffer;
        }
    }
}
