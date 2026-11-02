using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // A plot looked after by machines for the given minutes of play (Docs/Time.md): eight crops on soil around a
    // sprinkler, which keeps them from drying, with a harvester reaping the three rows and a chest behind it. Crops
    // grow by real time only while wet, so this is also what a player gets who waters the plot whenever it needs
    // it; the machines spare them the walking. Prints the harvest per hour of play.
    static class FarmSim
    {
        public static void Run(LootData loot, TileData tiles, MonsterData monsters, int minutes, string cropId)
        {
            var crop = tiles.Crops.FirstOrDefault(c => c.Id == cropId);
            if (crop == null)
            {
                Console.Error.WriteLine($"없는 작물: {cropId} (있는 것: {string.Join(", ", tiles.Crops.Select(c => c.Id))})");
                Environment.Exit(1);
            }

            var map = new TileMap(16, 16, TileType.Floor);
            for (int i = 0; i < 16; i++)
            {
                map[i, 0] = map[i, 15] = map[0, i] = map[15, i] = TileType.Wall;
            }
            // The crop's own biome everywhere, so it may grow here.
            var biome = crop.Biomes.Count > 0 ? crop.Biomes[0] : Biome.Cave;
            var world = new World(map, TileMap.Center(12, 12), monsters, 1, new WorldConfig { EnemyTarget = 0 }, loot, tiles,
                BiomeMap.Of(map.Width, map.Height, (x, y) => biome));
            world.AddPlayer("farmer", null);

            // Harvester at (5, 8) facing right: rows x 6..8, y 7..9, a sprinkler in the middle of them.
            var patch = new List<(int X, int Y)>();
            for (int x = 6; x <= 8; x++)
            {
                for (int y = 7; y <= 9; y++)
                {
                    if (x != 7 || y != 8) patch.Add((x, y));
                }
            }
            var laid = patch.Select(t => new TileChange(t.X, t.Y, TileType.Soil))
                .Append(new TileChange(7, 8, TileType.Sprinkler))
                .Append(new TileChange(5, 8, TileType.Harvester))
                .Append(new TileChange(4, 8, TileType.Chest))
                .ToList();
            // A new machine faces right (Machine.Direction 0).
            world.Restore(laid, patch.Select(t => (crop, t.X, t.Y, World.SoilHeadStart(crop), 0L)), 1);

            long ticks = (long)minutes * 60 * World.TicksPerSecond;
            int harvests = 0;
            for (long tick = 0; tick < ticks; tick++)
            {
                world.Step();
                harvests += world.TakeEvents().Count(e => e.Type == SimEventType.Harvested);
            }

            var chest = world.ChestAt(4, 8);
            float hours = minutes / 60f;
            int got = crop.HarvestMaterial != null ? chest.CountOf(crop.HarvestMaterial) : 0;
            int seeds = chest.CountOf(crop.Seed);
            Console.WriteLine($"{crop.Name} ({crop.BiomeNames}), {crop.GrowDays}일 = 물을 준 채로 {crop.GrowDays * Calendar.DaySeconds / 60:0}분, 밭이면 하루 덜");
            Console.WriteLine($"밭 {patch.Count}칸, 스프링클러와 수확기로 {minutes}분");
            Console.WriteLine($"  거둔 횟수 {harvests}번, {crop.HarvestName} {got}개, 남은 씨앗 {seeds}개");
            Console.WriteLine($"  한 시간에 {crop.HarvestName} {got / hours:0.#}개, 한 칸이 한 시간에 {got / hours / patch.Count:0.##}개");

            // What it cooks into, by the recipes that use it.
            if (crop.HarvestMaterial == null) return;
            foreach (var recipe in loot.Recipes.Where(r => r.Consumable != null && r.Ingredients.Any(i => i.Material == crop.HarvestMaterial)))
            {
                int need = recipe.Ingredients.First(i => i.Material == crop.HarvestMaterial).Count;
                float dishes = got / hours / need * recipe.Count;
                var food = recipe.Consumable;
                string buff = food.BuffSeconds > 0 ? $", 버프 {dishes * food.BuffSeconds / 60:0.#}분어치" : "";
                string others = recipe.Ingredients.Count > 1 ? $" (다른 재료: {string.Join(", ", recipe.Ingredients.Where(i => i.Material != crop.HarvestMaterial).Select(i => $"{i.Material.Name} {i.Count}"))})" : "";
                Console.WriteLine($"  → {food.Name} 한 시간에 {dishes:0.#}개{buff}{others}");
            }
        }
    }
}
