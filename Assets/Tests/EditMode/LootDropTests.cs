using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class LootDropTests
    {
        static readonly MonsterDefinition Slime = Monsters.Shipped.Find("slime");

        static World Room(int size = 14, MonsterData monsters = null) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), monsters ?? Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data);

        // A monster that always drops an item and a rune, and dies to one sword hit.
        static readonly MonsterData Pinata = MonsterSheet.Parse(
            Monsters.Header.TrimEnd('\n') + ",drop_chance,rune_chance\npinata,피냐타,1,1,0,0,1,0.4,0,0.5,1,1,1,100,100\n");

        static void Kill(World world, Player player, MonsterDefinition monster)
        {
            world.SpawnEnemy(monster, player.Position + new Vec2(1.2f, 0));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            world.SetAttacking(player.Id, true);
            world.Step();
            world.SetAttacking(player.Id, false);
            for (int i = 0; i < World.Ticks(0.5f); i++) world.Step();
        }

        [Test]
        public void ShippedSheetsGiveSlimesAndTheBossSomethingToDrop()
        {
            Assert.Greater(Slime.DropChance, 0);
            Assert.Greater(Slime.RuneChance, 0);
            var warden = Bosses.Shipped.Find("cave_warden");
            Assert.Greater(warden.DropCount, 0);
            Assert.AreEqual(10, warden.DropLevel, "the first boss opens the iron step");
            Assert.AreEqual(Rarity.Rare, warden.DropRarity);
        }

        [Test]
        public void MonstersDropAtTheirChance()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword"));

            int kills = 400;
            for (int i = 0; i < kills; i++)
            {
                world.SpawnEnemy(Slime, player.Position + new Vec2(1.2f, 0));
                world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
                world.SetAttacking(player.Id, true);
                for (int t = 0; t < 20 && world.Enemies.Count > 0; t++) world.Step();
            }

            int items = world.GroundLoot.Count(l => l.Item != null) + player.Inventory.Items.Count;
            int runes = world.GroundLoot.Count(l => l.Rune != null) + player.Inventory.Runes.Values.Sum();
            Assert.That(items / (double)kills, Is.InRange(0.14, 0.26), "about 20%");
            Assert.That(runes / (double)kills, Is.InRange(0.03, 0.10), "about 6%");
        }

        [Test]
        public void DropsArePickedUpByWalkingOverThem()
        {
            var world = Room(monsters: Pinata);
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            Kill(world, player, Pinata.Find("pinata"));

            // Close enough already: the scatter keeps drops within a tile of where the monster stood.
            var events = new List<SimEvent>();
            for (int i = 0; i < 30 && world.GroundLoot.Count > 0; i++)
            {
                var next = world.GroundLoot[0].Position;
                world.MovePlayer(player.Id, next, new Vec2(1, 0), 10);
                world.Step();
                events.AddRange(world.TakeEvents());
            }

            Assert.AreEqual(0, world.GroundLoot.Count);
            Assert.AreEqual(1, player.Inventory.Items.Count);
            Assert.AreEqual(1, player.Inventory.Runes.Values.Sum());
            Assert.AreNotEqual(0, player.Inventory.Items[0].Id, "items get an id when they appear");
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.Pickup && e.Source == player.Id));
        }

        [Test]
        public void AFullBagLeavesItemsOnTheFloorButStillTakesRunes()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            for (int i = 0; i < Inventory.Capacity; i++) player.Inventory.TryAdd(Gear.Plain("copper_ring"));
            world.DropItem(Gear.Plain("copper_sword"), player.Position);
            world.DropRune(Gear.Data.FindRune("ember"), player.Position);

            world.Step();

            Assert.AreEqual(1, world.GroundLoot.Count);
            Assert.IsNotNull(world.GroundLoot[0].Item);
            Assert.AreEqual(1, player.Inventory.CountOf(Gear.Data.FindRune("ember")));
        }

        [Test]
        public void BeatingTheBossDropsRareNextStepGearForEachPlayerAndRaisesLaterDrops()
        {
            var world = new World(Maps.Room(40), TileMap.Center(20, 20), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data);
            var definition = Bosses.Parse(
                Bosses.BossesHeader.TrimEnd('\n') + ",drop_count,drop_level,drop_rarity,rune_count\n" +
                "dummy,허수아비,10,0,0,2,1,,1,3,10,2,10,rare,1\n",
                Bosses.AttacksHeader + "dummy,a,가,,circle,nearest,1,1,1,,,,1,,,1,1,\n").Find("dummy");
            var boss = world.PlaceBoss(definition, TileMap.Center(20, 20), 8);
            var alice = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            var bob = world.AddPlayer("b", null);
            world.MovePlayer(alice.Id, boss.Position + new Vec2(0, -2), new Vec2(0, 1), 10);
            world.MovePlayer(bob.Id, boss.Position + new Vec2(0, 2), new Vec2(0, -1), 10);

            Assert.AreEqual(1, world.ProgressLevel);
            world.SetAttacking(alice.Id, true);
            for (int i = 0; i < 90 && world.Bosses.Count > 0 && !boss.IsDead; i++) world.Step();

            Assert.IsTrue(boss.IsDead);
            Assert.AreEqual(10, world.ProgressLevel);
            var items = world.GroundLoot.Where(l => l.Item != null).Select(l => l.Item).Concat(alice.Inventory.Items).Concat(bob.Inventory.Items).ToList();
            Assert.AreEqual(4, items.Count, "two for each of the two players");
            Assert.IsTrue(items.All(i => i.Level == 10 && i.Rarity >= Rarity.Rare && i.Base.Level == 10));
        }

        [Test]
        public void EquippingSwapsWithTheWornItemInPlace()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            var worn = player.Equipment[ItemSlot.Weapon];
            var ring = world.DropItem(Gear.Plain("copper_ring"), player.Position).Item;
            var iron = world.DropItem(Gear.Plain("iron_sword"), player.Position).Item;
            world.Step();
            int version = player.InventoryVersion;

            Assert.IsFalse(world.EquipItem(player.Id, iron.Id), "iron asks for level 5");
            world.GainExperience(player, Leveling.TotalFor(5));
            Assert.IsTrue(world.EquipItem(player.Id, iron.Id));

            Assert.AreSame(iron, player.Equipment[ItemSlot.Weapon]);
            CollectionAssert.AreEqual(new[] { ring, worn }, player.Inventory.Items);
            Assert.AreEqual(9, player.Stats.Damage, 0.001f);
            Assert.Greater(player.InventoryVersion, version);
            Assert.IsFalse(world.EquipItem(player.Id, iron.Id), "already worn, not in the bag");
        }

        [Test]
        public void UnequippingNeedsRoomInTheBag()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));

            Assert.IsTrue(world.UnequipItem(player.Id, ItemSlot.Weapon));
            Assert.IsNull(player.Equipment[ItemSlot.Weapon]);
            Assert.AreEqual(1, player.Stats.Damage, 0.001f, "bare hands");

            world.EquipItem(player.Id, player.Inventory.Items[0].Id);
            for (int i = 0; i < Inventory.Capacity; i++) player.Inventory.TryAdd(Gear.Plain("copper_ring"));
            Assert.IsFalse(world.UnequipItem(player.Id, ItemSlot.Weapon));
            Assert.IsNotNull(player.Equipment[ItemSlot.Weapon]);
        }

        [Test]
        public void RunesGoIntoTheFirstEmptySocketAndCanCompleteARuneword()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var sword = world.DropItem(Gear.Plain("copper_sword", sockets: 2), player.Position).Item;
            world.DropRune(Gear.Data.FindRune("wind"), player.Position);
            world.DropRune(Gear.Data.FindRune("spark"), player.Position);
            world.Step();
            world.EquipItem(player.Id, sword.Id);

            Assert.IsTrue(world.InsertRune(player.Id, sword.Id, Gear.Data.FindRune("wind")));
            Assert.IsFalse(world.InsertRune(player.Id, sword.Id, Gear.Data.FindRune("wind")), "no wind rune left");
            Assert.IsTrue(world.InsertRune(player.Id, sword.Id, Gear.Data.FindRune("spark")));

            Assert.AreEqual("storm", sword.Runeword?.Id);
            Assert.AreEqual(0, player.Inventory.Runes.Count);
            // Increases add up: the wind rune's 6% and the runeword's 10%.
            Assert.AreEqual(2 * (1 + 0.06f + 0.10f), player.Stats.AttacksPerSecond, 0.01f, "the runeword counts once it is worn");
        }

        [Test]
        public void DiscardedItemsLandInFrontAndAreNotPickedStraightBackUp()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var ring = world.DropItem(Gear.Plain("copper_ring"), player.Position).Item;
            world.Step();
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);

            Assert.IsTrue(world.DiscardItem(player.Id, ring.Id));
            Assert.AreEqual(0, player.Inventory.Items.Count);
            var loot = world.GroundLoot.Single();
            Assert.Greater(loot.Position.X, player.Position.X + 1);

            world.MovePlayer(player.Id, loot.Position, new Vec2(1, 0), 10);
            world.Step();
            Assert.AreEqual(0, player.Inventory.Items.Count, "too soon");
            for (int i = 0; i < World.Ticks(5); i++) world.Step();
            Assert.AreEqual(1, player.Inventory.Items.Count);
        }

        [Test]
        public void ItemRarityOnGearImprovesDropsFromKills()
        {
            double RareShare(params Item[] gear)
            {
                var equipment = new Equipment();
                foreach (var item in gear) equipment.Equip(item);
                var world = Room(monsters: Pinata);
                var player = world.AddPlayer("a", equipment);

                // Every item seen as it lands, since the floor only keeps the newest few hundred.
                var dropped = new Dictionary<int, Rarity>();
                for (int i = 0; i < 1500; i++)
                {
                    Kill(world, player, Pinata.Find("pinata"));
                    foreach (var item in world.GroundLoot.Where(l => l.Item != null).Select(l => l.Item).Concat(player.Inventory.Items))
                    {
                        dropped[item.Id] = item.Rarity;
                    }
                }
                return dropped.Values.Count(r => r >= Rarity.Rare) / (double)dropped.Count;
            }

            var luckyRing = Gear.Plain("copper_ring", 0, Rarity.Uncommon, Gear.Affix("lucky", 200));
            double plain = RareShare(Gear.Plain("copper_sword"));
            double lucky = RareShare(Gear.Plain("copper_sword"), luckyRing);

            // 8% of drops are rare or better without the bonus, 15% with +200%.
            Assert.That(plain, Is.InRange(0.06, 0.10));
            Assert.That(lucky, Is.InRange(0.12, 0.18));
        }
            [Test]
        public void BossesHaveLegendariesThatNeverDropAtRandom()
        {
            Assert.IsTrue(Gear.Data.Success, string.Join("\n", Gear.Data.Errors));
            var generator = new LootGenerator(Gear.Data);
            foreach (string boss in new[] { "cave_warden", "crystal_queen", "lava_titan" })
            {
                var legendaries = generator.LegendariesOf(boss).ToList();
                Assert.AreEqual(2, legendaries.Count, boss);
                Assert.IsTrue(legendaries.All(b => b.LegendaryChance > 0 && b.RequiredLevel == b.Level), boss);
            }
            for (int level = 1; level <= 40; level++) Assert.IsFalse(generator.BasesAt(level).Any(b => b.IsLegendary), $"level {level}");

            var maul = Gear.Data.FindBase("warden_maul");
            var item = generator.RollLegendary(maul, new GameRandom(3));
            Assert.AreEqual(Rarity.Legendary, item.Rarity);
            Assert.AreEqual(maul.MaxSockets, item.Sockets.Count, "every socket open");
            Assert.AreEqual(0, item.Affixes.Count);
            Assert.AreEqual(maul.Implicits.Count, item.Implicits.Count);
            Assert.AreEqual("[전설] 수호자의 바위망치 (레벨 10)", ItemText.Describe(item)[0]);

            var broken = TestLoot.Parse(TestLoot.Bases.Replace("weight\n", "weight,tool,legendary,chance,require\n") + "odd,이상한 검,weapon,1,melee,Damage+1~2,1,0,,dummy,0,\n");
            TestLoot.AssertError(broken, "chance");
        }

        [Test]
        public void ABossDropsItsLegendariesAndEveryoneLevelsUp()
        {
            var bases = TestLoot.Bases.Replace("weight\n", "weight,tool,legendary,chance,require\n") + "dummy_blade,허수아비의 칼,weapon,10,melee,Damage+20,2,0,,dummy,100,\n";
            var loot = TestLoot.Parse(bases);
            Assert.IsTrue(loot.Success, string.Join("\n", loot.Errors));
            var world = new World(Maps.Room(40), TileMap.Center(20, 20), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, loot);
            var definition = Bosses.Parse(
                Bosses.BossesHeader.TrimEnd('\n') + ",drop_count,drop_level,drop_rarity,rune_count\n" +
                "dummy,허수아비,10,0,0,2,1,,1,3,10,0,10,rare,0\n",
                Bosses.AttacksHeader + "dummy,a,가,,circle,nearest,1,1,1,,,,1,,,1,1,\n").Find("dummy");
            var boss = world.PlaceBoss(definition, TileMap.Center(20, 20), 8);
            var alice = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            var bob = world.AddPlayer("b", null);
            world.MovePlayer(alice.Id, boss.Position + new Vec2(0, -2), new Vec2(0, 1), 10);
            world.MovePlayer(bob.Id, boss.Position + new Vec2(0, 2), new Vec2(0, -1), 10);
            float health = alice.Stats.MaxHealth;

            world.SetAttacking(alice.Id, true);
            var events = new List<SimEvent>();
            for (int i = 0; i < 300 && !boss.IsDead; i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }

            Assert.IsTrue(boss.IsDead);
            var blades = world.GroundLoot.Where(l => l.Item != null).Select(l => l.Item).Concat(alice.Inventory.Items).Concat(bob.Inventory.Items)
                .Where(i => i.Base.Id == "dummy_blade").ToList();
            Assert.AreEqual(2, blades.Count, "one for each fighter at 100%");
            Assert.IsTrue(blades.All(i => i.Rarity == Rarity.Legendary));
            foreach (var player in new[] { alice, bob })
            {
                Assert.AreEqual(Leveling.ForBoss(definition), player.Experience, "the boss's experience to each fighter");
                Assert.AreEqual(Leveling.LevelFor(300), player.Level);
                Assert.IsTrue(events.Count(e => e.Type == SimEventType.LevelUp && e.Source == player.Id) == player.Level - 1);
            }
            Assert.AreEqual(health + (alice.Level - 1) * Leveling.HealthPerLevel, alice.Stats.MaxHealth, 0.01f, "a little more health a level");
        }

        [Test]
        public void MonstersGiveExperienceToEveryoneNearAndMoreAfterABoss()
        {
            // Wide enough for one player to stand beyond the share range of the other.
            var world = Room(44);
            var near = world.AddPlayer("near", Gear.Wearing("mithril_sword"));
            var far = world.AddPlayer("far", null);
            Moves.Walk(world, far, near.Position + new Vec2(Leveling.ShareRange + 2, 0));
            var slime = Monsters.Shipped.Find("slime");
            Assert.That(Vec2.Distance(far.Position, near.Position), Is.GreaterThan(Leveling.ShareRange));
            var target = world.SpawnEnemy(slime, near.Position + new Vec2(0, -1));
            world.SetAttacking(near.Id, true);
            for (int i = 0; i < 60 && !target.IsDead; i++) world.Step();
            Assert.IsTrue(target.IsDead);
            Assert.AreEqual(Leveling.ForMonster(slime.Level), near.Experience);
            Assert.AreEqual(0, far.Experience, "too far away");

            Assert.AreEqual(3 * 10 + 6, Leveling.ForMonster(Math.Max(slime.Level, 10)), "after the first boss");
            Assert.AreEqual(1, Leveling.LevelFor(0));
            Assert.AreEqual(5, Leveling.LevelFor(Leveling.TotalFor(5)));
            Assert.AreEqual(4, Leveling.LevelFor(Leveling.TotalFor(5) - 1));
            Assert.AreEqual(Leveling.MaxLevel, Leveling.LevelFor(int.MaxValue));
            Assert.AreEqual(5, Gear.Data.FindBase("iron_sword").RequiredLevel);
            Assert.AreEqual(15, Gear.Data.FindBase("mithril_sword").RequiredLevel);
            Assert.AreEqual(1, Gear.Data.FindBase("copper_sword").RequiredLevel);
        }
}
}
