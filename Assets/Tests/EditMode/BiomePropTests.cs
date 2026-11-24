using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The biomes' props (Docs/BiomeRework.md 묶음 B1a): the generator's version 3 is version 2 with props stood on its
    // open floor last of all, each with a way round it; version 2 stays as it was. A swing lights a lamp that is out,
    // bursts a spore sac into a cloud, and takes a wide prop with both its tiles. TileLight says what gives light.
    public class BiomePropTests
    {
        const int Size = 512;

        static readonly Dictionary<(ulong, int, bool), GeneratedWorld> Made = new Dictionary<(ulong, int, bool), GeneratedWorld>();

        static GeneratedWorld Generated(ulong seed, int version, bool story = false)
        {
            if (!Made.TryGetValue((seed, version, story), out var world))
            {
                Made[(seed, version, story)] = world = WorldGenerator.Generate(seed, Size, Size, story ? Tales.Shipped : null, story ? Bosses.Staged.Arenas : null, version,
                    Tiles.Shipped.Terrain);
            }
            return world;
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
        public void VersionThreeHasPropsAndVersionTwoHasNone()
        {
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.PropVersion, "a new world has the props (and from version 4 more)");
            Assert.Greater(WorldGenerator.PropVersion, WorldGenerator.RoomVersion);
            var old = Generated(3, WorldGenerator.RoomVersion);
            Assert.AreEqual(WorldGenerator.RoomVersion, old.Version);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    Assert.IsFalse(BiomeProps.Is(old.Map[x, y]), $"a prop at {x},{y} of a version 2 world");
            Assert.AreEqual(WorldGenerator.PropVersion, Generated(3, WorldGenerator.PropVersion).Version);
        }

        [TestCase(1UL)]
        [TestCase(3UL)]
        public void VersionThreeIsVersionTwoWithPropsOnItsOpenFloor(ulong seed)
        {
            var old = Generated(seed, WorldGenerator.RoomVersion).Map;
            var made = Generated(seed, WorldGenerator.PropVersion);
            var map = made.Map;
            Assert.AreEqual(RoomMapTests.Hash(map), RoomMapTests.Hash(WorldGenerator.Generate(seed, Size, Size, version: WorldGenerator.PropVersion, terrain: Tiles.Shipped.Terrain).Map),
                "the same seed, the same props");
            int props = 0, solidBefore = 0, solidAfter = 0;
            var kinds = new HashSet<TileType>();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (old.IsSolid(x, y)) solidBefore++;
                    if (map.IsSolid(x, y)) solidAfter++;
                    if (map[x, y] == old[x, y]) continue;
                    Assert.AreEqual(TileType.Floor, old[x, y], $"{x},{y}: only open floor gets a prop");
                    var type = map[x, y];
                    if (type == TileType.StructurePart)
                    {
                        Assert.IsTrue(BiomeProps.Wide(map[x - 1, y]), $"{x},{y}: the right half of a wide prop");
                        continue;
                    }
                    Assert.IsTrue(BiomeProps.Is(type), $"{x},{y}: {type}");
                    Assert.IsTrue(BiomeProps.Of(made.Biomes[x, y]).Any(k => k.Type == type), $"{x},{y}: {type} in the {made.Biomes[x, y]}");
                    props++;
                    kinds.Add(type);
                    // Open floor all round its footprint: a way round it, and it touches nothing.
                    int w = Structures.Footprint(type).W;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= w; dx++)
                            if (dy != 0 || dx < 0 || dx >= w) Assert.AreEqual(TileType.Floor, map[x + dx, y + dy], $"{type} at {x},{y}: the tile at {dx},{dy} from it");
                    Assert.Greater(Math.Max(Math.Abs(x - made.SpawnX), Math.Abs(y - made.SpawnY)), 5, "none by the spawn");
                    foreach (var room in made.Rooms.Nodes)
                        Assert.IsFalse(Math.Abs(x - room.AnchorX) <= 2 && Math.Abs(y - room.AnchorY) <= 2, $"{type} at {x},{y} on the middle of a room");
                }
            }
            Assert.Greater(props, 100);
            Assert.GreaterOrEqual(kinds.Count, 10, "most of the two biomes' kinds stand somewhere");
            Assert.LessOrEqual((solidAfter - solidBefore) * 100.0 / (Size * Size), 2.0, "blocked tiles rise by no more than two points in a hundred");

            // Everything walked to before is walked to still, but the tiles props stand on.
            var before = Walk(Generated(seed, WorldGenerator.RoomVersion));
            var after = Walk(made);
            for (int i = 0; i < before.Length; i++)
                if (before[i] >= 0 && !map.IsSolid(i % Size, i / Size)) Assert.GreaterOrEqual(after[i], 0, $"{i % Size},{i / Size} is shut off by a prop");
            foreach (var room in made.Rooms.Nodes)
                if (before[room.AnchorY * Size + room.AnchorX] >= 0) Assert.GreaterOrEqual(after[room.AnchorY * Size + room.AnchorX], 0, $"the room at {room.X},{room.Y}");
        }

        [Test]
        public void PropsAndDecorAreAboutAsDenseAsMeant()
        {
            var made = Generated(1, WorldGenerator.PropVersion, story: true);
            var map = made.Map;
            var floor = new Dictionary<Biome, int>();
            var props = new Dictionary<Biome, int>();
            var decor = new Dictionary<Biome, int>();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var biome = made.Biomes[x, y];
                    var type = map[x, y];
                    if (BiomeProps.Is(type)) props[biome] = props.GetValueOrDefault(biome) + 1;
                    if (type != TileType.Floor) continue;
                    floor[biome] = floor.GetValueOrDefault(biome) + 1;
                    var pick = Decor.On(map, 1, biome, x, y);
                    if (!pick.Any) continue;
                    decor[biome] = decor.GetValueOrDefault(biome) + 1;
                    Assert.IsTrue(Decor.Clear(map, x, y));
                }
            }
            foreach (var biome in new[] { Biome.Cave, Biome.Mushroom })
            {
                if (floor.GetValueOrDefault(biome) < 2000) continue;
                float propsPerHundred = props.GetValueOrDefault(biome) * 100f / floor[biome], decorPerHundred = decor.GetValueOrDefault(biome) * 100f / floor[biome];
                Assert.That(propsPerHundred, Is.InRange(1.5f, 6f), $"{biome}: props in a hundred tiles of floor");
                Assert.That(decorPerHundred, Is.InRange(5f, 13f), $"{biome}: decor in a hundred tiles of floor");
            }
            foreach (var pair in props)
                Assert.IsTrue(pair.Key == Biome.Cave || pair.Key == Biome.Mushroom, $"{pair.Key} has props before its package");
            // Places and arenas are kept clear of props.
            foreach (var site in made.Places)
                for (int dy = -site.Radius; dy <= site.Radius; dy++)
                    for (int dx = -site.Radius; dx <= site.Radius; dx++)
                        Assert.IsFalse(BiomeProps.Is(map[site.X + dx, site.Y + dy]), $"a prop in {site.Definition.Id}");
            foreach (var arena in made.Arenas)
                for (int dy = -made.ArenaRadius; dy <= made.ArenaRadius; dy++)
                    for (int dx = -made.ArenaRadius; dx <= made.ArenaRadius; dx++)
                        Assert.IsFalse(BiomeProps.Is(map[TileMap.Cell(arena.X) + dx, TileMap.Cell(arena.Y) + dy]), "a prop in an arena");
        }

        [Test]
        public void TheHeroHutsGardenFenceTurnsItsCornerInVersionThree()
        {
            var made = Generated(1, WorldGenerator.PropVersion, story: true);
            var hut = made.Places.First(p => p.Definition.Id == "hero_hut");
            var map = made.Map;
            bool Fence(int dx, int dy) => map[hut.X + dx, hut.Y + dy] == TileType.Fence;
            Assert.IsTrue(Fence(4, 4), "the corner post");
            Assert.IsTrue(Fence(4, 3) && Fence(3, 4), "the two runs meet it at a right angle");
            foreach (var (dx, dy) in new[] { (2, 2), (3, 2), (2, 3), (3, 3) }) Assert.AreEqual(TileType.Soil, map[hut.X + dx, hut.Y + dy]);
            // The garden is walked into from the plaza.
            var steps = Walk(made);
            Assert.GreaterOrEqual(steps[(hut.Y + 2) * Size + hut.X + 2], 0);
            // A version 2 world keeps the garden it was made with.
            var old = Generated(1, WorldGenerator.RoomVersion, story: true);
            var was = old.Places.First(p => p.Definition.Id == "hero_hut");
            Assert.AreEqual(TileType.Fence, old.Map[was.X + 2, was.Y + 5]);
            Assert.AreNotEqual(TileType.Fence, old.Map[was.X + 4, was.Y + 5]);
        }

        [Test]
        public void TheHostAndTheClientMakeTheSameVersionThreeMap()
        {
            var host = new WorldHost(11, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 256, MapVersion = WorldGenerator.PropVersion },
                bosses: Bosses.Staged, classes: Classes.Shipped, tiles: Tiles.Shipped, story: Tales.Shipped);
            Assert.AreEqual(WorldGenerator.PropVersion, host.Generated.Version);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Staged, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            Assert.IsNotNull(client.Map);
            Assert.AreEqual(WorldGenerator.PropVersion, client.Generator);
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(client.Map));
            int props = 0;
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    if (BiomeProps.Is(client.Map[x, y])) props++;
            Assert.Greater(props, 20, "the client's map has the props too");
            var save = WorldSave.Read(host.SaveWorld(now: 5), Tiles.Shipped);
            Assert.AreEqual(WorldGenerator.PropVersion, save.Generator);
        }

        // A small room with a thing in its middle and a player west of it, facing it, sword in hand.
        static World Room(out Player player, TileType thing, string weapon = "copper_sword")
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
            player = world.AddPlayer("a", Gear.Wearing(weapon));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            return world;
        }

        static List<SimEvent> Swing(World world, Player player, int swings)
        {
            world.SetAttacking(player.Id, true);
            var events = new List<SimEvent>();
            for (int tick = 0; tick < 10 * World.TicksPerSecond && events.Count(e => e.Type == SimEventType.Attack && e.Source == player.Id) < swings; tick++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            world.SetAttacking(player.Id, false);
            return events;
        }

        [Test]
        public void ASwingLightsALampAndItStaysLit()
        {
            var world = Room(out var player, TileType.UnlitLamp);
            Assert.IsFalse(TileLight.Lit(world.Map, Tiles.Shipped, player.Position, 6));

            var events = Swing(world, player, 1);

            Assert.AreEqual(TileType.LitLamp, world.Map[3, 2]);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Dug && e.Amount == World.LampLit), "the blow that lit it is told");
            Assert.IsTrue(world.ChangedTiles().Any(t => t.X == 3 && t.Y == 2 && t.Type == TileType.LitLamp), "a changed tile: saved and sent like any other");
            Assert.AreEqual(0, player.Inventory.CountOf(Gear.Data.FindMaterial("torch")), "nothing is taken from it");
            Assert.IsTrue(TileLight.Lit(world.Map, Tiles.Shipped, player.Position, 6));

            Swing(world, player, 3);
            Assert.AreEqual(TileType.LitLamp, world.Map[3, 2], "a lit lamp is not put out or dug");
            Assert.IsTrue(world.Map.IsSolid(3, 2));
        }

        [Test]
        public void ASporeSacBurstsIntoACloudThatFillsMonsters()
        {
            var world = Room(out var player, TileType.SporeSac);

            Swing(world, player, 1);

            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(Gear.Data.FindMaterial("glow_spore")));
            var cloud = world.Hazards.Single(h => h.Shape == HazardShape.Balm);
            Assert.AreEqual(TileMap.Center(3, 2), cloud.Position);
            Assert.AreEqual(World.DeathcloudRadius, cloud.Size);
        }

        [Test]
        public void AWidePropGoesWithBothItsTilesFromEitherSide()
        {
            var world = Room(out var player, TileType.FallenLog);
            Assert.IsTrue(world.Map.IsSolid(3, 2) && world.Map.IsSolid(4, 2));
            // From the east, at its right half.
            world.MovePlayer(player.Id, TileMap.Center(5, 2), new Vec2(-1, 0), 1);
            player.Position = TileMap.Center(5, 2);

            Swing(world, player, 12);

            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(TileType.Floor, world.Map[4, 2]);
            Assert.GreaterOrEqual(player.Inventory.CountOf(Gear.Data.FindMaterial("mushroom")), 1);
        }

        [Test]
        public void TileLightSaysWhatBurnsAndHowFar()
        {
            Assert.AreEqual(6, TileLight.RadiusOf(TileType.LitLamp, Tiles.Shipped));
            Assert.AreEqual(0, TileLight.RadiusOf(TileType.UnlitLamp, Tiles.Shipped));
            Assert.Greater(TileLight.RadiusOf(TileType.Torch, Tiles.Shipped), 5);
            Assert.AreEqual(6, TileLight.RadiusOf(TileType.Campfire, null), "the dressing burns without a sheet");
            var sources = TileLight.Sources(Tiles.Shipped).Select(s => s.Type).ToList();
            CollectionAssert.IsSubsetOf(new[] { TileType.Torch, TileType.LitLamp, TileType.GlowCaps, TileType.Campfire, TileType.LampPost }, sources);
            CollectionAssert.DoesNotContain(sources, TileType.UnlitLamp);

            var map = new TileMap(30, 12, TileType.Floor);
            map[10, 5] = TileType.LitLamp;
            Assert.IsTrue(TileLight.Lit(map, Tiles.Shipped, TileMap.Center(15, 5)), "five tiles off");
            Assert.IsFalse(TileLight.Lit(map, Tiles.Shipped, TileMap.Center(17, 5)), "seven tiles off");
            Assert.IsFalse(TileLight.Lit(map, Tiles.Shipped, TileMap.Center(15, 5), within: 3), "asked for lights within three");
            map[10, 5] = TileType.UnlitLamp;
            Assert.IsFalse(TileLight.Lit(map, Tiles.Shipped, TileMap.Center(11, 5)));
        }

        [Test]
        public void EveryPropIsInTheSheetButTheGiantCap()
        {
            foreach (TileType type in Enum.GetValues(typeof(TileType)))
            {
                if (!BiomeProps.Is(type)) continue;
                Assert.IsTrue(new TileMap(3, 3, type).IsSolid(1, 1), $"{type} blocks its tile");
                if (type == TileType.GiantCap) Assert.IsNull(Tiles.Shipped.Find(type), "a landmark is never dug");
                else Assert.IsNotNull(Tiles.Shipped.Find(type), $"{type} has a row in tiles.csv");
            }
            Assert.IsTrue(BiomeProps.Of(Biome.Cave).Count >= 7 && BiomeProps.Of(Biome.Mushroom).Count >= 6);
            Assert.AreEqual(0, BiomeProps.Of(Biome.Ice).Count, "the other biomes get theirs with their packages");
        }
    }
}
