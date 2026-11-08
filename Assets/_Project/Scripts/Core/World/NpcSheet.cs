using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Someone who lives in the world: where they stand, what they think of gifts, and what they say. See Docs/Npcs.md.
    public sealed class NpcDefinition
    {
        internal NpcDefinition(string id, string name, TileType tile, IReadOnlyCollection<object> loves, IReadOnlyCollection<object> likes,
            IReadOnlyCollection<object> dislikes, IReadOnlyList<string> lines, string role = "", string color = "#9A5AC8", bool buysGear = false,
            IReadOnlyList<(int Hearts, object Thing, int Count)> gifts = null, IReadOnlyDictionary<int, string> stories = null)
        {
            Role = role.Length > 0 ? role : name;
            Color = color;
            BuysGear = buysGear;
            Gifts = gifts ?? Array.Empty<(int, object, int)>();
            Stories = stories ?? new Dictionary<int, string>();
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
        // What they are (the bag's tab says it), the colour they wear, and whether they buy gear.
        public string Role { get; }
        public string Color { get; }
        public bool BuysGear { get; }
        // What they give once a player reaches so many hearts, and what they tell then.
        public IReadOnlyList<(int Hearts, object Thing, int Count)> Gifts { get; }
        public IReadOnlyDictionary<int, string> Stories { get; }

        // What they say today to someone with this many hearts: one of their lines, a different one each day.
        public string LineFor(int day, int hearts) => Lines.Count == 0 ? "" : Lines[(Math.Max(0, day) + hearts) % Lines.Count];
    }

    // One row of a shop: a material or consumable a person sells (Buy coins), buys (Sell coins), or both. A daily one
    // is on sale only some days; one with hearts only to players that friendly.
    public sealed class ShopEntry
    {
        internal ShopEntry(MaterialDefinition material, ConsumableDefinition consumable, int buy, int sell, bool daily, int hearts, NpcDefinition npc = null)
        {
            Npc = npc;
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
        // Whose shop it is.
        public NpcDefinition Npc { get; }
    }

    // Something a person asks for (requests.csv): bring them these, and they give those and think better of you.
    // A row with a step is part of their story (Docs/Npcs.md, 이야기): asked once each, in step order, never as the
    // day's request; its rewards may name a piece of gear (rolled rare at its level).
    public sealed class RequestDefinition
    {
        internal RequestDefinition(string id, NpcDefinition npc, string text, IReadOnlyList<(object Thing, int Count)> need,
            IReadOnlyList<(object Thing, int Count)> rewards, int hearts, int points, int step = 0, string title = "")
        {
            Step = step;
            Title = title ?? "";
            Id = id;
            Npc = npc;
            Text = text;
            Need = need;
            Rewards = rewards;
            Hearts = hearts;
            Points = points;
        }

        public string Id { get; }
        public NpcDefinition Npc { get; }
        public string Text { get; }
        // Materials and consumables, and how many.
        public IReadOnlyList<(object Thing, int Count)> Need { get; }
        public IReadOnlyList<(object Thing, int Count)> Rewards { get; }
        // Asked only of players this friendly.
        public int Hearts { get; }
        public int Points { get; }
        // Its place in the person's story (1 the first), or 0 for a daily request; and the story step's name.
        public int Step { get; }
        public string Title { get; }
        public bool IsStory => Step > 0;

        public static string NameOf(object thing) => thing switch
        {
            MaterialDefinition m => m.Name,
            ItemBase b => b.Name,
            _ => ((ConsumableDefinition)thing).Name,
        };

        public string NeedText => string.Join(" · ", Need.Select(n => $"{NameOf(n.Thing)} {n.Count}"));
        public string RewardText => string.Join(" · ", Rewards.Select(r => $"{NameOf(r.Thing)} {r.Count}"));
    }

    public sealed class NpcData
    {
        internal NpcData(IReadOnlyList<NpcDefinition> npcs, IReadOnlyList<ShopEntry> shop, IReadOnlyList<string> errors,
            IReadOnlyList<RequestDefinition> requests = null)
        {
            Requests = requests ?? Array.Empty<RequestDefinition>();
            Npcs = npcs;
            Shop = shop;
            Errors = errors;
        }

        public IReadOnlyList<NpcDefinition> Npcs { get; }
        public IReadOnlyList<ShopEntry> Shop { get; }
        public IReadOnlyList<RequestDefinition> Requests { get; }
        public IReadOnlyList<string> Errors { get; }

        // A person's story steps, first to last (Docs/Npcs.md, 이야기).
        public IReadOnlyList<RequestDefinition> StoryOf(NpcDefinition npc) => Requests.Where(r => r.Npc == npc && r.IsStory).OrderBy(r => r.Step).ToList();
        public bool Success => Errors.Count == 0;

        // The merchant (the first person at a stall), or null.
        public NpcDefinition Merchant => Npcs.FirstOrDefault(n => n.Tile == TileType.MerchantStall);

        public NpcDefinition Find(string id) => Npcs.FirstOrDefault(n => n.Id == id);

        public int IndexOf(NpcDefinition npc) => npc == null ? -1 : Npcs.ToList().IndexOf(npc);
    }

    // npcs.csv and shop.csv (Docs/Npcs.md).
    public static class NpcSheet
    {
        public const string NpcsFile = "npcs.csv";
        public const string ShopFile = "shop.csv";
        public const string RequestsFile = "requests.csv";

        // Without the sheets there is no one (an asset made before they existed).
        public static NpcData Parse(string npcsCsv, string shopCsv, LootData loot, string requestsCsv = "")
        {
            var errors = new List<string>();
            var npcs = ParseNpcs(npcsCsv, loot, errors);
            var shop = ParseShop(shopCsv, loot, errors, npcs);
            var requests = ParseRequests(requestsCsv, loot, errors, npcs);
            return new NpcData(npcs, shop, errors, requests);
        }

        static List<RequestDefinition> ParseRequests(string csv, LootData loot, List<string> errors, List<NpcDefinition> npcs)
        {
            var requests = new List<RequestDefinition>();
            if (string.IsNullOrWhiteSpace(csv)) return requests;
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "npc", "text", "need", "reward");
            if (missing.Count > 0)
            {
                errors.Add($"{RequestsFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return requests;
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (requests.Any(r => r.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    var npc = npcs.FirstOrDefault(n => n.Id == row["npc"]) ?? throw new FormatException($"'{row["npc"]}'라는 사람이 {NpcsFile}에 없습니다.");
                    if (row["text"].Length == 0) throw new FormatException("'text' 칸이 비어 있습니다.");
                    int step = row["step"].Length == 0 ? 0 : SheetValues.ParseNumber(row["step"], "step");
                    if (step < 0) throw new FormatException("step은 0 이상입니다.");
                    if (step > 0 && requests.Any(r => r.Npc == npc && r.Step == step)) throw new FormatException($"'{npc.Id}'의 이야기 {step}단계가 이미 있습니다.");
                    if (step > 0 && row["title"].Length == 0) throw new FormatException("이야기(step)에는 title이 있어야 합니다.");
                    var need = Counts(row["need"], loot);
                    // Only a story step may give gear.
                    var rewards = Counts(row["reward"], loot, items: step > 0);
                    if (need.Count == 0 || rewards.Count == 0) throw new FormatException("need와 reward가 비어 있으면 안 됩니다.");
                    int hearts = row["hearts"].Length == 0 ? 0 : SheetValues.ParseNumber(row["hearts"], "hearts");
                    if (hearts > World.MaxHearts) throw new FormatException($"hearts는 {World.MaxHearts} 이하입니다.");
                    int points = row["points"].Length == 0 ? 30 : SheetValues.ParseNumber(row["points"], "points");
                    requests.Add(new RequestDefinition(id, npc, row["text"], need, rewards, hearts, points, step, row["title"]));
                }
                catch (FormatException e)
                {
                    errors.Add($"{RequestsFile} {row.Line}행: {e.Message}");
                }
            }
            return requests;
        }

        // "copper_ore:20|coin:40".
        static List<(object, int)> Counts(string text, LootData loot, bool items = false) => text.Split('|').Select(part => part.Trim()).Where(part => part.Length > 0).Select(part =>
        {
            string[] bits = part.Split(':');
            if (bits.Length != 2) throw new FormatException($"'{part}'는 <id>:<개수>로 적습니다.");
            int count = SheetValues.ParseNumber(bits[1], "개수");
            if (count < 1) throw new FormatException("개수는 1 이상이어야 합니다.");
            var itemBase = items ? loot.Bases.FirstOrDefault(b => b.Id == bits[0]) : null;
            return (itemBase ?? Thing(bits[0], loot), count);
        }).ToList();

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
                    var gifts = row["gifts"].Split('|').Select(part => part.Trim()).Where(part => part.Length > 0).Select(part =>
                    {
                        string[] bits = part.Split(':');
                        if (bits.Length != 3) throw new FormatException($"gifts '{part}'는 <호감>:<id>:<개수>로 적습니다.");
                        int hearts = SheetValues.ParseNumber(bits[0], "호감");
                        if (hearts < 1 || hearts > World.MaxHearts) throw new FormatException($"gifts의 호감은 1~{World.MaxHearts}입니다.");
                        return (hearts, Thing(bits[1], loot), SheetValues.ParseNumber(bits[2], "개수"));
                    }).ToList();
                    var stories = new Dictionary<int, string>();
                    foreach (string part in row["stories"].Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
                    {
                        int colon = part.IndexOf(':');
                        if (colon < 1) throw new FormatException($"stories '{part}'는 <호감>:<이야기>로 적습니다.");
                        stories[SheetValues.ParseNumber(part.Substring(0, colon), "호감")] = part.Substring(colon + 1);
                    }
                    bool buysGear = row["gear"] switch
                    {
                        "" => false,
                        "yes" => true,
                        "no" => false,
                        _ => throw new FormatException($"gear는 yes나 no입니다: '{row["gear"]}'"),
                    };
                    string color = row["color"].Length > 0 ? row["color"] : "#9A5AC8";
                    npcs.Add(new NpcDefinition(id, name, tile, Things(row["loves"], loot), Things(row["likes"], loot), Things(row["dislikes"], loot), lines,
                        row["role"], color, buysGear, gifts, stories));
                }
                catch (FormatException e)
                {
                    errors.Add($"{NpcsFile} {row.Line}행: {e.Message}");
                }
            }
            return npcs;
        }

        static List<ShopEntry> ParseShop(string csv, LootData loot, List<string> errors, List<NpcDefinition> npcs)
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
                    // Blank: the merchant's.
                    var npc = row["npc"].Length == 0 ? npcs.FirstOrDefault(n => n.Tile == TileType.MerchantStall) ?? npcs.FirstOrDefault()
                        : npcs.FirstOrDefault(n => n.Id == row["npc"]) ?? throw new FormatException($"'{row["npc"]}'라는 사람이 {NpcsFile}에 없습니다.");
                    if (shop.Any(s => s.Thing == thing && s.Npc == npc)) throw new FormatException($"'{row["item"]}'가 그 사람의 상점에 이미 있습니다.");
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
                    shop.Add(new ShopEntry(thing as MaterialDefinition, thing as ConsumableDefinition, buy, sell, daily, hearts, npc));
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
