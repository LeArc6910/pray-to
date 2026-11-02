using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class LavaZoneTests
    {
        static readonly BossDefinition Titan = Bosses.Shipped.Bosses.Single(b => b.Id == "lava_titan");

        static IEnumerable<(int X, int Y)> TilesOf(TileMap map, TileType type) =>
            Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y))).Where(t => map[t.Item1, t.Item2] == type);

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(7UL)]
        public void TheLavaArenaLiesInTheLavaFieldsAndCanBeReached(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            var lava = world.Arenas[2];
            Assert.AreEqual(Biome.Lava, world.Biomes.At(lava));
            Assert.Greater(Vec2.Distance(lava, world.Spawn), 60);
            Assert.Greater(Vec2.Distance(lava, world.Arenas[1]), 120, "the opposite corner from the deep arena");
            Assert.IsFalse(Collision.Overlaps(world.Map, lava, world.ArenaRadius - 1), "its floor is clear");
            var field = new FlowField(world.Map);
            field.Build(new[] { world.Spawn }, int.MaxValue - 1);
            Assert.AreNotEqual(FlowField.Unreached, field.DistanceAt(TileMap.Cell(lava.X), TileMap.Cell(lava.Y)));
        }

        [TestCase(1UL)]
        [TestCase(7UL)]
        public void ObsidianAndLavaPoolsAreOnlyInTheLavaFields(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            var obsidian = TilesOf(world.Map, TileType.ObsidianOre).ToList();
            var pools = TilesOf(world.Map, TileType.Lava).ToList();
            Assert.Greater(obsidian.Count, 20);
            Assert.Greater(pools.Count, 30);
            Assert.IsTrue(obsidian.Concat(pools).All(t => world.Biomes[t.X, t.Y] == Biome.Lava));
            Assert.IsTrue(pools.All(t => world.Arenas.All(a => Vec2.Distance(TileMap.Center(t.X, t.Y), a) > world.ArenaRadius + 1)), "none in an arena");
            Assert.IsFalse(world.Map.IsSolid(pools[0].X, pools[0].Y), "lava is walked over");
        }

        [Test]
        public void ObsidianNeedsAMithrilPickaxe()
        {
            World Make(string pickaxe, out Player player)
            {
                var map = Maps.FromRows(
                    "#######",
                    "#.....#",
                    "#..#..#",
                    "#.....#",
                    "#######");
                map[3, 2] = TileType.ObsidianOre;
                var world = new World(map, TileMap.Center(2, 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
                player = world.AddPlayer("a", Gear.Wearing(pickaxe));
                world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
                return world;
            }

            var weak = Make("iron_pickaxe", out var miner);
            weak.SetAttacking(miner.Id, true);
            weak.Step();
            var refused = weak.TakeEvents().Single(e => e.Type == SimEventType.Dug);
            Assert.AreEqual(World.NeedsBetterPickaxe, refused.Amount);

            var strong = Make("mithril_pickaxe", out miner);
            strong.SetAttacking(miner.Id, true);
            for (int i = 0; i < World.TicksPerSecond * 20 && strong.Map[3, 2] == TileType.ObsidianOre; i++) strong.Step();
            Assert.AreEqual(TileType.Floor, strong.Map[3, 2]);
            Assert.AreEqual(1, miner.Inventory.CountOf(Gear.Data.FindMaterial("obsidian_ore")));
        }

        [Test]
        public void LavaBurnsWhoeverStandsInIt()
        {
            var map = Maps.Room(10);
            map[5, 5] = TileType.Lava;
            var world = new World(map, TileMap.Center(5, 5), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            float health = player.Health;
            for (int i = 0; i < World.TicksPerSecond * 2; i++) world.Step();
            Assert.Less(player.Health, health - 10, "about 8 a second");
            Assert.IsTrue(player.Afflictions.Burning);

            world.MovePlayer(player.Id, player.Position + new Vec2(1, 0), new Vec2(1, 0), 10);
            for (int i = 0; i < World.TicksPerSecond * 2; i++) world.Step();
            Assert.IsFalse(player.Afflictions.Burning, "out of it, the burn dies down");
        }

        [Test]
        public void TheTitanBurnsAndOpensTheObsidianStep()
        {
            Assert.AreEqual(SkillTag.Fire, Titan.Element);
            Assert.AreEqual(30, Titan.DropLevel, "obsidian gear is level 30");
            Assert.AreEqual("titan_heart", Titan.MaterialId);
            Assert.IsTrue(Titan.Attacks.Any(a => a.Summon?.Id == "magma_slime"));
            var sword = Gear.Data.Recipes.Single(r => r.Item?.Id == "obsidian_sword");
            Assert.IsTrue(sword.Ingredients.Any(i => i.Material.Id == "titan_heart"));

            var world = new World(Maps.Room(40), TileMap.Center(20, 14), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var boss = world.PlaceBoss(Titan, TileMap.Center(20, 20), 10);
            var player = world.AddPlayer("a", Gear.Wearing("mithril_armor"));
            for (int i = 0; i < World.TicksPerSecond * 20 && !player.Afflictions.Burning && !player.IsDead; i++) world.Step();
            Assert.IsTrue(player.Afflictions.Burning || player.IsDead, "its blows set the player alight");
        }
            [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(7UL)]
        public void TheAbyssLiesBeyondTheLavaArenaWithVoidOre(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            var abyss = world.Arenas[3];
            Assert.AreEqual(Biome.Abyss, world.Biomes.At(abyss));
            var field = new FlowField(world.Map);
            field.Build(new[] { world.Arenas[2] }, int.MaxValue - 1);
            Assert.AreNotEqual(FlowField.Unreached, field.DistanceAt(TileMap.Cell(abyss.X), TileMap.Cell(abyss.Y)), "a tunnel runs from the lava arena");
            var veins = TilesOf(world.Map, TileType.VoidOre).ToList();
            Assert.Greater(veins.Count, 5);
            Assert.IsTrue(veins.All(v => world.Biomes[v.X, v.Y] == Biome.Abyss), "void ore only in the abyss");
            Assert.IsTrue(veins.All(v => Vec2.Distance(TileMap.Center(v.X, v.Y), abyss) > WorldGenerator.ArenaRadius), "none in the arena");
        }

        [Test]
        public void TheVoidLordEndsTheClimb()
        {
            var lord = Bosses.Shipped.Bosses.Single(b => b.Id == "void_lord");
            Assert.AreSame(lord, Bosses.Shipped.Bosses[3], "fourth, for the fourth arena");
            Assert.AreEqual(40, lord.DropLevel);
            Assert.Greater(lord.Health, Titan.Health);
            Assert.AreEqual(SkillTag.Cold, lord.Element);
            Assert.AreEqual(30, Tiles.Shipped.Find(TileType.VoidOre).Pickaxe, "an obsidian pickaxe or better");
            Assert.AreEqual(30, Gear.Data.FindBase("obsidian_pickaxe").Level);
            Assert.IsTrue(Gear.Data.Recipes.Where(r => r.Item?.Level == 40).All(r => r.Ingredients.Any(i => i.Material.Id == "void_essence")),
                "void gear takes the lord's essence");
            Assert.AreEqual(6, Gear.Data.Recipes.Count(r => r.Item?.Level == 40));
            Assert.AreEqual(2, new LootGenerator(Gear.Data).LegendariesOf("void_lord").Count());
            foreach (string id in new[] { "void_wisp", "abyss_stalker" }) Assert.AreEqual(Biome.Abyss, Monsters.Shipped.Find(id).Biome, id);
            Assert.AreEqual("공허의 심연", BiomeMap.Name(Biome.Abyss));
        }
}
}
