using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class MonsterTests
    {
        static readonly MonsterDefinition Slime = Monsters.Shipped.Find("slime");
        static readonly MonsterDefinition Shooter = Monsters.Shipped.Find("spore_shooter");
        static readonly MonsterDefinition Beetle = Monsters.Shipped.Find("cave_beetle");

        static World Room(int size = 30, TileMap map = null) =>
            new World(map ?? Maps.Room(size), TileMap.Center(size / 2, size / 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });

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
        public void ShippedMonstersBehaveDifferently()
        {
            Assert.IsTrue(Monsters.Shipped.Success, string.Join("\n", Monsters.Shipped.Errors));
            Assert.AreEqual(MonsterBehavior.Melee, Slime.Behavior);
            Assert.AreEqual(MonsterBehavior.Ranged, Shooter.Behavior);
            Assert.Greater(Shooter.ShotSpeed, 0);
            Assert.AreEqual(MonsterBehavior.Charge, Beetle.Behavior);
            Assert.Greater(Beetle.ChargeSpeed, 0);
            Assert.AreEqual("#8CD94D", Slime.Color);
            Assert.AreEqual(1, Slime.Level);
            Assert.AreEqual(2, Shooter.Level);
        }

        [Test]
        public void SheetErrorsNameTheMissingColumns()
        {
            string header = Monsters.Header.Replace("weight\n", "weight,behavior,shot_speed,keep_distance,charge_speed,color\n");
            var data = MonsterSheet.Parse(header +
                "a,가,1,10,1,0,1,0.3,5,1,0.5,1,1,ranged,,,,\n" +
                "b,나,1,10,1,0,1,0.3,5,1,0.5,1,1,charge,,,,\n" +
                "c,다,1,10,1,0,1,0.3,5,1,0.5,1,1,melee,,,,green\n");

            Assert.IsTrue(data.Errors.Any(e => e.Contains("2행") && e.Contains("shot_speed")), string.Join("\n", data.Errors));
            Assert.IsTrue(data.Errors.Any(e => e.Contains("3행") && e.Contains("charge_speed")));
            Assert.IsTrue(data.Errors.Any(e => e.Contains("4행") && e.Contains("#RRGGBB")));
        }

        [Test]
        public void TougherMonstersOnlyLiveFarOut()
        {
            var world = new World(Maps.Room(100), TileMap.Center(50, 50), Monsters.Shipped, 3, new WorldConfig { EnemyTarget = 300, SpawnDistance = 5 });

            Assert.AreEqual(1, world.ZoneLevelAt(world.Spawn + new Vec2(29, 0)));
            Assert.AreEqual(2, world.ZoneLevelAt(world.Spawn + new Vec2(31, 0)));
            var far = world.Enemies.Where(e => e.Definition.Level >= 2).ToList();
            Assert.IsNotEmpty(far);
            Assert.IsTrue(far.All(e => Vec2.Distance(e.Position, world.Spawn) >= 30));
            Assert.IsTrue(world.Enemies.Any(e => e.Definition == Slime && Vec2.Distance(e.Position, world.Spawn) < 30));
        }

        [Test]
        public void ShootersKeepTheirDistanceAndShoot()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var close = world.SpawnEnemy(Shooter, player.Position + new Vec2(2, 0));

            Run(world, 1);
            Assert.Greater(Vec2.Distance(close.Position, player.Position), 2.5f, "backed away");

            var events = Run(world, 3);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Attack && e.Source == close.Id));
            Assert.Less(player.Health, player.Stats.MaxHealth, "a player who stands still gets hit");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Hit && e.Source == close.Id));
        }

        [Test]
        public void ShootersDoNotShootThroughWalls()
        {
            var map = Maps.FromRows(
                "###########",
                "#.........#",
                "#.........#",
                "#....#....#",
                "#....#....#",
                "#....#....#",
                "#.........#",
                "###########");
            var world = new World(map, TileMap.Center(2, 4), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var player = world.AddPlayer("a", null);
            world.SpawnEnemy(Shooter, TileMap.Center(8, 4));

            world.Step();
            world.Step();

            Assert.IsEmpty(world.Projectiles);
            Assert.IsTrue(world.Enemies.All(e => e.State != EnemyState.Windup), "it walks round instead");
        }

        [Test]
        public void ChargersMarkTheirPathThenRush()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var beetle = world.SpawnEnemy(Beetle, player.Position + new Vec2(3, 0));

            Run(world, 0.2f);
            var path = world.Hazards.Single(h => h.Source == beetle.Id);
            Assert.AreEqual(HazardShape.Line, path.Shape);
            Assert.AreEqual(0, path.Damage, "only a mark; the rush itself hurts");

            var events = Run(world, 2);
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.Hit && e.Source == beetle.Id), "hit once per rush");
            Assert.AreEqual(player.Stats.MaxHealth - Beetle.Damage, player.Health, 0.01f);
        }

        [Test]
        public void SteppingOffTheMarkedPathDodgesTheRush()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var beetle = world.SpawnEnemy(Beetle, player.Position + new Vec2(3, 0));
            Run(world, 0.2f);
            Assert.AreEqual(EnemyState.Windup, beetle.State);

            for (int i = 0; i < 4; i++) world.MovePlayer(player.Id, player.Position + new Vec2(0, 0.5f), new Vec2(0, 1), 3);
            var events = Run(world, 1.2f);

            Assert.IsFalse(events.Any(e => e.Type == SimEventType.Hit && e.Source == beetle.Id));
            Assert.AreEqual(player.Stats.MaxHealth, player.Health);
        }
    }
}
