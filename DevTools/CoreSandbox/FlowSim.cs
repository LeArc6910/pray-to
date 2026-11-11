using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // The way a new world leads (Docs/WorldFlow.md, the research report's candidate 24): for each seed, how many steps
    // a player walks from the spawn, without digging, to the first copper vein, the first iron vein, the first boss's
    // arena and each place of the story, and how many biomes lie within a short walk. A step is one tile through open
    // floor (not rock, water, lava or anything solid). "-" is out of walking reach: the player digs to get there.
    static class FlowSim
    {
        // How far "a short walk" is, for counting biomes near the spawn.
        const int NearSteps = 60;

        public static void Run(StoryData story, IEnumerable<ulong> seeds)
        {
            var places = story?.Places.Select(p => p.Id).ToList() ?? new List<string>();
            Console.WriteLine("| 시드 | 구리 광맥 | 철 광맥 | 첫 보스 아레나 | " + string.Join(" | ", places) + $" | {NearSteps}걸음 안 생물군계 |");
            Console.WriteLine("|---|---|---|---|" + string.Concat(places.Select(_ => "---|")) + "---|");
            foreach (ulong seed in seeds)
            {
                var world = WorldGenerator.Generate(seed, story: story);
                var steps = Walk(world.Map, world.SpawnX, world.SpawnY);
                int Near(Func<int, int, bool> what)
                {
                    int best = -1;
                    for (int y = 0; y < world.Map.Height; y++)
                    {
                        for (int x = 0; x < world.Map.Width; x++)
                        {
                            if (!what(x, y)) continue;
                            // A vein is reached from a floor tile beside it.
                            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                int s = StepsAt(steps, world.Map, x + dx, y + dy);
                                if (s >= 0 && (best < 0 || s < best)) best = s;
                            }
                        }
                    }
                    return best;
                }
                int copper = Near((x, y) => world.Map[x, y] == TileType.CopperOre);
                int iron = Near((x, y) => world.Map[x, y] == TileType.IronOre);
                int arena = StepsAt(steps, world.Map, TileMap.Cell(world.Arena.X), TileMap.Cell(world.Arena.Y));
                var toPlaces = places.Select(id =>
                {
                    var site = world.Places.FirstOrDefault(p => p.Definition.Id == id);
                    return site == null ? -1 : StepsAt(steps, world.Map, site.X, site.Y - 2);
                }).ToList();
                var biomes = new HashSet<Biome>();
                for (int y = 0; y < world.Map.Height; y++)
                    for (int x = 0; x < world.Map.Width; x++)
                    {
                        int s = steps[y * world.Map.Width + x];
                        if (s >= 0 && s <= NearSteps) biomes.Add(world.Biomes[x, y]);
                    }
                string Show(int s) => s < 0 ? "-" : s.ToString();
                Console.WriteLine($"| {seed} | {Show(copper)} | {Show(iron)} | {Show(arena)} | " + string.Join(" | ", toPlaces.Select(Show)) + $" | {biomes.Count} |");
            }
        }

        static int StepsAt(int[] steps, TileMap map, int x, int y) => map.InBounds(x, y) ? steps[y * map.Width + x] : -1;

        // Steps from the start to every tile through open floor, -1 where walking cannot reach.
        public static int[] Walk(TileMap map, int startX, int startY)
        {
            var steps = Enumerable.Repeat(-1, map.Width * map.Height).ToArray();
            var queue = new Queue<(int X, int Y)>();
            steps[startY * map.Width + startX] = 0;
            queue.Enqueue((startX, startY));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                int here = steps[y * map.Width + x];
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (!map.InBounds(nx, ny) || steps[ny * map.Width + nx] >= 0 || map.IsSolid(nx, ny)) continue;
                    var type = map[nx, ny];
                    if (type == TileType.Water || type == TileType.Lava) continue;
                    steps[ny * map.Width + nx] = here + 1;
                    queue.Enqueue((nx, ny));
                }
            }
            return steps;
        }
    }
}
