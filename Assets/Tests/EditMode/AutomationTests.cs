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
            var stand = TileMap.Center(x, y) - facing;
            for (int i = 0; i < 20 && Vec2.Distance(player.Position, stand) > 0.01f; i++)
            {
                world.MovePlayer(player.Id, player.Position + (stand - player.Position).ClampLength(1.5f), facing, 10);
            }
            world.MovePlayer(player.Id, player.Position, facing, 1);
            player.Inventory.AddMaterial(material);
            Assert.IsTrue(world.PlaceTile(player.Id, material), $"{material.Id} at ({x}, {y})");
            Assert.AreEqual(material.Places, world.Map[x, y], $"{material.Id} landed somewhere else");
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
    }
}
