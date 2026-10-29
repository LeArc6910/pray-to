using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class Player
    {
        readonly int[] _cooldowns;
        readonly int[] _buffTicks;

        internal Player(int id, string name, Vec2 position, Equipment equipment, IReadOnlyList<ClassDefinition> classes, Inventory bag = null)
        {
            Id = id;
            Name = name;
            Position = position;
            Equipment = equipment;
            Inventory = bag ?? new Inventory();
            Classes = classes;
            Skills = ClassRules.SkillsFor(classes);
            _cooldowns = new int[Skills.Count];
            _buffTicks = new int[Skills.Count];
            Stats = PlayerStats.From(equipment, classes);
            Health = Stats.MaxHealth;
        }

        public int Id { get; }
        public string Name { get; }
        public Vec2 Position { get; internal set; }
        public Vec2 Facing { get; internal set; } = new Vec2(0, -1);
        public float Health { get; internal set; }
        public bool IsDead => Health <= 0;
        public Equipment Equipment { get; }
        public Inventory Inventory { get; }
        // Goes up whenever the bag or the equipment changes, so the host knows when to send them again.
        public int InventoryVersion { get; internal set; }
        public PlayerStats Stats { get; private set; }
        // Goes up whenever the host puts the player somewhere the owning client did not: a refused move or a
        // respawn. The client snaps to the host's position when it sees a new value.
        public int Corrections { get; internal set; }
        public bool Attacking { get; internal set; }
        public IReadOnlyList<ClassDefinition> Classes { get; }
        // One per skill slot, in slot order.
        public IReadOnlyList<SkillDefinition> Skills { get; }

        // Ticks until any consumable can be used again.
        public int ConsumableCooldown { get; internal set; }

        internal int AttackCooldown { get; set; }
        internal int RespawnTicks { get; set; }
        // Health still to come from a consumable: so much each tick, for so many ticks.
        internal float HealPerTick { get; set; }
        internal int HealTicks { get; set; }
        // Counts up while the player is not hurt, to when resting starts to heal.
        internal int RestingTicks { get; set; }

        // Ticks until the skill in the slot can be used again, and until the buff it put on runs out.
        public int CooldownTicks(int slot) => _cooldowns[slot];
        public int BuffTicks(int slot) => _buffTicks[slot];
        public bool HasBuff(int slot) => _buffTicks[slot] > 0;

        public Item Equip(Item item)
        {
            var previous = Equipment.Equip(item);
            RefreshStats();
            InventoryVersion++;
            return previous;
        }

        public Item Unequip(ItemSlot slot)
        {
            var item = Equipment.Unequip(slot);
            RefreshStats();
            InventoryVersion++;
            return item;
        }

        internal void StartCooldown(int slot) => _cooldowns[slot] = Skills[slot].CooldownTicks;

        // Using a buff again starts its time over.
        internal void StartBuff(int slot)
        {
            bool wasOn = HasBuff(slot);
            _buffTicks[slot] = Skills[slot].DurationTicks;
            if (!wasOn) RefreshStats();
        }

        // One tick of cooldowns and buffs.
        internal void TickSkills()
        {
            bool expired = false;
            for (int slot = 0; slot < Skills.Count; slot++)
            {
                if (_cooldowns[slot] > 0) _cooldowns[slot]--;
                if (_buffTicks[slot] > 0 && --_buffTicks[slot] == 0) expired = true;
            }
            if (expired) RefreshStats();
        }

        internal void ClearBuffs()
        {
            if (Array.TrueForAll(_buffTicks, ticks => ticks == 0)) return;
            Array.Clear(_buffTicks, 0, _buffTicks.Length);
            RefreshStats();
        }

        // Works the numbers out again after the gear or buffs changed, keeping the same share of health.
        internal void RefreshStats()
        {
            float healthShare = Health / Stats.MaxHealth;
            var buffs = new List<SkillDefinition>();
            for (int slot = 0; slot < Skills.Count; slot++)
            {
                if (HasBuff(slot)) buffs.Add(Skills[slot]);
            }
            Stats = PlayerStats.From(Equipment, Classes, buffs);
            if (!IsDead) Health = healthShare * Stats.MaxHealth;
        }
    }

    public enum EnemyState : byte
    {
        Idle,
        Chase,
        // Telegraphing an attack. The hit lands when this ends, if the target is still in reach.
        Windup,
        Recover,
    }

    public sealed class Enemy
    {
        internal Enemy(int id, MonsterDefinition definition, Vec2 position)
        {
            Id = id;
            Definition = definition;
            Position = position;
            Health = definition.Health;
        }

        public int Id { get; }
        public MonsterDefinition Definition { get; }
        public Vec2 Position { get; internal set; }
        public Vec2 Facing { get; internal set; } = new Vec2(0, -1);
        public float Health { get; internal set; }
        public bool IsDead => Health <= 0;
        public EnemyState State { get; internal set; }
        public int StateTicks { get; internal set; }
        public int TargetId { get; internal set; }

        internal Vec2 Knockback { get; set; }
    }

    public enum SimEventType : byte
    {
        // A player attacked or an enemy struck; Direction is where. For a player, Amount is the BasicAttackKind.
        Attack,
        // Source hit Target for Amount.
        Hit,
        Died,
        // Something heavy landed (a boss's area attack); Amount is how hard, for shaking the screen.
        Impact,
        // Source, a boss, woke up because a player walked into its arena.
        BossAwake,
        // Source, a boss, entered phase Amount.
        PhaseChanged,
        BossDefeated,
        // Source, a player, picked up Target, a piece of ground loot.
        Pickup,
        // Source, a player, used the skill in slot Amount; Direction is where they aimed.
        SkillUsed,
        // The skill in slot Amount of Source, a player, burst at Position.
        Burst,
        // Source, a player, drank or ate something that heals Amount over the next moments.
        Healed,
        // Source, a player, struck the wall at Position; Amount is 1 when it broke.
        Dug,
        // Source, a player, harvested Amount from the crop at Position.
        Harvested,
    }

    public readonly struct SimEvent
    {
        public SimEvent(SimEventType type, int source, int target = 0, int amount = 0, bool critical = false, Vec2 direction = default, int cause = 0, Vec2 position = default)
        {
            Type = type;
            Source = source;
            Target = target;
            Amount = amount;
            Critical = critical;
            Direction = direction;
            Cause = cause;
            Position = position;
        }

        public SimEventType Type { get; }
        public int Source { get; }
        public int Target { get; }
        public int Amount { get; }
        public bool Critical { get; }
        public Vec2 Direction { get; }
        // What dealt a hit, for balancing tools; not sent to clients. From a boss: its attack's index + 1 (0 for a
        // roar). From a player: the skill slot + 1, or 0 for the basic attack.
        public int Cause { get; }
        // Only for events that happen somewhere rather than to someone (HasPosition).
        public Vec2 Position { get; }

        public static bool HasPosition(SimEventType type) =>
            type == SimEventType.Burst || type == SimEventType.Dug || type == SimEventType.Harvested;
    }

    public enum BossState : byte
    {
        // Asleep in the middle of its arena until a player walks in.
        Dormant,
        // Moving into range for the attack it has picked.
        Approach,
        // Showing where the attack will land.
        Telegraph,
        Charge,
        Recover,
        // Between phases: unhurt, then a blast around itself.
        Roar,
    }

    public sealed class Boss
    {
        internal Boss(int id, BossDefinition definition, Vec2 home, float arenaRadius)
        {
            Id = id;
            Definition = definition;
            Home = home;
            ArenaRadius = arenaRadius;
            Position = home;
            Health = definition.Health;
            MaxHealth = definition.Health;
        }

        public int Id { get; }
        public BossDefinition Definition { get; }
        public Vec2 Home { get; }
        public float ArenaRadius { get; }
        public Vec2 Position { get; internal set; }
        public Vec2 Facing { get; internal set; } = new Vec2(0, -1);
        public float Health { get; internal set; }
        // Set when the boss wakes, from how many players are in the game.
        public float MaxHealth { get; internal set; }
        public int Phase { get; internal set; } = 1;
        public BossState State { get; internal set; }
        public int StateTicks { get; internal set; }
        public int TargetId { get; internal set; }
        public BossAttack Attack { get; internal set; }
        public bool IsDead => Health <= 0;
        public bool Invulnerable => State == BossState.Roar;

        internal Vec2 ChargeDirection { get; set; }
        internal HashSet<int> ChargeHits { get; } = new HashSet<int>();
        // Ticks with nobody left to fight; the boss resets after a while.
        internal int EmptyTicks { get; set; }
    }

    public enum HazardShape : byte
    {
        Circle,
        Cone,
        Line,
    }

    // A marked area that hurts every player inside it when ResolveTick comes. Zero damage marks a charge's path.
    public sealed class Hazard
    {
        public Hazard(int id, int source, int cause, HazardShape shape, Vec2 position, Vec2 direction, float size, float width, int damage, long startTick, long resolveTick)
        {
            Id = id;
            Source = source;
            Cause = cause;
            Shape = shape;
            Position = position;
            Direction = direction;
            Size = size;
            Width = width;
            Damage = damage;
            StartTick = startTick;
            ResolveTick = resolveTick;
        }

        public int Id { get; }
        public int Source { get; }
        public int Cause { get; }
        public HazardShape Shape { get; }
        // Centre of a circle, or where a cone or line starts.
        public Vec2 Position { get; }
        public Vec2 Direction { get; }
        // Circle radius, or cone and line length.
        public float Size { get; }
        // Cone angle in degrees, or line width.
        public float Width { get; }
        public int Damage { get; }
        public long StartTick { get; }
        public long ResolveTick { get; }

        // Whether a circle of the given radius at point touches the area.
        public bool Contains(Vec2 point, float radius)
        {
            var offset = point - Position;
            float distance = offset.Length;
            switch (Shape)
            {
                case HazardShape.Circle:
                    return distance <= Size + radius;
                case HazardShape.Cone:
                    if (distance > Size + radius) return false;
                    return distance < 1e-4f || Vec2.Dot(offset / distance, Direction) >= MathF.Cos(Width * 0.5f * MathF.PI / 180f);
                default:
                    float along = Vec2.Dot(offset, Direction);
                    float across = MathF.Abs(offset.X * Direction.Y - offset.Y * Direction.X);
                    return along >= -radius && along <= Size + radius && across <= Width / 2 + radius;
            }
        }
    }

    // An item or a rune lying on the floor. Players pick it up by walking over it once ReadyTick has passed.
    public sealed class GroundLoot
    {
        internal GroundLoot(
            int id,
            Vec2 position,
            Item item,
            RuneDefinition rune,
            long readyTick,
            long expireTick,
            ConsumableDefinition consumable = null,
            MaterialDefinition material = null)
        {
            Id = id;
            Position = position;
            Item = item;
            Rune = rune;
            Consumable = consumable;
            Material = material;
            ReadyTick = readyTick;
            ExpireTick = expireTick;
        }

        public int Id { get; }
        public Vec2 Position { get; }
        // Exactly one of these is set.
        public Item Item { get; }
        public RuneDefinition Rune { get; }
        public ConsumableDefinition Consumable { get; }
        public MaterialDefinition Material { get; }
        public long ReadyTick { get; }
        public long ExpireTick { get; }
    }

    // A boss's projectile hurts players. A player's (Friendly) flies through players and stops at the first enemy;
    // a skill's bursts there.
    public sealed class Projectile
    {
        internal Projectile(
            int id,
            int source,
            int cause,
            Vec2 position,
            Vec2 velocity,
            float radius,
            float damage,
            int ticks,
            bool friendly = false,
            SkillTag tags = SkillTag.None,
            SkillDefinition skill = null,
            bool critical = false)
        {
            Id = id;
            Source = source;
            Cause = cause;
            Position = position;
            Velocity = velocity;
            Radius = radius;
            Damage = damage;
            RemainingTicks = ticks;
            Friendly = friendly;
            Tags = tags;
            Skill = skill;
            Critical = critical;
        }

        public int Id { get; }
        public int Source { get; }
        // Which of the boss's attacks fired it (its index + 1), or which of the player's skill slots (-1 for a basic
        // attack).
        public int Cause { get; }
        public Vec2 Position { get; internal set; }
        // Tiles per second.
        public Vec2 Velocity { get; }
        public float Radius { get; }
        // Before armor. A player's projectile rolls its damage and crit when it is fired.
        public float Damage { get; }
        public bool Friendly { get; }
        // A player's: the tags it hits with, buffs included.
        public SkillTag Tags { get; }
        // The skill that fired it, or null for a basic attack or a boss's.
        public SkillDefinition Skill { get; }
        public bool Critical { get; }
        internal int RemainingTicks { get; set; }
    }
}
