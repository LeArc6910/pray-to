using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Version 10's grown village and royal city (Docs/Towns.md).
    public class TownTests
    {
        static GeneratedWorld Make(ulong seed, int version) =>
            WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, version, Tiles.Shipped.Terrain);

        static HashSet<(int, int)> Reached(TileMap map, int x0, int y0, int reach)
        {
            var seen = new HashSet<(int, int)> { (x0, y0) };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((x0, y0));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (!map.InBounds(nx, ny) || System.Math.Abs(nx - x0) > reach || System.Math.Abs(ny - y0) > reach || map.IsSolid(nx, ny) || !seen.Add((nx, ny))) continue;
                    queue.Enqueue((nx, ny));
                }
            }
            return seen;
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        public void TheTownsAreGrownAndHaveTheirBuildingsAndEveryWayIn(ulong seed)
        {
            Assert.AreEqual(10, WorldGenerator.TownVersion);
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.TownVersion);
            var world = Make(seed, WorldGenerator.TownVersion);
            var village = world.Places.Single(p => p.Definition.Id == "dawn_village");
            var city = world.Places.Single(p => p.Definition.Id == "royal_city");
            Assert.AreEqual(WorldGenerator.VillageRadius, village.Radius);
            Assert.AreEqual(WorldGenerator.CityRadius, city.Radius);
            Assert.AreEqual(9, village.Definition.Radius, "the sheet is untouched");

            int Count(PlaceSite site, TileType type)
            {
                int n = 0;
                for (int y = site.Y - site.Radius; y <= site.Y + site.Radius; y++)
                    for (int x = site.X - site.Radius; x <= site.X + site.Radius; x++)
                        if (world.Map.InBounds(x, y) && world.Map[x, y] == type) n++;
                return n;
            }
            foreach (var type in new[] { TileType.Inn, TileType.Smithy, TileType.Temple, TileType.Stable }) Assert.GreaterOrEqual(Count(village, type), 1, $"village {type}");
            Assert.GreaterOrEqual(Count(village, TileType.Shop), 2);
            Assert.GreaterOrEqual(Count(village, TileType.Cottage) + Count(village, TileType.Cabin), 15);
            Assert.GreaterOrEqual(Count(village, TileType.Scarecrow), 2, "farm plots");
            foreach (var type in new[] { TileType.Palace, TileType.Throne, TileType.Barracks, TileType.Stable, TileType.Inn, TileType.Smithy }) Assert.AreEqual(1, Count(city, type), $"city {type}");
            Assert.AreEqual(2, Count(city, TileType.Temple));
            Assert.GreaterOrEqual(Count(city, TileType.Cottage) + Count(city, TileType.Cabin), 25);
            Assert.GreaterOrEqual(Count(city, TileType.Tower), 10);
            Assert.Greater(Count(city, TileType.Water), 100, "the moat");

            // From the waypoint every person, and every road that comes up to the plaza, is walked to.
            foreach (var site in new[] { village, city })
            {
                var reached = Reached(world.Map, site.X, site.Y, site.Radius + 3);
                foreach (var (character, at) in site.People)
                    Assert.IsTrue(reached.Contains((TileMap.Cell(at.X), TileMap.Cell(at.Y))), $"{site.Definition.Id}: {character.Name}");
                int edge = site.Radius + 2;
                for (int y = site.Y - edge; y <= site.Y + edge; y++)
                    for (int x = site.X - edge; x <= site.X + edge; x++)
                    {
                        int d = (x - site.X) * (x - site.X) + (y - site.Y) * (y - site.Y);
                        if (d > site.Radius * site.Radius && d <= edge * edge && world.Map[x, y] == TileType.Road)
                            Assert.IsTrue(reached.Contains((x, y)), $"{site.Definition.Id}: the road at ({x},{y})");
                    }
            }
        }

        [Test]
        public void WorldsOfVersionNineKeepTheirSmallPlaces()
        {
            var nine = Make(1, WorldGenerator.HarvestVersion);
            Assert.AreEqual(9, nine.Places.Single(p => p.Definition.Id == "dawn_village").Radius);
            Assert.AreEqual(10, nine.Places.Single(p => p.Definition.Id == "royal_city").Radius);
            for (int y = 0; y < nine.Map.Height; y++)
                for (int x = 0; x < nine.Map.Width; x++)
                    Assert.IsFalse(Structures.IsTownBuilding(nine.Map[x, y]));
        }
    }
}
