using System;
using System.Collections.Generic;

namespace Game.Core
{
    // Adds up modifiers from gear, runes, class passives and buffs:
    // final = (base + flat) × (1 + increased%) × each (1 + more%).
    public sealed class StatSheet
    {
        readonly List<Modifier> _modifiers = new List<Modifier>();

        public IReadOnlyList<Modifier> Modifiers => _modifiers;

        public void Add(Modifier modifier) => _modifiers.Add(modifier);

        public void Add(IEnumerable<Modifier> modifiers) => _modifiers.AddRange(modifiers);

        // Context is the tags of whatever is being measured, e.g. Magic | Fire for a fire spell.
        public double Get(StatType stat, double baseValue = 0, SkillTag context = SkillTag.None)
        {
            int flat = 0;
            int increased = 0;
            double more = 1;
            foreach (var modifier in _modifiers)
            {
                if (modifier.Stat != stat || !modifier.AppliesTo(context)) continue;
                switch (modifier.Kind)
                {
                    case ModifierKind.Flat:
                        flat += modifier.Value;
                        break;
                    case ModifierKind.Increased:
                        increased += modifier.Value;
                        break;
                    case ModifierKind.More:
                        more *= 1 + modifier.Value / 100.0;
                        break;
                }
            }
            return (baseValue + flat) * Math.Max(0, 1 + increased / 100.0) * more;
        }
    }
}
