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
        }
    }
}
