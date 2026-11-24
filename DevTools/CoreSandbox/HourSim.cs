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
        public static void Run(BotSheets sheets, int minutes, string seeds, int mapSize = WorldGenerator.GameSize, int mapVersion = WorldGenerator.NewWorldVersion)
        {
            var runs = new List<BotRunResult>();
            var elites = new List<string>();
            var pois = new List<string>();
            var paces = new List<string>();
            var falls = new List<string>();
            foreach (string text in seeds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                ulong seed = ulong.Parse(text.Trim());
                var watch = Stopwatch.StartNew();
                // The field elites it met on the way (묶음 2-2).
                var seen = new EliteWatch();
                // Where it fell, and what was round it.
                var fell = new FallWatch();
                FirstHourBot last = null;
                var run = BotRun.FirstHour(sheets, seed, minutes, each: bot =>
                {
                    last = bot;
                    seen.Look(bot.World, bot.Player);
                    fell.Look(bot);
                }, mapSize: mapSize, mapVersion: mapVersion, director: Program.Director);
                elites.Add($"- 시드 {seed}: {seen.Summary(run.Ticks)}");
                falls.Add($"- 시드 {seed}: {fell.Summary(run.Memory)}");
                // The things to find it used on its way (묶음 3a) and what they gave, beside the coins it holds at the end.
                if (last != null)
                {
                    var coin = last.World.Loot?.Materials.FirstOrDefault(m => m.Id == World.Coin);
                    int held = coin == null ? 0 : last.Player.Inventory.CountOf(coin);
                    var kinds = last.PoisUsed.GroupBy(p => p.Kind).Select(g => $"{g.Key} {g.Count()}");
                    // What the pacing director did (묶음 4b).
                    var pace = last.World.PaceOf(last.Player);
                    paces.Add($"- 시드 {seed}: 보낸 떠돌이 {pace.Sends}, 매복 {pace.Ambushes}, 습격 {pace.Raids}, 정점 {pace.Peaks}, 쉬기 {pace.Rests}{(last.World.Directs ? "" : " (감독 끔)")}");
                    pois.Add($"- 시드 {seed}: {last.PoisUsed.Count}곳({string.Join(", ", kinds)}), 금화 {last.PoiCoins}(끝에 가진 금화 {held}), 장비 {last.PoiItems}");
                }
                runs.Add(run);
                Console.Error.WriteLine($"시드 {seed}: 게임 {BotReport.Time(run.Ticks)}을 {watch.Elapsed.TotalSeconds:0.0}초에");
            }
            Console.WriteLine($"## 첫 한 시간 봇 ({minutes}분까지, 레벨 5 목표에서 멈춤, 생성기 {mapVersion}판)");
            Console.WriteLine();
            Console.WriteLine(BotReport.Table(runs));
            Console.WriteLine("### 안내 목표별 도달 시간");
            Console.WriteLine();
            Console.WriteLine(BotReport.Goals(runs));
            Console.WriteLine("### 정예");
            Console.WriteLine();
            foreach (string line in elites) Console.WriteLine(line);
            Console.WriteLine();
            Console.WriteLine("### 완급 감독");
            Console.WriteLine();
            foreach (string line in paces) Console.WriteLine(line);
            Console.WriteLine();
            Console.WriteLine("### 쓰러진 곳");
            Console.WriteLine();
            foreach (string line in falls) Console.WriteLine(line);
            Console.WriteLine();
            Console.WriteLine("### 지나며 쓴 볼거리");
            Console.WriteLine();
            foreach (string line in pois) Console.WriteLine(line);
            Console.WriteLine();
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
