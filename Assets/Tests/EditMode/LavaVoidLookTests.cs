using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The lava zone's and the void abyss's looks (Docs/BiomeRework.md 묶음 B4a): the generator's version 7 lays their
    // landmarks and props over version 6's map, on open floor (and, for a rock in the lava, on a pool's lava) and
    // shutting nothing off; versions 2 to 6 are as they were made; the same seed lays the same things on the host and
    // the client; the props are dug for what the sheet says; the lava's edge has its own cover.
    public class LavaVoidLookTests
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

        static bool Mine(TileType type) => LavaVoidProps.Is(type) || LavaVoidProps.IsLandmark(type);

        // What the other half of the package (B4b: the hazards and the rest) may lay after these on what was open floor
        // or lava: any tile this half does not know.
        static bool Others(TileType type) => type > TileType.StarAltar;

        // A world made at version 6 (묶음 B3, live since 2026-11-25) is made the same for ever: the hashes of its maps as
        // the code of that day made them (commit 48cb129), without the story's places and with them.
        [TestCase(1UL, 5274504754179708031UL, 16550109542144776297UL)]
        [TestCase(2UL, 11438534846344511411UL, 9553517340583536803UL)]
        [TestCase(3UL, 5547484019641265516UL, 2023057608017035076UL)]
        public void VersionSixIsAsItWasMade(ulong seed, ulong plain, ulong storied)
        {
            Assert.AreEqual(plain, RoomMapTests.Hash(WorldGenerator.Generate(seed, Size, Size, version: WorldGenerator.CrystalLakeVersion, terrain: Tiles.Shipped.Terrain).Map));
            Assert.AreEqual(storied, RoomMapTests.Hash(Generated(seed, WorldGenerator.CrystalLakeVersion).Map));
        }

        [Test]
        public void ANewWorldIsVersionSevenAndTheOlderVersionsHaveNoneOfItsThings()
        {
            Assert.AreEqual(7, WorldGenerator.LavaVoidVersion);
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.LavaVoidVersion);
            Assert.Greater(WorldGenerator.LavaVoidVersion, WorldGenerator.CrystalLakeVersion);
            foreach (int version in new[] { WorldGenerator.RoomVersion, WorldGenerator.PropVersion, WorldGenerator.LandmarkVersion, WorldGenerator.IceSandVersion, WorldGenerator.CrystalLakeVersion })
            {
                var old = Generated(1, version);
                Assert.AreEqual(version, old.Version);
                Assert.IsFalse(old.BiomeLandmarks.Any(l => LavaVoidProps.Lays(l.Kind)), $"version {version} has none of the two biomes' landmarks");
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        Assert.IsFalse(Mine(old.Map[x, y]), $"{old.Map[x, y]} at {x},{y} of a version {version} world");
            }
            var made = Generated(1, WorldGenerator.LavaVoidVersion);
            Assert.AreEqual(WorldGenerator.LavaVoidVersion, made.Version);
            var again = WorldGenerator.Generate(1, Size, Size, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.LavaVoidVersion, Tiles.Shipped.Terrain);
            Assert.AreEqual(RoomMapTests.Hash(made.Map), RoomMapTests.Hash(again.Map), "the same seed, the same map");
            CollectionAssert.AreEqual(made.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)), again.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)));
        }

        // The tiles round a footprint were bare floor or lava in version 6, the floor among them one run and more than half.
        static void AssertWalkedRound(TileMap map, TileMap old, int x, int y, int w, string what)
        {
            var ring = new List<(int X, int Y)>();
            for (int i = -1; i <= w; i++) ring.Add((x + i, y - 1));
            ring.Add((x + w, y));
            for (int i = w; i >= -1; i--) ring.Add((x + i, y + 1));
            ring.Add((x - 1, y));
            int turns = 0, floor = 0;
            // As version 6 had it, so a hazard the other half lays on the floor beside it does not count against it.
            bool Walked(int i) => old[ring[i].X, ring[i].Y] == TileType.Floor && !Mine(map[ring[i].X, ring[i].Y]);
            for (int i = 0; i < ring.Count; i++)
            {
                var was = old[ring[i].X, ring[i].Y];
                Assert.IsTrue(was == TileType.Floor || was == TileType.Lava, $"{what}: {was} beside it");
                Assert.IsFalse(Mine(map[ring[i].X, ring[i].Y]) && map[ring[i].X, ring[i].Y] != TileType.LavaRock, $"{what}: {map[ring[i].X, ring[i].Y]} touches it");
                if (Walked(i)) floor++;
                if (Walked(i) != Walked((i + 1) % ring.Count)) turns++;
            }
            Assert.LessOrEqual(turns, 2, $"{what}: the floor round it is one run");
            Assert.Greater(floor * 2, ring.Count, $"{what}: more floor than lava round it");
        }

        // Version 7 is version 6's map with the two biomes' things on what was its open floor, a rock on what was a
        // pool's lava (a camp's trapdoor, laid after them by its own stream, may stand elsewhere in its camp; the other
        // half's tiles may lie on what was floor or lava).
        [TestCase(1UL)]
        [TestCase(3UL)]
        public void VersionSevenOnlyTurnsOpenFloorAndLavaOfTheTwoBiomesIntoTheirThings(ulong seed)
        {
            var old = Generated(seed, WorldGenerator.CrystalLakeVersion);
            var made = Generated(seed, WorldGenerator.LavaVoidVersion);
            var map = made.Map;
            int props = 0, solidBefore = 0, solidAfter = 0, shore = 0;
            var kinds = new HashSet<TileType>();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (old.Map.IsSolid(x, y)) solidBefore++;
                    if (map.IsSolid(x, y)) solidAfter++;
                    var was = old.Map[x, y];
                    var type = map[x, y];
                    if (type == was) continue;
                    if (was == TileType.Trapdoor || type == TileType.Trapdoor)
                    {
                        Assert.IsTrue(was == TileType.Floor || type == TileType.Floor, $"{x},{y}: a trapdoor moved onto {(was == TileType.Trapdoor ? type : was)}");
                        continue;
                    }
                    if (Others(type) || type == TileType.Floor)
                    {
                        Assert.IsTrue(was == TileType.Floor || was == TileType.Lava || Others(was), $"{x},{y}: {type} on {was}");
                        continue;
                    }
                    Assert.AreEqual(type == TileType.LavaRock ? TileType.Lava : TileType.Floor, was, $"{x},{y}: only open floor changes, or a pool's lava under a rock ({was} became {type})");
                    if (type == TileType.StructurePart)
                    {
                        Assert.IsTrue(BiomeProps.Wide(map[x - 1, y]) && LavaVoidProps.Is(map[x - 1, y]) || made.BiomeLandmarks.Any(l => LavaVoidProps.Lays(l.Kind) && l.Holds(x, y)),
                            $"{x},{y}: part of nothing");
                        continue;
                    }
                    var biome = made.Biomes[x, y];
                    Assert.IsTrue(biome == Biome.Lava || biome == Biome.Abyss, $"{x},{y}: {type} in the {biome}");
                    Assert.IsTrue(Mine(type), $"{x},{y}: {type}");
                    if (made.BiomeLandmarks.Any(l => l.Holds(x, y))) continue;
                    Assert.IsTrue(LavaVoidProps.Any(biome).Any(k => k.Type == type), $"{x},{y}: {type} in the {biome}");
                    props++;
                    kinds.Add(type);
                    int w = Structures.Footprint(type).W;
                    if (type == TileType.LavaRock)
                    {
                        // Lava all round it.
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if (dy != 0 || dx != 0) Assert.AreEqual(TileType.Lava, old.Map[x + dx, y + dy], $"the rock at {x},{y}: the tile at {dx},{dy} from it");
                        continue;
                    }
                    bool byLava = false;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= w; dx++)
                            if (old.Map[x + dx, y + dy] == TileType.Lava) byLava = true;
                    if (byLava)
                    {
                        shore++;
                        Assert.AreEqual(Biome.Lava, biome);
                        AssertWalkedRound(map, old.Map, x, y, w, $"{type} at {x},{y}");
                    }
                    else
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= w; dx++)
                                if (dy != 0 || dx < 0 || dx >= w)
                                    // (A camp's trapdoor stood there in version 6 and stands elsewhere now.)
                                    Assert.IsTrue(old.Map[x + dx, y + dy] == TileType.Floor || map[x + dx, y + dy] == TileType.Floor && old.Map[x + dx, y + dy] == TileType.Trapdoor,
                                        $"{type} at {x},{y}: {old.Map[x + dx, y + dy]} at {dx},{dy} from it");
                    Assert.Greater(Math.Max(Math.Abs(x - made.SpawnX), Math.Abs(y - made.SpawnY)), 5, "none by the spawn");
                    foreach (var room in made.Rooms.Nodes)
                        Assert.IsFalse(Math.Abs(x - room.AnchorX) <= 2 && Math.Abs(y - room.AnchorY) <= 2, $"{type} at {x},{y} on the middle of a room");
                }
            }
            Assert.Greater(props, 60);
            Assert.Greater(shore, 3, "some stand at the lava's edge");
            Assert.GreaterOrEqual(kinds.Count, 10, "most of the two biomes' kinds stand somewhere");
            Assert.LessOrEqual((solidAfter - solidBefore) * 100.0 / (Size * Size), 2.0, "blocked tiles rise by no more than two points in a hundred");
            // The landmarks version 6 laid are where it laid them.
            CollectionAssert.AreEqual(old.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)),
                made.BiomeLandmarks.Where(l => !LavaVoidProps.Lays(l.Kind)).Select(l => (l.Kind, l.Left, l.Bottom)));
        }

        // Walked from the spawn; dry: lava is not walked (it burns).
        static int[] Walk(GeneratedWorld world, bool dry)
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
                    if (ny >= map.Height || !map.InBounds(nx, ny) || steps[ny * map.Width + nx] >= 0 || map.IsSolid(nx, ny) || dry && map[nx, ny] == TileType.Lava) continue;
                    steps[ny * map.Width + nx] = steps[at] + 1;
                    queue.Enqueue(ny * map.Width + nx);
                }
            }
            return steps;
        }

        [Test]
        public void NothingWalkedToInVersionSixIsShutOff()
        {
            foreach (ulong seed in Seeds)
            {
                var old = Generated(seed, WorldGenerator.CrystalLakeVersion);
                var made = Generated(seed, WorldGenerator.LavaVoidVersion);
                foreach (bool dry in new[] { true, false })
                {
                    var before = Walk(old, dry);
                    var after = Walk(made, dry);
                    for (int i = 0; i < before.Length; i++)
                        if (before[i] >= 0 && !made.Map.IsSolid(i % Size, i / Size)) Assert.GreaterOrEqual(after[i], 0, $"seed {seed}: {i % Size},{i / Size} is shut off (dry {dry})");
                    foreach (var room in made.Rooms.Nodes)
                        if (before[room.AnchorY * Size + room.AnchorX] >= 0) Assert.GreaterOrEqual(after[room.AnchorY * Size + room.AnchorX], 0, $"seed {seed}: the room at {room.X},{room.Y}");
                    foreach (var mark in made.BiomeLandmarks)
                        if (LavaVoidProps.Lays(mark.Kind) && before[mark.StandY * Size + mark.StandX] >= 0)
                            Assert.GreaterOrEqual(after[mark.StandY * Size + mark.StandX], 0, $"seed {seed}: the {mark.Kind} at {mark.Left},{mark.Bottom} is walked to");
                }
            }
        }

        [Test]
        public void TheLandmarksAreRareLaidAsDrawnAndOffWhatIsKeptClear()
        {
            var counts = new Dictionary<BiomeLandmarkKind, int>();
            foreach (ulong seed in Seeds)
            {
                var old = Generated(seed, WorldGenerator.CrystalLakeVersion).Map;
                var made = Generated(seed, WorldGenerator.LavaVoidVersion);
                var map = made.Map;
                var marks = made.BiomeLandmarks.Where(l => LavaVoidProps.Lays(l.Kind)).ToList();
                int rooms = made.Rooms.Nodes.Count(r => r.Biome == Biome.Lava || r.Biome == Biome.Abyss);
                Assert.LessOrEqual(marks.Count, Math.Max(3, rooms / 5), $"seed {seed}: landmarks are rare ({marks.Count} in {rooms} rooms)");
                foreach (var mark in marks)
                {
                    counts[mark.Kind] = counts.GetValueOrDefault(mark.Kind) + 1;
                    var (w, h, standX, standY) = LavaVoidProps.Box(mark.Kind);
                    Assert.AreEqual((w, h), (mark.Width, mark.Height));
                    Assert.AreEqual((mark.Left + standX, mark.Bottom + standY), (mark.StandX, mark.StandY));
                    Assert.IsFalse(map.IsSolid(mark.StandX, mark.StandY), $"seed {seed}: the tile before the {mark.Kind} is open");
                    var biome = mark.Kind == BiomeLandmarkKind.FallenChimney || mark.Kind == BiomeLandmarkKind.GiantAnvil ? Biome.Lava : Biome.Abyss;
                    Assert.AreEqual(biome, made.Biomes[mark.Left, mark.Bottom], $"seed {seed}: the {mark.Kind}'s biome");
                    foreach (var (dx, dy, type) in LavaVoidProps.Layout(mark.Kind))
                    {
                        var (fw, fh) = Structures.Footprint(type);
                        for (int j = 0; j < fh; j++)
                            for (int i = 0; i < fw; i++)
                            {
                                int x = mark.Left + dx + i, y = mark.Bottom + dy + j;
                                Assert.AreEqual(i == 0 && j == 0 ? type : TileType.StructurePart, map[x, y], $"seed {seed}: the {mark.Kind}'s {type} at {x},{y}");
                            }
                    }
                    // Its whole box and a tile round it was open floor in version 6: a way round it on every side.
                    for (int y = mark.Bottom - 1; y <= mark.Bottom + h; y++)
                        for (int x = mark.Left - 1; x <= mark.Left + w; x++)
                            Assert.AreEqual(TileType.Floor, old[x, y], $"seed {seed}: the {mark.Kind}'s box at {x},{y}");
                    // Off the spawn, the places and the other landmarks.
                    Assert.Greater(Math.Max(Math.Abs(mark.Left + w / 2 - made.SpawnX), Math.Abs(mark.Bottom + h / 2 - made.SpawnY)), 6);
                    foreach (var site in made.Places)
                        Assert.IsFalse(mark.Left + mark.Width > site.X - site.Radius && mark.Left < site.X + site.Radius && mark.Bottom + mark.Height > site.Y - site.Radius
                            && mark.Bottom < site.Y + site.Radius, $"seed {seed}: the {mark.Kind} in {site.Definition.Id}");
                    foreach (var other in made.BiomeLandmarks)
                        if (other.Left != mark.Left || other.Bottom != mark.Bottom)
                            Assert.IsFalse(other.Left < mark.Left + mark.Width && mark.Left < other.Left + other.Width && other.Bottom < mark.Bottom + mark.Height
                                && mark.Bottom < other.Bottom + other.Height, $"seed {seed}: the {mark.Kind} and the {other.Kind} overlap");
                }
            }
            // A map this small has few rooms of each biome: more seeds until every kind has been laid somewhere.
            var all = new[] { BiomeLandmarkKind.FallenChimney, BiomeLandmarkKind.GiantAnvil, BiomeLandmarkKind.BrokenGate, BiomeLandmarkKind.StarAltar };
            for (ulong seed = 5; seed <= 20 && all.Any(k => counts.GetValueOrDefault(k) == 0); seed++)
                foreach (var mark in Generated(seed, WorldGenerator.LavaVoidVersion).BiomeLandmarks) counts[mark.Kind] = counts.GetValueOrDefault(mark.Kind) + 1;
            foreach (var kind in all) Assert.Greater(counts.GetValueOrDefault(kind), 0, $"{kind} in none of the seeds");
        }

        [Test]
        public void TheTwoBiomesPropsAndDecorAreAboutAsDenseAsMeant()
        {
            int measured = 0, bubbles = 0, rafts = 0, rocks = 0;
            foreach (ulong seed in new ulong[] { 1, 2 })
            {
                var made = Generated(seed, WorldGenerator.LavaVoidVersion);
                var map = made.Map;
                var floor = new Dictionary<Biome, int>();
                var props = new Dictionary<Biome, int>();
                var decor = new Dictionary<Biome, int>();
                var decorKinds = new Dictionary<Biome, HashSet<DecorKind>>();
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        var biome = made.Biomes[x, y];
                        var type = map[x, y];
                        if (type == TileType.LavaRock) rocks++;
                        else if (LavaVoidProps.Is(type)) props[biome] = props.GetValueOrDefault(biome) + 1;
                        if (type == TileType.Lava)
                        {
                            var afloat = Decor.On(map, seed, biome, x, y);
                            Assert.IsTrue(!afloat.Any || biome == Biome.Lava && afloat.Kind == DecorKind.CrustRaft, $"{afloat.Kind} on the lava of the {biome}");
                            if (afloat.Any)
                            {
                                rafts++;
                                Assert.IsTrue(map[x + 1, y] == TileType.Lava && map[x - 1, y] == TileType.Lava && map[x, y + 1] == TileType.Lava && map[x, y - 1] == TileType.Lava, $"a raft at {x},{y} lies off the edge");
                            }
                            continue;
                        }
                        if (type != TileType.Floor) continue;
                        floor[biome] = floor.GetValueOrDefault(biome) + 1;
                        var pick = Decor.On(map, seed, biome, x, y);
                        if (!pick.Any) continue;
                        decor[biome] = decor.GetValueOrDefault(biome) + 1;
                        if (!decorKinds.ContainsKey(biome)) decorKinds[biome] = new HashSet<DecorKind>();
                        decorKinds[biome].Add(pick.Kind);
                        if (pick.Kind == DecorKind.LavaBubble)
                        {
                            bubbles++;
                            Assert.IsTrue(map[x + 1, y] == TileType.Lava || map[x - 1, y] == TileType.Lava || map[x, y + 1] == TileType.Lava || map[x, y - 1] == TileType.Lava,
                                $"a blister at {x},{y} lies by the lava");
                        }
                    }
                }
                foreach (var biome in new[] { Biome.Lava, Biome.Abyss })
                {
                    if (floor.GetValueOrDefault(biome) < 2000) continue;
                    measured++;
                    float propsPerHundred = props.GetValueOrDefault(biome) * 100f / floor[biome], decorPerHundred = decor.GetValueOrDefault(biome) * 100f / floor[biome];
                    Assert.That(propsPerHundred, Is.InRange(1.2f, 6f), $"seed {seed}, {biome}: props in a hundred tiles of floor");
                    Assert.That(decorPerHundred, Is.InRange(4f, 16f), $"seed {seed}, {biome}: decor in a hundred tiles of floor");
                }
                foreach (var pair in props)
                    Assert.IsTrue(pair.Key == Biome.Lava || pair.Key == Biome.Abyss, $"{pair.Key} has the two biomes' props");
                if (decorKinds.TryGetValue(Biome.Lava, out var lava)) CollectionAssert.IsSubsetOf(lava, new[] { DecorKind.Embers, DecorKind.Ash, DecorKind.Pebbles, DecorKind.LavaBubble });
                if (decorKinds.TryGetValue(Biome.Abyss, out var abyss))
                    CollectionAssert.IsSubsetOf(abyss, new[] { DecorKind.Shards, DecorKind.StarDust, DecorKind.FloatStones, DecorKind.Pebbles });
                // The places and the arenas are kept clear.
                foreach (var site in made.Places)
                    for (int dy = -site.Radius; dy <= site.Radius; dy++)
                        for (int dx = -site.Radius; dx <= site.Radius; dx++)
                            Assert.IsFalse(Mine(map[site.X + dx, site.Y + dy]), $"a prop in {site.Definition.Id}");
                foreach (var arena in made.Arenas)
                    for (int dy = -made.ArenaRadius; dy <= made.ArenaRadius; dy++)
                        for (int dx = -made.ArenaRadius; dx <= made.ArenaRadius; dx++)
                            Assert.IsFalse(Mine(map[TileMap.Cell(arena.X) + dx, TileMap.Cell(arena.Y) + dy]), "a prop in an arena");
            }
            Assert.Greater(measured, 0, "the seeds have one of the two biomes");
            Assert.IsTrue(Decor.Flat(DecorKind.Ash) && Decor.Flat(DecorKind.CrustRaft) && Decor.Flat(DecorKind.StarDust) && !Decor.Flat(DecorKind.LavaBubble) && !Decor.Flat(DecorKind.FloatStones));
            Assert.Greater(bubbles, 20, "blisters along the lava's edge");
            Assert.Greater(rafts, 5, "rafts of crust on its pools");
            Assert.Greater(rocks, 5, "rocks standing in its pools");
        }

        // The lava's edge on a small map: blisters only on floor beside lava, rafts only on lava with lava on every
        // side, nothing by a road; no other biome's lava has any.
        [Test]
        public void TheLavasEdgeHasBlistersAndRaftsOnlyInTheLavaZone()
        {
            var map = new TileMap(40, 40, TileType.Floor);
            for (int y = 10; y < 30; y++)
                for (int x = 10; x < 30; x++)
                    map[x, y] = TileType.Lava;
            for (int y = 18; y < 22; y++) map[9, y] = TileType.Road;
            int blisters = 0, rafts = 0;
            for (int y = 2; y < 38; y++)
            {
                for (int x = 2; x < 38; x++)
                {
                    Assert.IsFalse(map[x, y] == TileType.Lava && Decor.On(map, 9, Biome.Cave, x, y).Any, "another biome's lava is bare");
                    var pick = Decor.On(map, 9, Biome.Lava, x, y);
                    if (!pick.Any) continue;
                    bool pool = map[x, y] == TileType.Lava;
                    if (pick.Kind == DecorKind.LavaBubble)
                    {
                        Assert.IsTrue(!pool && new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => map[x + d.Item1, y + d.Item2] == TileType.Lava), $"a blister at {x},{y}");
                        blisters++;
                    }
                    else if (pick.Kind == DecorKind.CrustRaft)
                    {
                        Assert.IsTrue(pool && x > 10 && x < 29 && y > 10 && y < 29, $"a raft at {x},{y}");
                        rafts++;
                    }
                    else Assert.IsFalse(pool, $"{pick.Kind} on the lava");
                    Assert.Greater(Math.Max(Math.Abs(x - 9), Math.Max(18 - y, y - 21)), 2, $"{pick.Kind} at {x},{y} by the road");
                    Assert.AreEqual(pick.Kind, Decor.On(map, 9, Biome.Lava, x, y).Kind, "the same again");
                }
            }
            Assert.Greater(blisters, 8);
            Assert.Greater(rafts, 15);
        }

        [Test]
        public void TheHostAndTheClientMakeTheSameVersionSevenMap()
        {
            var host = new WorldHost(3, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 512, MapVersion = WorldGenerator.LavaVoidVersion },
                bosses: Bosses.Staged, classes: Classes.Shipped, tiles: Tiles.Shipped, story: Tales.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Staged, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            int mine = 0;
            for (int y = 0; y < 512; y++)
                for (int x = 0; x < 512; x++)
                    if (Mine(host.World.Map[x, y])) mine++;
            Assert.Greater(mine, 50, "the two biomes' things stand in the host's world");
            Assert.AreEqual(WorldGenerator.LavaVoidVersion, client.Generator);
            Assert.AreEqual(WorldGenerator.LavaVoidVersion, WorldSave.Read(host.SaveWorld(now: 5), Tiles.Shipped).Generator);
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(client.Map));
        }

        [Test]
        public void ThePropsBlockAreInTheSheetAndTheLandmarksAreNeverDug()
        {
            Assert.AreEqual(6, LavaVoidProps.Any(Biome.Lava).Count);
            Assert.AreEqual(6, LavaVoidProps.Of(Biome.Abyss).Count);
            Assert.AreSame(CrystalLakeProps.Any(Biome.Crystal), LavaVoidProps.Any(Biome.Crystal));
            Assert.AreSame(IceSandProps.Of(Biome.Ice), LavaVoidProps.Any(Biome.Ice));
            foreach (var biome in new[] { Biome.Lava, Biome.Abyss })
            {
                Assert.AreEqual(0, BiomeProps.Of(biome).Count + IceSandProps.Of(biome).Count + CrystalLakeProps.Of(biome).Count, "versions 3 to 6 lay none of them");
                foreach (var kind in LavaVoidProps.Any(biome))
                {
                    Assert.IsTrue(BiomeProps.Is(kind.Type) && LavaVoidProps.Is(kind.Type) && !CrystalLakeProps.Is(kind.Type) && !IceSandProps.Is(kind.Type), $"{kind.Type}");
                    Assert.IsNotNull(Tiles.Shipped.Find(kind.Type), $"{kind.Type} has a row in tiles.csv");
                    Assert.AreEqual(kind.Big, Structures.Footprint(kind.Type).W > 1, $"{kind.Type}: the large ones are two tiles wide");
                    var map = new TileMap(8, 8, TileType.Floor);
                    map[3, 3] = kind.Type;
                    Assert.IsTrue(map.IsSolid(3, 3), $"{kind.Type} blocks");
                }
                // By the lava the same kinds, weighted toward cooled boulders and what burns.
                foreach (var kind in LavaVoidProps.Of(biome, shore: true)) Assert.IsTrue(LavaVoidProps.Any(biome).Any(k => k.Type == kind.Type));
            }
            foreach (var type in new[] { TileType.FallenChimney, TileType.GiantAnvil, TileType.BrokenGate, TileType.StarAltar })
            {
                Assert.IsTrue(BiomeLandmarks.Is(type) && LavaVoidProps.IsLandmark(type) && !BiomeProps.Is(type), $"{type}");
                Assert.IsNull(Tiles.Shipped.Find(type), $"{type} is never dug");
                var map = new TileMap(8, 8, TileType.Floor);
                map[3, 3] = type;
                Assert.IsTrue(map.IsSolid(3, 3), $"{type} blocks");
            }
            // A rock in the lava is lava to the eye.
            var pool = new TileMap(8, 8, TileType.Lava);
            pool[3, 3] = TileType.LavaRock;
            pool[5, 5] = TileType.CooledBoulder;
            Assert.IsTrue(LavaVoidProps.Molten(pool, 3, 3) && LavaVoidProps.Molten(pool, 2, 2));
            Assert.IsFalse(LavaVoidProps.Molten(pool, 5, 5) || LavaVoidProps.Molten(pool, -1, 0));
            // What burns and what holds the void's light give light (the screen reads tiles.csv light).
            Assert.Greater(TileLight.RadiusOf(TileType.FireBush, Tiles.Shipped), 0);
            Assert.Greater(TileLight.RadiusOf(TileType.CooledBoulder, Tiles.Shipped), 0);
            Assert.Greater(TileLight.RadiusOf(TileType.VoidCrystal, Tiles.Shipped), 0);
            Assert.AreEqual(0, TileLight.RadiusOf(TileType.SlagHeap, Tiles.Shipped));
            Assert.IsFalse(Lights.Gives(TileType.FireBush));
        }

        static World Room(out Player player, TileType thing)
        {
            var map = Maps.FromRows(
                "########",
                "#......#",
                "#......#",
                "#......#",
                "########");
            map[3, 2] = thing;
            if (BiomeProps.Wide(thing)) map[4, 2] = TileType.StructurePart;
            var world = new World(map, TileMap.Center(2, 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            return world;
        }

        static void Swing(World world, Player player, int swings)
        {
            world.SetAttacking(player.Id, true);
            int swung = 0;
            for (int tick = 0; tick < 12 * World.TicksPerSecond && swung < swings; tick++)
            {
                world.Step();
                swung += world.TakeEvents().Count(e => e.Type == SimEventType.Attack && e.Source == player.Id);
            }
            world.SetAttacking(player.Id, false);
        }

        // Struck with a weapon like the other biomes' props: a burning bush gives its pepper, a chain post its coins.
        [Test]
        public void ABurningBushIsPickedForItsPepperAndAChainPostBrokenForCoins()
        {
            var world = Room(out var player, TileType.FireBush);
            Swing(world, player, 4);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(Gear.Data.FindMaterial("fire_pepper")));

            world = Room(out player, TileType.ChainPost);
            Swing(world, player, 8);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(3, player.Inventory.CountOf(Gear.Data.FindMaterial("coin")));
        }

        // Obsidian and void stone are behind their pickaxes here as in their veins: a sword gets none of either.
        [Test]
        public void AnObsidianSpikeAndAVoidCrystalWantTheirVeinsPickaxes()
        {
            Assert.AreEqual(Tiles.Shipped.Find(TileType.ObsidianOre).Pickaxe, Tiles.Shipped.Find(TileType.ObsidianSpike).Pickaxe);
            Assert.AreEqual(Tiles.Shipped.Find(TileType.VoidOre).Pickaxe, Tiles.Shipped.Find(TileType.VoidCrystal).Pickaxe);
            foreach (var (type, ore) in new[] { (TileType.ObsidianSpike, "obsidian_ore"), (TileType.VoidCrystal, "void_ore") })
            {
                var world = Room(out var player, type);
                Swing(world, player, 12);
                Assert.AreEqual(type, world.Map[3, 2], $"{type} stands against a sword");
                Assert.AreEqual(0, player.Inventory.CountOf(Gear.Data.FindMaterial(ore)));
            }
        }

        // A wide one struck on its right half goes with both its tiles.
        [Test]
        public void ACooledBoulderGoesWithBothItsTiles()
        {
            var world = Room(out var player, TileType.CooledBoulder);
            Assert.IsTrue(world.Map.IsSolid(3, 2) && world.Map.IsSolid(4, 2));
            world.MovePlayer(player.Id, TileMap.Center(5, 2), new Vec2(-1, 0), 1);
            player.Position = TileMap.Center(5, 2);
            Swing(world, player, 16);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(TileType.Floor, world.Map[4, 2]);
            Assert.AreEqual(2, player.Inventory.CountOf(Gear.Data.FindMaterial("stone")));
        }

        // A landmark's tiles take no blow.
        [Test]
        public void ALandmarkIsNotDug()
        {
            foreach (var type in new[] { TileType.FallenChimney, TileType.GiantAnvil, TileType.BrokenGate, TileType.StarAltar })
            {
                var world = Room(out var player, type);
                Swing(world, player, 6);
                Assert.AreEqual(type, world.Map[3, 2]);
            }
        }
    }
}
