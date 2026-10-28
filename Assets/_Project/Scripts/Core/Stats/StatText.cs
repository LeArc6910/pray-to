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

        // These stats are percentages themselves, so a flat +3 reads as +3%.
        static bool IsPercentPoint(StatType stat) => stat == StatType.CritChance || stat == StatType.CritDamage;
    }

    public static class SkillText
    {
        // The skill's numbers in one line, e.g. "피해 150% · 둘레 1.8칸 · 재사용 4초".
        public static string Numbers(SkillDefinition skill)
        {
            var parts = new List<string>();
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    parts.Add($"피해 {skill.Damage}%");
                    parts.Add($"둘레 {Number(skill.Radius)}칸");
                    break;
                case SkillKind.Projectile:
                    parts.Add($"피해 {skill.Damage}%");
                    parts.Add($"사거리 {Number(skill.Range)}칸");
                    if (skill.Radius > 0) parts.Add($"폭발 {Number(skill.Radius)}칸");
                    break;
                case SkillKind.Buff:
                    var effects = skill.Modifiers.Select(StatText.Describe).ToList();
                    if (skill.Grants != SkillTag.None) effects.Add($"모든 공격에 {StatText.TagNames(skill.Grants)} 태그");
                    parts.Add($"{Number(skill.Duration)}초 동안 {string.Join(", ", effects)}");
                    break;
            }
            parts.Add($"재사용 {Number(skill.Cooldown)}초");
            return string.Join(" · ", parts);
        }

        // "최대 생명력 20% 증가, 근접 피해 10% 증가"
        public static string Passive(ClassDefinition chosen) => string.Join(", ", chosen.Passive.Select(StatText.Describe));

        static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
