using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ModifierTests
    {
        [TestCase("Damage+3~5", StatType.Damage, ModifierKind.Flat, 3, 5, SkillTag.None)]
        [TestCase("Damage+20%", StatType.Damage, ModifierKind.Increased, 20, 20, SkillTag.None)]
        [TestCase("Damage*15%", StatType.Damage, ModifierKind.More, 15, 15, SkillTag.None)]
        [TestCase("MoveSpeed-3~5%", StatType.MoveSpeed, ModifierKind.Increased, -5, -3, SkillTag.None)]
        [TestCase("Fire:Damage+10%", StatType.Damage, ModifierKind.Increased, 10, 10, SkillTag.Fire)]
        [TestCase(" fire & melee : damage + 4 ", StatType.Damage, ModifierKind.Flat, 4, 4, SkillTag.Fire | SkillTag.Melee)]
        public void ParsesTheSheetSyntax(string text, StatType stat, ModifierKind kind, int min, int max, SkillTag tags)
        {
            var range = ModifierSyntax.Parse(text);

            Assert.AreEqual(stat, range.Stat);
            Assert.AreEqual(kind, range.Kind);
            Assert.AreEqual(min, range.Min);
            Assert.AreEqual(max, range.Max);
            Assert.AreEqual(tags, range.Tags);
        }

        [TestCase("Damage+5~3", "앞 수치")]
        [TestCase("Damage*20", "%로")]
        [TestCase("Speed+5", "Speed")]
        [TestCase("Ice:Damage+5%", "Ice")]
        [TestCase("Damage", "+, -, *")]
        [TestCase("Damage+1~2~3", "하나만")]
        public void RejectsBadSyntaxWithAReadableMessage(string text, string expected)
        {
            var error = Assert.Throws<FormatException>(() => ModifierSyntax.Parse(text));

            StringAssert.Contains(expected, error.Message);
        }

        [TestCase("Damage+3~5")]
        [TestCase("Melee&Fire:Damage+10%")]
        [TestCase("MoveSpeed-5%")]
        [TestCase("Damage*15%")]
        public void FormatsBackToTheSameText(string text)
        {
            Assert.AreEqual(text, ModifierSyntax.Parse(text).ToString());
        }

        [Test]
        public void RollsStayInsideTheRangeAndReachBothEnds()
        {
            var range = ModifierSyntax.Parse("Damage+3~5");
            var random = new GameRandom(9);

            var values = Enumerable.Range(0, 300).Select(_ => range.Roll(random).Value).Distinct().OrderBy(v => v);

            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, values);
        }

        [Test]
        public void StatSheetAddsFlatThenIncreasedThenMultipliesMore()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(StatType.Damage, ModifierKind.Flat, 5));
            sheet.Add(new Modifier(StatType.Damage, ModifierKind.Increased, 20));
            sheet.Add(new Modifier(StatType.Damage, ModifierKind.Increased, 30));
            sheet.Add(new Modifier(StatType.Damage, ModifierKind.More, 10));
            sheet.Add(new Modifier(StatType.Damage, ModifierKind.More, 20));
            sheet.Add(new Modifier(StatType.Armor, ModifierKind.Flat, 100));

            // (10 + 5) × (1 + 0.2 + 0.3) × 1.1 × 1.2
            Assert.AreEqual(29.7, sheet.Get(StatType.Damage, baseValue: 10), 1e-9);
            Assert.AreEqual(100, sheet.Get(StatType.Armor));
        }

        [Test]
        public void TaggedModifiersNeedEveryTagInTheContext()
        {
            var sheet = new StatSheet();
            sheet.Add("Damage+10;Fire:Damage+50%;Melee&Fire:Damage+100%".Split(';').Select(Mods.Of));

            Assert.AreEqual(10, sheet.Get(StatType.Damage, context: SkillTag.Melee));
            Assert.AreEqual(15, sheet.Get(StatType.Damage, context: SkillTag.Magic | SkillTag.Fire));
            Assert.AreEqual(25, sheet.Get(StatType.Damage, context: SkillTag.Melee | SkillTag.Fire));
        }

        [Test]
        public void DecreasesStopAtZero()
        {
            var sheet = new StatSheet();
            sheet.Add(Mods.Of("MoveSpeed-150%"));

            Assert.AreEqual(0, sheet.Get(StatType.MoveSpeed, baseValue: 100));
        }

        [TestCase("Damage+5", "피해 +5")]
        [TestCase("Fire:Damage+20%", "화염 피해 20% 증가")]
        [TestCase("MoveSpeed-5%", "이동 속도 5% 감소")]
        [TestCase("CritChance+3", "치명타 확률 +3%")]
        [TestCase("Melee&Fire:Damage*10%", "근접 화염 피해 10% 증폭")]
        public void DescribesModifiersInKorean(string text, string expected)
        {
            Assert.AreEqual(expected, StatText.Describe(Mods.Of(text)));
        }
    }
}
