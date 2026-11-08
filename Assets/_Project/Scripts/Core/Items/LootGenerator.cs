using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class RarityRule
    {
        public RarityRule(int weight, int minAffixes, int maxAffixes, int maxPerKind)
        {
            Weight = weight;
            MinAffixes = minAffixes;
            MaxAffixes = maxAffixes;
            MaxPerKind = maxPerKind;
        }

        public int Weight { get; set; }
        public int MinAffixes { get; set; }
        public int MaxAffixes { get; set; }
        // The most prefixes an item may have, and separately the most suffixes.
        public int MaxPerKind { get; set; }
    }

    public sealed class LootConfig
    {
        public RarityRule Common { get; set; } = new RarityRule(700, 0, 0, 0);
        public RarityRule Uncommon { get; set; } = new RarityRule(220, 1, 2, 1);
        public RarityRule Rare { get; set; } = new RarityRule(70, 3, 4, 2);
        public RarityRule Epic { get; set; } = new RarityRule(10, 5, 6, 3);
        // Chance of each further socket, one after another, up to the base's maximum.
        public int SocketChancePercent { get; set; } = 40;

        public RarityRule RuleFor(Rarity rarity) => rarity switch
        {
            Rarity.Common => Common,
            Rarity.Uncommon => Uncommon,
            Rarity.Rare => Rare,
            _ => Epic,
        };
    }

    public sealed class LootGenerator
    {
        static readonly Rarity[] Rarities = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic };

        readonly LootData _data;
        readonly LootConfig _config;

        public LootGenerator(LootData data, LootConfig config = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (!data.Success) throw new ArgumentException("Loot data has errors:\n" + string.Join("\n", data.Errors), nameof(data));
            if (data.Bases.Count == 0) throw new ArgumentException("Loot data has no item bases.", nameof(data));
            _config = config ?? new LootConfig();
        }

        // itemRarity is the finder's ItemRarity stat in percent. It raises the odds of everything above Common.
        // A roll below minimum is raised to it, which is how bosses guarantee good rewards.
        public Item RollItem(int level, GameRandom random, int itemRarity = 0, Rarity minimum = Rarity.Common)
        {
            var itemBase = random.PickWeighted(BasesAt(level), b => b.Weight);
            var rarity = random.PickWeighted(Rarities, r => RarityWeight(r, itemRarity));
            return RollItem(itemBase, level, rarity < minimum ? minimum : rarity, random);
        }

        // For drops whose base or rarity is already decided, such as boss rewards.
        public Item RollItem(ItemBase itemBase, int level, Rarity rarity, GameRandom random)
        {
            var implicits = itemBase.Implicits.Select(range => range.Roll(random)).ToList();
            var affixes = RollAffixes(itemBase.Slot, level, _config.RuleFor(rarity), random);

            int sockets = 0;
            while (sockets < itemBase.MaxSockets && random.Next(100) < _config.SocketChancePercent) sockets++;

            return new Item(itemBase, level, rarity, implicits, affixes, sockets);
        }

        // Fresh affixes for the item, as many and as good as its rarity and level allow (an anvil's reroll).
        public List<ItemAffix> RerollAffixes(Item item, GameRandom random) =>
            RollAffixes(item.Base.Slot, item.Level, _config.RuleFor(item.Rarity), random);

        public RuneDefinition RollRune(int level, GameRandom random)
        {
            var runes = _data.Runes.Where(r => r.Level <= level && r.Weight > 0).ToList();
            return runes.Count == 0 ? null : random.PickWeighted(runes, r => r.Weight);
        }

        // Growth comes in steps: only the newest step of bases that the level has reached drops, so older gear
        // stops cluttering the floor once a boss opens the next tier. Below every step, the first one drops.
        // The bases of the highest step the level reaches. Legendaries never drop at random.
        public IReadOnlyList<ItemBase> BasesAt(int level)
        {
            // Weight 0: made only, never dropped (the rift's band and charm), so no step of their own.
            var ordinary = _data.Bases.Where(b => !b.IsLegendary && b.Weight > 0).ToList();
            var reached = ordinary.Where(b => b.Level <= level).ToList();
            int step = reached.Count > 0 ? reached.Max(b => b.Level) : ordinary.Min(b => b.Level);
            return ordinary.Where(b => b.Level == step).ToList();
        }

        // The boss's legendaries.
        public IEnumerable<ItemBase> LegendariesOf(string bossId) => _data.Bases.Where(b => b.Legendary == bossId);

        // A legendary as the boss drops it: its own stats, rolled within their ranges, every socket, no affixes.
        public Item RollLegendary(ItemBase itemBase, GameRandom random)
        {
            var implicits = itemBase.Implicits.Select(range => range.Roll(random)).ToList();
            return new Item(itemBase, itemBase.Level, Rarity.Legendary, implicits, new List<ItemAffix>(), itemBase.MaxSockets);
        }

        int RarityWeight(Rarity rarity, int itemRarity)
        {
            int weight = _config.RuleFor(rarity).Weight;
            return rarity == Rarity.Common ? weight : weight * (100 + Math.Max(0, itemRarity)) / 100;
        }

        // Each pick draws from every tier the level allows, across prefixes and suffixes that still have room.
        // An affix appears at most once per item.
        List<ItemAffix> RollAffixes(ItemSlot slot, int level, RarityRule rule, GameRandom random)
        {
            var affixes = new List<ItemAffix>();
            int count = rule.MinAffixes + random.Next(rule.MaxAffixes - rule.MinAffixes + 1);
            while (affixes.Count < count)
            {
                var pool = _data.Affixes
                    .Where(affix => affix.AllowedOn(slot)
                        && affixes.All(taken => taken.Affix != affix)
                        && affixes.Count(taken => taken.Affix.Kind == affix.Kind) < rule.MaxPerKind)
                    .SelectMany(affix => affix.Tiers.Where(tier => tier.Level <= level && tier.Weight > 0))
                    .ToList();
                if (pool.Count == 0) break;

                var picked = random.PickWeighted(pool, tier => tier.Weight);
                affixes.Add(new ItemAffix(picked, picked.Modifier.Roll(random)));
            }
            return affixes;
        }
    }
}
