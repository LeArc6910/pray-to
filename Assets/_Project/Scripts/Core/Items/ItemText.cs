using System;
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
            Rarity.Legendary => "전설",
            _ => rarity.ToString(),
        };

        // The Korean names of the class ids gear names (classes.csv), so the text needs no sheet.
        public static string ClassName(string id) => id switch
        {
            "warrior" => "전사",
            "fire_mage" => "화염 마법사",
            "archer" => "궁수",
            "summoner" => "소환사",
            "storm_caller" => "폭풍술사",
            _ => id,
        };

        static string ClassLine(string classId, IEnumerable<Modifier> bonus, IReadOnlyCollection<string> classes)
        {
            string mark = classes == null ? "" : classes.Contains(classId) ? " (적용)" : " (다른 클래스)";
            return $"{ClassName(classId)} 보너스: {string.Join(", ", bonus.Select(StatText.Describe))}{mark}";
        }

        // The second weapon's slot (Docs/Weapons.md, 무기 두 벌).
        public const string SpareName = "보조 무기";

        // A weapon's kind and how its basic attack goes, for the tooltip: "대검 · 근접 · 공격 속도 ×0.6 — 느리지만 …".
        public static string KindLine(ItemBase itemBase)
        {
            var kind = itemBase?.Kind;
            if (kind == null) return "";
            string branch = kind.Branch == SkillTag.None ? "클래스 무기 아님" : StatText.TagName(kind.Branch);
            string pace = Math.Abs(kind.Speed - 1) < 0.01f ? "" : $" · 공격 속도 ×{kind.Speed:0.##}";
            return $"{kind.Name} · {branch}{pace}" + (kind.Description.Length > 0 ? $" — {kind.Description}" : "");
        }

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
        // "+3 철 검" once an anvil has worked it.
        public static string Name(Item item) => item.Upgrade > 0 ? $"+{item.Upgrade} {BaseName(item)}" : BaseName(item);

        static string BaseName(Item item)
        {
            if (item.Runeword != null) return $"{item.Base.Name} 「{item.Runeword.Name}」";
            if (item.Rarity != Rarity.Uncommon) return item.Base.Name;

            var suffix = item.Affixes.FirstOrDefault(a => a.Affix.Kind == AffixKind.Suffix);
            var prefix = item.Affixes.FirstOrDefault(a => a.Affix.Kind == AffixKind.Prefix);
            return string.Join(" ", new[] { suffix?.Affix.Name, prefix?.Affix.Name, item.Base.Name }.Where(part => part != null));
        }

        // "고유" for an elite's unique (Docs/Gear.md); otherwise the rarity's name.
        public static string GradeName(Item item) => item.Base.IsEliteUnique ? "고유" : RarityName(item.Rarity);

        // worn: what the player has on, so a set says how many of it are worn and which bonuses hold; classes: the
        // player's class ids, so a class bonus says whether it counts. Both may be left out.
        public static List<string> Describe(Item item, bool showTiers = false, Equipment worn = null, IReadOnlyCollection<string> classes = null)
        {
            var lines = new List<string> { $"[{GradeName(item)}] {Name(item)} (레벨 {item.Level})", $"  착용 레벨 {item.Base.RequiredLevel}" };
            if (item.Base.Slot == ItemSlot.Weapon) lines.Add("  " + KindLine(item.Base));
            lines.AddRange(item.Implicits.Select(m => "  " + StatText.Describe(m)));

            foreach (var affix in item.Affixes.OrderBy(a => a.Affix.Kind))
            {
                string tier = showTiers ? $" [T{affix.Tier.Number}]" : "";
                lines.Add($"  · {StatText.Describe(affix.Modifier)}{tier}");
            }

            lines.AddRange(item.Base.Effects.Select(e => $"  ★ {e.Describe()}"));
            if (item.Base.ClassBonus.Count > 0) lines.Add("  " + ClassLine(item.Base.ClassId, item.Base.ClassBonus, classes));
            if (item.Base.Set is GearSet set)
            {
                int count = worn == null ? -1 : worn.Items.Count(i => i.Base.Set == set);
                lines.Add(count < 0 ? $"  세트: {set.Name}" : $"  세트: {set.Name} ({count}/{set.MostPieces})");
                foreach (var bonus in set.AllBonuses)
                {
                    var parts = bonus.Modifiers.Select(StatText.Describe).Concat(bonus.Effects.Select(e => e.Describe())).ToList();
                    if (bonus.ClassBonus.Count > 0) parts.Add(ClassLine(bonus.ClassId, bonus.ClassBonus, classes));
                    string held = count >= bonus.Pieces ? " (적용)" : "";
                    lines.Add($"  · {bonus.Pieces}개: {string.Join(", ", parts)}{held}");
                }
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
