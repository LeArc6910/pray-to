using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // --dungeon <id> <seeds> (Docs/WorldRework.md 묶음 5b, a first cut of 5c's): bots go into a dungeon of dungeons.csv by
    // its biome's gate in a world of the game's size, walk its spine room by room (each side room right after the pack
    // room it hangs off; a hidden room's crack is left), fight what comes as --fight does, take the key, the master and
    // the end chest. Per seed: how long the run took to its end chest, how often a bot fell, the seconds each room took
    // from its pack coming to its last fall, and what the run gave in coins as the merchants pay for it (coins, the
    // gear's sale price, on the floor and from the chests) and experience, a minute. --players n sends n bots (they go
    // together, each driven alike).
    static class DungeonSim
    {
        const int MostSeconds = 25 * 60;

        public static void Run(BotSheets sheets, string id, IEnumerable<ulong> seeds, int players, string[] gear, ClassDefinition[] classes, bool dodge)
        {
            var definition = sheets.Tiles.Terrain.Dungeons.Find(id);
            if (definition == null)
            {
                Console.WriteLine($"dungeons.csv에 '{id}'가 없습니다.");
                return;
            }
            var biome = definition.Biome ?? (definition.Cellar ? Biome.Cave : Biome.Sand);
            Console.WriteLine($"== {definition.Name} ({definition.Id}, {biome}), 사람 {players}, 장비 {string.Join(", ", gear)}, 클래스 {FightSim.ClassNames(classes)}, 레벨 {SkillBot.Level?.ToString() ?? "-"} ==");
            Console.WriteLine("시드  방(글자)            완주   쓰러짐  금화값  분당  경험치  분당  장비(희귀+)  방마다 걸린 초");
            var totals = new List<(float Seconds, int Deaths, int Value, int Experience, bool Done)>();
            foreach (ulong seed in seeds)
            {
                var result = One(sheets, definition, biome, seed, players, gear, classes, dodge);
                totals.Add((result.Seconds, result.Deaths, result.Value, result.Experience, result.Done));
            }
            var done = totals.Where(t => t.Done).ToList();
            if (done.Count > 0)
                Console.WriteLine($"평균(끝낸 {done.Count}/{totals.Count}): {done.Average(t => t.Seconds) / 60:0.0}분, 쓰러짐 {done.Average(t => t.Deaths):0.0}, 금화값 {done.Average(t => t.Value):0} (분당 {done.Average(t => t.Value / Math.Max(1, t.Seconds / 60)):0}), "
                    + $"경험치 {done.Average(t => t.Experience):0} (분당 {done.Average(t => t.Experience / Math.Max(1, t.Seconds / 60)):0})");
        }

        static (float Seconds, int Deaths, int Value, int Experience, bool Done) One(BotSheets sheets, DungeonDefinition definition, Biome biome, ulong seed, int players,
            string[] gear, ClassDefinition[] classes, bool dodge)
        {
            var host = new WorldHost(seed, sheets.Monsters, sheets.Loot, bosses: sheets.Bosses, classes: sheets.Classes, tiles: sheets.Tiles,
                guide: sheets.Guide, npcs: sheets.Npcs, config: new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.RoomVersion },
                worldConfig: new WorldConfig { Director = Program.Director }, story: sheets.Story);
            var world = host.World;
            // Loot at the first boss's step for anything past copper, as the gear says the player has gone that far.
            var firstBoss = sheets.Bosses?.Bosses.FirstOrDefault(b => b.Place == null);
            if (firstBoss != null && !gear.All(g => g.StartsWith("copper"))) world.Restore(Array.Empty<TileChange>(), Array.Empty<(CropDefinition, int, int, long, long)>(), firstBoss.DropLevel);
            var gate = world.Gates.FirstOrDefault(g => !g.Cellar && g.Biome == biome) ?? world.Gates.First(g => !g.Cellar);
            world.ChooseDungeon(gate.Index, definition.Id);
            var random = new GameRandom(seed, 97);
            var generator = new LootGenerator(sheets.Loot);
            var bots = new List<(Player Player, SkillBot Skills)>();
            for (int i = 0; i < players; i++)
            {
                var equipment = new Equipment();
                foreach (string g in gear)
                {
                    var itemBase = sheets.Loot.FindBase(g);
                    equipment.Equip(generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random));
                }
                var player = world.AddPlayer("bot" + i, equipment, classes);
                bots.Add((player, new SkillBot(player, sheets.Loot)));
                player.Position = Collision.Resolve(world.Map, gate.Center, World.PlayerRadius);
            }
            foreach (var (player, _) in bots) world.EnterDungeon(player.Id, gate.Index);
            for (int i = 0; i < World.Ticks(World.EnterSeconds + 0.2f); i++) world.Step();
            var dungeon = world.DungeonOf(gate);
            var layout = dungeon.Layout;
            int startExperience = bots.Sum(b => b.Player.Experience);
            int startValue = bots.Sum(b => Value(b.Player));

            // The goals: the spine's rooms in order, each side room after its pack room (not a hidden one), the end chest.
            var goals = new List<DungeonRoom>();
            foreach (var room in layout.Nodes.Where(n => !n.Branch).Skip(1))
            {
                goals.Add(room);
                goals.AddRange(layout.Nodes.Where(n => n.Branch && n.Parent == room.Index && n.Letter != 'H'));
            }
            var toGoal = new FlowField(world.Map);
            var toEnemies = new FlowField(world.Map);
            var toLeft = new FlowField(world.Map);
            int goal = 0, deaths = 0, felledFloor = 0;
            DungeonRoom builtFor = null;
            bool builtOpen = false;
            var woke = new Dictionary<int, long>();
            var roomSeconds = new List<(char Letter, float Seconds)>();
            var seenLoot = new HashSet<int>();
            int floorValue = 0, rares = 0;
            for (int tick = 0; tick < MostSeconds * World.TicksPerSecond && dungeon.State != DungeonState.Done; tick++)
            {
                while (goal < goals.Count && Reached(world, dungeon, goals[goal], bots.Select(b => b.Player))) goal++;
                var room = goal < goals.Count ? goals[goal] : null;
                // The way: to its middle, or, to a locked room still shut, to the grate.
                bool open = room == null || room.Letter != 'L' || dungeon.Has(DungeonFlags.KeyDoorOpen);
                if (room != null && (room != builtFor || open != builtOpen || tick % (2 * World.TicksPerSecond) == 0))
                {
                    var target = room.Letter == 'X' ? layout.Things.First(t => t.End).Center + new Vec2(0, -1.2f)
                        : room.Letter == 'T' ? layout.Things.First(t => t.Room == room.Index).Center + new Vec2(0, -1.2f)
                        : open ? room.Center : Before(layout, room);
                    toGoal.Build(new[] { Collision.Resolve(world.Map, target, World.PlayerRadius) }, 400);
                    (builtFor, builtOpen) = (room, open);
                }
                foreach (var (player, skills) in bots)
                {
                    var near = world.Enemies.Where(e => !e.Away && !e.IsDead && world.DungeonAt(e.Position) == dungeon && Vec2.Distance(e.Position, player.Position) < 14
                        && (e.State != EnemyState.Idle && !e.Returning || Vec2.Distance(e.Position, player.Position) < 6)).ToList();
                    // The last of the goal room's pack standing idle at its edge (a split slime, a rat out of the light),
                    // once the bot stands in the room: gone after, so the room is cleared.
                    var left = near.Count > 0 || room == null || !DungeonSheet.HasPack(room.Letter) || !dungeon.RoomWoken(room.Index) || !room.Contains(player.Position, 1)
                        ? new List<Enemy>()
                        : world.Enemies.Where(e => !e.Away && !e.IsDead && !e.Buried && world.DungeonAt(e.Position) == dungeon && room.Contains(e.Position, 4)).ToList();
                    if (near.Count > 0 || player.IsDead)
                    {
                        if (tick % 10 == 0) toEnemies.Build(world.Enemies.Where(e => !e.Away && world.DungeonAt(e.Position) == dungeon && Vec2.Distance(e.Position, player.Position) < 24).Select(e => e.Position), 40);
                        FightSim.Drive(world, player, toEnemies, dodge);
                    }
                    else if (left.Count > 0)
                    {
                        world.SetAttacking(player.Id, false);
                        if (tick % 10 == 0) toLeft.Build(left.Select(e => e.Position), 60);
                        var way = toLeft.DirectionFrom(player.Position);
                        RollBot.Move(world, player, way, way, Vec2.Zero, dodge);
                    }
                    else
                    {
                        world.SetAttacking(player.Id, false);
                        var way = toGoal.DirectionFrom(player.Position);
                        RollBot.Move(world, player, way, way, Vec2.Zero, dodge);
                        // At a chest: open it.
                        if (World.ThingNear(dungeon, player.Position) is DungeonThing thing && thing.Kind == DungeonThingKind.Chest) world.UseDungeonThing(player.Id, thing.Index);
                    }
                    var nearest = near.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                    if (Environment.GetEnvironmentVariable("DUNGEON_DUMP") == "1" && tick % (20 * World.TicksPerSecond) == 0)
                        Console.WriteLine($"        [{tick / World.TicksPerSecond}s] 봇 ({player.Position.X:0.0},{player.Position.Y:0.0}) 곁 {near.Count} 목표 {room?.Letter} 가장 가까운 {nearest?.Definition.Id} {(nearest == null ? 0 : Vec2.Distance(nearest.Position, player.Position)):0.0} 흐름 {toGoal.DirectionFrom(player.Position)} 생명력 {player.Health:0} 아래 {world.Map[TileMap.Cell(player.Position.X), TileMap.Cell(player.Position.Y) - 1]} 한걸음 {PlayerMotor.Step(world.Map, player.Position, toGoal.DirectionFrom(player.Position), player.MoveSpeed, world.DoorsFor(player))} 속도 {player.MoveSpeed}");
                    skills.Use(world, nearest?.Position ?? player.Position, nearest != null && Vec2.Distance(nearest.Position, player.Position) < 8);
                }
                world.Step();
                foreach (var e in world.TakeEvents())
                {
                    foreach (var (player, skills) in bots) skills.Count(e);
                    if (e.Type == SimEventType.Died && bots.Any(b => b.Player.Id == e.Source)) deaths++;
                }
                foreach (var node in layout.Nodes)
                {
                    if (dungeon.RoomWoken(node.Index) && !woke.ContainsKey(node.Index)) woke[node.Index] = world.Tick;
                    if (dungeon.RoomCleared(node.Index) && woke.TryGetValue(node.Index, out long from) && _done.Add((seed, node.Index)))
                    {
                        roomSeconds.Add((node.Letter, (world.Tick - from) / (float)World.TicksPerSecond));
                    }
                }
                foreach (var loot in world.GroundLoot.Where(l => world.DungeonAt(l.Position) == dungeon))
                {
                    if (!seenLoot.Add(loot.Id)) continue;
                    felledFloor++;
                    if (loot.Item != null)
                    {
                        floorValue += World.ItemValue(loot.Item, 0);
                        if (loot.Item.Rarity >= Rarity.Rare) rares++;
                    }
                    if (loot.Material?.Id == "coin") floorValue += loot.Count;
                }
            }
            bool done = dungeon.State == DungeonState.Done;
            float seconds = done ? dungeon.ClearSeconds : MostSeconds;
            int value = bots.Sum(b => Value(b.Player)) - startValue + floorValue;
            rares += bots.Sum(b => b.Player.Inventory.Items.Count(i => i.Rarity >= Rarity.Rare));
            int experience = bots.Sum(b => b.Player.Experience) - startExperience;
            string letters = new string(layout.Nodes.Select(n => n.Letter).ToArray());
            string rooms = string.Join(" ", roomSeconds.Select(r => $"{r.Letter}{r.Seconds:0}"));
            float minutes = Math.Max(1, seconds) / 60f;
            Console.WriteLine($"{seed,4}  {letters,-18} {(done ? $"{(int)seconds / 60}:{(int)seconds % 60:00}" : "못 끝냄"),6}  {deaths,6}  {value,6}  {value / minutes,4:0}  {experience,6}  {experience / minutes,4:0}  {rares,9}    {rooms}"
                + (done ? "" : $"  (멈춘 곳: {(goal < goals.Count ? goals[goal].Name : "상자")})"));
            if (!done)
            {
                // What was left: the living monsters of the dungeon, and where the bots stood.
                foreach (var e in world.Enemies.Where(e => !e.IsDead && world.DungeonAt(e.Position) == dungeon))
                    Console.WriteLine($"        남은 것: {e.Definition.Id} ({e.Position.X:0.0}, {e.Position.Y:0.0}) {e.State}{(e.Away ? " 멀리" : "")} 생명력 {e.Health:0}/{e.MaxHealth:0}");
                foreach (var (player, _) in bots) Console.WriteLine($"        봇: ({player.Position.X:0.0}, {player.Position.Y:0.0}){(player.IsDead ? " 쓰러짐" : "")}");
                if (goal < goals.Count) Console.WriteLine($"        방 상자: {goals[goal].Box}");
                if (Environment.GetEnvironmentVariable("DUNGEON_DUMP") == "1" && bots.Count > 0)
                {
                    int bx = TileMap.Cell(bots[0].Player.Position.X), by = TileMap.Cell(bots[0].Player.Position.Y);
                    for (int y = by + 6; y >= by - 30; y--)
                    {
                        var line = new System.Text.StringBuilder("        ");
                        for (int x = bx - 20; x <= bx + 20; x++)
                        {
                            char c = world.Map.IsSolid(x, y) ? '#' : '.';
                            if (x == bx && y == by) c = '@';
                            foreach (var e in world.Enemies) if (!e.IsDead && TileMap.Cell(e.Position.X) == x && TileMap.Cell(e.Position.Y) == y) c = 'm';
                            line.Append(c);
                        }
                        Console.WriteLine(line);
                    }
                }
            }
            return (seconds, deaths, value, experience, done);
        }

        static readonly HashSet<(ulong, int)> _done = new HashSet<(ulong, int)>();

        // A goal room is behind the bots: its pack fell (a pack room), its chest opened (treasure, the end), or a bot stood
        // in it (any other).
        static bool Reached(World world, DungeonInstance dungeon, DungeonRoom room, IEnumerable<Player> players)
        {
            if (DungeonSheet.HasPack(room.Letter)) return dungeon.RoomCleared(room.Index);
            var chest = dungeon.Layout.Things.FirstOrDefault(t => t.Room == room.Index && t.Kind == DungeonThingKind.Chest);
            if (chest != null) return dungeon.ThingUsed(chest.Index);
            return players.Any(p => !p.IsDead && room.Contains(p.Position, -1));
        }

        // Just outside a locked room's grate, on the way it is come to from.
        static Vec2 Before(DungeonLayout layout, DungeonRoom room)
        {
            var (x, y) = layout.KeyDoor[layout.KeyDoor.Count / 2];
            var from = layout.Nodes[room.Parent].Center;
            var door = TileMap.Center(x, y);
            return door + (from - door).Normalized * 2f;
        }

        // What a player holds in coins as the merchants pay: their coins and their gear's sale price.
        static int Value(Player player)
        {
            int coins = player.Inventory.Materials.Where(m => m.Key.Id == "coin").Sum(m => m.Value);
            return coins + player.Inventory.Items.Sum(i => World.ItemValue(i, 0));
        }
    }
}
