using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The boss bundle R1b (2026-11-28, Docs/BossRedesign.md 8, Docs/Bosses.md 늦은 공격·납도 반격·색 표지): a late blow
    // waits past its telegraph and gives its tell just before it lands; the noto counter answers a blow struck in its
    // stance and stands open when left alone; a colour's blow hurts by how near one stands; and a finisher rests
    // before it comes again.
    public class BossR1bTests
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

        // One boss with the rows given (attack columns from shape on), and the finisher rest given.
        static BossDefinition Dummy(string rest, params string[] attacks) => Bosses.Parse(
            Bosses.BossesHeader.TrimEnd('\n') + ",tempo,finisher_rest\n" + $"dummy,허수아비,100000,50,0,2,1,66|33,1,3,10,,{rest}\n",
            Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost\n" + string.Concat(attacks.Select(a => "dummy," + a + "\n"))).Find("dummy");

        static (World World, Boss Boss, Player Player) Awake(BossDefinition definition, Vec2 at)
        {
            var world = Arena(out var boss, definition);
            var player = world.AddPlayer("a", null);
            Walk(world, player, at);
            world.Step();
            return (world, boss, player);
        }

        [Test]
        public void ALateBlowWaitsPastItsTelegraphAndCuesJustBeforeItLands()
        {
            var waits = new HashSet<long>();
            for (int seed = 0; seed < 6; seed++)
            {
                var (world, boss, _) = Awake(Dummy("", "slam,내려찍기,,circle,nearest,1.2,30,2,,,,1,,,3,1,,,,,2:hold=0.5"), Home + new Vec2(0, -4));
                for (int i = 0; i < seed * 7; i++) world.Step();
                boss.Phase = 2;
                Assert.IsTrue(world.ForceAttack(boss, "slam"));
                var circle = world.Hazards.Where(h => h.Shape == HazardShape.Circle && h.Source == boss.Id).OrderBy(h => h.Id).Last();
                long wait = circle.ResolveTick - world.Tick - World.Ticks(boss.Attack.Telegraph);
                Assert.GreaterOrEqual(wait, World.Ticks(0.5f * 0.4f) - 1, "it waits at least 0.4 of its hold");
                Assert.LessOrEqual(wait, World.Ticks(0.5f) + 1, "and no more than its hold");
                Assert.AreEqual(circle.ResolveTick - world.Tick, boss.StateTicks, "the boss swings as its mark lands");
                waits.Add(wait);
                long cue = -1;
                while (boss.State == BossState.Telegraph)
                {
                    world.Step();
                    if (world.TakeEvents().Any(e => e.Type == SimEventType.Cue && e.Source == boss.Id)) cue = world.Tick;
                }
                Assert.AreEqual(World.Ticks(World.CueSeconds), circle.ResolveTick - cue, 1, "the tell comes CueSeconds before the blow");
            }
            Assert.Greater(waits.Count, 1, "the wait is not the same every time");
        }

        [Test]
        public void PhaseOneAndARepeatDoNotWait()
        {
            var slam = Dummy("", "slam,내려찍기,,circle,nearest,1.2,30,2,,,,1,,,3,1,,,,,2:hold=0.5 repeat=1").Attacks[0];
            Assert.AreEqual(0, slam.ForPhase(1).Hold);
            Assert.AreEqual(0.5f, slam.ForPhase(2).Hold, 1e-4f);
            Assert.AreEqual(0, slam.ForPhase(2).RepeatForm.Hold, "a repeat comes on its own beat");
        }

        static BossDefinition Noto() => Dummy("",
            "noto,납도,,counter,self,1.6,80,,5,,1.2,3,,,0.8,1,,,,,",
            "slam,내려찍기,,circle,nearest,1.2,30,2,,,,1,,,3,1,,,,,");

        [Test]
        public void TheNotoCounterAnswersABlowStruckInItsStance()
        {
            var (world, boss, player) = Awake(Noto(), Home + new Vec2(0, -2));
            Assert.IsTrue(world.ForceAttack(boss, "noto"));
            Assert.IsFalse(world.Hazards.Any(h => h.Source == boss.Id), "the stance lays nothing");
            world.Step();
            float before = player.Health;
            world.Hurt(boss, player, 50);
            Assert.IsTrue(boss.Countered);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Cue && e.Source == boss.Id && e.Amount == World.CueCounter));
            var cuts = world.Hazards.Where(h => h.Source == boss.Id && h.Shape == HazardShape.Line).ToList();
            Assert.AreEqual(3, cuts.Count, "three cuts right through it");
            Assert.IsTrue(cuts.All(c => c.ResolveTick - world.Tick == World.Ticks(World.CounterSeconds)));
            Assert.IsTrue(cuts.Any(c => c.Contains(player.Position, 0.3f)), "one is turned on the striker");
            world.Hurt(boss, player, 50);
            Assert.AreEqual(3, world.Hazards.Count(h => h.Source == boss.Id && h.Shape == HazardShape.Line), "it answers once");
            for (int i = 0; i < World.Ticks(World.CounterSeconds) + 1; i++) world.Step();
            Assert.AreEqual(before - 80, player.Health, 0.5f, "the answer lands");
            Assert.AreEqual(BossState.Recover, boss.State);
        }

        [Test]
        public void LeftAloneTheNotoStanceStandsOpen()
        {
            var (world, boss, player) = Awake(Noto(), Home + new Vec2(0, -2));
            Assert.IsTrue(world.ForceAttack(boss, "noto"));
            float before = player.Health;
            while (boss.State == BossState.Telegraph) world.Step();
            Assert.AreEqual(BossState.Recover, boss.State);
            Assert.IsFalse(boss.Countered);
            Assert.AreEqual(World.Ticks(World.CounterOpening), boss.StateTicks, 1, "open for a second");
            Assert.AreEqual(before, player.Health);
            float health = boss.Health;
            world.Hurt(boss, player, 50);
            Assert.Less(boss.Health, health, "struck while open");
            Assert.IsFalse(world.Hazards.Any(h => h.Source == boss.Id && h.Shape == HazardShape.Line), "no answer once the stance is over");
        }

        [Test]
        public void RedHurtsLessCloseInAndBlueHurtsLessFarOut()
        {
            Assert.AreEqual(World.NearLeast, World.FalloffShare(AttackWarning.Near, 0, 6), 1e-4f);
            Assert.AreEqual(1, World.FalloffShare(AttackWarning.Near, 6, 6), 1e-4f);
            Assert.AreEqual(1, World.FalloffShare(AttackWarning.Far, 0, 6), 1e-4f);
            Assert.AreEqual(World.FarLeast, World.FalloffShare(AttackWarning.Far, 6, 6), 1e-4f);

            float Taken(string warn, float distance)
            {
                var (world, boss, player) = Awake(Dummy("", $"wave,파동,,circle,self,1.2,100,6,,,,1,,,3,1,,{warn},,,"), Home + new Vec2(0, -distance));
                boss.Position = Home;
                float before = player.Health;
                Assert.IsTrue(world.ForceAttack(boss, "wave"));
                Assert.AreEqual(warn == "near" ? AttackWarning.Near : AttackWarning.Far, world.Hazards.Single(h => h.Source == boss.Id).Falloff);
                while (world.Hazards.Any(h => h.Source == boss.Id)) world.Step();
                return before - player.Health;
            }
            Assert.Less(Taken("near", 1), Taken("near", 5), "red: closer is safer");
            Assert.Greater(Taken("far", 1), Taken("far", 5), "blue: farther is safer");
        }

        [Test]
        public void AColourIsOnlyForABlowMeasuredFromTheBoss()
        {
            var data = Bosses.Parse(
                Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,66|33,1,3,10\n",
                Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost\n" +
                "dummy,shot,탄,,burst,nearest,1,30,0.5,,,,5,,8,3,1,,near,,,\n");
            Assert.IsFalse(data.Success);
        }

        [Test]
        public void AFinisherRestsBeforeItIsChosenAgain()
        {
            var definition = Dummy("8",
                "big,필살,,circle,nearest,1.2,30,2,,,,1,,,0.5,50,,strong,,,",
                "slam,내려찍기,,circle,nearest,1.2,30,2,,,,1,,,0.5,1,,,,,");
            Assert.AreEqual(8, definition.FinisherRest, 1e-4f);
            Assert.AreEqual(BossDefinition.DefaultFinisherRest, Dummy("", "slam,내려찍기,,circle,nearest,1.2,30,2,,,,1,,,0.5,1,,,,,").FinisherRest);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -4));
            player.Health = 1e6f;
            var starts = new List<long>();
            BossAttack last = null;
            for (int i = 0; i < World.Ticks(60); i++)
            {
                world.Step();
                player.Health = System.Math.Max(player.Health, 1000);
                if (boss.State == BossState.Approach && boss.Attack != last)
                {
                    last = boss.Attack;
                    if (last.Base.Id == "big") starts.Add(world.Tick);
                }
                else if (boss.State != BossState.Approach) last = null;
            }
            Assert.Greater(starts.Count, 2, "it still comes");
            for (int i = 1; i < starts.Count; i++)
                Assert.GreaterOrEqual(starts[i] - starts[i - 1], World.Ticks(8) - 1, "no sooner than its rest");
        }
    }
}
