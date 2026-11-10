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
        public bool Success => Errors.Count == 0;

        public MonsterDefinition Find(string id) =>
            Monsters.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException($"Unknown monster '{id}'.", nameof(id));
    }

    // Reads Assets/_Project/Data/Monsters/monsters.csv. Columns are explained in Docs/Combat.md.
    public static class MonsterSheet
    {
        public const string File = "monsters.csv";

        // A monster has to fit through a one-tile gap.
        const float MaxRadius = 0.45f;

        public static MonsterData Parse(string csv)
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
                    if (radius <= 0 || radius > MaxRadius) throw new FormatException($"radius는 0보다 크고 {MaxRadius} 이하여야 합니다.");
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
                        element) { Place = row["place"].Length == 0 ? null : row["place"] });
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return new MonsterData(monsters, errors);
        }

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
