using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // A party of bots opens a void rift at an altar beside the spawn, tier after tier, and fights its waves the way
    // the fight bot does (Docs/Bosses.md, 공허 균열). Prints for each tier whether it was closed in time, how long it
    // took, how far it got and how hard it hit the bots, to set the tiers' numbers by.
    static class RiftSim
    {
        public static void Run(LootData loot, MonsterData monsters, TileData tiles, int maxTier, ulong seed, int players, string[] gear, ClassDefinition[] classes,
            bool dodge)
        {
            Console.WriteLine($"균열: seed = {seed}, 봇 {players}명, 장비 {string.Join(", ", gear)}, 클래스 {FightSim.ClassNames(classes)}, 피하기 {(dodge ? "켬" : "끔")}");
            Console.WriteLine("단계  닫음   걸린 시간  물결  사망  받은 피해  접사");
            for (int tier = 1; tier <= Math.Min(maxTier, World.MaxRiftTier); tier++) RunTier(loot, monsters, tiles, tier, seed, players, gear, classes, dodge);
        }

        static void RunTier(LootData loot, MonsterData monsters, TileData tiles, int tier, ulong seed, int players, string[] gear, ClassDefinition[] classes, bool dodge)
        {
            var generated = WorldGenerator.Generate(seed);
            var world = new World(generated.Map, generated.Spawn, monsters, seed + (ulong)tier, new WorldConfig { EnemyTarget = 0 }, loot, tiles, generated.Biomes);
            world.RestoreRiftTier(tier);
            // The altar on the floor tile nearest the spawn's right.
            int sx = TileMap.Cell(generated.Spawn.X), sy = TileMap.Cell(generated.Spawn.Y);
            var spot = Enumerable.Range(1, 6).SelectMany(r => new[] { (sx + r, sy), (sx - r, sy), (sx, sy + r), (sx, sy - r) })
                .First(t => world.Map[t.Item1, t.Item2] == TileType.Floor);
            world.Restore(new[] { new TileChange(spot.Item1, spot.Item2, TileType.RiftAltar) }, Array.Empty<(CropDefinition, int, int, long, long)>(), 1);

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
                bots.Add((player, new SkillBot(player, loot)));
            }
            var key = loot.FindMaterial(World.RiftKey);
            bots[0].Player.Inventory.AddMaterial(key);
            if (!world.OpenRift(bots[0].Player.Id)) throw new InvalidOperationException("The rift did not open.");
            var affixes = world.ActiveRift.Affixes;

            var toEnemies = new FlowField(world.Map);
            int deaths = 0, taken = 0, wave = 1;
            bool? cleared = null;
            long ticks = World.Ticks(World.RiftSeconds + 2);
            long tick = 0;
            for (; tick < ticks && cleared == null; tick++)
            {
                if (tick % 10 == 0) toEnemies.Build(world.Enemies.Select(e => e.Position), 64);
                foreach (var (player, skills) in bots)
                {
                    FightSim.Drive(world, player, toEnemies, dodge);
                    var nearest = world.Enemies.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                    skills.Use(world, nearest?.Position ?? player.Position, nearest != null && Vec2.Distance(nearest.Position, player.Position) < 8);
                }
                world.Step();
                if (world.ActiveRift != null) wave = world.ActiveRift.Wave;
                foreach (var e in world.TakeEvents())
                {
                    foreach (var (_, skills) in bots) skills.Count(e);
                    if (e.Type == SimEventType.Hit && bots.Any(b => b.Player.Id == e.Target)) taken += e.Amount;
                    if (e.Type == SimEventType.Died && bots.Any(b => b.Player.Id == e.Source)) deaths++;
                    if (e.Type == SimEventType.Rift && e.Amount != World.RiftOpened) cleared = e.Amount == World.RiftCleared;
                }
            }
            string waves = wave > World.RiftWaves ? "수호자" : $"{wave}/{World.RiftWaves}";
            Console.WriteLine($"{tier,3}   {(cleared == true ? "예" : "아니오"),-4} {tick / (double)World.TicksPerSecond,7:0}초  {waves,-5} {deaths,4}  {taken,8}   {World.AffixNames(affixes)}");
        }
    }
}
