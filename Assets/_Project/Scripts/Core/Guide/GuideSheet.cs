using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // One thing a goal can ask for. Docs/Guide.md lists the kinds.
    public sealed class GoalCondition
    {
        internal GoalCondition(string kind, string id, int count, TileType tile)
        {
            Kind = kind;
            Id = id;
            Count = count;
            Tile = tile;
        }

        // "have", "place", "plant" or "reach".
        public string Kind { get; }
        // What to have: a material, consumable or item base id.
        public string Id { get; }
        // How many to have, or how many tiles from the spawn to reach.
        public int Count { get; }
        // What to place.
        public TileType Tile { get; }
    }

    // A step of the guide for new players: what to do, a hint at how, and when it counts as done (any one of its
    // conditions).
    public sealed class Goal
    {
        internal Goal(string id, string text, string hint, IReadOnlyList<GoalCondition> conditions)
        {
            Id = id;
            Text = text;
            Hint = hint;
            Conditions = conditions;
        }

        public string Id { get; }
        public string Text { get; }
        public string Hint { get; }
        public IReadOnlyList<GoalCondition> Conditions { get; }
    }

    public sealed class GuideData
    {
        internal GuideData(IReadOnlyList<Goal> goals, IReadOnlyList<string> errors)
        {
            Goals = goals;
            Errors = errors;
        }

        public IReadOnlyList<Goal> Goals { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;
    }

    // What the guide checks goals against, read from what a client knows about its own player and the world.
    public sealed class GuideFacts
    {
        public GuideFacts(Func<string, int> countOf, Func<TileType, bool> hasTile, bool hasCrop, float distanceFromSpawn)
        {
            CountOf = countOf;
            HasTile = hasTile;
            HasCrop = hasCrop;
            DistanceFromSpawn = distanceFromSpawn;
        }

        // How many of a material, consumable or item base (carried or worn) the player has.
        public Func<string, int> CountOf { get; }
        // Whether a tile of the type stands anywhere in the world.
        public Func<TileType, bool> HasTile { get; }
        public bool HasCrop { get; }
        public float DistanceFromSpawn { get; }

        // The facts for a client's player: the bag and worn gear, the map, the crops and where the player stands.
        public static GuideFacts Of(WorldClient client)
        {
            int CountOf(string id)
            {
                var material = client.Bag.Materials.Keys.FirstOrDefault(m => m.Id == id);
                if (material != null) return client.Bag.CountOf(material);
                var consumable = client.Bag.Consumables.Keys.FirstOrDefault(c => c.Id == id);
                if (consumable != null) return client.Bag.CountOf(consumable);
                return client.Bag.Items.Concat(client.Equipment.Items).Count(item => item.Base.Id == id);
            }

            bool HasTile(TileType type)
            {
                var map = client.Map;
                if (map == null) return false;
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (map[x, y] == type) return true;
                    }
                }
                return false;
            }

            return new GuideFacts(CountOf, HasTile, client.Crops.Count > 0, Vec2.Distance(client.Position, client.Spawn));
        }
    }

    // How far a player has come through the guide. Goals are done in order; every goal already met is passed at
    // once, so a player who has done things before (a loaded character) skips ahead. Once passed, a goal stays done
    // even if what it asked for is used up.
    public sealed class GuideProgress
    {
        readonly GuideData _data;

        public GuideProgress(GuideData data, int done)
        {
            _data = data;
            Done = Math.Max(0, Math.Min(done, data.Goals.Count));
        }

        public int Done { get; private set; }

        // Null once every goal is done.
        public Goal Current => Done < _data.Goals.Count ? _data.Goals[Done] : null;

        public int Total => _data.Goals.Count;

        // Passes every goal met now, in order, and returns the ones just passed.
        public List<Goal> Update(GuideFacts facts)
        {
            var passed = new List<Goal>();
            while (Current != null && Current.Conditions.Any(condition => IsMet(condition, facts)))
            {
                passed.Add(Current);
                Done++;
            }
            return passed;
        }

        public static bool IsMet(GoalCondition condition, GuideFacts facts) => condition.Kind switch
        {
            "have" => facts.CountOf(condition.Id) >= condition.Count,
            "place" => facts.HasTile(condition.Tile),
            "plant" => facts.HasCrop,
            "reach" => facts.DistanceFromSpawn >= condition.Count,
            _ => false,
        };
    }

    // Reads Assets/_Project/Data/Guide/goals.csv (columns in Docs/Guide.md).
    public static class GuideSheet
    {
        public const string File = "goals.csv";

        public static GuideData Parse(string csv, LootData loot)
        {
            var errors = new List<string>();
            var goals = new List<Goal>();
            if (string.IsNullOrWhiteSpace(csv)) return new GuideData(goals, errors);
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "text", "hint", "done");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new GuideData(goals, errors);
            }

            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (goals.Any(g => g.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    string text = row["text"];
                    if (text.Length == 0) throw new FormatException("'text' 칸이 비어 있습니다.");
                    var conditions = row["done"].Split('|').Select(part => Condition(part.Trim(), loot)).ToList();
                    goals.Add(new Goal(id, text, row["hint"], conditions));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return new GuideData(goals, errors);
        }

        // "have:stone:10", "have:copper_helmet", "place:Workbench", "plant" or "reach:60".
        static GoalCondition Condition(string text, LootData loot)
        {
            string[] parts = text.Split(':');
            switch (parts[0])
            {
                case "have":
                    if (parts.Length < 2 || parts.Length > 3) throw new FormatException($"'{text}'는 have:<id> 또는 have:<id>:<개수>로 적습니다.");
                    string id = parts[1];
                    bool known = loot.Materials.Any(m => m.Id == id) || loot.Consumables.Any(c => c.Id == id) || loot.Bases.Any(b => b.Id == id);
                    if (!known) throw new FormatException($"'{id}'는 재료, 소모품, 아이템 어디에도 없습니다.");
                    int count = parts.Length == 3 ? SheetValues.ParseNumber(parts[2], "개수") : 1;
                    if (count < 1) throw new FormatException("개수는 1 이상이어야 합니다.");
                    return new GoalCondition("have", id, count, default);
                case "place":
                    if (parts.Length != 2) throw new FormatException($"'{text}'는 place:<칸 종류>로 적습니다.");
                    return new GoalCondition("place", null, 0, SheetValues.ParseEnum<TileType>(parts[1], "칸 종류"));
                case "plant":
                    if (parts.Length != 1) throw new FormatException("plant에는 덧붙이는 값이 없습니다.");
                    return new GoalCondition("plant", null, 0, default);
                case "reach":
                    if (parts.Length != 2) throw new FormatException($"'{text}'는 reach:<타일 수>로 적습니다.");
                    int tiles = SheetValues.ParseNumber(parts[1], "타일 수");
                    if (tiles < 1) throw new FormatException("타일 수는 1 이상이어야 합니다.");
                    return new GoalCondition("reach", null, tiles, default);
                default:
                    throw new FormatException($"알 수 없는 조건 '{text}' (have, place, plant, reach 중 하나)");
            }
        }
    }
}
