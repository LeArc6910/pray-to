using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What sort of weapon a base is (weapon_kinds.csv, Docs/Weapons.md): its branch, how its basic attack goes and how
    // often. The damage itself is the base's own (item_bases.csv implicit), written so that each kind's damage a second
    // comes out near the others' of the same step; the kinds differ in reach, speed and shape instead.
    public sealed class WeaponKind
    {
        public const string File = "weapon_kinds.csv";

        // Without the sheet (and for a base that names no kind) a weapon is one of these, as before there were kinds.
        public static readonly WeaponKind Sword = new WeaponKind("sword", "검", SkillTag.Melee, 1, BasicAttack.Swing);
        public static readonly WeaponKind Bow = new WeaponKind("bow", "활", SkillTag.Ranged, 1, BasicAttack.Arrow);
        public static readonly WeaponKind Staff = new WeaponKind("staff", "지팡이", SkillTag.Magic, 1, BasicAttack.Bolt);
        // A pickaxe swings like a sword but counts for no class (Docs/Weapons.md, 스킬이 힘을 받는 규칙).
        public static readonly WeaponKind Pickaxe = new WeaponKind("pickaxe", "곡괭이", SkillTag.None, 1, BasicAttack.Swing);
        public static readonly WeaponKind Fists = new WeaponKind("fists", "맨손", SkillTag.None, 1, BasicAttack.Swing);

        internal WeaponKind(string id, string name, SkillTag branch, float speed, BasicAttack attack, string description = "", WeaponArt art = null)
        {
            Art = art;
            Id = id;
            Name = name;
            Branch = branch;
            Speed = speed;
            Attack = attack;
            Description = description;
        }

        public string Id { get; }
        public string Name { get; }
        // Melee, Ranged or Magic: which classes' skills draw on it (ClassDefinition.Weapon); None for a pickaxe or fists.
        public SkillTag Branch { get; }
        // Times the usual basic attacks a second (PlayerStats.BaseAttacksPerSecond).
        public float Speed { get; }
        public BasicAttack Attack { get; }
        // What the tooltip says of it.
        public string Description { get; }
        // Its own art (Docs/Weapons.md 묶음 C), or null for a pickaxe, fists and a kind with none.
        public WeaponArt Art { get; }

        // The kind a base is without a kind of its own: by its tool and tags.
        public static WeaponKind DefaultFor(ItemBase itemBase) =>
            itemBase == null ? Fists
            : itemBase.IsPickaxe ? Pickaxe
            : (itemBase.Tags & SkillTag.Melee) != 0 ? Sword
            : (itemBase.Tags & SkillTag.Ranged) != 0 ? Bow
            : (itemBase.Tags & SkillTag.Magic) != 0 ? Staff
            : Sword;

        public static IReadOnlyList<WeaponKind> Defaults { get; } = new[] { Sword, Bow, Staff, Pickaxe };

        // Columns in Docs/Weapons.md. A bad row is skipped and reported in errors.
        internal static List<WeaponKind> Parse(string csv, List<string> errors)
        {
            var kinds = new List<WeaponKind>();
            if (string.IsNullOrWhiteSpace(csv)) return kinds;
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "name", "branch", "attack", "speed");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return kinds;
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    string id = row["id"];
                    if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
                    if (kinds.Any(k => k.Id == id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
                    string name = row["name"];
                    if (name.Length == 0) throw new FormatException("'name' 칸이 비어 있습니다.");
                    var branch = row["branch"].Length == 0 ? SkillTag.None : SheetValues.ParseEnum<SkillTag>(row["branch"], "branch");
                    if (branch != SkillTag.None && branch != SkillTag.Melee && branch != SkillTag.Ranged && branch != SkillTag.Magic)
                        throw new FormatException("branch는 melee, ranged, magic 가운데 하나이거나 비웁니다.");
                    var shape = SheetValues.ParseEnum<BasicAttackKind>(row["attack"], "attack");
                    float speed = Decimal(row, "speed", 1);
                    if (speed < 0.2f || speed > 3) throw new FormatException("speed는 0.2~3입니다.");
                    var fallback = shape == BasicAttackKind.Arrow ? BasicAttack.Arrow : shape == BasicAttackKind.Bolt ? BasicAttack.Bolt : BasicAttack.Swing;
                    var attack = new BasicAttack(shape, fallback.Name,
                        speed: Decimal(row, "shot_speed", fallback.Speed),
                        range: Decimal(row, "range", fallback.Range),
                        radius: Decimal(row, "radius", fallback.Radius),
                        reach: Decimal(row, "reach", World.MeleeReach),
                        arc: Decimal(row, "arc", World.MeleeArcDegrees),
                        push: Decimal(row, "push", 1),
                        pierce: row["pierce"].Length > 0 ? SheetValues.ParseNumber(row["pierce"], "pierce") : 0,
                        returns: row["returns"].Length > 0 && SheetValues.ParseBool(row["returns"], "returns"),
                        burst: Decimal(row, "burst", 0),
                        stun: Decimal(row, "stun", 0));
                    if (attack.Shoots && (attack.Speed <= 0 || attack.Range <= 0)) throw new FormatException("쏘는 무기는 shot_speed와 range가 0보다 커야 합니다.");
                    if (!attack.Shoots && (attack.Reach <= 0 || attack.Arc <= 0 || attack.Arc > 360)) throw new FormatException("휘두르는 무기는 reach가 0보다, arc가 0~360이어야 합니다.");
                    kinds.Add(new WeaponKind(id, name, branch, speed, attack, row["description"], WeaponArt.Read(row)));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return kinds;

            static float Decimal(CsvRow row, string column, float fallback) =>
                row[column].Length > 0 ? SheetValues.ParseDecimal(row[column], column) : fallback;
        }
    }

    // Each weapon kind's own art (Docs/Weapons.md 묶음 C): used with the weapon in hand, apart from the class skills, no
    // mana, only its cooldown, which each kind keeps for itself. It hits with the weapon's own numbers and tags
    // (PlayerStats of the weapon in hand), so a class's passives for that branch count.
    public enum WeaponArtKind : byte
    {
        None,
        // Sword: a guard for Time; a blow taken in it is turned aside and answered round the player.
        Parry,
        // Dagger: the nearest monster ahead, a sure crit.
        Vital,
        // Greatsword: held to gather up to Time, let go to cleave from Low to Power.
        Charge,
        // Spear: a rush Reach tiles that strikes all it passes.
        Dash,
        // Hammer: lifted for Time, then all round within Radius, dazed Stun seconds.
        Slam,
        // Bow: a jump Reach tiles back, then Count arrows Arc degrees apart.
        Backstep,
        // Crossbow: aimed for Time, then everything on a line Reach long, Radius wide, to the first wall.
        Pierce,
        // Boomerang: thrown round the player for Time, striking within Radius Count times, Power in all.
        Whirl,
        // Staff: a cone Reach long and Arc wide that pushes hard.
        Wave,
        // Wand: Count bolts over Time, fanned Arc degrees.
        Barrage,
        // Orb: a well Reach ahead that draws monsters in for Time, then bursts within Radius.
        Gravity,
    }

    public sealed class WeaponArt
    {
        public WeaponArt(WeaponArtKind kind, string name, float power, float cooldown, float low = 0, float reach = 0, float radius = 0, float arc = 0,
            float time = 0, int count = 0, float stun = 0, string description = "")
        {
            Kind = kind;
            Name = name;
            Power = power;
            Cooldown = cooldown;
            Low = low;
            Reach = reach;
            Radius = radius;
            Arc = arc;
            Time = time;
            Count = count;
            Stun = stun;
            Description = description;
        }

        public WeaponArtKind Kind { get; }
        public string Name { get; }
        // Percent of the weapon's hit (a boomerang's in all, split over Count).
        public float Power { get; }
        public float Cooldown { get; }
        public float Low { get; }
        public float Reach { get; }
        public float Radius { get; }
        public float Arc { get; }
        public float Time { get; }
        public int Count { get; }
        public float Stun { get; }
        public string Description { get; }

        // Held down: gathers while held (a greatsword's).
        public bool Holds => Kind == WeaponArtKind.Charge;

        // The row's art columns, or null when art is empty or missing.
        internal static WeaponArt Read(CsvRow row)
        {
            if (row["art"].Length == 0) return null;
            var kind = SheetValues.ParseEnum<WeaponArtKind>(row["art"], "art");
            if (kind == WeaponArtKind.None) return null;
            string name = row["art_name"];
            if (name.Length == 0) throw new FormatException("'art_name' 칸이 비어 있습니다.");
            var art = new WeaponArt(kind, name, Number("art_power"), Number("art_cooldown"), Number("art_low"), Number("art_reach"), Number("art_radius"),
                Number("art_arc"), Number("art_time"), (int)Number("art_count"), Number("art_stun"), row["art_description"]);
            if (art.Power <= 0 || art.Cooldown <= 0) throw new FormatException("art_power와 art_cooldown은 0보다 커야 합니다.");
            bool needsReach = kind == WeaponArtKind.Parry || kind == WeaponArtKind.Vital || kind == WeaponArtKind.Charge || kind == WeaponArtKind.Dash
                || kind == WeaponArtKind.Backstep || kind == WeaponArtKind.Pierce || kind == WeaponArtKind.Wave || kind == WeaponArtKind.Gravity;
            if (needsReach && art.Reach <= 0) throw new FormatException($"{row["art"]}에는 art_reach가 있어야 합니다.");
            bool needsRadius = kind == WeaponArtKind.Dash || kind == WeaponArtKind.Slam || kind == WeaponArtKind.Pierce || kind == WeaponArtKind.Whirl || kind == WeaponArtKind.Gravity;
            if (needsRadius && art.Radius <= 0) throw new FormatException($"{row["art"]}에는 art_radius가 있어야 합니다.");
            bool needsTime = kind == WeaponArtKind.Parry || kind == WeaponArtKind.Charge || kind == WeaponArtKind.Slam || kind == WeaponArtKind.Pierce
                || kind == WeaponArtKind.Whirl || kind == WeaponArtKind.Barrage || kind == WeaponArtKind.Gravity;
            if (needsTime && art.Time <= 0) throw new FormatException($"{row["art"]}에는 art_time이 있어야 합니다.");
            bool needsCount = kind == WeaponArtKind.Backstep || kind == WeaponArtKind.Whirl || kind == WeaponArtKind.Barrage;
            if (needsCount && art.Count <= 0) throw new FormatException($"{row["art"]}에는 art_count가 있어야 합니다.");
            if ((kind == WeaponArtKind.Parry || kind == WeaponArtKind.Vital || kind == WeaponArtKind.Charge || kind == WeaponArtKind.Wave) && (art.Arc <= 0 || art.Arc > 360))
                throw new FormatException("art_arc는 0~360입니다.");
            return art;

            float Number(string column) => row[column].Length > 0 ? SheetValues.ParseDecimal(row[column], column) : 0;
        }
    }
}
