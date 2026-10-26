using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class LootSheetsTests
    {
        [Test]
        public void ShippedSheetsAreValid()
        {
            var data = ShippedLoot.Load();

            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
            {
                Assert.IsTrue(data.Bases.Any(b => b.Slot == slot && b.Level == 1), $"{slot} has nothing to drop at level 1");
            }
        }

        [Test]
        public void ShippedAffixesCanFillAnEpicItemInEverySlot()
        {
            var data = ShippedLoot.Load();
            var epic = new LootConfig().Epic;

            foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
            {
                int prefixes = data.Affixes.Count(a => a.Kind == AffixKind.Prefix && a.AllowedOn(slot));
                int suffixes = data.Affixes.Count(a => a.Kind == AffixKind.Suffix && a.AllowedOn(slot));
                int room = Math.Min(prefixes, epic.MaxPerKind) + Math.Min(suffixes, epic.MaxPerKind);
                Assert.GreaterOrEqual(room, epic.MinAffixes, $"{slot}: {prefixes} prefixes, {suffixes} suffixes");
            }
        }

        [Test]
        public void TierRowsShareTheFirstRowsKindNameAndSlots()
        {
            var data = TestLoot.Parse();
            var sharp = data.FindAffix("sharp");

            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            Assert.AreEqual(AffixKind.Prefix, sharp.Kind);
            Assert.AreEqual("날카로운", sharp.Name);
            CollectionAssert.AreEqual(new[] { ItemSlot.Weapon }, sharp.Slots);
            Assert.AreEqual(2, sharp.Tiers.Count);
            Assert.AreEqual(2, sharp.Tiers[1].Number);
            Assert.AreEqual(10, sharp.Tiers[1].Level);
            Assert.AreEqual("Damage+4~5", sharp.Tiers[1].Modifier.ToString());
        }

        [Test]
        public void SlotGroupsExpand()
        {
            var data = TestLoot.Parse(affixes: TestLoot.AffixesHeader + "tough,prefix,튼튼한,armor|jewelry,1,1,Armor+1\n");

            CollectionAssert.AreEquivalent(
                new[] { ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Boots, ItemSlot.Ring, ItemSlot.Amulet },
                data.FindAffix("tough").Slots);
        }

        [TestCase("bases", "axe,도끼,offhand,1,,Damage+5,2,1", "item_bases.csv 2행", "offhand")]
        [TestCase("bases", "sword,검,weapon,1,,Damage+5,9,1", "item_bases.csv 2행", "sockets")]
        [TestCase("bases", "sword,검,weapon,1,,Damage+5,2,1\nsword,검,weapon,1,,Damage+5,2,1", "item_bases.csv 3행", "이미")]
        [TestCase("affixes", "sharp,prefix,날카로운,weapon,10,1,Damage+1\nsharp,,,,5,1,Damage+2", "affixes.csv 3행", "순서")]
        [TestCase("affixes", "sharp,prefix,날카로운,weapon,1,1,Damage+1\nsharp,,,,10,1,Armor+2", "affixes.csv 3행", "능력치")]
        [TestCase("affixes", "sharp,prefix,날카로운,weapon,1,1,Damage+1\nsharp,suffix,,,10,1,Damage+2", "affixes.csv 3행", "kind")]
        [TestCase("runes", "ember,불씨 룬,1,1,Fire:Damage+1~2%,,", "runes.csv 2행", "하나의 값")]
        [TestCase("runewords", "storm,폭풍,weapon,wind|nope,Damage+1", "runewords.csv 2행", "nope")]
        [TestCase("runewords", "crown,왕관,helmet,ember|ember|ember,Damage+1", "runewords.csv 2행", "소켓")]
        public void ReportsBadRowsWithSheetAndLine(string sheet, string rows, string where, string what)
        {
            var data = sheet switch
            {
                "bases" => TestLoot.Parse(bases: TestLoot.BasesHeader + rows),
                "affixes" => TestLoot.Parse(affixes: TestLoot.AffixesHeader + rows),
                "runes" => TestLoot.Parse(runes: TestLoot.RunesHeader + rows),
                _ => TestLoot.Parse(runewords: TestLoot.RunewordsHeader + rows),
            };

            TestLoot.AssertError(data, where, what);
        }

        [Test]
        public void ReportsMissingColumns()
        {
            var data = TestLoot.Parse(runes: "id,name,level\nember,불씨 룬,1\n");

            TestLoot.AssertError(data, "runes.csv", "weight", "weapon");
        }

        [Test]
        public void GeneratorRefusesBrokenData()
        {
            var data = TestLoot.Parse(bases: TestLoot.BasesHeader + "axe,도끼,offhand,1,,Damage+5,2,1\n");

            Assert.Throws<ArgumentException>(() => new LootGenerator(data));
        }
    }
}
