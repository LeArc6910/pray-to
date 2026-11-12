using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum AttackShape : byte
    {
        // Circles on the ground that hurt whoever stands in them when the telegraph ends.
        Circle,
        // A wedge in front of the boss.
        Cone,
        // A straight beam from the boss.
        Line,
        // The boss rushes along a line and hurts whoever it runs into.
        Charge,
        // Projectiles flying out from the boss.
        Burst,
        // Monsters appearing around the boss.
        Summon,
        // A ring on the ground, from radius out to length: safe close to its centre (the boss, or a player).
        Ring,
        // Two beams crossing on the target (or the boss), each length out from the centre, turned by angle.
        Cross,
        // Circles from the boss toward the target, one after another, the blasts walking out at speed.
        Trail,
        // Volleys of projectiles all the way round, each turned by angle from the last: width volleys.
        Spiral,
        // A circle on the target whose damage is shared among everyone standing in it: co-op players stack up.
        Share,
        // The boss digs in and cannot be hurt while the telegraph lasts, then bursts up where the target stood: a
        // circle there, and count rocks flung all round at speed (2026-11-12).
        Burrow,
        // The same as Burrow through the air: the boss leaps up out of reach and lands on where the target stood.
        Leap,
    }

    // How plainly an attack is announced (2026-11-12, the vein eater). Marked is how every boss announced every attack
    // before: its area on the ground and the boss glowing the colour of its Tell. Hidden lays nothing on the ground
    // and gives no glow: only the boss's own wind-up shows it is coming (as Don't Starve's bosses do). Strong is for
    // the few blows that hurt most: the area on the ground and a warning sign over the boss.
    public enum AttackWarning : byte
    {
        Marked,
        Hidden,
        Strong,
    }

    // How a boss shows what is coming while it winds up (Docs/Bosses.md 전조 말, 2026-11-11, the research report's
    // candidate 9). Five kinds, each always drawn the same way for every boss, so a player who has learnt one fight
    // reads the next: Area, red marks filling in on the ground; Dash, a yellow path and the boss glowing yellow;
    // Gather, a gold circle pulsing to stand in together; Barrage, the boss flickering white with nothing on the
    // ground (shots are coming: keep moving); Call, the boss glowing violet (monsters are coming).
    public enum Tell : byte
    {
        None,
        Area,
        Dash,
        Gather,
        Barrage,
        Call,
    }

    public enum AttackTarget : byte
    {
        Nearest,
        Farthest,
        Random,
        // One copy of the attack for every player in the fight.
        Each,
        // Centred on the boss itself.
        Self,
    }

    public sealed class BossAttack
    {
        internal BossAttack(
            string id,
            string name,
            IReadOnlyList<int> phases,
            AttackShape shape,
            AttackTarget target,
            float telegraph,
            int damage,
            float radius,
            float length,
            float angle,
            float width,
            int count,
            float spread,
            float speed,
            float cooldown,
            int weight,
            MonsterDefinition summon,
            AttackWarning warning = AttackWarning.Marked)
        {
            Warning = warning;
            Id = id;
            Name = name;
            Phases = phases;
            Shape = shape;
            Target = target;
            Telegraph = telegraph;
            Damage = damage;
            Radius = radius;
            Length = length;
            Angle = angle;
            Width = width;
            Count = count;
            Spread = spread;
            Speed = speed;
            Cooldown = cooldown;
            Weight = weight;
            Summon = summon;
        }

        public string Id { get; }
        public string Name { get; }
        // Empty means every phase.
        public IReadOnlyList<int> Phases { get; }
        public AttackShape Shape { get; }
        public AttackTarget Target { get; }
        // Seconds of warning before it lands.
        public float Telegraph { get; }
        public int Damage { get; }
        // Circle radius, or projectile radius for a burst.
        public float Radius { get; }
        // Cone, line and charge length in tiles from the boss's centre.
        public float Length { get; }
        // Cone width in degrees, or the fan a burst spreads over (0 means all the way round).
        public float Angle { get; }
        // Line and charge width in tiles.
        public float Width { get; }
        // Circles per target, projectiles in a burst, or monsters summoned.
        public int Count { get; }
        // How far extra circles scatter around their target, or how far from the boss summons appear.
        public float Spread { get; }
        // Projectile or charge speed in tiles per second.
        public float Speed { get; }
        // Seconds the boss rests after the attack lands.
        public float Cooldown { get; }
        public int Weight { get; }
        public MonsterDefinition Summon { get; }
        // How plainly it is announced (warn column: blank, none or strong).
        public AttackWarning Warning { get; }

        // A burrow or a leap: the boss is away (out of reach) while it winds up and comes down on the target.
        public bool GoesAway => Shape == AttackShape.Burrow || Shape == AttackShape.Leap;

        public bool InPhase(int phase) => Phases.Count == 0 || Phases.Contains(phase);

        // How close the boss must get to its target before starting; cones, lines and charges need to reach.
        public float Range => Shape switch
        {
            AttackShape.Cone => Length * 0.8f,
            AttackShape.Line => Length * 0.9f,
            AttackShape.Charge => Length,
            AttackShape.Circle => 14,
            AttackShape.Share => 14,
            AttackShape.Trail => Length,
            AttackShape.Cross => 14,
            _ => float.PositiveInfinity,
        };
    }

    public sealed class BossDefinition
    {
        // The place of the story (places.csv id) whose arena the boss waits in, or null for the caves' own arenas in
        // sheet order (2026-11-10).
        public string Place { get; internal set; }

        readonly List<BossAttack> _attacks = new List<BossAttack>();

        internal BossDefinition(
            string id,
            string name,
            int health,
            int healthPerPlayer,
            int armor,
            float speed,
            float radius,
            IReadOnlyList<int> thresholds,
            float roar,
            float roarRadius,
            int roarDamage,
            int dropCount,
            int dropLevel,
            Rarity dropRarity,
            int runeCount,
            int potionCount,
            string materialId,
            int materialCount,
            SkillTag element = SkillTag.None)
        {
            Element = element;
            Id = id;
            Name = name;
            Health = health;
            HealthPerPlayer = healthPerPlayer;
            Armor = armor;
            Speed = speed;
            Radius = radius;
            Thresholds = thresholds;
            Roar = roar;
            RoarRadius = roarRadius;
            RoarDamage = roarDamage;
            DropCount = dropCount;
            DropLevel = dropLevel;
            DropRarity = dropRarity;
            RuneCount = runeCount;
            PotionCount = potionCount;
            MaterialId = materialId;
            MaterialCount = materialCount;
        }

        public string Id { get; }
        public string Name { get; }
        public int Health { get; }
        // Extra health, in percent of Health, for every player past the first.
        public int HealthPerPlayer { get; }
        public int Armor { get; }
        public float Speed { get; }
        public float Radius { get; }
        // Health percentages where the next phase starts, highest first. Two thresholds make three phases.
        public IReadOnlyList<int> Thresholds { get; }
        public int PhaseCount => Thresholds.Count + 1;
        // Seconds the boss roars between phases, unhurt, before a blast around itself.
        public float Roar { get; }
        public float RoarRadius { get; }
        public int RoarDamage { get; }
        // Items dropped for each player in the fight, at this item level and at least this rarity. Beating the
        // boss also raises every later drop to at least DropLevel, which is how a boss opens the next step of gear.
        public int DropCount { get; }
        public int DropLevel { get; }
        public Rarity DropRarity { get; }
        // Runes and consumables dropped for each player in the fight.
        public int RuneCount { get; }
        public int PotionCount { get; }
        // A material from materials.csv, and how many for each player in the fight.
        public string MaterialId { get; }
        public int MaterialCount { get; }
        // The character level the fight is made for (bosses.csv level, 2026-11-09; 0 when not given), shown on the
        // boss bar.
        public int Level { get; internal set; }
        // The element of every attack (bosses.csv element): fire burns whoever it hits, cold slows them.
        public SkillTag Element { get; }
        public IReadOnlyList<BossAttack> Attacks => _attacks;

        internal void Add(BossAttack attack) => _attacks.Add(attack);
    }

    public sealed class BossData
    {
        internal BossData(IReadOnlyList<BossDefinition> bosses, IReadOnlyList<string> errors)
        {
            Bosses = bosses;
            Errors = errors;
        }

        public IReadOnlyList<BossDefinition> Bosses { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public BossDefinition Find(string id) =>
            Bosses.FirstOrDefault(b => b.Id == id) ?? throw new ArgumentException($"Unknown boss '{id}'.", nameof(id));
    }

    // Reads Assets/_Project/Data/Bosses: bosses.csv for the bosses and boss_attacks.csv for what they do.
    // Columns are explained in Docs/Bosses.md.
    public static class BossSheet
    {
        public const string BossesFile = "bosses.csv";
        public const string AttacksFile = "boss_attacks.csv";

        public static BossData Parse(string bossesCsv, string attacksCsv, MonsterData monsters)
        {
            var errors = new List<string>();
            var bosses = ParseBosses(bossesCsv, errors);
            ParseAttacks(attacksCsv, bosses, monsters, errors);
            foreach (var boss in bosses)
            {
                for (int phase = 1; phase <= boss.PhaseCount; phase++)
                {
                    if (!boss.Attacks.Any(a => a.InPhase(phase) && a.Weight > 0))
                        errors.Add($"{AttacksFile}: '{boss.Id}'는 {phase}페이즈에 쓸 공격이 없습니다.");
                }
            }
            return new BossData(bosses, errors);
        }

        static List<BossDefinition> ParseBosses(string csv, List<string> errors)
        {
            var bosses = new List<BossDefinition>();
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns(
                "id", "name", "health", "health_per_player", "armor", "speed", "radius", "phases", "roar", "roar_radius", "roar_damage");
            if (missing.Count > 0)
            {
                errors.Add($"{BossesFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return bosses;
            }

            foreach (var row in table.Rows)
            {
                TryRow(BossesFile, row, errors, () =>
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (bosses.Any(b => b.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    var thresholds = SplitList(row["phases"]).Select(t => SheetValues.ParseNumber(t, "phases")).ToList();
                    if (thresholds.Any(t => t <= 0 || t >= 100)) throw new FormatException("phases는 1~99 사이 체력 비율이어야 합니다. 예: 66|33");
                    if (thresholds.Zip(thresholds.Skip(1), (a, b) => a <= b).Any(wrong => wrong))
                        throw new FormatException("phases는 큰 수부터 적습니다. 예: 66|33");

                    bosses.Add(new BossDefinition(
                        id,
                        Required(row, "name"),
                        Positive(SheetValues.ParseNumber(row["health"], "health"), "health"),
                        SheetValues.ParseNumber(row["health_per_player"], "health_per_player"),
                        SheetValues.ParseNumber(row["armor"], "armor"),
                        Positive(SheetValues.ParseDecimal(row["speed"], "speed"), "speed"),
                        Positive(SheetValues.ParseDecimal(row["radius"], "radius"), "radius"),
                        thresholds,
                        SheetValues.ParseDecimal(row["roar"], "roar"),
                        SheetValues.ParseDecimal(row["roar_radius"], "roar_radius"),
                        SheetValues.ParseNumber(row["roar_damage"], "roar_damage"),
                        Whole(row, "drop_count"),
                        Math.Max(1, Whole(row, "drop_level")),
                        row["drop_rarity"].Length == 0 ? Rarity.Common : SheetValues.ParseEnum<Rarity>(row["drop_rarity"], "drop_rarity"),
                        Whole(row, "rune_count"),
                        Whole(row, "potion_count"),
                        row["material"],
                        Whole(row, "material_count"),
                        row["element"].Length == 0 ? SkillTag.None : BossElement(row["element"])) { Level = Whole(row, "level"), Place = row["place"].Length == 0 ? null : row["place"] });
                });
            }
            return bosses;
        }

        static void ParseAttacks(string csv, List<BossDefinition> bosses, MonsterData monsters, List<string> errors)
        {
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns(
                "boss", "id", "name", "phases", "shape", "target", "telegraph", "damage", "radius", "length", "angle",
                "width", "count", "spread", "speed", "cooldown", "weight", "summon");
            if (missing.Count > 0)
            {
                errors.Add($"{AttacksFile}: 열이 없습니다: {string.Join(", ", missing)}");
                return;
            }

            foreach (var row in table.Rows)
            {
                TryRow(AttacksFile, row, errors, () =>
                {
                    var boss = bosses.FirstOrDefault(b => b.Id == row["boss"])
                        ?? throw new FormatException($"보스 '{row["boss"]}'가 {BossesFile}에 없습니다.");
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (boss.Attacks.Any(a => a.Id == id)) throw new FormatException($"'{boss.Id}'에 공격 id '{id}'가 이미 있습니다.");

                    var phases = SplitList(row["phases"]).Select(p => SheetValues.ParseNumber(p, "phases")).ToList();
                    if (phases.Any(p => p < 1 || p > boss.PhaseCount))
                        throw new FormatException($"phases는 1~{boss.PhaseCount} 사이여야 합니다 ('{boss.Id}'는 {boss.PhaseCount}페이즈).");

                    var shape = SheetValues.ParseEnum<AttackShape>(row["shape"], "shape");
                    var attack = new BossAttack(
                        id,
                        Required(row, "name"),
                        phases,
                        shape,
                        SheetValues.ParseEnum<AttackTarget>(row["target"], "target"),
                        Number(row, "telegraph"),
                        Whole(row, "damage"),
                        Number(row, "radius"),
                        Number(row, "length"),
                        Number(row, "angle"),
                        Number(row, "width"),
                        Whole(row, "count"),
                        Number(row, "spread"),
                        Number(row, "speed"),
                        Number(row, "cooldown"),
                        Whole(row, "weight"),
                        row["summon"].Length > 0 ? FindMonster(monsters, row["summon"]) : null,
                        Warning(row["warn"]));
                    Validate(attack);
                    boss.Add(attack);
                });
            }
        }

        // Each shape needs its own columns filled in; the rest may stay blank.
        static void Validate(BossAttack attack)
        {
            void Need(bool ok, string what)
            {
                if (!ok) throw new FormatException($"{attack.Shape} 공격에는 {what}이 필요합니다.");
            }

            Need(attack.Telegraph > 0, "0보다 큰 telegraph");
            switch (attack.Shape)
            {
                case AttackShape.Circle:
                    Need(attack.Radius > 0 && attack.Count > 0 && attack.Damage > 0, "radius, count, damage");
                    break;
                case AttackShape.Cone:
                    Need(attack.Length > 0 && attack.Angle > 0 && attack.Angle <= 360 && attack.Damage > 0, "length, angle(1~360), damage");
                    break;
                case AttackShape.Line:
                    Need(attack.Length > 0 && attack.Width > 0 && attack.Damage > 0, "length, width, damage");
                    break;
                case AttackShape.Charge:
                    Need(attack.Length > 0 && attack.Width > 0 && attack.Speed > 0 && attack.Damage > 0, "length, width, speed, damage");
                    break;
                case AttackShape.Burst:
                    Need(attack.Count > 0 && attack.Speed > 0 && attack.Radius > 0 && attack.Damage > 0, "count, speed, radius, damage");
                    break;
                case AttackShape.Summon:
                    Need(attack.Count > 0 && attack.Summon != null, "count, summon");
                    break;
                case AttackShape.Ring:
                    Need(attack.Radius > 0 && attack.Length > attack.Radius && attack.Damage > 0, "radius(안쪽), radius보다 큰 length(바깥), damage");
                    break;
                case AttackShape.Cross:
                    Need(attack.Length > 0 && attack.Width > 0 && attack.Damage > 0, "length, width, damage");
                    break;
                case AttackShape.Trail:
                    Need(attack.Count > 0 && attack.Radius > 0 && attack.Length > 0 && attack.Speed > 0 && attack.Damage > 0, "count, radius, length, speed, damage");
                    break;
                case AttackShape.Spiral:
                    Need(attack.Count > 0 && attack.Radius > 0 && attack.Speed > 0 && attack.Width >= 1 && attack.Damage > 0, "count, radius, speed, width(몇 번), damage");
                    break;
                case AttackShape.Share:
                    Need(attack.Radius > 0 && attack.Damage > 0, "radius, damage");
                    break;
                case AttackShape.Burrow:
                case AttackShape.Leap:
                    Need(attack.Radius > 0 && attack.Damage > 0 && (attack.Count == 0 || attack.Speed > 0), "radius, damage(돌을 흩뿌리면 count와 speed)");
                    break;
            }
        }

        // The warn column: blank as before (marked on the ground), none (only the boss's wind-up shows it) or strong.
        static AttackWarning Warning(string cell) => cell switch
        {
            "" => AttackWarning.Marked,
            "none" => AttackWarning.Hidden,
            "strong" => AttackWarning.Strong,
            _ => throw new FormatException($"warn은 비우거나 none, strong 가운데 하나여야 합니다('{cell}')."),
        };

        static MonsterDefinition FindMonster(MonsterData monsters, string id) =>
            monsters?.Monsters.FirstOrDefault(m => m.Id == id) ?? throw new FormatException($"몬스터 '{id}'가 {MonsterSheet.File}에 없습니다.");

        static float Number(CsvRow row, string column) => row[column].Length == 0 ? 0 : SheetValues.ParseDecimal(row[column], column);

        static int Whole(CsvRow row, string column) => row[column].Length == 0 ? 0 : SheetValues.ParseNumber(row[column], column);

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

        static string Required(CsvRow row, string column)
        {
            string value = row[column];
            if (value.Length == 0) throw new FormatException($"'{column}' 칸이 비어 있습니다.");
            return value;
        }

        static List<string> SplitList(string cell) =>
            cell.Split('|').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();

        static int Positive(int value, string label) =>
            value > 0 ? value : throw new FormatException($"{label}는 0보다 커야 합니다.");

        static float Positive(float value, string label) =>
            value > 0 ? value : throw new FormatException($"{label}는 0보다 커야 합니다.");

        static SkillTag BossElement(string cell)
        {
            var tag = SheetValues.ParseEnum<SkillTag>(cell, "element");
            return tag == SkillTag.Fire || tag == SkillTag.Cold || tag == SkillTag.Lightning
                ? tag
                : throw new FormatException($"element '{cell}'는 fire, cold, lightning 중 하나입니다.");
        }
    }
}
