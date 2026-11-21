using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // How the sims' bots use the dodge roll (World.Roll.cs), the way a practised person does: they walk out of what
    // they can, and roll at the last moment when a blow is about to land on them anyway — a telegraph they are still
    // in, a shot or a charging body about to touch them, a monster's bite. --no-roll turns it off, to compare, and
    // --roll-miss 0.3 has them fumble that share of the rolls.
    static class RollBot
    {
        public static bool Enabled = true;
        // How many ticks before a blow lands the bot rolls: inside the roll's unhurt ticks without the host's slack
        // (World.RollSlackTicks), which a person playing over the network does not get.
        const int Ahead = 4;

        // The share of blows the bot fails to roll through (--roll-miss): a person's timing is not a bot's.
        public static float Miss;
        static readonly GameRandom Random = new GameRandom(7, 41);
        static readonly Dictionary<int, long> Fumbled = new Dictionary<int, long>();

        static readonly Dictionary<int, Vec2> Going = new Dictionary<int, Vec2>();

        // One tick of the bot's movement: the roll it is in, a roll it begins now, or a step along input. Returns
        // whether it rolled this tick (a rolling bot does not turn or choose where to go).
        public static bool Move(World world, Player player, Vec2 input, Vec2 facing, Vec2 escape, bool dodge)
        {
            if (!player.Rolling) Going.Remove(player.Id);
            if (Enabled && dodge && !player.Rolling && player.RollCooldown == 0 && !(Fumbled.TryGetValue(player.Id, out long until) && world.Tick < until)
                && Threatened(world, player, out var threat))
            {
                // A fumble: this blow is not rolled through.
                if (Miss > 0 && Random.Chance(Miss)) Fumbled[player.Id] = world.Tick + Ahead + 2;
                else
                {
                    var direction = escape.LengthSquared > 0 ? escape : threat.LengthSquared > 0 ? threat : input;
                    if (world.Roll(player.Id, direction)) Going[player.Id] = player.RollDirection;
                }
            }
            if (Going.TryGetValue(player.Id, out var rolling))
            {
                var to = PlayerMotor.Roll(world.Map, player.Position, rolling, World.RollSlow(player.Afflictions.Chilled, player.Afflictions.Snared), world.DoorsFor(player));
                world.MovePlayer(player.Id, to, facing, 1, rolling: true);
                return true;
            }
            var next = PlayerMotor.Step(world.Map, player.Position, input, player.MoveSpeed, world.DoorsFor(player));
            world.MovePlayer(player.Id, next, facing, 1);
            return false;
        }

        // Whether a blow lands on the player within Ahead ticks where they stand; away is a way out of it, or nothing.
        static bool Threatened(World world, Player player, out Vec2 away)
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
