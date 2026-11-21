using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The generator's versions (Docs/WorldRework.md 1단계): version 1 is the caves every world had before and never
    // changes again; version 2 is rooms, corridors and great masses of rock, different in every biome (terrain.csv).
    public class RoomMapTests
    {
        const int Size = WorldGenerator.GameSize;

        static GeneratedWorld Rooms(ulong seed, int size = Size) =>
            WorldGenerator.Generate(seed, size, size, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion, Tiles.Shipped.Terrain);

        // The rooms alone: no places or roads of the story laid over them.
        static GeneratedWorld Bare(ulong seed, int size = Size) =>
            WorldGenerator.Generate(seed, size, size, version: WorldGenerator.RoomVersion, terrain: Tiles.Shipped.Terrain);

        // Every tile of the map folded into one number (FNV-1a).
        static ulong Hash(TileMap map)
        {
            ulong hash = 14695981039346656037UL;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    hash ^= (byte)map[x, y];
                    hash *= 1099511628211UL;
                }
            }
            return hash;
        }

        // Steps from the spawn to every tile, walking: not through anything solid, nor lava. -1 where none.
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
                    if (!map.InBounds(nx, ny) || steps[ny * map.Width + nx] >= 0 || map.IsSolid(nx, ny) || map[nx, ny] == TileType.Lava) continue;
                    steps[ny * map.Width + nx] = steps[at] + 1;
                    queue.Enqueue(ny * map.Width + nx);
                }
            }
            return steps;
        }

        // The hashes were taken from the generator as it was the day version 2 was added (2026-11-21, origin/main
        // 2b33756): a world saved before then must open as the same caves for good.
        [Test]
        public void VersionOneIsTheCavesItAlwaysWas()
        {
            Assert.AreEqual(13141294806712872822UL, Hash(WorldGenerator.Generate(1).Map));
            Assert.AreEqual(4743839515206118998UL, Hash(WorldGenerator.Generate(7).Map));
            Assert.AreEqual(17259121183764716915UL, Hash(WorldGenerator.Generate(1, 256, 256, Tales.Shipped, Bosses.Staged.Arenas).Map));
            Assert.AreEqual(13866312223472256165UL, Hash(WorldGenerator.Generate(7, 256, 256, Tales.Shipped, Bosses.Staged.Arenas).Map));
            Assert.AreEqual(13217574400438954417UL, Hash(WorldGenerator.Generate(3, Size, Size, Tales.Shipped, Bosses.Staged.Arenas).Map));
            // Asked for by number, and with a terrain sheet it has no use for, it is the same.
            Assert.AreEqual(4743839515206118998UL, Hash(WorldGenerator.Generate(7, version: WorldGenerator.CaveVersion, terrain: Tiles.Shipped.Terrain).Map));
            var caves = WorldGenerator.Generate(7);
            Assert.AreEqual(WorldGenerator.CaveVersion, caves.Version);
            Assert.IsNull(caves.Rooms, "the caves have no rooms");
        }

        [Test]
        public void TheSameSeedMakesTheSameRooms()
        {
            Assert.AreEqual(Hash(Rooms(5, 256).Map), Hash(Rooms(5, 256).Map));
            Assert.AreNotEqual(Hash(Rooms(5, 256).Map), Hash(Rooms(6, 256).Map));
            Assert.AreNotEqual(Hash(Rooms(5, 256).Map), Hash(WorldGenerator.Generate(5, 256, 256, Tales.Shipped, Bosses.Staged.Arenas).Map));
        }

        [Test]
        public void EveryRoomArenaAndPlaceIsWalkedToFromTheSpawn()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var world = Rooms(seed);
                var steps = Walk(world);
                int At(int x, int y) => steps[y * Size + x];
                Assert.AreEqual(WorldGenerator.RoomVersion, world.Version);
                // A place of the story may stand on a room's middle; without the story every room's middle is floor.
                var bare = Walk(Bare(seed));
                foreach (var room in world.Rooms.Nodes) Assert.GreaterOrEqual(bare[room.Y * Size + room.X], 0, $"seed {seed}: the {room.Biome} room at ({room.X}, {room.Y})");
                foreach (var arena in world.Arenas)
                    Assert.GreaterOrEqual(At(TileMap.Cell(arena.X), TileMap.Cell(arena.Y)), 0, $"seed {seed}: the arena at {arena.X}, {arena.Y}");
                Assert.AreEqual(Tales.Shipped.Places.Count, world.Places.Count);
                foreach (var place in world.Places)
                {
                    // A waypoint is stood on; the tiles round it are the plaza's.
                    bool reached = new[] { (0, 0), (0, -2), (0, 2), (2, 0), (-2, 0) }.Any(o => At(place.X + o.Item1, place.Y + o.Item2) >= 0);
                    Assert.IsTrue(reached, $"seed {seed}: {place.Definition.Id}");
                }
                // No floor is left that nothing walks to (lava may lie between).
                var map = world.Map;
                var open = new bool[Size * Size];
                var stack = new Stack<int>();
                open[world.SpawnY * Size + world.SpawnX] = true;
                stack.Push(world.SpawnY * Size + world.SpawnX);
                while (stack.Count > 0)
                {
                    int at = stack.Pop();
                    int x = at % Size, y = at / Size;
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (!map.InBounds(nx, ny) || open[ny * Size + nx] || map.IsSolid(nx, ny)) continue;
                        open[ny * Size + nx] = true;
                        stack.Push(ny * Size + nx);
                    }
                }
                int shut = 0;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        if (!map.IsSolid(x, y) && !open[y * Size + x]) shut++;
                // The royal city's walls close a few tiles of its own paving, as they did in the caves of version 1; the rooms leave none.
                Assert.Less(shut, 40, $"seed {seed}: floor shut off from the spawn");
            }
        }

        [Test]
        public void EachArenaIsARoomInTheRockWithOneWayIn()
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                // Without the story: no road comes by.
                var world = WorldGenerator.Generate(seed, Size, Size, version: WorldGenerator.RoomVersion);
                var arenas = world.Rooms.Nodes.Where(n => n.Kind == RoomKind.Arena).ToList();
                Assert.AreEqual(world.CaveArenas, arenas.Count);
                foreach (var arena in arenas)
                {
                    Assert.AreEqual(1, arena.Edges.Count, "an arena is a dead end");
                    // The ring just outside the room: rock but for one gap, the way in.
                    int ring = WorldGenerator.ArenaRadius + 2, gaps = 0;
                    bool was = true;
                    for (int i = 0; i <= 360; i++)
                    {
                        double angle = i * Math.PI / 180;
                        int x = arena.X + (int)Math.Round(Math.Cos(angle) * ring), y = arena.Y + (int)Math.Round(Math.Sin(angle) * ring);
                        bool solid = world.Map.IsSolid(x, y);
                        if (!solid && was && i > 0) gaps++;
                        was = solid;
                    }
                    Assert.LessOrEqual(gaps, 1, $"seed {seed}: ways into the arena at ({arena.X}, {arena.Y})");
                }
            }
        }

        [Test]
        public void EachBiomeHasItsOwnShareOfRock()
        {
            var terrain = Tiles.Shipped.Terrain;
            var rock = new Dictionary<Biome, long>();
            var liquid = new Dictionary<Biome, long>();
            var tiles = new Dictionary<Biome, long>();
            long solid = 0;
            ulong[] seeds = { 1, 2, 3 };
            foreach (ulong seed in seeds)
            {
                var world = Rooms(seed);
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        var biome = world.Biomes[x, y];
                        var type = world.Map[x, y];
                        tiles[biome] = tiles.GetValueOrDefault(biome) + 1;
                        if (type == TileType.Water || type == TileType.Lava) liquid[biome] = liquid.GetValueOrDefault(biome) + 1;
                        else if (world.Map.IsSolid(x, y)) rock[biome] = rock.GetValueOrDefault(biome) + 1;
                        if (world.Map.IsSolid(x, y)) solid++;
                    }
                }
            }
            var shares = new List<double>();
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            {
                var rule = terrain.Of(biome);
                double share = 100.0 * rock.GetValueOrDefault(biome) / tiles[biome];
                double wet = 100.0 * liquid.GetValueOrDefault(biome) / tiles[biome];
                Assert.AreEqual(rule.WallTarget, share, RockBand, $"{biome}: rock, of a hundred tiles");
                // Pools to fish from are set in the rock of every biome but the dry ones, a few in a hundred tiles.
                Assert.AreEqual(rule.LiquidTarget, wet, LiquidBand, $"{biome}: lava and water, of a hundred tiles");
                shares.Add(share);
            }
            Assert.GreaterOrEqual(shares.Max() - shares.Min(), 30, "the most closed biome against the most open");
            double all = 100.0 * solid / ((long)seeds.Length * Size * Size);
            Assert.That(all, Is.InRange(48, 62), "the whole map's closed tiles (the caves of version 1 had 29)");
        }

        const double RockBand = 6;
        const double LiquidBand = 6;

        [Test]
        public void RockStandsInGreatMassesAndRoomsAreWide()
        {
            var world = Rooms(2);
            var map = world.Map;
            // Lumps of rock joined by their sides.
            var lump = new int[Size * Size];
            var sizes = new List<int>();
            var stack = new Stack<int>();
            for (int start = 0; start < lump.Length; start++)
            {
                if (lump[start] != 0 || !Rock(start % Size, start / Size)) continue;
                int size = 0;
                lump[start] = sizes.Count + 1;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int at = stack.Pop();
                    size++;
                    int x = at % Size, y = at / Size;
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (!map.InBounds(nx, ny) || lump[ny * Size + nx] != 0 || !Rock(nx, ny)) continue;
                        lump[ny * Size + nx] = sizes.Count + 1;
                        stack.Push(ny * Size + nx);
                    }
                }
                sizes.Add(size);
            }
            bool Rock(int x, int y) => map.IsSolid(x, y) && map[x, y] != TileType.Water;
            // Leaving out the biggest (the one the map's border belongs to), half the rock is in lumps of a screen or more.
            var inner = sizes.OrderBy(s => s).Take(sizes.Count - 1).ToList();
            long half = inner.Sum(s => (long)s) / 2, running = 0;
            int median = inner.First(s => (running += s) >= half);
            Assert.GreaterOrEqual(median, 800, "the lump the middle tile of rock is in (the caves of version 1: about 110)");

            // Of the floor, a good part has nothing within four tiles of it: the insides of rooms.
            int floor = 0, wide = 0;
            for (int y = 4; y < Size - 4; y++)
            {
                for (int x = 4; x < Size - 4; x++)
                {
                    if (map.IsSolid(x, y)) continue;
                    floor++;
                    bool clear = true;
                    for (int oy = -4; oy <= 4 && clear; oy++)
                        for (int ox = -4; ox <= 4 && clear; ox++)
                            clear = !map.IsSolid(x + ox, y + oy);
                    if (clear) wide++;
                }
            }
            Assert.GreaterOrEqual(100.0 * wide / floor, 18, "floor open nine tiles across, of a hundred (the caves of version 1: 6)");
        }

        [Test]
        public void TheRoomsMakeLoopsAndFewDeadEnds()
        {
            var rooms = Rooms(3).Rooms;
            Assert.AreEqual(32, rooms.CellSize);
            int count = rooms.Nodes.Count(n => n.Kind != RoomKind.Arena);
            Assert.That(count, Is.InRange(700, 1600));
            Assert.AreEqual(1, rooms.Nodes.Count(n => n.Kind == RoomKind.Spawn));
            // A tree over every room, the arenas hung on it, and loops beyond.
            Assert.AreEqual(rooms.Nodes.Count - 1, rooms.Edges.Count(e => !e.Loop));
            Assert.GreaterOrEqual(rooms.Loops, count / 5, "ways opened again for loops");
            Assert.Less(rooms.DeadEnds, count / 8, "rooms left with one way in");
            foreach (var edge in rooms.Edges) Assert.That(edge.Width, Is.InRange(2, 6));
            foreach (var node in rooms.Nodes) Assert.GreaterOrEqual(node.Edges.Count, 1);
        }

        [Test]
        public void CopperIsNearTheSpawn()
        {
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var world = Rooms(seed);
                var steps = Walk(world);
                int nearest = int.MaxValue;
                for (int y = world.SpawnY - 40; y <= world.SpawnY + 40; y++)
                {
                    for (int x = world.SpawnX - 40; x <= world.SpawnX + 40; x++)
                    {
                        if (world.Map[x, y] != TileType.CopperOre) continue;
                        foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                            if (steps[ny * Size + nx] >= 0) nearest = Math.Min(nearest, steps[ny * Size + nx]);
                    }
                }
                Assert.LessOrEqual(nearest, WorldGenerator.CopperSteps, $"seed {seed}: steps to the first copper");
            }
        }

        [Test]
        public void SmallMapsGetSmallRooms()
        {
            foreach (int size in new[] { 128, 256 })
            {
                for (ulong seed = 1; seed <= 6; seed++)
                {
                    var world = Bare(seed, size);
                    Assert.AreEqual(16, world.Rooms.CellSize);
                    var map = world.Map;
                    var seen = new HashSet<int> { world.SpawnY * size + world.SpawnX };
                    var queue = new Queue<int>(seen);
                    while (queue.Count > 0)
                    {
                        int at = queue.Dequeue();
                        int x = at % size, y = at / size;
                        foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                            if (map.InBounds(nx, ny) && !map.IsSolid(nx, ny) && map[nx, ny] != TileType.Lava && seen.Add(ny * size + nx)) queue.Enqueue(ny * size + nx);
                    }
                    foreach (var arena in world.Arenas)
                        Assert.IsTrue(seen.Contains(TileMap.Cell(arena.Y) * size + TileMap.Cell(arena.X)), $"{size}, seed {seed}: the arena at {arena.X}, {arena.Y}");
                    foreach (var room in world.Rooms.Nodes) Assert.IsTrue(seen.Contains(room.Y * size + room.X), $"{size}, seed {seed}: the room at ({room.X}, {room.Y})");
                    for (int i = 0; i < size; i++)
                        Assert.IsTrue(map.IsSolid(i, 0) && map.IsSolid(i, size - 1) && map.IsSolid(0, i) && map.IsSolid(size - 1, i), "the border is rock");

                    // With the story on top, the places and the arenas are still walked to.
                    var story = Rooms(seed, size);
                    var steps = Walk(story);
                    foreach (var arena in story.Arenas)
                        Assert.GreaterOrEqual(steps[TileMap.Cell(arena.Y) * size + TileMap.Cell(arena.X)], 0, $"{size}, seed {seed}, with the story: the arena at {arena.X}, {arena.Y}");
                    foreach (var place in story.Places)
                        Assert.IsTrue(new[] { (0, 0), (0, -2), (0, 2), (2, 0), (-2, 0) }.Any(o => steps[(place.Y + o.Item2) * size + place.X + o.Item1] >= 0),
                            $"{size}, seed {seed}: {place.Definition.Id}");
                }
            }
        }

        [Test]
        public void TheHostTellsTheClientWhichGeneratorAndBothMakeTheSameMap()
        {
            var host = new WorldHost(11, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 256, MapVersion = WorldGenerator.RoomVersion },
                bosses: Bosses.Staged, classes: Classes.Shipped, tiles: Tiles.Shipped, story: Tales.Shipped);
            Assert.AreEqual(WorldGenerator.RoomVersion, host.Generated.Version);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Staged, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            Assert.IsNotNull(client.Map);
            Assert.AreEqual(Hash(host.World.Map), Hash(client.Map));

            var welcome = WelcomeMessage.Decode(new WelcomeMessage(3, 9, 64, 32, new Vec2(5, 6), generator: WorldGenerator.RoomVersion).Encode());
            Assert.AreEqual(WorldGenerator.RoomVersion, welcome.Generator);
            Assert.AreEqual(WorldGenerator.CaveVersion, WelcomeMessage.Decode(new WelcomeMessage(3, 9, 64, 32, new Vec2(5, 6)).Encode()).Generator);
        }

        [Test]
        public void ASavedWorldKeepsItsGenerator()
        {
            var rooms = new WorldHost(11, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 128, MapVersion = WorldGenerator.RoomVersion }, tiles: Tiles.Shipped);
            var save = WorldSave.Read(rooms.SaveWorld(now: 5), Tiles.Shipped);
            Assert.AreEqual(WorldGenerator.RoomVersion, save.Generator);
            Assert.AreEqual(128, save.Width);

            var caves = new WorldHost(11, Monsters.Shipped, Gear.Data, tiles: Tiles.Shipped);
            byte[] data = caves.SaveWorld(now: 5);
            Assert.AreEqual(WorldGenerator.CaveVersion, WorldSave.Read(data, Tiles.Shipped).Generator);

            // A save from before the generator was written down (format 11: no byte after the size) is the caves.
            var old = data.ToList();
            old.RemoveAt(4 + 4 + 8 + 4 + 4);
            old[4] = 11;
            var read = WorldSave.Read(old.ToArray(), Tiles.Shipped);
            Assert.AreEqual(WorldGenerator.CaveVersion, read.Generator);
            Assert.AreEqual(11UL, read.Seed);
            Assert.AreEqual(5, read.SavedAt);
        }

        [Test]
        public void TheTerrainSheetIsTheOneTheCodeFallsBackOn()
        {
            var shipped = TerrainSheet.Parse(File.ReadAllText(RepoFile.Find("Assets/_Project/Data/World/" + TerrainSheet.File)));
            Assert.IsTrue(shipped.Success, string.Join("\n", shipped.Errors));
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            {
                var a = shipped.Of(biome);
                var b = TerrainSheet.Default.Of(biome);
                Assert.IsTrue(shipped.Rules.Contains(a), $"{biome} has a row");
                Assert.AreEqual(
                    (b.Style, b.RoomFill, b.Rooms, b.RoomMin, b.RoomMax, b.CorridorMin, b.CorridorMax, b.LoopPercent, b.DeadEndPercent, b.WallTarget, b.LiquidTarget, b.Feature, b.FeaturePercent),
                    (a.Style, a.RoomFill, a.Rooms, a.RoomMin, a.RoomMax, a.CorridorMin, a.CorridorMax, a.LoopPercent, a.DeadEndPercent, a.WallTarget, a.LiquidTarget, a.Feature, a.FeaturePercent),
                    $"{biome}: terrain.csv and TerrainSheet.Default");
            }
            Assert.AreSame(TerrainSheet.Default, TerrainSheet.Parse(""), "no sheet: the one in code");
        }

        [Test]
        public void TheTerrainSheetSaysWhatIsWrong()
        {
            const string header = "biome,style,room_fill,rooms,room_min,room_max,corridor_min,corridor_max,loop_percent,deadend_percent,wall_target,liquid_target,feature,feature_percent\n";
            Assert.IsFalse(TerrainSheet.Parse("biome,style\ncave,cave\n").Success, "columns missing");
            Assert.IsFalse(TerrainSheet.Parse(header + "swamp,cave,80,1,10,20,3,3,30,15,55,0,,0\n").Success, "no such biome");
            Assert.IsFalse(TerrainSheet.Parse(header + "cave,spiral,80,1,10,20,3,3,30,15,55,0,,0\n").Success, "no such style");
            Assert.IsFalse(TerrainSheet.Parse(header + "cave,cave,80,3,10,20,3,3,30,15,55,0,,0\n").Success, "three rooms to a cell");
            Assert.IsFalse(TerrainSheet.Parse(header + "cave,cave,80,1,20,10,3,3,30,15,55,0,,0\n").Success, "rooms from big to small");
            Assert.IsFalse(TerrainSheet.Parse(header + "cave,cave,80,1,10,20,3,3,30,15,55,0,,0\ncave,cave,80,1,10,20,3,3,30,15,55,0,,0\n").Success, "a biome twice");
            var one = TerrainSheet.Parse(header + "ice,maze,50,2,8,10,2,3,10,20,70,0,Pillar,4\n");
            Assert.IsTrue(one.Success, string.Join("\n", one.Errors));
            Assert.AreEqual(TerrainStyle.Maze, one.Of(Biome.Ice).Style);
            Assert.AreEqual(TileType.Pillar, one.Of(Biome.Ice).Feature);
            Assert.AreEqual(TerrainStyle.Cave, one.Of(Biome.Cave).Style, "a biome with no row is as the code has it");
        }
    }
}
