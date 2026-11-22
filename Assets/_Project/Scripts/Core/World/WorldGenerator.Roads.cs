using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum RoadKind : byte
    {
        // A road of the story (places.csv road): three wide and paved, with lamps, signposts and a rest spot.
        Road,
        // From a place to the boss arena beside it (places.csv arena).
        Arena,
        // A by-way between two places that a road does not join (places.csv links): two wide, no lamps, a cracked wall
        // half way. Dug through, it closes a ring of roads.
        Link,
    }

    // A road as laid in a world of rooms (generator version 2, Docs/WorldRework.md 길과 고리): the rooms it runs
    // through in order and the ways between them (indexes into RoomGraph.Nodes and Edges), and its middle line from
    // one end to the other.
    public sealed class RoadRoute
    {
        internal RoadRoute(RoadKind kind, PlaceDefinition from, PlaceDefinition to, List<int> rooms, List<int> ways, List<(int X, int Y)> tiles)
        {
            Kind = kind;
            From = from;
            To = to;
            Rooms = rooms;
            Ways = ways;
            Tiles = tiles;
        }

        public RoadKind Kind { get; }
        public PlaceDefinition From { get; }
        // The place at the other end; for an arena's road, the place whose arena it goes to.
        public PlaceDefinition To { get; }
        public IReadOnlyList<int> Rooms { get; }
        public IReadOnlyList<int> Ways { get; }
        public IReadOnlyList<(int X, int Y)> Tiles { get; }
        // How many short tunnels it cuts through rock between rooms the ways do not join.
        public int Tunnels { get; internal set; }
        // A by-way's cracked wall: the way it stands across (RoomGraph.Edges) and a tile of it; null where none fitted.
        public int? CrackedWay { get; internal set; }
        public (int X, int Y)? Cracked { get; internal set; }
    }

    // ---- The roads of a world of rooms (2026-11-22, Docs/WorldRework.md 길과 고리, the person: "I only feel I am walking
    // to where I am going"). Version 1 laid each road as a line, bent a few times, through whatever rock lay between;
    // here a road follows the rooms and the ways between them (the shortest way by the rooms, as the corridors were
    // carved, so it winds as they do), and only the short leg from a plaza to the first room is cut through rock. The
    // corridors a road runs along are widened to a wide tunnel. By-ways (places.csv links) are laid the same way but
    // keep off the roads where they can, so the places stand on rings. Integer math only. ----
    public static partial class WorldGenerator
    {
        // Each tile of a way costs this much; leaving the rooms to cut through rock to the first or from the last room
        // costs RockLead times as much, a narrow way (two wide or less) NarrowWay tenths, a way a road already takes
        // (for a by-way) SharedWay times, and going through a room by another place OtherPlace tiles more.
        const int WayCost = 10;
        const int RockLead = 3;
        const int NarrowWay = 13;
        const int SharedWay = 2;
        const int OtherPlace = 400;
        // Rock either side of a road is opened this far from its middle (a wide tunnel, five across).
        const int RoadShoulder = 2;
        // A by-way is two wide.
        const int LinkWidth = 2;

        // Laid by CarvePlaces on a world of rooms, in place of version 1's lines: the roads (and their rooms), then the
        // roads to the places' arenas, then the by-ways. Each list entry: the road's tiles for the dressing that follows.
        sealed class RoadLayer
        {
            readonly GeneratedWorld _world;
            readonly TileMap _map;
            readonly RoomGraph _graph;
            readonly List<(PlaceDefinition Place, int X, int Y)> _spots;
            readonly List<(int X, int Y)> _arenas;
            readonly int _reach;
            readonly bool[] _open;
            // Whether each way may carry a road (WayOpen), worked out once.
            readonly bool[] _wayOpen;
            // How many roads run along each way.
            readonly int[] _used;
            // Short tunnels a road may cut through the rock between two rooms the ways do not join (from, to, tiles),
            // and those from each room: where the rooms go a long way round (two biomes that meet at a wall of rock).
            readonly List<(int A, int B, List<(int X, int Y)> Path)> _tunnels = new List<(int, int, List<(int, int)>)>();
            readonly List<int>[] _tunnelsFrom;
            // Each tile (y * width + x): 0, or Plaza for a place's plaza, or one more than the index of the arena whose
            // floor or ring of rock it is in (ArenaRadius + ArenaWall round its middle).
            readonly byte[] _held;
            const byte Plaza = 255;
            public readonly List<RoadRoute> Routes = new List<RoadRoute>();

            public RoadLayer(GeneratedWorld world, List<(PlaceDefinition Place, int X, int Y)> spots, List<(int X, int Y)> arenas)
            {
                _world = world;
                _map = world.Map;
                _graph = world.Rooms;
                _spots = spots;
                _arenas = arenas;
                _reach = _graph.CellSize + _graph.CellSize / 4;
                _open = new bool[_graph.Nodes.Count];
                _used = new int[_graph.Edges.Count];
                _held = new byte[_map.Width * _map.Height];
                int ring = ArenaRadius + ArenaWall;
                for (int i = _arenas.Count - 1; i >= 0; i--)
                {
                    var (ax, ay) = _arenas[i];
                    for (int y = Math.Max(0, ay - ring); y <= Math.Min(_map.Height - 1, ay + ring); y++)
                        for (int x = Math.Max(0, ax - ring); x <= Math.Min(_map.Width - 1, ax + ring); x++)
                            if (Square(x - ax, y - ay) <= ring * ring) _held[y * _map.Width + x] = (byte)(i + 1);
                }
                foreach (var spot in _spots)
                {
                    int r = spot.Place.Radius;
                    for (int y = Math.Max(0, spot.Y - r); y <= Math.Min(_map.Height - 1, spot.Y + r); y++)
                        for (int x = Math.Max(0, spot.X - r); x <= Math.Min(_map.Width - 1, spot.X + r); x++)
                            if (Square(x - spot.X, y - spot.Y) <= r * r) _held[y * _map.Width + x] = Plaza;
                }
                int keep = ArenaRadius + ArenaWall + 3;
                foreach (var node in _graph.Nodes)
                    _open[node.Index] = node.Kind != RoomKind.Arena && _arenas.All(a => Square(node.X - a.X, node.Y - a.Y) > keep * keep);
                _wayOpen = _graph.Edges.Select(WayOpen).ToArray();
                _tunnelsFrom = new List<int>[_graph.Nodes.Count];
                for (int i = 0; i < _tunnelsFrom.Length; i++) _tunnelsFrom[i] = new List<int>();
                int tunnel = _graph.CellSize * 3 / 2;
                foreach (var a in _graph.Nodes)
                {
                    if (!_open[a.Index]) continue;
                    foreach (var b in _graph.Nodes)
                    {
                        if (b.Index <= a.Index || !_open[b.Index] || Square(a.X - b.X, a.Y - b.Y) > tunnel * tunnel) continue;
                        if (a.Ways.Any(w => _graph.Edges[w].A == b.Index || _graph.Edges[w].B == b.Index)) continue;
                        var line = Line(a.X, a.Y, b.X, b.Y).ToList();
                        if (line.Any(p => _held[p.Y * _map.Width + p.X] is byte h && h != 0 && h != Plaza)) continue;
                        _tunnelsFrom[a.Index].Add(_tunnels.Count);
                        _tunnelsFrom[b.Index].Add(_tunnels.Count);
                        _tunnels.Add((a.Index, b.Index, line));
                    }
                }
            }

            static int Square(int dx, int dy) => dx * dx + dy * dy;

            static int Root(long square) => (int)Math.Sqrt(square);

            bool InPlaza(int x, int y) => _held[y * _map.Width + x] == Plaza;

            // Whether a way may carry a road: carved, between open rooms, and clear of every arena (the places' arenas
            // were cut over the rooms after the ways were carved).
            bool WayOpen(RoomEdge edge)
            {
                if (edge.Path == null || !_open[edge.A] || !_open[edge.B]) return false;
                int near = ArenaRadius + 3;
                for (int i = 0; i < edge.Path.Count; i += 2)
                {
                    var (x, y) = edge.Path[i];
                    foreach (var a in _arenas)
                        if (Square(x - a.X, y - a.Y) <= near * near) return false;
                }
                return true;
            }

            // The cheapest way by the rooms from a point to another: from a room within reach of the start (the leg
            // to it cut through rock), along the ways, to a room within reach of the end. Null when there is none.
            RoadRoute Find(RoadKind kind, PlaceDefinition fromPlace, PlaceDefinition toPlace, (int X, int Y) from, int fromReach, (int X, int Y) to, int toReach,
                Func<int, bool> nodeOk, Func<RoomEdge, int, long> wayCost, Func<RoomNode, long> roomCost)
            {
                int count = _graph.Nodes.Count;
                var cost = new long[count];
                var came = new int[count];
                for (int i = 0; i < count; i++)
                {
                    cost[i] = long.MaxValue;
                    came[i] = -1;
                }
                var open = new SortedSet<(long Cost, int Node)>();
                long Lead(RoomNode node, (int X, int Y) at) => (long)Root(Square(node.X - at.X, node.Y - at.Y)) * WayCost * RockLead;
                bool Ok(RoomNode node) => _open[node.Index] && nodeOk(node.Index);
                var starts = _graph.Nodes.Where(n => Ok(n) && Square(n.X - from.X, n.Y - from.Y) <= fromReach * fromReach).ToList();
                if (starts.Count == 0)
                {
                    var nearest = _graph.Nodes.Where(Ok).OrderBy(n => Square(n.X - from.X, n.Y - from.Y)).ThenBy(n => n.Index).FirstOrDefault();
                    if (nearest == null) return null;
                    starts.Add(nearest);
                }
                foreach (var node in starts)
                {
                    cost[node.Index] = Lead(node, from);
                    open.Add((cost[node.Index], node.Index));
                }
                while (open.Count > 0)
                {
                    var (here, at) = open.Min;
                    open.Remove(open.Min);
                    if (here > cost[at]) continue;
                    var node = _graph.Nodes[at];
                    void Try(int next, long step, int how)
                    {
                        if (!Ok(_graph.Nodes[next])) return;
                        long total = here + step + roomCost(_graph.Nodes[next]);
                        if (total >= cost[next]) return;
                        if (cost[next] != long.MaxValue) open.Remove((cost[next], next));
                        cost[next] = total;
                        came[next] = how;
                        open.Add((total, next));
                    }
                    foreach (int way in node.Ways)
                    {
                        var edge = _graph.Edges[way];
                        if (_wayOpen[way]) Try(edge.A == at ? edge.B : edge.A, wayCost(edge, way), way);
                    }
                    // A tunnel is a step from the last: -2 for the first, -3 for the second, and so on.
                    foreach (int t in _tunnelsFrom[at])
                    {
                        var (a, b, path) = _tunnels[t];
                        Try(a == at ? b : a, (long)path.Count * WayCost * RockLead, -2 - t);
                    }
                }
                RoomNode end = null;
                long best = long.MaxValue;
                foreach (var node in _graph.Nodes)
                {
                    if (cost[node.Index] == long.MaxValue) continue;
                    long d = Square(node.X - to.X, node.Y - to.Y);
                    if (d > (long)toReach * toReach) continue;
                    long total = cost[node.Index] + Lead(node, to);
                    if (total < best) (best, end) = (total, node);
                }
                if (end == null)
                {
                    // Nothing within reach of the end was come to: the nearest room that was.
                    end = _graph.Nodes.Where(n => cost[n.Index] != long.MaxValue).OrderBy(n => Square(n.X - to.X, n.Y - to.Y)).ThenBy(n => n.Index).FirstOrDefault();
                    if (end == null) return null;
                }

                var rooms = new List<int> { end.Index };
                var steps = new List<int>();
                for (int at = end.Index; came[at] != -1;)
                {
                    steps.Add(came[at]);
                    if (came[at] >= 0)
                    {
                        var edge = _graph.Edges[came[at]];
                        at = edge.A == at ? edge.B : edge.A;
                    }
                    else
                    {
                        var (a, b, _) = _tunnels[-2 - came[at]];
                        at = a == at ? b : a;
                    }
                    rooms.Add(at);
                }
                rooms.Reverse();
                steps.Reverse();

                var tiles = new List<(int X, int Y)>();
                void Add((int X, int Y) p)
                {
                    if (tiles.Count > 0)
                    {
                        var last = tiles[tiles.Count - 1];
                        if (last == p) return;
                        // The wandering of a way can skip a tile: the gap is filled with a line.
                        if (Math.Abs(last.X - p.X) > 1 || Math.Abs(last.Y - p.Y) > 1)
                        {
                            foreach (var q in Line(last.X, last.Y, p.X, p.Y).Skip(1)) tiles.Add(q);
                            return;
                        }
                    }
                    tiles.Add(p);
                }
                var first = _graph.Nodes[rooms[0]];
                foreach (var p in Line(from.X, from.Y, first.X, first.Y)) Add(p);
                for (int i = 0; i < steps.Count; i++)
                {
                    IEnumerable<(int X, int Y)> path;
                    if (steps[i] >= 0)
                    {
                        var edge = _graph.Edges[steps[i]];
                        path = edge.A == rooms[i] ? edge.Path : Enumerable.Reverse(edge.Path);
                    }
                    else
                    {
                        var (a, _, line) = _tunnels[-2 - steps[i]];
                        path = a == rooms[i] ? line : Enumerable.Reverse(line);
                    }
                    foreach (var p in path) Add(p);
                    var room = _graph.Nodes[rooms[i + 1]];
                    Add((room.X, room.Y));
                }
                foreach (var p in Line(end.X, end.Y, to.X, to.Y)) Add(p);
                return new RoadRoute(kind, fromPlace, toPlace, rooms, steps.Where(w => w >= 0).ToList(), tiles) { Tunnels = steps.Count(w => w < 0) };
            }

            // Which other place's plaza (and a little round it) a room is by, so a road does not run through a town it
            // does not go to.
            bool ByOtherPlace(RoomNode node, PlaceDefinition a, PlaceDefinition b) =>
                _spots.Any(s => s.Place != a && s.Place != b && Square(node.X - s.X, node.Y - s.Y) <= (s.Place.Radius + 8) * (s.Place.Radius + 8));

            // As the crow flies from room to room (a way's own tiles step sideways and up, so a slanting way would count
            // longer than it is walked), a narrow way dearer.
            long Way(RoomEdge edge)
            {
                var (a, b) = (_graph.Nodes[edge.A], _graph.Nodes[edge.B]);
                return (long)Root(Square(a.X - b.X, a.Y - b.Y)) * (edge.Width <= 2 ? NarrowWay : WayCost);
            }

            // Paves a road along its tiles, width wide (centred, as version 1's TunnelHalfWidth brush is), never over a
            // plaza or an arena's floor and ring but for the one it goes into; and with shoulder, the plain rock either
            // side is opened to bare floor that far from the middle.
            void Pave(List<(int X, int Y)> tiles, int width, int shoulder, int exempt)
            {
                // An arena's floor and ring, but for the one the road goes into, whose floor alone (its ring is cut).
                bool Kept(int x, int y)
                {
                    int held = _held[y * _map.Width + x];
                    if (held == 0 || held == Plaza) return false;
                    if (held - 1 != exempt) return true;
                    var (ax, ay) = _arenas[exempt];
                    return Square(x - ax, y - ay) <= ArenaRadius * ArenaRadius;
                }
                int low = -(width / 2), high = width - width / 2;
                foreach (var (x, y) in tiles)
                {
                    for (int ty = y - shoulder + low; ty < y + high + shoulder; ty++)
                    {
                        for (int tx = x - shoulder + low; tx < x + high + shoulder; tx++)
                        {
                            if (!IsInside(_map, tx, ty) || tx < Border || ty < Border || tx >= _map.Width - Border || ty >= _map.Height - Border) continue;
                            if (InPlaza(tx, ty) || Kept(tx, ty)) continue;
                            bool middle = tx - x >= low && tx - x < high && ty - y >= low && ty - y < high;
                            if (middle) _map[tx, ty] = TileType.Road;
                            else if (_map[tx, ty] == TileType.Wall) _map[tx, ty] = TileType.Floor;
                        }
                    }
                }
            }

            public void Roads()
            {
                foreach (var spot in _spots)
                {
                    var from = _spots.FirstOrDefault(s => s.Place.Id == spot.Place.Road);
                    if (from.Place == null || from.Place == spot.Place) continue;
                    var route = Find(RoadKind.Road, from.Place, spot.Place, (from.X, from.Y), from.Place.Radius + _reach, (spot.X, spot.Y), spot.Place.Radius + _reach,
                        _ => true, (edge, _) => Way(edge), node => ByOtherPlace(node, from.Place, spot.Place) ? OtherPlace * WayCost : 0);
                    if (route == null) continue;
                    Pave(route.Tiles.ToList(), 2 * TunnelHalfWidth + 1, RoadShoulder, -1);
                    foreach (int way in route.Ways) _used[way]++;
                    Routes.Add(route);
                }
            }

            // From a place to its arena (index into the arenas), the last leg straight in through the arena's ring.
            public RoadRoute ToArena(PlaceDefinition place, int px, int py, int arena)
            {
                var (ax, ay) = _arenas[arena];
                // An arena just off the plaza (most are) is reached straight, as in version 1.
                if (Square(ax - px, ay - py) <= (place.Radius + ArenaRadius + RoomCell / 2) * (place.Radius + ArenaRadius + RoomCell / 2))
                {
                    var line = Line(px, py, ax, ay).ToList();
                    Pave(line, 2 * TunnelHalfWidth + 1, 0, arena);
                    var straight = new RoadRoute(RoadKind.Arena, place, place, new List<int>(), new List<int>(), line);
                    Routes.Add(straight);
                    return straight;
                }
                var route = Find(RoadKind.Arena, place, place, (px, py), place.Radius + _reach, (ax, ay), ArenaRadius + ArenaWall + _reach,
                    _ => true, (edge, _) => Way(edge), node => ByOtherPlace(node, place, null) ? OtherPlace * WayCost : 0);
                if (route == null) return null;
                Pave(route.Tiles.ToList(), 2 * TunnelHalfWidth + 1, 0, arena);
                foreach (int way in route.Ways) _used[way]++;
                Routes.Add(route);
                return route;
            }

            // The by-ways: each pair of places.csv links once, keeping off the ways the roads took where it can.
            public void Links()
            {
                var done = new HashSet<(int, int)>();
                foreach (var spot in _spots)
                {
                    foreach (string id in spot.Place.Links)
                    {
                        var other = _spots.FirstOrDefault(s => s.Place.Id == id);
                        if (other.Place == null || other.Place == spot.Place) continue;
                        var pair = (Math.Min(spot.Place.Index, other.Place.Index), Math.Max(spot.Place.Index, other.Place.Index));
                        if (!done.Add(pair)) continue;
                        var route = Find(RoadKind.Link, spot.Place, other.Place, (spot.X, spot.Y), spot.Place.Radius + _reach, (other.X, other.Y), other.Place.Radius + _reach,
                            _ => true, (edge, way) => Way(edge) * (_used[way] > 0 ? SharedWay : 1),
                            node => ByOtherPlace(node, spot.Place, other.Place) ? OtherPlace * WayCost : 0);
                        if (route == null) continue;
                        Pave(route.Tiles.ToList(), LinkWidth, 0, -1);
                        foreach (int way in route.Ways) _used[way]++;
                        Crack(route);
                        Routes.Add(route);
                    }
                }
            }

            // A by-way's cracked wall: across the way nearest its middle that is long enough, between two rooms, not
            // taken by a road, and that the rooms are all still reached without; where the bar really shuts the way.
            void Crack(RoadRoute route)
            {
                int length = CrackedLength * Math.Min(_map.Width, _map.Height) / GameSize;
                // How far along the route each way's middle lies.
                var along = new List<(int Distance, int Way)>();
                int walked = 0;
                for (int i = 0; i < route.Ways.Count; i++)
                {
                    var edge = _graph.Edges[route.Ways[i]];
                    along.Add((walked + edge.Path.Count / 2, route.Ways[i]));
                    walked += edge.Path.Count;
                }
                foreach (var (_, way) in along.OrderBy(a => Math.Abs(a.Distance - walked / 2)).ThenBy(a => a.Way))
                {
                    var edge = _graph.Edges[way];
                    if (edge.Path.Count < length || _used[way] > 1 || edge.Kind != RoomEdgeKind.Way) continue;
                    if (_graph.Nodes[edge.A].Kind != RoomKind.Room || _graph.Nodes[edge.B].Kind != RoomKind.Room) continue;
                    if (!StillJoined(way)) continue;
                    int middle = edge.Path.Count / 2, back = Math.Max(0, middle - 5), ahead = Math.Min(edge.Path.Count - 1, middle + 6);
                    var (mx, my) = edge.Path[middle];
                    if (InPlaza(mx, my)) continue;
                    bool across = Math.Abs(edge.Path[ahead].X - edge.Path[back].X) >= Math.Abs(edge.Path[ahead].Y - edge.Path[back].Y);
                    int half = edge.Width / 2 + 3;
                    var bar = new List<(int X, int Y)>();
                    bool clear = true;
                    for (int t = -half; t <= half && clear; t++)
                    {
                        for (int thick = 0; thick < 2 && clear; thick++)
                        {
                            int x = across ? mx + thick : mx + t, y = across ? my + t : my + thick;
                            if (!IsInside(_map, x, y)) clear = false;
                            else if (_map[x, y] == TileType.Road || _map[x, y] == TileType.Floor) bar.Add((x, y));
                            else if (!_map.IsSolid(x, y)) clear = false;
                        }
                    }
                    if (!clear || bar.Count == 0 || bar.Count > 2 * (edge.Width + 3)) continue;
                    var was = bar.Select(t => _map[t.X, t.Y]).ToList();
                    foreach (var (x, y) in bar) _map[x, y] = TileType.CrackedWall;
                    if (Leaks(_map, edge.Path[back], edge.Path[ahead], mx, my, half + 8))
                    {
                        for (int i = 0; i < bar.Count; i++) _map[bar[i].X, bar[i].Y] = was[i];
                        continue;
                    }
                    edge.Kind = RoomEdgeKind.Cracked;
                    route.CrackedWay = way;
                    route.Cracked = (mx, my);
                    return;
                }
            }

            // Whether every room reached from the spawn's by the ways that are not shut is still reached without this one.
            bool StillJoined(int without) => Reached(without) == Reached(-1);

            int Reached(int without)
            {
                var seen = new bool[_graph.Nodes.Count];
                var open = new Stack<int>();
                int start = _graph.Nodes.First(n => n.Kind == RoomKind.Spawn).Index;
                seen[start] = true;
                open.Push(start);
                int reached = 1;
                while (open.Count > 0)
                {
                    var node = _graph.Nodes[open.Pop()];
                    foreach (int way in node.Ways)
                    {
                        var edge = _graph.Edges[way];
                        if (way == without || edge.Kind == RoomEdgeKind.Cracked) continue;
                        int next = edge.A == node.Index ? edge.B : edge.A;
                        if (seen[next]) continue;
                        seen[next] = true;
                        reached++;
                        open.Push(next);
                    }
                }
                return reached;
            }
        }

        // Whether one tile is walked to from the other without leaving the square of that reach round (cx, cy).
        static bool Leaks(TileMap map, (int X, int Y) from, (int X, int Y) to, int cx, int cy, int reach)
        {
            if (map.IsSolid(from.X, from.Y) || map.IsSolid(to.X, to.Y)) return true;
            var seen = new HashSet<(int, int)> { from };
            var open = new Queue<(int X, int Y)>();
            open.Enqueue(from);
            while (open.Count > 0)
            {
                var (x, y) = open.Dequeue();
                if ((x, y) == to) return true;
                foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (Math.Abs(next.Item1 - cx) > reach || Math.Abs(next.Item2 - cy) > reach || map.IsSolid(next.Item1, next.Item2) || !seen.Add(next)) continue;
                    open.Enqueue(next);
                }
            }
            return false;
        }
    }
}
