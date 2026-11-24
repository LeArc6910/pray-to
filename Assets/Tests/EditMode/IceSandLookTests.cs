using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The ice cave's and the sand cave's looks (Docs/BiomeRework.md 묶음 B2a): the generator's version 5 lays their
    // landmarks and props over version 4's map, on open floor only and shutting nothing off; versions 2, 3 and 4 are as
    // they were made; the same seed lays the same things on the host and the client; the props are dug for what the
    // sheet says; their floor's cover is the two biomes' own.
    public class IceSandLookTests
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

        // A world made at version 4 (묶음 B1c, live since 2026-11-24) is made the same for ever: the hashes of its maps as
        // the code of that day made them (commit 803d7a9), without the story's places and with them.
        [TestCase(1UL, 4398937307479922836UL, 12010841846165551722UL)]
        [TestCase(2UL, 3935993690776416143UL, 13781251848032422410UL)]
        [TestCase(3UL, 1361645667314800996UL, 17259105259442561422UL)]
        public void VersionFourIsAsItWasMade(ulong seed, ulong plain, ulong storied)
        {
            Assert.AreEqual(plain, RoomMapTests.Hash(WorldGenerator.Generate(seed, Size, Size, version: WorldGenerator.LandmarkVersion, terrain: Tiles.Shipped.Terrain).Map));
            Assert.AreEqual(storied, RoomMapTests.Hash(Generated(seed, WorldGenerator.LandmarkVersion).Map));
        }

        [Test]
        public void ANewWorldIsVersionFiveAndTheOlderVersionsHaveNoneOfItsThings()
        {
            Assert.AreEqual(5, WorldGenerator.IceSandVersion);
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.IceSandVersion);
            Assert.Greater(WorldGenerator.IceSandVersion, WorldGenerator.LandmarkVersion);
            foreach (int version in new[] { WorldGenerator.RoomVersion, WorldGenerator.PropVersion, WorldGenerator.LandmarkVersion })
            {
                var old = Generated(1, version);
                Assert.AreEqual(version, old.Version);
                Assert.IsFalse(old.BiomeLandmarks.Any(l => IceSandProps.Lays(l.Kind)), $"version {version} has none of the two biomes' landmarks");
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        Assert.IsFalse(IceSandProps.Is(old.Map[x, y]) || IceSandProps.IsLandmark(old.Map[x, y]), $"{old.Map[x, y]} at {x},{y} of a version {version} world");
            }
            var made = Generated(1, WorldGenerator.IceSandVersion);
            Assert.AreEqual(WorldGenerator.IceSandVersion, made.Version);
            var again = WorldGenerator.Generate(1, Size, Size, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.IceSandVersion, Tiles.Shipped.Terrain);
            Assert.AreEqual(RoomMapTests.Hash(made.Map), RoomMapTests.Hash(again.Map), "the same seed, the same map");
            CollectionAssert.AreEqual(made.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)), again.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)));
        }

        // Version 5 is version 4's map with the two biomes' things on what was its open floor (a camp's trapdoor, laid
        // after them by its own stream, may stand elsewhere in its camp).
        [TestCase(1UL)]
        [TestCase(3UL)]
        public void VersionFiveOnlyTurnsOpenFloorOfTheTwoBiomesIntoTheirThings(ulong seed)
        {
            var old = Generated(seed, WorldGenerator.LandmarkVersion);
            var made = Generated(seed, WorldGenerator.IceSandVersion);
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
                    Assert.AreEqual(TileType.Floor, was, $"{x},{y}: only open floor changes ({was} became {type})");
                    // The right half of a wide prop (which may reach a tile over its biome's border) or more of a landmark.
                    if (type == TileType.StructurePart)
                    {
                        Assert.IsTrue(BiomeProps.Wide(map[x - 1, y]) && IceSandProps.Is(map[x - 1, y]) || made.BiomeLandmarks.Any(l => IceSandProps.Lays(l.Kind) && l.Holds(x, y)),
                            $"{x},{y}: part of nothing");
                        continue;
                    }
                    var biome = made.Biomes[x, y];
                    Assert.IsTrue(biome == Biome.Ice || biome == Biome.Sand, $"{x},{y}: {type} in the {biome}");
                    Assert.IsTrue(IceSandProps.Is(type) || IceSandProps.IsLandmark(type), $"{x},{y}: {type}");
                    if (made.BiomeLandmarks.Any(l => l.Holds(x, y))) continue;
                    Assert.IsTrue(IceSandProps.Of(biome).Any(k => k.Type == type), $"{x},{y}: {type} in the {biome}");
                    props++;
                    kinds.Add(type);
                    // Open floor all round its footprint; a frozen fall alone has the rock above it.
                    int w = Structures.Footprint(type).W;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= w; dx++)
                        {
                            if (dy == 0 && dx >= 0 && dx < w) continue;
                            if (IceSandProps.Hangs(type) && dy == 1)
                            {
                                if (dx >= 0 && dx < w) Assert.AreEqual(TileType.Wall, map[x + dx, y + dy], $"{type} at {x},{y} hangs from rock");
                                continue;
                            }
                            Assert.AreEqual(TileType.Floor, map[x + dx, y + dy], $"{type} at {x},{y}: the tile at {dx},{dy} from it");
                        }
                    Assert.Greater(Math.Max(Math.Abs(x - made.SpawnX), Math.Abs(y - made.SpawnY)), 5, "none by the spawn");
                    foreach (var room in made.Rooms.Nodes)
                        Assert.IsFalse(Math.Abs(x - room.AnchorX) <= 2 && Math.Abs(y - room.AnchorY) <= 2, $"{type} at {x},{y} on the middle of a room");
                }
            }
            Assert.Greater(props, 60);
            Assert.GreaterOrEqual(kinds.Count, 10, "most of the two biomes' kinds stand somewhere");
            Assert.LessOrEqual((solidAfter - solidBefore) * 100.0 / (Size * Size), 2.0, "blocked tiles rise by no more than two points in a hundred");
            // The dirt cave's and the mushroom forest's landmarks are where version 4 laid them.
            CollectionAssert.AreEqual(old.BiomeLandmarks.Select(l => (l.Kind, l.Left, l.Bottom)),
                made.BiomeLandmarks.Where(l => !IceSandProps.Lays(l.Kind)).Select(l => (l.Kind, l.Left, l.Bottom)));
        }

        [Test]
        public void NothingWalkedToInVersionFourIsShutOff()
        {
            foreach (ulong seed in Seeds)
            {
                var old = Generated(seed, WorldGenerator.LandmarkVersion);
                var before = Walk(old);
                var made = Generated(seed, WorldGenerator.IceSandVersion);
                var after = Walk(made);
                for (int i = 0; i < before.Length; i++)
                    if (before[i] >= 0 && !made.Map.IsSolid(i % Size, i / Size)) Assert.GreaterOrEqual(after[i], 0, $"seed {seed}: {i % Size},{i / Size} is shut off");
                foreach (var room in made.Rooms.Nodes)
                    if (before[room.AnchorY * Size + room.AnchorX] >= 0) Assert.GreaterOrEqual(after[room.AnchorY * Size + room.AnchorX], 0, $"seed {seed}: the room at {room.X},{room.Y}");
                foreach (var mark in made.BiomeLandmarks)
                    if (IceSandProps.Lays(mark.Kind)) Assert.GreaterOrEqual(after[mark.StandY * Size + mark.StandX], 0, $"seed {seed}: the {mark.Kind} at {mark.Left},{mark.Bottom} is walked to");
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
        public void TheLandmarksAreRareLaidAsDrawnAndOffWhatIsKeptClear()
        {
            var counts = new Dictionary<BiomeLandmarkKind, int>();
            foreach (ulong seed in Seeds)
            {
                var old = Generated(seed, WorldGenerator.LandmarkVersion).Map;
                var made = Generated(seed, WorldGenerator.IceSandVersion);
                var map = made.Map;
                var marks = made.BiomeLandmarks.Where(l => IceSandProps.Lays(l.Kind)).ToList();
                int rooms = made.Rooms.Nodes.Count(r => r.Biome == Biome.Ice || r.Biome == Biome.Sand);
                Assert.LessOrEqual(marks.Count, Math.Max(3, rooms / 5), $"seed {seed}: landmarks are rare ({marks.Count} in {rooms} rooms)");
                foreach (var mark in marks)
                {
                    counts[mark.Kind] = counts.GetValueOrDefault(mark.Kind) + 1;
                    var (w, h, standX, standY) = IceSandProps.Box(mark.Kind);
                    Assert.AreEqual((w, h), (mark.Width, mark.Height));
                    Assert.AreEqual((mark.Left + standX, mark.Bottom + standY), (mark.StandX, mark.StandY));
                    Assert.IsFalse(map.IsSolid(mark.StandX, mark.StandY), $"seed {seed}: the tile before the {mark.Kind} is open");
                    var biome = mark.Kind == BiomeLandmarkKind.FrozenBeast || mark.Kind == BiomeLandmarkKind.IceRuin ? Biome.Ice : Biome.Sand;
                    Assert.AreEqual(biome, made.Biomes[mark.Left, mark.Bottom], $"seed {seed}: the {mark.Kind}'s biome");
                    foreach (var (dx, dy, type) in IceSandProps.Layout(mark.Kind))
                    {
                        // A temple's urns stand only in a nook of the rock.
                        if (mark.Kind == BiomeLandmarkKind.TempleFront && type == TileType.BrokenUrn && map[mark.Left + dx, mark.Bottom + dy] != type) continue;
                        var (fw, fh) = Structures.Footprint(type);
                        for (int j = 0; j < fh; j++)
                            for (int i = 0; i < fw; i++)
                            {
                                int x = mark.Left + dx + i, y = mark.Bottom + dy + j;
                                Assert.AreEqual(i == 0 && j == 0 ? type : TileType.StructurePart, map[x, y], $"seed {seed}: the {mark.Kind}'s {type} at {x},{y}");
                                Assert.AreEqual(TileType.Floor, old[x, y], $"seed {seed}: the {mark.Kind} stands on what was open floor");
                            }
                    }
                    if (mark.Kind == BiomeLandmarkKind.TempleFront)
                        for (int i = 1; i <= 5; i++) Assert.AreEqual(TileType.Wall, map[mark.Left + i, mark.Bottom + 4], $"seed {seed}: the temple's front is under rock");
                    // Off the spawn, the places, the arenas and the other landmarks.
                    Assert.Greater(Math.Max(Math.Abs(mark.Left + 3 - made.SpawnX), Math.Abs(mark.Bottom + 2 - made.SpawnY)), 6);
                    foreach (var site in made.Places)
                        Assert.IsFalse(mark.Left + mark.Width > site.X - site.Radius && mark.Left < site.X + site.Radius && mark.Bottom + mark.Height > site.Y - site.Radius
                            && mark.Bottom < site.Y + site.Radius, $"seed {seed}: the {mark.Kind} in {site.Definition.Id}");
                    foreach (var other in made.BiomeLandmarks)
                        if (other.Left != mark.Left || other.Bottom != mark.Bottom)
                            Assert.IsFalse(other.Left < mark.Left + mark.Width && mark.Left < other.Left + other.Width && other.Bottom < mark.Bottom + mark.Height
                                && mark.Bottom < other.Bottom + other.Height, $"seed {seed}: the {mark.Kind} and the {other.Kind} overlap");
                }
            }
            // A map this small has some thirty rooms of each biome: more seeds until every kind has been laid somewhere.
            var all = new[] { BiomeLandmarkKind.FrozenBeast, BiomeLandmarkKind.IceRuin, BiomeLandmarkKind.TempleFront, BiomeLandmarkKind.GiantSkull };
            for (ulong seed = 5; seed <= 16 && all.Any(k => counts.GetValueOrDefault(k) == 0); seed++)
                foreach (var mark in Generated(seed, WorldGenerator.IceSandVersion).BiomeLandmarks) counts[mark.Kind] = counts.GetValueOrDefault(mark.Kind) + 1;
            foreach (var kind in all) Assert.Greater(counts.GetValueOrDefault(kind), 0, $"{kind} in none of the seeds");
        }

        [Test]
        public void TheTwoBiomesPropsAndDecorAreAboutAsDenseAsMeant()
        {
            var made = Generated(2, WorldGenerator.IceSandVersion);
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
                    if (IceSandProps.Is(type)) props[biome] = props.GetValueOrDefault(biome) + 1;
                    if (type != TileType.Floor) continue;
                    floor[biome] = floor.GetValueOrDefault(biome) + 1;
                    var pick = Decor.On(map, 2, biome, x, y);
                    if (!pick.Any) continue;
                    decor[biome] = decor.GetValueOrDefault(biome) + 1;
                    if (!decorKinds.ContainsKey(biome)) decorKinds[biome] = new HashSet<DecorKind>();
                    decorKinds[biome].Add(pick.Kind);
                }
            }
            int measured = 0;
            foreach (var biome in new[] { Biome.Ice, Biome.Sand })
            {
                if (floor.GetValueOrDefault(biome) < 2000) continue;
                measured++;
                float propsPerHundred = props.GetValueOrDefault(biome) * 100f / floor[biome], decorPerHundred = decor.GetValueOrDefault(biome) * 100f / floor[biome];
                Assert.That(propsPerHundred, Is.InRange(1.5f, 6f), $"{biome}: props in a hundred tiles of floor");
                Assert.That(decorPerHundred, Is.InRange(4f, 13f), $"{biome}: decor in a hundred tiles of floor");
            }
            Assert.Greater(measured, 0, "the seed has one of the two biomes");
            foreach (var pair in props)
                Assert.IsTrue(pair.Key == Biome.Ice || pair.Key == Biome.Sand, $"{pair.Key} has the two biomes' props");
            if (decorKinds.TryGetValue(Biome.Ice, out var ice)) CollectionAssert.IsSubsetOf(ice, new[] { DecorKind.FrostGrass, DecorKind.Shards, DecorKind.Snow, DecorKind.Pebbles });
            if (decorKinds.TryGetValue(Biome.Sand, out var sand)) CollectionAssert.IsSubsetOf(sand, new[] { DecorKind.DryGrass, DecorKind.Bones, DecorKind.Pebbles });
            Assert.IsTrue(Decor.Sways(DecorKind.FrostGrass) && Decor.Sways(DecorKind.DryGrass) && Decor.Flat(DecorKind.Snow));
            // The places and the arenas are kept clear.
            foreach (var site in made.Places)
                for (int dy = -site.Radius; dy <= site.Radius; dy++)
                    for (int dx = -site.Radius; dx <= site.Radius; dx++)
                        Assert.IsFalse(IceSandProps.Is(map[site.X + dx, site.Y + dy]), $"a prop in {site.Definition.Id}");
            foreach (var arena in made.Arenas)
                for (int dy = -made.ArenaRadius; dy <= made.ArenaRadius; dy++)
                    for (int dx = -made.ArenaRadius; dx <= made.ArenaRadius; dx++)
                        Assert.IsFalse(IceSandProps.Is(map[TileMap.Cell(arena.X) + dx, TileMap.Cell(arena.Y) + dy]), "a prop in an arena");
        }

        [Test]
        public void TheHostAndTheClientMakeTheSameVersionFiveMap()
        {
            var host = new WorldHost(3, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 512, MapVersion = WorldGenerator.IceSandVersion },
                bosses: Bosses.Staged, classes: Classes.Shipped, tiles: Tiles.Shipped, story: Tales.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Staged, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            Assert.IsTrue(host.Generated.BiomeLandmarks.Any(l => IceSandProps.Lays(l.Kind)));
            Assert.AreEqual(WorldGenerator.IceSandVersion, client.Generator);
            Assert.AreEqual(WorldGenerator.IceSandVersion, WorldSave.Read(host.SaveWorld(now: 5), Tiles.Shipped).Generator);
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(client.Map));
        }

        [Test]
        public void ThePropsBlockAreInTheSheetAndTheLandmarksAreNeverDug()
        {
            foreach (var biome in new[] { Biome.Ice, Biome.Sand })
            {
                Assert.AreEqual(6, IceSandProps.Of(biome).Count);
                Assert.AreSame(IceSandProps.Of(biome), IceSandProps.Any(biome));
                Assert.AreEqual(0, BiomeProps.Of(biome).Count, "version 3 and 4 lay none of them");
                foreach (var kind in IceSandProps.Of(biome))
                {
                    Assert.IsTrue(BiomeProps.Is(kind.Type) && IceSandProps.Is(kind.Type), $"{kind.Type}");
                    Assert.IsNotNull(Tiles.Shipped.Find(kind.Type), $"{kind.Type} has a row in tiles.csv");
                    Assert.AreEqual(kind.Big, Structures.Footprint(kind.Type).W > 1, $"{kind.Type}: the large ones are two tiles wide");
                    var map = new TileMap(8, 8, TileType.Floor);
                    map[3, 3] = kind.Type;
                    Assert.IsTrue(map.IsSolid(3, 3), $"{kind.Type} blocks");
                }
            }
            Assert.AreSame(BiomeProps.Of(Biome.Cave), IceSandProps.Any(Biome.Cave));
            foreach (var type in new[] { TileType.FrozenBeast, TileType.IceColumn, TileType.TempleFront, TileType.GiantSkull })
            {
                Assert.IsTrue(BiomeLandmarks.Is(type) && IceSandProps.IsLandmark(type) && !BiomeProps.Is(type), $"{type}");
                Assert.IsNull(Tiles.Shipped.Find(type), $"{type} is never dug");
            }
            // The glowing ones give light (the screen reads tiles.csv light; a lit pillar is no lamp to a rat).
            Assert.Greater(TileLight.RadiusOf(TileType.IceCrystals, Tiles.Shipped), 0);
            Assert.Greater(TileLight.RadiusOf(TileType.IcePillar, Tiles.Shipped), 0);
            Assert.AreEqual(0, TileLight.RadiusOf(TileType.CaveCactus, Tiles.Shipped));
            Assert.IsFalse(Lights.Gives(TileType.IceCrystals));
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

        // Struck with a weapon like the first two biomes' props: an urn gives its coins, crystals their ice.
        [Test]
        public void AnUrnIsBrokenForCoinsAndCrystalsArePicked()
        {
            var world = Room(out var player, TileType.BrokenUrn);
            Swing(world, player, 4);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(3, player.Inventory.CountOf(Gear.Data.FindMaterial("coin")));

            world = Room(out player, TileType.IceCrystals);
            Swing(world, player, 6);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.GreaterOrEqual(player.Inventory.CountOf(Gear.Data.FindMaterial("ice_crystal")), 1);
        }

        // A wide one struck on its right half goes with both its tiles.
        [Test]
        public void AnIcePillarGoesWithBothItsTiles()
        {
            var world = Room(out var player, TileType.IcePillar);
            Assert.IsTrue(world.Map.IsSolid(3, 2) && world.Map.IsSolid(4, 2));
            world.MovePlayer(player.Id, TileMap.Center(5, 2), new Vec2(-1, 0), 1);
            player.Position = TileMap.Center(5, 2);
            Swing(world, player, 16);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(TileType.Floor, world.Map[4, 2]);
            Assert.AreEqual(2, player.Inventory.CountOf(Gear.Data.FindMaterial("ice_crystal")));
        }

        // A landmark's tiles take no blow.
        [Test]
        public void ALandmarkIsNotDug()
        {
            var world = Room(out var player, TileType.IceColumn);
            Swing(world, player, 6);
            Assert.AreEqual(TileType.IceColumn, world.Map[3, 2]);
        }
    }
}
