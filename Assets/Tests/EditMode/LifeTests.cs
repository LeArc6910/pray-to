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
        public void APerkIsPickedOnceAtItsLevelAndKeptInTheSave()
        {
            var world = Pond(out var player);
            Assert.IsFalse(world.ChoosePerk(player.Id, LifeSkill.Mining, 0, 2), "not before level 5");
            player.LifeExperience[(int)LifeSkill.Mining] = LifeSkills.TotalFor(5);
            float before = World.DigPowerOf(player);
            Assert.IsTrue(world.ChoosePerk(player.Id, LifeSkill.Mining, 0, 2));
            Assert.Greater(World.DigPowerOf(player), before, "the quick pickaxe digs faster");
            Assert.IsFalse(world.ChoosePerk(player.Id, LifeSkill.Mining, 0, 1), "kept for good");
            Assert.IsFalse(world.ChoosePerk(player.Id, LifeSkill.Mining, 1, 1), "level 10's not yet");

            var saved = CharacterSave.Read(CharacterSave.Write(new Equipment(), new Inventory(), 0, lifePerks: player.LifePerks), Gear.Data);
            Assert.AreEqual(2, saved.LifePerks[LifeSkills.PerkIndex(LifeSkill.Mining, 0)]);
            var other = Pond(out var again);
            World.RestoreLifePerks(again, saved.LifePerks);
            Assert.IsTrue(World.HasPerk(again, LifeSkill.Mining, 0, 2));
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
        public void TheWeatherComesInFourKinds()
        {
            var days = Enumerable.Range(1, 2000).Select(World.WeatherOf).ToList();
            Assert.AreEqual(Weather.Clear, days[0], "never on the first day");
            Assert.That(days.Count(w => w == Weather.Storm), Is.InRange(100, 230));
            Assert.That(days.Count(w => w == Weather.Fog), Is.InRange(160, 330));
            for (int day = 1; day <= 2000; day++)
                Assert.AreEqual(World.WeatherOf(day) is Weather.Rain or Weather.Storm, World.IsRainy(day), $"day {day}: a storm wets the crops too");
        }

        [Test]
        public void InFogMonstersNoticePlayersLater()
        {
            var slime = Monsters.Shipped.Find("slime");
            bool Noticed(Weather weather)
            {
                var world = new World(Maps.Room(30), TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
                int day = Enumerable.Range(2, 400).First(d => World.WeatherOf(d) == weather);
                world.RestoreClock((day - 1) * Calendar.TicksPerDay);
                var player = world.AddPlayer("a", null);
                var monster = world.SpawnEnemy(slime, player.Position + new Vec2(slime.Aggro * 0.85f, 0));
                world.Step();
                world.Step();
                return monster.State != EnemyState.Idle;
            }
            Assert.IsTrue(Noticed(Weather.Clear));
            Assert.IsFalse(Noticed(Weather.Fog), $"fog: only {World.FogAggroPercent}% as far");
        }

        [Test]
        public void AStormHidesTheSunAndSwellsTheWater()
        {
            var map = Maps.Room(16);
            map[8, 10] = TileType.Water;
            var world = new World(map, TileMap.Center(8, 4), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            Place(world, player, Gear.Data.FindMaterial("water_wheel"), 8, 9, new Vec2(0, 1));
            Place(world, player, Gear.Data.FindMaterial("solar_panel"), 2, 2, new Vec2(0, 1));
            int Supply(int x, int y) => world.GridOf(world.MachineAt(x, y)).Supply;

            world.Step();
            Assert.AreEqual(4, Supply(8, 9));
            Assert.AreEqual(3, Supply(2, 2));
            int stormy = Enumerable.Range(2, 400).First(d => World.WeatherOf(d) == Weather.Storm);
            world.RestoreClock((stormy - 1) * Calendar.TicksPerDay);
            world.Step();
            Assert.AreEqual(6, Supply(8, 9), "half again in a storm");
            Assert.AreEqual(0, Supply(2, 2), "no sun in a storm");
        }

        [Test]
        public void TheCalendarHasMarketDaysFestivalsAndFullMoons()
        {
            Assert.AreEqual(DayEvent.None, World.EventOf(1));
            Assert.AreEqual(DayEvent.Market, World.EventOf(5));
            Assert.AreEqual(DayEvent.Harvest, World.EventOf(7));
            Assert.AreEqual(DayEvent.FullMoon, World.EventOf(8));
            Assert.AreEqual(DayEvent.Harvest, World.EventOf(35), "the rarer wins");
            Assert.AreEqual(DayEvent.FullMoon, World.EventOf(40));

            var seeds = People.Shipped.Shop.First(e => e.Buy >= 20 && e.Material != null);
            Assert.AreEqual(World.BuyPrice(seeds, 0) * World.MarketPercent / 100f, World.BuyPrice(seeds, 0, 5), 1);
            var crop = People.Shipped.Shop.First(e => e.Sell >= 10 && e.Material?.HasQuality == true);
            var bar = People.Shipped.Shop.First(e => e.Sell > 0 && e.Material?.Id == "iron_bar");
            Assert.AreEqual(World.SellPrice(crop, 0) * World.HarvestPercent / 100, World.SellPrice(crop, 0, 0, 7));
            Assert.AreEqual(World.SellPrice(bar, 0), World.SellPrice(bar, 0, 0, 7), "only crops");

            // A full moon's night: half again the experience for a kill.
            int Experience(int day, float hour)
            {
                var world = new World(Maps.Room(20), TileMap.Center(10, 10), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
                world.RestoreClock((long)((day - 1 + hour / 24) * Calendar.TicksPerDay));
                var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
                var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(1, 0));
                slime.Health = 1;
                world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
                world.SetAttacking(player.Id, true);
                for (int i = 0; i < 10 && !slime.IsDead; i++) world.Step();
                return player.Experience;
            }
            Assert.AreEqual(Experience(9, 18) * World.FullMoonExperiencePercent / 100, Experience(8, 18), 1, "the full moon's night (22:00 is 16 hours past six)");
        }

        [Test]
        public void DaylightFallsThroughWellsNearTheSpawnAndHereAndThere()
        {
            var generated = WorldGenerator.Generate(7);
            var wells = World.SkyWells(generated.Map, generated.Spawn, 7);
            CollectionAssert.AreEqual(wells, World.SkyWells(generated.Map, generated.Spawn, 7), "the same from the seed");
            Assert.Greater(wells.Count, 3);
            Assert.IsTrue(wells.All(w => generated.Map[w.X, w.Y] == TileType.Floor));
            Assert.LessOrEqual(Vec2.Distance(TileMap.Center(wells[0].X, wells[0].Y), generated.Spawn), 9, "one by the spawn");
            Assert.IsTrue(TileMap.IsPaving(TileType.Skylight), "a skylight is laid like a floor");
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

        [Test]
        public void SheepGiveWoolAndPettedAnimalsGiveBetterThings()
        {
            var world = Pond(out var player);
            Place(world, player, Gear.Data.FindMaterial("fold"), 8, 10, new Vec2(0, 1));
            var sheep = Gear.Data.FindMaterial(World.Sheep);
            var wool = Gear.Data.FindMaterial(World.Wool);
            var fold = world.MachineAt(8, 10);
            Assert.IsFalse(world.PetAnimals(player.Id), "an empty fold has no one to pet");
            player.Inventory.AddMaterial(sheep, 4);
            player.Inventory.AddMaterial(FeedItem, 10);
            Assert.AreEqual(3, world.LoadMachine(player.Id, sheep, 4), "three at most");
            Assert.AreEqual(10, world.LoadMachine(player.Id, FeedItem, 10));

            // Once a day.
            Assert.IsTrue(world.PetAnimals(player.Id));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.AnimalsPetted && e.Amount == 1));
            Assert.IsFalse(world.PetAnimals(player.Id), "already petted today");
            Assert.AreEqual(1, fold.Affection);

            // Two days for three wool, one feed each.
            Run(world, Tiles.Shipped.Machine(TileType.Fold).Seconds + 1);
            Assert.AreEqual(3, wool.AllQualities.Sum(w => fold.Outputs.GetValueOrDefault(w)));
            Assert.AreEqual(7, fold.Inputs.GetValueOrDefault(FeedItem));
            Assert.IsTrue(world.PetAnimals(player.Id), "a new day");
            Assert.AreEqual(2, fold.Affection);

            // Fond animals give silver and gold more often; none at all, only plain.
            var egg = Gear.Data.FindMaterial(World.Egg);
            int fine = Enumerable.Range(0, 1000).Count(_ => world.AnimalQuality(egg, World.AffectionMost) != egg);
            Assert.That(fine, Is.InRange(600, 760), "about 68% silver or gold at the most");
            Assert.IsTrue(Enumerable.Range(0, 200).All(_ => world.AnimalQuality(egg, 0) == egg));

            // Left alone too long, they cool by one each time they give.
            fold.Affection = 5;
            fold.PettedDay = world.Today - World.AffectionPatience - 1;
            Run(world, Tiles.Shipped.Machine(TileType.Fold).Seconds + 1);
            Assert.AreEqual(4, fold.Affection);
        }
    }
}
