using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    public static class StatText
    {
        public static string Name(StatType stat) => stat switch
        {
            StatType.MaxHealth => "최대 생명력",
            StatType.HealthRegen => "생명력 재생",
            StatType.Armor => "방어도",
            StatType.MoveSpeed => "이동 속도",
            StatType.Damage => "피해",
            StatType.AttackSpeed => "공격 속도",
            StatType.CritChance => "치명타 확률",
            StatType.CritDamage => "치명타 피해",
            StatType.MiningSpeed => "채굴 속도",
            StatType.ItemRarity => "아이템 희귀도",
            StatType.MaxMana => "최대 마나",
            StatType.ManaRegen => "마나 재생",
            StatType.SkillRank => "스킬 단계",
            StatType.CooldownRecovery => "스킬 재사용 속도",
            StatType.SkillDuration => "스킬 지속 시간",
            _ => stat.ToString(),
        };

        public static string TagName(SkillTag tag) => tag switch
        {
            SkillTag.Melee => "근접",
            SkillTag.Ranged => "원거리",
            SkillTag.Magic => "마법",
            SkillTag.Summon => "소환",
            SkillTag.Fire => "화염",
            SkillTag.Cold => "냉기",
            SkillTag.Lightning => "번개",
            _ => tag.ToString(),
        };

        // "마법 화염" for Magic | Fire.
        public static string TagNames(SkillTag tags) => string.Join(" ", ModifierSyntax.TagsIn(tags).Select(TagName));

        public static string Describe(Modifier modifier)
        {
            string subject = Name(modifier.Stat);
            if (modifier.Tags != SkillTag.None) subject = TagNames(modifier.Tags) + " " + subject;

            int value = modifier.Value;
            switch (modifier.Kind)
            {
                case ModifierKind.Increased:
                    return value >= 0 ? $"{subject} {value}% 증가" : $"{subject} {-value}% 감소";
                case ModifierKind.More:
                    return value >= 0 ? $"{subject} {value}% 증폭" : $"{subject} {-value}% 감쇄";
                default:
                    string unit = IsPercentPoint(modifier.Stat) ? "%" : "";
                    return value >= 0 ? $"{subject} +{value}{unit}" : $"{subject} -{-value}{unit}";
            }
        }

        // A modifier as a sheet writes it, before it is rolled: "피해 +4~6".
        public static string Describe(ModifierRange range)
        {
            string low = Describe(new Modifier(range.Stat, range.Kind, range.Min, range.Tags));
            if (range.IsFixed) return low;
            string number = Math.Abs(range.Min).ToString(CultureInfo.InvariantCulture);
            int at = low.LastIndexOf(number, StringComparison.Ordinal);
            return low.Substring(0, at + number.Length) + "~" + Math.Abs(range.Max) + low.Substring(at + number.Length);
        }

        // These stats are percentages themselves, so a flat +3 reads as +3%.
        static bool IsPercentPoint(StatType stat) => stat == StatType.CritChance || stat == StatType.CritDamage;
    }

    public static class SkillText
    {
        // The skill's numbers in one line, e.g. "피해 150% · 둘레 1.8칸 · 재사용 4초".
        // At a rank (Docs/Classes.md, 스킬 포인트와 단계): its damage, cooldown and time as that rank has them. With the
        // player's stats, the damage also in points ("피해 31 (150%)"), a heal in health, and the cooldown and time
        // with what the passive tree and gear add. With the crystals set in it (스킬 변형), as they make it: the
        // hit, reach, count, time, mana and cooldown, and what they add besides.
        public static string Numbers(SkillDefinition skill, int rank = 1, PlayerStats stats = null, IReadOnlyCollection<SupportKind> supports = null)
        {
            var parts = new List<string>();
            int damage = (int)Math.Round(skill.Damage * SkillRules.DamageScale(rank) * SkillSupports.DamageFactor(skill, supports));
            string Hit(string percent) => stats == null ? percent : $"{Points(stats.DamageFor(stats.ContextFor(skill) | SkillSupports.Tags(supports)) * damage / 100f)} ({percent})";
            string duration = Number(SkillRules.DurationTicks(skill, rank, stats?.SkillDurationFor(skill.Tags) ?? 0) * SkillSupports.DurationFactor(supports) / World.TicksPerSecond);
            float radius = SkillSupports.Radius(skill, supports);
            int count = SkillSupports.Count(skill, supports);
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    parts.Add($"피해 {Hit($"{damage}%")}");
                    parts.Add($"둘레 {Number(radius)}칸");
                    break;
                case SkillKind.Projectile:
                    parts.Add(count > 1 ? $"피해 {Hit($"{damage}%")} × {count}발" : $"피해 {Hit($"{damage}%")}");
                    parts.Add($"사거리 {Number(skill.Range)}칸");
                    if (radius > 0) parts.Add($"폭발 {Number(radius)}칸");
                    break;
                case SkillKind.Buff:
                    var effects = skill.Modifiers.Select(StatText.Describe).ToList();
                    if (skill.Grants != SkillTag.None) effects.Add($"모든 공격에 {StatText.TagNames(skill.Grants)} 태그");
                    parts.Add($"{duration}초 동안 {string.Join(", ", effects)}");
                    break;
                case SkillKind.Summon:
                    parts.Add($"{count}마리 · {duration}초");
                    parts.Add($"한 번에 피해 {Hit($"{damage}%")}");
                    parts.Add($"{Number(skill.Range)}칸 안의 적");
                    break;
                case SkillKind.Dash:
                    parts.Add($"{Number(skill.Range)}칸 돌진");
                    if (skill.Damage > 0) parts.Add($"지나는 적에게 피해 {Hit($"{damage}%")}");
                    break;
                case SkillKind.Heal:
                    parts.Add(stats == null ? $"최대 생명력의 {damage}% 회복" : $"생명력 {Points(stats.MaxHealth * damage / 100f)} 회복 (최대의 {damage}%)");
                    break;
            }
            if (SkillSupports.Pierces(supports) > 0) parts.Add($"적 {SkillSupports.Pierces(supports)}을 더 뚫는다");
            if (SkillSupports.Leech(supports) > 0) parts.Add($"준 피해의 {Math.Round(SkillSupports.Leech(supports) * 100)}% 회복");
            var added = SkillSupports.Tags(supports);
            if (added != SkillTag.None) parts.Add($"{StatText.TagNames(added)} 태그");
            int mana = SkillSupports.ManaCost(skill, supports);
            if (mana > 0) parts.Add($"마나 {mana}");
            parts.Add($"재사용 {Number(SkillRules.CooldownTicks(skill, rank, stats?.CooldownRecoveryFor(skill.Tags) ?? 0) * SkillSupports.CooldownFactor(supports) / World.TicksPerSecond)}초");
            return string.Join(" · ", parts);
        }

        static string Points(float value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

        // "최대 생명력 20% 증가, 근접 피해 10% 증가"
        public static string Passive(ClassDefinition chosen) => string.Join(", ", chosen.Passive.Select(StatText.Describe));

        static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
