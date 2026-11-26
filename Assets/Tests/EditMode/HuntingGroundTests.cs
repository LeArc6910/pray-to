using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Version 8 (Docs/HuntingGrounds.md): hunting grounds and monsters beside the roads.
    public class HuntingGroundTests
    {
        static WorldHost Host(ulong seed, int version) =>
            new WorldHost(seed, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = version },
                new WorldConfig(), Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);

        static readonly WorldHost Eight = Host(2, WorldGenerator.HuntVersion);

        [Test]
        public void VersionEightMarksTwoHuntingGroundsABiomeOverVersionSevensTiles()
        {
            Assert.AreEqual(8, WorldGenerator.HuntVersion);
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.HuntVersion);
            var seven = WorldGenerator.Generate(2, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.LavaVoidVersion,
                Tiles.Shipped.Terrain);
            Assert.AreEqual(RoomMapTests.Hash(seven.Map), RoomMapTests.Hash(Eight.Generated.Map), "only marked: the same tiles");
            Assert.IsEmpty(seven.Rooms.HuntingGrounds);
            var rooms = Eight.Generated.Rooms;
            Assert.AreEqual(8, rooms.Version);
            foreach (var biome in rooms.Nodes.Select(n => n.Biome).Distinct())
                Assert.AreEqual(WorldGenerator.HuntPerBiome, rooms.HuntingGrounds.Count(i => rooms.Nodes[i].Biome == biome), biome.ToString());
            foreach (int index in rooms.HuntingGrounds)
            {
                var room = rooms.Nodes[index];
                Assert.IsFalse(room.Roadside);
                Assert.GreaterOrEqual(room.Radius, WorldGenerator.HuntMinRadius);
                Assert.IsTrue(room.Role == RoomRole.Pack || room.Role == RoomRole.Plain);
            }
            Assert.AreEqual(rooms.HuntingGrounds.Count, Eight.World.HuntingGrounds.Count);
        }

        [Test]
        public void AHuntingGroundFillsKeepsComingAndEmpties()
        {
            var host = Host(3, WorldGenerator.HuntVersion);
            var world = host.World;
            var room = world.HuntingGrounds.OrderBy(r => Vec2.Distance(r.Center, world.Spawn)).First();
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword", "iron_armor"));
            player.Position = Collision.Resolve(world.Map, room.Center + new Vec2(0, room.Radius + 4), World.PlayerRadius);
            world.MonstersHold = true;
            for (int i = 0; i < World.TicksPerSecond; i++) world.Step();
            var pack = world.Packs.Single(p => p.Hunt);
            int Living() => world.MembersOf(pack).Count();
            Assert.AreEqual(World.HuntFirst, Living(), "a crowd at first");
            for (int i = 0; i < 30 * World.TicksPerSecond; i++) world.Step();
            Assert.AreEqual(World.HuntCap, Living(), "then one every few seconds up to the cap");
            foreach (var enemy in world.MembersOf(pack).Take(4).ToList()) world.Hurt(enemy, player, 99999);
            Assert.Less(world.RewardShare(world.MembersOf(pack).First(), drops: true), world.RewardShare(world.MembersOf(pack).First()));
            for (int i = 0; i < 20 * World.TicksPerSecond; i++) world.Step();
            Assert.AreEqual(World.HuntCap, Living(), "they keep coming");
            player.Position = world.Spawn;
            for (int i = 0; i < World.TicksPerSecond; i++) world.Step();
            Assert.IsFalse(world.Packs.Any(p => p.Hunt), "everyone gone, they go");
        }

        [Test]
        public void VersionEightsMonstersComeBesideTheRoadsButNotOnThemNorByATown()
        {
            var seven = Host(2, WorldGenerator.LavaVoidVersion).World;
            var eight = Eight.World;
            var map = eight.Map;
            int besideSeven = 0, besideEight = 0, onRoad = 0, byTown = 0;
            foreach (var route in Eight.Generated.Roads.Where(r => r.Kind == RoadKind.Road))
            {
                for (int i = 0; i < route.Tiles.Count; i += 7)
                {
                    var (x, y) = route.Tiles[i];
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int tx = x + dx;
                        if (!map.InBounds(tx, y) || map.IsSolid(tx, y)) continue;
                        var at = TileMap.Center(tx, y);
                        bool town = eight.Places.Any(p => Vec2.Distance(at, p.Center) <= p.Radius + World.TownClearance);
                        if (eight.WandererSpot(tx, y))
                        {
                            besideEight++;
                            if (Structures.IsWay(map[tx, y])) onRoad++;
                            if (town) byTown++;
                        }
                        if (seven.WandererSpot(tx, y)) besideSeven++;
                    }
                }
            }
            Assert.AreEqual(0, besideSeven, "version 7: nothing by a road");
            Assert.Greater(besideEight, 100);
            Assert.AreEqual(0, onRoad);
            Assert.AreEqual(0, byTown);
        }
    }
}
