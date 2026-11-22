using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Getting about a version 2 world faster (2026-11-22, Docs/WorldRework.md 광차·길잡이 돌, 묶음 3c): the minecart lines
    // between the stations and the waystones by the camps, where the generator laid them (RoomGraph.Lines, Pois).
    //
    // A station's cart is ridden to the line's other end at its row's speed (pois.csv ride): the rider is taken along
    // the rails, untouched by blows, and gets off at the far station. It is a run, not a jump: the rooms on the way
    // go by. Each line starts broken at its far end (RailLine.Broken); a player there mends it with its row's repair
    // (five iron bars), and from then on it is ridden both ways. What is mended is kept in the world's save.
    //
    // A waystone opens for a player who comes within reach of it (by name, with their waypoints in the character save,
    // as "stone:<biome>"), and from beside an open waypoint or stone they go to any other open one (World.Teleport).
    public sealed partial class World
    {
        // A Rail event's Target: the line mended, a ride begun, a ride ended.
        public const int RailMended = 0;
        public const int RailSet = 1;
        public const int RailArrived = 2;
        // A Waystone event's Target: opened, or come to from another waypoint.
        public const int StoneOpened = 0;
        public const int StoneUsed = 1;
        // A Teleport target at or past this is a waystone: StoneTarget + its index in Pois.
        public const int StoneTarget = 1000;
        // What a waystone is called among a player's waypoints: one to a biome.
        public const string StonePrefix = "stone:";

        IReadOnlyList<RailLine> _lines = Array.Empty<RailLine>();
        bool[] _railFixed = Array.Empty<bool>();
        readonly Dictionary<int, RailLine> _lineOf = new Dictionary<int, RailLine>();
        List<Poi> _stones = new List<Poi>();

        public IReadOnlyList<RailLine> Lines => _lines;

        void SetTravel(RoomGraph rooms)
        {
            _lines = rooms?.Lines ?? (IReadOnlyList<RailLine>)Array.Empty<RailLine>();
            _railFixed = new bool[_lines.Count];
            _lineOf.Clear();
            foreach (var line in _lines)
            {
                _lineOf[line.StationA] = line;
                _lineOf[line.StationB] = line;
            }
            _stones = _pois.Where(p => p.Kind == PoiKind.Waystone).ToList();
        }

        // ---- The minecarts ----

        // The line a station is an end of, or null.
        public RailLine LineOf(Poi station) => station != null && _lineOf.TryGetValue(station.Index, out var line) ? line : null;

        public bool RailFixed(int line) => line >= 0 && line < _railFixed.Length && _railFixed[line];

        // The lines mended, by index, for the save and the clients.
        public IEnumerable<int> FixedRails => Enumerable.Range(0, _railFixed.Length).Where(i => _railFixed[i]);

        // Puts back the lines a save had mended (WorldSave format 14).
        public void RestoreRails(IEnumerable<int> mended)
        {
            foreach (int line in mended ?? Enumerable.Empty<int>())
                if (line >= 0 && line < _railFixed.Length) _railFixed[line] = true;
            PoiVersion++;
        }

        // For the tests and the playtests: a line mended, or a waystone opened for a player, as if they had been there.
        internal void MendRail(int line)
        {
            if (line < 0 || line >= _railFixed.Length) return;
            _railFixed[line] = true;
            PoiVersion++;
        }

        internal void OpenStone(Player player, Poi stone)
        {
            if (player.Waypoints.Add(StoneId(stone.Biome))) PoiVersion++;
        }

        // Everyone riding now, for the clients: who, the line, which way, when the cart set off and how fast.
        public IEnumerable<(int Player, int Line, bool Forward, long Start, float Speed)> Riders =>
            _players.Where(p => p.Riding).Select(p => (p.Id, p.RideLine, p.RideForward, p.RideStart, p.RideSpeed));

        // A player at a station: rides to the other end, or at the broken end of a line not yet mended mends it with
        // what its row asks. False when the line is broken at the other end (it is mended there), or they lack what
        // mends it.
        bool UseStation(Player player, Poi station)
        {
            var line = LineOf(station);
            if (line == null || player.Riding) return false;
            if (!_railFixed[line.Index])
            {
                if (station.Index != line.Broken) return false;
                var repair = station.Definition.Reward(PoiRewardKind.Repair);
                var material = repair == null ? null : Loot?.Materials.FirstOrDefault(m => m.Id == repair.Material);
                if (repair != null)
                {
                    if (material == null || player.Inventory.CountOf(material) < repair.Min) return false;
                    player.Inventory.TakeMaterial(material, repair.Min);
                    player.InventoryVersion++;
                }
                _railFixed[line.Index] = true;
                _events.Add(new SimEvent(SimEventType.Rail, player.Id, RailMended, station.Index, position: station.Center));
                PoiVersion++;
                return true;
            }
            var ride = station.Definition.Reward(PoiRewardKind.Ride);
            player.RideLine = line.Index;
            player.RideForward = station.Index == line.StationA;
            player.RideStart = Tick;
            player.RideSpeed = ride?.Min ?? 16;
            player.Rides++;
            player.Attacking = false;
            EndRoll(player);
            BreakRecall(player);
            EndFishing(player, FishingReeled);
            player.Position = line.RidePoint(player.RideForward, 0);
            _events.Add(new SimEvent(SimEventType.Rail, player.Id, RailSet, station.Index, position: station.Center));
            PoiVersion++;
            return true;
        }

        // How far a rider's cart has gone at the tick, tiles.
        public static float Travelled(long start, float speed, double tick) => (float)Math.Max(0, (tick - start) * speed * TickSeconds);

        // Each tick for a rider: the cart moves on along the rails; at the end the rider gets off at the far station.
        void UpdateRide(Player player)
        {
            if (!player.Riding) return;
            if (player.RideLine >= _lines.Count)
            {
                player.RideLine = -1;
                return;
            }
            var line = _lines[player.RideLine];
            float travelled = Travelled(player.RideStart, player.RideSpeed, Tick);
            if (travelled < line.Length)
            {
                player.Position = line.RidePoint(player.RideForward, travelled);
                return;
            }
            int arrived = player.RideForward ? line.StationB : line.StationA;
            var end = _pois[arrived];
            player.Position = Collision.Resolve(Map, end.Center, PlayerRadius);
            player.RideLine = -1;
            player.Corrections++;
            _events.Add(new SimEvent(SimEventType.Rail, player.Id, RailArrived, arrived, position: end.Center));
            PoiVersion++;
        }

        // ---- The waystones ----

        public IReadOnlyList<Poi> Stones => _stones;

        public static string StoneId(Biome biome) => StonePrefix + biome.ToString().ToLowerInvariant();

        public static bool IsStoneId(string id) => id != null && id.StartsWith(StonePrefix, StringComparison.Ordinal);

        // The waystone within reach of the position, or null.
        public Poi StoneNear(Vec2 position) => StoneNear(_stones, position);

        public static Poi StoneNear(IEnumerable<Poi> stones, Vec2 position)
        {
            Poi best = null;
            float nearest = PoiReach;
            foreach (var stone in stones)
            {
                float distance = Vec2.Distance(position, stone.Center);
                if (distance > nearest) continue;
                (best, nearest) = (stone, distance);
            }
            return best;
        }

        // The waystones open to a player, by index in Pois.
        public IEnumerable<int> OpenStones(Player player) => _stones.Where(s => player.Waypoints.Contains(StoneId(s.Biome))).Select(s => s.Index);

        // Every half second: a waystone a player has come to opens for them.
        void UpdateStones()
        {
            if (_stones.Count == 0 || Tick % (TicksPerSecond / 2) != 0) return;
            foreach (var player in _players)
            {
                if (player.IsDead || player.Riding) continue;
                var stone = StoneNear(player.Position);
                if (stone == null || !player.Waypoints.Add(StoneId(stone.Biome))) continue;
                PoiVersion++;
                _events.Add(new SimEvent(SimEventType.Waystone, player.Id, StoneOpened, stone.Index, position: stone.Center));
            }
        }

        // Whether the player stands by a waypoint or waystone open to them, from which they may go to another.
        bool AtOpenWaypoint(Player player, out PlaceSite place, out Poi stone)
        {
            place = WaypointNear(Places, player.Position);
            stone = StoneNear(player.Position);
            if (place != null && !player.Waypoints.Contains(place.Definition.Id)) place = null;
            if (stone != null && !player.Waypoints.Contains(StoneId(stone.Biome))) stone = null;
            return place != null || stone != null;
        }

        // From beside an open waypoint or waystone to an open waystone: the player stands on the floor below it.
        bool TravelToStone(Player player, int index)
        {
            if (index < 0 || index >= _pois.Count || _pois[index].Kind != PoiKind.Waystone) return false;
            var there = _pois[index];
            if (!player.Waypoints.Contains(StoneId(there.Biome)) || !AtOpenWaypoint(player, out _, out var here) || here == there) return false;
            player.Position = Collision.Resolve(Map, TileMap.Center(there.X, there.Y - 1), PlayerRadius);
            player.Corrections++;
            _events.Add(new SimEvent(SimEventType.Waystone, player.Id, StoneUsed, index, position: player.Position));
            return true;
        }
    }
}
