using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A tile that can be dug: how many hits it takes and the material it gives, plus sometimes one extra.
    public sealed class TileDefinition
    {
        internal TileDefinition(
            TileType tile, string name, int hardness, MaterialDefinition material, int count, MaterialDefinition extra, float extraChance, bool drillable,
            float light = 0, string lightColor = "", int pickaxe = 0)
        {
            Pickaxe = pickaxe;
            Drillable = drillable;
            Light = light;
            LightColor = lightColor;
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
        // The item level a pickaxe needs to dig it (tiles.csv pickaxe), 0 for any pickaxe.
        public int Pickaxe { get; }
        // How far it lights the dark caves around it, in tiles; 0 for none. Only the view uses it.
        public float Light { get; }
        // #RRGGBB, or empty for the warm light of a flame.
        public string LightColor { get; }
    }

    // A machine's numbers: seconds per ore, per tile moved or per smelt, and how much it holds. Docs/Automation.md.
    public sealed class MachineDefinition
    {
        internal MachineDefinition(TileType tile, float seconds, int capacity, int power = 0, bool powerOnly = false)
        {
            PowerOnly = powerOnly;
            Tile = tile;
            Seconds = seconds;
            Capacity = capacity;
            Power = power;
        }

        public TileType Tile { get; }
        public float Seconds { get; }
        public int Ticks => World.Ticks(Seconds);
        public int Capacity { get; }
        // What it draws from a power grid to work twice as fast; for a generator, what it gives while it burns. 0 for
        // a machine that power does not speed up (belts, splitters). See Docs/Automation.md, 전력망.
        public int Power { get; }
        // Works only with power, once a tick with the grid's whole draw met, instead of once by itself and once more
        // with power (machines.csv power_only).
        public bool PowerOnly { get; }
    }

    // Something that grows from a seed on a floor tile of its biomes. See Docs/Farming.md and Docs/Time.md.
    public sealed class CropDefinition
    {
        internal CropDefinition(
            string id, string name, MaterialDefinition seed, float growDays, MaterialDefinition material, ConsumableDefinition consumable, int count, int seeds,
            float seedChance, IReadOnlyList<Biome> biomes = null, float regrowDays = 0)
        {
            RegrowDays = regrowDays;
            Id = id;
            Name = name;
            Seed = seed;
            GrowDays = growDays;
            Biomes = biomes ?? Array.Empty<Biome>();
            HarvestMaterial = material;
            HarvestConsumable = consumable;
            Count = count;
            Seeds = seeds;
            SeedChance = seedChance;
        }

        public string Id { get; }
        public string Name { get; }
        public MaterialDefinition Seed { get; }
        // Days of play (Calendar.DaySeconds, real time) it must have water for to ripen.
        public float GrowDays { get; }
        // For a crop that stays after harvest and ripens again: the days of water it needs to do so; 0 for one that is
        // gone once harvested.
        public float RegrowDays { get; }
        public long RegrowTicks => RegrowDays <= 0 ? 0 : Math.Max(1, (long)Math.Round(RegrowDays * Calendar.TicksPerDay));
        public bool Regrows => RegrowDays > 0;
        public long GrowTicks => Math.Max(1, (long)Math.Round(GrowDays * Calendar.TicksPerDay));
        // Where it can be planted; none means anywhere.
        public IReadOnlyList<Biome> Biomes { get; }

        public bool GrowsIn(Biome biome) => Biomes.Count == 0 || Biomes.Contains(biome);

        // "흙 동굴, 버섯 숲", or "어디서나".
        public string BiomeNames => Biomes.Count == 0 ? "어디서나" : string.Join(", ", Biomes.Select(BiomeMap.Name));
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
                    float light = row["light"].Length > 0 ? SheetValues.ParseDecimal(row["light"], "light") : 0;
                    if (light < 0 || light > MaxLight) throw new FormatException($"light는 0~{MaxLight} 사이의 타일 수입니다.");
                    string lightColor = row["light_color"];
                    if (lightColor.Length > 0 && !SheetValues.IsColor(lightColor)) throw new FormatException($"light_color '{lightColor}'는 #RRGGBB로 적습니다.");
                    int pickaxe = row["pickaxe"].Length > 0 ? SheetValues.ParseNumber(row["pickaxe"], "pickaxe") : 0;
                    tiles.Add(new TileDefinition(tile, name, hardness, material, count, extra, extraChance, drillable, light, lightColor, pickaxe));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return new TileData(tiles, crops, machines, errors);
        }

        // The view lights at most this far around a tile; more would not fit its light map.
        const float MaxLight = 12;

        static readonly TileType[] MachineTiles =
        {
            TileType.Drill, TileType.Smelter, TileType.Conveyor, TileType.Splitter, TileType.Arm, TileType.Sprinkler, TileType.Harvester,
            TileType.Filter, TileType.Generator, TileType.Lever, TileType.Assembler, TileType.PowerPole, TileType.FastConveyor, TileType.UndergroundBelt,
            TileType.Keg, TileType.PreservesJar, TileType.Coop, TileType.CrabPot,
            TileType.Turret, TileType.Barn, TileType.ArcFurnace, TileType.WaterWheel, TileType.SolarPanel, TileType.TeslaTower,
        };

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
                    if (!MachineTiles.Contains(tile)) throw new FormatException($"'{tile}'는 기계가 아닙니다({string.Join(", ", MachineTiles)}).");
                    if (machines.Any(m => m.Tile == tile)) throw new FormatException($"'{tile}'가 이미 있습니다.");
                    float seconds = SheetValues.ParseDecimal(row["seconds"], "seconds");
                    if (seconds <= 0) throw new FormatException("seconds는 0보다 커야 합니다.");
                    int capacity = SheetValues.ParseNumber(row["capacity"], "capacity");
                    if (capacity < 1) throw new FormatException("capacity는 1 이상이어야 합니다.");
                    int power = string.IsNullOrWhiteSpace(row["power"]) ? 0 : SheetValues.ParseNumber(row["power"], "power");
                    if (power < 0) throw new FormatException("power는 0 이상이어야 합니다.");
                    bool powerOnly = SheetValues.ParseBool(row["power_only"], "power_only");
                    if (powerOnly && power == 0) throw new FormatException("power_only인 기계는 power가 있어야 합니다.");
                    machines.Add(new MachineDefinition(tile, seconds, capacity, power, powerOnly));
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
            var missing = table.MissingColumns("id", "name", "seed", "grow_days", "biomes", "harvest", "count", "seeds", "seed_chance");
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
                    float grow = SheetValues.ParseDecimal(row["grow_days"], "grow_days");
                    if (grow <= 0) throw new FormatException("grow_days는 0보다 커야 합니다.");
                    var biomes = row["biomes"].Length == 0 ? new List<Biome>()
                        : row["biomes"].Split('|').Select(b => SheetValues.ParseEnum<Biome>(b.Trim(), "biomes")).Distinct().ToList();
                    var material = loot.Materials.FirstOrDefault(m => m.Id == row["harvest"]);
                    var consumable = material == null ? loot.Consumables.FirstOrDefault(c => c.Id == row["harvest"]) : null;
                    if (material == null && consumable == null) throw new FormatException($"harvest '{row["harvest"]}'가 재료나 소모품에 없습니다.");
                    int count = SheetValues.ParseNumber(row["count"], "count");
                    if (count < 1) throw new FormatException("count는 1 이상이어야 합니다.");
                    float regrow = row["regrow_days"].Length == 0 ? 0 : SheetValues.ParseDecimal(row["regrow_days"], "regrow_days");
                    if (regrow < 0 || regrow >= grow && regrow > 0) throw new FormatException("regrow_days는 비우거나 0보다 크고 grow_days보다 작아야 합니다.");
                    crops.Add(new CropDefinition(
                        id, name, seed, grow, material, consumable, count, SheetValues.ParseNumber(row["seeds"], "seeds"), Percent(row["seed_chance"], "seed_chance"),
                        biomes, regrow));
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
