using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The vein eater of the mining village (2026-11-12, Docs/Bosses.md): most of its attacks are announced only by its
    // own wind-up, the strongest by a warning; it burrows and leaps out of reach and comes down where its target stood,
    // and its frenzy charges three times in a row.
    public class VeinEaterTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);
        const float ArenaRadius = 8;

        const string Header =
            "boss,id,name,phases,shape,target,telegraph,damage,radius,length,angle,width,count,spread,speed,cooldown,weight,summon,warn\n";

        static BossDefinition With(string rows) =>
            Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,,1,3,10\n", Header + rows).Find("dummy");

        static World Arena(out Boss boss, BossDefinition definition)
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            boss = world.PlaceBoss(definition, Home, ArenaRadius);
            return world;
        }

        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 100 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(2), player.Facing, 10));
        }

        static List<SimEvent> Run(World world, float seconds)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(seconds); i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        [Test]
        public void TheSheetSaysWhichAttacksAreAnnouncedOnlyByTheBossItself()
        {
            var eater = Bosses.Shipped.Find("vein_eater");
            Assert.AreEqual("iron_valley", eater.Place);
            Assert.AreEqual(AttackWarning.Hidden, eater.Attacks.Single(a => a.Id == "claw").Warning);
            Assert.AreEqual(AttackWarning.Strong, eater.Attacks.Single(a => a.Id == "burrow").Warning);
            // Only the few strongest carry a warning; the rest are read from the boss's wind-up.
            var strong = eater.Attacks.Where(a => a.Warning == AttackWarning.Strong).ToList();
            Assert.That(strong.Count, Is.InRange(2, 4));
            Assert.That(strong.Min(a => a.Damage), Is.GreaterThan(eater.Attacks.Where(a => a.Warning == AttackWarning.Hidden).Max(a => a.Damage)));
            // Every phase brings attacks of its own.
            for (int phase = 2; phase <= eater.PhaseCount; phase++)
                Assert.IsTrue(eater.Attacks.Any(a => a.Phases.Count > 0 && a.Phases.Min() == phase), $"phase {phase} brings something new");
            // The bosses from before are announced as they always were.
            foreach (var boss in Bosses.Shipped.Bosses.Where(b => b.Id != "vein_eater"))
                Assert.IsTrue(boss.Attacks.All(a => a.Warning == AttackWarning.Marked), boss.Id);
            var wrong = Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,,1,3,10\n", Header + "dummy,a,공격,,circle,nearest,1,30,2,,,,1,,,1,1,,loud\n");
            Assert.IsFalse(wrong.Success);
            Assert.IsTrue(wrong.Errors.Any(e => e.Contains("warn")), string.Join(" / ", wrong.Errors));
        }

        [Test]
        public void ABurrowIsOutOfReachUntilItBurstsUpWhereTheTargetStood()
        {
            var world = Arena(out var boss, With("dummy,a,땅속 습격,,burrow,nearest,1.5,50,2.5,,,,8,,8,1,1,,strong\n"));
            var near = world.AddPlayer("near", null);
            Walk(world, near, Home + new Vec2(4, -3));
            var stood = near.Position;

            Run(world, 0.2f);
            Assert.AreEqual(BossState.Telegraph, boss.State);
            Assert.IsTrue(boss.Away && boss.Invulnerable, "underground");
            var hazard = world.Hazards.Single();
            Assert.AreEqual(stood, hazard.Position);
            Assert.AreEqual(2.5f, hazard.Size);

            var events = Run(world, 1.4f);
            Assert.IsFalse(boss.Away);
            Assert.Less(Vec2.Distance(boss.Position, stood), 0.01f, "up where the target stood");
            Assert.AreEqual(PlayerStats.BaseHealth - 50, near.Health, 0.01f, "the blast, before any rock");
            Assert.AreEqual(8, world.Projectiles.Count(p => p.Source == boss.Id), "rocks all round");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Impact && e.Source == boss.Id));
        }

        [Test]
        public void ALeapComesDownInsideTheArenaEvenWhenTheTargetStandsOutside()
        {
            var world = Arena(out var boss, With("dummy,a,대지 분쇄,,leap,farthest,1.2,60,3,,,,,,,1,1,,strong\n"));
            var far = world.AddPlayer("far", null);
            Walk(world, far, Home + new Vec2(0, -ArenaRadius - 3));

            Run(world, 1.4f);
            Assert.LessOrEqual(Vec2.Distance(boss.Position, Home), ArenaRadius - boss.Definition.Radius + 0.01f);
        }

        [Test]
        public void AFrenzyChargesThreeTimesInARow()
        {
            var world = Arena(out var boss, With("dummy,a,광폭 연속 돌진,,charge,nearest,0.8,30,,7,,2,3,,16,2,1,,none\n"));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));

            int charges = 0;
            var before = BossState.Dormant;
            for (int i = 0; i < World.Ticks(4); i++)
            {
                world.Step();
                world.TakeEvents();
                if (boss.State == BossState.Charge && before != BossState.Charge) charges++;
                before = boss.State;
                if (boss.State == BossState.Recover) break;
            }
            Assert.AreEqual(3, charges);
        }

        [Test]
        public void TheViewKnowsWhichAttackTheBossIsOnAndHowPlainlyItsAreasAreShown()
        {
            var definition = With(
                "dummy,a,할퀴기,,cone,nearest,1,30,,4,90,,,,,1,1,,none\n" +
                "dummy,b,땅속 습격,,burrow,nearest,1.5,50,2.5,,,,,,,1,0,,strong\n");
            var world = Arena(out var boss, definition);
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -2.5f));
            Run(world, 0.2f);

            Assert.AreEqual(BossState.Telegraph, boss.State);
            int field = World.BossPhaseField(boss);
            var view = new EntityView(boss.Id, EntityKind.Boss, null, definition, null, boss.Position, boss.Facing, boss.Health, boss.MaxHealth,
                World.BossStateByte(boss), Protocol.WithStatus(field, 0));
            Assert.AreEqual(1, view.Phase);
            Assert.AreEqual("a", view.BossAttack.Id);
            Assert.AreEqual(AttackWarning.Hidden, view.BossAttack.Warning);

            // Asleep, a boss is on no attack.
            var sleeper = Arena(out var asleep, definition);
            Assert.AreEqual(1, World.BossPhaseField(asleep));
        }
    }
}
