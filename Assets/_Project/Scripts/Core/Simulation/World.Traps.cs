using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Where a trap room's ambush stands (Docs/WorldRework.md 함정 방, 묶음 4b).
    public enum TrapPhase : byte
    {
        // Its chest waits shut: opening it springs the trap.
        Ready,
        // The doors fell shut; the first band comes in a moment.
        Shut,
        // A band is in the room: Wave says which.
        Fighting,
        // The first band fell; the second comes in a moment.
        Between,
        // Both fell: the doors are up and the chest gave a tier higher. Its chest is opened for good.
        Won,
        // Everyone shut in fell or left: the doors are up, the chest still shut, and the trap ready again the next dawn.
        Lost,
    }

    // A trap room's chest and what its ambush has come to. Kept by the host while the world runs: the chest opened is
    // saved as any chest is (World.OpenedPois); a fight in progress is not (monsters are not), and the doors it shut are
    // saved as they were (World.SavedTiles).
    public sealed class Trap
    {
        internal Trap(Poi chest, RoomNode room)
        {
            Chest = chest;
            Room = room;
        }

        public Poi Chest { get; }
        public RoomNode Room { get; }
        public TrapPhase Phase { get; internal set; }
        // 0 before the first band, then 1 and 2.
        public int Wave { get; internal set; }
        // The day it was lost on (it is ready again the next), or -1.
        public int LostDay { get; internal set; } = -1;
        public bool Active => Phase == TrapPhase.Shut || Phase == TrapPhase.Fighting || Phase == TrapPhase.Between;
        // The players shut in, by id.
        public IReadOnlyList<int> Inside => InsideList;
        // The tiles the doors fell on and what they were.
        public IReadOnlyList<(int X, int Y, TileType Was)> Doors => DoorList;
        internal readonly List<int> InsideList = new List<int>();
        internal readonly List<(int X, int Y, TileType Was)> DoorList = new List<(int, int, TileType)>();
        // The floor shut in, by tile index.
        internal readonly HashSet<int> Floor = new HashSet<int>();
        internal long NextAt;
        internal Pack Band;
    }

    // The trap rooms' ambush (Docs/WorldRework.md 3단계 볼거리 함정 방): opening the chest shuts the room's ways with
    // grates and two bands of the room's biome come one after the other; beating both lifts the grates and the chest
    // gives what a chest a tier higher gives, to each player shut in. Falling or leaving lifts them with the chest
    // still shut, until the next dawn.
    public sealed partial class World
    {
        public const int TrapWaves = 2;
        // The floor shut in: what is walked to from the chest within the room's radius and this much more, or one of
        // TrapReaches more, whichever has the fewest doorways.
        public const float TrapReach = 2.5f;
        public static readonly float[] TrapReaches = { TrapReach, 4, 6, 9, 12 };
        // The doors fall, and TrapFirstSeconds later the first band comes; the second TrapBetweenSeconds after the first
        // fell, one more than the first, each with PackExtraPerPlayer more for each player past the first.
        public const float TrapFirstSeconds = 1.5f;
        public const float TrapBetweenSeconds = 2;
        public const int TrapSecondExtra = 1;
        // A band's monsters come at least this far from everyone shut in.
        public const float TrapSpawnGap = 3.5f;
        // The events' amounts (SimEventType.Trap, at the chest): the doors fell, a band came (TrapWave + its number),
        // won, lost.
        public const int TrapSprung = 0;
        public const int TrapWave = 10;
        public const int TrapWon = 1;
        public const int TrapLost = 2;

        readonly Dictionary<int, Trap> _traps = new Dictionary<int, Trap>();

        public IEnumerable<Trap> Traps => _traps.Values;

        public Trap TrapAt(int poi) => _traps.TryGetValue(poi, out var trap) ? trap : null;

        // The trap chests that cannot be opened now, by index (World.TrapReady), for the clients (PoisMessage).
        public IEnumerable<int> HeldTraps => _traps.Values.Where(t => !TrapReady(t.Chest)).Select(t => t.Chest.Index).OrderBy(i => i);

        // The trap a player is shut in, or null.
        public Trap TrapOf(Player player)
        {
            if (player == null || _traps.Count == 0) return null;
            foreach (var trap in _traps.Values)
                if (trap.Active && trap.InsideList.Contains(player.Id)) return trap;
            return null;
        }

        static bool IsTrapChest(Poi poi) => poi.Kind == PoiKind.Chest && poi.Definition.Role == RoomRole.Trap;

        // Whether a trap's chest may be opened now: not while its fight is on, nor the day it was lost.
        bool TrapReady(Poi poi)
        {
            if (!IsTrapChest(poi) || !_traps.TryGetValue(poi.Index, out var trap)) return true;
            return !trap.Active && trap.LostDay != Today;
        }

        // A player opened a trap's chest (World.UsePoi): the doors fall and the first band is on its way. False when the
        // trap cannot be sprung here (no rooms, no packs to send, no floor round the chest): the chest opens as any.
        bool SpringTrap(Player player, Poi poi)
        {
            // A world with no monsters (the tests' EnemyTarget 0) springs none.
            if (_rooms == null || poi.Room < 0 || poi.Room >= _rooms.Nodes.Count || PackSheetData == null || _config.EnemyTarget <= 0) return false;
            var room = _rooms.Nodes[poi.Room];
            if (PackChoices(room.Center, room.Biome, big: false, loose: true).Count == 0) return false;
            if (!_traps.TryGetValue(poi.Index, out var trap)) _traps[poi.Index] = trap = new Trap(poi, room);
            trap.InsideList.Clear();
            // The cut with the fewest doorways: a cave room's rim is ragged past its radius, and a little further out
            // only its ways go on (2026-11-22: a cave room of radius 17 had 88 doorways at its radius and 2.5).
            float best = TrapReaches[0];
            int fewest = int.MaxValue;
            foreach (float reach in TrapReaches)
            {
                ShutIn(trap, reach);
                if (trap.DoorList.Count < fewest) (best, fewest) = (reach, trap.DoorList.Count);
            }
            ShutIn(trap, best);
            // The room's ways in (B1c, Docs/BiomeRework.md 4-5 함정 방 창살): the narrowest cut just past the room, when
            // that shuts it in with fewer grates than the cut by distance (2026-11-22: up to 86).
            var ways = new HashSet<int>();
            var wayDoors = new List<(int X, int Y)>();
            if (CutByNarrows(Map, room, trap.Chest.X, trap.Chest.Y, ways, wayDoors) && wayDoors.Count < trap.DoorList.Count)
            {
                trap.Floor.Clear();
                trap.Floor.UnionWith(ways);
                trap.DoorList.Clear();
                foreach (var (x, y) in wayDoors) trap.DoorList.Add((x, y, Map[x, y]));
            }
            if (trap.Floor.Count == 0) return false;
            foreach (var other in _players)
                if (!other.IsDead && trap.Floor.Contains(TileIndex(other.Position))) trap.InsideList.Add(other.Id);
            if (!trap.InsideList.Contains(player.Id)) trap.InsideList.Add(player.Id);
            foreach (var (x, y, _) in trap.DoorList)
            {
                SetTile(x, y, TileType.Grate);
                // Whoever stood in the doorway is put inside, a monster pushed off.
                foreach (var other in _players.Where(p => !p.IsDead && Touches(p.Position, PlayerRadius, x, y)))
                {
                    other.Position = Collision.Resolve(Map, other.Position, PlayerRadius);
                    other.Corrections++;
                    if (!trap.InsideList.Contains(other.Id) && trap.Floor.Contains(TileIndex(other.Position))) trap.InsideList.Add(other.Id);
                }
                foreach (var enemy in _enemies.Where(e => !e.IsDead && Touches(e.Position, e.Definition.Radius, x, y)))
                    enemy.Position = Collision.Resolve(Map, enemy.Position, enemy.Definition.Radius, monster: true);
            }
            trap.Phase = TrapPhase.Shut;
            trap.Wave = 0;
            trap.NextAt = Tick + Ticks(TrapFirstSeconds);
            _events.Add(new SimEvent(SimEventType.Trap, player.Id, amount: TrapSprung, position: poi.Center));
            PoiVersion++;
            return true;
        }

        int TileIndex(Vec2 at) => TileMap.Cell(at.Y) * Map.Width + TileMap.Cell(at.X);

        // The floor walked to from the chest within the room's radius and past it, and the floor tiles just past it
        // that touch it: the doorways the grates fall on.
        void ShutIn(Trap trap, float past)
        {
            var doors = new List<(int X, int Y)>();
            CutByReach(Map, trap.Room, trap.Chest.X, trap.Chest.Y, past, trap.Floor, doors);
            trap.DoorList.Clear();
            foreach (var (x, y) in doors) trap.DoorList.Add((x, y, Map[x, y]));
        }

        // The floor walked to from the chest within the room's radius and past it, and the floor tiles just past it that
        // touch it (the doorways the grates fall on), in index order (World.ShutIn, and --traps).
        public static void CutByReach(TileMap map, RoomNode room, int chestX, int chestY, float past, HashSet<int> floor, List<(int X, int Y)> doorTiles)
        {
            floor.Clear();
            doorTiles.Clear();
            float reach = room.Radius + past;
            bool Open(int x, int y) => map.InBounds(x, y) && !map.IsEdge(x, y) && !map.IsSolid(x, y);
            bool Within(int x, int y) => Vec2.Distance(TileMap.Center(x, y), room.Center) <= reach;
            var queue = new Queue<(int X, int Y)>();
            var doors = new HashSet<int>();
            foreach (var (dx, dy) in Around)
            {
                int x = chestX + dx, y = chestY + dy;
                if (Open(x, y) && Within(x, y) && floor.Add(y * map.Width + x)) queue.Enqueue((x, y));
            }
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in Around)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!Open(nx, ny)) continue;
                    int index = ny * map.Width + nx;
                    if (!Within(nx, ny))
                    {
                        doors.Add(index);
                        continue;
                    }
                    if (floor.Add(index)) queue.Enqueue((nx, ny));
                }
            }
            foreach (int index in doors.OrderBy(i => i)) doorTiles.Add((index % map.Width, index / map.Width));
        }

        static readonly (int X, int Y)[] Around = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

        // The narrowest cut is looked for this far past the room's radius (the floor further out is "out"), and a tile
        // further past the radius costs a little more, so of two cuts nearly as narrow the nearer is taken.
        public const float NarrowReach = 16;
        const int NarrowCost = 32;
        const int Unbounded = 1 << 28;

        // The trap room shut in at its narrowest (B1c, Docs/BiomeRework.md 4-5 함정 방 창살: "방의 입구만"): the fewest
        // floor tiles between NarrowReach past the room's radius and its radius whose shutting leaves the floor within its
        // radius walked to from the chest (eight ways) cut off from the floor further out: a minimum vertex cut (each
        // tile's in and out, max flow). Where a ragged cave room's rim is cut by distance in long rows of grates, this
        // finds the mouths of its ways. floor gets the tiles shut in (by index), doors the tiles the grates fall on (by
        // row, then column). False when there is no floor round the chest.
        public static bool CutByNarrows(TileMap map, RoomNode room, int chestX, int chestY, HashSet<int> floor, List<(int X, int Y)> doors)
        {
            floor.Clear();
            doors.Clear();
            if (room == null) return false;
            bool Open(int x, int y) => map.InBounds(x, y) && !map.IsEdge(x, y) && !map.IsSolid(x, y);
            float inner = room.Radius, outer = room.Radius + NarrowReach;
            float Away(int x, int y) => Vec2.Distance(TileMap.Center(x, y), room.Center);
            // The floor within outer walked to from the chest; those touching floor further out lead out.
            var index = new Dictionary<int, int>();
            var tiles = new List<(int X, int Y)>();
            var queue = new Queue<(int X, int Y)>();
            void Add(int x, int y)
            {
                int key = y * map.Width + x;
                if (index.ContainsKey(key)) return;
                index[key] = tiles.Count;
                tiles.Add((x, y));
                queue.Enqueue((x, y));
            }
            foreach (var (dx, dy) in Around)
                if (Open(chestX + dx, chestY + dy) && Away(chestX + dx, chestY + dy) <= outer) Add(chestX + dx, chestY + dy);
            if (tiles.Count == 0) return false;
            var leads = new List<int>();
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                bool lead = false;
                foreach (var (dx, dy) in Around)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!Open(nx, ny)) continue;
                    if (Away(nx, ny) > outer) lead = true;
                    else Add(nx, ny);
                }
                if (lead) leads.Add(index[y * map.Width + x]);
            }
            int n = tiles.Count, source = 2 * n, sink = 2 * n + 1;
            var flow = new Flow(2 * n + 2);
            for (int i = 0; i < n; i++)
            {
                var (x, y) = tiles[i];
                float d = Away(x, y);
                flow.Edge(2 * i, 2 * i + 1, d <= inner ? Unbounded : NarrowCost + (int)(d - inner));
                if (d <= inner) flow.Edge(source, 2 * i, Unbounded);
                foreach (var (dx, dy) in Around)
                    if (index.TryGetValue((y + dy) * map.Width + x + dx, out int j)) flow.Edge(2 * i + 1, 2 * j, Unbounded);
            }
            foreach (int i in leads) flow.Edge(2 * i + 1, sink, Unbounded);
            var cut = new HashSet<int>();
            if (leads.Count > 0 && flow.Max(source, sink) < Unbounded)
            {
                var reached = flow.Reached(source);
                for (int i = 0; i < n; i++)
                    if (reached[2 * i] && !reached[2 * i + 1]) cut.Add(i);
            }
            else if (leads.Count > 0) return false;
            // The floor shut in: walked to from the chest without crossing the cut.
            var seen = new bool[n];
            var walk = new Queue<int>();
            foreach (var (dx, dy) in Around)
                if (index.TryGetValue((chestY + dy) * map.Width + chestX + dx, out int first) && !cut.Contains(first) && !seen[first])
                {
                    seen[first] = true;
                    walk.Enqueue(first);
                }
            while (walk.Count > 0)
            {
                int i = walk.Dequeue();
                var (x, y) = tiles[i];
                floor.Add(y * map.Width + x);
                foreach (var (dx, dy) in Around)
                    if (index.TryGetValue((y + dy) * map.Width + x + dx, out int j) && !seen[j] && !cut.Contains(j))
                    {
                        seen[j] = true;
                        walk.Enqueue(j);
                    }
            }
            foreach (int i in cut.OrderBy(i => tiles[i].Y).ThenBy(i => tiles[i].X)) doors.Add(tiles[i]);
            return floor.Count > 0;
        }

        // A flow network of integer capacities (Dinic's way): the narrowest cut's max flow, and what the source reaches
        // in what is left of it.
        sealed class Flow
        {
            readonly List<int>[] _out;
            readonly List<int> _to = new List<int>();
            readonly List<int> _left = new List<int>();
            int[] _level;
            int[] _next;

            public Flow(int nodes)
            {
                _out = new List<int>[nodes];
                for (int i = 0; i < nodes; i++) _out[i] = new List<int>();
            }

            public void Edge(int from, int to, int capacity)
            {
                _out[from].Add(_to.Count);
                _to.Add(to);
                _left.Add(capacity);
                _out[to].Add(_to.Count);
                _to.Add(from);
                _left.Add(0);
            }

            public long Max(int source, int sink)
            {
                long total = 0;
                while (Levels(source, sink))
                {
                    _next = new int[_out.Length];
                    int pushed;
                    while ((pushed = Push(source, sink, Unbounded)) > 0)
                    {
                        total += pushed;
                        if (total >= Unbounded) return total;
                    }
                }
                return total;
            }

            bool Levels(int source, int sink)
            {
                _level = Enumerable.Repeat(-1, _out.Length).ToArray();
                _level[source] = 0;
                var queue = new Queue<int>();
                queue.Enqueue(source);
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue();
                    foreach (int e in _out[at])
                    {
                        if (_left[e] <= 0 || _level[_to[e]] >= 0) continue;
                        _level[_to[e]] = _level[at] + 1;
                        queue.Enqueue(_to[e]);
                    }
                }
                return _level[sink] >= 0;
            }

            // One way of the levels from at to the sink, pushing at most limit (iterative, the ways are long).
            int Push(int source, int sink, int limit)
            {
                var path = new List<int>();
                int at = source;
                while (true)
                {
                    if (at == sink)
                    {
                        int amount = limit;
                        foreach (int e in path) amount = Math.Min(amount, _left[e]);
                        foreach (int e in path)
                        {
                            _left[e] -= amount;
                            _left[e ^ 1] += amount;
                        }
                        return amount;
                    }
                    bool moved = false;
                    for (; _next[at] < _out[at].Count; _next[at]++)
                    {
                        int e = _out[at][_next[at]];
                        if (_left[e] > 0 && _level[_to[e]] == _level[at] + 1)
                        {
                            path.Add(e);
                            at = _to[e];
                            moved = true;
                            break;
                        }
                    }
                    if (moved) continue;
                    // A dead end: never again this round, and back a step.
                    _level[at] = -1;
                    if (path.Count == 0) return 0;
                    int back = path[path.Count - 1];
                    path.RemoveAt(path.Count - 1);
                    at = _to[back ^ 1];
                    _next[at]++;
                }
            }

            public bool[] Reached(int source)
            {
                var seen = new bool[_out.Length];
                var queue = new Queue<int>();
                seen[source] = true;
                queue.Enqueue(source);
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue();
                    foreach (int e in _out[at])
                    {
                        if (_left[e] <= 0 || seen[_to[e]]) continue;
                        seen[_to[e]] = true;
                        queue.Enqueue(_to[e]);
                    }
                }
                return seen;
            }
        }

        // Each tick: a band comes when its time comes, the next when one fell, the end when the second fell or no one is
        // left inside.
        void UpdateTraps()
        {
            if (_traps.Count == 0) return;
            foreach (var trap in _traps.Values)
            {
                if (!trap.Active) continue;
                if (!trap.InsideList.Any(id => FindPlayer(id) is Player p && !p.IsDead && trap.Floor.Contains(TileIndex(p.Position))))
                {
                    EndTrap(trap, won: false);
                    continue;
                }
                if (trap.Phase == TrapPhase.Fighting)
                {
                    if (_enemies.Any(e => e.PackId == trap.Band.Id && !e.IsDead)) continue;
                    _packs.Remove(trap.Band.Id);
                    if (trap.Wave >= TrapWaves)
                    {
                        EndTrap(trap, won: true);
                        continue;
                    }
                    trap.Phase = TrapPhase.Between;
                    trap.NextAt = Tick + Ticks(TrapBetweenSeconds);
                    continue;
                }
                if (Tick >= trap.NextAt) SendWave(trap);
            }
        }

        // The next band of the room's biome and zone, round the room out of reach of those inside, already after them.
        void SendWave(Trap trap)
        {
            trap.Wave++;
            var room = trap.Room;
            var random = new GameRandom(unchecked(_forageSeed * 0x9E3779B97F4A7C15UL + (ulong)trap.Chest.Index * 0xBF58476D1CE4E5B9UL
                + (ulong)Today * 0x94D049BB133111EBUL + (ulong)trap.Wave), stream: 54);
            var choices = PackChoices(room.Center, room.Biome, big: false, loose: true);
            var definition = random.PickWeighted(choices, k => k.Weight);
            // The second band is another of the room's packs where there is one.
            if (trap.Wave > 1 && choices.Count > 1 && trap.Band?.Definition == definition) definition = random.PickWeighted(choices.Where(k => k != definition).ToList(), k => k.Weight);
            var inside = trap.InsideList.Select(FindPlayer).Where(p => p != null && !p.IsDead).ToList();
            var roster = new List<MonsterDefinition>();
            foreach (var member in definition.Members)
            {
                int count = member.Min + random.Next(member.Max - member.Min + 1);
                for (int i = 0; i < count; i++) roster.Add(member.Monster);
            }
            int extra = PackExtraPerPlayer * Math.Max(0, inside.Count - 1) + (trap.Wave > 1 ? TrapSecondExtra : 0);
            for (int i = 0; i < extra; i++) roster.Add(definition.Members[0].Monster);
            // A trap's band comes out at the grates, where a big body (L, XL) does not fit (World.Sizes.cs).
            LeaveOutBig(roster);
            // Floor tiles shut in, in a seeded order, far enough from everyone inside and apart.
            var floor = trap.Floor.OrderBy(i => i).ToList();
            for (int i = floor.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (floor[i], floor[j]) = (floor[j], floor[i]);
            }
            var spots = new List<Vec2>();
            foreach (float gap in new[] { TrapSpawnGap, 2f })
            {
                foreach (int index in floor)
                {
                    if (spots.Count >= roster.Count) break;
                    var at = TileMap.Center(index % Map.Width, index / Map.Width);
                    if (inside.Any(p => Vec2.Distance(p.Position, at) < gap) || spots.Any(s => Vec2.Distance(s, at) < 1.2f)) continue;
                    if (Map[index % Map.Width, index / Map.Width] == TileType.Water || Map[index % Map.Width, index / Map.Width] == TileType.Lava) continue;
                    if (Collision.Overlaps(Map, at, 0.45f, monster: true)) continue;
                    spots.Add(at);
                }
            }
            int zone = ZoneLevelAt(room.Center);
            var pack = new Pack(_nextPackId++, room, 2 + trap.Wave, room.Center) { Definition = definition, Active = true, Day = Today, Shares = true, Alerted = true };
            _packs[pack.Id] = pack;
            for (int i = 0; i < spots.Count && i < roster.Count; i++)
            {
                var enemy = SpawnEnemy(roster[i], spots[i]);
                ToughenForZone(enemy, zone);
                enemy.PackId = pack.Id;
                enemy.PackSlot = -1;
                enemy.Home = spots[i];
                var target = inside.OrderBy(p => Vec2.Distance(p.Position, spots[i])).FirstOrDefault();
                if (target == null) continue;
                enemy.TargetId = target.Id;
                enemy.State = EnemyState.Chase;
                _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxVanish, position: spots[i]));
            }
            trap.Band = pack;
            trap.Phase = TrapPhase.Fighting;
            _events.Add(new SimEvent(SimEventType.Trap, inside.FirstOrDefault()?.Id ?? 0, amount: TrapWave + trap.Wave, position: trap.Chest.Center));
        }

        // The grates lift. Won: the chest opens and gives each player inside (and any near) what the chest a tier higher
        // gives. Lost: what is left of the band goes in a puff, and the chest waits for the next dawn.
        void EndTrap(Trap trap, bool won)
        {
            foreach (var (x, y, was) in trap.DoorList)
                if (Map[x, y] == TileType.Grate) SetTile(x, y, was);
            if (trap.Band != null)
            {
                foreach (var enemy in _enemies.Where(e => e.PackId == trap.Band.Id && !e.IsDead))
                {
                    _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxVanish, position: enemy.Position));
                    enemy.Health = 0;
                }
                _packs.Remove(trap.Band.Id);
                trap.Band = null;
            }
            var poi = trap.Chest;
            var inside = trap.InsideList.Select(FindPlayer).Where(p => p != null && !p.IsDead).ToList();
            if (won)
            {
                trap.Phase = TrapPhase.Won;
                _poiOpened[poi.Index] = true;
                SetTile(poi.X, poi.Y, TileType.OpenChest);
                var better = UpgradeOf(poi.Definition);
                var owners = _players.Where(p => !p.IsDead && (inside.Contains(p) || Vec2.Distance(p.Position, poi.Center) <= ChestShareReach)).ToList();
                foreach (var owner in owners) Grant(owner, poi, better);
                _events.Add(new SimEvent(SimEventType.Trap, inside.FirstOrDefault()?.Id ?? 0, amount: TrapWon, position: poi.Center));
                _events.Add(new SimEvent(SimEventType.PoiOpened, inside.FirstOrDefault()?.Id ?? 0, amount: poi.Index, position: poi.Center));
            }
            else
            {
                trap.Phase = TrapPhase.Lost;
                trap.LostDay = Today;
                _events.Add(new SimEvent(SimEventType.Trap, 0, amount: TrapLost, position: poi.Center));
            }
            trap.InsideList.Clear();
            PoiVersion++;
        }

        // The chest row a tier above (wood → iron → gold): the treasure room's of the next chest tile, the most likely
        // of them; the same row when there is none higher.
        PoiDefinition UpgradeOf(PoiDefinition definition)
        {
            int Tier(TileType? tile) => tile == TileType.WoodChest ? 0 : tile == TileType.IronChest ? 1 : tile == TileType.GoldChest ? 2 : -1;
            int tier = Tier(definition.Tile);
            var rows = Tiles?.Terrain?.Pois?.Pois ?? (IReadOnlyList<PoiDefinition>)_pois.Select(p => p.Definition).Distinct().ToList();
            return rows.Where(r => r.Role == RoomRole.Treasure && Tier(r.Tile) == tier + 1).OrderByDescending(r => r.Weight).FirstOrDefault() ?? definition;
        }

        // A band's monster that lost sight of whoever is shut in with it (a wide room) walks at the nearest of them.
        bool TrapHuntOn(Enemy enemy)
        {
            if (enemy.PackId == 0 || _traps.Count == 0) return false;
            foreach (var trap in _traps.Values)
            {
                if (!trap.Active || trap.Band == null || trap.Band.Id != enemy.PackId) continue;
                var target = trap.InsideList.Select(FindPlayer).Where(p => p != null && !p.IsDead).OrderBy(p => Vec2.Distance(p.Position, enemy.Position)).FirstOrDefault();
                if (target == null) return false;
                Step(enemy, target.Position, enemy.Speed);
                return true;
            }
            return false;
        }

        // The tiles a save keeps: the grates of a trap still shut go as the floor they fell on (the fight is not kept).
        public List<TileChange> SavedTiles()
        {
            var tiles = ChangedTiles();
            if (_traps.Count == 0) return tiles;
            var was = new Dictionary<(int, int), TileType>();
            foreach (var trap in _traps.Values.Where(t => t.Active))
                foreach (var (x, y, type) in trap.DoorList) was[(x, y)] = type;
            return tiles.Select(t => t.Type == TileType.Grate && was.TryGetValue((t.X, t.Y), out var type) ? new TileChange(t.X, t.Y, type) : t).ToList();
        }
    }
}
