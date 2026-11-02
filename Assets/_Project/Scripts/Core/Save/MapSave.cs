using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Core
{
    // The tiles a player has seen on the map of one world, kept on their device for each character and world (by
    // the world's seed), so the map is still filled in when they come back. It is the client's view, not game
    // state: in co-op each player has their own. See Docs/Saving.md (가 본 곳 지도).
    //
    // Stored as runs: the width and height, then the lengths of alternating unseen and seen runs, row by row from
    // the first tile, each as a 7-bit varint. A cave explored in patches comes to a few kilobytes at most.
    public static class MapSave
    {
        const uint Magic = 0x504D5450; // "PTMP" as written (little-endian); SaveKeys.MagicOf has it as text
        const int FormatVersion = 1;

        public static byte[] Write(IReadOnlyList<bool> seen, int width, int height)
        {
            if (seen.Count != width * height) throw new ArgumentException($"{seen.Count} tiles for a {width}x{height} map.");
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic);
                w.Write(FormatVersion);
                w.Write((ushort)width);
                w.Write((ushort)height);
                bool current = false;
                int run = 0;
                for (int i = 0; i < seen.Count; i++)
                {
                    if (seen[i] == current)
                    {
                        run++;
                        continue;
                    }
                    WriteVarint(w, run);
                    current = seen[i];
                    run = 1;
                }
                WriteVarint(w, run);
            }
            return stream.ToArray();
        }

        // The seen tiles, or null when the data is not a map save, is for a map of another size, or is cut short;
        // then the map simply starts empty.
        public static bool[] Read(byte[] data, int width, int height)
        {
            if (data == null) return null;
            try
            {
                using var r = new BinaryReader(new MemoryStream(data));
                if (r.ReadUInt32() != Magic || r.ReadInt32() != FormatVersion) return null;
                if (r.ReadUInt16() != width || r.ReadUInt16() != height) return null;
                var seen = new bool[width * height];
                int at = 0;
                bool current = false;
                while (at < seen.Length)
                {
                    int run = ReadVarint(r);
                    if (run < 0 || run > seen.Length - at) return null;
                    if (current)
                    {
                        for (int i = 0; i < run; i++) seen[at + i] = true;
                    }
                    at += run;
                    current = !current;
                }
                return seen;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        static void WriteVarint(BinaryWriter w, int value)
        {
            uint v = (uint)value;
            while (v >= 0x80)
            {
                w.Write((byte)(v | 0x80));
                v >>= 7;
            }
            w.Write((byte)v);
        }

        static int ReadVarint(BinaryReader r)
        {
            int value = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte b = r.ReadByte();
                value |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value;
            }
            return -1;
        }
    }
}
