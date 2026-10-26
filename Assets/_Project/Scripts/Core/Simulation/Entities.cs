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
    }

    public readonly struct SimEvent
    {
        public SimEvent(SimEventType type, int source, int target = 0, int amount = 0, bool critical = false, Vec2 direction = default)
        {
            Type = type;
            Source = source;
            Target = target;
            Amount = amount;
            Critical = critical;
            Direction = direction;
        }

        public SimEventType Type { get; }
        public int Source { get; }
        public int Target { get; }
        public int Amount { get; }
        public bool Critical { get; }
        public Vec2 Direction { get; }
    }
}
