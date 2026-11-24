using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum MonsterBehavior : byte
    {
        // Walks up, winds up, strikes whoever is still in reach.
        Melee,
        // Keeps its distance and shoots where the player was when the wind-up began.
        Ranged,
        // Marks a straight path, then rushes along it.
        Charge,
    }

    public sealed class MonsterDefinition
    {
        // The place of the story (places.csv id) this monster keeps to (2026-11-10): it comes only round that place,
        // whatever the zone's level or the biome, and only such monsters come there. Null for the caves at large.
        public string Place { get; internal set; }

        // How it fights (monsters.csv role), what is always true of it (traits) and whether it comes in packs (pack),
        // and the attacks it picks among (monster_attacks.csv, 2026-11-21, Docs/WorldRework.md 2b). One with no attack
        // in that sheet fights the old way, by its Behavior.
        public MonsterRole Role { get; internal set; }
        public IReadOnlyList<MonsterTrait> Traits { get; internal set; } = Array.Empty<MonsterTrait>();
        // "always", "solo" or empty for either: whether it comes in packs (packs.csv) or alone (a wanderer, World.Packs.cs).
        public string Pack { get; internal set; } = "";
        // The share of its experience and of its chances of dropping things it gives when it came in a pack, in percent
        // (monsters.csv reward, 2026-11-22, World.RewardShare): many come at once, so each gives less
        // (Docs/WorldRework.md 숫자 다시 맞추기).
        public int RewardPercent { get; internal set; } = 100;
        // Its size class (monsters.csv size, MonsterSizes; from its radius when the column is blank) and the kind it is
        // a bigger or smaller make of (monsters.csv base: a big slime of the slime), whose numbers its own are held to.
        public MonsterSize Size { get; internal set; } = MonsterSize.M;
        public string Base { get; internal set; }
        // L or XL: it needs room to walk, comes only in rooms and keeps to its own (World.Packs.cs, Clearance).
        public bool Big => MonsterSizes.IsBig(Size);
        readonly List<MonsterAttack> _attacks = new List<MonsterAttack>();
        public IReadOnlyList<MonsterAttack> Attacks => _attacks;
        internal void AddAttack(MonsterAttack attack) => _attacks.Add(attack);
        public MonsterTrait Trait(MonsterTraitKind kind)
        {
            foreach (var trait in Traits) if (trait.Kind == kind) return trait;
            return null;
        }
        public bool Has(MonsterTraitKind kind) => Trait(kind) != null;

        internal MonsterDefinition(
            string id,
            string name,
            int level,
            int health,
            int damage,
            int armor,
            float speed,
            float radius,
            float aggro,
            float reach,
            float windup,
            float cooldown,
            int weight,
            float dropChance,
            float runeChance,
            float potionChance,
            string materialId,
            float materialChance,
            MonsterBehavior behavior = MonsterBehavior.Melee,
            float shotSpeed = 0,
            float keepDistance = 0,
            float chargeSpeed = 0,
            string color = "",
            Biome? biome = null,
            IReadOnlyDictionary<SkillTag, int> resists = null,
            SkillTag element = SkillTag.None)
        {
            Biome = biome;
            _resists = resists ?? new Dictionary<SkillTag, int>();
            Element = element;
            Id = id;
            Name = name;
            Level = level;
            Health = health;
            Damage = damage;
            Armor = armor;
            Speed = speed;
            Radius = radius;
            Aggro = aggro;
            Reach = reach;
            Windup = windup;
            Cooldown = cooldown;
            Weight = weight;
            DropChance = dropChance;
            RuneChance = runeChance;
            PotionChance = potionChance;
            MaterialId = materialId;
            MaterialChance = materialChance;
            Behavior = behavior;
            ShotSpeed = shotSpeed;
            KeepDistance = keepDistance;
            ChargeSpeed = chargeSpeed;
            Color = color;
        }

        public string Id { get; }
        public string Name { get; }
        // Monsters spawn where the zone level has reached their level.
        public int Level { get; }
        // Only spawns in this biome, or anywhere when null.
        public Biome? Biome { get; }
        // The element its attacks carry (fire burns, cold chills), or None.
        public SkillTag Element { get; }
        readonly IReadOnlyDictionary<SkillTag, int> _resists;

        // How much less damage it takes from an element, in percent; negative for a weakness.
        public int Resist(SkillTag element) => _resists.TryGetValue(element, out int percent) ? percent : 0;
        public int Health { get; }
        public int Damage { get; }
        public int Armor { get; }
        // Tiles per second.
        public float Speed { get; }
        public float Radius { get; }
        // Notices players this close, in tiles.
        public float Aggro { get; }
        // Attacks when the gap between it and the player is this small.
        public float Reach { get; }
        // Seconds of visible wind-up before the hit lands; the player can step away during it.
        public float Windup { get; }
        // Seconds after an attack before it chases again.
        public float Cooldown { get; }
        public int Weight { get; }
        // Percent chance to drop an item, and separately a rune and a consumable, when killed.
        public float DropChance { get; }
        public float RuneChance { get; }
        public float PotionChance { get; }
        // A material from materials.csv (checked when the world drops it), and the percent chance to drop it.
        public string MaterialId { get; }
        public float MaterialChance { get; }
        public MonsterBehavior Behavior { get; }
        // Ranged: tiles per second its shots fly, and how close it lets a player come before backing off.
        public float ShotSpeed { get; }
        public float KeepDistance { get; }
        // Charge: tiles per second it rushes; the rush covers ChargeLength.
        public float ChargeSpeed { get; }
        public float ChargeLength => Reach + 2;
        // "#RRGGBB" for the screen, or empty for the default.
        public string Color { get; }
    }

    public sealed class MonsterData
    {
        internal MonsterData(IReadOnlyList<MonsterDefinition> monsters, IReadOnlyList<string> errors)
        {
            Monsters = monsters;
            Errors = errors;
        }

        public IReadOnlyList<MonsterDefinition> Monsters { get; }
        public IReadOnlyList<string> Errors { get; }
        // The packs the rooms of a world hold (packs.csv, PackSheet.Parse puts them here); null without the sheet, and
        // then monsters come one by one (Docs/WorldRework.md 무리).
        public PackData Packs { get; internal set; }
        public bool Success => Errors.Count == 0;

        public MonsterDefinition Find(string id) =>
            Monsters.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException($"Unknown monster '{id}'.", nameof(id));
    }

    // Reads Assets/_Project/Data/Monsters/monsters.csv. Columns are explained in Docs/Combat.md.
    public static class MonsterSheet
    {
        public const string File = "monsters.csv";

        // A monster's radius is held to its size class's band (MonsterSizes, 2026-11-24): XS to M fit through a one-tile
        // gap, L and XL do not and keep to the rooms. A row with a base is held to it within these shares of what its
        // class makes of the base's numbers.
        const float BaseHealthSlack = 2f;
        const float BaseSpeedSlack = 1.25f;

        // attacksCsv is monster_attacks.csv (MonsterAttackSheet); without it every monster fights by its behavior.
        public static MonsterData Parse(string csv, string attacksCsv = null)
        {
            var errors = new List<string>();
            var monsters = new List<MonsterDefinition>();
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns(
                "id", "name", "level", "health", "damage", "armor", "speed", "radius", "aggro", "reach", "windup", "cooldown", "weight");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new MonsterData(monsters, errors);
            }

            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (monsters.Any(m => m.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    string name = row["name"];
                    if (name.Length == 0) throw new FormatException("'name' 칸이 비어 있습니다.");

                    float radius = SheetValues.ParseDecimal(row["radius"], "radius");
                    var size = Size(row["size"], radius);
                    float speed = SheetValues.ParseDecimal(row["speed"], "speed");
                    if (speed <= 0) throw new FormatException("speed는 0보다 커야 합니다.");
                    var behavior = row["behavior"].Length == 0 ? MonsterBehavior.Melee : SheetValues.ParseEnum<MonsterBehavior>(row["behavior"], "behavior");
                    float shotSpeed = Number(row, "shot_speed");
                    float chargeSpeed = Number(row, "charge_speed");
                    if (behavior == MonsterBehavior.Ranged && shotSpeed <= 0) throw new FormatException("ranged 몬스터는 shot_speed 칸을 채워야 합니다.");
                    if (behavior == MonsterBehavior.Charge && chargeSpeed <= 0) throw new FormatException("charge 몬스터는 charge_speed 칸을 채워야 합니다.");
                    string color = row["color"];
                    if (color.Length > 0 && !SheetValues.IsColor(color)) throw new FormatException($"color '{color}'는 #RRGGBB로 적습니다.");
                    Biome? biome = row["biome"].Length == 0 ? (Biome?)null : SheetValues.ParseEnum<Biome>(row["biome"], "biome");
                    var resists = Resists(row["resist"]);
                    var element = row["element"].Length == 0 ? SkillTag.None : Element(SheetValues.ParseEnum<SkillTag>(row["element"], "element"));

                    monsters.Add(new MonsterDefinition(
                        id,
                        name,
                        SheetValues.ParseNumber(row["level"], "level"),
                        Positive(SheetValues.ParseNumber(row["health"], "health"), "health"),
                        SheetValues.ParseNumber(row["damage"], "damage"),
                        SheetValues.ParseNumber(row["armor"], "armor"),
                        speed,
                        radius,
                        SheetValues.ParseDecimal(row["aggro"], "aggro"),
                        SheetValues.ParseDecimal(row["reach"], "reach"),
                        SheetValues.ParseDecimal(row["windup"], "windup"),
                        SheetValues.ParseDecimal(row["cooldown"], "cooldown"),
                        SheetValues.ParseNumber(row["weight"], "weight"),
                        Percent(row, "drop_chance"),
                        Percent(row, "rune_chance"),
                        Percent(row, "potion_chance"),
                        row["material"],
                        Percent(row, "material_chance"),
                        behavior,
                        shotSpeed,
                        Number(row, "keep_distance"),
                        chargeSpeed,
                        color,
                        biome,
                        resists,
                        element)
                    {
                        Place = row["place"].Length == 0 ? null : row["place"],
                        Role = row["role"].Length == 0 ? MonsterRole.None : SheetValues.ParseEnum<MonsterRole>(row["role"], "role"),
                        Traits = MonsterAttackSheet.Traits(row["traits"]),
                        Pack = Pack(row["pack"]),
                        RewardPercent = row["reward"].Length == 0 ? 100 : Positive(SheetValues.ParseNumber(row["reward"], "reward"), "reward"),
                        Size = size,
                        Base = row["base"].Length == 0 ? null : row["base"],
                    });
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            MonsterAttackSheet.Parse(attacksCsv, monsters, errors);
            foreach (var monster in monsters) CheckSize(monster, monsters, errors);
            return new MonsterData(monsters, errors);
        }

        // "xs", "s", "m", "l", "xl", or blank for the class the radius falls in. The radius has to be in the class's band.
        static MonsterSize Size(string cell, float radius)
        {
            if (radius <= 0) throw new FormatException("radius는 0보다 커야 합니다.");
            MonsterSize size;
            if (cell.Length == 0)
            {
                size = MonsterSizes.FromRadius(radius)
                    ?? throw new FormatException($"radius {radius}는 어느 크기 등급에도 들지 않습니다(xs 0.20~0.25, s 0.28~0.35, m 0.38~0.45, l 0.70~0.90, xl 1.20~1.40).");
            }
            else
            {
                size = cell.ToLowerInvariant() switch
                {
                    "xs" => MonsterSize.XS,
                    "s" => MonsterSize.S,
                    "m" => MonsterSize.M,
                    "l" => MonsterSize.L,
                    "xl" => MonsterSize.XL,
                    _ => throw new FormatException($"size는 xs, s, m, l, xl 중 하나입니다('{cell}')."),
                };
            }
            var band = MonsterSizes.Of(size);
            if (!MonsterSizes.Fits(size, radius))
                throw new FormatException($"{MonsterSizes.Name(size)} 등급의 radius는 {band.MinRadius}~{band.MaxRadius}입니다({radius}).");
            return size;
        }

        // The class's band for what the sheet says (Docs/Combat.md 크기 등급): a big body walks no faster and winds up
        // no quicker than its class allows, in its own wind-up and every attack's; with a base, its health and pace are
        // what its class makes of the base's within the slack. A split into another kind names a kind there is, smaller.
        static void CheckSize(MonsterDefinition monster, List<MonsterDefinition> monsters, List<string> errors)
        {
            var band = MonsterSizes.Of(monster.Size);
            string at = $"{File} {monster.Id}";
            if (monster.Speed > band.MaxSpeed + 1e-4f)
                errors.Add($"{at}: {MonsterSizes.Name(monster.Size)} 등급은 speed {band.MaxSpeed} 이하입니다({monster.Speed}).");
            if (monster.Windup < band.LeastWindup - 1e-4f)
                errors.Add($"{at}: {MonsterSizes.Name(monster.Size)} 등급은 windup {band.LeastWindup}초 이상입니다({monster.Windup}).");
            foreach (var attack in monster.Attacks)
            {
                if (attack.Telegraph < band.LeastWindup - 1e-4f)
                    errors.Add($"{at}.{attack.Id}: {MonsterSizes.Name(monster.Size)} 등급의 공격 예고는 {band.LeastWindup}초 이상입니다({attack.Telegraph}).");
            }
            if (monster.Base != null)
            {
                var source = monsters.FirstOrDefault(m => m.Id == monster.Base);
                if (source == null) errors.Add($"{at}: base '{monster.Base}'라는 몬스터가 없습니다.");
                else if (source == monster || source.Size == monster.Size) errors.Add($"{at}: base는 크기 등급이 다른 종입니다('{monster.Base}').");
                else
                {
                    var from = MonsterSizes.Of(source.Size);
                    float health = source.Health * band.Health / from.Health;
                    float speed = source.Speed * band.Speed / from.Speed;
                    if (monster.Health < health / BaseHealthSlack || monster.Health > health * BaseHealthSlack)
                        errors.Add($"{at}: base {source.Id}의 {MonsterSizes.Name(monster.Size)} 등급이면 health는 {health / BaseHealthSlack:0}~{health * BaseHealthSlack:0}입니다({monster.Health}).");
                    if (monster.Speed < speed / BaseSpeedSlack - 1e-4f || monster.Speed > speed * BaseSpeedSlack + 1e-4f)
                        errors.Add($"{at}: base {source.Id}의 {MonsterSizes.Name(monster.Size)} 등급이면 speed는 {speed / BaseSpeedSlack:0.##}~{speed * BaseSpeedSlack:0.##}입니다({monster.Speed}).");
                }
            }
            var split = monster.Trait(MonsterTraitKind.Split);
            string into = split != null && split.Arguments.Count > 1 ? split.Arguments[1] : null;
            if (into != null)
            {
                var kind = monsters.FirstOrDefault(m => m.Id == into);
                if (kind == null) errors.Add($"{at}: split의 '{into}'라는 몬스터가 없습니다.");
                else if (kind.Size >= monster.Size) errors.Add($"{at}: split으로 나오는 종은 더 작은 등급입니다('{into}').");
            }
            // A shed (a slime mother's slimes, 2026-11-24): a kind there is, smaller than it.
            var shed = monster.Trait(MonsterTraitKind.Shed);
            if (shed != null)
            {
                var kind = monsters.FirstOrDefault(m => m.Id == shed.Arguments[0]);
                if (kind == null) errors.Add($"{at}: shed의 '{shed.Arguments[0]}'라는 몬스터가 없습니다.");
                else if (kind.Size >= monster.Size) errors.Add($"{at}: shed로 떨어지는 종은 더 작은 등급입니다('{shed.Arguments[0]}').");
            }
        }

        static string Pack(string cell) =>
            cell == "" || cell == "always" || cell == "solo" ? cell : throw new FormatException($"pack은 비우거나 always, solo입니다('{cell}').");

        static readonly SkillTag[] ElementTags = { SkillTag.Fire, SkillTag.Cold, SkillTag.Lightning };

        static SkillTag Element(SkillTag tag) =>
            Array.IndexOf(ElementTags, tag) >= 0 ? tag : throw new FormatException($"element '{tag}'는 fire, cold, lightning 중 하나입니다.");

        // "fire:75|cold:-25": percent less damage from each element, negative for more.
        static Dictionary<SkillTag, int> Resists(string cell)
        {
            var resists = new Dictionary<SkillTag, int>();
            foreach (string part in cell.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                string[] pieces = part.Split(':');
                if (pieces.Length != 2) throw new FormatException($"resist '{part}'는 fire:50처럼 적습니다.");
                var element = Element(SheetValues.ParseEnum<SkillTag>(pieces[0].Trim(), "resist의 원소"));
                string number = pieces[1].Trim();
                bool negative = number.StartsWith("-");
                int percent = SheetValues.ParseNumber(negative ? number.Substring(1) : number, "resist");
                if (percent > 100) throw new FormatException("resist는 100 이하입니다.");
                resists[element] = negative ? -percent : percent;
            }
            return resists;
        }

        // Drop columns may be left out or blank, which means no drops.
        static float Percent(CsvRow row, string column)
        {
            if (row[column].Length == 0) return 0;
            float value = SheetValues.ParseDecimal(row[column], column);
            return value <= 100 ? value : throw new FormatException($"{column}는 0~100 사이의 퍼센트입니다.");
        }

        static float Number(CsvRow row, string column) => row[column].Length == 0 ? 0 : SheetValues.ParseDecimal(row[column], column);

        static int Positive(int value, string label) =>
            value > 0 ? value : throw new FormatException($"{label}는 0보다 커야 합니다.");
    }
}
