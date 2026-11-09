using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A room (Docs/Npcs.md, 이사): floor closed in by solid tiles and doors, which a person can live in when it has a
    // bed, a door and something nice in or round it. Rooms are found from the map alone, so the host and every
    // client see the same ones and nothing about them needs saving: a person lives where their tile stands.
    public sealed class Room
    {
        internal Room(IReadOnlyList<(int X, int Y)> floor, IReadOnlyList<(int X, int Y)> edge, bool closed, bool door, bool bed, bool decor,
            IReadOnlyList<TileType> people)
        {
            Floor = floor;
            Edge = edge;
            Closed = closed;
            HasDoor = door;
            HasBed = bed;
            HasDecor = decor;
            People = people;
        }

        // The floor tiles inside, and the solid tiles and doors round them (furniture against the wall counts here).
        public IReadOnlyList<(int X, int Y)> Floor { get; }
        public IReadOnlyList<(int X, int Y)> Edge { get; }
        // False when the floor ran on past Housing.MaxTiles: not a room but open cave.
        public bool Closed { get; }
        public bool HasDoor { get; }
        public bool HasBed { get; }
        public bool HasDecor { get; }
        // The people's tiles in its edge: whoever lives there.
        public IReadOnlyList<TileType> People { get; }

        // What it lacks to be lived in, for the screen, or null when a person could live here.
        public string Fault =>
            !Closed ? "방이 닫혀 있지 않다(벽과 문으로 둘러야 한다)"
            : Floor.Count + People.Count < Housing.MinTiles ? $"방이 너무 좁다(바닥 {Housing.MinTiles}칸 이상)"
            : !HasBed ? "방에 침대가 없다"
            : !HasDoor ? "방에 문이 없다"
            : !HasDecor ? "방에 장식(탁자, 깃발, 화분, 등불, 횃불)이 없다"
            : null;

        public bool Fit => Fault == null;
    }

    public static class Housing
    {
        // A room's floor: at least this many tiles, and no more than this before it counts as open cave.
        public const int MinTiles = 6;
        public const int MaxTiles = 48;
        // Asking a person to move in needs this many hearts with them.
        public const int InviteHearts = 2;
        // A person living in a room of the base warms to the player faster: talks and gifts count this much more.
        public const float FriendshipBonus = 1.5f;

        static readonly TileType[] Decor = { TileType.Table, TileType.Banner, TileType.Pot, TileType.Lamp, TileType.Torch };

        static bool Inside(TileMap map, int x, int y) => map.InBounds(x, y) && !map.IsSolid(x, y) && map[x, y] != TileType.Door;

        // The room the floor tile is in: flood-filled over floor, doors and solid tiles stopping it, until it runs past
        // MaxTiles. People's tiles are the given person tiles (NpcData's).
        public static Room RoomAt(TileMap map, int x, int y, ICollection<TileType> people)
        {
            if (!Inside(map, x, y)) return null;
            var floor = new List<(int, int)>();
            var edge = new HashSet<(int, int)>();
            var seen = new HashSet<(int, int)> { (x, y) };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((x, y));
            bool closed = true;
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                floor.Add((cx, cy));
                if (floor.Count > MaxTiles)
                {
                    closed = false;
                    break;
                }
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    if (!Inside(map, nx, ny))
                    {
                        edge.Add((nx, ny));
                        continue;
                    }
                    if (seen.Add((nx, ny))) queue.Enqueue((nx, ny));
                }
            }
            var tiles = edge.Select(e => map[e.Item1, e.Item2]).ToList();
            return new Room(floor, edge.ToList(), closed, tiles.Contains(TileType.Door), tiles.Contains(TileType.Bed), tiles.Any(t => Decor.Contains(t)),
                tiles.Where(t => people != null && people.Contains(t)).Distinct().ToList());
        }

        // Every room beside a bed (the rooms a person might move into), each once.
        public static IReadOnlyList<Room> Rooms(TileMap map, ICollection<TileType> people)
        {
            var rooms = new List<Room>();
            var counted = new HashSet<(int, int)>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (map[x, y] != TileType.Bed) continue;
                    foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (-1, 0), (0, 1) })
                    {
                        if (counted.Contains((x + dx, y + dy))) continue;
                        var room = RoomAt(map, x + dx, y + dy, people);
                        if (room == null) continue;
                        foreach (var tile in room.Floor) counted.Add(tile);
                        rooms.Add(room);
                    }
                }
            }
            return rooms;
        }

        // The room the person whose tile stands at (x, y) lives in: a fit room with their tile in its edge, or null.
        public static Room HomeAt(TileMap map, int x, int y, ICollection<TileType> people)
        {
            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (-1, 0), (0, 1) })
            {
                var room = RoomAt(map, x + dx, y + dy, people);
                if (room != null && room.Fit) return room;
            }
            return null;
        }

        // A fit room nobody lives in, nearest the given point (the player's bed), or null.
        public static Room FreeRoom(TileMap map, ICollection<TileType> people, Vec2 near) =>
            Rooms(map, people).Where(r => r.Fit && r.People.Count == 0)
                .OrderBy(r => r.Floor.Min(f => Vec2.Distance(TileMap.Center(f.X, f.Y), near))).FirstOrDefault();

        // Why no room is free, for the screen: the fault of the room nearest the point, or that there is none.
        public static string NoRoom(TileMap map, ICollection<TileType> people, Vec2 near)
        {
            var rooms = Rooms(map, people);
            if (rooms.Count == 0) return "침대가 있는 방이 없다";
            if (rooms.Any(r => r.Fit)) return "빈 방이 없다(한 방에 한 사람)";
            return rooms.OrderBy(r => r.Floor.Min(f => Vec2.Distance(TileMap.Center(f.X, f.Y), near))).First().Fault;
        }

        // Where in the room the person's tile goes: a floor tile against the top wall with room floor in front (below)
        // of it, away from the door, where the room is still whole and fit with the tile standing in it.
        public static (int X, int Y)? Spot(TileMap map, Room room, TileType person, ICollection<TileType> people)
        {
            var floor = new HashSet<(int, int)>(room.Floor);
            bool NearDoor(int x, int y) =>
                new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => map.InBounds(x + d.Item1, y + d.Item2) && map[x + d.Item1, y + d.Item2] == TileType.Door);
            var candidates = room.Floor
                .Where(f => floor.Contains((f.X, f.Y - 1)) && !floor.Contains((f.X, f.Y + 1)) && !NearDoor(f.X, f.Y) && !NearDoor(f.X, f.Y - 1))
                .OrderByDescending(f => f.Y).ThenBy(f => f.X);
            foreach (var (x, y) in candidates)
            {
                var was = map[x, y];
                map[x, y] = person;
                var after = RoomAt(map, x, y - 1, people);
                map[x, y] = was;
                if (after != null && after.Fit && after.Floor.Count + 1 == room.Floor.Count) return (x, y);
            }
            return null;
        }
    }

    public sealed partial class World
    {
        ICollection<TileType> PersonTiles => Npcs?.Npcs.Select(n => n.Tile).ToList() ?? new List<TileType>();

        // Where the person's tile stands, or null.
        (int X, int Y)? PlaceOf(NpcDefinition npc)
        {
            for (int y = 0; y < Map.Height; y++)
            {
                for (int x = 0; x < Map.Width; x++)
                {
                    if (Map[x, y] == npc.Tile) return (x, y);
                }
            }
            return null;
        }

        // Whether the person lives in a fit room (and so warms faster, Housing.FriendshipBonus).
        public bool LivesHere(NpcDefinition npc) => PlaceOf(npc) is (int x, int y) && Housing.HomeAt(Map, x, y, PersonTiles) != null;

        // Friendship points with the person's home counted in.
        int Warmed(NpcDefinition npc, int points) => points > 0 && LivesHere(npc) ? (int)MathF.Round(points * Housing.FriendshipBonus) : points;

        // Asks the person within reach to move into the free room nearest the player's bed (or the spawn): their tile
        // leaves its place for one in that room. Needs Housing.InviteHearts. Null when they moved, else why not.
        public string Invite(int playerId)
        {
            var at = AtMerchant(playerId);
            if (at == null) return "곁에 아무도 없다";
            var (player, npc, friendship) = at.Value;
            if (friendship.Hearts < Housing.InviteHearts) return $"호감 {Housing.InviteHearts}부터 이사를 권할 수 있다";
            if (LivesHere(npc)) return "이미 방에 산다";
            var near = BedOf(player) is (int bx, int by) ? TileMap.Center(bx, by) : Spawn;
            var room = Housing.FreeRoom(Map, PersonTiles, near);
            if (room == null) return Housing.NoRoom(Map, PersonTiles, near);
            var spot = Housing.Spot(Map, room, npc.Tile, PersonTiles);
            if (spot == null) return "방에 설 자리가 없다";
            var from = PlaceOf(npc);
            if (from != null) SetTile(from.Value.X, from.Value.Y, TileType.Floor);
            SetTile(spot.Value.X, spot.Value.Y, npc.Tile);
            _events.Add(new SimEvent(SimEventType.MovedIn, player.Id, target: Npcs.Npcs.ToList().IndexOf(npc), position: TileMap.Center(spot.Value.X, spot.Value.Y)));
            return null;
        }
    }
}
