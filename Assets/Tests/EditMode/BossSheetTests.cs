using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BossSheetTests
    {
        [Test]
        public void ShippedBossSheetsAreValid()
        {
            Assert.IsTrue(Bosses.Shipped.Success, string.Join("\n", Bosses.Shipped.Errors));
            var warden = Bosses.Shipped.Find("cave_warden");
            Assert.AreEqual(3, warden.PhaseCount);
            CollectionAssert.AreEqual(new[] { 66, 33 }, warden.Thresholds);
            Assert.AreEqual(Monsters.Shipped.Find("slime"), warden.Attacks.Single(a => a.Shape == AttackShape.Summon).Summon);
        }

        [Test]
        public void BlankPhasesMeanEveryPhase()
        {
            var boss = Bosses.With(
                "dummy,a,가,,circle,nearest,1,5,2,,,,1,,,1,1,\n" +
                "dummy,b,나,2,circle,nearest,1,5,2,,,,1,,,1,1,\n",
                phases: "50");

            Assert.IsTrue(boss.Attacks[0].InPhase(1) && boss.Attacks[0].InPhase(2));
            Assert.IsFalse(boss.Attacks[1].InPhase(1));
        }

        [TestCase("dummy,a,가,,circle,nearest,1,5,,,,,1,,,1,1,", "radius")]
        [TestCase("dummy,a,가,,cone,nearest,1,5,,3,,,,,,1,1,", "angle")]
        [TestCase("dummy,a,가,,charge,nearest,1,5,,8,,2,,,,1,1,", "speed")]
        [TestCase("dummy,a,가,,summon,self,1,,,,,,2,2,,1,1,blob", "blob")]
        [TestCase("dummy,a,가,,circle,nearest,0,5,2,,,,1,,,1,1,", "telegraph")]
        [TestCase("dummy,a,가,3,circle,nearest,1,5,2,,,,1,,,1,1,", "phases")]
        [TestCase("nobody,a,가,,circle,nearest,1,5,2,,,,1,,,1,1,", "nobody")]
        public void ReportsBadAttackRows(string row, string expected)
        {
            var data = Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,100,0,0,2,1,50,1,3,10\n", Bosses.AttacksHeader + row + "\n");

            Assert.IsTrue(data.Errors.Any(e => e.Contains("boss_attacks.csv") && e.Contains(expected)), string.Join("\n", data.Errors));
        }

        [Test]
        public void EveryPhaseNeedsAnAttack()
        {
            var data = Bosses.Parse(
                Bosses.BossesHeader + "dummy,허수아비,100,0,0,2,1,50,1,3,10\n",
                Bosses.AttacksHeader + "dummy,a,가,1,circle,nearest,1,5,2,,,,1,,,1,1,\n");

            Assert.IsTrue(data.Errors.Any(e => e.Contains("2페이즈")), string.Join("\n", data.Errors));
        }

        [Test]
        public void PhaseThresholdsGoDown()
        {
            var data = Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,100,0,0,2,1,30|60,1,3,10\n", Bosses.AttacksHeader);

            Assert.IsTrue(data.Errors.Any(e => e.Contains("bosses.csv 2행") && e.Contains("큰 수부터")), string.Join("\n", data.Errors));
        }
    }
}
