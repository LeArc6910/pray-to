using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class Equipment
    {
        readonly Item[] _slots = new Item[Enum.GetValues(typeof(ItemSlot)).Length];

        public Item this[ItemSlot slot] => _slots[(int)slot];

        public IEnumerable<Item> Items => _slots.Where(item => item != null);

        public Item Unequip(ItemSlot slot)
        {
            var item = _slots[(int)slot];
            _slots[(int)slot] = null;
            return item;
        }

        // Returns whatever the slot held before, or null.
        public Item Equip(Item item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            int slot = (int)item.Base.Slot;
            var previous = _slots[slot];
            _slots[slot] = item;
            return previous;
        }
    }

    // The numbers combat uses, worked out from the gear, the chosen classes' passives and the buffs running, once
    // whenever any of them changes.
    public sealed class PlayerStats
    {
        public const float BaseHealth = 100;
        public const float BaseMoveSpeed = 5;
        public const float BaseAttacksPerSecond = 2;
        public const float BaseCritChance = 5;
        public const float BaseCritDamage = 150;

        readonly StatSheet _sheet;
        readonly bool _armed;

        PlayerStats(StatSheet sheet, SkillTag weaponTags, SkillTag grantedTags, bool armed)
        {
            _sheet = sheet;
            _armed = armed;
            GrantedTags = grantedTags;
            AttackTags = weaponTags | grantedTags;
            MaxHealth = (float)sheet.Get(StatType.MaxHealth, BaseHealth);
            HealthRegen = (float)sheet.Get(StatType.HealthRegen);
            Armor = (float)sheet.Get(StatType.Armor);
            MoveSpeed = (float)sheet.Get(StatType.MoveSpeed, BaseMoveSpeed);
            Damage = DamageFor(AttackTags);
            AttacksPerSecond = (float)sheet.Get(StatType.AttackSpeed, BaseAttacksPerSecond, AttackTags);
            CritChance = CritChanceFor(AttackTags);
            CritDamage = CritDamageFor(AttackTags);
            // The sheets write item rarity as "+N%", an increase, so it is worked out on a base of 100 and given
            // back as the bonus over that: two +50% affixes make +100%.
            ItemRarity = (float)sheet.Get(StatType.ItemRarity, 100) - 100;
        }

        // Tags of the basic attack: the weapon's, or melee when bare-handed, plus whatever buffs grant.
        public SkillTag AttackTags { get; }
        // Tags buffs add to every attack and skill.
        public SkillTag GrantedTags { get; }
        public float MaxHealth { get; }
        // Per second.
        public float HealthRegen { get; }
        public float Armor { get; }
        // Tiles per second.
        public float MoveSpeed { get; }
        // Of the basic attack. Fists hit for 1 before any gear.
        public float Damage { get; }
        public float AttacksPerSecond { get; }
        // Percent.
        public float CritChance { get; }
        // Percent of normal damage a critical hit deals.
        public float CritDamage { get; }
        // Percent bonus to the odds of better drops from what this player kills.
        public float ItemRarity { get; }

        // The tags a skill hits with: its own and whatever buffs grant.
        public SkillTag ContextFor(SkillDefinition skill) => skill.Tags | GrantedTags;

        // Damage, crit chance and crit damage for an attack or skill with these tags.
        public float DamageFor(SkillTag context) => (float)_sheet.Get(StatType.Damage, _armed ? 0 : 1, context);

        public float CritChanceFor(SkillTag context) => (float)_sheet.Get(StatType.CritChance, BaseCritChance, context);

        public float CritDamageFor(SkillTag context) => (float)_sheet.Get(StatType.CritDamage, BaseCritDamage, context);

        public static PlayerStats From(Equipment equipment, IEnumerable<ClassDefinition> classes = null, IEnumerable<SkillDefinition> buffs = null)
        {
            var sheet = new StatSheet();
            foreach (var item in equipment.Items) sheet.Add(item.Modifiers());
            foreach (var chosen in classes ?? Enumerable.Empty<ClassDefinition>()) sheet.Add(chosen.Passive);
            var granted = SkillTag.None;
            foreach (var buff in buffs ?? Enumerable.Empty<SkillDefinition>())
            {
                sheet.Add(buff.Modifiers);
                granted |= buff.Grants;
            }
            var weapon = equipment[ItemSlot.Weapon];
            return new PlayerStats(sheet, weapon?.Base.Tags ?? SkillTag.Melee, granted, weapon != null);
        }
    }
}
