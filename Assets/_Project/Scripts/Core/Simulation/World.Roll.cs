using System;

namespace Game.Core
{
    // The dodge roll (Docs/Combat.md 구르기, Docs/WorldRework.md 4단계). Every class rolls by the same rules: RollDistance
    // tiles in RollSeconds the way the player is moving (or faces), unhurt by blows for the first DodgeSeconds, and again
    // RollCooldownSeconds after it began. It costs nothing. The owning client moves itself with PlayerMotor.Roll, as it
    // does when walking (World.MovePlayer); the host is told the roll began (World.Roll), gives the unhurt ticks, and
    // takes the faster steps for as long as the roll lasts.
    public sealed partial class World
    {
        public const float RollDistance = 3.2f;
        public const float RollSeconds = 0.3f;
        public const float DodgeSeconds = 0.2f;
        public const float RollCooldownSeconds = 0.9f;
        public static readonly int RollTicks = Ticks(RollSeconds);
        public static readonly int DodgeTicks = Ticks(DodgeSeconds);
        public static readonly int RollCooldownTicks = Ticks(RollCooldownSeconds);
        // Tiles a second: the distance spread evenly over the roll's ticks.
        public static readonly float RollSpeed = RollDistance / (RollTicks * TickSeconds);
        // The host hears of a roll a little after the client began it, and its ticks do not line up with the client's:
        // it keeps the player unhurt this many ticks longer, and lets the next roll begin this many ticks early.
        public const int RollSlackTicks = 2;

        // A chill or a web shortens the roll as it slows the walk.
        public static float RollSlow(bool chilled, bool snared) => (chilled ? PlayerChillSpeed : 1) * (snared ? PlayerSnareSpeed : 1);

        // The player begins a roll along direction (the way they face if it is nothing). Returns false, changing
        // nothing, when they are fallen or rolled too recently. It ends the recovery of a basic attack, so a roll comes
        // out at once after a swing; the combo goes on if the next attack is soon enough after.
        public bool Roll(int id, Vec2 direction)
        {
            var player = FindPlayer(id);
            if (player == null || player.IsDead || player.RollCooldown > RollSlackTicks) return false;
            if (!direction.IsFinite || direction.LengthSquared < 1e-6f) direction = player.Facing;
            player.RollDirection = direction.Normalized;
            player.RollTicks = RollTicks;
            player.DodgeTicks = DodgeTicks + RollSlackTicks;
            player.RollCooldown = RollCooldownTicks;
            player.Dodged = false;
            player.Rolls++;
            player.AttackCooldown = 0;
            _events.Add(new SimEvent(SimEventType.Rolled, player.Id, direction: player.RollDirection));
            return true;
        }

        void UpdateRoll(Player player)
        {
            if (player.RollCooldown > 0) player.RollCooldown--;
            if (player.RollTicks > 0) player.RollTicks--;
            if (player.DodgeTicks > 0) player.DodgeTicks--;
        }

        static void EndRoll(Player player)
        {
            player.RollTicks = 0;
            player.DodgeTicks = 0;
        }

        // Whether a blow passes through the player because they are rolling. The first one of a roll is told to the
        // clients (Dodged), so the screen can say so.
        bool Dodges(Player player, int source)
        {
            if (!player.Dodging) return false;
            player.Dodges++;
            if (!player.Dodged) _events.Add(new SimEvent(SimEventType.Dodged, source, player.Id));
            player.Dodged = true;
            return true;
        }
    }
}
