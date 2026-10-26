using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class LootGeneratorTests
    {
        static readonly LootData Data = ShippedLoot.Load();

        [Test]
        public void SameSeedRollsTheSameItems()
        {
            var first = new LootGenerator(Data);
            var second = new LootGenerator(Data);
            var firstRandom = new GameRandom(77);
            var secondRandom = new GameRandom(77);

            for (int i = 0; i < 50; i++)
            {
                CollectionAssert.AreEqual(
                    ItemText.Describe(first.RollItem(25, firstRandom), showTiers: true),
                    ItemText.Describe(second.RollItem(25, secondRandom), showTiers: true));
            }
        }

        [Test]
        public void AffixesFollowTheRarityRulesTheSlotAndTheLevel()
        {
            var config = new LootConfig();
            var generator = new LootGenerator(Data, config);
            var random = new GameRandom(5);

            foreach (int level in new[] { 1, 15, 30 })
            {
                for (int i = 0; i < 2000; i++)
                {
                    var item = generator.RollItem(level, random, itemRarity: 300);
                    var rule = config.RuleFor(item.Rarity);
                    string shown = string.Join(" / ", ItemText.Describe(item, showTiers: true));

                    Assert.That(item.Affixes.Count, Is.InRange(rule.MinAffixes, rule.MaxAffixes), shown);
                    Assert.LessOrEqual(item.Affixes.Count(a => a.Affix.Kind == AffixKind.Prefix), rule.MaxPerKind, shown);
                    Assert.LessOrEqual(item.Affixes.Count(a => a.Affix.Kind == AffixKind.Suffix), rule.MaxPerKind, shown);
                    Assert.AreEqual(item.Affixes.Count, item.Affixes.Select(a => a.Affix).Distinct().Count(), shown);
                    foreach (var affix in item.Affixes)
                    {
                        Assert.IsTrue(affix.Affix.AllowedOn(item.Base.Slot), shown);
                        Assert.LessOrEqual(affix.Tier.Level, level, shown);
                        Assert.That(affix.Modifier.Value, Is.InRange(affix.Tier.Modifier.Min, affix.Tier.Modifier.Max), shown);
                    }
                    Assert.LessOrEqual(item.Sockets.Count, item.Base.MaxSockets, shown);
                }
            }
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 10)]
        [TestCase(25, 20)]
        [TestCase(99, 30)]
        public void OnlyTheNewestStepOfBasesDrops(int level, int step)
        {
            var bases = new LootGenerator(Data).BasesAt(level);

            Assert.IsTrue(bases.All(b => b.Level == step), string.Join(", ", bases.Select(b => b.Id)));
            Assert.AreEqual(Enum.GetValues(typeof(ItemSlot)).Length, bases.Select(b => b.Slot).Distinct().Count());
        }

        [Test]
        public void ItemRarityRaisesTheShareOfRareAndEpicItems()
        {
            double BetterShare(int itemRarity)
            {
                var generator = new LootGenerator(Data);
                var random = new GameRandom(11);
                return Enumerable.Range(0, 5000).Count(_ => generator.RollItem(10, random, itemRarity).Rarity >= Rarity.Rare) / 5000.0;
            }

            // 8% without the bonus; 240 / 1600 = 15% with +200%.
            Assert.That(BetterShare(0), Is.InRange(0.07, 0.09));
            Assert.That(BetterShare(200), Is.InRange(0.135, 0.165));
        }

        [Test]
        public void EachSocketNeedsThePreviousOneUpToTheBaseMaximum()
        {
            var generator = new LootGenerator(Data, new LootConfig { SocketChancePercent = 40 });
            var random = new GameRandom(8);
            var sword = Data.FindBase("copper_sword");

            var counts = new int[sword.MaxSockets + 1];
            for (int i = 0; i < 5000; i++) counts[generator.RollItem(sword, 1, Rarity.Common, random).Sockets.Count]++;

            // 60% none, 24% one, 9.6% two, 6.4% three.
            Assert.That(counts[0] / 5000.0, Is.InRange(0.57, 0.63));
            Assert.That(counts[1] / 5000.0, Is.InRange(0.21, 0.27));
            Assert.That(counts[3] / 5000.0, Is.InRange(0.05, 0.08));
        }

        [Test]
        public void RunesOnlyDropOnceTheirLevelIsReached()
        {
            var generator = new LootGenerator(Data);
            var random = new GameRandom(4);

            Assert.IsNull(generator.RollRune(0, random));
            var early = Enumerable.Range(0, 2000).Select(_ => generator.RollRune(5, random)).ToList();
            Assert.IsTrue(early.All(rune => rune.Level <= 5));
            var late = Enumerable.Range(0, 5000).Select(_ => generator.RollRune(30, random).Id).ToList();
            CollectionAssert.IsSubsetOf(Data.Runes.Select(rune => rune.Id).ToList(), late);
        }
    }
}
