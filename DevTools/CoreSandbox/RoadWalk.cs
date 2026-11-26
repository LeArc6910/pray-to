using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // --roadwalk [minutes] [seeds] (Docs/HuntingGrounds.md): a bot that keeps to the roads of the story, out along each
    // and back, fighting only what comes after it, in a world of version 7 and of the new version: how often it meets
    // monsters (a new one coming after it more than EncounterGap after the last). The target is one or two a minute.
    // Then each world's hunting grounds, and a bot standing in the nearest one: what it fells a minute and what it gains.
    static class RoadWalk
    {
        const float EncounterGap = 10;

        public static void Run(BotSheets sheets, int minutes, IEnumerable<ulong> seeds)
        {
            Console.WriteLine($"길만 걷는 봇: 길을 따라 갔다 오고, 쫓아온 것만 싸운다. 만남 = 앞 만남에서 {EncounterGap}초 지나 새로 쫓아온 몬스터(Docs/HuntingGrounds.md)");
            Console.WriteLine("판 시드  분  만남  분당   처치  싸운 시간  길 위 시간");
            var rates = new Dictionary<int, List<float>>();
            foreach (int version in new[] { WorldGenerator.LavaVoidVersion, WorldGenerator.NewWorldVersion }.Distinct())
            {
                rates[version] = new List<float>();
                foreach (ulong seed in seeds)
                {
                    var (meets, kills, fighting, onRoad) = Walk(sheets, seed, version, minutes);
                    rates[version].Add(meets / (float)minutes);
                    Console.WriteLine($"{version,2} {seed,4}  {minutes,2}  {meets,4}  {meets / (float)minutes,4:0.0}  {kills,5}  {fighting / 60f,6:0.0}분  {onRoad * 100,6:0}%");
                }
            }
            foreach (var (version, list) in rates) Console.WriteLine($"{version}판 분당 만남 평균 {list.Average():0.00} ({list.Min():0.0}~{list.Max():0.0})");

            Console.WriteLine();
            Console.WriteLine("사냥터: 생물군계마다 수, 가장 가까운 곳에 서서 3분 싸우기(처치/분, 경험치/분)");
            foreach (ulong seed in seeds) Hunt(sheets, seed);
        }

        static WorldHost Host(BotSheets sheets, ulong seed, int version) =>
            new WorldHost(seed, sheets.Monsters, sheets.Loot, bosses: sheets.Bosses, classes: sheets.Classes, tiles: sheets.Tiles,
                guide: sheets.Guide, npcs: sheets.Npcs, config: new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = version },
                worldConfig: new WorldConfig { Forage = true }, story: sheets.Story);

        static Player Bot(BotSheets sheets, World world, int level)
        {
            var equipment = HitTable.Wearing(sheets.Loot, DamageBands.BandOfLevel(level));
            var player = world.AddPlayer("bot", equipment);
            player.Experience = Leveling.TotalFor(level);
            player.Health = player.Stats.MaxHealth;
            return player;
        }

        static (int Meets, int Kills, int Fighting, float OnRoad) Walk(BotSheets sheets, ulong seed, int version, int minutes)
        {
            var host = Host(sheets, seed, version);
            var world = host.World;
            var player = Bot(sheets, world, 12);
            var way = new List<(int X, int Y)>();
            foreach (var route in host.Generated.Roads.Where(r => r.Kind == RoadKind.Road))
            {
                way.AddRange(route.Tiles);
                way.AddRange(route.Tiles.Reverse());
            }
            if (way.Count == 0) return (0, 0, 0, 0);
            int at = way.Select((t, i) => (i, d: Vec2.Distance(TileMap.Center(t.X, t.Y), player.Position))).OrderBy(p => p.d).First().i;
            player.Position = TileMap.Center(way[at].X, way[at].Y);
            var toEnemies = new FlowField(world.Map);
            var chased = new HashSet<int>();
            long last = long.MinValue / 2;
            int meets = 0, kills = 0, fighting = 0, roadTicks = 0, stuck = 0;
            int total = minutes * 60 * World.TicksPerSecond;
            for (int tick = 0; tick < total; tick++)
            {
                player.Health = Math.Max(player.Health, player.Stats.MaxHealth * 0.6f);
                foreach (var enemy in world.Enemies)
                {
                    if (enemy.IsDead || enemy.TargetId != player.Id || enemy.State == EnemyState.Idle || !chased.Add(enemy.Id)) continue;
                    if (world.Tick - last > EncounterGap * World.TicksPerSecond) meets++;
                    last = world.Tick;
                }
                bool fight = world.Enemies.Any(e => !e.IsDead && !e.Away && e.TargetId == player.Id && e.State != EnemyState.Idle && Vec2.Distance(e.Position, player.Position) < 14);
                if (fight)
                {
                    fighting++;
                    if (tick % 10 == 0) toEnemies.Build(world.Enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) < 20).Select(e => e.Position), 30);
                    FightSim.Drive(world, player, toEnemies, dodge: true);
                }
                else
                {
                    world.SetAttacking(player.Id, false);
                    var target = TileMap.Center(way[at].X, way[at].Y);
                    if (Vec2.Distance(player.Position, target) < 0.8f || ++stuck > 3 * World.TicksPerSecond)
                    {
                        at = (at + (stuck > 3 * World.TicksPerSecond ? 6 : 2)) % way.Count;
                        stuck = 0;
                        target = TileMap.Center(way[at].X, way[at].Y);
                    }
                    var step = target - player.Position;
                    if (step.LengthSquared > 1e-6f) step /= step.Length;
                    RollBot.Move(world, player, step, step, Vec2.Zero, false);
                }
                int cx = TileMap.Cell(player.Position.X), cy = TileMap.Cell(player.Position.Y);
                if (world.Map.InBounds(cx, cy) && Structures.IsWay(world.Map[cx, cy])) roadTicks++;
                world.Step();
                foreach (var e in world.TakeEvents())
                    if (e.Type == SimEventType.Died && world.FindPlayer(e.Source) == null) kills++;
            }
            return (meets, kills, fighting / World.TicksPerSecond, roadTicks / (float)total);
        }

        static void Hunt(BotSheets sheets, ulong seed)
        {
            var host = Host(sheets, seed, WorldGenerator.NewWorldVersion);
            var world = host.World;
            var grounds = world.HuntingGrounds;
            string byBiome = string.Join(", ", grounds.GroupBy(g => g.Biome).Select(g => $"{g.Key} {g.Count()}"));
            if (grounds.Count == 0)
            {
                Console.WriteLine($"  시드 {seed}: 사냥터 없음");
                return;
            }
            var player = Bot(sheets, world, 8);
            var room = grounds.OrderBy(g => Vec2.Distance(g.Center, world.Spawn)).First();
            player.Position = room.Center;
            var toEnemies = new FlowField(world.Map);
            int kills = 0, most = 0;
            long experience = player.Experience;
            int total = 3 * 60 * World.TicksPerSecond;
            for (int tick = 0; tick < total; tick++)
            {
                player.Health = Math.Max(player.Health, player.Stats.MaxHealth * 0.6f);
                if (tick % 10 == 0) toEnemies.Build(world.Enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, room.Center) < room.Radius + 8).Select(e => e.Position), 40);
                FightSim.Drive(world, player, toEnemies, dodge: true);
                world.Step();
                most = Math.Max(most, world.Enemies.Count(e => !e.IsDead && Vec2.Distance(e.Position, room.Center) <= room.Radius + 1));
                foreach (var e in world.TakeEvents())
                    if (e.Type == SimEventType.Died && world.FindPlayer(e.Source) == null) kills++;
            }
            Console.WriteLine($"  시드 {seed}: {grounds.Count}곳({byBiome}). {room.Biome} 반지름 {room.Radius}(스폰에서 {Vec2.Distance(room.Center, world.Spawn):0}칸): 처치 {kills / 3f:0.0}/분, 경험치 {(player.Experience - experience) / 3f:0}/분, 한때 가장 많이 {most}");
        }
    }
}
