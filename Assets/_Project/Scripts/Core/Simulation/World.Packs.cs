using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A pack of monsters (Docs/WorldRework.md 2단계 무리,묶음 2-1): one in a pack room of a world of rooms (two in a big
    // room), or, in a world of caves (the generator's version 1), a clump come round the players.
    public sealed class Pack
    {
        internal Pack(int id, RoomNode room, int slot, Vec2 home)
        {
            Id = id;
            Room = room;
            Slot = slot;
            Home = home;
        }

        public int Id { get; }
        // Its room, or null for a clump of a world of caves.
        public RoomNode Room { get; }
        // Which of its room's packs it is (0, or 1 in a big room).
        public int Slot { get; }
        // Its room's middle, or the middle of the clump: the leash is measured from here.
        public Vec2 Home { get; internal set; }
        public PackDefinition Definition { get; internal set; }
        // Its monsters out in the world now (some may have fallen since).
        public bool Active { get; internal set; }
        // It has noticed someone since it came.
        public bool Alerted { get; internal set; }
        // The day its roster was made for (Calendar.Day): the same all day, made again the next.
        public int Day { get; internal set; }
        internal readonly List<MonsterDefinition> Roster = new List<MonsterDefinition>();
        internal readonly List<Vec2> Spots = new List<Vec2>();
        // Which of the roster fell today: those do not come again until the next day's dawn.
        internal bool[] Fallen = Array.Empty<bool>();
        // Its monsters give their share of experience and drops (monsters.csv reward, World.RewardShare): a room's pack,
        // which brings more monsters than the field had. Not a clump of a world of caves, which keeps the caves' cap.
        public bool Shares { get; internal set; }
        // A camp's elites (World.Elites.cs): a leader first in its roster, or every one of it a champion; and their
        // affixes. None for an ordinary pack.
        public EliteRank Elite { get; internal set; }
        public RiftAffix Affixes { get; internal set; }
        // The players a dungeon room's pack was made for (World.WakeRoom, B5a): its monsters are as much tougher and
        // worth as much more as World.DungeonParty says. 1 everywhere else.
        public int Players { get; internal set; } = 1;
        // A hunting ground's (World.Hunt.cs): no roster, its monsters keep coming while someone is there.
        public bool Hunt { get; internal set; }
        internal long NextRefill;
        public int Size => Roster.Count;
        public int FallenCount => Fallen.Count(f => f);
        // Every one of today's roster fell: the room is empty until dawn.
        public bool Cleared => Roster.Count > 0 && Fallen.All(f => f);
    }

    // Packs in their rooms: they come when someone is near and go when everyone has gone, notice a player together,
    // are held to their room by a leash, and come back the next dawn once felled. Wanderers walk between the rooms
    // with no pack. The caps on monsters go by parties of players. See Docs/WorldRework.md 무리, 상한.
    public sealed partial class World
    {
        // A pack room's packs come when a player is this near its middle and go when every player is further than
        // PackSleepReach (Docs/WorldRework.md 무리).
        public const float PackWakeReach = 48;
        public const float PackSleepReach = 80;
        // A pack's monster drawn further than this from its room's middle turns back and walks home the way it came,
        // healing this share of its health a second (줄).
        public const float PackLeash = 30;
        public const float ReturnHealPercent = 10;
        // When one of a pack notices a player, the rest have within this long; wanderers this near join them.
        public const float PackAlertSeconds = 0.3f;
        public const float WandererJoinReach = 10;
        // Players this near each other are a party, which has its own cap on monsters; the host has another (상한).
        public const float PartyReach = 30;
        public const int PartyCap = 48;
        public const int HostCap = 120;
        // More monsters for each player of the party past the first, and at night (two under a full moon), in each pack.
        public const int PackExtraPerPlayer = 2;
        public const int PackExtraAtNight = 1;
        // Nothing comes into being this near a road or a camp (길·쉼터의 밀도 0), nor a pack this near a room's way in.
        public const float RoadClearance = 6;
        public const float EntranceClearance = 3;
        // A room this wide (its radius in tiles) holds two packs.
        public const int BigRoomRadius = 15;
        // Wanderers: about this share of the monsters about, at least and at most so many to a party; half again at
        // night and twice under a full moon. They walk at this share of their speed.
        public const float WandererShare = 0.15f;
        public const int MinWanderers = 2;
        public const int MaxWanderers = 8;
        public const float WanderPace = 0.5f;
        // A pack's monsters stand this far apart.
        public const float PackSpacingMin = 1.5f;
        public const float PackSpacingMax = 3;
        // A monster that left home leaves a point of its way every TrailStep tiles, at most TrailMost of them.
        public const float TrailStep = 2;
        const int TrailMost = 120;
        // An idle pack monster further than this from where it stood walks back.
        const float StrayReach = 6;
        const int PackCheckTicks = 15;

        RoomGraph _rooms;
        readonly Dictionary<int, Pack> _packs = new Dictionary<int, Pack>();
        // A room's packs by room index × 2 + slot, kept while the world runs (never saved: monsters are not).
        readonly Dictionary<int, Pack> _roomPacks = new Dictionary<int, Pack>();
        List<RoomNode> _packRooms = new List<RoomNode>();
        List<RoomNode> _restRooms = new List<RoomNode>();
        readonly Dictionary<int, List<Vec2>> _entrances = new Dictionary<int, List<Vec2>>();
        int _nextPackId = 1;

        public IReadOnlyCollection<Pack> Packs => _packs.Values;

        public Pack PackOf(Enemy enemy) => enemy != null && enemy.PackId != 0 && _packs.TryGetValue(enemy.PackId, out var pack) ? pack : null;

        public IEnumerable<Enemy> MembersOf(Pack pack) => _enemies.Where(e => e.PackId == pack.Id && !e.IsDead);

        // The rooms of a world of rooms (GeneratedWorld.Rooms, the generator's version 2): their pack rooms hold the
        // packs of packs.csv (MonsterData.Packs) and the others the wanderers. Null for a world of caves. The monsters
        // that came before (World's constructor spreads EnemyTarget over a small map) go.
        public void SetRooms(RoomGraph rooms)
        {
            _rooms = rooms;
            // Not the room before a boss's arena: the way in to a boss is quiet (2026-11-22: the first-hour bot met two
            // packs of the ice caves at the warden's door and fell there five times).
            _packRooms = rooms?.Nodes.Where(n => n.Kind == RoomKind.Room && !n.Roadside && (n.Role == RoomRole.Pack || n.Role == RoomRole.Elite)
                    && !n.Edges.Any(i => rooms.Nodes[rooms.Edges[i].A == n.Index ? rooms.Edges[i].B : rooms.Edges[i].A].Kind == RoomKind.Arena)).ToList()
                ?? new List<RoomNode>();
            _restRooms = rooms?.Nodes.Where(n => n.Role == RoomRole.Rest).ToList() ?? new List<RoomNode>();
            // The hunting grounds (version 8) keep their own monsters, not a pack of the day's.
            SetHunts(rooms);
            _packRooms.RemoveAll(n => rooms.IsHuntingGround(n.Index));
            // The camps, shrines, chests and carts in them (World.Pois.cs).
            SetPois(rooms);
            // The dungeons' gates in their gate rooms (World.Dungeon.cs).
            SetGates(rooms);
            _entrances.Clear();
            if (RoomPacks) _enemies.RemoveAll(e => e.Rift == 0);
        }

        public RoomGraph Rooms => _rooms;

        PackData PackSheetData => _monsters.Packs != null && _monsters.Packs.Packs.Count > 0 ? _monsters.Packs : null;

        // Monsters live in their rooms' packs and wander between (a world of rooms with packs.csv)...
        bool RoomPacks => _rooms != null && PackSheetData != null && _config.EnemyTarget > 0;

        // ... or come round the players as clumps (a big world of caves with packs.csv), or one by one as they always did.
        bool ClumpPacks => _rooms == null && PackSheetData != null && SpawnsNearPlayers;

        static bool BigRoom(RoomNode room) => room.Radius >= BigRoomRadius;

        // A big room holds two packs from SecondPackZone on; before, one (2026-11-22: two packs of the sand caves at
        // once on the first boss's way were twelve monsters at a level 5 bot in copper, dead in six seconds).
        public const int SecondPackZone = 3;

        // A camp of elites holds its band alone.
        int Slots(RoomNode room) => room.Role != RoomRole.Elite && BigRoom(room) && ZoneLevelAt(room.Center) >= SecondPackZone ? 2 : 1;

        // ---- The parties and the caps (상한) ----

        // The living players in parties: each within PartyReach of another of its party.
        List<List<Player>> Parties()
        {
            var alive = _players.Where(p => !p.IsDead).ToList();
            var parties = new List<List<Player>>();
            var placed = new HashSet<Player>();
            foreach (var first in alive)
            {
                if (!placed.Add(first)) continue;
                var party = new List<Player> { first };
                for (int i = 0; i < party.Count; i++)
                {
                    foreach (var other in alive)
                    {
                        if (placed.Contains(other) || Vec2.Distance(other.Position, party[i].Position) > PartyReach) continue;
                        placed.Add(other);
                        party.Add(other);
                    }
                }
                parties.Add(party);
            }
            return parties;
        }

        // How many field monsters (not a rift's or a defense's) are nearest each party's players.
        int[] PartyCounts(List<List<Player>> parties, Func<Enemy, bool> which = null)
        {
            var counts = new int[parties.Count];
            if (parties.Count == 0) return counts;
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || enemy.Rift != 0 || which != null && !which(enemy)) continue;
                int best = 0;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < parties.Count; i++)
                {
                    foreach (var player in parties[i])
                    {
                        float distance = (player.Position - enemy.Position).LengthSquared;
                        if (distance < bestDistance) (best, bestDistance) = (i, distance);
                    }
                }
                counts[best]++;
            }
            return counts;
        }

        int FieldCount => _enemies.Count(e => !e.IsDead && e.Rift == 0);

        static int PartyOf(List<List<Player>> parties, Vec2 at)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < parties.Count; i++)
            {
                foreach (var player in parties[i])
                {
                    float distance = (player.Position - at).LengthSquared;
                    if (distance < bestDistance) (best, bestDistance) = (i, distance);
                }
            }
            return best;
        }

        // What night adds to each pack.
        int NightExtra => !Calendar.IsNight(Clock) ? 0 : FullMoonNight ? 2 * PackExtraAtNight : PackExtraAtNight;

        // ---- Packs coming and going ----

        // Every PackCheckTicks: a pack whose room everyone has left goes, one all of whose roster fell is done for the
        // day, and the pack rooms near the players fill, nearest first, while their parties' caps and the host's allow.
        void UpdatePacks()
        {
            if (Tick % PackCheckTicks != 0) return;
            foreach (var pack in _packs.Values.ToList())
            {
                if (pack.Hunt) continue;
                if (pack.Room == null)
                {
                    // A clump of a world of caves is gone with the last of it.
                    if (!_enemies.Any(e => e.PackId == pack.Id && !e.IsDead)) _packs.Remove(pack.Id);
                    continue;
                }
                if (!pack.Active) continue;
                if (_players.All(p => Vec2.Distance(p.Position, pack.Home) > PackSleepReach)) Sleep(pack);
                else if (!_enemies.Any(e => e.PackId == pack.Id && !e.IsDead)) pack.Active = false;
            }
            if (!RoomPacks) return;
            var parties = Parties();
            if (parties.Count == 0) return;
            var counts = PartyCounts(parties);
            int total = FieldCount;
            var near = _packRooms
                .Select(room => (room, distance: parties.SelectMany(p => p).Min(p => Vec2.Distance(p.Position, room.Center))))
                .Where(r => r.distance <= PackWakeReach)
                .OrderBy(r => r.distance);
            foreach (var (room, _) in near)
            {
                for (int slot = 0; slot < Slots(room); slot++)
                {
                    var pack = RoomPack(room, slot);
                    if (pack.Active || pack.Definition == null || pack.Cleared) continue;
                    int party = PartyOf(parties, room.Center);
                    int extra = PackExtraPerPlayer * (parties[party].Count - 1) + NightExtra;
                    int coming = pack.Size - pack.FallenCount + extra;
                    if (counts[party] + coming > PartyCap || total + coming > HostCap) continue;
                    Wake(pack, extra);
                    counts[party] += coming;
                    total += coming;
                }
            }
        }

        // A room's pack, its roster made for today (internal for the tests).
        internal Pack RoomPack(RoomNode room, int slot)
        {
            int key = room.Index * 2 + slot;
            if (!_roomPacks.TryGetValue(key, out var pack))
            {
                pack = new Pack(_nextPackId++, room, slot, room.Center) { Day = -1, Shares = true };
                _roomPacks[key] = pack;
                _packs[pack.Id] = pack;
            }
            if (pack.Day != Today && !pack.Active) Muster(pack);
            return pack;
        }

        // The same seed, room and day make the same pack in the same places: a room come back to on the same day holds
        // what it held, less what fell (Docs/WorldRework.md 무리).
        GameRandom PackRandom(RoomNode room, int slot, int day) =>
            new GameRandom(unchecked(_forageSeed * 0x9E3779B97F4A7C15UL + (ulong)room.Index * 0xBF58476D1CE4E5B9UL + (ulong)slot * 0x94D049BB133111EBUL
                + (ulong)day * 0xD6E8FEB86659FD93UL), stream: 11);

        void Muster(Pack pack)
        {
            var room = pack.Room;
            pack.Day = Today;
            pack.Roster.Clear();
            pack.Spots.Clear();
            pack.Definition = null;
            pack.Elite = EliteRank.None;
            pack.Affixes = RiftAffix.None;
            pack.Fallen = Array.Empty<bool>();
            if (InSafePlace(room.Center)) return;
            var random = PackRandom(room, pack.Slot, pack.Day);
            var choices = PackChoices(room.Center, room.Biome, BigRoom(room), room);
            if (choices.Count == 0) return;
            pack.Definition = random.PickWeighted(choices, p => p.Weight);
            // A mid-boss's lair (B1c): the biome's mid-boss and a few of its pack, each day anew.
            if (pack.Slot == 0 && _rooms != null && _rooms.Lairs.Contains(room.Index) && MusterLair(pack, random)) return;
            // A camp of elites (묶음 2-2): its leader's pack or its champions, each day anew.
            if (room.Role == RoomRole.Elite && pack.Slot == 0 && _rooms != null)
            {
                MusterElite(pack, random);
                while (pack.Roster.Count > pack.Spots.Count) pack.Roster.RemoveAt(pack.Roster.Count - 1);
                pack.Fallen = new bool[pack.Roster.Count];
                if (pack.Roster.Count == 0) pack.Definition = null;
                return;
            }
            foreach (var member in pack.Definition.Members)
            {
                int count = member.Min + random.Next(member.Max - member.Min + 1);
                for (int i = 0; i < count; i++) pack.Roster.Add(member.Monster);
            }
            // Two packs in a big room stand apart: the second round a spot halfway out.
            var middle = pack.Slot == 0 ? TileMap.Center(room.AnchorX, room.AnchorY) : room.Center + Vec2.FromAngle(random.NextFloat() * 2 * MathF.PI) * (room.Radius * 0.55f);
            var marks = pack.Slot == 0 && _rooms != null
                ? _rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.Monster).Select(m => TileMap.Center(m.X, m.Y)).ToList()
                : new List<Vec2>();
            bool Fits(Vec2 at) => InRoom(room, at) && Clear(at) && !NearEntrance(room, at);
            // The big bodies (L, XL) first, where they fit; the rest round them (World.PlaceRoster).
            var (placed, waiting) = PlaceRoster(pack.Roster.ToList(), middle, room.Radius, Fits, (count, away) => Spread(middle, marks, count, random, at => Fits(at) && away(at)));
            pack.Roster.Clear();
            foreach (var (kind, spot) in placed)
            {
                pack.Roster.Add(kind);
                pack.Spots.Add(spot);
            }
            pack.Roster.AddRange(waiting);
            Crowd(pack, middle, random);
            // A room too cramped for all of them keeps those that found a spot.
            while (pack.Roster.Count > pack.Spots.Count) pack.Roster.RemoveAt(pack.Roster.Count - 1);
            pack.Fallen = new bool[pack.Roster.Count];
            if (pack.Roster.Count == 0) pack.Definition = null;
        }

        // The packs of packs.csv that come there: round a place some monsters keep to, its own packs; else the biome's
        // own from their zone, else those of the plain cave. Not a zone early as a biome's monsters come one by one
        // (World.Comes): a whole pack of a later biome on the first boss's way was too much (2026-11-22, --hour).
        // In a room, only the packs whose big bodies it holds (World.RoomHolds: L in a room of radius 5 or more, XL where
        // 7×7 of floor is open).
        // Out of a room (loose: the director's surges and ambushes, a trap's waves, a world of caves' clumps), only the
        // packs with no big body, so a band is what the pack always was rather than a pack with its big one left out.
        List<PackDefinition> PackChoices(Vec2 at, Biome biome, bool big, RoomNode room = null, bool loose = false)
        {
            var packs = PackSheetData?.Packs;
            if (packs == null) return new List<PackDefinition>();
            if (room != null) packs = packs.Where(k => k.Members.All(m => RoomHolds(room, m.Monster))).ToList();
            if (loose) packs = packs.Where(k => k.Members.All(m => !m.Monster.Big)).ToList();
            var haunt = Places.FirstOrDefault(p => Vec2.Distance(at, p.Center) <= p.Radius + HauntReach && packs.Any(k => k.Place == p.Definition.Id && k.Weight > 0));
            if (haunt != null) return packs.Where(k => k.Place == haunt.Definition.Id && k.Weight > 0 && (big || !k.Big)).ToList();
            int zone = ZoneLevelAt(at);
            var own = packs.Where(k => k.Place == null && k.Weight > 0 && k.Biome == biome && k.Zone <= zone && (big || !k.Big)).ToList();
            if (own.Count > 0) return own;
            return packs.Where(k => k.Place == null && k.Weight > 0 && k.Biome == null && k.Zone <= zone && (big || !k.Big)).ToList();
        }

        // Spots for count monsters: the marks first (their chunk's M), then round them or the middle, each
        // PackSpacingMin to PackSpacingMax from one already chosen and no nearer than PackSpacingMin to any.
        static List<Vec2> Spread(Vec2 middle, List<Vec2> marks, int count, GameRandom random, Func<Vec2, bool> fits)
        {
            var spots = new List<Vec2>();
            bool Apart(Vec2 at, float least) => spots.All(s => Vec2.Distance(s, at) >= least);
            for (int i = marks.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (marks[i], marks[j]) = (marks[j], marks[i]);
            }
            foreach (var mark in marks)
            {
                if (spots.Count >= count) break;
                if (fits(mark) && Apart(mark, PackSpacingMin)) spots.Add(mark);
            }
            if (spots.Count == 0 && count > 0)
            {
                // The middle, or the fitting tile nearest it.
                for (int ring = 0; ring <= 4 && spots.Count == 0; ring++)
                {
                    for (int k = 0; k < Math.Max(1, ring * 8) && spots.Count == 0; k++)
                    {
                        var at = middle + Vec2.FromAngle(2 * MathF.PI * k / Math.Max(1, ring * 8)) * ring;
                        at = TileMap.Center(TileMap.Cell(at.X), TileMap.Cell(at.Y));
                        if (fits(at)) spots.Add(at);
                    }
                }
            }
            for (int tries = 0; spots.Count > 0 && spots.Count < count && tries < 40 * count; tries++)
            {
                var from = spots[random.Next(spots.Count)];
                var at = from + Vec2.FromAngle(random.NextFloat() * 2 * MathF.PI) * (PackSpacingMin + random.NextFloat() * (PackSpacingMax - PackSpacingMin));
                // Kept together round the middle of those placed; a cramped room: closer and wider once half the tries
                // are spent.
                bool late = tries >= 20 * count;
                var centre = spots.Aggregate(Vec2.Zero, (sum, s) => sum + s) / spots.Count;
                if (Vec2.Distance(at, centre) > PackSpacingMax * (late ? 3 : 2)) continue;
                if (fits(at) && Apart(at, late ? 1 : PackSpacingMin)) spots.Add(at);
            }
            return spots;
        }

        static bool InRoom(RoomNode room, Vec2 at) => Vec2.Distance(at, room.Center) <= room.Radius + 1;

        // A small room of a maze (the mushroom forest's are three or four tiles across, most of it its ways in) may not
        // hold a whole pack: the rest stand in the turns of the maze round it, up to PackSpill tiles past its edge, by
        // its ways in and closer together (2026-11-22: the web sweep found a mushroom room's pack of one; 5~8% of pack
        // rooms, nearly all in the mushroom forest, held fewer than their pack's least).
        public const float PackSpill = 4;

        public static bool InPackRoom(RoomNode room, Vec2 at) => Vec2.Distance(at, room.Center) <= room.Radius + PackSpill + 0.5f;

        void Crowd(Pack pack, Vec2 middle, GameRandom random)
        {
            var room = pack.Room;
            int least = pack.Elite != EliteRank.None || pack.Definition == null ? pack.Roster.Count : Math.Min(pack.Roster.Count, pack.Definition.MinCount);
            if (pack.Spots.Count >= least) return;
            var more = Spread(pack.Spots.Count > 0 ? pack.Spots[0] : middle, pack.Spots.ToList(), pack.Roster.Count, random,
                at => Vec2.Distance(at, room.Center) <= room.Radius + PackSpill && Clear(at));
            if (more.Count <= pack.Spots.Count) return;
            // The ones placed first keep their spots (an elite leader its E).
            var kept = pack.Spots.ToList();
            pack.Spots.Clear();
            pack.Spots.AddRange(kept);
            foreach (var spot in more)
                if (pack.Spots.Count < pack.Roster.Count && !kept.Any(k => Vec2.Distance(k, spot) < 0.5f)) pack.Spots.Add(spot);
        }

        // Open floor a monster stands on, out of the places, not by a road or a camp.
        bool Clear(Vec2 at)
        {
            int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
            if (!Map.InBounds(x, y) || Map.IsSolid(x, y) || Map[x, y] == TileType.Lava || Map[x, y] == TileType.Water) return false;
            if (Collision.Overlaps(Map, at, 0.45f, monster: true)) return false;
            if (Places.Any(p => Vec2.Distance(at, p.Center) <= p.Radius + 2)) return false;
            return !NearRoad(x, y) && !_restRooms.Any(r => Vec2.Distance(at, r.Center) <= r.Radius + RoadClearance);
        }

        // A road, a dock, a rail, an arena's floor or a waypoint within RoadClearance (or reach).
        bool NearRoad(int x, int y, float clearance = RoadClearance)
        {
            int reach = (int)clearance;
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    if (dx * dx + dy * dy > reach * reach || !Map.InBounds(x + dx, y + dy)) continue;
                    var type = Map[x + dx, y + dy];
                    if (Structures.IsWay(type) || type == TileType.Waypoint) return true;
                }
            }
            return false;
        }

        // Where the ways come into a room: the first tile of each way past its edge.
        bool NearEntrance(RoomNode room, Vec2 at)
        {
            if (!_entrances.TryGetValue(room.Index, out var ways))
            {
                ways = new List<Vec2>();
                foreach (int index in room.Edges)
                {
                    var edge = _rooms.Edges[index];
                    var path = edge.Path;
                    if (path == null || path.Count == 0) continue;
                    bool fromHere = edge.A == room.Index;
                    for (int i = 0; i < path.Count; i++)
                    {
                        var (x, y) = path[fromHere ? i : path.Count - 1 - i];
                        var tile = TileMap.Center(x, y);
                        if (Vec2.Distance(tile, room.Center) < room.Radius * 0.8f) continue;
                        ways.Add(tile);
                        break;
                    }
                }
                _entrances[room.Index] = ways;
            }
            return ways.Any(w => Vec2.Distance(w, at) < EntranceClearance);
        }

        // The pack's monsters come: those of its roster that have not fallen today, and extra more of its first kind.
        void Wake(Pack pack, int extra)
        {
            int zone = ZoneLevelAt(pack.Home);
            var random = PackRandom(pack.Room, pack.Slot, pack.Day + 7919);
            for (int i = 0; i < pack.Roster.Count; i++)
            {
                if (pack.Fallen[i]) continue;
                var enemy = Join(pack, pack.Roster[i], pack.Spots[i], i, zone, random);
                if (pack.Elite == EliteRank.Champion || pack.Elite == EliteRank.Leader && i == 0) MakeElite(enemy, pack.Elite, pack.Affixes);
            }
            // More players and the night bring more of its first kind; of a leader's, more of its minions; champions
            // stay three.
            var more = pack.Elite == EliteRank.None ? pack.Roster[0] : pack.Elite == EliteRank.Leader && pack.Roster.Count > 1 ? pack.Roster[1] : null;
            // Never more of a big body: they stand where they fit, one or two to a room.
            if (more != null && more.Big) more = pack.Roster.FirstOrDefault(k => !k.Big);
            if (extra > 0 && pack.Spots.Count > 0 && more != null)
            {
                var spots = Spread(pack.Spots[0], new List<Vec2>(), extra + 1, random, at => (pack.Room == null || InRoom(pack.Room, at)) && Clear(at)
                    && !pack.Spots.Any(s => Vec2.Distance(s, at) < 1));
                for (int i = 1; i < spots.Count; i++) Join(pack, more, spots[i], -1, zone, random);
            }
            pack.Active = true;
            pack.Alerted = false;
        }

        Enemy Join(Pack pack, MonsterDefinition kind, Vec2 spot, int slot, int zone, GameRandom random)
        {
            var enemy = SpawnEnemy(kind, spot);
            ToughenForZone(enemy, zone);
            enemy.PackId = pack.Id;
            enemy.PackSlot = slot;
            enemy.Home = spot;
            enemy.Facing = Vec2.FromAngle(random.NextFloat() * 2 * MathF.PI);
            // A big body keeps to its room (World.WayFor).
            if (pack.Room != null) Bind(enemy, pack.Room.Center, pack.Room.Radius);
            return enemy;
        }

        // Everyone has left the room: its monsters go (and come again, less the fallen, when someone comes back).
        void Sleep(Pack pack)
        {
            _enemies.RemoveAll(e => e.PackId == pack.Id && !e.IsDead);
            pack.Active = false;
        }

        // Before the fallen leave the world: a felled monster of a roster stays fallen until dawn.
        void NotePackFalls()
        {
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsDead || enemy.PackId == 0 || enemy.PackSlot < 0) continue;
                if (!_packs.TryGetValue(enemy.PackId, out var pack) || enemy.PackSlot >= pack.Fallen.Length) continue;
                pack.Fallen[enemy.PackSlot] = true;
                // A camp's band fell: its trapdoor, if it has one, opens (B1c).
                if (pack.Room != null && pack.Room.Role == RoomRole.Elite && EliteFell(pack)) OpenCellar(pack.Room);
            }
        }

        // A world of caves: a pack of packs.csv as one clump where one monster would have come (Docs/WorldRework.md 상한,
        // 1판 월드), within the cap the caves always had (WorldConfig.EnemyTarget, more at night): at 48 a party the
        // open caves held twice the monsters and the first-hour bot fell ten times as often (2026-11-22). False when
        // no pack comes there.
        bool TryClump(Vec2 position, Player near)
        {
            var choices = PackChoices(position, Biomes.At(position), big: true, loose: true);
            if (choices.Count == 0) return false;
            var definition = _random.PickWeighted(choices, p => p.Weight);
            var parties = Parties();
            int party = PartyOf(parties, position);
            int extra = PackExtraPerPlayer * (parties[party].Count - 1) + NightExtra;
            var roster = new List<MonsterDefinition>();
            foreach (var member in definition.Members)
            {
                int count = member.Min + _random.Next(member.Max - member.Min + 1);
                for (int i = 0; i < count; i++) roster.Add(member.Monster);
            }
            for (int i = 0; i < extra; i++) roster.Add(definition.Members[0].Monster);
            // A world of caves has no rooms to hold a big body (L, XL): its clumps come without them.
            LeaveOutBig(roster);
            if (FieldCount + roster.Count > EnemyTargetNow) return true;
            var spots = Spread(position, new List<Vec2>(), roster.Count, _random, at => Clear(at) && Vec2.Distance(at, near.Position) >= _config.SpawnDistance);
            if (spots.Count == 0) return true;
            var pack = new Pack(_nextPackId++, null, 0, position) { Definition = definition, Active = true, Day = Today };
            _packs[pack.Id] = pack;
            int zone = ZoneLevelAt(position);
            for (int i = 0; i < spots.Count; i++) Join(pack, roster[i], spots[i], -1, zone, _random);
            return true;
        }

        // For the playtests, the play mode scenes and the tests: these monsters one pack as a room's would be, standing
        // where they are (a world with no rooms has none of its own near the spawn).
        internal Pack Gather(IReadOnlyList<Enemy> members)
        {
            var home = members.Aggregate(Vec2.Zero, (sum, m) => sum + m.Position) / Math.Max(1, members.Count);
            var pack = new Pack(_nextPackId++, null, 0, home) { Active = true, Day = Today, Shares = true };
            _packs[pack.Id] = pack;
            foreach (var member in members)
            {
                member.PackId = pack.Id;
                member.PackSlot = -1;
                member.Home = member.Position;
            }
            return pack;
        }

        // ---- Noticing together, the leash, going home ----

        // One noticed a player: the rest of its pack take it up one after another within PackAlertSeconds, and the
        // wanderers near it with them.
        void Noticed(Enemy enemy, Player player)
        {
            var pack = PackOf(enemy);
            if (pack == null && !enemy.Wanders) return;
            _events.Add(new SimEvent(SimEventType.Alerted, enemy.Id, player.Id));
            if (pack == null) return;
            pack.Alerted = true;
            // A crystal mimic (B3b) is not woken by its pack's alarm: it stands as crystals until it is come to or struck.
            var rest = _enemies.Where(e => e != enemy && !e.IsDead && e.State == EnemyState.Idle && !e.Returning && e.AlertTicks == 0
                && !Mimics(e)
                && (e.PackId == pack.Id || e.PackId == 0 && e.Wanders && e.Rift == 0 && Vec2.Distance(e.Position, enemy.Position) <= WandererJoinReach)).ToList();
            int spread = Ticks(PackAlertSeconds);
            for (int i = 0; i < rest.Count; i++)
            {
                rest[i].AlertTicks = 1 + (i + 1) * spread / (rest.Count + 1);
                rest[i].AlertTarget = player.Id;
            }
        }

        // A blow on a pack's monster that had not noticed anyone: it has now, and its pack with it.
        void Provoked(Enemy enemy, Player by)
        {
            // A dormant one (B2) wakes when struck whether or not it came with a pack: it notices no one further off.
            if (by == null || enemy.IsDead || enemy.State != EnemyState.Idle || enemy.Returning || enemy.Rift != 0
                || enemy.PackId == 0 && !enemy.Definition.Has(MonsterTraitKind.Dormant) && !enemy.Definition.Has(MonsterTraitKind.Mimic)) return;
            enemy.TargetId = by.Id;
            enemy.State = EnemyState.Chase;
            Noticed(enemy, by);
        }

        // Each tick for a pack's monster or a wanderer, before it fights: its alarm, the way home, the leash. True when
        // that was all it did this tick.
        bool PackTick(Enemy enemy)
        {
            if (enemy.AlertTicks > 0 && --enemy.AlertTicks == 0 && enemy.State == EnemyState.Idle && !enemy.Returning)
            {
                var player = FindPlayer(enemy.AlertTarget);
                if (player != null && !player.IsDead && !InSafePlace(player.Position))
                {
                    enemy.TargetId = player.Id;
                    enemy.State = EnemyState.Chase;
                    _events.Add(new SimEvent(SimEventType.Alerted, enemy.Id, player.Id));
                }
            }
            if (enemy.Returning)
            {
                GoHome(enemy);
                HomeInTime(enemy);
                return true;
            }
            if (enemy.PackId == 0 || enemy.State == EnemyState.Idle) return false;
            Mark(enemy);
            var pack = PackOf(enemy);
            // A big body drawn out past its room's leash (by a blow, or after a target right at its edge) turns back too.
            if (enemy.State == EnemyState.Chase && pack != null && (Vec2.Distance(enemy.Position, pack.Home) > PackLeash
                || !InBound(enemy, enemy.Position) && Vec2.Distance(enemy.Position, enemy.BoundCenter) > enemy.BoundRadius + 1))
            {
                TurnBack(enemy);
                return true;
            }
            return false;
        }

        // Idle with no one to go after: a pack's monster that strayed walks back, a wanderer goes on its way.
        void IdleAbout(Enemy enemy)
        {
            if (enemy.Rift != 0) return;
            // One the director sent after a player walks to them; a trap's band at whoever is shut in with it
            // (World.Director.cs, World.Traps.cs).
            if (HuntOn(enemy) || TrapHuntOn(enemy)) return;
            if (enemy.PackId != 0 && Vec2.Distance(enemy.Position, enemy.Home) > StrayReach) TurnBack(enemy);
            else if (enemy.Wanders) Wander(enemy);
        }

        void TurnBack(Enemy enemy)
        {
            enemy.State = EnemyState.Idle;
            enemy.TargetId = 0;
            enemy.Attack = null;
            enemy.AlertTicks = 0;
            enemy.Returning = true;
            enemy.Stuck = 0;
            // The way back's length at its pace, half again, and three seconds.
            float way = 0;
            var from = enemy.Position;
            for (int i = enemy.Trail.Count - 1; i >= 0; i--)
            {
                way += Vec2.Distance(from, enemy.Trail[i]);
                from = enemy.Trail[i];
            }
            way += Vec2.Distance(from, enemy.Home);
            enemy.HomeTicks = Ticks(way / Math.Max(0.5f, enemy.Speed) * 1.5f + 3);
        }

        // A point of the way it came, a little way on from the last; a way that came round on itself is cut short.
        void Mark(Enemy enemy)
        {
            var trail = enemy.Trail;
            if (trail.Count == 0) trail.Add(enemy.Home);
            if (Vec2.Distance(trail[trail.Count - 1], enemy.Position) < TrailStep) return;
            for (int i = 0; i < trail.Count - 1; i++)
            {
                if (Vec2.Distance(trail[i], enemy.Position) >= TrailStep * 0.75f) continue;
                trail.RemoveRange(i + 1, trail.Count - i - 1);
                return;
            }
            if (trail.Count >= TrailMost) trail[trail.Count - 1] = enemy.Position;
            else trail.Add(enemy.Position);
        }

        // Back along its way, point by point, healing; at home it stands again and heeds players.
        void GoHome(Enemy enemy)
        {
            enemy.Health = Math.Min(enemy.MaxHealth, enemy.Health + enemy.MaxHealth * ReturnHealPercent / 100f * TickSeconds);
            var trail = enemy.Trail;
            var to = trail.Count > 0 ? trail[trail.Count - 1] : enemy.Home;
            if (Step(enemy, to, enemy.Speed) < 0.5f)
            {
                enemy.Stuck = 0;
                if (trail.Count > 0) trail.RemoveAt(trail.Count - 1);
                else enemy.Returning = false;
                return;
            }
            // Stuck on a corner for a second: on to the next point. Stuck with none left (it was put where it never
            // walked, or a corner holds it): home at once where no player sees it, else after a few more tries, gone in
            // a puff (2026-11-22: a scorpion caught on a corner healed faster than the first-hour bot struck it, and
            // the bot struck it for eight minutes).
            if (enemy.Stuck <= Ticks(1)) return;
            enemy.Stuck = 0;
            if (trail.Count > 0)
            {
                trail.RemoveAt(trail.Count - 1);
                return;
            }
            if (_players.Any(p => !p.IsDead && Vec2.Distance(p.Position, enemy.Position) <= UnseenReach)) return;
            enemy.Position = enemy.Home;
            enemy.Returning = false;
        }

        // Whatever holds it (a corner, blows pushing it back), it is home within the time its way takes and a little
        // more: then put there in a puff (2026-11-22: a scorpion caught on a corner and knocked back by every blow
        // healed faster than the first-hour bot struck it, and the bot struck it for eight minutes).
        void HomeInTime(Enemy enemy)
        {
            if (!enemy.Returning || --enemy.HomeTicks > 0) return;
            if (_players.Any(p => !p.IsDead && Vec2.Distance(p.Position, enemy.Position) <= UnseenReach))
                _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxVanish, position: enemy.Position));
            enemy.Position = enemy.Home;
            enemy.Returning = false;
            enemy.Trail.Clear();
        }

        // Further than this from every player, nobody sees a monster.
        const float UnseenReach = 24;

        // A step toward a point, at a speed; the distance left. Counts the ticks it barely moved in Stuck.
        float Step(Enemy enemy, Vec2 to, float speed)
        {
            var way = to - enemy.Position;
            float distance = way.Length;
            if (distance < 1e-3f) return 0;
            float step = Math.Min(distance, speed * TickSeconds);
            var before = enemy.Position;
            enemy.Position = Collision.Move(Map, enemy.Position, enemy.Definition.Radius, way / distance * step, monster: true);
            enemy.Facing = way / distance;
            if (Vec2.Distance(before, enemy.Position) < step * 0.3f) enemy.Stuck++;
            else enemy.Stuck = 0;
            return Vec2.Distance(enemy.Position, to);
        }

        // ---- Wanderers (떠돌이) ----

        // Rooms a wanderer comes into being in and walks to: no pack's, no camp, off the roads.
        static bool Wanderable(RoomNode room) =>
            room.Kind == RoomKind.Room && !room.Roadside && (room.Role == RoomRole.Plain || room.Role == RoomRole.Ore || room.Role == RoomRole.Pond);

        // Version 8 (2026-11-26, Docs/HuntingGrounds.md, the owner: "길로만 다니면 몬스터를 아예 만나지 않는다"): the
        // monsters about walk the roads' rooms too and come into being beside a road, though never on it, nor in a camp
        // or by a town.
        bool Wayside => _rooms != null && _rooms.Version >= WorldGenerator.HuntVersion;
        // Beside a road: nothing nearer it than this (the road is three wide, so a monster stands a tile or two off it).
        public const float WaysideClearance = 2;
        // Nor this near a town's plaza (past its edge).
        public const float TownClearance = 8;
        // Version 8's wanderers: a bigger share, more at least and at most.
        public const float WaysideShare = 0.3f;
        public const int WaysideMin = 7;
        public const int WaysideMax = 14;

        bool Walkable(RoomNode room) =>
            Wanderable(room) || Wayside && room.Kind == RoomKind.Room && room.Roadside
                && (room.Role == RoomRole.Plain || room.Role == RoomRole.Ore || room.Role == RoomRole.Pond || room.Role == RoomRole.Cart || room.Role == RoomRole.Shrine);

        // Whether a monster may come into being here in a world of rooms: in a room wanderers walk, or in a way between
        // rooms; never by a road or a camp.
        internal bool WandererSpot(int x, int y)
        {
            var at = TileMap.Center(x, y);
            var room = _rooms.Nearest(x, y);
            if (room != null && Vec2.Distance(at, room.Center) <= room.Radius + 2 && !Walkable(room)) return false;
            if (Wayside && Places.Any(p => Vec2.Distance(at, p.Center) <= p.Radius + TownClearance)) return false;
            return !NearRoad(x, y, Wayside ? WaysideClearance : RoadClearance) && !_restRooms.Any(r => Vec2.Distance(at, r.Center) <= r.Radius + RoadClearance);
        }

        // How many wanderers the parties want: WandererShare of the monsters about, within its bounds, more at night.
        bool WantsWanderer()
        {
            var parties = Parties();
            if (parties.Count == 0) return false;
            var packed = PartyCounts(parties, e => e.PackId != 0);
            var wandering = PartyCounts(parties, e => e.PackId == 0);
            int want = 0;
            for (int i = 0; i < parties.Count; i++)
                want += Wayside
                    ? Math.Clamp((int)MathF.Round(packed[i] * WaysideShare / (1 - WaysideShare)), WaysideMin, WaysideMax)
                    : Math.Clamp((int)MathF.Round(packed[i] * WandererShare / (1 - WandererShare)), MinWanderers, MaxWanderers);
            if (Calendar.IsNight(Clock)) want = want * (FullMoonNight ? 200 : 150) / 100;
            var all = PartyCounts(parties);
            return wandering.Sum() < want && all.Max() < PartyCap && FieldCount < HostCap;
        }

        // From room to room along the ways, slowly, standing a while in each.
        void Wander(Enemy enemy)
        {
            if (_rooms == null) return;
            if (enemy.WanderPause > 0)
            {
                enemy.WanderPause--;
                return;
            }
            if (enemy.WanderEdge < 0 && !PickWay(enemy))
            {
                enemy.WanderPause = Ticks(3);
                return;
            }
            var edge = _rooms.Edges[enemy.WanderEdge];
            var path = edge.Path;
            var (x, y) = path[enemy.WanderStep];
            Step(enemy, TileMap.Center(x, y), enemy.Speed * WanderPace);
            bool there = Vec2.Distance(enemy.Position, TileMap.Center(x, y)) < 0.6f;
            if (!there && enemy.Stuck <= Ticks(1.5f)) return;
            enemy.Stuck = 0;
            enemy.WanderStep += enemy.WanderWay;
            if (enemy.WanderStep >= 0 && enemy.WanderStep < path.Count) return;
            // At the far room: a while there before the next way.
            enemy.WanderRoom = enemy.WanderWay > 0 ? edge.B : edge.A;
            enemy.WanderEdge = -1;
            enemy.WanderPause = Ticks(2) + _random.Next(Ticks(3));
        }

        // A way out of the room it is in to a room wanderers walk, from the tile of it nearest the wanderer.
        bool PickWay(Enemy enemy)
        {
            var room = enemy.WanderRoom >= 0 ? _rooms.Nodes[enemy.WanderRoom] : _rooms.Nearest(TileMap.Cell(enemy.Position.X), TileMap.Cell(enemy.Position.Y));
            if (room == null) return false;
            enemy.WanderRoom = room.Index;
            var ways = room.Edges.Where(i =>
            {
                var e = _rooms.Edges[i];
                return e.Kind == RoomEdgeKind.Way && e.Path != null && e.Path.Count > 1 && Walkable(_rooms.Nodes[e.A == room.Index ? e.B : e.A]);
            }).ToList();
            if (ways.Count == 0) return false;
            int index = ways[_random.Next(ways.Count)];
            var edge = _rooms.Edges[index];
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < edge.Path.Count; i++)
            {
                float distance = (TileMap.Center(edge.Path[i].X, edge.Path[i].Y) - enemy.Position).LengthSquared;
                if (distance < best) (nearest, best) = (i, distance);
            }
            enemy.WanderEdge = index;
            enemy.WanderWay = edge.A == room.Index ? 1 : -1;
            enemy.WanderStep = nearest;
            enemy.Stuck = 0;
            return true;
        }
    }
}
