using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // --traps <seeds> (Docs/BiomeRework.md 묶음 B1c, 4-5 함정 방 창살): each trap room of the game's world (versions 2 and
    // 3) shut in as its chest would shut it: the grates of the cut by distance (the fewest of World.TrapReaches, as
    // before), of the cut by the room's ways (World.CutByNarrows), and which a trap would take now; the floor shut in.
    static class TrapCuts
    {
        public static void Run(BotSheets sheets, IEnumerable<ulong> seeds)
        {
            Console.WriteLine("판 시드  함정 방(자리, 반지름, 생물군계)        거리로 닫기(창살/바닥)  통로로 닫기(창살/바닥)  고른 것");
            var chosen = new List<int>();
            var before = new List<int>();
            double most = 0;
            foreach (int version in new[] { WorldGenerator.RoomVersion, WorldGenerator.PropVersion })
            {
                foreach (ulong seed in seeds)
                {
                    var world = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, sheets.Story, sheets.Bosses?.Arenas, version, sheets.Tiles.Terrain);
                    var rooms = world.Rooms;
                    foreach (var chest in rooms.Pois.Where(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Trap))
                    {
                        if (chest.Room < 0) continue;
                        var room = rooms.Nodes[chest.Room];
                        var floor = new HashSet<int>();
                        var doors = new List<(int X, int Y)>();
                        int fewest = int.MaxValue, fewestFloor = 0;
                        foreach (float reach in World.TrapReaches)
                        {
                            World.CutByReach(world.Map, room, chest.X, chest.Y, reach, floor, doors);
                            if (doors.Count < fewest) (fewest, fewestFloor) = (doors.Count, floor.Count);
                        }
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        bool ways = World.CutByNarrows(world.Map, room, chest.X, chest.Y, floor, doors);
                        double ms = clock.Elapsed.TotalMilliseconds;
                        most = Math.Max(most, ms);
                        int pick = ways && doors.Count < fewest ? doors.Count : fewest;
                        chosen.Add(pick);
                        before.Add(fewest);
                        Console.WriteLine($"{version,2} {seed,4}  ({room.X,4},{room.Y,4}) r{room.Radius,-3} {room.Biome,-9}            {fewest,4} / {fewestFloor,-6}        "
                            + (ways ? $"{doors.Count,4} / {floor.Count,-6}" : "   못 닫음    ") + $"        {(pick == fewest ? "거리" : "통로")} {pick}  ({ms:0.0}ms)");
                    }
                }
            }
            if (chosen.Count == 0) return;
            Console.WriteLine($"함정 방 {chosen.Count}곳: 창살 전에 {before.Min()}~{before.Max()}칸(평균 {before.Average():0.0}), 지금 {chosen.Min()}~{chosen.Max()}칸(평균 {chosen.Average():0.0}), 24칸 넘는 방 {before.Count(c => c > 24)} → {chosen.Count(c => c > 24)}, 가장 오래 걸린 셈 {most:0.0}ms");
        }
    }
}
