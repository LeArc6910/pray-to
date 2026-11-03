using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Working gear at an anvil (World.Refining.cs, Docs/Loot.md 장비 다듬기).
    public class RefiningTests
    {
        static readonly MaterialDefinition Essence = Gear.Data.FindMaterial(World.Essence);
        static readonly MaterialDefinition IronBar = Gear.Data.FindMaterial("iron_bar");
        static readonly RuneDefinition Ember = Gear.Data.FindRune("ember");

        // A player standing beside an anvil.
        static World Forge(out Player player, bool anvil = true)
        {
            var map = Maps.Room(12);
            if (anvil) map[6, 7] = TileType.Anvil;
            var world = new World(map, TileMap.Center(6, 6), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", null);
            return world;
        }

        static Item Carry(World world, Player player, Item item)
        {
            world.Give(player, item);
            return player.Inventory.Items.Last();
        }

        [Test]
        public void TheAnvilIsMadeAndPlaced()
        {
            Assert.AreEqual(TileType.Anvil, Gear.Data.FindMaterial("anvil").Places);
            Assert.IsTrue(Gear.Data.Recipes.Any(r => r.Material?.Id == "anvil" && r.Station == TileType.Workbench));
            Assert.IsNotNull(Tiles.Shipped.Find(TileType.Anvil), "it can be taken down again");
            Assert.IsNotNull(Essence);
        }

        [Test]
        public void UpgradesAddATenthOfTheBaseStatsEachStepAndCostEssenceAndMetal()
        {
            var world = Forge(out var player);
            var sword = Carry(world, player, Gear.Plain("iron_sword"));
            float damage = sword.Modifiers().Where(m => m.Stat == StatType.Damage && m.Kind == ModifierKind.Flat).Sum(m => m.Value);
            var cost = World.UpgradeCost(sword, Gear.Data).Value;
            Assert.AreEqual((2, IronBar, 1), cost, "two essence and one of its iron bars for the first step");
            Assert.IsFalse(world.UpgradeItem(player.Id, sword.Id), "nothing to pay with");

            player.Inventory.AddMaterial(Essence, 100);
            player.Inventory.AddMaterial(IronBar, 100);
            for (int step = 1; step <= Item.MaxUpgrade; step++) Assert.IsTrue(world.UpgradeItem(player.Id, sword.Id), $"+{step}");
            Assert.IsFalse(world.UpgradeItem(player.Id, sword.Id), "+5 is the top");
            Assert.IsNull(World.UpgradeCost(sword, Gear.Data));

            Assert.AreEqual(5, sword.Upgrade);
            Assert.AreEqual(100 - (2 + 4 + 6 + 8 + 10), player.Inventory.CountOf(Essence));
            Assert.AreEqual(100 - 15, player.Inventory.CountOf(IronBar));
            float upgraded = sword.Modifiers().Where(m => m.Stat == StatType.Damage && m.Kind == ModifierKind.Flat).Sum(m => m.Value);
            Assert.AreEqual(damage * 1.5f, upgraded, damage * 0.06f + 1, "half again at +5");
            StringAssert.StartsWith("+5 ", ItemText.Name(sword));
        }

        [Test]
        public void WornGearCanBeUpgradedAndTheStatsFollow()
        {
            var world = Forge(out var player);
            player.Equip(Gear.Plain("iron_sword"));
            var sword = player.Equipment[ItemSlot.Weapon];
            world.Give(player, Gear.Plain("copper_helmet"));
            float before = player.Stats.Damage;
            player.Inventory.AddMaterial(Essence, 10);
            player.Inventory.AddMaterial(IronBar, 10);

            Assert.IsTrue(world.UpgradeItem(player.Id, sword.Id));
            Assert.Greater(player.Stats.Damage, before);
        }

        [Test]
        public void NothingHappensAwayFromAnAnvil()
        {
            var world = Forge(out var player, anvil: false);
            var sword = Carry(world, player, Gear.Plain("iron_sword"));
            player.Inventory.AddMaterial(Essence, 10);
            player.Inventory.AddMaterial(IronBar, 10);
            Assert.IsFalse(world.UpgradeItem(player.Id, sword.Id));
            Assert.IsFalse(world.SalvageItem(player.Id, sword.Id));
            Assert.IsFalse(world.NearAnvil(player));
        }

        [Test]
        public void BreakingDownGivesEssenceByRarityAndHandsBackTheRunes()
        {
            var world = Forge(out var player);
            var rare = Carry(world, player, Gear.Plain("iron_sword", 2, Rarity.Rare, Gear.Affix("sharp", 2)));
            rare.TryInsertRune(0, Ember, Gear.Data.Runewords);
            int value = World.SalvageValue(rare);
            Assert.AreEqual(4 + rare.Level / 10, value);

            Assert.IsTrue(world.SalvageItem(player.Id, rare.Id));
            Assert.IsNull(player.Inventory.Find(rare.Id));
            Assert.AreEqual(value, player.Inventory.CountOf(Essence));
            Assert.AreEqual(1, player.Inventory.CountOf(Ember));

            // Worn gear is not broken down.
            player.Equip(Gear.Plain("copper_sword"));
            Assert.IsFalse(world.SalvageItem(player.Id, player.Equipment[ItemSlot.Weapon].Id));
        }

        [Test]
        public void RunesComeBackOutForEssenceAndTheRunewordGoes()
        {
            var world = Forge(out var player);
            var sword = Carry(world, player, Gear.Plain("iron_sword", 1));
            sword.TryInsertRune(0, Ember, Gear.Data.Runewords);
            Assert.IsFalse(world.UnsocketItem(player.Id, sword.Id), "no essence");
            player.Inventory.AddMaterial(Essence, 1);

            Assert.IsTrue(world.UnsocketItem(player.Id, sword.Id));
            Assert.IsTrue(sword.Sockets.All(r => r == null));
            Assert.IsNull(sword.Runeword);
            Assert.AreEqual(1, player.Inventory.CountOf(Ember));
            Assert.AreEqual(0, player.Inventory.CountOf(Essence));
            Assert.IsFalse(world.UnsocketItem(player.Id, sword.Id), "nothing left to take out");
        }

        [Test]
        public void RerollingGivesNewAffixesOfTheSameRarity()
        {
            var world = Forge(out var player);
            var rare = Carry(world, player, Gear.Plain("iron_sword", 0, Rarity.Rare, Gear.Affix("sharp", 2)));
            var plain = Carry(world, player, Gear.Plain("iron_sword"));
            Assert.AreEqual(5, World.RerollCost(rare));
            Assert.AreEqual(0, World.RerollCost(plain), "nothing to roll on a common item");
            player.Inventory.AddMaterial(Essence, 50);

            Assert.IsFalse(world.RerollItem(player.Id, plain.Id));
            bool changed = false;
            for (int i = 0; i < 5; i++)
            {
                var before = rare.Affixes.Select(a => (a.Affix.Id, a.Modifier.Value)).ToList();
                Assert.IsTrue(world.RerollItem(player.Id, rare.Id));
                Assert.AreEqual(Rarity.Rare, rare.Rarity);
                Assert.That(rare.Affixes.Count, Is.InRange(3, 4), "as many as a rare gets");
                changed |= !before.SequenceEqual(rare.Affixes.Select(a => (a.Affix.Id, a.Modifier.Value)));
            }
            Assert.IsTrue(changed, "the affixes changed");
            Assert.AreEqual(50 - 25, player.Inventory.CountOf(Essence));
        }

        [Test]
        public void TheUpgradeTravelsAndIsSavedInTheRarityByte()
        {
            var world = Forge(out var player);
            var item = Carry(world, player, Gear.Plain("iron_sword", 1, Rarity.Epic, Gear.Affix("sharp", 2)));
            player.Inventory.AddMaterial(Essence, 50);
            player.Inventory.AddMaterial(IronBar, 50);
            for (int i = 0; i < 4; i++) world.UpgradeItem(player.Id, item.Id);

            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) ItemCodec.Write(w, item, Gear.Data);
            stream.Position = 0;
            var sent = ItemCodec.Read(new BinaryReader(stream), Gear.Data);
            Assert.AreEqual((4, Rarity.Epic), (sent.Upgrade, sent.Rarity));

            var bag = new Inventory();
            bag.TryAdd(item);
            var saved = CharacterSave.Read(CharacterSave.Write(new Equipment(), bag), Gear.Data).Bag.Items.Single();
            Assert.AreEqual((4, Rarity.Epic), (saved.Upgrade, saved.Rarity));
        }
    }
}
