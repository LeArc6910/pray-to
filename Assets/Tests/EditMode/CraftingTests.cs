using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class CraftingTests
    {
        static readonly MaterialDefinition Stone = Gear.Data.FindMaterial("stone");
        static readonly MaterialDefinition CopperOre = Gear.Data.FindMaterial("copper_ore");
        static readonly MaterialDefinition SlimeGel = Gear.Data.FindMaterial("slime_gel");

        static Recipe RecipeFor(string id) => Gear.Data.Recipes.Single(r => r.Id == id);

        // A room with one free-standing wall at (3, 2); the player stands left of it, facing it.
        static World Room(out Player player, TileType inner = TileType.Wall, Equipment gear = null, MonsterData monsters = null)
        {
            var map = Maps.FromRows(
                "#######",
                "#.....#",
                "#..#..#",
                "#.....#",
                "#######");
            map[3, 2] = inner;
            var world = new World(map, TileMap.Center(2, 2), monsters ?? Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", gear ?? Gear.Wearing("copper_pickaxe"));
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

        // Holds attack until the player has attacked this many times.
        static List<SimEvent> Swing(World world, Player player, int swings)
        {
            world.SetAttacking(player.Id, true);
            var events = new List<SimEvent>();
            for (int tick = 0; tick < 10 * World.TicksPerSecond && events.Count(e => e.Type == SimEventType.Attack && e.Source == player.Id) < swings; tick++)
            {
                events.AddRange(Run(world, 1));
            }
            world.SetAttacking(player.Id, false);
            return events;
        }

        static void AssertError(IReadOnlyList<string> errors, params string[] parts) =>
            Assert.IsTrue(errors.Any(error => parts.All(error.Contains)), $"No error mentions {string.Join(" + ", parts)}. Errors:\n{string.Join("\n", errors)}");

        [Test]
        public void ShippedSheetsHangTogether()
        {
            Assert.IsTrue(Gear.Data.Success, string.Join("\n", Gear.Data.Errors));
            Assert.IsTrue(Tiles.Shipped.Success, string.Join("\n", Tiles.Shipped.Errors));
            foreach (var monster in Monsters.Shipped.Monsters.Where(m => m.MaterialChance > 0))
                Assert.DoesNotThrow(() => Gear.Data.FindMaterial(monster.MaterialId), monster.Id);
            foreach (var boss in Bosses.Shipped.Bosses.Where(b => b.MaterialCount > 0))
                Assert.DoesNotThrow(() => Gear.Data.FindMaterial(boss.MaterialId), boss.Id);
            Assert.IsTrue(Gear.Data.Recipes.Where(r => r.Item?.Level == 10 && !r.Item.IsPickaxe).All(r => r.Ingredients.Any(i => i.Material.Id == "warden_core")),
                "iron gear takes the boss's core");
            var pickaxes = Gear.Data.Bases.Where(b => b.IsPickaxe).Select(b => b.Id).ToList();
            CollectionAssert.AreEqual(new[] { "copper_pickaxe", "iron_pickaxe", "mithril_pickaxe", "obsidian_pickaxe" }, pickaxes);
            Assert.IsTrue(pickaxes.All(id => Gear.Data.Recipes.Any(r => r.Item?.Id == id)), "every pickaxe can be made");
            Assert.IsTrue(Gear.Data.Bases.Where(b => b.IsPickaxe).All(b => b.Slot == ItemSlot.Weapon && b.Weight == 0), "held, and made rather than dropped");
            Assert.AreEqual(TileType.Wall, Stone.Places);
        }

        [Test]
        public void RangesReadLikeTheirStat()
        {
            Assert.AreEqual("피해 +4~6", StatText.Describe(ModifierSyntax.Parse("Damage+4~6")));
            Assert.AreEqual("마법 피해 30% 증가", StatText.Describe(ModifierSyntax.Parse("Magic:Damage+30%")));
            Assert.AreEqual("공격 속도 15% 감소", StatText.Describe(ModifierSyntax.Parse("AttackSpeed-15%")));
            Assert.AreEqual("방어도 +6~9", StatText.Describe(ModifierSyntax.Parse("Armor+6~9")));
        }

        [Test]
        public void SheetErrorsNameTheRow()
        {
            var loot = TestLoot.Parse(
                materials: "id,name,places,description\nore,광석,,\nhole,구멍,Floor,\n",
                recipes: "id,result,count,ingredients\n" +
                    "a,nothing,1,ore:1\n" +
                    "b,sword,1,gold:1\n" +
                    "c,sword,1,ore\n" +
                    "d,sword,2,ore:1\n");
            AssertError(loot.Errors, "materials.csv 3행", "단단한 칸");
            AssertError(loot.Errors, "recipes.csv 2행", "result 'nothing'");
            AssertError(loot.Errors, "recipes.csv 3행", "재료 'gold'");
            AssertError(loot.Errors, "recipes.csv 4행", "id:개수");
            AssertError(loot.Errors, "recipes.csv 5행", "하나씩");

            var tiles = TileSheet.Parse("tile,name,hardness,material,count\nFloor,바닥,1,ore,1\nWall,벽,3,gold,1\n", loot);
            AssertError(tiles.Errors, "tiles.csv 2행", "바닥은 캘 수 없습니다");
            AssertError(tiles.Errors, "tiles.csv 3행", "재료 'gold'");
        }

        [Test]
        public void CavesGetOreVeinsInTheirRock()
        {
            var world = WorldGenerator.Generate(7);
            var map = world.Map;
            var copper = new List<(int X, int Y)>();
            var iron = new List<(int X, int Y)>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (map[x, y] == TileType.CopperOre) copper.Add((x, y));
                    if (map[x, y] == TileType.IronOre) iron.Add((x, y));
                }
            }

            Assert.Greater(copper.Count, 30);
            Assert.Greater(iron.Count, 10);
            Assert.IsTrue(iron.All(t => Vec2.Distance(TileMap.Center(t.X, t.Y), world.Spawn) >= 29), "iron is far from the spawn");
            Assert.IsFalse(copper.Concat(iron).Any(t => map.IsEdge(t.X, t.Y)));
            Assert.AreEqual(TileType.Floor, map[world.SpawnX, world.SpawnY]);
            var again = WorldGenerator.Generate(7).Map;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++) Assert.AreEqual(map[x, y], again[x, y]);
            }
        }

        [Test]
        public void WallsBreakAfterTheirHardnessInHits()
        {
            var world = Room(out var player);

            var events = Swing(world, player, 2);
            Assert.AreEqual(TileType.Wall, world.Map[3, 2], "two of three hits");
            events.AddRange(Swing(world, player, 1));

            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            var digs = events.Where(e => e.Type == SimEventType.Dug).ToList();
            // How far it is dug after each hit, for the cracks; 100 when it gives way.
            CollectionAssert.AreEqual(new[] { 33, 67, World.DugThrough }, digs.Select(e => e.Amount));
            Assert.AreEqual(TileMap.Center(3, 2), digs[0].Position);
            Assert.AreEqual(1, player.Inventory.CountOf(Stone));
            var change = world.TakeTileChanges().Single();
            Assert.AreEqual((3, 2, TileType.Floor), (change.X, change.Y, change.Type));
            Assert.AreEqual(1, world.ChangedTiles().Count);
        }

        [Test]
        public void OreIsHarderAndGivesOre()
        {
            var world = Room(out var player, TileType.CopperOre);

            var events = Swing(world, player, 4);
            Assert.AreEqual(TileType.CopperOre, world.Map[3, 2]);
            events.AddRange(Swing(world, player, 1));
            CollectionAssert.AreEqual(new[] { 20, 40, 60, 80, World.DugThrough },
                events.Where(e => e.Type == SimEventType.Dug).Select(e => e.Amount), "a fifth more cracked with each hit");

            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(CopperOre));
        }

        [Test]
        public void AWallBesideIsDugFromTheFarSideOfTheTile()
        {
            // 2026-11-09, the guide bot on the web: standing at the left edge of its tile, a reach of 0.9 ended in
            // the player's own tile, and a swing at the wall right beside did nothing.
            var world = Room(out var player);
            player.Position = new Vec2(2.02f, 2.5f);
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            Assert.AreEqual((3, 2), World.DigAhead(player.Position, player.Facing));

            Swing(world, player, 4);

            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual((3, 2), World.DigAhead(new Vec2(2.5f, 2.5f), new Vec2(1, 0)), "from the middle, the tile beside as before");
            Assert.AreEqual((3, 2), World.DigAhead(new Vec2(2.09f, 2.5f), new Vec2(1, 0)), "never two tiles on");
        }

        [Test]
        public void MiningSpeedDigsFaster()
        {
            var pick = new Equipment();
            pick.Equip(Gear.Plain("copper_pickaxe", 0, Rarity.Uncommon, Gear.Affix("miner", 50)));
            var world = Room(out var player, gear: pick);

            Swing(world, player, 2);

            Assert.AreEqual(TileType.Floor, world.Map[3, 2], "1.5 a hit takes a hardness 3 wall in two");
        }

        [Test]
        public void SwingsHitEnemiesBeforeWalls()
        {
            var world = Room(out var player);
            world.SpawnEnemy(Monsters.Dummy, new Vec2(3.1f, 3.3f));

            var events = Swing(world, player, 1);

            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Hit && e.Source == player.Id));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.Dug));
        }

        [Test]
        public void ASwingDoesNotReachThroughAWall()
        {
            // 2026-11-09: a swing reached a monster behind the one-tile wall the player stood against, and the wall
            // was never dug while it was there.
            var world = Room(out var player);
            player.Position = new Vec2(2.64f, 2.5f);
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            var behind = world.SpawnEnemy(Monsters.Dummy, new Vec2(4.45f, 2.5f));

            var events = Swing(world, player, 1);

            Assert.IsFalse(events.Any(e => e.Type == SimEventType.Hit && e.Target == behind.Id), "the wall is between them");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Dug), "the swing digs the wall instead");
        }

        [Test]
        public void OnlyAPickaxeDigsRock()
        {
            var world = Room(out var player, gear: Gear.Wearing("copper_sword"));

            var events = Swing(world, player, 5);

            Assert.AreEqual(TileType.Wall, world.Map[3, 2], "a sword does not dig");
            var digs = events.Where(e => e.Type == SimEventType.Dug).ToList();
            Assert.AreEqual(5, digs.Count);
            Assert.IsTrue(digs.All(e => e.Amount == -1 && e.Position.Equals(TileMap.Center(3, 2))), "each swing says a pickaxe is needed");
            Assert.AreEqual(0, player.Inventory.CountOf(Stone));

            var iron = new Equipment();
            iron.Equip(Gear.Plain("iron_pickaxe"));
            world = Room(out player, gear: iron);
            Swing(world, player, 2);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2], "the iron pickaxe's mining speed 60% takes a hardness 3 wall in two");
        }

        [Test]
        public void PickaxeSheetErrors()
        {
            var data = LootSheets.Parse(
                TestLoot.BasesHeader.Replace("weight\n", "weight,tool\n") +
                "a,가,weapon,1,melee,Damage+1,0,1,shovel\n" +
                "b,나,helmet,1,,Armor+1,0,1,pickaxe\n" +
                "c,다,weapon,1,melee,Damage+1,0,1,pickaxe\n",
                TestLoot.Affixes, TestLoot.Runes, TestLoot.Runewords);

            AssertError(data.Errors, "item_bases.csv 2행", "shovel");
            AssertError(data.Errors, "item_bases.csv 3행", "weapon");
            Assert.IsTrue(data.Bases.Single(b => b.Id == "c").IsPickaxe);
            Assert.IsFalse(Gear.Data.FindBase("copper_sword").IsPickaxe, "a sheet without the column has no tools");
        }

        [Test]
        public void AnythingTakesDownPlacedThings()
        {
            var world = Room(out var player, TileType.Workbench, Gear.Wearing("copper_sword"));
            Swing(world, player, 1);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(Gear.Data.FindMaterial("workbench")));
        }

        [Test]
        public void BowsShootAtRockButTakeDownPlacedThingsLikeASwing()
        {
            var world = Room(out var player, gear: Gear.Wearing("copper_bow"));
            var events = Swing(world, player, 1);
            Assert.AreEqual((int)BasicAttackKind.Arrow, events.First(e => e.Type == SimEventType.Attack).Amount);
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.Dug));
            Assert.AreEqual(TileType.Wall, world.Map[3, 2]);

            world = Room(out player, TileType.Chest, Gear.Wearing("copper_bow"));
            events = Swing(world, player, 1);
            Assert.AreEqual((int)BasicAttackKind.Swing, events.First(e => e.Type == SimEventType.Attack).Amount);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.IsEmpty(world.Projectiles);
        }

        [Test]
        public void TheMapEdgeCannotBeDug()
        {
            var world = Room(out var player);
            world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 1);
            world.MovePlayer(player.Id, TileMap.Center(2, 3), new Vec2(0, 1), 10);

            var events = Swing(world, player, 5);

            Assert.IsFalse(events.Any(e => e.Type == SimEventType.Dug));
            Assert.AreEqual(TileType.Wall, world.Map[2, 4]);
        }

        [Test]
        public void StonePlacesAWallOnTheFloorAhead()
        {
            var world = Room(out var player, TileType.Floor);
            player.Inventory.AddMaterial(Stone, 2);

            Assert.IsFalse(world.PlaceTile(player.Id, CopperOre), "copper ore places nothing");
            Assert.IsTrue(world.PlaceTile(player.Id, Stone));

            Assert.AreEqual(TileType.Wall, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(Stone));
            Assert.AreEqual(TileType.Wall, world.TakeTileChanges().Single().Type);
            Assert.IsFalse(world.PlaceTile(player.Id, Stone), "already a wall");
        }

        [Test]
        public void NothingIsBuiltOnSomeoneOrInABossArena()
        {
            var world = Room(out var player, TileType.Floor);
            player.Inventory.AddMaterial(Stone, 5);
            var dummy = world.SpawnEnemy(Monsters.Dummy, TileMap.Center(3, 2));

            Assert.IsFalse(world.PlaceTile(player.Id, Stone), "the dummy is there");

            var arena = Room(out var fighter, TileType.Floor);
            fighter.Inventory.AddMaterial(Stone, 5);
            arena.PlaceBoss(Bosses.Shipped.Bosses[0], TileMap.Center(5, 3), 2);
            Assert.IsFalse(arena.PlaceTile(fighter.Id, Stone), "inside the arena");
        }

        [Test]
        public void CraftingTurnsMaterialsIntoGearAndPotions()
        {
            var world = Room(out var player, TileType.Workbench);
            player.Inventory.AddMaterial(CopperOre, 6);
            player.Inventory.AddMaterial(Stone, 2);
            player.Inventory.AddMaterial(SlimeGel, 3);

            Assert.IsTrue(world.Craft(player.Id, RecipeFor("copper_sword")));
            var sword = player.Inventory.Items.Single();
            Assert.AreEqual("copper_sword", sword.Base.Id);
            Assert.AreEqual(Rarity.Common, sword.Rarity);
            Assert.AreEqual(0, player.Inventory.CountOf(CopperOre));
            Assert.AreEqual(0, player.Inventory.CountOf(Stone));
            Assert.IsFalse(world.Craft(player.Id, RecipeFor("copper_sword")), "out of ore");

            Assert.IsTrue(world.Craft(player.Id, RecipeFor("healing_potion")));
            Assert.AreEqual(1, player.Inventory.CountOf(Gear.Data.FindConsumable("healing_potion")));
            Assert.AreEqual(0, player.Inventory.CountOf(SlimeGel));
        }

        [Test]
        public void GearIsNotCraftedIntoAFullBag()
        {
            var world = Room(out var player, TileType.Workbench);
            while (!player.Inventory.IsFull) player.Inventory.TryAdd(Gear.Plain("copper_ring"));
            player.Inventory.AddMaterial(CopperOre, 6);
            player.Inventory.AddMaterial(Stone, 2);

            Assert.IsFalse(world.Craft(player.Id, RecipeFor("copper_sword")));
            Assert.AreEqual(6, player.Inventory.CountOf(CopperOre));
        }

        [Test]
        public void MonstersAndBossesDropTheirMaterials()
        {
            var gelSlimes = MonsterSheet.Parse(
                Monsters.Header.Replace("weight\n", "weight,material,material_chance\n") + "goo,젤리,1,1,0,0,1,0.3,0,0.5,0.5,1,1,slime_gel,100\n");
            var world = Room(out var player, TileType.Floor, Gear.Wearing("copper_sword"), gelSlimes);
            world.SpawnEnemy(gelSlimes.Monsters[0], player.Position + new Vec2(0.9f, 0));
            Swing(world, player, 1);
            Assert.AreEqual(1, world.GroundLoot.Count(l => l.Material == SlimeGel) + player.Inventory.CountOf(SlimeGel));

            var bosses = Bosses.Parse(
                Bosses.BossesHeader.Replace("roar_damage\n", "roar_damage,material,material_count\n") + "dummy,허수아비,8,0,0,2,1,,1,3,10,warden_core,1\n",
                Bosses.AttacksHeader + "dummy,slam,내려찍기,,circle,nearest,1,5,1,,,,1,,,1,1,\n");
            var arena = Room(out var fighter, TileType.Floor, Gear.Wearing("copper_sword"));
            arena.PlaceBoss(bosses.Find("dummy"), new Vec2(3.6f, 2.5f), 3);
            Swing(arena, fighter, 3);
            Assert.IsEmpty(arena.Bosses.Where(b => !b.IsDead));
            var core = Gear.Data.FindMaterial("warden_core");
            Assert.AreEqual(1, arena.GroundLoot.Count(l => l.Material == core) + fighter.Inventory.CountOf(core));
        }
            [Test]
        public void LaidFloorsAreWalkedOnBuiltOnAndOnlyAPickaxeTakesThemUp()
        {
            var floor = Gear.Data.FindMaterial("stone_floor");
            var world = Room(out var player, TileType.Floor, Gear.Wearing("copper_sword"));
            player.Inventory.AddMaterial(floor, 2);
            Assert.IsTrue(world.PlaceTile(player.Id, floor));
            Assert.AreEqual(TileType.StoneFloor, world.Map[3, 2]);
            Assert.IsFalse(world.Map.IsSolid(3, 2), "walked on like floor");
            Assert.IsFalse(world.PlaceTile(player.Id, floor), "already laid");

            // A sword swung at it leaves it be.
            world.SetAttacking(player.Id, true);
            Run(world, 40);
            world.SetAttacking(player.Id, false);
            Assert.AreEqual(TileType.StoneFloor, world.Map[3, 2]);

            // Building on it gives the floor back.
            var workbench = Gear.Data.FindMaterial("workbench");
            player.Inventory.AddMaterial(workbench);
            int before = player.Inventory.CountOf(floor);
            Assert.IsTrue(world.PlaceTile(player.Id, workbench));
            Assert.AreEqual(TileType.Workbench, world.Map[3, 2]);
            Assert.AreEqual(before + 1, player.Inventory.CountOf(floor));

            // No crops on a laid floor.
            var spore = Gear.Data.FindMaterial("mushroom_spore");
            world.MovePlayer(player.Id, TileMap.Center(2, 2), new Vec2(0, 1), 1);
            Assert.IsTrue(world.PlaceTile(player.Id, floor));
            player.Inventory.AddMaterial(spore);
            Assert.IsFalse(world.Plant(player.Id, Tiles.Shipped.CropFrom(spore)));
        }

        [Test]
        public void ALaidFloorComesUpToAPickaxe()
        {
            var world = Room(out var player, TileType.StoneFloor);
            world.SetAttacking(player.Id, true);
            Run(world, 60);
            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(Gear.Data.FindMaterial("stone_floor")));
        }

        [Test]
        public void ADoorLetsPlayersThroughAndKeepsMonstersOut()
        {
            var map = Maps.FromRows(
                "#########",
                "#...#...#",
                "#.......#",
                "#...#...#",
                "#########");
            map[4, 2] = TileType.Door;
            Assert.IsFalse(map.IsSolid(4, 2));
            Assert.IsTrue(map.BlocksMonsters(4, 2));
            var world = new World(map, TileMap.Center(2, 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            for (int i = 0; i < 60; i++) world.MovePlayer(player.Id, player.Position + new Vec2(0.15f, 0), new Vec2(1, 0), 1);
            Assert.Greater(player.Position.X, 5.5f, "through the door");

            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), TileMap.Center(2, 2));
            for (int i = 0; i < World.Ticks(6); i++) world.Step();
            Assert.Less(slime.Position.X, 4, "the slime stays on its side");

            var field = new FlowField(map);
            field.Build(new[] { TileMap.Center(6, 2) }, int.MaxValue - 1);
            Assert.AreEqual(FlowField.Unreached, field.DistanceAt(2, 2), "no way round for monsters");
        }

        static World DoorRoom()
        {
            var map = Maps.FromRows(
                "#########",
                "#...#...#",
                "#.......#",
                "#...#...#",
                "#########");
            map[4, 2] = TileType.Door;
            return new World(map, TileMap.Center(2, 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
        }

        static void Walk(World world, Player player, Vec2 step, int steps)
        {
            for (int i = 0; i < steps; i++) world.MovePlayer(player.Id, player.Position + step, step, 1);
        }

        [Test]
        public void ALockedDoorLetsOnlyItsOwnerThrough()
        {
            var world = DoorRoom();
            var owner = world.AddPlayer("a", Gear.Wearing("copper_pickaxe"));
            var other = world.AddPlayer("b", Gear.Wearing("copper_pickaxe"));
            owner.Position = TileMap.Center(3, 2);
            world.MovePlayer(owner.Id, owner.Position, new Vec2(1, 0), 1);

            // Not while someone stands in the doorway.
            other.Position = TileMap.Center(4, 2);
            Assert.IsFalse(world.LockDoor(owner.Id), "someone is in the door");
            other.Position = TileMap.Center(3, 1);
            int version = world.LockVersion;
            Assert.IsTrue(world.LockDoor(owner.Id));
            Assert.AreEqual(TileType.LockedDoor, world.Map[4, 2]);
            Assert.AreEqual("a", world.LockOwner(4, 2));
            Assert.Greater(world.LockVersion, version, "the host sends the locks again");
            Assert.IsTrue(world.Map.BlocksMonsters(4, 2));
            Assert.IsTrue(Housing.RoomAt(world.Map, 2, 2, null).HasDoor, "a locked door is still a room's door");

            // Its owner walks through, on the host and in their own client's prediction alike.
            Walk(world, owner, new Vec2(0.15f, 0), 30);
            Assert.Greater(owner.Position.X, 5.5f, "the owner goes through");
            var predicted = TileMap.Center(3, 2);
            for (int i = 0; i < 30; i++) predicted = PlayerMotor.Step(world.Map, predicted, new Vec2(1, 0), 4, world.DoorsFor(owner));
            Assert.Greater(predicted.X, 5.5f, "the owner's client lets them through too");

            // Anyone else finds a wall: they cannot walk, dig or unlock it.
            other.Position = TileMap.Center(3, 2);
            Walk(world, other, new Vec2(0.15f, 0), 30);
            Assert.Less(other.Position.X, 4f, "someone else stops at it");
            var blocked = TileMap.Center(3, 2);
            for (int i = 0; i < 30; i++) blocked = PlayerMotor.Step(world.Map, blocked, new Vec2(1, 0), 4, world.DoorsFor(other));
            Assert.Less(blocked.X, 4f);
            world.MovePlayer(other.Id, other.Position, new Vec2(1, 0), 1);
            Swing(world, other, 4);
            Assert.AreEqual(TileType.LockedDoor, world.Map[4, 2], "only its owner takes it up");
            Assert.IsFalse(world.UnlockDoor(other.Id));

            // The owner, from the far side, takes the lock off; then it is a door for all.
            owner.Position = TileMap.Center(5, 2);
            world.MovePlayer(owner.Id, owner.Position, new Vec2(-1, 0), 1);
            Assert.IsTrue(world.UnlockDoor(owner.Id));
            Assert.AreEqual(TileType.Door, world.Map[4, 2]);
            Assert.IsNull(world.LockOwner(4, 2));
            Assert.IsEmpty(world.Locks);
            Walk(world, other, new Vec2(0.15f, 0), 30);
            Assert.Greater(other.Position.X, 5.5f);
        }

        [Test]
        public void ALockOutlivesTheSaveAndALockedDoorWithNoOwnerOpens()
        {
            var world = DoorRoom();
            var owner = world.AddPlayer("a", Gear.Wearing("copper_pickaxe"));
            owner.Position = TileMap.Center(3, 2);
            world.MovePlayer(owner.Id, owner.Position, new Vec2(1, 0), 1);
            Assert.IsTrue(world.LockDoor(owner.Id));

            var save = WorldSave.Read(WorldSave.Write(world, 1, 0), Tiles.Shipped, Gear.Data);
            Assert.AreEqual(new[] { (4, 2, "a") }, save.Locks.Select(l => (l.X, l.Y, l.Owner)).ToArray());
            var again = DoorRoom();
            again.Restore(save.Tiles, save.Crops.Select(c => (c.Crop, c.X, c.Y, c.Grown, c.WetLeft)), save.ProgressLevel, save.Chests, save.Beds, save.Locks);
            Assert.AreEqual(TileType.LockedDoor, again.Map[4, 2]);
            Assert.AreEqual("a", again.LockOwner(4, 2));

            // Without its owner a locked door would be a wall to all; it comes back open.
            var lost = DoorRoom();
            lost.Restore(save.Tiles, save.Crops.Select(c => (c.Crop, c.X, c.Y, c.Grown, c.WetLeft)), save.ProgressLevel);
            Assert.AreEqual(TileType.Door, lost.Map[4, 2]);
        }

        [Test]
        public void TheClientHearsWhichLockedDoorsAreItsOwn()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            var player = host.World.Players.Single();
            var (x, y) = World.TileAhead(player.Position, player.Facing);
            host.World.Build(x, y, TileType.Door);
            void Pump()
            {
                for (int i = 0; i < 3; i++)
                {
                    host.Tick();
                    foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
                }
            }
            Pump();
            Assert.AreEqual((x, y), client.DoorAhead());

            host.Receive(1, client.LockDoorCommand());
            Pump();
            Assert.AreEqual(TileType.LockedDoor, client.Map[x, y]);
            Assert.IsTrue(client.IsMyDoor(x, y));
            Assert.AreEqual("앨리스", client.LockOwner(x, y));

            host.Receive(1, client.UnlockDoorCommand());
            Pump();
            Assert.AreEqual(TileType.Door, client.Map[x, y]);
            Assert.IsFalse(client.IsMyDoor(x, y));
        }

        [Test]
        public void CraftingTakesFromChestsNearbyAndMakesSeveralAtOnce()
        {
            var world = Room(out var player, TileType.Workbench);
            var chest = Gear.Data.FindMaterial("chest");
            world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 1);
            player.Inventory.AddMaterial(chest);
            Assert.IsTrue(world.PlaceTile(player.Id, chest));
            var box = world.ChestAt(2, 3);
            box.AddMaterial(Stone, 7);
            player.Inventory.AddMaterial(Stone, 1);
            Assert.AreEqual(7, world.StockNear(player.Position)[Stone]);

            // Four stone floors take two stone: once from the bag and the chest, then three more from the chest.
            var floors = RecipeFor("stone_floor");
            int version = world.ChestVersion(2, 3);
            Assert.AreEqual(4, world.CraftMany(player.Id, floors, 10), "eight stone, four times");
            Assert.AreEqual(0, player.Inventory.CountOf(Stone));
            Assert.AreEqual(0, box.CountOf(Stone));
            Assert.AreEqual(16, player.Inventory.CountOf(Gear.Data.FindMaterial("stone_floor")));
            Assert.Greater(world.ChestVersion(2, 3), version, "the chest's watchers hear of it");
            Assert.IsFalse(world.Craft(player.Id, floors));
        }

        // [상자에 넣기] (Docs/Crafting.md 근처 상자에 넣기, 2026-11-11): a whole stack goes into a chest nearby that holds
        // its kind already, and nothing goes where there is none of it.
        [Test]
        public void StashingPutsMaterialsWhereTheyAlreadyGo()
        {
            var world = Room(out var player, TileType.Workbench);
            var chest = Gear.Data.FindMaterial("chest");
            var copper = Gear.Data.FindMaterial("copper_ore");
            world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 1);
            player.Inventory.AddMaterial(chest);
            Assert.IsTrue(world.PlaceTile(player.Id, chest));
            var box = world.ChestAt(2, 3);
            box.AddMaterial(Stone, 2);
            player.Inventory.AddMaterial(Stone, 5);
            player.Inventory.AddMaterial(copper, 3);
            int version = world.ChestVersion(2, 3);

            Assert.AreEqual(5, world.StashMaterial(player.Id, Stone));
            Assert.AreEqual(7, box.CountOf(Stone));
            Assert.AreEqual(0, player.Inventory.CountOf(Stone));
            Assert.Greater(world.ChestVersion(2, 3), version);
            Assert.AreEqual(0, world.StashMaterial(player.Id, copper), "no chest holds copper ore");
            Assert.AreEqual(3, player.Inventory.CountOf(copper));
            Assert.AreEqual(0, world.StashMaterial(player.Id, Stone), "none left");

            // Out of reach, nothing goes.
            player.Inventory.AddMaterial(Stone, 4);
            player.Position = new Vec2(player.Position.X + World.CraftChestReach + 2, player.Position.Y);
            Assert.AreEqual(0, world.StashMaterial(player.Id, Stone));
        }

        [Test]
        public void TheClientCountsTheChestsNearby()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            var player = host.World.Players.Single();
            var chest = Gear.Data.FindMaterial("chest");
            player.Inventory.AddMaterial(chest);
            Assert.IsTrue(host.World.PlaceTile(player.Id, chest));
            var (cx, cy) = World.TileAhead(player.Position, player.Facing);
            host.World.ChestAt(cx, cy).AddMaterial(Stone, 6);
            // And a workbench on the other side, which floors are made at.
            var workbench = Gear.Data.FindMaterial("workbench");
            player.Inventory.AddMaterial(workbench);
            host.World.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            Assert.IsTrue(host.World.PlaceTile(player.Id, workbench));

            for (int i = 0; i < 10; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            Assert.AreEqual(6, client.StockOf(Stone));
            Assert.AreEqual(6 + client.Bag.CountOf(Stone), client.Available(Stone));
            // Three times over from six stone (and whatever the bag holds).
            Assert.GreaterOrEqual(client.CraftableTimes(RecipeFor("stone_floor")), 3);

            host.Receive(1, client.CraftCommand(RecipeFor("stone_floor"), 3));
            Assert.AreEqual(12, player.Inventory.CountOf(Gear.Data.FindMaterial("stone_floor")));

            // [상자에 넣기] goes over the wire too.
            host.World.ChestAt(cx, cy).AddMaterial(Stone, 1);
            player.Inventory.AddMaterial(Stone, 4);
            int stone = player.Inventory.CountOf(Stone);
            host.Receive(1, client.StashCommand(Stone));
            Assert.AreEqual(0, player.Inventory.CountOf(Stone));
            Assert.GreaterOrEqual(host.World.ChestAt(cx, cy).CountOf(Stone), stone);
            Assert.IsEmpty(client.NeededMaterials(), "no goals asking for anything");
        }
}
}
