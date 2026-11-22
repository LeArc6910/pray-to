using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A room of a dungeon as the generator laid it: its letter of the graph, the cell of the pocket it stands in, its box
    // of floor and what its chunk marked (Docs/WorldRework.md 5단계 던전의 내용, 묶음 5b).
    public sealed class DungeonRoom
    {
        internal DungeonRoom(int index, char letter, (int X, int Y) cell, bool branch, int parent)
        {
            Index = index;
            Letter = letter;
            Cell = cell;
            Branch = branch;
            Parent = parent;
        }

        public int Index { get; }
        public char Letter { get; }
        public RoomRole Role => DungeonSheet.RoleOf(Letter);
        public (int X, int Y) Cell { get; }
        // A side room hung off a pack room (T, H), not on the way to the end.
        public bool Branch { get; }
        // The room it is entered from: the one before it on the spine, or the pack room it hangs off; -1 for the entrance.
        public int Parent { get; }
        public (int Left, int Bottom, int W, int H) Box { get; internal set; }
        public string Chunk { get; internal set; }
        // Where its pack's monsters stand first (its chunk's M), and its elite (E).
        public List<Vec2> Monsters { get; } = new List<Vec2>();
        public Vec2? Elite { get; internal set; }
        // Its chunk's other marks (a chest, a fire, a shrine's stone).
        internal List<(RoomMarkKind Kind, int X, int Y)> MarkList { get; } = new List<(RoomMarkKind, int, int)>();
        public Vec2 Center => new Vec2(Box.Left + Box.W / 2f, Box.Bottom + Box.H / 2f);
        public string Name => DungeonSheet.NameOf(Letter);

        // Whether a spot is in its box, or within margin tiles of it.
        public bool Contains(Vec2 at, float margin = 0) =>
            at.X >= Box.Left - margin && at.Y >= Box.Bottom - margin && at.X <= Box.Left + Box.W + margin && at.Y <= Box.Bottom + Box.H + margin;

        public bool Contains(int x, int y) => x >= Box.Left && y >= Box.Bottom && x < Box.Left + Box.W && y < Box.Bottom + Box.H;
    }

    public enum DungeonThingKind : byte
    {
        // A chest: the end's (the run's reward), a treasure room's or a hidden room's.
        Chest,
        // A shrine's stone: a blessing for the run.
        Shrine,
        // The rest room's fire: heals whoever stands by it, and nothing comes near it.
        Fire,
    }

    // Something used in a dungeon: where it stands, its room and its row of pois.csv.
    public sealed class DungeonThing
    {
        internal DungeonThing(int index, DungeonThingKind kind, int x, int y, int room, PoiDefinition definition, bool end = false)
        {
            Index = index;
            Kind = kind;
            X = x;
            Y = y;
            Room = room;
            Definition = definition;
            End = end;
        }

        public int Index { get; }
        public DungeonThingKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public Vec2 Center => TileMap.Center(X, Y);
        public int Room { get; }
        public PoiDefinition Definition { get; }
        // The end chest.
        public bool End { get; }
    }

    // Lays an instance dungeon in a slot of the pocket from its seed and its row of dungeons.csv (Docs/WorldRework.md 5단계
    // 던전의 내용, 묶음 5b): the graph's letters (pack rooms put in until the spine has its count), the spine walked through
    // the 5×5 cells of 24 tiles without crossing itself and the side rooms hung off its pack rooms, a box of floor in each
    // cell with a chunk of rooms.csv (where = dungeon) set in its middle, ways three wide between them, the doors (the
    // locked room's grate, the end's grate until the boss falls, the hidden room's cracked wall), the things to use and
    // the doors out. If the entrance does not reach every room, the seed goes up by one, up to eight times, and then the
    // rooms are laid in a snake (which always reaches). Integer maths and GameRandom only: the host and every client lay
    // the same tiles. The pocket's rock round it stays rock (DungeonPocket.Clear first).
    public static class DungeonGenerator
    {
        const ulong Stream = 56;
        public const int Tries = 8;
        // Ways between rooms are this wide; a room's box leaves this much floor round its chunk.
        public const int WayWidth = 3;
        const int Margin = 2;
        // A box is at most this big, so neighbouring boxes stand at least two tiles of rock apart.
        const int MostBox = DungeonPocket.Cell - 2;
        static readonly (int Dx, int Dy)[] Sides = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        // How many tries the last Lay took (1 the first; Tries + 1 for the snake), for the playtests and the tests.
        public static int LastTries { get; private set; }

        public static DungeonLayout Lay(TileMap map, int slot, ulong seed, DungeonDefinition definition, Biome biome, TerrainData terrain)
        {
            for (int attempt = 0; attempt < Tries; attempt++)
            {
                var layout = TryLay(map, slot, seed + (ulong)attempt, definition, biome, terrain, line: false);
                if (layout != null && Reaches(map, layout))
                {
                    LastTries = attempt + 1;
                    return layout;
                }
            }
            LastTries = Tries + 1;
            return TryLay(map, slot, seed, definition, biome, terrain, line: true);
        }

        // The letters of the spine: the graph's, with pack rooms put in before the champions', the rest's or the boss's
        // until there are as many as the dungeon draws.
        public static List<char> Letters(DungeonDefinition definition, GameRandom random)
        {
            var spine = definition.Graph.Spine.ToList();
            int rooms = random.Next(definition.MinRooms, definition.MaxRooms + 1);
            while (spine.Count < rooms)
            {
                int limit = spine.FindIndex(c => c == 'M' || c == 'R' || c == 'B');
                spine.Insert(random.Next(1, limit + 1), 'C');
            }
            return spine;
        }

        static DungeonLayout TryLay(TileMap map, int slot, ulong seed, DungeonDefinition definition, Biome biome, TerrainData terrain, bool line)
        {
            DungeonPocket.Clear(map, slot);
            var random = new GameRandom(seed, Stream);
            var letters = Letters(definition, random);
            var cells = line ? Snake(letters.Count) : Walk(letters.Count, random);
            if (cells == null) return null;
            var taken = new HashSet<(int, int)>(cells);
            var rooms = new List<DungeonRoom>();
            for (int i = 0; i < letters.Count; i++) rooms.Add(new DungeonRoom(i, letters[i], cells[i], false, i - 1));
            // The side rooms, each off a pack room with a free cell beside it.
            foreach (char letter in definition.Graph.Branches)
            {
                var open = rooms.Where(r => r.Letter == 'C' && !r.Branch && rooms.All(o => o.Parent != r.Index || !o.Branch))
                    .SelectMany(r => Sides.Select(s => (Room: r, Cell: (X: r.Cell.X + s.Dx, Y: r.Cell.Y + s.Dy))))
                    .Where(c => c.Cell.X >= 0 && c.Cell.Y >= 0 && c.Cell.X < DungeonPocket.Cells && c.Cell.Y < DungeonPocket.Cells && !taken.Contains(c.Cell)).ToList();
                if (open.Count == 0)
                {
                    if (!line) return null;
                    continue;
                }
                var (parent, cell) = open[random.Next(open.Count)];
                taken.Add(cell);
                rooms.Add(new DungeonRoom(rooms.Count, letter, cell, true, parent.Index));
            }
            return Carve(map, slot, random, definition, biome, terrain, rooms);
        }

        // The spine's cells: a walk from a cell of the bottom row to a neighbour not yet walked each step, turning back
        // where it is shut in. Null when no walk of that length was found within the steps allowed.
        static List<(int X, int Y)> Walk(int count, GameRandom random)
        {
            var path = new List<(int X, int Y)> { (random.Next(DungeonPocket.Cells), 0) };
            var seen = new HashSet<(int, int)> { path[0] };
            int budget = 4000;
            bool Go()
            {
                if (path.Count == count) return true;
                if (--budget <= 0) return false;
                var (x, y) = path[path.Count - 1];
                var ways = Sides.ToList();
                for (int i = ways.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (ways[i], ways[j]) = (ways[j], ways[i]);
                }
                foreach (var (dx, dy) in ways)
                {
                    var next = (X: x + dx, Y: y + dy);
                    if (next.X < 0 || next.Y < 0 || next.X >= DungeonPocket.Cells || next.Y >= DungeonPocket.Cells || !seen.Add(next)) continue;
                    path.Add(next);
                    if (Go()) return true;
                    path.RemoveAt(path.Count - 1);
                    seen.Remove(next);
                    if (budget <= 0) return false;
                }
                return false;
            }
            return Go() ? path : null;
        }

        // The cells row by row, back and forth: neighbours each to the next, whatever the count.
        static List<(int X, int Y)> Snake(int count)
        {
            var cells = new List<(int X, int Y)>();
            for (int y = 0; y < DungeonPocket.Cells && cells.Count < count; y++)
                for (int i = 0; i < DungeonPocket.Cells && cells.Count < count; i++)
                    cells.Add((y % 2 == 0 ? i : DungeonPocket.Cells - 1 - i, y));
            return cells;
        }

        static DungeonLayout Carve(TileMap map, int slot, GameRandom random, DungeonDefinition definition, Biome biome, TerrainData terrain, List<DungeonRoom> rooms)
        {
            var (ix, iy) = DungeonPocket.Inside(map, slot);
            int margin = (DungeonPocket.Inner - DungeonPocket.Cell * DungeonPocket.Cells) / 2;
            (int Left, int Bottom) CellCorner((int X, int Y) cell) => (ix + margin + cell.X * DungeonPocket.Cell, iy + margin + cell.Y * DungeonPocket.Cell);
            var chunks = new Dictionary<int, (RoomChunk Chunk, int Way)>();

            // Each room's chunk, and its box round it.
            foreach (var room in rooms)
            {
                var chunk = PickChunk(room.Letter, biome, terrain?.Rooms ?? RoomData.None, random);
                int w, h;
                if (chunk != null)
                {
                    int way = chunk.Turn ? random.Next(8) : 0;
                    var (cw, ch) = chunk.Size(way);
                    int pad = Math.Max(cw, ch) + 2 * Margin <= MostBox ? Margin : 1;
                    w = cw + 2 * pad;
                    h = ch + 2 * pad;
                    chunks[room.Index] = (chunk, way);
                    room.Chunk = chunk.Id;
                }
                else
                {
                    w = 11 + 2 * random.Next(0, 4);
                    h = 11 + 2 * random.Next(0, 4);
                }
                var (cl, cb) = CellCorner(room.Cell);
                int spareX = MostBox - w, spareY = MostBox - h;
                int left = cl + 1 + spareX / 2 + (spareX >= 4 ? random.Next(-spareX / 4, spareX / 4 + 1) : 0);
                int bottom = cb + 1 + spareY / 2 + (spareY >= 4 ? random.Next(-spareY / 4, spareY / 4 + 1) : 0);
                room.Box = (left, bottom, w, h);
            }

            bool Inside(int x, int y) => x > ix && y > iy && x < ix + DungeonPocket.Inner - 1 && y < iy + DungeonPocket.Inner - 1;
            void Floor(int x, int y)
            {
                if (Inside(x, y)) map[x, y] = TileType.Floor;
            }
            foreach (var room in rooms)
                for (int y = room.Box.Bottom; y < room.Box.Bottom + room.Box.H; y++)
                    for (int x = room.Box.Left; x < room.Box.Left + room.Box.W; x++)
                        Floor(x, y);

            // The ways: straight across the rock between two boxes, three wide, where the boxes face each other (neighbouring
            // cells' boxes always overlap by more than that), so a way touches each box only on the side facing the other
            // and a door on that side shuts it.
            (int X, int Y) Middle(DungeonRoom room) => (room.Box.Left + room.Box.W / 2, room.Box.Bottom + room.Box.H / 2);
            foreach (var room in rooms.Where(r => r.Parent >= 0))
            {
                var from = rooms[room.Parent];
                bool across = from.Cell.Y == room.Cell.Y;
                var (first, second) = (across ? from.Cell.X < room.Cell.X : from.Cell.Y < room.Cell.Y) ? (from, room) : (room, from);
                if (across)
                {
                    int lo = Math.Max(first.Box.Bottom, second.Box.Bottom) + 1, hi = Math.Min(first.Box.Bottom + first.Box.H, second.Box.Bottom + second.Box.H) - 2;
                    int y = (lo + hi) / 2;
                    for (int x = first.Box.Left + first.Box.W - 1; x <= second.Box.Left; x++)
                        for (int d = -(WayWidth / 2); d <= WayWidth / 2; d++) Floor(x, y + d);
                }
                else
                {
                    int lo = Math.Max(first.Box.Left, second.Box.Left) + 1, hi = Math.Min(first.Box.Left + first.Box.W, second.Box.Left + second.Box.W) - 2;
                    int x = (lo + hi) / 2;
                    for (int y = first.Box.Bottom + first.Box.H - 1; y <= second.Box.Bottom; y++)
                        for (int d = -(WayWidth / 2); d <= WayWidth / 2; d++) Floor(x + d, y);
                }
            }

            // The chunks, over the floor and the ways through their boxes.
            foreach (var room in rooms)
            {
                if (!chunks.TryGetValue(room.Index, out var set))
                {
                    // A plain box: a torch in two corners.
                    CornerTorches(map, room);
                    continue;
                }
                Stamp(map, room, set.Chunk, set.Way, random);
                // A chunk with no light of its own: a torch in the two top corners of its box, so no room is dark.
                if (!set.Chunk.Rows.Any(r => r.IndexOf('t') >= 0 || r.IndexOf('l') >= 0 || r.IndexOf('F') >= 0))
                    CornerTorches(map, room);
            }

            // The doors: the locked room's grate on its way in, the end's until the boss falls, the hidden room's crack.
            var keyDoor = new List<(int X, int Y)>();
            var bossDoor = new List<(int X, int Y)>();
            var crack = new List<(int X, int Y)>();
            foreach (var room in rooms.Where(r => r.Parent >= 0))
            {
                var list = room.Letter == 'L' ? keyDoor : room.Letter == 'X' ? bossDoor : room.Letter == 'H' ? crack : null;
                if (list == null) continue;
                var tile = room.Letter == 'H' ? TileType.CrackedWall : TileType.Grate;
                foreach (var (x, y) in Seal(map, room, rooms[room.Parent]))
                {
                    map[x, y] = tile;
                    list.Add((x, y));
                }
            }

            // The doors out: between a pair of posts in the entrance's and the end's chunk, else laid low in the box.
            var exits = new List<(int X, int Y)>();
            foreach (var room in new[] { rooms[0], rooms.First(r => r.Letter == 'X') })
            {
                var door = PostsIn(map, room);
                if (door == null)
                {
                    int x = room.Box.Left + room.Box.W / 2, y = room.Box.Bottom + room.Box.H - 3;
                    map[x - 1, y] = TileType.Torii;
                    map[x + 1, y] = TileType.Torii;
                    door = (x, y);
                }
                map[door.Value.X, door.Value.Y] = TileType.RuneFloor;
                exits.Add(door.Value);
            }

            // Where those who come in (and get up) stand: in the entrance's box, clear of the door out, nearest its middle.
            var entranceRoom = rooms[0];
            var exit = exits[0];
            (int X, int Y)? stand = null;
            int best = int.MaxValue;
            var (mx, my) = Middle(entranceRoom);
            for (int y = entranceRoom.Box.Bottom + 1; y < entranceRoom.Box.Bottom + entranceRoom.Box.H - 1; y++)
            {
                for (int x = entranceRoom.Box.Left + 1; x < entranceRoom.Box.Left + entranceRoom.Box.W - 1; x++)
                {
                    if (!Open3(map, x, y) || Square(x - exit.X, y - exit.Y) < 16) continue;
                    int distance = Square(x - mx, y - my);
                    if (distance < best) (best, stand) = (distance, (x, y));
                }
            }
            var entrance = TileMap.Center(stand?.X ?? mx, stand?.Y ?? my);

            // The things to use.
            var pois = terrain?.Pois ?? PoiData.None;
            var things = new List<DungeonThing>();
            foreach (var room in rooms)
            {
                var mark = room.Letter switch
                {
                    'X' or 'T' or 'H' => RoomMarkKind.Chest,
                    'S' => RoomMarkKind.Shrine,
                    'R' => RoomMarkKind.Campfire,
                    _ => (RoomMarkKind?)null,
                };
                if (mark == null) continue;
                var at = Marked(room, mark.Value) ?? Free(map, room);
                if (at == null) continue;
                var (x, y) = at.Value;
                PoiDefinition row = room.Letter switch
                {
                    'X' => pois.Find(definition.Chest),
                    'T' => Pick(pois.For(RoomRole.Treasure, biome), random),
                    'H' => Pick(pois.For(RoomRole.Hidden, biome), random),
                    'S' => Pick(pois.For(RoomRole.Shrine, biome), random),
                    _ => Pick(pois.For(RoomRole.Rest, biome), random),
                };
                var kind = mark == RoomMarkKind.Chest ? DungeonThingKind.Chest : mark == RoomMarkKind.Shrine ? DungeonThingKind.Shrine : DungeonThingKind.Fire;
                map[x, y] = kind == DungeonThingKind.Chest ? row?.Tile ?? TileType.WoodChest : kind == DungeonThingKind.Shrine ? TileType.RuneStone : TileType.Campfire;
                things.Add(new DungeonThing(things.Count, kind, x, y, room.Index, row, end: room.Letter == 'X'));
            }

            // The boss room's floor: a ring and a cross of inlay round its master, as the bosses' arenas have.
            var boss = rooms.First(r => r.Letter == 'B');
            var spot = boss.Elite ?? boss.Center;
            int sx = TileMap.Cell(spot.X), sy = TileMap.Cell(spot.Y);
            for (int y = boss.Box.Bottom; y < boss.Box.Bottom + boss.Box.H; y++)
            {
                for (int x = boss.Box.Left; x < boss.Box.Left + boss.Box.W; x++)
                {
                    if (map[x, y] != TileType.Floor) continue;
                    int d = Square(x - sx, y - sy);
                    if (d >= 25 && d < 36 || (x == sx || y == sy) && d <= 9) map[x, y] = TileType.ArenaInlay;
                }
            }

            // The shortcut's two stones: in the rest room and in the entrance's, each in a corner of its box.
            (int X, int Y)? shortcutFrom = null, shortcutTo = null;
            var rest = rooms.FirstOrDefault(r => r.Letter == 'R');
            if (definition.Graph.Shortcut && rest != null)
            {
                shortcutTo = Corner(map, rest);
                shortcutFrom = Corner(map, entranceRoom, avoid: exit);
                if (shortcutTo != null && shortcutFrom != null)
                {
                    map[shortcutTo.Value.X, shortcutTo.Value.Y] = TileType.Waystone;
                    map[shortcutFrom.Value.X, shortcutFrom.Value.Y] = TileType.Waystone;
                }
                else shortcutFrom = shortcutTo = null;
            }

            return new DungeonLayout(entrance, exits, rooms.Select(r => r.Box).ToList())
            {
                Definition = definition,
                Biome = biome,
                Nodes = rooms,
                Things = things,
                KeyDoor = keyDoor,
                BossDoor = bossDoor,
                Crack = crack,
                ShortcutFrom = shortcutFrom,
                ShortcutTo = shortcutTo,
            };
        }

        // A torch in the box's two top corners, where no way comes in beside them.
        static void CornerTorches(TileMap map, DungeonRoom room)
        {
            var (l, b, w, h) = room.Box;
            foreach (var (x, y, ox) in new[] { (l, b + h - 1, -1), (l + w - 1, b + h - 1, 1) })
                if (map.IsSolid(x + ox, y) && map.IsSolid(x, y + 1) && map.IsSolid(x + ox, y - 1)) map[x, y] = TileType.Torch;
        }

        static PoiDefinition Pick(IEnumerable<PoiDefinition> rows, GameRandom random)
        {
            var list = rows.ToList();
            return list.Count == 0 ? null : random.PickWeighted(list, p => p.Weight);
        }

        // A chunk of the letter's role made for dungeons, one made for the biome first seven times in ten: the entrance's
        // without a chest and the end's with one (both with a pair of posts), the boss's the biggest of the elites',
        // the champions' the others.
        static RoomChunk PickChunk(char letter, Biome biome, RoomData data, GameRandom random)
        {
            var role = DungeonSheet.RoleOf(letter);
            bool Has(RoomChunk c, char what) => c.Rows.Any(r => r.IndexOf(what) >= 0);
            var choices = data.Chunks.Where(c => c.Dungeon && !c.Walled && c.Role == role && c.For(biome)).ToList();
            if (letter == 'E') choices = choices.Where(c => Has(c, 'D') && !Has(c, 'C')).ToList();
            if (letter == 'X') choices = choices.Where(c => Has(c, 'D') && Has(c, 'C')).ToList();
            if (letter == 'B' || letter == 'M')
            {
                int biggest = choices.Count == 0 ? 0 : choices.Max(c => Math.Max(c.Width, c.Height));
                var big = choices.Where(c => Math.Max(c.Width, c.Height) == biggest).ToList();
                var small = choices.Where(c => Math.Max(c.Width, c.Height) < biggest).ToList();
                choices = letter == 'B' ? big : small.Count > 0 ? small : big;
            }
            choices = choices.Where(c => Math.Max(c.Width, c.Height) + 2 <= MostBox).ToList();
            if (choices.Count == 0) return null;
            var own = choices.Where(c => c.Biomes.Count > 0).ToList();
            var pool = own.Count > 0 && random.Next(100) < 70 ? own : choices;
            return random.PickWeighted(pool, c => c.Weight);
        }

        // Sets the chunk's letters in the middle of the room's box (its '?' leave the floor), and keeps its marks.
        static void Stamp(TileMap map, DungeonRoom room, RoomChunk chunk, int way, GameRandom random)
        {
            var (w, h) = chunk.Size(way);
            int left = room.Box.Left + (room.Box.W - w) / 2, bottom = room.Box.Bottom + (room.Box.H - h) / 2;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    char letter = chunk.At(x, y, way);
                    int tx = left + x, ty = bottom + h - 1 - y;
                    if (letter == '?') continue;
                    map[tx, ty] = letter == '%' ? (random.Next(2) == 0 ? TileType.Wall : TileType.Floor) : RoomSheet.Tile(letter) ?? TileType.Floor;
                    switch (RoomSheet.Mark(letter))
                    {
                        case RoomMarkKind.Monster:
                            room.Monsters.Add(TileMap.Center(tx, ty));
                            break;
                        case RoomMarkKind.Elite:
                            room.Elite = TileMap.Center(tx, ty);
                            break;
                        case RoomMarkKind kind when kind != RoomMarkKind.Barrel && kind != RoomMarkKind.Trap:
                            room.MarkList.Add((kind, tx, ty));
                            break;
                    }
                }
            }
        }

        static (int X, int Y)? Marked(DungeonRoom room, RoomMarkKind kind)
        {
            foreach (var (k, x, y) in room.MarkList)
                if (k == kind) return (x, y);
            return null;
        }

        // A floor tile near the box's middle with floor all round, for a thing a chunk did not mark.
        static (int X, int Y)? Free(TileMap map, DungeonRoom room)
        {
            var (cx, cy) = (room.Box.Left + room.Box.W / 2, room.Box.Bottom + room.Box.H / 2);
            for (int ring = 0; ring < 6; ring++)
                for (int dy = -ring; dy <= ring; dy++)
                    for (int dx = -ring; dx <= ring; dx++)
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == ring && Open3(map, cx + dx, cy + dy)) return (cx + dx, cy + dy);
            return null;
        }

        // A corner of the box's floor (inside its margin), clear of the ways in and of a tile to avoid.
        static (int X, int Y)? Corner(TileMap map, DungeonRoom room, (int X, int Y)? avoid = null)
        {
            var (l, b, w, h) = room.Box;
            foreach (var (x, y) in new[] { (l + 1, b + 1), (l + w - 2, b + 1), (l + 1, b + h - 2), (l + w - 2, b + h - 2) })
            {
                if (avoid is (int ax, int ay) && Square(x - ax, y - ay) < 9) continue;
                if (!Open3(map, x, y)) continue;
                // Not on the way of a way in: the box's edge beside it is walled on both sides.
                bool byWay = false;
                foreach (var (dx, dy) in Sides)
                    for (int k = 1; k <= 2; k++)
                        if (!room.Contains(x + dx * k, y + dy * k) && !map.IsSolid(x + dx * k, y + dy * k)) byWay = true;
                if (!byWay) return (x, y);
            }
            return null;
        }

        // Floor on the tile and the eight round it.
        static bool Open3(TileMap map, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (map.IsSolid(x + dx, y + dy)) return false;
            return true;
        }

        // The floor tiles just outside the room's box on the side it is entered from: the way in, shut by a door there.
        static List<(int X, int Y)> Seal(TileMap map, DungeonRoom room, DungeonRoom from)
        {
            var (l, b, w, h) = room.Box;
            var line = new List<(int X, int Y)>();
            if (from.Cell.X < room.Cell.X) for (int y = b - 1; y <= b + h; y++) line.Add((l - 1, y));
            else if (from.Cell.X > room.Cell.X) for (int y = b - 1; y <= b + h; y++) line.Add((l + w, y));
            else if (from.Cell.Y < room.Cell.Y) for (int x = l - 1; x <= l + w; x++) line.Add((x, b - 1));
            else for (int x = l - 1; x <= l + w; x++) line.Add((x, b + h));
            return line.Where(t => !map.IsSolid(t.X, t.Y)).ToList();
        }

        // The tile between a pair of posts (D) two apart in the room's box, if any.
        static (int X, int Y)? PostsIn(TileMap map, DungeonRoom room)
        {
            var (l, b, w, h) = room.Box;
            for (int y = b; y < b + h; y++)
                for (int x = l; x < l + w; x++)
                {
                    if (map[x, y] != TileType.Torii) continue;
                    if (x + 2 < l + w && map[x + 2, y] == TileType.Torii && !map.IsSolid(x + 1, y)) return (x + 1, y);
                    if (y + 2 < b + h && map[x, y + 2] == TileType.Torii && !map.IsSolid(x, y + 1)) return (x, y + 1);
                }
            return null;
        }

        static int Square(int dx, int dy) => dx * dx + dy * dy;

        // Whether, with its doors open, the entrance reaches every room's box, both doors out and beside every thing.
        public static bool Reaches(TileMap map, DungeonLayout layout)
        {
            var doors = new HashSet<(int, int)>(layout.KeyDoor.Concat(layout.BossDoor).Concat(layout.Crack));
            bool Open(int x, int y) => !map.IsSolid(x, y) || doors.Contains((x, y));
            var start = (TileMap.Cell(layout.Entrance.X), TileMap.Cell(layout.Entrance.Y));
            if (!Open(start.Item1, start.Item2)) return false;
            var seen = new HashSet<(int, int)> { start };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in Sides)
                {
                    var next = (x + dx, y + dy);
                    if (Open(next.Item1, next.Item2) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            bool Beside(int x, int y) => seen.Contains((x, y)) || Sides.Any(s => seen.Contains((x + s.Dx, y + s.Dy)));
            foreach (var room in layout.Nodes)
            {
                bool any = false;
                for (int y = room.Box.Bottom; y < room.Box.Bottom + room.Box.H && !any; y++)
                    for (int x = room.Box.Left; x < room.Box.Left + room.Box.W && !any; x++)
                        any = seen.Contains((x, y));
                if (!any) return false;
            }
            return layout.Exits.All(e => seen.Contains(e)) && layout.Things.All(t => Beside(t.X, t.Y))
                && (layout.ShortcutFrom == null || Beside(layout.ShortcutFrom.Value.X, layout.ShortcutFrom.Value.Y) && Beside(layout.ShortcutTo.Value.X, layout.ShortcutTo.Value.Y));
        }
    }
}
