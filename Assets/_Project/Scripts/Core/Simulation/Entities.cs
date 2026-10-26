using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class Player
    {
        internal Player(int id, string name, Vec2 position, Equipment equipment)
        {
            Id = id;
            Name = name;
            Position = position;
            Equipment = equipment;
            Stats = PlayerStats.From(equipment);
            Health = Stats.MaxHealth;
        }

        public int Id { get; }
        public string Name { get; }
        public Vec2 Position { get; internal set; }
        public Vec2 Facing { get; internal set; } = new Vec2(0, -1);
        public float Health { get; internal set; }
        public bool IsDead => Health <= 0;
        public Equipment Equipment { get; }
        public PlayerStats Stats { get; private set; }
        // Goes up whenever the host puts the player somewhere the owning client did not: a refused move or a
        // respawn. The client snaps to the host's position when it sees a new value.
        public int Corrections { get; internal set; }
        public bool Attacking { get; internal set; }

        internal int AttackCooldown { get; set; }
        internal int RespawnTicks { get; set; }

        public Item Equip(Item item)
        {
            var previous = Equipment.Equip(item);
            float healthShare = Health / Stats.MaxHealth;
            Stats = PlayerStats.From(Equipment);
            if (!IsDead) Health = healthShare * Stats.MaxHealth;
            return previous;
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
        // A player swung or an enemy struck; Direction is where.
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
    }

    public readonly struct SimEvent
    {
        public SimEvent(SimEventType type, int source, int target = 0, int amount = 0, bool critical = false, Vec2 direction = default, int cause = 0)
        {
            Type = type;
            Source = source;
            Target = target;
            Amount = amount;
            Critical = critical;
            Direction = direction;
            Cause = cause;
        }

        public SimEventType Type { get; }
        public int Source { get; }
        public int Target { get; }
        public int Amount { get; }
        public bool Critical { get; }
        public Vec2 Direction { get; }
        // Which of the boss's attacks dealt a hit (its index + 1), for balancing tools. Not sent to clients.
        public int Cause { get; }
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

    public sealed class Projectile
    {
        internal Projectile(int id, int source, int cause, Vec2 position, Vec2 velocity, float radius, int damage, int ticks)
        {
            Id = id;
            Source = source;
            Cause = cause;
            Position = position;
            Velocity = velocity;
            Radius = radius;
            Damage = damage;
            RemainingTicks = ticks;
        }

        public int Id { get; }
        public int Source { get; }
        public int Cause { get; }
        public Vec2 Position { get; internal set; }
        // Tiles per second.
        public Vec2 Velocity { get; }
        public float Radius { get; }
        public int Damage { get; }
        internal int RemainingTicks { get; set; }
    }
}
