using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // A new character's first hour in fresh worlds (Docs/Playtest.md, 첫 한 시간 봇): FirstHourBot follows the guide
    // up to level 5 on the host's Core with no screen, and the runs come out as tables of numbers (BotReport): how much
    // of the round about the spawn it saw, the biomes, what it felled, when it met each goal and the warden's arena,
    // and where it gave up (with a small map).
    static class HourSim
    {
        public static void Run(BotSheets sheets, int minutes, string seeds, int mapSize = WorldGenerator.GameSize)
        {
            var runs = new List<BotRunResult>();
            foreach (string text in seeds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                ulong seed = ulong.Parse(text.Trim());
                var watch = Stopwatch.StartNew();
                var run = BotRun.FirstHour(sheets, seed, minutes, mapSize: mapSize);
                runs.Add(run);
                Console.Error.WriteLine($"시드 {seed}: 게임 {BotReport.Time(run.Ticks)}을 {watch.Elapsed.TotalSeconds:0.0}초에");
            }
            Console.WriteLine($"## 첫 한 시간 봇 ({minutes}분까지, 레벨 5 목표에서 멈춤)");
            Console.WriteLine();
            Console.WriteLine(BotReport.Table(runs));
            Console.WriteLine("### 안내 목표별 도달 시간");
            Console.WriteLine();
            Console.WriteLine(BotReport.Goals(runs));
            string stuck = BotReport.Stuck(runs);
            if (stuck.Length > 0)
            {
                Console.WriteLine("### 막힘 보고서");
                Console.WriteLine();
                Console.WriteLine(stuck);
            }
        }
    }
}
