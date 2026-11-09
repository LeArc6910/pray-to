using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // The assembler (Docs/Automation.md, 조립기): it makes the workbench recipe it was set to from what arms and belts bring.
    public class AssemblerTests
    {
        static readonly MaterialDefinition IronBar = Gear.Data.FindMaterial("iron_bar");
        static readonly MaterialDefinition IronGear = Gear.Data.FindMaterial("iron_gear");
        static readonly MaterialDefinition CopperOre = Gear.Data.FindMaterial("copper_ore");
        static readonly MaterialDefinition SlimeGel = Gear.Data.FindMaterial("slime_gel");
        static readonly MaterialDefinition Stone = Gear.Data.FindMaterial("stone");
        static readonly MaterialDefinition Chest = Gear.Data.FindMaterial("chest");
        static readonly MaterialDefinition Arm = Gear.Data.FindMaterial("robot_arm");
        static readonly MaterialDefinition AssemblerItem = Gear.Data.FindMaterial("assembler");
        static readonly MachineDefinition Assembler = Tiles.Shipped.Machine(TileType.Assembler);
        static readonly ConsumableDefinition Potion = Gear.Data.Consumables.Single(c => c.Id == "healing_potion");

        static Recipe RecipeFor(string id) => Gear.Data.Recipes.Single(r => r.Id == id);

        static World Room(out Player player)
        {
            var world = new World(Maps.Room(16), TileMap.Center(8, 8), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", null);
            return world;
        }

        // Chest (3, 8) → arm (4, 8) → assembler (5, 8) → chest (6, 8), and the player below the assembler facing it.
        static World Line(out Player player, bool chestInFront = true)
        {
            var world = Room(out player);
            if (chestInFront) Place(world, player, Chest, 6, 8, new Vec2(1, 0));
            Place(world, player, AssemblerItem, 5, 8, new Vec2(1, 0));
            Place(world, player, Arm, 4, 8, new Vec2(1, 0));
            Place(world, player, Chest, 3, 8, new Vec2(1, 0));
            // Round the chest, then under the assembler.
            Walk(world, player, TileMap.Center(2, 6), new Vec2(0, 1));
            Face(world, player, 5, 8, new Vec2(0, 1));
            return world;
        }

        [Test]
        public void PartsAreMadeAtTheWorkbenchAndMachinesNeedThem()
        {
            Assert.AreEqual(TileType.Workbench, RecipeFor("iron_gear").Station);
            Assert.AreEqual(2, RecipeFor("iron_gear").Count);
            Assert.IsTrue(RecipeFor("circuit").Ingredients.Any(i => i.Material == IronGear));
            foreach (var machine in new[] { "drill", "robot_arm", "harvester", "assembler" })
            {
                Assert.IsTrue(RecipeFor(machine).Ingredients.Any(i => i.Material == IronGear), $"{machine} is built from gears");
            }
            Assert.AreEqual(TileType.Assembler, AssemblerItem.Places);
        }

        [Test]
        public void ItMakesWhatTheHandWorkbenchAndAnvilMake()
        {
            Assert.IsTrue(World.Assembles(RecipeFor("iron_gear")));
            Assert.IsTrue(World.Assembles(RecipeFor("healing_potion")), "a recipe needing no station");
            Assert.IsFalse(World.Assembles(RecipeFor("iron_bar")), "the furnace's, which the smelter does");
            // Since 2026-11-09 gear too, so what takes long by hand can be left to it (Docs/Automation.md).
            Assert.IsTrue(World.Assembles(RecipeFor("copper_sword")), "gear from the workbench");
            Assert.IsTrue(World.Assembles(RecipeFor("flame_blade")), "and from the anvil");
        }

        [Test]
        public void AnArmFeedsItAndGearsComeOutTheFront()
        {
            var world = Line(out var player);
            world.ChestAt(3, 8).AddMaterial(IronBar, 3);
            world.ChestAt(3, 8).AddMaterial(Stone, 4);

            Run(world, 3);
            Assert.AreEqual(3, world.ChestAt(3, 8).CountOf(IronBar), "with no recipe it takes nothing");

            Assert.IsTrue(world.SetRecipe(player.Id, RecipeFor("iron_gear")));
            Run(world, 3 + Assembler.Seconds * 3 + 3);

            Assert.AreEqual(0, world.ChestAt(3, 8).CountOf(IronBar));
            Assert.AreEqual(4, world.ChestAt(3, 8).CountOf(Stone), "nothing the recipe does not use");
            Assert.AreEqual(6, world.ChestAt(6, 8).CountOf(IronGear));
        }

        [Test]
        public void GearTakesTimeByHandAndTheAssemblerMakesItUnattended()
        {
            // By hand: a copper sword takes 3 seconds at a workbench, the ore used only when it is done.
            var world = Room(out var player);
            var sword = RecipeFor("copper_sword");
            Assert.AreEqual(3f, CraftRules.Seconds(sword, Tiles.Shipped));
            Assert.AreEqual(0f, CraftRules.Seconds(RecipeFor("healing_potion"), Tiles.Shipped), "a potion is at once");
            Assert.AreEqual(CraftRules.MachineSeconds, CraftRules.Seconds(RecipeFor("assembler"), Tiles.Shipped));
            int x = TileMap.Cell(player.Position.X) + 1, y = TileMap.Cell(player.Position.Y);
            world.Map[x, y] = TileType.Workbench;
            player.Inventory.AddMaterial(CopperOre, 12);
            player.Inventory.AddMaterial(Stone, 4);
            int items = player.Inventory.Items.Count;
            Assert.AreEqual(2, world.StartCrafting(player.Id, sword, 2));
            Run(world, 1);
            Assert.AreEqual(items, player.Inventory.Items.Count, "not yet");
            Assert.AreEqual(12, player.Inventory.CountOf(CopperOre), "nothing used yet");
            Run(world, 2.2f);
            Assert.AreEqual(items + 1, player.Inventory.Items.Count, "one done");
            Assert.AreEqual(6, player.Inventory.CountOf(CopperOre));
            Assert.IsNotNull(player.Crafting, "the second under way");
            Assert.IsTrue(world.StopCrafting(player.Id));
            Assert.IsNull(player.Crafting);
            Assert.AreEqual(6, player.Inventory.CountOf(CopperOre), "stopping costs nothing");
            // Walking off stops it too.
            world.StartCrafting(player.Id, sword, 1);
            player.Position = TileMap.Center(x - 8, y);
            Run(world, 3.5f);
            Assert.IsNull(player.Crafting);
            Assert.AreEqual(items + 1, player.Inventory.Items.Count);

            // The assembler: the same sword, into the chest in front, while no one waits.
            var line = Line(out var keeper);
            Assert.IsTrue(line.SetRecipe(keeper.Id, sword));
            line.ChestAt(3, 8).AddMaterial(CopperOre, 6);
            line.ChestAt(3, 8).AddMaterial(Stone, 2);
            Run(line, 12);
            Assert.AreEqual(1, line.ChestAt(6, 8).Items.Count(i => i.Base.Id == "copper_sword"), "a sword in the chest in front");
        }

        [Test]
        public void ItHoldsAtMostTwiceWhatOneMakeNeeds()
        {
            // No chest in front, so the potion has nowhere to go and the assembler waits.
            var world = Line(out var player, chestInFront: false);
            Assert.IsTrue(world.SetRecipe(player.Id, RecipeFor("healing_potion")));
            world.ChestAt(3, 8).AddMaterial(SlimeGel, 10);

            Run(world, 15);

            var assembler = world.MachineAt(5, 8);
            Assert.AreEqual(6, assembler.Input.GetValueOrDefault(SlimeGel), "two potions' worth");
            Assert.AreEqual(SlimeGel, world.MachineAt(4, 8).Carried, "the arm waits with one more");
            Assert.AreEqual(3, world.ChestAt(3, 8).CountOf(SlimeGel));
        }

        [Test]
        public void AConsumableGoesOnlyIntoAChestInFront()
        {
            var world = Room(out var player);
            Place(world, player, AssemblerItem, 5, 8, new Vec2(1, 0));
            Face(world, player, 5, 8, new Vec2(0, 1));
            Assert.IsTrue(world.SetRecipe(player.Id, RecipeFor("healing_potion")));
            world.MachineAt(5, 8).Input[SlimeGel] = 6;

            Run(world, Assembler.Seconds * 2 + 1);
            Assert.AreEqual(6, world.MachineAt(5, 8).Input[SlimeGel], "no chest in front: it waits");

            Place(world, player, Chest, 6, 8, new Vec2(0, 1));
            Run(world, Assembler.Seconds * 2 + 1);
            Assert.AreEqual(2, world.ChestAt(6, 8).CountOf(Potion));
            Assert.AreEqual(0, world.MachineAt(5, 8).Input.GetValueOrDefault(SlimeGel));
        }

        [Test]
        public void ANewRecipeGivesBackWhatItNoLongerUses()
        {
            var world = Line(out var player);
            Assert.IsTrue(world.SetRecipe(player.Id, RecipeFor("iron_gear")));
            world.MachineAt(5, 8).Input[IronBar] = 2;

            Assert.IsTrue(world.SetRecipe(player.Id, RecipeFor("copper_wire")));
            Assert.AreEqual(2, player.Inventory.CountOf(IronBar));
            Assert.AreEqual(0, world.MachineAt(5, 8).Input.Count);
            Assert.AreEqual("copper_wire", world.MachineAt(5, 8).Recipe.Id);

            world.MachineAt(5, 8).Input[CopperOre] = 1;
            Assert.IsTrue(world.SetRecipe(player.Id, null), "stopping it");
            Assert.IsNull(world.MachineAt(5, 8).Recipe);
            Assert.AreEqual(1, player.Inventory.CountOf(CopperOre));
        }

        [Test]
        public void ItRefusesOtherStationsRecipesAndOtherMachines()
        {
            var world = Line(out var player);
            Assert.IsFalse(world.SetRecipe(player.Id, RecipeFor("iron_bar")));
            Assert.IsNull(world.MachineAt(5, 8).Recipe);

            Face(world, player, 4, 8, new Vec2(0, 1));
            Assert.IsFalse(world.SetRecipe(player.Id, RecipeFor("iron_gear")), "an arm is no assembler");
        }

        [Test]
        public void ItsRecipeIsSavedAndSent()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            var player = host.World.Players.Single();
            // Facing down from the spawn: an assembler there, set through the client's command.
            player.Inventory.AddMaterial(AssemblerItem);
            Assert.IsTrue(host.World.PlaceTile(player.Id, AssemblerItem));
            for (int i = 0; i < 4; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            host.Receive(1, client.SetRecipeCommand(RecipeFor("circuit")));
            for (int i = 0; i < 8; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            var assembler = host.World.Machines.Single();
            Assert.AreEqual("circuit", assembler.Recipe?.Id);
            Assert.AreEqual("circuit", client.Machines().Single().Recipe?.Id);

            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            var again = new WorldHost(save.Seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            again.Restore(save);
            Assert.AreEqual("circuit", again.World.MachineAt(assembler.X, assembler.Y).Recipe?.Id);
        }
    }
}
