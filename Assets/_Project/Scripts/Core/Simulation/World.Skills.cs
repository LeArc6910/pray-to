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

        // Reorders the player's skills, anywhere; or gives them other classes, only beside a workbench. False when it
        // cannot: dead, or new classes away from a workbench. See Docs/Classes.md (게임 중에 바꾸기).
        public bool ChangeClasses(int playerId, IReadOnlyList<ClassDefinition> classes, IReadOnlyList<int> order)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || classes == null) return false;
            bool same = classes.SequenceEqual(player.Classes);
            if (!same && !Map.Near(player.Position, TileType.Workbench, StationReach)) return false;
            player.ChangeKit(classes, order);
            return true;
        }

        // Uses the skill in the slot, toward aim or the way the player faces when aim is zero. False when the
        // player cannot: dead, no such slot, the skill is still cooling down, or there is not mana enough for it.
        public bool UseSkill(int playerId, int slot, Vec2 aim)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || slot < 0 || slot >= player.Skills.Count || player.CooldownTicks(slot) > 0) return false;
            var skill = player.Skills[slot];
            if (player.Mana < skill.Mana) return false;
            if (aim.IsFinite && aim.LengthSquared > 1e-6f) player.Facing = aim.Normalized;

            player.Mana -= skill.Mana;
            player.StartCooldown(slot);
            _events.Add(new SimEvent(SimEventType.SkillUsed, player.Id, amount: slot, direction: player.Facing));
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    float damage = RollSkill(player, skill, out bool critical);
                    HitArea(player, player.Position, skill.Radius, damage, critical, slot + 1, player.Stats.ContextFor(skill));
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
            }
            return true;
        }

        // Damage before armor for the skill's tags and those buffs grant.
        float RollSkill(Player player, SkillDefinition skill, out bool critical)
        {
            var stats = player.Stats;
            var context = stats.ContextFor(skill);
            critical = _random.Chance(stats.CritChanceFor(context) / 100f);
            return stats.DamageFor(context) * skill.Damage / 100f * (critical ? stats.CritDamageFor(context) / 100f : 1f);
        }

        // One projectile where the player faces, or a volley fanned out around that.
        void Fire(Player player, SkillDefinition skill, int slot)
        {
            float damage = RollSkill(player, skill, out bool critical);
            for (int i = 0; i < skill.Count; i++)
            {
                float degrees = (i - (skill.Count - 1) / 2f) * VolleySpread;
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
                    tags: player.Stats.ContextFor(skill),
                    skill: skill,
                    critical: critical));
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
            for (int i = 0; i < skill.Count; i++)
            {
                var position = player.Position + WaitOffset(i, skill.Count);
                _minions.Add(new Minion(_nextId++, player.Id, slot, skill, i, position) { Facing = player.Facing });
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
                var home = owner.Position + WaitOffset(minion.Index, skill.Count);
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
            var tags = owner.Stats.ContextFor(minion.Skill);
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
            bool touching = _enemies.Any(e => !e.IsDead && Vec2.Distance(e.Position, at) <= projectile.Radius + e.Definition.Radius)
                || _bosses.Any(b => !b.IsDead && Vec2.Distance(b.Position, at) <= projectile.Radius + b.Definition.Radius);
            if (!touching && !ended) return false;

            if (projectile.Skill?.Radius > 0) _events.Add(new SimEvent(SimEventType.Burst, owner.Id, amount: projectile.Cause, position: at));
            HitArea(owner, at, Math.Max(projectile.Skill?.Radius ?? 0, projectile.Radius), projectile.Damage, projectile.Critical, projectile.Cause + 1,
                projectile.Tags);
            return true;
        }

        // Every enemy and boss touching the circle takes the hit.
        void HitArea(Player player, Vec2 centre, float radius, float damage, bool critical, int cause, SkillTag tags)
        {
            foreach (var enemy in _enemies.ToList())
            {
                if (enemy.IsDead || Vec2.Distance(enemy.Position, centre) > radius + enemy.Definition.Radius) continue;
                HitEnemy(player, enemy, damage, critical, centre, tags, cause);
            }
            foreach (var boss in _bosses)
            {
                if (boss.IsDead || Vec2.Distance(boss.Position, centre) > radius + boss.Definition.Radius) continue;
                if (boss.State == BossState.Dormant) Wake(boss);
                HitBoss(player, boss, damage, critical, tags, cause);
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
            AfflictBoss(player, boss, tags, amount, damage, critical, cause);
        }
    }
}
