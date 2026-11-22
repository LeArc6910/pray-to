using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The shape of a dungeon as letters (a row of dungeon_graphs.csv, Docs/WorldRework.md 5단계 던전의 내용): the rooms that
    // must be passed through in order, the side rooms hung off its pack rooms, and whether a shortcut joins its rest room
    // back to its entrance.
    public sealed class DungeonGraph
    {
        internal DungeonGraph(string id, IReadOnlyList<char> spine, IReadOnlyList<char> branches, bool shortcut)
        {
            Id = id;
            Spine = spine;
            Branches = branches;
            Shortcut = shortcut;
        }

        public string Id { get; }
        // From the entrance (E) to the end (X).
        public IReadOnlyList<char> Spine { get; }
        // Each hangs off a pack room (C) of the spine: treasure (T) or a hidden room (H).
        public IReadOnlyList<char> Branches { get; }
        public bool Shortcut { get; }
    }

    // A dungeon (a row of dungeons.csv): its name, biome, level, graph, how many rooms its spine has, its master and its
    // end chest (Docs/WorldRework.md 5단계 던전의 내용, 묶음 5b).
    public sealed class DungeonDefinition
    {
        internal DungeonDefinition(int index, string id, string name, Biome? biome, int level, DungeonGraph graph, int minRooms, int maxRooms, string boss, string chest,
            int minutes)
        {
            Index = index;
            Id = id;
            Name = name;
            Biome = biome;
            Level = level;
            Graph = graph;
            MinRooms = minRooms;
            MaxRooms = maxRooms;
            Boss = boss;
            Chest = chest;
            Minutes = minutes;
        }

        // Its row's place in the sheet: the host names a dungeon to the clients by it (DungeonsMessage).
        public int Index { get; }
        public string Id { get; }
        public string Name { get; }
        // Null: the gate of any biome without a dungeon of its own leads here, in its own biome ("모래 동굴의 깊은 굴").
        public Biome? Biome { get; }
        // Its zone level at least (World.ZoneLevelAt's scale, as packs.csv zone): the gate's own when that is higher.
        public int Level { get; }
        public DungeonGraph Graph { get; }
        // How many rooms its spine has: the graph's, and pack rooms (C) put in until there are this many.
        public int MinRooms { get; }
        public int MaxRooms { get; }
        // The kind of monster whose big elite holds the boss room (monsters.csv id), or null for the leader of the
        // biome's own pack.
        public string Boss { get; }
        // The end chest's row of pois.csv.
        public string Chest { get; }
        // About how long a run takes, for the screen.
        public int Minutes { get; }

        // What the save and the seed know it by: its id, and for a dungeon of any biome the biome too.
        public string KeyFor(Biome biome) => Biome == null ? $"{Id}@{biome.ToString().ToLowerInvariant()}" : Id;

        public string NameFor(Biome biome) => Biome == null ? $"{BiomeMap.Name(biome)}의 {Name}" : Name;
    }

    public sealed class DungeonData
    {
        public static readonly DungeonData None = new DungeonData(new DungeonDefinition[0], new DungeonGraph[0], new string[0]);

        internal DungeonData(IReadOnlyList<DungeonDefinition> dungeons, IReadOnlyList<DungeonGraph> graphs, IReadOnlyList<string> errors)
        {
            Dungeons = dungeons;
            Graphs = graphs;
            Errors = errors;
        }

        public IReadOnlyList<DungeonDefinition> Dungeons { get; }
        public IReadOnlyList<DungeonGraph> Graphs { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public DungeonDefinition Find(string id) => Dungeons.FirstOrDefault(d => d.Id == id);

        // A dungeon's record key as a number for the wire (RecordsMessage): its row's index times 16, and for a row of any
        // biome the biome + 1 in the low bits. -1 for a key of no row (5a's plain dungeons).
        public int IndexOfKey(string key)
        {
            foreach (var dungeon in Dungeons)
            {
                if (dungeon.Biome != null)
                {
                    if (dungeon.Id == key) return dungeon.Index << 4;
                    continue;
                }
                foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                    if (dungeon.KeyFor(biome) == key) return dungeon.Index << 4 | (int)biome + 1;
            }
            return -1;
        }

        public string KeyOfIndex(int index)
        {
            int row = index >> 4, biome = (index & 15) - 1;
            if (row < 0 || row >= Dungeons.Count) return null;
            var dungeon = Dungeons[row];
            return dungeon.Biome != null ? dungeon.Id : biome >= 0 && Enum.IsDefined(typeof(Biome), (Biome)biome) ? dungeon.KeyFor((Biome)biome) : null;
        }

        // The dungeon a gate of the biome leads to: the first of the biome's own, else the first of any biome; null
        // without either (then the gate leads to 5a's plain dungeon).
        public DungeonDefinition For(Biome biome) => Dungeons.FirstOrDefault(d => d.Biome == biome) ?? Dungeons.FirstOrDefault(d => d.Biome == null);
    }

    // Reads Assets/_Project/Data/World/dungeons.csv and dungeon_graphs.csv (Docs/Biomes.md 던전).
    public static class DungeonSheet
    {
        public const string File = "dungeons.csv";
        public const string GraphsFile = "dungeon_graphs.csv";
        // The letters of a graph (Docs/WorldRework.md 5단계 던전의 내용).
        public const string SpineLetters = "ECTSKLMRBXH";
        public const string BranchLetters = "TH";
        // A dungeon's rooms in all are at most this many: the pocket's 5×5 cells, less some room to turn.
        public const int MostRooms = 18;

        public static DungeonData Parse(string csv, string graphsCsv)
        {
            if (string.IsNullOrWhiteSpace(csv) && string.IsNullOrWhiteSpace(graphsCsv)) return DungeonData.None;
            var errors = new List<string>();
            var graphs = ReadGraphs(graphsCsv, errors);
            var dungeons = new List<DungeonDefinition>();
            var table = CsvTable.Read(csv ?? "");
            var missing = table.MissingColumns("id", "name", "biome", "level", "graph", "rooms", "boss", "chest", "minutes");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new DungeonData(dungeons, graphs, errors);
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'가 올바르지 않습니다.");
                    if (dungeons.Any(d => d.Id == id)) throw new FormatException($"'{id}'가 두 번 있습니다.");
                    string name = row["name"].Length > 0 ? row["name"] : id;
                    Biome? biome = row["biome"].Length == 0 ? (Biome?)null : SheetValues.ParseEnum<Biome>(row["biome"], "biome");
                    if (dungeons.Any(d => d.Biome == biome && biome == null)) throw new FormatException("biome이 빈 줄(어느 생물군계든)은 하나만 둡니다.");
                    int level = row["level"].Length == 0 ? 0 : SheetValues.ParseNumber(row["level"], "level");
                    if (level > 20) throw new FormatException("level은 구역 레벨(0~20)입니다.");
                    var graph = graphs.FirstOrDefault(g => g.Id == row["graph"]) ?? throw new FormatException($"graph '{row["graph"]}'가 {GraphsFile}에 없습니다.");
                    var bounds = row["rooms"].Split('-');
                    int min = SheetValues.ParseNumber(bounds[0], "rooms");
                    int max = bounds.Length > 1 ? SheetValues.ParseNumber(bounds[1], "rooms") : min;
                    if (min < graph.Spine.Count || max < min) throw new FormatException($"rooms는 그래프의 줄({graph.Spine.Count}개) 이상, 작은 수부터입니다.");
                    if (max + graph.Branches.Count > MostRooms) throw new FormatException($"방은 곁가지까지 {MostRooms}개를 넘지 않습니다.");
                    string boss = row["boss"];
                    if (boss != "elite" && !boss.StartsWith("elite:")) throw new FormatException("boss는 elite 또는 elite:몬스터 id입니다(던전만의 보스는 아직 없다).");
                    string kind = boss.Length > "elite:".Length ? boss.Substring("elite:".Length) : null;
                    if (kind != null && !SheetValues.IsValidId(kind)) throw new FormatException($"boss의 몬스터 id '{kind}'가 올바르지 않습니다.");
                    string chest = row["chest"];
                    if (!SheetValues.IsValidId(chest)) throw new FormatException("chest는 pois.csv의 상자 id입니다.");
                    int minutes = row["minutes"].Length == 0 ? 0 : SheetValues.ParseNumber(row["minutes"], "minutes");
                    dungeons.Add(new DungeonDefinition(dungeons.Count, id, name, biome, level, graph, min, max, kind, chest, minutes));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            if (dungeons.Count > byte.MaxValue - 1) errors.Add($"{File}: 던전은 {byte.MaxValue - 1}개까지입니다.");
            return new DungeonData(dungeons, graphs, errors);
        }

        static List<DungeonGraph> ReadGraphs(string csv, List<string> errors)
        {
            var graphs = new List<DungeonGraph>();
            var table = CsvTable.Read(csv ?? "");
            var missing = table.MissingColumns("id", "spine", "branches", "loop");
            if (missing.Count > 0)
            {
                errors.Add($"{GraphsFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return graphs;
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'가 올바르지 않습니다.");
                    if (graphs.Any(g => g.Id == id)) throw new FormatException($"'{id}'가 두 번 있습니다.");
                    var spine = Letters(row["spine"], SpineLetters, "spine");
                    var branches = Letters(row["branches"], BranchLetters, "branches");
                    if (spine.Count < 3 || spine[0] != 'E' || spine[spine.Count - 1] != 'X') throw new FormatException("spine은 E로 시작해 X로 끝납니다.");
                    if (spine.Count(c => c == 'E') != 1 || spine.Count(c => c == 'X') != 1 || spine.Count(c => c == 'B') != 1)
                        throw new FormatException("spine에 E, B, X는 하나씩입니다.");
                    if (spine.IndexOf('B') != spine.Count - 2) throw new FormatException("B(보스 방)는 X(끝) 바로 앞입니다.");
                    if (spine.Contains('H')) throw new FormatException("H(숨은 방)는 branches에만 둡니다.");
                    if (spine.Contains('L') != spine.Contains('K') || spine.Contains('L') && spine.IndexOf('K') > spine.IndexOf('L'))
                        throw new FormatException("L(잠긴 문)은 그 앞의 K(열쇠 방)와 함께 둡니다.");
                    if (branches.Count > 0 && !spine.Contains('C')) throw new FormatException("곁가지는 C(무리 방)에 붙으므로 spine에 C가 있어야 합니다.");
                    string loop = row["loop"].ToLowerInvariant();
                    if (loop != "" && loop != "shortcut") throw new FormatException("loop는 shortcut 또는 빈칸입니다.");
                    if (loop == "shortcut" && !spine.Contains('R')) throw new FormatException("shortcut은 R(쉼터)에서 입구로 이어지므로 spine에 R이 있어야 합니다.");
                    graphs.Add(new DungeonGraph(id, spine, branches, loop == "shortcut"));
                }
                catch (FormatException e)
                {
                    errors.Add($"{GraphsFile} {row.Line}행: {e.Message}");
                }
            }
            return graphs;
        }

        // "E C C R B X": the letters, each one of those allowed.
        static List<char> Letters(string text, string allowed, string label)
        {
            var letters = new List<char>();
            foreach (var part in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length != 1 || allowed.IndexOf(part[0]) < 0) throw new FormatException($"{label}의 '{part}'는 {string.Join(", ", allowed.ToCharArray())} 가운데 하나입니다.");
                letters.Add(part[0]);
            }
            return letters;
        }

        // The role a letter's room takes its chunk by (rooms.csv where = dungeon): the entrance and the end are gate rooms
        // (their posts are the doors out), the boss room and the champions' are an elite's.
        public static RoomRole RoleOf(char letter) => letter switch
        {
            'E' or 'X' => RoomRole.Gate,
            'T' => RoomRole.Treasure,
            'H' => RoomRole.Hidden,
            'S' => RoomRole.Shrine,
            'R' => RoomRole.Rest,
            'M' or 'B' => RoomRole.Elite,
            _ => RoomRole.Pack,
        };

        // What a letter's room is called on the screen and in the playtests.
        public static string NameOf(char letter) => letter switch
        {
            'E' => "입구",
            'C' => "무리 방",
            'T' => "보물 방",
            'S' => "제단",
            'K' => "열쇠 방",
            'L' => "잠긴 방",
            'M' => "챔피언 방",
            'R' => "쉼터",
            'B' => "보스 방",
            'X' => "끝",
            'H' => "숨은 방",
            _ => "방",
        };

        // A room of the letter holds a pack.
        public static bool HasPack(char letter) => letter == 'C' || letter == 'K' || letter == 'L' || letter == 'M' || letter == 'B';
    }
}
