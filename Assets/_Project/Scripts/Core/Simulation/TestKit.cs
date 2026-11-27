using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What the test ground's panel hands a character (Docs/TestGround.md), and what the sandbox's bots are given
    // (DevTools/CoreSandbox --level --build): gear of a band at a chosen rarity, roll and upgrade, and a level with its
    // points spent the way a player would.
    public static class TestKit
    {
        // Where in its range each number of an item falls.
        public enum Roll : byte
        {
            Low,
            Middle,
            High,
        }

        // How the points of a level are spent.
        public enum Build : byte
        {
            // Left to spend by hand on the skills and passives screens (only each class's first skill known).
            Points,
            // As a fighter would: the skills of the classes at the rank the points reach, passives on damage first, the
            // best crystals in every damage skill (the sandbox's --build).
            Attack,
            // The same skills, the passives on health, armor and regeneration first.
            Defense,
        }

        // The ore weapons of a band, by their ids' ends (Docs/Weapons.md: eleven kinds).
        public static readonly string[] Weapons = { "sword", "dagger", "greatsword", "spear", "hammer", "bow", "crossbow", "boomerang", "staff", "wand", "orb" };
        static readonly string[] Pieces = { "helmet", "armor", "boots", "ring", "amulet" };

        static int At(ModifierRange range, Roll roll) => roll switch
        {
            Roll.Low => range.Min,
            Roll.High => range.Max,
            _ => (range.Min + range.Max) / 2,
        };

        // An item of the base: its numbers at the roll, the rarity's count of affixes (the highest tier its level allows,
        // picked the same way each time for the same base), every socket open, worked to the upgrade. A boss's or the
        // rift's legendary keeps its own name and has no affixes, whatever rarity is asked. Common at the middle and +0
        // is the band's set the hit table weighs (HitTable.Wearing).
        public static Item Make(LootData loot, ItemBase itemBase, Rarity rarity, Roll roll, int upgrade)
        {
            if (itemBase == null) throw new ArgumentNullException(nameof(itemBase));
            var implicits = itemBase.Implicits.Select(r => new Modifier(r.Stat, r.Kind, At(r, roll), r.Tags)).ToList();
            if (itemBase.IsLegendary) rarity = Rarity.Legendary;
            var affixes = new List<ItemAffix>();
            if (rarity != Rarity.Legendary && rarity != Rarity.Common)
            {
                var rule = new LootConfig().RuleFor(rarity);
                int count = roll == Roll.Low ? rule.MinAffixes : roll == Roll.High ? rule.MaxAffixes : (rule.MinAffixes + rule.MaxAffixes + 1) / 2;
                var random = new GameRandom(Seed(itemBase.Id));
                while (affixes.Count < count)
                {
                    var pool = loot.Affixes
                        .Where(affix => affix.AllowedOn(itemBase.Slot) && affixes.All(taken => taken.Affix != affix)
                            && affixes.Count(taken => taken.Affix.Kind == affix.Kind) < rule.MaxPerKind
                            && affix.Tiers.Any(t => t.Level <= itemBase.Level && t.Weight > 0))
                        .ToList();
                    if (pool.Count == 0) break;
                    var affix = pool[random.Next(pool.Count)];
                    var tier = affix.Tiers.Where(t => t.Level <= itemBase.Level && t.Weight > 0).OrderByDescending(t => t.Level).First();
                    var range = tier.Modifier;
                    affixes.Add(new ItemAffix(tier, new Modifier(range.Stat, range.Kind, At(range, roll), range.Tags)));
                }
            }
            return new Item(itemBase, itemBase.Level, rarity, implicits, affixes, itemBase.MaxSockets)
            {
                Upgrade = Math.Clamp(upgrade, 0, Item.MaxUpgrade),
            };
        }

        static ulong Seed(string id)
        {
            ulong seed = 1469598103934665603UL;
            foreach (char c in id) seed = (seed ^ c) * 1099511628211UL;
            return seed;
        }

        // The band's full set (DamageBands.TierNames: 1 copper ... 5 void): the weapon of the kind (sword, bow, staff),
        // helmet, chest, boots, ring and amulet.
        public static List<Item> BandSet(LootData loot, int band, string weapon, Rarity rarity, Roll roll, int upgrade)
        {
            string tier = DamageBands.TierNames[DamageBands.Clamp(band) - 1];
            var items = new List<Item>();
            foreach (string piece in new[] { Weapons.Contains(weapon) ? weapon : "sword" }.Concat(Pieces))
            {
                var itemBase = loot.FindBase($"{tier}_{piece}");
                if (itemBase != null) items.Add(Make(loot, itemBase, rarity, roll, upgrade));
            }
            return items;
        }

        // Puts the rune into the first open socket of what the player wears (the weapon first, then the chest, helmet,
        // boots, ring and amulet). The item it went into, or null when every socket is full.
        public static Item InsertRune(Player player, RuneDefinition rune, IReadOnlyList<Runeword> runewords)
        {
            foreach (var slot in new[] { ItemSlot.Weapon, ItemSlot.Chest, ItemSlot.Helmet, ItemSlot.Boots, ItemSlot.Ring, ItemSlot.Amulet })
            {
                var item = player.Equipment[slot];
                if (item == null) continue;
                for (int socket = 0; socket < item.Sockets.Count; socket++)
                {
                    if (item.Sockets[socket] != null || !item.TryInsertRune(socket, rune, runewords)) continue;
                    player.RefreshStats();
                    player.InventoryVersion++;
                    return item;
                }
            }
            return null;
        }

        // Every rune out of what the player wears (they are gone). How many came out.
        public static int ClearRunes(Player player)
        {
            int count = 0;
            foreach (var item in player.Equipment.Items) count += item.TakeRunes().Count;
            player.RefreshStats();
            player.InventoryVersion++;
            return count;
        }

        // The player at the level (1 to Leveling.MaxLevel) with its points spent as the build says: the classes kept,
        // the skills and passives they had forgotten first. Whole again afterwards (unless fallen).
        public static void SetLevel(Player player, ClassData classes, LootData loot, int level, Build build)
        {
            level = Math.Clamp(level, 1, Leveling.MaxLevel);
            player.Experience = Leveling.TotalFor(level);
            player.Supports.Clear();
            player.SkillRanks.Clear();
            player.LearnBasics(player.Classes);
            player.Passives.Clear();
            if (build != Build.Points)
            {
                var open = ClassRules.AllSkillsFor(player.Classes).Where(s => s.Level <= level).ToList();
                int rank = BuiltRank(open, level);
                foreach (var skill in open) player.SkillRanks[skill] = rank;
                if (classes != null) SpendPassives(player, classes, build == Build.Defense ? DefenseWorth : (Func<PassiveNode, float>)AttackWorth);
                if (loot != null) SetCrystals(player, open, rank, loot);
            }
            player.RefreshStats();
            player.ChangeKit(player.Classes, null);
            foreach (var skill in player.Skills) player.ClearCooldown(skill);
            // A fallen player gets up as usual, not here.
            if (!player.IsDead)
            {
                player.Health = player.Stats.MaxHealth;
                player.Mana = player.Stats.MaxMana;
            }
            player.InventoryVersion++;
        }

        // The rank the four skills in the slots reach with the level's points, once every open skill is learned (a
        // class's first skill is free).
        public static int BuiltRank(IReadOnlyList<SkillDefinition> open, int level)
        {
            int learning = open.Count(s => !s.IsBasic);
            int left = Math.Max(0, level - 1 - learning);
            return Math.Clamp(1 + left / ClassRules.SkillSlots, 1, SkillRules.MaxRank);
        }

        // How much a passive point is worth to a fighter, by what it gives: damage and speed first, then crits,
        // skill ranks and cooldowns, then health and armour.
        public static float AttackWorth(PassiveNode node) => node.Modifiers.Sum(m => m.Stat switch
        {
            StatType.Damage => m.Kind == ModifierKind.Flat ? 3f * m.Value : m.Kind == ModifierKind.More ? 1.5f * m.Value : m.Value,
            StatType.AttackSpeed => 0.9f * m.Value,
            StatType.CritChance => 0.6f * m.Value,
            StatType.CritDamage => 0.3f * m.Value,
            StatType.SkillRank => 15f * m.Value,
            StatType.CooldownRecovery => 0.6f * m.Value,
            StatType.MaxHealth => m.Kind == ModifierKind.Flat ? 0.1f * m.Value : 0.4f * m.Value,
            StatType.Armor => 0.1f * m.Value,
            StatType.HealthRegen => 0.5f * m.Value,
            StatType.ManaRegen or StatType.MaxMana => 0.1f * m.Value,
            _ => 0f,
        });

        // To one who would rather live: health, armor and regeneration first, then skill ranks and cooldowns, damage last.
        public static float DefenseWorth(PassiveNode node) => node.Modifiers.Sum(m => m.Stat switch
        {
            StatType.MaxHealth => m.Kind == ModifierKind.Flat ? 1f * m.Value : 4f * m.Value,
            StatType.Armor => m.Kind == ModifierKind.Flat ? 1.5f * m.Value : 4f * m.Value,
            StatType.HealthRegen => 3f * m.Value,
            StatType.SkillRank => 6f * m.Value,
            StatType.CooldownRecovery => 0.6f * m.Value,
            StatType.MoveSpeed => 0.5f * m.Value,
            StatType.Damage => m.Kind == ModifierKind.Flat ? 0.3f * m.Value : 0.1f * m.Value,
            StatType.ManaRegen or StatType.MaxMana => 0.1f * m.Value,
            _ => 0.05f * m.Value,
        });

        // Spends every passive point, each on the open point (next to one taken) worth most, looking one step past it.
        public static void SpendPassives(Player player, ClassData classes, Func<PassiveNode, float> worth)
        {
            player.Passives.Clear();
            while (player.PassivePoints > 0)
            {
                var open = classes.Passives.Where(n => !n.IsStart && !player.Passives.Contains(n) && n.Links.Any(l => l.IsStart || player.Passives.Contains(l))).ToList();
                if (open.Count == 0) break;
                var next = open.OrderByDescending(n => worth(n) + 0.5f * n.Links.Where(l => !l.IsStart && !player.Passives.Contains(l)).Select(worth).DefaultIfEmpty(0).Max())
                    .First();
                player.Passives.Add(next);
            }
            player.RefreshStats();
        }

        // The crystals a player would set: in every skill that deals damage, as many as its rank holds, the first that
        // fit of power, split, spread, haste, pierce, then the rest.
        static readonly SupportKind[] Preferred =
        {
            SupportKind.Power, SupportKind.Multi, SupportKind.Area, SupportKind.Haste, SupportKind.Pierce, SupportKind.Leech, SupportKind.Duration,
        };

        public static void SetCrystals(Player player, IEnumerable<SkillDefinition> skills, int rank, LootData loot)
        {
            var crystals = SkillSupports.Ids.Select(id => loot.Materials.FirstOrDefault(m => m.Id == id)).Where(m => m != null)
                .ToDictionary(m => SkillSupports.KindOf(m).Value);
            foreach (var skill in skills)
            {
                var held = new List<SupportKind>();
                foreach (var kind in Preferred)
                {
                    if (held.Count >= SkillSupports.SlotsFor(rank) || !crystals.ContainsKey(kind)) continue;
                    if (SkillSupports.CanAdd(skill, rank, held, kind)) held.Add(kind);
                }
                if (held.Count > 0) player.Supports[skill] = held.Select(k => crystals[k]).ToList();
            }
        }
    }
}
