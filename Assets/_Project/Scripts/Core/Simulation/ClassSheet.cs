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
        // Calls spirits that fight beside the player for a while (Minion).
        Summon,
        // Rushes the player the way they face, striking what lies along the way (none when damage is 0).
        Dash,
        // Gives back that percent of the player's most health.
        Heal,
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
            string description,
            int count = 1,
            int mana = 0,
            int level = 1)
        {
            Mana = mana;
            Level = Math.Max(1, level);
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
            Count = count;
        }

        public string Id { get; }
        // Its place in the sheet (ClassData.Skills), which the network uses to name it.
        public int Index { get; internal set; }
        public string Name { get; }
        public ClassDefinition Class { get; }
        public SkillKind Kind { get; }
        public SkillTag Tags { get; }
        // Percent of the player's damage for the skill's tags: for a summon, each spirit's every hit.
        public int Damage { get; }
        // Seconds.
        public float Cooldown { get; }
        // What using it costs (PlayerStats.MaxMana); 0 for nothing.
        public int Mana { get; }
        // The character level it can be learned from (Docs/Classes.md, 스킬 포인트와 단계).
        public int Level { get; }
        // A class's first skill: known at rank 1 without a point while the class is chosen.
        public bool IsBasic => Class != null && Class.Skills.Count > 0 && Class.Skills[0] == this;
        // A nova's reach, or how wide a projectile bursts (0: only what it hits).
        public float Radius { get; }
        // A projectile's tiles per second and how far it flies. For a summon, how fast its spirits move and how far
        // from the player they go after enemies.
        public float Speed { get; }
        public float Range { get; }
        // Seconds a buff or a summon's spirits last.
        public float Duration { get; }
        // How many projectiles fly at once, fanned out (1 for a single one), or how many spirits a summon calls.
        public int Count { get; }
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

    // A point on the passive tree (Docs/Classes.md, 패시브 트리): what it gives, where it is drawn, and the points
    // next to it. A point can be taken once one of the points it links to has been, starting from the start.
    public sealed class PassiveNode
    {
        readonly List<PassiveNode> _links = new List<PassiveNode>();

        internal PassiveNode(string id, string name, int x, int y, IReadOnlyList<Modifier> modifiers, bool notable, bool start)
        {
            Id = id;
            Name = name;
            X = x;
            Y = y;
            Modifiers = modifiers;
            Notable = notable;
            IsStart = start;
        }

        public string Id { get; }
        public string Name { get; }
        // Its place on the drawn tree, in steps; y grows downward like the screen.
        public int X { get; }
        public int Y { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        // A bigger point, drawn larger.
        public bool Notable { get; }
        // Where everyone begins: always taken, worth nothing, costs nothing.
        public bool IsStart { get; }
        public IReadOnlyList<PassiveNode> Links => _links;

        internal void Link(PassiveNode other)
        {
            if (other == this || _links.Contains(other)) return;
            _links.Add(other);
            other._links.Add(this);
        }
    }

    public sealed class ClassData
    {
        internal ClassData(IReadOnlyList<ClassDefinition> classes, IReadOnlyList<SkillDefinition> skills, IReadOnlyList<string> errors,
            IReadOnlyList<PassiveNode> passives = null)
        {
            Classes = classes;
            Skills = skills;
            Errors = errors;
            Passives = passives ?? Array.Empty<PassiveNode>();
        }

        // The passive tree in sheet order; the network names points by their index here. Empty without the sheet.
        public IReadOnlyList<PassiveNode> Passives { get; }
        public PassiveNode PassiveStart => Passives.FirstOrDefault(p => p.IsStart);

        public PassiveNode FindPassive(string id) => Passives.FirstOrDefault(p => p.Id == id);

        public IReadOnlyList<ClassDefinition> Classes { get; }
        // Every skill in sheet order. The network names skills by their index here.
        public IReadOnlyList<SkillDefinition> Skills { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public ClassDefinition Find(string id) =>
            Classes.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException($"Unknown class '{id}'.", nameof(id));
    }

    // How chosen classes turn into a player's kit. See Docs/Classes.md.
    // What a skill's rank does (Docs/Classes.md, 스킬 포인트와 단계): each rank past the first adds 15% to its damage
    // (a spirit's every hit, a heal's amount), takes 5% off its cooldown and adds 10% to a buff's or spirits' time.
    // A skill's power is its learned rank plus the ranks the passive tree and gear add for its tags (SkillRank), up to
    // MaxPower; cooldowns and durations also take the player's CooldownRecovery and SkillDuration.
    public static class SkillRules
    {
        public const int MaxRank = 5;
        // Learned ranks and added ones together.
        public const int MaxPower = MaxRank + 3;
        const float DamagePerRank = 0.15f;
        const float CooldownPerRank = 0.05f;
        const float DurationPerRank = 0.1f;

        static int Above(int power) => Math.Clamp(power, 1, MaxPower) - 1;

        public static float DamageScale(int power) => 1 + DamagePerRank * Above(power);

        public static int CooldownTicks(SkillDefinition skill, int power, float recovery = 0) =>
            Math.Max(1, (int)Math.Round(skill.CooldownTicks * (1 - CooldownPerRank * Above(power)) / (1 + Math.Max(0, recovery) / 100)));

        public static int DurationTicks(SkillDefinition skill, int power, float bonus = 0) =>
            (int)Math.Round(skill.DurationTicks * (1 + DurationPerRank * Above(power) + Math.Max(0, bonus) / 100));

        // The rank a learned skill works at: its own and what the stats add for its tags; 0 for one not learned.
        public static int Power(int rank, PlayerStats stats, SkillDefinition skill) =>
            rank <= 0 ? 0 : Math.Min(MaxPower, rank + (stats?.SkillRankFor(skill.Tags) ?? 0));

        // Coins to forget every learned skill and have the points back (World.ResetSkills): so many a point spent.
        public const int ResetCoinsPerPoint = 10;

        public static int SpentPoints(IEnumerable<KeyValuePair<SkillDefinition, int>> ranks) => ranks.Sum(pair => pair.Value - (pair.Key.IsBasic ? 1 : 0));

        // Whether a player could put a point in the skill now: one of their classes', their level high enough, a point
        // to spend, and not yet at the top rank.
        public static bool CanRaise(Player player, SkillDefinition skill) =>
            CanRaise(skill, player.Classes, player.Level, player.SkillPoints, player.RankOf(skill));

        public static bool CanRaise(SkillDefinition skill, IEnumerable<ClassDefinition> classes, int level, int points, int rank) =>
            skill != null && classes.Contains(skill.Class) && level >= skill.Level && points > 0 && rank < MaxRank;

        // The skill points a character of the level has, one a level past the first, less those spent (a class's
        // first skill's first rank costs none).
        public static int PointsLeft(int level, IEnumerable<KeyValuePair<SkillDefinition, int>> ranks) => Math.Max(0, level - 1 - SpentPoints(ranks));
    }

    public static class ClassRules
    {
        // How many classes a player may mix unless the host says otherwise (HostConfig.MaxClasses).
        public const int DefaultMaxClasses = 3;
        public const int SkillSlots = 4;
        // Each class brings up to this many skills; the player learns them with skill points and picks which
        // SkillSlots of the learned ones go in the slots.
        public const int MaxSkillsPerClass = 6;
        // Class passives are made for two classes; with more, each counts this share of two (2/3 for three), so
        // mixing more classes widens a build without making it stronger.
        public const int PassiveClasses = 2;

        public static float PassiveShare(int classes) => classes > PassiveClasses ? (float)PassiveClasses / classes : 1;

        // The level each class slot opens at (2026-11-09, the owner's call): a new character has no class and picks one
        // in the game, a second at level 10 and a third at 20 (about the antlion and the tide serpent). Docs/Classes.md.
        public static readonly int[] SlotLevels = { 1, 10, 20 };

        // How many classes a character of the level may have.
        public static int SlotsAt(int level, int max = DefaultMaxClasses) => Math.Min(max, SlotLevels.Count(l => level >= l));

        // The level the next class slot opens at, or 0 when every one is open.
        public static int NextSlotLevel(int level) => SlotLevels.FirstOrDefault(l => l > level);

        // Coins to give a class up for another (World.ChangeClasses, 2026-11-09): more the further the character has
        // come, 30 at level 1 and 520 at 50. Taking a class into an open slot and reordering stay free.
        public static int ChangeCoins(int level) => 20 + 10 * Math.Max(1, level);

        // Coins a point to take the whole passive tree back (World.ResetPassives), half what a skill point costs.
        public const int PassiveResetCoinsPerPoint = 5;

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

        // Every skill the chosen classes have, each class's in sheet order, the classes in the order chosen: what the
        // slots are filled from.
        public static List<SkillDefinition> AllSkillsFor(IEnumerable<ClassDefinition> classes) => classes.SelectMany(c => c.Skills).ToList();

        // The skills in the slots unless the player picks: the known ones (all of them when known is null, as for
        // another player's view), each class's first skills, an equal share of the slots each (two each for two
        // classes; one each for three), class by class, then the next known ones to fill what is left.
        public static List<SkillDefinition> SkillsFor(IEnumerable<ClassDefinition> classes, Func<SkillDefinition, bool> known = null)
        {
            var list = classes.ToList();
            if (list.Count == 0) return new List<SkillDefinition>();
            known ??= _ => true;
            int share = Math.Max(1, SkillSlots / list.Count);
            var skills = list.SelectMany(c => c.Skills.Where(known).Take(share)).Take(SkillSlots).ToList();
            skills.AddRange(AllSkillsFor(list).Where(s => known(s) && !skills.Contains(s)).Take(SkillSlots - skills.Count));
            return skills;
        }

        // The skills the player picked for the slots: order[i] is which of AllSkillsFor's goes in slot i. Only known
        // skills may be slotted; anything but as many different known ones as the slots hold (or as are known)
        // leaves the defaults.
        public static List<SkillDefinition> SkillsFor(IEnumerable<ClassDefinition> classes, IReadOnlyList<int> order, Func<SkillDefinition, bool> known = null)
        {
            var all = AllSkillsFor(classes);
            known ??= _ => true;
            int knownCount = all.Count(known);
            return IsOrder(order, all.Count) && order.Count == Math.Min(SkillSlots, knownCount) && order.All(i => known(all[i]))
                ? order.Select(i => all[i]).ToList()
                : SkillsFor(classes, known);
        }

        // Exactly the skills the order names (as the host has put them in the slots), or the defaults when it is no
        // order: how a client reads the kit the host sends.
        public static List<SkillDefinition> Slotted(IEnumerable<ClassDefinition> classes, IReadOnlyList<int> order)
        {
            var all = AllSkillsFor(classes);
            return IsOrder(order, all.Count) && order.Count > 0 ? order.Select(i => all[i]).ToList() : SkillsFor(classes);
        }

        public static bool IsOrder(IReadOnlyList<int> order, int count) =>
            order != null && order.Count <= SkillSlots && order.Count <= count && order.All(i => i >= 0 && i < count) && order.Distinct().Count() == order.Count;
    }

    // Reads Assets/_Project/Data/Classes: classes.csv for the classes and skills.csv for their skills.
    // Columns are explained in Docs/Classes.md.
    public static class ClassSheet
    {
        public const string ClassesFile = "classes.csv";
        public const string SkillsFile = "skills.csv";
        public const string PassivesFile = "passives.csv";
        // Most projectiles in one volley, or spirits in one summon.
        public const int MaxCount = 8;

        // Without passivesCsv (an older asset) there is no passive tree.
        public static ClassData Parse(string classesCsv, string skillsCsv, string passivesCsv = null)
        {
            var errors = new List<string>();
            var classes = ParseClasses(classesCsv, errors);
            var skills = ParseSkills(skillsCsv, classes, errors);
            var passives = string.IsNullOrWhiteSpace(passivesCsv) ? new List<PassiveNode>() : ParsePassives(passivesCsv, errors);
            return new ClassData(classes, skills, errors, passives);
        }

        // passives.csv: id, name, x, y, links (ids of points next to it, | between), stats (as a class passive),
        // kind (start, notable, or empty for a small point). Links go both ways; a link may name a later row.
        static List<PassiveNode> ParsePassives(string csv, List<string> errors)
        {
            var nodes = new List<PassiveNode>();
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "name", "x", "y", "links", "stats", "kind");
            if (missing.Count > 0)
            {
                errors.Add($"{PassivesFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return nodes;
            }
            var links = new List<(PassiveNode Node, string Links, int Line)>();
            foreach (var row in table.Rows)
            {
                TryRow(PassivesFile, row, errors, () =>
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (nodes.Any(n => n.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    string kind = row["kind"];
                    if (kind != "" && kind != "start" && kind != "notable") throw new FormatException($"kind '{kind}'는 start, notable이나 빈칸입니다.");
                    int x = SheetValues.ParseSigned(row["x"], "x");
                    int y = SheetValues.ParseSigned(row["y"], "y");
                    if (nodes.Any(n => n.X == x && n.Y == y)) throw new FormatException($"({x}, {y})에 이미 다른 점이 있습니다.");
                    var node = new PassiveNode(id, Required(row, "name"), x, y, FixedModifiers(row["stats"]), kind == "notable", kind == "start");
                    nodes.Add(node);
                    links.Add((node, row["links"], row.Line));
                });
            }
            foreach (var (node, cell, line) in links)
            {
                foreach (string other in cell.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
                {
                    var found = nodes.FirstOrDefault(n => n.Id == other);
                    if (found == null) errors.Add($"{PassivesFile} {line}행: 이어진 점 '{other}'가 없습니다.");
                    else node.Link(found);
                }
            }
            int starts = nodes.Count(n => n.IsStart);
            if (nodes.Count > 0 && starts != 1) errors.Add($"{PassivesFile}: 시작점(kind start)이 {starts}개입니다. 하나여야 합니다.");
            // Every point must be reachable from the start.
            var start = nodes.FirstOrDefault(n => n.IsStart);
            if (start != null)
            {
                var reached = new HashSet<PassiveNode> { start };
                var queue = new Queue<PassiveNode>(reached);
                while (queue.Count > 0)
                {
                    foreach (var next in queue.Dequeue().Links.Where(reached.Add)) queue.Enqueue(next);
                }
                foreach (var lost in nodes.Where(n => !reached.Contains(n))) errors.Add($"{PassivesFile}: '{lost.Id}'는 시작점에서 이어지지 않습니다.");
            }
            return nodes;
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
                    if (owner.Skills.Count >= ClassRules.MaxSkillsPerClass)
                        throw new FormatException(
                            $"'{owner.Id}'의 스킬이 {ClassRules.MaxSkillsPerClass}개를 넘습니다.");

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
                        row["description"],
                        row["count"].Length == 0 ? 1 : Whole(row, "count"),
                        Whole(row, "mana"),
                        row["level"].Length > 0 ? Whole(row, "level") : 1);
                    Validate(skill);
                    skill.Index = skills.Count;
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
            if (skill.Mana < 0 || skill.Mana > PlayerStats.BaseMana) throw new FormatException($"mana는 0에서 {PlayerStats.BaseMana} 사이여야 합니다.");
            if (skill.Count < 1 || skill.Count > MaxCount) throw new FormatException($"count는 1에서 {MaxCount} 사이여야 합니다.");
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
                case SkillKind.Summon:
                    Need(skill.Damage > 0, "damage");
                    Need(skill.Speed > 0, "speed");
                    Need(skill.Range > 0, "range");
                    Need(skill.Duration > 0, "duration");
                    break;
                case SkillKind.Dash:
                    Need(skill.Range > 0, "range");
                    Need(skill.Damage == 0 || skill.Radius > 0, "damage가 있으면 radius");
                    break;
                case SkillKind.Heal:
                    Need(skill.Damage > 0 && skill.Damage <= 100, "damage(1~100, 최대 생명력의 %)");
                    break;
            }
            if (skill.IsBasic && skill.Level > 1) throw new FormatException("클래스의 첫 스킬(기본 스킬)은 level이 1이어야 합니다.");
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
