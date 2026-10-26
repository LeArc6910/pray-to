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

    // The numbers combat uses, worked out from the gear once whenever it changes.
    public sealed class PlayerStats
    {
        public const float BaseHealth = 100;
        public const float BaseMoveSpeed = 5;
        public const float BaseAttacksPerSecond = 2;
        public const float BaseCritChance = 5;
        public const float BaseCritDamage = 150;

        PlayerStats(StatSheet sheet, SkillTag attackTags, bool armed)
        {
            AttackTags = attackTags;
            MaxHealth = (float)sheet.Get(StatType.MaxHealth, BaseHealth);
            HealthRegen = (float)sheet.Get(StatType.HealthRegen);
            Armor = (float)sheet.Get(StatType.Armor);
            MoveSpeed = (float)sheet.Get(StatType.MoveSpeed, BaseMoveSpeed);
            Damage = (float)sheet.Get(StatType.Damage, armed ? 0 : 1, attackTags);
            AttacksPerSecond = (float)sheet.Get(StatType.AttackSpeed, BaseAttacksPerSecond, attackTags);
            CritChance = (float)sheet.Get(StatType.CritChance, BaseCritChance, attackTags);
            CritDamage = (float)sheet.Get(StatType.CritDamage, BaseCritDamage, attackTags);
        }

        // Tags of the basic attack: the weapon's, or melee when bare-handed.
        public SkillTag AttackTags { get; }
        public float MaxHealth { get; }
        // Per second.
        public float HealthRegen { get; }
        public float Armor { get; }
        // Tiles per second.
        public float MoveSpeed { get; }
        // Fists hit for 1 before any gear.
        public float Damage { get; }
        public float AttacksPerSecond { get; }
        // Percent.
        public float CritChance { get; }
        // Percent of normal damage a critical hit deals.
        public float CritDamage { get; }

        public static PlayerStats From(Equipment equipment)
        {
            var sheet = new StatSheet();
            foreach (var item in equipment.Items) sheet.Add(item.Modifiers());
            var weapon = equipment[ItemSlot.Weapon];
            return new PlayerStats(sheet, weapon?.Base.Tags ?? SkillTag.Melee, weapon != null);
        }
    }
}
