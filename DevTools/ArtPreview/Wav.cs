using System;
using System.IO;

namespace Game.Client
{
    // Saves a sound as a 16-bit mono WAV file.
    static class Wav
    {
        public static void Save(float[] samples, int sampleRate, string path)
        {
            using var file = new BinaryWriter(File.Create(path));
            int bytes = samples.Length * 2;
            file.Write("RIFF".ToCharArray());
            file.Write(36 + bytes);
            file.Write("WAVEfmt ".ToCharArray());
            file.Write(16);
            file.Write((short)1);
            file.Write((short)1);
            file.Write(sampleRate);
            file.Write(sampleRate * 2);
            file.Write((short)2);
            file.Write((short)16);
            file.Write("data".ToCharArray());
            file.Write(bytes);
            foreach (float sample in samples) file.Write((short)Math.Round(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
    }
}
