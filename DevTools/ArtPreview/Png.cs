using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Game.Client
{
    // Just enough of PNG to save a PixelImage: 8-bit RGBA, no filtering, one zlib stream.
    static class Png
    {
        static readonly uint[] CrcTable = MakeCrcTable();

        public static void Save(PixelImage image, string path)
        {
            using var file = File.Create(path);
            file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            var header = new byte[13];
            WriteBigEndian(header, 0, (uint)image.Width);
            WriteBigEndian(header, 4, (uint)image.Height);
            header[8] = 8;
            header[9] = 6;
            Chunk(file, "IHDR", header);

            var raw = new byte[image.Height * (1 + image.Width * 4)];
            int i = 0;
            for (int y = 0; y < image.Height; y++)
            {
                raw[i++] = 0;
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    raw[i++] = pixel.R;
                    raw[i++] = pixel.G;
                    raw[i++] = pixel.B;
                    raw[i++] = pixel.A;
                }
            }
            using var packed = new MemoryStream();
            using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(raw);
            Chunk(file, "IDAT", packed.ToArray());
            Chunk(file, "IEND", Array.Empty<byte>());
        }

        static void Chunk(Stream stream, string type, byte[] data)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, (uint)data.Length);
            stream.Write(length);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);
            uint crc = Crc(Crc(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
            var crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc);
            stream.Write(crcBytes);
        }

        static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        static uint Crc(uint crc, byte[] data)
        {
            foreach (byte b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        static uint[] MakeCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }
    }
}
