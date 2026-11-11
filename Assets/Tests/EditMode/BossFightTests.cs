using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BossFightTests
    {
        // A 40-tile room: the spawn near the bottom, the boss's arena (radius 8) near the top, far enough apart
        // that a player at the spawn is not in the fight.
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);
        const float ArenaRadius = 8;

        static World Arena(out Boss boss, BossDefinition definition, WorldConfig config = null)
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, config ?? new WorldConfig { EnemyTarget = 0 });
            boss = world.PlaceBoss(definition, Home, ArenaRadius);
            return world;
        }

        // Puts the player somewhere at once, in steps the host accepts.
        static void Walk(World world, Player player, Vec2 to, Vec2? facing = null)
        {
            for (int i = 0; i < 100 && Vec2.Distance(player.Position, to) > 0.001f; i++)
            {
                var step = (to - player.Position).ClampLength(2);
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + step, facing ?? player.Facing, 10));
            }
            if (facing.HasValue) world.MovePlayer(player.Id, player.Position, facing.Value, 1);
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

        // One attack row for the test boss, with the columns that shape uses.
        static string Attack(string shape, string target = "nearest", float telegraph = 1, int damage = 30, string radius = "",
            string length = "", string angle = "", string width = "", string count = "", string spread = "", string speed = "", string summon = "",
            float cooldown = 1) =>
            $"dummy,a,공격,,{shape},{target},{telegraph},{damage},{radius},{length},{angle},{width},{count},{spread},{speed},{cooldown},1,{summon}\n";

        [Test]
        public void SleepsUntilAPlayerWalksIntoTheArena()
        {
            var world = Arena(out var boss, Bosses.With(Attack("circle", radius: "2", count: "1")));
            var alice = world.AddPlayer("a", null);
            world.AddPlayer("b", null);

            Run(world, 1);
            Assert.AreEqual(BossState.Dormant, boss.State);

            Walk(world, alice, Home + new Vec2(0, -6));
            var events = Run(world, 0.1f);

            Assert.AreNotEqual(BossState.Dormant, boss.State);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.BossAwake && e.Source == boss.Id));
            Assert.AreEqual(1500, boss.MaxHealth, "+50% for the second player");
            Assert.AreEqual(boss.MaxHealth, boss.Health);
        }

        [Test]
        public void CirclesLandWhereTheTargetStood()
        {
            var world = Arena(out var boss, Bosses.With(Attack("circle", radius: "2", count: "1")));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -5));

            Run(world, 0.1f);
            var hazard = world.Hazards.Single();
            Assert.AreEqual(HazardShape.Circle, hazard.Shape);
            Assert.AreEqual(player.Position, hazard.Position);
            Run(world, 1);

            Assert.AreEqual(PlayerStats.BaseHealth - 30, player.Health, 0.01f);
        }

        [Test]
        public void SteppingOutOfACircleDodgesIt()
        {
            var world = Arena(out _, Bosses.With(Attack("circle", radius: "2", count: "1")));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -5));

            Run(world, 0.1f);
            Walk(world, player, player.Position + new Vec2(3, 0));
            var events = Run(world, 1);

            Assert.AreEqual(PlayerStats.BaseHealth, player.Health);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Impact), "it still lands, on empty ground");
        }

        [Test]
        public void ConesOnlyHitInFrontOfTheBoss()
        {
            var world = Arena(out var boss, Bosses.With(Attack("cone", length: "4", angle: "90")));
            var front = world.AddPlayer("front", null);
            var behind = world.AddPlayer("behind", null);
            Walk(world, front, Home + new Vec2(0, -2.5f));
            Walk(world, behind, Home + new Vec2(0, 3));

            Run(world, 1.2f);

            Assert.AreEqual(PlayerStats.BaseHealth - 30, front.Health, 0.01f);
            Assert.AreEqual(PlayerStats.BaseHealth, behind.Health);
            Assert.AreEqual(Home, boss.Position, "already in range, so it did not move");
        }

        [Test]
        public void ChargesRushDownTheMarkedLine()
        {
            var world = Arena(out var boss, Bosses.With(Attack("charge", target: "farthest", length: "7", width: "2", speed: "14")));
            var inLine = world.AddPlayer("in line", null);
            var aside = world.AddPlayer("aside", null);
            Walk(world, inLine, Home + new Vec2(0, -5));
            Walk(world, aside, Home + new Vec2(3, -2));

            Run(world, 0.1f);
            Assert.AreEqual(HazardShape.Line, world.Hazards.Single().Shape);
            var events = Run(world, 1.6f);

            Assert.AreEqual(PlayerStats.BaseHealth - 30, inLine.Health, 0.01f, "hit once, not every tick");
            Assert.AreEqual(PlayerStats.BaseHealth, aside.Health);
            Assert.Less(boss.Position.Y, Home.Y - 5, "the boss ended up down the line");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Impact && e.Source == boss.Id));
        }

        // Every attack shape gives one of five warnings, the same on every boss, and a winding-up boss sends it with its
        // state (Docs/Bosses.md 전조 말, 2026-11-11).
        [Test]
        public void EachAttackGivesItsKindOfWarning()
        {
            foreach (AttackShape shape in System.Enum.GetValues(typeof(AttackShape))) Assert.AreNotEqual(Tell.None, World.TellOf(shape), shape.ToString());
            Assert.AreEqual(Tell.Area, World.TellOf(AttackShape.Circle));
            Assert.AreEqual(Tell.Area, World.TellOf(AttackShape.Trail));
            Assert.AreEqual(Tell.Dash, World.TellOf(AttackShape.Charge));
            Assert.AreEqual(Tell.Gather, World.TellOf(AttackShape.Share));
            Assert.AreEqual(Tell.Barrage, World.TellOf(AttackShape.Spiral));
            Assert.AreEqual(Tell.Call, World.TellOf(AttackShape.Summon));

            var world = Arena(out var boss, Bosses.With(Attack("burst", target: "self", radius: "0.3", count: "8", speed: "6", cooldown: 20)));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));
            for (int i = 0; i < World.Ticks(3) && boss.State != BossState.Telegraph; i++) world.Step();
            Assert.AreEqual(BossState.Telegraph, boss.State);
            byte sent = World.BossStateByte(boss);
            Assert.AreEqual(BossState.Telegraph, (BossState)(sent & 0x0F));
            Assert.AreEqual(Tell.Barrage, (Tell)(sent >> 4));
            Run(world, 1.1f);
            Assert.AreEqual((byte)boss.State, World.BossStateByte(boss), "no warning once it has struck");
        }

        [Test]
        public void BurstsSendProjectilesThatStopAtPlayersAndWalls()
        {
            // A long cooldown so only one burst flies during the test.
            var world = Arena(out _, Bosses.With(Attack("burst", target: "self", damage: 7, radius: "0.3", count: "36", speed: "6", cooldown: 20)));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));

            Run(world, 1.1f);
            Assert.AreEqual(36, world.Projectiles.Count);
            Run(world, 4);

            float taken = PlayerStats.BaseHealth - player.Health;
            Assert.Greater(taken, 0);
            Assert.AreEqual(0, taken % 7, 0.01f);
            Assert.Less(taken, 7 * 4, "only the few aimed near the player");
            Assert.AreEqual(0, world.Projectiles.Count);
        }

        [Test]
        public void FannedBurstsAimAtTheTarget()
        {
            var world = Arena(out _, Bosses.With(Attack("burst", target: "nearest", damage: 7, radius: "0.3", angle: "60", count: "5", speed: "6")));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(5, 0));

            Run(world, 1.1f);

            Assert.AreEqual(5, world.Projectiles.Count);
            foreach (var projectile in world.Projectiles)
            {
                float degrees = System.MathF.Abs(projectile.Velocity.Angle) * 180 / System.MathF.PI;
                Assert.LessOrEqual(degrees, 30.5f);
            }
        }

        [Test]
        public void SummonsCallMonstersAroundTheBoss()
        {
            var world = Arena(out _, Bosses.With(Attack("summon", target: "self", damage: 0, count: "3", spread: "3", summon: "slime")));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -6));

            Run(world, 1.1f);

            Assert.AreEqual(3, world.Enemies.Count);
            Assert.IsTrue(world.Enemies.All(e => Vec2.Distance(e.Position, Home) < 4));
        }

        [Test]
        public void PhasesChangeAtTheThresholdWithARoar()
        {
            var definition = Bosses.With(Attack("circle", damage: 1, radius: "1", count: "1"), phases: "50", health: 100);
            var world = Arena(out var boss, definition);
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            Walk(world, player, Home + new Vec2(0, -2), facing: new Vec2(0, 1));

            world.SetAttacking(player.Id, true);
            var events = new List<SimEvent>();
            for (int i = 0; i < 600 && boss.Phase == 1; i++)
            {
                world.MovePlayer(player.Id, player.Position, boss.Position - player.Position, 1);
                world.Step();
                events.AddRange(world.TakeEvents());
            }

            Assert.AreEqual(2, boss.Phase);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.PhaseChanged && e.Amount == 2));
            Assert.AreEqual(BossState.Roar, boss.State);
            Assert.IsTrue(world.Hazards.Any(h => h.Size == definition.RoarRadius), "the roar's blast is marked");

            float health = boss.Health;
            Run(world, 0.5f);
            Assert.AreEqual(health, boss.Health, "unhurt while roaring");
        }

        [Test]
        public void APhaseOpensWithAnAttackItBroughtAndFavoursIt()
        {
            // "a" in every phase, "b" from phase 2.
            string rows = Attack("circle", damage: 1, radius: "1", count: "1") + "dummy,b,새 공격,2,ring,self,1,1,2,5,,,,,,1,1,\n";
            var definition = Bosses.With(rows, phases: "50", health: 100);
            var world = Arena(out var boss, definition);
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            Walk(world, player, Home + new Vec2(0, -2), facing: new Vec2(0, 1));

            world.SetAttacking(player.Id, true);
            for (int i = 0; i < 600 && boss.Phase == 1; i++)
            {
                world.MovePlayer(player.Id, player.Position, boss.Position - player.Position, 1);
                world.Step();
            }
            world.SetAttacking(player.Id, false);
            Assert.AreEqual(2, boss.Phase);
            // Past the roar, the first attack chosen is the one phase 2 brought.
            for (int i = 0; i < 600 && boss.State == BossState.Roar; i++) world.Step();
            Assert.AreEqual("b", boss.Attack?.Id);

            var a = definition.Attacks.Single(x => x.Id == "a");
            var b = definition.Attacks.Single(x => x.Id == "b");
            Assert.Greater(World.PhaseWeight(b, 2), World.PhaseWeight(a, 2) * 3, "the new attack comes up far more often than the old");
            Assert.AreEqual(World.PhaseWeight(a, 1), a.Weight * 10, "phase 1 as the sheet says");
        }

        [Test]
        public void EachFighterGetsTheirOwnBossDropsThatOthersCannotTake()
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var boss = world.PlaceBoss(Bosses.Shipped.Find("cave_warden"), Home, ArenaRadius);
            var a = world.AddPlayer("a", null);
            var b = world.AddPlayer("b", null);
            Walk(world, a, Home + new Vec2(0, -3));
            Walk(world, b, Home + new Vec2(2, -3));
            Run(world, 0.5f);
            world.Hurt(boss, (int)boss.MaxHealth + 1);
            var drops = world.GroundLoot.ToList();
            Assert.IsTrue(drops.Any(l => l.Owner == a.Id) && drops.Any(l => l.Owner == b.Id), "a share for each");
            Assert.IsTrue(drops.All(l => l.Owner == a.Id || l.Owner == b.Id));
            var mine = drops.First(l => l.Owner == a.Id);
            Assert.IsTrue(mine.For(a.Id, world.Tick));
            Assert.IsFalse(mine.For(b.Id, world.Tick), "b neither sees nor takes it");
            Assert.IsTrue(drops.Any(l => l.Material?.Id == World.Coin && l.Count > 1), "a heap of coins in each share");
        }

        [Test]
        public void TheBossResetsWhenEveryoneLeaves()
        {
            var world = Arena(out var boss, Bosses.With(Attack("circle", radius: "2", count: "1")));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -5));
            Run(world, 0.5f);
            Assert.IsTrue(world.Hazards.Count > 0);

            Walk(world, player, Spawn);
            Run(world, 6);

            Assert.AreEqual(BossState.Dormant, boss.State);
            Assert.AreEqual(Home, boss.Position);
            Assert.AreEqual(boss.Definition.Health, boss.Health);
            Assert.AreEqual(0, world.Hazards.Count);
        }

        [Test]
        public void DefeatedBossesComeBackLater()
        {
            var world = Arena(out var boss, Bosses.With(Attack("circle", damage: 1, radius: "1", count: "1"), health: 10), new WorldConfig { EnemyTarget = 0, BossRespawnSeconds = 5 });
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            Walk(world, player, Home + new Vec2(0, -2), facing: new Vec2(0, 1));

            world.SetAttacking(player.Id, true);
            var events = Run(world, 2);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.BossDefeated && e.Source == boss.Id));
            Assert.AreEqual(0, world.Bosses.Count);

            world.SetAttacking(player.Id, false);
            Walk(world, player, Spawn);
            Run(world, 5);
            var again = world.Bosses.Single();
            Assert.AreEqual(BossState.Dormant, again.State);
            Assert.AreEqual(Home, again.Position);
        }

        // Ring: safe close to the boss, hurt out to its edge, safe again beyond it.
        [Test]
        public void RingsSpareWhoeverHugsTheBoss()
        {
            var world = Arena(out var boss, Bosses.With(Attack("ring", target: "self", radius: "2", length: "6")));
            var near = world.AddPlayer("a", null);
            var mid = world.AddPlayer("b", null);
            Walk(world, near, Home + new Vec2(0, -1));
            Walk(world, mid, Home + new Vec2(4, 0));
            Run(world, 0.1f);
            var ring = world.Hazards.Single();
            Assert.AreEqual(HazardShape.Ring, ring.Shape);
            Assert.AreEqual(6, ring.Size);
            Assert.AreEqual(2, ring.Width);
            Run(world, 1.1f);
            Assert.AreEqual(PlayerStats.BaseHealth, near.Health, "inside the safe middle");
            Assert.Less(mid.Health, PlayerStats.BaseHealth);
        }

        [Test]
        public void CrossesLayTwoBeamsOnTheTarget()
        {
            var world = Arena(out _, Bosses.With(Attack("cross", length: "5", width: "1", angle: "45")));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -5));
            Run(world, 0.1f);
            var beams = world.Hazards.ToList();
            Assert.AreEqual(2, beams.Count);
            Assert.IsTrue(beams.All(b => b.Shape == HazardShape.Line && b.Size == 10));
            Assert.IsTrue(beams.All(b => b.Contains(player.Position, 0)), "both cross where the player stood");
            Assert.AreEqual(0, Vec2.Dot(beams[0].Direction, beams[1].Direction), 0.001f);
        }

        // Trail: circles walking out from the boss, each later than the one before.
        [Test]
        public void TrailsLandOneAfterAnother()
        {
            var world = Arena(out var boss, Bosses.With(Attack("trail", radius: "1", length: "8", count: "4", speed: "8")));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -6));
            Run(world, 0.1f);
            var blasts = world.Hazards.OrderBy(h => h.ResolveTick).ToList();
            Assert.AreEqual(4, blasts.Count);
            for (int i = 1; i < blasts.Count; i++)
            {
                Assert.Greater(blasts[i].ResolveTick, blasts[i - 1].ResolveTick);
                Assert.Greater(Vec2.Distance(blasts[i].Position, boss.Position), Vec2.Distance(blasts[i - 1].Position, boss.Position));
            }
        }

        [Test]
        public void SpiralsFireSeveralTurningVolleys()
        {
            var world = Arena(out var boss, Bosses.With(Attack("spiral", target: "self", telegraph: 0.2f, radius: "0.3", count: "4", width: "3", angle: "30", speed: "1", cooldown: 2)));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(7, 0));
            var events = Run(world, 1.2f);
            Assert.AreEqual(12, world.Projectiles.Count(p => !p.Friendly), "three volleys of four");
            Assert.AreEqual(3, events.Count(e => e.Type == SimEventType.Attack && e.Source == boss.Id));
        }

        // Share: the circle's damage is split among the players standing in it.
        [Test]
        public void SharedCirclesSplitTheirDamage()
        {
            var world = Arena(out _, Bosses.With(Attack("share", damage: 40, radius: "2")));
            var alice = world.AddPlayer("a", null);
            var bob = world.AddPlayer("b", null);
            Walk(world, alice, Home + new Vec2(0, -5));
            Walk(world, bob, Home + new Vec2(0.5f, -5));
            Run(world, 0.1f);
            Assert.AreEqual(HazardShape.Share, world.Hazards.Single().Shape);
            Run(world, 1);
            Assert.AreEqual(PlayerStats.BaseHealth - 20, alice.Health, 0.01f);
            Assert.AreEqual(PlayerStats.BaseHealth - 20, bob.Health, 0.01f);

            var lone = Arena(out _, Bosses.With(Attack("share", damage: 40, radius: "2")));
            var carol = lone.AddPlayer("c", null);
            Walk(lone, carol, Home + new Vec2(0, -5));
            Run(lone, 1.1f);
            Assert.AreEqual(PlayerStats.BaseHealth - 40, carol.Health, 0.01f, "alone, one takes it all");
        }

        [Test]
        public void HazardShapesCoverTheRightGround()
        {
            var circle = new Hazard(1, 0, 0, HazardShape.Circle, new Vec2(0, 0), new Vec2(1, 0), 2, 0, 1, 0, 1);
            Assert.IsTrue(circle.Contains(new Vec2(2.2f, 0), 0.35f));
            Assert.IsFalse(circle.Contains(new Vec2(2.5f, 0), 0.35f));

            var cone = new Hazard(2, 0, 0, HazardShape.Cone, new Vec2(0, 0), new Vec2(1, 0), 4, 90, 1, 0, 1);
            Assert.IsTrue(cone.Contains(new Vec2(3, 2.5f), 0));
            Assert.IsFalse(cone.Contains(new Vec2(1, 2), 0));
            Assert.IsFalse(cone.Contains(new Vec2(-1, 0), 0));

            var line = new Hazard(3, 0, 0, HazardShape.Line, new Vec2(0, 0), new Vec2(0, 1), 6, 2, 1, 0, 1);
            Assert.IsTrue(line.Contains(new Vec2(1.2f, 5), 0.35f));
            Assert.IsFalse(line.Contains(new Vec2(1.5f, 5), 0.35f));
            Assert.IsFalse(line.Contains(new Vec2(0, 7), 0.35f));

            var ring = new Hazard(4, 0, 0, HazardShape.Ring, new Vec2(0, 0), new Vec2(1, 0), 6, 2, 1, 0, 1);
            Assert.IsFalse(ring.Contains(new Vec2(1, 0), 0.35f));
            Assert.IsTrue(ring.Contains(new Vec2(4, 0), 0.35f));
            Assert.IsFalse(ring.Contains(new Vec2(7, 0), 0.35f));
        }
    }
}
