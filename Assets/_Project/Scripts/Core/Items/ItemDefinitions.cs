using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum ItemSlot
    {
        Weapon,
        Helmet,
        Chest,
        Boots,
        Ring,
        Amulet,
    }

    // Runes give different stats depending on which of these they sit in.
    public enum SlotCategory
    {
        Weapon,
        Armor,
        Jewelry,
    }

    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
    }

    public enum AffixKind
    {
        Prefix,
        Suffix,
    }

    public static class ItemSlots
    {
        public static SlotCategory CategoryOf(ItemSlot slot) => slot switch
        {
            ItemSlot.Weapon => SlotCategory.Weapon,
            ItemSlot.Ring => SlotCategory.Jewelry,
            ItemSlot.Amulet => SlotCategory.Jewelry,
            _ => SlotCategory.Armor,
        };
    }

    public sealed class ItemBase
    {
        internal ItemBase(
            string id,
            string name,
            ItemSlot slot,
            int level,
            SkillTag tags,
            IReadOnlyList<ModifierRange> implicits,
            int maxSockets,
            int weight)
        {
            Id = id;
            Name = name;
            Slot = slot;
            Level = level;
            Tags = tags;
            Implicits = implicits;
            MaxSockets = maxSockets;
            Weight = weight;
        }

        public string Id { get; }
        public string Name { get; }
        public ItemSlot Slot { get; }
        // Bases with the same level form one step of progression; see LootGenerator.BasesAt.
        public int Level { get; }
        // What kind of attack the weapon makes, e.g. Melee, so tagged modifiers know whether they count.
        public SkillTag Tags { get; }
        // Stats every item of this base has, rolled when it drops.
        public IReadOnlyList<ModifierRange> Implicits { get; }
        public int MaxSockets { get; }
        public int Weight { get; }
        public SlotCategory Category => ItemSlots.CategoryOf(Slot);
    }

    public sealed class Affix
    {
        readonly List<AffixTier> _tiers = new List<AffixTier>();

        internal Affix(string id, AffixKind kind, string name, IReadOnlyList<ItemSlot> slots)
        {
            Id = id;
            Kind = kind;
            Name = name;
            Slots = slots;
        }

        public string Id { get; }
        public AffixKind Kind { get; }
        public string Name { get; }
        public IReadOnlyList<ItemSlot> Slots { get; }
        public IReadOnlyList<AffixTier> Tiers => _tiers;

        public bool AllowedOn(ItemSlot slot) => Slots.Contains(slot);

        internal void AddTier(int level, int weight, ModifierRange modifier) =>
            _tiers.Add(new AffixTier(this, _tiers.Count + 1, level, weight, modifier));
    }

    // Tier 1 is the weakest. Each tier unlocks once the item level reaches its Level.
    public sealed class AffixTier
    {
        internal AffixTier(Affix affix, int number, int level, int weight, ModifierRange modifier)
        {
            Affix = affix;
            Number = number;
            Level = level;
            Weight = weight;
            Modifier = modifier;
        }

        public Affix Affix { get; }
        public int Number { get; }
        public int Level { get; }
        public int Weight { get; }
        public ModifierRange Modifier { get; }
    }

    public sealed class RuneDefinition
    {
        readonly IReadOnlyList<Modifier>[] _modifiers;

        internal RuneDefinition(
            string id,
            string name,
            int level,
            int weight,
            IReadOnlyList<Modifier> weapon,
            IReadOnlyList<Modifier> armor,
            IReadOnlyList<Modifier> jewelry)
        {
            Id = id;
            Name = name;
            Level = level;
            Weight = weight;
            _modifiers = new[] { weapon, armor, jewelry };
        }

        public string Id { get; }
        public string Name { get; }
        public int Level { get; }
        public int Weight { get; }

        public IReadOnlyList<Modifier> ModifiersFor(SlotCategory category) => _modifiers[(int)category];
    }

    // Filling every socket of a Common item with exactly these runes, in this order, adds the runeword's bonus on
    // top of the runes' own stats. Only Common items qualify, so plain drops with sockets are worth picking up.
    public sealed class Runeword
    {
        internal Runeword(
            string id,
            string name,
            IReadOnlyList<ItemSlot> slots,
            IReadOnlyList<RuneDefinition> runes,
            IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            Name = name;
            Slots = slots;
            Runes = runes;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<ItemSlot> Slots { get; }
        public IReadOnlyList<RuneDefinition> Runes { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }

        public bool Matches(Item item) =>
            item.Rarity == Rarity.Common
            && Slots.Contains(item.Base.Slot)
            && item.Sockets.Count == Runes.Count
            && item.Sockets.SequenceEqual(Runes);
    }
}
