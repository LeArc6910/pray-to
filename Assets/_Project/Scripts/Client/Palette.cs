using Game.Core;
using UnityEngine;

namespace Game.Client
{
    static class Palette
    {
        public static readonly Color Rune = new Color(1f, 0.65f, 0.3f);
        public static readonly Color Potion = new Color(0.95f, 0.35f, 0.45f);
        public static readonly Color Heal = new Color(0.45f, 1f, 0.5f);
        public static readonly Color Material = new Color(0.75f, 0.75f, 0.72f);
        public static readonly Color32 CopperOre = new Color32(214, 120, 64, 255);
        public static readonly Color32 IronOre = new Color32(168, 184, 204, 255);
        public static readonly Color Dust = new Color(0.8f, 0.72f, 0.6f);
        public static readonly Color Sprout = new Color(0.45f, 0.8f, 0.35f);
        public static readonly Color Ripe = new Color(0.95f, 0.75f, 0.55f);

        public static Color Of(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => new Color(0.45f, 0.95f, 0.45f),
            Rarity.Rare => new Color(0.45f, 0.7f, 1f),
            Rarity.Epic => new Color(0.8f, 0.5f, 1f),
            _ => new Color(0.85f, 0.85f, 0.85f),
        };

        // A skill's colour from its tags, elements first: a fire whirlwind burns orange, a plain one is steel.
        public static Color Of(SkillTag tags)
        {
            if ((tags & SkillTag.Fire) != 0) return new Color(1f, 0.5f, 0.15f);
            if ((tags & SkillTag.Cold) != 0) return new Color(0.55f, 0.85f, 1f);
            if ((tags & SkillTag.Lightning) != 0) return new Color(1f, 0.95f, 0.4f);
            if ((tags & SkillTag.Magic) != 0) return new Color(0.75f, 0.55f, 1f);
            if ((tags & SkillTag.Melee) != 0) return new Color(0.9f, 0.9f, 0.95f);
            return new Color(1f, 0.8f, 0.35f);
        }
    }
}
