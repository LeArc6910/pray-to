using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What a record counts (Docs/Guide.md, 도감과 업적): monsters felled by kind, bosses felled, materials and gear
    // ever held. Fish and crops are materials.
    public enum RecordKind : byte
    {
        Kill,
        Boss,
        Found,
        Gear,
    }

    // The groups the bestiary shows and the achievements count in.
    public enum RecordGroup : byte
    {
        Monsters,
        Bosses,
        Fish,
        Crops,
        Gear,
        // Every monster felled, all kinds together.
        Felled,
    }

    // A milestone in the bestiary: so many of a group, with a reward handed out the moment it is reached.
    public sealed class Achievement
    {
        internal Achievement(string id, string name, RecordGroup group, int need, params (string Material, int Count)[] rewards)
        {
            Id = id;
            Name = name;
            Group = group;
            Need = need;
            Rewards = rewards;
        }

        public string Id { get; }
        public string Name { get; }
        public RecordGroup Group { get; }
        // How many; 0 means all there are.
        public int Need { get; }
        public IReadOnlyList<(string Material, int Count)> Rewards { get; }
    }

    // The bestiary and the achievements. What there is to find comes from the sheets (monsters, bosses, fish and
    // crops in materials and crops, gear bases that drop), so a new row joins by itself; the milestones are here.
    public static class Records
    {
        public static readonly IReadOnlyList<Achievement> Achievements = new[]
        {
            new Achievement("hunter_5", "몬스터 5종 잡기", RecordGroup.Monsters, 5, ("coin", 100)),
            new Achievement("hunter_10", "몬스터 10종 잡기", RecordGroup.Monsters, 10, ("coin", 300), ("essence", 10)),
            new Achievement("hunter_all", "모든 몬스터 잡기", RecordGroup.Monsters, 0, ("coin", 1000), ("essence", 30)),
            new Achievement("slayer_100", "몬스터 100마리 잡기", RecordGroup.Felled, 100, ("coin", 200)),
            new Achievement("slayer_1000", "몬스터 1000마리 잡기", RecordGroup.Felled, 1000, ("coin", 1200), ("essence", 20)),
            new Achievement("boss_1", "보스 하나 쓰러뜨리기", RecordGroup.Bosses, 1, ("coin", 200)),
            new Achievement("boss_3", "보스 셋 쓰러뜨리기", RecordGroup.Bosses, 3, ("coin", 500), ("essence", 10)),
            new Achievement("boss_all", "모든 보스 쓰러뜨리기", RecordGroup.Bosses, 0, ("coin", 1500), ("rift_shard", 5)),
            new Achievement("angler_3", "물고기 3종 낚기", RecordGroup.Fish, 3, ("coin", 150), ("bait", 10)),
            new Achievement("angler_all", "모든 물고기 낚기", RecordGroup.Fish, 0, ("coin", 800)),
            new Achievement("farmer_5", "작물 5종 거두기", RecordGroup.Crops, 5, ("coin", 200), ("fertilizer", 5)),
            new Achievement("farmer_all", "모든 작물 거두기", RecordGroup.Crops, 0, ("coin", 800)),
            new Achievement("collector_10", "장비 10종 모으기", RecordGroup.Gear, 10, ("coin", 200)),
            new Achievement("collector_30", "장비 30종 모으기", RecordGroup.Gear, 30, ("coin", 600), ("essence", 15)),
        };

        public static Achievement Find(string id) => Achievements.FirstOrDefault(a => a.Id == id);

        public static string GroupName(RecordGroup group) => group switch
        {
            RecordGroup.Monsters => "몬스터",
            RecordGroup.Bosses => "보스",
            RecordGroup.Fish => "물고기",
            RecordGroup.Crops => "작물",
            RecordGroup.Gear => "장비",
            _ => "잡은 몬스터",
        };

        // Everything there is to find in a group, as (kind, id, name).
        public static IReadOnlyList<(RecordKind Kind, string Id, string Name)> Entries(RecordGroup group, MonsterData monsters, IEnumerable<BossDefinition> bosses,
            LootData loot, TileData tiles)
        {
            switch (group)
            {
                case RecordGroup.Monsters:
                    return (monsters?.Monsters ?? Enumerable.Empty<MonsterDefinition>()).Where(m => m.Weight > 0 || IsSummoned(m, bosses))
                        .Select(m => (RecordKind.Kill, m.Id, m.Name)).ToList();
                case RecordGroup.Bosses:
                    return (bosses ?? Enumerable.Empty<BossDefinition>()).Select(b => (RecordKind.Boss, b.Id, b.Name)).Distinct().ToList();
                case RecordGroup.Fish:
                    return (loot?.Materials ?? Enumerable.Empty<MaterialDefinition>()).Where(m => m.Fish != null && m.Base == null)
                        .Select(m => (RecordKind.Found, m.Id, m.Name)).ToList();
                case RecordGroup.Crops:
                    return (tiles?.Crops ?? Enumerable.Empty<CropDefinition>()).Where(c => c.HarvestMaterial != null)
                        .Select(c => c.HarvestMaterial).Distinct().Select(m => (RecordKind.Found, m.Id, m.Name)).ToList();
                case RecordGroup.Gear:
                    return (loot?.Bases ?? Enumerable.Empty<ItemBase>()).Select(b => (RecordKind.Gear, b.Id, b.Name)).ToList();
                default:
                    return Array.Empty<(RecordKind, string, string)>();
            }
        }

        static bool IsSummoned(MonsterDefinition monster, IEnumerable<BossDefinition> bosses) =>
            bosses != null && bosses.Any(b => b.Attacks.Any(a => a.Summon == monster));

        // How far the records go toward the group: kinds found, or for Felled every monster felled.
        public static int Progress(RecordGroup group, IReadOnlyDictionary<(RecordKind Kind, string Id), int> records,
            IReadOnlyList<(RecordKind Kind, string Id, string Name)> entries)
        {
            if (group == RecordGroup.Felled) return records.Where(r => r.Key.Kind == RecordKind.Kill).Sum(r => r.Value);
            return entries.Count(e => records.TryGetValue((e.Kind, e.Id), out int count) && count > 0);
        }

        // The number an achievement needs: its own, or all there are in its group.
        public static int Needed(Achievement achievement, IReadOnlyList<(RecordKind Kind, string Id, string Name)> entries) =>
            achievement.Need > 0 ? achievement.Need : Math.Max(1, entries.Count);

        public static string RewardText(Achievement achievement, LootData loot) =>
            string.Join(" · ", achievement.Rewards.Select(r => $"{loot?.Materials.FirstOrDefault(m => m.Id == r.Material)?.Name ?? r.Material} {r.Count}"));
    }

    public sealed partial class World
    {
        // The bestiary, for the host's world: whatever the sheets have to find.
        IReadOnlyList<BossDefinition> BossDefinitions => _bosses.Select(b => b.Definition).Concat(_bossRespawns.Select(r => r.Definition)).Distinct().ToList();

        public IReadOnlyList<(RecordKind Kind, string Id, string Name)> RecordEntries(RecordGroup group) =>
            Records.Entries(group, _monsters, BossDefinitions, Loot, Tiles);

        void Record(Player player, RecordKind kind, string id)
        {
            if (player == null || id == null) return;
            var key = (kind, id);
            player.Records.TryGetValue(key, out int count);
            player.Records[key] = count + 1;
            player.RecordsVersion++;
            CheckAchievements(player);
        }

        // Materials and gear the player holds now that they had not held before join the records.
        void RecordHoldings(Player player)
        {
            if (player.RecordedInventory == player.InventoryVersion) return;
            player.RecordedInventory = player.InventoryVersion;
            bool added = false;
            foreach (var pair in player.Inventory.Materials)
            {
                if (pair.Value <= 0 || player.Records.ContainsKey((RecordKind.Found, pair.Key.Kind.Id))) continue;
                player.Records[(RecordKind.Found, pair.Key.Kind.Id)] = 1;
                added = true;
            }
            foreach (var item in player.Inventory.Items.Concat(player.Equipment.Items))
            {
                if (player.Records.ContainsKey((RecordKind.Gear, item.Base.Id))) continue;
                player.Records[(RecordKind.Gear, item.Base.Id)] = 1;
                added = true;
            }
            if (!added) return;
            player.RecordsVersion++;
            CheckAchievements(player);
        }

        // Hands out every achievement the player has reached and not had yet, with an event for each.
        void CheckAchievements(Player player)
        {
            for (int i = 0; i < Records.Achievements.Count; i++)
            {
                var achievement = Records.Achievements[i];
                if (player.Achievements.Contains(achievement.Id)) continue;
                var entries = RecordEntries(achievement.Group);
                if (achievement.Group != RecordGroup.Felled && entries.Count == 0) continue;
                if (Records.Progress(achievement.Group, player.Records, entries) < Records.Needed(achievement, entries)) continue;
                player.Achievements.Add(achievement.Id);
                foreach (var (material, count) in achievement.Rewards)
                {
                    var found = Loot?.Materials.FirstOrDefault(m => m.Id == material);
                    if (found != null) player.Inventory.AddMaterial(found, count);
                }
                player.InventoryVersion++;
                player.RecordsVersion++;
                _events.Add(new SimEvent(SimEventType.Achievement, player.Id, target: i + 1));
            }
        }

        // Puts back the records and achievements a character save held (unknown ids are kept: a sheet row may come
        // back), without handing the rewards out again.
        public static void RestoreRecords(Player player, IEnumerable<(string Kind, string Id, int Count)> records, IEnumerable<string> achievements)
        {
            player.Records.Clear();
            foreach (var (kind, id, count) in records ?? Enumerable.Empty<(string, string, int)>())
            {
                if (Enum.TryParse<RecordKind>(kind, out var parsed) && count > 0) player.Records[(parsed, id)] = count;
            }
            player.Achievements.Clear();
            foreach (string id in achievements ?? Enumerable.Empty<string>()) player.Achievements.Add(id);
            player.RecordsVersion++;
        }
    }
}
