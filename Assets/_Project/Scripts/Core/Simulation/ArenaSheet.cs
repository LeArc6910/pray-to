using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What a boss's arena is made to look like (arenas.csv arena, Docs/Bosses.md 아레나 컨셉): the generator shapes
    // and dresses the room by it (WorldGenerator.DressArenas), and the view picks its floor by it.
    public enum ArenaKind : byte
    {
        // Bare: the round room of red flagstones every arena was before (2026-11-17), and any arena with no row.
        Plain,
        // A fallen shrine: eight-sided, broken pillars and statues round the rim, an inlaid ring.
        Ruin,
        // A crystal nest: webs across the floor, cocoons and crystal spires in the corners.
        Nest,
        // A lava basin: pools of lava in the rim, glowing cracks running in from them.
        Caldera,
        // A summoning altar: a great circle of runes on the floor, rune stones and braziers at its points.
        Altar,
        // A drowned hall: pools along the walls with planks over them, pillars standing in the water.
        Flooded,
        // A sand pit: rings going down to the middle, bones thrown out round the edge.
        Pit,
        // A war camp: banners, weapon racks, totems and fires round a trodden ground.
        Warcamp,
        // A sunken chapel: water in the corners, an aisle of runes up to a throne, lanterns.
        Chapel,
        // A mine gallery: rails across it, carts and heaps of ore, the floor cracked from below.
        Mine,
        // A duelling ground under cherry trees: a round stone court, a gate at each way in, petals on the floor.
        Dojo,
        // A throne hall of black iron: squared, a red carpet up the middle to the throne, armour and banners.
        Hall,
        // A crypt hung with webs: thick webs, cocoons, bones.
        Web,
    }

    // How a boss comes to be there when someone walks in (arenas.csv appear): the sort the fight's opening belongs to.
    public enum BossAppearance : byte
    {
        // There all along, still: a statue, someone sitting, something set hard in the rock.
        Wait,
        // Hidden under the floor, the water or the sand, and up out of it.
        Lurk,
        // Down from the roof.
        Descend,
        // Walks in.
        March,
        // Called up in a circle.
        Summon,
    }

    // The motion a boss comes in with (arenas.csv entrance). Common is the same for every boss that has no motion of
    // its own: the view turns to it, its name is shown, it stirs. The rest are a boss's own (the view's art).
    public enum BossEntrance : byte
    {
        Common,
        // Up through the floor as it splits (the vein eater).
        Burrow,
        // Down its own thread from the roof, the legs opening (the silk mother).
        Thread,
        // Up from sitting on its heels, the hand going to the hilt (the shrine samurai).
        Seated,
        // Walking out dragging its hammer, and the hammer brought down (the black knight).
        Stride,
        // A heap of stone that wakes: eyes and core lit, up on its feet, the floor pounded (the cave warden).
        Awaken,
    }

    public sealed class ArenaDefinition
    {
        internal ArenaDefinition(string boss, ArenaKind kind, BossAppearance appearance, BossEntrance entrance, float seconds, float again, string title)
        {
            Boss = boss;
            Kind = kind;
            Appearance = appearance;
            Entrance = entrance;
            Seconds = seconds;
            Again = again;
            Title = title;
        }

        public string Boss { get; }
        public ArenaKind Kind { get; }
        public BossAppearance Appearance { get; }
        public BossEntrance Entrance { get; }
        // How long the boss takes to come in the first time it wakes in a world, and every time after (seconds).
        public float Seconds { get; }
        public float Again { get; }
        // What it is called under its name on the card shown as it comes in.
        public string Title { get; }
    }

    public sealed class ArenaData
    {
        readonly Dictionary<string, ArenaDefinition> _byBoss = new Dictionary<string, ArenaDefinition>();
        readonly List<ArenaKind> _cave = new List<ArenaKind>();
        readonly Dictionary<string, ArenaKind> _place = new Dictionary<string, ArenaKind>();

        internal ArenaData(IEnumerable<ArenaDefinition> arenas, IReadOnlyList<BossDefinition> bosses, IReadOnlyList<string> errors)
        {
            foreach (var arena in arenas) _byBoss[arena.Boss] = arena;
            Errors = errors;
            // Where each boss stands, as WorldHost places them: those with no place in the caves' arenas in sheet
            // order, the rest in the arena beside their place.
            foreach (var boss in bosses)
            {
                var kind = Find(boss.Id)?.Kind ?? ArenaKind.Plain;
                if (boss.Place == null) _cave.Add(kind);
                else _place[boss.Place] = kind;
            }
        }

        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;
        public IEnumerable<ArenaDefinition> Arenas => _byBoss.Values;

        // A boss's row, or null for a boss with none (a plain arena, no entrance).
        public ArenaDefinition Find(string boss) => boss != null && _byBoss.TryGetValue(boss, out var arena) ? arena : null;

        // The kind of the nth of the caves' own arenas (GeneratedWorld.Arenas), and of the arena beside a place
        // (GeneratedWorld.PlaceArenas' key).
        public ArenaKind CaveKind(int index) => index >= 0 && index < _cave.Count ? _cave[index] : ArenaKind.Plain;
        public ArenaKind PlaceKind(string place) => place != null && _place.TryGetValue(place, out var kind) ? kind : ArenaKind.Plain;
    }

    // Reads Assets/_Project/Data/Bosses/arenas.csv: for each boss, what its arena is made to look like and how it
    // comes in (Docs/Bosses.md 아레나 컨셉과 입장 연출). A boss with no row has a plain arena and wakes at once, as
    // every boss did before the sheet.
    public static class ArenaSheet
    {
        public const string File = "arenas.csv";
        // The longest a boss may take to come in (seconds): nobody should stand waiting longer.
        public const float LongestEntrance = 6;

        public static ArenaData Parse(string csv, BossData bosses)
        {
            var errors = new List<string>();
            var arenas = new List<ArenaDefinition>();
            var known = bosses?.Bosses ?? new List<BossDefinition>();
            if (!string.IsNullOrWhiteSpace(csv))
            {
                var table = CsvTable.Read(csv);
                var missing = table.MissingColumns("boss", "arena", "appear", "entrance", "seconds", "again", "title");
                if (missing.Count > 0) errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                else
                {
                    foreach (var row in table.Rows)
                    {
                        try
                        {
                            string boss = row["boss"];
                            if (known.All(b => b.Id != boss)) throw new FormatException($"보스 '{boss}'가 bosses.csv에 없습니다.");
                            if (arenas.Any(a => a.Boss == boss)) throw new FormatException($"보스 '{boss}'의 줄이 이미 있습니다.");
                            float seconds = row["seconds"].Length == 0 ? 0 : SheetValues.ParseDecimal(row["seconds"], "seconds");
                            float again = row["again"].Length == 0 ? 0 : SheetValues.ParseDecimal(row["again"], "again");
                            if (seconds < 0 || seconds > LongestEntrance) throw new FormatException($"seconds는 0~{LongestEntrance}초입니다.");
                            if (again < 0 || again > seconds) throw new FormatException("again은 0초부터 seconds까지입니다.");
                            arenas.Add(new ArenaDefinition(
                                boss,
                                SheetValues.ParseEnum<ArenaKind>(row["arena"], "arena"),
                                SheetValues.ParseEnum<BossAppearance>(row["appear"], "appear"),
                                row["entrance"].Length == 0 ? BossEntrance.Common : SheetValues.ParseEnum<BossEntrance>(row["entrance"], "entrance"),
                                seconds,
                                again,
                                row["title"]));
                        }
                        catch (FormatException e)
                        {
                            errors.Add($"{File} {row.Line}행: {e.Message}");
                        }
                    }
                }
            }
            var data = new ArenaData(arenas, known, errors);
            // Each boss carries its own row, so the simulation and the view find it without the sheet.
            foreach (var boss in known) boss.Arena = data.Find(boss.Id);
            if (bosses != null) bosses.Arenas = data;
            return data;
        }
    }
}
