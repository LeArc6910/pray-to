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
        static readonly float[] TrapReaches = { TrapReach, 4, 6, 9, 12 };
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
            if (PackChoices(room.Center, room.Biome, big: false).Count == 0) return false;
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
            trap.Floor.Clear();
            trap.DoorList.Clear();
            var room = trap.Room;
            float reach = room.Radius + past;
            bool Open(int x, int y) => Map.InBounds(x, y) && !Map.IsEdge(x, y) && !Map.IsSolid(x, y);
            bool Within(int x, int y) => Vec2.Distance(TileMap.Center(x, y), room.Center) <= reach;
            var queue = new Queue<(int X, int Y)>();
            var doors = new HashSet<int>();
            foreach (var (dx, dy) in Around)
            {
                int x = trap.Chest.X + dx, y = trap.Chest.Y + dy;
                if (Open(x, y) && Within(x, y) && trap.Floor.Add(y * Map.Width + x)) queue.Enqueue((x, y));
            }
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in Around)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!Open(nx, ny)) continue;
                    int index = ny * Map.Width + nx;
                    if (!Within(nx, ny))
                    {
                        doors.Add(index);
                        continue;
                    }
                    if (trap.Floor.Add(index)) queue.Enqueue((nx, ny));
                }
            }
            foreach (int index in doors.OrderBy(i => i))
                trap.DoorList.Add((index % Map.Width, index / Map.Width, Map[index % Map.Width, index / Map.Width]));
        }

        static readonly (int X, int Y)[] Around = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

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
            var choices = PackChoices(room.Center, room.Biome, big: false);
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
