using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Core
{
    // A world as its host saves it: the seed to rebuild the caves from, the tiles players changed since, the crops
    // and how long they had grown, what the chests and machines hold, and how far the bosses have moved drops on. Monsters, ground loot and half-dug
    // walls are not kept. Tiles and crops are written by name, so a save still reads after the sheets change.
    // SavedAt is given by the caller (Core never reads the clock) and lets crops grow for the time the world was
    // closed. See Docs/Saving.md.
    public sealed class WorldSave
    {
        const string Magic = "PTWO";
        // 2 added chests, 3 machines.
        const int FormatVersion = 3;

        WorldSave(
            ulong seed,
            long savedAt,
            int progressLevel,
            List<TileChange> tiles,
            List<(CropDefinition Crop, int X, int Y, long Age)> crops,
            List<(int X, int Y, Inventory Contents)> chests,
            List<SavedMachine> machines,
            List<string> warnings)
        {
            Chests = chests;
            Machines = machines;
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
        public IReadOnlyList<(int X, int Y, Inventory Contents)> Chests { get; }
        public IReadOnlyList<SavedMachine> Machines { get; }

        public sealed class SavedMachine
        {
            internal SavedMachine(int x, int y, int direction, int timer, MaterialDefinition carried, float progress,
                List<(MaterialDefinition, int)> input, List<(MaterialDefinition, int)> output)
            {
                X = x;
                Y = y;
                Direction = direction;
                Timer = timer;
                Carried = carried;
                Progress = progress;
                Input = input;
                Output = output;
            }

            public int X { get; }
            public int Y { get; }
            public int Direction { get; }
            public int Timer { get; }
            public MaterialDefinition Carried { get; }
            public float Progress { get; }
            public IReadOnlyList<(MaterialDefinition Material, int Count)> Input { get; }
            public IReadOnlyList<(MaterialDefinition Material, int Count)> Output { get; }
        }
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

                var chests = world.Chests.ToList();
                w.Write(chests.Count);
                foreach (var (x, y, contents) in chests)
                {
                    w.Write((ushort)x);
                    w.Write((ushort)y);
                    w.Write((ushort)contents.Items.Count);
                    foreach (var item in contents.Items) SavedItems.Write(w, item);
                    SavedItems.WriteCounts(w, contents.Runes.Select(p => (p.Key.Id, p.Value)));
                    SavedItems.WriteCounts(w, contents.Consumables.Select(p => (p.Key.Id, p.Value)));
                    SavedItems.WriteCounts(w, contents.Materials.Select(p => (p.Key.Id, p.Value)));
                }

                var machines = world.Machines.ToList();
                w.Write(machines.Count);
                foreach (var machine in machines)
                {
                    w.Write((ushort)machine.X);
                    w.Write((ushort)machine.Y);
                    w.Write((byte)machine.Direction);
                    w.Write(machine.Timer);
                    w.Write(machine.Carried?.Id ?? "");
                    w.Write(machine.Progress);
                    SavedItems.WriteCounts(w, machine.Inputs.Select(p => (p.Key.Id, p.Value)));
                    SavedItems.WriteCounts(w, machine.Outputs.Select(p => (p.Key.Id, p.Value)));
                }
            }
            return stream.ToArray();
        }

        // Throws InvalidDataException when the data is not a world save this game can read. Chests need the loot
        // sheets to read their contents; without them chests come back empty.
        public static WorldSave Read(byte[] data, TileData tileData, LootData loot = null)
        {
            var warnings = new List<string>();
            try
            {
                using var r = new BinaryReader(new MemoryStream(data ?? Array.Empty<byte>()), Encoding.UTF8);
                int version = SavedItems.ReadHeader(r, Magic, FormatVersion, "월드");
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

                var chests = new List<(int, int, Inventory)>();
                for (int count = version >= 2 ? r.ReadInt32() : 0; count > 0; count--)
                {
                    int x = r.ReadUInt16();
                    int y = r.ReadUInt16();
                    var contents = new Inventory();
                    int items = r.ReadUInt16();
                    for (int i = 0; i < items; i++)
                    {
                        var item = loot == null ? SkipItem(r) : SavedItems.Read(r, loot, warnings);
                        if (item != null) contents.TryAdd(item);
                    }
                    var runes = SavedItems.ReadCounts(r);
                    var consumables = SavedItems.ReadCounts(r);
                    var materials = SavedItems.ReadCounts(r);
                    if (loot != null)
                    {
                        foreach (var (id, n) in runes) Add(loot.Runes.FirstOrDefault(d => d.Id == id), d => contents.AddRune(d, n), id);
                        foreach (var (id, n) in consumables) Add(loot.Consumables.FirstOrDefault(d => d.Id == id), d => contents.AddConsumable(d, n), id);
                        foreach (var (id, n) in materials) Add(loot.Materials.FirstOrDefault(d => d.Id == id), d => contents.AddMaterial(d, n), id);
                    }
                    chests.Add((x, y, contents));
                }
                var machines = new List<SavedMachine>();
                for (int count = version >= 3 ? r.ReadInt32() : 0; count > 0; count--)
                {
                    int x = r.ReadUInt16();
                    int y = r.ReadUInt16();
                    int direction = r.ReadByte();
                    int timer = r.ReadInt32();
                    string carried = r.ReadString();
                    float along = r.ReadSingle();
                    var input = SavedItems.ReadCounts(r);
                    var output = SavedItems.ReadCounts(r);
                    MaterialDefinition Find(string id) => loot?.Materials.FirstOrDefault(m => m.Id == id);
                    List<(MaterialDefinition, int)> Resolve(List<(string Id, int Count)> stacks) =>
                        stacks.Select(s => (Find(s.Id), s.Count)).Where(s => s.Item1 != null).ToList();
                    machines.Add(new SavedMachine(x, y, direction, timer, carried.Length > 0 ? Find(carried) : null, along, Resolve(input), Resolve(output)));
                }
                return new WorldSave(seed, savedAt, Math.Max(1, progress), tiles, crops, chests, machines, warnings);

                void Add<T>(T definition, Action<T> add, string id) where T : class
                {
                    if (definition != null) add(definition);
                    else warnings.Add($"상자 속 없어진 '{id}'를 건너뛰었습니다.");
                }
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is ArgumentException)
            {
                throw new InvalidDataException("월드 저장본이 깨졌습니다.", e);
            }
        }

        // Reads past an item without the sheets to build it.
        static Item SkipItem(BinaryReader r)
        {
            r.ReadString();
            r.ReadInt16();
            r.ReadByte();
            for (int count = r.ReadByte(); count > 0; count--) r.ReadInt32();
            for (int count = r.ReadByte(); count > 0; count--)
            {
                r.ReadString();
                r.ReadByte();
                r.ReadInt32();
            }
            for (int count = r.ReadByte(); count > 0; count--) r.ReadString();
            return null;
        }
    }
}
