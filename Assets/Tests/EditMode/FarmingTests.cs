using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class FarmingTests
    {
        static readonly CropDefinition Mushroom = Tiles.Shipped.Crops.Single(c => c.Id == "mushroom");
        static readonly MaterialDefinition Spore = Gear.Data.FindMaterial("mushroom_spore");
        static readonly MaterialDefinition MushroomItem = Gear.Data.FindMaterial("mushroom");

        // A room with a free-standing wall at (3, 4); the player stands at (2, 2) facing the floor tile (3, 2).
        static World Room(out Player player, TileData tiles = null)
        {
            var map = Maps.FromRows(
                "#######",
                "#..#..#",
                "#.....#",
                "#.....#",
                "#.....#",
                "#######");
            var world = new World(map, TileMap.Center(2, 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, tiles ?? Tiles.Shipped);
            player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            return world;
        }

        static List<SimEvent> Run(World world, int ticks)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < ticks; i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        static void AssertError(IReadOnlyList<string> errors, params string[] parts) =>
            Assert.IsTrue(errors.Any(error => parts.All(error.Contains)), $"No error mentions {string.Join(" + ", parts)}. Errors:\n{string.Join("\n", errors)}");

        // A crop that ripens after a hundredth of a day of water (360 ticks), for the rules rather than the numbers.
        static readonly CropDefinition Quick = TileSheet.Parse(
            "tile,name,hardness,material,count,extra,extra_chance\nWall,돌벽,3,stone,1,,\n",
            Gear.Data,
            "id,name,seed,grow_days,biomes,harvest,count,seeds,seed_chance\nquick,빠른 버섯,mushroom_spore,0.01,,mushroom,2,1,50\n").Crops.Single();

        static void Water(World world, Player player)
        {
            if (player.Inventory.CountOf(Gear.Data.FindMaterial(World.WateringCan)) == 0) player.Inventory.AddMaterial(Gear.Data.FindMaterial(World.WateringCan));
            Assert.IsTrue(world.Water(player.Id), "watered");
        }

        [Test]
        public void ShippedCropsGrowInDaysInTheirOwnBiomes()
        {
            Assert.IsTrue(Tiles.Shipped.Success, string.Join("\n", Tiles.Shipped.Errors));
            Assert.AreSame(Spore, Mushroom.Seed);
            Assert.AreEqual(2, Mushroom.GrowDays, 0.001f);
            Assert.AreEqual(2 * Calendar.TicksPerDay, Mushroom.GrowTicks);
            CollectionAssert.AreEquivalent(new[] { Biome.Cave, Biome.Mushroom }, Mushroom.Biomes);
            Assert.AreSame(MushroomItem, Mushroom.HarvestMaterial);
            Assert.AreEqual(2, Mushroom.Count);
            Assert.AreSame(Mushroom, Tiles.Shipped.CropFrom(Spore));
            Assert.AreSame(Spore, Tiles.Shipped.Find(TileType.Wall).Extra);

            var expected = new Dictionary<string, (float Days, Biome Biome)>
            {
                ["glow_cap"] = (3, Biome.Mushroom),
                ["fire_pepper"] = (4, Biome.Lava),
                ["frost_berry"] = (4, Biome.Ice),
                ["star_melon"] = (6, Biome.Abyss),
            };
            foreach (var (id, (days, biome)) in expected)
            {
                var crop = Tiles.Shipped.Crops.Single(c => c.Id == id);
                Assert.AreEqual(days, crop.GrowDays, 0.001f, id);
                CollectionAssert.AreEqual(new[] { biome }, crop.Biomes, id);
            }
        }

        [Test]
        public void CropSheetErrorsNameTheRow()
        {
            var data = TileSheet.Parse(
                "tile,name,hardness,material,count,extra,extra_chance\nWall,돌벽,3,stone,1,gold,5\n",
                Gear.Data,
                "id,name,seed,grow_days,biomes,harvest,count,seeds,seed_chance\n" +
                "a,가,nothing,1,,mushroom,1,1,0\n" +
                "b,나,mushroom_spore,0,,mushroom,1,1,0\n" +
                "c,다,mushroom_spore,1,,gold,1,1,0\n" +
                "d,라,mushroom_spore,1,cave|desert,mushroom,1,1,0\n" +
                "e,마,mushroom_spore,1,cave|ice,mushroom,1,1,0\n" +
                "f,바,mushroom_spore,1,,mushroom,1,1,0\n");

            AssertError(data.Errors, "tiles.csv 2행", "재료 'gold'");
            AssertError(data.Errors, "crops.csv 2행", "씨앗 'nothing'");
            AssertError(data.Errors, "crops.csv 3행", "grow_days");
            AssertError(data.Errors, "crops.csv 4행", "harvest 'gold'");
            AssertError(data.Errors, "crops.csv 5행", "biomes", "desert");
            AssertError(data.Errors, "crops.csv 7행", "이미 다른 작물의 씨앗");
            CollectionAssert.AreEqual(new[] { Biome.Cave, Biome.Ice }, data.Crops.Single().Biomes);
        }

        [Test]
        public void MonstersTrampleCropsAtNightButNotNearAScarecrow()
        {
            var world = Room(out var player);
            long half = Mushroom.GrowTicks / 2;
            world.Restore(new TileChange[0], new[] { (Mushroom, 4, 3, half, 0L) }, 0);
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), TileMap.Center(4, 3));

            // By day nothing happens.
            Run(world, 1);
            Assert.AreEqual(half, (long)world.CropAt(4, 3).GrownAt(world.Tick), 1);

            // At night it sets the crop back a quarter of its growing, and not again for a while.
            world.RestoreClock(Calendar.TicksPerDay * 17 / 24);
            slime.Position = TileMap.Center(4, 3);
            var events = Run(world, 1);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Trampled), "trampled");
            long after = (long)world.CropAt(4, 3).GrownAt(world.Tick);
            Assert.AreEqual(half - (long)(Mushroom.GrowTicks * World.TrampleShare), after, 1);
            slime.Position = TileMap.Center(4, 3);
            Assert.IsFalse(Run(world, 2).Any(e => e.Type == SimEventType.Trampled), "once in a while for each tile");

            // A scarecrow within four tiles keeps them off.
            world.Restore(new[] { new TileChange(1, 1, TileType.Scarecrow) }, new[] { (Mushroom, 5, 3, half, 0L) }, 0);
            Assert.IsTrue(world.Guarded(5, 3));
            slime.Position = TileMap.Center(5, 3);
            Assert.IsFalse(Run(world, 1).Any(e => e.Type == SimEventType.Trampled), "guarded");
            Assert.AreEqual(half, (long)world.CropAt(5, 3).GrownAt(world.Tick), 1);

            // Where it happened goes over the wire, as for petting and moving in.
            var sent = SnapshotMessage.Decode(new SnapshotMessage(1, new SelfState(0, new Vec2(1, 1), 50, 100, 5), new EntityState[0], new ProjectileState[0],
                new HazardState[0], new[]
                {
                    new SimEvent(SimEventType.Trampled, 3, position: new Vec2(4.5f, 3.5f)),
                    new SimEvent(SimEventType.AnimalsPetted, 1, amount: 2, position: new Vec2(7.5f, 2.5f)),
                    new SimEvent(SimEventType.MovedIn, 1, amount: 1, position: new Vec2(9.5f, 9.5f)),
                }).Encode());
            Assert.AreEqual(4.5f, sent.Events[0].Position.X, 0.05f);
            Assert.AreEqual(7.5f, sent.Events[1].Position.X, 0.05f);
            Assert.AreEqual(2, sent.Events[1].Amount);
            Assert.AreEqual(9.5f, sent.Events[2].Position.Y, 0.05f);
            Assert.AreEqual(1, sent.Events[2].Amount);
        }

        [Test]
        public void PlantingTakesASeedAndAFreeFloor()
        {
            var world = Room(out var player);
            player.Inventory.AddMaterial(Spore, 2);

            Assert.IsTrue(world.Plant(player.Id, Mushroom));
            Assert.AreSame(Mushroom, world.CropAt(3, 2).Definition);
            Assert.AreEqual(1, player.Inventory.CountOf(Spore));
            Assert.IsFalse(world.Plant(player.Id, Mushroom), "already growing there");
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("stone"));
            Assert.IsFalse(world.PlaceTile(player.Id, Gear.Data.FindMaterial("stone")), "no wall on a crop");

            world.MovePlayer(player.Id, TileMap.Center(3, 3), new Vec2(0, 1), 10);
            Assert.IsFalse(world.Plant(player.Id, Mushroom), "the tile ahead is the wall at (3, 4)");
        }

        [Test]
        public void NothingIsPlantedWithoutASeedOrInABossArena()
        {
            var world = Room(out var player);
            Assert.IsFalse(world.Plant(player.Id, Mushroom), "no spore");

            player.Inventory.AddMaterial(Spore);
            world.PlaceBoss(Bosses.Shipped.Bosses[0], TileMap.Center(4, 2), 1);
            Assert.IsFalse(world.Plant(player.Id, Mushroom));
            Assert.AreEqual(1, player.Inventory.CountOf(Spore));
        }

        [Test]
        public void CropsGrowOnlyWhileWateredAndAreHarvestedByWalkingOverThem()
        {
            var world = Room(out var player);
            player.Inventory.AddMaterial(Spore);
            Assert.IsTrue(world.Plant(player.Id, Quick));
            world.TakeCropChanges();

            // Dry, it waits.
            Run(world, (int)Quick.GrowTicks * 2);
            Assert.AreEqual(0, world.CropAt(3, 2).Growth(world.Tick), 0.001f);
            Assert.IsFalse(world.CropAt(3, 2).IsWet(world.Tick));

            Water(world, player);
            var crop = world.CropAt(3, 2);
            Assert.IsTrue(crop.IsWet(world.Tick));
            Run(world, (int)Quick.GrowTicks / 2);
            Assert.AreEqual(0.5f, crop.Growth(world.Tick), 0.02f);
            world.MovePlayer(player.Id, TileMap.Center(3, 2), new Vec2(1, 0), 10);
            Run(world, 1);
            Assert.AreSame(crop, world.CropAt(3, 2), "not ripe yet");
            world.TakeCropChanges();

            var events = Run(world, (int)Quick.GrowTicks / 2 + 1);

            Assert.IsNull(world.CropAt(3, 2));
            Assert.AreEqual(2, player.Inventory.CountOf(MushroomItem));
            Assert.That(player.Inventory.CountOf(Spore), Is.EqualTo(1).Or.EqualTo(2), "one back, maybe two");
            var harvest = events.Single(e => e.Type == SimEventType.Harvested);
            Assert.AreEqual(TileMap.Center(3, 2), harvest.Position);
            Assert.IsNull(world.TakeCropChanges().Single().Crop, "the crop is gone");
        }

        [Test]
        public void WaterLastsADayOfPlay()
        {
            var world = Room(out var player);
            // Two dry days in a row, so no rain waters it (World.IsRainy).
            int day = Enumerable.Range(1, 100).First(d => !World.IsRainy(d) && !World.IsRainy(d + 1));
            world.RestoreClock((day - 1) * Calendar.TicksPerDay);
            player.Inventory.AddMaterial(Spore);
            world.Plant(player.Id, Mushroom);
            Water(world, player);
            var crop = world.CropAt(3, 2);
            Assert.AreEqual(world.Tick + Calendar.TicksPerDay, crop.WetUntil);

            Run(world, (int)Calendar.TicksPerDay + 600);
            Assert.IsFalse(crop.IsWet(world.Tick), "dry after a day");
            Assert.AreEqual(0.5f, crop.Growth(world.Tick), 0.001f, "one day of the two, no more for the time it was dry");

            // Watering again carries on from there.
            Water(world, player);
            Assert.AreEqual(0.5f, world.CropAt(3, 2).Growth(world.Tick), 0.001f);
            Run(world, (int)Calendar.TicksPerDay);
            Assert.IsTrue(world.CropAt(3, 2).IsRipe(world.Tick));
        }

        [Test]
        public void MushroomsRoastIntoFood()
        {
            var world = Room(out var player);
            world.Map[3, 4] = TileType.Furnace;
            player.Inventory.AddMaterial(MushroomItem, 2);

            Assert.IsTrue(world.Craft(player.Id, Gear.Data.Recipes.Single(r => r.Id == "roasted_mushroom")));

            var food = Gear.Data.FindConsumable("roasted_mushroom");
            Assert.AreEqual(1, player.Inventory.CountOf(food));
            Assert.AreEqual(25, food.Heal);
            Assert.AreEqual(0, food.Weight, "grown, not dropped");
        }

        [Test]
        public void WateringGivesEachCropAroundADayOfWater()
        {
            var world = Room(out var player);
            var can = Gear.Data.FindMaterial(World.WateringCan);
            player.Inventory.AddMaterial(Spore, 3);
            world.Plant(player.Id, Mushroom);
            // A second crop beside the first, and a third out of reach.
            world.MovePlayer(player.Id, TileMap.Center(2, 3), new Vec2(1, 0), 10);
            world.Plant(player.Id, Mushroom);
            world.MovePlayer(player.Id, TileMap.Center(4, 3), new Vec2(1, 0), 10);
            world.MovePlayer(player.Id, TileMap.Center(4, 2), new Vec2(1, 0), 10);
            Assert.IsTrue(world.Plant(player.Id, Mushroom));
            world.MovePlayer(player.Id, TileMap.Center(2, 2), new Vec2(1, 0), 10);
            world.TakeCropChanges();

            Assert.IsFalse(world.Water(player.Id), "no watering can");
            player.Inventory.AddMaterial(can);
            Assert.IsTrue(world.Water(player.Id));

            Assert.IsTrue(world.CropAt(3, 2).IsWet(world.Tick));
            Assert.IsTrue(world.CropAt(3, 3).IsWet(world.Tick));
            Assert.IsFalse(world.CropAt(5, 2).IsWet(world.Tick), "two tiles away");
            Assert.AreEqual(1, player.Inventory.CountOf(can), "a tool, not used up");
            Assert.AreEqual(2, world.TakeCropChanges().Count(c => c.Crop != null && c.Crop.IsWet(world.Tick)));
            var events = world.TakeEvents();
            Assert.AreEqual(2, events.Single(e => e.Type == SimEventType.Watered).Amount);

            Run(world, 900);
            Assert.AreEqual(900f / Mushroom.GrowTicks, world.CropAt(3, 2).Growth(world.Tick), 0.0001f);
            Assert.AreEqual(0, world.CropAt(5, 2).Growth(world.Tick), "dry, so still");
            // Watering again tops it up to a whole day from now, keeping what it grew.
            Assert.IsTrue(world.Water(player.Id));
            Assert.AreEqual(world.Tick + Calendar.TicksPerDay, world.CropAt(3, 2).WetUntil);
            Assert.AreEqual(900f / Mushroom.GrowTicks, world.CropAt(3, 2).Growth(world.Tick), 0.0001f);
        }

        [Test]
        public void CropsGrowOnlyInTheirOwnBiomes()
        {
            var map = Maps.FromRows(
                "#######",
                "#.....#",
                "#.....#",
                "#######");
            // The left half is ice cave, the right half plain cave.
            var biomes = BiomeMap.Of(map.Width, map.Height, (x, y) => x < 3 ? Biome.Ice : Biome.Cave);
            var world = new World(map, TileMap.Center(1, 1), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped, biomes);
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            var berry = Tiles.Shipped.Crops.Single(c => c.Id == "frost_berry");
            player.Inventory.AddMaterial(Spore, 2);
            player.Inventory.AddMaterial(berry.Seed, 2);
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);

            Assert.AreEqual(Biome.Ice, world.FarmBiomeAt(2, 1));
            Assert.IsFalse(world.Plant(player.Id, Mushroom), "mushrooms do not grow in the ice caves");
            Assert.IsTrue(world.Plant(player.Id, berry));
            Assert.AreEqual(2, player.Inventory.CountOf(Spore), "the spore is kept");

            world.MovePlayer(player.Id, TileMap.Center(3, 1), new Vec2(1, 0), 10);
            Assert.IsFalse(world.Plant(player.Id, berry), "frost berries only in the ice caves");
            Assert.IsTrue(world.Plant(player.Id, Mushroom));
            Assert.AreEqual("얼음 동굴", berry.BiomeNames);
            Assert.AreEqual("흙 동굴, 버섯 숲", Mushroom.BiomeNames);
        }

        [Test]
        public void PeppersGrowFromIronAndCookIntoFoodThatBuffs()
        {
            var pepper = Tiles.Shipped.Crops.Single(c => c.Id == "fire_pepper");
            Assert.AreSame(Gear.Data.FindMaterial("fire_pepper_seed"), pepper.Seed);
            Assert.AreSame(pepper.Seed, Tiles.Shipped.Find(TileType.IronOre).Extra);

            var world = Room(out var player);
            world.Map[3, 4] = TileType.Furnace;
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("fire_pepper"), 2);
            player.Inventory.AddMaterial(MushroomItem, 1);
            Assert.IsTrue(world.Craft(player.Id, Gear.Data.Recipes.Single(r => r.Id == "spicy_fry")));
            var fry = Gear.Data.FindConsumable("spicy_fry");
            Assert.AreEqual(1, player.Inventory.CountOf(fry));

            float damage = player.Stats.Damage;
            Assert.IsTrue(world.UseConsumable(player.Id, fry), "eaten at full health");
            Assert.AreSame(fry, player.Food);
            Assert.AreEqual(damage * 1.1f, player.Stats.Damage, 0.001f);
            Assert.AreEqual(0, player.Inventory.CountOf(fry));

            // A new food replaces the old; the buff ends after its time.
            var soup = Gear.Data.FindConsumable("mushroom_soup");
            player.Inventory.AddConsumable(soup);
            Run(world, World.Ticks(fry.Cooldown));
            float health = player.Stats.MaxHealth;
            Assert.IsTrue(world.UseConsumable(player.Id, soup));
            Assert.AreSame(soup, player.Food);
            Assert.AreEqual(damage, player.Stats.Damage, 0.001f);
            Assert.AreEqual(health * 1.15f, player.Stats.MaxHealth, 0.01f);
            Run(world, soup.BuffTicks);
            Assert.IsNull(player.Food);
            Assert.AreEqual(health, player.Stats.MaxHealth, 0.01f);
        }

        [Test]
        public void FoodSheetErrors()
        {
            var data = LootSheets.Parse(
                TestLoot.Bases, TestLoot.Affixes, TestLoot.Runes, TestLoot.Runewords,
                "id,name,level,heal,seconds,cooldown,weight,description,buff,buff_seconds\n" +
                "a,가,1,0,1,10,0,,Damage+10%,\n" +
                "b,나,1,0,1,10,0,,,\n" +
                "c,다,1,0,1,10,0,,Damage+5~10%,60\n" +
                "d,라,1,0,1,10,0,,Damage+10%,60\n");

            AssertError(data.Errors, "consumables.csv 2행", "buff_seconds");
            AssertError(data.Errors, "consumables.csv 3행", "heal");
            AssertError(data.Errors, "consumables.csv 4행", "범위");
            Assert.IsTrue(data.Consumables.Single(c => c.Id == "d").IsFood);
        }

        [Test]
        public void DiggingStoneCanTurnUpSpores()
        {
            var lucky = TileSheet.Parse("tile,name,hardness,material,count,extra,extra_chance\nWall,돌벽,1,stone,1,mushroom_spore,100\n", Gear.Data);
            var world = Room(out var player, lucky);
            player.Equip(Gear.Plain("copper_pickaxe"));
            world.MovePlayer(player.Id, TileMap.Center(3, 3), new Vec2(0, 1), 10);

            world.SetAttacking(player.Id, true);
            Run(world, 1);

            Assert.AreEqual(TileType.Floor, world.Map[3, 4]);
            Assert.AreEqual(1, player.Inventory.CountOf(Spore));
        }
        [Test]
        public void SoilBroughtFromAnotherBiomeGrowsItsCropsAtHome()
        {
            var world = Room(out var player);
            var frostSoil = Gear.Data.FindMaterial("frost_soil");
            var berry = Tiles.Shipped.Crops.Single(c => c.Id == "frost_berry");
            Assert.AreEqual(TileType.FrostSoil, frostSoil.Places);
            Assert.IsTrue(Gear.Data.Recipes.Any(r => r.Material == frostSoil && r.Ingredients.Any(i => i.Material.Id == "ice_crystal")), "made from ice");
            player.Inventory.AddMaterial(berry.Seed);
            Assert.IsFalse(world.Plant(player.Id, berry), "the plain cave is no place for frost berries");

            player.Inventory.AddMaterial(frostSoil);
            Assert.IsTrue(world.PlaceTile(player.Id, frostSoil));
            Assert.AreEqual(TileType.FrostSoil, world.Map[3, 2]);
            Assert.AreEqual(Biome.Ice, world.FarmBiomeAt(3, 2));
            Assert.IsFalse(world.Map.IsSolid(3, 2), "walked on");
            Assert.IsTrue(world.Plant(player.Id, berry));
            Assert.AreEqual(World.SoilHeadStart(berry) / (float)berry.GrowTicks, world.CropAt(3, 2).Growth(world.Tick), 0.001f, "a soil's day sooner");
            player.Inventory.AddMaterial(Gear.Data.FindMaterial(World.Fertilizer));
            Assert.IsFalse(world.Fertilize(player.Id), "never fertile");

            // Watered each of the three days it still needs, then harvested, it comes up again with the hoe, back
            // into the bag.
            for (int day = 0; day < 3; day++)
            {
                Water(world, player);
                Run(world, (int)Calendar.TicksPerDay);
            }
            Assert.IsTrue(world.CropAt(3, 2).IsRipe(world.Tick));
            Moves.Walk(world, player, TileMap.Center(3, 2));
            Run(world, 1);
            Assert.IsNull(world.CropAt(3, 2));
            Moves.Walk(world, player, TileMap.Center(2, 2));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial(World.Hoe));
            Assert.IsTrue(world.Till(player.Id));
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(frostSoil));
        }

        [Test]
        public void AHoeTurnsFloorIntoSoilWhereCropsGrowADaySooner()
        {
            var world = Room(out var player);
            var hoe = Gear.Data.FindMaterial(World.Hoe);
            Assert.IsFalse(world.Till(player.Id), "no hoe");
            player.Inventory.AddMaterial(hoe);
            Assert.IsTrue(world.Till(player.Id));
            Assert.AreEqual(TileType.Soil, world.Map[3, 2]);
            Assert.IsFalse(world.Map.IsSolid(3, 2), "walked on");

            player.Inventory.AddMaterial(Spore);
            Assert.IsTrue(world.Plant(player.Id, Mushroom));
            var crop = world.CropAt(3, 2);
            Assert.AreEqual(0.5f, crop.Growth(world.Tick), 0.001f, "a day of its two");
            Assert.IsFalse(world.Till(player.Id), "not while something grows");

            // Never less than a day left: a crop of a day or less gets no head start.
            Assert.AreEqual(0, World.SoilHeadStart(Quick));
            Assert.AreEqual(Calendar.TicksPerDay, World.SoilHeadStart(Tiles.Shipped.Crops.Single(c => c.Id == "star_melon")));

            // Back to floor once it is gone.
            Water(world, player);
            world.MovePlayer(player.Id, TileMap.Center(3, 2), new Vec2(1, 0), 10);
            Run(world, (int)Calendar.TicksPerDay + 2);
            Assert.IsNull(world.CropAt(3, 2), "harvested by walking over it");
            world.MovePlayer(player.Id, TileMap.Center(2, 2), new Vec2(1, 0), 10);
            Assert.IsTrue(world.Till(player.Id));
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
        }

        [Test]
        public void FertileSoilGivesOneMoreAndIsUsedUpByTheHarvest()
        {
            var world = Room(out var player);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial(World.Hoe));
            var fertilizer = Gear.Data.FindMaterial(World.Fertilizer);
            Assert.IsTrue(world.Till(player.Id));
            player.Inventory.AddMaterial(Spore);
            Assert.IsTrue(world.Plant(player.Id, Quick));
            Assert.IsFalse(world.Fertilize(player.Id), "no fertilizer");
            player.Inventory.AddMaterial(fertilizer, 2);
            Assert.IsTrue(world.Fertilize(player.Id), "growing or not");
            Assert.AreEqual(TileType.FertileSoil, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(fertilizer));
            Assert.IsFalse(world.Fertilize(player.Id), "nothing plain left to fertilize");
            Assert.AreEqual(1, player.Inventory.CountOf(fertilizer));

            Water(world, player);
            world.MovePlayer(player.Id, TileMap.Center(3, 2), new Vec2(1, 0), 10);
            Run(world, (int)Quick.GrowTicks + 1);
            Assert.AreEqual(Quick.Count + 1, player.Inventory.CountOf(MushroomItem));
            Assert.AreEqual(TileType.Soil, world.Map[3, 2], "used up");
        }

        [Test]
        public void TheNewCropsGrowFromSeedsPlayersCanGet()
        {
            foreach (var (id, seed) in new[] { ("glow_cap", "glow_spore"), ("frost_berry", "frost_berry_seed"), ("star_melon", "star_melon_seed") })
            {
                var crop = Tiles.Shipped.Crops.Single(c => c.Id == id);
                Assert.AreEqual(seed, crop.Seed.Id);
                Assert.IsTrue(seed == "glow_spore" || Gear.Data.Recipes.Any(r => r.Material?.Id == seed), $"{seed} can be made");
                Assert.IsTrue(Gear.Data.Recipes.Any(r => r.Consumable != null && r.Ingredients.Any(i => i.Material == crop.HarvestMaterial)), $"{id} is cooked");
            }
        }
}
}
