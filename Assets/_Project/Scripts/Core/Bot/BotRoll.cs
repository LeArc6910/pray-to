using System;

namespace Game.Core
{
    // The dodge roll of the bots (2026-11-25, moved here from DevTools/CoreSandbox/RollBot so the first hour's bot
    // rolls as the guide tells a new player to): a roll through a blow that lands within Ahead ticks where the player
    // stands, inside the roll's unhurt ticks.
    public static class BotRoll
    {
        // How many ticks before a blow lands the bot rolls: inside the roll's unhurt ticks without the host's slack
        // (World.RollSlackTicks), which a person playing over the network does not get.
        public const int Ahead = 4;

        // Whether a blow lands on the player within Ahead ticks where they stand; away is a way out of it, or nothing (a
        // mark on the floor: the bot's own way out of it).
        public static bool Threatened(World world, Player player, out Vec2 away)
        {
            away = Vec2.Zero;
            foreach (var hazard in world.Hazards)
            {
                if (hazard.Lingers || hazard.Damage <= 0 || hazard.ResolveTick - world.Tick > Ahead) continue;
                if (hazard.Contains(player.Position, World.PlayerRadius)) return true;
            }
            foreach (var projectile in world.Projectiles)
            {
                if (projectile.Friendly) continue;
                float speed = projectile.Velocity.Length;
                if (speed < 1e-3f) continue;
                var heading = projectile.Velocity / speed;
                var toPlayer = player.Position - projectile.Position;
                float along = Vec2.Dot(toPlayer, heading);
                float across = Vec2.Dot(toPlayer, new Vec2(-heading.Y, heading.X));
                if (along < 0 || along > speed * World.TickSeconds * Ahead + projectile.Radius + World.PlayerRadius) continue;
                if (MathF.Abs(across) > projectile.Radius + World.PlayerRadius) continue;
                away = new Vec2(-heading.Y, heading.X) * (across >= 0 ? 1 : -1);
                return true;
            }
            foreach (var boss in world.Bosses)
            {
                if (boss.IsDead || boss.State != BossState.Charge || boss.Attack == null) continue;
                var toPlayer = player.Position - boss.Position;
                float along = Vec2.Dot(toPlayer, boss.Facing);
                float across = Vec2.Dot(toPlayer, new Vec2(-boss.Facing.Y, boss.Facing.X));
                float touch = boss.Definition.Radius + World.PlayerRadius;
                if (along < -touch || along > boss.Attack.Speed * World.TickSeconds * Ahead + touch || MathF.Abs(across) > touch) continue;
                away = new Vec2(-boss.Facing.Y, boss.Facing.X) * (across >= 0 ? 1 : -1);
                return true;
            }
            foreach (var enemy in world.Enemies)
            {
                if (enemy.IsDead) continue;
                var definition = enemy.Definition;
                float gap = Vec2.Distance(enemy.Position, player.Position) - definition.Radius - World.PlayerRadius;
                bool bite = enemy.State == EnemyState.Windup && definition.Behavior == MonsterBehavior.Melee && enemy.TargetId == player.Id
                    && enemy.StateTicks <= Ahead && gap <= definition.Reach + 0.2f;
                bool rush = enemy.State == EnemyState.Charge && gap <= definition.ChargeSpeed * World.TickSeconds * Ahead
                    && Vec2.Dot((player.Position - enemy.Position).Normalized, enemy.Facing) > 0.7f;
                if (!bite && !rush) continue;
                away = rush ? new Vec2(-enemy.Facing.Y, enemy.Facing.X) : (player.Position - enemy.Position).Normalized;
                return true;
            }
            return false;
        }
    }
}
