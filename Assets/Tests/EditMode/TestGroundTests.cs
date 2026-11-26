using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The test ground (Docs/TestGround.md): the hit log, "죽지 않기", getting up where chosen, the boss brought back,
    // monsters made as the field's, and the panel's gear and levels (TestKit).
    public class TestGroundTests
    {
        static World Room(bool testGround = true)
        {
            var world = new World(Maps.Room(30), TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data);
            world.TestGround = testGround;
            return world;
        }

        [Test]
        public void TheHitLogNotesWhoWhatAndTheArmorWeighed()
        {
            var world = Room();
            var player = world.AddPlayer("a", HitTable.Wearing(Gear.Data, 2));
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(4, 0));
            int taken = world.Hurt(player, slime, 40);
            var hit = world.HitsOn(player.Id).Single();
            Assert.AreEqual("슬라임", hit.Who);
            Assert.AreEqual("몸통 공격", hit.What);
            Assert.AreEqual(taken, hit.Amount);
            Assert.AreEqual(40, hit.Raw, 1e-4f);
            Assert.IsTrue(hit.Armored);
            Assert.AreEqual(1, hit.Band, "a slime is of the copper band");
            Assert.AreEqual(DamageBands.ArmorOf(1), hit.BandArmor);
            Assert.AreEqual(player.Stats.Armor, hit.Armor, 1e-4f);
            Assert.AreEqual(DamageBands.Share(player.Stats.Armor, 1), hit.Share, 1e-5f);
            Assert.AreEqual(taken / player.Stats.MaxHealth, hit.Fraction, 1e-5f);
            Assert.AreEqual(HitKind.Normal, hit.Kind);
            Assert.AreEqual(DamageBands.TargetOf(HitKind.Normal).Aim, hit.Aim);
            Assert.AreEqual(1, world.HitCount(player.Id));
        }

        [Test]
        public void TheLogKeepsTheLastHitsAndOnlyInTheTestGround()
        {
            var world = Room();
            var player = world.AddPlayer("a", HitTable.Wearing(Gear.Data, 5));
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(4, 0));
            for (int i = 0; i < World.HitLogSize + 5; i++) world.Hurt(player, slime, 2);
            Assert.AreEqual(World.HitLogSize, world.HitsOn(player.Id).Count);
            Assert.AreEqual(World.HitLogSize + 5, world.HitCount(player.Id));
            world.ClearHits(player.Id);
            Assert.AreEqual(0, world.HitsOn(player.Id).Count);

            var ordinary = Room(testGround: false);
            var other = ordinary.AddPlayer("b", HitTable.Wearing(Gear.Data, 1));
            var bat = ordinary.SpawnEnemy(Monsters.Shipped.Find("slime"), other.Position + new Vec2(4, 0));
            ordinary.Hurt(other, bat, 10);
            Assert.AreEqual(0, ordinary.HitsOn(other.Id).Count, "nothing is kept in an ordinary world");
            ordinary.SetUndying(other.Id, true);
            Assert.IsFalse(ordinary.Undying(other.Id), "nor can a player be kept from falling there");
        }

        [Test]
        public void AnUndyingPlayerStaysAtOneAndTheBlowIsStillWritten()
        {
            var world = Room();
            var player = world.AddPlayer("a", new Equipment());
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(4, 0));
            world.SetUndying(player.Id, true);
            int taken = world.Hurt(player, slime, 100000);
            Assert.IsFalse(player.IsDead);
            Assert.AreEqual(1, player.Health, 1e-4f);
            var hit = world.HitsOn(player.Id).Last();
            Assert.IsTrue(hit.Saved);
            Assert.AreEqual(taken, hit.Amount, "the log shows the whole blow");
            Assert.Greater(hit.Fraction, 1);

            world.SetUndying(player.Id, false);
            world.Hurt(player, slime, 100000);
            Assert.IsTrue(player.IsDead);
        }

        [Test]
        public void AFallenPlayerGetsUpWhereTheTestGroundSays()
        {
            var world = Room();
            var player = world.AddPlayer("a", new Equipment());
            var spot = TileMap.Center(5, 20);
            world.SetRising(player.Id, spot);
            world.Hurt(player, 100000);
            Assert.IsTrue(player.IsDead);
            for (int i = 0; i < World.Ticks(30) && player.IsDead; i++) world.Step();
            Assert.IsFalse(player.IsDead);
            Assert.AreEqual(spot.X, player.Position.X, 1e-3f);
            Assert.AreEqual(spot.Y, player.Position.Y, 1e-3f);
        }

        [Test]
        public void ABossFelledComesBackAtOnceAndOneAwakeGoesBackToSleep()
        {
            var world = Room();
            var home = TileMap.Center(15, 24);
            var boss = world.PlaceBoss(Bosses.Shipped.Find("cave_warden"), home, 4);
            Assert.AreEqual(home, world.BossHome("cave_warden"));
            world.Hurt(boss, (int)boss.MaxHealth + 10);
            world.Step();
            Assert.IsFalse(world.Bosses.Any(b => b.Definition.Id == "cave_warden" && !b.IsDead), "felled");
            Assert.AreEqual(home, world.BossHome("cave_warden"), "its arena is known while it waits");
            Assert.IsTrue(world.ReviveBoss("cave_warden"));
            var back = world.Bosses.Single(b => b.Definition.Id == "cave_warden");
            Assert.IsFalse(back.IsDead);
            Assert.AreEqual(back.Definition.Health, back.Health);
            Assert.IsFalse(world.ReviveBoss("no_such_boss"));
        }

        [Test]
        public void MonstersAreMadeAsTheFieldsWithTheirZone()
        {
            var world = Room();
            var player = world.AddPlayer("a", new Equipment());
            var at = player.Position + new Vec2(5, 0);
            var one = world.SpawnForTest(Monsters.Shipped.Find("slime"), at, EliteRank.None);
            Assert.AreEqual(1, one.Count);
            var pack = world.SpawnForTest(Monsters.Shipped.Find("slime"), at, EliteRank.None, 4);
            Assert.AreEqual(4, pack.Count);
            Assert.IsNotNull(world.PackOf(pack[0]));
            var leader = world.SpawnForTest(Monsters.Shipped.Find("cave_beetle"), at, EliteRank.Leader);
            Assert.AreEqual(EliteRank.Leader, leader[0].Elite);
            Assert.AreEqual(3, leader.Count, "the leader and two of its kind");
            var champions = world.SpawnForTest(Monsters.Shipped.Find("cave_beetle"), at, EliteRank.Champion);
            Assert.IsTrue(champions.All(e => e.Elite == EliteRank.Champion));
            Assert.Greater(champions.Count, 1);
            int standing = world.Enemies.Count(e => !e.IsDead);
            Assert.AreEqual(standing, world.ClearMonstersNear(player.Position, 30));
            Assert.AreEqual(0, world.Enemies.Count(e => !e.IsDead));
        }

        [Test]
        public void AnEliteHitIsWrittenAsOne()
        {
            var world = Room();
            var player = world.AddPlayer("a", HitTable.Wearing(Gear.Data, 1));
            var leader = world.SpawnForTest(Monsters.Shipped.Find("cave_beetle"), player.Position + new Vec2(5, 0), EliteRank.Leader)[0];
            world.Hurt(player, leader, 30);
            var hit = world.HitsOn(player.Id).Single();
            StringAssert.EndsWith(" 우두머리", hit.Who);
            Assert.AreEqual(HitKind.Elite, hit.Kind);
        }

        [Test]
        public void TheMiddleCommonBandSetIsTheHitTablesSet()
        {
            for (int band = 1; band <= DamageBands.Bands; band++)
            {
                var set = TestKit.BandSet(Gear.Data, band, "sword", Rarity.Common, TestKit.Roll.Middle, 0);
                Assert.AreEqual(6, set.Count, $"band {band}");
                var worn = new Equipment();
                foreach (var item in set) worn.Equip(item);
                var table = PlayerStats.From(HitTable.Wearing(Gear.Data, band));
                Assert.AreEqual(table.Armor, PlayerStats.From(worn).Armor, 1e-4f, $"band {band}");
                Assert.AreEqual(table.MaxHealth, PlayerStats.From(worn).MaxHealth, 1e-4f, $"band {band}");
                Assert.IsTrue(set.All(i => i.Base.Id.StartsWith(DamageBands.TierNames[band - 1] + "_")));
            }
            var bow = TestKit.BandSet(Gear.Data, 3, "bow", Rarity.Common, TestKit.Roll.Middle, 0);
            Assert.AreEqual("mithril_bow", bow[0].Base.Id);
        }

        [Test]
        public void TheRollRarityAndUpgradeShowInTheNumbers()
        {
            var chest = Gear.Data.FindBase("iron_armor");
            float Armor(Item item) => PlayerStats.From(Wear(item)).Armor;
            var low = TestKit.Make(Gear.Data, chest, Rarity.Common, TestKit.Roll.Low, 0);
            var high = TestKit.Make(Gear.Data, chest, Rarity.Common, TestKit.Roll.High, 0);
            var range = chest.Implicits.First(r => r.Stat == StatType.Armor);
            Assert.AreEqual(range.Min, low.Implicits.First(m => m.Stat == StatType.Armor).Value);
            Assert.AreEqual(range.Max, high.Implicits.First(m => m.Stat == StatType.Armor).Value);
            var upgraded = TestKit.Make(Gear.Data, chest, Rarity.Common, TestKit.Roll.High, Item.MaxUpgrade);
            Assert.AreEqual(Item.MaxUpgrade, upgraded.Upgrade);
            Assert.Greater(Armor(upgraded), Armor(high));
            Assert.AreEqual(chest.MaxSockets, high.Sockets.Count, "every socket open for runes");

            Assert.AreEqual(0, TestKit.Make(Gear.Data, chest, Rarity.Common, TestKit.Roll.High, 0).Affixes.Count);
            var rule = new LootConfig().RuleFor(Rarity.Epic);
            var epic = TestKit.Make(Gear.Data, chest, Rarity.Epic, TestKit.Roll.High, 0);
            // As many as it may have, as far as the chest's affixes go.
            Assert.That(epic.Affixes.Count, Is.InRange(rule.MinAffixes, rule.MaxAffixes));
            Assert.Less(TestKit.Make(Gear.Data, chest, Rarity.Uncommon, TestKit.Roll.Low, 0).Affixes.Count, epic.Affixes.Count);
            Assert.AreEqual(Rarity.Epic, epic.Rarity);
            // The same press makes the same item.
            var again = TestKit.Make(Gear.Data, chest, Rarity.Epic, TestKit.Roll.High, 0);
            CollectionAssert.AreEqual(epic.Affixes.Select(a => a.Affix.Id), again.Affixes.Select(a => a.Affix.Id));
            // A boss's own item stays legendary, with no affixes.
            var maul = TestKit.Make(Gear.Data, Gear.Data.FindBase("warden_maul"), Rarity.Epic, TestKit.Roll.High, 2);
            Assert.AreEqual(Rarity.Legendary, maul.Rarity);
            Assert.AreEqual(0, maul.Affixes.Count);
        }

        static Equipment Wear(Item item)
        {
            var equipment = new Equipment();
            equipment.Equip(item);
            return equipment;
        }

        [Test]
        public void RunesGoIntoTheFirstOpenSocketAndComeOut()
        {
            var world = Room();
            var player = world.AddPlayer("a", new Equipment());
            foreach (var item in TestKit.BandSet(Gear.Data, 2, "sword", Rarity.Common, TestKit.Roll.Middle, 0)) world.WearForTest(player, item);
            var rune = Gear.Data.Runes.First();
            var into = TestKit.InsertRune(player, rune, Gear.Data.Runewords);
            Assert.IsNotNull(into);
            Assert.AreEqual(rune, into.Sockets.First(s => s != null));
            Assert.AreEqual(1, TestKit.ClearRunes(player));
            Assert.IsTrue(player.Equipment.Items.All(i => i.Sockets.All(s => s == null)));
        }

        [Test]
        public void ALevelIsSetWithItsPointsSpentAsAsked()
        {
            var world = Room();
            var player = world.AddPlayer("a", new Equipment(), ClassRules.Choose(Classes.Shipped, new[] { "warrior" }, 3).ToArray());
            TestKit.SetLevel(player, Classes.Shipped, Gear.Data, 20, TestKit.Build.Points);
            Assert.AreEqual(20, player.Level);
            Assert.AreEqual(19, player.PassivePoints, "all to spend by hand");
            Assert.Greater(player.SkillPoints, 0);

            TestKit.SetLevel(player, Classes.Shipped, Gear.Data, 20, TestKit.Build.Attack);
            Assert.AreEqual(0, player.PassivePoints);
            float attackHealth = player.Stats.MaxHealth, attackArmor = player.Stats.Armor;
            Assert.AreEqual(player.Stats.MaxHealth, player.Health, 1e-3f, "whole again");
            TestKit.SetLevel(player, Classes.Shipped, Gear.Data, 20, TestKit.Build.Defense);
            Assert.AreEqual(0, player.PassivePoints);
            Assert.Greater(player.Stats.MaxHealth + player.Stats.Armor, attackHealth + attackArmor, "the defensive build lives longer");

            TestKit.SetLevel(player, Classes.Shipped, Gear.Data, 3, TestKit.Build.Points);
            Assert.AreEqual(3, player.Level);
            Assert.AreEqual(0, player.Passives.Count, "going down forgets what was taken");
        }

        [Test]
        public void RefillingMakesThePlayerWholeWithPotions()
        {
            var world = Room();
            var player = world.AddPlayer("a", new Equipment());
            var potion = Gear.Data.Consumables.First(c => c.Id == "healing_potion");
            world.Hurt(player, player.Health / 2);
            world.RefillForTest(player, potion, 5);
            Assert.AreEqual(player.Stats.MaxHealth, player.Health, 1e-4f);
            Assert.AreEqual(5, player.Inventory.CountOf(potion));
        }
    }
}
