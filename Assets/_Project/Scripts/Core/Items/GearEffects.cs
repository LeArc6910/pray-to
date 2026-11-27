using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    // What a piece of gear or a set does besides its stats (Docs/Gear.md, 특이 기능): written in item_bases.csv and
    // sets.csv as "kill_heal:3;roll_ward:8/4/6", the numbers of one effect parted by '/'.
    public enum GearEffectKind
    {
        // A kill heals A% of the most health.
        KillHeal,
        // A kill gives back A mana.
        KillMana,
        // A roll raises a ward of A% of the most health for B seconds, at most once in C seconds.
        RollWard,
        // A blow taken deals A% of it back to the monster that struck.
        Thorns,
        // Under A% health: B% more damage dealt and C% less taken.
        LowHealth,
        // A% more damage to a monster still at full health.
        FirstStrike,
        // A critical hit sets the target burning for A% of the hit.
        CritBurn,
        // A% more damage to a burning target.
        VsBurning,
        // A% more damage to a chilled (slowed) target.
        VsChilled,
        // A% more damage while a ward holds.
        WardDamage,
    }

    public readonly struct GearEffect
    {
        public GearEffect(GearEffectKind kind, float a, float b = 0, float c = 0)
        {
            Kind = kind;
            A = a;
            B = b;
            C = c;
        }

        public GearEffectKind Kind { get; }
        public float A { get; }
        public float B { get; }
        public float C { get; }

        static readonly (string Name, GearEffectKind Kind, int Numbers)[] Names =
        {
            ("kill_heal", GearEffectKind.KillHeal, 1),
            ("kill_mana", GearEffectKind.KillMana, 1),
            ("roll_ward", GearEffectKind.RollWard, 3),
            ("thorns", GearEffectKind.Thorns, 1),
            ("low_health", GearEffectKind.LowHealth, 3),
            ("first_strike", GearEffectKind.FirstStrike, 1),
            ("crit_burn", GearEffectKind.CritBurn, 1),
            ("vs_burning", GearEffectKind.VsBurning, 1),
            ("vs_chilled", GearEffectKind.VsChilled, 1),
            ("ward_damage", GearEffectKind.WardDamage, 1),
        };

        public static GearEffect Parse(string text)
        {
            var parts = text.Trim().Split(':');
            if (parts.Length != 2) throw new FormatException($"효과 '{text}'는 '이름:숫자'로 적습니다.");
            var entry = Names.FirstOrDefault(n => n.Name == parts[0].Trim());
            if (entry.Name == null) throw new FormatException($"효과 '{parts[0]}'는 모릅니다({string.Join(", ", Names.Select(n => n.Name))}).");
            var numbers = parts[1].Split('/').Select(n => float.TryParse(n.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0
                ? v : throw new FormatException($"효과 '{text}'의 숫자 '{n}'를 읽지 못했습니다.")).ToArray();
            if (numbers.Length != entry.Numbers) throw new FormatException($"효과 '{entry.Name}'에는 숫자가 {entry.Numbers}개 있어야 합니다.");
            return new GearEffect(entry.Kind, numbers[0], numbers.Length > 1 ? numbers[1] : 0, numbers.Length > 2 ? numbers[2] : 0);
        }

        public static IReadOnlyList<GearEffect> ParseList(string text) =>
            string.IsNullOrWhiteSpace(text) ? Array.Empty<GearEffect>()
            : text.Split(';').Where(p => p.Trim().Length > 0).Select(Parse).ToList();

        static string N(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        public string Describe() => Kind switch
        {
            GearEffectKind.KillHeal => $"처치하면 생명력 {N(A)}% 회복",
            GearEffectKind.KillMana => $"처치하면 마나 {N(A)} 회복",
            GearEffectKind.RollWard => $"구르면 {N(B)}초 동안 생명력 {N(A)}% 보호막({N(C)}초마다)",
            GearEffectKind.Thorns => $"맞으면 받은 피해의 {N(A)}%를 되돌림",
            GearEffectKind.LowHealth => $"생명력 {N(A)}% 아래면 피해 {N(B)}% 더, 받는 피해 {N(C)}% 덜",
            GearEffectKind.FirstStrike => $"생명력이 가득한 적에게 피해 {N(A)}% 더",
            GearEffectKind.CritBurn => $"치명타가 적을 불태움(피해의 {N(A)}%)",
            GearEffectKind.VsBurning => $"불타는 적에게 피해 {N(A)}% 더",
            GearEffectKind.VsChilled => $"느려진 적에게 피해 {N(A)}% 더",
            GearEffectKind.WardDamage => $"보호막이 있는 동안 피해 {N(A)}% 더",
            _ => Kind.ToString(),
        };
    }

    // All a player's gear effects worn at once, added up (PlayerStats.Effects). The same kind twice adds its numbers;
    // a ward takes the biggest share, the longest time and the shortest wait.
    public sealed class GearEffects
    {
        public static readonly GearEffects None = new GearEffects(Array.Empty<GearEffect>());

        // Less damage taken never goes past this, however many pieces give it.
        public const float MostLessTaken = 50;

        public GearEffects(IEnumerable<GearEffect> effects)
        {
            foreach (var effect in effects)
            {
                switch (effect.Kind)
                {
                    case GearEffectKind.KillHeal: KillHeal += effect.A; break;
                    case GearEffectKind.KillMana: KillMana += effect.A; break;
                    case GearEffectKind.RollWard:
                        WardShare = Math.Max(WardShare, effect.A);
                        WardSeconds = Math.Max(WardSeconds, effect.B);
                        WardCooldown = WardCooldown > 0 ? Math.Min(WardCooldown, effect.C) : effect.C;
                        break;
                    case GearEffectKind.Thorns: Thorns += effect.A; break;
                    case GearEffectKind.LowHealth:
                        LowHealthBelow = Math.Max(LowHealthBelow, effect.A);
                        LowHealthMore += effect.B;
                        LowHealthLess = Math.Min(MostLessTaken, LowHealthLess + effect.C);
                        break;
                    case GearEffectKind.FirstStrike: FirstStrike += effect.A; break;
                    case GearEffectKind.CritBurn: CritBurn += effect.A; break;
                    case GearEffectKind.VsBurning: VsBurning += effect.A; break;
                    case GearEffectKind.VsChilled: VsChilled += effect.A; break;
                    case GearEffectKind.WardDamage: WardDamage += effect.A; break;
                }
                Any = true;
            }
        }

        public bool Any { get; }
        public float KillHeal { get; }
        public float KillMana { get; }
        public float WardShare { get; }
        public float WardSeconds { get; }
        public float WardCooldown { get; }
        public float Thorns { get; }
        public float LowHealthBelow { get; }
        public float LowHealthMore { get; }
        public float LowHealthLess { get; }
        public float FirstStrike { get; }
        public float CritBurn { get; }
        public float VsBurning { get; }
        public float VsChilled { get; }
        public float WardDamage { get; }

        public bool Low(float health, float maxHealth) => LowHealthBelow > 0 && maxHealth > 0 && health < maxHealth * LowHealthBelow / 100f;

        // How much more a blow of the player's deals for what the effects ask of the moment.
        public float DealtFactor(float health, float maxHealth, bool warded, bool targetFull, bool targetBurning, bool targetChilled)
        {
            float more = 0;
            if (Low(health, maxHealth)) more += LowHealthMore;
            if (warded) more += WardDamage;
            if (targetFull) more += FirstStrike;
            if (targetBurning) more += VsBurning;
            if (targetChilled) more += VsChilled;
            return 1 + more / 100f;
        }

        // The share of a blow that still lands.
        public float TakenFactor(float health, float maxHealth) => Low(health, maxHealth) ? 1 - LowHealthLess / 100f : 1;
    }
}
