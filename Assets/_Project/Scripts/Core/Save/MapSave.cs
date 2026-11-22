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
    // Format 2 (묶음 4c, Docs/WorldRework.md 덜 정확한 안내): then the rumours heard here (Rumors), each its kind, the
    // thing's number and the day heard, as varints after their count. Format 1 reads as no rumours.
    public static class MapSave
    {
        const uint Magic = 0x504D5450; // "PTMP" as written (little-endian); SaveKeys.MagicOf has it as text
        const int FormatVersion = 2;
        // Rumours kept at most: the oldest go first.
        public const int MaxRumors = 200;

        public static byte[] Write(IReadOnlyList<bool> seen, int width, int height, IReadOnlyList<HeardRumor> rumors = null)
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
                int from = rumors == null ? 0 : Math.Max(0, rumors.Count - MaxRumors);
                WriteVarint(w, rumors == null ? 0 : rumors.Count - from);
                for (int i = from; rumors != null && i < rumors.Count; i++)
                {
                    WriteVarint(w, (int)rumors[i].Kind);
                    WriteVarint(w, Math.Max(0, rumors[i].Index));
                    WriteVarint(w, Math.Max(0, rumors[i].Day));
                }
            }
            return stream.ToArray();
        }

        // The rumours heard, in the order heard; none for format 1, a save of another map's size or one cut short.
        public static List<HeardRumor> ReadRumors(byte[] data, int width, int height)
        {
            var heard = new List<HeardRumor>();
            if (data == null) return heard;
            try
            {
                using var r = new BinaryReader(new MemoryStream(data));
                if (r.ReadUInt32() != Magic || r.ReadInt32() != FormatVersion) return heard;
                if (r.ReadUInt16() != width || r.ReadUInt16() != height) return heard;
                for (int at = 0; at < width * height;)
                {
                    int run = ReadVarint(r);
                    if (run < 0 || run > width * height - at) return heard;
                    at += run;
                }
                int count = ReadVarint(r);
                for (int i = 0; i < count && i < MaxRumors; i++)
                {
                    int kind = ReadVarint(r), index = ReadVarint(r), day = ReadVarint(r);
                    if (kind < 0 || kind > (int)RumorKind.Arena || index < 0 || day < 0) return new List<HeardRumor>();
                    heard.Add(new HeardRumor((RumorKind)kind, index, day));
                }
            }
            catch (EndOfStreamException)
            {
            }
            return heard;
        }

        // The seen tiles, or null when the data is not a map save, is for a map of another size, or is cut short;
        // then the map simply starts empty.
        public static bool[] Read(byte[] data, int width, int height)
        {
            if (data == null) return null;
            try
            {
                using var r = new BinaryReader(new MemoryStream(data));
                if (r.ReadUInt32() != Magic) return null;
                int format = r.ReadInt32();
                if (format != 1 && format != FormatVersion) return null;
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
                // Format 2's rumours follow; one cut short there is cut short.
                if (format >= 2)
                {
                    int count = ReadVarint(r);
                    for (int i = 0; i < count * 3; i++) ReadVarint(r);
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
