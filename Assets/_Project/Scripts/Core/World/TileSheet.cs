using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A tile that can be dug: how many hits it takes and the material it gives, plus sometimes one extra.
    public sealed class TileDefinition
    {
        internal TileDefinition(TileType tile, string name, int hardness, MaterialDefinition material, int count, MaterialDefinition extra, float extraChance, bool drillable)
        {
            Drillable = drillable;
            Tile = tile;
            Name = name;
            Hardness = hardness;
            Material = material;
            Count = count;
            Extra = extra;
            ExtraChance = extraChance;
        }

        public TileType Tile { get; }
        public string Name { get; }
        // Hits at no mining speed.
        public int Hardness { get; }
        public MaterialDefinition Material { get; }
        public int Count { get; }
        // Null for none; percent chance otherwise.
        public MaterialDefinition Extra { get; }
        public float ExtraChance { get; }
        // An ore vein a drill can pull Material from without using it up.
        public bool Drillable { get; }
    }

    // A machine's numbers: seconds per ore, per tile moved or per smelt, and how much it holds. Docs/Automation.md.
    public sealed class MachineDefinition
    {
        internal MachineDefinition(TileType tile, float seconds, int capacity)
        {
            Tile = tile;
            Seconds = seconds;
            Capacity = capacity;
        }

        public TileType Tile { get; }
        public float Seconds { get; }
        public int Ticks => World.Ticks(Seconds);
        public int Capacity { get; }
    }

    // Something that grows from a seed on a floor tile. See Docs/Farming.md.
    public sealed class CropDefinition
    {
        internal CropDefinition(
            string id, string name, MaterialDefinition seed, float growSeconds, MaterialDefinition material, ConsumableDefinition consumable, int count, int seeds, float seedChance)
        {
            Id = id;
            Name = name;
            Seed = seed;
            GrowSeconds = growSeconds;
            HarvestMaterial = material;
            HarvestConsumable = consumable;
            Count = count;
            Seeds = seeds;
            SeedChance = seedChance;
        }

        public string Id { get; }
        public string Name { get; }
        public MaterialDefinition Seed { get; }
        public float GrowSeconds { get; }
        public int GrowTicks => World.Ticks(GrowSeconds);
        // What a harvest gives: one of these, Count of it.
        public MaterialDefinition HarvestMaterial { get; }
        public ConsumableDefinition HarvestConsumable { get; }
        public int Count { get; }
        // Seeds every harvest gives back, and the percent chance of one more.
        public int Seeds { get; }
        public float SeedChance { get; }
        public string HarvestName => HarvestMaterial?.Name ?? HarvestConsumable.Name;
    }

    // What the world's tiles do: which can be dug (tiles.csv) and what can grow on them (crops.csv).
    public sealed class TileData
    {
        internal TileData(IReadOnlyList<TileDefinition> tiles, IReadOnlyList<CropDefinition> crops, IReadOnlyList<MachineDefinition> machines, IReadOnlyList<string> errors)
        {
            Tiles = tiles;
            Crops = crops;
            Machines = machines;
            Errors = errors;
        }

        public IReadOnlyList<MachineDefinition> Machines { get; }

        // Null for a tile that is not a machine.
        public MachineDefinition Machine(TileType tile) => Machines.FirstOrDefault(m => m.Tile == tile);

        public IReadOnlyList<TileDefinition> Tiles { get; }
        // The network names crops by their index here.
        public IReadOnlyList<CropDefinition> Crops { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        // Null for a tile nobody can dig.
        public TileDefinition Find(TileType tile) => Tiles.FirstOrDefault(t => t.Tile == tile);

        // The crop this material is the seed of, or null.
        public CropDefinition CropFrom(MaterialDefinition seed) => Crops.FirstOrDefault(c => c.Seed == seed);
    }

    // Reads Assets/_Project/Data/World: tiles.csv (columns in Docs/Crafting.md), crops.csv (Docs/Farming.md) and
    // machines.csv (Docs/Automation.md).
    public static class TileSheet
    {
        public const string File = "tiles.csv";
        public const string CropsFile = "crops.csv";
        public const string MachinesFile = "machines.csv";

        // Without a crops sheet nothing grows; without a machines sheet no machine runs.
        public static TileData Parse(string csv, LootData loot, string cropsCsv = "", string machinesCsv = "")
        {
            var errors = new List<string>();
            var tiles = new List<TileDefinition>();
            var crops = ParseCrops(cropsCsv, loot, errors);
            var machines = ParseMachines(machinesCsv, errors);
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("tile", "name", "hardness", "material", "count");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new TileData(tiles, crops, machines, errors);
            }

            foreach (var row in table.Rows)
            {
                try
                {
                    var tile = SheetValues.ParseEnum<TileType>(row["tile"], "칸 종류");
                    if (tile == TileType.Floor) throw new FormatException("바닥은 캘 수 없습니다.");
                    if (tiles.Any(t => t.Tile == tile)) throw new FormatException($"'{tile}'가 이미 있습니다.");
                    string name = row["name"];
                    if (name.Length == 0) throw new FormatException("'name' 칸이 비어 있습니다.");
                    int hardness = SheetValues.ParseNumber(row["hardness"], "hardness");
                    if (hardness < 1) throw new FormatException("hardness는 1 이상이어야 합니다.");
                    var material = loot?.Materials.FirstOrDefault(m => m.Id == row["material"])
                        ?? throw new FormatException($"재료 '{row["material"]}'가 {LootSheets.MaterialsFile}에 없습니다.");
                    int count = SheetValues.ParseNumber(row["count"], "count");
                    if (count < 1) throw new FormatException("count는 1 이상이어야 합니다.");
                    MaterialDefinition extra = null;
                    float extraChance = 0;
                    if (row["extra"].Length > 0)
                    {
                        extra = loot.Materials.FirstOrDefault(m => m.Id == row["extra"])
                            ?? throw new FormatException($"재료 '{row["extra"]}'가 {LootSheets.MaterialsFile}에 없습니다.");
                        extraChance = Percent(row["extra_chance"], "extra_chance");
                    }
                    bool drillable = row["drill"].Length > 0 && row["drill"].ToLowerInvariant() == "yes";
                    tiles.Add(new TileDefinition(tile, name, hardness, material, count, extra, extraChance, drillable));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return new TileData(tiles, crops, machines, errors);
        }

        static readonly TileType[] MachineTiles = { TileType.Drill, TileType.Smelter, TileType.Conveyor };

        static List<MachineDefinition> ParseMachines(string csv, List<string> errors)
        {
            var machines = new List<MachineDefinition>();
            if (string.IsNullOrWhiteSpace(csv)) return machines;
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("tile", "seconds", "capacity");
            if (missing.Count > 0)
            {
                errors.Add($"{MachinesFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return machines;
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    var tile = SheetValues.ParseEnum<TileType>(row["tile"], "칸 종류");
                    if (!MachineTiles.Contains(tile)) throw new FormatException($"'{tile}'는 기계가 아닙니다(Drill, Smelter, Conveyor).");
                    if (machines.Any(m => m.Tile == tile)) throw new FormatException($"'{tile}'가 이미 있습니다.");
                    float seconds = SheetValues.ParseDecimal(row["seconds"], "seconds");
                    if (seconds <= 0) throw new FormatException("seconds는 0보다 커야 합니다.");
                    int capacity = SheetValues.ParseNumber(row["capacity"], "capacity");
                    if (capacity < 1) throw new FormatException("capacity는 1 이상이어야 합니다.");
                    machines.Add(new MachineDefinition(tile, seconds, capacity));
                }
                catch (FormatException e)
                {
                    errors.Add($"{MachinesFile} {row.Line}행: {e.Message}");
                }
            }
            return machines;
        }

        static List<CropDefinition> ParseCrops(string csv, LootData loot, List<string> errors)
        {
            var crops = new List<CropDefinition>();
            if (string.IsNullOrWhiteSpace(csv)) return crops;
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "name", "seed", "grow_seconds", "harvest", "count", "seeds", "seed_chance");
            if (missing.Count > 0)
            {
                errors.Add($"{CropsFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return crops;
            }

            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (crops.Any(c => c.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    string name = row["name"];
                    if (name.Length == 0) throw new FormatException("'name' 칸이 비어 있습니다.");
                    var seed = loot.Materials.FirstOrDefault(m => m.Id == row["seed"])
                        ?? throw new FormatException($"씨앗 '{row["seed"]}'가 {LootSheets.MaterialsFile}에 없습니다.");
                    if (crops.Any(c => c.Seed == seed)) throw new FormatException($"'{seed.Id}'는 이미 다른 작물의 씨앗입니다.");
                    float grow = SheetValues.ParseDecimal(row["grow_seconds"], "grow_seconds");
                    if (grow <= 0) throw new FormatException("grow_seconds는 0보다 커야 합니다.");
                    var material = loot.Materials.FirstOrDefault(m => m.Id == row["harvest"]);
                    var consumable = material == null ? loot.Consumables.FirstOrDefault(c => c.Id == row["harvest"]) : null;
                    if (material == null && consumable == null) throw new FormatException($"harvest '{row["harvest"]}'가 재료나 소모품에 없습니다.");
                    int count = SheetValues.ParseNumber(row["count"], "count");
                    if (count < 1) throw new FormatException("count는 1 이상이어야 합니다.");
                    crops.Add(new CropDefinition(
                        id, name, seed, grow, material, consumable, count, SheetValues.ParseNumber(row["seeds"], "seeds"), Percent(row["seed_chance"], "seed_chance")));
                }
                catch (FormatException e)
                {
                    errors.Add($"{CropsFile} {row.Line}행: {e.Message}");
                }
            }
            return crops;
        }

        static float Percent(string raw, string column)
        {
            if (raw.Length == 0) return 0;
            float value = SheetValues.ParseDecimal(raw, column);
            return value <= 100 ? value : throw new FormatException($"{column}는 0~100 사이의 퍼센트입니다.");
        }
    }
}
