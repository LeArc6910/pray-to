using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class ItemAffix
    {
        public ItemAffix(AffixTier tier, Modifier modifier)
        {
            Tier = tier ?? throw new ArgumentNullException(nameof(tier));
            Modifier = modifier;
        }

        public AffixTier Tier { get; }
        public Affix Affix => Tier.Affix;
        public Modifier Modifier { get; }
    }

    public sealed class Item
    {
        readonly RuneDefinition[] _sockets;

        public Item(
            ItemBase itemBase,
            int level,
            Rarity rarity,
            IReadOnlyList<Modifier> implicits,
            IReadOnlyList<ItemAffix> affixes,
            int socketCount)
        {
            Base = itemBase ?? throw new ArgumentNullException(nameof(itemBase));
            if (socketCount < 0 || socketCount > itemBase.MaxSockets) throw new ArgumentOutOfRangeException(nameof(socketCount));
            Level = level;
            Rarity = rarity;
            Implicits = implicits ?? throw new ArgumentNullException(nameof(implicits));
            Affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));
            _sockets = new RuneDefinition[socketCount];
        }

        // How far an anvil has worked it (World.Refining.cs): each step adds UpgradePercent of its base stats.
        public const int MaxUpgrade = 5;
        public const int UpgradePercent = 10;

        // Given by the world when the item first appears there, so commands can name it. 0 until then.
        public int Id { get; internal set; }
        public ItemBase Base { get; }
        public int Level { get; }
        public Rarity Rarity { get; }
        public IReadOnlyList<Modifier> Implicits { get; }
        public IReadOnlyList<ItemAffix> Affixes { get; private set; }
        // +0 to +MaxUpgrade.
        public int Upgrade { get; internal set; }
        // One entry per socket; null means the socket is still empty.
        public IReadOnlyList<RuneDefinition> Sockets => _sockets;
        public Runeword Runeword { get; private set; }

        // Runes stay in for good. Filling the last socket may complete a runeword.
        public bool TryInsertRune(int socket, RuneDefinition rune, IReadOnlyList<Runeword> runewords)
        {
            if (rune == null) throw new ArgumentNullException(nameof(rune));
            if (socket < 0 || socket >= _sockets.Length || _sockets[socket] != null) return false;

            _sockets[socket] = rune;
            Runeword = runewords.FirstOrDefault(word => word.Matches(this));
            return true;
        }

        // New affixes from an anvil's reroll; the rest of the item stays as it was.
        internal void SetAffixes(IReadOnlyList<ItemAffix> affixes) => Affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));

        // Takes every rune out, which undoes a runeword; returns them.
        internal List<RuneDefinition> TakeRunes()
        {
            var runes = _sockets.Where(r => r != null).ToList();
            for (int i = 0; i < _sockets.Length; i++) _sockets[i] = null;
            Runeword = null;
            return runes;
        }

        // What the upgrade adds: UpgradePercent of each flat base stat per step, at least one.
        public IEnumerable<Modifier> UpgradeModifiers()
        {
            if (Upgrade <= 0) yield break;
            foreach (var modifier in Implicits)
            {
                if (modifier.Kind != ModifierKind.Flat || modifier.Value <= 0) continue;
                int bonus = Math.Max(1, (modifier.Value * UpgradePercent * Upgrade + 99) / 100);
                yield return new Modifier(modifier.Stat, ModifierKind.Flat, bonus, modifier.Tags);
            }
        }

        public IEnumerable<Modifier> Modifiers()
        {
            foreach (var modifier in Implicits) yield return modifier;
            foreach (var modifier in UpgradeModifiers()) yield return modifier;
            foreach (var affix in Affixes) yield return affix.Modifier;
            foreach (var rune in _sockets.Where(r => r != null))
            {
                foreach (var modifier in rune.ModifiersFor(Base.Category)) yield return modifier;
            }
            if (Runeword == null) yield break;
            foreach (var modifier in Runeword.Modifiers) yield return modifier;
        }
    }
}
