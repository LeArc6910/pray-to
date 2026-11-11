using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Core
{
    // One playtest bot run as numbers (Docs/Playtest.md, 실행마다 숫자 지표, 2026-11-11).
    public sealed class BotRunResult
    {
        public string Name { get; set; } = "";
        public BotMemory Memory { get; set; }
        // Ticks the run went on for, from the bot's start.
        public long Ticks { get; set; }
        public int Level { get; set; }
        // The first boss in the sheet's order that stands in the caves (the warden): its id, for the moments.
        public string FirstBoss { get; set; } = "";
        public IReadOnlyList<Goal> Goals { get; set; } = Array.Empty<Goal>();
    }

    // Writes runs as Markdown tables: one line of numbers each, then each guide goal's time per run, then the stuck
    // reports. The same for the sim (CoreSandbox --hour), the tests and the web playtest's report.md.
    public static class BotReport
    {
        public static string Time(long? ticks) => ticks is long t ? BotPilot.Clock(t) : "-";

        static long? Since(BotMemory memory, string moment) =>
            memory.Moments.TryGetValue(moment, out long tick) ? tick - memory.StartTick : (long?)null;

        static long? GoalAt(BotMemory memory, string goal) =>
            memory.Goals.TryGetValue(goal, out long tick) ? tick - memory.StartTick : (long?)null;

        // The moments a run is judged by (the research's first-hour numbers): the first monster felled, the first thing
        // made (the workbench), the first boss's arena reached, its first warning seen, and it felled.
        public static IEnumerable<(string Name, long? Ticks)> Moments(BotRunResult run)
        {
            var m = run.Memory;
            yield return ("첫 처치", Since(m, BotMemory.FirstKill));
            yield return ("첫 제작", GoalAt(m, "workbench"));
            yield return ("첫 보스 입구", Since(m, BotMemory.Arena + run.FirstBoss));
            yield return ("첫 보스 전조", Since(m, BotMemory.Warning + run.FirstBoss));
            yield return ("첫 보스 처치", Since(m, BotMemory.Felled + run.FirstBoss));
        }

        public static string Table(IReadOnlyList<BotRunResult> runs)
        {
            var text = new StringBuilder();
            text.AppendLine("| 실행 | 시간 | 레벨 | 탐색률(시작 40칸) | 본 칸 | 생물군계 | 처치(종류: 수) | 첫 처치 | 첫 제작 | 첫 보스 입구 | 첫 보스 전조 | 첫 보스 처치 | 쓰러짐 | 멈춤 | 막힘 |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var run in runs)
            {
                var m = run.Memory;
                var kills = m.Kills();
                string killed = kills.Count == 0 ? "0" : $"{kills.Count}종 {kills.Values.Sum()}마리 ({string.Join(", ", kills.Select(k => $"{k.Key} {k.Value}"))})";
                string biomes = string.Join(", ", m.Biomes.OrderBy(b => b.Value).Select(b => BiomeMap.Name(b.Key)));
                var moments = Moments(run).Select(x => Time(x.Ticks));
                text.AppendLine($"| {run.Name} | {Time(run.Ticks)} | {run.Level} | {m.Explored:P0} | {m.SeenTiles} | {biomes} | {killed} | {string.Join(" | ", moments)} | {m.Deaths} | {m.Stalls} | {m.Stuck.Count} |");
            }
            return text.ToString();
        }

        // Each goal (rows) by run (columns): when it was first met, or "건너뜀" and why, or "-" when the run ended first.
        public static string Goals(IReadOnlyList<BotRunResult> runs)
        {
            if (runs.Count == 0) return "";
            var text = new StringBuilder();
            text.AppendLine("| 목표 | " + string.Join(" | ", runs.Select(r => r.Name)) + " |");
            text.AppendLine("|---|" + string.Concat(runs.Select(_ => "---|")));
            foreach (var goal in runs[0].Goals)
            {
                var cells = runs.Select(r =>
                {
                    if (GoalAt(r.Memory, goal.Id) is long t) return Time(t);
                    return r.Memory.Skipped.TryGetValue(goal.Id, out string why) ? $"건너뜀: {why.Replace("|", "/")}" : "-";
                });
                text.AppendLine($"| {goal.Id} {goal.Text} | {string.Join(" | ", cells)} |");
            }
            return text.ToString();
        }

        // The stuck reports of the runs: where, after what, a small map.
        public static string Stuck(IReadOnlyList<BotRunResult> runs, int most = 3)
        {
            var text = new StringBuilder();
            foreach (var run in runs)
            {
                foreach (var stuck in run.Memory.Stuck.Take(most))
                {
                    text.AppendLine($"- {run.Name} {Time(stuck.Tick)} 목표 {stuck.Goal}, 가던 곳 {stuck.Target}, 칸 {stuck.At.X},{stuck.At.Y}: {stuck.Why}");
                    if (stuck.Recent.Count > 0) text.AppendLine("  - 최근 행동: " + string.Join(" / ", stuck.Recent.Skip(Math.Max(0, stuck.Recent.Count - 6))));
                    text.AppendLine("```");
                    text.Append(stuck.Map);
                    text.AppendLine("```");
                }
                if (run.Memory.Stuck.Count > most) text.AppendLine($"- {run.Name}: 막힘 {run.Memory.Stuck.Count - most}개 더");
            }
            return text.ToString();
        }
    }
}
