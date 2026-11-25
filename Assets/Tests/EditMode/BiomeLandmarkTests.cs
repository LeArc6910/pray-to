using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The biomes' landmarks (Docs/BiomeRework.md 묶음 B1c): rare things laid in some rooms of a version 4 world before
    // its scattered props, which are denser there. An old miner's hut with a fenced yard and the frame of a boarded-up drift against the rock
    // in the dirt cave, a fairy ring in the mushroom forest. Versions 2 and 3 have none and are as they were made; the same seed lays the same ones; each
    // is on floor that was bare, with a way round it; a ring's caps are walked through and picked.
    public class BiomeLandmarkTests
    {
        const int Size = 512;
        static readonly ulong[] Seeds = { 1, 2, 3, 4 };

        static readonly Dictionary<(ulong, int), GeneratedWorld> Made = new Dictionary<(ulong, int), GeneratedWorld>();

        static GeneratedWorld Generated(ulong seed, int version)
        {
            if (!Made.TryGetValue((seed, version), out var world))
                Made[(seed, version)] = world = WorldGenerator.Generate(seed, Size, Size, Tales.Shipped, Bosses.Staged.Arenas, version, Tiles.Shipped.Terrain);
            return world;
        }

        static (int X, int Y, TileType Type)[] Layout(BiomeLandmark mark) => mark.Kind == BiomeLandmarkKind.MinerHut ? BiomeLandmarks.MinerHut
            : mark.Kind == BiomeLandmarkKind.MineFrame ? BiomeLandmarks.MineFrame : mark.Width == 7 ? BiomeLandmarks.FairyRing : BiomeLandmarks.SmallRing;

        [Test]
        public void VersionsTwoAndThreeHaveNoLandmarksAndVersionFourLaysTheSameOnesEachTime()
        {
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.LandmarkVersion, "a new world has the landmarks (and from version 5 the ice cave's and the sand cave's)");
            Assert.Greater(WorldGenerator.LandmarkVersion, WorldGenerator.PropVersion);
            foreach (int version in new[] { WorldGenerator.RoomVersion, WorldGenerator.PropVersion })
            {
                var old = Generated(1, version);
                Assert.AreEqual(version, old.Version);
                Assert.AreEqual(0, old.BiomeLandmarks.Count);
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        Assert.IsFalse(BiomeLandmarks.Is(old.Map[x, y]), $"a landmark's tile at {x},{y} of a version {version} world");
            }
            Assert.AreEqual(WorldGenerator.LandmarkVersion, Generated(1, WorldGenerator.LandmarkVersion).Version);

            var made = Generated(1, WorldGenerator.LandmarkVersion);
            var again = WorldGenerator.Generate(1, Size, Size, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.LandmarkVersion, Tiles.Shipped.Terrain);
            Assert.AreEqual(RoomMapTests.Hash(made.Map), RoomMapTests.Hash(again.Map));
            CollectionAssert.AreEqual(made.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom, l.Width)), again.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom, l.Width)));
        }

        // A world made at version 3 (묶음 B1a, live since 2026-11-24) is made the same for ever: the hashes of its maps as
        // the code of that day made them (commit fe99e94), without the story's places and with them.
        [TestCase(1UL, 9286895792707084182UL, 11138141934581874261UL)]
        [TestCase(2UL, 2774438440319024708UL, 8495030137196241124UL)]
        [TestCase(3UL, 7415677465583928469UL, 8973264031662636262UL)]
        public void VersionThreeIsAsItWasMade(ulong seed, ulong plain, ulong storied)
        {
            Assert.AreEqual(plain, RoomMapTests.Hash(WorldGenerator.Generate(seed, Size, Size, version: WorldGenerator.PropVersion, terrain: Tiles.Shipped.Terrain).Map));
            Assert.AreEqual(storied, RoomMapTests.Hash(Generated(seed, WorldGenerator.PropVersion).Map));
        }

        // Version 4 is version 3's map with more on its open floor: landmarks, and props where it had none.
        [Test]
        public void VersionFourOnlyAddsToVersionThreesOpenFloor()
        {
            var old = Generated(1, WorldGenerator.PropVersion).Map;
            var made = Generated(1, WorldGenerator.LandmarkVersion);
            int props = 0, before = 0;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (BiomeProps.Is(old[x, y])) before++;
                    if (BiomeProps.Is(made.Map[x, y])) props++;
                    if (made.Map[x, y] == old[x, y]) continue;
                    // A prop of version 3 may be gone (a landmark or another prop took its room), never rock or a place's thing.
                    Assert.IsTrue(old[x, y] == TileType.Floor || BiomeProps.Is(old[x, y]) || old[x, y] == TileType.StructurePart, $"{x},{y}: {old[x, y]} became {made.Map[x, y]}");
                }
            }
            Assert.Greater(props, before, "version 4 has more props");
        }

        [Test]
        public void EveryKindIsLaidSomewhereAndFewOfEach()
        {
            var counts = new Dictionary<BiomeLandmarkKind, int>();
            foreach (ulong seed in Seeds)
            {
                var made = Generated(seed, WorldGenerator.LandmarkVersion);
                foreach (var mark in made.BiomeLandmarks) counts[mark.Kind] = counts.GetValueOrDefault(mark.Kind) + 1;
                int rooms = made.Rooms.Nodes.Count(r => r.Biome == Biome.Cave || r.Biome == Biome.Mushroom);
                Assert.LessOrEqual(made.BiomeLandmarks.Count, Math.Max(3, rooms / 6), $"seed {seed}: landmarks are rare ({made.BiomeLandmarks.Count} in {rooms} rooms)");
            }
            foreach (BiomeLandmarkKind kind in Enum.GetValues(typeof(BiomeLandmarkKind)))
                // The ice cave's and the sand cave's are version 5's (IceSandLookTests), the crystal cave's and the lake's
                // version 6's (CrystalLakeLookTests).
                if (!IceSandProps.Lays(kind) && !CrystalLakeProps.Lays(kind)) Assert.Greater(counts.GetValueOrDefault(kind), 0, $"{kind} in none of the seeds");
        }

        [Test]
        public void EachIsLaidAsDrawnOnFloorThatWasBareInItsOwnBiome()
        {
            foreach (ulong seed in Seeds)
            {
                var old = Generated(seed, WorldGenerator.RoomVersion).Map;
                var made = Generated(seed, WorldGenerator.LandmarkVersion);
                var map = made.Map;
                var marks = made.BiomeLandmarks;
                foreach (var mark in marks)
                {
                    var biome = mark.Kind == BiomeLandmarkKind.FairyRing ? Biome.Mushroom : Biome.Cave;
                    Assert.AreEqual(biome, made.Biomes[mark.Left, mark.Bottom], $"seed {seed}: {mark.Kind} at {mark.Left},{mark.Bottom}");
                    var expected = new Dictionary<(int, int), TileType>();
                    foreach (var (dx, dy, type) in Layout(mark))
                    {
                        var (w, h) = Structures.Footprint(type);
                        for (int j = 0; j < h; j++)
                            for (int i = 0; i < w; i++)
                                expected[(mark.Left + dx + i, mark.Bottom + dy + j)] = i == 0 && j == 0 ? type : TileType.StructurePart;
                    }
                    for (int y = mark.Bottom; y < mark.Bottom + mark.Height; y++)
                    {
                        for (int x = mark.Left; x < mark.Left + mark.Width; x++)
                        {
                            // A frame's lamp and sack stand only where the rock leaves floor beside its posts.
                            bool loose = mark.Kind == BiomeLandmarkKind.MineFrame && y == mark.Bottom + 4 && (x == mark.Left || x == mark.Left + 4);
                            if (loose && map[x, y] == old[x, y]) continue;
                            Assert.AreEqual(TileType.Floor, old[x, y], $"seed {seed}: {mark.Kind} at {x},{y} was bare floor");
                            Assert.AreEqual(expected.TryGetValue((x, y), out var type) ? type : TileType.Floor, map[x, y], $"seed {seed}: {mark.Kind} at {x},{y}");
                        }
                    }
                    Assert.IsFalse(map.IsSolid(mark.StandX, mark.StandY), "somewhere to stand before it");
                    if (mark.Kind == BiomeLandmarkKind.MineFrame)
                    {
                        for (int i = 1; i <= 3; i++) Assert.AreEqual(TileType.Wall, map[mark.Left + i, mark.Bottom + 5], "a drift's frame is set under rock");
                        continue;
                    }
                    if (mark.Kind == BiomeLandmarkKind.FairyRing) continue;
                    // The hut and its yard: open floor all round the box.
                    for (int y = mark.Bottom - 1; y <= mark.Bottom + mark.Height; y++)
                        for (int x = mark.Left - 1; x <= mark.Left + mark.Width; x++)
                            if (!mark.Holds(x, y)) Assert.AreEqual(TileType.Floor, map[x, y], $"seed {seed}: the tile at {x},{y} beside a {mark.Kind}");
                }
                // No two share a tile; none in a place or an arena, none on the middle of a room or by the spawn.
                for (int a = 0; a < marks.Count; a++)
                    for (int b = a + 1; b < marks.Count; b++)
                        Assert.IsFalse(marks[a].Left < marks[b].Left + marks[b].Width && marks[b].Left < marks[a].Left + marks[a].Width
                            && marks[a].Bottom < marks[b].Bottom + marks[b].Height && marks[b].Bottom < marks[a].Bottom + marks[a].Height, "two landmarks overlap");
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        if (!BiomeLandmarks.Is(map[x, y])) continue;
                        Assert.IsTrue(marks.Any(m => m.Holds(x, y)), $"seed {seed}: {map[x, y]} at {x},{y} outside every landmark");
                        foreach (var site in made.Places)
                            Assert.Greater(Math.Max(Math.Abs(x - site.X), Math.Abs(y - site.Y)), site.Radius, $"a landmark in {site.Definition.Id}");
                        foreach (var arena in made.Arenas)
                            Assert.Greater(Vec2.Distance(TileMap.Center(x, y), arena), made.ArenaRadius, "a landmark in an arena");
                        if (map.IsSolid(x, y)) Assert.Greater(Math.Max(Math.Abs(x - made.SpawnX), Math.Abs(y - made.SpawnY)), 5, "none by the spawn");
                    }
                }
            }
        }

        [Test]
        public void NothingWalkedToBeforeIsShutOff()
        {
            foreach (ulong seed in Seeds)
            {
                var before = Walk(Generated(seed, WorldGenerator.RoomVersion));
                var made = Generated(seed, WorldGenerator.LandmarkVersion);
                var after = Walk(made);
                for (int i = 0; i < before.Length; i++)
                    if (before[i] >= 0 && !made.Map.IsSolid(i % Size, i / Size)) Assert.GreaterOrEqual(after[i], 0, $"seed {seed}: {i % Size},{i / Size} is shut off");
                foreach (var mark in made.BiomeLandmarks)
                    if (before[mark.StandY * Size + mark.StandX] >= 0) Assert.GreaterOrEqual(after[mark.StandY * Size + mark.StandX], 0, $"seed {seed}: the {mark.Kind} at {mark.Left},{mark.Bottom} is walked to");
            }
        }

        static int[] Walk(GeneratedWorld world)
        {
            var map = world.Map;
            var steps = Enumerable.Repeat(-1, map.Width * map.Height).ToArray();
            var queue = new Queue<int>();
            steps[world.SpawnY * map.Width + world.SpawnX] = 0;
            queue.Enqueue(world.SpawnY * map.Width + world.SpawnX);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                int x = at % map.Width, y = at / map.Width;
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (ny >= map.Height || !map.InBounds(nx, ny) || steps[ny * map.Width + nx] >= 0 || map.IsSolid(nx, ny) || map[nx, ny] == TileType.Lava) continue;
                    steps[ny * map.Width + nx] = steps[at] + 1;
                    queue.Enqueue(ny * map.Width + nx);
                }
            }
            return steps;
        }

        [Test]
        public void TheHostAndTheClientLayTheSameLandmarks()
        {
            var host = new WorldHost(3, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 512, MapVersion = WorldGenerator.LandmarkVersion },
                bosses: Bosses.Staged, classes: Classes.Shipped, tiles: Tiles.Shipped, story: Tales.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Staged, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            Assert.Greater(host.Generated.BiomeLandmarks.Count, 0);
            Assert.AreEqual(WorldGenerator.LandmarkVersion, client.Generator);
            Assert.AreEqual(WorldGenerator.LandmarkVersion, WorldSave.Read(host.SaveWorld(now: 5), Tiles.Shipped).Generator);
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(client.Map));
            foreach (var mark in host.Generated.BiomeLandmarks)
                foreach (var (dx, dy, type) in Layout(mark))
                    if (BiomeLandmarks.Is(type)) Assert.AreEqual(type, client.Map[mark.Left + dx, mark.Bottom + dy], $"the client's {mark.Kind}");
        }

        [Test]
        public void ARingsCapIsWalkedThroughAndPickedAndTheHutAndTheFrameAreNeverDug()
        {
            Assert.IsFalse(new TileMap(3, 3, TileType.RingCap).IsSolid(1, 1), "a cap of a fairy ring is walked through");
            Assert.IsTrue(new TileMap(3, 3, TileType.MinerHut).IsSolid(1, 1));
            Assert.IsTrue(new TileMap(3, 3, TileType.MineFrame).IsSolid(1, 1));
            Assert.IsNotNull(Tiles.Shipped.Find(TileType.RingCap));
            Assert.IsNull(Tiles.Shipped.Find(TileType.MinerHut), "a landmark is never dug");
            Assert.IsNull(Tiles.Shipped.Find(TileType.MineFrame), "a landmark is never dug");
            Assert.AreEqual((3, 2), Structures.Footprint(TileType.MinerHut));
            Assert.AreEqual((3, 1), Structures.Footprint(TileType.MineFrame));
            Assert.Greater(TileLight.RadiusOf(TileType.RingCap, Tiles.Shipped), 0, "the ring glows a little");

            var map = Maps.FromRows(
                "########",
                "#......#",
                "#......#",
                "#......#",
                "########");
            map[3, 2] = TileType.RingCap;
            var world = new World(map, TileMap.Center(2, 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            world.SetAttacking(player.Id, true);
            for (int tick = 0; tick < 3 * World.TicksPerSecond && world.Map[3, 2] == TileType.RingCap; tick++) world.Step();
            world.SetAttacking(player.Id, false);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2], "a swing picks it");
            Assert.GreaterOrEqual(player.Inventory.CountOf(Gear.Data.FindMaterial("mushroom")), 1);
            Assert.IsTrue(world.ChangedTiles().Any(t => t.X == 3 && t.Y == 2), "a changed tile: saved and sent like any other");
        }
    }
}
