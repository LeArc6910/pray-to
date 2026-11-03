using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // Bots defend a ward stone by the spawn level after level (Docs/Defense.md), with or without turrets full of
    // ammo around it. Prints whether the stone held, how much of it was left, and how long it took.
    static class DefenseSim
    {
        public static void Run(LootData loot, MonsterData monsters, TileData tiles, int maxLevel, ulong seed, int players, string[] gear, ClassDefinition[] classes,
            bool dodge, int turrets)
        {
            Console.WriteLine($"기지 방어: seed = {seed}, 봇 {players}명, 장비 {string.Join(", ", gear)}, 클래스 {FightSim.ClassNames(classes)}, 포탑 {turrets}개");
            Console.WriteLine("단계  지킴   걸린 시간  수호석  사망  포탑이 잡은 적");
            for (int level = 1; level <= Math.Min(maxLevel, World.MaxDefenseLevel); level++) RunLevel(loot, monsters, tiles, level, seed, players, gear, classes, dodge, turrets);
        }

        static void RunLevel(LootData loot, MonsterData monsters, TileData tiles, int level, ulong seed, int players, string[] gear, ClassDefinition[] classes, bool dodge,
            int turrets)
        {
            var generated = WorldGenerator.Generate(seed);
            var world = new World(generated.Map, generated.Spawn, monsters, seed + (ulong)level, new WorldConfig { EnemyTarget = 0 }, loot, tiles, generated.Biomes);
            world.RestoreDefense(level, 0);
            int sx = TileMap.Cell(generated.Spawn.X), sy = TileMap.Cell(generated.Spawn.Y);
            var floor = Enumerable.Range(1, 8).SelectMany(r => new[] { (sx + r, sy), (sx - r, sy), (sx, sy + r), (sx, sy - r), (sx + r, sy + r), (sx - r, sy - r) })
                .Where(t => world.Map[t.Item1, t.Item2] == TileType.Floor).ToList();
            var stone = floor[0];
            var laid = new List<TileChange> { new TileChange(stone.Item1, stone.Item2, TileType.WardStone) };
            // Turrets on the floor tiles nearest the stone, past its four sides.
            var turretSpots = floor.Skip(1).Where(t => Math.Abs(t.Item1 - stone.Item1) + Math.Abs(t.Item2 - stone.Item2) >= 2).Take(turrets).ToList();
            laid.AddRange(turretSpots.Select(t => new TileChange(t.Item1, t.Item2, TileType.Turret)));
            world.Restore(laid, Array.Empty<(CropDefinition, int, int, long, long)>(), 1);
            var ammo = loot.FindMaterial(World.Ammo);
            var random = new GameRandom(seed, 99);
            var generator = new LootGenerator(loot);
            var bots = new List<(Player Player, SkillBot Skills)>();
            for (int i = 0; i < players; i++)
            {
                var equipment = new Equipment();
                foreach (string id in gear)
                {
                    var itemBase = loot.FindBase(id);
                    equipment.Equip(generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random));
                }
                var player = world.AddPlayer("bot" + (i + 1), equipment, classes);
                player.Position = TileMap.Center(stone.Item1, stone.Item2 - 1);
                bots.Add((player, new SkillBot(player, loot)));
            }
            foreach (var spot in turretSpots)
            {
                bots[0].Player.Position = TileMap.Center(spot.Item1, spot.Item2 - 1);
                bots[0].Player.Facing = new Vec2(0, 1);
                bots[0].Player.Inventory.AddMaterial(ammo, 20);
                world.LoadMachine(bots[0].Player.Id, ammo, 20);
            }
            bots[0].Player.Position = TileMap.Center(stone.Item1, stone.Item2 - 1);
            if (!world.StartDefense(bots[0].Player.Id)) throw new InvalidOperationException("The defense did not start.");
            var defense = world.ActiveDefense;

            var toEnemies = new FlowField(world.Map);
            int deaths = 0, turretKills = 0;
            var lastHit = new Dictionary<int, int>();
            bool? won = null;
            long tick = 0;
            for (; tick < World.Ticks(600) && won == null; tick++)
            {
                if (tick % 10 == 0) toEnemies.Build(world.Enemies.Select(e => e.Position), 64);
                foreach (var (player, skills) in bots)
                {
                    FightSim.Drive(world, player, toEnemies, dodge);
                    var nearest = world.Enemies.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                    skills.Use(world, nearest?.Position ?? player.Position, nearest != null && Vec2.Distance(nearest.Position, player.Position) < 8);
                }
                world.Step();
                foreach (var e in world.TakeEvents())
                {
                    foreach (var (_, skills) in bots) skills.Count(e);
                    if (e.Type == SimEventType.Hit) lastHit[e.Target] = e.Source;
                    if (e.Type == SimEventType.Died && bots.Any(b => b.Player.Id == e.Source)) deaths++;
                    else if (e.Type == SimEventType.Died && lastHit.GetValueOrDefault(e.Source, -1) == 0) turretKills++;
                    if (e.Type == SimEventType.Defense && e.Amount != World.DefenseStarted) won = e.Amount == World.DefenseWon;
                }
            }
            int stoneLeft = (int)Math.Ceiling(defense.StoneHealth / defense.MaxStoneHealth * 100);
            Console.WriteLine($"{level,3}   {(won == true ? "예" : "아니오"),-4} {tick / (double)World.TicksPerSecond,7:0}초  {stoneLeft,5}%  {deaths,4}  {turretKills,6}");
        }
    }
}
