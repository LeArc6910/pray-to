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
        internal const string Magic = "PTWO";
        // 2 added chests, 3 machines, 4 whether each crop was watered, 5 beds and their owners.
        // 6: a filter's chosen material on each machine. 7: the world's clock (Calendar), and crops as how far they have
        // grown and the water they have left (Docs/Time.md). 8: the highest void rift tier it may open. 9: the base
        // defense level and the day it was last defended (Docs/Defense.md).
        // 10 (2026-11-09): each machine ends with an animal house's affection and the day it was last petted.
        // 11 (2026-11-09): locked doors and their owners, after the beds.
        const int FormatVersion = 11;
        // How long each crop took to grow before format 7, in seconds, so a crop from then comes back as far along.
        static readonly Dictionary<string, float> SecondsBeforeDays = new Dictionary<string, float>
        {
            ["mushroom"] = 180,
            ["fire_pepper"] = 240,
            ["glow_cap"] = 200,
            ["frost_berry"] = 240,
            ["star_melon"] = 300,
        };

        WorldSave(
            ulong seed,
            long savedAt,
            long clock,
            int riftTier,
            int progressLevel,
            List<TileChange> tiles,
            List<(CropDefinition Crop, int X, int Y, long Grown, long WetLeft)> crops,
            List<(int X, int Y, Inventory Contents)> chests,
            List<SavedMachine> machines,
            List<string> warnings,
            List<(int X, int Y, string Owner)> beds = null,
            int defenseLevel = 1,
            int defenseDay = 0,
            List<(int X, int Y, string Owner)> locks = null)
        {
            Locks = locks ?? new List<(int, int, string)>();
            DefenseLevel = defenseLevel;
            DefenseDay = defenseDay;
            Beds = beds ?? new List<(int, int, string)>();
            Chests = chests;
            Machines = machines;
            Seed = seed;
            SavedAt = savedAt;
            Clock = clock;
            RiftTier = riftTier;
            ProgressLevel = progressLevel;
            Tiles = tiles;
            Crops = crops;
            Warnings = warnings;
        }

        public ulong Seed { get; }
        // The map's size when it was made: a world saved before the bigger map (2026-11-11) goes on at its own size.
        public int Width { get; private set; } = WorldGenerator.DefaultSize;
        public int Height { get; private set; } = WorldGenerator.DefaultSize;
        // Seconds since 1970 when it was saved, as the caller counted them. Nothing grows by it: a closed world waits.
        public long SavedAt { get; }
        // The world's clock (Calendar); 0, the first morning, for a save from before days.
        public long Clock { get; }
        // 1 for a save from before rifts.
        public int RiftTier { get; }
        // 1 and 0 (never) for a save from before base defense.
        public int DefenseLevel { get; }
        public int DefenseDay { get; }
        public int ProgressLevel { get; }
        public IReadOnlyList<TileChange> Tiles { get; }
        // How many ticks each crop had grown, and the ticks of water it had left.
        public IReadOnlyList<(CropDefinition Crop, int X, int Y, long Grown, long WetLeft)> Crops { get; }
        public IReadOnlyList<(int X, int Y, Inventory Contents)> Chests { get; }
        // Whose bed is where, by player name.
        public IReadOnlyList<(int X, int Y, string Owner)> Beds { get; }
        // Whose lock is on which door (World.LockDoor), by player name.
        public IReadOnlyList<(int X, int Y, string Owner)> Locks { get; }
        public IReadOnlyList<SavedMachine> Machines { get; }

        public sealed class SavedMachine
        {
            internal SavedMachine(int x, int y, int direction, int timer, MaterialDefinition carried, float progress,
                List<(MaterialDefinition, int)> input, List<(MaterialDefinition, int)> output, IReadOnlyList<MaterialDefinition> settings = null,
                Recipe recipe = null, int affection = 0, int pettedDay = -1)
            {
                Affection = affection;
                PettedDay = pettedDay;
                Settings = settings ?? Array.Empty<MaterialDefinition>();
                Recipe = recipe;
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
            // A filter's chosen material, or null.
            // A filter's materials.
            public IReadOnlyList<MaterialDefinition> Settings { get; }
            // An assembler's recipe, or null.
            public Recipe Recipe { get; }
            public IReadOnlyList<(MaterialDefinition Material, int Count)> Output { get; }
            // A coop's, barn's or fold's (World.PetAnimals); 0 and -1 for the rest.
            public int Affection { get; }
            public int PettedDay { get; }
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
                w.Write(world.Clock);
                w.Write(world.RiftTier);
                w.Write(world.DefenseLevel);
                w.Write(world.DefenseDay);
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
                    w.Write((long)crop.GrownAt(world.Tick));
                    w.Write(crop.WetLeft(world.Tick));
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
                    // The high bit is set for a machine switched off; saves from before switches have it clear.
                    w.Write((byte)(machine.Direction | (machine.Enabled ? 0 : Machine.OffBit)));
                    w.Write(machine.Timer);
                    w.Write(machine.Carried?.Id ?? "");
                    w.Write(machine.Progress);
                    SavedItems.WriteCounts(w, machine.Inputs.Select(p => (p.Key.Id, p.Value)));
                    SavedItems.WriteCounts(w, machine.Outputs.Select(p => (p.Key.Id, p.Value)));
                    // A filter's materials (ids joined by |, which no id has), or an assembler's recipe.
                    w.Write(machine.Settings.Count > 0 ? string.Join("|", machine.Settings.Select(m => m.Id)) : machine.Recipe?.Id ?? "");
                    w.Write((byte)machine.Affection);
                    w.Write(machine.PettedDay);
                }

                var beds = world.Beds.ToList();
                w.Write(beds.Count);
                foreach (var (x, y, owner) in beds)
                {
                    w.Write((ushort)x);
                    w.Write((ushort)y);
                    w.Write(owner);
                }

                var locks = world.Locks.ToList();
                w.Write(locks.Count);
                foreach (var (x, y, owner) in locks)
                {
                    w.Write((ushort)x);
                    w.Write((ushort)y);
                    w.Write(owner);
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
                long clock = version >= 7 ? r.ReadInt64() : 0;
                int riftTier = version >= 8 ? r.ReadInt32() : 1;
                int defenseLevel = version >= 9 ? r.ReadInt32() : 1;
                int defenseDay = version >= 9 ? r.ReadInt32() : 0;
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

                var crops = new List<(CropDefinition, int, int, long, long)>();
                for (int count = r.ReadInt32(); count > 0; count--)
                {
                    int x = r.ReadUInt16();
                    int y = r.ReadUInt16();
                    string id = r.ReadString();
                    long grown = Math.Max(0, r.ReadInt64());
                    long wetLeft = 0;
                    if (version >= 7) wetLeft = Math.Max(0, r.ReadInt64());
                    else if (version >= 4) r.ReadBoolean();
                    var crop = tileData?.Crops.FirstOrDefault(c => c.Id == id);
                    if (crop == null)
                    {
                        warnings.Add($"없어진 작물 '{id}'를 건너뛰었습니다.");
                        continue;
                    }
                    // Before days a crop's age was ticks since planting; it comes back as far along, dry.
                    if (version < 7 && SecondsBeforeDays.TryGetValue(id, out float before))
                        grown = (long)Math.Min(crop.GrowTicks, grown / (before * World.TicksPerSecond) * crop.GrowTicks);
                    crops.Add((crop, x, y, grown, wetLeft));
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
                    string setting = version >= 6 ? r.ReadString() : "";
                    var picks = setting.Split('|').Where(id => id.Length > 0).Select(Find).Where(m => m != null).ToList();
                    int affection = version >= 10 ? r.ReadByte() : 0;
                    int pettedDay = version >= 10 ? r.ReadInt32() : -1;
                    machines.Add(new SavedMachine(x, y, direction, timer, carried.Length > 0 ? Find(carried) : null, along, Resolve(input), Resolve(output),
                        picks, setting.Length > 0 ? loot?.Recipes.FirstOrDefault(rc => rc.Id == setting) : null, affection, pettedDay));
                }
                var beds = new List<(int, int, string)>();
                for (int count = version >= 5 ? r.ReadInt32() : 0; count > 0; count--)
                {
                    int x = r.ReadUInt16();
                    int y = r.ReadUInt16();
                    beds.Add((x, y, r.ReadString()));
                }
                var locks = new List<(int, int, string)>();
                for (int count = version >= 11 ? r.ReadInt32() : 0; count > 0; count--)
                {
                    int x = r.ReadUInt16();
                    int y = r.ReadUInt16();
                    locks.Add((x, y, r.ReadString()));
                }
                return new WorldSave(seed, savedAt, clock, riftTier, Math.Max(1, progress), tiles, crops, chests, machines, warnings, beds,
                    defenseLevel, defenseDay, locks) { Width = width, Height = height };

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
