using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // Where a bot fell on its run (--hour, 2026-11-24): the time, the tile and its biome, the goal it was on, the
    // monsters within FoeReach of it by kind, and how many of the biomes' props (BiomeProps, 묶음 B1a) stood within
    // PropReach. Written to tell a fall a map caused (shut in among props) from one the bot fought into.
    sealed class FallWatch
    {
        const float FoeReach = 8;
        const int PropReach = 4;

        readonly List<string> _falls = new List<string>();
        int _seen = -1;
        long _start;

        // Each tick, after the world has stepped.
        public void Look(FirstHourBot bot)
        {
            var world = bot.World;
            var player = bot.Player;
            if (_seen < 0)
            {
                _seen = player.Falls;
                _start = world.Tick;
            }
            if (player.Falls == _seen) return;
            _seen = player.Falls;
            int x = TileMap.Cell(player.Position.X), y = TileMap.Cell(player.Position.Y);
            int props = 0;
            for (int ty = y - PropReach; ty <= y + PropReach; ty++)
                for (int tx = x - PropReach; tx <= x + PropReach; tx++)
                    if (world.Map.InBounds(tx, ty) && BiomeProps.Is(world.Map[tx, ty])) props++;
            var foes = world.Enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) <= FoeReach)
                .GroupBy(e => e.Definition.Id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key} {g.Count()}").ToList();
            _falls.Add($"{BotReport.Time(world.Tick - _start)} {x},{y} {world.Biomes[x, y]} (목표 {bot.Current?.Id ?? "-"}, 곁 {(foes.Count == 0 ? "없음" : string.Join(", ", foes))}, 소품 {props})");
        }

        // One line for the run: its falls and how often it got away to rest.
        public string Summary(BotMemory memory) =>
            $"쓰러짐 {_falls.Count}, 물러나 쉼 {memory.Retreats}" + (_falls.Count == 0 ? "" : ": " + string.Join(" / ", _falls));
    }
}
