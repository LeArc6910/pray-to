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

        public static string Describe(Modifier modifier)
        {
            string subject = Name(modifier.Stat);
            if (modifier.Tags != SkillTag.None)
            {
                subject = string.Join(" ", ModifierSyntax.TagsIn(modifier.Tags).Select(TagName)) + " " + subject;
            }

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
}
