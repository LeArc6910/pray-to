using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Core
{
    // The map's fingerprint and the map itself for a joiner whose own came out otherwise (2026-11-27, Docs/CrossPlay.md
    // 지도 일치). Everyone makes the map from the seed, but the generator still has float math (a few dozen lines), so a
    // phone, a browser and a PC could round one step apart. The host says its print in Welcome; a joiner whose print
    // differs asks for the host's tiles and biomes (MapRequestMessage) and takes them (MapDataMessage), run-length
    // packed: rock and floor come in long runs, so the game's map is a few hundred kilobytes rather than two megabytes.
    public static class MapPrint
    {
        // FNV-1a over the world's tiles and biomes, row by row (not the dungeons' pocket above it).
        public static ulong Of(TileMap map, BiomeMap biomes)
        {
            ulong hash = 14695981039346656037UL;
            int height = Math.Min(map.Height, biomes.Height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    hash = (hash ^ (byte)map[x, y]) * 1099511628211UL;
                    hash = (hash ^ (byte)biomes[x, y]) * 1099511628211UL;
                }
            }
            return hash == 0 ? 1 : hash;
        }

        // Runs of (count, tile, biome), the count a ushort.
        public static byte[] Pack(TileMap map, BiomeMap biomes)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            int height = Math.Min(map.Height, biomes.Height);
            w.Write(map.Width);
            w.Write(height);
            int count = 0;
            byte tile = 0, biome = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    byte t = (byte)map[x, y], b = (byte)biomes[x, y];
                    if (count > 0 && (t != tile || b != biome || count == ushort.MaxValue))
                    {
                        w.Write((ushort)count);
                        w.Write(tile);
                        w.Write(biome);
                        count = 0;
                    }
                    tile = t;
                    biome = b;
                    count++;
                }
            }
            if (count > 0)
            {
                w.Write((ushort)count);
                w.Write(tile);
                w.Write(biome);
            }
            return stream.ToArray();
        }

        // Puts the packed tiles and biomes over the map. False (and nothing changed) when they are not of its size.
        public static bool Unpack(byte[] data, TileMap map, BiomeMap biomes)
        {
            using var r = new BinaryReader(new MemoryStream(data));
            int width = r.ReadInt32(), height = r.ReadInt32();
            if (width != map.Width || height != Math.Min(map.Height, biomes.Height)) return false;
            var tiles = new List<(int Count, TileType Tile, Biome Biome)>();
            long total = 0;
            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                int count = r.ReadUInt16();
                tiles.Add((count, (TileType)r.ReadByte(), (Biome)r.ReadByte()));
                total += count;
            }
            if (total != (long)width * height) return false;
            int i = 0;
            foreach (var (count, tile, biome) in tiles)
            {
                for (int k = 0; k < count; k++, i++)
                {
                    int x = i % width, y = i / width;
                    if (map[x, y] != tile) map[x, y] = tile;
                    biomes.Set(x, y, biome);
                }
            }
            return true;
        }

        // The packed map in parts of at most this many bytes, each one message.
        public const int PartBytes = 48 * 1024;
    }

    public sealed class MapRequestMessage
    {
        public byte[] Encode() => Protocol.Build(MessageType.MapRequest, w => { });
    }

    public sealed class MapDataMessage
    {
        public MapDataMessage(int part, int parts, byte[] bytes)
        {
            Part = part;
            Parts = parts;
            Bytes = bytes ?? Array.Empty<byte>();
        }

        public int Part { get; }
        public int Parts { get; }
        public byte[] Bytes { get; }

        public byte[] Encode() => Protocol.Build(MessageType.MapData, w =>
        {
            w.Write(Part);
            w.Write(Parts);
            w.Write(Bytes.Length);
            w.Write(Bytes);
        });

        public static MapDataMessage Decode(byte[] data) => Protocol.Read(data, MessageType.MapData, r =>
        {
            int part = r.ReadInt32(), parts = r.ReadInt32(), length = r.ReadInt32();
            if (length < 0 || length > MapPrint.PartBytes || parts <= 0 || parts > 4096 || part < 0 || part >= parts) throw new InvalidDataException("Bad map part.");
            return new MapDataMessage(part, parts, r.ReadBytes(length));
        });

        // The packed map cut into its parts' messages.
        public static List<byte[]> Split(byte[] packed)
        {
            var messages = new List<byte[]>();
            int parts = Math.Max(1, (packed.Length + MapPrint.PartBytes - 1) / MapPrint.PartBytes);
            for (int i = 0; i < parts; i++)
            {
                int start = i * MapPrint.PartBytes, length = Math.Min(MapPrint.PartBytes, packed.Length - start);
                var bytes = new byte[length];
                Array.Copy(packed, start, bytes, 0, length);
                messages.Add(new MapDataMessage(i, parts, bytes).Encode());
            }
            return messages;
        }
    }
}
