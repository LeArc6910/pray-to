using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // Keeps count of the field elites a bot meets (Docs/WorldRework.md 들판 정예, 묶음 2-2): when each first came after
    // it, which of them fell, the bot's falls with one near, and (given the events) how hard their blows were against
    // the bot's health. For --fight --rooms and --hour.
    sealed class EliteWatch
    {
        readonly Dictionary<int, (string Name, EliteRank Rank, long Met)> _met = new Dictionary<int, (string, EliteRank, long)>();
        readonly HashSet<int> _felled = new HashSet<int>();
        readonly Dictionary<int, Vec2> _last = new Dictionary<int, Vec2>();
        readonly List<float> _hits = new List<float>();
        readonly List<float> _fights = new List<float>();
        long _start = -1;
        int _falls = -1;

        public int Falls { get; private set; }

        // Each tick, after the world has stepped.
        public void Look(World world, Player player, IEnumerable<SimEvent> events = null)
        {
            if (_start < 0) _start = world.Tick;
            // A felled monster leaves the world in the tick it falls; a pack goes only when everyone is far away
            // (World.PackSleepReach), so an elite met that is gone while the bot is near fell.
            var here = new HashSet<int>();
            foreach (var enemy in world.Enemies)
            {
                if (enemy.Elite == EliteRank.None) continue;
                here.Add(enemy.Id);
                if (!_met.ContainsKey(enemy.Id) && !enemy.IsDead && enemy.State != EnemyState.Idle && enemy.TargetId == player.Id)
                    _met[enemy.Id] = (World.EliteName(enemy.Definition, enemy.Elite, enemy.Affixes), enemy.Elite, world.Tick);
                _last[enemy.Id] = enemy.Position;
            }
            foreach (var pair in _met)
            {
                if (here.Contains(pair.Key) || _felled.Contains(pair.Key) || !_last.TryGetValue(pair.Key, out var was)) continue;
                if (Vec2.Distance(was, player.Position) > 50) continue;
                _felled.Add(pair.Key);
                _fights.Add((world.Tick - pair.Value.Met) / (float)World.TicksPerSecond);
            }
            if (_falls < 0) _falls = player.Falls;
            if (player.Falls > _falls && world.Enemies.Any(e => e.Elite != EliteRank.None && !e.IsDead && Vec2.Distance(e.Position, player.Position) < 14)) Falls++;
            _falls = player.Falls;
            if (events == null) return;
            foreach (var e in events)
            {
                if (e.Type != SimEventType.Hit || e.Target != player.Id || !_met.TryGetValue(e.Source, out var hitter) || hitter.Rank != EliteRank.Leader) continue;
                _hits.Add(e.Amount / Math.Max(1f, player.Stats.MaxHealth));
            }
        }

        // The minutes between one elite (a leader, or the first of a band of champions) and the next coming after the bot.
        IEnumerable<float> Gaps()
        {
            var times = new List<long>();
            foreach (var met in _met.Values.OrderBy(m => m.Met))
                if (times.Count == 0 || met.Met - times[times.Count - 1] > 20 * World.TicksPerSecond) times.Add(met.Met);
            for (int i = 1; i < times.Count; i++) yield return (times[i] - times[i - 1]) / (60f * World.TicksPerSecond);
        }

        public int Bands
        {
            get
            {
                int bands = 0;
                long? last = null;
                foreach (var met in _met.Values.OrderBy(m => m.Met))
                {
                    if (last == null || met.Met - last.Value > 20 * World.TicksPerSecond) bands++;
                    last = met.Met;
                }
                return bands;
            }
        }

        public string Summary(long ticks)
        {
            var gaps = Gaps().ToList();
            int leaders = _met.Values.Count(m => m.Rank == EliteRank.Leader), champions = _met.Values.Count(m => m.Rank == EliteRank.Champion);
            int leadersFelled = _felled.Count(id => _met[id].Rank == EliteRank.Leader), championsFelled = _felled.Count(id => _met[id].Rank == EliteRank.Champion);
            float minutes = ticks / (60f * World.TicksPerSecond);
            string first = _met.Count == 0 ? "-" : BotReport.Time(_met.Values.Min(m => m.Met) - _start);
            string hits = _hits.Count == 0 ? "없음" : $"{_hits.Count}번, 가장 큰 {_hits.Max():P0}, 큰 것 다섯의 평균 {_hits.OrderByDescending(h => h).Take(5).Average():P0}";
            return $"정예: 만난 무리 {Bands}(처음 {first}, 사이 {(gaps.Count > 0 ? $"{gaps.Average():0.0}분" : "-")}, 탐험 {minutes:0.0}분에), 우두머리 {leadersFelled}/{leaders}, 챔피언 {championsFelled}/{champions} 잡음, "
                + $"잡기까지 평균 {(_fights.Count > 0 ? _fights.Average() : 0):0.0}초, 정예 곁 쓰러짐 {Falls}, 우두머리에게 맞은 것 {hits}"
                + (_met.Count > 0 ? $" ({string.Join(", ", _met.Values.Where(m => m.Rank == EliteRank.Leader).Select(m => m.Name).Distinct().Take(6))})" : "");
        }
    }
}
