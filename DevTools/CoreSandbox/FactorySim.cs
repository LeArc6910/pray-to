using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // Iron bars an hour from small factories built straight onto a test map (Docs/Automation.md, 공장 깊이): drills on
    // veins feeding a smelter or an arc furnace, with no power, a generator kept fed with gel, or water wheels. Then
    // what the bars are worth to the blacksmith, and what a full set of gear and the machines cost in bars, against
    // digging by hand.
    static class FactorySim
    {
        sealed class Setup
        {
            public string Name;
            public int Drills;
            public TileType Processor;
            // "none", "generator" or "wheels".
            public string Power;
        }

        public static void Run(LootData loot, MonsterData monsters, TileData tiles, NpcData npcs, int minutes)
        {
            var setups = new[]
            {
                new Setup { Name = "채굴기 1 + 제련기", Drills = 1, Processor = TileType.Smelter, Power = "none" },
                new Setup { Name = "채굴기 2 + 제련기", Drills = 2, Processor = TileType.Smelter, Power = "none" },
                new Setup { Name = "채굴기 2 + 제련기 + 발전기", Drills = 2, Processor = TileType.Smelter, Power = "generator" },
                new Setup { Name = "채굴기 2 + 전기로 + 발전기", Drills = 2, Processor = TileType.ArcFurnace, Power = "generator" },
                new Setup { Name = "채굴기 3 + 전기로 + 물레방아 2", Drills = 3, Processor = TileType.ArcFurnace, Power = "wheels" },
                new Setup { Name = "채굴기 3 + 전기로 + 발전기", Drills = 3, Processor = TileType.ArcFurnace, Power = "generator" },
            };
            var bar = loot.FindMaterial("iron_bar");
            int barPrice = npcs.Shop.Where(e => e.Material == bar).Select(e => e.Sell).DefaultIfEmpty(0).Max();
            float hours = minutes / 60f;

            Console.WriteLine($"== 공장: 철 광맥에서 {minutes}분 돌린 뒤 한 시간으로 고친 값 ==");
            Console.WriteLine("구성                           주괴/시간  광석/주괴  젤/시간  전력(공급/수요)  금화/시간(주괴 {0})".Replace("{0}", barPrice.ToString()));
            foreach (var setup in setups)
            {
                var (bars, ores, gel, supply, demand) = Build(setup, loot, monsters, tiles, minutes);
                Console.WriteLine($"{Pad(setup.Name, 30)} {bars / hours,8:0}   {(bars > 0 ? ores / (float)bars : 0),7:0.00}   {gel / hours,6:0}   {supply,6}/{demand,-8}   {bars / hours * barPrice,10:0}");
            }

            Console.WriteLine();
            Console.WriteLine($"== 팔 때: 사람마다 같은 물건을 하루 {World.DailySellLimit}개까지 (하루 = {Calendar.DaySeconds / 60:0}분) ==");
            foreach (var id in new[] { "iron_bar", "mithril_bar", "obsidian_bar", "void_bar", "iron_ore" })
            {
                var thing = loot.FindMaterial(id);
                var buyers = npcs.Shop.Where(e => e.Material == thing && e.Sell > 0).ToList();
                int perDay = buyers.Sum(e => e.Sell) * World.DailySellLimit;
                Console.WriteLine($"{Pad(thing.Name, 12)} 사는 사람 {buyers.Count}명, 하루 최대 금화 {perDay,5} (시간당 {perDay * 3600 / Calendar.DaySeconds,5:0})");
            }

            Console.WriteLine();
            Console.WriteLine("== 손으로 캐기 (쉬지 않고 칠 때) ==");
            var vein = tiles.Find(TileType.IronOre);
            float swings = PlayerStats.BaseAttacksPerSecond;
            foreach (var (pickaxe, speed) in new[] { ("구리 곡괭이", 0), ("철 곡괭이", 25), ("미스릴 곡괭이", 50) })
            {
                float seconds = vein.Hardness / (1 + speed / 100f) / swings;
                // Half the time goes to walking between veins.
                float perHour = 3600 / seconds / 2;
                Console.WriteLine($"{Pad(pickaxe, 14)} 광맥 하나 {seconds,4:0.0}초, 걷는 시간을 반으로 치면 시간당 광석 {perHour,4:0} → 주괴 {perHour / 2,4:0}");
            }

            Console.WriteLine();
            Console.WriteLine("== 주괴가 드는 곳 ==");
            int Bars(string id, string barId) =>
                loot.Recipes.Where(r => r.Id == id).SelectMany(r => r.Ingredients).Where(i => i.Material.Id == barId).Sum(i => i.Count);
            foreach (var tier in new[] { "iron", "mithril", "obsidian", "void" })
            {
                string barId = tier + "_bar";
                int set = new[] { "sword", "helmet", "armor", "boots", "pickaxe" }.Sum(part => Bars($"{tier}_{part}", barId));
                Console.WriteLine($"{Pad(loot.FindMaterial(barId).Name, 10)} 장비 한 벌(무기·투구·갑옷·장화·곡괭이) {set,3}개");
            }
            foreach (var machine in new[] { "drill", "smelter", "generator", "water_wheel", "turret", "anvil" })
            {
                var recipe = loot.Recipes.FirstOrDefault(r => r.Id == machine);
                if (recipe == null) continue;
                Console.WriteLine($"{Pad(recipe.Name, 10)} {string.Join(", ", recipe.Ingredients.Select(i => $"{i.Material.Name} {i.Count}"))}");
            }
        }

        // A room with the veins on its left; the drills face them and hand their ore back into the processor, which sends
        // its bars into a chest ahead of it. Power sits two tiles above the processor, kept full.
        static (int Bars, int Ores, int Gel, int Supply, int Demand) Build(Setup setup, LootData loot, MonsterData monsters, TileData tiles, int minutes)
        {
            var map = new TileMap(20, 20, TileType.Floor);
            for (int i = 0; i < 20; i++)
            {
                map[i, 0] = map[i, 19] = map[0, i] = map[19, i] = TileType.Wall;
            }
            const int px = 6, py = 8;
            var laid = new List<TileChange> { new TileChange(px, py, setup.Processor), new TileChange(px + 1, py, TileType.Chest) };
            // Drills on three sides of the processor, each facing away from it toward a vein.
            var drillSpots = new[] { (px - 1, py, 2), (px, py - 1, 3), (px, py + 1, 1) }.Take(setup.Drills).ToList();
            foreach (var (x, y, direction) in drillSpots)
            {
                var (dx, dy) = Machine.Steps[direction];
                map[x + dx, y + dy] = TileType.IronOre;
                laid.Add(new TileChange(x, y, TileType.Drill));
            }
            var generators = new List<(int, int)>();
            if (setup.Power == "generator")
            {
                generators.Add((px + 3, py + 3));
                laid.Add(new TileChange(px + 3, py + 3, TileType.Generator));
            }
            else if (setup.Power == "wheels")
            {
                map[px + 3, py + 4] = TileType.Water;
                laid.Add(new TileChange(px + 2, py + 4, TileType.WaterWheel));
                laid.Add(new TileChange(px + 4, py + 4, TileType.WaterWheel));
            }

            var world = new World(map, TileMap.Center(15, 15), monsters, 1, new WorldConfig { EnemyTarget = 0 }, loot, tiles);
            world.Restore(laid, Array.Empty<(CropDefinition, int, int, long, long)>(), 1);
            var none = Array.Empty<(MaterialDefinition, int)>();
            foreach (var (x, y, direction) in drillSpots) world.RestoreMachine(x, y, direction, 0, null, 0, none, none);
            world.RestoreMachine(px, py, 0, 0, null, 0, none, none);

            var gel = loot.FindMaterial("slime_gel");
            var ore = loot.FindMaterial("iron_ore");
            var bar = loot.FindMaterial("iron_bar");
            int fed = 0;
            long ticks = (long)minutes * 60 * World.TicksPerSecond;
            int supply = 0, demand = 0;
            for (long t = 0; t < ticks; t++)
            {
                foreach (var (x, y) in generators)
                {
                    var generator = world.MachineAt(x, y);
                    int room = generator.Definition.Capacity - generator.Input.Values.Sum();
                    if (room <= 0) continue;
                    generator.Input[gel] = generator.Input.GetValueOrDefault(gel) + room;
                    fed += room;
                }
                world.Step();
                if (t % 600 == 0) world.TakeEvents();
                if (t == World.TicksPerSecond * 2)
                {
                    var grid = world.GridOf(world.MachineAt(px, py));
                    supply = grid?.Supply ?? 0;
                    demand = grid?.Demand ?? 0;
                }
            }
            // Gel still in the generators was not burnt.
            int left = generators.Sum(g => world.MachineAt(g.Item1, g.Item2).Input.GetValueOrDefault(gel));
            var processor = world.MachineAt(px, py);
            int bars = world.ChestAt(px + 1, py).CountOf(bar) + processor.Output.GetValueOrDefault(bar);
            // Ore for each bar, over the production list's last minutes (World.Production), scaled to all the bars.
            var made = world.Production();
            int recentOre = made.Where(p => p.Made == ore).Sum(p => p.Count);
            int recentBars = made.Where(p => p.Made == bar).Sum(p => p.Count);
            int ores = recentBars > 0 ? (int)Math.Round(bars * recentOre / (double)recentBars) : 0;
            return (bars, ores, fed - left, supply, demand);
        }

        static string Pad(string text, int width)
        {
            int shown = text.Sum(c => c > 0x2E80 ? 2 : 1);
            return text + new string(' ', Math.Max(0, width - shown));
        }
    }
}
