using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Class skills: novas around the player, projectiles that burst where they land, buffs and summoned spirits. A
    // skill rolls its damage and crit once when used; everything it hits takes that, less its own armor (a spirit
    // rolls each of its hits). Its hits carry the slot + 1 as their Cause (0 is the basic attack), for balancing
    // tools. See Docs/Classes.md.
    public sealed partial class World
    {
        const float SkillProjectileRadius = 0.25f;
        // Degrees between the projectiles of a volley.
        const float VolleySpread = 12;
        public const float MinionRadius = 0.25f;
        // How far past its edge a spirit reaches, how often it strikes, and how far it waits from its player.
        const float MinionReach = 0.35f;
        const float MinionAttackSeconds = 0.8f;
        const float MinionWaitDistance = 0.9f;

        readonly List<Minion> _minions = new List<Minion>();

        public IReadOnlyList<Minion> Minions => _minions;

        public Player AddPlayer(string name, Equipment equipment, params ClassDefinition[] classes) => AddPlayer(name, equipment, null, classes);

        // A player who brings a bag along, as a loaded character does. Its items get ids in this world.
        public Player AddPlayer(string name, Equipment equipment, Inventory bag, params ClassDefinition[] classes)
        {
            var player = new Player(_nextId++, name, Spawn, equipment ?? new Equipment(), classes.ToList(), bag);
            foreach (var item in player.Equipment.Items.Concat(player.Inventory.Items)) Register(item);
            _players.Add(player);
            return player;
        }

        // Takes classes into the slots the level has opened (ClassRules.SlotsAt) and reorders skills, anywhere and free;
        // giving a class up (for another, or for none) is a change, only beside a workbench and for
        // ClassRules.ChangeCoins. False when it cannot: dead, more classes than the slots, a change away from a
        // workbench, or the coins short. A character from before the slots keeps the classes it had. See
        // Docs/Classes.md (클래스 고르기, 게임 중에 바꾸기).
        public bool ChangeClasses(int playerId, IReadOnlyList<ClassDefinition> classes, IReadOnlyList<int> order)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || classes == null) return false;
            if (classes.Count > Math.Max(ClassRules.SlotsAt(player.Level), player.Classes.Count)) return false;
            bool givesUp = player.Classes.Any(c => !classes.Contains(c));
            if (givesUp)
            {
                if (!Map.Near(player.Position, TileType.Workbench, StationReach)) return false;
                // A world without the loot sheets (some tests and sims) has no coins to ask for.
                var coin = CoinMaterial;
                int cost = ClassRules.ChangeCoins(player.Level);
                if (coin != null)
                {
                    if (player.Inventory.CountOf(coin) < cost) return false;
                    player.Inventory.TakeMaterial(coin, cost);
                    player.InventoryVersion++;
                }
            }
            player.ChangeKit(classes, order);
            // A class taken brings its first skill (Player.LearnBasics): the ranks go out with the bag, so it must be
            // sent again even when no coins changed hands (2026-11-09: a second class picked free showed its first
            // skill as not learned).
            player.InventoryVersion++;
            return true;
        }

        // Puts a skill point into the skill: learns it at rank 1 or raises its rank (SkillRules.CanRaise). A newly
        // learned skill goes into a free slot. False when it cannot.
        public bool LearnSkill(int playerId, SkillDefinition skill)
        {
            var player = FindPlayer(playerId);
            if (player == null || !SkillRules.CanRaise(player, skill)) return false;
            bool learned = !player.Knows(skill);
            player.SkillRanks[skill] = player.RankOf(skill) + 1;
            if (learned && player.Skills.Count < ClassRules.SkillSlots)
            {
                var natural = ClassRules.AllSkillsFor(player.Classes);
                var order = player.SkillOrder.Concat(new[] { natural.IndexOf(skill) }).ToList();
                player.ChangeKit(player.Classes, order);
                player.ClearCooldown(skill);
            }
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.SkillLearned, player.Id, target: skill.Index + 1, amount: player.RankOf(skill)));
            return true;
        }

        // Forgets every learned skill but the classes' first ones and gives the points back, for coins: so many a
        // point spent (SkillRules.ResetCoinsPerPoint). False when nothing was learned or the coins are short.
        public bool ResetSkills(int playerId)
        {
            var player = FindPlayer(playerId);
            var coin = CoinMaterial;
            if (player == null) return false;
            int spent = SkillRules.SpentPoints(player.SkillRanks);
            int cost = spent * SkillRules.ResetCoinsPerPoint;
            if (spent == 0 || coin == null || player.Inventory.CountOf(coin) < cost) return false;
            player.Inventory.TakeMaterial(coin, cost);
            // The crystals come back to the bag with the points.
            foreach (var gem in player.Supports.Values.SelectMany(g => g)) player.Inventory.AddMaterial(gem, 1);
            player.Supports.Clear();
            player.SkillRanks.Clear();
            player.LearnBasics(player.Classes);
            player.ChangeKit(player.Classes, null);
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.SkillLearned, player.Id, target: 0, amount: 0));
            return true;
        }

        // Sets a support crystal into a free slot of a learned skill. False when the skill is not learned, the
        // crystal is not in the bag, does nothing for the skill, the skill holds one of its kind already or has no
        // slot free at its rank (SkillSupports.SlotsFor).
        public bool AddSupport(int playerId, SkillDefinition skill, MaterialDefinition gem)
        {
            var player = FindPlayer(playerId);
            if (player == null || skill == null || !player.Knows(skill) || gem == null || player.Inventory.CountOf(gem) == 0) return false;
            if (SkillSupports.KindOf(gem) is not SupportKind kind || !SkillSupports.CanAdd(skill, player.RankOf(skill), player.SupportsOf(skill).ToList(), kind)) return false;
            player.Inventory.TakeMaterial(gem, 1);
            if (!player.Supports.TryGetValue(skill, out var gems)) player.Supports[skill] = gems = new List<MaterialDefinition>();
            gems.Add(gem);
            player.InventoryVersion++;
            return true;
        }

        // Takes the crystal in that slot (0, 1, 2) out of the skill, back to the bag.
        public bool RemoveSupport(int playerId, SkillDefinition skill, int slot)
        {
            var player = FindPlayer(playerId);
            if (player == null || skill == null || !player.Supports.TryGetValue(skill, out var gems) || slot < 0 || slot >= gems.Count) return false;
            player.Inventory.AddMaterial(gems[slot], 1);
            gems.RemoveAt(slot);
            if (gems.Count == 0) player.Supports.Remove(skill);
            player.InventoryVersion++;
            return true;
        }

        // Puts back the crystals a character save had in its skills, for the skills it still knows. One the skill no
        // longer takes (the rules changed after the save, or its slots are full) goes back to the bag.
        public static void RestoreSupports(Player player, ClassData classes, LootData loot, IEnumerable<(string Skill, string Gem)> saved)
        {
            player.Supports.Clear();
            foreach (var (skillId, gemId) in saved ?? Enumerable.Empty<(string, string)>())
            {
                var skill = classes?.Skills.FirstOrDefault(s => s.Id == skillId);
                var gem = loot?.Materials.FirstOrDefault(m => m.Id == gemId);
                if (skill == null || gem == null || SkillSupports.KindOf(gem) is not SupportKind kind) continue;
                if (player.Knows(skill) && SkillSupports.CanAdd(skill, player.RankOf(skill), player.SupportsOf(skill).ToList(), kind))
                {
                    if (!player.Supports.TryGetValue(skill, out var gems)) player.Supports[skill] = gems = new List<MaterialDefinition>();
                    gems.Add(gem);
                }
                else player.Inventory.AddMaterial(gem, 1);
            }
        }

        // Puts back the ranks a character save held; unknown ids are dropped. A save from before skill points knows
        // only its classes' first skills and has every point of its level to spend.
        public static void RestoreSkills(Player player, ClassData classes, IEnumerable<(string Id, int Rank)> ranks)
        {
            player.SkillRanks.Clear();
            foreach (var (id, rank) in ranks)
            {
                var skill = classes?.Skills.FirstOrDefault(s => s.Id == id);
                if (skill != null && rank > 0) player.SkillRanks[skill] = Math.Min(rank, SkillRules.MaxRank);
            }
            player.LearnBasics(player.Classes);
            player.ChangeKit(player.Classes, player.SkillOrder);
            player.InventoryVersion++;
        }

        // Every skill of the player's classes at the rank given, for the balance tools (CoreSandbox), the tests and
        // the playtests, whose players have no levels to earn points with.
        internal static void TeachAll(Player player, int rank)
        {
            foreach (var skill in ClassRules.AllSkillsFor(player.Classes)) player.SkillRanks[skill] = Math.Clamp(rank, 1, SkillRules.MaxRank);
            // The slots stay as they are when they are full; otherwise the defaults fill them.
            player.ChangeKit(player.Classes, player.SkillOrder);
            foreach (var skill in player.Skills) player.ClearCooldown(skill);
            player.InventoryVersion++;
        }

        // Uses the skill in the slot, toward aim or the way the player faces when aim is zero. False when the
        // player cannot: dead, no such slot, the skill is still cooling down, or there is not mana enough for it.
        public bool UseSkill(int playerId, int slot, Vec2 aim)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || slot < 0 || slot >= player.Skills.Count || player.CooldownTicks(slot) > 0) return false;
            var skill = player.Skills[slot];
            if (player.Mana < player.ManaCost(skill)) return false;
            if (aim.IsFinite && aim.LengthSquared > 1e-6f) player.Facing = aim.Normalized;

            player.Mana -= player.ManaCost(skill);
            player.StartCooldown(slot);
            // The skill itself rides as the target (its index + 1), so others draw the right one.
            _events.Add(new SimEvent(SimEventType.SkillUsed, player.Id, target: skill.Index + 1, amount: slot, direction: player.Facing));
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    float damage = RollSkill(player, skill, out bool critical);
                    HitArea(player, player.Position, SkillSupports.Radius(skill, player.SupportsOf(skill)), damage, critical, slot + 1, player.ContextFor(skill));
                    break;
                case SkillKind.Projectile:
                    Fire(player, skill, slot);
                    break;
                case SkillKind.Buff:
                    player.StartBuff(slot);
                    break;
                case SkillKind.Summon:
                    Summon(player, skill, slot);
                    break;
                case SkillKind.Dash:
                    Dash(player, skill, slot);
                    break;
                case SkillKind.Heal:
                    float healed = Math.Min(player.Stats.MaxHealth - player.Health,
                        player.Stats.MaxHealth * skill.Damage / 100f * SkillRules.DamageScale(player.PowerOf(skill)) * SkillSupports.DamageFactor(skill, player.SupportsOf(skill)));
                    player.Health += Math.Max(0, healed);
                    _events.Add(new SimEvent(SimEventType.Healed, player.Id, amount: (int)Math.Round(Math.Max(0, healed))));
                    break;
            }
            return true;
        }

        const float DashStep = 0.25f;

        // Rushes the player the skill's range the way they face, stopping at walls, and strikes once every enemy
        // within its radius of the way it went. The client is told to put the player where the host has them.
        void Dash(Player player, SkillDefinition skill, int slot)
        {
            var start = player.Position;
            var hit = new HashSet<int>();
            bool critical = false;
            float damage = skill.Damage > 0 ? RollSkill(player, skill, out critical) : 0;
            var tags = player.ContextFor(skill);
            float reach = SkillSupports.Radius(skill, player.SupportsOf(skill));
            var doors = DoorsFor(player);
            for (float gone = 0; gone < skill.Range; gone += DashStep)
            {
                var next = Collision.Move(Map, player.Position, PlayerRadius, player.Facing * Math.Min(DashStep, skill.Range - gone), passable: doors);
                if (Vec2.Distance(next, player.Position) < 1e-4f) break;
                player.Position = next;
                if (damage <= 0) continue;
                foreach (var enemy in _enemies.ToList())
                {
                    if (enemy.IsDead || hit.Contains(enemy.Id) || Vec2.Distance(enemy.Position, player.Position) > reach + enemy.Definition.Radius) continue;
                    hit.Add(enemy.Id);
                    HitEnemy(player, enemy, damage, critical, player.Position, tags, slot + 1);
                }
                foreach (var boss in _bosses)
                {
                    if (boss.IsDead || hit.Contains(boss.Id) || Vec2.Distance(boss.Position, player.Position) > reach + boss.Definition.Radius) continue;
                    hit.Add(boss.Id);
                    if (boss.State == BossState.Dormant) Wake(boss);
                    HitBoss(player, boss, damage, critical, tags, slot + 1);
                }
            }
            if (Vec2.Distance(start, player.Position) > 1e-4f) player.Corrections++;
        }

        // Damage before armor for the skill's tags and those buffs grant.
        float RollSkill(Player player, SkillDefinition skill, out bool critical)
        {
            var stats = player.Stats;
            var context = player.ContextFor(skill);
            critical = _random.Chance(stats.CritChanceFor(context) / 100f);
            return stats.DamageFor(context) * skill.Damage / 100f * SkillRules.DamageScale(player.PowerOf(skill)) * SkillSupports.DamageFactor(skill, player.SupportsOf(skill))
                * (critical ? stats.CritDamageFor(context) / 100f : 1f);
        }

        // One projectile where the player faces, or a volley fanned out around that.
        void Fire(Player player, SkillDefinition skill, int slot)
        {
            float damage = RollSkill(player, skill, out bool critical);
            int count = SkillSupports.Count(skill, player.SupportsOf(skill));
            for (int i = 0; i < count; i++)
            {
                float degrees = (i - (count - 1) / 2f) * VolleySpread;
                var direction = Rotate(player.Facing, degrees * MathF.PI / 180f);
                _projectiles.Add(new Projectile(
                    _nextId++,
                    player.Id,
                    slot,
                    player.Position + direction * PlayerRadius,
                    direction * skill.Speed,
                    SkillProjectileRadius,
                    damage,
                    Ticks(skill.Range / skill.Speed),
                    friendly: true,
                    tags: player.ContextFor(skill),
                    skill: skill,
                    critical: critical) { PiercesLeft = SkillSupports.Pierces(player.SupportsOf(skill)) });
            }
        }

        static Vec2 Rotate(Vec2 v, float radians)
        {
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            return new Vec2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

        // The skill's spirits appear around the player. Calling them again sends the old ones away first.
        void Summon(Player player, SkillDefinition skill, int slot)
        {
            DismissMinions(m => m.OwnerId == player.Id && m.Slot == slot);
            int count = SkillSupports.Count(skill, player.SupportsOf(skill));
            for (int i = 0; i < count; i++)
            {
                var position = player.Position + WaitOffset(i, count);
                _minions.Add(new Minion(_nextId++, player.Id, slot, skill, i, position)
                {
                    Facing = player.Facing,
                    TicksLeft = player.DurationFor(skill),
                });
            }
        }

        // Where a spirit waits: spread evenly on a ring around its player, the first one behind them on the left.
        static Vec2 WaitOffset(int index, int count)
        {
            float angle = MathF.PI * 0.75f + index * MathF.PI * 2 / Math.Max(1, count);
            return new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * MinionWaitDistance;
        }

        void DismissMinions(Predicate<Minion> which)
        {
            foreach (var minion in _minions)
            {
                if (which(minion)) _events.Add(new SimEvent(SimEventType.Died, minion.Id));
            }
            _minions.RemoveAll(which);
        }

        // Spirits are gone when their time runs out or their player falls or leaves. Each goes after the nearest enemy
        // or awake boss within the skill's range of its player, strikes it when close, and otherwise floats back to its
        // place by the player. Spirits fly over walls.
        void UpdateMinions()
        {
            foreach (var minion in _minions) minion.TicksLeft--;
            DismissMinions(m =>
            {
                var owner = FindPlayer(m.OwnerId);
                return owner == null || owner.IsDead || m.TicksLeft <= 0;
            });
            foreach (var minion in _minions)
            {
                var owner = FindPlayer(minion.OwnerId);
                var skill = minion.Skill;
                if (minion.AttackCooldown > 0) minion.AttackCooldown--;
                float step = skill.Speed * TickSeconds;

                var (target, at, radius) = MinionTarget(minion, owner);
                if (target != 0)
                {
                    minion.TargetId = target;
                    var toTarget = at - minion.Position;
                    float gap = toTarget.Length - radius - MinionRadius;
                    if (toTarget.LengthSquared > 1e-6f) minion.Facing = toTarget.Normalized;
                    if (gap > MinionReach)
                    {
                        minion.Position += minion.Facing * Math.Min(step, gap - MinionReach * 0.5f);
                    }
                    else if (minion.AttackCooldown == 0)
                    {
                        Strike(minion, owner, target);
                    }
                    continue;
                }

                minion.TargetId = 0;
                var home = owner.Position + WaitOffset(minion.Index, SkillSupports.Count(skill, owner.SupportsOf(skill)));
                var toHome = home - minion.Position;
                float distance = toHome.Length;
                if (distance < 0.05f) continue;
                // A spirit left far behind hurries to catch up.
                float speed = distance > 4 ? step * 2.5f : step;
                minion.Position += toHome / distance * Math.Min(speed, distance);
                if (distance > 0.3f) minion.Facing = toHome / distance;
            }
        }

        // The enemy or boss the spirit goes after: the one it already has while it stays in range, else the nearest.
        // Dormant bosses are left alone, so spirits never wake one.
        (int id, Vec2 position, float radius) MinionTarget(Minion minion, Player owner)
        {
            float range = minion.Skill.Range;
            bool InRange(Vec2 position, float radius) => Vec2.Distance(position, owner.Position) <= range + radius;

            (int, Vec2, float) best = (0, default, 0);
            float bestDistance = float.MaxValue;
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || !InRange(enemy.Position, enemy.Definition.Radius)) continue;
                if (enemy.Id == minion.TargetId) return (enemy.Id, enemy.Position, enemy.Definition.Radius);
                float distance = Vec2.Distance(enemy.Position, minion.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (enemy.Id, enemy.Position, enemy.Definition.Radius);
                }
            }
            foreach (var boss in _bosses)
            {
                if (boss.IsDead || boss.State == BossState.Dormant || !InRange(boss.Position, boss.Definition.Radius)) continue;
                if (boss.Id == minion.TargetId) return (boss.Id, boss.Position, boss.Definition.Radius);
                float distance = Vec2.Distance(boss.Position, minion.Position) - boss.Definition.Radius;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (boss.Id, boss.Position, boss.Definition.Radius);
                }
            }
            return best;
        }

        // A spirit's hit: the skill's share of its player's damage for the skill's tags (and any a buff grants), as
        // the player has it now.
        void Strike(Minion minion, Player owner, int targetId)
        {
            minion.AttackCooldown = Ticks(MinionAttackSeconds);
            _events.Add(new SimEvent(SimEventType.Attack, minion.Id, direction: minion.Facing));
            float damage = RollSkill(owner, minion.Skill, out bool critical);
            var tags = owner.ContextFor(minion.Skill);
            var enemy = FindEnemy(targetId);
            if (enemy != null)
            {
                HitEnemy(owner, enemy, damage, critical, minion.Position, tags, minion.Slot + 1, knockback: false);
                return;
            }
            var boss = _bosses.FirstOrDefault(b => b.Id == targetId);
            if (boss != null) HitBoss(owner, boss, damage, critical, tags, minion.Slot + 1);
        }

        // A player's projectile stops at the first enemy or boss it touches, at a wall, or at the end of its range,
        // and hits what it touches there; a skill's bursts and hits everything in its radius. Returns whether it is
        // gone.
        bool FlyFriendly(Projectile projectile, bool ended)
        {
            var owner = FindPlayer(projectile.Source);
            if (owner == null) return true;
            var at = projectile.Position;
            var pierced = projectile.Pierced;
            bool touching = _enemies.Any(e => !e.IsDead && !pierced.Contains(e.Id) && Vec2.Distance(e.Position, at) <= projectile.Radius + e.Definition.Radius)
                || _bosses.Any(b => !b.IsDead && !pierced.Contains(b.Id) && Vec2.Distance(b.Position, at) <= projectile.Radius + b.Definition.Radius);
            if (!touching && !ended) return false;
            // One that goes through hits what it touches and flies on, softer, while it has monsters left to pass.
            if (touching && !ended && projectile.PiercesLeft > 0)
            {
                HitArea(owner, at, projectile.Radius, projectile.Damage, projectile.Critical, projectile.Cause + 1, projectile.Tags, pierced,
                    projectile.Skill == null ? projectile.More : 0);
                projectile.PiercesLeft--;
                projectile.Damage *= SkillSupports.PierceFalloff;
                return false;
            }

            if (projectile.Skill?.Radius > 0) _events.Add(new SimEvent(SimEventType.Burst, owner.Id, amount: projectile.Cause, position: at));
            float burst = projectile.Skill == null ? 0 : SkillSupports.Radius(projectile.Skill, owner.SupportsOf(projectile.Skill));
            HitArea(owner, at, Math.Max(burst, projectile.Radius), projectile.Damage, projectile.Critical, projectile.Cause + 1,
                projectile.Tags, pierced.Count > 0 ? pierced : null, projectile.Skill == null ? projectile.More : 0);
            return true;
        }

        // Every enemy and boss touching the circle takes the hit, but those in skip; hit ones join skip.
        // A basic attack's arrow or bolt (elements > 0, its finisher's more) also strikes with the weapon's elements.
        void HitArea(Player player, Vec2 centre, float radius, float damage, bool critical, int cause, SkillTag tags, HashSet<int> skip = null,
            float elements = 0)
        {
            foreach (var enemy in _enemies.ToList())
            {
                if (enemy.IsDead || Vec2.Distance(enemy.Position, centre) > radius + enemy.Definition.Radius) continue;
                if (skip != null && !skip.Add(enemy.Id)) continue;
                HitEnemy(player, enemy, damage, critical, centre, tags, cause);
                if (elements > 0) AddElements(player, enemy, null, critical, elements);
            }
            foreach (var boss in _bosses)
            {
                if (boss.IsDead || Vec2.Distance(boss.Position, centre) > radius + boss.Definition.Radius) continue;
                if (skip != null && !skip.Add(boss.Id)) continue;
                if (boss.State == BossState.Dormant) Wake(boss);
                HitBoss(player, boss, damage, critical, tags, cause);
                if (elements > 0) AddElements(player, null, boss, critical, elements);
            }
        }

        // damage is before the monster's resistance to the hit's elements and its armor. Knocks the enemy away from
        // where the hit came from, unless a spirit struck it (spirits strike often and would keep it from ever
        // reaching anyone) or lightning jumped to it. Then the elements do their part (World.Elements.cs).
        void HitEnemy(Player player, Enemy enemy, float damage, bool critical, Vec2 from, SkillTag tags, int cause = 0, bool knockback = true,
            bool chained = false, float push = 1)
        {
            if (enemy.IsDead) return;
            int amount = Combat.Mitigate(damage * ElementFactor(enemy.Definition, tags), enemy.Definition.Armor);
            enemy.Health -= amount;
            if (knockback) enemy.Knockback = (enemy.Position - from).Normalized * KnockbackSpeed * push;
            _events.Add(new SimEvent(SimEventType.Hit, player.Id, enemy.Id, amount, critical, cause: cause));
            Leech(player, cause, amount);
            if (enemy.IsDead)
            {
                _events.Add(new SimEvent(SimEventType.Died, enemy.Id));
                DropFrom(enemy, player);
            }
            Afflict(player, enemy, tags, amount, damage, critical, cause, chained);
            if (enemy.IsDead && (tags & SkillTag.Lightning) != 0 && !chained) Chain(player, enemy.Id, enemy.Position, damage, critical, tags, cause);
        }

        // A boss takes the hit less its armor, burns, and passes lightning on; an invulnerable one takes nothing.
        void HitBoss(Player player, Boss boss, float damage, bool critical, SkillTag tags, int cause = 0)
        {
            if (boss.IsDead || boss.Invulnerable) return;
            int amount = Combat.Mitigate(damage, boss.Definition.Armor);
            DamageBoss(boss, player.Id, amount, critical, cause);
            Leech(player, cause, amount);
            AfflictBoss(player, boss, tags, amount, damage, critical, cause);
        }

        // A skill with the drinking crystal heals its player a share of what it dealt (cause is its slot + 1).
        static void Leech(Player player, int cause, int amount)
        {
            if (cause < 1 || cause > player.Skills.Count || amount <= 0 || player.IsDead) return;
            float share = SkillSupports.Leech(player.SupportsOf(player.Skills[cause - 1]));
            if (share > 0) player.Health = Math.Min(player.Stats.MaxHealth, player.Health + amount * share);
        }
    }
}
