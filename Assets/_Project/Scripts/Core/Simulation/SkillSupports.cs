using System;
using System.Collections.Generic;

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
    }

    // Skill supports (Docs/Classes.md, 스킬 변형): crystals made at the anvil (materials.csv, recipes.csv) that a
    // player sets into one learned skill each to change how it works, as Craft to Exile 2's support gems do. Taking
    // one out gives the crystal back. The crystals are materials; what each does is here, by its id.
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
        };

        public static IEnumerable<string> Ids => ById.Keys;

        public static SupportKind? KindOf(MaterialDefinition material) =>
            material != null && ById.TryGetValue(material.Id, out var kind) ? kind : (SupportKind?)null;

        // Whether the support changes anything for the skill: area for skills with a radius, more for projectiles and
        // spirits, an element for skills that hit; haste and power for any.
        public static bool Fits(SkillDefinition skill, SupportKind kind) => kind switch
        {
            SupportKind.Area => skill.Radius > 0,
            SupportKind.Multi => skill.Kind == SkillKind.Projectile || skill.Kind == SkillKind.Summon,
            SupportKind.Fire or SupportKind.Cold or SupportKind.Lightning => skill.Damage > 0 && skill.Kind != SkillKind.Heal,
            _ => true,
        };

        public static float Radius(SkillDefinition skill, SupportKind? kind) => skill.Radius * (kind == SupportKind.Area ? 1.35f : 1);

        public static int Count(SkillDefinition skill, SupportKind? kind) =>
            kind != SupportKind.Multi ? skill.Count : skill.Kind == SkillKind.Projectile ? skill.Count + 2 : skill.Kind == SkillKind.Summon ? skill.Count + 1 : skill.Count;

        public static float DamageFactor(SupportKind? kind) => kind switch
        {
            SupportKind.Multi => 0.75f,
            SupportKind.Haste => 0.85f,
            SupportKind.Power => 1.4f,
            _ => 1,
        };

        public static float CooldownFactor(SupportKind? kind) => kind == SupportKind.Haste ? 0.75f : kind == SupportKind.Power ? 1.15f : 1;

        public static int ManaCost(SkillDefinition skill, SupportKind? kind) =>
            (int)Math.Round(skill.Mana * (kind == SupportKind.Power ? 1.5f : kind == SupportKind.Area || kind == SupportKind.Multi ? 1.2f : 1));

        public static SkillTag Tags(SupportKind? kind) => kind switch
        {
            SupportKind.Fire => SkillTag.Fire,
            SupportKind.Cold => SkillTag.Cold,
            SupportKind.Lightning => SkillTag.Lightning,
            _ => SkillTag.None,
        };

        // What it does, for the screen.
        public static string Describe(SupportKind kind) => kind switch
        {
            SupportKind.Area => "범위 35% 넓게, 마나 20% 더",
            SupportKind.Multi => "투사체 2발 더(소환은 1마리 더), 피해 25% 덜, 마나 20% 더",
            SupportKind.Haste => "재사용 대기 25% 짧게, 피해 15% 덜",
            SupportKind.Power => "피해 40% 더, 재사용 대기 15% 길게, 마나 50% 더",
            SupportKind.Fire => "화염 태그가 붙는다(불태우기, 화염 피해 증가)",
            SupportKind.Cold => "냉기 태그가 붙는다(느리게, 냉기 피해 증가)",
            _ => "번개 태그가 붙는다(튀기기, 번개 피해 증가)",
        };
    }
}
