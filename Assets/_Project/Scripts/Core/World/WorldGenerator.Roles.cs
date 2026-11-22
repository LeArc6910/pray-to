using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // ---- What the rooms of version 2 are for (2026-11-21, Docs/WorldRework.md 방의 역할, 방 조각 시트). Run once the
    // rooms, the places and roads of the story and the arenas' dressing are all laid, so it knows which rooms a road
    // runs through and which a town was built over. In order: patches of ice and quicksand on the rooms' floors
    // (terrain.csv ground); a role for every room, by the roles' numbers and spacing (RoomSheet.Defaults,
    // room_roles.csv); a chunk of rooms.csv set into each room whose role has one that fits, turned any of eight ways;
    // veins in the ore rooms; and cracked walls across some of the loops' ways. What needs a later system (packs,
    // leaders, chests, shrines, traps) is only marked here (RoomGraph.Marks) and dressed, so the room already reads
    // as its own place. Integer math and GameRandom only: the host and a client furnish the same rooms. ----
    public static partial class WorldGenerator
    {
        const ulong RoleStream = 45;
        const ulong ChunkStream = 46;
        const ulong PatchStream = 47;
        const ulong PlugStream = 48;
        const ulong WaysideStream = 49;
        // What the rooms by a road are, in turn, about every LandmarkEvery tiles of it (in place of version 1's small
        // places by the roads, CarveLandmarks).
        static readonly RoomRole[] WaysideRoles = { RoomRole.Rest, RoomRole.Shrine, RoomRole.Cart };
        // A walk this wide is cleared round a walled chunk, so its doors open on floor and the room's ways meet round it.
        const int ChunkWalk = 2;
        // Nothing but plain rooms within this many rooms of the spawn's; no leaders or traps within NoDangerDepth.
        const int PlainDepth = 1;
        const int NoDangerDepth = 3;
        // From any room, a room that is neither plain nor a pack's is within this many rooms.
        public const int SpecialWithin = 3;
        // Of the loops' ways long enough, how many in a hundred are shut by a cracked wall.
        const int CrackedPercent = 14;
        const int CrackedLength = 14;

        // The order the roles are dealt in: the scarce and the picky first.
        static readonly RoomRole[] RoleOrder =
        {
            RoomRole.Gate, RoomRole.Elite, RoomRole.Treasure, RoomRole.Locked, RoomRole.Hidden, RoomRole.Trap, RoomRole.Rest, RoomRole.Cart, RoomRole.Shrine,
            RoomRole.Pond, RoomRole.Ore,
        };

        // What the generator itself laid as terrain: a chunk may be set over it. Anything else (a road, a town's
        // houses, an arena's floor) is left alone.
        static bool Natural(TileType type) => type == TileType.Wall || type == TileType.Floor || type == TileType.Water || type == TileType.Lava
            || type == TileType.CopperOre || type == TileType.IronOre || type == TileType.MithrilOre || type == TileType.ObsidianOre || type == TileType.VoidOre
            || type == TileType.MushroomTree || type == TileType.IceFloor || type == TileType.Quicksand;

        static void Furnish(GeneratedWorld world, ulong seed, TerrainData terrain)
        {
            var graph = world.Rooms;
            if (graph == null) return;
            new Furnisher(world, seed, terrain).Run();
        }

        sealed class Furnisher
        {
            readonly GeneratedWorld _world;
            readonly TileMap _map;
            readonly RoomGraph _graph;
            readonly TerrainData _terrain;
            readonly RoomData _rooms;
            readonly ulong _seed;
            readonly int _width, _height, _scale;
            readonly bool[] _built;
            readonly bool[] _dealt;
            // Tiles a chunk (and the walk round it) took: no other chunk goes there.
            readonly HashSet<int> _taken = new HashSet<int>();
            // The rooms' middles (y * width + x): no chunk is set over another room's.
            readonly Dictionary<int, int> _middles = new Dictionary<int, int>();
            readonly GameRandom _chunkRandom;

            public Furnisher(GeneratedWorld world, ulong seed, TerrainData terrain)
            {
                _world = world;
                _map = world.Map;
                _graph = world.Rooms;
                _terrain = terrain;
                _rooms = terrain.Rooms;
                _seed = seed;
                _width = _map.Width;
                _height = _map.Height;
                _scale = ScaleOf(_width, _height);
                _built = new bool[_graph.Nodes.Count];
                _dealt = new bool[_graph.Nodes.Count];
                _chunkRandom = new GameRandom(seed, ChunkStream);
                foreach (var node in _graph.Nodes) _middles[node.Y * _width + node.X] = node.Index;
            }

            bool Inside(int x, int y) => x >= Border && y >= Border && x < _width - Border && y < _height - Border;

            RoomNode Other(RoomEdge edge, RoomNode node) => _graph.Nodes[edge.A == node.Index ? edge.B : edge.A];

            IEnumerable<RoomNode> Neighbours(RoomNode node) => node.Edges.Select(e => Other(_graph.Edges[e], node));

            public void Run()
            {
                Survey();
                Patches(new GameRandom(_seed, PatchStream));
                Wayside(new GameRandom(_seed, WaysideStream));
                Deal(new GameRandom(_seed, RoleStream));
                Plug(new GameRandom(_seed, PlugStream));
            }

            // Which rooms a road runs through, which something was built over, and how many rooms each is from the spawn's.
            // A by-way (places.csv links) is no road: the rooms it runs through keep their packs, its narrow path does not.
            void Survey()
            {
                var byWays = new HashSet<int>();
                foreach (var route in _world.Roads.Where(r => r.Kind == RoadKind.Link))
                    foreach (var (x, y) in route.Tiles)
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                byWays.Add((y + oy) * _width + x + ox);
                // Where a by-way runs along a road, it is the road.
                foreach (var route in _world.Roads.Where(r => r.Kind != RoadKind.Link))
                    foreach (var (x, y) in route.Tiles)
                        for (int oy = -2; oy <= 2; oy++)
                            for (int ox = -2; ox <= 2; ox++)
                                byWays.Remove((y + oy) * _width + x + ox);
                foreach (var node in _graph.Nodes)
                {
                    node.AnchorX = node.X;
                    node.AnchorY = node.Y;
                    node.Depth = -1;
                    int reach = Math.Max(3, node.Radius);
                    bool nearPlace = _world.Places.Any(p => Square(p.X - node.X, p.Y - node.Y) <= (p.Radius + reach + 2) * (p.Radius + reach + 2));
                    bool nearArena = node.Kind == RoomKind.Room
                        && _world.Arenas.Any(a => Square(TileMap.Cell(a.X) - node.X, TileMap.Cell(a.Y) - node.Y) <= (ArenaRadius + ArenaWall + 2) * (ArenaRadius + ArenaWall + 2));
                    bool built = nearPlace || nearArena, road = false;
                    for (int y = node.Y - reach; y <= node.Y + reach; y++)
                    {
                        for (int x = node.X - reach; x <= node.X + reach; x++)
                        {
                            if (!_map.InBounds(x, y) || Square(x - node.X, y - node.Y) > reach * reach) continue;
                            var type = _map[x, y];
                            if ((Structures.IsWay(type) || type == TileType.Waypoint) && !byWays.Contains(y * _width + x)) road = true;
                            // A road's lamps, signposts and rest spot stand by it in the rooms it runs through
                            // (1c): they make the room a road's, not a town's.
                            else if (!Natural(type) && type != TileType.LampPost && type != TileType.Signpost && type != TileType.Campfire && type != TileType.Bench) built = true;
                        }
                    }
                    node.Roadside = road;
                    _built[node.Index] = built || node.Kind != RoomKind.Room;
                }
                var spawn = _graph.Nodes.First(n => n.Kind == RoomKind.Spawn);
                spawn.Depth = 0;
                var open = new Queue<RoomNode>();
                open.Enqueue(spawn);
                while (open.Count > 0)
                {
                    var node = open.Dequeue();
                    foreach (var next in Neighbours(node))
                    {
                        if (next.Depth >= 0) continue;
                        next.Depth = node.Depth + 1;
                        open.Enqueue(next);
                    }
                }
            }

            static int Square(int dx, int dy) => dx * dx + dy * dy;

            // Patches of the biome's own floor (ice, quicksand) over the rooms' bare floor, each a rough round sheet.
            void Patches(GameRandom random)
            {
                foreach (var node in _graph.Nodes)
                {
                    if (node.Kind != RoomKind.Room) continue;
                    var rule = _terrain.Of(node.Biome);
                    if (rule.Ground is not TileType ground || rule.GroundPercent <= 0) continue;
                    int r = Math.Max(3, node.Radius);
                    int wanted = 3 * r * r * rule.GroundPercent / 100, laid = 0;
                    ulong noise = _seed + 31337UL * (ulong)(node.Index + 1);
                    for (int patch = 0; patch < 10 && laid < wanted; patch++)
                    {
                        int size = Math.Max(2, random.Next(r / 4, r / 2 + 1));
                        int cx = node.X + random.Next(2 * r + 1) - r, cy = node.Y + random.Next(2 * r + 1) - r;
                        for (int dy = -size - 2; dy <= size + 2; dy++)
                        {
                            for (int dx = -size - 2; dx <= size + 2; dx++)
                            {
                                int x = cx + dx, y = cy + dy;
                                if (x < 0 || y < 0 || !Inside(x, y) || _map[x, y] != TileType.Floor) continue;
                                int reach = size + Rough(noise, x, y, 4, 2);
                                if (dx * dx + dy * dy > reach * reach) continue;
                                _map[x, y] = ground;
                                laid++;
                            }
                        }
                    }
                }
            }

            // The small places along the roads (in place of version 1's, CarveLandmarks): about every LandmarkEvery tiles
            // of each road, a room it runs through, or else one beside it, becomes a camp, a shrine or a cart in turn.
            void Wayside(GameRandom random)
            {
                int every = Math.Max(40, LandmarkEvery * Math.Min(_width, _height) / GameSize);
                int turn = random.Next(WaysideRoles.Length);
                foreach (var route in _world.Roads.Where(r => r.Kind == RoadKind.Road))
                {
                    var rooms = route.Rooms.Select(i => _graph.Nodes[i]).ToList();
                    var along = new List<int>();
                    int walked = 0;
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        if (i > 0) walked += (int)Math.Sqrt(Square(rooms[i].X - rooms[i - 1].X, rooms[i].Y - rooms[i - 1].Y));
                        along.Add(walked);
                    }
                    for (int mark = every / 2; mark < walked - every / 3; mark += every)
                    {
                        // The room of the road nearest the mark, then those beside it off the road, nearest first.
                        int at = 0;
                        for (int i = 1; i < rooms.Count; i++)
                            if (Math.Abs(along[i] - mark) < Math.Abs(along[at] - mark)) at = i;
                        var spots = new[] { rooms[at] }
                            .Concat(Neighbours(rooms[at]).Where(n => !n.Roadside).OrderBy(n => Square(n.X - rooms[at].X, n.Y - rooms[at].Y)).ThenBy(n => n.Index))
                            .Concat(new[] { at - 1, at + 1 }.Where(i => i >= 0 && i < rooms.Count).Select(i => rooms[i]))
                            .Where(n => n.Kind == RoomKind.Room && !_dealt[n.Index]).Distinct().ToList();
                        bool placed = false;
                        foreach (var spot in spots)
                        {
                            for (int k = 0; k < WaysideRoles.Length && !placed; k++)
                            {
                                var role = WaysideRoles[(turn + k) % WaysideRoles.Length];
                                if (!Allowed(spot, role) || Near(spot, _rooms.Rule(spot.Biome, role).MinGap, n => n.Role == role && _dealt[n.Index])) continue;
                                if (Take(spot, role)) placed = true;
                            }
                            if (placed) break;
                        }
                        if (placed) turn++;
                    }
                }
            }

            // Whether a room of the role lies within gap rooms of the node (the node itself not counted).
            bool Near(RoomNode from, int gap, Func<RoomNode, bool> wanted)
            {
                if (gap <= 0) return false;
                var seen = new HashSet<int> { from.Index };
                var open = new Queue<(RoomNode Node, int Steps)>();
                open.Enqueue((from, 0));
                while (open.Count > 0)
                {
                    var (node, steps) = open.Dequeue();
                    if (steps > 0 && wanted(node)) return true;
                    if (steps >= gap) continue;
                    foreach (var next in Neighbours(node))
                        if (seen.Add(next.Index)) open.Enqueue((next, steps + 1));
                }
                return false;
            }

            // Somewhere that is not one more cave room: a role beyond plain and pack, an arena, the spawn's village.
            static bool Special(RoomNode node) => node.Kind != RoomKind.Room || node.Role != RoomRole.Plain && node.Role != RoomRole.Pack;

            void Deal(GameRandom random)
            {
                // Biome by biome (each its own share of every role), the rooms of each shuffled for every role.
                foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                {
                    var rooms = _graph.Nodes.Where(n => n.Kind == RoomKind.Room && n.Biome == biome && !_built[n.Index]).ToList();
                    if (rooms.Count == 0) continue;
                    foreach (var role in RoleOrder)
                    {
                        var rule = _rooms.Rule(biome, role);
                        int count = role == RoomRole.Gate ? (rooms.Count >= 12 ? 1 : 0) : (rooms.Count * rule.Per100 + 50) / 100;
                        if (count <= 0) continue;
                        var order = rooms.ToList();
                        for (int i = order.Count - 1; i > 0; i--)
                        {
                            int j = random.Next(i + 1);
                            (order[i], order[j]) = (order[j], order[i]);
                        }
                        // The gate stands about half way out through its biome, at a dead end where there is one.
                        if (role == RoomRole.Gate)
                        {
                            var depths = rooms.Select(n => n.Depth).OrderBy(d => d).ToList();
                            int middle = depths[depths.Count / 2];
                            order = order.OrderBy(n => Math.Abs(n.Depth - middle)).ToList();
                        }
                        // First the rooms the role asks for (dead ends, loops), then any.
                        for (int pass = 0; pass < 2 && count > 0; pass++)
                        {
                            foreach (var node in order)
                            {
                                if (count <= 0) break;
                                if (_dealt[node.Index] || !Allowed(node, role)) continue;
                                bool fits = rule.Where == RoleWhere.Any || (rule.Where == RoleWhere.DeadEnd) == node.DeadEnd;
                                if (pass == 0 && !fits) continue;
                                if (Near(node, rule.MinGap, n => n.Role == role && _dealt[n.Index])) continue;
                                if (!Take(node, role)) continue;
                                count--;
                            }
                        }
                    }
                }

                // The rest: a pack's or plain, by the two roles' numbers. Plain where a road runs and next to the spawn; a
                // pack before every treasure.
                foreach (var node in _graph.Nodes)
                {
                    if (node.Kind != RoomKind.Room || _dealt[node.Index]) continue;
                    _dealt[node.Index] = true;
                    if (_built[node.Index] || node.Roadside || node.Depth <= PlainDepth) continue;
                    int pack = _rooms.Rule(node.Biome, RoomRole.Pack).Per100, plain = _rooms.Rule(node.Biome, RoomRole.Plain).Per100;
                    bool guards = Neighbours(node).Any(n => n.Role == RoomRole.Treasure);
                    if (!guards && random.Next(Math.Max(1, pack + plain)) >= pack) continue;
                    node.Role = RoomRole.Pack;
                    if (_chunkRandom.Next(100) < _terrain.Of(node.Biome).ChunkPercent) Stamp(node, RoomRole.Pack);
                }

                // No stretch of rooms with nothing in it: a room with nothing but plain rooms and packs within
                // SpecialWithin rooms becomes (or a room beside it becomes) an ore room, or a shrine or a pond where one fits.
                var fill = new[] { RoomRole.Shrine, RoomRole.Pond, RoomRole.Ore };
                int turn = 0;
                foreach (var node in _graph.Nodes)
                {
                    if (node.Kind != RoomKind.Room || Special(node) || Near(node, SpecialWithin, Special)) continue;
                    var spots = new[] { node }.Concat(Neighbours(node)).Concat(Neighbours(node).SelectMany(Neighbours))
                        .Where(n => n.Kind == RoomKind.Room && !_built[n.Index] && !Special(n) && n.Chunk == null && n.Depth > PlainDepth).Distinct().ToList();
                    bool filled = false;
                    foreach (var spot in spots)
                    {
                        for (int i = 0; i < fill.Length && !filled; i++)
                        {
                            var role = fill[(turn + i) % fill.Length];
                            if (!Allowed(spot, role) || role == RoomRole.Pond && _rooms.Rule(spot.Biome, role).Per100 == 0) continue;
                            filled = Take(spot, role);
                        }
                        if (filled) break;
                    }
                    turn++;
                }
            }

            // Where a role may go at all. A room a road runs through takes only what is safe (Docs/Design.md: no monsters
            // on the roads); nothing but plain rooms by the spawn; no leader or trap near it.
            bool Allowed(RoomNode node, RoomRole role)
            {
                if (_built[node.Index] || node.Depth <= PlainDepth) return false;
                // Veins and a pool bring no monsters either (2026-11-22: with the roads along the rooms, a road runs
                // through some eighty rooms, and with only three roles for them stretches of the road had nothing by it).
                if (node.Roadside) return role == RoomRole.Rest || role == RoomRole.Cart || role == RoomRole.Shrine || role == RoomRole.Ore || role == RoomRole.Pond;
                if ((role == RoomRole.Elite || role == RoomRole.Trap) && node.Depth <= NoDangerDepth) return false;
                return true;
            }

            // Gives the room the role, with a chunk where the role has any; false (and the room untouched) when the role
            // needs a chunk and none fits there.
            bool Take(RoomNode node, RoomRole role)
            {
                // A hidden room goes into the rock beside its room first (3b), and only where none fits into its middle.
                bool stamped = role == RoomRole.Hidden && Bury(node) || Stamp(node, role);
                if (!stamped && role != RoomRole.Ore) return false;
                node.Role = role;
                _dealt[node.Index] = true;
                if (role == RoomRole.Ore) Veins(node);
                return true;
            }

            // Sets a chunk of the role into the room: one picked by weight among those of its biome, up to three tries, at
            // the room's middle or a little off it. False when none fits.
            bool Stamp(RoomNode node, RoomRole role)
            {
                var choices = _rooms.For(role, node.Biome).ToList();
                // A chunk made for the biome comes before one for anywhere.
                var own = choices.Where(c => c.Biomes.Count > 0).ToList();
                for (int attempt = 0; attempt < 4 && choices.Count > 0; attempt++)
                {
                    var pool = attempt == 0 && own.Count > 0 && _chunkRandom.Next(100) < 70 ? own : choices;
                    var chunk = _chunkRandom.PickWeighted(pool, c => c.Weight);
                    int way = chunk.Turn ? _chunkRandom.Next(8) : 0;
                    int shift = Math.Max(2, node.Radius / 2);
                    // A road through the middle of the room: further off to either side, clear of it.
                    var offsets = new[] { (0, 0), (shift, 0), (-shift, 0), (0, shift), (0, -shift) };
                    int far = node.Radius;
                    if (node.Roadside) offsets = offsets.Concat(new[] { (far, 0), (-far, 0), (0, far), (0, -far), (far, far), (-far, -far), (far, -far), (-far, far) }).ToArray();
                    foreach (var (ox, oy) in offsets)
                    {
                        if (!Fits(node, chunk, way, node.X + ox, node.Y + oy)) continue;
                        Set(node, chunk, way, node.X + ox, node.Y + oy);
                        return true;
                    }
                    choices.Remove(chunk);
                    own.Remove(chunk);
                }
                return false;
            }

            // The chunk's bottom left tile when its middle is at (cx, cy).
            static (int Left, int Bottom) Corner(RoomChunk chunk, int way, int cx, int cy)
            {
                var (w, h) = chunk.Size(way);
                return (cx - w / 2, cy - h / 2);
            }

            bool Fits(RoomNode node, RoomChunk chunk, int way, int cx, int cy)
            {
                var (w, h) = chunk.Size(way);
                int walk = chunk.Walled ? ChunkWalk : 0;
                // Not much bigger than the room it stands in.
                if (Math.Max(w, h) + 2 * walk > 2 * node.Radius + 5) return false;
                var (left, bottom) = Corner(chunk, way, cx, cy);
                for (int y = bottom - walk; y < bottom + h + walk; y++)
                {
                    for (int x = left - walk; x < left + w + walk; x++)
                    {
                        if (!Inside(x, y) || _taken.Contains(y * _width + x)) return false;
                        if (_middles.TryGetValue(y * _width + x, out int other) && other != node.Index) return false;
                        // Rock the generator made may be opened only where the chunk says so; what is not terrain is not touched.
                        bool inChunk = x >= left && y >= bottom && x < left + w && y < bottom + h;
                        if (inChunk && chunk.At(x - left, h - 1 - (y - bottom), way) == '?') continue;
                        if (!Natural(_map[x, y])) return false;
                        foreach (var arena in _world.Arenas)
                            if (Square(TileMap.Cell(arena.X) - x, TileMap.Cell(arena.Y) - y) <= (ArenaRadius + ArenaWall + 1) * (ArenaRadius + ArenaWall + 1)) return false;
                    }
                }
                return true;
            }

            void Set(RoomNode node, RoomChunk chunk, int way, int cx, int cy)
            {
                var (w, h) = chunk.Size(way);
                var (left, bottom) = Corner(chunk, way, cx, cy);
                int walk = chunk.Walled ? ChunkWalk : 0;
                for (int y = bottom - walk; y < bottom + h + walk; y++)
                {
                    for (int x = left - walk; x < left + w + walk; x++)
                    {
                        _taken.Add(y * _width + x);
                        bool inChunk = x >= left && y >= bottom && x < left + w && y < bottom + h;
                        // The walk round a walled chunk: whatever stood there is floor.
                        if (!inChunk && _map.IsSolid(x, y) || !inChunk && (_map[x, y] == TileType.Lava)) _map[x, y] = TileType.Floor;
                    }
                }

                // The doors the room's ways face are opened; the others are wall.
                var doors = new List<(int X, int Y)>();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (chunk.At(x, y, way) == RoomSheet.Door) doors.Add((left + x, bottom + h - 1 - y));
                var opened = new HashSet<(int, int)>();
                if (doors.Count > 0)
                {
                    foreach (var other in Neighbours(node))
                    {
                        long best = long.MinValue;
                        var pick = doors[0];
                        foreach (var door in doors)
                        {
                            long facing = (long)(door.X - cx) * (other.X - cx) + (long)(door.Y - cy) * (other.Y - cy);
                            if (facing > best) (best, pick) = (facing, door);
                        }
                        opened.Add(pick);
                    }
                    if (opened.Count == 0) opened.Add(doors[0]);
                }

                (int X, int Y)? inside = null;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        char letter = chunk.At(x, y, way);
                        int tx = left + x, ty = bottom + h - 1 - y;
                        if (letter == '?') continue;
                        TileType type;
                        if (letter == RoomSheet.Door) type = opened.Contains((tx, ty)) ? TileType.Floor : TileType.Wall;
                        else if (letter == '%') type = _chunkRandom.Next(2) == 0 ? TileType.Wall : TileType.Floor;
                        else type = RoomSheet.Tile(letter) ?? TileType.Floor;
                        _map[tx, ty] = type;
                        if (type == TileType.Wall && letter != '%') _graph.Kept.Add(ty * _width + tx);
                        if (RoomSheet.Mark(letter) is RoomMarkKind mark) _graph.MarkList.Add(new RoomMark(node.Index, mark, tx, ty));
                        if (letter == '.' && (inside == null || Square(tx - cx, ty - cy) < Square(inside.Value.X - cx, inside.Value.Y - cy))) inside = (tx, ty);
                    }
                }

                node.Chunk = chunk;
                node.ChunkWay = way;
                node.Box = (left, bottom, w, h);
                if (opened.Count > 0)
                {
                    // The tile of the walk just outside a door.
                    var (dx, dy) = opened.OrderBy(d => d.Item2 * _width + d.Item1).First();
                    int outX = dx == left ? -1 : dx == left + w - 1 ? 1 : 0, outY = dy == bottom ? -1 : dy == bottom + h - 1 ? 1 : 0;
                    (node.AnchorX, node.AnchorY) = (dx + outX, dy + outY);
                }
                else if (inside is (int ix, int iy)) (node.AnchorX, node.AnchorY) = (ix, iy);
                if (Shut(chunk)) node.Inside = inside;
            }

            // ---- A hidden room in the rock (2026-11-22, Docs/WorldRework.md 숨은 방과 금 간 벽, 묶음 3b) ----

            // How far from its room's middle a buried room's door may open, beyond the room's radius.
            const int BuryReach = 4;
            // Of the floor at its room's edge, how many tiles are tried for a buried room's door.
            const int BuryTries = 80;

            // Sets a hidden room's chunk into the solid rock beside the room, its door against the room's floor and the
            // door itself a cracked wall: from the room it is only a crack in the rock face. A rock all round it, so
            // nothing shows it but the crack (and what the screen makes of it: dust, a draught). False when no rock
            // beside the room is thick enough; then it stands in the room's middle as before (Stamp).
            bool Bury(RoomNode node)
            {
                var choices = _rooms.For(RoomRole.Hidden, node.Biome).Where(c => c.Walled).ToList();
                if (choices.Count == 0) return false;
                var chunk = _chunkRandom.PickWeighted(choices, c => c.Weight);
                int reach = Math.Max(3, node.Radius) + BuryReach;
                // The room's floor along its rock: each tile with the way into the rock.
                var faces = new List<(int X, int Y, int Dx, int Dy)>();
                for (int y = node.Y - reach; y <= node.Y + reach; y++)
                {
                    for (int x = node.X - reach; x <= node.X + reach; x++)
                    {
                        if (!Inside(x, y) || Square(x - node.X, y - node.Y) > reach * reach || !Bare(_map[x, y]) || _taken.Contains(y * _width + x)) continue;
                        // Not a crumb of floor: a floor tile with floor on two sides at least.
                        int open = 0;
                        foreach (var (dx, dy) in Sides)
                            if (Bare(_map[x + dx, y + dy])) open++;
                        if (open < 2) continue;
                        foreach (var (dx, dy) in Sides)
                            if (_map[x + dx, y + dy] == TileType.Wall) faces.Add((x, y, dx, dy));
                    }
                }
                for (int tries = 0; tries < BuryTries && faces.Count > 0; tries++)
                {
                    int pick = _chunkRandom.Next(faces.Count);
                    var (fx, fy, ix, iy) = faces[pick];
                    faces[pick] = faces[faces.Count - 1];
                    faces.RemoveAt(faces.Count - 1);
                    // The ways round whose door looks out of the rock toward the floor.
                    int first = _chunkRandom.Next(8);
                    for (int k = 0; k < (chunk.Turn ? 8 : 1); k++)
                    {
                        int way = chunk.Turn ? (first + k) % 8 : 0;
                        var (w, h) = chunk.Size(way);
                        for (int cy = 0; cy < h; cy++)
                        {
                            for (int cx = 0; cx < w; cx++)
                            {
                                if (chunk.At(cx, cy, way) != RoomSheet.Door) continue;
                                // The door's way out of the chunk must be back toward the floor.
                                int outX = cx == 0 ? -1 : cx == w - 1 ? 1 : 0, outY = cy == 0 ? 1 : cy == h - 1 ? -1 : 0;
                                if (outX != -ix || outY != -iy) continue;
                                int left = fx + ix - cx, bottom = fy + iy - (h - 1 - cy);
                                if (!Solid(node, left, bottom, w, h, -ix, -iy)) continue;
                                Inter(node, chunk, way, left, bottom, fx, fy);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            static readonly (int Dx, int Dy)[] Sides = { (1, 0), (-1, 0), (0, 1), (0, -1) };

            static bool Bare(TileType type) => type == TileType.Floor || type == TileType.IceFloor || type == TileType.Quicksand;

            // Whether the chunk's box and a tile round it are the generator's own rock, but on the side its door looks
            // out of (ox, oy), where the room's floor is.
            bool Solid(RoomNode node, int left, int bottom, int w, int h, int ox, int oy)
            {
                for (int y = bottom - 1; y <= bottom + h; y++)
                {
                    for (int x = left - 1; x <= left + w; x++)
                    {
                        if (ox == -1 && x == left - 1 || ox == 1 && x == left + w || oy == -1 && y == bottom - 1 || oy == 1 && y == bottom + h) continue;
                        if (!Inside(x, y) || _taken.Contains(y * _width + x) || _graph.Kept.Contains(y * _width + x)) return false;
                        if (_middles.TryGetValue(y * _width + x, out int other) && other != node.Index) return false;
                        var type = _map[x, y];
                        if (type != TileType.Wall && !type.ToString().EndsWith("Ore")) return false;
                        foreach (var arena in _world.Arenas)
                            if (Square(TileMap.Cell(arena.X) - x, TileMap.Cell(arena.Y) - y) <= (ArenaRadius + ArenaWall + 1) * (ArenaRadius + ArenaWall + 1)) return false;
                    }
                }
                return true;
            }

            // Lays a buried chunk: as Set, but its door is a cracked wall too and nothing round it is opened.
            void Inter(RoomNode node, RoomChunk chunk, int way, int left, int bottom, int fx, int fy)
            {
                var (w, h) = chunk.Size(way);
                for (int y = bottom - 1; y <= bottom + h; y++)
                    for (int x = left - 1; x <= left + w; x++)
                        _taken.Add(y * _width + x);
                (int X, int Y)? inside = null;
                int mx = left + w / 2, my = bottom + h / 2;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        char letter = chunk.At(x, y, way);
                        int tx = left + x, ty = bottom + h - 1 - y;
                        if (letter == '?') continue;
                        TileType type = letter == RoomSheet.Door ? TileType.CrackedWall
                            : letter == '%' ? (_chunkRandom.Next(2) == 0 ? TileType.Wall : TileType.Floor)
                            : RoomSheet.Tile(letter) ?? TileType.Floor;
                        _map[tx, ty] = type;
                        if (type == TileType.Wall && letter != '%') _graph.Kept.Add(ty * _width + tx);
                        if (RoomSheet.Mark(letter) is RoomMarkKind mark) _graph.MarkList.Add(new RoomMark(node.Index, mark, tx, ty));
                        if (letter == '.' && (inside == null || Square(tx - mx, ty - my) < Square(inside.Value.X - mx, inside.Value.Y - my))) inside = (tx, ty);
                    }
                }
                node.Chunk = chunk;
                node.ChunkWay = way;
                node.Box = (left, bottom, w, h);
                node.Buried = true;
                (node.AnchorX, node.AnchorY) = (fx, fy);
                node.Inside = inside;
            }

            // A chunk whose inside is behind a wall that must be dug.
            static bool Shut(RoomChunk chunk) => chunk.Role == RoomRole.Hidden || chunk.Role == RoomRole.Locked;

            // Two or three veins in the rock round an ore room: the ore of where it is (copper near the spawn, iron and
            // mithril further out, obsidian in the lava fields, void stone in the abyss).
            void Veins(RoomNode node)
            {
                var random = new GameRandom(_seed + (ulong)node.Index, RoleStream + 100);
                int k = _scale;
                long far = Square(node.X - _world.SpawnX, node.Y - _world.SpawnY);
                TileType ore = node.Biome == Biome.Abyss ? TileType.VoidOre
                    : node.Biome == Biome.Lava && far >= (long)ObsidianDistance * k * ObsidianDistance * k ? TileType.ObsidianOre
                    : far >= (long)MithrilDistance * k * MithrilDistance * k * 16 && random.Next(2) == 0 ? TileType.MithrilOre
                    : far >= (long)IronDistance * k * IronDistance * k ? TileType.IronOre
                    : TileType.CopperOre;
                int r = Math.Max(3, node.Radius) + 3, veins = 2 + random.Next(2);
                for (int vein = 0; vein < veins; vein++)
                {
                    for (int attempt = 0; attempt < 60; attempt++)
                    {
                        int x = node.X + random.Next(2 * r + 1) - r, y = node.Y + random.Next(2 * r + 1) - r;
                        if (!Inside(x, y) || _map[x, y] != TileType.Wall || _graph.Kept.Contains(y * _width + x)) continue;
                        // In the face of the rock: beside open floor.
                        if (_map.IsSolid(x + 1, y) && _map.IsSolid(x - 1, y) && _map.IsSolid(x, y + 1) && _map.IsSolid(x, y - 1)) continue;
                        for (int vy = y - 1; vy <= y + 1; vy++)
                            for (int vx = x - 1; vx <= x + 1; vx++)
                                if (Inside(vx, vy) && _map[vx, vy] == TileType.Wall && !_graph.Kept.Contains(vy * _width + vx) && (vx == x && vy == y || random.Next(100) < VeinPercent))
                                    _map[vx, vy] = ore;
                        break;
                    }
                }
            }

            // Cracked walls across some of the ways that close loops: a bar two tiles thick across the way at its
            // middle. Dug through, the loop is a short cut. Only where the bar really shuts the way (checked by walking
            // round it close by), and never on a road or in a chunk.
            void Plug(GameRandom random)
            {
                foreach (var edge in _graph.Edges)
                {
                    if (!edge.Loop || edge.Path == null || edge.Path.Count < CrackedLength * Math.Min(_width, _height) / GameSize) continue;
                    var a = _graph.Nodes[edge.A];
                    var b = _graph.Nodes[edge.B];
                    if (a.Kind != RoomKind.Room || b.Kind != RoomKind.Room || random.Next(100) >= CrackedPercent) continue;
                    if (a.Roadside && b.Roadside) continue;
                    int middle = edge.Path.Count / 2, back = Math.Max(0, middle - 5), ahead = Math.Min(edge.Path.Count - 1, middle + 6);
                    var (mx, my) = edge.Path[middle];
                    bool across = Math.Abs(edge.Path[ahead].X - edge.Path[back].X) >= Math.Abs(edge.Path[ahead].Y - edge.Path[back].Y);
                    int half = edge.Width / 2 + 3;
                    var bar = new List<(int X, int Y)>();
                    bool clear = true;
                    for (int t = -half; t <= half && clear; t++)
                    {
                        for (int thick = 0; thick < 2 && clear; thick++)
                        {
                            int x = across ? mx + thick : mx + t, y = across ? my + t : my + thick;
                            if (!Inside(x, y) || _taken.Contains(y * _width + x)) clear = false;
                            else if (_map[x, y] == TileType.Floor || _map[x, y] == TileType.IceFloor || _map[x, y] == TileType.Quicksand) bar.Add((x, y));
                            else if (!Natural(_map[x, y])) clear = false;
                        }
                    }
                    // A bar wider than the way and a little is not across a way but in a room.
                    if (!clear || bar.Count == 0 || bar.Count > 2 * (edge.Width + 3)) continue;
                    var was = bar.Select(t => _map[t.X, t.Y]).ToList();
                    foreach (var (x, y) in bar) _map[x, y] = TileType.CrackedWall;
                    if (Leaks(edge.Path[back], edge.Path[ahead], mx, my, half + 8))
                    {
                        for (int i = 0; i < bar.Count; i++) _map[bar[i].X, bar[i].Y] = was[i];
                        continue;
                    }
                    edge.Kind = RoomEdgeKind.Cracked;
                }
            }

            // Whether one tile is walked to from the other without leaving the square of that reach round (cx, cy).
            bool Leaks((int X, int Y) from, (int X, int Y) to, int cx, int cy, int reach)
            {
                if (_map.IsSolid(from.X, from.Y) || _map.IsSolid(to.X, to.Y)) return true;
                var seen = new HashSet<(int, int)> { from };
                var open = new Queue<(int X, int Y)>();
                open.Enqueue(from);
                while (open.Count > 0)
                {
                    var (x, y) = open.Dequeue();
                    if ((x, y) == to) return true;
                    foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (Math.Abs(next.Item1 - cx) > reach || Math.Abs(next.Item2 - cy) > reach || _map.IsSolid(next.Item1, next.Item2) || !seen.Add(next)) continue;
                        open.Enqueue(next);
                    }
                }
                return false;
            }
        }
    }
}
