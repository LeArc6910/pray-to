using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class WorldMapTests
    {
        static IEnumerable<TileType> Tiles(TileMap map) =>
            Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => map[x, y]));

        [Test]
        public void SameSeedBuildsTheSameCaves()
        {
            CollectionAssert.AreEqual(Tiles(WorldGenerator.Generate(42).Map), Tiles(WorldGenerator.Generate(42).Map));
            CollectionAssert.AreNotEqual(Tiles(WorldGenerator.Generate(42).Map), Tiles(WorldGenerator.Generate(43).Map));
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        public void CavesAreClosedOpenAtSpawnAndAllReachable(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            var map = world.Map;

            for (int i = 0; i < map.Width; i++)
            {
                Assert.IsTrue(map.IsSolid(i, 0) && map.IsSolid(i, map.Height - 1));
                Assert.IsTrue(map.IsSolid(0, i) && map.IsSolid(map.Width - 1, i));
            }
            Assert.IsFalse(Collision.Overlaps(map, world.Spawn, 2f), "the spawn needs room around it");

            int floor = Tiles(map).Count(t => t == TileType.Floor);
            var field = new FlowField(map);
            field.Build(new[] { world.Spawn }, int.MaxValue - 1);
            int reached = Enumerable.Range(0, map.Height)
                .Sum(y => Enumerable.Range(0, map.Width).Count(x => field.DistanceAt(x, y) != FlowField.Unreached));
            Assert.AreEqual(floor, reached);
            Assert.That(floor / (double)(map.Width * map.Height), Is.InRange(0.25, 0.75));
        }

        [Test]
        public void WallsStopMovementAtTheRadius()
        {
            var map = Maps.Room(5);

            var stopped = Collision.Move(map, new Vec2(2.5f, 2.5f), 0.35f, new Vec2(3, 0));

            Assert.AreEqual(4 - 0.35f, stopped.X, 0.01f);
            Assert.AreEqual(2.5f, stopped.Y, 0.001f);
        }

        [Test]
        public void MovingIntoAWallAtAnAngleSlidesAlongIt()
        {
            var map = Maps.Room(8);

            var slid = Collision.Move(map, new Vec2(6.5f, 2.5f), 0.35f, new Vec2(1, 1));

            Assert.AreEqual(7 - 0.35f, slid.X, 0.01f);
            Assert.AreEqual(3.5f, slid.Y, 0.01f);
        }

        [Test]
        public void InsideCornersPushOutAlongBothWalls()
        {
            var map = Maps.FromRows(
                "#####",
                "#...#",
                "#..##",
                "#####");

            // Tile (2, 1) is open with walls below it and to its right. A circle tucked into that corner should
            // come out just far enough from both walls.
            Assert.IsFalse(Collision.Overlaps(map, TileMap.Center(2, 1), 0.35f));
            var resolved = Collision.Resolve(map, new Vec2(2.7f, 1.3f), 0.35f);
            Assert.AreEqual(2.65f, resolved.X, 0.01f);
            Assert.AreEqual(1.35f, resolved.Y, 0.01f);
        }

        [Test]
        public void CirclesCannotSqueezeBetweenTilesTouchingAtACorner()
        {
            var map = Maps.FromRows(
                "####",
                "#.##",
                "##.#",
                "####");

            var position = Collision.Move(map, TileMap.Center(1, 2), 0.35f, new Vec2(1, -1));

            Assert.AreEqual(1, TileMap.Cell(position.X));
            Assert.AreEqual(2, TileMap.Cell(position.Y));
        }

        [Test]
        public void RandomWalksNeverEndInsideAWall()
        {
            var world = WorldGenerator.Generate(3);
            var random = new GameRandom(3);
            var position = world.Spawn;

            for (int i = 0; i < 5000; i++)
            {
                var input = Vec2.FromAngle(random.NextFloat() * 6.2831853f);
                position = PlayerMotor.Step(world.Map, position, input, moveSpeed: 9);
                Assert.IsFalse(Collision.Overlaps(world.Map, position, World.PlayerRadius - 0.001f), $"step {i} at {position}");
            }
        }

        [Test]
        public void FlowFieldLeadsAroundWalls()
        {
            var map = Maps.FromRows(
                "#######",
                "#.....#",
                "#.###.#",
                "#.#.#.#",
                "#.#.#.#",
                "#######");
            var field = new FlowField(map);
            field.Build(new[] { TileMap.Center(1, 1) }, 50);

            var position = TileMap.Center(5, 1);
            for (int i = 0; i < 400; i++)
            {
                var direction = field.DirectionFrom(position);
                if (direction.LengthSquared == 0) break;
                position = Collision.Move(map, position, 0.4f, direction * 0.1f);
            }

            Assert.AreEqual(1, TileMap.Cell(position.X));
            Assert.AreEqual(1, TileMap.Cell(position.Y));
            Assert.AreEqual(FlowField.Unreached, field.DistanceAt(3, 1), "the pocket in the middle has no way in");
        }

        [Test]
        public void FlowFieldDoesNotCutCorners()
        {
            var map = Maps.FromRows(
                "####",
                "#.##",
                "##.#",
                "####");
            var field = new FlowField(map);
            field.Build(new[] { TileMap.Center(1, 2) }, 50);

            Assert.AreEqual(FlowField.Unreached, field.DistanceAt(2, 1));
        }
    }
}
