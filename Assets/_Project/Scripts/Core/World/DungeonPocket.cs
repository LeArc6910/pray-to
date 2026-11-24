using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A dungeon's door out in the world (Docs/WorldRework.md 5단계 입구): the floor between the two gate posts of a
    // gate room (RoomRole.Gate, rooms.csv 'D'). Numbered in the order of the rooms; the host and a client find the same
    // from the seed, so where they are is never sent or saved.
    public sealed class DungeonGate
    {
        public DungeonGate(int index, int x, int y, Biome biome, int room = -1, bool cellar = false)
        {
            Cellar = cellar;
            Index = index;
            X = x;
            Y = y;
            Biome = biome;
            Room = room;
        }

        public int Index { get; }
        public int X { get; }
        public int Y { get; }
        public Biome Biome { get; }
        // The gate room's index in RoomGraph.Nodes, or -1 for one laid by hand (tests).
        public int Room { get; }
        // A camp's trapdoor (B1c, Docs/BiomeRework.md 4-5 지하실): the tile itself (TileType.Trapdoor, shut until the camp's
        // elites fall, then OpenTrapdoor) leads to its biome's cellar.
        public bool Cellar { get; }
        public Vec2 Center => TileMap.Center(X, Y);
    }

    // What a dungeon in the pocket is made of, as the seed lays it: where its players come in and get up when they
    // fall, the doors out, and its rooms (each a box of tiles), entrance first and the end last.
    public sealed class DungeonLayout
    {
        internal DungeonLayout(Vec2 entrance, IReadOnlyList<(int X, int Y)> exits, IReadOnlyList<(int Left, int Bottom, int W, int H)> rooms)
        {
            Entrance = entrance;
            Exits = exits;
            Rooms = rooms;
        }

        public Vec2 Entrance { get; }
        // The doors out: the entrance room's first, the end room's second.
        public IReadOnlyList<(int X, int Y)> Exits { get; }
        // Each room's box of floor, in the order of Nodes (the entrance first; 5a's plain dungeon: the end last).
        public IReadOnlyList<(int Left, int Bottom, int W, int H)> Rooms { get; }

        // ---- What DungeonGenerator lays from a row of dungeons.csv (묶음 5b); empty for 5a's plain dungeon. ----

        // Its row, and the biome it is laid in (the gate's).
        public DungeonDefinition Definition { get; internal set; }
        public Biome Biome { get; internal set; }
        // The rooms: the spine in order (the entrance first, the end last of it), then the side rooms.
        public IReadOnlyList<DungeonRoom> Nodes { get; internal set; } = Array.Empty<DungeonRoom>();
        // The chests, the shrine and the rest room's fire, by index (DungeonInstance.Used).
        public IReadOnlyList<DungeonThing> Things { get; internal set; } = Array.Empty<DungeonThing>();
        // The tiles of the doors: the locked room's grate (opens with the key room's pack felled), the end room's grate
        // (opens when the boss falls) and the hidden room's cracked wall (opens to blows).
        public IReadOnlyList<(int X, int Y)> KeyDoor { get; internal set; } = Array.Empty<(int, int)>();
        public IReadOnlyList<(int X, int Y)> BossDoor { get; internal set; } = Array.Empty<(int, int)>();
        public IReadOnlyList<(int X, int Y)> Crack { get; internal set; } = Array.Empty<(int, int)>();
        // The shortcut's stones (dungeon_graphs.csv loop): touched in the rest room it opens, and then the entrance's takes
        // whoever steps to it on to the rest room's. Null without one.
        public (int X, int Y)? ShortcutFrom { get; internal set; }
        public (int X, int Y)? ShortcutTo { get; internal set; }

        public DungeonRoom Room(char letter) => Nodes.FirstOrDefault(n => n.Letter == letter);

        // The room whose box (with a margin) holds the spot, or null.
        public DungeonRoom RoomAt(Vec2 at, float margin = 0)
        {
            foreach (var room in Nodes)
                if (room.Contains(at, margin)) return room;
            return null;
        }
    }

    // The dungeons' pocket (Docs/WorldRework.md 5단계 주머니의 모양, 묶음 5a): Rows rows of rock above the world, in the same
    // TileMap, cut into slots side by side, each Inner tiles square inside a Wall of rock, so slots lie at least two
    // walls apart (more than a snapshot's reach and a light's). A dungeon is laid in a slot from its seed, the same on
    // the host and on every client (Carve), and given back to the rock when it closes (Clear). Nothing in the pocket is
    // saved or dug.
    public static class DungeonPocket
    {
        public const int Rows = 160;
        public const int Span = 160;
        public const int Wall = 16;
        public const int Inner = Span - 2 * Wall;
        // One for each player a host lets in (HostConfig.MaxPlayers): four apart at four gates never run out.
        public const int MaxSlots = 4;
        // The 24-tile cells the inside is cut into, five each way (the design's 5×5 of 24 tiles).
        public const int Cell = 24;
        public const int Cells = 5;
        const ulong LayoutStream = 55;

        // A world of rooms wide enough for one slot or more gets the pocket (WorldGenerator.Generate).
        public static bool Fits(int width) => width >= Span;

        public static int SlotsOf(TileMap map) => map == null || !map.HasPocket ? 0 : Math.Min(MaxSlots, map.Width / Span);

        // The slot a tile is in, or -1 for the world (or rock past the last slot).
        public static int SlotAt(TileMap map, int x, int y)
        {
            if (map == null || !map.InPocket(y) || x < 0) return -1;
            int slot = x / Span;
            return slot < SlotsOf(map) ? slot : -1;
        }

        public static int SlotAt(TileMap map, Vec2 position) => SlotAt(map, TileMap.Cell(position.X), TileMap.Cell(position.Y));

        // A slot's box of tiles, its wall included.
        public static (int Left, int Bottom, int W, int H) Box(TileMap map, int slot) => (slot * Span, map.Height, Span, Rows);

        // The lower left tile of a slot's inside.
        public static (int X, int Y) Inside(TileMap map, int slot) => (slot * Span + Wall, map.Height + Wall);

        // The seed of a dungeon: the world's, the gate and how many times it was entered, so every time is different
        // and the host and a client lay the same.
        public static ulong SeedFor(ulong world, int gate, int entered)
        {
            ulong x = world ^ ((ulong)(gate + 1) * 0x9E3779B97F4A7C15UL) ^ ((ulong)(entered + 1) * 0xC2B2AE3D27D4EB4FUL);
            x ^= x >> 31;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            return x;
        }

        // The seed of a dungeon of dungeons.csv (묶음 5b): the world's, the dungeon's key and how often it was entered.
        public static ulong SeedFor(ulong world, string key, int entered)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in key ?? "")
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            ulong x = world ^ hash ^ ((ulong)(entered + 1) * 0xC2B2AE3D27D4EB4FUL);
            x ^= x >> 31;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            return x;
        }

        // The slot back to rock.
        public static void Clear(TileMap map, int slot)
        {
            var (left, bottom, w, h) = Box(map, slot);
            for (int y = bottom; y < bottom + h; y++)
                for (int x = left; x < left + w; x++)
                    map[x, y] = TileType.Wall;
        }

        // Lays a dungeon in the slot (cleared first) and says what it is made of. 5a's dungeon is a plain one, its rooms
        // empty (5b lays the real ones from dungeons.csv and dungeon_graphs.csv): a walk of four to seven rooms through
        // the cells, each a box of floor joined to the last by a way three wide, the first the entrance (a door out
        // between two gate posts, torches) and the last the end (another door out).
        public static DungeonLayout Carve(TileMap map, int slot, ulong seed)
        {
            Clear(map, slot);
            var random = new GameRandom(seed, LayoutStream);
            var (ix, iy) = Inside(map, slot);
            int margin = (Inner - Cell * Cells) / 2;
            (int X, int Y) CellCenter(int cx, int cy) => (ix + margin + cx * Cell + Cell / 2, iy + margin + cy * Cell + Cell / 2);

            // The walk: from a cell of the bottom row, to an unvisited neighbour each step, as far as it goes.
            int length = random.Next(4, 8);
            var cells = new List<(int X, int Y)> { (random.Next(Cells), 0) };
            var taken = new HashSet<(int, int)> { cells[0] };
            var ways = new[] { (0, 1), (1, 0), (-1, 0), (0, -1) };
            while (cells.Count < length)
            {
                var (cx, cy) = cells[cells.Count - 1];
                var open = ways.Select(w => (X: cx + w.Item1, Y: cy + w.Item2))
                    .Where(c => c.X >= 0 && c.Y >= 0 && c.X < Cells && c.Y < Cells && !taken.Contains((c.X, c.Y))).ToList();
                if (open.Count == 0) break;
                // Upward more often than not, so a dungeon goes somewhere.
                var next = open.Any(c => c.Y > cy) && random.Next(3) > 0 ? open.First(c => c.Y > cy) : open[random.Next(open.Count)];
                cells.Add(next);
                taken.Add(next);
            }

            var rooms = new List<(int Left, int Bottom, int W, int H)>();
            foreach (var (cx, cy) in cells)
            {
                var (mx, my) = CellCenter(cx, cy);
                int w = 11 + 2 * random.Next(0, 5), h = 11 + 2 * random.Next(0, 4);
                rooms.Add((mx - w / 2, my - h / 2, w, h));
            }
            void Floor(int x, int y)
            {
                if (SlotAt(map, x, y) == slot && x > ix && y > iy && x < ix + Inner - 1 && y < iy + Inner - 1) map[x, y] = TileType.Floor;
            }
            foreach (var (left, bottom, w, h) in rooms)
                for (int y = bottom; y < bottom + h; y++)
                    for (int x = left; x < left + w; x++)
                        Floor(x, y);
            // Each room to the next: neighbouring cells, so their middles line up along one axis.
            for (int i = 1; i < cells.Count; i++)
            {
                var a = CellCenter(cells[i - 1].X, cells[i - 1].Y);
                var b = CellCenter(cells[i].X, cells[i].Y);
                for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
                    for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
                        for (int d = -1; d <= 1; d++)
                        {
                            if (a.X == b.X) Floor(x + d, y);
                            else Floor(x, y + d);
                        }
            }
            // A door out between two gate posts, low in the room, and a torch in two corners.
            var exits = new List<(int X, int Y)>();
            void Door(int index)
            {
                var (left, bottom, w, h) = rooms[index];
                int x = left + w / 2, y = bottom + 2;
                map[x - 1, y] = TileType.Torii;
                map[x + 1, y] = TileType.Torii;
                map[x, y] = TileType.RuneFloor;
                exits.Add((x, y));
            }
            Door(0);
            if (rooms.Count > 1) Door(rooms.Count - 1);
            foreach (var (left, bottom, w, h) in rooms)
            {
                map[left, bottom + h - 1] = TileType.Torch;
                map[left + w - 1, bottom + h - 1] = TileType.Torch;
            }
            var first = rooms[0];
            var entrance = TileMap.Center(first.Left + first.W / 2, first.Bottom + 5);
            return new DungeonLayout(entrance, exits, rooms);
        }

        // The gates of a world of rooms: in each gate room, the floor between its two posts (or, without a pair, the tile the
        // generator keeps walked to).
        public static List<DungeonGate> Gates(RoomGraph rooms, TileMap map)
        {
            var gates = new List<DungeonGate>();
            if (rooms == null) return gates;
            foreach (var room in rooms.Nodes.Where(n => n.Role == RoomRole.Gate))
            {
                var posts = rooms.Marks.Where(m => m.Room == room.Index && m.Kind == RoomMarkKind.DungeonDoor).ToList();
                (int X, int Y)? at = null;
                for (int i = 0; i < posts.Count && at == null; i++)
                    for (int j = i + 1; j < posts.Count && at == null; j++)
                    {
                        var a = posts[i];
                        var b = posts[j];
                        bool pair = a.X == b.X && Math.Abs(a.Y - b.Y) == 2 || a.Y == b.Y && Math.Abs(a.X - b.X) == 2;
                        if (pair && !map.IsSolid((a.X + b.X) / 2, (a.Y + b.Y) / 2)) at = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                    }
                at ??= (room.AnchorX, room.AnchorY);
                gates.Add(new DungeonGate(gates.Count, at.Value.X, at.Value.Y, room.Biome, room.Index));
            }
            // The camps' trapdoors (generator version 3, WorldGenerator.Cellars.cs) after the gates, so the gates keep
            // their numbers.
            foreach (var (room, x, y) in rooms.Cellars)
                gates.Add(new DungeonGate(gates.Count, x, y, rooms.Nodes[room].Biome, room, cellar: true));
            return gates;
        }
    }
}
