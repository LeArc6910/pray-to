using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A minecart line of a version 2 world (2026-11-22, Docs/WorldRework.md 광차, 묶음 3c): the tiles its rails run on
    // from the station at one end (A) to the one at the other (B), and the line a cart follows along them, eased round
    // the corners. One station's end starts broken (Broken): until it is mended there, nobody rides the line.
    public sealed class RailLine
    {
        readonly Vec2[] _points;
        readonly float[] _along;

        internal RailLine(int index, List<(int X, int Y)> route)
        {
            Index = index;
            Route = route;
            // The tile centres averaged with two on either side, so a cart rounds a staircase of tiles as a slope;
            // the two ends stay where they are.
            _points = new Vec2[route.Count];
            for (int i = 0; i < route.Count; i++)
            {
                int from = Math.Max(0, i - 2), to = Math.Min(route.Count - 1, i + 2);
                if (i == 0 || i == route.Count - 1)
                {
                    _points[i] = TileMap.Center(route[i].X, route[i].Y);
                    continue;
                }
                float x = 0, y = 0;
                for (int j = from; j <= to; j++)
                {
                    x += route[j].X + 0.5f;
                    y += route[j].Y + 0.5f;
                }
                _points[i] = new Vec2(x / (to - from + 1), y / (to - from + 1));
            }
            _along = new float[route.Count];
            for (int i = 1; i < route.Count; i++) _along[i] = _along[i - 1] + Vec2.Distance(_points[i - 1], _points[i]);
            Length = _along[route.Count - 1];
        }

        public int Index { get; }
        // The rail tiles from A to B, each beside the one before (never diagonal).
        public IReadOnlyList<(int X, int Y)> Route { get; }
        // The things to find (RoomGraph.Pois) that are its two stations, and the one of them whose end starts broken.
        public int StationA { get; internal set; }
        public int StationB { get; internal set; }
        public int Broken { get; internal set; }
        // How far a cart goes from A to B, tiles.
        public float Length { get; }

        // Where a cart is that has gone the distance from A (clamped to the line).
        public Vec2 PointAt(float distance)
        {
            if (distance <= 0) return _points[0];
            if (distance >= Length) return _points[_points.Length - 1];
            int low = 0, high = _along.Length - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (_along[middle] <= distance) low = middle;
                else high = middle;
            }
            float span = _along[high] - _along[low];
            float t = span <= 1e-6f ? 0 : (distance - _along[low]) / span;
            return _points[low] + (_points[high] - _points[low]) * t;
        }

        // Where a rider is the ticks after setting off from the station (from A when forward), at speed tiles a second.
        public Vec2 RidePoint(bool forward, float travelled) => PointAt(forward ? travelled : Length - travelled);

        // The other station of the line.
        public int Other(int station) => station == StationA ? StationB : StationA;
    }

    // ---- The minecart lines and the waystones (2026-11-22, Docs/WorldRework.md 광차·길잡이 돌, 묶음 3c). Four to six
    // lines of rails between cart rooms in different biomes, each 150 to 300 tiles along the ways the rooms are joined
    // by, a station at each end (pois.csv ride); then a waystone (pois.csv waypoint) by one camp of each biome. Both are
    // numbered after the things of the chunks' marks, so those keep their numbers. ----
    public static partial class WorldGenerator
    {
        const ulong RailStream = 51;
        const ulong StoneStream = 52;
        public const int MinRail = 150;
        public const int MaxRail = 300;
        public const int MaxLines = 6;
        // At most this many routes are worked out for a world (the rest of the pairs are not tried).
        const int RailTries = 36;
        // The rails keep this far from an arena's middle beyond its radius, and from a place's plaza.
        const int RailArenaMargin = 4;
        const int RailPlaceMargin = 3;

        static void LayRails(GeneratedWorld world, ulong seed, TerrainData terrain)
        {
            var graph = world.Rooms;
            var data = terrain?.Pois ?? PoiData.None;
            if (graph == null || !data.Pois.Any(p => p.Kind == PoiKind.Station)) return;
            var map = world.Map;
            var random = new GameRandom(seed, RailStream);

            // Each cart room's end of a line: the rail of its chunk furthest from its cart.
            var ends = new List<(RoomNode Room, int X, int Y)>();
            foreach (var mark in graph.Marks)
            {
                if (mark.Kind != RoomMarkKind.Cart) continue;
                var room = graph.Nodes[mark.Room];
                if (room.Role != RoomRole.Cart || room.Box is not (int left, int bottom, int w, int h) || ends.Any(e => e.Room == room)) continue;
                (int X, int Y)? best = null;
                int far = 0;
                for (int y = bottom; y < bottom + h; y++)
                {
                    for (int x = left; x < left + w; x++)
                    {
                        if (map[x, y] != TileType.Rail) continue;
                        int distance = (x - mark.X) * (x - mark.X) + (y - mark.Y) * (y - mark.Y);
                        if (distance <= far) continue;
                        (best, far) = ((x, y), distance);
                    }
                }
                if (best is (int bx, int by)) ends.Add((room, bx, by));
            }

            // The pairs in different biomes about the right way apart, in a seeded order.
            var pairs = new List<(int A, int B)>();
            for (int a = 0; a < ends.Count; a++)
            {
                for (int b = a + 1; b < ends.Count; b++)
                {
                    if (ends[a].Room.Biome == ends[b].Room.Biome) continue;
                    float apart = MathF.Sqrt((ends[a].X - ends[b].X) * (ends[a].X - ends[b].X) + (ends[a].Y - ends[b].Y) * (ends[a].Y - ends[b].Y));
                    if (apart < MinRail * 0.6f || apart > MaxRail * 0.95f) continue;
                    pairs.Add((a, b));
                }
            }
            for (int i = pairs.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (pairs[i], pairs[j]) = (pairs[j], pairs[i]);
            }

            var arenas = world.Arenas.Select(a => (X: TileMap.Cell(a.X), Y: TileMap.Cell(a.Y))).ToList();
            int arenaReach = world.ArenaRadius + RailArenaMargin;
            bool Off(int x, int y)
            {
                var type = map[x, y];
                if (map.IsSolid(x, y) || type == TileType.Lava || type == TileType.Water || type == TileType.Waypoint || type == TileType.Dock) return true;
                foreach (var (ax, ay) in arenas)
                    if ((x - ax) * (x - ax) + (y - ay) * (y - ay) <= arenaReach * arenaReach) return true;
                foreach (var place in world.Places)
                    if ((x - place.X) * (x - place.X) + (y - place.Y) * (y - place.Y) <= (place.Radius + RailPlaceMargin) * (place.Radius + RailPlaceMargin)) return true;
                return false;
            }

            var used = new HashSet<RoomNode>();
            var railed = new HashSet<int>();
            int tries = 0;
            foreach (var (a, b) in pairs)
            {
                if (graph.LineList.Count >= MaxLines || tries >= RailTries) break;
                if (used.Contains(ends[a].Room) || used.Contains(ends[b].Room)) continue;
                tries++;
                var route = RailRoute(map, (ends[a].X, ends[a].Y), (ends[b].X, ends[b].Y), Off);
                if (route == null || route.Count - 1 < MinRail || route.Count - 1 > MaxRail) continue;
                // Not much along another line's rails.
                if (route.Count(t => railed.Contains(t.Y * map.Width + t.X)) > route.Count / 7) continue;
                used.Add(ends[a].Room);
                used.Add(ends[b].Room);
                // A runs from the room nearer the spawn: the far end is the broken one, mended by one who walked there.
                var (near, far) = ends[a].Room.Depth <= ends[b].Room.Depth ? (ends[a], ends[b]) : (ends[b], ends[a]);
                if (near.Room != ends[a].Room) route.Reverse();
                var line = new RailLine(graph.LineList.Count, route);
                foreach (var (x, y) in route)
                {
                    railed.Add(y * map.Width + x);
                    if (map[x, y] == TileType.Floor || map[x, y] == TileType.IceFloor || map[x, y] == TileType.Quicksand) map[x, y] = TileType.Rail;
                }
                line.StationA = AddStation(graph, data, random, near.Room, near.X, near.Y);
                line.StationB = AddStation(graph, data, random, far.Room, far.X, far.Y);
                line.Broken = line.StationB;
                graph.LineList.Add(line);
            }
        }

        static int AddStation(RoomGraph graph, PoiData data, GameRandom random, RoomNode room, int x, int y)
        {
            var choices = data.Of(PoiKind.Station, room.Biome).ToList();
            if (choices.Count == 0) choices = data.Pois.Where(p => p.Kind == PoiKind.Station).ToList();
            var definition = random.PickWeighted(choices, c => c.Weight);
            graph.PoiList.Add(new Poi(graph.PoiList.Count, definition, x, y, room.Index, room.Biome));
            return graph.PoiList.Count - 1;
        }

        // The cheapest way from one tile to the other over open floor, side by side (never diagonal), within a box
        // round the two: bare floor is cheap, a road or a laid floor dear (the rails keep off the roads where they can).
        // Null when there is none.
        static List<(int X, int Y)> RailRoute(TileMap map, (int X, int Y) from, (int X, int Y) to, Func<int, int, bool> off)
        {
            const int margin = 30;
            int left = Math.Max(1, Math.Min(from.X, to.X) - margin), right = Math.Min(map.Width - 2, Math.Max(from.X, to.X) + margin);
            int bottom = Math.Max(1, Math.Min(from.Y, to.Y) - margin), top = Math.Min(map.Height - 2, Math.Max(from.Y, to.Y) + margin);
            int width = right - left + 1, height = top - bottom + 1;
            var cost = new int[width * height];
            Array.Fill(cost, int.MaxValue);
            var back = new int[width * height];
            // Dial's buckets: every step costs 2 to 6.
            const int most = 7;
            var buckets = new Queue<int>[most];
            for (int i = 0; i < most; i++) buckets[i] = new Queue<int>();
            int Index(int x, int y) => (y - bottom) * width + (x - left);
            int start = Index(from.X, from.Y), goal = Index(to.X, to.Y);
            cost[start] = 0;
            back[start] = -1;
            buckets[0].Enqueue(start);
            int pending = 1, at = 0;
            var steps = new[] { (0, 1), (1, 0), (0, -1), (-1, 0) };
            while (pending > 0)
            {
                while (buckets[at % most].Count == 0) at++;
                int here = buckets[at % most].Dequeue();
                pending--;
                if (cost[here] != at) continue;
                if (here == goal) break;
                int hx = here % width + left, hy = here / width + bottom;
                foreach (var (dx, dy) in steps)
                {
                    int x = hx + dx, y = hy + dy;
                    if (x < left || x > right || y < bottom || y > top) continue;
                    int next = Index(x, y);
                    if (next != goal && off(x, y)) continue;
                    var type = map[x, y];
                    int step = type == TileType.Floor || type == TileType.Rail || type == TileType.IceFloor || type == TileType.Quicksand ? 2 : 6;
                    if (at + step >= cost[next]) continue;
                    cost[next] = at + step;
                    back[next] = here;
                    buckets[(at + step) % most].Enqueue(next);
                    pending++;
                }
            }
            if (cost[goal] == int.MaxValue) return null;
            var route = new List<(int X, int Y)>();
            for (int i = goal; i >= 0; i = back[i]) route.Add((i % width + left, i / width + bottom));
            route.Reverse();
            return route;
        }

        // A waystone by a camp of each biome (pois.csv waypoint): on bare floor two to four tiles from the fire, with
        // open floor all round it, so it never shuts a way.
        static void LayWaystones(GeneratedWorld world, ulong seed, TerrainData terrain)
        {
            var graph = world.Rooms;
            var data = terrain?.Pois ?? PoiData.None;
            if (graph == null || !data.Pois.Any(p => p.Kind == PoiKind.Waystone)) return;
            var map = world.Map;
            var random = new GameRandom(seed, StoneStream);
            var camps = graph.Pois.Where(p => p.Kind == PoiKind.Camp).ToList();
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            {
                var here = camps.Where(c => c.Biome == biome).ToList();
                var choices = data.Of(PoiKind.Waystone, biome).ToList();
                if (here.Count == 0 || choices.Count == 0) continue;
                // A camp picked at random, or the next that has room for a stone.
                int first = random.Next(here.Count);
                var definition = random.PickWeighted(choices, c => c.Weight);
                for (int i = 0; i < here.Count; i++)
                {
                    var camp = here[(first + i) % here.Count];
                    if (OpenSpot(map, camp.X, camp.Y, 2, 4, up: true) is not (int x, int y)) continue;
                    if (definition.Tile is TileType tile) map[x, y] = tile;
                    graph.PoiList.Add(new Poi(graph.PoiList.Count, definition, x, y, camp.Room, biome));
                    break;
                }
            }
        }

        // A tile of bare floor between near and far tiles from (x, y) whose eight neighbours are all open floor (not
        // lava, water or a fire), so a solid thing set on it shuts nothing off; the nearest, above first when up.
        internal static (int X, int Y)? OpenSpot(TileMap map, int cx, int cy, int near, int far, bool up)
        {
            for (int r = near; r <= far; r++)
            {
                for (int i = 0; i <= 2 * r; i++)
                {
                    int dy = up ? r - i : i - r;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (!map.InBounds(x, y) || map.IsEdge(x, y) || map[x, y] != TileType.Floor) continue;
                        bool open = true;
                        for (int oy = -1; oy <= 1 && open; oy++)
                        {
                            for (int ox = -1; ox <= 1 && open; ox++)
                            {
                                var type = map[x + ox, y + oy];
                                if (map.IsSolid(x + ox, y + oy) || type == TileType.Lava || type == TileType.Water) open = false;
                            }
                        }
                        if (open) return (x, y);
                    }
                }
            }
            return null;
        }
    }
}
