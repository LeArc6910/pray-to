using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class Player
    {
        int[] _cooldowns;
        int[] _buffTicks;

        internal Player(int id, string name, Vec2 position, Equipment equipment, IReadOnlyList<ClassDefinition> classes, Inventory bag = null)
        {
            Id = id;
            Name = name;
            Position = position;
            Equipment = equipment;
            Inventory = bag ?? new Inventory();
            Classes = classes;
            LearnBasics(classes);
            Skills = ClassRules.SkillsFor(classes, Knows);
            var natural = ClassRules.AllSkillsFor(classes);
            SkillOrder = Skills.Select(s => natural.IndexOf(s)).ToList();
            _cooldowns = new int[Skills.Count];
            _buffTicks = new int[Skills.Count];
            Stats = PlayerStats.From(equipment, classes);
            Health = Stats.MaxHealth;
            Mana = Stats.MaxMana;
        }

        public int Id { get; }
        public string Name { get; }
        public Vec2 Position { get; internal set; }
        public Vec2 Facing { get; internal set; } = new Vec2(0, -1);
        public float Health { get; internal set; }
        public bool IsDead => Health <= 0;
        // What is left for skills (SkillDefinition.Mana), up to Stats.MaxMana.
        public float Mana { get; internal set; }
        public Equipment Equipment { get; }
        public Inventory Inventory { get; }
        // Goes up whenever the bag or the equipment changes, so the host knows when to send them again.
        public int InventoryVersion { get; internal set; }
        public PlayerStats Stats { get; private set; }
        // The guide goals whose rewards the character has had (World.ClaimGoal), by goal id. Kept in its save.
        public HashSet<string> ClaimedGoals { get; } = new HashSet<string>();
        // The line in the water, or null when not fishing (World.Fish).
        public FishingLine Fishing { get; internal set; }
        // Experience gathered over the character's life; the level follows from it (Leveling).
        public int Experience { get; internal set; }
        public int Level => Leveling.LevelFor(Experience);
        // The points taken on the passive tree (World.Passives.cs), not counting the start.
        public List<PassiveNode> Passives { get; } = new List<PassiveNode>();
        // Experience in each life skill (LifeSkill order), and a count that goes up when any changes.
        public int[] LifeExperience { get; } = new int[LifeSkills.Count];
        public int LifeVersion { get; internal set; }
        // How well they know each person, by id (World.Merchant.cs).
        public Dictionary<string, Friendship> Friendships { get; } = new Dictionary<string, Friendship>();
        // One point for every level past the first.
        public int PassivePoints => Math.Max(0, Level - 1 - Passives.Count);
        // Goes up whenever the host puts the player somewhere the owning client did not: a refused move or a
        // respawn. The client snaps to the host's position when it sees a new value.
        public int Corrections { get; internal set; }
        public bool Attacking { get; internal set; }
        public IReadOnlyList<ClassDefinition> Classes { get; private set; }
        // One per skill slot, in slot order.
        public IReadOnlyList<SkillDefinition> Skills { get; private set; }
        // Which of the classes' skills (ClassRules.SkillsFor) is in each slot.
        public IReadOnlyList<int> SkillOrder { get; private set; }

        // Skill ranks bought with skill points (Docs/Classes.md, 스킬 포인트와 단계): 1 to SkillRules.MaxRank for a
        // known skill. They stay with the character when its classes change.
        public Dictionary<SkillDefinition, int> SkillRanks { get; } = new Dictionary<SkillDefinition, int>();

        public int RankOf(SkillDefinition skill) => skill != null && SkillRanks.TryGetValue(skill, out int rank) ? rank : 0;

        // The rank the skill works at: learned and added by the stats (SkillRules.Power).
        public int PowerOf(SkillDefinition skill) => SkillRules.Power(RankOf(skill), Stats, skill);

        internal int CooldownFor(SkillDefinition skill) => SkillRules.CooldownTicks(skill, PowerOf(skill), Stats.CooldownRecoveryFor(skill.Tags));

        internal int DurationFor(SkillDefinition skill) => SkillRules.DurationTicks(skill, PowerOf(skill), Stats.SkillDurationFor(skill.Tags));

        public bool Knows(SkillDefinition skill) => RankOf(skill) > 0;

        // One a level past the first; a class's first skill's first rank is free.
        public int SkillPoints => SkillRules.PointsLeft(Level, SkillRanks);

        // Each chosen class's first skill is known from the start.
        internal void LearnBasics(IEnumerable<ClassDefinition> classes)
        {
            foreach (var basic in classes.Where(c => c.Skills.Count > 0).Select(c => c.Skills[0]))
            {
                if (!Knows(basic)) SkillRanks[basic] = 1;
            }
        }

        // New classes, or the same in a new order (ClassRules.SkillsFor). A skill that stays keeps its cooldown and
        // buff; a new one starts as if just used, so changing back and forth never skips a cooldown. Only known
        // skills go in the slots.
        internal void ChangeKit(IReadOnlyList<ClassDefinition> classes, IReadOnlyList<int> order)
        {
            LearnBasics(classes);
            var skills = ClassRules.SkillsFor(classes, order, Knows);
            var cooldowns = new int[skills.Count];
            var buffs = new int[skills.Count];
            for (int slot = 0; slot < skills.Count; slot++)
            {
                int old = Skills.ToList().IndexOf(skills[slot]);
                cooldowns[slot] = old >= 0 ? _cooldowns[old] : CooldownFor(skills[slot]);
                buffs[slot] = old >= 0 ? _buffTicks[old] : 0;
            }
            Classes = classes;
            Skills = skills;
            var natural = ClassRules.AllSkillsFor(classes);
            SkillOrder = skills.Select(s => natural.IndexOf(s)).ToList();
            _cooldowns = cooldowns;
            _buffTicks = buffs;
            KitVersion++;
            RefreshStats();
        }

        // Goes up whenever the classes or the skills in the slots change, so the host can tell the player.
        public int KitVersion { get; private set; }

        // Ticks until any consumable can be used again.
        public int ConsumableCooldown { get; internal set; }
        // Burns and chills from monsters' elements.
        public Afflictions Afflictions { get; } = new Afflictions();
        // Ticks until a recall scroll takes the player home, or 0.
        public int RecallTicks { get; internal set; }
        public bool Recalling => RecallTicks > 0;
        // Lying in their bed, waiting for everyone else to, and where they lay down (World.Sleep).
        public bool Sleeping { get; internal set; }
        internal Vec2 SleptAt { get; set; }
        // How fast the player may move now: slower while chilled.
        public float MoveSpeed => Stats.MoveSpeed * (Afflictions.Chilled ? World.PlayerChillSpeed : 1);
        // The last food eaten while its buff lasts, and the ticks it has left. Falling does not end it.
        public ConsumableDefinition Food { get; private set; }
        public int FoodTicks { get; private set; }

        internal int AttackCooldown { get; set; }
        // The basic attack combo (World.Attack): the step of the last attack, when it was, and how long it took to
        // recover from.
        public int ComboStep { get; internal set; }
        internal long LastAttackTick { get; set; } = -1;
        internal int AttackRecovery { get; set; }
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

        // A skill just learned (World.LearnSkill) is ready at once: it was never used, so nothing is skipped.
        internal void ClearCooldown(SkillDefinition skill)
        {
            int slot = Skills.ToList().IndexOf(skill);
            if (slot >= 0) _cooldowns[slot] = 0;
        }

        internal void StartCooldown(int slot) => _cooldowns[slot] = CooldownFor(Skills[slot]);

        // Using a buff again starts its time over.
        internal void StartBuff(int slot)
        {
            bool wasOn = HasBuff(slot);
            _buffTicks[slot] = DurationFor(Skills[slot]);
            if (!wasOn) RefreshStats();
        }

        // A new food's buff replaces the one before.
        internal void Eat(ConsumableDefinition food)
        {
            Food = food;
            FoodTicks = food.BuffTicks;
            RefreshStats();
        }

        // One tick of cooldowns and buffs.
        internal void TickSkills()
        {
            bool expired = false;
            if (FoodTicks > 0 && --FoodTicks == 0)
            {
                Food = null;
                expired = true;
            }
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

        // Works the numbers out again after the gear or buffs changed, keeping the same share of health and mana.
        internal void RefreshStats()
        {
            float healthShare = Health / Stats.MaxHealth;
            float manaShare = Mana / Stats.MaxMana;
            var buffs = new List<SkillDefinition>();
            for (int slot = 0; slot < Skills.Count; slot++)
            {
                if (HasBuff(slot)) buffs.Add(Skills[slot]);
            }
            Stats = PlayerStats.From(Equipment, Classes, buffs, Food, Level, Passives);
            if (!IsDead) Health = healthShare * Stats.MaxHealth;
            Mana = manaShare * Stats.MaxMana;
        }
    }

    public enum EnemyState : byte
    {
        Idle,
        Chase,
        // Telegraphing an attack. The hit lands when this ends, if the target is still in reach.
        Windup,
        Recover,
        // A charging monster rushing along its marked path.
        Charge,
    }

    public sealed class Enemy
    {
        internal Enemy(int id, MonsterDefinition definition, Vec2 position)
        {
            Id = id;
            Definition = definition;
            Position = position;
            Health = definition.Health;
            MaxHealth = definition.Health;
        }

        public int Id { get; }
        public MonsterDefinition Definition { get; }
        // A rift makes its monsters tougher, harder-hitting or quicker than their sheet (World.Rift.cs).
        public float MaxHealth { get; internal set; }
        public float Power { get; internal set; } = 1;
        public float SpeedScale { get; internal set; } = 1;
        // 0 for an ordinary monster, RiftMonster or RiftGuardian for one a rift brought.
        public int Rift { get; internal set; }
        public float Damage => Definition.Damage * Power;
        public float Speed => Definition.Speed * SpeedScale;
        public Vec2 Position { get; internal set; }
        public Vec2 Facing { get; internal set; } = new Vec2(0, -1);
        public float Health { get; internal set; }
        public bool IsDead => Health <= 0;
        public EnemyState State { get; internal set; }
        public int StateTicks { get; internal set; }
        public int TargetId { get; internal set; }

        public Afflictions Afflictions { get; } = new Afflictions();

        internal Vec2 Knockback { get; set; }
        internal Vec2 ChargeDirection { get; set; }
        internal HashSet<int> ChargeHits { get; } = new HashSet<int>();
    }

    // A spirit a summon skill called. It follows its player, goes after enemies near them and lasts the skill's
    // duration; nothing can hurt it. Its hits count as the player's. See Docs/Classes.md.
    public sealed class Minion
    {
        internal Minion(int id, int ownerId, int slot, SkillDefinition skill, int index, Vec2 position)
        {
            Id = id;
            OwnerId = ownerId;
            Slot = slot;
            Skill = skill;
            Index = index;
            Position = position;
            TicksLeft = skill.DurationTicks;
        }

        public int Id { get; }
        public int OwnerId { get; }
        // The skill slot that called it, and the skill.
        public int Slot { get; }
        public SkillDefinition Skill { get; }
        // Which of the skill's spirits it is: where it waits around its player.
        public int Index { get; }
        public Vec2 Position { get; internal set; }
        public Vec2 Facing { get; internal set; } = new Vec2(0, -1);
        public int TicksLeft { get; internal set; }
        // The enemy or boss it is after, or 0.
        public int TargetId { get; internal set; }

        internal int AttackCooldown { get; set; }
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
        // Source, a player, struck the wall at Position; Amount is how far it is dug in percent (World.DugThrough when
        // it broke), -1 when it needs a pickaxe, or World.NeedsBetterPickaxe.
        Dug,
        // Source, a player, harvested Amount from the crop at Position.
        Harvested,
        // Source, a player, watered Amount crops around Position.
        Watered,
        // Source, a player, ate the consumable with sheet index Amount, which feeds them a buff.
        Ate,
        // Lightning jumped from Source (a monster or boss) to Target, a monster.
        Chained,
        // Source, a player, began going home (Amount World.RecallStarted), got there (RecallArrived) or was stopped by
        // a hit (RecallBroken), at Position.
        Recall,
        // Source, a player, reached a new level; Amount is the level (World.GainExperience).
        LevelUp,
        // Source, a player, cast (Amount World.FishingCast), had a bite, landed a fish, lost it or reeled in, with the
        // line's spot at Position (World.Fish).
        Fishing,
        // Source, a player, was given a guide goal's reward; Amount is the goal's index (World.ClaimGoal).
        GoalReward,
        // Everyone slept, and it is morning of day Amount (World.Sleep).
        Morning,
        // A void rift opened (Amount World.RiftOpened), was beaten (RiftCleared) or ran out (RiftFailed) at Position.
        Rift,
        // Source, a player, gave a gift (Amount World.Disliked to World.Loved) or talked (World.Talked) to the person
        // at Position (World.Merchant.cs).
        Npc,
        // Source, a player, reached a new level of a life skill: Amount is the skill × 16 + the level (World.Life.cs).
        LifeLevel,
        // A base defense started (Amount World.DefenseStarted), was held (DefenseWon) or lost (DefenseLost) at the ward
        // stone at Position (World.Defense.cs).
        Defense,
        // A turret at Position fired along Direction; Amount is how far in tenths of a tile (World.Turrets).
        TurretShot,
        // A rainy day began and wet Amount crops (World.Weather.cs).
        Rain,
        // Source, a player, put a skill point into the skill Target - 1 (its index in the sheet); Amount is its rank now.
        // Target 0: they forgot every skill for coins (World.ResetSkills).
        SkillLearned,
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
            type == SimEventType.Burst || type == SimEventType.Dug || type == SimEventType.Harvested || type == SimEventType.Watered
            || type == SimEventType.Recall || type == SimEventType.Fishing || type == SimEventType.Rift || type == SimEventType.Npc
            || type == SimEventType.LifeLevel || type == SimEventType.Defense || type == SimEventType.TurretShot;
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

        public Afflictions Afflictions { get; } = new Afflictions();

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
