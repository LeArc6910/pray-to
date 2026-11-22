using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // What the generated map is like as numbers (Docs/WorldRework.md 고치기 전에 잰 것, 1단계 확인): for each seed, how
    // much of it is rock, how many lumps the rock is in and how big, how far the floor is from a wall, how much of the
    // floor is open nine tiles across, the same for each biome against its target (terrain.csv), the rooms and ways of
    // a version 2 world, and how long it took to make.
    static class MapStats
    {
        public sealed class Numbers
        {
            public double Milliseconds;
            // Shares of all tiles, in percent.
            public double Rock, Water, Lava;
            public int Lumps;
            public double LumpMean;
            // The lump the middle tile of rock is in, counting by area.
            public int LumpMedian;
            // Lumps big enough to hide what is behind them (BigLump tiles or more).
            public int BigLumps;
            public double WallDistance;
            public double Open9;
            public Dictionary<Biome, (int Tiles, double Rock, double Water, double Lava, double Open9, int Rooms)> Biomes
                = new Dictionary<Biome, (int, double, double, double, double, int)>();
            public RoomGraph Rooms;
            public IReadOnlyList<RoadRoute> Roads;
        }

        const int BigLump = 100;

        public static Numbers Measure(GeneratedWorld world)
        {
            var map = world.Map;
            int width = map.Width, height = map.Height, total = width * height;
            var numbers = new Numbers { Rooms = world.Rooms, Roads = world.Roads };
            // Rock is whatever is solid but water; open is whatever is walked on (lava too: it burns, it does not stop).
            var rock = new bool[total];
            var open = new bool[total];
            int rocks = 0, water = 0, lava = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var type = map[x, y];
                    if (type == TileType.Water) water++;
                    else if (map.IsSolid(x, y))
                    {
                        rock[y * width + x] = true;
                        rocks++;
                    }
                    else
                    {
                        open[y * width + x] = true;
                        if (type == TileType.Lava) lava++;
                    }
                }
            }
            numbers.Rock = 100.0 * rocks / total;
            numbers.Water = 100.0 * water / total;
            numbers.Lava = 100.0 * lava / total;

            // Lumps of rock (joined by their sides), the one that holds the map's border left out.
            var lump = new int[total];
            var sizes = new List<int>();
            var stack = new Stack<int>();
            int border = -1;
            for (int start = 0; start < total; start++)
            {
                if (!rock[start] || lump[start] != 0) continue;
                int id = sizes.Count + 1, size = 0;
                lump[start] = id;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int at = stack.Pop();
                    size++;
                    int x = at % width, y = at / width;
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1) border = id;
                    if (x > 0 && rock[at - 1] && lump[at - 1] == 0) { lump[at - 1] = id; stack.Push(at - 1); }
                    if (x < width - 1 && rock[at + 1] && lump[at + 1] == 0) { lump[at + 1] = id; stack.Push(at + 1); }
                    if (y > 0 && rock[at - width] && lump[at - width] == 0) { lump[at - width] = id; stack.Push(at - width); }
                    if (y < height - 1 && rock[at + width] && lump[at + width] == 0) { lump[at + width] = id; stack.Push(at + width); }
                }
                sizes.Add(size);
            }
            var inner = sizes.Where((_, i) => i + 1 != border).OrderBy(s => s).ToList();
            numbers.Lumps = inner.Count;
            numbers.LumpMean = inner.Count > 0 ? inner.Average() : 0;
            numbers.BigLumps = inner.Count(size => size >= BigLump);
            long half = inner.Sum(s => (long)s) / 2, running = 0;
            foreach (int size in inner)
            {
                running += size;
                if (running >= half)
                {
                    numbers.LumpMedian = size;
                    break;
                }
            }

            // From each open tile, how far a wall is along each of the eight ways.
            var ways = new[] { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };
            long reach = 0, rays = 0;
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (!open[y * width + x]) continue;
                    foreach (var (dx, dy) in ways)
                    {
                        int steps = 1;
                        while (open[(y + dy * steps) * width + x + dx * steps]) steps++;
                        reach += steps;
                        rays++;
                    }
                }
            }
            numbers.WallDistance = rays > 0 ? (double)reach / rays : 0;

            // Open tiles whose nine by nine round them is all open (a summed table of the closed tiles).
            var closed = new int[(width + 1) * (height + 1)];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    closed[(y + 1) * (width + 1) + x + 1] = (open[y * width + x] ? 0 : 1) + closed[y * (width + 1) + x + 1] + closed[(y + 1) * (width + 1) + x] - closed[y * (width + 1) + x];
            bool Wide(int x, int y)
            {
                if (x < 4 || y < 4 || x >= width - 4 || y >= height - 4) return false;
                int x0 = x - 4, y0 = y - 4, x1 = x + 5, y1 = y + 5;
                return closed[y1 * (width + 1) + x1] - closed[y0 * (width + 1) + x1] - closed[y1 * (width + 1) + x0] + closed[y0 * (width + 1) + x0] == 0;
            }

            var tiles = new Dictionary<Biome, int[]>();
            int openTiles = 0, wideTiles = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var biome = world.Biomes[x, y];
                    if (!tiles.TryGetValue(biome, out var count)) tiles[biome] = count = new int[6];
                    count[0]++;
                    int at = y * width + x;
                    if (rock[at]) count[1]++;
                    else if (map[x, y] == TileType.Water) count[2]++;
                    else
                    {
                        if (map[x, y] == TileType.Lava) count[3]++;
                        count[4]++;
                        openTiles++;
                        if (Wide(x, y))
                        {
                            count[5]++;
                            wideTiles++;
                        }
                    }
                }
            }
            numbers.Open9 = openTiles > 0 ? 100.0 * wideTiles / openTiles : 0;
            foreach (var (biome, count) in tiles)
            {
                int rooms = world.Rooms?.Nodes.Count(n => n.Kind != RoomKind.Arena && n.Biome == biome) ?? 0;
                numbers.Biomes[biome] = (count[0], 100.0 * count[1] / count[0], 100.0 * count[2] / count[0], 100.0 * count[3] / count[0],
                    count[4] > 0 ? 100.0 * count[5] / count[4] : 0, rooms);
            }
            return numbers;
        }

        // From each room, how many rooms along the ways to the nearest that holds a thing to find.
        static string PoiRooms(RoomGraph rooms)
        {
            var holds = new HashSet<int>(rooms.Pois.Select(p => p.Room));
            if (holds.Count == 0) return "-";
            var steps = new List<int>();
            foreach (var start in rooms.Nodes.Where(n => n.Kind == RoomKind.Room))
            {
                var seen = new HashSet<int> { start.Index };
                var ring = new List<RoomNode> { start };
                int step = 0;
                while (ring.Count > 0 && !ring.Any(r => holds.Contains(r.Index)) && step < 30)
                {
                    ring = ring.SelectMany(r => Beside(rooms, r)).Where(r => seen.Add(r.Index)).ToList();
                    step++;
                }
                steps.Add(step);
            }
            steps.Sort();
            return $"{steps.Average():0.0} / {steps[steps.Count * 95 / 100]}";
        }

        static bool Special(RoomNode room) => room.Kind != RoomKind.Room || room.Role != RoomRole.Plain && room.Role != RoomRole.Pack;

        static IEnumerable<RoomNode> Beside(RoomGraph rooms, RoomNode room) =>
            room.Edges.Select(e => rooms.Nodes[rooms.Edges[e].A == room.Index ? rooms.Edges[e].B : rooms.Edges[e].A]);

        // Rooms with nothing but plain rooms and packs within WorldGenerator.SpecialWithin rooms of them.
        static int Bare(RoomGraph rooms)
        {
            int bare = 0;
            foreach (var room in rooms.Nodes)
            {
                if (room.Kind != RoomKind.Room || Special(room)) continue;
                var seen = new HashSet<int> { room.Index };
                var ring = new List<RoomNode> { room };
                bool found = false;
                for (int step = 0; step < WorldGenerator.SpecialWithin && !found; step++)
                {
                    ring = ring.SelectMany(r => Beside(rooms, r)).Where(r => seen.Add(r.Index)).ToList();
                    found = ring.Any(Special);
                }
                if (!found) bare++;
            }
            return bare;
        }

        // Walking out from the spawn along the rooms (the shortest way by rooms), the seconds between one room worth
        // stopping at and the next, at a walk of WalkSpeed tiles a second: the mean and the 95th of a hundred.
        const double WalkSpeed = 5;

        static string Gap(RoomGraph rooms, bool packs)
        {
            bool Notable(RoomNode room) => Special(room) || packs && room.Role == RoomRole.Pack;
            var spawn = rooms.Nodes.First(r => r.Kind == RoomKind.Spawn);
            var parent = new Dictionary<int, RoomNode> { [spawn.Index] = null };
            var open = new Queue<RoomNode>();
            open.Enqueue(spawn);
            var gaps = new List<double>();
            while (open.Count > 0)
            {
                var room = open.Dequeue();
                foreach (var next in Beside(rooms, room))
                {
                    if (parent.ContainsKey(next.Index)) continue;
                    parent[next.Index] = room;
                    open.Enqueue(next);
                }
                if (room == spawn || !Notable(room)) continue;
                double walked = 0;
                for (var at = room; parent[at.Index] is RoomNode before; at = before)
                {
                    walked += Vec2.Distance(at.Center, before.Center);
                    if (Notable(before)) break;
                }
                gaps.Add(walked / WalkSpeed);
            }
            if (gaps.Count == 0) return "-";
            gaps.Sort();
            return $"{gaps.Average():0}초 / {gaps[(int)(gaps.Count * 0.95)]:0}초";
        }

        // Walking a road from one end to the other, the seconds between one room worth stopping at and the next, where a
        // room counts when the road runs through it or past its door (the rooms beside those it runs through); the ends
        // (the places) count too.
        static string RoadGap(RoomGraph rooms, IReadOnlyList<RoadRoute> roads)
        {
            var gaps = new List<double>();
            foreach (var road in roads.Where(r => r.Kind == RoadKind.Road))
            {
                var path = road.Rooms.Select(i => rooms.Nodes[i]).ToList();
                double walked = 0, last = 0;
                for (int i = 0; i < path.Count; i++)
                {
                    if (i > 0) walked += Vec2.Distance(path[i].Center, path[i - 1].Center);
                    bool notable = Special(path[i]) || Beside(rooms, path[i]).Any(Special);
                    if (!notable && i < path.Count - 1) continue;
                    gaps.Add((walked - last) / WalkSpeed);
                    last = walked;
                }
            }
            if (gaps.Count == 0) return "-";
            gaps.Sort();
            return $"{gaps.Average():0}초 / {gaps[(int)(gaps.Count * 0.95)]:0}초";
        }

        // How much longer the roads are than the straight line between their ends: the mean and the most.
        static string Winding(IEnumerable<RoadRoute> roads)
        {
            var ratios = roads.Select(r =>
            {
                var (ax, ay) = r.Tiles[0];
                var (bx, by) = r.Tiles[r.Tiles.Count - 1];
                double straight = Math.Max(1, Math.Sqrt((double)(bx - ax) * (bx - ax) + (double)(by - ay) * (by - ay)));
                // Every eighth tile, so the steps of a slanting way and its wandering do not count as length.
                double walked = 0;
                for (int i = 8, last = 0; last < r.Tiles.Count - 1; last = i, i = Math.Min(r.Tiles.Count - 1, i + 8))
                    walked += Math.Sqrt((double)(r.Tiles[i].X - r.Tiles[last].X) * (r.Tiles[i].X - r.Tiles[last].X) + (double)(r.Tiles[i].Y - r.Tiles[last].Y) * (r.Tiles[i].Y - r.Tiles[last].Y));
                return (Ratio: walked / straight, Length: walked);
            }).ToList();
            return ratios.Count == 0 ? "-" : $"{ratios.Average(r => r.Ratio):0.00}배, {ratios.Max(r => r.Ratio):0.00}배 ({ratios.Sum(r => r.Length):0}칸)";
        }

        public static void Run(StoryData story, ArenaData arenas, TerrainData terrain, IEnumerable<ulong> seeds, IEnumerable<int> versions, int size)
        {
            var seedList = seeds.ToList();
            // Once before the clock, so the first seed does not pay for the runtime warming up.
            WorldGenerator.Generate(seedList[0], size, size, story, arenas, versions.Last(), terrain);
            foreach (int version in versions)
            {
                var runs = new List<(ulong Seed, Numbers Numbers)>();
                foreach (ulong seed in seedList)
                {
                    var watch = Stopwatch.StartNew();
                    var world = WorldGenerator.Generate(seed, size, size, story, arenas, version, terrain);
                    double took = watch.Elapsed.TotalMilliseconds;
                    var numbers = Measure(world);
                    numbers.Milliseconds = took;
                    runs.Add((seed, numbers));
                }
                Console.WriteLine($"## 생성기 {version}판, {size}칸");
                Console.WriteLine();
                Console.WriteLine("| 잰 것 | " + string.Join(" | ", runs.Select(r => $"시드 {r.Seed}")) + " |");
                Console.WriteLine("|---|" + string.Concat(runs.Select(_ => "---|")));
                void Row(string name, Func<Numbers, string> value) => Console.WriteLine($"| {name} | " + string.Join(" | ", runs.Select(r => value(r.Numbers))) + " |");
                Row("막힌 칸(벽·광맥·물)", n => $"{n.Rock + n.Water:0.0}%");
                Row("바위(벽·광맥)", n => $"{n.Rock:0.0}%");
                Row("물", n => $"{n.Water:0.0}%");
                Row("용암", n => $"{n.Lava:0.0}%");
                Row("벽 덩어리 수", n => n.Lumps.ToString());
                Row($"그 가운데 {BigLump}칸 이상", n => n.BigLumps.ToString());
                Row("덩어리 평균 크기(테두리 빼고)", n => $"{n.LumpMean:0}칸");
                Row("넓이 기준 중앙 덩어리", n => $"{n.LumpMedian}칸");
                Row("바닥에서 여덟 방향으로 벽까지", n => $"{n.WallDistance:0.0}칸");
                Row("둘레 9×9칸이 다 열린 바닥", n => $"{n.Open9:0.0}%");
                if (version >= WorldGenerator.RoomVersion)
                {
                    Row("방(아레나 빼고)", n => n.Rooms.Nodes.Count(r => r.Kind != RoomKind.Arena).ToString());
                    Row("통로", n => n.Rooms.Edges.Count.ToString());
                    Row("고리(되살린 통로)", n => n.Rooms.Loops.ToString());
                    Row("막다른 방", n => n.Rooms.DeadEnds.ToString());
                    Row("금 간 벽으로 막힌 고리(지름길)", n => n.Rooms.Edges.Count(e => e.Kind == RoomEdgeKind.Cracked).ToString());
                    Row("조각을 끼운 방", n => n.Rooms.Nodes.Count(r => r.Chunk != null).ToString());
                    Row("길이 지나는 방", n => n.Rooms.Nodes.Count(r => r.Kind == RoomKind.Room && r.Roadside).ToString());
                    Row("길 수(길·아레나 길·샛길)", n => $"{n.Roads.Count(r => r.Kind == RoadKind.Road)} · {n.Roads.Count(r => r.Kind == RoadKind.Arena)} · {n.Roads.Count(r => r.Kind == RoadKind.Link)}");
                    Row("길 길이 / 곧은 거리(평균, 가장 큰 것)", n => Winding(n.Roads.Where(r => r.Kind == RoadKind.Road)));
                    Row("샛길 길이 / 곧은 거리(평균, 가장 큰 것)", n => Winding(n.Roads.Where(r => r.Kind == RoadKind.Link)));
                    Row("금 간 벽이 있는 샛길", n => n.Roads.Count(r => r.Kind == RoadKind.Link && r.Cracked != null).ToString());
                    Row("길을 걸을 때 새것 사이(평균 / 95%)", n => RoadGap(n.Rooms, n.Roads));
                    Row("길이 지나는 방의 쉼터·제단·수레", n => n.Roads.Where(r => r.Kind == RoadKind.Road).SelectMany(r => r.Rooms).Distinct()
                        .Count(i => n.Rooms.Nodes[i].Role == RoomRole.Rest || n.Rooms.Nodes[i].Role == RoomRole.Shrine || n.Rooms.Nodes[i].Role == RoomRole.Cart).ToString());
                    Row("길이 지나는 방(길마다 평균)", n => n.Roads.Count(r => r.Kind == RoadKind.Road) == 0 ? "-" : n.Roads.Where(r => r.Kind == RoadKind.Road).Average(r => r.Rooms.Count).ToString("0.0"));
                    foreach (RoomRole role in Enum.GetValues(typeof(RoomRole)))
                        Row($"역할 {role.ToString().ToLowerInvariant()}", n => n.Rooms.Nodes.Count(r => r.Kind == RoomKind.Room && r.Role == role).ToString());
                    Row($"{WorldGenerator.SpecialWithin}방 안에 새것이 없는 방", n => Bare(n.Rooms).ToString());
                    Row("새것 사이 걷는 시간(평균 / 95%)", n => Gap(n.Rooms, false));
                    Row("무리 방도 세면(평균 / 95%)", n => Gap(n.Rooms, true));
                    // The things to find (묶음 3a·3b): how many of each, and from each room's middle the walk to the nearest.
                    foreach (PoiKind kind in Enum.GetValues(typeof(PoiKind)))
                        Row($"볼거리 {kind.ToString().ToLowerInvariant()}", n => n.Rooms.Pois.Count(p => p.Kind == kind).ToString());
                    Row("상자: 나무 · 철 · 금", n => string.Join(" · ", new[] { TileType.WoodChest, TileType.IronChest, TileType.GoldChest }
                        .Select(t => n.Rooms.Pois.Count(p => p.Definition.Tile == t))));
                    Row("바위 속 숨은 방 / 숨은 방", n => $"{n.Rooms.Nodes.Count(r => r.Buried)} / {n.Rooms.Nodes.Count(r => r.Role == RoomRole.Hidden)}");
                    Row("방에서 가장 가까운 볼거리까지 방 수(평균 / 95%)", n => PoiRooms(n.Rooms));
                }
                Row("생성 시간", n => $"{n.Milliseconds:0} ms");
                Console.WriteLine();

                var biomes = Enum.GetValues(typeof(Biome)).Cast<Biome>().Where(b => runs.Any(r => r.Numbers.Biomes.ContainsKey(b))).ToList();
                Console.WriteLine($"생물군계마다(시드 {string.Join(", ", seedList)}의 평균). 목표는 `terrain.csv`의 `wall_target`·`liquid_target`.");
                Console.WriteLine();
                Console.WriteLine("| 생물군계 | 넓이 | 바위 | 바위 목표 | 물 | 용암 | 물·용암 목표 | 9×9 열린 바닥 | 방 |");
                Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
                foreach (var biome in biomes)
                {
                    var rows = runs.Where(r => r.Numbers.Biomes.ContainsKey(biome)).Select(r => r.Numbers.Biomes[biome]).ToList();
                    var rule = terrain.Of(biome);
                    bool rooms = version >= WorldGenerator.RoomVersion;
                    Console.WriteLine($"| {BiomeMap.Name(biome)} | {rows.Average(r => 100.0 * r.Tiles / (size * size)):0.0}% | {rows.Average(r => r.Rock):0.0}% | "
                        + $"{(rooms ? rule.WallTarget + "%" : "-")} | {rows.Average(r => r.Water):0.0}% | {rows.Average(r => r.Lava):0.0}% | "
                        + $"{(rooms ? rule.LiquidTarget + "%" : "-")} | {rows.Average(r => r.Open9):0.0}% | {(rooms ? rows.Average(r => r.Rooms).ToString("0") : "-")} |");
                }
                Console.WriteLine();
                Console.WriteLine("걷는 걸음(시작 지점에서, 캐지 않고):");
                Console.WriteLine();
                FlowSim.Run(story, seedList, version, terrain, arenas, size);
                Console.WriteLine();
            }
        }
    }
}
