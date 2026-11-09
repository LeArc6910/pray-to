using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // The factory's deeper end (Docs/Automation.md, 공장 깊이): generators that need no fuel, a furnace that runs only
    // on power, harvesters that plant what they are given, and filters that pick several materials.
    public class FactoryTests
    {
        static readonly MaterialDefinition IronOre = Gear.Data.FindMaterial("iron_ore");
        static readonly MaterialDefinition IronBar = Gear.Data.FindMaterial("iron_bar");
        static readonly MaterialDefinition CopperOre = Gear.Data.FindMaterial("copper_ore");
        static readonly MaterialDefinition Stone = Gear.Data.FindMaterial("stone");
        static readonly MaterialDefinition Gel = Gear.Data.FindMaterial("slime_gel");
        static readonly MaterialDefinition Spore = Gear.Data.FindMaterial("mushroom_spore");
        static readonly MaterialDefinition Chest = Gear.Data.FindMaterial("chest");
        static readonly MaterialDefinition WheelItem = Gear.Data.FindMaterial("water_wheel");
        static readonly MaterialDefinition SolarItem = Gear.Data.FindMaterial("solar_panel");
        static readonly MaterialDefinition ArcItem = Gear.Data.FindMaterial("arc_furnace");
        static readonly MaterialDefinition FilterItem = Gear.Data.FindMaterial("filter");
        static readonly MachineDefinition Arc = Tiles.Shipped.Machine(TileType.ArcFurnace);

        // A room with a pool of water in its top right corner at (13, 13) and (14, 13).
        static World Room(out Player player)
        {
            var map = Maps.Room(16);
            map[13, 13] = TileType.Water;
            map[14, 13] = TileType.Water;
            var world = new World(map, TileMap.Center(8, 8), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", null);
            return world;
        }

        [Test]
        public void TheSheetSaysWhatTheNewMachinesGiveAndDraw()
        {
            Assert.AreEqual(4, Tiles.Shipped.Machine(TileType.WaterWheel).Power);
            Assert.AreEqual(3, Tiles.Shipped.Machine(TileType.SolarPanel).Power);
            Assert.IsTrue(Arc.PowerOnly);
            Assert.IsFalse(Tiles.Shipped.Machine(TileType.Smelter).PowerOnly);
            Assert.IsTrue(World.IsGenerator(TileType.WaterWheel) && World.IsPowerNode(TileType.SolarPanel));
        }

        [Test]
        public void AWaterWheelTurnsOnlyBesideWater()
        {
            var world = Room(out var player);
            Place(world, player, WheelItem, 13, 12, new Vec2(0, 1));
            Place(world, player, WheelItem, 3, 3, new Vec2(0, 1));
            Run(world, 0.1f);

            var wet = world.MachineAt(13, 12);
            var dry = world.MachineAt(3, 3);
            Assert.IsTrue(world.IsPowered(wet));
            Assert.AreEqual(4, world.GridOf(wet).Supply, "no fuel needed");
            Assert.IsFalse(world.IsPowered(dry), "nothing to turn it");
            Assert.AreEqual(0, world.GridOf(dry).Supply);
            Assert.AreEqual(0, player.Inventory.CountOf(Gel));
        }

        [Test]
        public void ASolarPanelWorksByDayAndRestsAtNight()
        {
            var world = Room(out var player);
            Place(world, player, SolarItem, 4, 4, new Vec2(0, 1));
            Run(world, 0.1f);
            var panel = world.MachineAt(4, 4);
            Assert.IsTrue(world.IsPowered(panel), "the first morning");
            Assert.AreEqual(3, world.GridOf(panel).Supply);

            // 23:00, an hour into the night.
            world.RestoreClock(Calendar.TicksPerDay * 17 / 24);
            Assert.IsTrue(Calendar.IsNight(world.Clock));
            Run(world, 0.1f);
            Assert.IsFalse(world.IsPowered(panel));
            Assert.AreEqual(0, world.GridOf(panel).Supply);
        }

        [Test]
        public void AnArcFurnaceRunsOnlyOnPowerAndGetsMoreOutOfTheOre()
        {
            var world = Room(out var player);
            Place(world, player, ArcItem, 8, 12, new Vec2(0, 1));
            var furnace = world.MachineAt(8, 12);
            Face(world, player, 8, 12, new Vec2(0, 1));
            player.Inventory.AddMaterial(IronOre, 8);
            Assert.AreEqual(8, world.LoadMachine(player.Id, IronOre, 8));

            Run(world, Arc.Seconds * 3);
            Assert.AreEqual(0, furnace.Outputs.Values.Sum(), "nothing without power");

            // A generator burning gel gives 8, more than the 6 it draws.
            var generator = Gear.Data.FindMaterial("generator");
            Place(world, player, generator, 6, 12, new Vec2(0, 1));
            player.Inventory.AddMaterial(Gel, 4);
            Assert.IsTrue(world.Refuel(player.Id, Gel));
            Run(world, Arc.Seconds * 4 + 0.2f);

            Assert.AreEqual(0, furnace.Inputs.Values.Sum(), "four smelts of two ores");
            Assert.AreEqual(6, furnace.Outputs.GetValueOrDefault(IronBar), "four bars and one more for every two");
        }

        [Test]
        public void ABigSmelterTakesFourTilesAndSmeltsThriceASmeltersPaceOnPower()
        {
            // 2026-11-09: the second big machine.
            var world = Room(out var player);
            var item = Gear.Data.FindMaterial("big_smelter");
            Assert.AreEqual((8, 9), World.BigOrigin(8, 10, new Vec2(0, -1), 2), "facing down, the square reaches down from the tile ahead");
            Place(world, player, item, 8, 10, new Vec2(0, 1));
            var big = world.MachineAt(8, 10);
            Assert.AreEqual(TileType.BigSmelter, big.Kind);
            foreach (var (x, y) in new[] { (9, 10), (8, 11), (9, 11) }) Assert.AreSame(big, world.MachineAt(x, y), "a part is the machine");

            // Ore in from beside a part; nothing without power.
            player.Position = TileMap.Center(10, 11);
            world.MovePlayer(player.Id, player.Position, new Vec2(-1, 0), 10);
            player.Inventory.AddMaterial(IronOre, 12);
            Assert.Greater(world.LoadMachine(player.Id, IronOre, 12), 0);
            Run(world, 3);
            Assert.AreEqual(0, big.Outputs.Values.Sum(), "nothing without power");

            world.Build(6, 10, TileType.Generator);
            player.Position = TileMap.Center(6, 9);
            world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 10);
            player.Inventory.AddMaterial(Gel, 4);
            Assert.IsTrue(world.Refuel(player.Id, Gel));
            Run(world, 4.2f);
            int bars = big.Outputs.GetValueOrDefault(IronBar);
            Assert.GreaterOrEqual(bars, 3, $"three smelts in 4.2 seconds, where a smelter takes 4 each ({bars})");
        }

        [Test]
        public void ABigAssemblerTakesFourTilesRunsOnPowerAndComesUpWhole()
        {
            var world = Room(out var player);
            var bigItem = Gear.Data.FindMaterial("big_assembler");
            var gear = Gear.Data.FindMaterial("iron_gear");
            // Facing up from below: its own tile at (8, 10), the square on up and to the right.
            Place(world, player, bigItem, 8, 10, new Vec2(0, 1));
            var big = world.MachineAt(8, 10);
            Assert.AreEqual(TileType.BigAssembler, big.Kind);
            foreach (var (x, y) in new[] { (9, 10), (8, 11), (9, 11) })
            {
                Assert.AreEqual(TileType.MachinePart, world.Map[x, y]);
                Assert.AreSame(big, world.MachineAt(x, y), "a part is the machine");
            }
            Assert.AreEqual((8, 12), big.Front, "the tile in front is past its far side");

            // Set and loaded from beside a part (stood there: walking straight over would bump into it).
            player.Position = TileMap.Center(10, 11);
            world.MovePlayer(player.Id, player.Position, new Vec2(-1, 0), 10);
            Assert.IsTrue(world.SetRecipe(player.Id, Gear.Data.Recipes.Single(r => r.Id == "iron_gear")));
            player.Inventory.AddMaterial(IronBar, 4);
            Assert.AreEqual(2, world.LoadMachine(player.Id, IronBar, 4), "twice what one make needs");
            Run(world, 3);
            Assert.AreEqual(0, big.Outputs.Values.Sum(), "nothing without power");

            world.Build(6, 10, TileType.Generator);
            player.Position = TileMap.Center(6, 9);
            world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 10);
            player.Inventory.AddMaterial(Gel, 4);
            Assert.IsTrue(world.Refuel(player.Id, Gel));
            Run(world, 2.5f);
            Assert.AreEqual(4, big.Outputs.GetValueOrDefault(gear), "two makes of two gears in a couple of seconds");

            // Struck on a part, the whole of it comes up and back to the bag.
            player.Position = TileMap.Center(10, 11);
            world.MovePlayer(player.Id, player.Position, new Vec2(-1, 0), 10);
            int had = player.Inventory.CountOf(bigItem);
            world.SetAttacking(player.Id, true);
            for (int i = 0; i < World.Ticks(6) && world.Map[8, 10] == TileType.BigAssembler; i++) world.Step();
            world.SetAttacking(player.Id, false);
            foreach (var (x, y) in World.Footprint(8, 10, 2)) Assert.AreEqual(TileType.Floor, world.Map[x, y]);
            Assert.AreEqual(had + 1, player.Inventory.CountOf(bigItem));
            Assert.IsNull(world.MachineAt(9, 11));
        }

        [Test]
        public void AHarvesterPlantsTheSeedsItIsGiven()
        {
            var world = Room(out var player);
            // Soil on the three tiles just ahead of a harvester at (5, 5) facing up; the rows past them stay floor.
            for (int x = 4; x <= 6; x++) world.Map[x, 6] = TileType.Soil;
            Place(world, player, Gear.Data.FindMaterial("harvester"), 5, 5, new Vec2(0, 1));
            var harvester = world.MachineAt(5, 5);
            Face(world, player, 5, 5, new Vec2(0, 1));
            player.Inventory.AddMaterial(Spore, 5);
            Assert.AreEqual(5, world.LoadMachine(player.Id, Spore, 5));
            Assert.AreEqual(0, world.LoadMachine(player.Id, Stone, 1), "only seeds go in");

            Run(world, harvester.Definition.Seconds * 5 + 0.2f);

            for (int x = 4; x <= 6; x++) Assert.IsNotNull(world.CropAt(x, 6), $"planted at ({x}, 6)");
            Assert.IsNull(world.CropAt(5, 7), "only on soil");
            Assert.AreEqual(2, harvester.Inputs.GetValueOrDefault(Spore), "the rest wait for room");
        }

        [Test]
        public void AFilterPassesSeveralMaterialsStraightOn()
        {
            var world = Room(out var player);
            Place(world, player, FilterItem, 7, 8, new Vec2(1, 0));
            Assert.IsTrue(world.SetFilter(player.Id, IronOre));
            Assert.IsTrue(world.SetFilter(player.Id, CopperOre));
            var filter = world.MachineAt(7, 8);
            CollectionAssert.AreEqual(new[] { IronOre, CopperOre }, filter.Settings);
            Assert.IsTrue(world.SetFilter(player.Id, IronOre));
            CollectionAssert.AreEqual(new[] { CopperOre }, filter.Settings, "choosing one again takes it off");
            var silver = Gear.Data.Materials.First(m => m.Kind != m);
            Assert.IsTrue(world.SetFilter(player.Id, silver));
            CollectionAssert.AreEqual(new[] { CopperOre, silver.Kind }, filter.Settings, "a quality is picked as its plain kind");
            Assert.IsTrue(world.SetFilter(player.Id, silver.Kind));
            Assert.IsTrue(world.SetFilter(player.Id, IronOre));
            Assert.IsTrue(world.SetFilter(player.Id, Stone));
            Assert.IsTrue(world.SetFilter(player.Id, IronBar));
            Assert.IsFalse(world.SetFilter(player.Id, Gel), $"{World.FilterPicks} at most");

            Place(world, player, Chest, 7, 9, new Vec2(0, -1));
            Place(world, player, Chest, 8, 8, new Vec2(-1, 0));
            Place(world, player, Chest, 7, 7, new Vec2(0, 1));
            Place(world, player, Gear.Data.FindMaterial("robot_arm"), 6, 8, new Vec2(1, 0));
            Place(world, player, Chest, 5, 8, new Vec2(1, 0));
            world.MovePlayer(player.Id, TileMap.Center(12, 4), new Vec2(1, 0), 30);
            world.ChestAt(5, 8).AddMaterial(IronOre, 2);
            world.ChestAt(5, 8).AddMaterial(CopperOre, 2);
            world.ChestAt(5, 8).AddMaterial(Gel, 2);

            Run(world, 20);

            Assert.AreEqual(2, world.ChestAt(8, 8).CountOf(IronOre), "both picks straight on");
            Assert.AreEqual(2, world.ChestAt(8, 8).CountOf(CopperOre));
            Assert.AreEqual(2, world.ChestAt(7, 9).CountOf(Gel) + world.ChestAt(7, 7).CountOf(Gel), "the rest aside");
        }

        [Test]
        public void AFiltersPicksGoThroughASaveAndOverTheWire()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var player = host.World.AddPlayer("a", null);
            player.Inventory.AddMaterial(FilterItem);
            Assert.IsTrue(host.World.PlaceTile(player.Id, FilterItem));
            Assert.IsTrue(host.World.SetFilter(player.Id, IronBar));
            Assert.IsTrue(host.World.SetFilter(player.Id, Stone));
            var placed = host.World.Machines.Single();

            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            var again = new WorldHost(save.Seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            again.Restore(save);
            CollectionAssert.AreEqual(new[] { IronBar, Stone }, again.World.MachineAt(placed.X, placed.Y).Settings);

            var message = MachinesMessage.Decode(new MachinesMessage(new[]
            {
                new MachineState(3, 4, TileType.Filter, 1, -1, 0, picks: new[] { 5, 9 }),
                new MachineState(4, 4, TileType.ArcFurnace, 0, -1, 0.5f),
            }).Encode());
            CollectionAssert.AreEqual(new[] { 5, 9 }, message.Machines[0].Picks);
            Assert.IsEmpty(message.Machines[1].Picks);
            Assert.AreEqual(TileType.ArcFurnace, message.Machines[1].Kind);
        }
    }
}
