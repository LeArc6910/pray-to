using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public static class ItemText
    {
        public static string RarityName(Rarity rarity) => rarity switch
        {
            Rarity.Common => "일반",
            Rarity.Uncommon => "고급",
            Rarity.Rare => "희귀",
            Rarity.Epic => "영웅",
            _ => rarity.ToString(),
        };

        public static string SlotName(ItemSlot slot) => slot switch
        {
            ItemSlot.Weapon => "무기",
            ItemSlot.Helmet => "투구",
            ItemSlot.Chest => "갑옷",
            ItemSlot.Boots => "장화",
            ItemSlot.Ring => "반지",
            ItemSlot.Amulet => "목걸이",
            _ => slot.ToString(),
        };

        // Uncommon items are named after their affixes ("신속의 날카로운 구리 검"). Rarer items have too many
        // affixes for that, so they keep the base name and list the affixes below it.
        public static string Name(Item item)
        {
            if (item.Runeword != null) return $"{item.Base.Name} 「{item.Runeword.Name}」";
            if (item.Rarity != Rarity.Uncommon) return item.Base.Name;

            var suffix = item.Affixes.FirstOrDefault(a => a.Affix.Kind == AffixKind.Suffix);
            var prefix = item.Affixes.FirstOrDefault(a => a.Affix.Kind == AffixKind.Prefix);
            return string.Join(" ", new[] { suffix?.Affix.Name, prefix?.Affix.Name, item.Base.Name }.Where(part => part != null));
        }

        public static List<string> Describe(Item item, bool showTiers = false)
        {
            var lines = new List<string> { $"[{RarityName(item.Rarity)}] {Name(item)} (레벨 {item.Level})" };
            lines.AddRange(item.Implicits.Select(m => "  " + StatText.Describe(m)));

            foreach (var affix in item.Affixes.OrderBy(a => a.Affix.Kind))
            {
                string tier = showTiers ? $" [T{affix.Tier.Number}]" : "";
                lines.Add($"  · {StatText.Describe(affix.Modifier)}{tier}");
            }

            if (item.Sockets.Count == 0) return lines;
            lines.Add("  룬 소켓 " + string.Join(" ", item.Sockets.Select(rune => rune == null ? "[ ]" : $"[{rune.Name}]")));
            foreach (var rune in item.Sockets.Where(r => r != null))
            {
                lines.AddRange(rune.ModifiersFor(item.Base.Category).Select(m => $"  · {StatText.Describe(m)} ({rune.Name})"));
            }
            if (item.Runeword != null)
            {
                lines.AddRange(item.Runeword.Modifiers.Select(m => $"  · {StatText.Describe(m)} (룬워드)"));
            }
            return lines;
        }
    }
}
