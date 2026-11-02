using System;
using System.Linq;

namespace Game.Core
{
    public enum DayPhase : byte
    {
        Morning,
        Day,
        Evening,
        Night,
    }

    // The days of a world (Docs/Time.md). A clock in ticks from the first morning: a day is DaySeconds of play, from
    // six in the morning to six the next, so a game hour is 50 seconds. It runs only while the world is open, and
    // sleeping moves it on to the next morning. Crops do not follow it: they grow by the ticks that really pass.
    public static class Calendar
    {
        public const float DaySeconds = 1200;
        public const long TicksPerDay = (long)(DaySeconds * World.TicksPerSecond);
        // The hour the clock starts each day at.
        public const int MorningHour = 6;
        const int MinutesPerDay = 24 * 60;

        // 1 on the first day.
        public static int Day(long clock) => (int)(Math.Max(0, clock) / TicksPerDay) + 1;

        // Minutes since midnight: 360 when a day begins.
        public static int MinuteOfDay(long clock)
        {
            long into = Math.Max(0, clock) % TicksPerDay;
            return (MorningHour * 60 + (int)(into * MinutesPerDay / TicksPerDay)) % MinutesPerDay;
        }

        // Morning 6 to 12, day to 18, evening to 22, night until 6.
        public static DayPhase Phase(long clock)
        {
            int hour = MinuteOfDay(clock) / 60;
            if (hour >= 6 && hour < 12) return DayPhase.Morning;
            if (hour >= 12 && hour < 18) return DayPhase.Day;
            if (hour >= 18 && hour < 22) return DayPhase.Evening;
            return DayPhase.Night;
        }

        public static bool IsNight(long clock) => Phase(clock) == DayPhase.Night;

        // The clock at the next morning.
        public static long NextMorning(long clock) => (Math.Max(0, clock) / TicksPerDay + 1) * TicksPerDay;

        // "3일째 14:20".
        public static string Text(long clock)
        {
            int minute = MinuteOfDay(clock);
            return $"{Day(clock)}일째 {minute / 60:00}:{minute % 60:00}";
        }

        public static string PhaseName(DayPhase phase) => phase switch
        {
            DayPhase.Morning => "아침",
            DayPhase.Day => "낮",
            DayPhase.Evening => "저녁",
            _ => "밤",
        };
    }

    // The world's clock, sleeping through the night, and the night's extra monsters.
    public sealed partial class World
    {
        // How near their bed a player must be to lie down in it.
        public const float BedReach = 1.6f;
        // At night the caves hold this share more monsters (24 become 30).
        public const int NightMonstersPercent = 25;

        // Ticks since the first morning (Calendar). Sleeping moves it on; a world save keeps it.
        public long Clock { get; private set; }

        public DayPhase Phase => Calendar.Phase(Clock);

        // How many monsters the caves keep now.
        int EnemyTargetNow => Calendar.IsNight(Clock) ? _config.EnemyTarget * (100 + NightMonstersPercent) / 100 : _config.EnemyTarget;

        // A player beside their own bed lies down, from the evening on. Once everyone in the world is lying down the
        // clock moves on to the next morning. Moving, attacking or being hit gets them up again. False when they
        // cannot: no bed of theirs near, too early, or fallen.
        public bool Sleep(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || !CanSleepNow) return false;
            if (BedOf(player) is not (int x, int y) || Vec2.Distance(player.Position, TileMap.Center(x, y)) > BedReach) return false;
            player.Sleeping = true;
            player.SleptAt = player.Position;
            player.Attacking = false;
            return true;
        }

        public void Wake(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player != null) player.Sleeping = false;
        }

        // Beds are for the evening and the night.
        public bool CanSleepNow => Phase == DayPhase.Evening || Phase == DayPhase.Night;

        public int Sleepers => _players.Count(p => p.Sleeping);

        internal void RestoreClock(long clock) => Clock = Math.Max(0, clock);

        // Called each tick after the players have moved: anyone who moved, attacked or fell gets up; when everyone
        // is still lying down, it is morning.
        void UpdateClock()
        {
            Clock++;
            foreach (var player in _players)
            {
                if (!player.Sleeping) continue;
                if (player.IsDead || player.Attacking || Vec2.Distance(player.Position, player.SleptAt) > 0.25f || BedOf(player) == null)
                    player.Sleeping = false;
            }
            if (_players.Count == 0 || _players.Any(p => !p.Sleeping)) return;
            Clock = Calendar.NextMorning(Clock);
            foreach (var player in _players) player.Sleeping = false;
            _events.Add(new SimEvent(SimEventType.Morning, 0, amount: Calendar.Day(Clock)));
        }
    }
}
