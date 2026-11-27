using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class Equipment
    {
        readonly Item[] _slots = new Item[Enum.GetValues(typeof(ItemSlot)).Length];

        // The weapon slot is the one in hand; Spare is the second weapon, carried and swapped to (Docs/Weapons.md,
        // 무기 두 벌).
        public Item this[ItemSlot slot] => _slots[(int)slot];

        public Item Spare { get; private set; }

        // Everything worn, the spare weapon last.
        public IEnumerable<Item> Items => Spare == null ? _slots.Where(item => item != null) : _slots.Where(item => item != null).Append(Spare);

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

        // A weapon into the spare slot; returns the one there before, or null.
        public Item EquipSpare(Item item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Base.Slot != ItemSlot.Weapon) throw new ArgumentException("Only a weapon goes in the spare slot.", nameof(item));
            var previous = Spare;
            Spare = item;
            return previous;
        }

        public Item UnequipSpare()
        {
            var item = Spare;
            Spare = null;
            return item;
        }

        // The weapon in hand and the spare change places (either may be empty).
        public void SwapWeapons()
        {
            var held = _slots[(int)ItemSlot.Weapon];
            _slots[(int)ItemSlot.Weapon] = Spare;
            Spare = held;
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
        // Skills cost mana (SkillDefinition.Mana); it comes back by itself, so much a second.
        public const float BaseMana = 100;
        public const float BaseManaRegen = 5;

        // What a class skill draws on when no weapon of its class's branch is worn (Docs/Weapons.md): the stronger
        // weapon's numbers, this share of its damage.
        public const float OffBranchShare = 0.8f;

        readonly StatSheet _sheet;
        readonly bool _armed;
        // 1, or OffBranchShare for the numbers a skill without its branch's weapon takes.
        readonly float _share;
        PlayerStats _weaker;

        PlayerStats(StatSheet sheet, WeaponKind kind, SkillTag weaponTags, SkillTag grantedTags, bool armed, float share = 1)
        {
            _sheet = sheet;
            _armed = armed;
            _share = share;
            Kind = kind ?? WeaponKind.Fists;
            Attack = Kind.Attack;
            GrantedTags = grantedTags;
            AttackTags = weaponTags | grantedTags;
            MaxHealth = (float)sheet.Get(StatType.MaxHealth, BaseHealth);
            HealthRegen = (float)sheet.Get(StatType.HealthRegen);
            MaxMana = Math.Max(1, (float)sheet.Get(StatType.MaxMana, BaseMana));
            ManaRegen = Math.Max(0, (float)sheet.Get(StatType.ManaRegen, BaseManaRegen));
            Armor = (float)sheet.Get(StatType.Armor);
            MoveSpeed = (float)sheet.Get(StatType.MoveSpeed, BaseMoveSpeed);
            Damage = DamageFor(AttackTags);
            AttacksPerSecond = (float)sheet.Get(StatType.AttackSpeed, BaseAttacksPerSecond * Kind.Speed, AttackTags);
            CritChance = CritChanceFor(AttackTags);
            CritDamage = CritDamageFor(AttackTags);
            // The sheets write item rarity as "+N%", an increase, so it is worked out on a base of 100 and given
            // back as the bonus over that: two +50% affixes make +100%.
            ItemRarity = (float)sheet.Get(StatType.ItemRarity, 100) - 100;
            MiningSpeed = (float)sheet.Get(StatType.MiningSpeed, 100) - 100;
            FireAdded = Added(StatType.FireDamage, SkillTag.Fire);
            ColdAdded = Added(StatType.ColdDamage, SkillTag.Cold);
            LightningAdded = Added(StatType.LightningDamage, SkillTag.Lightning);

            // The flat damage of the element, grown by whatever grows the basic attack and that element's attacks.
            float Added(StatType stat, SkillTag element)
            {
                double flat = sheet.Get(stat, 0, AttackTags | element);
                return flat <= 0 ? 0 : (float)(flat * sheet.Scale(StatType.Damage, AttackTags | element)) * share;
            }
        }

        // The elemental damage each basic attack adds (StatType.FireDamage and the others), before a crit.
        public float FireAdded { get; }
        public float ColdAdded { get; }
        public float LightningAdded { get; }

        public float AddedFor(SkillTag element) => element switch
        {
            SkillTag.Fire => FireAdded,
            SkillTag.Cold => ColdAdded,
            SkillTag.Lightning => LightningAdded,
            _ => 0,
        };

        // The weapon in hand's kind (fists when empty) and what its basic attack does.
        public WeaponKind Kind { get; }
        public BasicAttack Attack { get; }
        // The numbers as though the spare weapon were in hand (null without one): a skill of the spare's branch draws
        // on these (ForSkill).
        public PlayerStats Spare { get; private set; }
        // Tags of the basic attack: the weapon's, or melee when bare-handed, plus whatever buffs grant.
        public SkillTag AttackTags { get; }
        // Tags buffs add to every attack and skill.
        public SkillTag GrantedTags { get; }
        // What the worn gear and its sets do besides their stats (Docs/Gear.md).
        public GearEffects Effects { get; private set; } = GearEffects.None;
        public float MaxHealth { get; }
        // Per second.
        public float HealthRegen { get; }
        public float MaxMana { get; }
        // Mana a second.
        public float ManaRegen { get; }
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
        // Percent more wall each hit digs away, written like item rarity.
        public float MiningSpeed { get; }

        // The tags a skill hits with: its own and whatever buffs grant.
        public SkillTag ContextFor(SkillDefinition skill) => skill.Tags | GrantedTags;

        // Damage, crit chance and crit damage for an attack or skill with these tags.
        public float DamageFor(SkillTag context) => (float)_sheet.Get(StatType.Damage, _armed ? 0 : 1, context) * _share;

        // The numbers a class's skill draws on (Docs/Weapons.md, 스킬이 힘을 받는 규칙): the worn weapon of the class's
        // branch (ClassDefinition.Weapon), the one in hand first; with neither, the stronger of the two at
        // OffBranchShare. A skill of no class, or a class of no branch, takes the weapon in hand.
        public PlayerStats ForSkill(SkillDefinition skill) => ForBranch(skill?.Class?.Weapon ?? SkillTag.None, skill == null ? SkillTag.None : ContextFor(skill));

        public PlayerStats ForBranch(SkillTag branch, SkillTag context = SkillTag.None)
        {
            if (branch == SkillTag.None || Kind.Branch == branch) return this;
            if (Spare != null && Spare.Kind.Branch == branch) return Spare;
            var stronger = Spare != null && Spare.DamageFor(context) > DamageFor(context) ? Spare : this;
            return stronger._weaker ??= new PlayerStats(stronger._sheet, stronger.Kind, stronger.AttackTags & ~stronger.GrantedTags, stronger.GrantedTags,
                stronger._armed, OffBranchShare) { Effects = stronger.Effects };
        }

        // Whether a class of this branch has a weapon of it worn (else its skills draw at OffBranchShare).
        public bool Wields(SkillTag branch) => branch == SkillTag.None || Kind.Branch == branch || Spare?.Kind.Branch == branch;

        public float CritChanceFor(SkillTag context) => (float)_sheet.Get(StatType.CritChance, BaseCritChance, context);

        public float CritDamageFor(SkillTag context) => (float)_sheet.Get(StatType.CritDamage, BaseCritDamage, context);

        // What the passive tree and gear add to a skill with these tags: ranks over the ones learned, percent faster
        // cooldowns and percent longer buffs and spirits (SkillRules).
        public int SkillRankFor(SkillTag tags) => (int)Math.Round(_sheet.Get(StatType.SkillRank, 0, tags));

        public float CooldownRecoveryFor(SkillTag tags) => Math.Max(0, (float)_sheet.Get(StatType.CooldownRecovery, 100, tags) - 100);

        public float SkillDurationFor(SkillTag tags) => Math.Max(0, (float)_sheet.Get(StatType.SkillDuration, 100, tags) - 100);

        public static PlayerStats From(
            Equipment equipment,
            IEnumerable<ClassDefinition> classes = null,
            IEnumerable<SkillDefinition> buffs = null,
            ConsumableDefinition food = null,
            int level = 1,
            IEnumerable<PassiveNode> passives = null,
            IEnumerable<Modifier> pet = null,
            IEnumerable<Modifier> blessing = null)
        {
            var common = new List<Modifier>();
            if (pet != null) common.AddRange(pet);
            // A shrine's blessing (World.Pois.cs).
            if (blessing != null) common.AddRange(blessing);
            foreach (var point in passives ?? Enumerable.Empty<PassiveNode>()) common.AddRange(point.Modifiers);
            if (level > 1)
            {
                common.Add(new Modifier(StatType.MaxHealth, ModifierKind.Flat, Leveling.HealthAt(level)));
                common.Add(new Modifier(StatType.Damage, ModifierKind.More, Leveling.DamageAt(level)));
            }
            var picked = (classes ?? Enumerable.Empty<ClassDefinition>()).ToList();
            var effects = new List<GearEffect>();
            var held = equipment[ItemSlot.Weapon];
            var spare = equipment.Spare;
            foreach (var item in equipment.Items)
            {
                effects.AddRange(item.Base.Effects);
                if (item == held || item == spare) continue;
                common.AddRange(item.Modifiers());
                // A piece made for one class gives that class more (class, class_bonus).
                if (item.Base.ClassBonus.Count > 0 && picked.Any(c => c.Id == item.Base.ClassId)) common.AddRange(item.Base.ClassBonus);
            }
            // Sets: what so many pieces worn together give, and the more a set's class gets. Both weapons count.
            foreach (var worn in equipment.Items.Where(i => i.Base.Set != null).GroupBy(i => i.Base.Set))
            {
                foreach (var bonus in worn.Key.ActiveBonuses(worn.Count()))
                {
                    common.AddRange(bonus.Modifiers);
                    effects.AddRange(bonus.Effects);
                    if (bonus.ClassBonus.Count > 0 && picked.Any(c => c.Id == bonus.ClassId)) common.AddRange(bonus.ClassBonus);
                }
            }
            if (food != null) common.AddRange(food.Buff);
            float share = ClassRules.PassiveShare(picked.Count);
            foreach (var chosen in picked)
            {
                common.AddRange(share >= 1 ? chosen.Passive
                    : chosen.Passive.Select(m => new Modifier(m.Stat, m.Kind, (int)Math.Round(m.Value * share), m.Tags)));
            }
            var granted = SkillTag.None;
            foreach (var buff in buffs ?? Enumerable.Empty<SkillDefinition>())
            {
                common.AddRange(buff.Modifiers);
                granted |= buff.Grants;
            }
            var gear = effects.Count > 0 ? new GearEffects(effects) : GearEffects.None;
            var stats = Make(held, spare);
            if (spare != null) stats.Spare = Make(spare, held);
            return stats;

            // The numbers with one weapon in hand: all of its stats, and of the other's only those that are not its
            // attack's (Docs/Weapons.md, 무기 두 벌).
            PlayerStats Make(Item hand, Item other)
            {
                var sheet = new StatSheet();
                sheet.Add(common);
                sheet.Add(WeaponStats(hand, attack: true));
                sheet.Add(WeaponStats(other, attack: false));
                return new PlayerStats(sheet, hand?.Base.Kind ?? WeaponKind.Fists, hand?.Base.Tags ?? SkillTag.Melee, granted, hand != null) { Effects = gear };
            }

            IEnumerable<Modifier> WeaponStats(Item weapon, bool attack)
            {
                if (weapon == null) return Enumerable.Empty<Modifier>();
                var all = weapon.Modifiers().AsEnumerable();
                if (weapon.Base.ClassBonus.Count > 0 && picked.Any(c => c.Id == weapon.Base.ClassId)) all = all.Concat(weapon.Base.ClassBonus);
                return attack ? all : all.Where(m => !IsAttackStat(m.Stat));
            }
        }

        // The stats a weapon gives only to the attacks made with it (and to the skills that draw on it).
        public static bool IsAttackStat(StatType stat) => stat switch
        {
            StatType.Damage or StatType.AttackSpeed or StatType.CritChance or StatType.CritDamage or StatType.MiningSpeed
                or StatType.FireDamage or StatType.ColdDamage or StatType.LightningDamage => true,
            _ => false,
        };
    }

    public enum BasicAttackKind : byte
    {
        Swing,
        Arrow,
        Bolt,
    }

    // A weapon's basic attack (Docs/Weapons.md): its kind's (weapon_kinds.csv) shape and numbers. Melee (and bare
    // hands) swings, ranged shoots arrows, magic shoots bolts. A swing hits everything in its arc; a shot flies straight,
    // hits the first enemy it touches (or goes through Pierce more), may come back to the thrower (Returns) and may
    // burst there (Burst), and stops at walls. See Docs/Combat.md.
    public sealed class BasicAttack
    {
        public static readonly BasicAttack Swing = new BasicAttack(BasicAttackKind.Swing, "휘두르기");
        public static readonly BasicAttack Arrow = new BasicAttack(BasicAttackKind.Arrow, "화살", speed: 16, range: 8, radius: 0.12f);
        public static readonly BasicAttack Bolt = new BasicAttack(BasicAttackKind.Bolt, "마법 탄", speed: 11, range: 7, radius: 0.2f);

        internal BasicAttack(BasicAttackKind kind, string name, float speed = 0, float range = 0, float radius = 0, float reach = World.MeleeReach,
            float arc = World.MeleeArcDegrees, float push = 1, int pierce = 0, bool returns = false, float burst = 0, float stun = 0)
        {
            Kind = kind;
            Name = name;
            Speed = speed;
            Range = range;
            Radius = radius;
            Reach = reach;
            Arc = arc;
            Push = push;
            Pierce = pierce;
            Returns = returns;
            Burst = burst;
            Stun = stun;
        }

        public BasicAttackKind Kind { get; }
        public string Name { get; }
        // For shots: tiles per second, how far they fly, and how thick they are.
        public float Speed { get; }
        public float Range { get; }
        public float Radius { get; }
        // For swings: how far past the player's edge and how wide (degrees), and how far it pushes (1 the usual).
        public float Reach { get; }
        public float Arc { get; }
        public float Push { get; }
        // For shots: how many monsters it goes on through, whether it comes back to the thrower (striking again on the
        // way), and how wide it bursts where it stops (0: only what it touches).
        public int Pierce { get; }
        public bool Returns { get; }
        public float Burst { get; }
        // Seconds the finisher dazes a monster it hits (0: none).
        public float Stun { get; }
        public bool Shoots => Kind != BasicAttackKind.Swing;

        public static BasicAttack For(SkillTag weaponTags) =>
            (weaponTags & SkillTag.Melee) != 0 ? Swing
            : (weaponTags & SkillTag.Ranged) != 0 ? Arrow
            : (weaponTags & SkillTag.Magic) != 0 ? Bolt
            : Swing;
    }
}
