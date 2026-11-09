using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum SupportKind : byte
    {
        // Wider: novas, bursts and dashes reach further; costs more mana.
        Area,
        // More: two more projectiles or one more spirit, each hitting softer; costs more mana.
        Multi,
        // Faster: a shorter cooldown, softer hits.
        Haste,
        // Stronger: harder hits, a longer cooldown and much more mana.
        Power,
        // An element added to the skill's tags, so its effects and every bonus for it count.
        Fire,
        Cold,
        Lightning,
        // Through: a projectile goes on through two more monsters, softer after each.
        Pierce,
        // Longer: buffs and spirits last longer; costs more mana.
        Duration,
        // Drinking: a share of what the skill deals heals its player; softer hits.
        Leech,
    }

    // Skill supports (Docs/Classes.md, 스킬 변형): crystals made at the anvil (materials.csv, recipes.csv) that a
    // player sets into learned skills to change how they work, as Craft to Exile 2's support gems do. A skill holds
    // one at rank 1, two from rank 3 and three at rank 5 (SlotsFor), never two of a kind; their effects multiply.
    // Taking one out gives the crystal back. The crystals are materials; what each does is here, by its id.
    public static class SkillSupports
    {
        static readonly Dictionary<string, SupportKind> ById = new Dictionary<string, SupportKind>
        {
            ["support_area"] = SupportKind.Area,
            ["support_multi"] = SupportKind.Multi,
            ["support_haste"] = SupportKind.Haste,
            ["support_power"] = SupportKind.Power,
            ["support_fire"] = SupportKind.Fire,
            ["support_cold"] = SupportKind.Cold,
            ["support_lightning"] = SupportKind.Lightning,
            ["support_pierce"] = SupportKind.Pierce,
            ["support_duration"] = SupportKind.Duration,
            ["support_leech"] = SupportKind.Leech,
        };

        public static IEnumerable<string> Ids => ById.Keys;

        public static SupportKind? KindOf(MaterialDefinition material) =>
            material != null && ById.TryGetValue(material.Id, out var kind) ? kind : (SupportKind?)null;

        public const int MaxSlots = 3;

        // How many crystals a skill learned to this rank holds: one, two from rank 3, three at rank 5.
        public static int SlotsFor(int rank) => rank >= 5 ? 3 : rank >= 3 ? 2 : rank >= 1 ? 1 : 0;

        // The rank a slot (0, 1, 2) opens at.
        public static int RankForSlot(int slot) => slot <= 0 ? 1 : slot == 1 ? 3 : 5;

        // Whether the support does the skill any good: area for skills with a radius, more for projectiles and
        // spirits, haste for any but spirits (calling them again sends the old ones away, so a shorter wait gains
        // nothing), power for skills that hit or heal, an element for skills that hit and do not carry it already,
        // through for projectiles that do not burst, longer for buffs and spirits, drinking for skills that hit.
        // Buffs and a dash that does not hit only wait longer under power, so it is not offered for them.
        public static bool Fits(SkillDefinition skill, SupportKind kind) => kind switch
        {
            SupportKind.Area => skill.Radius > 0,
            SupportKind.Multi => skill.Kind == SkillKind.Projectile || skill.Kind == SkillKind.Summon,
            SupportKind.Haste => skill.Kind != SkillKind.Summon,
            SupportKind.Power => skill.Damage > 0,
            SupportKind.Pierce => skill.Kind == SkillKind.Projectile && skill.Radius <= 0,
            SupportKind.Duration => skill.Kind == SkillKind.Buff || skill.Kind == SkillKind.Summon,
            SupportKind.Leech => skill.Damage > 0 && skill.Kind != SkillKind.Heal,
            _ => skill.Damage > 0 && skill.Kind != SkillKind.Heal && (skill.Tags & Tag(kind)) == 0,
        };

        // Whether the crystal can go in beside those the skill holds at that rank: it fits, the skill has none of its
        // kind yet and a slot is free.
        public static bool CanAdd(SkillDefinition skill, int rank, IReadOnlyCollection<SupportKind> held, SupportKind kind) =>
            Fits(skill, kind) && !(held?.Contains(kind) ?? false) && (held?.Count ?? 0) < SlotsFor(rank);

        static bool Has(IReadOnlyCollection<SupportKind> kinds, SupportKind kind) => kinds != null && kinds.Contains(kind);

        public const float AreaRadius = 1.2f;

        public static float Radius(SkillDefinition skill, IReadOnlyCollection<SupportKind> kinds) =>
            skill.Radius * (Has(kinds, SupportKind.Area) ? AreaRadius : 1);

        // Two more projectiles, or one more spirit.
        static int Extra(SkillDefinition skill) => skill.Kind == SkillKind.Projectile ? 2 : skill.Kind == SkillKind.Summon ? 1 : 0;

        public static int Count(SkillDefinition skill, IReadOnlyCollection<SupportKind> kinds) =>
            Has(kinds, SupportKind.Multi) ? skill.Count + Extra(skill) : skill.Count;

        // What every hit (or heal) of the skill is worth with one support. The extra projectiles or spirits share
        // MultiTotal of what the skill dealt, all of them hitting, so a single fireball split in three is not three
        // times as strong on a boss (the balance check of 2026-11-09, `--supports`); a skill that already has many
        // keeps its hits whole. Lightning jumps to two more monsters for half each, which near doubled any skill that
        // hit a pack, so its hits are softer; fire's burn adds about 30% on its own.
        public const float MultiTotal = 1.4f;

        public static float DamageFactor(SkillDefinition skill, SupportKind kind) => kind switch
        {
            SupportKind.Multi => Math.Min(1f, MultiTotal * Math.Max(1, skill.Count) / (Math.Max(1, skill.Count) + Extra(skill))),
            SupportKind.Haste => 0.85f,
            SupportKind.Power => 1.4f,
            // Spirits hit often and lightly, and a burn only takes over from a bigger one, so it adds little to them.
            SupportKind.Fire => skill.Kind == SkillKind.Summon ? 1 : 0.9f,
            SupportKind.Lightning => 0.7f,
            SupportKind.Leech => 0.9f,
            _ => 1,
        };

        // All the skill's supports together: their factors multiply.
        public static float DamageFactor(SkillDefinition skill, IReadOnlyCollection<SupportKind> kinds)
        {
            float factor = 1;
            if (kinds != null) foreach (var kind in kinds) factor *= DamageFactor(skill, kind);
            return factor;
        }

        public static float CooldownFactor(IReadOnlyCollection<SupportKind> kinds) =>
            (Has(kinds, SupportKind.Haste) ? 0.75f : 1) * (Has(kinds, SupportKind.Power) ? 1.15f : 1);

        public const float DurationLonger = 1.4f;

        public static float DurationFactor(IReadOnlyCollection<SupportKind> kinds) => Has(kinds, SupportKind.Duration) ? DurationLonger : 1;

        static float ManaFactor(SupportKind kind) => kind switch
        {
            SupportKind.Power => 1.5f,
            SupportKind.Area or SupportKind.Multi or SupportKind.Pierce or SupportKind.Duration => 1.2f,
            _ => 1,
        };

        public static int ManaCost(SkillDefinition skill, IReadOnlyCollection<SupportKind> kinds)
        {
            float factor = 1;
            if (kinds != null) foreach (var kind in kinds) factor *= ManaFactor(kind);
            return (int)Math.Round(skill.Mana * factor);
        }

        // Through: how many more monsters a projectile goes on through, and what each later hit is worth of the one
        // before.
        public const int PierceCount = 2;
        public const float PierceFalloff = 0.4f;

        public static int Pierces(IReadOnlyCollection<SupportKind> kinds) => Has(kinds, SupportKind.Pierce) ? PierceCount : 0;

        // Drinking: the share of the damage a skill deals that heals its player.
        public const float LeechShare = 0.04f;

        public static float Leech(IReadOnlyCollection<SupportKind> kinds) => Has(kinds, SupportKind.Leech) ? LeechShare : 0;

        static SkillTag Tag(SupportKind kind) => kind switch
        {
            SupportKind.Fire => SkillTag.Fire,
            SupportKind.Cold => SkillTag.Cold,
            SupportKind.Lightning => SkillTag.Lightning,
            _ => SkillTag.None,
        };

        public static SkillTag Tags(IReadOnlyCollection<SupportKind> kinds)
        {
            var tags = SkillTag.None;
            if (kinds != null) foreach (var kind in kinds) tags |= Tag(kind);
            return tags;
        }

        // What it does, for the screen; with the skill, the hit for the split of that skill.
        public static string Describe(SupportKind kind, SkillDefinition skill = null) => kind switch
        {
            SupportKind.Area => "범위 20% 넓게(넓이 1.44배), 마나 20% 더",
            SupportKind.Multi when skill != null && skill.Kind == SkillKind.Summon => $"1마리 더, 한 마리 피해 {Less(skill, kind)}, 마나 20% 더",
            SupportKind.Multi when skill != null => $"투사체 2발 더, 한 발 피해 {Less(skill, kind)}, 마나 20% 더",
            SupportKind.Multi => "투사체 2발 더(소환은 1마리 더), 다 맞으면 피해 40%까지 더, 마나 20% 더",
            SupportKind.Haste => "재사용 대기 25% 짧게, 피해·회복 15% 덜",
            SupportKind.Power => "피해·회복 40% 더, 재사용 대기 15% 길게, 마나 50% 더",
            SupportKind.Fire when skill != null && skill.Kind == SkillKind.Summon => "화염 태그가 붙는다(불태우기, 화염 피해 증가)",
            SupportKind.Fire => "화염 태그가 붙는다(불태우기, 화염 피해 증가), 피해 10% 덜(소환은 그대로)",
            SupportKind.Cold => "냉기 태그가 붙는다(느리게, 냉기 피해 증가)",
            SupportKind.Lightning => "번개 태그가 붙는다(옆의 둘에게 튄다, 번개 피해 증가), 피해 30% 덜",
            SupportKind.Pierce => "투사체가 적 둘을 더 뚫고 간다(뚫을 때마다 피해 60% 덜), 마나 20% 더",
            SupportKind.Duration => "지속 시간 40% 길게, 마나 20% 더",
            _ => "준 피해의 4%만큼 생명력이 찬다, 피해 10% 덜",
        };

        static string Less(SkillDefinition skill, SupportKind kind)
        {
            int less = (int)Math.Round((1 - DamageFactor(skill, kind)) * 100);
            return less <= 0 ? "그대로" : $"{less}% 덜";
        }
    }
}
