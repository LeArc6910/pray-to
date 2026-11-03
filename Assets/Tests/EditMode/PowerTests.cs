using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // Power grids, fast and underground belts, and the production list (Docs/Automation.md, 전력망, 물류, 생산 통계).
    public class PowerTests
    {
        static readonly MaterialDefinition IronOre = Gear.Data.FindMaterial("iron_ore");
        static readonly MaterialDefinition Gel = Gear.Data.FindMaterial("slime_gel");
        static readonly MaterialDefinition Chest = Gear.Data.FindMaterial("chest");
        static readonly MaterialDefinition DrillItem = Gear.Data.FindMaterial("drill");
        static readonly MaterialDefinition GeneratorItem = Gear.Data.FindMaterial("generator");
        static readonly MaterialDefinition PoleItem = Gear.Data.FindMaterial("power_pole");
        static readonly MaterialDefinition BeltItem = Gear.Data.FindMaterial("conveyor");
        static readonly MaterialDefinition FastBeltItem = Gear.Data.FindMaterial("fast_conveyor");
        static readonly MaterialDefinition UndergroundItem = Gear.Data.FindMaterial("underground_belt");
        static readonly MachineDefinition Drill = Tiles.Shipped.Machine(TileType.Drill);
        static readonly MachineDefinition Generator = Tiles.Shipped.Machine(TileType.Generator);

        // A room with iron veins down its left side at (1, 3), (1, 5) … (1, 11).
        static World Veins(out Player player, int veins = 1)
        {
            var map = Maps.Room(16);
            for (int i = 0; i < veins; i++) map[1, 3 + 2 * i] = TileType.IronOre;
            var world = new World(map, TileMap.Center(8, 8), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", null);
            return world;
        }

        // A drill on the vein at (1, y) with a chest behind it.
        static void DrillAt(World world, Player player, int y)
        {
            Place(world, player, DrillItem, 2, y, new Vec2(-1, 0));
            Place(world, player, Chest, 3, y, new Vec2(-1, 0));
        }

        static void Fuel(World world, Player player, int x, int y, int gels)
        {
            Face(world, player, x, y, new Vec2(0, 1));
            player.Inventory.AddMaterial(Gel, gels);
            Assert.IsTrue(world.Refuel(player.Id, Gel));
        }

        [Test]
        public void TheMachineSheetSaysWhatEachDrawsAndAGeneratorGives()
        {
            Assert.AreEqual(2, Drill.Power);
            Assert.AreEqual(8, Generator.Power);
            Assert.AreEqual(0, Tiles.Shipped.Machine(TileType.Conveyor).Power, "belts are not sped up");
            Assert.AreEqual(Tiles.Shipped.Machine(TileType.Conveyor).Seconds / 2, Tiles.Shipped.Machine(TileType.FastConveyor).Seconds);
            Assert.AreEqual(TileType.PowerPole, PoleItem.Places);
        }

        [Test]
        public void PolesCarryAGeneratorsPowerToMachinesFarAway()
        {
            var world = Veins(out var player);
            DrillAt(world, player, 3);
            Place(world, player, GeneratorItem, 12, 12, new Vec2(0, 1));
            Fuel(world, player, 12, 12, 3);
            Run(world, 0.1f);
            Assert.IsFalse(world.IsPowered(world.MachineAt(2, 3)), "too far from the generator");

            // (7, 7) is in wire reach of the generator, (4, 4) of that pole and in its power reach of the drill.
            Place(world, player, PoleItem, 7, 7, new Vec2(0, 1));
            Place(world, player, PoleItem, 4, 4, new Vec2(0, 1));
            Assert.AreSame(world.GridOf(world.MachineAt(12, 12)), world.GridOf(world.MachineAt(4, 4)), "one grid");
            int before = world.ChestAt(3, 3).CountOf(IronOre);
            Run(world, Drill.Seconds * 2 + 0.2f);

            Assert.IsTrue(world.IsPowered(world.MachineAt(2, 3)));
            Assert.AreEqual(before + 4, world.ChestAt(3, 3).CountOf(IronOre), "two ores in each drill time");
            var grid = world.GridOf(world.MachineAt(7, 7));
            Assert.AreEqual(8, grid.Supply);
            Assert.AreEqual(2, grid.Demand);
        }

        [Test]
        public void TooLittlePowerIsShared()
        {
            var world = Veins(out var player, veins: 5);
            for (int i = 0; i < 5; i++) DrillAt(world, player, 3 + 2 * i);
            // In power reach of all five drills: they draw 10, it gives 8.
            Place(world, player, GeneratorItem, 5, 7, new Vec2(0, 1));
            Fuel(world, player, 5, 7, 3);
            Run(world, Drill.Seconds * 5);

            var grid = world.GridOf(world.MachineAt(5, 7));
            Assert.AreEqual(8, grid.Supply);
            Assert.AreEqual(10, grid.Demand);
            int ores = Enumerable.Range(0, 5).Sum(i => world.ChestAt(3, 3 + 2 * i).CountOf(IronOre));
            // Without power 25, with enough 50; with four fifths of enough, 1.8 times.
            Assert.AreEqual(45, ores, 3);
        }

        [Test]
        public void AFastBeltCarriesTwiceAsFast()
        {
            var world = Veins(out var player);
            for (int x = 3; x <= 6; x++) Place(world, player, BeltItem, x, 5, new Vec2(1, 0));
            Place(world, player, Chest, 7, 5, new Vec2(1, 0));
            for (int x = 3; x <= 6; x++) Place(world, player, FastBeltItem, x, 9, new Vec2(1, 0));
            Place(world, player, Chest, 7, 9, new Vec2(1, 0));
            world.MachineAt(3, 5).Carried = IronOre;
            world.MachineAt(3, 9).Carried = IronOre;

            Run(world, 1.2f);
            Assert.AreEqual(1, world.ChestAt(7, 9).CountOf(IronOre), "four fast tiles in a second");
            Assert.AreEqual(0, world.ChestAt(7, 5).CountOf(IronOre));
            Run(world, 1);
            Assert.AreEqual(1, world.ChestAt(7, 5).CountOf(IronOre));
        }

        [Test]
        public void AnUndergroundBeltPassesUnderABeltCrossingIt()
        {
            var world = Veins(out var player);
            // Underground (3, 8) → under the belt at (4, 8) going up into the chest at (4, 9) → up at (6, 8) → chest (7, 8).
            Place(world, player, Chest, 7, 8, new Vec2(1, 0));
            Place(world, player, UndergroundItem, 6, 8, new Vec2(1, 0));
            Place(world, player, Chest, 4, 9, new Vec2(0, 1));
            Place(world, player, BeltItem, 4, 8, new Vec2(0, 1));
            Walk(world, player, TileMap.Center(2, 6), new Vec2(1, 0));
            Place(world, player, UndergroundItem, 3, 8, new Vec2(1, 0));
            Assert.AreSame(world.MachineAt(6, 8), world.UndergroundExit(world.MachineAt(3, 8)));
            Assert.IsNull(world.UndergroundExit(world.MachineAt(6, 8)), "nothing ahead of the exit");

            world.MachineAt(3, 8).Carried = IronOre;
            world.MachineAt(4, 8).Carried = Gel;
            Run(world, 2);

            Assert.AreEqual(1, world.ChestAt(7, 8).CountOf(IronOre), "under, up, and on");
            Assert.AreEqual(0, world.ChestAt(7, 8).CountOf(Gel));
            Assert.AreEqual(1, world.ChestAt(4, 9).CountOf(Gel), "the crossing belt carries on undisturbed");
        }

        [Test]
        public void MachinesCountWhatTheyMakeForTheLastTenMinutes()
        {
            var world = Veins(out var player);
            DrillAt(world, player, 3);
            Run(world, Drill.Seconds * 3 + 0.5f);

            var made = world.Production();
            Assert.AreEqual(1, made.Count);
            Assert.AreSame(IronOre, made[0].Made);
            Assert.AreEqual(3, made[0].Count);
            Assert.AreEqual((Drill.Seconds * 3 + 0.5f) / 60, world.ProductionSpan, 0.01f);

            // Off: after the window nothing is left.
            Walk(world, player, TileMap.Center(4, 5), new Vec2(0, -1));
            Face(world, player, 2, 3, new Vec2(0, -1));
            Assert.IsTrue(world.ToggleMachine(player.Id));
            Run(world, 60 * World.ProductionMinutes + 1);
            Assert.AreEqual(0, world.Production().Count);
            Assert.AreEqual(World.ProductionMinutes, world.ProductionSpan, 1);
        }

        [Test]
        public void PlayersHearWhatWasMadeAndWhatTheGridGives()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            var player = host.World.Players.Single();
            // Facing down from the spawn: a generator there with fuel.
            player.Inventory.AddMaterial(GeneratorItem);
            Assert.IsTrue(host.World.PlaceTile(player.Id, GeneratorItem));
            player.Inventory.AddMaterial(Gel, 2);
            Assert.IsTrue(host.World.Refuel(player.Id, Gel));

            for (int i = 0; i < World.Ticks(6); i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            var generator = client.Machines().Single();
            Assert.AreEqual(8, generator.Supply);
            Assert.AreEqual(0, generator.Demand);
            Assert.Greater(client.ProductionMinutes, 0);
            Assert.AreEqual(0, client.Production.Count, "nothing made yet");
        }
    }
}
