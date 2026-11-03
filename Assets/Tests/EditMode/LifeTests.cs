using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // Life skills, the reel gauge, the coop and the crab pot (Docs/LifeSkills.md, Docs/Animals.md, Docs/Fishing.md).
    public class LifeTests
    {
        static readonly MaterialDefinition Rod = Gear.Data.FindMaterial(World.FishingRod);
        static readonly MaterialDefinition ChickenItem = Gear.Data.FindMaterial(World.Chicken);
        static readonly MaterialDefinition FeedItem = Gear.Data.FindMaterial(World.Feed);
        static readonly MaterialDefinition EggItem = Gear.Data.FindMaterial(World.Egg);
        static readonly MaterialDefinition BaitItem = Gear.Data.FindMaterial(World.Bait);
        static readonly MachineDefinition Coop = Tiles.Shipped.Machine(TileType.Coop);
        static readonly MachineDefinition Pot = Tiles.Shipped.Machine(TileType.CrabPot);

        // A room with a pool down its right side (x 13), the player at its middle.
        static World Pond(out Player player, Equipment wearing = null)
        {
            var map = Maps.Room(16);
            for (int y = 1; y < 15; y++) map[13, y] = TileType.Water;
            var world = new World(map, TileMap.Center(8, 8), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", wearing);
            return world;
        }

        [Test]
        public void LevelsTakeMoreExperienceEachTime()
        {
            Assert.AreEqual(0, LifeSkills.TotalFor(1));
            Assert.AreEqual(100, LifeSkills.TotalFor(2));
            Assert.AreEqual(300, LifeSkills.TotalFor(3));
            Assert.AreEqual(4500, LifeSkills.TotalFor(10));
            Assert.AreEqual(1, LifeSkills.LevelOf(99));
            Assert.AreEqual(2, LifeSkills.LevelOf(100));
            Assert.AreEqual(10, LifeSkills.LevelOf(999999));
            Assert.AreEqual(0, LifeSkills.Bonus(1, LifeSkills.DigPercent), "nothing at level 1");
            Assert.AreEqual(36, LifeSkills.Bonus(10, LifeSkills.DigPercent));
        }

        [Test]
        public void DiggingTeachesMiningAndMiningDigsFaster()
        {
            var world = Pond(out var player, Gear.Wearing("copper_pickaxe"));
            world.Map[8, 10] = TileType.Wall;
            Face(world, player, 8, 10, new Vec2(0, 1));
            for (int i = 0; i < 40 && world.Map[8, 10] == TileType.Wall; i++)
            {
                world.SetAttacking(player.Id, true);
                Run(world, 0.6f);
            }
            Assert.AreEqual(TileType.Floor, world.Map[8, 10]);
            Assert.AreEqual(1, player.LifeExperience[(int)LifeSkill.Mining], "plain rock: a third of its hardness 3");

            float before = World.DigPowerOf(player);
            player.LifeExperience[(int)LifeSkill.Mining] = LifeSkills.TotalFor(10);
            Assert.AreEqual(before * 1.36f, World.DigPowerOf(player), 0.001f);
        }

        [Test]
        public void ReachingALevelIsAnEvent()
        {
            var world = Pond(out var player);
            world.GainLifeExperience(player, LifeSkill.Farming, 99);
            Assert.IsFalse(world.TakeEvents().Any(e => e.Type == SimEventType.LifeLevel));
            world.GainLifeExperience(player, LifeSkill.Farming, 1);
            var level = world.TakeEvents().Single(e => e.Type == SimEventType.LifeLevel);
            Assert.AreEqual((int)LifeSkill.Farming * 16 + 2, level.Amount);
            Assert.AreEqual(2, World.LifeLevel(player, LifeSkill.Farming));
        }

        [Test]
        public void TheGaugeSlidesToAndFroAndHarderFishHaveSmallerZones()
        {
            Assert.AreEqual(0, World.GaugeAt(0, 1), 0.001f);
            Assert.AreEqual(0.5f, World.GaugeAt(World.CenterTicks(1), 1), 0.05f);
            Assert.AreEqual(0.5f, World.GaugeAt(World.CenterTicks(5), 5), 0.1f);
            Assert.Less(World.CatchHalfWidth(5, 1), World.CatchHalfWidth(1, 1));
            Assert.Greater(World.CatchHalfWidth(5, 10), World.CatchHalfWidth(5, 1), "the fishing skill widens it");
            Assert.IsFalse(World.InCatchZone(0, 1, 10), "the very edge never catches");
        }

        static void Bite(World world, Player player)
        {
            player.Inventory.AddMaterial(Rod);
            Face(world, player, 13, 8, new Vec2(1, 0));
            Assert.IsTrue(world.Fish(player.Id));
            for (int i = 0; i < World.Ticks(World.BiteMaxSeconds + 1) && !IsBiting(player); i++) world.Step();
            Assert.IsTrue(IsBiting(player), "a bite");
        }

        static bool IsBiting(Player player) => player.Fishing != null && player.Fishing.BiteTicks > 0;

        [Test]
        public void PressingAtTheEdgeLosesTheFishAndInTheMiddleLandsIt()
        {
            var world = Pond(out var player);
            Bite(world, player);
            world.Step();
            Assert.IsTrue(world.Fish(player.Id, 0), "pressed with the marker at the end");
            Assert.IsNull(player.Fishing);
            Assert.AreEqual(0, player.Inventory.Materials.Keys.Count(m => m.Fish != null));

            Bite(world, player);
            int difficulty = player.Fishing.Difficulty;
            Reels.Land(world, player);
            Assert.AreEqual(1, player.Inventory.Materials.Keys.Count(m => m.Fish != null));
            Assert.AreEqual(3 + 2 * difficulty, player.LifeExperience[(int)LifeSkill.Fishing]);
        }

        [Test]
        public void APressCannotClaimMoreTimeThanHasPassed()
        {
            var world = Pond(out var player);
            Bite(world, player);
            // The middle is some ticks away; claiming it now counts as now, the end of the gauge.
            Assert.IsTrue(world.Fish(player.Id, World.CenterTicks(player.Fishing.Difficulty)));
            Assert.AreEqual(0, player.Inventory.Materials.Keys.Count(m => m.Fish != null));
        }

        [Test]
        public void ChickensLayAnEggADayEachWhileThereIsFeed()
        {
            var world = Pond(out var player);
            Place(world, player, Gear.Data.FindMaterial("coop"), 8, 10, new Vec2(0, 1));
            player.Inventory.AddMaterial(ChickenItem, 5);
            player.Inventory.AddMaterial(FeedItem, 6);
            Assert.AreEqual(World.CoopChickens, world.LoadMachine(player.Id, ChickenItem, 5), "four at most");
            Assert.AreEqual(6, world.LoadMachine(player.Id, FeedItem, 6));
            Assert.AreEqual(4, World.ChickensIn(world.MachineAt(8, 10)));

            Run(world, Coop.Seconds + 1);
            Assert.AreEqual(4, world.MachineAt(8, 10).Outputs.GetValueOrDefault(EggItem));
            Run(world, Coop.Seconds + 1);
            Assert.AreEqual(6, world.MachineAt(8, 10).Outputs.GetValueOrDefault(EggItem), "only two feed were left");
            Assert.AreEqual(6, world.UnloadMachine(player.Id));
            Assert.AreEqual(6, player.Inventory.CountOf(EggItem));
        }

        [Test]
        public void ABaitedPotByTheWaterCatchesSomething()
        {
            var world = Pond(out var player);
            // (12, 8) is beside the water; (8, 10) is not.
            Place(world, player, Gear.Data.FindMaterial("crab_pot"), 12, 8, new Vec2(1, 0));
            player.Inventory.AddMaterial(BaitItem, 2);
            Assert.AreEqual(2, world.LoadMachine(player.Id, BaitItem, 5));
            Walk(world, player, TileMap.Center(8, 8), new Vec2(0, 1));
            Place(world, player, Gear.Data.FindMaterial("crab_pot"), 8, 10, new Vec2(0, 1));
            player.Inventory.AddMaterial(BaitItem, 2);
            Assert.AreEqual(2, world.LoadMachine(player.Id, BaitItem, 5));

            Run(world, Pot.Seconds * 2 + 1);
            var wet = world.MachineAt(12, 8);
            Assert.AreEqual(2, wet.Outputs.Values.Sum());
            Assert.IsTrue(wet.Outputs.Keys.All(m => m.PotCatch));
            Assert.AreEqual(0, world.MachineAt(8, 10).Outputs.Count, "no water beside it");
        }

        [Test]
        public void LifeSkillsAreKeptWithTheCharacterAndSentToThePlayer()
        {
            var data = CharacterSave.Write(new Equipment(), new Inventory(), 0, null, null, null, new[] { 120, 0, 400, 30 });
            CollectionAssert.AreEqual(new[] { 120, 0, 400, 30 }, CharacterSave.Read(data, Gear.Data).LifeExperience);

            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Character = data };
            host.Receive(1, client.JoinMessage());
            for (int i = 0; i < 8; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            Assert.AreEqual(400, host.World.Players.Single().LifeExperience[(int)LifeSkill.Mining]);
            Assert.AreEqual(2, client.LifeLevel(LifeSkill.Farming));
            CollectionAssert.AreEqual(new[] { 120, 0, 400, 30 }, CharacterSave.Read(client.SaveCharacter(), Gear.Data).LifeExperience);
        }
    
        [Test]
        public void SomeDaysRainAndWetEveryCrop()
        {
            var rainy = Enumerable.Range(1, 400).Count(World.IsRainy);
            Assert.That(rainy, Is.InRange(60, 140), "about one day in four");
            Assert.IsFalse(World.IsRainy(1), "never the first");

            var world = Pond(out var player);
            int day = Enumerable.Range(2, 100).First(World.IsRainy);
            var crop = Tiles.Shipped.Crops.Single(c => c.Id == "mushroom");
            Face(world, player, 8, 6, new Vec2(0, -1));
            player.Inventory.AddMaterial(crop.Seed);
            Assert.IsTrue(world.Plant(player.Id, crop));
            Assert.IsFalse(world.CropAt(8, 6).IsWet(world.Tick), "a dry first day");
            world.RestoreClock((day - 1) * Calendar.TicksPerDay);
            world.Step();
            Assert.IsTrue(world.CropAt(8, 6).IsWet(world.Tick), "the rain watered it");
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Rain && e.Amount == 1));
        }

        [Test]
        public void CookingAtAFurnaceTeachesCooking()
        {
            var world = Pond(out var player);
            Place(world, player, Gear.Data.FindMaterial("furnace"), 8, 6, new Vec2(0, -1));
            var roast = Gear.Data.Recipes.Single(r => r.Id == "roasted_mushroom");
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("mushroom"), 2);
            Assert.IsTrue(world.Craft(player.Id, roast));
            Assert.AreEqual(2 + 2, player.LifeExperience[(int)LifeSkill.Cooking]);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("slime_gel"), 3);
            Assert.IsTrue(world.Craft(player.Id, Gear.Data.Recipes.Single(r => r.Id == "healing_potion")));
            Assert.AreEqual(4, player.LifeExperience[(int)LifeSkill.Cooking], "a potion is no cooking");
        }
    
        [Test]
        public void CowsGiveMilkForTwoFeedEach()
        {
            var world = Pond(out var player);
            Place(world, player, Gear.Data.FindMaterial("barn"), 8, 10, new Vec2(0, 1));
            var cow = Gear.Data.FindMaterial(World.Cow);
            var milk = Gear.Data.FindMaterial(World.Milk);
            player.Inventory.AddMaterial(cow, 3);
            player.Inventory.AddMaterial(FeedItem, 5);
            Assert.AreEqual(2, world.LoadMachine(player.Id, cow, 3), "two at most");
            Assert.AreEqual(0, world.LoadMachine(player.Id, ChickenItem, 1), "no chickens in a barn");
            Assert.AreEqual(5, world.LoadMachine(player.Id, FeedItem, 5));
            var barn = Tiles.Shipped.Machine(TileType.Barn);
            Run(world, barn.Seconds + 1);
            Assert.AreEqual(2, world.MachineAt(8, 10).Outputs.GetValueOrDefault(milk));
            Run(world, barn.Seconds + 1);
            Assert.AreEqual(2, world.MachineAt(8, 10).Outputs.GetValueOrDefault(milk), "one feed left is not enough for a cow");
            Assert.AreEqual(2, World.AnimalsIn(world.MachineAt(8, 10)));
        }
    }
}
