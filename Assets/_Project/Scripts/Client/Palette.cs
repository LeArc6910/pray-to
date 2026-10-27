using Game.Core;
using UnityEngine;

namespace Game.Client
{
    static class Palette
    {
        public static readonly Color Rune = new Color(1f, 0.65f, 0.3f);

        public static Color Of(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => new Color(0.45f, 0.95f, 0.45f),
            Rarity.Rare => new Color(0.45f, 0.7f, 1f),
            Rarity.Epic => new Color(0.8f, 0.5f, 1f),
            _ => new Color(0.85f, 0.85f, 0.85f),
        };
    }
}
