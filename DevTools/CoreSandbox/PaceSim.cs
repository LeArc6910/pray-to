using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;

namespace CoreSandbox
{
    // --pace [minutes] [seeds] (Docs/WorldRework.md 완급 감독, 묶음 4b): one bot roams a world of rooms as the game
    // makes it, from the spawn to a room it has not been in (one of the few nearest by the way, at random), fighting what
    // comes, and standing to heal when hurt with nothing about, in a world whose first boss fell. Its tension (World.PaceOf) is written as a curve of text (the highest in each half minute), with the state
    // it was most in under it; then the seconds in each state, the peaks and rests, what the director sent (wanderers,
    // ambushes, raids), the fights and the quiet between them, and the hits taken while resting (from wanderers and the
    // director's: these should be none; from the packs of rooms the bot walked into, apart). With --no-director the same
    // world plays without the director acting (the tension is still measured), for the numbers to set beside.
    static class PaceSim
    {
        const string Bars = " ▁▂▃▄▅▆▇█";
        const int Bucket = 30;

        public static void Run(BotSheets sheets, int minutes, IEnumerable<ulong> seeds, string[] gear, ClassDefinition[] classes, bool director, int level)
        {
            Console.WriteLine($"## 완급 ({minutes}분, 감독 {(director ? "켬" : "끔(재기만)")}, 장비 {string.Join(", ", gear)}, 클래스 {FightSim.ClassNames(classes)}, 레벨 {level})");
            Console.WriteLine();
            var rows = new List<string>();
            foreach (ulong seed in seeds) rows.Add(RunOne(sheets, minutes, seed, gear, classes, director, level));
            Console.WriteLine("| 시드 | 쌓기 / 정점 / 내려오기 / 쉬기(초) | 정점 | 쉬기 | 보낸 떠돌이 | 매복 | 습격 | 울음소리 → 첫 닿음(초): 평균(가장 짧은~가장 긴) | 싸움 | 싸움 사이 평균(가장 긴) | 싸운 몫 | 처치 | 쓰러짐 | 받은 피해 | 쉬기 동안 맞음: 떠돌이·감독 / 무리 방 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (string row in rows) Console.WriteLine(row);
        }

        // A raid's howl to the first of it reaching the player (World.RaidRunSeconds, B5c): the mean, the shortest and the
        // longest, and the raids that never reached.
        static string Reach(Pace pace)
        {
            var reaches = pace.RaidReaches;
            string text = reaches.Count == 0 ? "-" : $"{reaches.Average():0.0}({reaches.Min():0.0}~{reaches.Max():0.0})";
            return pace.RaidMisses > 0 ? $"{text}, 못 닿음 {pace.RaidMisses}" : text;
        }

        static string RunOne(BotSheets sheets, int minutes, ulong seed, string[] gear, ClassDefinition[] classes, bool director, int level)
        {
            var host = new WorldHost(seed, sheets.Monsters, sheets.Loot, bosses: sheets.Bosses, classes: sheets.Classes, tiles: sheets.Tiles,
                guide: sheets.Guide, npcs: sheets.Npcs, config: new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.RoomVersion },
                worldConfig: new WorldConfig { Forage = true, Director = director }, story: sheets.Story);
            var world = host.World;
            var rooms = host.Generated.Rooms;
            // A world whose first boss fell, as the iron gear's stage is: ambushes and raids come only then.
            var firstBoss = sheets.Bosses?.Bosses.FirstOrDefault(b => b.Place == null);
            if (firstBoss != null) world.Restore(Array.Empty<TileChange>(), Array.Empty<(CropDefinition, int, int, long, long)>(), firstBoss.DropLevel);
            var random = new GameRandom(seed, 98);
            var generator = new LootGenerator(sheets.Loot);
            var equipment = new Equipment();
            foreach (string id in gear)
            {
                var itemBase = sheets.Loot.FindBase(id);
                equipment.Equip(generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random));
            }
            var player = world.AddPlayer("bot", equipment, classes);
            player.Experience = Leveling.TotalFor(level);
            var skills = new SkillBot(player, sheets.Loot);
            player.Health = player.Stats.MaxHealth;
            var pace = world.PaceOf(player);

            var toEnemies = new FlowField(world.Map);
            var toRoom = new FlowField(world.Map);
            var fromBot = new FlowField(world.Map);
            var visited = new HashSet<int>();
            RoomNode goal = null;
            long goalSince = 0;
            int kills = 0, deaths = 0, taken = 0, restHitsLoose = 0, restHitsRooms = 0;
            var names = new Dictionary<int, string>();
            var inRoom = new Dictionary<int, bool>();
            var seconds = new Dictionary<PaceState, int>();
            var tension = new List<float>();
            var states = new List<PaceState>();
            var gaps = new List<float>();
            int fights = 0, fightingSeconds = 0;
            bool wasFighting = false, healing = false;
            long quietFrom = 0;
            int total = minutes * 60 * World.TicksPerSecond;
            int falls = player.Falls;
            var fallPlaces = new List<string>();
            for (int tick = 0; tick < total; tick++)
            {
                var near = world.Enemies.Where(e => !e.Away && !e.IsDead && Vec2.Distance(e.Position, player.Position) < 16
                    && (e.State != EnemyState.Idle && !e.Returning || Vec2.Distance(e.Position, player.Position) < 5)).ToList();
                if (near.Count > 0 || player.IsDead)
                {
                    if (tick % 10 == 0) toEnemies.Build(world.Enemies.Where(e => !e.Away && Vec2.Distance(e.Position, player.Position) < 24).Select(e => e.Position), 40);
                    FightSim.Drive(world, player, toEnemies, dodge: true);
                }
                else if (player.Health < player.Stats.MaxHealth * 0.45f || healing && player.Health < player.Stats.MaxHealth * 0.9f)
                {
                    // Hurt with nothing about: stands until mostly healed, as a person does.
                    healing = true;
                    world.SetAttacking(player.Id, false);
                }
                else
                {
                    healing = false;
                    world.SetAttacking(player.Id, false);
                    // On to a room not yet walked into: one of the four nearest by the way, at random; a new one after
                    // a while getting nowhere.
                    if (goal == null || Vec2.Distance(player.Position, TileMap.Center(goal.AnchorX, goal.AnchorY)) < 2.5f || world.Tick - goalSince > 90 * World.TicksPerSecond)
                    {
                        if (goal != null) visited.Add(goal.Index);
                        fromBot.Build(new[] { player.Position }, 400);
                        var choices = rooms.Nodes.Where(n => n.Kind == RoomKind.Room && !visited.Contains(n.Index) && n.Role != RoomRole.Locked && n.Role != RoomRole.Hidden
                                && fromBot.DistanceAt(n.AnchorX, n.AnchorY) is int d && d != FlowField.Unreached && d > 6)
                            .OrderBy(n => fromBot.DistanceAt(n.AnchorX, n.AnchorY)).Take(4).ToList();
                        goal = choices.Count > 0 ? choices[random.Next(choices.Count)] : null;
                        goalSince = world.Tick;
                        if (goal != null) toRoom.Build(new[] { TileMap.Center(goal.AnchorX, goal.AnchorY) }, 500);
                    }
                    var way = goal == null ? Vec2.Zero : toRoom.DirectionFrom(player.Position);
                    if (goal != null && way.LengthSquared < 1e-6f) visited.Add(goal.Index);
                    var flee = BossSim.Escape(world, player, World.Ticks(0.25f));
                    RollBot.Move(world, player, flee.LengthSquared > 0 ? flee : way, way, flee, true);
                }
                var nearest = near.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                skills.Use(world, nearest?.Position ?? player.Position, nearest != null && Vec2.Distance(nearest.Position, player.Position) < 8);
                foreach (var enemy in world.Enemies)
                {
                    names[enemy.Id] = enemy.Definition.Name;
                    inRoom[enemy.Id] = world.PackOf(enemy)?.Room != null;
                }
                var state = pace.State;
                world.Step();
                foreach (var e in world.TakeEvents())
                {
                    skills.Count(e);
                    if (e.Type == SimEventType.Hit && e.Target == player.Id)
                    {
                        taken += e.Amount;
                        if (state == PaceState.Rest && names.ContainsKey(e.Source))
                        {
                            if (inRoom.GetValueOrDefault(e.Source)) restHitsRooms++;
                            else restHitsLoose++;
                        }
                    }
                    if (e.Type == SimEventType.Died && e.Source != player.Id && names.ContainsKey(e.Source)) kills++;
                }
                if (player.Falls > falls)
                {
                    deaths += player.Falls - falls;
                    // Where it fell and what was about (B2b): to tell a new kind's doing from the run's luck.
                    var foes = world.Enemies.Where(e => !e.IsDead && Vec2.Distance(e.Position, player.Position) <= 8)
                        .GroupBy(e => e.Definition.Id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}");
                    fallPlaces.Add($"{tick / World.TicksPerSecond / 60}:{tick / World.TicksPerSecond % 60:00} {world.Biomes.At(player.Position)} (곁 {string.Join(", ", foes)})");
                }
                falls = player.Falls;
                if (tick % World.TicksPerSecond != 0) continue;
                // Each second: the state, the tension, and whether a fight is on.
                seconds[pace.State] = seconds.GetValueOrDefault(pace.State) + 1;
                tension.Add(pace.Tension);
                states.Add(pace.State);
                bool fighting = world.Tick - pace.LastFight < World.Ticks(World.CalmSeconds);
                if (fighting) fightingSeconds++;
                if (fighting && !wasFighting)
                {
                    fights++;
                    if (tick > 0) gaps.Add((world.Tick - quietFrom) / (float)World.TicksPerSecond);
                }
                if (!fighting && wasFighting) quietFrom = world.Tick;
                if (tick == 0 && !fighting) quietFrom = world.Tick;
                wasFighting = fighting;
            }

            // The curve: the highest tension in each half minute, and the state most of it was in.
            var curve = new StringBuilder();
            var under = new StringBuilder();
            for (int from = 0; from < tension.Count; from += Bucket)
            {
                float top = tension.Skip(from).Take(Bucket).Max();
                curve.Append(Bars[Math.Min(Bars.Length - 1, (int)MathF.Ceiling(top / World.MaxTension * (Bars.Length - 1)))]);
                var most = states.Skip(from).Take(Bucket).GroupBy(s => s).OrderByDescending(g => g.Count()).First().Key;
                under.Append(most switch { PaceState.Build => '·', PaceState.Peak => '▲', PaceState.Relax => '↘', _ => '_' });
            }
            Console.WriteLine($"### 시드 {seed}");
            Console.WriteLine();
            Console.WriteLine("```");
            Console.WriteLine("긴장도 " + curve);
            Console.WriteLine("상태   " + under + "   (· 쌓기, ▲ 정점, ↘ 내려오기, _ 쉬기; 한 칸 30초)");
            Console.WriteLine("```");
            if (fallPlaces.Count > 0) Console.WriteLine("쓰러진 곳: " + string.Join(" / ", fallPlaces));
            if (pace.RaidReaches.Count + pace.RaidMisses > 0)
                Console.WriteLine($"습격마다 울음소리 → 첫 닿음: {string.Join(" / ", pace.RaidReaches.Select(s => $"{s:0.0}초"))}{(pace.RaidMisses > 0 ? $" (못 닿음 {pace.RaidMisses}: 닿기 전에 쉬기가 되어 돌아갔거나 다 쓰러졌다)" : "")}");
            Console.WriteLine();
            int S(PaceState s) => seconds.GetValueOrDefault(s);
            return $"| {seed} | {S(PaceState.Build)} / {S(PaceState.Peak)} / {S(PaceState.Relax)} / {S(PaceState.Rest)} | {pace.Peaks} | {pace.Rests} | {pace.Sends} | {pace.Ambushes} | {pace.Raids} | {Reach(pace)} | {fights} "
                + $"| {(gaps.Count > 0 ? gaps.Average() : 0):0}초({(gaps.Count > 0 ? gaps.Max() : 0):0}) | {fightingSeconds * 100 / Math.Max(1, tension.Count)}% | {kills} | {deaths} | {taken} | {restHitsLoose} / {restHitsRooms} |";
        }
    }
}
