using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The towns' people and the people of the story keep apart instead of walking through one another (Core Crowd,
    // the owner's ask of 2026-11-29).
    public class CrowdTests
    {
        static TileMap Open(int size = 20) => new TileMap(size, size, TileType.Floor);

        [Test]
        public void TwoWhoMeetHeadOnStepAsideAndPass()
        {
            var map = Open();
            float least = float.MaxValue;
            Vec2? lastA = null, lastB = null;
            for (int k = 0; k <= 40; k++)
            {
                // A walks east along y = 10, B west along the same line; they meet at x = 10.
                float t = k / 40f;
                var a = new Vec2(8 + 4 * t, 10);
                var b = new Vec2(12 - 4 * t, 10);
                var spread = Crowd.Spread(new[] { a, b }, new[] { new Vec2(1, 0), new Vec2(-1, 0) }, map);
                least = System.Math.Min(least, Vec2.Distance(spread[0], spread[1]));
                Assert.LessOrEqual(Vec2.Distance(spread[0], a), Crowd.MaxNudge + 1e-4f, "never far from their way");
                // Each keeps to their own right: A (going east) to the south, B (going west) to the north.
                Assert.LessOrEqual(spread[0].Y, 10.0001f);
                Assert.GreaterOrEqual(spread[1].Y, 9.9999f);
                if (lastA is Vec2 la && lastB is Vec2 lb)
                {
                    Assert.Less(Vec2.Distance(spread[0], la), 0.2f, "no jump from one moment to the next");
                    Assert.Less(Vec2.Distance(spread[1], lb), 0.2f);
                }
                lastA = spread[0];
                lastB = spread[1];
            }
            Assert.GreaterOrEqual(least, Crowd.Gap * 0.6f, "they pass side by side, not through each other");
        }

        [Test]
        public void OneWhoStandsLetsTheWalkerGoRound()
        {
            var map = Open();
            var stand = new Vec2(10, 10);
            var walker = new Vec2(10.2f, 10.1f);
            var spread = Crowd.Spread(new[] { stand, walker }, new[] { Vec2.Zero, new Vec2(1, 0) }, map);
            Assert.Less(Vec2.Distance(spread[0], stand), 0.1f, "the one standing barely moves");
            Assert.Greater(Vec2.Distance(spread[1], spread[0]), Vec2.Distance(walker, stand) + 0.2f, "the walker steps round");
        }

        [Test]
        public void NoOneIsNudgedIntoAWall()
        {
            var map = Open();
            for (int x = 0; x < 20; x++) map[x, 9] = TileType.Wall;
            // Two standing on top of each other right above a wall: the push is down-and-up, and the one going down
            // stays where they are.
            var a = new Vec2(10, 10.2f);
            var b = new Vec2(10, 10.4f);
            var spread = Crowd.Spread(new[] { a, b }, new[] { Vec2.Zero, Vec2.Zero }, map);
            foreach (var p in spread) Assert.IsFalse(map.IsSolid(TileMap.Cell(p.X), TileMap.Cell(p.Y)));
            Assert.Greater(Vec2.Distance(spread[0], spread[1]), Vec2.Distance(a, b));
        }

        [Test]
        public void TheTownsPeopleKeepApartOverADay()
        {
            var kinds = TownsfolkSheet.Parse(System.IO.File.ReadAllText(RepoFile.Find("Assets/_Project/Data/Story/" + TownsfolkSheet.File)));
            var world = WorldGenerator.Generate(2, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas,
                WorldGenerator.TownVersion, Tiles.Shipped.Terrain);
            int before = 0, after = 0, checks = 0;
            foreach (var site in world.Places.Where(p => p.Definition.Style == "village" || p.Definition.Style == "kingdom"))
            {
                var folk = Townsfolk.Of(site, world.Map, kinds, 2);
                var standing = folk.Where(p => p.Route.Count == 1).Select(p => p.Route[0]).ToList();
                Assert.AreEqual(standing.Count, standing.Distinct().Count(), $"{site.Definition.Id}: no two stand on one spot");
                for (long clock = 600; clock < 12000; clock += 37)
                {
                    var at = new List<Vec2>();
                    var heading = new List<Vec2>();
                    foreach (var (character, home) in site.People)
                    {
                        var p = site.PersonAt(character, home, clock);
                        at.Add(p);
                        heading.Add(p - site.PersonAt(character, home, clock - 6));
                    }
                    foreach (var person in folk)
                    {
                        var p = person.At(clock);
                        at.Add(p);
                        heading.Add(p - person.At(clock - 6));
                    }
                    var spread = Crowd.Spread(at, heading, world.Map);
                    for (int i = 0; i < at.Count; i++)
                    {
                        Assert.IsFalse(world.Map.IsSolid(TileMap.Cell(spread[i].X), TileMap.Cell(spread[i].Y)), "kept on open ground");
                        for (int j = i + 1; j < at.Count; j++)
                        {
                            if (Vec2.Distance(at[i], at[j]) < 0.35f) before++;
                            if (Vec2.Distance(spread[i], spread[j]) < 0.35f) after++;
                        }
                    }
                    checks++;
                }
            }
            Assert.Greater(checks, 0);
            TestContext.WriteLine($"pairs closer than 0.35 tiles: {before} as the clock puts them, {after} nudged apart");
            Assert.LessOrEqual(after, before / 4, "most who would walk through one another keep apart");
        }
    }
}
