using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class MonsterDefinition
    {
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
            int weight)
        {
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
        }

        public string Id { get; }
        public string Name { get; }
        // Monsters spawn where the zone level has reached their level.
        public int Level { get; }
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
                        SheetValues.ParseNumber(row["weight"], "weight")));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return new MonsterData(monsters, errors);
        }

        static int Positive(int value, string label) =>
            value > 0 ? value : throw new FormatException($"{label}는 0보다 커야 합니다.");
    }
}
