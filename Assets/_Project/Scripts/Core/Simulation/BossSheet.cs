using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Two beams crossing on the target (or the boss), each length out from the centre, turned by angle. With a
        // count of two or more it is a star: 4 x count arms evenly round, each from radius out to length.
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
        // The boss plucks its own webs (2026-11-17, the silk mother): every web it has left on the floor is marked, a
        // little wider than itself, and they all burst together and are gone. With no web down, a circle of radius
        // under each player instead.
        Pluck,
        // The boss sheathes its blade and waits (2026-11-28, R1b, the shrine samurai's noto counter): struck while it
        // waits, it answers at once (World.CounterSeconds) with count cuts right through itself, each length out each
        // way and width across; left alone, it stands open for World.CounterOpening when the telegraph ends.
        Counter,
        // R2 (2026-11-28, Docs/BossRedesign.md 7.1 T3): every vent of the boss's (BossProp) bursts, count times
        // spread seconds apart, the vents taking turns, so the way through is between the beats.
        Vent,
        // A blast over the whole arena that spares whoever stands on cooled rock (PoolKind.Rock, an edge's islands);
        // with no rock down, a ring of it is laid round the boss (radius tiles past its body) for the telegraph.
        Refuge,
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
        // A colour over the boss that says where to be (2026-11-28, R1b, the black knight's last phase): Near, red, is
        // a blow that hurts less the closer one stands (get in close); Far, blue, hurts more the closer (get away).
        // Its area is marked on the ground in that colour (World.Falloff).
        Near,
        Far,
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

    // What a circle leaves on the ground after it lands (2026-11-13, the silk mother, boss_attacks.csv pool): a web
    // that snares whoever walks through it, or a pool of venom that hurts whoever stands in it.
    public enum PoolKind : byte
    {
        None,
        Web,
        Venom,
        // R2 (2026-11-28, T3): lava bites every World.GroundSeconds and slows (World.PlayerWadeSpeed), and cools into
        // rock when its time is up; shallow water only slows; a crack only bites; cooled rock does nothing and is
        // where a blast that spares rock cannot reach (AttackShape.Refuge), and where lava does not burn.
        Lava,
        Water,
        Crack,
        Rock,
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
            AttackWarning warning = AttackWarning.Marked,
            PoolKind pool = PoolKind.None,
            float linger = 0)
        {
            Warning = warning;
            Pool = pool;
            Linger = linger;
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
        public float Telegraph { get; private set; }
        public int Damage { get; private set; }
        // Circle radius, or projectile radius for a burst.
        public float Radius { get; private set; }
        // Cone, line and charge length in tiles from the boss's centre.
        public float Length { get; private set; }
        // Cone width in degrees, or the fan a burst spreads over (0 means all the way round).
        public float Angle { get; private set; }
        // Line and charge width in tiles.
        public float Width { get; private set; }
        // Circles per target, projectiles in a burst, or monsters summoned.
        public int Count { get; private set; }
        // How far extra circles scatter around their target, or how far from the boss summons appear.
        public float Spread { get; private set; }
        // Projectile or charge speed in tiles per second.
        public float Speed { get; private set; }
        // Seconds the boss rests after the attack lands.
        public float Cooldown { get; private set; }
        public int Weight { get; }
        public MonsterDefinition Summon { get; }
        // How plainly it is announced (warn column: blank, none or strong).
        public AttackWarning Warning { get; }

        // What each of its circles leaves behind once it has landed, and for how many seconds (pool column, "web:6").
        // A third part is the first phase it leaves it in ("lava:5:2"); a boost may change the seconds (linger).
        public PoolKind Pool { get; }
        public float Linger { get; private set; }
        public int PoolFrom { get; internal set; } = 1;

        // Another attack of the same boss laid at the same moment on the same target, landing FollowDelay seconds
        // after this one (2026-11-25, follow column "attack:seconds"). The gap is longer than a roll's unhurt moment and
        // shorter than its cooldown, so the roll that clears the first does not clear the second: the way out of one
        // is to walk, or to roll out of both at once.
        public BossAttack FollowUp { get; internal set; }
        public float FollowDelay { get; internal set; }

        // Whether it may come again straight after itself (boost repeat, and a raging boss's one more: World.Rage).
        public bool CanRepeat => !(Shape == AttackShape.Charge || Shape == AttackShape.Spiral || Shape == AttackShape.Summon
            || Shape == AttackShape.Counter || Shape == AttackShape.Vent || Shape == AttackShape.Refuge || Shape == AttackShape.Burrow
            || Shape == AttackShape.Leap || Shape == AttackShape.Pluck);

        // Whether it marks ground that lands by itself when the telegraph ends (what a follow-up may be).
        public bool LaysAreas => Shape switch
        {
            AttackShape.Circle or AttackShape.Cone or AttackShape.Line or AttackShape.Ring or AttackShape.Cross or AttackShape.Trail => true,
            _ => false,
        };

        // A burrow or a leap: the boss is away (out of reach) while it winds up and comes down on the target.
        public bool GoesAway => Shape == AttackShape.Burrow || Shape == AttackShape.Leap;

        public bool InPhase(int phase) => Phases.Count == 0 || Phases.Contains(phase);

        // ---- Phases made harder (2026-11-28, Docs/BossRedesign.md 4.2): the attack as a later phase uses it, built
        // when the sheet is read: its telegraph shorter by the boss's tempo (bosses.csv tempo) and its boost
        // (boss_attacks.csv boost) on top. The engine and the screen both read the phase's copy, so a wind-up drawn
        // for it lasts as long as the host's. The copies keep the sheet attack as their Base (its place in the list). ----

        // The attack as the sheet has it; itself for the sheet's own.
        public BossAttack Base => _base ?? this;
        BossAttack _base;
        // The phase this copy is for (0 for the sheet's own).
        public int ForPhaseNumber { get; private set; }
        // How many more times it comes straight after itself (boost repeat), each wound up in less time (Again) and
        // hitting softer (RepeatShare). A charge runs again by its count instead.
        public int Repeat { get; private set; }
        // How much of its telegraph a run after the first takes (World.AgainTelegraph unless a boost says).
        public float Again { get; private set; } = DefaultAgain;
        public const float DefaultAgain = 0.5f;
        // The most seconds it may wait past its telegraph before it lands (boost hold, 2026-11-28 R1b: the late blow).
        // The host waits 0.4 to 1 of it, rolled as the wind-up begins (World.StartTelegraph); its mark fills in its
        // telegraph and then blinks while it waits, so the wait is not given away, and World.CueSeconds before the
        // blow the boss gives its tell (SimEventType.Cue).
        public float Hold { get; private set; }
        // What a repeated blow hits for, of the first's (Docs/Difficulty.md: a blow that follows at once is a share).
        public const float RepeatShare = 0.6f;
        // The shortest telegraphs a phase may bring an attack down to: an ordinary one, a strong one (the person's
        // finishers), and a run after the first. One already shorter keeps its own.
        public const float LeastTelegraph = 0.5f;
        public const float LeastStrongTelegraph = 1.2f;
        public const float LeastAgainTelegraph = 0.3f;

        BossAttack[] _phases;

        // The copy a phase uses; itself when the boss has no copy for it.
        public BossAttack ForPhase(int phase) =>
            _phases != null && phase >= 1 && phase < _phases.Length && _phases[phase] != null ? _phases[phase] : this;

        // A run after the first: the same, wound up in less time and hitting for a share (Repeat).
        public BossAttack RepeatForm { get; private set; }

        // Seconds a run after the first winds up for, the same on the host and the screen. A charge's later run is
        // quick (a share of a hit, Docs/Difficulty.md); a repeated blow lands no sooner after the last than a roll is
        // ready again (World.RollCooldownSeconds), so each one can be rolled through.
        public float AgainSeconds => Shape == AttackShape.Charge
            ? Math.Max(Telegraph * Again, Math.Min(Telegraph, LeastAgainTelegraph))
            : Math.Max(Telegraph * Again, World.RollCooldownSeconds - RepeatGapSeconds);

        // The pause between a blow and its repeat: the follow-through, then the next wind-up (World.UpdateBoss).
        public const float RepeatGapSeconds = 0.15f;

        BossAttack Copy()
        {
            var copy = (BossAttack)MemberwiseClone();
            copy.RepeatForm = null;
            return copy;
        }

        // Builds every phase's copy: telegraph by tempo (phase 1 keeps the sheet's), then the boost for that phase.
        // Speeds of shots, charges and trails go up with the move tempo.
        internal void BuildPhases(int phaseCount, IReadOnlyList<float> telegraphTempo, IReadOnlyList<float> moveTempo, IReadOnlyDictionary<int, IReadOnlyList<Boost>> boosts)
        {
            _phases = new BossAttack[phaseCount + 1];
            for (int phase = 1; phase <= phaseCount; phase++)
            {
                var copy = phase == 1 ? this : Copy();
                copy._base = this;
                copy.ForPhaseNumber = phase;
                float floor = Warning == AttackWarning.Strong ? LeastStrongTelegraph : LeastTelegraph;
                float tempo = phase - 1 < telegraphTempo.Count ? telegraphTempo[phase - 1] : 1;
                float move = phase - 1 < moveTempo.Count ? moveTempo[phase - 1] : 1;
                if (copy != this)
                {
                    copy.Telegraph = Math.Max(Telegraph * tempo, Math.Min(Telegraph, floor));
                    if (Speed > 0) copy.Speed = Speed * move;
                }
                if (boosts.TryGetValue(phase, out var list))
                {
                    foreach (var boost in list) boost.Apply(copy);
                    if (copy.Telegraph < Math.Min(Telegraph, floor)) copy.Telegraph = Math.Min(Telegraph, floor);
                }
                if (copy.Repeat > 0 || CanRepeat)
                {
                    var again = copy.Copy();
                    again.Telegraph = copy.AgainSeconds;
                    again.Damage = Math.Max(1, (int)MathF.Round(copy.Damage * RepeatShare));
                    again.Repeat = 0;
                    again.Hold = 0;
                    copy.RepeatForm = again;
                }
                _phases[phase] = copy;
            }
        }

        // One change a phase makes to an attack (boost column, "count+1", "tele*0.8", "repeat=2").
        public readonly struct Boost
        {
            public Boost(string field, char op, float value)
            {
                Field = field;
                Op = op;
                Value = value;
            }

            public string Field { get; }
            public char Op { get; }
            public float Value { get; }

            public static readonly string[] Fields = { "count", "repeat", "tele", "again", "radius", "length", "angle", "width", "spread", "speed", "cooldown", "hold", "linger" };

            float On(float now) => Op == '+' ? now + Value : Op == '*' ? now * Value : Value;

            internal void Apply(BossAttack a)
            {
                switch (Field)
                {
                    case "count": a.Count = Math.Max(0, (int)MathF.Round(On(a.Count))); break;
                    case "repeat": a.Repeat = Math.Max(0, (int)MathF.Round(On(a.Repeat))); break;
                    case "tele": a.Telegraph = On(a.Telegraph); break;
                    case "again": a.Again = On(a.Again); break;
                    case "radius": a.Radius = On(a.Radius); break;
                    case "length": a.Length = On(a.Length); break;
                    case "angle": a.Angle = On(a.Angle); break;
                    case "width": a.Width = On(a.Width); break;
                    case "spread": a.Spread = On(a.Spread); break;
                    case "speed": a.Speed = On(a.Speed); break;
                    case "cooldown": a.Cooldown = On(a.Cooldown); break;
                    case "hold": a.Hold = Math.Max(0, On(a.Hold)); break;
                    case "linger": a.Linger = Math.Max(0, On(a.Linger)); break;
                }
            }
        }

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

        // How it closes in (2026-11-12, gait column): blank walks at its speed; burst darts in a sudden rush and then all
        // but stops, over and over (the shrine samurai), a little faster than walking on the whole.
        public bool Bursts { get; internal set; }

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
        // What its arena is made to look like and how it comes in (arenas.csv, ArenaSheet), or null with no row.
        public ArenaDefinition Arena { get; internal set; }

        internal void Add(BossAttack attack) => _attacks.Add(attack);

        // How much shorter its telegraphs are in each phase (bosses.csv tempo "0.85|0.75" for phases 2 and 3;
        // Docs/BossRedesign.md 4.2), and how much faster it walks, charges and shoots.
        public IReadOnlyList<float> TelegraphTempo { get; internal set; } = DefaultTelegraphTempo;
        public static readonly IReadOnlyList<float> DefaultTelegraphTempo = new[] { 1f, 0.85f, 0.75f };
        public static readonly IReadOnlyList<float> MoveTempo = new[] { 1f, 1.1f, 1.2f };

        // Seconds after a finisher (warn strong) starts before it may choose one again (bosses.csv finisher_rest,
        // 2026-11-28, the person: "the finishers could wait a little longer"). 0 lets it choose one at once.
        public float FinisherRest { get; internal set; } = DefaultFinisherRest;
        public const float DefaultFinisherRest = 12;

        // ---- R2 (2026-11-28, Docs/BossRedesign.md 7.1 T3·T9; BossTerrainSheet) ----
        // The ground that closes in from the arena's edge from a phase on (edge column), or null.
        public BossEdge Edge { get; internal set; }
        // What stands in the arena from each phase (props column; index by phase, null where it changes nothing).
        public IReadOnlyList<IReadOnlyList<(PropKind Kind, int Count)>> PropsByPhase { get; internal set; } = BossTerrainSheet.NoProps;
        // How it rages in each phase (rage column; index by phase, null for not at all).
        public IReadOnlyList<BossRage> RageByPhase { get; internal set; } = BossTerrainSheet.NoRage;
        public BossRage RageIn(int phase) => phase >= 0 && phase < RageByPhase.Count ? RageByPhase[phase] : null;
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
        // The arenas' sheet, once read (ArenaSheet.Parse); null before it, and every arena is plain.
        public ArenaData Arenas { get; internal set; }

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
                        row["element"].Length == 0 ? SkillTag.None : BossElement(row["element"])) { Level = Whole(row, "level"), Place = row["place"].Length == 0 ? null : row["place"], Bursts = Gait(row["gait"]), TelegraphTempo = Tempo(row["tempo"]), FinisherRest = FinisherRest(row["finisher_rest"]),
                        Edge = BossTerrainSheet.Edge(row["edge"], thresholds.Count + 1), PropsByPhase = BossTerrainSheet.Props(row["props"], thresholds.Count + 1),
                        RageByPhase = BossTerrainSheet.Rage(row["rage"], thresholds.Count + 1) });
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
            var follows = new List<(BossAttack, BossDefinition, string, int)>();
            var boosts = new Dictionary<BossAttack, IReadOnlyDictionary<int, IReadOnlyList<BossAttack.Boost>>>();

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
                    var pool = Pool(row["pool"], shape);
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
                        Warning(row["warn"]),
                        pool.Kind,
                        pool.Seconds) { PoolFrom = pool.From };
                    Validate(attack);
                    boosts[attack] = Boosts(row["boost"], boss.PhaseCount, shape);
                    boss.Add(attack);
                    if (row["follow"].Length > 0) follows.Add((attack, boss, row["follow"], row.Line));
                });
            }
            foreach (var (attack, boss, cell, line) in follows)
            {
                try
                {
                    var parts = cell.Split(':');
                    var next = boss.Attacks.FirstOrDefault(a => a.Id == parts[0].Trim());
                    if (parts.Length != 2 || next == null)
                        throw new FormatException($"follow는 '{boss.Id}'의 공격 id와 초를 :로 적습니다. 예: whirlpool:0.5('{cell}')");
                    float delay = SheetValues.ParseDecimal(parts[1].Trim(), "follow");
                    if (!(attack.LaysAreas || attack.GoesAway) || !next.LaysAreas || next == attack || next.FollowUp != null)
                        throw new FormatException("follow는 바닥에 떨어지는 공격(circle, cone, line, ring, cross, trail, 앞에는 burrow·leap도) 뒤에 다른 그런 공격 하나만 잇습니다.");
                    if (delay < LeastFollowDelay || delay > MostFollowDelay)
                        throw new FormatException($"follow의 초는 {LeastFollowDelay.ToString(CultureInfo.InvariantCulture)}~{MostFollowDelay.ToString(CultureInfo.InvariantCulture)}초여야 합니다(구르기의 무적보다 길고 다시 쓰기보다 짧게).");
                    attack.FollowUp = next;
                    attack.FollowDelay = delay;
                }
                catch (FormatException e)
                {
                    errors.Add($"{AttacksFile} {line}행: {e.Message}");
                }
            }
            BuildPhases(bosses, boosts);
        }

        // Every phase's copy of every attack, once all are read (BossAttack.BuildPhases).
        static void BuildPhases(List<BossDefinition> bosses, Dictionary<BossAttack, IReadOnlyDictionary<int, IReadOnlyList<BossAttack.Boost>>> boosts)
        {
            var none = new Dictionary<int, IReadOnlyList<BossAttack.Boost>>();
            foreach (var boss in bosses)
                foreach (var attack in boss.Attacks)
                    attack.BuildPhases(boss.PhaseCount, boss.TelegraphTempo, BossDefinition.MoveTempo, boosts.TryGetValue(attack, out var b) ? b : none);
        }

        // finisher_rest column: seconds before a finisher may be chosen again, blank for the usual (12).
        static float FinisherRest(string cell)
        {
            if (cell.Length == 0) return BossDefinition.DefaultFinisherRest;
            float seconds = SheetValues.ParseDecimal(cell, "finisher_rest");
            return seconds >= 0 && seconds <= 60 ? seconds : throw new FormatException("finisher_rest는 0~60초입니다.");
        }

        // tempo column: the telegraph factor of phases 2 and 3 ("0.85|0.75"), blank for the usual.
        static IReadOnlyList<float> Tempo(string cell)
        {
            if (cell.Length == 0) return BossDefinition.DefaultTelegraphTempo;
            var parts = SplitList(cell).Select(t => SheetValues.ParseDecimal(t, "tempo")).ToList();
            if (parts.Count == 0 || parts.Any(t => t <= 0.3f || t > 1))
                throw new FormatException("tempo는 2·3페이즈의 예고 배수를 0.3~1 사이로 적습니다. 예: 0.85|0.75");
            return new[] { 1f }.Concat(parts).ToList();
        }

        // boost column: "2:count+1 tele*0.8;3:count=5 repeat=1" (changes apart by spaces, phases by ;). Each phase from 2 on; each change is a field, + * or =,
        // and a number (BossAttack.Boost.Fields).
        static IReadOnlyDictionary<int, IReadOnlyList<BossAttack.Boost>> Boosts(string cell, int phaseCount, AttackShape shape)
        {
            var result = new Dictionary<int, IReadOnlyList<BossAttack.Boost>>();
            if (cell.Length == 0) return result;
            foreach (var part in cell.Split(';'))
            {
                var halves = part.Split(new[] { ':' }, 2);
                if (halves.Length != 2) throw new FormatException($"boost는 페이즈:바꿀 것으로 적습니다. 예: 2:count+1 tele*0.8;3:repeat=1('{part}')");
                int phase = SheetValues.ParseNumber(halves[0].Trim(), "boost");
                if (phase < 2 || phase > phaseCount) throw new FormatException($"boost의 페이즈는 2~{phaseCount}입니다(1페이즈는 시트 그대로).");
                if (result.ContainsKey(phase)) throw new FormatException($"boost에 {phase}페이즈가 두 번 있습니다.");
                var list = new List<BossAttack.Boost>();
                foreach (var change in halves[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string text = change.Trim();
                    int at = text.IndexOfAny(new[] { '+', '*', '=' });
                    if (at <= 0) throw new FormatException($"boost의 '{text}'는 count+1, tele*0.8, repeat=1처럼 적습니다.");
                    string field = text.Substring(0, at);
                    if (!BossAttack.Boost.Fields.Contains(field))
                        throw new FormatException($"boost의 '{field}'는 {string.Join(", ", BossAttack.Boost.Fields)} 중 하나여야 합니다.");
                    if (field == "repeat" && (shape == AttackShape.Charge || shape == AttackShape.Spiral || shape == AttackShape.Summon))
                        throw new FormatException("repeat는 돌진(count로), 나선, 부르기에는 쓰지 않습니다.");
                    list.Add(new BossAttack.Boost(field, text[at], SheetValues.ParseDecimal(text.Substring(at + 1).Trim(), "boost")));
                }
                result[phase] = list;
            }
            return result;
        }

        // The gap a follow-up lands after the first (follow column): past a roll's unhurt ticks with the host's slack
        // (World.DodgeSeconds + World.RollSlackTicks), and short of its cooldown (World.RollCooldownSeconds).
        public const float LeastFollowDelay = 0.3f;
        public const float MostFollowDelay = 0.8f;

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
                    Need(attack.Length > 0 && attack.Width > 0 && attack.Damage > 0 && attack.Radius >= 0 && attack.Radius < attack.Length, "length, width, damage(별이면 length보다 작은 radius)");
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
                case AttackShape.Pluck:
                    Need(attack.Radius > 0 && attack.Damage > 0, "radius(거미줄이 없을 때의 원), damage");
                    break;
                case AttackShape.Counter:
                    Need(attack.Length > 0 && attack.Width > 0 && attack.Count > 0 && attack.Damage > 0, "length, width, count(일섬 몇 줄), damage");
                    break;
                case AttackShape.Vent:
                    Need(attack.Radius > 0 && attack.Count > 0 && attack.Spread > 0 && attack.Damage > 0, "radius, count(몇 번), spread(사이 초), damage");
                    break;
                case AttackShape.Refuge:
                    Need(attack.Radius > 0 && attack.Damage > 0, "radius(바위가 없을 때 몸 둘레의 안전한 칸), damage");
                    break;
            }
            if ((attack.Warning == AttackWarning.Near || attack.Warning == AttackWarning.Far)
                && !(attack.Shape == AttackShape.Circle || attack.Shape == AttackShape.Cone || attack.Shape == AttackShape.Ring))
                throw new FormatException($"warn near·far는 보스에서 재는 공격(circle, cone, ring)에만 씁니다({attack.Shape}).");
        }

        // The gait column: blank (walks) or burst.
        static bool Gait(string cell) => cell switch
        {
            "" => false,
            "burst" => true,
            _ => throw new FormatException($"gait은 비우거나 burst여야 합니다('{cell}')."),
        };

        // The warn column: blank as before (marked on the ground), none (only the boss's wind-up shows it) or strong.
        static AttackWarning Warning(string cell) => cell switch
        {
            "" => AttackWarning.Marked,
            "none" => AttackWarning.Hidden,
            "strong" => AttackWarning.Strong,
            "near" => AttackWarning.Near,
            "far" => AttackWarning.Far,
            _ => throw new FormatException($"warn은 비우거나 none, strong, near, far 가운데 하나여야 합니다('{cell}')."),
        };

        // The pool column: blank, or what a circle leaves behind and for how long, as web:6 or venom:4.5. Only the
        // shapes that land as circles can leave one.
        static (PoolKind Kind, float Seconds, int From) Pool(string cell, AttackShape shape)
        {
            if (cell.Length == 0) return (PoolKind.None, 0, 1);
            var parts = cell.Split(':');
            PoolKind kind = parts[0] switch
            {
                "web" => PoolKind.Web,
                "venom" => PoolKind.Venom,
                "lava" => PoolKind.Lava,
                "water" => PoolKind.Water,
                "crack" => PoolKind.Crack,
                "rock" => PoolKind.Rock,
                _ => throw new FormatException($"pool은 비우거나 web, venom, lava, water, crack, rock 가운데 하나와 초입니다('{cell}')."),
            };
            if (parts.Length != 2 && parts.Length != 3) throw new FormatException($"pool은 lava:6처럼, 늦게 깔면 lava:5:2(2페이즈부터)처럼 적습니다('{cell}').");
            float seconds = SheetValues.ParseDecimal(parts[1], "pool");
            if (seconds <= 0 || seconds > 30) throw new FormatException("pool의 시간은 0보다 크고 30초 이하여야 합니다.");
            int from = parts.Length == 3 ? SheetValues.ParseNumber(parts[2], "pool") : 1;
            if (from < 1 || from > 3) throw new FormatException("pool의 페이즈는 1~3입니다.");
            bool circles = shape == AttackShape.Circle || shape == AttackShape.Trail || shape == AttackShape.Burrow || shape == AttackShape.Leap;
            // Ground (not webs or venom) may also lie along a charge's run and a cross's arms (2026-11-28, R2).
            bool strips = (shape == AttackShape.Charge || shape == AttackShape.Cross) && kind >= PoolKind.Lava;
            if (!circles && !strips)
                throw new FormatException($"pool은 원으로 떨어지는 공격(circle, trail, burrow, leap)에, 용암·물·균열·바위는 charge·cross에도 씁니다({shape}).");
            return (kind, seconds, from);
        }

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
