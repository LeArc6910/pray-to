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
        // Each skill's perk picked at each tier (LifeSkills.PerkIndex): 0 none, 1 or 2.
        public int[] LifePerks { get; } = new int[LifeSkills.Count * LifeSkills.PerkTiers];
        // What the player is making by hand that takes time (World.StartCrafting), or null.
        public CraftJob Crafting { get; internal set; }
        public int LifeVersion { get; internal set; }

        // The bestiary (Records.cs): how many of each kind of thing felled or held, the achievements had, and a
        // counter that moves whenever either changes. RecordedInventory is the inventory version last looked through.
        public Dictionary<(RecordKind Kind, string Id), int> Records { get; } = new Dictionary<(RecordKind, string), int>();
        public HashSet<string> Achievements { get; } = new HashSet<string>();

        // The pet following the player (Pets.cs), or null.
        public PetDefinition Pet { get; internal set; }

        // Each pet's experience, by pet id, kept when it goes back in its egg (Pets.LevelOf).
        public Dictionary<string, int> PetExperience { get; } = new Dictionary<string, int>();

        public int PetLevel => Pet == null ? 0 : Pets.LevelOf(PetExperience.TryGetValue(Pet.Id, out int had) ? had : 0);
        public int RecordsVersion { get; internal set; }
        internal int RecordedInventory { get; set; } = -1;
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

        // How far through each person's story the player is: the last step done, by person id (World.CompleteStory).
        public Dictionary<string, int> StorySteps { get; } = new Dictionary<string, int>();

        // The main story (World.Story.cs, Docs/Story.md): the step they are on (story.csv order, from 0; the count of
        // steps once done), whether they have talked to its giver, the waypoints open to them by place id, and a
        // counter that moves when any of these changes.
        public int JourneyStep { get; internal set; }
        public bool JourneyTaken { get; internal set; }
        public HashSet<string> Waypoints { get; } = new HashSet<string>();
        // What the player answered where the story asked (story.csv choices, 2026-11-10): the choice by step index.
        public Dictionary<int, int> StoryChoices { get; } = new Dictionary<int, int>();
        // Where the player is in each side story (story.csv chain, 2026-11-11): the step (index; the step count once
        // done), absent for one not begun, and the side stories whose step they have taken.
        public Dictionary<string, int> SideSteps { get; } = new Dictionary<string, int>();
        public HashSet<string> SideTaken { get; } = new HashSet<string>();
        public int JourneyVersion { get; internal set; }

        public int RankOf(SkillDefinition skill) => skill != null && SkillRanks.TryGetValue(skill, out int rank) ? rank : 0;

        // The rank the skill works at: learned and added by the stats (SkillRules.Power).
        public int PowerOf(SkillDefinition skill) => SkillRules.Power(RankOf(skill), Stats, skill);

        internal int CooldownFor(SkillDefinition skill) =>
            Math.Max(1, (int)Math.Round(SkillRules.CooldownTicks(skill, PowerOf(skill), Stats.CooldownRecoveryFor(skill.Tags)) * SkillSupports.CooldownFactor(SupportsOf(skill))));

        // The crystals set into each learned skill, in the order they went in (Docs/Classes.md, 스킬 변형), and what
        // they make of the skill.
        public Dictionary<SkillDefinition, List<MaterialDefinition>> Supports { get; } = new Dictionary<SkillDefinition, List<MaterialDefinition>>();

        static readonly SupportKind[] NoSupports = new SupportKind[0];

        public IReadOnlyList<SupportKind> SupportsOf(SkillDefinition skill) =>
            skill != null && Supports.TryGetValue(skill, out var gems) && gems.Count > 0
                ? gems.Select(SkillSupports.KindOf).Where(k => k != null).Select(k => k.Value).ToArray()
                : NoSupports;

        // The tags a skill hits with: its own, what buffs grant and the elements its supports add.
        public SkillTag ContextFor(SkillDefinition skill) => Stats.ContextFor(skill) | SkillSupports.Tags(SupportsOf(skill));

        public int ManaCost(SkillDefinition skill) => SkillSupports.ManaCost(skill, SupportsOf(skill));

        internal int DurationFor(SkillDefinition skill) =>
            (int)Math.Round(SkillRules.DurationTicks(skill, PowerOf(skill), Stats.SkillDurationFor(skill.Tags)) * SkillSupports.DurationFactor(SupportsOf(skill)));

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
        // Going into a dungeon or out of one (World.Dungeon.cs): ticks left, or 0, and where to: a gate's index going in,
        // World.LeaveByDoor or World.LeaveByMenu going out.
        public int DungeonTicks { get; internal set; }
        public int DungeonGoal { get; internal set; }
        public bool DungeonCharging => DungeonTicks > 0;
        // Lying in their bed, waiting for everyone else to, and where they lay down (World.Sleep).
        public bool Sleeping { get; internal set; }
        internal Vec2 SleptAt { get; set; }
        // How fast the player may move now: slower while chilled.
        public float MoveSpeed => Afflictions.Rooted ? 0
            : Stats.MoveSpeed * (Afflictions.Chilled ? World.PlayerChillSpeed : 1) * (Afflictions.Snared ? World.PlayerSnareSpeed : 1);
        // The last food eaten while its buff lasts, and the ticks it has left. Falling does not end it.
        public ConsumableDefinition Food { get; private set; }
        public int FoodTicks { get; private set; }
        // A shrine's blessing while it lasts (World.Pois.cs): the shrine's row and the ticks left. A new one replaces it.
        public PoiDefinition Blessing { get; private set; }
        public int BlessingTicks { get; private set; }
        // Rested at a camp's fire: more experience for these ticks.
        public int RestedTicks { get; internal set; }
        // Ticks by a camp's fire without leaving it.
        internal int CampTicks { get; set; }
        // Riding a minecart (World.Travel.cs, 3c): the line (RoomGraph.Lines), or -1; whether from its A end, the tick
        // the cart set off and its speed, tiles a second. Blows pass by a rider, who goes where the cart goes.
        public int RideLine { get; internal set; } = -1;
        public bool RideForward { get; internal set; }
        public long RideStart { get; internal set; }
        public float RideSpeed { get; internal set; }
        public bool Riding => RideLine >= 0;
        // Rides taken since joining, for the sims.
        public int Rides { get; internal set; }

        // The dodge roll (World.Roll.cs): ticks of the roll left, ticks left in which blows pass through, ticks until the
        // next may begin, and the way it goes. Rolls and Dodges count them since joining, for the sims.
        public int RollTicks { get; internal set; }
        public int DodgeTicks { get; internal set; }
        public int RollCooldown { get; internal set; }
        public Vec2 RollDirection { get; internal set; }
        public bool Rolling => RollTicks > 0;
        public bool Dodging => DodgeTicks > 0;
        public int Rolls { get; internal set; }
        public int Dodges { get; internal set; }
        // Whether this roll's first dodged blow was told already.
        internal bool Dodged { get; set; }

        internal int AttackCooldown { get; set; }
        // The basic attack combo (World.Attack): the step of the last attack, when it was, and how long it took to
        // recover from.
        public int ComboStep { get; internal set; }
        internal long LastAttackTick { get; set; } = -1;
        internal int AttackRecovery { get; set; }
        internal int RespawnTicks { get; set; }
        // How many times the player has fallen since joining (the playtest bot's numbers, BotMemory.Deaths).
        public int Falls { get; internal set; }
        // While fallen in co-op: how long an ally has stood beside them (World.Revive).
        internal int ReviveTicks { get; set; }
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

        internal void Bless(PoiDefinition shrine, int ticks)
        {
            Blessing = shrine;
            BlessingTicks = ticks;
            RefreshStats();
        }

        // The blessing and the rest running out.
        internal void TickPoiBuffs()
        {
            if (RestedTicks > 0) RestedTicks--;
            if (BlessingTicks <= 0 || --BlessingTicks > 0) return;
            Blessing = null;
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
            Stats = PlayerStats.From(Equipment, Classes, buffs, Food, Level, Passives, Pet == null ? null : Pets.ModifiersAt(Pet, PetLevel), Blessing?.Buff);
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
        // Holding a line to an ally it mends (MonsterAttackShape.Mend); a hit breaks it.
        Channel,
        // Dazed after running into a wall (MonsterTraitKind.Stagger): still, unguarded, taking more.
        Stagger,
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
        // Zones past the first where a field monster came (World.ToughenForZone): its experience is that many levels up.
        public int ZoneLevels { get; internal set; }
        public float SpeedScale { get; internal set; } = 1;
        // 0 for an ordinary monster, RiftMonster or RiftGuardian for one a rift brought.
        public int Rift { get; internal set; }
        public float Damage => Definition.Damage * Power * (Empowered ? 1 + EmpowerShare : 1);
        public float Speed => Definition.Speed * SpeedScale * (Empowered ? 1 + EmpowerShare : 1);
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

        // ---- Sheet attacks (monster_attacks.csv, World.MonsterAttacks.cs) ----

        // The attack it is winding up, making or resting from; null between attacks and for one that fights the old way.
        public MonsterAttack Attack { get; internal set; }
        // Under the ground before a burrow comes up: out of reach and not drawn.
        public bool Away => Buried || State == EnemyState.Windup && Attack != null
            && (Attack.Shape == MonsterAttackShape.Burrow || Attack.Shape == MonsterAttackShape.Ambush || Attack.Behind);
        // Under the ground between attacks (MonsterTraitKind.Burrowed) or lying in wait (MonsterAttackShape.Ambush):
        // out of reach, and on the screen only a mound or a ripple where it is.
        public bool Buried { get; internal set; }
        // One a split left: smaller, weaker, and it neither splits again nor drops anything.
        public bool Small { get; internal set; }
        // Made stronger by an ally (MonsterAttackShape.Empower) for these ticks, by this share.
        public bool Empowered => EmpowerTicks > 0;
        internal int EmpowerTicks { get; set; }
        internal float EmpowerShare { get; set; }
        // The ally it mends while it channels, and the ticks between its fills.
        public int TetherId { get; internal set; }
        // The tick each attack (by its place in the sheet) can next be picked.
        internal long[] ReadyAt { get; set; }
        internal bool[] Used { get; set; }
        // Where a leap, burrow or blink comes down, and where the leap began.
        internal Vec2 LandAt { get; set; }
        internal Vec2 LeapFrom { get; set; }
        // Charges, lobbed circles or shots still to come after the first, and the ticks to the next.
        internal int RepeatsLeft { get; set; }
        internal int RepeatTicks { get; set; }
        // How fast its rush is and how long (a sheet charge's own, not the definition's).
        internal float RushSpeed { get; set; }
        // Ticks since it last left a pool behind it (MonsterTraitKind.Trail).
        internal int TrailTicks { get; set; }
        internal Vec2 TrailAt { get; set; }
        // Which way round its target it circles (MonsterTraitKind.Orbit): 1 or -1, turned when it runs into rock.
        internal int OrbitSide { get; set; }
        // The elements of the blow that last hurt it (a deathburst that fire quenches).
        internal SkillTag LastTags { get; set; }
        // What spore clouds gave it since it was last told, and when it may next be told (World.TellCloudHeal); how many
        // a slime mother has shed (MonsterTraitKind.Shed).
        internal float CloudGain { get; set; }
        internal long CloudToldAt { get; set; }
        internal long CloudTick { get; set; } = -1;
        internal int Shed { get; set; }
        // It went off as a bomber (MonsterAttackShape.Explode) rather than being felled.
        internal bool BlewUp { get; set; }

        // ---- Packs and wanderers (World.Packs.cs, Docs/WorldRework.md 무리) ----

        // The pack it came with (World.Pack.Id), or 0 for one that came alone.
        public int PackId { get; internal set; }
        // Its place in its pack's roster, or -1 for one more than the roster (for more players, at night) or one a split
        // left: those are not counted when the pack is felled.
        internal int PackSlot { get; set; } = -1;
        // Where it stood when it came, which it goes back to.
        internal Vec2 Home { get; set; }
        // Walking back to Home the way it came after it was drawn too far (World.PackLeash), healing as it goes and
        // heeding no one.
        public bool Returning { get; internal set; }
        // The way it came since it left Home, a point every World.TrailStep tiles, walked back along when it returns.
        internal List<Vec2> Trail { get; } = new List<Vec2>();
        // Ticks until it takes up its pack's alarm, and the player it then goes after.
        internal int AlertTicks { get; set; }
        internal int AlertTarget { get; set; }
        // One that goes about alone between rooms (World.Wander), and where it is on its way: the room it is in or
        // left, the way it walks and which tile of it, which way along, how long it stands, how long it has been stuck.
        public bool Wanders { get; internal set; }
        internal int WanderRoom { get; set; } = -1;
        internal int WanderEdge { get; set; } = -1;
        internal int WanderStep { get; set; }
        internal int WanderWay { get; set; }
        internal int WanderPause { get; set; }
        internal int Stuck { get; set; }
        // Ticks left to be home in before it is put there (World.HomeInTime).
        internal int HomeTicks { get; set; }
        // A big body's room (L, XL, World.Sizes.cs): it steps no further than this from the middle. Endless for every
        // other monster and for a big one put down with no room (a playtest's).
        internal Vec2 BoundCenter { get; set; }
        internal float BoundRadius { get; set; } = float.PositiveInfinity;

        // ---- Field elites (World.Elites.cs, Docs/WorldRework.md 들판 정예) ----

        // A pack's leader or one of a band of champions, and its affixes; None and no affixes for every other monster.
        public EliteRank Elite { get; internal set; }
        public RiftAffix Affixes { get; internal set; }
        // How many times it has called its minions (RiftAffix.Summon), and the ticks to its next frost burst.
        internal int Summons { get; set; }
        internal int FrostTicks { get; set; }
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
        // Source, a player, reached the achievement Target - 1 (its index in Records.Achievements) and had its reward.
        Achievement,
        // Source, a player, hatched an egg: Target is the pet's number (Pets.IndexOf).
        Hatched,
        // Source, a player, asked the person Target (their row in npcs.csv) to move in, and they did: Position is
        // their new place (World.Invite).
        MovedIn,
        // Source, a player, whose pet (Target, Pets.IndexOf) grew to level Amount (2026-11-09).
        PetLevelUp,
        // Source, a player, drank something that restores mana: Amount the mana it gave, Target the consumable's row
        // in consumables.csv (2026-11-09, so the screen plays the drink).
        ManaRestored,
        // Source, a player, petted the animals of the house at Position; Amount is the house's affection now
        // (World.PetAnimals, 2026-11-09).
        AnimalsPetted,
        // Source, a player, finished making one of what they make by hand (Amount 1), or stopped with it unfinished
        // (Amount 0): walked off, ran short, the bag full, or asked to (World.StartCrafting, 2026-11-09).
        Crafted,
        // Source, a monster, trampled the crop at Position at night, setting it back (World.Farming.cs, 2026-11-09).
        Trampled,
        // Source, a player, took story step Target (Amount World.StoryTaken) or finished it (Amount the step after,
        // from 1) (World.Story.cs, 2026-11-10).
        Story,
        // Source, a player, opened the waypoint of place Target (Amount World.WaypointOpened) or went to it
        // (World.WaypointUsed).
        Waypoint,
        // Source, a player, got Target, a fallen player, back up where they fell (World.Revive, 2026-11-11).
        Revived,
        // Source, a player, began a dodge roll along Direction (World.Roll.cs).
        Rolled,
        // A blow of Source's passed through Target, a player, who was rolling. Once a roll.
        Dodged,
        // Source, a monster, took a hit on its guard (MonsterTraitKind.Guard, 2026-11-21): most of it was turned.
        Guarded,
        // Source, a monster, began mending Target, an ally, for Amount ticks; Amount 0: the line broke or ran out.
        Tether,
        // Something a monster's attack or trait did at Position, for the screen and the ear only: Amount is which
        // (World.MonsterFx*), Source the monster (which may be gone already).
        MonsterFx,
        // Source, a monster of a pack (or a wanderer that joined it), noticed Target, a player: the pack's alarm, one
        // after another (World.Packs.cs). The screen puts a mark over its head.
        Alerted,
        // A chest or a cart was opened (World.UsePoi): Source the opener, Amount the thing's index (World.Pois), at it.
        PoiOpened,
        // A shrine blessed a player, or a player rested at a camp: Source the player, Amount the thing's index, at it.
        Blessed,
        // A minecart (World.Travel.cs, 3c): Source the player, Amount the station's index (World.Pois), Target which
        // (World.RailMended, RailSet, RailArrived), at the station.
        Rail,
        // Source, a player, opened the waystone Amount (World.Pois) (Target World.StoneOpened) or came to it from a
        // waypoint (World.StoneUsed), at it.
        Waystone,
        // Source, a player, read a map scrap (consumables.csv effect map): their own map marks the nearest thing to
        // find they have not seen.
        MapRead,
        // The pacing director (World.Director.cs): dust falling off a wall before an ambush comes out of it (Amount
        // World.AmbushDust), or a raid's howl far off (World.RaidHowl), at Position; Source the player it is for.
        Ambush,
        // A trap room (World.Traps.cs), at its chest: its doors fell (Amount World.TrapSprung), a band came
        // (World.TrapWave + its number), it was won (TrapWon) or lost (TrapLost).
        Trap,
        // A dungeon (World.Dungeon.cs, 묶음 5a): Source, a player, began going in at the gate at Position (Amount
        // World.DungeonEntering) or out (DungeonLeaving, DungeonLeavingMenu), came in at its entrance (DungeonEntered), out by
        // its gate (DungeonLeft), got up at its entrance after falling (DungeonRose), was stopped (DungeonBroken), or found
        // every slot of the pocket taken (DungeonFull).
        Dungeon,
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
            || type == SimEventType.LifeLevel || type == SimEventType.Defense || type == SimEventType.TurretShot
            || type == SimEventType.AnimalsPetted || type == SimEventType.Trampled || type == SimEventType.MovedIn
            || type == SimEventType.MonsterFx || type == SimEventType.PoiOpened || type == SimEventType.Blessed
            || type == SimEventType.Ambush || type == SimEventType.Trap || type == SimEventType.Dungeon;
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
        // Coming in (2026-11-17, arenas.csv): woken, it takes a moment to rise, come down or walk out before the fight
        // starts. It does not attack and cannot be hurt; the view shows its entrance.
        Enter,
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
        // Out of reach while it roars, and while it is underground or in the air before a burrow or a leap comes down.
        public bool Invulnerable => State == BossState.Roar || State == BossState.Enter || Away;
        public bool Away => State == BossState.Telegraph && Attack != null && Attack.GoesAway;

        internal Vec2 ChargeDirection { get; set; }
        internal HashSet<int> ChargeHits { get; } = new HashSet<int>();
        // A spiral's volleys still to fire, the ticks to the next and the turn it has reached (radians).
        internal int VolleysLeft { get; set; }
        internal int VolleyTicks { get; set; }
        internal float VolleyAngle { get; set; }
        // Ticks with nobody left to fight; the boss resets after a while.
        internal int EmptyTicks { get; set; }
        // Just past a roar: the next attack is one the new phase brought (World.Boss.cs, phases).
        internal bool OpensPhase { get; set; }
        // Where a burrow or a leap comes down.
        internal Vec2 LandAt { get; set; }
        // Charges still to come after this one (an attack with count runs that many in a row).
        internal int ChargesLeft { get; set; }
        // Coming in for the first time in this world (the whole entrance) or again (the short one).
        public bool EntersAgain { get; internal set; }
        // Ticks into its rush-and-stop gait while it closes in (BossDefinition.Bursts).
        internal int GaitTicks { get; set; }
    }

    public enum HazardShape : byte
    {
        Circle,
        Cone,
        Line,
        // From Width (the safe middle) out to Size.
        Ring,
        // A circle whose damage is shared among the players in it.
        Share,
        // What a circle left behind (2026-11-13, PoolKind): a web that snares whoever is in it, a pool of venom that
        // hurts whoever stands in it, each until its ResolveTick, when it is simply gone.
        Web,
        Venom,
        // What a monster's attack leaves (2026-11-21, MonsterPool): fire burns whoever stands in it, ice chills them,
        // spores hurt like venom, sand slows like a web.
        Fire,
        Ice,
        Spore,
        Sand,
        // A cloud that fills the monsters in it and does nothing to players (MonsterTraitKind.Deathcloud).
        Balm,
        // A circle that drags the players in it toward its middle at Width tiles a second until it bursts.
        Pull,
        // A trap: a circle that waits, and goes off early when someone steps into it once it is armed.
        Mine,
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
        public Vec2 Position { get; internal set; }
        public Vec2 Direction { get; }
        // Circle radius, a ring's outer radius, or cone and line length.
        public float Size { get; }
        // Cone angle in degrees, line width, or a ring's inner (safe) radius.
        public float Width { get; }
        public int Damage { get; }
        public long StartTick { get; }
        public long ResolveTick { get; internal set; }
        // A monster's own (2026-11-21): drawn from the monster's state on the screen, so it is not sent (Attached); what
        // its hit does besides damage; what it leaves behind and for how long; and for a trap, the tick it is armed.
        public bool Attached { get; internal set; }
        internal HitEffect Effect { get; set; }
        internal float EffectAmount { get; set; }
        internal MonsterPool Leaves { get; set; }
        internal float LeavesSeconds { get; set; }
        internal float LeavesRadius { get; set; }
        public long ArmTick { get; internal set; }
        // 0 for a boss's; for a monster's, the attack shape that laid it plus one, or DeathFlash (what the wire
        // carries in place of the cause, HazardState.MonsterShape).
        public byte Mark { get; internal set; }
        public const byte DeathFlash = 100;
        // The web whose burst this marks (AttackShape.Pluck), taken away when it lands; 0 for none.
        public int Plucks { get; internal set; }
        // A web or a pool of venom: it does its work while it lies there, and nothing when its time is up.
        public bool Lingers => Shape == HazardShape.Web || Shape == HazardShape.Venom || Shape == HazardShape.Fire || Shape == HazardShape.Ice
            || Shape == HazardShape.Spore || Shape == HazardShape.Sand || Shape == HazardShape.Balm;

        // Whether a circle of the given radius at point touches the area.
        public bool Contains(Vec2 point, float radius)
        {
            var offset = point - Position;
            float distance = offset.Length;
            switch (Shape)
            {
                case HazardShape.Circle:
                case HazardShape.Share:
                case HazardShape.Pull:
                case HazardShape.Mine:
                    return distance <= Size + radius;
                case HazardShape.Web:
                case HazardShape.Venom:
                case HazardShape.Fire:
                case HazardShape.Ice:
                case HazardShape.Spore:
                case HazardShape.Sand:
                case HazardShape.Balm:
                    // A pool takes whoever's feet are in it, not whoever brushes its edge.
                    return distance <= Size;
                case HazardShape.Ring:
                    return distance <= Size + radius && distance >= Width - radius;
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
        // Whose it is (2026-11-09, 0 for anyone's) and until when: a boss's drops are each fighter's own for good, a
        // monster's are its killer's for a while. Only the owner sees and picks it up till then.
        internal int Owner { get; set; }
        internal long OwnedUntil { get; set; }
        // How many of the material lie there (coins come in heaps).
        internal int Count { get; set; } = 1;

        public bool For(int playerId, long tick) => Owner == 0 || Owner == playerId || tick >= OwnedUntil;
    }

    // A boss's projectile hurts players. A player's (Friendly) flies through players and stops at the first enemy;
    // a skill's bursts there.
    // A thing being made by hand (World.StartCrafting): the recipe, how many more after this one, and the ticks done
    // of this one out of how many it takes.
    public sealed class CraftJob
    {
        internal CraftJob(Recipe recipe, int left, int ticks)
        {
            Recipe = recipe;
            Left = left;
            Ticks = Math.Max(1, ticks);
        }

        public Recipe Recipe { get; }
        public int Left { get; internal set; }
        public int Done { get; internal set; }
        public int Ticks { get; }
        public float Progress => (float)Done / Ticks;
    }

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
            bool critical = false,
            float more = 1)
        {
            More = more;
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
        // Tiles per second. A bouncing or returning shot's changes on the way (2026-11-21).
        public Vec2 Velocity { get; internal set; }
        // A monster's: how many more walls it comes off; and for one that comes back, the ticks until it turns (0 for
        // one that does not, -1 once it has turned) and the pause left at the turn.
        internal int BouncesLeft { get; set; }
        internal int TurnTicks { get; set; }
        internal int PauseTicks { get; set; }
        internal float Pace { get; set; }
        // What its hit does besides damage (MonsterAttack.Effect).
        internal HitEffect Effect { get; set; }
        internal float EffectAmount { get; set; }
        public float Radius { get; }
        // Before armor. A player's projectile rolls its damage and crit when it is fired; one that goes through
        // monsters (SkillSupports.Pierces) is softer after each.
        public float Damage { get; internal set; }
        // How many more monsters it goes on through, and those it has gone through.
        internal int PiercesLeft { get; set; }
        internal HashSet<int> Pierced { get; } = new HashSet<int>();
        public bool Friendly { get; }
        // A player's: the tags it hits with, buffs included.
        public SkillTag Tags { get; }
        // The skill that fired it, or null for a basic attack or a boss's.
        public SkillDefinition Skill { get; }
        public bool Critical { get; }
        // A basic attack's: how much harder than a plain one it hits (a finisher's), for the elements it adds.
        public float More { get; }
        internal int RemainingTicks { get; set; }
    }
}
