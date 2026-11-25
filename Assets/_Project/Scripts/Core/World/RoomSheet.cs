using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What a room of the generator's version 2 is for (Docs/WorldRework.md 방의 역할). Laid on the room graph when the
    // rooms, the places and the roads are carved, and kept there for what comes after: packs (Pack, Elite), things to
    // find (Treasure, Shrine, Hidden, Locked, Trap, Cart, Rest), dungeons (Gate).
    public enum RoomRole : byte
    {
        // Nothing: somewhere passed through.
        Plain,
        // Where a pack of monsters lives.
        Pack,
        // Veins of ore in its walls.
        Ore,
        // A camp with a fire: nothing comes into being there.
        Rest,
        // The camp of a pack with a leader.
        Elite,
        // A chest, behind a pack or a leader.
        Treasure,
        Shrine,
        // A room behind a cracked wall.
        Hidden,
        // A room behind obsidian or void stone, seen through a grate first.
        Locked,
        Trap,
        // An abandoned cart and its rails.
        Cart,
        // The way into the biome's dungeon; one to a biome.
        Gate,
        // A pool to fish from.
        Pond,
    }

    // Which rooms a role goes to (room_roles.csv where).
    public enum RoleWhere : byte
    {
        Any,
        // Rooms with one way in, as far as there are any; then any.
        DeadEnd,
        // Rooms with two ways or more.
        Loop,
    }

    // How many rooms in a hundred get a role, and how far apart (a row of room_roles.csv, or the code's own).
    public sealed class RoomRoleRule
    {
        internal RoomRoleRule(Biome? biome, RoomRole role, int per100, int minGap, RoleWhere where)
        {
            Biome = biome;
            Role = role;
            Per100 = per100;
            MinGap = minGap;
            Where = where;
        }

        // Null: every biome without a row of its own.
        public Biome? Biome { get; }
        public RoomRole Role { get; }
        public int Per100 { get; }
        // Rooms of the role stand more than this many rooms apart (0: anywhere).
        public int MinGap { get; }
        public RoleWhere Where { get; }
    }

    // Something a chunk marks for what comes later: where a pack's monsters stand, where the chest is.
    public enum RoomMarkKind : byte
    {
        Monster,
        Elite,
        Chest,
        Campfire,
        Shrine,
        Trap,
        Barrel,
        DungeonDoor,
        // An abandoned mine cart ('j'): a cart room's things to find (3단계 볼거리).
        Cart,
    }

    public readonly struct RoomMark
    {
        public RoomMark(int room, RoomMarkKind kind, int x, int y)
        {
            Room = room;
            Kind = kind;
            X = x;
            Y = y;
        }

        // The room's index in RoomGraph.Nodes.
        public int Room { get; }
        public RoomMarkKind Kind { get; }
        public int X { get; }
        public int Y { get; }
    }

    // A room drawn by hand in letters (a row of rooms.csv, Docs/Biomes.md 방 조각), set into the middle of a room of
    // its role.
    public sealed class RoomChunk
    {
        readonly string[] _rows;

        internal RoomChunk(string id, RoomRole role, IReadOnlyList<Biome> biomes, bool field, bool dungeon, int weight, bool turn, string[] rows)
        {
            Id = id;
            Role = role;
            Biomes = biomes;
            Field = field;
            Dungeon = dungeon;
            Weight = weight;
            Turn = turn;
            _rows = rows;
            Width = rows[0].Length;
            Height = rows.Length;
            Walled = rows.Any(r => r.IndexOf(RoomSheet.Door) >= 0);
        }

        public string Id { get; }
        public RoomRole Role { get; }
        // The biomes it is used in; none means all.
        public IReadOnlyList<Biome> Biomes { get; }
        // In the open world, in a dungeon (Docs/WorldRework.md 5단계).
        public bool Field { get; }
        public bool Dungeon { get; }
        public int Weight { get; }
        // Set any of eight ways round: turned by quarters and mirrored.
        public bool Turn { get; }
        public int Width { get; }
        public int Height { get; }
        // The rows as written, the first the top one.
        public IReadOnlyList<string> Rows => _rows;
        // A building with doors ('+'): the generator clears a walk round it and opens the doors its room's ways face.
        // Without doors it is dressing laid on the room's own floor.
        public bool Walled { get; }

        public bool For(Biome biome) => Biomes.Count == 0 || Biomes.Contains(biome);

        // The chunk's size turned a way (0 to 7): odd ways lie on their side.
        public (int W, int H) Size(int way) => (way & 1) == 0 ? (Width, Height) : (Height, Width);

        // The letter at (x, y) of the chunk turned a way, x across from the left and y down from the top. Ways 0 to 3
        // are quarter turns; 4 to 7 the same mirrored.
        public char At(int x, int y, int way)
        {
            var (w, h) = Size(way);
            if (way >= 4) x = w - 1 - x;
            switch (way & 3)
            {
                case 1: (x, y) = (y, Height - 1 - x); break;
                case 2: (x, y) = (Width - 1 - x, Height - 1 - y); break;
                case 3: (x, y) = (Width - 1 - y, x); break;
            }
            return _rows[y][x];
        }
    }

    // The room chunks and the roles' numbers: what the generator furnishes its rooms with. A part of TerrainData, so
    // the host and a client carve the same rooms from the same sheets.
    public sealed class RoomData
    {
        public static readonly RoomData None = new RoomData(new RoomChunk[0], new RoomRoleRule[0], new string[0]);

        readonly List<RoomRoleRule> _rules;

        internal RoomData(IReadOnlyList<RoomChunk> chunks, IEnumerable<RoomRoleRule> rules, IReadOnlyList<string> errors)
        {
            Chunks = chunks;
            _rules = rules.ToList();
            Errors = errors;
        }

        public IReadOnlyList<RoomChunk> Chunks { get; }
        public IReadOnlyList<RoomRoleRule> Rules => _rules;
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        // A role's numbers in a biome: the sheet's row for the biome, or its row for every biome, or the code's.
        public RoomRoleRule Rule(Biome biome, RoomRole role) =>
            _rules.FirstOrDefault(r => r.Role == role && r.Biome == biome) ?? _rules.FirstOrDefault(r => r.Role == role && r.Biome == null)
            ?? RoomSheet.Defaults.First(r => r.Role == role);

        // The chunks a room of the role in the biome can take, in sheet order.
        public IEnumerable<RoomChunk> For(RoomRole role, Biome biome) => Chunks.Where(c => c.Role == role && c.Field && c.For(biome));
    }

    // Reads Assets/_Project/Data/World/rooms.csv and room_roles.csv (Docs/Biomes.md 방 조각, 방의 역할).
    public static class RoomSheet
    {
        public const string File = "rooms.csv";
        public const string RolesFile = "room_roles.csv";
        public const int MinSize = 3;
        public const int MaxSize = 25;
        public const char Door = '+';

        // Of a hundred rooms, how many get each role, how far apart, and where (Docs/WorldRework.md 방의 역할). Pack and
        // Plain share what the others leave, by these two numbers.
        public static readonly IReadOnlyList<RoomRoleRule> Defaults = new[]
        {
            new RoomRoleRule(null, RoomRole.Pack, 42, 0, RoleWhere.Any),
            new RoomRoleRule(null, RoomRole.Plain, 18, 0, RoleWhere.Any),
            new RoomRoleRule(null, RoomRole.Ore, 10, 2, RoleWhere.DeadEnd),
            new RoomRoleRule(null, RoomRole.Rest, 4, 6, RoleWhere.Loop),
            new RoomRoleRule(null, RoomRole.Elite, 3, 4, RoleWhere.DeadEnd),
            new RoomRoleRule(null, RoomRole.Treasure, 5, 3, RoleWhere.DeadEnd),
            new RoomRoleRule(null, RoomRole.Shrine, 3, 4, RoleWhere.Any),
            new RoomRoleRule(null, RoomRole.Hidden, 4, 3, RoleWhere.Any),
            new RoomRoleRule(null, RoomRole.Locked, 3, 4, RoleWhere.Any),
            new RoomRoleRule(null, RoomRole.Trap, 2, 4, RoleWhere.DeadEnd),
            new RoomRoleRule(null, RoomRole.Cart, 2, 5, RoleWhere.Any),
            new RoomRoleRule(null, RoomRole.Gate, 0, 0, RoleWhere.DeadEnd),
            new RoomRoleRule(null, RoomRole.Pond, 4, 3, RoleWhere.Any),
        };

        // What each letter of a chunk lays: a tile, and for some a mark kept on the room graph. '?' leaves the tile as
        // the generator made it, '%' is rock or floor by the toss of a coin, '+' a door.
        public const string Letters = "#.?%+MECFSXGOVLWPtTBRDiqmbcnorlkasuejhw=~^gpxzyAHZKf";

        // The tile a letter lays; null for '?', and for '%' and '+', which the generator decides.
        public static TileType? Tile(char letter) => letter switch
        {
            '#' => TileType.Wall,
            '.' or 'M' or 'E' => TileType.Floor,
            'C' => TileType.WoodChest,
            'F' => TileType.Campfire,
            'S' => TileType.RuneStone,
            'X' => TileType.CrackedWall,
            'G' => TileType.Grate,
            'O' => TileType.ObsidianWall,
            'V' => TileType.VoidWall,
            'L' => TileType.Lava,
            'W' => TileType.Water,
            'P' => TileType.Pillar,
            't' => TileType.Torch,
            'T' or '=' => TileType.RuneFloor,
            'B' => TileType.Barrel,
            'R' => TileType.Rail,
            'D' => TileType.Torii,
            'i' => TileType.IceFloor,
            'q' => TileType.Quicksand,
            'm' => TileType.MushroomTree,
            'b' => TileType.Bones,
            'c' => TileType.Crates,
            'n' => TileType.Tent,
            'o' => TileType.Totem,
            'r' => TileType.Rubble,
            'l' => TileType.Lantern,
            'k' => TileType.Cage,
            'a' => TileType.DemonBanner,
            's' => TileType.Statue,
            'u' => TileType.BrokenPillar,
            'e' => TileType.OrePile,
            'j' => TileType.MineCart,
            'h' => TileType.Bench,
            'w' => TileType.Well,
            '~' => TileType.Cobweb,
            '^' => TileType.Spire,
            // The biomes' props (묶음 B1a) for the dungeons' chunks (B1c): the dirt cave's stalagmite, pit prop, tipped
            // cart, ore sack and dead lamp; the mushroom forest's tall cap, shelf stump, spore sac and glowing caps.
            'g' => TileType.Stalagmite,
            'p' => TileType.PitProp,
            'x' => TileType.TippedCart,
            'z' => TileType.OreSack,
            'y' => TileType.UnlitLamp,
            'A' => TileType.PillarCap,
            'H' => TileType.ShelfStump,
            'Z' => TileType.SporeSac,
            'K' => TileType.GlowCaps,
            // The lake's shallows for the sunken aqueduct's rooms (묶음 B3b).
            'f' => TileType.ShallowWater,
            _ => null,
        };

        // What a letter marks on the room graph, if anything.
        public static RoomMarkKind? Mark(char letter) => letter switch
        {
            'M' => RoomMarkKind.Monster,
            'E' => RoomMarkKind.Elite,
            'C' => RoomMarkKind.Chest,
            'F' => RoomMarkKind.Campfire,
            'S' => RoomMarkKind.Shrine,
            'T' => RoomMarkKind.Trap,
            'B' => RoomMarkKind.Barrel,
            'D' => RoomMarkKind.DungeonDoor,
            'j' => RoomMarkKind.Cart,
            _ => null,
        };

        // Whether a letter's tile is walked on. '%' may be rock, so it is not counted on.
        public static bool Walked(char letter) => letter == '.' || letter == Door || letter == 'M' || letter == 'E' || letter == 'T' || letter == '=' || letter == 'R'
            || letter == 'i' || letter == 'q' || letter == '~' || letter == 'f' || letter == '?';

        // Dug through with the right pickaxe: what shuts a hidden room or a locked one.
        static bool DugThrough(char letter) => letter == 'X' || letter == 'O' || letter == 'V';

        public static RoomData Parse(string csv, string rolesCsv = "")
        {
            if (string.IsNullOrWhiteSpace(csv) && string.IsNullOrWhiteSpace(rolesCsv)) return RoomData.None;
            var errors = new List<string>();
            var chunks = new List<RoomChunk>();
            if (!string.IsNullOrWhiteSpace(csv)) ReadChunks(csv, chunks, errors);
            var rules = new List<RoomRoleRule>();
            if (!string.IsNullOrWhiteSpace(rolesCsv)) ReadRoles(rolesCsv, rules, errors);
            return new RoomData(chunks, rules, errors);
        }

        static void ReadChunks(string csv, List<RoomChunk> chunks, List<string> errors)
        {
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "role", "biomes", "where", "weight", "turn", "rows");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return;
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (id.Length == 0) throw new FormatException("id가 비었습니다.");
                    if (chunks.Any(c => c.Id == id)) throw new FormatException($"'{id}'가 두 번 있습니다.");
                    var role = SheetValues.ParseEnum<RoomRole>(row["role"], "role");
                    var biomes = row["biomes"].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(b => SheetValues.ParseEnum<Biome>(b.Trim(), "biomes")).ToList();
                    string where = row["where"].ToLowerInvariant();
                    if (where != "" && where != "field" && where != "dungeon") throw new FormatException("where는 field, dungeon 또는 빈칸입니다.");
                    int weight = row["weight"].Length == 0 ? 10 : SheetValues.ParseNumber(row["weight"], "weight");
                    if (weight <= 0) throw new FormatException("weight는 1 이상입니다.");
                    bool turn = row["turn"].Length > 0 && SheetValues.ParseBool(row["turn"], "turn");
                    var rows = row["rows"].Split('/');
                    Check(rows);
                    chunks.Add(new RoomChunk(id, role, biomes, where != "dungeon", where != "field", weight, turn, rows));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
        }

        // The drawing holds together: a rectangle of known letters, doors on its edge, and everything marked walked to
        // from a door (through what a pickaxe opens), or for dressing without doors, from its edge.
        static void Check(string[] rows)
        {
            int height = rows.Length, width = rows[0].Length;
            if (width < MinSize || height < MinSize || width > MaxSize || height > MaxSize)
                throw new FormatException($"조각은 가로세로 {MinSize}~{MaxSize}칸입니다({width}×{height}).");
            if (rows.Any(r => r.Length != width)) throw new FormatException("rows의 줄 길이가 서로 다릅니다.");
            bool doors = false;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char letter = rows[y][x];
                    if (Letters.IndexOf(letter) < 0) throw new FormatException($"모르는 글자 '{letter}'({x + 1}, {y + 1}).");
                    bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    if (letter == Door)
                    {
                        if (!edge) throw new FormatException($"'+'는 테두리에만 둡니다({x + 1}, {y + 1}).");
                        bool corner = (x == 0 || x == width - 1) && (y == 0 || y == height - 1);
                        if (corner) throw new FormatException("'+'는 모서리에 두지 않습니다.");
                        doors = true;
                    }
                }
            }
            // From each door by itself (the generator may open only one), or for dressing, from all its open edge.
            var starts = new List<List<(int X, int Y)>>();
            var rim = new List<(int X, int Y)>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char letter = rows[y][x];
                    bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    if (doors && letter == Door) starts.Add(new List<(int X, int Y)> { (x, y) });
                    else if (!doors && edge && Walked(letter)) rim.Add((x, y));
                }
            }
            if (!doors) starts.Add(rim);
            foreach (var start in starts)
            {
                var seen = new bool[width * height];
                var open = new Queue<(int X, int Y)>();
                foreach (var (x, y) in start)
                {
                    seen[y * width + x] = true;
                    open.Enqueue((x, y));
                }
                while (open.Count > 0)
                {
                    var (x, y) = open.Dequeue();
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height || seen[ny * width + nx]) continue;
                        char next = rows[ny][nx];
                        // Another door may be shut: it is not a way through.
                        if (!Walked(next) && !DugThrough(next) || next == Door) continue;
                        seen[ny * width + nx] = true;
                        open.Enqueue((nx, ny));
                    }
                }
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        char letter = rows[y][x];
                        if (Mark(letter) == null && !Walked(letter) || letter == '?' || letter == Door) continue;
                        // Something marked that stands on its tile (a chest, a fire) is come to from beside it.
                        bool reached = seen[y * width + x] || new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) }
                            .Any(n => n.Item1 >= 0 && n.Item2 >= 0 && n.Item1 < width && n.Item2 < height && seen[n.Item2 * width + n.Item1]);
                        if (!reached) throw new FormatException($"'{letter}'({x + 1}, {y + 1})에 {(doors ? "문('+') 하나하나" : "테두리")}에서 걸어서 닿지 않습니다.");
                    }
                }
            }
        }

        static void ReadRoles(string csv, List<RoomRoleRule> rules, List<string> errors)
        {
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("biome", "role", "per_100", "min_gap", "where");
            if (missing.Count > 0)
            {
                errors.Add($"{RolesFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return;
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    Biome? biome = row["biome"].Length == 0 ? (Biome?)null : SheetValues.ParseEnum<Biome>(row["biome"], "biome");
                    var role = SheetValues.ParseEnum<RoomRole>(row["role"], "role");
                    if (rules.Any(r => r.Biome == biome && r.Role == role)) throw new FormatException($"'{row["biome"]}'의 {row["role"]}가 두 번 있습니다.");
                    var standard = Defaults.First(r => r.Role == role);
                    int per100 = row["per_100"].Length == 0 ? standard.Per100 : SheetValues.ParseNumber(row["per_100"], "per_100");
                    if (per100 > 100) throw new FormatException("per_100은 0~100입니다.");
                    int gap = row["min_gap"].Length == 0 ? standard.MinGap : SheetValues.ParseNumber(row["min_gap"], "min_gap");
                    if (gap > 20) throw new FormatException("min_gap은 0~20입니다.");
                    var where = row["where"].Length == 0 ? standard.Where : SheetValues.ParseEnum<RoleWhere>(row["where"], "where");
                    rules.Add(new RoomRoleRule(biome, role, per100, gap, where));
                }
                catch (FormatException e)
                {
                    errors.Add($"{RolesFile} {row.Line}행: {e.Message}");
                }
            }
        }
    }
}
