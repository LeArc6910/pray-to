using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The hunt's top end (Docs/Loot.md, 균열 전설과 세트): tier-5 affixes only past level 50, the rift's own
    // legendaries, and gear sets.
    public class HuntingTests
    {
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
            Assert.AreEqual(5, own.Count);
            Assert.IsTrue(own.All(b => b.Level == 45 && b.RequiredLevel == 45));
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
