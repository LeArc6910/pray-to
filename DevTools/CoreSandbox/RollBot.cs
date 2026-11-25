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
        const int Ahead = BotRoll.Ahead;

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

        static bool Threatened(World world, Player player, out Vec2 away) => BotRoll.Threatened(world, player, out away);
    }
}
