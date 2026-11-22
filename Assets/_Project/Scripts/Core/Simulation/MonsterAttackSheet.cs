using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    // What an ordinary monster's attack is (monster_attacks.csv shape, Docs/WorldRework.md 패턴 말). Most are a boss's
    // shape under another drawing; seven are new to the engine: Bounce, Boomerang, Blink, Pull, Mine, Mend, Empower.
    public enum MonsterAttackShape : byte
    {
        // A narrow wedge ahead.
        Strike,
        // Crouches, then hops onto where its target stood: a circle there.
        Leap,
        // A circle round itself.
        Slam,
        // A wide wedge; with count, that many one after another, turned to either side.
        Sweep,
        // A thin long line ahead.
        Thrust,
        // Marks a yellow path and rushes along it; with count, again and again.
        Charge,
        // A short rush through and past its target.
        Swoop,
        // Count shots fanned over angle.
        Spread,
        // Count shots one after another.
        Volley,
        // Thrown in an arc: a circle where the target stood; with count, another on them each LobSeconds.
        Lob,
        // A shot that comes off walls count times.
        Bounce,
        // A shot that flies length out and comes back to whoever threw it.
        Boomerang,
        // A thin aiming line that becomes a beam.
        Beam,
        // A circle round itself (or, with length, a ring from radius out to length).
        Nova,
        // Goes under (out of reach) and comes up under its target.
        Burrow,
        // Burrow from hiding: one with this attack waits under the ground, unseen but for its mound, until someone
        // comes within the attack's range (World.WaitsInAmbush).
        Ambush,
        // Marks a spot length away and is there a moment later. Aimed at itself it goes away from its target; aimed at
        // its target it is gone while the mark shows and comes out that far behind them (MonsterAttack.Behind).
        Blink,
        // A circle on its target that drags whoever is in it to its middle at speed, then bursts.
        Pull,
        // Count circles walking out from it toward its target; whoever is hit is shoved.
        Push,
        // Count traps round its target: armed after the telegraph, each goes off when stepped on or when its time is up.
        Mine,
        // A circle laid on its target with a long fuse.
        Delayed,
        // A circle that follows it while it runs at its target; then it bursts and dies.
        Explode,
        // Count of another monster appear round it.
        Summon,
        // A line to a hurt ally that fills it; hitting the mender breaks it.
        Mend,
        // Allies within radius hit harder and move faster for a while.
        Empower,
    }

    public enum MonsterAttackTarget : byte
    {
        Nearest,
        Self,
        Random,
        Ally,
    }

    // What an attack's areas leave on the floor (monster_attacks.csv pool). Web and venom are the bosses' own.
    public enum MonsterPool : byte
    {
        None,
        Fire,
        Ice,
        Spore,
        Web,
        Venom,
        Sand,
    }

    // What a hit of the attack does besides its damage (monster_attacks.csv effect).
    public enum HitEffect : byte
    {
        None,
        // Cannot walk for the seconds given.
        Root,
        // Shoved that many tiles away from the blow.
        Push,
        // Poisoned for that many seconds: World.PoisonShare of the blow again, a bite every half second.
        Venom,
        // Slowed for that many seconds, as by a cold monster's blow.
        Chill,
    }

    // How a monster fights and how many come together (monsters.csv role, Docs/WorldRework.md 소모전과 회피전).
    public enum MonsterRole : byte
    {
        None,
        Swarm,
        Bruiser,
        Shooter,
        Support,
        Heavy,
    }

    public enum MonsterTraitKind : byte
    {
        // Split:n — felled, n small ones come out of it (30% health, half its hits, no drops, never again).
        Split,
        // Guard:degrees — hits from within that arc ahead of it do World.GuardShare of their damage.
        Guard,
        // Trail:kind:seconds — leaves a pool where it walks.
        Trail,
        // Aura:guard:reach:percent — with another of its kind within reach, both take that percent less.
        Aura,
        // Deathburst:count:percent — where it fell flashes, then count shards fly out, each that percent of its damage.
        Deathburst,
        // Deathcloud — where it fell a cloud fills the monsters in it.
        Deathcloud,
        // Never walks.
        Rooted,
        // Burrowed:seconds — goes under the ground between attacks (out of reach, a ripple on the screen) and stays up
        // that long after each.
        Burrowed,
        // Orbit:tiles — never stands: with nothing to do it circles its target that far out.
        Orbit,
        // A charge that ends at a wall leaves it dazed: still, unguarded and taking more for World.StaggerSeconds.
        Stagger,
        // Turn:degrees — turns no faster than that a second, and only attacks what is in front of it: walk round it.
        Turn,
    }

    public sealed class MonsterTrait
    {
        internal MonsterTrait(MonsterTraitKind kind, IReadOnlyList<string> arguments)
        {
            Kind = kind;
            Arguments = arguments;
        }

        public MonsterTraitKind Kind { get; }
        public IReadOnlyList<string> Arguments { get; }

        // The n-th argument as a number, or the fallback when it was left out.
        public float Number(int index, float fallback) =>
            index < Arguments.Count && float.TryParse(Arguments[index], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }

    public sealed class MonsterAttack
    {
        internal MonsterAttack() { }

        public string Id { get; internal set; }
        public string Name { get; internal set; }
        // Its place among its monster's attacks, from 1: what the wire carries (World.EnemyPhaseField).
        public int Index { get; internal set; }
        public MonsterAttackShape Shape { get; internal set; }
        public MonsterAttackTarget Target { get; internal set; }
        // Seconds of warning before it lands.
        public float Telegraph { get; internal set; }
        // Percent of the monster's damage (which its zone and its rift have already scaled). Mend: percent of the ally's
        // health a second. Empower: percent more damage and pace.
        public int Damage { get; internal set; }
        public float Radius { get; internal set; }
        public float Length { get; internal set; }
        public float Angle { get; internal set; }
        public float Width { get; internal set; }
        public int Count { get; internal set; }
        public float Spread { get; internal set; }
        public float Speed { get; internal set; }
        // Seconds before the monster can pick this attack again (it rests its own monsters.csv cooldown after any).
        public float Cooldown { get; internal set; }
        public int Weight { get; internal set; }
        // Only when the gap to its target is between these.
        public float RangeMin { get; internal set; }
        public float RangeMax { get; internal set; }
        public MonsterPool Pool { get; internal set; }
        public float Linger { get; internal set; }
        // The pool's own radius when the sheet gives one (fire:4:0.9); 0 for the size of the blow that left it.
        public float PoolRadius { get; internal set; }
        // hp<N: below that percent of its health. once: one time in its life. allies>=N: that many others at hand.
        public int HealthBelow { get; internal set; }
        public bool Once { get; internal set; }
        public int AlliesAtLeast { get; internal set; }
        public AttackWarning Warning { get; internal set; }
        public HitEffect Effect { get; internal set; }
        public float EffectAmount { get; internal set; }
        // Mend and Empower: how long they last. Mine: how long a trap waits.
        public float Duration { get; internal set; }
        public MonsterDefinition Summon { get; internal set; }
        // The attack it goes straight into when this one is done, with no rest between (a blink, then a cut).
        public MonsterAttack Then { get; internal set; }
        internal string ThenId { get; set; }

        // A blink that comes out behind its target, the monster gone while its mark shows.
        public bool Behind => Shape == MonsterAttackShape.Blink && Target != MonsterAttackTarget.Self;

        public Tell Tell => MonsterAttackSheet.TellOf(Shape);

        // Whether its warning is drawn from the monster's own state (it starts at the monster and is aimed along its
        // facing), so no hazard goes over the wire for it (Docs/WorldRework.md 통신).
        public bool Attached => Shape switch
        {
            MonsterAttackShape.Strike or MonsterAttackShape.Slam or MonsterAttackShape.Sweep or MonsterAttackShape.Thrust
                or MonsterAttackShape.Charge or MonsterAttackShape.Swoop or MonsterAttackShape.Beam or MonsterAttackShape.Nova
                or MonsterAttackShape.Explode => true,
            _ => false,
        };

        // Lays a mark on the ground under its target (the cap on marks aimed at one player counts these).
        public bool MarksGround => Shape switch
        {
            MonsterAttackShape.Leap or MonsterAttackShape.Lob or MonsterAttackShape.Burrow or MonsterAttackShape.Ambush
                or MonsterAttackShape.Pull or MonsterAttackShape.Push or MonsterAttackShape.Mine or MonsterAttackShape.Delayed => true,
            _ => false,
        };
    }

    // Reads Assets/_Project/Data/Monsters/monster_attacks.csv into the monsters MonsterSheet has read. Columns are
    // explained in Docs/Combat.md (몬스터의 공격 시트).
    public static class MonsterAttackSheet
    {
        public const string File = "monster_attacks.csv";
        // The wire has three bits for a monster's attack (World.EnemyPhaseField).
        public const int MaxAttacks = 7;
        // A player walks five tiles a second and needs a quarter of a second to see a mark (Docs/WorldRework.md
        // 소모전과 회피전): every mark must leave time to walk out of it. The roll (4a) is not counted on yet.
        public const float WalkSpeed = 5;
        public const float NoticeSeconds = 0.25f;

        public static Tell TellOf(MonsterAttackShape shape) => shape switch
        {
            MonsterAttackShape.Charge or MonsterAttackShape.Swoop or MonsterAttackShape.Blink => Tell.Dash,
            MonsterAttackShape.Spread or MonsterAttackShape.Volley or MonsterAttackShape.Bounce or MonsterAttackShape.Boomerang => Tell.Barrage,
            MonsterAttackShape.Summon or MonsterAttackShape.Mend or MonsterAttackShape.Empower => Tell.Call,
            _ => Tell.Area,
        };

        // How far a player caught in the worst spot of the attack's mark has to walk to be out of it, in tiles; 0 for
        // attacks that mark no area (shots, rushes, calls).
        public static float WayOut(MonsterAttack attack)
        {
            float half = attack.Angle * 0.5f * MathF.PI / 180f;
            switch (attack.Shape)
            {
                case MonsterAttackShape.Strike:
                case MonsterAttackShape.Sweep:
                    // Out of the far end or out of the side, whichever is nearer.
                    return half >= MathF.PI / 2 ? attack.Length : Math.Min(attack.Length, attack.Length * MathF.Sin(half));
                case MonsterAttackShape.Thrust:
                case MonsterAttackShape.Beam:
                    return attack.Width / 2;
                case MonsterAttackShape.Nova:
                    // A ring's safe middle is as good as its outside.
                    return attack.Length > 0 ? (attack.Length - attack.Radius) / 2 : attack.Radius;
                case MonsterAttackShape.Leap:
                case MonsterAttackShape.Slam:
                case MonsterAttackShape.Lob:
                case MonsterAttackShape.Burrow:
                case MonsterAttackShape.Ambush:
                case MonsterAttackShape.Pull:
                case MonsterAttackShape.Push:
                case MonsterAttackShape.Mine:
                case MonsterAttackShape.Delayed:
                case MonsterAttackShape.Explode:
                    return attack.Radius;
                default:
                    return 0;
            }
        }

        // The least telegraph that lets a walking player out: (way out + their radius) / their pace + the time to notice.
        // A pull slows the way out by its drag.
        public static float LeastTelegraph(MonsterAttack attack)
        {
            float way = WayOut(attack);
            if (way <= 0) return 0;
            float pace = attack.Shape == MonsterAttackShape.Pull ? Math.Max(0.5f, WalkSpeed - attack.Speed) : WalkSpeed;
            return (way + World.PlayerRadius) / pace + NoticeSeconds;
        }

        internal static void Parse(string csv, List<MonsterDefinition> monsters, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(csv)) return;
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("monster", "id", "name", "shape", "telegraph", "cooldown", "weight");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return;
            }

            foreach (var row in table.Rows)
            {
                try
                {
                    var monster = monsters.FirstOrDefault(m => m.Id == row["monster"])
                        ?? throw new FormatException($"몬스터 '{row["monster"]}'가 {MonsterSheet.File}에 없습니다.");
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (monster.Attacks.Any(a => a.Id == id)) throw new FormatException($"'{monster.Id}'에 공격 '{id}'가 이미 있습니다.");
                    if (monster.Attacks.Count >= MaxAttacks) throw new FormatException($"한 몬스터의 공격은 {MaxAttacks}개까지입니다.");
                    if (row["name"].Length == 0) throw new FormatException("'name' 칸이 비어 있습니다.");

                    var attack = new MonsterAttack
                    {
                        Id = id,
                        Name = row["name"],
                        Index = monster.Attacks.Count + 1,
                        Shape = SheetValues.ParseEnum<MonsterAttackShape>(row["shape"], "shape"),
                        Target = row["target"].Length == 0 ? MonsterAttackTarget.Nearest : SheetValues.ParseEnum<MonsterAttackTarget>(row["target"], "target"),
                        Telegraph = SheetValues.ParseDecimal(row["telegraph"], "telegraph"),
                        Damage = Whole(row, "damage"),
                        Radius = Number(row, "radius"),
                        Length = Number(row, "length"),
                        Angle = Number(row, "angle"),
                        Width = Number(row, "width"),
                        Count = Whole(row, "count"),
                        Spread = Number(row, "spread"),
                        Speed = Number(row, "speed"),
                        Cooldown = Number(row, "cooldown"),
                        Weight = SheetValues.ParseNumber(row["weight"], "weight"),
                        RangeMin = Number(row, "range_min"),
                        RangeMax = row["range_max"].Length == 0 ? float.PositiveInfinity : SheetValues.ParseDecimal(row["range_max"], "range_max"),
                        Warning = row["warn"] == "" ? AttackWarning.Marked : row["warn"] == "strong" ? AttackWarning.Strong
                            : throw new FormatException($"warn은 비우거나 strong입니다('{row["warn"]}')."),
                        Duration = Number(row, "duration"),
                        ThenId = row["then"],
                    };
                    (attack.Pool, attack.Linger, attack.PoolRadius) = PoolOf(row["pool"]);
                    (attack.Effect, attack.EffectAmount) = Effect(row["effect"]);
                    When(row["when"], attack);
                    if (row["summon"].Length > 0)
                        attack.Summon = monsters.FirstOrDefault(m => m.Id == row["summon"]) ?? throw new FormatException($"몬스터 '{row["summon"]}'가 {MonsterSheet.File}에 없습니다.");
                    Validate(attack);
                    monster.AddAttack(attack);
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }

            foreach (var monster in monsters)
            {
                foreach (var attack in monster.Attacks)
                {
                    if (string.IsNullOrEmpty(attack.ThenId)) continue;
                    attack.Then = monster.Attacks.FirstOrDefault(a => a.Id == attack.ThenId);
                    if (attack.Then == null || attack.Then == attack || attack.Then.Then == attack)
                        errors.Add($"{File}: '{monster.Id}'의 공격 '{attack.Id}'의 then '{attack.ThenId}'는 그 몬스터의 다른 공격이어야 합니다.");
                }
            }
        }

        static void Validate(MonsterAttack attack)
        {
            void Need(bool ok, string what)
            {
                if (!ok) throw new FormatException($"{attack.Shape.ToString().ToLowerInvariant()} 공격은 {what} 칸을 채워야 합니다.");
            }

            if (attack.Telegraph <= 0) throw new FormatException("telegraph는 0보다 커야 합니다.");
            if (attack.Weight <= 0) throw new FormatException("weight는 0보다 커야 합니다.");
            if (attack.RangeMax < attack.RangeMin) throw new FormatException("range_max는 range_min 이상이어야 합니다.");
            bool harms = attack.Shape != MonsterAttackShape.Blink && attack.Shape != MonsterAttackShape.Summon;
            if (harms) Need(attack.Damage > 0, "damage");
            if (attack.Count == 0) attack.Count = 1;
            switch (attack.Shape)
            {
                case MonsterAttackShape.Strike:
                case MonsterAttackShape.Sweep:
                    Need(attack.Length > 0 && attack.Angle > 0, "length와 angle");
                    break;
                case MonsterAttackShape.Thrust:
                case MonsterAttackShape.Beam:
                    Need(attack.Length > 0 && attack.Width > 0, "length와 width");
                    break;
                case MonsterAttackShape.Charge:
                case MonsterAttackShape.Swoop:
                    Need(attack.Length > 0 && attack.Width > 0 && attack.Speed > 0, "length, width, speed");
                    break;
                case MonsterAttackShape.Spread:
                    Need(attack.Radius > 0 && attack.Speed > 0 && attack.Angle > 0, "radius, speed, angle");
                    break;
                case MonsterAttackShape.Volley:
                case MonsterAttackShape.Bounce:
                    Need(attack.Radius > 0 && attack.Speed > 0, "radius와 speed");
                    break;
                case MonsterAttackShape.Boomerang:
                    Need(attack.Radius > 0 && attack.Speed > 0 && attack.Length > 0, "radius, speed, length");
                    break;
                case MonsterAttackShape.Blink:
                    Need(attack.Length > 0, "length");
                    break;
                case MonsterAttackShape.Pull:
                    Need(attack.Radius > 0 && attack.Speed > 0, "radius와 speed");
                    if (attack.Speed >= WalkSpeed) throw new FormatException($"pull의 speed는 걷는 속도({WalkSpeed})보다 작아야 합니다.");
                    break;
                case MonsterAttackShape.Push:
                    Need(attack.Radius > 0 && attack.Length > 0 && attack.Speed > 0, "radius, length, speed");
                    if (attack.Effect == HitEffect.None) (attack.Effect, attack.EffectAmount) = (HitEffect.Push, 2);
                    break;
                case MonsterAttackShape.Mine:
                    Need(attack.Radius > 0 && attack.Duration > 0, "radius와 duration");
                    break;
                case MonsterAttackShape.Summon:
                    Need(attack.Summon != null, "summon");
                    break;
                case MonsterAttackShape.Mend:
                    Need(attack.Length > 0 && attack.Duration > 0, "length와 duration");
                    attack.Target = MonsterAttackTarget.Ally;
                    break;
                case MonsterAttackShape.Empower:
                    Need(attack.Radius > 0 && attack.Duration > 0, "radius와 duration");
                    break;
                default:
                    Need(attack.Radius > 0, "radius");
                    break;
            }
            if (attack.Pool != MonsterPool.None && !LeavesPools(attack.Shape))
                throw new FormatException($"pool은 바닥에 떨어지거나 길을 긋는 공격에만 씁니다({attack.Shape}).");
        }

        static bool LeavesPools(MonsterAttackShape shape) => shape switch
        {
            MonsterAttackShape.Leap or MonsterAttackShape.Slam or MonsterAttackShape.Lob or MonsterAttackShape.Nova or MonsterAttackShape.Burrow
                or MonsterAttackShape.Ambush or MonsterAttackShape.Push or MonsterAttackShape.Delayed or MonsterAttackShape.Explode
                or MonsterAttackShape.Thrust or MonsterAttackShape.Beam or MonsterAttackShape.Charge or MonsterAttackShape.Swoop => true,
            _ => false,
        };

        // "fire:3": what is left and for how many seconds.
        internal static (MonsterPool Kind, float Seconds) Pool(string cell)
        {
            var (kind, seconds, _) = PoolOf(cell);
            return (kind, seconds);
        }

        // "fire:4:0.9": with a third number, the pool is that wide whatever the blow's size.
        internal static (MonsterPool Kind, float Seconds, float Radius) PoolOf(string cell)
        {
            if (cell.Length == 0) return (MonsterPool.None, 0, 0);
            string[] parts = cell.Split(':');
            if (parts.Length != 2 && parts.Length != 3) throw new FormatException($"pool은 fire:3이나 fire:3:0.9처럼 적습니다('{cell}').");
            var kind = SheetValues.ParseEnum<MonsterPool>(parts[0].Trim(), "pool");
            float seconds = SheetValues.ParseDecimal(parts[1].Trim(), "pool");
            if (kind == MonsterPool.None || seconds <= 0 || seconds > 30) throw new FormatException("pool의 시간은 0보다 크고 30초 이하여야 합니다.");
            float radius = parts.Length == 3 ? SheetValues.ParseDecimal(parts[2].Trim(), "pool") : 0;
            if (radius < 0 || radius > 5) throw new FormatException("pool의 반지름은 5칸 이하여야 합니다.");
            return (kind, seconds, radius);
        }

        static (HitEffect Kind, float Amount) Effect(string cell)
        {
            if (cell.Length == 0) return (HitEffect.None, 0);
            string[] parts = cell.Split(':');
            if (parts.Length != 2) throw new FormatException($"effect는 root:1, push:2, venom:3, chill:2처럼 적습니다('{cell}').");
            var kind = SheetValues.ParseEnum<HitEffect>(parts[0].Trim(), "effect");
            float amount = SheetValues.ParseDecimal(parts[1].Trim(), "effect");
            if (kind == HitEffect.None || amount <= 0 || amount > 5) throw new FormatException("effect의 값은 0보다 크고 5 이하여야 합니다.");
            return (kind, amount);
        }

        // "hp<30|once|allies>=2"
        static void When(string cell, MonsterAttack attack)
        {
            foreach (string part in cell.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                if (part == "once") attack.Once = true;
                else if (part.StartsWith("hp<")) attack.HealthBelow = SheetValues.ParseNumber(part.Substring(3), "when hp<");
                else if (part.StartsWith("allies>=")) attack.AlliesAtLeast = SheetValues.ParseNumber(part.Substring(8), "when allies>=");
                else throw new FormatException($"when '{part}'는 hp<30, once, allies>=2 가운데 하나입니다.");
            }
        }

        // "split:2|guard:120|trail:fire:2"
        internal static List<MonsterTrait> Traits(string cell)
        {
            var traits = new List<MonsterTrait>();
            foreach (string part in cell.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                string[] pieces = part.Split(':');
                var kind = SheetValues.ParseEnum<MonsterTraitKind>(pieces[0], "traits");
                var trait = new MonsterTrait(kind, pieces.Skip(1).Select(p => p.Trim()).ToList());
                if (traits.Any(t => t.Kind == kind)) throw new FormatException($"traits에 '{pieces[0]}'가 두 번 있습니다.");
                switch (kind)
                {
                    case MonsterTraitKind.Split:
                        if (trait.Number(0, 0) < 1 || trait.Number(0, 0) > 4) throw new FormatException("split:n의 n은 1~4입니다.");
                        break;
                    case MonsterTraitKind.Guard:
                        if (trait.Number(0, 0) <= 0 || trait.Number(0, 0) > 360) throw new FormatException("guard:각의 각은 0보다 크고 360 이하입니다.");
                        if (trait.Number(1, 70) <= 0 || trait.Number(1, 70) >= 100) throw new FormatException("guard:각:퍼센트의 퍼센트는 1~99입니다.");
                        break;
                    case MonsterTraitKind.Trail:
                        if (pieces.Length != 3) throw new FormatException("trail은 trail:fire:3처럼 적습니다.");
                        Pool(pieces[1] + ":" + pieces[2]);
                        break;
                    case MonsterTraitKind.Aura:
                        if (pieces.Length != 4 || pieces[1].Trim() != "guard" || trait.Number(1, 0) <= 0 || trait.Number(2, 0) <= 0 || trait.Number(2, 0) >= 100)
                            throw new FormatException("aura는 aura:guard:1.5:20처럼 적습니다(종류는 guard, 거리, 덜 받는 퍼센트).");
                        break;
                    case MonsterTraitKind.Turn:
                        if (trait.Number(0, 0) < 30) throw new FormatException("turn:각의 각은 초당 30도 이상입니다.");
                        break;
                    case MonsterTraitKind.Deathburst:
                        if (pieces.Length > 3) SheetValues.ParseEnum<SkillTag>(pieces[3].Trim(), "deathburst");
                        break;
                }
                traits.Add(trait);
            }
            return traits;
        }

        static float Number(CsvRow row, string column) => row[column].Length == 0 ? 0 : SheetValues.ParseDecimal(row[column], column);

        static int Whole(CsvRow row, string column) => row[column].Length == 0 ? 0 : SheetValues.ParseNumber(row[column], column);
    }
}
