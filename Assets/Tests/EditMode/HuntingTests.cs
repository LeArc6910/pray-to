using System;
using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // The hunt's top end (Docs/Loot.md, 균열 전설과 세트): tier-5 affixes only past level 50, the rift's own
    // legendaries, and gear sets. And mana, which skills cost (Docs/Classes.md, 마나).
    public class HuntingTests
    {
        static ClassDefinition Class(string id) => Classes.Shipped.Find(id);

        static World Room() =>
            new World(Maps.Room(20), TileMap.Center(10, 10), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, loot: Gear.Data);

        [Test]
        public void SkillsCostManaAndItComesBackBySecond()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, Class("fire_mage"));
            int fireball = player.Skills.ToList().FindIndex(s => s.Id == "fireball");
            var skill = player.Skills[fireball];
            Assert.Greater(skill.Mana, 0);
            Assert.AreEqual(PlayerStats.BaseMana, player.Mana);

            Assert.IsTrue(world.UseSkill(player.Id, fireball, Vec2.Zero));
            Assert.AreEqual(PlayerStats.BaseMana - skill.Mana, player.Mana, 0.01f);

            player.Mana = skill.Mana - 2;
            Run(world, skill.Cooldown + 0.05f);
            player.Mana = skill.Mana - 0.5f;
            Assert.IsFalse(world.UseSkill(player.Id, fireball, Vec2.Zero), "not mana enough");
            Assert.AreEqual(0, player.CooldownTicks(fireball), "a refused skill does not start its cooldown");
            Run(world, 0.5f);
            Assert.AreEqual(skill.Mana - 0.5f + PlayerStats.BaseManaRegen * 0.5f, player.Mana, 0.05f, "mana comes back at its rate");
            Assert.IsTrue(world.UseSkill(player.Id, fireball, Vec2.Zero));
        }

        [Test]
        public void AManaPotionFillsHalfTheManaAtOnce()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, Class("fire_mage"));
            var potion = Gear.Data.FindConsumable("mana_potion");
            Assert.IsTrue(potion.RestoresMana);
            player.Inventory.AddConsumable(potion, 2);

            Assert.IsFalse(world.UseConsumable(player.Id, potion), "no use at full mana");
            player.Mana = 10;
            float health = player.Health;
            Assert.IsTrue(world.UseConsumable(player.Id, potion));
            Assert.AreEqual(10 + PlayerStats.BaseMana * potion.Heal / 100f, player.Mana, 0.01f);
            Assert.AreEqual(health, player.Health, "it gives no health");
            Assert.AreEqual(1, player.Inventory.CountOf(potion));
            // The screen hears of it, to play the drink (2026-11-09).
            var drank = world.TakeEvents().Single(e => e.Type == SimEventType.ManaRestored);
            Assert.AreEqual(player.Id, drank.Source);
            Assert.AreEqual(Gear.Data.Consumables.ToList().IndexOf(potion), drank.Target);
            Assert.AreEqual((int)Math.Round(PlayerStats.BaseMana * potion.Heal / 100f), drank.Amount);
        }

        [Test]
        public void GearAndPassivesAddMana()
        {
            var plain = PlayerStats.From(new Equipment());
            Assert.AreEqual(PlayerStats.BaseMana, plain.MaxMana);
            Assert.AreEqual(PlayerStats.BaseManaRegen, plain.ManaRegen);

            var equipment = new Equipment();
            equipment.Equip(Gear.Plain("copper_helmet", affixes: new[] { Gear.Affix("wise", 12), Gear.Affix("spring", 1) }));
            var worn = PlayerStats.From(equipment);
            Assert.AreEqual(PlayerStats.BaseMana + 12, worn.MaxMana);
            Assert.AreEqual(PlayerStats.BaseManaRegen + 1, worn.ManaRegen);

            var world = Room();
            var player = world.AddPlayer("a", null, Class("fire_mage"));
            player.Experience = Leveling.TotalFor(3);
            Assert.IsTrue(world.AllocatePassive(player.Id, Classes.Shipped.FindPassive("int1")));
            Assert.AreEqual(PlayerStats.BaseMana + 15, player.Stats.MaxMana);
            Assert.AreEqual(player.Stats.MaxMana, player.Mana, 0.01f, "a bigger pool keeps the share that was full");
        }

        [Test]
        public void TheSnapshotCarriesTheManaAndSkillsListTheirCost()
        {
            var snapshot = SnapshotMessage.Decode(new SnapshotMessage(
                1,
                new SelfState(0, new Vec2(1, 1), 50, 100, 5, mana: 37.5f, maxMana: 140),
                new EntityState[0], new ProjectileState[0], new HazardState[0], new SimEvent[0]).Encode());
            Assert.AreEqual(37.5f, snapshot.Self.Mana);
            Assert.AreEqual(140, snapshot.Self.MaxMana);
            Assert.IsTrue(SkillText.Numbers(Classes.Skill("meteor")).Contains("마나 30"));
            Assert.IsTrue(Classes.Shipped.Skills.All(s => s.Mana > 0), "every skill has a cost");
        }

        [Test]
        public void TheFifthTierOfAnAffixNeedsLevelFifty()
        {
            var generator = new LootGenerator(Gear.Data);
            var random = new GameRandom(5);
            int Fifth(int level) => Enumerable.Range(0, 3000).Select(_ => generator.RollItem(level, random, 0, Rarity.Rare))
                .Sum(item => item.Affixes.Count(a => a.Tier.Number == 5));
            Assert.AreEqual(0, Fifth(45), "none from the bosses' levels");
            Assert.Greater(Fifth(50), 0, "a rift of tier 5 rolls at 50");
            Assert.IsTrue(Gear.Data.Affixes.All(a => a.Tiers.Count == 5), "every affix has a fifth tier");
        }

        [Test]
        public void RiftsHaveLegendariesOfTheirOwn()
        {
            var generator = new LootGenerator(Gear.Data);
            var own = generator.LegendariesOf(World.RiftLegendaries).ToList();
            Assert.AreEqual(7, own.Count);
            Assert.IsTrue(own.All(b => (b.Level == 45 || b.Level == 48) && b.RequiredLevel == b.Level));
            // Two at 48 for the levels past the void gear (2026-11-11, the content audit: nothing new from 46 to 50).
            CollectionAssert.AreEquivalent(new[] { "rift_scepter", "rift_loop" }, own.Where(b => b.Level == 48).Select(b => b.Id));
            var edge = generator.RollLegendary(own.Single(b => b.Id == "rift_edge"), new GameRandom(1));
            Assert.AreEqual(Rarity.Legendary, edge.Rarity);
        }

        [Test]
        public void WearingASetsPiecesTogetherGivesMore()
        {
            var set = Gear.Data.Sets.Single(s => s.Id == "pilgrim");
            var pieces = Gear.Data.Bases.Where(b => b.Set == set).ToList();
            Assert.AreEqual(3, pieces.Count);
            var generator = new LootGenerator(Gear.Data);
            var random = new GameRandom(2);
            var equipment = new Equipment();
            equipment.Equip(generator.RollLegendary(pieces.Single(b => b.Slot == ItemSlot.Helmet), random));
            var one = PlayerStats.From(equipment);
            equipment.Equip(generator.RollLegendary(pieces.Single(b => b.Slot == ItemSlot.Chest), random));
            var two = PlayerStats.From(equipment);
            equipment.Equip(generator.RollLegendary(pieces.Single(b => b.Slot == ItemSlot.Boots), random));
            var three = PlayerStats.From(equipment);

            float chestHealth = equipment[ItemSlot.Chest].Implicits.Where(m => m.Stat == StatType.MaxHealth).Sum(m => m.Value);
            Assert.AreEqual(one.MaxHealth + chestHealth + 40, two.MaxHealth, 0.01f, "two pieces: +40 health");
            Assert.AreEqual(two.CritChance + 6, three.CritChance, 0.01f, "three: +6 crit");
            Assert.IsTrue(ItemText.Describe(equipment[ItemSlot.Helmet]).Any(l => l.Contains("세트: 공허 순례자")));
        }

        [Test]
        public void ASetNamedByNoSetIsAnError()
        {
            var data = LootSheets.Parse(
                "id,name,slot,level,tags,implicit,sockets,weight,tool,legendary,chance,require,set\nhood,두건,helmet,1,,Armor+1,0,10,,,,,ghost\n",
                TestLoot.Affixes, TestLoot.Runes, TestLoot.Runewords, "", "", "", "id,name,pieces,modifiers\npilgrim,순례자,2,Damage+1\n");
            Assert.IsTrue(data.Errors.Any(e => e.Contains("ghost")), string.Join("\n", data.Errors));
        }
    }
}
