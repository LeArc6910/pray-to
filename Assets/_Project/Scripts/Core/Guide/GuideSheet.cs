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

        // "have", "place", "plant", "reach", "level", "passive", "life", "rift", "friend", "skills" or "rank".
        public string Kind { get; }
        // What to have: a material, consumable or item base id.
        public string Id { get; }
        // How many to have, how many tiles from the spawn to reach, the character level, how many passive points, the
        // life skill's level (Id is the skill), the rift tier the world has opened, the hearts with anyone, the skill
        // points spent or the rank of the best learned skill.
        public int Count { get; }
        // What to place.
        public TileType Tile { get; }
    }

    // A step of the guide for new players: what to do, a hint at how, and when it counts as done (any one of its
    // conditions).
    public sealed class Goal
    {
        internal Goal(string id, string text, string hint, IReadOnlyList<GoalCondition> conditions, IReadOnlyList<GoalReward> rewards = null)
        {
            Id = id;
            Text = text;
            Hint = hint;
            Conditions = conditions;
            Rewards = rewards ?? Array.Empty<GoalReward>();
        }

        public string Id { get; }
        public string Text { get; }
        public string Hint { get; }
        public IReadOnlyList<GoalCondition> Conditions { get; }
        // What the host gives the first time the goal is done (goals.csv reward), maybe nothing.
        public IReadOnlyList<GoalReward> Rewards { get; }

        // "구리 광석 6 · 회복 물약 2 · 경험치 50", or empty.
        public string RewardText => string.Join(" · ", Rewards.Select(r => r.Text));
    }

    // One reward: some of a material or consumable, or experience.
    public sealed class GoalReward
    {
        internal GoalReward(MaterialDefinition material, ConsumableDefinition consumable, int count)
        {
            Material = material;
            Consumable = consumable;
            Count = count;
        }

        public MaterialDefinition Material { get; }
        public ConsumableDefinition Consumable { get; }
        // How many, or how much experience when there is neither.
        public int Count { get; }
        public bool IsExperience => Material == null && Consumable == null;
        public string Text => IsExperience ? $"경험치 {Count}" : $"{Material?.Name ?? Consumable.Name} {Count}";
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
        public GuideFacts(Func<string, int> countOf, Func<TileType, bool> hasTile, bool hasCrop, float distanceFromSpawn, int level = 1,
            int passives = 0, Func<LifeSkill, int> lifeLevel = null, int riftTier = 1, int hearts = 0, int skillsSpent = 0, int topRank = 0)
        {
            SkillsSpent = skillsSpent;
            TopRank = topRank;
            Passives = passives;
            LifeLevel = lifeLevel ?? (_ => 1);
            RiftTier = riftTier;
            Hearts = hearts;
            Level = level;
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
        public int Level { get; }
        // Passive tree points taken, each life skill's level, the highest rift tier the world may open, and the most
        // hearts the player has with anyone.
        public int Passives { get; }
        public Func<LifeSkill, int> LifeLevel { get; }
        public int RiftTier { get; }
        public int Hearts { get; }
        // Skill points spent, and the highest rank of any learned skill (Docs/Classes.md, 스킬 포인트와 단계).
        public int SkillsSpent { get; }
        public int TopRank { get; }

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

            int hearts = client.Friendships.Values.Select(f => World.HeartsOf(f.Points)).DefaultIfEmpty(0).Max();
            return new GuideFacts(CountOf, HasTile, client.Crops.Count > 0, Vec2.Distance(client.Position, client.Spawn), client.Level,
                client.Passives.Count, client.LifeLevel, Math.Max(1, client.Rift.Unlocked), hearts,
                SkillRules.SpentPoints(client.SkillRanks), client.SkillRanks.Values.DefaultIfEmpty(0).Max());
        }

        // The same facts as the host sees them, to check a claimed reward.
        public static GuideFacts Of(World world, Player player)
        {
            int CountOf(string id)
            {
                var bag = player.Inventory;
                var material = bag.Materials.Keys.FirstOrDefault(m => m.Id == id);
                if (material != null) return bag.CountOf(material);
                var consumable = bag.Consumables.Keys.FirstOrDefault(c => c.Id == id);
                if (consumable != null) return bag.CountOf(consumable);
                return bag.Items.Concat(player.Equipment.Items).Count(item => item.Base.Id == id);
            }

            bool HasTile(TileType type)
            {
                var map = world.Map;
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (map[x, y] == type) return true;
                    }
                }
                return false;
            }

            int hearts = player.Friendships.Values.Select(f => f.Hearts).DefaultIfEmpty(0).Max();
            return new GuideFacts(CountOf, HasTile, world.Crops.Count > 0, Vec2.Distance(player.Position, world.Spawn), player.Level,
                player.Passives.Count, skill => World.LifeLevel(player, skill), world.RiftTier, hearts,
                SkillRules.SpentPoints(player.SkillRanks), player.SkillRanks.Values.DefaultIfEmpty(0).Max());
        }
    }

    // How far a player has come through the guide: the goals passed, by id. The current goal is the first one in the
    // sheet not yet passed, so goals added to the sheet later come up in their place even for someone further on.
    // Every goal met now is passed at once, so a player who has done things before (a loaded character) skips ahead.
    // Once passed, a goal stays done even if what it asked for is used up.
    public sealed class GuideProgress
    {
        readonly GuideData _data;
        readonly HashSet<string> _passed;

        public GuideProgress(GuideData data, IEnumerable<string> passed)
        {
            _data = data;
            _passed = new HashSet<string>(passed ?? Enumerable.Empty<string>());
        }

        // The first `done` goals passed.
        public GuideProgress(GuideData data, int done) : this(data, data.Goals.Take(Math.Max(0, done)).Select(g => g.Id))
        {
        }

        // Goals added after progress was saved as a count (2026-11-03): a count from then is of the goals without them.
        public static readonly IReadOnlyCollection<string> AddedAfterCounts = new[]
        {
            "life_mining", "merchant", "passive", "anvil", "assembler", "power_pole", "keg", "coop", "crab_pot", "friend", "rift_altar", "rift",
            "free_power", "arc_furnace", "skill", "rank3",
        };

        // The goals a count saved before AddedAfterCounts stood for.
        public static IEnumerable<string> FromCount(GuideData data, int done) =>
            data.Goals.Where(g => !AddedAfterCounts.Contains(g.Id)).Take(Math.Max(0, done)).Select(g => g.Id);

        public IReadOnlyCollection<string> Passed => _passed;

        public int Done => _data.Goals.Count(g => _passed.Contains(g.Id));

        // Null once every goal is done.
        public Goal Current => _data.Goals.FirstOrDefault(g => !_passed.Contains(g.Id));

        public int Total => _data.Goals.Count;

        // Passes every goal met now, in order, and returns the ones just passed.
        public List<Goal> Update(GuideFacts facts)
        {
            var passed = new List<Goal>();
            while (Current != null && Current.Conditions.Any(condition => IsMet(condition, facts)))
            {
                passed.Add(Current);
                _passed.Add(Current.Id);
            }
            return passed;
        }

        // How far along the goal is, for the screen's goal list: "3/10" for its first condition that counts toward
        // more than one, or null (a thing to place, a crop to plant).
        public static string Progress(Goal goal, GuideFacts facts)
        {
            foreach (var condition in goal.Conditions)
            {
                int have = condition.Kind switch
                {
                    "have" => facts.CountOf(condition.Id),
                    "reach" => (int)facts.DistanceFromSpawn,
                    "level" => facts.Level,
                    "passive" => facts.Passives,
                    "life" => Enum.TryParse<LifeSkill>(condition.Id, true, out var skill) ? facts.LifeLevel(skill) : -1,
                    "rift" => facts.RiftTier,
                    "friend" => facts.Hearts,
                    "skills" => facts.SkillsSpent,
                    "rank" => facts.TopRank,
                    _ => -1,
                };
                if (have >= 0 && condition.Count > 1) return $"{Math.Min(have, condition.Count)}/{condition.Count}";
            }
            return null;
        }

        public static bool IsMet(GoalCondition condition, GuideFacts facts) => condition.Kind switch
        {
            "have" => facts.CountOf(condition.Id) >= condition.Count,
            "place" => facts.HasTile(condition.Tile),
            "plant" => facts.HasCrop,
            "reach" => facts.DistanceFromSpawn >= condition.Count,
            "level" => facts.Level >= condition.Count,
            "passive" => facts.Passives >= condition.Count,
            "life" => Enum.TryParse<LifeSkill>(condition.Id, true, out var skill) && facts.LifeLevel(skill) >= condition.Count,
            "rift" => facts.RiftTier >= condition.Count,
            "friend" => facts.Hearts >= condition.Count,
            "skills" => facts.SkillsSpent >= condition.Count,
            "rank" => facts.TopRank >= condition.Count,
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
                    var rewards = row["reward"].Length == 0 ? new List<GoalReward>() : row["reward"].Split('|').Select(part => Reward(part.Trim(), loot)).ToList();
                    goals.Add(new Goal(id, text, row["hint"], conditions, rewards));
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
                case "level":
                    if (parts.Length != 2) throw new FormatException($"'{text}'는 level:<레벨>로 적습니다.");
                    int level = SheetValues.ParseNumber(parts[1], "레벨");
                    if (level < 1 || level > Leveling.MaxLevel) throw new FormatException($"레벨은 1~{Leveling.MaxLevel}이어야 합니다.");
                    return new GoalCondition("level", null, level, default);
                case "passive":
                case "rift":
                case "friend":
                case "skills":
                case "rank":
                    if (parts.Length != 2) throw new FormatException($"'{text}'는 {parts[0]}:<수>로 적습니다.");
                    int number = SheetValues.ParseNumber(parts[1], "수");
                    if (number < 1) throw new FormatException("수는 1 이상이어야 합니다.");
                    return new GoalCondition(parts[0], null, number, default);
                case "life":
                    if (parts.Length != 3 || !Enum.TryParse<LifeSkill>(parts[1], true, out _))
                        throw new FormatException($"'{text}'는 life:<farming|fishing|mining>:<레벨>로 적습니다.");
                    int lifeLevel = SheetValues.ParseNumber(parts[2], "레벨");
                    if (lifeLevel < 1 || lifeLevel > LifeSkills.MaxLevel) throw new FormatException($"생활 기술 레벨은 1~{LifeSkills.MaxLevel}이어야 합니다.");
                    return new GoalCondition("life", parts[1].ToLowerInvariant(), lifeLevel, default);
                default:
                    throw new FormatException($"알 수 없는 조건 '{text}' (have, place, plant, reach, level, passive, life, rift, friend, skills, rank 중 하나)");
            }
        }

        // "copper_ore:6", "healing_potion:2" or "xp:50".
        static GoalReward Reward(string text, LootData loot)
        {
            string[] parts = text.Split(':');
            if (parts.Length != 2) throw new FormatException($"보상 '{text}'는 <id>:<개수> 또는 xp:<경험치>로 적습니다.");
            int count = SheetValues.ParseNumber(parts[1], "개수");
            if (count < 1) throw new FormatException("보상의 개수는 1 이상이어야 합니다.");
            if (parts[0] == "xp") return new GoalReward(null, null, count);
            var material = loot.Materials.FirstOrDefault(m => m.Id == parts[0]);
            var consumable = loot.Consumables.FirstOrDefault(c => c.Id == parts[0]);
            if (material == null && consumable == null) throw new FormatException($"보상 '{parts[0]}'는 재료나 소모품이 아닙니다.");
            return new GoalReward(material, consumable, count);
        }
    }
}
