using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What a thing to find in the rooms of version 2 is (Docs/WorldRework.md 3단계 볼거리): a camp by its fire, a shrine,
    // a chest, an abandoned cart. Which it is follows from the room's role and the mark its chunk left.
    public enum PoiKind : byte
    {
        Camp,
        Shrine,
        Chest,
        Cart,
    }

    // How often it gives again (pois.csv again).
    public enum PoiAgain : byte
    {
        // Whenever one is by it (a camp's fire).
        Always,
        // Once a day for each player (a shrine).
        Daily,
        // Once for the world, kept in its save (a chest, a cart).
        Once,
    }

    // What it shows from afar (pois.csv signal, Docs/WorldRework.md 먼저 보이고 나중에 닿는다). The screen draws it.
    public enum PoiSignal : byte
    {
        None,
        // Smoke rising over the dark, and the fire's glow.
        Smoke,
        // A pillar of white light.
        Beam,
        // A gold glint, seen through a grate too.
        Glint,
    }

    public enum PoiRewardKind : byte
    {
        // Items rolled at the level (Count of them, at least Rarity).
        Items,
        // Min in a hundred of a rune (over 100: one sure and the rest a chance of another).
        Runes,
        // Potions rolled as monsters drop them.
        Potions,
        Coins,
        // The ore of where it stands (World.OreAt).
        Ore,
        // What grows in its biome (World.Forage).
        Forage,
        // A material named in the sheet.
        Material,
        // Health back, in percent of the most.
        Heal,
        // The row's buff for Min seconds (a shrine's blessing).
        Bless,
        // Health back each second by the fire, in percent of the most (a camp).
        Rest,
        // "Rested" for Min seconds: more experience (a camp).
        Rested,
    }

    // One part of what a thing gives (pois.csv reward, split by '|'): "items:2:uncommon", "coins:20-40",
    // "material:obsidian_ore:6-10", "bless:300".
    public sealed class PoiReward
    {
        internal PoiReward(PoiRewardKind kind, int min, int max, Rarity rarity = Rarity.Common, string material = null)
        {
            Kind = kind;
            Min = min;
            Max = max;
            Rarity = rarity;
            Material = material;
        }

        public PoiRewardKind Kind { get; }
        public int Min { get; }
        public int Max { get; }
        public Rarity Rarity { get; }
        public string Material { get; }

        public override string ToString()
        {
            string range = Max != Min ? $"{Min}-{Max}" : $"{Min}";
            string name = Kind.ToString().ToLowerInvariant();
            return Kind == PoiRewardKind.Material ? $"{name}:{Material}:{range}"
                : Kind == PoiRewardKind.Items && Rarity != Rarity.Common ? $"{name}:{range}:{Rarity.ToString().ToLowerInvariant()}"
                : $"{name}:{range}";
        }
    }

    // A row of pois.csv.
    public sealed class PoiDefinition
    {
        internal PoiDefinition(string id, string name, RoomRole role, IReadOnlyList<Biome> biomes, int weight, IReadOnlyList<PoiReward> rewards, int rewardLevel,
            bool perPlayer, PoiAgain again, PoiSignal signal, int signalRange, IReadOnlyList<Modifier> buff, TileType? tile)
        {
            Id = id;
            Name = name;
            Role = role;
            Biomes = biomes;
            Weight = weight;
            Rewards = rewards;
            RewardLevel = rewardLevel;
            PerPlayer = perPlayer;
            Again = again;
            Signal = signal;
            SignalRange = signalRange;
            Buff = buff;
            Tile = tile;
        }

        public string Id { get; }
        public string Name { get; }
        // The room role it stands in (a shrine stands in a hidden room too: its mark decides).
        public RoomRole Role { get; }
        public IReadOnlyList<Biome> Biomes { get; }
        public int Weight { get; }
        public IReadOnlyList<PoiReward> Rewards { get; }
        // Levels over the zone's that its items are rolled at.
        public int RewardLevel { get; }
        // Each player near rolls their own (a chest, as a boss's drops).
        public bool PerPlayer { get; }
        public PoiAgain Again { get; }
        public PoiSignal Signal { get; }
        // Tiles from which its signal is seen.
        public int SignalRange { get; }
        // What its blessing does while it lasts (bless).
        public IReadOnlyList<Modifier> Buff { get; }
        // A chest's tile while shut.
        public TileType? Tile { get; }
        public PoiKind Kind => KindOf(Role);

        public bool For(Biome biome) => Biomes.Count == 0 || Biomes.Contains(biome);

        public PoiReward Reward(PoiRewardKind kind) => Rewards.FirstOrDefault(r => r.Kind == kind);

        public static PoiKind KindOf(RoomRole role) => role switch
        {
            RoomRole.Rest => PoiKind.Camp,
            RoomRole.Shrine => PoiKind.Shrine,
            RoomRole.Cart => PoiKind.Cart,
            _ => PoiKind.Chest,
        };
    }

    // One thing to find where the generator put it: the tile of the chest, fire, shrine stone or cart, and its room.
    // Numbered in the order laid (room by room), which a world's save and the host's messages count by.
    public sealed class Poi
    {
        internal Poi(int index, PoiDefinition definition, int x, int y, int room, Biome biome)
        {
            Index = index;
            Definition = definition;
            X = x;
            Y = y;
            Room = room;
            Biome = biome;
        }

        public int Index { get; }
        public PoiDefinition Definition { get; }
        public int X { get; }
        public int Y { get; }
        public Vec2 Center => TileMap.Center(X, Y);
        // Its room's index in RoomGraph.Nodes.
        public int Room { get; }
        public Biome Biome { get; }
        public PoiKind Kind => Definition.Kind;
    }

    public sealed class PoiData
    {
        public static readonly PoiData None = new PoiData(new PoiDefinition[0], new string[0]);

        internal PoiData(IReadOnlyList<PoiDefinition> pois, IReadOnlyList<string> errors)
        {
            Pois = pois;
            Errors = errors;
        }

        public IReadOnlyList<PoiDefinition> Pois { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public PoiDefinition Find(string id) => Pois.FirstOrDefault(p => p.Id == id);

        // The rows a thing of the role in the biome can be, in sheet order.
        public IEnumerable<PoiDefinition> For(RoomRole role, Biome biome) => Pois.Where(p => p.Role == role && p.For(biome));
    }

    // Reads Assets/_Project/Data/World/pois.csv (Docs/Biomes.md 볼거리).
    public static class PoiSheet
    {
        public const string File = "pois.csv";

        public static PoiData Parse(string csv, LootData loot = null)
        {
            if (string.IsNullOrWhiteSpace(csv)) return PoiData.None;
            var errors = new List<string>();
            var pois = new List<PoiDefinition>();
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "name", "role", "biomes", "weight", "reward", "reward_level", "per_player", "again", "signal", "signal_range", "buff", "tile");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new PoiData(pois, errors);
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'가 올바르지 않습니다.");
                    if (pois.Any(p => p.Id == id)) throw new FormatException($"'{id}'가 두 번 있습니다.");
                    string name = row["name"].Length > 0 ? row["name"] : id;
                    var role = SheetValues.ParseEnum<RoomRole>(row["role"], "role");
                    if (role != RoomRole.Rest && role != RoomRole.Shrine && role != RoomRole.Cart && role != RoomRole.Treasure && role != RoomRole.Hidden
                        && role != RoomRole.Locked && role != RoomRole.Trap)
                        throw new FormatException($"role '{row["role"]}'에는 볼거리가 없습니다(rest, shrine, cart, treasure, hidden, locked, trap).");
                    var biomes = row["biomes"].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(b => SheetValues.ParseEnum<Biome>(b.Trim(), "biomes")).ToList();
                    int weight = row["weight"].Length == 0 ? 10 : SheetValues.ParseNumber(row["weight"], "weight");
                    if (weight <= 0) throw new FormatException("weight는 1 이상입니다.");
                    var rewards = row["reward"].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(part => Reward(part.Trim(), loot)).ToList();
                    if (rewards.Count == 0) throw new FormatException("reward가 비었습니다.");
                    int level = row["reward_level"].Length == 0 ? 0 : SheetValues.ParseSigned(row["reward_level"], "reward_level");
                    bool perPlayer = SheetValues.ParseBool(row["per_player"], "per_player");
                    var again = row["again"].Length == 0 ? PoiAgain.Once : SheetValues.ParseEnum<PoiAgain>(row["again"], "again");
                    var signal = row["signal"].Length == 0 ? PoiSignal.None : SheetValues.ParseEnum<PoiSignal>(row["signal"], "signal");
                    int range = row["signal_range"].Length == 0 ? 0 : SheetValues.ParseNumber(row["signal_range"], "signal_range");
                    if (signal != PoiSignal.None && range <= 0) throw new FormatException("signal이 있으면 signal_range가 1 이상입니다.");
                    var buff = ModifierSyntax.ParseList(row["buff"]).Select(m => new Modifier(m.Stat, m.Kind, m.Min, m.Tags)).ToList();
                    bool blesses = rewards.Any(r => r.Kind == PoiRewardKind.Bless);
                    if (blesses != (buff.Count > 0)) throw new FormatException(blesses ? "bless에는 buff가 있어야 합니다." : "buff는 bless와 함께 씁니다.");
                    TileType? tile = row["tile"].Length == 0 ? (TileType?)null : SheetValues.ParseEnum<TileType>(row["tile"], "tile");
                    var kind = PoiDefinition.KindOf(role);
                    if (kind == PoiKind.Chest && tile is not (TileType.WoodChest or TileType.IronChest or TileType.GoldChest))
                        throw new FormatException("상자는 tile이 WoodChest, IronChest, GoldChest 가운데 하나입니다.");
                    if (kind != PoiKind.Chest && tile != null) throw new FormatException("tile은 상자에만 씁니다.");
                    if (kind == PoiKind.Camp && again != PoiAgain.Always) throw new FormatException("야영지는 again이 always입니다.");
                    if (kind != PoiKind.Camp && again == PoiAgain.Always) throw new FormatException("always는 야영지에만 씁니다.");
                    pois.Add(new PoiDefinition(id, name, role, biomes, weight, rewards, level, perPlayer, again, signal, range, buff, tile));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            // Every kind of room with things to find has a row for every biome.
            foreach (var role in new[] { RoomRole.Rest, RoomRole.Shrine, RoomRole.Cart, RoomRole.Treasure, RoomRole.Hidden, RoomRole.Locked, RoomRole.Trap })
                foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                    if (pois.Count > 0 && !pois.Any(p => p.Role == role && p.For(biome)))
                        errors.Add($"{File}: {role.ToString().ToLowerInvariant()} 방의 볼거리가 {biome.ToString().ToLowerInvariant()}에 없습니다.");
            return new PoiData(pois, errors);
        }

        // "kind:a-b", "items:n:rarity", "material:id:a-b".
        static PoiReward Reward(string text, LootData loot)
        {
            var parts = text.Split(':').Select(p => p.Trim()).ToArray();
            var kind = SheetValues.ParseEnum<PoiRewardKind>(parts[0], "reward");
            string material = null;
            int at = 1;
            if (kind == PoiRewardKind.Material)
            {
                if (parts.Length < 3) throw new FormatException($"'{text}': material:재료id:수처럼 적습니다.");
                material = parts[1];
                if (loot != null && loot.Materials.All(m => m.Id != material)) throw new FormatException($"'{text}': 없는 재료 '{material}'.");
                at = 2;
            }
            if (parts.Length <= at) throw new FormatException($"'{text}': 수가 없습니다.");
            var bounds = parts[at].Split('-');
            int min = SheetValues.ParseNumber(bounds[0], $"'{text}'의 수");
            int max = bounds.Length > 1 ? SheetValues.ParseNumber(bounds[1], $"'{text}'의 수") : min;
            if (bounds.Length > 2 || max < min) throw new FormatException($"'{text}': 범위는 3-5처럼 적습니다.");
            var rarity = Rarity.Common;
            if (kind == PoiRewardKind.Items && parts.Length > at + 1) rarity = SheetValues.ParseEnum<Rarity>(parts[at + 1], "희귀도");
            else if (parts.Length > at + 1) throw new FormatException($"'{text}': 뒤에 남는 것이 있습니다.");
            if ((kind == PoiRewardKind.Heal || kind == PoiRewardKind.Rest) && max > 100) throw new FormatException($"'{text}': 회복은 100% 이하입니다.");
            return new PoiReward(kind, min, max, rarity, material);
        }
    }
}
