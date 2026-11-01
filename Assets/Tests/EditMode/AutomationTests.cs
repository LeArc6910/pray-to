using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class AutomationTests
    {
        static readonly MaterialDefinition CopperOre = Gear.Data.FindMaterial("copper_ore");
        static readonly MaterialDefinition IronOre = Gear.Data.FindMaterial("iron_ore");
        static readonly MaterialDefinition IronBar = Gear.Data.FindMaterial("iron_bar");
        static readonly MaterialDefinition DrillItem = Gear.Data.FindMaterial("drill");
        static readonly MaterialDefinition BeltItem = Gear.Data.FindMaterial("conveyor");
        static readonly MaterialDefinition SmelterItem = Gear.Data.FindMaterial("smelter");
        static readonly MachineDefinition Drill = Tiles.Shipped.Machine(TileType.Drill);
        static readonly MachineDefinition Belt = Tiles.Shipped.Machine(TileType.Conveyor);
        static readonly MachineDefinition Smelter = Tiles.Shipped.Machine(TileType.Smelter);
        static readonly MachineDefinition Sprinkler = Tiles.Shipped.Machine(TileType.Sprinkler);
        static readonly MachineDefinition Harvester = Tiles.Shipped.Machine(TileType.Harvester);
        static readonly MaterialDefinition SprinklerItem = Gear.Data.FindMaterial("sprinkler");
        static readonly MaterialDefinition HarvesterItem = Gear.Data.FindMaterial("harvester");
        static readonly MaterialDefinition Spore = Gear.Data.FindMaterial("mushroom_spore");
        static readonly MaterialDefinition MushroomItem = Gear.Data.FindMaterial("mushroom");
        static readonly CropDefinition Mushroom = Tiles.Shipped.Crops.Single(c => c.Id == "mushroom");

        // A long room with an iron vein at its left end (1, 3).
        static World Line(out Player player)
        {
            var map = Maps.Room(16);
            map[1, 3] = TileType.IronOre;
            var world = new World(map, TileMap.Center(8, 8), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", null);
            return world;
        }

        // Stands the player on the tile beside the target facing it, and places the material there.
        static void Place(World world, Player player, MaterialDefinition material, int x, int y, Vec2 facing)
        {
            Face(world, player, x, y, facing);
            player.Inventory.AddMaterial(material);
            Assert.IsTrue(world.PlaceTile(player.Id, material), $"{material.Id} at ({x}, {y})");
            Assert.AreEqual(material.Places, world.Map[x, y], $"{material.Id} landed somewhere else");
        }

        // Stands the player on the tile beside (x, y), facing it that way.
        static void Face(World world, Player player, int x, int y, Vec2 facing)
        {
            var stand = TileMap.Center(x, y) - facing;
            for (int i = 0; i < 20 && Vec2.Distance(player.Position, stand) > 0.01f; i++)
            {
                world.MovePlayer(player.Id, player.Position + (stand - player.Position).ClampLength(1.5f), facing, 10);
            }
            world.MovePlayer(player.Id, player.Position, facing, 1);
        }

        static void Sow(World world, Player player, int x, int y, Vec2 facing)
        {
            Face(world, player, x, y, facing);
            player.Inventory.AddMaterial(Spore);
            Assert.IsTrue(world.Plant(player.Id, Mushroom), $"a spore at ({x}, {y})");
        }

        static void Run(World world, float seconds)
        {
            for (int i = 0; i < World.Ticks(seconds); i++) world.Step();
            world.TakeEvents();
        }

        [Test]
        public void MachinesHaveTheirNumbers()
        {
            Assert.IsTrue(Tiles.Shipped.Success, string.Join("\n", Tiles.Shipped.Errors));
            Assert.AreEqual(8, Drill.Seconds, 0.001f);
            Assert.AreEqual(0.5f, Belt.Seconds, 0.001f);
            Assert.AreEqual(4, Smelter.Seconds, 0.001f);
            Assert.IsTrue(Tiles.Shipped.Find(TileType.IronOre).Drillable);
            Assert.IsFalse(Tiles.Shipped.Find(TileType.Wall).Drillable, "plain rock gives nothing to a drill");
            Assert.AreEqual(4, Gear.Data.Recipes.Single(r => r.Id == "conveyor").Count);
            Assert.AreEqual(3, Sprinkler.Seconds, 0.001f);
            Assert.AreEqual(1, Harvester.Seconds, 0.001f);
            Assert.AreEqual(20, Harvester.Capacity);
            Assert.AreEqual(TileType.Sprinkler, SprinklerItem.Places);
            Assert.AreEqual(TileType.Harvester, HarvesterItem.Places);
        }

        [Test]
        public void MachinesFaceTheWayThePlayerFaced()
        {
            Assert.AreEqual(0, Machine.DirectionOf(new Vec2(1, 0.2f)));
            Assert.AreEqual(1, Machine.DirectionOf(new Vec2(0.1f, 1)));
            Assert.AreEqual(2, Machine.DirectionOf(new Vec2(-1, -0.5f)));
            Assert.AreEqual(3, Machine.DirectionOf(new Vec2(0.3f, -1)));
        }

        [Test]
        public void ADrillPullsOreIntoTheChestBehindIt()
        {
            var world = Line(out var player);
            // Facing left into the vein from (2, 3), with a chest at (3, 3) behind it.
            Place(world, player, DrillItem, 2, 3, new Vec2(-1, 0));
            Place(world, player, Gear.Data.FindMaterial("chest"), 3, 3, new Vec2(-1, 0));
            Assert.AreEqual(2, world.MachineAt(2, 3).Direction);

            Run(world, Drill.Seconds * 3 + 0.5f);

            Assert.AreEqual(3, world.ChestAt(3, 3).CountOf(IronOre));
            Assert.AreEqual(TileType.IronOre, world.Map[1, 3], "the vein is not used up");
        }

        [Test]
        public void AFullDrillWithNowhereToSendStops()
        {
            var world = Line(out var player);
            Place(world, player, DrillItem, 2, 3, new Vec2(-1, 0));

            Run(world, Drill.Seconds * (Drill.Capacity + 3));

            Assert.AreEqual(Drill.Capacity, world.MachineAt(2, 3).Outputs.Values.Sum());
        }

        [Test]
        public void BeltsCarryOreAlongToAChest()
        {
            var world = Line(out var player);
            // Belts from (3, 3) to (6, 3) running right, then a chest, then the drill from the first belt. Each is
            // placed from the tile before it, so the order matters.
            for (int x = 3; x <= 6; x++) Place(world, player, BeltItem, x, 3, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("chest"), 7, 3, new Vec2(1, 0));
            Place(world, player, DrillItem, 2, 3, new Vec2(-1, 0));

            Run(world, Drill.Seconds * 2 + 4 * Belt.Seconds + 1);

            Assert.AreEqual(2, world.ChestAt(7, 3).CountOf(IronOre));
            Assert.IsFalse(world.Map.IsSolid(4, 3), "belts are walked over");
        }

        [Test]
        public void ASmelterMeltsOreFromABeltIntoBars()
        {
            var world = Line(out var player);
            // Drill (2, 3) → belt → smelter (4, 3) → belt → chest (6, 3), placed back to front so there is always
            // somewhere to stand.
            Place(world, player, BeltItem, 5, 3, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("chest"), 6, 3, new Vec2(1, 0));
            Place(world, player, SmelterItem, 4, 3, new Vec2(1, 0));
            Place(world, player, BeltItem, 3, 3, new Vec2(1, 0));
            Place(world, player, DrillItem, 2, 3, new Vec2(-1, 0));

            // Four ores make two bars; the last bar needs the smelt time after its second ore arrives.
            Run(world, Drill.Seconds * 4 + Smelter.Seconds + 3);

            Assert.AreEqual(2, world.ChestAt(6, 3).CountOf(IronBar));
            Assert.AreEqual(0, world.ChestAt(6, 3).CountOf(IronOre));
        }

        [Test]
        public void AnArmFeedsASplitterThatSendsEachLoadAnotherWay()
        {
            var world = Line(out var player);
            var chest = Gear.Data.FindMaterial("chest");
            // Chest (3, 8) → arm (4, 8) → splitter (5, 8), all facing right, with chests ahead (6, 8), to its left
            // (5, 9) and to its right (5, 7). Placed front to back so there is always somewhere to stand.
            Place(world, player, chest, 6, 8, new Vec2(1, 0));
            Place(world, player, chest, 5, 9, new Vec2(0, 1));
            Place(world, player, chest, 5, 7, new Vec2(0, -1));
            Place(world, player, Gear.Data.FindMaterial("splitter"), 5, 8, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("robot_arm"), 4, 8, new Vec2(1, 0));
            Place(world, player, chest, 3, 8, new Vec2(1, 0));
            world.ChestAt(3, 8).AddMaterial(IronOre, 6);

            Run(world, 6 * Tiles.Shipped.Machine(TileType.Arm).Seconds + 2);

            Assert.AreEqual(0, world.ChestAt(3, 8).CountOf(IronOre));
            Assert.AreEqual(2, world.ChestAt(6, 8).CountOf(IronOre), "ahead");
            Assert.AreEqual(2, world.ChestAt(5, 9).CountOf(IronOre), "left");
            Assert.AreEqual(2, world.ChestAt(5, 7).CountOf(IronOre), "right");
            Assert.IsFalse(world.Map.IsSolid(5, 8), "splitters are walked over like belts");
            Assert.IsTrue(world.Map.IsSolid(4, 8), "an arm is not");
        }

        [Test]
        public void ASplitterSkipsAWayThatCannotTakeItAndNeverSendsBack()
        {
            var world = Line(out var player);
            var chest = Gear.Data.FindMaterial("chest");
            var splitter = Gear.Data.FindMaterial("splitter");
            // Chest (2, 8) → arm (3, 8) → splitter (4, 8) facing right, with a chest ahead (5, 8), nothing to its right
            // and to its left a second splitter (4, 9), also facing right, with a chest ahead of it (5, 9). The first
            // splitter is on the second's right: it must not hand loads back.
            Place(world, player, chest, 5, 8, new Vec2(1, 0));
            Place(world, player, chest, 5, 9, new Vec2(1, 0));
            Place(world, player, splitter, 4, 9, new Vec2(1, 0));
            Place(world, player, splitter, 4, 8, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("robot_arm"), 3, 8, new Vec2(1, 0));
            Place(world, player, chest, 2, 8, new Vec2(1, 0));
            world.ChestAt(2, 8).AddMaterial(IronOre, 4);

            Run(world, 4 * Tiles.Shipped.Machine(TileType.Arm).Seconds + 3);

            Assert.AreEqual(2, world.ChestAt(5, 8).CountOf(IronOre), "every other load ahead");
            Assert.AreEqual(2, world.ChestAt(5, 9).CountOf(IronOre), "the rest through the second splitter, none back");
            Assert.IsNull(world.MachineAt(4, 8).Carried);
            Assert.IsNull(world.MachineAt(4, 9).Carried);
        }

        [Test]
        public void AnArmMovesOnlyWhatTheTileAheadTakes()
        {
            var world = Line(out var player);
            // Chest (3, 8) → arm (4, 8) → smelter (5, 8) → chest (6, 8).
            Place(world, player, Gear.Data.FindMaterial("chest"), 6, 8, new Vec2(1, 0));
            Place(world, player, SmelterItem, 5, 8, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("robot_arm"), 4, 8, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("chest"), 3, 8, new Vec2(1, 0));
            var stone = Gear.Data.FindMaterial("stone");
            world.ChestAt(3, 8).AddMaterial(stone, 5);
            world.ChestAt(3, 8).AddMaterial(IronOre, 4);

            Run(world, 4 + Smelter.Seconds * 2 + 3);

            Assert.AreEqual(5, world.ChestAt(3, 8).CountOf(stone), "a smelter cannot melt stone, so the arm leaves it");
            Assert.AreEqual(0, world.ChestAt(3, 8).CountOf(IronOre));
            Assert.AreEqual(2, world.ChestAt(6, 8).CountOf(IronBar));
            Assert.IsNull(world.MachineAt(4, 8).Carried);
        }

        [Test]
        public void AnArmTakesFromTheEndOfABeltFacingIt()
        {
            var world = Line(out var player);
            // Drill (2, 3) on the vein → belt (3, 3) → arm (4, 3) → chest (5, 3).
            Place(world, player, Gear.Data.FindMaterial("chest"), 5, 3, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("robot_arm"), 4, 3, new Vec2(1, 0));
            Place(world, player, BeltItem, 3, 3, new Vec2(1, 0));
            Place(world, player, DrillItem, 2, 3, new Vec2(-1, 0));

            Run(world, Drill.Seconds * 2 + Belt.Seconds + 2);

            Assert.AreEqual(2, world.ChestAt(5, 3).CountOf(IronOre));
        }

        [Test]
        public void DiggingAMachineGivesBackItAndWhatItHeld()
        {
            var world = Line(out var player);
            Place(world, player, DrillItem, 2, 3, new Vec2(-1, 0));
            Run(world, Drill.Seconds * 2 + 0.5f);
            Place(world, player, BeltItem, 3, 4, new Vec2(0, 1));
            player.Inventory.TakeMaterial(BeltItem, player.Inventory.CountOf(BeltItem));

            // Face the drill from (3, 3), to its right, and dig it.
            world.MovePlayer(player.Id, TileMap.Center(3, 3), new Vec2(-1, 0), 10);
            world.MovePlayer(player.Id, player.Position, new Vec2(-1, 0), 1);
            world.SetAttacking(player.Id, true);
            Run(world, 0.1f);

            Assert.AreEqual(TileType.Floor, world.Map[2, 3]);
            Assert.IsNull(world.MachineAt(2, 3));
            Assert.AreEqual(1, player.Inventory.CountOf(DrillItem));
            Assert.AreEqual(2, player.Inventory.CountOf(IronOre));
        }

        [Test]
        public void MachinesAreSavedWithTheWorld()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var player = host.World.AddPlayer("a", null);
            // Facing down from the spawn: a belt there, loaded by hand through a save round trip.
            player.Inventory.AddMaterial(BeltItem);
            Assert.IsTrue(host.World.PlaceTile(player.Id, BeltItem));
            var belt = host.World.Machines.Single();
            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);

            var again = new WorldHost(save.Seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            again.Restore(save, now: 0);

            var restored = again.World.MachineAt(belt.X, belt.Y);
            Assert.AreEqual(TileType.Conveyor, restored.Kind);
            Assert.AreEqual(belt.Direction, restored.Direction);
        }

        [Test]
        public void PlayersNearMachinesHearAboutThem()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            var player = host.World.Players.Single();
            player.Inventory.AddMaterial(BeltItem);
            host.World.PlaceTile(player.Id, BeltItem);

            for (int i = 0; i < 8; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }

            var seen = client.Machines().Single();
            Assert.AreEqual(TileType.Conveyor, seen.Kind);
            Assert.AreEqual(3, seen.Direction, "it faced down, the way the player first faces");
            Assert.IsNull(seen.Carried);
        }
            [Test]
        public void ASprinklerWatersTheCropsAroundIt()
        {
            var world = Line(out var player);
            Sow(world, player, 8, 6, new Vec2(1, 0));
            Sow(world, player, 12, 6, new Vec2(1, 0));
            Place(world, player, SprinklerItem, 9, 8, new Vec2(0, 1));
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(Sprinkler.Seconds) + 1; i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }

            Assert.IsTrue(world.CropAt(8, 6).Watered, "two tiles off, inside the square");
            Assert.IsFalse(world.CropAt(12, 6).Watered, "three tiles off along x");
            var splash = events.Single(e => e.Type == SimEventType.Watered);
            Assert.AreEqual(0, splash.Source, "from no player");
            Assert.AreEqual(1, splash.Amount);
            Assert.AreEqual(TileMap.Center(9, 8), splash.Position);

            Run(world, Sprinkler.Seconds * 2);
            Assert.IsTrue(world.CropAt(8, 6).Watered, "a crop takes water once; nothing else happens to it");
        }

        [Test]
        public void AHarvesterReapsRipeCropsPlantsThemAgainAndSendsTheHarvestBack()
        {
            var world = Line(out var player);
            // Facing right from (5, 8), a chest behind it at (4, 8); crops on its left and right two and three tiles
            // ahead, and one four tiles ahead, out of its reach.
            Sow(world, player, 7, 9, new Vec2(1, 0));
            Sow(world, player, 8, 7, new Vec2(1, 0));
            Sow(world, player, 9, 8, new Vec2(1, 0));
            Place(world, player, HarvesterItem, 5, 8, new Vec2(1, 0));
            Place(world, player, Gear.Data.FindMaterial("chest"), 4, 8, new Vec2(1, 0));
            CollectionAssert.AreEqual(new[] { (6, 7), (6, 8), (6, 9), (7, 7), (7, 8), (7, 9), (8, 7), (8, 8), (8, 9) },
                world.HarvesterTiles(world.MachineAt(5, 8)).ToArray(), "three rows ahead, nearest first, each from its right to its left");
            // Out of the way, so no one walks over a ripe crop.
            world.MovePlayer(player.Id, TileMap.Center(12, 12), new Vec2(1, 0), 20);
            long planted = world.CropAt(7, 9).PlantedTick;

            Run(world, Mushroom.GrowSeconds + Harvester.Seconds * 2 + 1);

            var chest = world.ChestAt(4, 8);
            Assert.AreEqual(Mushroom.Count * 2, chest.CountOf(MushroomItem), "both crops in reach");
            Assert.That(chest.CountOf(Spore), Is.InRange(0, 2), "each gave a seed or two, and one went back in the ground");
            foreach (var (x, y) in new[] { (7, 9), (8, 7) })
            {
                var again = world.CropAt(x, y);
                Assert.IsNotNull(again, $"planted again at ({x}, {y})");
                Assert.Greater(again.PlantedTick, planted);
                Assert.IsFalse(again.IsRipe(world.Tick));
            }
            Assert.IsTrue(world.CropAt(9, 8).IsRipe(world.Tick), "out of reach, left alone");
        }

        [Test]
        public void ASwitchedOffMachineStopsButKeepsWhatItHolds()
        {
            var world = Line(out var player);
            Place(world, player, DrillItem, 2, 3, new Vec2(-1, 0));
            Run(world, Drill.Seconds + 0.5f);
            var drill = world.MachineAt(2, 3);
            Assert.AreEqual(1, drill.Outputs.Values.Sum(), "nowhere to send it yet");

            // From below, facing up at it.
            Face(world, player, 2, 3, new Vec2(0, 1));
            Assert.AreSame(drill, world.MachineFacing(player.Position, player.Facing), $"at {player.Position} facing {player.Facing}");
            Assert.IsTrue(world.ToggleMachine(player.Id));
            Assert.IsFalse(drill.Enabled);
            Place(world, player, Gear.Data.FindMaterial("chest"), 3, 3, new Vec2(-1, 0));
            Run(world, Drill.Seconds * 2 + 0.5f);
            Assert.AreEqual(0, world.ChestAt(3, 3).CountOf(IronOre), "off: it neither digs nor sends out");
            Assert.AreEqual(1, drill.Outputs.Values.Sum());

            // Round the chest, then below the drill again.
            Face(world, player, 3, 3, new Vec2(0, 1));
            Face(world, player, 2, 3, new Vec2(0, 1));
            Assert.IsTrue(world.ToggleMachine(player.Id));
            Assert.IsTrue(drill.Enabled);
            Run(world, Drill.Seconds + 0.5f);
            Assert.AreEqual(2, world.ChestAt(3, 3).CountOf(IronOre));

            world.MovePlayer(player.Id, TileMap.Center(10, 10), new Vec2(1, 0), 20);
            Assert.IsFalse(world.ToggleMachine(player.Id), "no machine ahead or underfoot");
        }

        [Test]
        public void AMachineSwitchedOffStaysOffThroughASaveAndClientsSeeIt()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            var player = host.World.Players.Single();
            player.Inventory.AddMaterial(BeltItem);
            Assert.IsTrue(host.World.PlaceTile(player.Id, BeltItem));
            host.Receive(1, client.ToggleMachineCommand());
            var belt = host.World.Machines.Single();
            Assert.IsFalse(belt.Enabled);

            for (int i = 0; i < 8; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            var seen = client.Machines().Single();
            Assert.IsFalse(seen.On);
            Assert.AreEqual(3, seen.Direction, "the switch rides on the direction byte without changing it");
            Assert.AreEqual(seen.X, client.MachineAhead()?.X);

            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            var again = new WorldHost(save.Seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            again.Restore(save, now: 0);
            var restored = again.World.MachineAt(belt.X, belt.Y);
            Assert.IsFalse(restored.Enabled);
            Assert.AreEqual(belt.Direction, restored.Direction);
        }
    }
}
