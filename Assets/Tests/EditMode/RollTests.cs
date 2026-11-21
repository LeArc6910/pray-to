using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The dodge roll (World.Roll.cs, Docs/Combat.md 구르기).
    public class RollTests
    {
        static readonly Vec2 East = new Vec2(1, 0);

        static World Room(int size = 30, TileMap map = null) =>
            new World(map ?? Maps.Room(size), TileMap.Center(size / 2, size / 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });

        [Test]
        public void TheNumbersAreTheDesigns()
        {
            Assert.AreEqual(9, World.RollTicks);
            Assert.AreEqual(6, World.DodgeTicks);
            Assert.AreEqual(27, World.RollCooldownTicks);
            Assert.AreEqual(3.2f, World.RollSpeed * World.RollTicks * World.TickSeconds, 1e-4f);
        }

        [Test]
        public void ARollGoesItsDistanceWhateverTheMoveSpeed()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var start = player.Position;

            Assert.IsTrue(world.Roll(player.Id, East));
            Assert.IsTrue(player.Rolling);
            var events = world.TakeEvents();
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Rolled && e.Source == player.Id && e.Direction.X > 0.9f), "others are told");
            for (int i = 0; i < World.RollTicks; i++)
            {
                Assert.IsTrue(world.MovePlayer(player.Id, PlayerMotor.Roll(world.Map, player.Position, East), East, 1, rolling: true));
                world.Step();
            }

            Assert.AreEqual(World.RollDistance, player.Position.X - start.X, 1e-3f);
            Assert.AreEqual(start.Y, player.Position.Y, 1e-4f);
            Assert.IsFalse(player.Rolling);
            Assert.AreEqual(0, player.Corrections);
        }

        [Test]
        public void RollStepsAreRefusedWithoutARoll()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var next = PlayerMotor.Roll(world.Map, player.Position, East);

            Assert.IsFalse(world.MovePlayer(player.Id, next, East, 1), "too fast for a walk");
            Assert.AreEqual(1, player.Corrections);
        }

        [Test]
        public void BlowsPassThroughInsideTheWindowAndLandAfterIt()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            float full = player.Health;

            Assert.IsTrue(world.Roll(player.Id, East));
            Assert.IsTrue(player.Dodging);
            Assert.AreEqual(0, world.Hurt(player, 10));
            Assert.AreEqual(0, world.Hurt(player, 10));
            Assert.AreEqual(full, player.Health);
            Assert.AreEqual(2, player.Dodges);
            Assert.AreEqual(1, world.TakeEvents().Count(e => e.Type == SimEventType.Dodged && e.Target == player.Id), "told once a roll");

            // The window is the design's ticks and the slack for the host hearing of it late.
            for (int i = 0; i < World.DodgeTicks + World.RollSlackTicks - 1; i++) world.Step();
            Assert.IsTrue(player.Dodging);
            world.Step();
            Assert.IsFalse(player.Dodging);
            Assert.IsTrue(player.Rolling || World.RollTicks <= World.DodgeTicks + World.RollSlackTicks, "the end of the roll can be hit");

            Assert.AreEqual(10, world.Hurt(player, 10));
            Assert.AreEqual(full - 10, player.Health);
        }

        [Test]
        public void AHitWithoutARollLands()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            Assert.AreEqual(10, world.Hurt(player, 10));
            Assert.AreEqual(0, player.Dodges);
        }

        [Test]
        public void ABurnAlreadyOnThePlayerIsNotDodged()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            float full = player.Health;
            // One that burns out inside the roll's unhurt ticks.
            player.Afflictions.Burn(30, World.DodgeTicks - 2, 0);

            Assert.IsTrue(world.Roll(player.Id, East));
            for (int i = 0; i < World.DodgeTicks; i++) world.Step();

            Assert.IsTrue(player.Dodging);
            Assert.Less(player.Health, full);
        }

        [Test]
        public void TheNextRollWaitsForTheCooldown()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);

            Assert.IsTrue(world.Roll(player.Id, East));
            Assert.IsFalse(world.Roll(player.Id, East));
            for (int i = 0; i < World.RollCooldownTicks - World.RollSlackTicks - 1; i++)
            {
                world.Step();
                Assert.IsFalse(world.Roll(player.Id, East), $"tick {i}");
            }
            world.Step();
            Assert.IsTrue(world.Roll(player.Id, East), "the slack lets a client whose ticks run a little ahead roll");
            Assert.AreEqual(2, player.Rolls);
        }

        [Test]
        public void WallsStopARoll()
        {
            // Two tiles of floor east of the player, then rock.
            var map = Maps.FromRows(
                "#######",
                "#.....#",
                "#######");
            var world = new World(map, TileMap.Center(2, 1), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var player = world.AddPlayer("a", null);
            Assert.AreEqual(TileMap.Center(2, 1), player.Position);

            Assert.IsTrue(world.Roll(player.Id, East));
            for (int i = 0; i < World.RollTicks; i++)
            {
                world.MovePlayer(player.Id, PlayerMotor.Roll(world.Map, player.Position, East), East, 1, rolling: true);
                world.Step();
            }

            Assert.AreEqual(6 - World.PlayerRadius, player.Position.X, 0.01f, "against the wall, not in or past it");
            Assert.IsFalse(Collision.Overlaps(map, player.Position, World.PlayerRadius - 0.01f));
            // And a client that claims to be in the wall is put back.
            Assert.IsFalse(world.MovePlayer(player.Id, new Vec2(6.2f, player.Position.Y), East, 1, rolling: true));
        }

        [Test]
        public void TheFallenDoNotRoll()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            world.Hurt(player, 10000);
            Assert.IsTrue(player.IsDead);

            Assert.IsFalse(world.Roll(player.Id, East));
            Assert.IsFalse(player.Rolling);
            Assert.AreEqual(0, player.Rolls);
        }

        [Test]
        public void FallingEndsTheRoll()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            Assert.IsTrue(world.Roll(player.Id, East));
            for (int i = 0; i < World.DodgeTicks + World.RollSlackTicks; i++) world.Step();
            player.RollTicks = 3;

            world.Hurt(player, 10000);

            Assert.IsTrue(player.IsDead);
            Assert.IsFalse(player.Rolling);
        }

        [Test]
        public void ARollWithNoDirectionGoesTheWayThePlayerFaces()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 1);

            Assert.IsTrue(world.Roll(player.Id, Vec2.Zero));

            Assert.AreEqual(new Vec2(0, 1), player.RollDirection);
        }

        [Test]
        public void ARollEndsASwingsRecoveryAndNoSwingComesOutDuringIt()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            world.SetAttacking(player.Id, true);
            world.Step();
            Assert.AreEqual(1, world.TakeEvents().Count(e => e.Type == SimEventType.Attack));
            Assert.Greater(player.AttackCooldown, World.RollTicks, "still recovering when the roll ends, without it");

            Assert.IsTrue(world.Roll(player.Id, East));
            var during = new List<SimEvent>();
            for (int i = 0; i < World.RollTicks - 1; i++)
            {
                world.Step();
                during.AddRange(world.TakeEvents());
            }
            Assert.IsFalse(during.Any(e => e.Type == SimEventType.Attack), "no swing in the roll");

            world.Step();
            var after = world.TakeEvents().Where(e => e.Type == SimEventType.Attack).ToList();
            Assert.AreEqual(1, after.Count, "and one at once after it");
            Assert.AreEqual(1, World.ComboStepOf(after[0].Amount), "the combo goes on through the roll");
        }

        [Test]
        public void AChillAndAWebShortenTheRoll()
        {
            var world = Room();
            var start = TileMap.Center(15, 15);
            var plain = start;
            var snared = start;
            for (int i = 0; i < World.RollTicks; i++)
            {
                plain = PlayerMotor.Roll(world.Map, plain, East);
                snared = PlayerMotor.Roll(world.Map, snared, East, World.RollSlow(false, true));
            }
            Assert.AreEqual(World.RollDistance * World.PlayerSnareSpeed, snared.X - start.X, 1e-3f);
            Assert.AreEqual(World.RollDistance, plain.X - start.X, 1e-3f);
            Assert.AreEqual(World.PlayerChillSpeed * World.PlayerSnareSpeed, World.RollSlow(true, true), 1e-6f);
        }

        [Test]
        public void AMonstersBiteMissesARollingPlayer()
        {
            var slime = Monsters.Shipped.Find("slime");
            var world = Room(14);
            var player = world.AddPlayer("a", null);
            var enemy = world.SpawnEnemy(slime, player.Position + new Vec2(0.9f, 0));
            world.Step();
            world.Step();
            Assert.AreEqual(EnemyState.Windup, enemy.State);
            // Stand still through the wind-up and roll on the spot (no step reported) just as the bite lands.
            for (int i = 0; i < World.Ticks(slime.Windup) - 2; i++) world.Step();
            world.TakeEvents();
            Assert.IsTrue(world.Roll(player.Id, East));
            var events = new List<SimEvent>();
            for (int i = 0; i < 3; i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }

            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Attack && e.Source == enemy.Id), "it bit");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Dodged && e.Target == player.Id && e.Source == enemy.Id));
            Assert.AreEqual(player.Stats.MaxHealth, player.Health);
        }
    }
}
