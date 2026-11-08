using System;

namespace Game.Core
{
    public enum StatType
    {
        MaxHealth,
        HealthRegen,
        Armor,
        MoveSpeed,
        Damage,
        AttackSpeed,
        CritChance,
        CritDamage,
        MiningSpeed,
        ItemRarity,
        MaxMana,
        ManaRegen,
        // Ranks added to the skills with the modifier's tags (Docs/Classes.md, 스킬을 키우는 특성): "Fire:SkillRank+1".
        SkillRank,
        // Percent faster skill cooldowns, written like item rarity ("+10%" an increase on 100).
        CooldownRecovery,
        // Percent longer buffs and spirits, likewise.
        SkillDuration,
    }

    // Weapons and skills carry tags. Class combinations come from modifiers that only count for some tags.
    [Flags]
    public enum SkillTag
    {
        None = 0,
        Melee = 1 << 0,
        Ranged = 1 << 1,
        Magic = 1 << 2,
        Summon = 1 << 3,
        Fire = 1 << 4,
        Cold = 1 << 5,
        Lightning = 1 << 6,
    }

    public enum ModifierKind
    {
        // +N
        Flat,
        // +N%, added up with every other increase to the same stat before multiplying
        Increased,
        // ×(1 + N%), each one multiplies on its own
        More,
    }
}
