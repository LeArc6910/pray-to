using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum RoomKind : byte
    {
        Room,
        // The room the spawn stands in.
        Spawn,
        // A boss's arena: a dead end, one way in.
        Arena,
    }

    // A room of the generator's version 2: where it is, how big, whose terrain it was carved by, and the ways out of
    // it (indexes into RoomGraph.Edges).
    public sealed class RoomNode
    {
        internal readonly List<int> Ways = new List<int>();

        internal RoomNode(int index, int x, int y, int radius, Biome biome, TerrainStyle style, RoomKind kind)
        {
            Index = index;
            X = x;
            Y = y;
            Radius = radius;
            Biome = biome;
            Style = style;
            Kind = kind;
            AnchorX = x;
            AnchorY = y;
        }

        public int Index { get; }
        public int X { get; }
        public int Y { get; }
        // About how far its floor goes from the middle (half a crack's length, an arena's radius).
        public int Radius { get; }
        public Biome Biome { get; }
        public TerrainStyle Style { get; }
        public RoomKind Kind { get; }
        public Vec2 Center => TileMap.Center(X, Y);
        public IReadOnlyList<int> Edges => Ways;
        public bool DeadEnd => Ways.Count == 1;
        // What the room is for (WorldGenerator.Roles.cs); Plain for the spawn's room and the arenas.
        public RoomRole Role { get; internal set; }
        // The chunk of rooms.csv set into it and which of the eight ways round, or null for a room left as carved.
        public RoomChunk Chunk { get; internal set; }
        public int ChunkWay { get; internal set; }
        // A road of the story runs through it: no monsters come into being there (Docs/Design.md).
        public bool Roadside { get; internal set; }
        // Rooms from the spawn's room, along the ways.
        public int Depth { get; internal set; }
        // A tile of its open floor that is walked to from the spawn: its middle, or with a walled chunk on its middle,
        // the tile outside the chunk's door.
        public int AnchorX { get; internal set; }
        public int AnchorY { get; internal set; }
        // For a room shut behind a wall that must be dug (Hidden, Locked): a tile inside, come to only through that wall.
        public (int X, int Y)? Inside { get; internal set; }
        // Where its chunk was set: its lower left tile and size (null for a room left as carved).
        public (int Left, int Bottom, int Width, int Height)? Box { get; internal set; }
        // A hidden room set into the rock beside its room rather than in its middle (3b): from outside only a crack in
        // the rock face shows. Its Box is the hidden room itself.
        public bool Buried { get; internal set; }
    }

    // What a way is, beyond a corridor (Docs/WorldRework.md 뼈대, 간선).
    public enum RoomEdgeKind : byte
    {
        Way,
        // A wall with a crack across it, a tile or two thick: dug through, the way is a short cut. Only ever a loop,
        // so nothing lies behind it that is not reached another way.
        Cracked,
    }

    // A way between two rooms. Loop: opened again after the spanning tree was made, so it closes a ring.
    public sealed class RoomEdge
    {
        internal RoomEdge(int a, int b, int width, bool loop)
        {
            A = a;
            B = b;
            Width = width;
            Loop = loop;
        }

        public int A { get; }
        public int B { get; }
        public int Width { get; }
        public bool Loop { get; }
        public RoomEdgeKind Kind { get; internal set; }
        // The tiles of the way as it was carved, from A to B, for what is set across it later.
        internal List<(int X, int Y)> Path;
    }

    // The rooms of a version 2 world and the ways between them (Docs/WorldRework.md 뼈대). Kept on GeneratedWorld for
    // what comes after the skeleton: room roles, room chunks, roads along the graph, packs.
    public sealed class RoomGraph
    {
        internal RoomGraph(int cellSize, List<RoomNode> nodes, List<RoomEdge> edges)
        {
            CellSize = cellSize;
            Nodes = nodes;
            Edges = edges;
        }

        // The map is cut into cells this many tiles across; each has a room (or a few small ones) or none.
        public int CellSize { get; }
        public IReadOnlyList<RoomNode> Nodes { get; }
        public IReadOnlyList<RoomEdge> Edges { get; }
        public int Loops => Edges.Count(e => e.Loop);
        public int DeadEnds => Nodes.Count(n => n.Kind == RoomKind.Room && n.DeadEnd);
        // What the chunks marked for later (where a pack stands, where the chest is), in room order.
        public IReadOnlyList<RoomMark> Marks => MarkList;
        internal readonly List<RoomMark> MarkList = new List<RoomMark>();
        // The things to find in the rooms (WorldGenerator.Pois.cs), numbered as laid: Poi.Index is the place here.
        public IReadOnlyList<Poi> Pois => PoiList;
        internal readonly List<Poi> PoiList = new List<Poi>();
        // The rock of the chunks' walls (y * width + x): nothing that joins rooms digs through it.
        internal HashSet<int> Kept = new HashSet<int>();

        // The room whose middle is nearest a tile, or null with no rooms.
        public RoomNode Nearest(int x, int y)
        {
            RoomNode best = null;
            long bestDistance = long.MaxValue;
            foreach (var node in Nodes)
            {
                long distance = (long)(node.X - x) * (node.X - x) + (long)(node.Y - y) * (node.Y - y);
                if (distance < bestDistance) (best, bestDistance) = (node, distance);
            }
            return best;
        }
    }

    // ---- Version 2 of the generator (2026-11-21, the person: "the map only got bigger; I never feel I am exploring,
    // only walking to where I am going", Docs/WorldRework.md 1단계). The frame is version 1's: the spawn, the biomes,
    // the six cave arenas, the ore and the pools by the same rules, then the places and roads of the story and the
    // arenas' dressing on top. What changes is the rock between: the map starts solid, is cut into cells, each cell
    // gets a room shaped by its biome (terrain.csv), the rooms are joined into a spanning tree with some ways opened
    // again for loops, and the ways are carved. Integer math only. Version 1 (GenerateCaves) is never touched again:
    // worlds saved before this keep their caves. ----
    public static partial class WorldGenerator
    {
        // Which generator made a world: the caves every world had before (2026-11-21), or rooms and corridors.
        public const int CaveVersion = 1;
        public const int RoomVersion = 2;
        // What the game makes a new world with. Tests and sims keep CaveVersion unless they ask.
        public const int NewWorldVersion = RoomVersion;

        const ulong RoomStream = 40;
        const ulong ShapeStream = 41;
        const ulong WayStream = 42;
        const ulong RiverStream = 43;
        const ulong FeatureStream = 44;
        // The game's map is cut into cells this wide; maps under SmallMap into cells half as wide, with rooms half
        // the size.
        const int RoomCell = 32;
        const int SmallMap = 512;
        // The map's outer tiles stay rock.
        const int Border = 2;
        // Rock is kept this far round an arena beyond its radius, but for its one way in.
        const int ArenaWall = 3;
        // Ore lies in the rock this many tiles or fewer behind a wall's face.
        const int OreDepth = 4;
        // Copper is always within this many steps of the spawn (the guide's first goals).
        public const int CopperSteps = 35;

        static GeneratedWorld GenerateRooms(ulong seed, int width, int height, TerrainData terrain)
        {
            var map = new TileMap(width, height);
            int spawnX = width / 2;
            int spawnY = height / 2;
            int k = ScaleOf(width, height), area = k * k;

            // The frame, by version 1's rules (GenerateCaves): the first arena one of eight ways from the spawn, the
            // deep one in a far corner on its side, the lava one opposite, the abyss and the lake in the two side
            // corners, the sand one on a flank of the crystal quarter.
            var random = new GameRandom(seed, Stream);
            var (offsetX, offsetY) = ArenaOffsets[random.Next(ArenaOffsets.Length)];
            offsetX *= k;
            offsetY *= k;
            int ClampX(int x) => Clamp(x, ArenaRadius + 2, width - ArenaRadius - 3);
            int ClampY(int y) => Clamp(y, ArenaRadius + 2, height - ArenaRadius - 3);
            int arenaX = ClampX(spawnX + offsetX), arenaY = ClampY(spawnY + offsetY);
            var deep = new GameRandom(seed, DeepStream);
            var corners = new[] { (1, 1), (-1, 1), (-1, -1), (1, -1) }.Where(c => c.Item1 * offsetX + c.Item2 * offsetY > 0).ToArray();
            var (cornerX, cornerY) = corners[deep.Next(corners.Length)];
            int deepX = ClampX(spawnX + cornerX * DeepArenaOffset * k), deepY = ClampY(spawnY + cornerY * DeepArenaOffset * k);
            bool mushroomFirst = new GameRandom(seed, BiomeStream).Next(2) == 0;
            var abyss = new GameRandom(seed, AbyssStream);
            var (abyssCornerX, abyssCornerY) = abyss.Next(2) == 0 ? (cornerX, -cornerY) : (-cornerX, cornerY);
            var (lakeCornerX, lakeCornerY) = abyssCornerX == cornerX ? (-cornerX, cornerY) : (cornerX, -cornerY);
            var sand = new GameRandom(seed, SandStream);
            var flanks = new[]
            {
                (spawnX + cornerX * SandArenaOffset.Along * k, spawnY + cornerY * SandArenaOffset.Across * k),
                (spawnX + cornerX * SandArenaOffset.Across * k, spawnY + cornerY * SandArenaOffset.Along * k),
            };
            int Far((int X, int Y) f) => (f.X - arenaX) * (f.X - arenaX) + (f.Y - arenaY) * (f.Y - arenaY);
            var (sandX, sandY) = Far(flanks[0]) == Far(flanks[1]) ? flanks[sand.Next(2)] : Far(flanks[0]) > Far(flanks[1]) ? flanks[0] : flanks[1];
            sandX = ClampX(sandX);
            sandY = ClampY(sandY);
            var biomes = BiomeMap.Make(seed, width, height, spawnX, spawnY, (cornerX, cornerY), mushroomFirst, (abyssCornerX, abyssCornerY),
                (lakeCornerX, lakeCornerY), (sandX, sandY));
            int lavaX = ClampX(spawnX - cornerX * DeepArenaOffset * k), lavaY = ClampY(spawnY - cornerY * DeepArenaOffset * k);
            int abyssX = ClampX(spawnX + abyssCornerX * DeepArenaOffset * k), abyssY = ClampY(spawnY + abyssCornerY * DeepArenaOffset * k);
            int lakeX = ClampX(spawnX + lakeCornerX * DeepArenaOffset * k), lakeY = ClampY(spawnY + lakeCornerY * DeepArenaOffset * k);
            var arenas = new[] { (arenaX, arenaY), (deepX, deepY), (lavaX, lavaY), (abyssX, abyssY), (lakeX, lakeY), (sandX, sandY) };

            var graph = new RoomCarver(map, biomes, terrain, seed, arenas).Carve(spawnX, spawnY);

            // Ore, by version 1's counts and distances, but in the rock a few tiles behind the rooms' walls, where it is
            // seen and dug to, not lost in the middle of a mass.
            var rim = RimRock(map, OreDepth);
            bool NearArena(int x, int y, int margin) =>
                arenas.Any(a => (x - a.Item1) * (x - a.Item1) + (y - a.Item2) * (y - a.Item2) <= (ArenaRadius + margin) * (ArenaRadius + margin));
            bool From(int x, int y, int distance) => (x - spawnX) * (x - spawnX) + (y - spawnY) * (y - spawnY) >= distance * distance;
            var ore = new GameRandom(seed, OreStream);
            PlaceRimVeins(map, ore, rim, TileType.CopperOre, CopperVeins * area, (x, y) => true);
            PlaceRimVeins(map, ore, rim, TileType.IronOre, IronVeins * area, (x, y) => From(x, y, IronDistance * k));
            PlaceRimVeins(map, new GameRandom(seed, MithrilStream), rim, TileType.MithrilOre, MithrilVeins * area, (x, y) => From(x, y, MithrilDistance * k));
            PlaceRimVeins(map, new GameRandom(seed, LavaStream), rim, TileType.ObsidianOre, ObsidianVeins * area,
                (x, y) => biomes[x, y] == Biome.Lava && From(x, y, ObsidianDistance * k));
            PlaceRimVeins(map, abyss, rim, TileType.VoidOre, VoidVeins * area, (x, y) => biomes[x, y] == Biome.Abyss && !NearArena(x, y, 2));

            // Pools to fish from, set into the rock beside the floor as before: not by the spawn or an arena, nor in
            // the lava fields or the dry sand caves. The lake has its own water (the islands' style) and more pools.
            bool Shore(int x, int y) => map[x, y] == TileType.Wall && biomes[x, y] != Biome.Lava && biomes[x, y] != Biome.Sand
                && From(x, y, WaterDistance * k) && !NearArena(x, y, ArenaWall);
            PlaceWater(map, new GameRandom(seed, WaterStream), WaterPools * area, Shore);
            PlaceWater(map, new GameRandom(seed, LakeStream), WaterPools * area, (x, y) => Shore(x, y) && biomes[x, y] == Biome.Lake);

            return new GeneratedWorld(map, spawnX, spawnY, arenaX, arenaY, ArenaRadius, deepX, deepY, biomes, (lavaX, lavaY), (abyssX, abyssY), (lakeX, lakeY),
                (sandX, sandY))
            {
                Version = RoomVersion,
                Rooms = graph,
            };
        }

        // The plain rock within depth tiles of open floor, as indexes (y * width + x), in map order.
        static List<int> RimRock(TileMap map, int depth)
        {
            int width = map.Width, height = map.Height;
            var steps = new byte[width * height];
            var queue = new Queue<int>();
            var rim = new List<int>();
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (map[x, y] != TileType.Wall) continue;
                    if (!map.IsSolid(x + 1, y) || !map.IsSolid(x - 1, y) || !map.IsSolid(x, y + 1) || !map.IsSolid(x, y - 1))
                    {
                        steps[y * width + x] = 1;
                        queue.Enqueue(y * width + x);
                    }
                }
            }
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                rim.Add(at);
                if (steps[at] >= depth) continue;
                for (int way = 0; way < 4; way++)
                {
                    int next = way == 0 ? at + 1 : way == 1 ? at - 1 : way == 2 ? at + width : at - width;
                    int nx = next % width, ny = next / width;
                    if (steps[next] != 0 || !IsInside(map, nx, ny) || map[nx, ny] != TileType.Wall) continue;
                    steps[next] = (byte)(steps[at] + 1);
                    queue.Enqueue(next);
                }
            }
            rim.Sort();
            return rim;
        }

        // Veins as version 1 lays them (a tile and some of the rock round it), each started from a tile of the rim.
        static void PlaceRimVeins(TileMap map, GameRandom random, List<int> rim, TileType ore, int veins, Func<int, int, bool> allowed)
        {
            if (rim.Count == 0) return;
            for (int vein = 0; vein < veins; vein++)
            {
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    int at = rim[random.Next(rim.Count)];
                    int x = at % map.Width, y = at / map.Width;
                    if (map[x, y] != TileType.Wall || !allowed(x, y)) continue;
                    for (int vy = y - 1; vy <= y + 1; vy++)
                    {
                        for (int vx = x - 1; vx <= x + 1; vx++)
                        {
                            if (IsInside(map, vx, vy) && map[vx, vy] == TileType.Wall && allowed(vx, vy) && (vx == x && vy == y || random.Next(100) < VeinPercent))
                                map[vx, vy] = ore;
                        }
                    }
                    break;
                }
            }
        }

        // Copper within CopperSteps of the spawn, after the places are carved (the village takes the rock round the
        // spawn): where the veins left none, one is set in the nearest plain rock a few steps out of the plaza.
        static void EnsureCopper(GeneratedWorld world)
        {
            var map = world.Map;
            int width = map.Width;
            var steps = new Dictionary<int, int> { [world.SpawnY * width + world.SpawnX] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(world.SpawnY * width + world.SpawnX);
            (int X, int Y)? rock = null;
            // Far enough out to be past the village's plaza, where there is one.
            int from = Math.Min(14, CopperSteps / 2 * Math.Min(width, map.Height) / GameSize + 4);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                int here = steps[at];
                if (here >= CopperSteps && rock != null) break;
                int x = at % width, y = at / width;
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (!map.InBounds(nx, ny)) continue;
                    var type = map[nx, ny];
                    if (type == TileType.CopperOre && here < CopperSteps) return;
                    if (type == TileType.Wall && rock == null && here >= from && IsInside(map, nx, ny)) rock = (nx, ny);
                    if (map.IsSolid(nx, ny) || type == TileType.Lava || steps.ContainsKey(ny * width + nx)) continue;
                    steps[ny * width + nx] = here + 1;
                    queue.Enqueue(ny * width + nx);
                }
            }
            if (rock is not (int rx, int ry)) return;
            for (int y = ry - 1; y <= ry + 1; y++)
                for (int x = rx - 1; x <= rx + 1; x++)
                    if (IsInside(map, x, y) && map[x, y] == TileType.Wall) map[x, y] = TileType.CopperOre;
        }

        // Every room and arena is walked to from the spawn without wading through lava: one that is not (a way turned
        // aside, a river across its only door, a place of the story built over its way) gets a way two wide dug to the
        // nearest ground that is, through rock, ore, lava and water only: never through another arena's ring of rock,
        // nor through anything built. Run when the rooms are carved, and again after the places and the arenas'
        // dressing are laid over them, when pockets of open ground those left shut in the rock are opened too.
        static void JoinRooms(TileMap map, IReadOnlyList<RoomNode> rooms, IReadOnlyList<(int X, int Y)> arenas, int spawnX, int spawnY, bool pockets,
            HashSet<int> kept = null)
        {
            int width = map.Width, height = map.Height;
            int keep = ArenaRadius + ArenaWall;
            bool Held(int x, int y, int exempt)
            {
                for (int i = 0; i < arenas.Count; i++)
                    if (i != exempt && (x - arenas[i].X) * (x - arenas[i].X) + (y - arenas[i].Y) * (y - arenas[i].Y) <= keep * keep) return true;
                return false;
            }
            bool Dug(TileType type) => type == TileType.Wall || type == TileType.Water || type == TileType.Lava || type == TileType.CopperOre
                || type == TileType.IronOre || type == TileType.MithrilOre || type == TileType.ObsidianOre || type == TileType.VoidOre;
            var walked = new bool[width * height];
            var stack = new int[width * height];
            void Walk(int from)
            {
                if (!walked[from]) Flood(map, from, walked, stack, overLava: false);
            }
            // Digs from the tile through what can be dug to the nearest tile already come to, two wide.
            bool Dig(int start, int exempt, bool[] cameTo)
            {
                var from = new Dictionary<int, int> { [start] = -1 };
                var open = new Queue<int>();
                open.Enqueue(start);
                int found = -1;
                while (open.Count > 0 && found < 0)
                {
                    int at = open.Dequeue();
                    int x = at % width, y = at / width;
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        int next = ny * width + nx;
                        if (nx < Border || ny < Border || nx >= width - Border || ny >= height - Border || from.ContainsKey(next)) continue;
                        if (map.IsSolid(nx, ny) && !Dug(map[nx, ny]) || Held(nx, ny, exempt) || kept != null && kept.Contains(next)) continue;
                        from[next] = at;
                        if (cameTo[next])
                        {
                            found = next;
                            break;
                        }
                        open.Enqueue(next);
                    }
                }
                for (int at = found; at >= 0; at = from[at])
                {
                    for (int oy = 0; oy < 2; oy++)
                    {
                        for (int ox = 0; ox < 2; ox++)
                        {
                            int tx = at % width + ox, ty = at / width + oy;
                            if (tx < Border || ty < Border || tx >= width - Border || ty >= height - Border || Held(tx, ty, exempt)) continue;
                            if (kept != null && kept.Contains(ty * width + tx)) continue;
                            if (Dug(map[tx, ty])) map[tx, ty] = TileType.Floor;
                        }
                    }
                }
                return found >= 0;
            }
            Walk(spawnY * width + spawnX);
            foreach (var room in rooms)
            {
                // Its middle, or with a walled chunk there, the tile outside the chunk's door.
                int rx = room.Chunk != null ? room.AnchorX : room.X, ry = room.Chunk != null ? room.AnchorY : room.Y;
                int start = ry * width + rx;
                if (walked[start]) continue;
                // A room whose middle something was built on is that thing's now.
                if (map.IsSolid(rx, ry) && !Dug(map[rx, ry])) continue;
                int exempt = room.Kind == RoomKind.Arena ? arenas.ToList().IndexOf((room.X, room.Y)) : -1;
                if (Dig(start, exempt, walked)) Walk(start);
            }
            if (!pockets) return;

            // Then any open ground come to by no way at all, even through lava: a small place by a road whose path
            // ran corner to corner through the rock, a stall behind a town.
            // What lies behind a wall that is dug (a hidden room, a locked one) is come to in its own time.
            var reached = new bool[width * height];
            Flood(map, spawnY * width + spawnX, reached, stack, overLava: true, through: true);
            for (int at = 0; at < reached.Length; at++)
            {
                if (reached[at] || map.IsSolid(at % width, at / width)) continue;
                Dig(at, -1, reached);
                Flood(map, at, reached, stack, overLava: true, through: true);
            }
        }

        // Marks every open tile come to from the start by its sides (the start too), over lava or not. The map's
        // border is rock, so no step leaves it. stack: room for every tile.
        // through: the walls a pickaxe opens (cracked, obsidian, void stone) are walked through too.
        static void Flood(TileMap map, int start, bool[] seen, int[] stack, bool overLava, bool through = false)
        {
            int width = map.Width, count = 0;
            seen[start] = true;
            stack[count++] = start;
            while (count > 0)
            {
                int at = stack[--count];
                for (int way = 0; way < 4; way++)
                {
                    int next = way == 0 ? at + 1 : way == 1 ? at - 1 : way == 2 ? at + width : at - width;
                    if (seen[next]) continue;
                    int x = next % width, y = next / width;
                    if (map.IsSolid(x, y) && !(through && TileMap.IsRoomWall(map[x, y])) || !overLava && map[x, y] == TileType.Lava) continue;
                    seen[next] = true;
                    stack[count++] = next;
                }
            }
        }

        // A smooth value from -amp to amp: random values on a lattice every cell tiles, blended between them.
        static int Rough(ulong seed, int x, int y, int cell, int amp)
        {
            int cx = Math.DivRem(x, cell, out int fx);
            int cy = Math.DivRem(y, cell, out int fy);
            int v00 = RoughAt(seed, cx, cy), v10 = RoughAt(seed, cx + 1, cy), v01 = RoughAt(seed, cx, cy + 1), v11 = RoughAt(seed, cx + 1, cy + 1);
            int sum = v00 * (cell - fx) * (cell - fy) + v10 * fx * (cell - fy) + v01 * (cell - fx) * fy + v11 * fx * fy;
            // Blending pulls the values toward the middle; doubled and held to the range, the edge really moves by amp.
            return Clamp(sum / (cell * cell) * amp * 2 / 1000, -amp, amp);
        }

        // -1000 to 1000.
        static int RoughAt(ulong seed, int x, int y)
        {
            ulong h = seed ^ 0x9E3779B97F4A7C15UL;
            h ^= (ulong)(uint)x * 0xBF58476D1CE4E5B9UL;
            h ^= (ulong)(uint)y * 0x94D049BB133111EBUL;
            h ^= h >> 31;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 29;
            return (int)(h % 2001) - 1000;
        }

        sealed class RoomCarver
        {
            readonly TileMap _map;
            readonly BiomeMap _biomes;
            readonly TerrainData _terrain;
            readonly ulong _seed;
            readonly (int X, int Y)[] _arenas;
            readonly int _width, _height, _cell, _columns, _rows;
            // 0, or one more than the index of the arena whose wall the tile is kept for.
            readonly byte[] _hold;
            readonly List<RoomNode> _nodes = new List<RoomNode>();
            readonly List<TerrainRule> _rules = new List<TerrainRule>();
            readonly List<RoomEdge> _edges = new List<RoomEdge>();
            readonly List<int>[] _cells;

            public RoomCarver(TileMap map, BiomeMap biomes, TerrainData terrain, ulong seed, (int X, int Y)[] arenas)
            {
                _map = map;
                _biomes = biomes;
                _terrain = terrain;
                _seed = seed;
                _arenas = arenas;
                _width = map.Width;
                _height = map.Height;
                _cell = Math.Min(_width, _height) >= SmallMap ? RoomCell : RoomCell / 2;
                _columns = _width / _cell;
                _rows = _height / _cell;
                _hold = new byte[_width * _height];
                _cells = new List<int>[_columns * _rows];
                for (int i = 0; i < _cells.Length; i++) _cells[i] = new List<int>();
            }

            bool Inside(int x, int y) => x >= Border && y >= Border && x < _width - Border && y < _height - Border;

            int Add(int x, int y, int radius, RoomKind kind)
            {
                var rule = _terrain.Of(_biomes[x, y]);
                _nodes.Add(new RoomNode(_nodes.Count, x, y, radius, rule.Biome, rule.Style, kind));
                _rules.Add(rule);
                return _nodes.Count - 1;
            }

            static int Square(int dx, int dy) => dx * dx + dy * dy;

            public RoomGraph Carve(int spawnX, int spawnY)
            {
                // The arenas first: each a round room in a ring of rock nothing else may open.
                int keep = ArenaRadius + ArenaWall;
                for (int i = 0; i < _arenas.Length; i++)
                {
                    var (ax, ay) = _arenas[i];
                    for (int y = ay - keep; y <= ay + keep; y++)
                        for (int x = ax - keep; x <= ax + keep; x++)
                            if (_map.InBounds(x, y) && Square(x - ax, y - ay) <= keep * keep && _hold[y * _width + x] == 0) _hold[y * _width + x] = (byte)(i + 1);
                    ClearDisc(_map, ax, ay, ArenaRadius);
                }

                var random = new GameRandom(_seed, RoomStream);
                var taken = new bool[_cells.Length];
                int CellAt(int x, int y) => Clamp(y / _cell, 0, _rows - 1) * _columns + Clamp(x / _cell, 0, _columns - 1);
                int spawn = Add(spawnX, spawnY, Math.Max(SpawnClearing, 12 * _cell / RoomCell), RoomKind.Spawn);
                _cells[CellAt(spawnX, spawnY)].Add(spawn);
                taken[CellAt(spawnX, spawnY)] = true;
                var arenaNodes = new List<int>();
                foreach (var (ax, ay) in _arenas)
                {
                    arenaNodes.Add(Add(ax, ay, ArenaRadius, RoomKind.Arena));
                    taken[CellAt(ax, ay)] = true;
                }

                // A room in each cell (or two or four small ones), by the terrain of the biome at the cell's middle.
                for (int cy = 0; cy < _rows; cy++)
                {
                    for (int cx = 0; cx < _columns; cx++)
                    {
                        if (taken[cy * _columns + cx]) continue;
                        int left = cx * _cell, bottom = cy * _cell;
                        var rule = _terrain.Of(_biomes[left + _cell / 2, bottom + _cell / 2]);
                        bool turned = random.Next(2) == 0;
                        var parts = rule.Rooms == 1 ? new[] { (2, 2) }
                            : rule.Rooms == 2 ? (turned ? new[] { (1, 1), (3, 3) } : new[] { (1, 3), (3, 1) })
                            : new[] { (1, 1), (3, 1), (1, 3), (3, 3) };
                        int jitter = rule.Rooms == 1 ? _cell / 4 : _cell / 8;
                        foreach (var (px, py) in parts)
                        {
                            if (random.Next(100) >= rule.RoomFill) continue;
                            int radius = Math.Max(2, random.Next(rule.RoomMin, rule.RoomMax + 1) * _cell / (2 * RoomCell));
                            for (int attempt = 0; attempt < 3; attempt++)
                            {
                                int x = Clamp(left + _cell * px / 4 + random.Next(2 * jitter + 1) - jitter, Border + radius + 1, _width - Border - radius - 2);
                                int y = Clamp(bottom + _cell * py / 4 + random.Next(2 * jitter + 1) - jitter, Border + radius + 1, _height - Border - radius - 2);
                                if (!Inside(x, y) || _hold[y * _width + x] != 0) continue;
                                _cells[cy * _columns + cx].Add(Add(x, y, radius, RoomKind.Room));
                                break;
                            }
                        }
                    }
                }

                // The ways there could be: between the rooms of a cell, and from each cell to the next one right and up
                // (now and then on a diagonal), the nearest rooms of the two.
                var candidates = new List<(int A, int B)>();
                void Link(List<int> from, List<int> to, int count)
                {
                    var pairs = new List<(int Distance, int A, int B)>();
                    foreach (int a in from)
                        foreach (int b in to)
                            if (a != b) pairs.Add((Square(_nodes[a].X - _nodes[b].X, _nodes[a].Y - _nodes[b].Y), a, b));
                    pairs.Sort();
                    foreach (var (_, a, b) in pairs)
                    {
                        if (count <= 0) break;
                        if (!Clear(_nodes[a].X, _nodes[a].Y, _nodes[b].X, _nodes[b].Y, -1)) continue;
                        candidates.Add((a, b));
                        count--;
                    }
                }
                for (int cy = 0; cy < _rows; cy++)
                {
                    for (int cx = 0; cx < _columns; cx++)
                    {
                        var here = _cells[cy * _columns + cx];
                        // Within the cell: each room to those beside it.
                        for (int i = 0; i < here.Count; i++)
                            for (int j = i + 1; j < here.Count; j++)
                            {
                                var (a, b) = (_nodes[here[i]], _nodes[here[j]]);
                                if (here.Count > 2 && Square(a.X - b.X, a.Y - b.Y) > _cell * _cell * 121 / 256) continue;
                                if (Clear(a.X, a.Y, b.X, b.Y, -1)) candidates.Add((here[i], here[j]));
                            }
                        int links = here.Count > 2 ? 2 : 1;
                        if (cx + 1 < _columns) Link(here, _cells[cy * _columns + cx + 1], Math.Max(links, _cells[cy * _columns + cx + 1].Count > 2 ? 2 : 1));
                        if (cy + 1 < _rows) Link(here, _cells[(cy + 1) * _columns + cx], Math.Max(links, _cells[(cy + 1) * _columns + cx].Count > 2 ? 2 : 1));
                        if (cx + 1 < _columns && cy + 1 < _rows && random.Next(100) < 12) Link(here, _cells[(cy + 1) * _columns + cx + 1], 1);
                        if (cx + 1 < _columns && cy > 0 && random.Next(100) < 12) Link(here, _cells[(cy - 1) * _columns + cx + 1], 1);
                    }
                }

                // A spanning tree from the spawn's room (Prim's, the next way picked at random), so every room is
                // reached; rooms the cells' ways do not reach are joined to the nearest reached room.
                var ways = new List<int>[_nodes.Count];
                for (int i = 0; i < ways.Length; i++) ways[i] = new List<int>();
                for (int i = 0; i < candidates.Count; i++)
                {
                    ways[candidates[i].A].Add(i);
                    ways[candidates[i].B].Add(i);
                }
                var reached = new bool[_nodes.Count];
                var used = new List<bool>(new bool[candidates.Count]);
                var frontier = new List<int>();
                void Open(int candidate, bool loop)
                {
                    var (a, b) = candidates[candidate];
                    var rule = _rules[a];
                    used[candidate] = true;
                    _nodes[a].Ways.Add(_edges.Count);
                    _nodes[b].Ways.Add(_edges.Count);
                    _edges.Add(new RoomEdge(a, b, random.Next(rule.CorridorMin, rule.CorridorMax + 1), loop));
                }
                void Reach(int node)
                {
                    reached[node] = true;
                    frontier.AddRange(ways[node]);
                }
                Reach(spawn);
                while (true)
                {
                    while (frontier.Count > 0)
                    {
                        int pick = random.Next(frontier.Count);
                        int candidate = frontier[pick];
                        frontier[pick] = frontier[frontier.Count - 1];
                        frontier.RemoveAt(frontier.Count - 1);
                        var (a, b) = candidates[candidate];
                        if (reached[a] && reached[b]) continue;
                        Open(candidate, false);
                        Reach(reached[a] ? b : a);
                    }
                    // The nearest pair with a clear line between them; with none (an arena's rock in the way of every
                    // line), the nearest pair, whose way Join digs round the arena.
                    (int Distance, int From, int To) nearest = (int.MaxValue, -1, -1), blocked = (int.MaxValue, -1, -1);
                    for (int u = 0; u < _nodes.Count; u++)
                    {
                        if (reached[u] || _nodes[u].Kind == RoomKind.Arena) continue;
                        for (int v = 0; v < _nodes.Count; v++)
                        {
                            if (!reached[v] || _nodes[v].Kind == RoomKind.Arena) continue;
                            int distance = Square(_nodes[u].X - _nodes[v].X, _nodes[u].Y - _nodes[v].Y);
                            if (distance < blocked.Distance) blocked = (distance, v, u);
                            if (distance < nearest.Distance && Clear(_nodes[u].X, _nodes[u].Y, _nodes[v].X, _nodes[v].Y, -1)) nearest = (distance, v, u);
                        }
                    }
                    if (nearest.To < 0) nearest = blocked;
                    if (nearest.To < 0) break;
                    candidates.Add((nearest.From, nearest.To));
                    used.Add(false);
                    Open(candidates.Count - 1, false);
                    Reach(nearest.To);
                }

                // Loops: some of the ways the tree left out are opened again, and most rooms left with one way in get
                // a second.
                int tree = candidates.Count;
                for (int i = 0; i < tree; i++)
                {
                    if (!used[i] && random.Next(100) < _rules[candidates[i].A].LoopPercent) Open(i, true);
                }
                for (int node = 0; node < _nodes.Count; node++)
                {
                    if (_nodes[node].Kind != RoomKind.Room || _nodes[node].Ways.Count != 1) continue;
                    if (random.Next(100) < _rules[node].DeadEndPercent) continue;
                    int spare = ways[node].FindIndex(i => !used[i]);
                    if (spare >= 0) Open(ways[node][spare], true);
                }

                // Each arena's one way in: from the nearest room, of those nearer the spawn than it where there is one.
                for (int i = 0; i < arenaNodes.Count; i++)
                {
                    var arena = _nodes[arenaNodes[i]];
                    int home = Square(arena.X - spawnX, arena.Y - spawnY);
                    int best = -1;
                    long bestScore = long.MaxValue;
                    foreach (var node in _nodes)
                    {
                        if (node.Kind == RoomKind.Arena || !reached[node.Index]) continue;
                        long score = Square(node.X - arena.X, node.Y - arena.Y);
                        if (Square(node.X - spawnX, node.Y - spawnY) > home) score *= 4;
                        if (!Clear(node.X, node.Y, arena.X, arena.Y, i)) score += (long)_width * _width * 8;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = node.Index;
                        }
                    }
                    if (best < 0) continue;
                    _nodes[best].Ways.Add(_edges.Count);
                    arena.Ways.Add(_edges.Count);
                    _edges.Add(new RoomEdge(best, arena.Index, 2 * TunnelHalfWidth + 1, false));
                }

                // Carved in this order: the rooms; the lava rivers across them; the ways, which bridge whatever
                // lava or water they cross; then what stands in the rooms.
                var shape = new GameRandom(_seed, ShapeStream);
                var shapes = new List<Action>();
                foreach (var node in _nodes)
                {
                    if (node.Kind == RoomKind.Arena) continue;
                    var floor = Room(node, node.Kind == RoomKind.Spawn ? TerrainStyle.Cave : node.Style, shape);
                    if (floor != null) shapes.Add(floor);
                }
                foreach (var floor in shapes) floor();
                Rivers(new GameRandom(_seed, RiverStream));
                var way = new GameRandom(_seed, WayStream);
                foreach (var edge in _edges) Way(edge, way);
                Features(new GameRandom(_seed, FeatureStream));
                Join(spawnX, spawnY);
                return new RoomGraph(_cell, _nodes, _edges);
            }

            // Whether a straight way between the two points keeps out of every arena's rock (but the exempt one's).
            bool Clear(int ax, int ay, int bx, int by, int exempt)
            {
                foreach (var (x, y) in Line(ax, ay, bx, by))
                {
                    foreach (var (dx, dy) in new[] { (0, 0), (3, 0), (-3, 0), (0, 3), (0, -3) })
                    {
                        if (!_map.InBounds(x + dx, y + dy)) continue;
                        int held = _hold[(y + dy) * _width + x + dx];
                        if (held != 0 && held != exempt + 1) return false;
                    }
                }
                return true;
            }

            void Floor(int x, int y)
            {
                if (Inside(x, y) && _hold[y * _width + x] == 0) _map[x, y] = TileType.Floor;
            }

            // Carves what must be carved before any floor (an island's water) and gives back the carving of the floor.
            Action Room(RoomNode node, TerrainStyle style, GameRandom random)
            {
                int x = node.X, y = node.Y, r = node.Radius;
                ulong noise = _seed + (ulong)node.Index * 7919UL;
                switch (style)
                {
                    case TerrainStyle.Maze:
                    {
                        int a = Math.Max(2, r + random.Next(3) - 1), b = Math.Max(2, r + random.Next(3) - 1);
                        return () => Box(x, y, a, b, 1 + r / 4);
                    }
                    case TerrainStyle.Hall:
                    {
                        // A long hall lying any way: a row of round hollows run together, each its own size, the row bent
                        // a little, so no two halls have the same outline (2026-11-21: boxes read as a grid of rectangles).
                        int way = random.Next(Ways.Length);
                        int bend = (way + random.Next(5) - 2 + Ways.Length) % Ways.Length;
                        int reach = r * random.Next(60, 101) / 100;
                        var patches = new List<(int X, int Y, int R)>
                        {
                            (x, y, r * random.Next(85, 106) / 100),
                            (x + Ways[way].X * reach / 1000, y + Ways[way].Y * reach / 1000, Math.Max(3, r * random.Next(60, 91) / 100)),
                            (x - Ways[bend].X * reach / 1000, y - Ways[bend].Y * reach / 1000, Math.Max(3, r * random.Next(60, 91) / 100)),
                        };
                        if (random.Next(2) == 0)
                        {
                            var (sx, sy) = Ways[random.Next(Ways.Length)];
                            patches.Add((x + sx * r * 6 / 10000, y + sy * r * 6 / 10000, Math.Max(3, r * random.Next(30, 51) / 100)));
                        }
                        return () =>
                        {
                            foreach (var (px, py, pr) in patches) Blob(px, py, pr, Math.Max(1, pr / 4), noise, TileType.Floor);
                        };
                    }
                    case TerrainStyle.Fissure:
                    {
                        // One in three is a great hollow; the rest are cracks, straight and narrow, widened at the middle.
                        if (random.Next(100) < 34) return () => Blob(x, y, r, Math.Max(1, r / 5), noise, TileType.Floor);
                        var (dx, dy) = Ways[random.Next(Ways.Length)];
                        int half = r;
                        return () =>
                        {
                            for (int t = -half; t <= half; t++)
                            {
                                int px = x + dx * t / 1000, py = y + dy * t / 1000;
                                Floor(px, py);
                                Floor(px + 1, py);
                                Floor(px, py + 1);
                                Floor(px + 1, py + 1);
                            }
                            Blob(x, y, Math.Max(2, r / 4), 1, noise, TileType.Floor);
                        };
                    }
                    case TerrainStyle.Islands:
                    {
                        // The water round it now, so no island's water drowns another's ground.
                        Blob(x, y, r + Math.Max(3, _cell / 6), Math.Max(1, r / 5), noise + 1, TileType.Water);
                        return () => Blob(x, y, r, Math.Max(1, r / 5), noise, TileType.Floor);
                    }
                    case TerrainStyle.Shards:
                    {
                        // A broken slab: five to seven straight sides at uneven angles, each its own distance from the
                        // middle and chipped along its length, with a splinter or two lying off it (2026-11-21: slabs of
                        // four even sides read as a grid of diamonds).
                        var sides = new List<(int X, int Y, int D)>();
                        int count = 5 + random.Next(3), turn = random.Next(Ways.Length);
                        for (int i = 0; i < count; i++)
                        {
                            var (nx, ny) = Ways[(turn + i * Ways.Length / count + random.Next(3) - 1 + Ways.Length) % Ways.Length];
                            sides.Add((nx, ny, r * random.Next(60, 111) / 100));
                        }
                        var splinters = new List<(int X, int Y, int R)>();
                        for (int i = random.Next(3); i > 0; i--)
                        {
                            var (sx, sy) = Ways[random.Next(Ways.Length)];
                            splinters.Add((x + sx * r * random.Next(70, 111) / 100000, y + sy * r * random.Next(70, 111) / 100000, Math.Max(2, r * random.Next(20, 41) / 100)));
                        }
                        void Slab(int cx, int cy, int scale, int lean)
                        {
                            int reach = r * scale / 100 + 2;
                            for (int dy = -reach; dy <= reach; dy++)
                            {
                                for (int dx = -reach; dx <= reach; dx++)
                                {
                                    int px = cx + dx, py = cy + dy;
                                    if (px < 0 || py < 0) continue;
                                    int chip = Rough(noise, px, py, 4, 1);
                                    bool inside = true;
                                    for (int i = 0; i < sides.Count && inside; i++)
                                    {
                                        var side = sides[(i + lean) % sides.Count];
                                        inside = dx * side.X + dy * side.Y <= (side.D * scale / 100 + chip) * 1000;
                                    }
                                    if (inside) Floor(px, py);
                                }
                            }
                        }
                        return () =>
                        {
                            Slab(x, y, 100, 0);
                            for (int i = 0; i < splinters.Count; i++) Slab(splinters[i].X, splinters[i].Y, 100 * splinters[i].R / r, i + 1);
                        };
                    }
                    case TerrainStyle.Dunes:
                        return Lumps(x, y, r, 3, Math.Max(1, r / 3), noise, random);
                    case TerrainStyle.Rivers:
                        return Lumps(x, y, r, 1, Math.Max(1, r / 5), noise, random);
                    default:
                        return Lumps(x, y, r, 2, Math.Max(1, r / 3), noise, random);
                }
            }

            // A cave room: a round patch with a few smaller ones grown out of its sides, so no two are the same shape.
            Action Lumps(int x, int y, int r, int more, int rough, ulong noise, GameRandom random)
            {
                var patches = new List<(int X, int Y, int R)> { (x, y, r) };
                for (int i = 0; i < more; i++)
                {
                    var (dx, dy) = Ways[random.Next(Ways.Length)];
                    int reach = r * random.Next(45, 86) / 100;
                    patches.Add((x + dx * reach / 1000, y + dy * reach / 1000, Math.Max(2, r * random.Next(45, 76) / 100)));
                }
                return () =>
                {
                    foreach (var (px, py, pr) in patches) Blob(px, py, pr, Math.Max(1, rough * pr / r), noise, TileType.Floor);
                };
            }

            // A round patch whose edge wanders in and out by up to rough tiles. Water only takes rock.
            void Blob(int cx, int cy, int r, int rough, ulong noise, TileType type)
            {
                for (int dy = -r - rough; dy <= r + rough; dy++)
                {
                    for (int dx = -r - rough; dx <= r + rough; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0) continue;
                        int reach = r + Rough(noise, x, y, 5, rough);
                        if (dx * dx + dy * dy > reach * reach) continue;
                        if (type == TileType.Floor) Floor(x, y);
                        else if (Inside(x, y) && _hold[y * _width + x] == 0 && _map[x, y] == TileType.Wall) _map[x, y] = type;
                    }
                }
            }

            // A room with straight sides, a wide and b high either way, its corners cut off.
            void Box(int cx, int cy, int a, int b, int cut)
            {
                for (int dy = -b; dy <= b; dy++)
                    for (int dx = -a; dx <= a; dx++)
                        if (a - Math.Abs(dx) + b - Math.Abs(dy) >= cut) Floor(cx + dx, cy + dy);
            }

            // Rivers of lava through the biomes whose style has them: each from a room, wandering on across rooms and
            // rock alike, three or four tiles wide.
            void Rivers(GameRandom random)
            {
                var rooms = _nodes.Where(n => n.Kind == RoomKind.Room && n.Style == TerrainStyle.Rivers).ToList();
                int rivers = rooms.Count * RiversPerHundredRooms / 100;
                for (int river = 0; river < rivers; river++)
                {
                    var start = rooms[random.Next(rooms.Count)];
                    var biome = start.Biome;
                    int way = random.Next(Ways.Length);
                    int length = random.Next(110, 220) * _cell / RoomCell;
                    int wide = 4 + random.Next(2);
                    // Thousandths of a tile, starting back along the river so it runs through the room.
                    int px = start.X * 1000 - Ways[way].X * length / 2, py = start.Y * 1000 - Ways[way].Y * length / 2;
                    for (int step = 0; step < length; step++)
                    {
                        if (step % 14 == 13) way = (way + random.Next(3) - 1 + Ways.Length) % Ways.Length;
                        px += Ways[way].X;
                        py += Ways[way].Y;
                        int x = px / 1000, y = py / 1000;
                        for (int oy = -(wide / 2); oy < wide - wide / 2; oy++)
                        {
                            for (int ox = -(wide / 2); ox < wide - wide / 2; ox++)
                            {
                                int tx = x + ox, ty = y + oy;
                                if (!Inside(tx, ty) || _hold[ty * _width + tx] != 0 || _biomes[tx, ty] != biome) continue;
                                if (_map[tx, ty] == TileType.Floor || _map[tx, ty] == TileType.Wall) _map[tx, ty] = TileType.Lava;
                            }
                        }
                    }
                }
            }

            const int RiversPerHundredRooms = 13;

            // The tiles of a way from one point to another, each a step sideways or up from the last.
            static List<(int X, int Y)> Steps(int ax, int ay, int bx, int by)
            {
                var steps = new List<(int X, int Y)>();
                int lastX = ax, lastY = ay;
                foreach (var (x, y) in Line(ax, ay, bx, by))
                {
                    if (x != lastX && y != lastY) steps.Add((x, lastY));
                    steps.Add((x, y));
                    (lastX, lastY) = (x, y);
                }
                return steps;
            }

            void Way(RoomEdge edge, GameRandom random)
            {
                var a = _nodes[edge.A];
                var b = _nodes[edge.B];
                int exempt = b.Kind == RoomKind.Arena ? Array.IndexOf(_arenas, (b.X, b.Y)) : -1;
                // The way into an arena runs straight.
                var style = b.Kind == RoomKind.Arena ? TerrainStyle.Fissure : a.Style;
                List<(int X, int Y)> path;
                switch (style)
                {
                    case TerrainStyle.Maze:
                    {
                        // Along one side, then the other.
                        bool acrossFirst = random.Next(2) == 0;
                        var (mx, my) = acrossFirst ? (b.X, a.Y) : (a.X, b.Y);
                        path = Steps(a.X, a.Y, mx, my);
                        path.AddRange(Steps(mx, my, b.X, b.Y));
                        break;
                    }
                    case TerrainStyle.Cave:
                    case TerrainStyle.Dunes:
                    case TerrainStyle.Rivers:
                    case TerrainStyle.Hall:
                    {
                        // Bent once, the bend pushed off the straight line by up to a quarter of its length.
                        int dx = b.X - a.X, dy = b.Y - a.Y;
                        int push = random.Next(-250, 251);
                        int mx = Clamp((a.X + b.X) / 2 - dy * push / 1000, Border + 4, _width - Border - 5);
                        int my = Clamp((a.Y + b.Y) / 2 + dx * push / 1000, Border + 4, _height - Border - 5);
                        path = Steps(a.X, a.Y, mx, my);
                        path.AddRange(Steps(mx, my, b.X, b.Y));
                        break;
                    }
                    default:
                        path = Steps(a.X, a.Y, b.X, b.Y);
                        break;
                }
                // A bent way that would break into an arena's rock goes straight instead.
                if (path.Any(p => Held(p.X, p.Y, edge.Width, exempt))) path = Steps(a.X, a.Y, b.X, b.Y);
                // The ways of the round caves wander a little as they go; a narrow one stays where it is, to stay whole.
                bool wanders = edge.Width >= 3 && b.Kind != RoomKind.Arena
                    && (style == TerrainStyle.Cave || style == TerrainStyle.Dunes || style == TerrainStyle.Rivers || style == TerrainStyle.Hall);
                ulong noise = _seed + 104729UL * (ulong)(edge.A + 1);
                var carved = new List<(int X, int Y)>(path.Count);
                foreach (var (x, y) in path)
                {
                    var at = !wanders || Held(x, y, edge.Width + 4, exempt) ? (x, y) : (x + Rough(noise, x, y, 6, 2), y + Rough(noise + 1, x, y, 6, 2));
                    Brush(at.Item1, at.Item2, edge.Width, exempt);
                    carved.Add(at);
                }
                edge.Path = carved;
            }

            bool Held(int x, int y, int width, int exempt)
            {
                for (int oy = -(width / 2); oy < width - width / 2; oy++)
                {
                    for (int ox = -(width / 2); ox < width - width / 2; ox++)
                    {
                        if (!_map.InBounds(x + ox, y + oy)) continue;
                        int held = _hold[(y + oy) * _width + x + ox];
                        if (held != 0 && held != exempt + 1) return true;
                    }
                }
                return false;
            }

            // Opens the square of that width at the tile: rock, and lava and water too (a bridge, a causeway).
            void Brush(int x, int y, int width, int exempt)
            {
                for (int oy = -(width / 2); oy < width - width / 2; oy++)
                {
                    for (int ox = -(width / 2); ox < width - width / 2; ox++)
                    {
                        int tx = x + ox, ty = y + oy;
                        if (!Inside(tx, ty)) continue;
                        int held = _hold[ty * _width + tx];
                        if (held != 0 && held != exempt + 1) continue;
                        var type = _map[tx, ty];
                        if (type == TileType.Wall || type == TileType.Water || type == TileType.Lava) _map[tx, ty] = TileType.Floor;
                    }
                }
            }

            // What stands in the rooms (terrain.csv feature): pillars of rock three by three, anything else one tile, each
            // with open floor all round it, so nothing is shut off.
            void Features(GameRandom random)
            {
                foreach (var node in _nodes)
                {
                    var rule = _rules[node.Index];
                    if (node.Kind != RoomKind.Room || rule.Feature is not TileType feature || rule.FeaturePercent <= 0) continue;
                    int size = feature == TileType.Wall ? 3 : 1;
                    int r = node.Radius;
                    int count = Math.Max(1, 3 * r * r * rule.FeaturePercent / 100 / (size * size));
                    for (int n = 0; n < count; n++)
                    {
                        for (int attempt = 0; attempt < 6; attempt++)
                        {
                            int x = node.X + random.Next(2 * r + 1) - r, y = node.Y + random.Next(2 * r + 1) - r;
                            if (Square(x - node.X, y - node.Y) < 9) continue;
                            bool open = true;
                            for (int oy = -1; oy <= size && open; oy++)
                                for (int ox = -1; ox <= size && open; ox++)
                                    open = Inside(x + ox, y + oy) && _map[x + ox, y + oy] == TileType.Floor;
                            if (!open) continue;
                            // A pillar of rock is three by three with a corner or two knocked off.
                            for (int oy = 0; oy < size; oy++)
                                for (int ox = 0; ox < size; ox++)
                                    if (size == 1 || ox == 1 || oy == 1 || random.Next(2) == 0) _map[x + ox, y + oy] = feature;
                            break;
                        }
                    }
                }
            }

            // Every room is walked to from the spawn without wading through lava (JoinRooms). Then floor nothing reaches
            // at all, even through lava, is rock again.
            void Join(int spawnX, int spawnY)
            {
                JoinRooms(_map, _nodes, _arenas, spawnX, spawnY, false);
                var reached = new bool[_width * _height];
                Flood(_map, spawnY * _width + spawnX, reached, new int[_width * _height], overLava: true);
                for (int y = 0; y < _height; y++)
                    for (int x = 0; x < _width; x++)
                        if (!reached[y * _width + x] && !_map.IsSolid(x, y)) _map[x, y] = TileType.Wall;
            }
        }
    }
}
