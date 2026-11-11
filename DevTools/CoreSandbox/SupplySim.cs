using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // Which system supplies which (Docs/Systems.md, the research report's candidate 12), worked out from the sheets:
    // for every material, where it comes from (digging, hunting, bosses, farming, fishing, animals, foraging, making,
    // machines, the shop, rewards) and what takes it (gear, consumables, building, the factory, farming, selling,
    // requests and stories, upgrading). Then the systems that feed nothing but selling, and the materials nothing
    // gives or nothing takes: a feature fed by none or feeding none is cut off from the ladder.
    static class SupplySim
    {
        public static void Run(LootData loot, TileData tiles, NpcData npcs, MonsterData monsters, BossData bosses, StoryData story)
        {
            var from = new Dictionary<MaterialDefinition, HashSet<string>>();
            var to = new Dictionary<MaterialDefinition, HashSet<string>>();
            void Add(Dictionary<MaterialDefinition, HashSet<string>> map, MaterialDefinition material, string system)
            {
                if (material == null) return;
                material = material.Kind;
                if (!map.TryGetValue(material, out var set)) map[material] = set = new HashSet<string>();
                set.Add(system);
            }
            bool IsMachine(TileType? station) => station is TileType tile && tiles.Machine(tile) != null;

            // Where each comes from.
            foreach (var tile in tiles.Tiles)
            {
                Add(from, tile.Material, "캐기");
                Add(from, tile.Extra, "캐기");
            }
            foreach (var monster in monsters.Monsters) Add(from, loot.FindMaterial(monster.MaterialId ?? ""), "사냥");
            foreach (var boss in bosses.Bosses) Add(from, loot.FindMaterial(boss.MaterialId ?? ""), "보스");
            foreach (var crop in tiles.Crops) Add(from, crop.HarvestMaterial, "농사");
            foreach (var material in loot.Materials.Where(m => m.Fish != null || m.PotCatch)) Add(from, material, "낚시");
            foreach (string id in new[] { World.Egg, World.Milk, World.Wool }) Add(from, loot.FindMaterial(id), "동물");
            foreach (string id in World.ForageOf.Values) Add(from, loot.FindMaterial(id), "채집");
            foreach (var recipe in loot.Recipes.Where(r => r.Material != null)) Add(from, recipe.Material, IsMachine(recipe.Station) ? "공장·가공" : "제작");
            foreach (var entry in npcs.Shop.Where(e => e.Buy > 0)) Add(from, entry.Material, "상점");
            foreach (var request in npcs.Requests)
            {
                foreach (var (thing, _) in request.Rewards) Add(from, thing as MaterialDefinition, "부탁·이야기 보답");
            }
            Add(from, loot.FindMaterial(World.Essence), "장비 분해");
            // Rift shards from rifts (World.Rift.cs), and the pets' eggs from the monsters they hatch from (Pets).
            Add(from, loot.FindMaterial(World.RiftShard), "균열");
            foreach (var pet in Pets.All) Add(from, loot.FindMaterial(pet.EggId), "사냥");

            // What takes each.
            foreach (var recipe in loot.Recipes)
            {
                string system = recipe.Item != null ? "장비"
                    : recipe.Consumable != null ? "소모품"
                    : recipe.Material?.Places is TileType placed ? (tiles.Machine(placed) != null ? "공장" : "기지 짓기")
                    : IsMachine(recipe.Station) ? "공장·가공" : "제작(중간재)";
                foreach (var (material, _) in recipe.Ingredients) Add(to, material, system);
            }
            foreach (var crop in tiles.Crops) Add(to, crop.Seed, "농사");
            foreach (var material in loot.Materials.Where(m => m.Fuel > 0)) Add(to, material, "공장(연료)");
            foreach (var entry in npcs.Shop.Where(e => e.Sell > 0)) Add(to, entry.Material, "팔기");
            foreach (var request in npcs.Requests)
            {
                foreach (var (thing, _) in request.Need) Add(to, thing as MaterialDefinition, request.IsStory ? "이야기" : "부탁");
            }
            // What is placed is used where it stands: a machine in the factory, the rest in the base.
            foreach (var material in loot.Materials.Where(m => m.Places != null))
                Add(to, material, tiles.Machine(material.Places.Value) != null ? "공장" : "기지 짓기");
            Add(to, loot.FindMaterial(World.WateringCan), "농사");
            Add(to, loot.FindMaterial(World.Hoe), "농사");
            Add(to, loot.FindMaterial(World.Fertilizer), "농사");
            Add(to, loot.FindMaterial(World.FishingRod), "낚시");
            foreach (string id in new[] { World.Chicken, World.Cow, World.Sheep }) Add(to, loot.FindMaterial(id), "동물");
            foreach (var pet in Pets.All) Add(to, loot.FindMaterial(pet.EggId), "펫");
            foreach (var step in story?.Steps ?? Array.Empty<StoryStep>()) if (step.Goal == StoryGoal.Have) Add(to, step.Material, "이야기");
            Add(to, loot.FindMaterial(World.Bait), "낚시");
            // What the code takes by id: rift keys open rifts, coins buy, ammo feeds turrets, crystals sit in skills.
            Add(to, loot.FindMaterial(World.RiftKey), "균열");
            Add(to, loot.FindMaterial("coin"), "상점");
            foreach (var material in loot.Materials.Where(World.IsAmmo)) Add(to, material, "기지 방어");
            foreach (var material in loot.Materials.Where(m => m.Id.StartsWith("support_", StringComparison.Ordinal))) Add(to, material, "스킬");
            Add(to, loot.FindMaterial(World.Feed), "동물");
            Add(to, loot.FindMaterial(World.Essence), "장비 다듬기");
            // What a step of upgrading takes besides essence: the metal of the item's recipe.
            foreach (var recipe in loot.Recipes.Where(r => r.Item != null))
            {
                var metal = recipe.Ingredients.OrderByDescending(i => i.Count).Select(i => i.Material).FirstOrDefault();
                Add(to, metal, "장비 다듬기");
            }

            var materials = loot.Materials.Where(m => m.Base == null).ToList();
            Console.WriteLine($"== 재료 {materials.Count}가지: 어디서 오고 어디로 가나 ==");
            var flows = new Dictionary<(string From, string To), List<string>>();
            foreach (var material in materials)
            {
                foreach (string source in from.GetValueOrDefault(material) ?? new HashSet<string>())
                {
                    foreach (string use in to.GetValueOrDefault(material) ?? new HashSet<string>())
                    {
                        if (!flows.TryGetValue((source, use), out var list)) flows[(source, use)] = list = new List<string>();
                        list.Add(material.Name);
                    }
                }
            }
            string[] sources = from.Values.SelectMany(s => s).Distinct().OrderBy(s => s).ToArray();
            string[] uses = to.Values.SelectMany(s => s).Distinct().OrderBy(s => s).ToArray();
            Console.WriteLine();
            Console.WriteLine("| 주는 곳 \\ 받는 곳 | " + string.Join(" | ", uses) + " |");
            Console.WriteLine("|---|" + string.Concat(uses.Select(_ => "---|")));
            foreach (string source in sources)
            {
                Console.WriteLine($"| {source} | " + string.Join(" | ", uses.Select(use => flows.TryGetValue((source, use), out var list) ? list.Count.ToString() : "")) + " |");
            }

            Console.WriteLine();
            Console.WriteLine("== 시스템마다 주는 것(재료 수, 예) ==");
            foreach (string source in sources)
            {
                var given = flows.Where(f => f.Key.From == source).OrderByDescending(f => f.Value.Count).ToList();
                var only = materials.Where(m => from.GetValueOrDefault(m)?.Contains(source) == true).ToList();
                bool sellsOnly = only.Count > 0 && only.All(m => (to.GetValueOrDefault(m) ?? new HashSet<string>()).All(u => u == "팔기"));
                Console.WriteLine($"- {source}: 재료 {only.Count}가지 → " + string.Join(", ", given.Select(f => $"{f.Key.To} {f.Value.Count}({string.Join("·", f.Value.Take(2))})"))
                    + (sellsOnly ? "  ← 팔기에만 간다" : ""));
            }

            Console.WriteLine();
            var nowhere = materials.Where(m => from.ContainsKey(m) && !to.ContainsKey(m)).ToList();
            var sellOnly = materials.Where(m => to.TryGetValue(m, out var u) && u.All(x => x == "팔기")).ToList();
            var nothing = materials.Where(m => to.ContainsKey(m) && !from.ContainsKey(m)).ToList();
            var orphans = materials.Where(m => !from.ContainsKey(m) && !to.ContainsKey(m)).ToList();
            Console.WriteLine($"== 얻지만 쓰는 곳이 없다 ({nowhere.Count}) ==");
            foreach (var m in nowhere) Console.WriteLine($"- {m.Name} ({m.Id}): {string.Join(", ", from[m])}에서");
            Console.WriteLine($"== 팔기에만 간다 ({sellOnly.Count}) ==");
            foreach (var m in sellOnly) Console.WriteLine($"- {m.Name} ({m.Id}): {string.Join(", ", from.GetValueOrDefault(m) ?? new HashSet<string>())}에서");
            Console.WriteLine($"== 쓰지만 얻는 곳이 없다 ({nothing.Count}) ==");
            foreach (var m in nothing) Console.WriteLine($"- {m.Name} ({m.Id}): {string.Join(", ", to[m])}에 쓴다");
            Console.WriteLine($"== 시트로는 오지도 가지도 않는다 ({orphans.Count}, 코드가 따로 다루는 것일 수 있다) ==");
            foreach (var m in orphans) Console.WriteLine($"- {m.Name} ({m.Id})");
        }
    }
}
