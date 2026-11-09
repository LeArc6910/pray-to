using System;
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
        // A boss's own item: fixed stats under its own name, never rolled at random (item_bases.csv legendary).
        Legendary,
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
            int weight,
            string tool = "",
            string legendary = "",
            int legendaryChance = 0,
            int requiredLevel = 0)
        {
            Tool = tool ?? "";
            Legendary = legendary ?? "";
            LegendaryChance = legendaryChance;
            RequiredLevel = requiredLevel > 0 ? requiredLevel : Legendary.Length > 0 ? level : Math.Max(1, level - 5);
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
        // What else the item is for besides fighting: "pickaxe", or empty. Only a pickaxe in hand digs rock.
        public string Tool { get; }
        public bool IsPickaxe => Tool == Pickaxe;
        // The boss that drops this as a legendary, or empty for an ordinary base; and the chance (%) each fighter
        // gets one when it falls.
        public string Legendary { get; }
        // The gear set it belongs to (sets.csv), or none.
        public string SetId { get; internal set; } = "";
        public GearSet Set { get; internal set; }
        public int LegendaryChance { get; }
        public bool IsLegendary => Legendary.Length > 0;
        // The character level it takes to wear: five below the base's step (iron, step 10, from level 5), or a
        // legendary's own step, unless the sheet says (require).
        public int RequiredLevel { get; }

        public const string Pickaxe = "pickaxe";
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

    // Something a player drinks or eats from the bag: it fills Heal percent of their maximum health over Seconds, and
    // then every consumable waits Cooldown seconds. See Docs/Combat.md (healing) and Docs/Loot.md.
    public sealed class ConsumableDefinition
    {
        internal ConsumableDefinition(
            string id,
            string name,
            int level,
            int heal,
            float seconds,
            float cooldown,
            int weight,
            string description,
            IReadOnlyList<Modifier> buff = null,
            float buffSeconds = 0,
            string effect = "")
        {
            Effect = effect ?? "";
            Buff = buff ?? Array.Empty<Modifier>();
            BuffSeconds = buffSeconds;
            Id = id;
            Name = name;
            Level = level;
            Heal = heal;
            Seconds = seconds;
            Cooldown = cooldown;
            Weight = weight;
            Description = description;
        }

        public string Id { get; }
        public string Name { get; }
        public int Level { get; }
        // Percent of maximum health.
        public int Heal { get; }
        public float Seconds { get; }
        public float Cooldown { get; }
        public int Weight { get; }
        public string Description { get; }
        // What a food adds to its eater's stats, and for how long. Empty for a plain potion. See Docs/Farming.md.
        public IReadOnlyList<Modifier> Buff { get; }
        public float BuffSeconds { get; }
        public bool IsFood => Buff.Count > 0;
        // Something else it does (consumables.csv effect): "recall" takes its user home; "mana" gives back mana
        // instead of health, its Heal percent of the most at once. Empty for none.
        public string Effect { get; }
        public bool Recalls => Effect == Recall;
        public const string Recall = "recall";
        public bool RestoresMana => Effect == ManaEffect;
        public const string ManaEffect = "mana";
        // Whether it is any use at full health.
        public bool UsableAtFullHealth => IsFood || Effect.Length > 0;
        public int BuffTicks => World.Ticks(BuffSeconds);
    }

    // Pieces of gear that give more when several are worn together (sets.csv): each bonus once that many pieces are
    // on.
    public sealed class GearSet
    {
        internal GearSet(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
        internal List<(int Pieces, IReadOnlyList<Modifier> Modifiers)> Bonuses { get; } = new List<(int, IReadOnlyList<Modifier>)>();
        public IReadOnlyList<(int Pieces, IReadOnlyList<Modifier> Modifiers)> AllBonuses => Bonuses;

        // What wearing so many pieces gives.
        public IEnumerable<Modifier> BonusFor(int worn) => Bonuses.Where(b => b.Pieces <= worn).SelectMany(b => b.Modifiers);
    }

    // Something gathered to make things with: from walls, monsters and bosses. Some can be placed as a tile.
    // See Docs/Crafting.md.
    public sealed class MaterialDefinition
    {
        internal MaterialDefinition(string id, string name, TileType? places, string description, Biome? fish = null, float fuel = 0,
            bool fishAtNight = false, bool potCatch = false)
        {
            PotCatch = potCatch;
            Id = id;
            Name = name;
            Places = places;
            Description = description;
            Fish = fish;
            FishAtNight = fishAtNight;
            Fuel = fuel;
        }

        public string Id { get; }
        public string Name { get; }
        // The tile [놓기] puts down, or null when it cannot be placed.
        public TileType? Places { get; }
        public string Description { get; }
        // A fish caught in the water of this biome (materials.csv fish), or null; only at night when FishAtNight.
        public Biome? Fish { get; }
        public bool FishAtNight { get; }
        // What a crab pot catches (materials.csv fish "pot").
        public bool PotCatch { get; }

        // Quality (Docs/Farming.md, 품질): a crop comes in plain, silver (1) or gold (2). The sheet marks which have it
        // (materials.csv quality); the silver and gold kinds are made from them, with Base the plain one. Recipes,
        // machines, requests and gifts take any quality as the plain one; shops pay more for the better.
        public const int MaxQuality = 2;
        internal static readonly string[] QualityIds = { "", "silver", "gold" };
        public static readonly string[] QualityNames = { "", "은빛", "금빛" };
        public static int QualityPercent(int quality) => 100 + 25 * quality;

        public bool HasQuality { get; internal set; }
        public int Quality { get; internal set; }
        public MaterialDefinition Base { get; internal set; }
        public IReadOnlyList<MaterialDefinition> Variants { get; internal set; } = Array.Empty<MaterialDefinition>();
        // The plain kind: itself unless it is a silver or gold one.
        public MaterialDefinition Kind => Base ?? this;
        // It and its better kinds, plain first.
        public IEnumerable<MaterialDefinition> AllQualities => new[] { Kind }.Concat(Kind.Variants);
        // Seconds a generator runs on one of it (materials.csv fuel), or 0 when it does not burn.
        public float Fuel { get; }
    }

    // Turns materials into an item, a consumable or another material. Exactly one result is set.
    public sealed class Recipe
    {
        internal Recipe(
            string id,
            ItemBase item,
            ConsumableDefinition consumable,
            MaterialDefinition material,
            int count,
            IReadOnlyList<(MaterialDefinition Material, int Count)> ingredients,
            TileType? station = null)
        {
            Station = station;
            Id = id;
            Item = item;
            Consumable = consumable;
            Material = material;
            Count = count;
            Ingredients = ingredients;
        }

        public string Id { get; }
        public ItemBase Item { get; }
        public ConsumableDefinition Consumable { get; }
        public MaterialDefinition Material { get; }
        public int Count { get; }
        public IReadOnlyList<(MaterialDefinition Material, int Count)> Ingredients { get; }
        // The crafting station that must be nearby (World.StationReach), or null for anywhere.
        public TileType? Station { get; }
        public string Name => Item?.Name ?? Consumable?.Name ?? Material.Name;
        // Seconds the sheet sets for making it by hand (recipes.csv seconds), or -1 to go by CraftRules.Seconds.
        public float Seconds { get; internal set; } = -1;
    }

    // How long making a thing by hand takes (2026-11-09, Docs/Crafting.md 제작 시간): gear 3 seconds and half a second
    // every five levels (copper 3, iron 4, mithril 5, void 7), a machine 2, the rest none; the sheet may say otherwise.
    // An assembler makes the same in the same time without anyone waiting, twice as fast on power.
    public static class CraftRules
    {
        public const float MachineSeconds = 2;

        public static float Seconds(Recipe recipe, TileData tiles)
        {
            if (recipe == null) return 0;
            if (recipe.Seconds >= 0) return recipe.Seconds;
            if (recipe.Item != null) return 3 + (float)Math.Round(recipe.Item.Level / 5f) / 2;
            if (recipe.Material?.Places is TileType placed && tiles?.Machine(placed) != null) return MachineSeconds;
            return 0;
        }
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
