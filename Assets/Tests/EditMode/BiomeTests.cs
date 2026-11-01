using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class BiomeTests
    {
        static Dictionary<Biome, int> Count(BiomeMap biomes)
        {
            var counts = new Dictionary<Biome, int>();
            for (int y = 0; y < biomes.Height; y++)
            {
                for (int x = 0; x < biomes.Width; x++) counts[biomes[x, y]] = counts.GetValueOrDefault(biomes[x, y]) + 1;
            }
            return counts;
        }

        [TestCase(1UL)]
        [TestCase(7UL)]
        [TestCase(42UL)]
        public void EachCornerOfTheMapIsItsOwnBiomeAroundPlainCave(ulong seed)
        {
            var world = WorldGenerator.Generate(seed);
            var biomes = world.Biomes;
            Assert.AreEqual(Biome.Cave, biomes[world.SpawnX, world.SpawnY]);
            Assert.AreEqual(Biome.Crystal, biomes.At(world.Arenas[1]), "the deep arena is in the crystal caverns");

            var deep = world.Arenas[1] - world.Spawn;
            var opposite = world.Spawn - deep;
            Assert.AreEqual(Biome.Lava, biomes.At(opposite), "the lava fields lie opposite");
            var sides = new[] { world.Spawn + new Vec2(deep.X, -deep.Y), world.Spawn + new Vec2(-deep.X, deep.Y) }.Select(biomes.At).ToList();
            CollectionAssert.AreEquivalent(new[] { Biome.Mushroom, Biome.Ice }, sides);

            var counts = Count(biomes);
            foreach (Biome biome in System.Enum.GetValues(typeof(Biome))) Assert.Greater(counts.GetValueOrDefault(biome), 1000, biome.ToString());
        }

        [Test]
        public void TheSameSeedMakesTheSameBiomesAndTheBorderWinds()
        {
            var a = WorldGenerator.Generate(5).Biomes;
            var b = WorldGenerator.Generate(5).Biomes;
            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++) Assert.AreEqual(a[x, y], b[x, y]);
            }
            // Along a straight line out from the spawn the plain cave ends at different distances.
            var world = WorldGenerator.Generate(5);
            var ends = new HashSet<int>();
            for (int angle = 0; angle < 360; angle += 15)
            {
                var direction = new Vec2(System.MathF.Cos(angle * System.MathF.PI / 180), System.MathF.Sin(angle * System.MathF.PI / 180));
                int distance = Enumerable.Range(1, 60).First(d => a.At(world.Spawn + direction * d) != Biome.Cave);
                Assert.That(distance, Is.InRange(BiomeMap.CaveRadius - 13, BiomeMap.CaveRadius + 13));
                ends.Add(distance);
            }
            Assert.Greater(ends.Count, 4, "the border is not a circle");
        }

        [Test]
        public void OnlyTheLavaFieldsChangedTheCaves()
        {
            // Biomes only say what kind of cave a tile is in. The lava fields carved their arena, a tunnel from the
            // spawn and pools of lava, all in the plain cave and the lava fields; the other three biomes' caves are
            // what they were before.
            var world = WorldGenerator.Generate(7);
            int floors = 0;
            for (int y = 0; y < world.Map.Height; y++)
            {
                for (int x = 0; x < world.Map.Width; x++)
                {
                    var biome = world.Biomes[x, y];
                    bool elsewhere = biome == Biome.Mushroom || biome == Biome.Ice || biome == Biome.Crystal;
                    if (elsewhere && world.Map[x, y] == TileType.Floor) floors++;
                }
            }
            Assert.AreEqual(FloorsElsewhereOfSeedSeven, floors);
        }

        // Floor tiles of seed 7 in the mushroom forest, ice caves and crystal caverns before the lava fields, counted
        // with the code of that time, so a change to those caves shows up here.
        const int FloorsElsewhereOfSeedSeven = 6073;

        [Test]
        public void BiomeMonstersOnlyLiveInTheirBiome()
        {
            var biomes = BiomeMap.Make(3, 130, 130, 65, 65, (1, 1), true);
            var world = new World(Maps.Room(130), TileMap.Center(65, 65), Monsters.Shipped, 3, new WorldConfig { EnemyTarget = 600, SpawnDistance = 5 },
                biomes: biomes);

            var placed = world.Enemies.Where(e => e.Definition.Biome != null).ToList();
            Assert.IsNotEmpty(placed);
            Assert.IsTrue(placed.All(e => biomes.At(e.Position) == e.Definition.Biome), "each in its own biome");
            foreach (string id in new[] { "myconid", "glow_bat", "ice_slime", "frost_wisp", "crystal_bat", "crystal_wisp", "magma_slime", "fire_bat" })
            {
                Assert.IsTrue(world.Enemies.Any(e => e.Definition.Id == id), id);
            }
            Assert.IsTrue(world.Enemies.Any(e => e.Definition.Biome == null && biomes.At(e.Position) != Biome.Cave), "everyday monsters live everywhere");
        }

        [Test]
        public void TheBiomeColumnIsChecked()
        {
            var data = MonsterSheet.Parse(Monsters.Header.Replace("weight\n", "weight,biome\n") + "a,가,1,1,0,0,1,0.3,0,0.5,0.5,1,1,swamp\n");
            Assert.IsTrue(data.Errors.Any(e => e.Contains("swamp")), string.Join("\n", data.Errors));
            Assert.AreEqual(Biome.Crystal, Monsters.Shipped.Find("crystal_bat").Biome);
            Assert.IsNull(Monsters.Shipped.Find("slime").Biome);
        }

        [Test]
        public void LampsLightTheCaveFromTheMushroomForest()
        {
            var lamp = Gear.Data.FindMaterial("glow_lamp");
            Assert.AreEqual(TileType.Lamp, lamp.Places);
            Assert.Greater(Tiles.Shipped.Find(TileType.Lamp).Light, Tiles.Shipped.Find(TileType.Torch).Light);
            var recipe = Gear.Data.Recipes.Single(r => r.Id == "glow_lamp");
            Assert.IsTrue(recipe.Ingredients.Any(i => i.Material.Id == "glow_spore"));
        }
    }
}
