using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // --fight --rooms [biome] (Docs/WorldRework.md 묶음 2-1): one bot in a world of rooms as the game makes it (the
    // generator's version 2, the game's size, the story's places and roads), set down beside the pack room nearest the
    // spawn (in the biome, or any). It fights what it meets as --fight does and, with nothing about, walks to the
    // nearest pack room not yet emptied today. Kills, experience and drops a minute, the hurt it took, and the seconds
    // each pack took from its alarm to its last fall. With --no-packs the same world brings its monsters one by one,
    // as before packs, for the numbers to set beside. With --camps (묶음 2-2) it goes from elite camp to elite camp
    // instead, and says how the elites went (EliteWatch). With --players n (B5a) n−1 more bots like it come along: they
    // fight what is near them and otherwise keep up with the first, so the packs come as for a party (World.Packs); the
    // experience is the first bot's (each near a kill gets all of it), the falls are all of theirs.
    static class RoomFightSim
    {
        public static void Run(BotSheets sheets, int seconds, ulong seed, string[] gear, ClassDefinition[] classes, bool dodge, string biomeName, bool camps = false,
            int players = 1, float reaction = 0.25f)
        {
            Biome? biome = biomeName == "any" ? (Biome?)null : (Biome)Enum.Parse(typeof(Biome), biomeName, ignoreCase: true);
            var host = new WorldHost(seed, sheets.Monsters, sheets.Loot, bosses: sheets.Bosses, classes: sheets.Classes, tiles: sheets.Tiles,
                guide: sheets.Guide, npcs: sheets.Npcs, config: new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.RoomVersion },
                worldConfig: new WorldConfig { Director = Program.Director }, story: sheets.Story);
            var world = host.World;
            var rooms = host.Generated.Rooms;
            var random = new GameRandom(seed, 99);
            var generator = new LootGenerator(sheets.Loot);
            var equipment = new Equipment();
            foreach (string id in gear)
            {
                var itemBase = sheets.Loot.FindBase(id);
                equipment.Equip(generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random));
            }
            var player = world.AddPlayer("bot", equipment, classes);
            var skills = new SkillBot(player, sheets.Loot);
            var companions = new List<(Player Player, SkillBot Skills, FlowField ToEnemies)>();
            for (int i = 1; i < players; i++)
            {
                var their = new Equipment();
                foreach (string id in gear)
                {
                    var itemBase = sheets.Loot.FindBase(id);
                    their.Equip(generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random));
                }
                var companion = world.AddPlayer("bot" + i, their, classes);
                companions.Add((companion, new SkillBot(companion, sheets.Loot), new FlowField(world.Map)));
            }
            var toLeader = new FlowField(world.Map);

            // The camps of "any" are the plain cave's: the stage a copper sword is for.
            bool InBiome(RoomNode r) => biome == null ? !camps || r.Biome == Biome.Cave : r.Biome == biome;
            var goalRole = camps ? RoomRole.Elite : RoomRole.Pack;
            var packRooms = rooms.Nodes.Where(n => n.Kind == RoomKind.Room && !n.Roadside && n.Role == goalRole && InBiome(n)).ToList();
            if (packRooms.Count == 0)
            {
                Console.WriteLine($"{biomeName}에 무리 방이 없습니다.");
                return;
            }
            // Beside the first pack room: in the nearest room next to it that is not one, so the fight starts with a walk in.
            var first = packRooms.OrderBy(r => Vec2.Distance(r.Center, world.Spawn)).First();
            var start = first.Edges.Select(i => rooms.Edges[i]).Select(e => rooms.Nodes[e.A == first.Index ? e.B : e.A])
                .Where(n => n.Role != RoomRole.Pack && n.Role != RoomRole.Elite).OrderBy(n => n.Depth).FirstOrDefault() ?? first;
            player.Position = TileMap.Center(start.AnchorX, start.AnchorY);
            foreach (var (companion, _, _) in companions) companion.Position = Collision.Resolve(world.Map, player.Position + new Vec2(0.8f, 0), World.PlayerRadius);
            // The zone levels of the biome's pack rooms (World.ZoneLevelAt), to set a dungeon's level beside (B4b).
            var zones = packRooms.Select(r => world.ZoneLevelAt(r.Center)).ToList();
            Console.WriteLine($"무리 방의 구역 레벨 {zones.Min()}~{zones.Max()} (가장 가까운 방 {world.ZoneLevelAt(first.Center)})");

            var toEnemies = new FlowField(world.Map);
            var toRoom = new FlowField(world.Map);
            var fromBot = new FlowField(world.Map);
            RoomNode goal = null;
            int nextLook = 0;
            int kills = 0, deaths = 0, taken = 0, drops = 0, items = 0, packsMet = 0, roomsWalked = 0;
            int startExperience = player.Experience;
            var takenBy = new Dictionary<string, int>();
            var killsBy = new Dictionary<string, int>();
            var names = new Dictionary<int, string>();
            var seenLoot = new HashSet<int>(world.GroundLoot.Select(l => l.Id));
            var alarmed = new Dictionary<int, long>();
            var packSeconds = new List<float>();
            var packSizes = new List<int>();
            var felled = new HashSet<int>();
            int largestCrowd = 0, idleTicks = 0;
            var emptied = new HashSet<int>();
            var elites = new EliteWatch();

            for (int tick = 0; tick < seconds * World.TicksPerSecond; tick++)
            {
                // A room whose packs all fell today (or which holds none) is emptied.
                foreach (var pack in world.Packs)
                    if (pack.Room != null && (pack.Cleared || pack.Definition == null) && world.Packs.Where(p => p.Room == pack.Room).All(p => p.Cleared || p.Definition == null))
                        emptied.Add(pack.Room.Index);
                // What it fights: those after it, and any within a few steps (one standing across the rock is walked round to
                // by the way to its room).
                var near = world.Enemies.Where(e => !e.Away && !e.IsDead && Vec2.Distance(e.Position, player.Position) < 16
                    && (e.State != EnemyState.Idle && !e.Returning || Vec2.Distance(e.Position, player.Position) < 5)).ToList();
                largestCrowd = Math.Max(largestCrowd, world.Enemies.Count(e => !e.IsDead && e.State != EnemyState.Idle && Vec2.Distance(e.Position, player.Position) < 10));
                if (near.Count > 0 || player.IsDead)
                {
                    if (tick % 10 == 0) toEnemies.Build(world.Enemies.Where(e => !e.Away && Vec2.Distance(e.Position, player.Position) < 24).Select(e => e.Position), 40);
                    FightSim.Drive(world, player, toEnemies, dodge, reaction);
                }
                else
                {
                    // On to the nearest pack room not yet emptied.
                    world.SetAttacking(player.Id, false);
                    if ((goal == null || emptied.Contains(goal.Index)) && tick >= nextLook)
                    {
                        // The nearest by the steps walked to it; with none, a look again in a few seconds.
                        fromBot.Build(new[] { player.Position }, 500);
                        goal = packRooms.Where(r => !emptied.Contains(r.Index) && fromBot.DistanceAt(r.AnchorX, r.AnchorY) != FlowField.Unreached)
                            .OrderBy(r => fromBot.DistanceAt(r.AnchorX, r.AnchorY)).FirstOrDefault();
                        if (goal != null)
                        {
                            toRoom.Build(new[] { TileMap.Center(goal.AnchorX, goal.AnchorY) }, 500);
                            roomsWalked++;
                        }
                        else nextLook = tick + 5 * World.TicksPerSecond;
                    }
                    var way = goal == null ? Vec2.Zero : toRoom.DirectionFrom(player.Position);
                    // There, or no way on from here: that room is done with.
                    if (goal != null && (way.LengthSquared < 1e-6f || Vec2.Distance(player.Position, TileMap.Center(goal.AnchorX, goal.AnchorY)) < 2)) emptied.Add(goal.Index);
                    var flee = dodge ? BossSim.Escape(world, player, World.Ticks(reaction)) : Vec2.Zero;
                    RollBot.Move(world, player, flee.LengthSquared > 0 ? flee : way, way, flee, dodge);
                    idleTicks++;
                }
                var nearest = near.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                skills.Use(world, nearest?.Position ?? player.Position, nearest != null && Vec2.Distance(nearest.Position, player.Position) < 8);
                // The others: what is after them or the first bot, else on after the first bot.
                if (companions.Count > 0 && tick % 10 == 0) toLeader.Build(new[] { player.Position }, 200);
                foreach (var (companion, theirSkills, theirEnemies) in companions)
                {
                    var theirNear = world.Enemies.Where(e => !e.Away && !e.IsDead && (Vec2.Distance(e.Position, companion.Position) < 16 || Vec2.Distance(e.Position, player.Position) < 10)
                        && (e.State != EnemyState.Idle && !e.Returning || Vec2.Distance(e.Position, companion.Position) < 5)).ToList();
                    if (theirNear.Count > 0 || companion.IsDead)
                    {
                        if (tick % 10 == 0) theirEnemies.Build(world.Enemies.Where(e => !e.Away && Vec2.Distance(e.Position, companion.Position) < 24).Select(e => e.Position), 40);
                        FightSim.Drive(world, companion, theirEnemies, dodge, reaction);
                    }
                    else
                    {
                        world.SetAttacking(companion.Id, false);
                        var way = Vec2.Distance(companion.Position, player.Position) > 2.5f ? toLeader.DirectionFrom(companion.Position) : Vec2.Zero;
                        var flee = dodge ? BossSim.Escape(world, companion, World.Ticks(reaction)) : Vec2.Zero;
                        RollBot.Move(world, companion, flee.LengthSquared > 0 ? flee : way, way, flee, dodge);
                    }
                    var theirNearest = theirNear.OrderBy(e => Vec2.Distance(e.Position, companion.Position)).FirstOrDefault();
                    theirSkills.Use(world, theirNearest?.Position ?? companion.Position, theirNearest != null && Vec2.Distance(theirNearest.Position, companion.Position) < 8);
                }
                foreach (var enemy in world.Enemies) names[enemy.Id] = enemy.Definition.Name;
                var packOf = world.Enemies.Where(e => e.PackId != 0).ToDictionary(e => e.Id, e => e.PackId);
                world.Step();
                var events = world.TakeEvents();
                elites.Look(world, player, events);
                foreach (var e in events)
                {
                    skills.Count(e);
                    foreach (var (_, theirSkills, _) in companions) theirSkills.Count(e);
                    if (companions.Any(c => c.Player.Id == e.Source) && e.Type == SimEventType.Died) deaths++;
                    if (e.Type == SimEventType.Alerted && packOf.TryGetValue(e.Source, out int alarm) && !alarmed.ContainsKey(alarm))
                    {
                        alarmed[alarm] = world.Tick;
                        packsMet++;
                    }
                    if (e.Type == SimEventType.Hit && e.Target == player.Id)
                    {
                        taken += e.Amount;
                        if (names.TryGetValue(e.Source, out string hitter)) takenBy[hitter] = takenBy.GetValueOrDefault(hitter) + e.Amount;
                    }
                    if (e.Type == SimEventType.Died && e.Source == player.Id) deaths++;
                    else if (e.Type == SimEventType.Died && names.TryGetValue(e.Source, out string killed))
                    {
                        kills++;
                        killsBy[killed] = killsBy.GetValueOrDefault(killed) + 1;
                    }
                }
                foreach (var pack in world.Packs)
                {
                    if (!alarmed.TryGetValue(pack.Id, out long from) || felled.Contains(pack.Id) || world.MembersOf(pack).Any()) continue;
                    felled.Add(pack.Id);
                    packSeconds.Add((world.Tick - from) / (float)World.TicksPerSecond);
                    packSizes.Add(pack.Size);
                }
                foreach (var loot in world.GroundLoot)
                {
                    if (!seenLoot.Add(loot.Id)) continue;
                    drops++;
                    if (loot.Item != null) items++;
                }
            }

            float minutes = seconds / 60f;
            int experience = player.Experience - startExperience;
            Console.WriteLine($"seed = {seed}, {seconds}초, 방으로 된 지도({biomeName}{(camps ? ", 정예 야영지로" : "")}), 무리: {(sheets.Monsters.Packs != null ? "있음" : "없음(하나씩)")}, 장비: {string.Join(", ", gear)}, "
                + $"클래스: {FightSim.ClassNames(classes)}, 피하기: {(dodge ? "켬" : "끔")}" + (players > 1 ? $", 사람 {players}(경험치는 한 사람 것)" : "") + (reaction != 0.25f ? $", 반응 {reaction}초" : ""));
            Console.WriteLine($"처치 {kills} (분당 {kills / minutes:0.0}), 경험치 {experience} (분당 {experience / minutes:0.0}), 떨어진 것 {drops} (분당 {drops / minutes:0.0}, 장비 {items}), 사망 {deaths}");
            Console.WriteLine($"받은 피해 {taken} (초당 {taken / (float)seconds:0.0}), 최대 생명력 {player.Stats.MaxHealth:0}, 레벨 {player.Level}, 마신 물약 {skills.Drunk}개");
            Console.WriteLine($"만난 무리 {packsMet}, 다 잡은 무리 {packSeconds.Count} (마리 {(packSizes.Count > 0 ? packSizes.Average() : 0):0.0}, 걸린 초 평균 {(packSeconds.Count > 0 ? packSeconds.Average() : 0):0.0}, "
                + $"가장 긴 {(packSeconds.Count > 0 ? packSeconds.Max() : 0):0.0}), 한 번에 쫓아온 가장 많은 수 {largestCrowd}, 걸은 시간 {idleTicks / (float)World.TicksPerSecond:0}초, 다음 방으로 {roomsWalked}번");
            Console.WriteLine(elites.Summary(seconds * World.TicksPerSecond));
            Console.WriteLine("  몬스터별 처치: " + string.Join(", ", killsBy.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}")));
            Console.WriteLine("  받은 피해: " + string.Join(", ", takenBy.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}")));
        }
    }
}
