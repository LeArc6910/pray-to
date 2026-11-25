using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The crystal cave's and the underground lake's looks (Docs/BiomeRework.md 묶음 B3a): the generator's version 6 lays
    // their landmarks and props over version 5's map, on open floor (and, for an islet and a sunken boat, on a pool's
    // water) and shutting nothing off; versions 2 to 5 are as they were made; the same seed lays the same things on
    // the host and the client; the props are dug for what the sheet says; the lake's water's edge has its own cover.
    public class CrystalLakeLookTests
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

        static bool Mine(TileType type) => CrystalLakeProps.Is(type) || CrystalLakeProps.IsLandmark(type);

        // A world made at version 5 (묶음 B2, live since 2026-11-24) is made the same for ever: the hashes of its maps as
        // the code of that day made them (commit bb20dac), without the story's places and with them.
        [TestCase(1UL, 11017621877423367330UL, 6707494563888070325UL)]
        [TestCase(2UL, 12685065680120567711UL, 16317293172805451591UL)]
        [TestCase(3UL, 1943584571571506601UL, 16351204281850004877UL)]
        public void VersionFiveIsAsItWasMade(ulong seed, ulong plain, ulong storied)
        {
            Assert.AreEqual(plain, RoomMapTests.Hash(WorldGenerator.Generate(seed, Size, Size, version: WorldGenerator.IceSandVersion, terrain: Tiles.Shipped.Terrain).Map));
            Assert.AreEqual(storied, RoomMapTests.Hash(Generated(seed, WorldGenerator.IceSandVersion).Map));
        }

        [Test]
        public void ANewWorldIsVersionSixAndTheOlderVersionsHaveNoneOfItsThings()
        {
            Assert.AreEqual(6, WorldGenerator.CrystalLakeVersion);
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.CrystalLakeVersion);
            Assert.Greater(WorldGenerator.CrystalLakeVersion, WorldGenerator.IceSandVersion);
            foreach (int version in new[] { WorldGenerator.RoomVersion, WorldGenerator.PropVersion, WorldGenerator.LandmarkVersion, WorldGenerator.IceSandVersion })
            {
                var old = Generated(1, version);
                Assert.AreEqual(version, old.Version);
                Assert.IsFalse(old.BiomeLandmarks.Any(l => CrystalLakeProps.Lays(l.Kind)), $"version {version} has none of the two biomes' landmarks");
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        Assert.IsFalse(Mine(old.Map[x, y]), $"{old.Map[x, y]} at {x},{y} of a version {version} world");
            }
            var made = Generated(1, WorldGenerator.CrystalLakeVersion);
            Assert.AreEqual(WorldGenerator.CrystalLakeVersion, made.Version);
            var again = WorldGenerator.Generate(1, Size, Size, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.CrystalLakeVersion, Tiles.Shipped.Terrain);
            Assert.AreEqual(RoomMapTests.Hash(made.Map), RoomMapTests.Hash(again.Map), "the same seed, the same map");
            CollectionAssert.AreEqual(made.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)), again.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)));
        }

        // The tiles round a footprint are bare floor or water, the floor among them one run and more than half.
        static void AssertWalkedRound(TileMap map, int x, int y, int w, int h, string what)
        {
            var ring = new List<TileType>();
            for (int i = -1; i <= w; i++) ring.Add(map[x + i, y - 1]);
            for (int j = 0; j < h; j++) ring.Add(map[x + w, y + j]);
            for (int i = w; i >= -1; i--) ring.Add(map[x + i, y + h]);
            for (int j = h - 1; j >= 0; j--) ring.Add(map[x - 1, y + j]);
            int turns = 0, floor = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                // The lake's shallows (B3b, laid after the props) are walked like floor.
                static bool Walked(TileType t) => t == TileType.Floor || t == TileType.ShallowWater;
                Assert.IsTrue(Walked(ring[i]) || ring[i] == TileType.Water, $"{what}: {ring[i]} beside it");
                if (Walked(ring[i])) floor++;
                if (Walked(ring[i]) != Walked(ring[(i + 1) % ring.Count])) turns++;
            }
            Assert.LessOrEqual(turns, 2, $"{what}: the floor round it is one run");
            Assert.Greater(floor * 2, ring.Count, $"{what}: more floor than water round it");
        }

        // Version 6 is version 5's map with the two biomes' things on what was its open floor, an islet or a sunken
        // boat on what was a pool's water (a camp's trapdoor, laid after them by its own stream, may stand elsewhere in
        // its camp).
        [TestCase(1UL)]
        [TestCase(3UL)]
        public void VersionSixOnlyTurnsOpenFloorAndPoolsOfTheTwoBiomesIntoTheirThings(ulong seed)
        {
            var old = Generated(seed, WorldGenerator.IceSandVersion);
            var made = Generated(seed, WorldGenerator.CrystalLakeVersion);
            var map = made.Map;
            int props = 0, solidBefore = 0, solidAfter = 0;
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
                    // The lake's shallows (B3b) round its pools, on what was floor or water.
                    if (type == TileType.ShallowWater)
                    {
                        Assert.IsTrue(was == TileType.Floor || was == TileType.Water, $"{x},{y}: shallows on {was}");
                        continue;
                    }
                    bool afloat = CrystalLakeProps.Wet(map, x, y);
                    Assert.AreEqual(afloat ? TileType.Water : TileType.Floor, was, $"{x},{y}: only open floor changes, or a pool under what stands in it ({was} became {type})");
                    if (type == TileType.StructurePart)
                    {
                        Assert.IsTrue(BiomeProps.Wide(map[x - 1, y]) && CrystalLakeProps.Is(map[x - 1, y]) || made.BiomeLandmarks.Any(l => CrystalLakeProps.Lays(l.Kind) && l.Holds(x, y)),
                            $"{x},{y}: part of nothing");
                        continue;
                    }
                    var biome = made.Biomes[x, y];
                    Assert.IsTrue(biome == Biome.Crystal || biome == Biome.Lake, $"{x},{y}: {type} in the {biome}");
                    Assert.IsTrue(Mine(type), $"{x},{y}: {type}");
                    if (made.BiomeLandmarks.Any(l => l.Holds(x, y))) continue;
                    Assert.IsTrue(CrystalLakeProps.Any(biome).Any(k => k.Type == type), $"{x},{y}: {type} in the {biome}");
                    props++;
                    kinds.Add(type);
                    int w = Structures.Footprint(type).W;
                    if (type == TileType.MossIsle)
                    {
                        // Water all round it.
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= w; dx++)
                                if (dy != 0 || dx < 0 || dx >= w) Assert.AreEqual(TileType.Water, map[x + dx, y + dy], $"the islet at {x},{y}: the tile at {dx},{dy} from it");
                        continue;
                    }
                    if (biome == Biome.Lake) AssertWalkedRound(map, x, y, w, 1, $"{type} at {x},{y}");
                    else
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= w; dx++)
                                if (dy != 0 || dx < 0 || dx >= w) Assert.AreEqual(TileType.Floor, map[x + dx, y + dy], $"{type} at {x},{y}: the tile at {dx},{dy} from it");
                    Assert.Greater(Math.Max(Math.Abs(x - made.SpawnX), Math.Abs(y - made.SpawnY)), 5, "none by the spawn");
                    foreach (var room in made.Rooms.Nodes)
                        Assert.IsFalse(Math.Abs(x - room.AnchorX) <= 2 && Math.Abs(y - room.AnchorY) <= 2, $"{type} at {x},{y} on the middle of a room");
                }
            }
            Assert.Greater(props, 60);
            Assert.GreaterOrEqual(kinds.Count, 10, "most of the two biomes' kinds stand somewhere");
            Assert.LessOrEqual((solidAfter - solidBefore) * 100.0 / (Size * Size), 2.0, "blocked tiles rise by no more than two points in a hundred");
            // The landmarks version 5 laid are where it laid them.
            CollectionAssert.AreEqual(old.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)),
                made.BiomeLandmarks.Where(l => !CrystalLakeProps.Lays(l.Kind)).Select(l => (l.Kind, l.Left, l.Bottom)));
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
        public void NothingWalkedToInVersionFiveIsShutOff()
        {
            foreach (ulong seed in Seeds)
            {
                var old = Generated(seed, WorldGenerator.IceSandVersion);
                var before = Walk(old);
                var made = Generated(seed, WorldGenerator.CrystalLakeVersion);
                var after = Walk(made);
                for (int i = 0; i < before.Length; i++)
                    if (before[i] >= 0 && !made.Map.IsSolid(i % Size, i / Size)) Assert.GreaterOrEqual(after[i], 0, $"seed {seed}: {i % Size},{i / Size} is shut off");
                foreach (var room in made.Rooms.Nodes)
                    if (before[room.AnchorY * Size + room.AnchorX] >= 0) Assert.GreaterOrEqual(after[room.AnchorY * Size + room.AnchorX], 0, $"seed {seed}: the room at {room.X},{room.Y}");
                foreach (var mark in made.BiomeLandmarks)
                    if (CrystalLakeProps.Lays(mark.Kind) && before[mark.StandY * Size + mark.StandX] >= 0)
                        Assert.GreaterOrEqual(after[mark.StandY * Size + mark.StandX], 0, $"seed {seed}: the {mark.Kind} at {mark.Left},{mark.Bottom} is walked to");
            }
        }

        [Test]
        public void TheLandmarksAreRareLaidAsDrawnAndOffWhatIsKeptClear()
        {
            var counts = new Dictionary<BiomeLandmarkKind, int>();
            foreach (ulong seed in Seeds)
            {
                var old = Generated(seed, WorldGenerator.IceSandVersion).Map;
                var made = Generated(seed, WorldGenerator.CrystalLakeVersion);
                var map = made.Map;
                var marks = made.BiomeLandmarks.Where(l => CrystalLakeProps.Lays(l.Kind)).ToList();
                int rooms = made.Rooms.Nodes.Count(r => r.Biome == Biome.Crystal || r.Biome == Biome.Lake);
                Assert.LessOrEqual(marks.Count, Math.Max(3, rooms / 5), $"seed {seed}: landmarks are rare ({marks.Count} in {rooms} rooms)");
                foreach (var mark in marks)
                {
                    counts[mark.Kind] = counts.GetValueOrDefault(mark.Kind) + 1;
                    var (w, h, _, _) = CrystalLakeProps.Box(mark.Kind);
                    Assert.AreEqual((w, h), (mark.Width, mark.Height));
                    Assert.IsFalse(map.IsSolid(mark.StandX, mark.StandY), $"seed {seed}: the tile before the {mark.Kind} is open");
                    Assert.LessOrEqual(Math.Max(Math.Abs(mark.StandX - mark.Left - w / 2), Math.Abs(mark.StandY - mark.Bottom - h / 2)), 4, $"seed {seed}: the {mark.Kind} is looked at from beside it");
                    var biome = mark.Kind == BiomeLandmarkKind.GiantGeode || mark.Kind == BiomeLandmarkKind.CrystalArch ? Biome.Crystal : Biome.Lake;
                    Assert.AreEqual(biome, made.Biomes[mark.Left, mark.Bottom], $"seed {seed}: the {mark.Kind}'s biome");
                    foreach (var (dx, dy, type) in CrystalLakeProps.Layout(mark.Kind))
                    {
                        var (fw, fh) = Structures.Footprint(type);
                        for (int j = 0; j < fh; j++)
                            for (int i = 0; i < fw; i++)
                            {
                                int x = mark.Left + dx + i, y = mark.Bottom + dy + j;
                                Assert.AreEqual(i == 0 && j == 0 ? type : TileType.StructurePart, map[x, y], $"seed {seed}: the {mark.Kind}'s {type} at {x},{y}");
                                Assert.AreEqual(mark.Kind == BiomeLandmarkKind.SunkenBoat ? TileType.Water : TileType.Floor, old[x, y],
                                    $"seed {seed}: the {mark.Kind} stands on what was open floor (the boat on a pool)");
                            }
                    }
                    if (mark.Kind == BiomeLandmarkKind.OldWheel)
                    {
                        // By the water, and walked round.
                        AssertWalkedRound(map, mark.Left + 1, mark.Bottom + 1, 2, 2, $"seed {seed}: the wheel at {mark.Left},{mark.Bottom}");
                        int water = 0;
                        for (int dy = 0; dy < 4; dy++)
                            for (int dx = 0; dx < 4; dx++)
                                if (map[mark.Left + dx, mark.Bottom + dy] == TileType.Water) water++;
                        Assert.GreaterOrEqual(water, 2, $"seed {seed}: the wheel stands by the water");
                    }
                    if (mark.Kind == BiomeLandmarkKind.CrystalArch)
                    {
                        // The way under the span is open from one side of it to the other.
                        for (int dx = 2; dx <= 4; dx++)
                            for (int dy = 0; dy <= 2; dy++)
                                Assert.IsFalse(map.IsSolid(mark.Left + dx, mark.Bottom + dy), $"seed {seed}: under the arch at {mark.Left + dx},{mark.Bottom + dy}");
                        Assert.IsTrue(map.IsSolid(mark.Left + 1, mark.Bottom + 1) && map.IsSolid(mark.Left + 5, mark.Bottom + 1), "its piers block");
                    }
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
            var all = new[] { BiomeLandmarkKind.GiantGeode, BiomeLandmarkKind.CrystalArch, BiomeLandmarkKind.SunkenBoat, BiomeLandmarkKind.OldWheel };
            for (ulong seed = 5; seed <= 20 && all.Any(k => counts.GetValueOrDefault(k) == 0); seed++)
                foreach (var mark in Generated(seed, WorldGenerator.CrystalLakeVersion).BiomeLandmarks) counts[mark.Kind] = counts.GetValueOrDefault(mark.Kind) + 1;
            foreach (var kind in all) Assert.Greater(counts.GetValueOrDefault(kind), 0, $"{kind} in none of the seeds");
        }

        [Test]
        public void TheTwoBiomesPropsAndDecorAreAboutAsDenseAsMeant()
        {
            int measured = 0, reeds = 0, pads = 0, islets = 0;
            foreach (ulong seed in new ulong[] { 1, 2 })
            {
                var made = Generated(seed, WorldGenerator.CrystalLakeVersion);
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
                        if (type == TileType.MossIsle) islets++;
                        else if (CrystalLakeProps.Is(type)) props[biome] = props.GetValueOrDefault(biome) + 1;
                        if (type == TileType.Water)
                        {
                            var afloat = Decor.On(map, seed, biome, x, y);
                            Assert.IsTrue(!afloat.Any || biome == Biome.Lake && (afloat.Kind == DecorKind.Reeds || afloat.Kind == DecorKind.LilyPad), $"{afloat.Kind} on the water of the {biome}");
                            if (afloat.Kind == DecorKind.LilyPad) pads++;
                            if (afloat.Kind == DecorKind.Reeds) reeds++;
                            continue;
                        }
                        if (type != TileType.Floor) continue;
                        floor[biome] = floor.GetValueOrDefault(biome) + 1;
                        var pick = Decor.On(map, seed, biome, x, y);
                        if (!pick.Any) continue;
                        decor[biome] = decor.GetValueOrDefault(biome) + 1;
                        if (!decorKinds.ContainsKey(biome)) decorKinds[biome] = new HashSet<DecorKind>();
                        decorKinds[biome].Add(pick.Kind);
                        if (pick.Kind == DecorKind.Reeds)
                        {
                            reeds++;
                            // By the water or by the shallows round it (B3b).
                            static bool Wet(TileType t) => t == TileType.Water || t == TileType.ShallowWater;
                            Assert.IsTrue(Wet(map[x + 1, y]) || Wet(map[x - 1, y]) || Wet(map[x, y + 1]) || Wet(map[x, y - 1]),
                                $"reeds at {x},{y} stand by the water");
                        }
                    }
                }
                foreach (var biome in new[] { Biome.Crystal, Biome.Lake })
                {
                    if (floor.GetValueOrDefault(biome) < 2000) continue;
                    measured++;
                    float propsPerHundred = props.GetValueOrDefault(biome) * 100f / floor[biome], decorPerHundred = decor.GetValueOrDefault(biome) * 100f / floor[biome];
                    Assert.That(propsPerHundred, Is.InRange(1.2f, 6f), $"seed {seed}, {biome}: props in a hundred tiles of floor");
                    Assert.That(decorPerHundred, Is.InRange(4f, 16f), $"seed {seed}, {biome}: decor in a hundred tiles of floor");
                }
                foreach (var pair in props)
                    Assert.IsTrue(pair.Key == Biome.Crystal || pair.Key == Biome.Lake, $"{pair.Key} has the two biomes' props");
                if (decorKinds.TryGetValue(Biome.Crystal, out var crystal)) CollectionAssert.IsSubsetOf(crystal, new[] { DecorKind.Shards, DecorKind.CrystalDust, DecorKind.Pebbles });
                if (decorKinds.TryGetValue(Biome.Lake, out var lake))
                    CollectionAssert.IsSubsetOf(lake, new[] { DecorKind.Tuft, DecorKind.Shells, DecorKind.Moss, DecorKind.Pebbles, DecorKind.Reeds });
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
            Assert.IsTrue(Decor.Sways(DecorKind.Reeds) && Decor.Flat(DecorKind.LilyPad) && Decor.Flat(DecorKind.CrystalDust) && !Decor.Flat(DecorKind.Shells));
            Assert.Greater(reeds, 20, "reeds along the lake's banks");
            Assert.Greater(pads, 10, "lily pads on its pools");
        }

        // The lake's water's edge on a small map: reeds only on floor beside water or in water beside floor, pads only
        // on water, nothing by a pier or a road; no other biome's water has any.
        [Test]
        public void TheWatersEdgeHasReedsAndPadsOnlyInTheLake()
        {
            var map = new TileMap(40, 40, TileType.Floor);
            for (int y = 10; y < 30; y++)
                for (int x = 10; x < 30; x++)
                    map[x, y] = TileType.Water;
            for (int y = 18; y < 22; y++) map[9, y] = TileType.Dock;
            int reedsOnBank = 0, reedsInWater = 0, pads = 0;
            for (int y = 2; y < 38; y++)
            {
                for (int x = 2; x < 38; x++)
                {
                    Assert.IsFalse(map[x, y] == TileType.Water && Decor.On(map, 9, Biome.Ice, x, y).Any, "another biome's pool is bare");
                    var pick = Decor.On(map, 9, Biome.Lake, x, y);
                    if (!pick.Any) continue;
                    bool pool = map[x, y] == TileType.Water;
                    bool beside = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => map[x + d.Item1, y + d.Item2] == (pool ? TileType.Floor : TileType.Water));
                    if (pick.Kind == DecorKind.Reeds)
                    {
                        Assert.IsTrue(beside, $"reeds at {x},{y}");
                        if (pool) reedsInWater++;
                        else reedsOnBank++;
                    }
                    else if (pick.Kind == DecorKind.LilyPad)
                    {
                        Assert.IsTrue(pool, $"a pad at {x},{y}");
                        pads++;
                    }
                    else Assert.IsFalse(pool, $"{pick.Kind} on the water");
                    Assert.Greater(Math.Max(Math.Abs(x - 9), Math.Max(18 - y, y - 21)), 2, $"{pick.Kind} at {x},{y} by the pier");
                    Assert.AreEqual(pick.Kind, Decor.On(map, 9, Biome.Lake, x, y).Kind, "the same again");
                }
            }
            Assert.Greater(reedsOnBank, 8);
            Assert.Greater(reedsInWater, 5);
            Assert.Greater(pads, 15);
        }

        [Test]
        public void TheHostAndTheClientMakeTheSameVersionSixMap()
        {
            var host = new WorldHost(3, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 512, MapVersion = WorldGenerator.CrystalLakeVersion },
                bosses: Bosses.Staged, classes: Classes.Shipped, tiles: Tiles.Shipped, story: Tales.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Staged, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            int mine = 0;
            for (int y = 0; y < 512; y++)
                for (int x = 0; x < 512; x++)
                    if (Mine(host.World.Map[x, y])) mine++;
            Assert.Greater(mine, 50, "the two biomes' things stand in the host's world");
            Assert.AreEqual(WorldGenerator.CrystalLakeVersion, client.Generator);
            Assert.AreEqual(WorldGenerator.CrystalLakeVersion, WorldSave.Read(host.SaveWorld(now: 5), Tiles.Shipped).Generator);
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(client.Map));
        }

        [Test]
        public void ThePropsBlockAreInTheSheetAndTheLandmarksAreNeverDug()
        {
            Assert.AreEqual(6, CrystalLakeProps.Of(Biome.Crystal).Count);
            Assert.AreEqual(6, CrystalLakeProps.Any(Biome.Lake).Count);
            Assert.AreSame(IceSandProps.Of(Biome.Ice), CrystalLakeProps.Any(Biome.Ice));
            foreach (var biome in new[] { Biome.Crystal, Biome.Lake })
            {
                Assert.AreEqual(0, BiomeProps.Of(biome).Count + IceSandProps.Of(biome).Count, "versions 3 to 5 lay none of them");
                foreach (var kind in CrystalLakeProps.Any(biome))
                {
                    Assert.IsTrue(BiomeProps.Is(kind.Type) && CrystalLakeProps.Is(kind.Type) && !IceSandProps.Is(kind.Type), $"{kind.Type}");
                    Assert.IsNotNull(Tiles.Shipped.Find(kind.Type), $"{kind.Type} has a row in tiles.csv");
                    Assert.AreEqual(kind.Big, Structures.Footprint(kind.Type).W > 1, $"{kind.Type}: the large ones are two tiles wide");
                    var map = new TileMap(8, 8, TileType.Floor);
                    map[3, 3] = kind.Type;
                    Assert.IsTrue(map.IsSolid(3, 3), $"{kind.Type} blocks");
                }
                // By the water the same kinds, weighted toward shells, coral and clams.
                foreach (var kind in CrystalLakeProps.Of(biome, shore: true)) Assert.IsTrue(CrystalLakeProps.Any(biome).Any(k => k.Type == kind.Type));
            }
            foreach (var type in new[] { TileType.GiantGeode, TileType.ArchPier, TileType.CrystalArch, TileType.SunkenBoat, TileType.OldWheel })
            {
                Assert.IsTrue(BiomeLandmarks.Is(type) && CrystalLakeProps.IsLandmark(type) && !BiomeProps.Is(type), $"{type}");
                Assert.IsNull(Tiles.Shipped.Find(type), $"{type} is never dug");
            }
            // The arch's span is walked under; its piers block.
            var room = new TileMap(8, 8, TileType.Floor);
            room[3, 3] = TileType.CrystalArch;
            room[1, 3] = TileType.ArchPier;
            Assert.IsFalse(room.IsSolid(3, 3));
            Assert.IsTrue(room.IsSolid(1, 3));
            // What stands in a pool is water to the eye, all its tiles.
            room[4, 5] = TileType.SunkenBoat;
            foreach (var (x, y) in new[] { (5, 5), (6, 5), (4, 6), (5, 6), (6, 6) }) room[x, y] = TileType.StructurePart;
            room[1, 6] = TileType.CrystalMass;
            room[2, 6] = TileType.StructurePart;
            Assert.IsTrue(CrystalLakeProps.Wet(room, 4, 5) && CrystalLakeProps.Wet(room, 6, 6) && CrystalLakeProps.Wet(room, 5, 5));
            Assert.IsFalse(CrystalLakeProps.Wet(room, 2, 6) || CrystalLakeProps.Wet(room, 3, 3) || CrystalLakeProps.Wet(room, 7, 5));
            // The crystals give light (the screen reads tiles.csv light).
            Assert.Greater(TileLight.RadiusOf(TileType.CrystalCluster, Tiles.Shipped), 0);
            Assert.Greater(TileLight.RadiusOf(TileType.CrystalMass, Tiles.Shipped), 0);
            Assert.Greater(TileLight.RadiusOf(TileType.MossIsle, Tiles.Shipped), 0);
            Assert.AreEqual(0, TileLight.RadiusOf(TileType.MossRock, Tiles.Shipped));
            Assert.IsFalse(Lights.Gives(TileType.CrystalCluster));
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

        // Struck with a weapon like the other biomes' props: a clam gives its pearl, a cracked geode its coins.
        [Test]
        public void AClamIsPickedForItsPearlAndAGeodeBrokenForCoins()
        {
            var world = Room(out var player, TileType.GiantClam);
            Swing(world, player, 6);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(Gear.Data.FindMaterial("pearl")));

            world = Room(out player, TileType.CrackedGeode);
            Swing(world, player, 4);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(4, player.Inventory.CountOf(Gear.Data.FindMaterial("coin")));

            world = Room(out player, TileType.CrystalCluster);
            Swing(world, player, 6);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(Gear.Data.FindMaterial("mithril_ore")));
        }

        // A wide one struck on its right half goes with both its tiles.
        [Test]
        public void ACrystalMassGoesWithBothItsTiles()
        {
            var world = Room(out var player, TileType.CrystalMass);
            Assert.IsTrue(world.Map.IsSolid(3, 2) && world.Map.IsSolid(4, 2));
            world.MovePlayer(player.Id, TileMap.Center(5, 2), new Vec2(-1, 0), 1);
            player.Position = TileMap.Center(5, 2);
            Swing(world, player, 16);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(TileType.Floor, world.Map[4, 2]);
            Assert.AreEqual(2, player.Inventory.CountOf(Gear.Data.FindMaterial("stone")));
        }

        // A landmark's tiles take no blow, and the arch's span is not built on or dug though it is walked.
        [Test]
        public void ALandmarkIsNotDug()
        {
            foreach (var type in new[] { TileType.ArchPier, TileType.GiantGeode, TileType.OldWheel })
            {
                var world = Room(out var player, type);
                Swing(world, player, 6);
                Assert.AreEqual(type, world.Map[3, 2]);
            }
        }
    }
}
