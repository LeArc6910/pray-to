using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ItemTests
    {
        static readonly LootData Data = ShippedLoot.Load();

        // An item with the lowest implicit rolls, no affixes and empty sockets.
        static Item Plain(string baseId, int sockets, Rarity rarity = Rarity.Common, params ItemAffix[] affixes)
        {
            var itemBase = Data.FindBase(baseId);
            var implicits = itemBase.Implicits.Select(r => new Modifier(r.Stat, r.Kind, r.Min, r.Tags)).ToList();
            return new Item(itemBase, itemBase.Level, rarity, implicits, affixes, sockets);
        }

        static ItemAffix Affix(string id, int value)
        {
            var tier = Data.FindAffix(id).Tiers[0];
            return new ItemAffix(tier, new Modifier(tier.Modifier.Stat, tier.Modifier.Kind, value, tier.Modifier.Tags));
        }

        [Test]
        public void RunesGiveStatsForTheKindOfSlotTheySitIn()
        {
            var sword = Plain("copper_sword", 1);
            var armor = Plain("copper_armor", 1);
            var ring = Plain("copper_ring", 1);

            sword.TryInsertRune(0, Data.FindRune("ember"), Data.Runewords);
            armor.TryInsertRune(0, Data.FindRune("ember"), Data.Runewords);
            ring.TryInsertRune(0, Data.FindRune("ember"), Data.Runewords);

            CollectionAssert.Contains(sword.Modifiers().ToList(), Mods.Of("Fire:Damage+12%"));
            CollectionAssert.Contains(armor.Modifiers().ToList(), Mods.Of("Armor+4"));
            CollectionAssert.Contains(ring.Modifiers().ToList(), Mods.Of("Fire:Damage+6%"));
        }

        [Test]
        public void EachSocketTakesOneRune()
        {
            var sword = Plain("copper_sword", 2);
            var ember = Data.FindRune("ember");

            Assert.IsTrue(sword.TryInsertRune(0, ember, Data.Runewords));
            Assert.IsFalse(sword.TryInsertRune(0, ember, Data.Runewords));
            Assert.IsFalse(sword.TryInsertRune(2, ember, Data.Runewords));
            CollectionAssert.AreEqual(new[] { ember, null }, sword.Sockets);
        }

        [Test]
        public void RunewordNeedsExactlyItsRunesInOrderOnACommonItem()
        {
            Item Socketed(int sockets, Rarity rarity, params string[] runes)
            {
                var item = Plain("copper_sword", sockets, rarity);
                for (int i = 0; i < runes.Length; i++) item.TryInsertRune(i, Data.FindRune(runes[i]), Data.Runewords);
                return item;
            }

            var storm = Socketed(2, Rarity.Common, "wind", "spark");
            Assert.AreEqual("storm", storm.Runeword?.Id);
            CollectionAssert.Contains(storm.Modifiers().ToList(), Mods.Of("Lightning:Damage+40%"));
            Assert.AreEqual("구리 검 「폭풍」", ItemText.Name(storm));

            Assert.IsNull(Socketed(2, Rarity.Common, "spark", "wind").Runeword);
            Assert.IsNull(Socketed(2, Rarity.Rare, "wind", "spark").Runeword);
            Assert.IsNull(Socketed(3, Rarity.Common, "wind", "spark").Runeword);
        }

        [Test]
        public void DescribesAnItemInKorean()
        {
            var item = Plain("copper_sword", 2, Rarity.Uncommon, Affix("swift", 4), Affix("sharp", 2));
            item.TryInsertRune(0, Data.FindRune("ember"), Data.Runewords);

            CollectionAssert.AreEqual(
                new List<string>
                {
                    "[고급] 신속의 날카로운 구리 검 (레벨 1)",
                    "  피해 +4",
                    "  · 피해 +2",
                    "  · 공격 속도 4% 증가",
                    "  룬 소켓 [불씨 룬] [ ]",
                    "  · 화염 피해 12% 증가 (불씨 룬)",
                },
                ItemText.Describe(item));
        }

        [Test]
        public void RareItemsKeepTheBaseName()
        {
            var item = Plain("copper_sword", 0, Rarity.Rare, Affix("sharp", 2), Affix("swift", 4), Affix("precise", 1));

            Assert.AreEqual("구리 검", ItemText.Name(item));
        }

        [Test]
        public void ItemStatsAddUpOnAStatSheet()
        {
            var item = Plain("copper_staff", 0, Rarity.Uncommon, Affix("sharp", 2), Affix("sage", 10));
            var sheet = new StatSheet();
            sheet.Add(item.Modifiers());

            // Staff: Damage+2 and Magic:Damage+10%. Affixes: Damage+2 and Magic:Damage+10%.
            Assert.AreEqual(4.8, sheet.Get(StatType.Damage, context: item.Base.Tags), 1e-9);
            Assert.AreEqual(4, sheet.Get(StatType.Damage, context: SkillTag.Melee), 1e-9);
        }
    }
}
