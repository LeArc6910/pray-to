using System;
using System.Linq;

namespace Game.Core
{
    // Class skills: novas around the player, projectiles that burst where they land, and buffs. A skill rolls its
    // damage and crit once when used; everything it hits takes that, less its own armor. Its SkillUsed or Burst event
    // comes before the hits it causes. See Docs/Classes.md.
    public sealed partial class World
    {
        const float SkillProjectileRadius = 0.25f;

        public Player AddPlayer(string name, Equipment equipment, params ClassDefinition[] classes)
        {
            var player = new Player(_nextId++, name, Spawn, equipment ?? new Equipment(), classes.ToList());
            foreach (var item in player.Equipment.Items) Register(item);
            _players.Add(player);
            return player;
        }

        // Uses the skill in the slot, toward aim or the way the player faces when aim is zero. False when the
        // player cannot: dead, no such slot, or the skill is still cooling down.
        public bool UseSkill(int playerId, int slot, Vec2 aim)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || slot < 0 || slot >= player.Skills.Count || player.CooldownTicks(slot) > 0) return false;
            if (aim.IsFinite && aim.LengthSquared > 1e-6f) player.Facing = aim.Normalized;

            var skill = player.Skills[slot];
            player.StartCooldown(slot);
            _events.Add(new SimEvent(SimEventType.SkillUsed, player.Id, amount: slot, direction: player.Facing));
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    float damage = RollSkill(player, skill, out bool critical);
                    HitArea(player, player.Position, skill.Radius, damage, critical);
                    break;
                case SkillKind.Projectile:
                    Fire(player, skill, slot);
                    break;
                case SkillKind.Buff:
                    player.StartBuff(slot);
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

        void Fire(Player player, SkillDefinition skill, int slot)
        {
            float damage = RollSkill(player, skill, out bool critical);
            var direction = player.Facing;
            _projectiles.Add(new Projectile(
                _nextId++,
                player.Id,
                slot,
                player.Position + direction * PlayerRadius,
                direction * skill.Speed,
                SkillProjectileRadius,
                damage,
                Ticks(skill.Range / skill.Speed),
                skill,
                critical));
        }

        // A player's projectile bursts on the first enemy or boss it touches, on a wall, or at the end of its
        // range. Returns whether it is gone.
        bool FlyFriendly(Projectile projectile, bool ended)
        {
            var owner = FindPlayer(projectile.Source);
            if (owner == null) return true;
            var at = projectile.Position;
            bool touching = _enemies.Any(e => !e.IsDead && Vec2.Distance(e.Position, at) <= projectile.Radius + e.Definition.Radius)
                || _bosses.Any(b => !b.IsDead && Vec2.Distance(b.Position, at) <= projectile.Radius + b.Definition.Radius);
            if (!touching && !ended) return false;

            _events.Add(new SimEvent(SimEventType.Burst, owner.Id, amount: projectile.Cause, position: at));
            HitArea(owner, at, Math.Max(projectile.Skill.Radius, projectile.Radius), projectile.Damage, projectile.Critical);
            return true;
        }

        // Every enemy and boss touching the circle takes the hit.
        void HitArea(Player player, Vec2 centre, float radius, float damage, bool critical)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || Vec2.Distance(enemy.Position, centre) > radius + enemy.Definition.Radius) continue;
                HitEnemy(player, enemy, Combat.Mitigate(damage, enemy.Definition.Armor), critical, centre);
            }
            foreach (var boss in _bosses)
            {
                if (boss.IsDead || Vec2.Distance(boss.Position, centre) > radius + boss.Definition.Radius) continue;
                if (boss.State == BossState.Dormant) Wake(boss);
                if (!boss.Invulnerable) DamageBoss(boss, player.Id, Combat.Mitigate(damage, boss.Definition.Armor), critical);
            }
        }

        // Knocks the enemy away from where the hit came from.
        void HitEnemy(Player player, Enemy enemy, int amount, bool critical, Vec2 from)
        {
            enemy.Health -= amount;
            enemy.Knockback = (enemy.Position - from).Normalized * KnockbackSpeed;
            _events.Add(new SimEvent(SimEventType.Hit, player.Id, enemy.Id, amount, critical));
            if (!enemy.IsDead) return;
            _events.Add(new SimEvent(SimEventType.Died, enemy.Id));
            DropFrom(enemy, player);
        }
    }
}
