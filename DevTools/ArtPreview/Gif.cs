using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // A small animated GIF writer for the motion previews (Motions.cs). Every frame is the whole picture; only the
    // part that changed since the frame before is written, the unchanged pixels in it transparent, and a frame the
    // same as the one before only lengthens it. One palette for the whole file: when the frames hold more than 255
    // colours, they are rounded to fewer bits until they fit.
    static class Gif
    {
        public static void Save(string path, int width, int height, IReadOnlyList<(Rgba[] Pixels, int Centiseconds)> frames)
        {
            if (frames.Count == 0) throw new ArgumentException("no frames", nameof(frames));
            int mask = 0xFF;
            Dictionary<int, int> palette;
            while (true)
            {
                palette = new Dictionary<int, int>();
                foreach (var frame in frames)
                {
                    foreach (var pixel in frame.Pixels)
                    {
                        int key = Key(pixel, mask);
                        if (!palette.ContainsKey(key)) palette[key] = palette.Count;
                        if (palette.Count > 255) break;
                    }
                    if (palette.Count > 255) break;
                }
                if (palette.Count <= 255) break;
                mask = (mask << 1) & 0xFF;
            }
            const int transparent = 255;

            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write("GIF89a".ToCharArray());
            writer.Write((ushort)width);
            writer.Write((ushort)height);
            writer.Write((byte)0xF7);
            writer.Write((byte)0);
            writer.Write((byte)0);
            var table = new byte[256 * 3];
            foreach (var pair in palette)
            {
                table[pair.Value * 3] = (byte)(pair.Key >> 16);
                table[pair.Value * 3 + 1] = (byte)(pair.Key >> 8);
                table[pair.Value * 3 + 2] = (byte)pair.Key;
            }
            writer.Write(table);
            // Loop for ever.
            writer.Write(new byte[] { 0x21, 0xFF, 0x0B });
            writer.Write("NETSCAPE2.0".ToCharArray());
            writer.Write(new byte[] { 3, 1, 0, 0, 0 });

            // Same frames joined: each kept frame with its full time.
            var kept = new List<(int[] Indices, int Centiseconds)>();
            foreach (var frame in frames)
            {
                var indices = frame.Pixels.Select(p => palette[Key(p, mask)]).ToArray();
                if (kept.Count > 0 && kept[kept.Count - 1].Indices.SequenceEqual(indices))
                {
                    kept[kept.Count - 1] = (kept[kept.Count - 1].Indices, kept[kept.Count - 1].Centiseconds + frame.Centiseconds);
                    continue;
                }
                kept.Add((indices, frame.Centiseconds));
            }

            int[] previous = null;
            foreach (var (indices, centiseconds) in kept)
            {
                int left = 0, top = 0, right = width - 1, bottom = height - 1;
                if (previous != null)
                {
                    left = width;
                    top = height;
                    right = -1;
                    bottom = -1;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (indices[y * width + x] == previous[y * width + x]) continue;
                            left = Math.Min(left, x);
                            right = Math.Max(right, x);
                            top = Math.Min(top, y);
                            bottom = Math.Max(bottom, y);
                        }
                    }
                    if (right < 0)
                    {
                        left = top = right = bottom = 0;
                    }
                }
                int w = right - left + 1;
                int h = bottom - top + 1;
                var part = new byte[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int at = (top + y) * width + left + x;
                        part[y * w + x] = previous != null && indices[at] == previous[at] ? (byte)transparent : (byte)indices[at];
                    }
                }
                // Graphic control: leave the frame in place, the transparent index, the delay.
                writer.Write(new byte[] { 0x21, 0xF9, 4, 0x05 });
                writer.Write((ushort)Math.Max(2, centiseconds));
                writer.Write((byte)transparent);
                writer.Write((byte)0);
                writer.Write((byte)0x2C);
                writer.Write((ushort)left);
                writer.Write((ushort)top);
                writer.Write((ushort)w);
                writer.Write((ushort)h);
                writer.Write((byte)0);
                writer.Write((byte)8);
                var data = Lzw(part);
                for (int i = 0; i < data.Length; i += 255)
                {
                    int length = Math.Min(255, data.Length - i);
                    writer.Write((byte)length);
                    writer.Write(data, i, length);
                }
                writer.Write((byte)0);
                previous = indices;
            }
            writer.Write((byte)0x3B);
        }

        static int Key(Rgba pixel, int mask) => (pixel.R & mask) << 16 | (pixel.G & mask) << 8 | pixel.B & mask;

        // GIF's LZW with 8-bit symbols: codes grow from 9 to 12 bits, and the table starts again when full.
        static byte[] Lzw(byte[] symbols)
        {
            const int clear = 256;
            const int end = 257;
            var output = new List<byte>();
            int bitBuffer = 0;
            int bitCount = 0;
            int codeSize = 9;
            void Emit(int code)
            {
                bitBuffer |= code << bitCount;
                bitCount += codeSize;
                while (bitCount >= 8)
                {
                    output.Add((byte)bitBuffer);
                    bitBuffer >>= 8;
                    bitCount -= 8;
                }
            }

            var table = new Dictionary<int, int>();
            int next = 258;
            Emit(clear);
            int prefix = symbols.Length > 0 ? symbols[0] : 0;
            for (int i = 1; i < symbols.Length; i++)
            {
                int symbol = symbols[i];
                int key = prefix << 8 | symbol;
                if (table.TryGetValue(key, out int code))
                {
                    prefix = code;
                    continue;
                }
                Emit(prefix);
                if (next < 4096)
                {
                    table[key] = next++;
                    if (next > 1 << codeSize && codeSize < 12) codeSize++;
                }
                else
                {
                    Emit(clear);
                    table.Clear();
                    next = 258;
                    codeSize = 9;
                }
                prefix = symbol;
            }
            Emit(prefix);
            Emit(end);
            if (bitCount > 0) output.Add((byte)bitBuffer);
            return output.ToArray();
        }
    }
}
