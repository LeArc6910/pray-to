using System;
using System.Linq;

namespace Game.Core
{
    // Working gear at an anvil (Docs/Loot.md, 장비 다듬기): upgrading it +1 at a time up to +5, breaking it down into
    // essence, taking its runes back out, and rolling its affixes again. Essence comes from breaking gear down; an
    // upgrade also takes the metal the item was made of. The player stands within StationReach of an anvil.
    public sealed partial class World
    {
        public const string Essence = "essence";
        // Essence for breaking down an item of each rarity, plus one for every ten levels of it.
        static readonly int[] SalvageEssence = { 1, 2, 4, 7, 12 };
        // Essence for a reroll, by rarity (only rarities with affixes).
        static readonly int[] RerollEssence = { 0, 3, 5, 8, 0 };
        // Essence for taking the runes out.
        public const int UnsocketEssence = 1;
        // Essence for moving an upgrade onto newer gear (TransferUpgrade).
        public const int TransferEssence = 2;

        MaterialDefinition EssenceMaterial => Loot?.FindMaterial(Essence);

        // What the next step costs: essence (2 a step, 3 for an item no recipe makes) and, for a made one, the metal
        // its recipe takes most of, one a step. Null when it is already at the top.
        public static (int Essence, MaterialDefinition Metal, int MetalCount)? UpgradeCost(Item item, LootData loot)
        {
            if (item == null || item.Upgrade >= Item.MaxUpgrade) return null;
            int step = item.Upgrade + 1;
            var recipe = loot?.Recipes.FirstOrDefault(r => r.Item == item.Base);
            var metal = recipe?.Ingredients.OrderByDescending(i => i.Count).Select(i => i.Material).FirstOrDefault();
            return metal == null ? (step * 3, null, 0) : (step * 2, metal, step);
        }

        // What breaking it down gives.
        public static int SalvageValue(Item item)
        {
            int spent = Enumerable.Range(1, item.Upgrade).Sum(step => step * 2);
            return SalvageEssence[(int)item.Rarity] + item.Level / 10 + spent / 2;
        }

        public static int RerollCost(Item item) => item == null || item.Affixes.Count == 0 ? 0 : RerollEssence[(int)item.Rarity];

        public bool NearAnvil(Player player) => player != null && Map.Near(player.Position, TileType.Anvil, StationReach);

        // Raises a worn or carried item one step. False when it cannot: no anvil near, at the top, or short of the cost.
        public bool UpgradeItem(int playerId, int itemId)
        {
            var (player, item, worn) = Refinable(playerId, itemId);
            var cost = UpgradeCost(item, Loot);
            if (cost == null) return false;
            var (essence, metal, metalCount) = cost.Value;
            if (player.Inventory.CountOf(EssenceMaterial) < essence || metal != null && player.Inventory.CountOf(metal) < metalCount) return false;
            player.Inventory.TakeMaterial(EssenceMaterial, essence);
            if (metal != null) player.Inventory.TakeMaterial(metal, metalCount);
            item.Upgrade++;
            Refined(player, worn);
            return true;
        }

        // Breaks a carried item down into essence; its runes come back to the bag.
        public bool SalvageItem(int playerId, int itemId)
        {
            var (player, item, worn) = Refinable(playerId, itemId);
            if (item == null || worn || EssenceMaterial == null) return false;
            player.Inventory.Remove(item);
            player.Inventory.AddMaterial(EssenceMaterial, SalvageValue(item));
            foreach (var rune in item.TakeRunes()) player.Inventory.AddRune(rune);
            Refined(player, false);
            return true;
        }

        // Takes the item's runes back out into the bag, which undoes a runeword, for UnsocketEssence.
        public bool UnsocketItem(int playerId, int itemId)
        {
            var (player, item, worn) = Refinable(playerId, itemId);
            if (item == null || !item.Sockets.Any(r => r != null) || player.Inventory.CountOf(EssenceMaterial) < UnsocketEssence) return false;
            player.Inventory.TakeMaterial(EssenceMaterial, UnsocketEssence);
            foreach (var rune in item.TakeRunes()) player.Inventory.AddRune(rune);
            Refined(player, worn);
            return true;
        }

        // Rolls the item's affixes again, as many and as good as its rarity and level allow. Only items with affixes.
        public bool RerollItem(int playerId, int itemId)
        {
            var (player, item, worn) = Refinable(playerId, itemId);
            int cost = RerollCost(item);
            if (item == null || cost == 0 || _lootGenerator == null || player.Inventory.CountOf(EssenceMaterial) < cost) return false;
            player.Inventory.TakeMaterial(EssenceMaterial, cost);
            item.SetAffixes(_lootGenerator.RerollAffixes(item, _random));
            Refined(player, worn);
            return true;
        }

        // The item whose upgrade TransferUpgrade would move onto this one (Docs/Loot.md 강화 옮기기, 2026-11-11): worn or
        // carried, for the same slot, of an earlier tier (a lower base level), and upgraded further than this one; the
        // most upgraded of those. Null when there is none.
        public static Item UpgradeSource(Item target, System.Collections.Generic.IEnumerable<Item> owned) =>
            target == null ? null : owned
                .Where(i => i != target && i.Base.Slot == target.Base.Slot && i.Base.Level < target.Base.Level && i.Upgrade > target.Upgrade)
                .OrderByDescending(i => i.Upgrade).ThenBy(i => i.Id).FirstOrDefault();

        // Moves the upgrade of older gear onto this newer piece for TransferEssence, so what went into the old one is
        // not lost and a new tier is worth looking forward to (the research report's candidate 11): the new piece takes
        // the old one's steps, and the old one goes back to +0, keeping its affixes and runes.
        public bool TransferUpgrade(int playerId, int itemId)
        {
            var (player, item, _) = Refinable(playerId, itemId);
            if (item == null) return false;
            var source = UpgradeSource(item, player.Equipment.Items.Concat(player.Inventory.Items));
            if (source == null || player.Inventory.CountOf(EssenceMaterial) < TransferEssence) return false;
            player.Inventory.TakeMaterial(EssenceMaterial, TransferEssence);
            item.Upgrade = source.Upgrade;
            source.Upgrade = 0;
            Refined(player, true);
            return true;
        }

        // The player and their item by id, worn or in the bag, when they stand by an anvil.
        (Player Player, Item Item, bool Worn) Refinable(int playerId, int itemId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || !NearAnvil(player) || EssenceMaterial == null) return (player, null, false);
            var worn = player.Equipment.Items.FirstOrDefault(i => i.Id == itemId);
            return worn != null ? (player, worn, true) : (player, player.Inventory.Find(itemId), false);
        }

        void Refined(Player player, bool worn)
        {
            if (worn) player.RefreshStats();
            player.InventoryVersion++;
        }
    }
}
