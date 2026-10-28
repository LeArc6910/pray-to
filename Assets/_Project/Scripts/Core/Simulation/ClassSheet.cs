using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum SkillKind : byte
    {
        // Hits everything around the player.
        Nova,
        // Flies where the player aims and bursts on the first enemy or wall.
        Projectile,
        // Puts modifiers and tags on the player for a while.
        Buff,
    }

    public sealed class SkillDefinition
    {
        internal SkillDefinition(
            string id,
            string name,
            ClassDefinition owner,
            SkillKind kind,
            SkillTag tags,
            int damage,
            float cooldown,
            float radius,
            float speed,
            float range,
            float duration,
            IReadOnlyList<Modifier> modifiers,
            SkillTag grants,
            string description)
        {
            Id = id;
            Name = name;
            Class = owner;
            Kind = kind;
            Tags = tags;
            Damage = damage;
            Cooldown = cooldown;
            Radius = radius;
            Speed = speed;
            Range = range;
            Duration = duration;
            Modifiers = modifiers;
            Grants = grants;
            Description = description;
        }

        public string Id { get; }
        public string Name { get; }
        public ClassDefinition Class { get; }
        public SkillKind Kind { get; }
        public SkillTag Tags { get; }
        // Percent of the player's damage for the skill's tags.
        public int Damage { get; }
        // Seconds.
        public float Cooldown { get; }
        // A nova's reach, or how wide a projectile bursts (0: only what it hits).
        public float Radius { get; }
        // A projectile's tiles per second and how far it flies.
        public float Speed { get; }
        public float Range { get; }
        // Seconds a buff lasts.
        public float Duration { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        // Tags a buff adds to all of the player's attacks and skills.
        public SkillTag Grants { get; }
        public string Description { get; }

        public int CooldownTicks => World.Ticks(Cooldown);
        public int DurationTicks => World.Ticks(Duration);
    }

    public sealed class ClassDefinition
    {
        readonly List<SkillDefinition> _skills = new List<SkillDefinition>();

        internal ClassDefinition(string id, string name, string description, IReadOnlyList<Modifier> passive)
        {
            Id = id;
            Name = name;
            Description = description;
            Passive = passive;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        // Always on while the class is chosen, added up like gear.
        public IReadOnlyList<Modifier> Passive { get; }
        // In the order they take skill slots.
        public IReadOnlyList<SkillDefinition> Skills => _skills;

        internal void Add(SkillDefinition skill) => _skills.Add(skill);
    }

    public sealed class ClassData
    {
        internal ClassData(IReadOnlyList<ClassDefinition> classes, IReadOnlyList<SkillDefinition> skills, IReadOnlyList<string> errors)
        {
            Classes = classes;
            Skills = skills;
            Errors = errors;
        }

        public IReadOnlyList<ClassDefinition> Classes { get; }
        // Every skill in sheet order. The network names skills by their index here.
        public IReadOnlyList<SkillDefinition> Skills { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public ClassDefinition Find(string id) =>
            Classes.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException($"Unknown class '{id}'.", nameof(id));
    }

    // How chosen classes turn into a player's kit. See Docs/Classes.md.
    public static class ClassRules
    {
        // How many classes a player may mix unless the host says otherwise (HostConfig.MaxClasses).
        public const int DefaultMaxClasses = 2;
        public const int SkillSlots = 4;
        // So that DefaultMaxClasses classes always fit their skills into the slots.
        public const int SkillsPerClass = SkillSlots / DefaultMaxClasses;

        // The classes that count from a requested list: known ones, each once, at most max of them, in order.
        public static List<ClassDefinition> Choose(ClassData data, IEnumerable<string> ids, int max)
        {
            var chosen = new List<ClassDefinition>();
            if (data == null || ids == null) return chosen;
            foreach (string id in ids)
            {
                var found = data.Classes.FirstOrDefault(c => c.Id == id);
                if (found == null || chosen.Contains(found)) continue;
                if (chosen.Count >= max) break;
                chosen.Add(found);
            }
            return chosen;
        }

        // The skills in slot order: each class's skills, in the order the classes were chosen.
        public static List<SkillDefinition> SkillsFor(IEnumerable<ClassDefinition> classes) =>
            classes.SelectMany(c => c.Skills).Take(SkillSlots).ToList();
    }

    // Reads Assets/_Project/Data/Classes: classes.csv for the classes and skills.csv for their skills.
    // Columns are explained in Docs/Classes.md.
    public static class ClassSheet
    {
        public const string ClassesFile = "classes.csv";
        public const string SkillsFile = "skills.csv";

        public static ClassData Parse(string classesCsv, string skillsCsv)
        {
            var errors = new List<string>();
            var classes = ParseClasses(classesCsv, errors);
            var skills = ParseSkills(skillsCsv, classes, errors);
            return new ClassData(classes, skills, errors);
        }

        static List<ClassDefinition> ParseClasses(string csv, List<string> errors)
        {
            var classes = new List<ClassDefinition>();
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "name", "description", "passive");
            if (missing.Count > 0)
            {
                errors.Add($"{ClassesFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return classes;
            }

            foreach (var row in table.Rows)
            {
                TryRow(ClassesFile, row, errors, () =>
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (classes.Any(c => c.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    classes.Add(new ClassDefinition(id, Required(row, "name"), row["description"], FixedModifiers(row["passive"])));
                });
            }
            return classes;
        }

        static List<SkillDefinition> ParseSkills(string csv, List<ClassDefinition> classes, List<string> errors)
        {
            var skills = new List<SkillDefinition>();
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns(
                "id", "name", "class", "kind", "tags", "damage", "cooldown", "radius", "speed", "range", "duration", "modifiers", "grants", "description");
            if (missing.Count > 0)
            {
                errors.Add($"{SkillsFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return skills;
            }

            foreach (var row in table.Rows)
            {
                TryRow(SkillsFile, row, errors, () =>
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (skills.Any(s => s.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    var owner = classes.FirstOrDefault(c => c.Id == row["class"])
                        ?? throw new FormatException($"클래스 '{row["class"]}'가 {ClassesFile}에 없습니다.");
                    if (owner.Skills.Count >= ClassRules.SkillsPerClass)
                        throw new FormatException(
                            $"'{owner.Id}'의 스킬이 {ClassRules.SkillsPerClass}개를 넘습니다. 칸 {ClassRules.SkillSlots}개를 클래스 {ClassRules.DefaultMaxClasses}개가 나눠 씁니다.");

                    var skill = new SkillDefinition(
                        id,
                        Required(row, "name"),
                        owner,
                        SheetValues.ParseEnum<SkillKind>(row["kind"], "kind"),
                        ParseTags(row["tags"]),
                        Whole(row, "damage"),
                        Number(row, "cooldown"),
                        Number(row, "radius"),
                        Number(row, "speed"),
                        Number(row, "range"),
                        Number(row, "duration"),
                        FixedModifiers(row["modifiers"]),
                        ParseTags(row["grants"]),
                        row["description"]);
                    Validate(skill);
                    owner.Add(skill);
                    skills.Add(skill);
                });
            }
            return skills;
        }

        // Each kind needs its own columns filled in; the rest may stay blank.
        static void Validate(SkillDefinition skill)
        {
            string kind = skill.Kind.ToString().ToLowerInvariant();

            void Need(bool ok, string what)
            {
                if (!ok) throw new FormatException($"{kind} 스킬은 {what} 칸을 채워야 합니다.");
            }

            if (skill.Cooldown <= 0) throw new FormatException("cooldown은 0보다 커야 합니다.");
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    Need(skill.Damage > 0 && skill.Radius > 0, "damage, radius");
                    break;
                case SkillKind.Projectile:
                    Need(skill.Damage > 0, "damage");
                    Need(skill.Speed > 0, "speed");
                    Need(skill.Range > 0, "range");
                    break;
                case SkillKind.Buff:
                    Need(skill.Duration > 0, "duration");
                    Need(skill.Modifiers.Count > 0 || skill.Grants != SkillTag.None, "modifiers나 grants 중 하나");
                    break;
            }
        }

        // Classes and skills always give the same amount, so their modifiers are written without a range.
        static List<Modifier> FixedModifiers(string cell) =>
            ModifierSyntax.ParseList(cell)
                .Select(range => range.IsFixed
                    ? new Modifier(range.Stat, range.Kind, range.Min, range.Tags)
                    : throw new FormatException($"'{range}'처럼 범위로 적을 수 없습니다. 하나의 값으로 적습니다."))
                .ToList();

        static SkillTag ParseTags(string cell) =>
            cell.Split('|')
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .Aggregate(SkillTag.None, (tags, part) => tags | SheetValues.ParseEnum<SkillTag>(part, "태그"));

        static float Number(CsvRow row, string column) => row[column].Length == 0 ? 0 : SheetValues.ParseDecimal(row[column], column);

        static int Whole(CsvRow row, string column) => row[column].Length == 0 ? 0 : SheetValues.ParseNumber(row[column], column);

        static string Required(CsvRow row, string column)
        {
            string value = row[column];
            if (value.Length == 0) throw new FormatException($"'{column}' 칸이 비어 있습니다.");
            return value;
        }

        static void TryRow(string sheet, CsvRow row, List<string> errors, Action parse)
        {
            try
            {
                parse();
            }
            catch (FormatException e)
            {
                errors.Add($"{sheet} {row.Line}행: {e.Message}");
            }
        }
    }
}
