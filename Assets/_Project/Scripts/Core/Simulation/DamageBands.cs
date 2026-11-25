using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What one hit takes off a player (Docs/Difficulty.md, 2026-11-25, the owner's request: "적어도 한방에 체력이 20퍼센트
    // 이상 … 그 레벨대의 장비를 충분히 갖추면 5퍼센트 정도"). Every monster and boss belongs to a band, one for each tier of
    // gear (copper, iron, mithril, obsidian, void), and armor is weighed against the band of what struck: a full common
    // set of the band's helmet, chest and boots takes a hit down to 1 / (1 + GearGuard), the set of the band below to
    // about twice that, no armor not at all. Armor counts as the square of its share of the band's, so a band's gear is
    // what keeps its monsters off and the gear of the band below gives way fast (the gate between bands).
    public static class DamageBands
    {
        public const int Bands = 5;
        public static readonly string[] TierNames = { "copper", "iron", "mithril", "obsidian", "void" };
        public static readonly string[] BandNames = { "구리", "철", "미스릴", "흑요석", "공허" };

        // The armor of each tier's helmet, chest and boots, common, at the middle of their rolls rounded down (item_bases.csv): the
        // armor a band's monsters are weighed against. DifficultyTests checks these against the sheet.
        static readonly float[] BandArmor = { 13, 29, 59, 113, 207 };

        // Full band armor divides a hit by 1 + GearGuard (3.5): 20% bare is about 5% in the band's gear.
        public const float GearGuard = 2.5f;

        // The level a band's monsters are weighed at (Docs/Difficulty.md): where the band's fields and dungeons are met.
        // A boss is weighed at its own level + 1.
        static readonly int[] BandLevels = { 5, 13, 23, 32, 42 };

        // The maximum health of a character at the band's level in the band's full common set (sword, helmet, chest,
        // boots, ring, amulet at the middle of their rolls), no class: what a band's hits are made against. The rift
        // scales its monsters by it (World.Rift.cs). DifficultyTests checks these against the sheet.
        static readonly float[] BandHealth = { 125, 193, 278, 382, 525 };

        public static int Clamp(int band) => Math.Clamp(band, 1, Bands);

        // The band of a level: its tens, copper below 10 (a level 23 boss is a mithril boss).
        public static int BandOfLevel(int level) => Clamp(level / 10 + 1);

        public static float ArmorOf(int band) => BandArmor[Clamp(band) - 1];

        public static int LevelOf(int band) => BandLevels[Clamp(band) - 1];

        public static float HealthOf(int band) => BandHealth[Clamp(band) - 1];

        // The band a monster of a biome belongs to when the sheet leaves it blank (monsters.csv band).
        public static int BandOf(Biome? biome) => biome switch
        {
            null or Biome.Cave => 1,
            Biome.Mushroom or Biome.Ice or Biome.Sand or Biome.Crystal => 2,
            Biome.Lake or Biome.Lava => 3,
            _ => 4,
        };

        // The share of a hit of the band that gets through this much armor.
        public static float Share(float armor, int band)
        {
            float share = Math.Max(0, armor) / ArmorOf(band);
            return 1 / (1 + GearGuard * share * share);
        }

        // A hit of the band through the armor: rounded, at least 1.
        public static int Taken(float damage, float armor, int band) => Math.Max(1, (int)MathF.Round(damage * Share(armor, band)));

        // The same, unrounded: what a burn or a pool's bite works from.
        public static float Through(float damage, float armor, int band) => damage * Share(armor, band);

        // ---- What a hit is for (the targets of Docs/Difficulty.md) ----

        // The share of the health of a character of the band in the band's gear a hit of the kind should take: Aim, and
        // the range it may fall in; Bare: the least a character with no gear at all should lose to it (0: no floor).
        public readonly struct Target
        {
            public Target(float aim, float low, float high, float bare = 0)
            {
                Aim = aim;
                Low = low;
                High = high;
                Bare = bare;
            }

            public float Aim { get; }
            public float Low { get; }
            public float High { get; }
            public float Bare { get; }
        }

        public static Target TargetOf(HitKind kind) => kind switch
        {
            HitKind.Normal => new Target(0.06f, 0.05f, 0.075f, 0.20f),
            HitKind.Elite => new Target(0.10f, 0.085f, 0.125f, 0.35f),
            HitKind.Large => new Target(0.10f, 0.085f, 0.125f, 0.35f),
            HitKind.Huge => new Target(0.16f, 0.14f, 0.20f, 0.50f),
            HitKind.Quick => new Target(0.40f, 0.35f, 0.45f),
            HitKind.Slow => new Target(0.55f, 0.45f, 0.65f),
            HitKind.Heavy => new Target(0.78f, 0.70f, 0.90f),
            HitKind.Ultimate => new Target(0.92f, 0.89f, 0.95f),
            HitKind.Barrage => new Target(0.12f, 0.09f, 0.16f),
            HitKind.Combo => new Target(0.25f, 0.20f, 0.30f),
            HitKind.FollowUp => new Target(0.18f, 0.14f, 0.24f),
            HitKind.Share => new Target(0.70f, 0.60f, 0.80f),
            HitKind.Roar => new Target(0.35f, 0.25f, 0.45f),
            _ => new Target(0, 0, 1),
        };

        // The gate: the gear of the band below takes this many times the band's gear's share of the health, at least.
        public const float GateLow = 2;
        public const float GateHigh = 3.2f;
        // A single attack of a monster against what its kind's usual hit is meant to be: its big blows may hurt more,
        // up to this many times the aim.
        public const float BigBlowCap = 2.5f;

        // A boss's attack by what it is (Docs/Difficulty.md 보스): a strongly warned one is its finishing blow; a volley's
        // shot, a dash of a run of dashes, an attack that only follows another and a shared circle have their own; the rest
        // by how long they warn.
        public static HitKind KindOf(BossAttack attack)
        {
            if (attack.Shape == AttackShape.Summon || attack.Damage <= 0) return HitKind.None;
            if (attack.Warning == AttackWarning.Strong) return HitKind.Ultimate;
            if (attack.Shape == AttackShape.Burst || attack.Shape == AttackShape.Spiral) return HitKind.Barrage;
            if (attack.Shape == AttackShape.Share) return HitKind.Share;
            if (attack.Shape == AttackShape.Charge && attack.Count >= 2) return HitKind.Combo;
            if (attack.Weight == 0) return HitKind.FollowUp;
            return attack.Telegraph >= HeavyTelegraph ? HitKind.Heavy : attack.Telegraph >= SlowTelegraph ? HitKind.Slow : HitKind.Quick;
        }

        // A boss's attack warned this long or longer is a slow one, and this long a heavy one.
        public const float SlowTelegraph = 1.2f;
        public const float HeavyTelegraph = 1.4f;

        // A monster's kind of hit by its body: XL a mid-boss, L a big one, the rest ordinary.
        public static HitKind KindOf(MonsterDefinition monster) =>
            monster.Size == MonsterSize.XL ? HitKind.Huge : monster.Size == MonsterSize.L ? HitKind.Large : HitKind.Normal;

        // Whether a monster's attack hurts (not a blink, a call, a mend or a rally).
        public static bool Hurts(MonsterAttack attack) =>
            attack.Damage > 0 && attack.Shape != MonsterAttackShape.Mend && attack.Shape != MonsterAttackShape.Empower
            && attack.Shape != MonsterAttackShape.Summon && attack.Shape != MonsterAttackShape.Blink;

        // A monster's usual hit, in percent of its damage: its hurting attacks by how often it picks them (an old-way
        // fighter's one blow is 100).
        public static float UsualPercent(MonsterDefinition monster)
        {
            var hurting = monster.Attacks.Where(Hurts).ToList();
            if (hurting.Count == 0) return 100;
            float weights = hurting.Sum(a => Math.Max(1, a.Weight));
            return hurting.Sum(a => a.Damage * (float)Math.Max(1, a.Weight)) / weights;
        }
    }

    public enum HitKind : byte
    {
        None,
        // A field monster's usual hit (XS~M).
        Normal,
        // A field elite's (a leader's or a champion's) usual hit.
        Elite,
        // An L body's.
        Large,
        // An XL body's: a mid-boss, a dungeon's master.
        Huge,
        // A boss's attack warned less than DamageBands.SlowTelegraph.
        Quick,
        // A boss's attack warned DamageBands.SlowTelegraph up to DamageBands.HeavyTelegraph.
        Slow,
        // A boss's attack warned DamageBands.HeavyTelegraph or longer: the big slow blows.
        Heavy,
        // A boss's strongly warned blow (boss_attacks.csv warn strong): its finishing blow, nearly all of the health.
        Ultimate,
        // One shot of a boss's volley or spiral.
        Barrage,
        // One dash of a boss's run of dashes (a charge of count 2 and more): the later ones warn half as long.
        Combo,
        // An attack that only follows another (weight 0): the second blow a roll cannot also pass.
        FollowUp,
        // A shared circle, taken alone.
        Share,
        // The blast of a boss's roar at a new phase.
        Roar,
    }

    // The table of what every hit takes (DevTools/CoreSandbox --hits, DifficultyTests): for every monster's usual hit
    // and attacks, its elites', every boss attack and roar, and a rift's monsters, the share of the health of a character
    // of the band (DamageBands.LevelOf, a boss's level + 1) with no gear, with the band below's full set and with the
    // band's own.
    public sealed class HitRow
    {
        public int Band { get; internal set; }
        public string Group { get; internal set; }
        public string Who { get; internal set; }
        public string What { get; internal set; }
        public HitKind Kind { get; internal set; }
        // Checked against the kind's target; the rest are shown and only kept under DamageBands.BigBlowCap.
        public bool Checked { get; internal set; }
        public float Raw { get; internal set; }
        public int Level { get; internal set; }
        public float Bare { get; internal set; }
        // NaN in the first band (no gear below).
        public float Below { get; internal set; }
        public float Geared { get; internal set; }

        public DamageBands.Target Target => DamageBands.TargetOf(Kind);
        public float Gate => float.IsNaN(Below) ? float.NaN : Below / Geared;

        // Whether the row meets its target: in range, over the bare floor, and the gate where there is one.
        public bool Meets =>
            Checked
                ? Geared >= Target.Low - 0.0005f && Geared <= Target.High + 0.0005f && Bare >= Target.Bare - 0.0005f
                    && (float.IsNaN(Gate) || Gate >= DamageBands.GateLow && Gate <= DamageBands.GateHigh)
                : Geared <= Target.Aim * DamageBands.BigBlowCap;
    }

    public static class HitTable
    {
        // A field elite's hits (World.Elites.cs): a leader's, a big (L) leader's, a champion's.
        public static IReadOnlyList<HitRow> Build(LootData loot, MonsterData monsters, BossData bosses)
        {
            var rows = new List<HitRow>();
            var players = new Dictionary<(int Level, int Gear), PlayerStats>();
            PlayerStats Player(int level, int gear)
            {
                if (players.TryGetValue((level, gear), out var known)) return known;
                return players[(level, gear)] = PlayerStats.From(Wearing(loot, gear), level: level);
            }

            void Add(int band, int level, string group, string who, string what, HitKind kind, bool check, float raw)
            {
                var bare = Player(level, 0);
                var below = band > 1 ? Player(level, band - 1) : null;
                var geared = Player(level, band);
                rows.Add(new HitRow
                {
                    Band = band,
                    Group = group,
                    Who = who,
                    What = what,
                    Kind = kind,
                    Checked = check,
                    Raw = raw,
                    Level = level,
                    Bare = DamageBands.Taken(raw, bare.Armor, band) / bare.MaxHealth,
                    Below = below == null ? float.NaN : DamageBands.Taken(raw, below.Armor, band) / below.MaxHealth,
                    Geared = DamageBands.Taken(raw, geared.Armor, band) / geared.MaxHealth,
                });
            }

            foreach (var monster in monsters.Monsters.OrderBy(m => m.Band).ThenBy(m => m.Size).ThenBy(m => m.Id))
            {
                int band = monster.Band;
                int level = DamageBands.LevelOf(band);
                var kind = DamageBands.KindOf(monster);
                float usual = DamageBands.UsualPercent(monster) / 100f;
                Add(band, level, "몬스터", monster.Name, "보통 한 방", kind, true, monster.Damage * usual);
                foreach (var attack in monster.Attacks.Where(DamageBands.Hurts))
                    Add(band, level, "몬스터", monster.Name, attack.Name, kind, false, monster.Damage * attack.Damage / 100f);

                // As a field elite: a leader (XL ones lead as they are) and a champion (never L or XL).
                if (monster.Size == MonsterSize.XL) continue;
                float lead = monster.Big ? World.BigLeaderPower : World.LeaderPower;
                Add(band, level, "정예", monster.Name + " 우두머리", "보통 한 방", monster.Big ? HitKind.Large : HitKind.Elite, true,
                    monster.Damage * usual * lead);
                var big = World.BigAttackOf(monster);
                if (big != null)
                    Add(band, level, "정예", monster.Name + " 우두머리", big.Name + "(큰 공격)", monster.Big ? HitKind.Large : HitKind.Elite, false,
                        monster.Damage * big.Damage / 100f * lead * World.BigHitShare);
                if (!monster.Big)
                    Add(band, level, "정예", monster.Name + " 챔피언", "보통 한 방", HitKind.Elite, true, monster.Damage * usual * World.ChampionPower);
            }

            foreach (var boss in bosses.Bosses.OrderBy(b => b.Level))
            {
                int band = DamageBands.BandOfLevel(boss.Level);
                int level = boss.Level + 1;
                foreach (var attack in boss.Attacks)
                {
                    var kind = DamageBands.KindOf(attack);
                    if (kind == HitKind.None) continue;
                    Add(band, level, "보스", boss.Name, attack.Name, kind, true, attack.Damage);
                }
                if (boss.RoarDamage > 0) Add(band, level, "보스", boss.Name, "포효", HitKind.Roar, true, boss.RoarDamage);
            }

            // A rift's monsters at its first tier, in the void band (World.Rift.cs).
            foreach (var monster in World.RiftKinds(monsters.Monsters))
                Add(DamageBands.Bands, DamageBands.LevelOf(DamageBands.Bands), "균열", monster.Name, "1단계 보통 한 방", HitKind.Normal, true,
                    monster.Damage * DamageBands.UsualPercent(monster) / 100f * World.RiftPowerAt(1, monster));
            return rows;
        }

        // The band's full common set at the middle of its rolls, rounded down (sword, helmet, chest, boots, ring, amulet); 0 for none.
        public static Equipment Wearing(LootData loot, int band)
        {
            var equipment = new Equipment();
            if (band <= 0) return equipment;
            string tier = DamageBands.TierNames[DamageBands.Clamp(band) - 1];
            foreach (string slot in new[] { "sword", "helmet", "armor", "boots", "ring", "amulet" })
            {
                var itemBase = loot.FindBase($"{tier}_{slot}");
                var implicits = itemBase.Implicits.Select(r => new Modifier(r.Stat, r.Kind, (r.Min + r.Max) / 2, r.Tags)).ToList();
                equipment.Equip(new Item(itemBase, itemBase.Level, Rarity.Common, implicits, Array.Empty<ItemAffix>(), 0));
            }
            return equipment;
        }
    }
}
