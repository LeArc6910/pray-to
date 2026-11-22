using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // One kind of monster in a pack and how many of it (packs.csv members: "slime:4-6", "cave_beetle:1").
    public sealed class PackMember
    {
        internal PackMember(MonsterDefinition monster, int min, int max)
        {
            Monster = monster;
            Min = min;
            Max = max;
        }

        public MonsterDefinition Monster { get; }
        public int Min { get; }
        public int Max { get; }
    }

    // A pack of monsters that lives in a pack room (a row of packs.csv, Docs/WorldRework.md 무리, Docs/Combat.md 무리).
    public sealed class PackDefinition
    {
        internal PackDefinition(string id, Biome? biome, int zone, int weight, IReadOnlyList<PackMember> members, bool big, string place)
        {
            Id = id;
            Biome = biome;
            Zone = zone;
            Weight = weight;
            Members = members;
            Big = big;
            Place = place;
        }

        public string Id { get; }
        // Only in this biome's rooms, or null for the plain cave and any biome with no pack of its own there.
        public Biome? Biome { get; }
        // The zone level it comes from (World.ZoneLevelAt).
        public int Zone { get; }
        public int Weight { get; }
        // In sheet order: the first is the one more of come for more players and at night.
        public IReadOnlyList<PackMember> Members { get; }
        // Only in a big room (World.BigRoom), which holds two packs.
        public bool Big { get; }
        // The place of the story (places.csv id) whose rooms round it it keeps (monsters.csv place); null for the rest.
        public string Place { get; }
        public int MinCount => Members.Sum(m => m.Min);
        public int MaxCount => Members.Sum(m => m.Max);
    }

    public sealed class PackData
    {
        internal PackData(IReadOnlyList<PackDefinition> packs, IReadOnlyList<string> errors)
        {
            Packs = packs;
            Errors = errors;
        }

        public IReadOnlyList<PackDefinition> Packs { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public PackDefinition Find(string id) => Packs.FirstOrDefault(p => p.Id == id);
    }

    // Reads Assets/_Project/Data/Monsters/packs.csv onto the monsters MonsterSheet read (MonsterData.Packs). Columns
    // are explained in Docs/Combat.md (무리). Without the sheet a world has no packs and its monsters come one by one,
    // as they always did.
    public static class PackSheet
    {
        public const string File = "packs.csv";
        // A pack is never more than this many before players and the night add to it.
        public const int MaxMembers = 10;

        public static PackData Parse(string csv, MonsterData monsters)
        {
            var errors = new List<string>();
            var packs = new List<PackDefinition>();
            if (!string.IsNullOrWhiteSpace(csv))
            {
                var table = CsvTable.Read(csv);
                var missing = table.MissingColumns("id", "biome", "zone", "weight", "members");
                if (missing.Count > 0) errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                else
                {
                    foreach (var row in table.Rows)
                    {
                        try
                        {
                            string id = row["id"];
                            if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                            if (packs.Any(p => p.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                            Biome? biome = row["biome"].Length == 0 ? (Biome?)null : SheetValues.ParseEnum<Biome>(row["biome"], "biome");
                            int zone = SheetValues.ParseNumber(row["zone"], "zone");
                            if (zone < 1) throw new FormatException("zone은 1 이상입니다.");
                            int weight = SheetValues.ParseNumber(row["weight"], "weight");
                            string size = row["size"];
                            if (size != "" && size != "big") throw new FormatException($"size는 비우거나 big입니다('{size}').");
                            string place = row["place"].Length == 0 ? null : row["place"];
                            var members = Members(row["members"], monsters);
                            int most = members.Sum(m => m.Max);
                            if (most > MaxMembers) throw new FormatException($"한 무리는 많아야 {MaxMembers}마리입니다({most}).");
                            packs.Add(new PackDefinition(id, biome, zone, weight, members, size == "big", place));
                        }
                        catch (FormatException e)
                        {
                            errors.Add($"{File} {row.Line}행: {e.Message}");
                        }
                    }
                }
            }
            var data = new PackData(packs, errors);
            if (monsters != null) monsters.Packs = data;
            return data;
        }

        // "slime:4-6|cave_beetle:1": each kind and how many, at least one in all.
        static List<PackMember> Members(string cell, MonsterData monsters)
        {
            var members = new List<PackMember>();
            foreach (string part in cell.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                string[] pieces = part.Split(':');
                if (pieces.Length != 2) throw new FormatException($"members '{part}'는 slime:4-6처럼 적습니다.");
                var monster = monsters?.Monsters.FirstOrDefault(m => m.Id == pieces[0].Trim())
                    ?? throw new FormatException($"몬스터 '{pieces[0].Trim()}'가 monsters.csv에 없습니다.");
                if (monster.Pack == "solo") throw new FormatException($"'{monster.Id}'는 혼자 다니는 몬스터(pack solo)라 무리에 넣지 않습니다.");
                if (members.Any(m => m.Monster == monster)) throw new FormatException($"'{monster.Id}'가 두 번 있습니다.");
                string[] range = pieces[1].Trim().Split('-');
                int min = SheetValues.ParseNumber(range[0].Trim(), "members의 수");
                int max = range.Length > 1 ? SheetValues.ParseNumber(range[1].Trim(), "members의 수") : min;
                if (range.Length > 2 || min < 0 || max < min || max == 0) throw new FormatException($"members '{part}'의 수는 1, 2-4처럼 적습니다.");
                members.Add(new PackMember(monster, min, max));
            }
            if (members.Count == 0) throw new FormatException("members 칸이 비어 있습니다.");
            if (members.Sum(m => m.Min) == 0) throw new FormatException("무리는 적어도 한 마리입니다.");
            return members;
        }
    }
}
