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
            player = world.AddPlayer("a", gear ?? Gear.Wearing("copper_sword"));
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
            Assert.IsTrue(Gear.Data.Recipes.Where(r => r.Item?.Level == 10).All(r => r.Ingredients.Any(i => i.Material.Id == "warden_core")),
                "iron gear takes the boss's core");
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
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, digs.Select(e => e.Amount));
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

            Swing(world, player, 4);
            Assert.AreEqual(TileType.CopperOre, world.Map[3, 2]);
            Swing(world, player, 1);

            Assert.AreEqual(TileType.Floor, world.Map[3, 2]);
            Assert.AreEqual(1, player.Inventory.CountOf(CopperOre));
        }

        [Test]
        public void MiningSpeedDigsFaster()
        {
            var pick = new Equipment();
            pick.Equip(Gear.Plain("copper_sword", 0, Rarity.Uncommon, Gear.Affix("miner", 50)));
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
        public void BowsDigWhenFacingAWall()
        {
            var world = Room(out var player, gear: Gear.Wearing("copper_bow"));

            var events = Swing(world, player, 1);

            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Dug));
            Assert.AreEqual((int)BasicAttackKind.Swing, events.First(e => e.Type == SimEventType.Attack).Amount);
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
            var world = Room(out var player, TileType.Floor, monsters: gelSlimes);
            world.SpawnEnemy(gelSlimes.Monsters[0], player.Position + new Vec2(0.9f, 0));
            Swing(world, player, 1);
            Assert.AreEqual(1, world.GroundLoot.Count(l => l.Material == SlimeGel) + player.Inventory.CountOf(SlimeGel));

            var bosses = Bosses.Parse(
                Bosses.BossesHeader.Replace("roar_damage\n", "roar_damage,material,material_count\n") + "dummy,허수아비,8,0,0,2,1,,1,3,10,warden_core,1\n",
                Bosses.AttacksHeader + "dummy,slam,내려찍기,,circle,nearest,1,5,1,,,,1,,,1,1,\n");
            var arena = Room(out var fighter, TileType.Floor);
            arena.PlaceBoss(bosses.Find("dummy"), new Vec2(3.6f, 2.5f), 3);
            Swing(arena, fighter, 3);
            Assert.IsEmpty(arena.Bosses.Where(b => !b.IsDead));
            var core = Gear.Data.FindMaterial("warden_core");
            Assert.AreEqual(1, arena.GroundLoot.Count(l => l.Material == core) + fighter.Inventory.CountOf(core));
        }
    }
}
