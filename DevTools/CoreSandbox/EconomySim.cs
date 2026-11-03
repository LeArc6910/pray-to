using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // Coins an hour of play from each way of earning them (Docs/Npcs.md), worked out from the sheets and the rules:
    // a tile of crops kept wet, fishing by hand, a coop, a crab pot, and gear from hunting. Then what the shop's
    // dearer things cost in hours of each. Friendship's better prices are left out (no hearts).
    static class EconomySim
    {
        const float Minute = 60f * World.TicksPerSecond;

        public static void Run(LootData loot, TileData tiles, NpcData npcs)
        {
            // The best anyone pays, and the cheapest anyone asks.
            int Sell(object thing) => npcs.Shop.Where(e => e.Thing == thing).Select(e => e.Sell).DefaultIfEmpty(0).Max();
            int Buy(string id) => npcs.Shop.Where(e => (e.Material?.Id ?? e.Consumable?.Id) == id && e.Buy > 0).Select(e => e.Buy).DefaultIfEmpty(0).Min();
            // What one of a material fetches at best: as it is, or made into something by a keg, a jar or the furnace.
            (float Value, string How) Best(MaterialDefinition material)
            {
                (float, string) best = (Sell(material), "그대로");
                foreach (var recipe in loot.Recipes.Where(r => r.Item == null && r.Ingredients.Count == 1 && r.Ingredients[0].Material == material))
                {
                    float each = Sell((object)recipe.Material ?? recipe.Consumable) * recipe.Count / (float)recipe.Ingredients[0].Count;
                    if (each > best.Item1) best = (each, recipe.Name);
                }
                return best;
            }

            Console.WriteLine("== 농사: 밭 한 칸, 물이 늘 있을 때 (스프링클러) ==");
            Console.WriteLine("작물              한 번(분)  칸·시간당  그대로 금화  가공하면 금화  (무엇으로)  씨앗 값");
            foreach (var crop in tiles.Crops)
            {
                long cycle = crop.Regrows ? crop.RegrowTicks : crop.GrowTicks - World.SoilHeadStart(crop);
                float perHour = crop.Count * Minute * 60 / cycle;
                float raw = perHour * Sell(crop.HarvestMaterial);
                var (value, how) = Best(crop.HarvestMaterial);
                Console.WriteLine($"{Pad(crop.Name, 16)} {cycle / Minute,7:0}   {perHour,7:0.0}   {raw,9:0}   {perHour * value,11:0}   {Pad(how, 10)} {Buy(crop.Seed.Id),5}{(crop.Regrows ? " (다시 자람)" : "")}");
            }

            Console.WriteLine();
            Console.WriteLine("== 낚시: 손으로, 한 마리마다 기다림 평균 + 게이지가 가운데 오기까지 + 0.5초 ==");
            float wait = (World.BiteMinSeconds + World.BiteMaxSeconds) / 2;
            foreach (var group in loot.Materials.Where(m => m.Fish != null).GroupBy(m => m.Fish.Value))
            {
                foreach (bool night in new[] { false, true })
                {
                    var fish = group.Where(m => !m.FishAtNight || night).ToList();
                    if (fish.Count == 0 || night && fish.All(m => !m.FishAtNight)) continue;
                    float seconds = wait + (float)fish.Average(m => World.CenterTicks(World.Difficulty(m))) / World.TicksPerSecond + 0.5f;
                    float perHour = 3600 / seconds * 1.12f;
                    float price = (float)fish.Average(m => Best(m).Value);
                    Console.WriteLine($"{Pad(BiomeMap.Name(group.Key) + (night ? " 밤" : " 낮"), 14)} 한 마리 {seconds,4:0.0}초, 시간당 {perHour,4:0}마리 × 평균 {price,5:0.0} = {perHour * price,6:0} 금화");
                }
            }

            Console.WriteLine();
            Console.WriteLine("== 닭장과 통발 ==");
            var coop = tiles.Machine(TileType.Coop);
            float eggsPerHour = 3600f / coop.Seconds;
            var egg = loot.FindMaterial(World.Egg);
            float feedCost = Buy(World.Feed);
            Console.WriteLine($"닭 한 마리: 시간당 달걀 {eggsPerHour:0.#}개 = 그대로 {eggsPerHour * Sell(egg):0} / 가공 {eggsPerHour * Best(egg).Value:0} ({Best(egg).How}) 금화, 사료 값 {eggsPerHour * feedCost:0}. 닭 값 {Buy(World.Chicken)}");
            var pot = tiles.Machine(TileType.CrabPot);
            var catches = loot.Materials.Where(m => m.PotCatch).ToList();
            float potPerHour = 3600f / pot.Seconds;
            Console.WriteLine($"통발 하나: 시간당 {potPerHour:0.#}개 × 평균 {catches.Average(m => Best(m).Value):0.0} = {potPerHour * catches.Average(m => Best(m).Value):0} 금화, 미끼 값 {potPerHour * Buy(World.Bait):0}");

            Console.WriteLine();
            Console.WriteLine("== 사냥: 장비 하나를 팔면 (그 레벨의 드랍 2000개 평균) ==");
            var generator = new LootGenerator(loot);
            var random = new GameRandom(7);
            foreach (int level in new[] { 5, 10, 20, 30, 40 })
            {
                var values = Enumerable.Range(0, 2000).Select(_ => World.ItemValue(generator.RollItem(level, random), 0)).ToList();
                Console.WriteLine($"레벨 {level,2}: 평균 {values.Average(),5:0.0} 금화 (가장 비싼 것 {values.Max()})");
            }

            Console.WriteLine();
            Console.WriteLine("== 비싼 것을 사려면 ==");
            float berryFarm = 8 * tiles.Crops.Where(c => c.Id == "cave_berry").Select(c => c.Count * Minute * 60 / c.RegrowTicks * Best(c.HarvestMaterial).Value).FirstOrDefault();
            float caveFishing = 3600 / (wait + World.CenterTicks(1) / (float)World.TicksPerSecond + 0.5f) * 1.12f
                * (float)loot.Materials.Where(m => m.Fish == Biome.Cave && !m.FishAtNight).Average(m => Best(m).Value);
            Console.WriteLine($"(딸기 밭 8칸을 술통으로: 시간당 {berryFarm:0}, 흙 동굴 낮 낚시: 시간당 {caveFishing:0})");
            foreach (var entry in npcs.Shop.Where(e => e.Buy >= 25).OrderBy(e => e.Buy))
            {
                Console.WriteLine($"{Pad(entry.Name + " (" + entry.Npc.Role + ")", 24)} {entry.Buy,4} 금화  딸기 밭 {entry.Buy / Math.Max(1, berryFarm),5:0.0}시간 · 낚시 {entry.Buy / Math.Max(1, caveFishing),5:0.0}시간{(entry.Hearts > 0 ? $" (호감 {entry.Hearts})" : "")}");
            }
        }

        static string Pad(string text, int width)
        {
            int used = text.Sum(c => c >= 0x1100 ? 2 : 1);
            return text + new string(' ', Math.Max(0, width - used));
        }
    }
}
