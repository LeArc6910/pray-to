using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // ---- R2 (2026-11-28, Docs/BossRedesign.md 7.1 T3·T9, Docs/Bosses.md "지형·물체·분노"): what bosses.csv says about the
    // ground and the things in the arena, and how a boss rages. ----

    // What closes in from the arena's edge (edge column): lava that bites and slows (with cooled islands in it), a wall
    // of petals that cuts, or webs that snare.
    public enum EdgeKind : byte
    {
        Lava,
        Petal,
        Web,
        // R3 (2026-11-28, the tide serpent's last phase): shallow water that only slows (World.PlayerWadeSpeed).
        Water,
    }

    // From Phase on, the outer Width tiles of the arena are Kind; Islands of cooled rock stand in lava's band.
    public sealed class BossEdge
    {
        public BossEdge(int phase, EdgeKind kind, float width, int islands)
        {
            Phase = phase;
            Kind = kind;
            Width = width;
            Islands = islands;
        }

        public int Phase { get; }
        public EdgeKind Kind { get; }
        public float Width { get; }
        public int Islands { get; }
    }

    // Something standing in a boss's arena (BossProp): a vent the boss bursts (AttackShape.Vent), a pillar that stops
    // the boss's shots and stuns it when it charges into it, a vein it eats to rage (R2b).
    public enum PropKind : byte
    {
        Vent,
        Pillar,
        Vein,
        // R3 (2026-11-28, the antlion): a sand pit that drags whoever is in it toward its middle (World.PitPull) and
        // bites there. A phase's first stands in the arena's middle, wide (World.BigPitRadius); the rest round it.
        Pit,
    }

    // How a boss rages in a phase (rage column, T9): it takes Hurt of its health (since the last) and then for Seconds
    // each attack comes once more (BossAttack.CanRepeat) and it walks Speed times as fast; then for Rest it cowers,
    // open (BossState.Spent).
    public sealed class BossRage
    {
        public BossRage(float hurt, float seconds, float rest, float speed)
        {
            Hurt = hurt;
            Seconds = seconds;
            Rest = rest;
            Speed = speed;
        }

        public float Hurt { get; }
        public float Seconds { get; }
        public float Rest { get; }
        public float Speed { get; }

        public const float DefaultSpeed = 1.2f;
    }

    public static class BossTerrainSheet
    {
        public static readonly IReadOnlyList<IReadOnlyList<(PropKind Kind, int Count)>> NoProps = new IReadOnlyList<(PropKind, int)>[0];
        public static readonly IReadOnlyList<BossRage> NoRage = new BossRage[0];

        // Most things standing for one boss at once (the snapshot carries them all, Docs/BossRedesign.md 7.1).
        public const int MaxProps = 6;

        // edge column: "3:lava:3:4" (from phase 3, the outer 3 tiles, four islands), "3:petal:2.5", "3:web:2".
        public static BossEdge Edge(string cell, int phaseCount)
        {
            if (cell.Length == 0) return null;
            var parts = cell.Split(':').Select(p => p.Trim()).ToArray();
            if (parts.Length < 3 || parts.Length > 4) throw new FormatException($"edge는 페이즈:종류:폭(:섬)으로 적습니다. 예: 3:lava:3:4('{cell}')");
            int phase = SheetValues.ParseNumber(parts[0], "edge");
            if (phase < 2 || phase > phaseCount) throw new FormatException($"edge의 페이즈는 2~{phaseCount}입니다.");
            var kind = parts[1] switch
            {
                "lava" => EdgeKind.Lava,
                "petal" => EdgeKind.Petal,
                "web" => EdgeKind.Web,
                "water" => EdgeKind.Water,
                _ => throw new FormatException($"edge의 종류는 lava, petal, web, water 가운데 하나입니다('{parts[1]}')."),
            };
            float width = SheetValues.ParseDecimal(parts[2], "edge");
            if (width < 1 || width > 4) throw new FormatException("edge의 폭은 1~4칸입니다(아레나 반지름 10칸).");
            int islands = parts.Length == 4 ? SheetValues.ParseNumber(parts[3], "edge") : 0;
            if (islands < 0 || islands > 6 || islands > 0 && kind != EdgeKind.Lava) throw new FormatException("edge의 섬은 용암에만 0~6개입니다.");
            return new BossEdge(phase, kind, width, islands);
        }

        // props column: "2:vent*3;3:vent*5" (from each phase, these replace what stood before).
        public static IReadOnlyList<IReadOnlyList<(PropKind Kind, int Count)>> Props(string cell, int phaseCount)
        {
            if (cell.Length == 0) return NoProps;
            var result = new IReadOnlyList<(PropKind, int)>[phaseCount + 1];
            foreach (var part in cell.Split(';'))
            {
                var halves = part.Split(new[] { ':' }, 2);
                if (halves.Length != 2) throw new FormatException($"props는 페이즈:종류*수로 적습니다. 예: 2:vent*3;3:vent*5('{part}')");
                int phase = SheetValues.ParseNumber(halves[0].Trim(), "props");
                if (phase < 1 || phase > phaseCount) throw new FormatException($"props의 페이즈는 1~{phaseCount}입니다.");
                if (result[phase] != null) throw new FormatException($"props에 {phase}페이즈가 두 번 있습니다.");
                var list = new List<(PropKind, int)>();
                foreach (var item in halves[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var bits = item.Split('*');
                    var kind = bits[0] switch
                    {
                        "vent" => PropKind.Vent,
                        "pillar" => PropKind.Pillar,
                        "vein" => PropKind.Vein,
                        "pit" => PropKind.Pit,
                        _ => throw new FormatException($"props의 종류는 vent, pillar, vein, pit 가운데 하나입니다('{bits[0]}')."),
                    };
                    int count = bits.Length == 2 ? SheetValues.ParseNumber(bits[1], "props") : 1;
                    if (bits.Length > 2 || count < 1) throw new FormatException($"props는 vent*3처럼 적습니다('{item}').");
                    list.Add((kind, count));
                }
                if (list.Sum(p => p.Item2) > MaxProps) throw new FormatException($"props는 한 페이즈에 {MaxProps}개까지입니다.");
                result[phase] = list;
            }
            return result;
        }

        // rage column: "2:hurt=0.1 seconds=20 rest=3;3:seconds=30". A later phase starts from the one before it.
        public static IReadOnlyList<BossRage> Rage(string cell, int phaseCount)
        {
            if (cell.Length == 0) return NoRage;
            var result = new BossRage[phaseCount + 1];
            var given = new Dictionary<int, string>();
            foreach (var part in cell.Split(';'))
            {
                var halves = part.Split(new[] { ':' }, 2);
                if (halves.Length != 2) throw new FormatException($"rage는 페이즈:hurt=몫 seconds=초 rest=초로 적습니다('{part}').");
                int phase = SheetValues.ParseNumber(halves[0].Trim(), "rage");
                if (phase < 1 || phase > phaseCount || given.ContainsKey(phase)) throw new FormatException($"rage의 페이즈는 1~{phaseCount}에서 한 번씩입니다.");
                given[phase] = halves[1];
            }
            BossRage last = null;
            for (int phase = 1; phase <= phaseCount; phase++)
            {
                if (given.TryGetValue(phase, out var text))
                {
                    float hurt = last?.Hurt ?? 0, seconds = last?.Seconds ?? 0, rest = last?.Rest ?? 0, speed = last?.Speed ?? BossRage.DefaultSpeed;
                    foreach (var change in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = change.Split('=');
                        if (kv.Length != 2) throw new FormatException($"rage의 '{change}'는 seconds=20처럼 적습니다.");
                        float value = SheetValues.ParseDecimal(kv[1], "rage");
                        switch (kv[0])
                        {
                            case "hurt": hurt = value; break;
                            case "seconds": seconds = value; break;
                            case "rest": rest = value; break;
                            case "speed": speed = value; break;
                            default: throw new FormatException($"rage의 '{kv[0]}'는 hurt, seconds, rest, speed 가운데 하나입니다.");
                        }
                    }
                    if (seconds <= 0 || seconds > 60 || rest < 0 || rest > 10 || hurt < 0 || hurt > 1 || speed < 1 || speed > 2)
                        throw new FormatException("rage는 seconds 0~60초, rest 0~10초, hurt 0~1(받은 피해 몫, 0이면 먹어서만), speed 1~2입니다.");
                    last = new BossRage(hurt, seconds, rest, speed);
                }
                result[phase] = last;
            }
            return result;
        }
    }
}
