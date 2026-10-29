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

        [Test]
        public void ShippedMushroomsGrowFromSporesInThreeMinutes()
        {
            Assert.IsTrue(Tiles.Shipped.Success, string.Join("\n", Tiles.Shipped.Errors));
            Assert.AreSame(Spore, Mushroom.Seed);
            Assert.AreEqual(180, Mushroom.GrowSeconds, 0.001f);
            Assert.AreSame(MushroomItem, Mushroom.HarvestMaterial);
            Assert.AreEqual(2, Mushroom.Count);
            Assert.AreSame(Mushroom, Tiles.Shipped.CropFrom(Spore));
            Assert.AreSame(Spore, Tiles.Shipped.Find(TileType.Wall).Extra);
        }

        [Test]
        public void CropSheetErrorsNameTheRow()
        {
            var data = TileSheet.Parse(
                "tile,name,hardness,material,count,extra,extra_chance\nWall,돌벽,3,stone,1,gold,5\n",
                Gear.Data,
                "id,name,seed,grow_seconds,harvest,count,seeds,seed_chance\n" +
                "a,가,nothing,10,mushroom,1,1,0\n" +
                "b,나,mushroom_spore,0,mushroom,1,1,0\n" +
                "c,다,mushroom_spore,10,gold,1,1,0\n" +
                "d,라,mushroom_spore,10,mushroom,1,1,0\n" +
                "e,마,mushroom_spore,10,mushroom,1,1,0\n");

            AssertError(data.Errors, "tiles.csv 2행", "재료 'gold'");
            AssertError(data.Errors, "crops.csv 2행", "씨앗 'nothing'");
            AssertError(data.Errors, "crops.csv 3행", "grow_seconds");
            AssertError(data.Errors, "crops.csv 4행", "harvest 'gold'");
            AssertError(data.Errors, "crops.csv 6행", "이미 다른 작물의 씨앗");
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
        public void RipeCropsAreHarvestedByWalkingOverThem()
        {
            var world = Room(out var player);
            player.Inventory.AddMaterial(Spore);
            world.Plant(player.Id, Mushroom);
            var crop = world.CropAt(3, 2);
            world.TakeCropChanges();

            Run(world, Mushroom.GrowTicks / 2);
            Assert.AreEqual(0.5f, crop.Growth(world.Tick), 0.02f);
            world.MovePlayer(player.Id, TileMap.Center(3, 2), new Vec2(1, 0), 10);
            Run(world, 1);
            Assert.AreSame(crop, world.CropAt(3, 2), "not ripe yet");

            var events = Run(world, Mushroom.GrowTicks / 2 + 1);

            Assert.IsNull(world.CropAt(3, 2));
            Assert.AreEqual(2, player.Inventory.CountOf(MushroomItem));
            Assert.That(player.Inventory.CountOf(Spore), Is.EqualTo(1).Or.EqualTo(2), "one back, maybe two");
            var harvest = events.Single(e => e.Type == SimEventType.Harvested);
            Assert.AreEqual(TileMap.Center(3, 2), harvest.Position);
            Assert.IsNull(world.TakeCropChanges().Single().Crop, "the crop is gone");
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
        public void DiggingStoneCanTurnUpSpores()
        {
            var lucky = TileSheet.Parse("tile,name,hardness,material,count,extra,extra_chance\nWall,돌벽,1,stone,1,mushroom_spore,100\n", Gear.Data);
            var world = Room(out var player, lucky);
            world.MovePlayer(player.Id, TileMap.Center(3, 3), new Vec2(0, 1), 10);

            world.SetAttacking(player.Id, true);
            Run(world, 1);

            Assert.AreEqual(TileType.Floor, world.Map[3, 4]);
            Assert.AreEqual(1, player.Inventory.CountOf(Spore));
        }
    }
}
