using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // A later phase is felt (2026-11-28, Docs/BossRedesign.md 4.2): its telegraphs are shorter by the boss's tempo, its
    // attacks stronger by their boost, and a blow may come again at once (boost repeat), never sooner than a roll is
    // ready again.
    public class BossTempoTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);

        static World Arena(out Boss boss, BossDefinition definition)
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            boss = world.PlaceBoss(definition, Home, 8);
            return world;
        }

        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 100 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(2), player.Facing, 10));
        }

        static BossDefinition Slam(string boost, string tempo = "", string warn = "") => Bosses.Parse(
            Bosses.BossesHeader.TrimEnd('\n') + ",tempo\n" + $"dummy,허수아비,100000,50,0,2,1,66|33,1,3,10,{tempo}\n",
            Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost\n" +
            $"dummy,slam,내려찍기,,circle,nearest,1.6,30,2,,,,1,,,3,1,,{warn},,,{boost}\n").Find("dummy");

        [Test]
        public void EveryShippedAttackWindsUpNoLongerInALaterPhaseAndNoShorterThanItsFloor()
        {
            foreach (var boss in Bosses.Shipped.Bosses)
            {
                foreach (var attack in boss.Attacks)
                {
                    Assert.AreSame(attack, attack.ForPhase(1), $"{boss.Id}.{attack.Id}: phase 1 is the sheet's");
                    float floor = attack.Warning == AttackWarning.Strong ? BossAttack.LeastStrongTelegraph : BossAttack.LeastTelegraph;
                    for (int phase = 2; phase <= boss.PhaseCount; phase++)
                    {
                        var copy = attack.ForPhase(phase);
                        Assert.AreSame(attack, copy.Base, $"{boss.Id}.{attack.Id}");
                        Assert.LessOrEqual(copy.Telegraph, attack.Telegraph + 1e-4f, $"{boss.Id}.{attack.Id} phase {phase}");
                        Assert.GreaterOrEqual(copy.Telegraph, System.Math.Min(attack.Telegraph, floor) - 1e-4f, $"{boss.Id}.{attack.Id} phase {phase}");
                        Assert.AreEqual(attack.Damage, copy.Damage, "a phase hits no harder");
                        if (copy.Repeat > 0)
                        {
                            Assert.GreaterOrEqual(copy.AgainSeconds + BossAttack.RepeatGapSeconds, World.RollCooldownSeconds - 1e-4f,
                                $"{boss.Id}.{attack.Id}: a repeat lands no sooner than the roll is back");
                            Assert.Less(copy.RepeatForm.Damage, copy.Damage, "a repeat hits for a share");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheUsualTempoShortensPhaseTwoAndThreeAndABossMayHaveItsOwn()
        {
            var usual = Slam("").Attacks[0];
            Assert.AreEqual(1.6f, usual.ForPhase(1).Telegraph, 1e-4f);
            Assert.AreEqual(1.6f * 0.85f, usual.ForPhase(2).Telegraph, 1e-4f);
            Assert.AreEqual(1.6f * 0.75f, usual.ForPhase(3).Telegraph, 1e-4f);

            var own = Slam("", tempo: "0.9|0.8").Attacks[0];
            Assert.AreEqual(1.6f * 0.9f, own.ForPhase(2).Telegraph, 1e-4f);
            Assert.AreEqual(1.6f * 0.8f, own.ForPhase(3).Telegraph, 1e-4f);

            var strong = Slam("3:tele*0.1", warn: "strong").Attacks[0];
            Assert.AreEqual(BossAttack.LeastStrongTelegraph, strong.ForPhase(3).Telegraph, 1e-4f, "a finisher keeps its floor");
        }

        [Test]
        public void ABoostChangesOnlyItsPhase()
        {
            var slam = Slam("2:count+1 radius*1.5;3:count=4").Attacks[0];
            Assert.AreEqual(1, slam.ForPhase(1).Count);
            Assert.AreEqual(2, slam.ForPhase(2).Count);
            Assert.AreEqual(3f, slam.ForPhase(2).Radius, 1e-4f);
            Assert.AreEqual(4, slam.ForPhase(3).Count);
            Assert.AreEqual(2f, slam.ForPhase(3).Radius, 1e-4f, "phase 3 starts from the sheet again");
        }

        [Test]
        public void ABadBoostIsReported()
        {
            foreach (var cell in new[] { "1:count+1", "2:power+1", "2count+1", "2:count+1;2:repeat=1" })
            {
                var data = Bosses.Parse(
                    Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,66|33,1,3,10\n",
                    Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost\n" +
                    $"dummy,slam,내려찍기,,circle,nearest,1,30,2,,,,1,,,3,1,,,,,{cell}\n");
                Assert.IsFalse(data.Success, cell);
            }
        }

        [Test]
        public void TheHostWindsUpForThePhasesTelegraph()
        {
            var world = Arena(out var boss, Slam(""));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));
            world.Step();
            boss.Phase = 3;
            Assert.IsTrue(world.ForceAttack(boss, "slam"));
            Assert.AreEqual(BossState.Telegraph, boss.State);
            Assert.AreEqual(3, boss.Attack.ForPhaseNumber);
            var circle = world.Hazards.Single(h => h.Shape == HazardShape.Circle);
            Assert.AreEqual(World.Ticks(1.6f * 0.75f), circle.ResolveTick - world.Tick, 2);
        }

        [Test]
        public void ARepeatedBlowComesAgainSofterAndNoSoonerThanTheRollIsBack()
        {
            var world = Arena(out var boss, Slam("2:repeat=2"));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));
            world.Step();
            boss.Phase = 2;
            Assert.IsTrue(world.ForceAttack(boss, "slam"));
            var landed = new List<(long Tick, int Damage)>();
            var seen = new HashSet<int>();
            foreach (var h in world.Hazards.Where(h => h.Shape == HazardShape.Circle && seen.Add(h.Id))) landed.Add((h.ResolveTick, h.Damage));
            for (int i = 0; i < World.Ticks(8) && landed.Count < 3; i++)
            {
                world.Step();
                foreach (var h in world.Hazards.Where(h => h.Shape == HazardShape.Circle && seen.Add(h.Id)))
                    landed.Add((h.ResolveTick, h.Damage));
            }
            Assert.AreEqual(3, landed.Count, "the blow and two repeats");
            Assert.AreEqual(30, landed[0].Damage);
            Assert.AreEqual((int)System.MathF.Round(30 * BossAttack.RepeatShare), landed[1].Damage);
            Assert.AreEqual(landed[1].Damage, landed[2].Damage);
            for (int i = 1; i < 3; i++)
                Assert.GreaterOrEqual(landed[i].Tick - landed[i - 1].Tick, World.Ticks(World.RollCooldownSeconds) - 1, "a roll is ready for each");
            Assert.AreEqual(0, boss.RepeatsLeft);
        }

        [Test]
        public void ARoarCutsARepeatShort()
        {
            var world = Arena(out var boss, Slam("2:repeat=3"));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));
            world.Step();
            boss.Phase = 2;
            Assert.IsTrue(world.ForceAttack(boss, "slam"));
            for (int i = 0; i < World.Ticks(5) && !(boss.State == BossState.Recover && boss.RepeatNext); i++) world.Step();
            Assert.IsTrue(boss.RepeatNext);
            world.DamageBoss(boss, player.Id, (int)(boss.MaxHealth * 0.7f), false);
            Assert.AreEqual(BossState.Roar, boss.State);
            Assert.AreEqual(0, boss.RepeatsLeft);
            Assert.IsFalse(boss.RepeatNext);
        }
    }
}
