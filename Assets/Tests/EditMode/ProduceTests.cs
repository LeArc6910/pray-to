using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // More crops, crops that regrow, and the keg and preserves jar (Docs/Farming.md, 다시 자라는 작물, 가공).
    public class ProduceTests
    {
        static readonly MaterialDefinition Berry = Gear.Data.FindMaterial("cave_berry");
        static readonly MaterialDefinition Juice = Gear.Data.FindMaterial("berry_juice");
        static readonly MaterialDefinition Cabbage = Gear.Data.FindMaterial("snow_cabbage");
        static readonly MaterialDefinition Kimchi = Gear.Data.FindMaterial("kimchi");
        static readonly MaterialDefinition Chest = Gear.Data.FindMaterial("chest");
        static readonly MaterialDefinition KegItem = Gear.Data.FindMaterial("keg");
        static readonly MaterialDefinition JarItem = Gear.Data.FindMaterial("preserves_jar");
        static readonly MachineDefinition Keg = Tiles.Shipped.Machine(TileType.Keg);
        static readonly MachineDefinition Jar = Tiles.Shipped.Machine(TileType.PreservesJar);

        // A berry that ripens after a hundredth of a day of water and again after half that, and the shipped machines.
        static readonly TileData QuickTiles = TileSheet.Parse(
            "tile,name,hardness,material,count,extra,extra_chance\nWall,돌벽,3,stone,1,,\n",
            Gear.Data,
            "id,name,seed,grow_days,biomes,harvest,count,seeds,seed_chance,regrow_days\nquick_berry,빠른 딸기,cave_berry_seed,0.01,,cave_berry,3,0,0,0.005\n",
            System.IO.File.ReadAllText(RepoFile.Find("Assets/_Project/Data/World/" + TileSheet.MachinesFile)));
        static CropDefinition QuickBerry => QuickTiles.Crops.Single();

        static World Room(out Player player, TileData tiles = null)
        {
            var world = new World(Maps.Room(16), TileMap.Center(8, 8), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, tiles ?? Tiles.Shipped);
            player = world.AddPlayer("a", null);
            return world;
        }

        [Test]
        public void EveryBiomeHasMoreCropsAndSomeRegrow()
        {
            Assert.IsTrue(Tiles.Shipped.Success, string.Join("\n", Tiles.Shipped.Errors));
            var regrow = new Dictionary<string, float> { ["cave_berry"] = 2, ["moon_grape"] = 2, ["ember_corn"] = 2, ["void_bean"] = 3 };
            foreach (var (id, days) in regrow)
            {
                var crop = Tiles.Shipped.Crops.Single(c => c.Id == id);
                Assert.AreEqual(days, crop.RegrowDays, 0.001f, id);
                Assert.Less(crop.RegrowDays, crop.GrowDays, id);
                Assert.AreEqual(0, crop.Seeds, $"{id} gives no seeds: it stays");
            }
            Assert.IsFalse(Tiles.Shipped.Crops.Single(c => c.Id == "cave_potato").Regrows);
            foreach (var biome in new[] { Biome.Cave, Biome.Mushroom, Biome.Ice, Biome.Lava, Biome.Abyss })
            {
                Assert.GreaterOrEqual(Tiles.Shipped.Crops.Count(c => c.GrowsIn(biome) && c.Biomes.Count > 0), 2, $"{biome} has two crops or more");
            }
            Assert.IsTrue(People.Shipped.Success, string.Join("\n", People.Shipped.Errors));
            Assert.IsTrue(Gear.Data.Errors.Count == 0, string.Join("\n", Gear.Data.Errors));
        }

        [Test]
        public void RegrowDaysMustBeShorterThanGrowDays()
        {
            var data = TileSheet.Parse(
                "tile,name,hardness,material,count,extra,extra_chance\nWall,돌벽,3,stone,1,,\n",
                Gear.Data,
                "id,name,seed,grow_days,biomes,harvest,count,seeds,seed_chance,regrow_days\nx,엑스,cave_berry_seed,2,,cave_berry,1,0,0,3\n");
            Assert.IsTrue(data.Errors.Any(e => e.Contains("regrow_days")), string.Join("\n", data.Errors));
        }

        [Test]
        public void ARegrowingCropStaysAfterHarvestAndRipensAgain()
        {
            var world = Room(out var player, QuickTiles);
            Face(world, player, 8, 9, new Vec2(0, 1));
            player.Inventory.AddMaterial(QuickBerry.Seed);
            Assert.IsTrue(world.Plant(player.Id, QuickBerry));
            player.Inventory.AddMaterial(Gear.Data.FindMaterial(World.WateringCan));
            Assert.IsTrue(world.Water(player.Id));
            Run(world, QuickBerry.GrowTicks / (float)World.TicksPerSecond + 0.1f);
            Assert.IsTrue(world.CropAt(8, 9).IsRipe(world.Tick));

            // Walk over it.
            Walk(world, player, TileMap.Center(8, 9), new Vec2(0, 1));
            Run(world, 0.1f);
            Assert.AreEqual(3, player.Inventory.CountOf(Berry));
            var again = world.CropAt(8, 9);
            Assert.IsNotNull(again, "still there");
            Assert.IsFalse(again.IsRipe(world.Tick));

            // With the water it had left, it is ripe again after its regrow days, and harvested again while stood on.
            Run(world, QuickBerry.RegrowTicks / (float)World.TicksPerSecond + 0.2f);
            Assert.AreEqual(6, player.Inventory.CountOf(Berry));
            Assert.IsNotNull(world.CropAt(8, 9));
        }

        [Test]
        public void AHarvesterLeavesARegrowingCropStanding()
        {
            var world = Room(out var player, QuickTiles);
            // Harvester at (8, 6) facing up reaps (8, 7); a chest behind it at (8, 5).
            Face(world, player, 8, 7, new Vec2(0, 1));
            player.Inventory.AddMaterial(QuickBerry.Seed);
            Assert.IsTrue(world.Plant(player.Id, QuickBerry));
            player.Inventory.AddMaterial(Gear.Data.FindMaterial(World.WateringCan));
            Assert.IsTrue(world.Water(player.Id));
            Walk(world, player, TileMap.Center(10, 6), new Vec2(-1, 0));
            Place(world, player, Gear.Data.FindMaterial("harvester"), 8, 6, new Vec2(0, 1));
            Walk(world, player, TileMap.Center(10, 5), new Vec2(-1, 0));
            Place(world, player, Chest, 8, 5, new Vec2(-1, 0));

            Run(world, QuickBerry.GrowTicks / (float)World.TicksPerSecond + 3);
            Assert.AreEqual(3, world.ChestAt(8, 5).CountOf(Berry));
            Assert.IsNotNull(world.CropAt(8, 7), "left to ripen again");
        }

        [Test]
        public void AKegBrewsWhatIsPutInAndGivesItBack()
        {
            var world = Room(out var player);
            Place(world, player, KegItem, 8, 9, new Vec2(0, 1));
            player.Inventory.AddMaterial(Berry, 5);
            player.Inventory.AddMaterial(Cabbage, 1);
            Assert.AreEqual(0, world.LoadMachine(player.Id, Cabbage, 1), "a keg does not take cabbage");
            Assert.AreEqual(5, world.LoadMachine(player.Id, Berry, 9));
            Assert.AreEqual(0, player.Inventory.CountOf(Berry));

            Run(world, Keg.Seconds * 2 + 1);
            var keg = world.MachineAt(8, 9);
            Assert.AreEqual(2, keg.Outputs.GetValueOrDefault(Juice), "two berries a juice, one berry left");
            Assert.AreEqual(1, keg.Inputs.GetValueOrDefault(Berry));
            Assert.AreEqual(2, world.UnloadMachine(player.Id));
            Assert.AreEqual(2, player.Inventory.CountOf(Juice));
            Assert.AreEqual(2, world.Production().Single().Count, "counted as made");
        }

        [Test]
        public void KegAndJarRecipesAreNotMadeByHand()
        {
            var world = Room(out var player);
            Place(world, player, KegItem, 8, 9, new Vec2(0, 1));
            var juice = Gear.Data.Recipes.Single(r => r.Id == "berry_juice");
            Assert.IsTrue(world.MadeByMachine(juice));
            Assert.IsFalse(world.MadeByMachine(Gear.Data.Recipes.Single(r => r.Id == "baked_potato")), "the furnace's own cooking");
            player.Inventory.AddMaterial(Berry, 2);
            Assert.IsFalse(world.Craft(player.Id, juice), "beside a keg, still not by hand");
            Assert.IsFalse(World.Assembles(juice), "nor by an assembler");
        }

        [Test]
        public void AnArmFeedsAJarFromAChest()
        {
            var world = Room(out var player);
            // Chest (3, 8) → arm (4, 8) → jar (5, 8) → chest (6, 8).
            Place(world, player, Chest, 6, 8, new Vec2(1, 0));
            Place(world, player, JarItem, 5, 8, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("robot_arm"), 4, 8, new Vec2(1, 0));
            Place(world, player, Chest, 3, 8, new Vec2(1, 0));
            world.ChestAt(3, 8).AddMaterial(Cabbage, 2);
            world.ChestAt(3, 8).AddMaterial(Berry, 2);

            Run(world, Jar.Seconds * 2 + 4);
            Assert.AreEqual(2, world.ChestAt(6, 8).CountOf(Kimchi));
            Assert.AreEqual(2, world.ChestAt(3, 8).CountOf(Berry), "a jar does not pickle berries");
        }

        [Test]
        public void TheMerchantPaysMoreForWhatWasMadeThanForTheCrop()
        {
            int Sell(string id) => People.Shipped.Shop.Single(e => e.Material?.Id == id).Sell;
            Assert.Greater(Sell("berry_juice"), Sell("cave_berry") * 2);
            Assert.Greater(Sell("kimchi"), Sell("snow_cabbage"));
            Assert.Greater(Sell("star_wine"), Sell("star_melon"));
        }
    }
}
