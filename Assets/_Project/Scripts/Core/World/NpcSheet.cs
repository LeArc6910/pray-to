using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Someone who lives in the world: where they stand, what they think of gifts, and what they say. See Docs/Npcs.md.
    public sealed class NpcDefinition
    {
        internal NpcDefinition(string id, string name, TileType tile, IReadOnlyCollection<object> loves, IReadOnlyCollection<object> likes,
            IReadOnlyCollection<object> dislikes, IReadOnlyList<string> lines)
        {
            Id = id;
            Name = name;
            Tile = tile;
            Loves = loves;
            Likes = likes;
            Dislikes = dislikes;
            Lines = lines;
        }

        public string Id { get; }
        public string Name { get; }
        // The tile they stand at (a merchant's stall); players beside it can trade and talk.
        public TileType Tile { get; }
        // Materials and consumables.
        public IReadOnlyCollection<object> Loves { get; }
        public IReadOnlyCollection<object> Likes { get; }
        public IReadOnlyCollection<object> Dislikes { get; }
        public IReadOnlyList<string> Lines { get; }

        // What they say today to someone with this many hearts: one of their lines, a different one each day.
        public string LineFor(int day, int hearts) => Lines.Count == 0 ? "" : Lines[(Math.Max(0, day) + hearts) % Lines.Count];
    }

    // One row of the shop: a material or consumable the merchant sells (Buy coins), buys (Sell coins), or both.
    // A daily one is on sale only some days; one with hearts only to players that friendly.
    public sealed class ShopEntry
    {
        internal ShopEntry(MaterialDefinition material, ConsumableDefinition consumable, int buy, int sell, bool daily, int hearts)
        {
            Material = material;
            Consumable = consumable;
            Buy = buy;
            Sell = sell;
            Daily = daily;
            Hearts = hearts;
        }

        public MaterialDefinition Material { get; }
        public ConsumableDefinition Consumable { get; }
        public object Thing => (object)Material ?? Consumable;
        public string Name => Material?.Name ?? Consumable.Name;
        // 0 when not sold, or not bought.
        public int Buy { get; }
        public int Sell { get; }
        public bool Daily { get; }
        public int Hearts { get; }
    }

    public sealed class NpcData
    {
        internal NpcData(IReadOnlyList<NpcDefinition> npcs, IReadOnlyList<ShopEntry> shop, IReadOnlyList<string> errors)
        {
            Npcs = npcs;
            Shop = shop;
            Errors = errors;
        }

        public IReadOnlyList<NpcDefinition> Npcs { get; }
        public IReadOnlyList<ShopEntry> Shop { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        // The merchant (the first person at a stall), or null.
        public NpcDefinition Merchant => Npcs.FirstOrDefault(n => n.Tile == TileType.MerchantStall);
    }

    // npcs.csv and shop.csv (Docs/Npcs.md).
    public static class NpcSheet
    {
        public const string NpcsFile = "npcs.csv";
        public const string ShopFile = "shop.csv";

        // Without the sheets there is no one (an asset made before they existed).
        public static NpcData Parse(string npcsCsv, string shopCsv, LootData loot)
        {
            var errors = new List<string>();
            var npcs = ParseNpcs(npcsCsv, loot, errors);
            var shop = ParseShop(shopCsv, loot, errors);
            return new NpcData(npcs, shop, errors);
        }

        static List<NpcDefinition> ParseNpcs(string csv, LootData loot, List<string> errors)
        {
            var npcs = new List<NpcDefinition>();
            if (string.IsNullOrWhiteSpace(csv)) return npcs;
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "name", "tile", "loves", "likes", "dislikes", "lines");
            if (missing.Count > 0)
            {
                errors.Add($"{NpcsFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return npcs;
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (npcs.Any(n => n.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    string name = row["name"];
                    if (name.Length == 0) throw new FormatException("'name' 칸이 비어 있습니다.");
                    var tile = SheetValues.ParseEnum<TileType>(row["tile"], "칸 종류");
                    var lines = row["lines"].Split('|').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                    npcs.Add(new NpcDefinition(id, name, tile, Things(row["loves"], loot), Things(row["likes"], loot), Things(row["dislikes"], loot), lines));
                }
                catch (FormatException e)
                {
                    errors.Add($"{NpcsFile} {row.Line}행: {e.Message}");
                }
            }
            return npcs;
        }

        static List<ShopEntry> ParseShop(string csv, LootData loot, List<string> errors)
        {
            var shop = new List<ShopEntry>();
            if (string.IsNullOrWhiteSpace(csv)) return shop;
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("item", "buy", "sell");
            if (missing.Count > 0)
            {
                errors.Add($"{ShopFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return shop;
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    var thing = Thing(row["item"], loot);
                    if (shop.Any(s => s.Thing == thing)) throw new FormatException($"'{row["item"]}'가 이미 있습니다.");
                    int buy = row["buy"].Length == 0 ? 0 : SheetValues.ParseNumber(row["buy"], "buy");
                    int sell = row["sell"].Length == 0 ? 0 : SheetValues.ParseNumber(row["sell"], "sell");
                    if (buy <= 0 && sell <= 0) throw new FormatException("buy와 sell 가운데 하나는 0보다 커야 합니다.");
                    if (buy > 0 && sell >= buy) throw new FormatException("sell은 buy보다 작아야 합니다(사서 되팔아 돈을 벌 수 없게).");
                    bool daily = row["daily"] switch
                    {
                        "" => false,
                        "yes" => true,
                        _ => throw new FormatException($"daily는 비우거나 yes입니다: '{row["daily"]}'"),
                    };
                    int hearts = row["hearts"].Length == 0 ? 0 : SheetValues.ParseNumber(row["hearts"], "hearts");
                    if (hearts > World.MaxHearts) throw new FormatException($"hearts는 {World.MaxHearts} 이하입니다.");
                    shop.Add(new ShopEntry(thing as MaterialDefinition, thing as ConsumableDefinition, buy, sell, daily, hearts));
                }
                catch (FormatException e)
                {
                    errors.Add($"{ShopFile} {row.Line}행: {e.Message}");
                }
            }
            return shop;
        }

        // "a|b|c" of material and consumable ids.
        static List<object> Things(string text, LootData loot) =>
            text.Split('|').Select(part => part.Trim()).Where(part => part.Length > 0).Select(part => Thing(part, loot)).ToList();

        static object Thing(string id, LootData loot) =>
            (object)loot.Materials.FirstOrDefault(m => m.Id == id) ?? loot.Consumables.FirstOrDefault(c => c.Id == id)
            ?? throw new FormatException($"'{id}'라는 재료나 소모품이 없습니다.");
    }
}
