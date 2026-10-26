using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Core
{
    public readonly struct Modifier : IEquatable<Modifier>
    {
        public Modifier(StatType stat, ModifierKind kind, int value, SkillTag tags = SkillTag.None)
        {
            Stat = stat;
            Kind = kind;
            Value = value;
            Tags = tags;
        }

        public StatType Stat { get; }
        public ModifierKind Kind { get; }
        public int Value { get; }
        // A tagged modifier only counts where every one of its tags is present.
        public SkillTag Tags { get; }

        public bool AppliesTo(SkillTag context) => (context & Tags) == Tags;

        public bool Equals(Modifier other) => Stat == other.Stat && Kind == other.Kind && Value == other.Value && Tags == other.Tags;

        public override bool Equals(object obj) => obj is Modifier other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Stat, Kind, Value, Tags);

        public override string ToString() => ModifierSyntax.Format(new ModifierRange(Stat, Kind, Value, Value, Tags));
    }

    // A modifier as the sheets write it. Items roll a value inside the range once, when they drop.
    public sealed class ModifierRange
    {
        public ModifierRange(StatType stat, ModifierKind kind, int min, int max, SkillTag tags = SkillTag.None)
        {
            if (max < min) throw new ArgumentException("max is below min.", nameof(max));
            Stat = stat;
            Kind = kind;
            Min = min;
            Max = max;
            Tags = tags;
        }

        public StatType Stat { get; }
        public ModifierKind Kind { get; }
        public int Min { get; }
        public int Max { get; }
        public SkillTag Tags { get; }
        public bool IsFixed => Min == Max;

        public Modifier Roll(GameRandom random) => new Modifier(Stat, Kind, IsFixed ? Min : random.Next(Min, Max + 1), Tags);

        public override string ToString() => ModifierSyntax.Format(this);
    }

    // The text form the sheets use: "[Tag&Tag:]Stat+N[~M][%]", "Stat-N[%]" or "Stat*N%". See Docs/Loot.md.
    public static class ModifierSyntax
    {
        public static ModifierRange Parse(string text)
        {
            string rest = (text ?? "").Trim();
            var tags = SkillTag.None;
            int colon = rest.IndexOf(':');
            if (colon >= 0)
            {
                foreach (string tag in rest.Substring(0, colon).Split('&'))
                {
                    tags |= SheetValues.ParseEnum<SkillTag>(tag.Trim(), "태그");
                }
                rest = rest.Substring(colon + 1).Trim();
            }

            int sign = rest.IndexOfAny(new[] { '+', '-', '*' });
            if (sign <= 0) throw new FormatException($"'{text}': 능력치 이름 뒤에 +, -, * 중 하나가 와야 합니다.");
            var stat = SheetValues.ParseEnum<StatType>(rest.Substring(0, sign).Trim(), "능력치");
            char op = rest[sign];

            string number = rest.Substring(sign + 1).Trim();
            bool percent = number.EndsWith("%");
            if (percent) number = number.Substring(0, number.Length - 1).Trim();
            if (op == '*' && !percent) throw new FormatException($"'{text}': 곱하기(*)는 %로 적습니다. 예: Damage*20%");

            string[] bounds = number.Split('~');
            if (bounds.Length > 2) throw new FormatException($"'{text}': 범위는 3~5처럼 하나만 적습니다.");
            int min = SheetValues.ParseNumber(bounds[0].Trim(), $"'{text}'의 수치");
            int max = bounds.Length == 2 ? SheetValues.ParseNumber(bounds[1].Trim(), $"'{text}'의 수치") : min;
            if (max < min) throw new FormatException($"'{text}': 범위의 앞 수치가 뒤보다 큽니다.");
            if (op == '-') (min, max) = (-max, -min);

            var kind = op == '*' ? ModifierKind.More : percent ? ModifierKind.Increased : ModifierKind.Flat;
            return new ModifierRange(stat, kind, min, max, tags);
        }

        // Several modifiers in one cell are separated by ';'. An empty cell is an empty list.
        public static List<ModifierRange> ParseList(string cell) =>
            (cell ?? "").Split(';')
                .Where(part => part.Trim().Length > 0)
                .Select(Parse)
                .ToList();

        public static string Format(ModifierRange range)
        {
            var text = new StringBuilder();
            if (range.Tags != SkillTag.None) text.Append(string.Join("&", TagsIn(range.Tags))).Append(':');
            text.Append(range.Stat);

            char op = range.Kind == ModifierKind.More ? '*' : range.Min < 0 ? '-' : '+';
            int low = op == '-' ? -range.Max : range.Min;
            int high = op == '-' ? -range.Min : range.Max;
            text.Append(op).Append(low);
            if (high != low) text.Append('~').Append(high);
            if (range.Kind != ModifierKind.Flat) text.Append('%');
            return text.ToString();
        }

        internal static IEnumerable<SkillTag> TagsIn(SkillTag tags) =>
            ((SkillTag[])Enum.GetValues(typeof(SkillTag))).Where(tag => tag != SkillTag.None && (tags & tag) == tag);
    }
}
