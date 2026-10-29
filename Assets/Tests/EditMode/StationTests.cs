using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class StationTests
    {
        static readonly MaterialDefinition Stone = Gear.Data.FindMaterial("stone");
        static readonly MaterialDefinition CopperOre = Gear.Data.FindMaterial("copper_ore");
        static readonly MaterialDefinition IronOre = Gear.Data.FindMaterial("iron_ore");
        static readonly MaterialDefinition IronBar = Gear.Data.FindMaterial("iron_bar");
        static readonly MaterialDefinition ChestItem = Gear.Data.FindMaterial("chest");
        static readonly MaterialDefinition WorkbenchItem = Gear.Data.FindMaterial("workbench");
        static readonly ConsumableDefinition Potion = Gear.Data.FindConsumable("healing_potion");

        static Recipe RecipeFor(string id) => Gear.Data.Recipes.Single(r => r.Id == id);

        // An open room; the player stands at (5, 5) facing right, at the floor tile (6, 5).
        static World Room(out Player player)
        {
            var world = new World(Maps.Room(12), TileMap.Center(5, 5), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            return world;
        }

        static void Swing(World world, Player player, int swings)
        {
            world.SetAttacking(player.Id, true);
            int attacks = 0;
            for (int tick = 0; tick < 300 && attacks < swings; tick++)
            {
                world.Step();
                attacks += world.TakeEvents().Count(e => e.Type == SimEventType.Attack && e.Source == player.Id);
            }
            world.SetAttacking(player.Id, false);
        }

        [Test]
        public void RecipesNameTheirStations()
        {
            Assert.IsNull(RecipeFor("workbench").Station, "the first station is made by hand");
            Assert.IsNull(RecipeFor("healing_potion").Station);
            Assert.AreEqual(TileType.Workbench, RecipeFor("copper_sword").Station);
            Assert.AreEqual(TileType.Furnace, RecipeFor("iron_bar").Station);
            Assert.IsTrue(RecipeFor("iron_sword").Ingredients.Any(i => i.Material == IronBar));
            Assert.AreEqual(TileType.Workbench, WorkbenchItem.Places);
        }

        [Test]
        public void CraftingNeedsItsStationWithinReach()
        {
            var world = Room(out var player);
            player.Inventory.AddMaterial(CopperOre, 12);
            player.Inventory.AddMaterial(Stone, 14);

            Assert.IsFalse(world.Craft(player.Id, RecipeFor("copper_sword")), "no workbench");
            Assert.IsTrue(world.Craft(player.Id, RecipeFor("workbench")));
            Assert.IsTrue(world.PlaceTile(player.Id, WorkbenchItem));
            Assert.AreEqual(TileType.Workbench, world.Map[6, 5]);
            Assert.IsTrue(world.Craft(player.Id, RecipeFor("copper_sword")));

            Assert.IsTrue(world.MovePlayer(player.Id, player.Position - new Vec2(1.5f, 0), new Vec2(-1, 0), 10));
            Assert.IsTrue(world.MovePlayer(player.Id, player.Position - new Vec2(1.5f, 0), new Vec2(-1, 0), 10));
            Assert.IsFalse(world.Craft(player.Id, RecipeFor("copper_sword")), "four tiles is out of reach");
        }

        [Test]
        public void FurnacesSmeltIronForIronGear()
        {
            var world = Room(out var player);
            world.Map[6, 6] = TileType.Furnace;
            player.Inventory.AddMaterial(IronOre, 4);

            Assert.IsTrue(world.Craft(player.Id, RecipeFor("iron_bar")));
            Assert.IsTrue(world.Craft(player.Id, RecipeFor("iron_bar")));

            Assert.AreEqual(2, player.Inventory.CountOf(IronBar));
            Assert.AreEqual(0, player.Inventory.CountOf(IronOre));
        }

        [Test]
        public void PlacedStationsDigBackIntoTheBag()
        {
            var world = Room(out var player);
            player.Inventory.AddMaterial(WorkbenchItem);
            world.PlaceTile(player.Id, WorkbenchItem);

            Swing(world, player, 1);

            Assert.AreEqual(TileType.Floor, world.Map[6, 5]);
            Assert.AreEqual(1, player.Inventory.CountOf(WorkbenchItem));
        }

        [Test]
        public void ChestsHoldItemsAndStacksForEveryoneNearby()
        {
            var world = Room(out var player);
            var friend = world.AddPlayer("b", null);
            player.Inventory.AddMaterial(ChestItem);
            Assert.IsTrue(world.PlaceTile(player.Id, ChestItem));
            var sword = player.Equipment.Items.Single();
            player.Unequip(ItemSlot.Weapon);
            player.Inventory.TryAdd(sword);
            player.Inventory.AddMaterial(Stone, 7);
            player.Inventory.AddConsumable(Potion, 2);
            int version = world.ChestVersion(6, 5);

            Assert.IsTrue(world.MoveItem(player.Id, sword.Id, intoChest: true));
            Assert.IsTrue(world.MoveStack(player.Id, StackKind.Material, Gear.Data.Materials.ToList().IndexOf(Stone), intoChest: true));
            Assert.IsTrue(world.MoveStack(player.Id, StackKind.Consumable, Gear.Data.Consumables.ToList().IndexOf(Potion), intoChest: true));

            var chest = world.ChestAt(6, 5);
            Assert.AreSame(sword, chest.Items.Single());
            Assert.AreEqual(7, chest.CountOf(Stone));
            Assert.AreEqual(2, chest.CountOf(Potion));
            Assert.AreEqual(0, player.Inventory.CountOf(Stone));
            Assert.Greater(world.ChestVersion(6, 5), version);

            Assert.IsTrue(world.MoveItem(friend.Id, sword.Id, intoChest: false), "anyone in reach can take from it");
            Assert.AreSame(sword, friend.Inventory.Items.Single());
        }

        [Test]
        public void ChestsOnlyWorkUpClose()
        {
            var world = Room(out var player);
            player.Inventory.AddMaterial(ChestItem);
            world.PlaceTile(player.Id, ChestItem);
            player.Inventory.AddMaterial(Stone, 3);

            Assert.IsTrue(world.MovePlayer(player.Id, player.Position - new Vec2(1.5f, 0), new Vec2(-1, 0), 10));
            Assert.IsTrue(world.MovePlayer(player.Id, player.Position - new Vec2(1.5f, 0), new Vec2(-1, 0), 10));

            Assert.IsNull(world.ChestNear(player.Position));
            Assert.IsFalse(world.MoveStack(player.Id, StackKind.Material, Gear.Data.Materials.ToList().IndexOf(Stone), intoChest: true));
        }

        [Test]
        public void AChestWithSomethingInItCannotBeDug()
        {
            var world = Room(out var player);
            player.Inventory.AddMaterial(ChestItem);
            world.PlaceTile(player.Id, ChestItem);
            player.Inventory.AddMaterial(Stone);
            world.MoveStack(player.Id, StackKind.Material, Gear.Data.Materials.ToList().IndexOf(Stone), intoChest: true);

            Swing(world, player, 3);
            Assert.AreEqual(TileType.Chest, world.Map[6, 5]);

            world.MoveStack(player.Id, StackKind.Material, Gear.Data.Materials.ToList().IndexOf(Stone), intoChest: false);
            Swing(world, player, 1);
            Assert.AreEqual(TileType.Floor, world.Map[6, 5]);
            Assert.AreEqual(1, player.Inventory.CountOf(ChestItem));
            Assert.IsNull(world.ChestAt(6, 5));
        }

        [Test]
        public void ChestsAreSavedWithTheWorld()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var player = host.World.AddPlayer("a", null);
            player.Inventory.AddMaterial(ChestItem);
            Assert.IsTrue(host.World.PlaceTile(player.Id, ChestItem));
            var (x, y, _) = host.World.Chests.Single();
            player.Inventory.TryAdd(Gear.Plain("iron_bow", 1, Rarity.Rare, Gear.Affix("swift", 5)));
            player.Inventory.AddMaterial(Stone, 9);
            host.World.MoveItem(player.Id, player.Inventory.Items.Single().Id, intoChest: true);
            host.World.MoveStack(player.Id, StackKind.Material, Gear.Data.Materials.ToList().IndexOf(Stone), intoChest: true);

            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            var again = new WorldHost(save.Seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            again.Restore(save, now: 0);

            var chest = again.World.ChestAt(x, y);
            Assert.AreEqual("iron_bow", chest.Items.Single().Base.Id);
            Assert.AreNotEqual(0, chest.Items.Single().Id);
            Assert.AreEqual(9, chest.CountOf(Stone));
        }

        [Test]
        public void ChestsFollowWhoeverStandsByThem()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            void Deliver()
            {
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            host.Receive(1, client.JoinMessage());
            host.Tick();
            Deliver();
            var player = host.World.Players.Single();
            Assert.IsFalse(client.Chest.IsOpen);

            player.Inventory.AddMaterial(ChestItem);
            Assert.IsTrue(host.World.PlaceTile(player.Id, ChestItem));
            host.Tick();
            Deliver();
            Assert.IsTrue(client.Chest.IsOpen, "standing by a new chest opens it");

            var potion = client.Bag.Consumables.Keys.First();
            host.Receive(1, client.StoreStackCommand(StackKind.Consumable, potion));
            host.Tick();
            Deliver();
            Assert.AreEqual(3, client.Chest.Contents.CountOf(potion));
            Assert.AreEqual(0, client.Bag.CountOf(potion));

            host.World.MovePlayer(player.Id, player.Position + new Vec2(0, 2), new Vec2(0, 1), 20);
            host.World.MovePlayer(player.Id, player.Position + new Vec2(0, 2), new Vec2(0, 1), 20);
            host.Tick();
            Deliver();
            Assert.IsFalse(client.Chest.IsOpen, "walking away closes it");
        }
    }
}
