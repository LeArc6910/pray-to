using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Core
{
    // A world as its host saves it: the seed to rebuild the caves from, the tiles players changed since, the crops
    // and how long they had grown, and how far the bosses have moved drops on. Monsters, ground loot and half-dug
    // walls are not kept. Tiles and crops are written by name, so a save still reads after the sheets change.
    // SavedAt is given by the caller (Core never reads the clock) and lets crops grow for the time the world was
    // closed. See Docs/Saving.md.
    public sealed class WorldSave
    {
        const string Magic = "PTWO";
        const int FormatVersion = 1;

        WorldSave(ulong seed, long savedAt, int progressLevel, List<TileChange> tiles, List<(CropDefinition Crop, int X, int Y, long Age)> crops, List<string> warnings)
        {
            Seed = seed;
            SavedAt = savedAt;
            ProgressLevel = progressLevel;
            Tiles = tiles;
            Crops = crops;
            Warnings = warnings;
        }

        public ulong Seed { get; }
        // Seconds since 1970 when it was saved, as the caller counted them.
        public long SavedAt { get; }
        public int ProgressLevel { get; }
        public IReadOnlyList<TileChange> Tiles { get; }
        // Age is in ticks at the moment of saving.
        public IReadOnlyList<(CropDefinition Crop, int X, int Y, long Age)> Crops { get; }
        public IReadOnlyList<string> Warnings { get; }

        public static byte[] Write(World world, ulong seed, long savedAt)
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                w.Write(Encoding.ASCII.GetBytes(Magic));
                w.Write(FormatVersion);
                w.Write(seed);
                w.Write(world.Map.Width);
                w.Write(world.Map.Height);
                w.Write(savedAt);
                w.Write(world.ProgressLevel);

                var tiles = world.ChangedTiles();
                var names = tiles.Select(t => t.Type.ToString()).Distinct().ToList();
                w.Write((ushort)names.Count);
                foreach (string name in names) w.Write(name);
                w.Write(tiles.Count);
                foreach (var tile in tiles)
                {
                    w.Write((ushort)tile.X);
                    w.Write((ushort)tile.Y);
                    w.Write((byte)names.IndexOf(tile.Type.ToString()));
                }

                var crops = world.Crops.ToList();
                w.Write(crops.Count);
                foreach (var crop in crops)
                {
                    w.Write((ushort)crop.X);
                    w.Write((ushort)crop.Y);
                    w.Write(crop.Definition.Id);
                    w.Write(world.Tick - crop.PlantedTick);
                }
            }
            return stream.ToArray();
        }

        // Throws InvalidDataException when the data is not a world save this game can read.
        public static WorldSave Read(byte[] data, TileData tileData)
        {
            var warnings = new List<string>();
            try
            {
                using var r = new BinaryReader(new MemoryStream(data ?? Array.Empty<byte>()), Encoding.UTF8);
                SavedItems.ReadHeader(r, Magic, FormatVersion, "월드");
                ulong seed = r.ReadUInt64();
                int width = r.ReadInt32();
                int height = r.ReadInt32();
                long savedAt = r.ReadInt64();
                int progress = r.ReadInt32();

                var names = new List<string>();
                for (int count = r.ReadUInt16(); count > 0; count--) names.Add(r.ReadString());
                var types = names.Select(name => Enum.TryParse(name, out TileType type) ? type : (TileType?)null).ToList();
                var tiles = new List<TileChange>();
                for (int count = r.ReadInt32(); count > 0; count--)
                {
                    int x = r.ReadUInt16();
                    int y = r.ReadUInt16();
                    int index = r.ReadByte();
                    var type = index < types.Count ? types[index] : null;
                    if (type == null || x >= width || y >= height) continue;
                    tiles.Add(new TileChange(x, y, type.Value));
                }
                int unknownTiles = names.Count(name => !Enum.TryParse(name, out TileType _));
                if (unknownTiles > 0) warnings.Add($"없어진 칸 종류 {unknownTiles}가지를 건너뛰었습니다.");

                var crops = new List<(CropDefinition, int, int, long)>();
                for (int count = r.ReadInt32(); count > 0; count--)
                {
                    int x = r.ReadUInt16();
                    int y = r.ReadUInt16();
                    string id = r.ReadString();
                    long age = r.ReadInt64();
                    var crop = tileData?.Crops.FirstOrDefault(c => c.Id == id);
                    if (crop != null) crops.Add((crop, x, y, Math.Max(0, age)));
                    else warnings.Add($"없어진 작물 '{id}'를 건너뛰었습니다.");
                }
                return new WorldSave(seed, savedAt, Math.Max(1, progress), tiles, crops, warnings);
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is ArgumentException)
            {
                throw new InvalidDataException("월드 저장본이 깨졌습니다.", e);
            }
        }
    }
}
