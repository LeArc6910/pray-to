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

        // The then column (2026-11-25): another attack of the same boss that lands a moment after, past a roll's unhurt
        // ticks and short of its cooldown.
        [TestCase("b:0.5", null)]
        [TestCase("a:0.5", "바닥에 떨어지는")]
        [TestCase("nobody:0.5", "then")]
        [TestCase("b", "then")]
        [TestCase("b:0.1", "0.3~0.8")]
        [TestCase("b:1.0", "0.3~0.8")]
        [TestCase("c:0.5", "바닥에 떨어지는")]
        public void ReadsWhatFollowsAnAttack(string then, string expected)
        {
            var data = Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,100,0,0,2,1,50,1,3,10\n",
                Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,then\n" +
                "dummy,b,나,,cone,nearest,1,5,,3,90,,,,,1,1,,,,\n" +
                "dummy,c,다,,charge,nearest,1,5,,8,,2,,,10,1,1,,,,\n" +
                $"dummy,a,가,,circle,nearest,1,5,2,,,,1,,,1,1,,,,{then}\n");

            if (expected == null)
            {
                Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
                var a = data.Find("dummy").Attacks.Single(x => x.Id == "a");
                Assert.AreEqual("b", a.FollowUp.Id);
                Assert.AreEqual(0.5f, a.FollowDelay, 1e-5f);
                Assert.IsNull(data.Find("dummy").Attacks.Single(x => x.Id == "b").FollowUp);
            }
            else
                Assert.IsTrue(data.Errors.Any(e => e.Contains("boss_attacks.csv") && e.Contains(expected)), string.Join("\n", data.Errors));
        }

        // The shipped follow-ups (B5b): each gap lies between a roll's unhurt ticks (with the host's slack) and its
        // cooldown, and a row that is never chosen by itself (weight 0) is there to follow something.
        [Test]
        public void ShippedFollowUpsLandBetweenTwoRolls()
        {
            int follows = 0;
            foreach (var boss in Bosses.Shipped.Bosses)
            {
                foreach (var attack in boss.Attacks.Where(a => a.FollowUp != null))
                {
                    follows++;
                    int gap = World.Ticks(attack.FollowDelay);
                    Assert.Greater(gap, World.DodgeTicks + World.RollSlackTicks, $"{boss.Id}.{attack.Id}");
                    Assert.Less(gap, World.RollCooldownTicks, $"{boss.Id}.{attack.Id}");
                }
                foreach (var quiet in boss.Attacks.Where(a => a.Weight == 0))
                    Assert.IsTrue(boss.Attacks.Any(a => a.FollowUp == quiet), $"{boss.Id}.{quiet.Id} follows something");
            }
            Assert.Greater(follows, 0);
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
