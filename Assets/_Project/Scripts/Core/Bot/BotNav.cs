using System;
using System.Collections.Generic;

namespace Game.Core
{
    // Path finding for the playtest bots (Docs/Playtest.md, 상태로 움직이는 봇, 2026-11-11): over the tiles as the host
    // has them, walking a floor tile for about the ticks a step takes and digging through rock for about the ticks its
    // swings take, when a good enough pickaxe is at hand. Water, lava, the map's edge and anything placed or built are
    // never crossed; neither are the tiles a caller marks (a goal it gave up on). Deterministic: the same map gives the
    // same path, ties going by the order of the four ways.
    public sealed class BotNav
    {
        public static readonly (int X, int Y)[] Ways = { (1, 0), (0, 1), (-1, 0), (0, -1) };

        readonly World _world;
        int[] _cost = Array.Empty<int>();
        int[] _from = Array.Empty<int>();
        int[] _stamp = Array.Empty<int>();
        int _search;
        readonly MinHeap _open = new MinHeap();

        public BotNav(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        // The pickaxe's level (ItemBase.Level) the bot can dig with: rock asking more is not dug; below 0 nothing is.
        public int PickaxeLevel { get; set; } = -1;
        // About how many ticks a step onto the next tile takes, and a swing at rock (with the pickaxe in hand).
        public int WalkTicks { get; set; } = 6;
        public int SwingTicks { get; set; } = 15;
        // How much a swing takes off a rock's hardness.
        public float DigPower { get; set; } = 1;
        // Tiles the path never enters (or ends on), besides what cannot be crossed.
        public Func<int, int, bool> Avoid { get; set; }
        // How many tiles one search may look at before it gives up (a 200×200 world is 40 000).
        public int Budget { get; set; } = 30000;
        // Tiles looked at by the last search.
        public int Searched { get; private set; }
        // The rooms of a world of rooms (GeneratedWorld.Rooms, generator version 2), or null: a far goal is then gone to
        // room by room (Toward), not by one search across the whole map.
        public RoomGraph Rooms { get; set; }
        // Further than this (tiles, along both axes) a goal is far.
        public int FarTiles { get; set; } = 64;
        // How far (tiles) from the bot the room it heads for next may be, along the rooms toward a far goal.
        public int LegTiles { get; set; } = 48;
        // After Toward, about how many tiles are left from the end of the path it gave to the goal (0 when the path
        // goes all the way): a bot counts it as getting on when this and the path together shrink.
        public int LastRest { get; private set; }

        // Sets the costs from the player: their speed, their swing and the best pickaxe they carry.
        public void Fit(Player player)
        {
            float speed = Math.Max(0.5f, player.MoveSpeed);
            WalkTicks = Math.Max(1, (int)MathF.Round(World.TicksPerSecond / speed));
            SwingTicks = Math.Max(1, (int)MathF.Round(World.TicksPerSecond / Math.Max(0.1f, player.Stats.AttacksPerSecond)));
            DigPower = Math.Max(0.1f, World.DigPowerOf(player));
            int best = -1;
            foreach (var item in player.Equipment.Items) if (item.Base.IsPickaxe) best = Math.Max(best, item.Base.Level);
            foreach (var item in player.Inventory.Items) if (item.Base.IsPickaxe) best = Math.Max(best, item.Base.Level);
            PickaxeLevel = best;
        }

        // Each tile type's row of tiles.csv (null for one with none), looked up once: a search asks for every tile it
        // looks at, and on a map of rooms it looks at a great many (2026-11-21).
        TileDefinition[] _definitions;

        TileDefinition Definition(TileType type)
        {
            if (_definitions == null)
            {
                _definitions = new TileDefinition[256];
                if (_world.Tiles != null)
                    foreach (var tile in _world.Tiles.Tiles) _definitions[(int)tile.Tile] ??= tile;
            }
            return _definitions[(int)type];
        }

        public bool Walkable(int x, int y)
        {
            var map = _world.Map;
            if (!map.InBounds(x, y) || map.IsEdge(x, y)) return false;
            var type = map[x, y];
            return type != TileType.Lava && type != TileType.Water && !map.IsSolid(x, y);
        }

        // Rock (walls and ore veins) that the bot's pickaxe breaks; not the walls of the story's places, nor anything placed.
        public bool Diggable(int x, int y)
        {
            var map = _world.Map;
            if (PickaxeLevel < 0 || !map.InBounds(x, y) || map.IsEdge(x, y)) return false;
            var tile = Definition(map[x, y]);
            return tile != null && (tile.Tile == TileType.Wall || tile.Tile == TileType.IronWall || tile.Drillable || TileMap.IsRoomWall(tile.Tile)) && tile.Pickaxe <= PickaxeLevel;
        }

        // The ticks it takes to stand on the tile from beside it: a step, and before that the swings that break it.
        // Below 0 when it cannot be done.
        public int CostOf(int x, int y)
        {
            if (Avoid != null && Avoid(x, y)) return -1;
            if (Walkable(x, y)) return WalkTicks;
            if (!Diggable(x, y)) return -1;
            var tile = Definition(_world.Map[x, y]);
            int swings = Math.Max(1, (int)MathF.Ceiling(tile.Hardness / DigPower - 1e-4f));
            return WalkTicks + swings * SwingTicks;
        }

        // The cheapest way from (x, y) to the first tile that passes `goal` (by the ticks it takes), the tiles in order
        // after the start, the goal last; empty when standing on one, null when none is in reach within Budget. With
        // `enter` false the goal tile itself is not walked onto, so it may be anything (a workbench, water): the path
        // ends beside it, and the last tile of the path is the goal. `heuristic` (ticks still to go, never more than
        // the real cost) makes it an A* toward one goal.
        public List<(int X, int Y)> Find(int x, int y, Func<int, int, bool> goal, bool enter = true, Func<int, int, int> heuristic = null)
        {
            LastRest = 0;
            var map = _world.Map;
            int size = map.Width * map.Height;
            if (_cost.Length != size)
            {
                _cost = new int[size];
                _from = new int[size];
                _stamp = new int[size];
                _search = 0;
            }
            _search++;
            Searched = 0;
            _open.Clear();
            if (!map.InBounds(x, y)) return null;
            int start = y * map.Width + x;
            Visit(start, 0, -1);
            _open.Push(start, heuristic?.Invoke(x, y) ?? 0);
            while (_open.Count > 0)
            {
                int at = _open.Pop(out int priority);
                int ax = at % map.Width, ay = at / map.Width;
                int cost = _cost[at];
                if (priority - (heuristic?.Invoke(ax, ay) ?? 0) > cost) continue;
                if (goal(ax, ay)) return Trace(at);
                // Toward one goal (A*) the search stays near the straight way, so it may look further (a 512-tile world).
                if (++Searched > (heuristic != null ? Budget * 8 : Budget)) return null;
                foreach (var (dx, dy) in Ways)
                {
                    int nx = ax + dx, ny = ay + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    int next = ny * map.Width + nx;
                    int step;
                    if (!enter && goal(nx, ny))
                    {
                        // Beside it is enough: the goal is not walked onto, only reached.
                        if (Avoid != null && Avoid(nx, ny)) continue;
                        step = 1;
                    }
                    else
                    {
                        step = CostOf(nx, ny);
                        if (step < 0) continue;
                    }
                    int total = cost + step;
                    if (_stamp[next] == _search && _cost[next] <= total) continue;
                    Visit(next, total, at);
                    _open.Push(next, total + (heuristic?.Invoke(nx, ny) ?? 0));
                }
            }
            return null;
        }

        // A* to one tile (walked onto, or with `enter` false reached beside).
        public List<(int X, int Y)> To(int x, int y, int tx, int ty, bool enter = true) =>
            Find(x, y, (gx, gy) => gx == tx && gy == ty, enter, (hx, hy) => (Math.Abs(hx - tx) + Math.Abs(hy - ty) - (enter ? 0 : 1)) * WalkTicks);

        // Toward one tile: near, or with no rooms, as To; far on a map of rooms (2026-11-22, Docs/WorldRework.md 묶음 1c:
        // one A* across a map of rooms looked at a quarter of a million tiles), along the rooms from the one nearest the
        // bot to the one nearest the goal (Route), the path going only as far as the furthest room on that way within
        // LegTiles. The way and the room headed for are kept while the goal stays the same, and the next room is only
        // chosen once the bot is at that one, so a bot does not turn back and forth between two ways as the room nearest
        // it changes. A room with no path to it is left out of the way for that goal. Falls back on To when the rooms
        // give no way.
        public List<(int X, int Y)> Toward(int x, int y, int tx, int ty, bool enter = true)
        {
            if (Rooms == null || Math.Abs(tx - x) <= FarTiles && Math.Abs(ty - y) <= FarTiles) return To(x, y, tx, ty, enter);
            if (_legGoal != (tx, ty))
            {
                _legGoal = (tx, ty);
                _legRoute = null;
                _badRooms.Clear();
            }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (_legRoute != null)
                {
                    var (wx, wy) = Anchor(_legRoute[_leg]);
                    int off = Math.Max(Math.Abs(wx - x), Math.Abs(wy - y));
                    // Knocked or led far off the way: it is found again.
                    if (off > 2 * LegTiles) _legRoute = null;
                    // There: on to the furthest room of the way within a leg.
                    else if (off <= 2) _leg = Math.Max(_leg + 1, Furthest(_legRoute, x, y, _leg + 1));
                }
                if (_legRoute == null)
                {
                    _legRoute = Route(x, y, tx, ty);
                    if (_legRoute == null) break;
                    _leg = Math.Max(1, Furthest(_legRoute, x, y, 1));
                }
                // The last room of the way is the goal's: from there it is gone to straight.
                if (_leg >= _legRoute.Count - 1) break;
                var room = _legRoute[_leg];
                var (ax, ay) = Anchor(room);
                var path = To(x, y, ax, ay);
                if (path != null && path.Count > 0)
                {
                    int rest = 0;
                    for (int i = _leg; i + 1 < _legRoute.Count; i++)
                    {
                        var (bx, by) = Anchor(_legRoute[i]);
                        var (cx, cy) = Anchor(_legRoute[i + 1]);
                        rest += Math.Abs(cx - bx) + Math.Abs(cy - by);
                    }
                    var (lx, ly) = Anchor(_legRoute[_legRoute.Count - 1]);
                    LastRest = rest + Math.Abs(tx - lx) + Math.Abs(ty - ly);
                    return path;
                }
                if (path != null && path.Count == 0) continue;
                // No way to that room: the way is found again without it.
                _badRooms.Add(room.Index);
                _legRoute = null;
            }
            return To(x, y, tx, ty, enter);
        }

        (int X, int Y)? _legGoal;
        List<RoomNode> _legRoute;
        int _leg;
        readonly HashSet<int> _badRooms = new HashSet<int>();

        // The furthest room of the way, from the index on, within LegTiles of (x, y) (the last room not counted); -1 for none.
        int Furthest(List<RoomNode> route, int x, int y, int from)
        {
            int leg = -1;
            for (int i = from; i < route.Count - 1; i++)
            {
                var (ax, ay) = Anchor(route[i]);
                if (Math.Abs(ax - x) <= LegTiles && Math.Abs(ay - y) <= LegTiles) leg = i;
            }
            return leg;
        }

        (int X, int Y) Anchor(RoomNode room) => (room.AnchorX, room.AnchorY);

        // The rooms from the one nearest (x, y) to the one nearest (tx, ty), fewest tiles between their middles first
        // (a pack's room counted three times),
        // by the ways the bot can take: not through a cracked wall it cannot dig, not into an arena but the goal's,
        // not to a room it has given up on. Null when there is none.
        public List<RoomNode> Route(int x, int y, int tx, int ty)
        {
            const int PackRoomCost = 3;
            if (Rooms == null || Rooms.Nodes.Count == 0) return null;
            var start = Rooms.Nearest(x, y);
            var end = Rooms.Nearest(tx, ty);
            bool cracked = Diggable(TileType.CrackedWall);
            int count = Rooms.Nodes.Count;
            var cost = new int[count];
            var came = new int[count];
            for (int i = 0; i < count; i++)
            {
                cost[i] = int.MaxValue;
                came[i] = -1;
            }
            var open = new SortedSet<(int Cost, int Node)>();
            cost[start.Index] = 0;
            open.Add((0, start.Index));
            while (open.Count > 0)
            {
                var (here, at) = open.Min;
                open.Remove(open.Min);
                if (at == end.Index) break;
                var node = Rooms.Nodes[at];
                foreach (int way in node.Edges)
                {
                    var edge = Rooms.Edges[way];
                    if (edge.Kind == RoomEdgeKind.Cracked && !cracked) continue;
                    var next = Rooms.Nodes[edge.A == at ? edge.B : edge.A];
                    if (next.Kind == RoomKind.Arena && next != end) continue;
                    if (next != end && (Avoid != null && Avoid(next.AnchorX, next.AnchorY) || _badRooms.Contains(next.Index))) continue;
                    int step = (int)Math.Sqrt((double)(next.X - node.X) * (next.X - node.X) + (double)(next.Y - node.Y) * (next.Y - node.Y)) + 1;
                    // Through a pack's room only when the way round is much longer: a player on their way somewhere
                    // keeps to the roads and the empty rooms (2026-11-22, Docs/WorldRework.md 묶음 2-1).
                    if (next != end && (next.Role == RoomRole.Pack || next.Role == RoomRole.Elite)) step *= PackRoomCost;
                    int total = here + step;
                    if (total >= cost[next.Index]) continue;
                    if (cost[next.Index] != int.MaxValue) open.Remove((cost[next.Index], next.Index));
                    cost[next.Index] = total;
                    came[next.Index] = at;
                    open.Add((total, next.Index));
                }
            }
            if (cost[end.Index] == int.MaxValue) return null;
            var route = new List<RoomNode>();
            for (int at = end.Index; at >= 0; at = came[at]) route.Add(Rooms.Nodes[at]);
            route.Reverse();
            return route;
        }

        // Whether the bot's pickaxe breaks a tile of the type (its row of tiles.csv).
        bool Diggable(TileType type)
        {
            var tile = Definition(type);
            return PickaxeLevel >= 0 && tile != null && tile.Pickaxe <= PickaxeLevel;
        }

        // The ticks of the last path found to the tile at its end.
        public int CostTo((int X, int Y) tile)
        {
            int at = tile.Y * _world.Map.Width + tile.X;
            return at >= 0 && at < _stamp.Length && _stamp[at] == _search ? _cost[at] : -1;
        }

        void Visit(int at, int cost, int from)
        {
            _stamp[at] = _search;
            _cost[at] = cost;
            _from[at] = from;
        }

        List<(int X, int Y)> Trace(int at)
        {
            int width = _world.Map.Width;
            var path = new List<(int X, int Y)>();
            while (_from[at] >= 0)
            {
                path.Add((at % width, at / width));
                at = _from[at];
            }
            path.Reverse();
            return path;
        }

        // A binary heap of tile indexes by priority; ties come out in the order they went in, so it stays deterministic.
        sealed class MinHeap
        {
            readonly List<(int Priority, long Order, int Value)> _items = new List<(int, long, int)>();
            long _order;

            public int Count => _items.Count;

            public void Clear()
            {
                _items.Clear();
                _order = 0;
            }

            public void Push(int value, int priority)
            {
                _items.Add((priority, _order++, value));
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (!Less(_items[i], _items[parent])) break;
                    (_items[i], _items[parent]) = (_items[parent], _items[i]);
                    i = parent;
                }
            }

            public int Pop(out int priority)
            {
                var top = _items[0];
                priority = top.Priority;
                var last = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);
                if (_items.Count > 0)
                {
                    _items[0] = last;
                    int i = 0;
                    while (true)
                    {
                        int left = 2 * i + 1, right = left + 1, least = i;
                        if (left < _items.Count && Less(_items[left], _items[least])) least = left;
                        if (right < _items.Count && Less(_items[right], _items[least])) least = right;
                        if (least == i) break;
                        (_items[i], _items[least]) = (_items[least], _items[i]);
                        i = least;
                    }
                }
                return top.Value;
            }

            static bool Less((int Priority, long Order, int Value) a, (int Priority, long Order, int Value) b) =>
                a.Priority < b.Priority || a.Priority == b.Priority && a.Order < b.Order;
        }
    }
}
